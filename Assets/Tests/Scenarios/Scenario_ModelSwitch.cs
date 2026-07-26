using System.Collections;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// 別のアバターを読み込んだとき。
    ///
    ///   モデルAを読んでキャリブレーション
    ///     → モデルB(VRM0.x⇔VRM1.0の他方)に差し替え
    ///     → 記録済みのトラッカー姿勢で自動再キャリブレーションが走る
    ///     → Tポーズを取り直さずにトラッカーへ追従する
    ///     → 表情とLookAtも新しいモデルに引き継がれる
    ///
    /// ModelKey が読み込み元、もう一方が切り替え先になる。
    /// VRM0.x→VRM1.0 と VRM1.0→VRM0.x の両方向を検証する。
    /// </summary>
    public sealed class Scenario_ModelSwitch : VMCTestScenario
    {
        public override string Name => "ModelSwitch";

        public override string Description => "別アバターに差し替えたときの自動再キャリブレーションと追従";

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            var fromKey = context.ModelKey;
            var toKey = fromKey == VMCTestModels.Vrm0 ? VMCTestModels.Vrm10 : VMCTestModels.Vrm0;
            var toPath = context.Config.GetModelPath(toKey);

            if (toPath == null)
            {
                //切り替え先が無いと検証できない。失敗ではなくスキップ扱いにする
                result.Skipped = true;
                result.SkipReason = $"切り替え先の {toKey} のVRMが設定されていません";
                yield break;
            }

            //--- 1. モデルAを読み込んでキャリブレーション ---
            context.Log($"1. モデルA({fromKey})の読み込みとキャリブレーション");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(fromKey));

            var receiver = context.CreateReceiver(setting =>
            {
                setting.ApplyTracker = true;
                setting.ApplyBlendShape = true;
                setting.ApplyLookAt = true;
            });

            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            //表情と視線も入れておく(モデル差し替えで引き継がれるかを見る)
            context.Inject(receiver, VMCTestOscBuilder.BlendShapes(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, float>("Joy", 0.6f),
            }));
            context.Inject(receiver, VMCTestOscBuilder.Eye(true, new Vector3(0.3f, 0.05f, 1.0f)));
            yield return context.Step(5);

            var beforeSwitch = context.Capture("01_before_switch", includeSent: false);
            result.CheckSnapshot(context, beforeSwitch);

            result.CheckThat("切り替え前のキャリブレーション記録",
                Settings.Current.LastCalibrationSnapshot != null &&
                Settings.Current.LastCalibrationSnapshot.Poses.Count >= 6,
                "キャリブレーション時のトラッカー姿勢が記録されていません。自動再キャリブレーションが動作しません");

            //--- 2. 別のアバターに差し替える ---
            context.Log($"2. モデルB({toKey})への差し替え");
            //自動再キャリブレーションを有効にする(これが本シナリオの検証対象)
            Settings.Current.EnableAutoCalibrationOnModelLoad = true;

            var previousModel = context.CurrentModel;
            yield return context.SwitchModel(toPath);

            result.CheckThat("モデルの差し替え",
                context.CurrentModel != null && context.CurrentModel != previousModel,
                "モデルが差し替わっていません");

            //--- 3. 自動再キャリブレーションの完了を待つ ---
            context.Log("3. 自動再キャリブレーションの待機");
            //トラッカーは流し続ける(実運用と同じ状況にする)
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitUntil(
                () => IKManager.Instance.CalibrationState == CalibrationState.Calibrated,
                900, "自動再キャリブレーションの完了");
            yield return context.Step(20);

            result.CheckThat("自動再キャリブレーション",
                IKManager.Instance.CalibrationState == CalibrationState.Calibrated,
                $"別アバター読み込み後にキャリブレーションが完了していません(state={IKManager.Instance.CalibrationState})。" +
                "Tポーズを取り直す必要が出てしまいます");

            var afterSwitch = context.Capture("02_after_switch_ipose", includeSent: false);
            result.CheckSnapshot(context, afterSwitch);

            //--- 4. 差し替え後もトラッカーに追従するか ---
            //実際のVMCProtocol送信側は毎フレーム送り続けるので、表情・視線も送り直した状態にする。
            //(表情はFaceControllerが値を保持するので送り直さなくても残るが、
            // 視線のターゲットは旧モデルの頭ボーン配下にあり破棄されているため、再送で復帰する)
            context.Log("4. 差し替え後の追従確認");
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            context.Inject(receiver, VMCTestOscBuilder.BlendShapes(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, float>("Joy", 0.6f),
            }));
            context.Inject(receiver, VMCTestOscBuilder.Eye(true, new Vector3(0.3f, 0.05f, 1.0f)));
            yield return context.Step(20);

            var afterSwitchTpose = context.Capture("03_after_switch_tpose", includeSent: false);
            result.CheckSnapshot(context, afterSwitchTpose);

            var delta = VMCTestSnapshot.MaxBoneRotationDelta(afterSwitch, afterSwitchTpose, out var movedBone);
            result.CheckThat("差し替え後の追従",
                delta > 15f,
                $"別アバターに差し替えた後、トラッカーに追従していません(最大回転差 {delta:F2}度 / {movedBone ?? "なし"})");

            //--- 5. 表情とLookAtが新しいモデルにも適用されるか ---
            context.Log("5. 表情とLookAtの引き継ぎ確認");
            var joy = afterSwitchTpose.GetExpression("Joy");
            result.CheckThat("表情の引き継ぎ",
                Mathf.Abs(joy - 0.6f) < 0.01f,
                $"モデル差し替え後に表情が引き継がれていません(Joy {joy:F3} 期待 0.600)");

            result.CheckThat("LookAtの引き継ぎ",
                afterSwitchTpose.HasLookAt && Mathf.Abs(afterSwitchTpose.LookAtYaw) > 5f,
                $"モデル差し替え後に視線が引き継がれていません(has={afterSwitchTpose.HasLookAt} yaw={afterSwitchTpose.LookAtYaw:F2})");
        }
    }
}
