using sh_akira;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// 旧バージョンの設定ファイルからのマイグレーション。
    ///
    /// ControlWPFWindow.ApplySettings に IsSettingVersionBefore による移行処理があり、
    /// ここが壊れると「アップデートしたら設定が消える/壊れる」という形でユーザー環境だけで発覚する。
    /// 手動では絶対に回帰確認しない部分なので自動化する。
    /// </summary>
    public sealed class Scenario_SettingsMigration : VMCTestScenario
    {
        public override string Name => "SettingsMigration";

        public override string Description => "旧バージョンの設定ファイルの読み込みと移行";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            //--- 2. v0.48とv0.56の両方の移行対象になる設定ファイルを作る ---
            //v0.48: 表情名の大文字小文字を正しい表記へ補正する
            //v0.56: VMCProtocolReceiverSettingsList を ExternalMotionReceiver*List から生成する
            context.Log("2. 旧バージョン(v0.47)の設定ファイルを作成");
            //実際の保存形式に合わせる(IsSettingVersionBefore は "v" を除去して解釈する)。
            //v0.48とv0.56の両方の移行を通すため、どちらより前のバージョンにする
            Settings.Current.AAA_SavedVersion = "v0.47";
            Settings.Current.VMCProtocolReceiverSettingsList = new List<VMCProtocolReceiverSettings>();
            Settings.Current.ExternalMotionReceiverPortList = new List<int> { 39540, 39541 };
            Settings.Current.ExternalMotionReceiverDelayMsList = new List<int> { 0, 120 };
            Settings.Current.ExternalMotionReceiverEnableList = new List<bool> { true, false };

            //v0.48より前の移行(表情名の大文字小文字補正)も同時に確認する
            Settings.Current.KeyActions = new List<KeyAction>
            {
                new KeyAction
                {
                    Name = "VMCTestFace",
                    KeyConfigs = new List<KeyConfig>(),
                    FaceAction = true,
                    //v0.48より前は表情名が大文字で保存されていた。当時はVRM0.x形式なので "JOY" 等。
                    //移行時に FaceController.GetCaseSensitiveKeyName で正しい表記へ直される。
                    //VRM1.0名(HAPPY)からも引けることを併せて確認する
                    FaceNames = new List<string> { "JOY", "BLINK_L", "HAPPY" },
                    FaceStrength = new List<float> { 1f, 1f, 1f },
                    HandAngles = new List<int>(),
                },
            };

            var oldSettingsPath = context.OutputPath($"{Name}.v047.json");
            File.WriteAllText(oldSettingsPath,
                Json.Serializer.ToReadable(Json.Serializer.Serialize(Settings.Current)));

            //保存したファイルはSaveSettings経由ではないのでバージョンはv0.47のまま
            result.CheckThat("旧設定ファイルの作成",
                File.Exists(oldSettingsPath),
                $"旧バージョンの設定ファイルを作成できませんでした({oldSettingsPath})");

            //--- 3. 読み込ませて移行を走らせる ---
            context.Log("3. 読み込みと移行");
            var previousModel = context.CurrentModel;
            context.Window.LoadSettings(oldSettingsPath);
            yield return context.WaitUntil(
                () => context.CurrentModel != null && context.CurrentModel != previousModel,
                600, "設定読み込みによるモデルの読み直し");
            yield return context.Step(30);
            //ApplySettingsはasync voidで、モデル読み込みの後に移行処理が走る。完了を待ってから判定する
            yield return context.WaitUntilOrTimeout(
                () => Settings.Current.VMCProtocolReceiverSettingsList != null
                   && Settings.Current.VMCProtocolReceiverSettingsList.Count >= 2, 600);

            //--- 4. v0.56の移行: 受信機リストが作られているか ---
            context.Log("4. 移行結果の確認");
            var receiverSettings = Settings.Current.VMCProtocolReceiverSettingsList;
            result.CheckThat("v0.56移行(受信機リスト)",
                receiverSettings != null && receiverSettings.Count == 2
                && receiverSettings[0].Port == 39540 && receiverSettings[0].Enable
                && receiverSettings[1].Port == 39541 && receiverSettings[1].Enable == false
                && receiverSettings[1].DelayMs == 120,
                "旧形式の受信機設定がVMCProtocolReceiverSettingsListへ移行されていません: " +
                (receiverSettings == null ? "null" :
                 string.Join(", ", receiverSettings.Select(d => $"[port={d.Port} enable={d.Enable} delay={d.DelayMs}]"))));

            //移行で作られた受信機は、ボーン適用が全てオフになっているのが正しい
            //(旧バージョンはトラッカー受信のみだったため)
            result.CheckThat("v0.56移行(ボーン適用の既定)",
                receiverSettings != null && receiverSettings.Count > 0
                && receiverSettings[0].ApplyHead == false && receiverSettings[0].ApplyLeftArm == false,
                "移行で作られた受信機のボーン適用が有効になっています。旧バージョンの挙動と変わってしまいます");

            //--- 5. v0.48の移行: 表情名の大文字小文字 ---
            var keyAction = Settings.Current.KeyActions?.FirstOrDefault(d => d.Name == "VMCTestFace");
            result.CheckThat("v0.48移行(表情名の大小文字)",
                keyAction != null && keyAction.FaceNames != null
                && keyAction.FaceNames.Contains("Joy")      //VRM0.x名
                && keyAction.FaceNames.Contains("Blink_L")  //VRM0.x名(アンダースコア入り)
                && keyAction.FaceNames.Contains("happy"),   //VRM1.0名
                "大文字で保存された表情名が正しい表記に補正されていません: " +
                (keyAction?.FaceNames == null ? "null" : string.Join(", ", keyAction.FaceNames)));

            //--- 6. 移行後に保存し直すと、現在のバージョンで安定するか ---
            context.Log("5. 移行後の再保存と再読み込み");
            var migratedPath = context.OutputPath($"{Name}.migrated.json");
            context.Window.Test_SaveSettings(migratedPath);
            yield return context.Step(2);

            var reloaded = Json.Serializer.Deserialize<Settings>(File.ReadAllText(migratedPath));
            result.CheckThat("移行後の再保存",
                reloaded.VMCProtocolReceiverSettingsList != null
                && reloaded.VMCProtocolReceiverSettingsList.Count == 2
                && string.IsNullOrEmpty(reloaded.AAA_SavedVersion) == false
                && reloaded.AAA_SavedVersion != "v0.47",
                "移行後に保存し直したファイルが正しくありません" +
                $"(version={reloaded.AAA_SavedVersion} receivers={reloaded.VMCProtocolReceiverSettingsList?.Count.ToString() ?? "null"})");

            //二重移行が起きないこと(既に移行済みのファイルを読んでも受信機が増えない)
            var previousModel2 = context.CurrentModel;
            context.Window.LoadSettings(migratedPath);
            yield return context.WaitUntil(
                () => context.CurrentModel != null && context.CurrentModel != previousModel2,
                600, "移行後ファイルの読み込み");
            yield return context.Step(30);

            result.CheckThat("二重移行の防止",
                Settings.Current.VMCProtocolReceiverSettingsList != null
                && Settings.Current.VMCProtocolReceiverSettingsList.Count == 2,
                "移行済みのファイルを読み直すと受信機が増えています" +
                $"({Settings.Current.VMCProtocolReceiverSettingsList?.Count.ToString() ?? "null"}個)");
        }
    }
}
