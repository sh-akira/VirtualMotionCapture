using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// BVH書き出しの往復。
    ///
    /// BVHはUniVRMに頼らず BvhWriter で完全に自前実装しており、
    /// チャンネル順(Yrotation Xrotation Zrotation) / X反転 / cm単位 /
    /// 「レストからのワールド差分回転」という独自の約束事が多い。
    /// 書き出したBVHを読み直して、見た目の姿勢が保たれるかを確認する。
    /// </summary>
    public sealed class Scenario_BvhExport : VMCTestScenario
    {
        public override string Name => "BvhExport";

        public override string Description => "BVH書き出しと読み込みの往復";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込みとキャリブレーション");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var receiver = context.CreateReceiver(setting => setting.ApplyTracker = true);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            //Tポーズにして、特徴のある姿勢を記録する
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            yield return context.Step(20);

            //--- 2. 記録 ---
            context.Log("2. モーションの記録");
            Settings.Current.MotionRecord_Fps = 30;
            Settings.Current.MotionRecord_CountdownSeconds = 0;
            Settings.Current.MotionRecord_SaveMotion = true;

            var recorder = context.MotionRecorder;
            recorder.StartRecording();
            yield return context.Step(40);
            var recorded = context.Capture("01_recorded", includeSent: false);
            var lastFrame = recorder.Test_RecordedFrameCount - 1;
            recorder.StopRecording();
            yield return context.Step(2);

            result.CheckThat("モーションの記録",
                recorder.Test_State == MotionRecorder.RecordState.Recorded && lastFrame > 0,
                $"記録できていません(state={recorder.Test_State} frames={recorder.Test_RecordedFrameCount})");
            if (recorder.Test_State != MotionRecorder.RecordState.Recorded) yield break;

            //--- 3. BVHで書き出す ---
            context.Log("3. BVHの書き出し");
            var bvhPath = context.OutputPath($"{Name}.{context.ModelKey}.bvh");
            recorder.Test_SaveRecording(bvhPath, 1, 0, lastFrame); //format 1 = BVH
            yield return context.Step(2);

            var fileInfo = new FileInfo(bvhPath);
            result.CheckThat("BVHの書き出し",
                fileInfo.Exists && fileInfo.Length > 1024,
                $"BVHが書き出されていません({bvhPath})");
            if (fileInfo.Exists == false) yield break;

            //中身の体裁も見ておく(壊れたBVHは読めても無音で崩れる)
            var text = File.ReadAllText(bvhPath);
            result.CheckThat("BVHの体裁",
                text.StartsWith("HIERARCHY") && text.Contains("MOTION")
                && text.Contains("Frames:") && text.Contains("Frame Time:")
                && text.Contains("Yrotation Xrotation Zrotation"),
                "BVHの必須セクション(HIERARCHY/MOTION/Frames/Frame Time/回転チャンネル順)が揃っていません");

            //--- 4. 記録データのプレビューを基準にする ---
            context.Log("4. 記録データのプレビュー取得");
            context.SetReceiverActive(receiver, false);
            yield return context.Step(2);
            recorder.PreviewSeek(lastFrame);
            yield return context.Step(5);
            var preview = context.Capture("02_preview", includeSent: false);
            recorder.PreviewStop();
            yield return context.Step(2);

            //--- 5. 書き出したBVHを再生する ---
            context.Log("5. BVHの読み込みと再生");
            var player = context.MotionPlayer;
            yield return context.Await(player.ApplyPoseByPathAsync(bvhPath, lastFrame));
            yield return context.Step(5);
            var replayed = context.Capture("03_replayed", includeSent: false);
            result.CheckSnapshot(context, replayed);

            //--- 6. 見た目の姿勢が保たれているか ---
            context.Log("6. 往復の一致確認");
            var endEffectorDifferences = VMCTestSnapshot.CompareEndEffectors(preview, replayed,
                context.Config.MotionRetargetToleranceDegrees, out var maxEnd, out var worstEnd);
            result.CheckThat("BVH往復の見た目",
                endEffectorDifferences.Count == 0,
                $"BVHの往復で末端の向きが変わっています(最大 {maxEnd:F2}度 @ {worstEnd}): " +
                string.Join(", ", endEffectorDifferences));

            //記録時の実際の姿勢とも比べる(こちらはマッスル空間のリターゲット誤差が乗る)
            var recordedDifferences = VMCTestSnapshot.CompareEndEffectors(recorded, replayed,
                context.Config.MotionRetargetToleranceDegrees, out var maxRecorded, out var worstRecorded);
            result.CheckThat("BVH往復と実際の姿勢",
                recordedDifferences.Count == 0,
                $"記録時の姿勢とBVH再生後で末端の向きが違います(最大 {maxRecorded:F2}度 @ {worstRecorded})");

            Debug.Log($"[VMCTest] BVH往復誤差: プレビュー比 最大{maxEnd:F2}度 @ {worstEnd} / 実姿勢比 最大{maxRecorded:F2}度 @ {worstRecorded}");

            player.Stop();
        }
    }
}
