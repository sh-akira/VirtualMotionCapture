using sh_akira;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityMemoryMappedFile;
using Valve.VR;

namespace VMC.Tests
{
    /// <summary>
    /// 設定の保存と再読み込み。
    ///
    ///   設定を変更 → 保存 → ファイルから読み直し
    ///   → 全項目が往復すること
    ///   → 読み直した後もアバターとキャリブレーションが復元されること
    ///
    /// 設定項目の追加時に [OptionalField] の付け忘れや初期化漏れで
    /// 値が失われるのを検出することが目的。
    /// </summary>
    public sealed class Scenario_SettingsSaveLoad : VMCTestScenario
    {
        public override string Name => "SettingsSaveLoad";

        public override string Description => "設定の保存→再読み込みで全項目とキャリブレーションが復元されるか";

        //色は適用時にガンマ/リニア変換を通って最下位ビットが揺れるため、桁落ちしない範囲で許容する
        private const float FloatTolerance = 1e-4f;

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            //--- 2. キャリブレーションまで済ませて、保存すべき状態を作る ---
            context.Log("2. トラッカー受信とキャリブレーション");
            var receiver = context.CreateReceiver(setting => setting.ApplyTracker = true);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            //--- 3. いろいろな型の設定を既定値から変える ---
            context.Log("3. 設定の変更");
            Settings.Current.ShowCameraGrid = true;
            Settings.Current.LeftHandTrackerOffsetToBottom = 0.035f;
            Settings.Current.WristRotationFix_UpperArmWeight = 321;
            Settings.Current.MotionRecord_Fps = 24;
            Settings.Current.CameraFOV = 42f;
            //LipSyncGainは適用時に[1,256]へクランプされる(Settingsの既定値0は範囲外)。
            //有効な値を入れておかないと「保存0 → 再読み込み後1」になり往復比較のノイズになる。
            Settings.Current.LipSyncGain = 4f;
            Settings.Current.ExternalMotionSenderPort = 39501;
            Settings.Current.ExternalMotionSenderAddress = "127.0.0.1";
            Settings.Current.ExternalMotionSenderOptionString = "VMCTest_Option";
            Settings.Current.LightColor = new Color(0.25f, 0.5f, 0.75f, 1f);
            Settings.Current.BackgroundColor = new Color(0.1f, 0.2f, 0.3f, 1f);
            Settings.Current.EnableAutoCalibrationOnModelLoad = true;
            Settings.Current.Head = System.Tuple.Create(ETrackedDeviceClass.HMD, VMCTestTrackerRig.Hmd);
            //ウインドウ関連はエディタでは無効なので既定のままにしておく
            Settings.Current.HideBorder = false;
            Settings.Current.IsTransparent = false;

            var snapshotBeforeSave = context.Capture("01_before_save", includeSent: false);

            //--- 4. 保存 ---
            context.Log("4. 設定の保存");
            var settingsPath = context.OutputPath($"{Name}.{context.ModelKey}.settings.json");
            context.Window.Test_SaveSettings(settingsPath);
            yield return context.Step(2);

            result.CheckThat("設定ファイルの書き出し",
                File.Exists(settingsPath) && new FileInfo(settingsPath).Length > 100,
                $"設定ファイルが書き出されていません({settingsPath})");
            if (File.Exists(settingsPath) == false) yield break;

            //SaveSettingsがAAA_SavedVersionを書き換えるので、保存後の状態を基準にする
            var expected = Settings.Current;

            //--- 5. ファイルからの復元(シリアライズの往復) ---
            context.Log("5. シリアライズ往復の確認");
            var deserialized = Json.Serializer.Deserialize<Settings>(File.ReadAllText(settingsPath));

            var serializeDifferences = VMCTestObjectComparer.Compare(expected, deserialized, FloatTolerance);
            result.CheckThat("設定のシリアライズ往復",
                serializeDifferences.Count == 0,
                $"保存して読み直すと値が変わる項目があります({serializeDifferences.Count}件): " +
                string.Join(" / ", serializeDifferences));

            //LastCalibrationSnapshotは自動再キャリブレーションの要なので個別に確認する
            result.CheckThat("キャリブレーション記録の保存",
                deserialized.LastCalibrationSnapshot != null &&
                deserialized.LastCalibrationSnapshot.Poses != null &&
                deserialized.LastCalibrationSnapshot.Poses.Count >= 6,
                "キャリブレーション時のトラッカー姿勢が設定ファイルに保存されていません" +
                $"(poses={deserialized.LastCalibrationSnapshot?.Poses?.Count.ToString() ?? "null"})");

            //--- 6. アプリとして読み直す ---
            context.Log("6. 設定ファイルの再読み込み");
            var previousModel = context.CurrentModel;
            context.Window.LoadSettings(settingsPath);

            //LoadSettings -> ApplySettings は async void でVRMを読み直すため、完了を待つ
            yield return context.WaitUntil(
                () => context.CurrentModel != null && context.CurrentModel != previousModel,
                600, "設定再読み込みによるモデルの読み直し");
            yield return context.Step(20);

            //ApplySettingsはasync voidで、モデル読み込み後もLipSync等の適用が続く。
            //比較が落ち着くまで待ってから判定する(待っても一致しなければ本物の不一致)
            yield return context.WaitUntilOrTimeout(
                () => VMCTestObjectComparer.Compare(deserialized, Settings.Current, FloatTolerance).Count == 0, 300);

            //LoadSettingsはSettings.Currentを差し替えるので、保存時の内容(ファイル)を基準に比較する
            var reloadDifferences = VMCTestObjectComparer.Compare(deserialized, Settings.Current, FloatTolerance);
            result.CheckThat("再読み込み後の設定",
                reloadDifferences.Count == 0,
                $"再読み込み後のSettingsが保存内容と違います({reloadDifferences.Count}件): " +
                string.Join(" / ", reloadDifferences));

            result.CheckThat("再読み込み後のモデル",
                context.CurrentModel != null && context.CurrentModel.GetComponent<Animator>() != null,
                "設定の再読み込み後にアバターが読み込まれていません");

            //--- 7. 再読み込み後もトラッカーで動くか ---
            //(EnableAutoCalibrationOnModelLoad=trueなので自動再キャリブレーションが走るはず)
            context.Log("7. 再読み込み後の自動再キャリブレーション");
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitUntil(
                () => IKManager.Instance.CalibrationState == CalibrationState.Calibrated,
                600, "自動再キャリブレーションの完了");
            yield return context.Step(20);

            var reloadedIpose = context.Capture("02_after_reload_ipose", includeSent: false);
            result.CheckSnapshot(context, reloadedIpose);

            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            yield return context.Step(20);
            var reloadedTpose = context.Capture("03_after_reload_tpose", includeSent: false);
            result.CheckSnapshot(context, reloadedTpose);

            var delta = VMCTestSnapshot.MaxBoneRotationDelta(reloadedIpose, reloadedTpose, out var movedBone);
            result.CheckThat("再読み込み後の追従",
                delta > 15f,
                $"設定を読み直した後にアバターがトラッカーへ追従していません(最大回転差 {delta:F2}度 / {movedBone ?? "なし"})");

            //保存前と保存後で同じ姿勢が再現されているか(キャリブレーション記録が効いているか)
            var reproduce = VMCTestSnapshot.MaxBoneRotationDelta(snapshotBeforeSave, reloadedIpose, out var worstBone);
            result.CheckThat("キャリブレーションの再現",
                reproduce < 5f,
                $"再読み込み後のIポーズが保存前と違います(最大回転差 {reproduce:F2}度 @ {worstBone})");
        }

    }
}
