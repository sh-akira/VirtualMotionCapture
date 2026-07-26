using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVRM10;

namespace VMC.Tests
{
    /// <summary>
    /// 表情の合成順序。
    ///
    /// FaceController には
    ///   ベース(SetFace) → 加算(MixPresets, 1.0でクランプ) → 上書き(OverwritePresets)
    /// の3段があり、リップシンク・VMCProtocol・MIDI・まばたき・モーション再生の
    /// 5系統が同時に書き込む。優先順位が崩れると「口が動かない」「表情が戻らない」になる。
    /// </summary>
    public sealed class Scenario_FaceMixing : VMCTestScenario
    {
        public override string Name => "FaceMixing";

        public override string Description => "複数入力源からの表情合成の優先順位";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0, VMCTestModels.Vrm10 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));
            context.FaceController.EnableBlink = false;
            yield return context.Step(5);

            var face = context.FaceController;
            var joy = ExpressionKey.CreateFromPreset(ExpressionPreset.happy);
            var angry = ExpressionKey.CreateFromPreset(ExpressionPreset.angry);
            var aa = ExpressionKey.CreateFromPreset(ExpressionPreset.aa);

            //--- 2. ベースの表情 ---
            context.Log("2. ベース表情");
            face.SetFace(joy, 0.5f, false);
            yield return context.Step(3);
            result.CheckThat("ベース表情",
                Near(context.Capture("tmp", false).GetExpression("Joy"), 0.5f),
                $"SetFaceで設定した表情が反映されていません(Joy={context.Capture("tmp", false).GetExpression("Joy"):F3})");

            //--- 3. 加算は足し合わされ、1.0でクランプされる ---
            context.Log("3. 加算の合成とクランプ");
            face.MixPresets("SourceA", new[] { joy }, new[] { 0.3f });
            face.MixPresets("SourceB", new[] { joy }, new[] { 0.4f });
            yield return context.Step(3);

            var mixed = context.Capture("01_mixed", includeSent: false).GetExpression("Joy");
            //0.5(ベース) + 0.3 + 0.4 = 1.2 → 1.0にクランプ
            result.CheckThat("加算の合成とクランプ",
                Near(mixed, 1.0f),
                $"複数ソースの加算とクランプが期待どおりではありません(Joy={mixed:F3} 期待 1.000)");

            //加算値を下げると合計も下がる
            face.MixPresets("SourceA", new[] { joy }, new[] { 0.1f });
            face.MixPresets("SourceB", new[] { joy }, new[] { 0.1f });
            yield return context.Step(3);
            var lowered = context.Capture("tmp", false).GetExpression("Joy");
            result.CheckThat("加算値の反映",
                Near(lowered, 0.7f),
                $"加算値を下げても合計に反映されていません(Joy={lowered:F3} 期待 0.700)");

            //--- 4. 上書きは加算より強い ---
            context.Log("4. 上書きの優先");
            face.OverwritePresets("Playback", new[] { joy }, new[] { 0.2f });
            yield return context.Step(3);

            var overwritten = context.Capture("02_overwritten", includeSent: false).GetExpression("Joy");
            result.CheckThat("上書きの優先",
                Near(overwritten, 0.2f),
                $"OverwritePresetsがMixPresetsより優先されていません(Joy={overwritten:F3} 期待 0.200)");

            //上書きを空にすると加算の合計に戻る
            face.OverwritePresets("Playback", new ExpressionKey[0], new float[0]);
            yield return context.Step(3);
            var restored = context.Capture("tmp", false).GetExpression("Joy");
            result.CheckThat("上書き解除",
                Near(restored, 0.7f),
                $"上書きを解除しても加算の合計に戻りません(Joy={restored:F3} 期待 0.700)");

            //--- 5. 別のキーは互いに影響しない ---
            context.Log("5. キーごとの独立性");
            face.MixPresets("SourceA", new[] { angry }, new[] { 0.6f });
            yield return context.Step(3);

            var independent = context.Capture("03_independent", includeSent: false);
            //SourceAはangryに切り替わったのでjoyへの寄与は消える(0.5 + 0.1(SourceB) = 0.6)
            result.CheckThat("キーごとの独立性",
                Near(independent.GetExpression("Angry"), 0.6f) && Near(independent.GetExpression("Joy"), 0.6f),
                $"表情キーごとの合成が独立していません(Angry={independent.GetExpression("Angry"):F3} 期待 0.600 / " +
                $"Joy={independent.GetExpression("Joy"):F3} 期待 0.600)");

            //--- 6. VRM0.x互換名でも同じ表情を指せる ---
            context.Log("6. VRM0.x互換名での指定");
            face.MixPresets("SourceA", new ExpressionKey[0], new float[0]);
            face.MixPresets("SourceB", new ExpressionKey[0], new float[0]);
            face.SetFace(new List<string> { "Neutral" }, new List<float> { 1f }, false);
            yield return context.Step(3);

            //"A" は VRM0.x での aa の名前
            face.MixPresets("Vrm0Name", new[] { "A" }, new[] { 0.8f });
            yield return context.Step(3);

            var byVrm0Name = context.Capture("04_vrm0_name", includeSent: false).GetExpression("A");
            result.CheckThat("VRM0.x互換名での指定",
                Near(byVrm0Name, 0.8f),
                $"VRM0.x名(\"A\")で表情を指定できていません(A={byVrm0Name:F3} 期待 0.800)。" +
                "VRM1.0モデルでもVRM0.x名で受信できる必要があります");

            //--- 7. 存在しない表情名を送っても壊れない ---
            context.Log("7. 存在しない表情名");
            //直前の "Vrm0Name" ソースが A=0.8 を加算し続けているので、先に解除しておく
            //(加算は入力源ごとに保持され、解除するまで残る)
            face.MixPresets("Vrm0Name", new string[0], new float[0]);
            yield return context.Step(3);

            context.BeginErrorCapture();
            face.MixPresets("Unknown", new[] { "ThisExpressionDoesNotExist", "A" }, new[] { 1.0f, 0.4f });
            yield return context.Step(5);
            var errors = context.EndErrorCapture();

            var stillWorks = context.Capture("05_unknown_name", includeSent: false).GetExpression("A");
            result.CheckThat("存在しない表情名の無視",
                errors.Count == 0 && Near(stillWorks, 0.4f),
                $"存在しない表情名でエラー({errors.Count}件)、または他の表情が壊れました(A={stillWorks:F3} 期待 0.400)");

            //--- 8. まばたきとの共存 ---
            context.Log("8. まばたきの停止指定");
            face.EnableBlink = true;
            face.StopBlink = true;
            yield return context.Step(30);

            var blinkStopped = context.Capture("06_blink_stopped", includeSent: false);
            result.CheckThat("まばたきの停止",
                blinkStopped.GetExpression("Blink") < 0.01f,
                $"StopBlink中なのにまばたきが適用されています(Blink={blinkStopped.GetExpression("Blink"):F3})");

            face.EnableBlink = false;
            face.StopBlink = false;
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < 0.02f;
    }
}
