using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UnityEngine;
using UnityMemoryMappedFile;
using Valve.VR;

namespace VMC.Tests
{
    /// <summary>
    /// シナリオから使うアプリ操作ヘルパー。
    /// 実機のVR機器・コントロールパネル(WPF)・ネットワークを使わずに、
    /// 本番と同じコードパスでアバターを動かすための足場を提供する。
    /// </summary>
    public sealed class VMCTestContext : IDisposable
    {
        public VMCTestConfig Config { get; }
        public ControlWPFWindow Window { get; private set; }
        public ExternalSender Sender { get; private set; }
        public FaceController FaceController { get; private set; }
        public VMCTestSendCapture SendCapture { get; private set; }

        /// <summary>現在実行中のシナリオ名(スナップショットのファイル名に使う)</summary>
        public string ScenarioName { get; set; }

        /// <summary>現在対象にしているモデル種別(vrm0 / vrm10)</summary>
        public string ModelKey { get; set; }

        public GameObject CurrentModel => Window != null ? Window.Test_CurrentModel : null;

        public int FrameCount { get; private set; }

        /// <summary>直近に実行した工程。ハングした時にどこで止まったかを示すために使う</summary>
        public string CurrentStep { get; private set; } = "(未開始)";

        private readonly List<ExternalReceiverForVMC> createdReceivers = new List<ExternalReceiverForVMC>();
        private float originalCaptureDeltaTime;
        private float originalFilterStrength;
        private int originalTargetFrameRate;
        private bool disposed;

        /// <summary>工程の開始を記録する</summary>
        public void Log(string step)
        {
            CurrentStep = step;
            Debug.Log($"[VMCTest] {ScenarioName}[{ModelKey}] {step}");
        }

        public VMCTestContext(VMCTestConfig config)
        {
            Config = config;
        }

        #region セットアップ

        /// <summary>
        /// シーン上のオブジェクトを解決し、実行を決定論的にする。
        /// </summary>
        public bool Initialize()
        {
            var windowObject = GameObject.Find("ControlWPFWindow");
            if (windowObject == null)
            {
                Debug.LogError("[VMCTest] ControlWPFWindow がシーンに見つかりません");
                return false;
            }
            Window = windowObject.GetComponent<ControlWPFWindow>();
            if (Window == null)
            {
                Debug.LogError("[VMCTest] ControlWPFWindow コンポーネントが見つかりません");
                return false;
            }

            Sender = Window.ExternalMotionSenderObject != null
                ? Window.ExternalMotionSenderObject.GetComponent<ExternalSender>()
                : null;
            FaceController = Window.faceController;

            //---コントロールパネル(WPF)への送信を無効化する---
            //MemoryMappedFileServerは相手が居なくてもIsConnected=trueになる。
            //その状態でSendCommandを2回呼ぶと、1回目に立てた完了フラグを誰もクリアしないため
            //  while (senderAccessor.ReadByte(0) == 1) Thread.Sleep(1);
            //で永久に待ち続ける。結果、
            //  ・await側(ImportVRM等)が二度と返らずテストが進まなくなる
            //  ・再生停止時のOnApplicationQuitが同期SendCommandを呼ぶためメインスレッドごと固まる
            //テストではコントロールパネルを起動しないので、送信自体を無効にしておく。
            //一度無効にしたら再生セッション中は戻さない(戻すと停止時に上記のフリーズが起きるため)。
            if (Window.server != null && Window.server.IsConnected)
            {
                Window.server.IsConnected = false;
                Debug.Log("[VMCTest] コントロールパネルへのパイプ送信を無効化しました(この再生セッション中は戻しません)");
            }

            //---決定論化---
            //Time.deltaTimeを固定してフレーム間の時間を実時間から切り離す
            originalCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = Config.FixedDeltaTime;
            //DeviceInfo.updateOkTime() が okTime = validFrames / Application.targetFrameRate を計算するため、
            //ここを -1(=制限なし)にすると okTime が負になり、トラッキング復帰の補間係数が常に0になって
            //トラッカーの姿勢が最初の値で固定されてしまう。必ず正の値、かつフレーム時間と整合させる。
            originalTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = FrameRate;
            //まばたきのランダム待ち時間などを固定する
            UnityEngine.Random.InitState(Config.Seed);

            //受信トラッカーのローパスフィルタを実質無効化する(収束待ちフレームを不要にし、dt依存を消す)
            originalFilterStrength = ExternalReceiverForVMC.filterStrength;
            ExternalReceiverForVMC.filterStrength = 100000f;

            //---共通設定ファイルを退避する---
            //SaveSettings / LoadSettings は「起動時に読み込む設定ファイル」を common.json に書き込むため、
            //テストが作った設定ファイルが次回のVMC起動時に読まれてしまう。テスト後に元へ戻す。
            BackupCommonSettings();

            SendCapture = new VMCTestSendCapture();
            return true;
        }

        private string commonSettingsPath;
        private string commonSettingsBackup;
        private bool commonSettingsExisted;

        private void BackupCommonSettings()
        {
            try
            {
                commonSettingsPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "Settings", "common.json"));
                commonSettingsExisted = System.IO.File.Exists(commonSettingsPath);
                commonSettingsBackup = commonSettingsExisted ? System.IO.File.ReadAllText(commonSettingsPath) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VMCTest] common.json の退避に失敗しました: {ex.Message}");
                commonSettingsPath = null;
            }
        }

        private void RestoreCommonSettings()
        {
            if (commonSettingsPath == null) return;
            try
            {
                if (commonSettingsExisted)
                {
                    System.IO.File.WriteAllText(commonSettingsPath, commonSettingsBackup);
                }
                else if (System.IO.File.Exists(commonSettingsPath))
                {
                    System.IO.File.Delete(commonSettingsPath);
                }
                CommonSettings.Load();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VMCTest] common.json の復元に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// Settingsをテスト用の既定値に初期化する。
        /// new Settings() だけでは [OnDeserializing] の初期化が走らず
        /// VMCProtocolReceiverSettingsList 等がnullのままになるため、
        /// 設定ファイル読み込み時と同じ初期化メソッドを明示的に呼ぶ。
        /// </summary>
        public void ResetSettings()
        {
            var settings = new Settings();
            settings.OnDeserializingMethod(default(StreamingContext));
            Settings.Current = settings;

            //テスト中に自動再キャリブレーションが割り込まないようにする
            Settings.Current.EnableAutoCalibrationOnModelLoad = false;
            Settings.Current.LastCalibrationSnapshot = null;
            //外部機器のUDPポートを掴まないようにする
            //(mocopiはプラグインへ移ったので、プラグイン設定領域の方を落とす)
            if (Settings.Current.PluginSettings == null)
            {
                Settings.Current.PluginSettings = new Dictionary<string, string>();
            }
            Settings.Current.PluginSettings["mocopi/Enable"] = "false";

            if (FaceController != null)
            {
                //まばたきは時間依存なのでスナップショット対象のテストでは止める
                FaceController.EnableBlink = false;
                FaceController.StopBlink = true;
                //入力源ごとの表情は解除されるまで残り続けるので、シナリオ間で持ち越さないようにする
                FaceController.Test_ClearAllMixes();
            }

            //疑似時計を使うシナリオが途中で失敗しても次のシナリオへ持ち越さないようにする
            AnimationController.TestTimeProvider = null;

            //設定ファイル読み込み時と同じく、各コンポーネントへ設定を配る。
            //MotionPlayerのVirtualAvatarのApply*フラグはここでしか更新されないため、
            //これを呼ばないとモーション再生でボーンも視線も一切適用されない
            //(起動時のSettings.Currentはnew Settings()で全boolがfalseのため)。
            Window.AdditionalSettingAction?.Invoke(null);
        }

        #endregion

        #region モデル

        /// <summary>VRMを読み込む(VRM0.x / VRM1.0 どちらも同じ経路)</summary>
        public IEnumerator LoadModel(string vrmPath)
        {
            var task = Window.ImportVRM(vrmPath);
            while (task.IsCompleted == false)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                throw new Exception($"VRMの読み込みに失敗しました: {vrmPath}", task.Exception);
            }

            //読み込み直後はVRIKの生成やボーンの初期化が走るため数フレーム落ち着かせる
            yield return Step(5);

            if (CurrentModel == null)
            {
                throw new Exception($"VRMを読み込みましたがモデルが生成されていません: {vrmPath}");
            }
        }

        /// <summary>Settingsを維持したままモデルだけ入れ替える(別アバター読み込みの検証用)</summary>
        public IEnumerator SwitchModel(string vrmPath)
        {
            var previousModel = CurrentModel;
            yield return LoadModel(vrmPath);
            if (CurrentModel == previousModel)
            {
                throw new Exception("モデルが入れ替わっていません");
            }
        }

        #endregion

        #region モーション

        public MotionPlayer MotionPlayer => Window != null ? Window.Test_MotionPlayer : null;

        public MotionRecorder MotionRecorder => Window != null ? Window.Test_MotionRecorder : null;

        /// <summary>結果フォルダ内のパスを作る</summary>
        public string OutputPath(string fileName)
        {
            var directory = Config.ResolvedOutputDirectory;
            System.IO.Directory.CreateDirectory(directory);
            return System.IO.Path.Combine(directory, fileName);
        }

        #endregion

        #region エラー捕捉

        private List<string> capturedErrors;

        /// <summary>
        /// この区間に出力されたエラー/例外ログを集める。
        /// 「不正な入力を与えても落ちないこと」を検査するのに使う。
        /// </summary>
        public void BeginErrorCapture()
        {
            capturedErrors = new List<string>();
            Application.logMessageReceived += OnLogMessage;
        }

        public List<string> EndErrorCapture()
        {
            Application.logMessageReceived -= OnLogMessage;
            var errors = capturedErrors ?? new List<string>();
            capturedErrors = null;
            return errors;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            //テストハーネス自身の失敗報告は対象外
            if (condition != null && condition.StartsWith("[VMCTest]")) return;
            capturedErrors?.Add($"{type}: {condition}");
        }

        #endregion

        #region 待機

        /// <summary>Taskの完了を待つ(例外はそのまま投げ直す)</summary>
        public IEnumerator Await(Task task)
        {
            while (task.IsCompleted == false)
            {
                yield return null;
                FrameCount++;
            }
            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        /// <summary>
        /// 条件が成立するまでフレームを進める。成立しなくても例外にしない。
        /// async void の処理(ApplySettings等)の完了を待つのに使う。
        /// 待った後で改めて検査すれば、待ち時間による偽の失敗を避けつつ本物の不一致は検出できる。
        /// </summary>
        public IEnumerator WaitUntilOrTimeout(Func<bool> condition, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (condition()) yield break;
                yield return null;
                FrameCount++;
            }
        }

        /// <summary>条件が成立するまでフレームを進める。成立しなければ例外</summary>
        public IEnumerator WaitUntil(Func<bool> condition, int maxFrames, string description)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (condition()) yield break;
                yield return null;
                FrameCount++;
            }
            if (condition() == false)
            {
                throw new Exception($"{description} が {maxFrames} フレーム以内に成立しませんでした");
            }
        }

        #endregion

        #region VMCProtocol 受信

        /// <summary>
        /// VMCProtocolの受信機を1つ作る。
        /// UDPソケットは開かず(ポート衝突と到着タイミングのゆらぎを避けるため)、
        /// VMCTestOscInjector から直接メッセージを流し込んで使う。
        /// </summary>
        public ExternalReceiverForVMC CreateReceiver(Action<VMCProtocolReceiverSettings> configure = null)
        {
            if (Settings.Current.VMCProtocolReceiverSettingsList == null)
            {
                Settings.Current.VMCProtocolReceiverSettingsList = new List<VMCProtocolReceiverSettings>();
            }

            var setting = new VMCProtocolReceiverSettings
            {
                Enable = false, //UDPを開かせないためfalseで追加し、あとで手動で有効化する
                Name = $"VMCTest {Settings.Current.VMCProtocolReceiverSettingsList.Count + 1}",
                Port = 39590 + Settings.Current.VMCProtocolReceiverSettingsList.Count,
                DelayMs = 0, //遅延バッファは実時間依存なので使わない
                ApplyTracker = true,
                ApplyBlendShape = true,
                ApplyLookAt = true,
            };
            configure?.Invoke(setting);
            setting.Enable = false;
            setting.DelayMs = 0;

            Settings.Current.VMCProtocolReceiverSettingsList.Add(setting);
            Window.Test_AddVMCProtocolReceiver(setting);

            var receiver = Window.externalMotionReceivers.LastOrDefault();
            if (receiver == null)
            {
                throw new Exception("VMCProtocol受信機の作成に失敗しました");
            }

            //ソケットを開かないまま受信処理だけを有効にする
            var server = receiver.GetComponent<uOSC.uOscServer>();
            if (server != null) server.enabled = false;
            receiver.gameObject.SetActive(true);

            //Enable=falseで追加したためSetSettingが未反映の項目がある。有効な設定を入れ直す。
            setting.Enable = true;
            receiver.SetSetting(setting);

            createdReceivers.Add(receiver);
            return receiver;
        }

        /// <summary>
        /// トラッキング機器から報告された生のローカル姿勢を取得する。
        /// キャリブレーションでTargetTransformは親子付け替えされるため、
        /// 「注入した値がそのまま届いているか」の確認にはこちらを使う。
        /// </summary>
        public bool TryGetTrackerPose(string name, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (TrackingPointManager.Instance == null) return false;
            if (TrackingPointManager.Instance.TryGetTrackingPoint(name, out var trackingPoint) == false) return false;
            position = trackingPoint.LastLocalPosition;
            rotation = trackingPoint.LastLocalRotation;
            return true;
        }

        /// <summary>注入したトラッカー構成が実際に届いているか(最大位置誤差)を返す。届いていない機器があれば -1</summary>
        public float GetTrackerRigError(IEnumerable<VMCTestTrackerRig.Entry> rig)
        {
            float max = 0f;
            foreach (var entry in rig)
            {
                if (TryGetTrackerPose(entry.Name, out var position, out _) == false) return -1f;
                max = Mathf.Max(max, Vector3.Distance(entry.Position, position));
            }
            return max;
        }

        /// <summary>受信機の有効/無効を切り替える(他の入力源を止めて切り分けるため)</summary>
        public void SetReceiverActive(ExternalReceiverForVMC receiver, bool active)
        {
            if (receiver == null) return;
            receiver.gameObject.SetActive(active);
        }

        public void Inject(ExternalReceiverForVMC receiver, params uOSC.Message[] messages)
            => VMCTestOscInjector.Inject(receiver, messages);

        public void Inject(ExternalReceiverForVMC receiver, IEnumerable<uOSC.Message> messages)
            => VMCTestOscInjector.Inject(receiver, messages);

        /// <summary>標準のトラッカー構成(HMD1 + コントローラ2 + トラッカー3)を送る</summary>
        public void InjectTrackerRig(ExternalReceiverForVMC receiver, IEnumerable<VMCTestTrackerRig.Entry> rig)
        {
            foreach (var entry in rig)
            {
                switch (entry.DeviceClass)
                {
                    case ETrackedDeviceClass.HMD:
                        Inject(receiver, VMCTestOscBuilder.Hmd(entry.Name, entry.Position, entry.Rotation));
                        break;
                    case ETrackedDeviceClass.Controller:
                        Inject(receiver, VMCTestOscBuilder.Controller(entry.Name, entry.Position, entry.Rotation));
                        break;
                    default:
                        Inject(receiver, VMCTestOscBuilder.Tracker(entry.Name, entry.Position, entry.Rotation));
                        break;
                }
            }
        }

        #endregion

        #region VMCProtocol 送信

        /// <summary>ExternalSenderを有効にする(送信先は設定しないのでUDPには出ない。フックだけが発火する)</summary>
        public void EnableSender()
        {
            if (Window.ExternalMotionSenderObject == null)
            {
                throw new Exception("ExternalMotionSenderObject がシーンに設定されていません");
            }
            Window.ExternalMotionSenderObject.SetActive(true);
        }

        public void DisableSender()
        {
            if (Window.ExternalMotionSenderObject != null)
            {
                Window.ExternalMotionSenderObject.SetActive(false);
            }
        }

        #endregion

        #region キャリブレーション

        /// <summary>トラッカー姿勢を入力にしてキャリブレーションを実行する</summary>
        public IEnumerator Calibrate(PipeCommands.CalibrateType calibrateType)
        {
            IKManager.Instance.ModelCalibrationInitialize(silent: true);
            yield return Step(3);
            yield return IKManager.Instance.Calibrate(calibrateType);
            IKManager.Instance.EndCalibrate();
            yield return Step(3);

            if (IKManager.Instance.CalibrationState != CalibrationState.Calibrated)
            {
                throw new Exception($"キャリブレーションに失敗しました state={IKManager.Instance.CalibrationState}");
            }
        }

        #endregion

        #region フレーム進行 / スナップショット

        public int FrameRate => Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Config.FixedDeltaTime)));

        public IEnumerator Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                FrameCount++;
            }
        }

        /// <summary>
        /// トラッカーが「信用できる」状態になるまで待つ。
        /// DeviceInfo は認識直後の1秒間(LEAP_SECONDS)、飛び対策として過去値から徐々に補間するため、
        /// それを過ぎるまで待たないと注入した姿勢がそのまま反映されない。
        /// </summary>
        public IEnumerator WaitTrackingWarmup()
        {
            Log("トラッキングのウォームアップ待ち(DeviceInfoの復帰補間 1秒)");
            yield return Step(FrameRate + 15);
        }

        /// <summary>現在の状態と、直近のClearSentから送信された内容をスナップショットに取る</summary>
        public VMCTestSnapshot Capture(string label, bool includeSent = true)
        {
            var snapshot = VMCTestSnapshot.Capture(ScenarioName, ModelKey, label, FrameCount, CurrentModel);
            if (includeSent && SendCapture != null)
            {
                snapshot.SetSentMessages(SendCapture.Messages);
            }
            return snapshot;
        }

        public void ClearSent() => SendCapture?.Clear();

        #endregion

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            SendCapture?.Dispose();
            SendCapture = null;

            //Destroyは遅延実行なので、先にリストから外す。
            //(残しておくと ExternalSender.externalReceiver が破棄済みオブジェクトを指したままになり、
            // /VMC/Ext/Rcv が送信されなくなる等の形で次のシナリオに影響する)
            foreach (var receiver in createdReceivers)
            {
                if (receiver == null) continue;
                Window?.externalMotionReceivers.Remove(receiver);
                UnityEngine.Object.DestroyImmediate(receiver.gameObject);
            }
            createdReceivers.Clear();
            if (Window != null)
            {
                Window.externalMotionReceivers.RemoveAll(d => d == null);
                if (Sender != null) Sender.externalReceiver = Window.externalMotionReceivers.FirstOrDefault();
            }

            RestoreCommonSettings();

            ExternalReceiverForVMC.filterStrength = originalFilterStrength;
            Time.captureDeltaTime = originalCaptureDeltaTime;
            Application.targetFrameRate = originalTargetFrameRate;
            //パイプ送信は意図的に戻さない(Initializeのコメント参照)
        }
    }

    /// <summary>
    /// テスト用のトラッカー配置。身長1.6m程度の人がIポーズで立っている想定。
    /// トラッカーの割り当ては未指定(自動割り当て)にしているので、
    /// 腰=最も高い位置のトラッカー、足=低い方2つ、という本番と同じ推定ロジックを通る。
    /// </summary>
    public static class VMCTestTrackerRig
    {
        public struct Entry
        {
            public string Name;
            public ETrackedDeviceClass DeviceClass;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public const string Hmd = "VMCTEST_HMD";
        public const string LeftController = "VMCTEST_CON_L";
        public const string RightController = "VMCTEST_CON_R";
        public const string WaistTracker = "VMCTEST_TRA_WAIST";
        public const string LeftFootTracker = "VMCTEST_TRA_FOOT_L";
        public const string RightFootTracker = "VMCTEST_TRA_FOOT_R";

        /// <summary>Iポーズ(両手を体の横に下ろした姿勢)</summary>
        public static IReadOnlyList<Entry> IPose { get; } = new[]
        {
            New(Hmd,               ETrackedDeviceClass.HMD,           new Vector3( 0.00f, 1.60f,  0.00f)),
            New(LeftController,    ETrackedDeviceClass.Controller,    new Vector3(-0.20f, 0.95f,  0.05f)),
            New(RightController,   ETrackedDeviceClass.Controller,    new Vector3( 0.20f, 0.95f,  0.05f)),
            New(WaistTracker,      ETrackedDeviceClass.GenericTracker, new Vector3( 0.00f, 1.00f, -0.10f)),
            New(LeftFootTracker,   ETrackedDeviceClass.GenericTracker, new Vector3(-0.10f, 0.10f,  0.00f)),
            New(RightFootTracker,  ETrackedDeviceClass.GenericTracker, new Vector3( 0.10f, 0.10f,  0.00f)),
        };

        /// <summary>両腕を横に伸ばした姿勢(動きを与えたときの追従確認用)</summary>
        public static IReadOnlyList<Entry> TPose { get; } = new[]
        {
            New(Hmd,               ETrackedDeviceClass.HMD,           new Vector3( 0.00f, 1.60f,  0.00f)),
            New(LeftController,    ETrackedDeviceClass.Controller,    new Vector3(-0.70f, 1.40f,  0.00f)),
            New(RightController,   ETrackedDeviceClass.Controller,    new Vector3( 0.70f, 1.40f,  0.00f)),
            New(WaistTracker,      ETrackedDeviceClass.GenericTracker, new Vector3( 0.00f, 1.00f, -0.10f)),
            New(LeftFootTracker,   ETrackedDeviceClass.GenericTracker, new Vector3(-0.10f, 0.10f,  0.00f)),
            New(RightFootTracker,  ETrackedDeviceClass.GenericTracker, new Vector3( 0.10f, 0.10f,  0.00f)),
        };

        private static Entry New(string name, ETrackedDeviceClass deviceClass, Vector3 position)
            => new Entry { Name = name, DeviceClass = deviceClass, Position = position, Rotation = Quaternion.identity };
    }
}
