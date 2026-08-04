using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// ショートカットキー(キーアクション)の検証。
    ///
    /// InputManager.CheckKey は「押されているキーの集合に、そのアクションのキーが全部含まれるか」で
    /// 判定し、同時押しの多いアクションを優先する。条件分岐が多く手動確認が難しい。
    /// </summary>
    public sealed class Scenario_KeyActions : VMCTestScenario
    {
        public override string Name => "KeyActions";

        public override string Description => "ショートカットキーによる表情・ハンド・機能の実行";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        private const int KeyA = 65;
        private const int KeyB = 66;
        private const int KeyC = 67;

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));
            //まばたきが表情を上書きしないようにする
            context.FaceController.EnableBlink = false;
            yield return context.Step(5);

            //--- 2. キーアクションを登録する ---
            context.Log("2. キーアクションの登録");
            Settings.Current.KeyActions = new List<KeyAction>
            {
                //A単独 → Joy
                FaceAction("Joy_A", new[] { KeyA }, "Joy", 1.0f),
                //A+B同時 → Angry (Aだけのアクションより優先されるはず)
                FaceAction("Angry_AB", new[] { KeyA, KeyB }, "Angry", 1.0f),
                //C単独 → 背景色を緑に変える機能
                FunctionAction("Green_C", new[] { KeyC }, Functions.ColorGreen),
            };
            yield return context.Step(2);

            //--- 3. 単独キー ---
            context.Log("3. 単独キーの実行");
            PressKey(KeyA);
            yield return context.Step(5);

            var afterA = context.Capture("01_key_a", includeSent: false);
            result.CheckThat("単独キーでの表情",
                Mathf.Abs(afterA.GetExpression("Joy") - 1.0f) < 0.01f,
                $"Aキーで Joy が適用されていません(Joy={afterA.GetExpression("Joy"):F3})");

            ReleaseKey(KeyA);
            yield return context.Step(3);

            //--- 4. 同時押しは「キーの多い方」が優先される ---
            context.Log("4. 同時押しの優先");
            PressKey(KeyA);
            yield return context.Step(2);
            PressKey(KeyB);
            yield return context.Step(5);

            var afterAB = context.Capture("02_key_ab", includeSent: false);
            var angry = afterAB.GetExpression("Angry");
            var joyOnAB = afterAB.GetExpression("Joy");
            result.CheckThat("同時押しの優先",
                Mathf.Abs(angry - 1.0f) < 0.01f && joyOnAB < 0.01f,
                $"A+Bの同時押しでAngryが優先されていません(Angry={angry:F3} Joy={joyOnAB:F3})。" +
                "キーの少ないアクションが後から上書きしている可能性があります");

            ReleaseKey(KeyB);
            ReleaseKey(KeyA);
            yield return context.Step(3);

            //--- 5. 機能アクション ---
            context.Log("5. 機能アクションの実行");
            Settings.Current.BackgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            yield return context.Step(2);

            PressKey(KeyC);
            yield return context.Step(5);
            ReleaseKey(KeyC);
            yield return context.Step(3);

            var background = Settings.Current.BackgroundColor;
            result.CheckThat("機能アクションの実行",
                background.g > 0.9f && background.r < 0.1f && background.b < 0.1f,
                $"Cキーで背景色が緑になっていません({background})");

            //--- 6. 未登録のキーでは何も起きない ---
            context.Log("6. 未登録キー");
            context.FaceController.SetFace(new List<string>(), new List<float>(), false);
            yield return context.Step(3);
            var beforeUnknown = context.Capture("03_before_unknown_key", includeSent: false);

            PressKey(90); //Z
            yield return context.Step(5);
            ReleaseKey(90);
            yield return context.Step(3);

            var afterUnknown = context.Capture("04_after_unknown_key", includeSent: false);
            var expressionDelta = 0f;
            foreach (var entry in beforeUnknown.Expressions)
            {
                expressionDelta = Mathf.Max(expressionDelta, Mathf.Abs(entry.Value - afterUnknown.GetExpression(entry.Name)));
            }
            result.CheckThat("未登録キーで何も起きないこと",
                expressionDelta < 0.01f,
                $"登録していないキーで表情が変わりました(最大差 {expressionDelta:F3})");
        }

        private static void PressKey(int keyCode)
            => KeyboardAction.KeyDownEvent?.Invoke(null, new KeyboardEventArgs(keyCode));

        private static void ReleaseKey(int keyCode)
            => KeyboardAction.KeyUpEvent?.Invoke(null, new KeyboardEventArgs(keyCode));

        private static KeyAction FaceAction(string name, int[] keyCodes, string faceName, float strength)
        {
            var action = NewAction(name, keyCodes);
            action.FaceAction = true;
            action.FaceNames = new List<string> { faceName };
            action.FaceStrength = new List<float> { strength };
            return action;
        }

        private static KeyAction FunctionAction(string name, int[] keyCodes, Functions function)
        {
            var action = NewAction(name, keyCodes);
            action.FunctionAction = true;
            action.Function = function;
            return action;
        }

        private static KeyAction NewAction(string name, int[] keyCodes)
        {
            var configs = new List<KeyConfig>();
            foreach (var keyCode in keyCodes)
            {
                //InputManager.KeyboardAction_KeyDown が組み立てる KeyConfig と
                //IsEqualKeyCode で一致するように同じ内容にする。
                //keyName も比較対象なので、実際のイベントと同じ値を入れる必要がある
                configs.Add(new KeyConfig
                {
                    type = KeyTypes.Keyboard,
                    actionType = KeyActionTypes.Face,
                    keyCode = keyCode,
                    keyName = new KeyboardEventArgs(keyCode).KeyName,
                });
            }
            return new KeyAction
            {
                Name = name,
                KeyConfigs = configs,
                HandAngles = new List<int>(),
                FaceNames = new List<string>(),
                FaceStrength = new List<float>(),
                LipSyncMaxLevel = 1f,
            };
        }
    }
}
