using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// 顔まわりのハードウェア入力(リップシンク)。
    ///
    /// マイク無しで、実機から値が来た所と同じ地点に値を注入して表情への反映を確認する。
    /// 残る実機依存は「デバイスが繋がるか」だけになる。
    ///
    /// VIVEのリップトラッキング/アイトラッキングの検査はプラグイン側へ移した
    /// (PluginProjects/VMC.Plugin.ViveSR)。
    /// </summary>
    public sealed class Scenario_FaceHardwareInputs : VMCTestScenario
    {
        public override string Name => "FaceHardwareInputs";

        public override string Description => "リップシンクの反映";

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

        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < 0.02f;
    }
}
