using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// VMCProtocol受信機を複数使ったときの挙動。
    ///
    /// 「トラッカーは1台目から、表情は2台目から」のような使い方ができる。
    /// 受信機ごとにVirtualAvatarが作られるので、適用範囲の分離と
    /// 片方を無効にしたときの独立性が壊れやすい。
    /// </summary>
    public sealed class Scenario_MultipleReceivers : VMCTestScenario
    {
        public override string Name => "MultipleReceivers";

        public override string Description => "複数のVMCProtocol受信機の分離と独立性";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));
            context.FaceController.EnableBlink = false;

            //--- 2. 受信機を2つ作る ---
            //1台目: トラッカーのみ / 2台目: 表情と視線のみ
            context.Log("2. 受信機を2つ作成");
            var trackerReceiver = context.CreateReceiver(setting =>
            {
                setting.Name = "TrackerOnly";
                setting.ApplyTracker = true;
                setting.ApplyBlendShape = false;
                setting.ApplyLookAt = false;
            });
            var faceReceiver = context.CreateReceiver(setting =>
            {
                setting.Name = "FaceOnly";
                setting.ApplyTracker = false;
                setting.ApplyBlendShape = true;
                setting.ApplyLookAt = true;
            });

            result.CheckThat("受信機の作成",
                context.Window.externalMotionReceivers.Count >= 2,
                $"受信機が2つ作られていません({context.Window.externalMotionReceivers.Count}個)");

            //--- 3. それぞれの担当だけが効くか ---
            context.Log("3. 担当範囲の分離");
            context.InjectTrackerRig(trackerReceiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            context.InjectTrackerRig(trackerReceiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            var ipose = context.Capture("01_two_receivers_ipose", includeSent: false);
            result.CheckSnapshot(context, ipose);

            //トラッカー担当でない方にトラッカーを送っても動かないこと
            context.InjectTrackerRig(faceReceiver, VMCTestTrackerRig.TPose);
            yield return context.Step(20);
            var afterWrongReceiver = context.Capture("tmp", includeSent: false);
            var wrongDelta = VMCTestSnapshot.MaxBoneRotationDelta(ipose, afterWrongReceiver, out _);
            result.CheckThat("トラッカー無効の受信機",
                wrongDelta < 5f,
                $"ApplyTracker=falseの受信機にトラッカーを送ったのにアバターが動きました(最大回転差 {wrongDelta:F2}度)");

            //トラッカー担当に送れば動くこと
            context.InjectTrackerRig(trackerReceiver, VMCTestTrackerRig.TPose);
            yield return context.Step(20);
            var tpose = context.Capture("02_two_receivers_tpose", includeSent: false);
            result.CheckSnapshot(context, tpose);

            var rightDelta = VMCTestSnapshot.MaxBoneRotationDelta(ipose, tpose, out var movedBone);
            result.CheckThat("トラッカー担当の受信機",
                rightDelta > 15f,
                $"トラッカー担当の受信機に送ってもアバターが動きません(最大回転差 {rightDelta:F2}度 / {movedBone ?? "なし"})");

            //--- 4. 表情は表情担当だけが効く ---
            context.Log("4. 表情の分離");
            context.Inject(trackerReceiver, VMCTestOscBuilder.BlendShapes(new[]
            {
                new KeyValuePair<string, float>("Angry", 0.9f),
            }));
            yield return context.Step(5);
            var afterWrongFace = context.Capture("tmp", includeSent: false);
            result.CheckThat("表情無効の受信機",
                afterWrongFace.GetExpression("Angry") < 0.01f,
                $"ApplyBlendShape=falseの受信機で表情が適用されました(Angry={afterWrongFace.GetExpression("Angry"):F3})");

            context.Inject(faceReceiver, VMCTestOscBuilder.BlendShapes(new[]
            {
                new KeyValuePair<string, float>("Joy", 0.7f),
            }));
            context.Inject(faceReceiver, VMCTestOscBuilder.Eye(true, new Vector3(0.3f, 0.05f, 1.0f)));
            yield return context.Step(5);

            var withFace = context.Capture("03_two_receivers_face", includeSent: false);
            result.CheckSnapshot(context, withFace);
            result.CheckThat("表情担当の受信機",
                Mathf.Abs(withFace.GetExpression("Joy") - 0.7f) < 0.01f
                && withFace.HasLookAt && Mathf.Abs(withFace.LookAtYaw) > 5f,
                $"表情担当の受信機で表情/視線が適用されていません(Joy={withFace.GetExpression("Joy"):F3} yaw={withFace.LookAtYaw:F2})");

            //--- 5. 片方を無効にしても、もう片方は動き続ける ---
            context.Log("5. 片方の無効化");
            context.SetReceiverActive(faceReceiver, false);
            yield return context.Step(5);

            context.InjectTrackerRig(trackerReceiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            var afterDisable = context.Capture("04_face_receiver_disabled", includeSent: false);
            result.CheckSnapshot(context, afterDisable);

            var recovered = VMCTestSnapshot.MaxBoneRotationDelta(tpose, afterDisable, out _);
            result.CheckThat("片方無効時の独立性",
                recovered > 15f,
                $"表情用受信機を無効にしたら、トラッカー用受信機まで止まりました(最大回転差 {recovered:F2}度)");

            context.SetReceiverActive(faceReceiver, true);
        }
    }
}
