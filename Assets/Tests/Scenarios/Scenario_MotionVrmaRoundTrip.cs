using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// 「すべて受信した状態」のVRMA書き出しと読み込みの往復。
    ///
    ///   トラッカー(VRIK) + 表情 + LookAt を全部受けた状態を記録
    ///     → VRMAに書き出し
    ///     → 書き出したVRMAを読み込んで再生
    ///     → 記録時のボーン・表情・視線が復元されるか
    ///
    /// 「合成後のデータが書き出されているか」を、記録時のスナップショットと
    /// 再生後のスナップショットの一致で確認する。
    /// 途中で姿勢を変えるので、モーションが時間変化として記録されていることも見る。
    /// </summary>
    public sealed class Scenario_MotionVrmaRoundTrip : VMCTestScenario
    {
        public override string Name => "MotionVrmaRoundTrip";

        public override string Description => "受信状態を記録→VRMA書き出し→読み込みで復元されるか";

        //記録するポーズAとポーズBの表情
        private static readonly KeyValuePair<string, float>[] FaceA =
        {
            new KeyValuePair<string, float>("Joy", 0.8f),
            new KeyValuePair<string, float>("A", 0.4f),
        };
        private static readonly KeyValuePair<string, float>[] FaceB =
        {
            new KeyValuePair<string, float>("Sorrow", 0.6f),
            new KeyValuePair<string, float>("O", 0.5f),
        };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            //--- 2. トラッカーを受けてキャリブレーション ---
            context.Log("2. トラッカー受信とキャリブレーション");
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

            //--- 3. 記録設定 ---
            Settings.Current.MotionRecord_Fps = 30;
            Settings.Current.MotionRecord_CountdownSeconds = 0;
            Settings.Current.MotionRecord_SaveMotion = true;
            Settings.Current.MotionRecord_SaveExpressionPreset = true;
            Settings.Current.MotionRecord_SaveExpressionCustom = true;
            Settings.Current.MotionRecord_SaveLookAt = true;

            var recorder = context.MotionRecorder;
            if (recorder == null)
            {
                throw new System.Exception("MotionRecorder が見つかりません");
            }

            //--- 4. ポーズA(Iポーズ + 表情A + 右を見る)を記録 ---
            context.Log("4. ポーズAの記録");
            context.Inject(receiver, VMCTestOscBuilder.BlendShapes(FaceA));
            context.Inject(receiver, VMCTestOscBuilder.Eye(true, new Vector3(0.35f, 0.05f, 1.0f)));
            yield return context.Step(5);

            recorder.StartRecording();
            yield return context.Step(40); //30fps記録 x 約0.67秒
            var poseA = context.Capture("01_recorded_pose_a", includeSent: false);
            var frameA = recorder.Test_RecordedFrameCount - 1;

            //--- 5. ポーズB(Tポーズ + 表情B + 左を見る)を記録 ---
            context.Log("5. ポーズBの記録");
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            context.Inject(receiver, VMCTestOscBuilder.BlendShapes(FaceB));
            context.Inject(receiver, VMCTestOscBuilder.Eye(true, new Vector3(-0.35f, -0.10f, 1.0f)));
            yield return context.Step(40);

            var poseB = context.Capture("02_recorded_pose_b", includeSent: false);
            var frameB = recorder.Test_RecordedFrameCount - 1;

            recorder.StopRecording();
            yield return context.Step(2);

            result.CheckThat("モーションの記録",
                recorder.Test_State == MotionRecorder.RecordState.Recorded && frameB > frameA && frameA >= 0,
                $"記録できていません(state={recorder.Test_State} frameA={frameA} frameB={frameB})");

            //ポーズAとBがちゃんと違う姿勢であること(そうでないと往復検証が意味を持たない)
            var poseDelta = VMCTestSnapshot.MaxBoneRotationDelta(poseA, poseB, out _);
            result.CheckThat("記録した2姿勢の差",
                poseDelta > 15f,
                $"ポーズAとBがほぼ同じです(最大回転差 {poseDelta:F2}度)。往復検証が成立しません");

            if (recorder.Test_State != MotionRecorder.RecordState.Recorded) yield break;

            //--- 6. VRMAに書き出す ---
            context.Log("6. VRMAの書き出し");
            var vrmaPath = context.OutputPath($"{Name}.{context.ModelKey}.vrma");
            recorder.Test_SaveRecording(vrmaPath, 0, 0, recorder.Test_RecordedFrameCount - 1);
            yield return context.Step(2);

            var fileInfo = new FileInfo(vrmaPath);
            result.CheckThat("VRMAの書き出し",
                fileInfo.Exists && fileInfo.Length > 1024,
                $"VRMAが書き出されていません({vrmaPath})");
            if (fileInfo.Exists == false) yield break;

            //--- 7. 比較の基準として、記録データそのもののプレビューを取る ---
            //プレビューもVRMA再生も同じHumanPose経由なので、両者の差はVRMAの書き出し/読み込みの精度だけになる。
            //これで「ファイル形式の往復」と「マッスル空間のリターゲット誤差」を切り分けられる。
            context.Log("7. 記録データのプレビュー取得(比較の基準)");
            context.SetReceiverActive(receiver, false);
            yield return context.Step(2);

            recorder.PreviewSeek(frameA);
            yield return context.Step(5);
            var previewA = context.Capture("03_preview_pose_a", includeSent: false);

            recorder.PreviewSeek(frameB);
            yield return context.Step(5);
            var previewB = context.Capture("04_preview_pose_b", includeSent: false);

            recorder.PreviewStop();
            yield return context.Step(2);
            context.SetReceiverActive(receiver, true);
            yield return context.Step(2);

            //--- 8. 他の入力を止めてから、書き出したVRMAを再生する ---
            context.Log("8. VRMAの読み込みと再生");
            //視線のOSCターゲットを外す(SpecifiedTransformが残っているとSetYawPitchManuallyが効かない)
            context.Inject(receiver, VMCTestOscBuilder.Eye(false, Vector3.zero));
            yield return context.Step(2);
            context.SetReceiverActive(receiver, false);
            yield return context.Step(2);

            var player = context.MotionPlayer;
            if (player == null)
            {
                throw new System.Exception("MotionPlayer が見つかりません");
            }

            yield return context.Await(player.ApplyPoseByPathAsync(vrmaPath, frameA));
            yield return context.Step(5);
            var replayA = context.Capture("05_replayed_pose_a", includeSent: false);

            yield return context.Await(player.ApplyPoseByPathAsync(vrmaPath, frameB));
            yield return context.Step(5);
            var replayB = context.Capture("06_replayed_pose_b", includeSent: false);

            //--- 9. 一致確認 ---
            context.Log("9. 往復の一致確認");
            //(a) VRMAファイル自体の往復。プレビューと再生の差はglTFの書き出し/読み込みの精度だけ
            VerifyVrmaFile(context, result, "A", previewA, replayA);
            VerifyVrmaFile(context, result, "B", previewB, replayB);
            //(b) 記録→再生の総合。マッスル空間のリターゲット誤差が乗る
            VerifyRoundTrip(context, result, "A", poseA, replayA);
            VerifyRoundTrip(context, result, "B", poseB, replayB);

            //再生した2フレームがちゃんと違うこと(=モーションが時間変化として記録されている)
            var replayDelta = VMCTestSnapshot.MaxBoneRotationDelta(replayA, replayB, out _);
            result.CheckThat("再生した2フレームの差",
                replayDelta > 15f,
                $"VRMAの2フレームがほぼ同じです(最大回転差 {replayDelta:F2}度)。姿勢の変化が記録されていません");

            result.CheckSnapshot(context, previewA);
            result.CheckSnapshot(context, previewB);
            result.CheckSnapshot(context, replayA);
            result.CheckSnapshot(context, replayB);

            player.Stop();
        }

        /// <summary>
        /// VRMAファイルの往復。記録データのプレビューと、書き出したVRMAの再生を比べる。
        /// どちらもHumanPose経由なので、差が出たらglTFの書き出し/読み込み側の問題。
        /// </summary>
        private static void VerifyVrmaFile(VMCTestContext context, VMCTestResult result, string label,
            VMCTestSnapshot preview, VMCTestSnapshot replayed)
        {
            //見た目の姿勢が保たれているか。ボーン単位の回転が多少違っても、
            //末端(頭・手・足)の向きが同じならアバターの見た目は変わらない
            var endEffectorDifferences = VMCTestSnapshot.CompareEndEffectors(preview, replayed,
                context.Config.VrmaEndEffectorToleranceDegrees, out var maxEnd, out var worstEnd);
            result.CheckThat($"VRMAファイルの往復・見た目({label})",
                endEffectorDifferences.Count == 0,
                $"書き出したVRMAで末端の向きが変わっています(最大 {maxEnd:F2}度 @ {worstEnd}): " +
                string.Join(", ", endEffectorDifferences));

            //ボーン単位。Humanoidのリターゲットが腕のツイストを配分し直すぶんだけ緩く見る
            var boneDifferences = VMCTestSnapshot.CompareBoneRotations(preview, replayed,
                context.Config.VrmaFileToleranceDegrees, out var maxAngle, out var worstBone);
            result.CheckThat($"VRMAファイルの往復・ボーン単位({label})",
                boneDifferences.Count == 0,
                $"書き出したVRMAが記録データと一致しません(最大 {maxAngle:F2}度 @ {worstBone}, {boneDifferences.Count}本): " +
                string.Join(", ", boneDifferences.GetRange(0, Mathf.Min(8, boneDifferences.Count))));

            Debug.Log($"[VMCTest] VRMAファイルの往復誤差({label}): 末端 最大{maxEnd:F3}度 @ {worstEnd} / ボーン単位 最大{maxAngle:F2}度 @ {worstBone}");
        }

        private static void VerifyRoundTrip(VMCTestContext context, VMCTestResult result, string label,
            VMCTestSnapshot recorded, VMCTestSnapshot replayed)
        {
            var config = context.Config;

            //指はマッスル空間の表現力が特に低いので別枠で見る
            var bodyDifferences = VMCTestSnapshot.CompareBoneRotations(recorded, replayed,
                config.MotionRetargetToleranceDegrees, name => VMCTestSnapshot.IsFingerBone(name) == false,
                out var maxBody, out var worstBody);
            result.CheckThat($"ボーンの往復・指以外({label})",
                bodyDifferences.Count == 0,
                $"記録時と再生後でボーンが一致しません(最大 {maxBody:F2}度 @ {worstBody}, {bodyDifferences.Count}本): " +
                string.Join(", ", bodyDifferences.GetRange(0, Mathf.Min(8, bodyDifferences.Count))));

            var fingerDifferences = VMCTestSnapshot.CompareBoneRotations(recorded, replayed,
                config.MotionFingerToleranceDegrees, VMCTestSnapshot.IsFingerBone,
                out var maxFinger, out var worstFinger);
            result.CheckThat($"ボーンの往復・指({label})",
                fingerDifferences.Count == 0,
                $"記録時と再生後で指が一致しません(最大 {maxFinger:F2}度 @ {worstFinger}, {fingerDifferences.Count}本): " +
                string.Join(", ", fingerDifferences.GetRange(0, Mathf.Min(8, fingerDifferences.Count))));

            //見た目の姿勢(末端の向き)が保たれているか
            var endEffectorDifferences = VMCTestSnapshot.CompareEndEffectors(recorded, replayed,
                config.MotionRetargetToleranceDegrees, out var maxEnd, out var worstEnd);
            result.CheckThat($"記録→再生の見た目({label})",
                endEffectorDifferences.Count == 0,
                $"記録時と再生後で末端の向きが変わっています(最大 {maxEnd:F2}度 @ {worstEnd}): " +
                string.Join(", ", endEffectorDifferences));

            Debug.Log($"[VMCTest] 記録→再生のリターゲット誤差({label}): 末端 最大{maxEnd:F2}度 @ {worstEnd} / " +
                      $"指以外 最大{maxBody:F2}度 @ {worstBody} / 指 最大{maxFinger:F2}度 @ {worstFinger}");

            var expressionDifferences = VMCTestSnapshot.CompareExpressions(recorded, replayed,
                config.MotionWeightTolerance, out var maxWeight, out var worstKey);
            result.CheckThat($"表情の往復({label})",
                expressionDifferences.Count == 0,
                $"記録時と再生後で表情が一致しません(最大 {maxWeight:F3} @ {worstKey}): " +
                string.Join(", ", expressionDifferences));

            var yawDelta = Mathf.Abs(Mathf.DeltaAngle(recorded.LookAtYaw, replayed.LookAtYaw));
            var pitchDelta = Mathf.Abs(Mathf.DeltaAngle(recorded.LookAtPitch, replayed.LookAtPitch));
            result.CheckThat($"視線の往復({label})",
                yawDelta < config.MotionRotationToleranceDegrees && pitchDelta < config.MotionRotationToleranceDegrees,
                $"記録時と再生後で視線が一致しません(yaw {recorded.LookAtYaw:F2}->{replayed.LookAtYaw:F2} / " +
                $"pitch {recorded.LookAtPitch:F2}->{replayed.LookAtPitch:F2})");
        }
    }
}
