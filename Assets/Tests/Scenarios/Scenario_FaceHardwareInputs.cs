using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// 顔まわりのハードウェア入力(リップシンク / リップトラッキング / アイトラッキング)。
    ///
    /// マイクもViveのフェイシャルトラッカーも無しで、
    /// 実機から値が来た所と同じ地点に値を注入して、表情と目線への反映を確認する。
    /// 残る実機依存は「デバイスが繋がるか」だけになる。
    /// </summary>
    public sealed class Scenario_FaceHardwareInputs : VMCTestScenario
    {
        public override string Name => "FaceHardwareInputs";

        public override string Description => "リップシンク・リップトラッキング・アイトラッキングの反映";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0, VMCTestModels.Vrm10 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));
            context.FaceController.EnableBlink = false;
            yield return context.Step(5);

            //--- 2. リップシンク ---
            context.Log("2. リップシンク(viseme)の反映");
            var lipSync = context.Window.LipSync;
            if (lipSync == null)
            {
                result.CheckThat("リップシンクの参照", false, "ControlWPFWindow.LipSync が設定されていません");
            }
            else
            {
                yield return CheckLipSync(context, result, lipSync);
            }

            //--- 3. リップトラッキング(Vive) ---
            context.Log("3. リップトラッキングの反映");
            var lipTracking = Object.FindObjectOfType<LipTracking_Vive>(true);
            if (lipTracking == null)
            {
                Debug.Log("[VMCTest] LipTracking_Vive がシーンに無いため検査をスキップします");
            }
            else
            {
                yield return CheckLipTracking(context, result, lipTracking);
            }

            //--- 4. アイトラッキング(Vive Pro Eye) ---
            context.Log("4. アイトラッキングの反映");
            var eyeTracking = Object.FindObjectOfType<EyeTracking_ViveProEye>(true);
            if (eyeTracking == null)
            {
                Debug.Log("[VMCTest] EyeTracking_ViveProEye がシーンに無いため検査をスキップします");
            }
            else
            {
                yield return CheckEyeTracking(context, result, eyeTracking);
            }
        }

        private static IEnumerator CheckLipSync(VMCTestContext context, VMCTestResult result, DynamicOVRLipSync lipSync)
        {
            lipSync.MaxLevel = 1.0f;
            lipSync.WeightThreashold = 0.0f;
            lipSync.MaxWeightEmphasis = false;
            lipSync.MaxWeightEnable = false;

            //「あ」を強く、「い」を弱く
            lipSync.Test_ApplyVisemes(0.8f, 0.2f, 0f, 0f, 0f);
            yield return context.Step(3);

            var basic = context.Capture("01_lipsync_basic", includeSent: false);
            result.CheckThat("リップシンクの反映",
                Near(basic.GetExpression("A"), 0.8f) && Near(basic.GetExpression("I"), 0.2f),
                $"visemeが口の表情に反映されていません(A={basic.GetExpression("A"):F3} 期待0.800 / " +
                $"I={basic.GetExpression("I"):F3} 期待0.200)");

            //MaxLevel は全体の倍率
            lipSync.MaxLevel = 0.5f;
            lipSync.Test_ApplyVisemes(0.8f, 0.2f, 0f, 0f, 0f);
            yield return context.Step(3);
            var scaled = context.Capture("tmp", false);
            result.CheckThat("リップシンクのMaxLevel",
                Near(scaled.GetExpression("A"), 0.4f),
                $"MaxLevelが効いていません(A={scaled.GetExpression("A"):F3} 期待0.400)");

            //しきい値未満は切り捨てられる
            lipSync.MaxLevel = 1.0f;
            lipSync.WeightThreashold = 0.3f;
            lipSync.Test_ApplyVisemes(0.8f, 0.2f, 0f, 0f, 0f);
            yield return context.Step(3);
            var thresholded = context.Capture("tmp", false);
            result.CheckThat("リップシンクのしきい値",
                Near(thresholded.GetExpression("A"), 0.8f) && thresholded.GetExpression("I") < 0.01f,
                $"しきい値未満のvisemeが切り捨てられていません(A={thresholded.GetExpression("A"):F3} / " +
                $"I={thresholded.GetExpression("I"):F3} 期待0.000)");

            //最大のものだけ残す
            lipSync.WeightThreashold = 0.0f;
            lipSync.MaxWeightEnable = true;
            lipSync.Test_ApplyVisemes(0.5f, 0.9f, 0.3f, 0f, 0f);
            yield return context.Step(3);
            var maxOnly = context.Capture("02_lipsync_maxonly", includeSent: false);
            result.CheckThat("リップシンクの最大値のみ",
                Near(maxOnly.GetExpression("I"), 0.9f)
                && maxOnly.GetExpression("A") < 0.01f && maxOnly.GetExpression("U") < 0.01f,
                $"MaxWeightEnableで最大のviseme以外が残っています(A={maxOnly.GetExpression("A"):F3} " +
                $"I={maxOnly.GetExpression("I"):F3} U={maxOnly.GetExpression("U"):F3})");

            //強調(3倍・1.0でクランプ)
            lipSync.MaxWeightEnable = false;
            lipSync.MaxWeightEmphasis = true;
            lipSync.Test_ApplyVisemes(0.2f, 0f, 0f, 0f, 0f);
            yield return context.Step(3);
            var emphasized = context.Capture("tmp", false);
            result.CheckThat("リップシンクの強調",
                Near(emphasized.GetExpression("A"), 0.6f),
                $"MaxWeightEmphasis(3倍)が効いていません(A={emphasized.GetExpression("A"):F3} 期待0.600)");

            //後片付け
            lipSync.MaxWeightEmphasis = false;
            lipSync.Test_ApplyVisemes(0f, 0f, 0f, 0f, 0f);
            yield return context.Step(3);
        }

        private static IEnumerator CheckLipTracking(VMCTestContext context, VMCTestResult result, LipTracking_Vive lipTracking)
        {
            var shapeName = "Jaw_Open";
            //SetLipShapeToBlendShapeStringMap は「デバイスから報告されたシェイプ一覧」に
            //含まれる名前しか登録しない。実機が無いとその一覧が空なので、
            //まず重み0で1回流し込んでシェイプ名を認識させる。
            lipTracking.Test_ApplyLipWeights(new Dictionary<string, float> { { shapeName, 0f } });
            yield return context.Step(2);

            //シェイプ名 → 表情名 の対応表を作る(通常はコントロールパネルから設定する)
            lipTracking.SetLipShapeToBlendShapeStringMap(new Dictionary<string, string> { { shapeName, "A" } });
            yield return context.Step(2);

            lipTracking.Test_ApplyLipWeights(new Dictionary<string, float> { { shapeName, 0.65f } });
            yield return context.Step(3);

            var applied = context.Capture("03_liptracking", includeSent: false);
            result.CheckThat("リップトラッキングの反映",
                Near(applied.GetExpression("A"), 0.65f),
                $"リップトラッキングのシェイプが表情に反映されていません(A={applied.GetExpression("A"):F3} 期待0.650)。" +
                $"対応表: {string.Join(", ", lipTracking.GetLipShapeToBlendShapeStringMap())}");

            //対応表に無いシェイプは無視される
            context.BeginErrorCapture();
            lipTracking.Test_ApplyLipWeights(new Dictionary<string, float>
            {
                { "ThisShapeDoesNotExist", 1.0f },
                { shapeName, 0.2f },
            });
            yield return context.Step(3);
            var errors = context.EndErrorCapture();

            var afterUnknown = context.Capture("tmp", false);
            result.CheckThat("未知のシェイプの無視",
                errors.Count == 0 && Near(afterUnknown.GetExpression("A"), 0.2f),
                $"未知のシェイプ名でエラー({errors.Count}件)、または他のシェイプが壊れました" +
                $"(A={afterUnknown.GetExpression("A"):F3} 期待0.200)");

            lipTracking.Test_ApplyLipWeights(new Dictionary<string, float> { { shapeName, 0f } });
            yield return context.Step(3);
        }

        private static IEnumerator CheckEyeTracking(VMCTestContext context, VMCTestResult result, EyeTracking_ViveProEye eyeTracking)
        {
            eyeTracking.UseEyelidMovements = true;
            context.FaceController.ViveProEyeEnabled = true;
            //StopBlink中は SetBlink_L/R が常に0を書くので、まぶたの検査では解除しておく
            context.FaceController.StopBlink = false;

            //LookTargetはモデル読み込み時に作られる。無ければまばたきだけ確認する
            //まぶたを閉じる(openness 0 = 完全に閉じている)
            eyeTracking.Test_ApplyEyeState(0.0f, 1.0f, new Vector3(0f, 0f, 1f));
            yield return context.Step(3);

            var winking = context.Capture("04_eyetracking_wink", includeSent: false);
            result.CheckThat("まぶたの反映(左目を閉じる)",
                Near(winking.GetExpression("Blink_L"), 1.0f) && winking.GetExpression("Blink_R") < 0.05f,
                $"まぶたの開閉が反映されていません(Blink_L={winking.GetExpression("Blink_L"):F3} 期待1.000 / " +
                $"Blink_R={winking.GetExpression("Blink_R"):F3} 期待0.000)");

            //両目を開く
            eyeTracking.Test_ApplyEyeState(1.0f, 1.0f, new Vector3(0f, 0f, 1f));
            yield return context.Step(3);
            var open = context.Capture("tmp", false);
            result.CheckThat("まぶたの反映(両目を開く)",
                open.GetExpression("Blink_L") < 0.05f && open.GetExpression("Blink_R") < 0.05f,
                $"目を開いてもまばたきが残っています(L={open.GetExpression("Blink_L"):F3} R={open.GetExpression("Blink_R"):F3})");

            //視線: スムージングされながらLookTargetが動くこと
            var before = eyeTracking.Test_LookTargetLocalPosition;
            for (int i = 0; i < 30; i++)
            {
                eyeTracking.Test_ApplyEyeState(1.0f, 1.0f, new Vector3(0.5f, 0.2f, 1f));
                yield return context.Step(1);
            }
            var after = eyeTracking.Test_LookTargetLocalPosition;

            result.CheckThat("視線方向の反映",
                Vector3.Distance(before, after) > 0.01f,
                $"視線方向を与えてもLookTargetが動きません({before} -> {after})");

            context.FaceController.ViveProEyeEnabled = false;
            eyeTracking.UseEyelidMovements = false;
            context.FaceController.StopBlink = true;
        }

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < 0.02f;
    }
}
