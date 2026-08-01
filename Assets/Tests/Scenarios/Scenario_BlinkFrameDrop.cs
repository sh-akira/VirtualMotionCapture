using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// 処理落ち時の自動まばたき。
    ///
    /// まばたきは 閉じる(0.06秒) → 閉じたまま維持(0.1秒) → 開く(0.03秒) の合計0.19秒しかなく、
    /// Maximum Allowed Timestep が0.33333秒なので、重い1フレームで丸ごと飛び越せる。
    /// 飛び越したときに中間状態(目を閉じたまま)で止まると、
    /// 次のまばたきまで(最大10秒)目が閉じっぱなしになる。
    ///
    /// AnimationController.TestTimeProvider で時計を差し替え、処理落ちを決定論的に再現する。
    /// </summary>
    public sealed class Scenario_BlinkFrameDrop : VMCTestScenario
    {
        public override string Name => "BlinkFrameDrop";

        public override string Description => "処理落ちしても自動まばたきが開いた状態に戻ること";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0, VMCTestModels.Vrm10 };

        private const float WaitTime = 0.5f;        //まばたきの間隔(最短=最長にして乱数を排除する)
        private const float FrameTime = 1f / 60f;
        private const float DropTime = 0.33333334f; //Maximum Allowed Timestep(1フレームで進みうる最大時間)

        private float clock;      //疑似時計
        private float cycleStart; //今のまばたきサイクルが始まった時刻

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var face = context.FaceController;
            var savedBlinkTimeMin = face.BlinkTimeMin;
            var savedBlinkTimeMax = face.BlinkTimeMax;
            face.EnableBlink = false;
            face.StopBlink = false;
            face.BlinkTimeMin = WaitTime;
            face.BlinkTimeMax = WaitTime;
            yield return context.Step(5);

            var closeTime = face.CloseAnimationTime;
            var openStart = WaitTime + closeTime + face.ClosingTime;
            var openTime = face.OpenAnimationTime;
            var blinkEnd = openStart + openTime;

            clock = 1000f;
            AnimationController.TestTimeProvider = () => clock;
            try
            {
                //--- 2. 処理落ちしていない時のまばたき ---
                context.Log("2. 通常のまばたき");
                yield return StartCycle(context, face);

                var maxBlink = 0f;
                var frames = Mathf.CeilToInt((blinkEnd + 0.05f) / FrameTime);
                for (int i = 0; i < frames; i++)
                {
                    yield return AdvanceBy(context, FrameTime);
                    maxBlink = Mathf.Max(maxBlink, ReadBlink(context));
                }
                var afterBlink = ReadBlink(context);

                result.CheckThat("通常のまばたき",
                    maxBlink > 0.9f && afterBlink < 0.01f,
                    $"目を閉じて開くまでの一連の動作になっていません" +
                    $"(最大 {maxBlink:F3} 期待 1.000 / 終了後 {afterBlink:F3} 期待 0.000)");

                //--- 3. 処理落ちで開くアニメーションの途中に着地した時 ---
                context.Log("3. 処理落ちで開くアニメーションの途中に着地");
                yield return StartCycle(context, face);
                yield return JumpTo(context, WaitTime - FrameTime);

                //1フレームで「閉じる」「維持」を飛び越して、「開く」のちょうど中間へ着地させる
                yield return JumpTo(context, openStart + openTime * 0.5f);
                var midOpenBlink = ReadBlink(context);

                result.CheckThat("処理落ち後の目の開き具合",
                    Mathf.Abs(midOpenBlink - 0.5f) < 0.1f,
                    $"飛び越した後、その時刻に対応した開き具合になっていません" +
                    $"(Blink={midOpenBlink:F3} 期待 0.500)。" +
                    $"アニメーションの先頭の値に戻していると1.000(目を閉じたまま)になります");

                //--- 4. 処理落ちでまばたき全体を飛び越した時 ---
                context.Log("4. 処理落ちでまばたき全体を飛び越す");
                yield return StartCycle(context, face);
                yield return JumpTo(context, WaitTime - FrameTime);

                //目を閉じている途中まで進めてから、そこで処理落ちさせる
                yield return JumpTo(context, WaitTime + closeTime * 0.5f);
                var duringClose = ReadBlink(context);

                yield return AdvanceBy(context, DropTime);
                var afterDrop = ReadBlink(context);

                result.CheckThat("処理落ち直後の復帰",
                    duringClose > 0.3f && afterDrop < 0.01f,
                    $"まばたきの途中で{DropTime:F3}秒の処理落ちが起きた後、目が開いた状態に戻っていません" +
                    $"(処理落ち前 {duringClose:F3} / 直後 {afterDrop:F3} 期待 0.000)");

                //報告された症状そのもの。処理落ち後、次のまばたきが来るまで目が閉じたままになっていないか
                var maxDuringWait = 0f;
                var waitFrames = Mathf.CeilToInt(WaitTime * 0.8f / FrameTime);
                for (int i = 0; i < waitFrames; i++)
                {
                    yield return AdvanceBy(context, FrameTime);
                    maxDuringWait = Mathf.Max(maxDuringWait, ReadBlink(context));
                }

                result.CheckThat("処理落ち後の待機中に目が閉じたままにならない",
                    maxDuringWait < 0.01f,
                    $"処理落ちの後、次のまばたきまでの待機中に目が閉じたままになっています" +
                    $"(最大 {maxDuringWait:F3} 期待 0.000)");
            }
            finally
            {
                AnimationController.TestTimeProvider = null;
                face.EnableBlink = false;
                face.BlinkTimeMin = savedBlinkTimeMin;
                face.BlinkTimeMax = savedBlinkTimeMax;
            }
        }

        /// <summary>まばたきを止めてリセットし、新しいサイクルを開始する</summary>
        private IEnumerator StartCycle(VMCTestContext context, FaceController face)
        {
            face.EnableBlink = false;
            clock += FrameTime;
            yield return Apply(context);

            face.EnableBlink = true;
            clock += FrameTime;
            cycleStart = clock; //このフレームのNext()でシーケンスの開始時刻になる
            yield return Apply(context);
        }

        /// <summary>サイクル開始からの経過時間がtargetElapsedになるところまで、1フレームで一気に進める</summary>
        private IEnumerator JumpTo(VMCTestContext context, float targetElapsed)
        {
            clock = cycleStart + targetElapsed;
            yield return Apply(context);
        }

        private IEnumerator AdvanceBy(VMCTestContext context, float seconds)
        {
            clock += seconds;
            yield return Apply(context);
        }

        /// <summary>
        /// 今の時刻をアバターに反映し、読み取れる状態にする。
        /// 表情は FaceController が Update で積み上げ、VRMのランタイムがその後で反映するため、
        /// 1フレームだけでは前フレームの値しか読めない。
        /// 時計を進めずにもう1フレーム回す(同じ時刻なので Next() の結果は変わらない)。
        /// </summary>
        private static IEnumerator Apply(VMCTestContext context)
        {
            yield return context.Step(1);
            yield return context.Step(1);
        }

        private static float ReadBlink(VMCTestContext context)
            => context.Capture("tmp", includeSent: false).GetExpression("Blink");
    }
}
