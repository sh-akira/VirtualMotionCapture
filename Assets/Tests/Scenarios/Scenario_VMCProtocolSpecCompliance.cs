using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;

namespace VMC.Tests
{
    /// <summary>
    /// VMCProtocol仕様(protocol.vmc.info)への準拠。
    ///
    /// このアプリはVMCProtocolのリファレンス実装なので、
    /// 仕様書に書かれた引数の数・既定値・オプションの扱いを機械的に検証する。
    /// </summary>
    public sealed class Scenario_VMCProtocolSpecCompliance : VMCTestScenario
    {
        public override string Name => "VMCProtocolSpecCompliance";

        public override string Description => "仕様書どおりの引数・既定値・オプションになっているか";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0, VMCTestModels.Vrm10 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var receiver = context.CreateReceiver(setting =>
            {
                setting.ApplyTracker = true;
                setting.ApplyBlendShape = true;
                setting.ApplyControl = true;
            });
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();

            //--- 2. 既定値が仕様どおりか ---
            context.Log("2. 既定値の確認");
            result.CheckThat("既定はオリジナルボーン送信",
                Settings.Current.ExternalMotionSenderUseNormalizedBone == false,
                "正規化(ControlRig)ボーンの送信は仕様上「既定で無効のオプション」ですが、既定で有効になっています");

            result.CheckThat("既定はVRM0.x名のみ送信",
                Settings.Current.ExternalMotionSenderSendVRM1Expression == false,
                "VRM1.0形式の表情送信はオプションですが、既定で有効になっています");

            //--- 3. 引数の数(V2.7準拠) ---
            context.Log("3. 送信メッセージの引数");
            context.EnableSender();
            yield return context.Step(3);
            context.ClearSent();
            //低頻度情報も出させる
            var metaTask = context.Window.LoadVRMMetaAsync(context.Config.GetModelPath(context.ModelKey));
            yield return context.Await(metaTask);
            context.Window.VRMmetaLoadedAction?.Invoke(metaTask.Result);
            yield return context.Step(4);

            CheckArgumentCount(context, result, "/VMC/Ext/OK", 4,
                "V2.7で (int)tracking status が追加されています");
            CheckArgumentCount(context, result, "/VMC/Ext/Rcv", 3,
                "V2.7で (string)IP Address が追加されています");
            CheckArgumentCount(context, result, "/VMC/Ext/VRM", 3,
                "V2.7で (string)Hash が追加されています");
            CheckArgumentCount(context, result, "/VMC/Ext/Root/Pos", 14,
                "v2.1でスケールとオフセットが追加されています");
            CheckArgumentCount(context, result, "/VMC/Ext/Cam", 9, "");

            //VRMハッシュが実際に計算されていること
            var vrmMessage = context.SendCapture.Messages.LastOrDefault(d => d.address == "/VMC/Ext/VRM");
            var hash = vrmMessage.values != null && vrmMessage.values.Length >= 3 ? vrmMessage.values[2] as string : null;
            result.CheckThat("VRMハッシュ",
                string.IsNullOrEmpty(hash) == false && hash.Length == 64,
                $"/VMC/Ext/VRM のHashが正しく計算されていません(\"{hash}\")");

            //--- 4. 送信ボーンがオリジナル(非正規化)であること ---
            context.Log("4. 送信ボーンの座標系");
            var vrm10Instance = context.CurrentModel.GetComponent<Vrm10Instance>();
            var animator = context.CurrentModel.GetComponent<Animator>();
            var converter = context.Window.BonePostureConverter;

            result.CheckThat("変換器の生成",
                converter != null,
                "BonePostureConverter がモデル読み込み時に作られていません");

            if (converter != null)
            {
                Debug.Log($"[VMCTest] このモデルは正規化済み(変換不要): {converter.IsIdentity}");

                var mismatches = new List<string>();
                foreach (var message in context.SendCapture.Messages.Where(d => d.address == "/VMC/Ext/Bone/Pos"))
                {
                    if (message.values.Length != 8 || (message.values[0] is string) == false) continue;
                    if (System.Enum.TryParse<HumanBodyBones>((string)message.values[0], out var bone) == false) continue;
                    var original = vrm10Instance.Humanoid.GetBoneTransform(bone);
                    if (original == null) continue;

                    var sent = new Quaternion((float)message.values[4], (float)message.values[5],
                                              (float)message.values[6], (float)message.values[7]);
                    if (Quaternion.Angle(sent, original.localRotation) > 0.1f)
                    {
                        mismatches.Add($"{bone} {Quaternion.Angle(sent, original.localRotation):F2}度");
                    }
                }

                result.CheckThat("送信ボーンがオリジナル姿勢",
                    mismatches.Count == 0,
                    $"送信しているボーン姿勢が Humanoid.GetBoneTransform(オリジナル)と一致しません" +
                    $"({mismatches.Count}本): {string.Join(", ", mismatches.Take(6))}");
            }

            //--- 5. 正規化ボーン送信オプション ---
            context.Log("5. 正規化ボーン送信オプション");
            Settings.Current.ExternalMotionSenderUseNormalizedBone = true;
            yield return context.Step(3);
            context.ClearSent();
            yield return context.Step(4);

            var normalizedMismatches = new List<string>();
            foreach (var message in context.SendCapture.Messages.Where(d => d.address == "/VMC/Ext/Bone/Pos"))
            {
                if (message.values.Length != 8 || (message.values[0] is string) == false) continue;
                if (System.Enum.TryParse<HumanBodyBones>((string)message.values[0], out var bone) == false) continue;
                var normalized = animator.GetBoneTransform(bone);
                if (normalized == null) continue;

                var sent = new Quaternion((float)message.values[4], (float)message.values[5],
                                          (float)message.values[6], (float)message.values[7]);
                if (Quaternion.Angle(sent, normalized.localRotation) > 0.1f)
                {
                    normalizedMismatches.Add($"{bone} {Quaternion.Angle(sent, normalized.localRotation):F2}度");
                }
            }
            result.CheckThat("正規化ボーン送信オプション",
                normalizedMismatches.Count == 0,
                $"オプションを有効にしても正規化ボーンが送られていません({normalizedMismatches.Count}本): " +
                string.Join(", ", normalizedMismatches.Take(6)));

            Settings.Current.ExternalMotionSenderUseNormalizedBone = false;
            yield return context.Step(3);

            //--- 6. VRM1.0形式の表情送信オプション ---
            context.Log("6. VRM1.0形式の表情送信オプション");
            context.Inject(receiver, VMCTestOscBuilder.BlendShapes(new[]
            {
                new KeyValuePair<string, float>("Joy", 0.75f),
            }));
            yield return context.Step(5);

            context.ClearSent();
            yield return context.Step(4);
            var vrm0Only = CollectBlendShapeNames(context);
            result.CheckThat("既定ではVRM0.x名のみ",
                vrm0Only.Contains("Joy") && vrm0Only.Contains("happy") == false,
                $"既定でVRM1.0名が送信されています(送信名: {string.Join(", ", vrm0Only.Take(20))})");

            Settings.Current.ExternalMotionSenderSendVRM1Expression = true;
            yield return context.Step(3);
            context.ClearSent();
            yield return context.Step(4);
            var both = CollectBlendShapeNames(context);
            result.CheckThat("オプション有効時はVRM1.0名も送信",
                both.Contains("Joy") && both.Contains("happy"),
                $"オプションを有効にしてもVRM0.x名とVRM1.0名の両方が送られていません" +
                $"(送信名: {string.Join(", ", both.Take(25))})");

            Settings.Current.ExternalMotionSenderSendVRM1Expression = false;
            yield return context.Step(3);

            //--- 7. キャリブレーション番号の一貫性 ---
            //仕様: 0=通常, 1=MR通常, 2=MR床補正。PipeCommands.CalibrateType の値と一致している必要がある
            context.Log("7. キャリブレーション番号");
            result.CheckThat("キャリブレーション番号の一貫性",
                (int)PipeCommands.CalibrateType.Default == 0
                && (int)PipeCommands.CalibrateType.FixedHand == 1
                && (int)PipeCommands.CalibrateType.FixedHandWithGround == 2,
                "CalibrateType の値が仕様(0=通常,1=MR通常,2=MR床補正)と一致していません");

            //--- 8. /VMC/Ext/Set/Shortcut ---
            context.Log("8. ショートカット呼び出し");
            Settings.Current.KeyActions = new List<KeyAction>
            {
                new KeyAction
                {
                    Name = "VMCTestShortcut",
                    KeyConfigs = new List<KeyConfig>(),
                    FunctionAction = true,
                    Function = Functions.ColorGreen,
                    HandAngles = new List<int>(),
                    FaceNames = new List<string>(),
                    FaceStrength = new List<float>(),
                    LipSyncMaxLevel = 1f,
                },
            };
            Settings.Current.BackgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            yield return context.Step(2);

            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Shortcut", "VMCTestShortcut"));
            yield return context.Step(5);

            var background = Settings.Current.BackgroundColor;
            result.CheckThat("/VMC/Ext/Set/Shortcut の受信",
                background.g > 0.9f && background.r < 0.1f,
                $"/VMC/Ext/Set/Shortcut でショートカットが実行されていません(背景色 {background})");

            //存在しない名前でも落ちないこと
            context.BeginErrorCapture();
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Shortcut", "NotExistShortcut"));
            yield return context.Step(3);
            var errors = context.EndErrorCapture();
            result.CheckThat("存在しないショートカット名",
                errors.Count == 0,
                $"存在しないショートカット名でエラーが出ました({errors.Count}件)");

            context.DisableSender();
        }

        private static void CheckArgumentCount(VMCTestContext context, VMCTestResult result, string address, int expected, string note)
        {
            var message = context.SendCapture.Messages.LastOrDefault(d => d.address == address);
            var actual = message.address == address && message.values != null ? message.values.Length : -1;
            result.CheckThat($"{address} の引数",
                actual == expected,
                actual < 0
                    ? $"{address} が送信されていません"
                    : $"{address} の引数が {actual} 個です(仕様は {expected} 個)。{note}");
        }

        private static List<string> CollectBlendShapeNames(VMCTestContext context)
        {
            return context.SendCapture.Messages
                .Where(d => d.address == "/VMC/Ext/Blend/Val" && d.values != null && d.values.Length == 2 && d.values[0] is string)
                .Select(d => (string)d.values[0])
                .Distinct()
                .ToList();
        }
    }
}
