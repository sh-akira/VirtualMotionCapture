using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// 縦串の基本シナリオ。
    ///
    ///   VRM読み込み
    ///     → VMCProtocolでトラッカー姿勢を受信(実機VRの代替)
    ///     → キャリブレーション
    ///     → トラッカーを動かしてアバターが追従することを確認
    ///     → VMCProtocolで表情とLookAtを受信
    ///     → その状態がVMCProtocolとして送信されることを確認
    ///
    /// 各段階でスナップショットを取ってゴールデンと比較するのに加えて、
    /// 「そもそも動いているか」をゴールデンに依存しない不変条件として検査する。
    /// (壊れた状態のままゴールデンが作られると回帰テストが無意味になるため)
    /// </summary>
    public sealed class Scenario_BasicVMCProtocol : VMCTestScenario
    {
        //受信した表情の期待値
        private const float ExpectedJoy = 0.7f;
        private const float ExpectedA = 0.3f;

        public override string Name => "BasicVMCProtocol";

        public override string Description
            => "VRM読込→トラッカー受信→キャリブレーション→追従→表情/LookAt受信→送信";

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            var vrmPath = context.Config.GetModelPath(context.ModelKey);

            //--- 1. モデル読み込み ---
            context.Log($"1. VRM読み込み: {vrmPath}");
            context.ResetSettings();
            yield return context.LoadModel(vrmPath);

            //--- 2. VMCProtocol受信機を用意してトラッカーを流し込む ---
            context.Log("2. VMCProtocol受信機の作成とトラッカー注入");
            var receiver = context.CreateReceiver(setting =>
            {
                setting.ApplyTracker = true;
                setting.ApplyBlendShape = true;
                setting.ApplyLookAt = true;
                //ボーンは受信しない(このシナリオはトラッカー駆動のVRIKを見る)
                setting.ApplyRootPosition = false;
                setting.ApplyRootRotation = false;
                setting.ApplySpine = false;
                setting.ApplyChest = false;
                setting.ApplyHead = false;
                setting.ApplyLeftArm = false;
                setting.ApplyRightArm = false;
                setting.ApplyLeftHand = false;
                setting.ApplyRightHand = false;
                setting.ApplyLeftLeg = false;
                setting.ApplyRightLeg = false;
                setting.ApplyLeftFoot = false;
                setting.ApplyRightFoot = false;
                setting.ApplyLeftFinger = false;
                setting.ApplyRightFinger = false;
                setting.ApplyEye = false;
            });

            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();

            var rigError = context.GetTrackerRigError(VMCTestTrackerRig.IPose);
            result.CheckThat("トラッカーの受信",
                rigError >= 0f && rigError < 0.005f,
                rigError < 0f
                    ? "注入したトラッカーがTrackingPointManagerに登録されていません"
                    : $"注入した姿勢が反映されていません(最大位置誤差 {rigError:F4}m)");

            //--- 3. キャリブレーション ---
            context.Log("3. キャリブレーション(Iポーズ)");
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            //キャリブレーション中はトラッカー入力が止まるので、もう一度姿勢を送って落ち着かせる
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            var iposeSnapshot = context.Capture("01_calibrated_ipose", includeSent: false);
            result.CheckSnapshot(context, iposeSnapshot);

            //--- 4. トラッカーを動かしてアバターが追従することを確認 ---
            context.Log("4. トラッカーを動かして追従を確認");
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            yield return context.Step(20);

            var tposeError = context.GetTrackerRigError(VMCTestTrackerRig.TPose);
            result.CheckThat("トラッカーの移動",
                tposeError >= 0f && tposeError < 0.005f,
                $"トラッカーを動かしたのに姿勢が更新されていません(最大位置誤差 {tposeError:F4}m)");

            var tposeSnapshot = context.Capture("02_tracking_tpose", includeSent: false);
            result.CheckSnapshot(context, tposeSnapshot);

            //腕を横に上げたので、腕のボーンが大きく回転しているはず。
            //ここが0度のままなら、キャリブレーションかVRIKかトラッカー入力のどこかが死んでいる。
            var maxDelta = VMCTestSnapshot.MaxBoneRotationDelta(iposeSnapshot, tposeSnapshot, out var movedBone);
            result.CheckThat("アバターの追従",
                maxDelta > 15f,
                $"トラッカーを動かしてもアバターが動いていません(最大回転差 {maxDelta:F2}度 / 最大は {movedBone ?? "なし"})");

            //--- 5. 表情とLookAtを受信 ---
            context.Log("5. 表情とLookAtの受信");
            //VMCProtocolの仕様上、表情はVRM1.0モデルでもVRM0.xの名称で送られてくる
            context.Inject(receiver, VMCTestOscBuilder.BlendShapes(new[]
            {
                new KeyValuePair<string, float>("Joy", ExpectedJoy),
                new KeyValuePair<string, float>("A", ExpectedA),
            }));
            //頭ボーンから見て右斜め前を見る
            context.Inject(receiver, VMCTestOscBuilder.Eye(true, new Vector3(0.3f, 0.05f, 1.0f)));
            yield return context.Step(5);

            var faceSnapshot = context.Capture("03_expression_lookat", includeSent: false);
            result.CheckSnapshot(context, faceSnapshot);

            var joy = faceSnapshot.GetExpression("Joy");
            var aa = faceSnapshot.GetExpression("A");
            result.CheckThat("表情の受信",
                Mathf.Abs(joy - ExpectedJoy) < 0.01f && Mathf.Abs(aa - ExpectedA) < 0.01f,
                $"受信した表情が反映されていません(Joy {joy:F3} 期待{ExpectedJoy} / A {aa:F3} 期待{ExpectedA})");

            result.CheckThat("LookAtの受信",
                faceSnapshot.HasLookAt && Mathf.Abs(faceSnapshot.LookAtYaw) > 5f,
                $"受信した視線が反映されていません(has={faceSnapshot.HasLookAt} yaw={faceSnapshot.LookAtYaw:F2} pitch={faceSnapshot.LookAtPitch:F2})");

            //--- 6. この状態がVMCProtocolとして送信されることを確認 ---
            context.Log("6. VMCProtocol送信のキャプチャ");
            context.EnableSender();
            yield return context.Step(3);
            //送信開始直後のフレームを避けてからキャプチャする
            context.ClearSent();
            yield return context.Step(4);

            var sentSnapshot = context.Capture("04_sent", includeSent: true);
            result.CheckSnapshot(context, sentSnapshot);

            //受信して合成した結果が、そのままVMCProtocolとして出ていること
            var sentBoneDifferences = sentSnapshot.VerifySentBonesMatchState(
                context.Config.PositionTolerance, context.Config.RotationToleranceDegrees);
            result.CheckThat("送信ボーンと状態の一致",
                sentBoneDifferences.Count == 0,
                $"送信されたボーン姿勢が実際のアバターと食い違っています: {string.Join(" / ", sentBoneDifferences.GetRange(0, Mathf.Min(5, sentBoneDifferences.Count)))}");

            var sentJoy = FindSentBlendShape(sentSnapshot, "Joy");
            result.CheckThat("表情の送信",
                sentJoy.HasValue && Mathf.Abs(sentJoy.Value - ExpectedJoy) < 0.01f,
                sentJoy.HasValue
                    ? $"送信された Joy が {sentJoy.Value:F3} で期待値 {ExpectedJoy} と違います"
                    : "/VMC/Ext/Blend/Val に Joy が含まれていません(VRM1.0でもVRM0.x名で送る必要がある)");

            context.DisableSender();
        }

        private static float? FindSentBlendShape(VMCTestSnapshot snapshot, string name)
        {
            foreach (var message in snapshot.Sent)
            {
                if (message.Address != "/VMC/Ext/Blend/Val") continue;
                if (message.Args.Count != 2 || message.Args[0].T != "s") continue;
                if (message.Args[0].S != name) continue;
                return message.Args[1].F;
            }
            return null;
        }
    }
}
