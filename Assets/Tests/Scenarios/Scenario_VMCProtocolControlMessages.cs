using System.Collections;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// VMCProtocolの制御系メッセージの受信。
    /// ボーン・表情・視線以外(カメラ / ライト / 周期設定 / スルー / 入力 / 状態文字列)を確認する。
    ///
    /// 特にカメラは「送信した画角がそのまま受信側に入るか」を、
    /// 送信キャプチャを自分の受信機へ流し込む形で往復検証する。
    /// </summary>
    public sealed class Scenario_VMCProtocolControlMessages : VMCTestScenario
    {
        public override string Name => "VMCProtocolControlMessages";

        public override string Description => "カメラ・ライト・周期設定・スルー・入力の受信";

        private const float SenderFov = 35f;
        private const float LocalFov = 62f;

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込みと受信機の用意");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var receiver = context.CreateReceiver(setting =>
            {
                setting.ApplyTracker = true;
                setting.ApplyCamera = true;
                setting.ApplyLight = true;
                setting.ApplySetting = true;
                setting.ApplyControl = true;
                setting.ApplyStatus = true;
                setting.ApplyMidi = true;
                setting.ApplyControllerInput = true;
                setting.ApplyKeyboardInput = true; //既定はfalseなので明示的に有効化する
            });
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();

            context.EnableSender();
            yield return context.Step(3);

            //--- 2. カメラ画角の往復 ---
            context.Log("2. カメラ画角の往復");
            var cameraManager = CameraManager.Current;
            if (cameraManager == null)
            {
                throw new System.Exception("CameraManager が見つかりません");
            }

            //カメラは HandTrackerRoot の子で、この親はキャリブレーションで身長比のスケールと
            //オフセットを持つ。/VMC/Ext/Cam はこの親から見たローカル座標で送受信する取り決めなので
            //(受信側アバターのスケールへ写像するため)、送信と受信で座標系が食い違うと
            //親の変換が二重に掛かる/掛からない形でカメラ距離がずれる。
            //あえて非単位の値を入れて、その食い違いを検出できるようにする
            var trackerRoot = IKManager.Instance.HandTrackerRoot;
            var savedScale = trackerRoot.localScale;
            var savedPosition = trackerRoot.position;
            trackerRoot.localScale = new Vector3(1.2f, 1.15f, 1.2f);
            trackerRoot.position = new Vector3(0.03f, 0.07f, -0.02f);

            //送信側の画角を決める
            cameraManager.Test_SetCameraFOV(SenderFov);
            yield return context.Step(3);
            context.ClearSent();
            yield return context.Step(4);

            var sentCamera = context.SendCapture.Messages.LastOrDefault(d => d.address == "/VMC/Ext/Cam");
            var hasCameraMessage = sentCamera.address == "/VMC/Ext/Cam" && sentCamera.values != null && sentCamera.values.Length == 9;
            result.CheckThat("カメラの送信",
                hasCameraMessage,
                "/VMC/Ext/Cam が送信されていません。受信側VMCは自分の画角を使い続けます");
            if (hasCameraMessage == false) yield break;

            var sentFov = (float)sentCamera.values[8];
            result.CheckThat("送信された画角",
                Mathf.Abs(sentFov - SenderFov) < 0.01f,
                $"送信された画角が設定値と違います({sentFov:F3} 期待 {SenderFov})");

            var sentPosition = new Vector3((float)sentCamera.values[1], (float)sentCamera.values[2], (float)sentCamera.values[3]);
            var sentRotation = new Quaternion((float)sentCamera.values[4], (float)sentCamera.values[5], (float)sentCamera.values[6], (float)sentCamera.values[7]);

            //送信直前のカメラ姿勢。往復後にここへ戻ってくるのが正しい
            var beforePosition = cameraManager.ControlCamera.transform.position;
            var beforeRotation = cameraManager.ControlCamera.transform.rotation;
            var beforeLocalPosition = cameraManager.ControlCamera.transform.localPosition;
            var beforeLocalRotation = cameraManager.ControlCamera.transform.localRotation;

            //送信値がローカル座標であること(受信側の適用と同じ座標系か)
            var sentLocalError = Vector3.Distance(sentPosition, beforeLocalPosition);
            result.CheckThat("送信されたカメラ位置の座標系",
                sentLocalError < 0.001f,
                $"送信されたカメラ位置がローカル座標になっていません" +
                $"(送信 {sentPosition} / ローカル {beforeLocalPosition} / ワールド {beforePosition})。" +
                $"受信側は localPosition として適用するため、送信もローカル座標で揃える必要があります");

            result.CheckThat("送信されたカメラ回転の座標系",
                Quaternion.Angle(sentRotation, beforeLocalRotation) < 0.1f,
                $"送信されたカメラ回転がローカル回転になっていません" +
                $"(誤差 {Quaternion.Angle(sentRotation, beforeLocalRotation):F3}度)");

            //テスト自体が意味を持つ条件の確認。ワールドとローカルが同じ値なら
            //座標系の食い違いは検出できず、以降のチェックは素通りしてしまう
            result.CheckThat("カメラ座標系テストの前提",
                Vector3.Distance(beforePosition, beforeLocalPosition) > 0.01f,
                $"HandTrackerRootの変換が効いておらず、ワールドとローカルが同値です" +
                $"({beforePosition} / {beforeLocalPosition})。座標系の食い違いを検出できません");

            //受信側を別の画角・別の位置にしてから、送信内容を流し込む
            cameraManager.Test_SetCameraFOV(LocalFov);
            cameraManager.FreeCamera.transform.position = beforePosition + new Vector3(1.5f, 0.8f, -1.2f);
            yield return context.Step(3);
            context.Inject(receiver, sentCamera);
            yield return context.Step(5);

            var appliedFov = cameraManager.ControlCamera.fieldOfView;
            result.CheckThat("受信した画角の反映",
                Mathf.Abs(appliedFov - SenderFov) < 0.01f,
                $"受信した画角がカメラに反映されていません(実際 {appliedFov:F3} / 受信値 {sentFov:F3} / 受信前 {LocalFov})");

            //VMC同士の往復ではカメラが送信直前と同じ場所に戻ること。
            //ずれる場合、送信と受信で座標系が食い違っていて
            //HandTrackerRootのスケール・オフセットが二重に掛かっている(または一度も掛かっていない)
            var appliedPosition = cameraManager.ControlCamera.transform.position;
            var appliedRotation = cameraManager.ControlCamera.transform.rotation;
            var positionError = Vector3.Distance(beforePosition, appliedPosition);
            var rotationError = Quaternion.Angle(beforeRotation, appliedRotation);

            result.CheckThat("受信したカメラ位置の反映",
                positionError < 0.001f,
                $"往復後のカメラ位置がずれています(誤差 {positionError:F4}m / 送信前 {beforePosition} → 実際 {appliedPosition})。" +
                $"HandTrackerRoot(scale={trackerRoot.localScale} pos={trackerRoot.position})の変換が二重に掛かっていないか確認してください");

            result.CheckThat("受信したカメラ回転の反映",
                rotationError < 0.1f,
                $"往復後のカメラ回転がずれています(誤差 {rotationError:F3}度)");

            trackerRoot.localScale = savedScale;
            trackerRoot.position = savedPosition;
            yield return context.Step(2);

            //受信した画角が勝手に戻らないか(受信側のカメラ制御が上書きし返さないこと)
            yield return context.Step(30);
            result.CheckThat("受信した画角の維持",
                Mathf.Abs(cameraManager.ControlCamera.fieldOfView - SenderFov) < 0.01f,
                $"受信した画角が維持されていません({cameraManager.ControlCamera.fieldOfView:F3} / 受信値 {sentFov:F3})");

            //受信した画角はSettings.CameraFOVには入らない(設計上の仕様)。
            //送信側が毎フレーム送るので実害は無いが、受信側のコントロールパネルの表示は自分の値のままになる。
            Debug.Log($"[VMCTest] 受信後のSettings.CameraFOV = {Settings.Current.CameraFOV:F3}" +
                      $"(受信値 {sentFov:F3} / カメラ実値 {cameraManager.ControlCamera.fieldOfView:F3})");

            //--- 3. ライトの受信 ---
            context.Log("3. ライトの受信");
            var lightPosition = new Vector3(1.5f, 2.5f, -3.5f);
            var lightRotation = Quaternion.Euler(30f, 40f, 50f);
            var lightColor = new Color(0.2f, 0.4f, 0.6f, 1f);
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Light", "Light",
                lightPosition.x, lightPosition.y, lightPosition.z,
                lightRotation.x, lightRotation.y, lightRotation.z, lightRotation.w,
                lightColor.r, lightColor.g, lightColor.b, lightColor.a));
            yield return context.Step(3);

            var light = context.Window.MainDirectionalLight;
            var lightTransform = context.Window.MainDirectionalLightTransform;
            result.CheckThat("ライトの受信",
                Vector3.Distance(lightTransform.position, lightPosition) < 0.001f
                && Quaternion.Angle(lightTransform.rotation, lightRotation) < 0.1f
                && Mathf.Abs(light.color.r - lightColor.r) < 0.01f
                && Mathf.Abs(light.color.g - lightColor.g) < 0.01f
                && Mathf.Abs(light.color.b - lightColor.b) < 0.01f,
                $"ライトが反映されていません(pos {lightTransform.position} / color {light.color})");

            //--- 4. 送信周期設定の受信 ---
            context.Log("4. 送信周期設定の受信");
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Period", 2, 3, 4, 5, 6, 7));
            yield return context.Step(2);

            var sender = context.Sender;
            result.CheckThat("送信周期設定の受信",
                sender.periodStatus == 2 && sender.periodRoot == 3 && sender.periodBone == 4
                && sender.periodBlendShape == 5 && sender.periodCamera == 6 && sender.periodDevices == 7,
                $"送信周期が反映されていません(status={sender.periodStatus} root={sender.periodRoot} bone={sender.periodBone} " +
                $"blend={sender.periodBlendShape} cam={sender.periodCamera} dev={sender.periodDevices})");

            //元に戻す(以降の送信が止まらないように)
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Period", 1, 1, 1, 1, 1, 1));
            yield return context.Step(2);

            //--- 5. スルー転送 ---
            context.Log("5. スルー転送の確認");
            context.ClearSent();
            context.Inject(receiver, new uOSC.Message("/VMC/Thru/VMCTest", "hello", 42));
            yield return context.Step(3);

            var forwarded = context.SendCapture.Messages.FirstOrDefault(d => d.address == "/VMC/Thru/VMCTest");
            result.CheckThat("スルー転送",
                forwarded.address == "/VMC/Thru/VMCTest"
                && forwarded.values != null && forwarded.values.Length == 2
                && (string)forwarded.values[0] == "hello" && (int)forwarded.values[1] == 42,
                "/VMC/Thru/* が転送されていません");

            //--- 6. 状態文字列の受信 ---
            context.Log("6. 状態文字列の受信");
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Res", "VMCTestStatus"));
            yield return context.Step(3);
            result.CheckThat("状態文字列の受信",
                receiver.statusString == "VMCTestStatus",
                $"状態文字列が反映されていません(\"{receiver.statusString}\")");

            //--- 7. 情報要求で低頻度情報が即時送信されるか ---
            context.Log("7. 情報要求の受信");
            context.ClearSent();
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Req"));
            yield return context.Step(2);
            result.CheckThat("情報要求の受信",
                context.SendCapture.Messages.Any(d => d.address == "/VMC/Ext/Setting/Color"),
                "/VMC/Ext/Set/Req を受けても低頻度情報が即時送信されていません");

            //--- 8. 入力の受信 ---
            context.Log("8. 入力の受信");
            //MIDIの受信は MidiCCWrapper.Update() で通知されるため、MIDIが有効(GameObjectがアクティブ)である必要がある
            Settings.Current.MidiEnable = true;
            context.Window.midiCCWrapper.gameObject.SetActive(true);
            yield return context.Step(2);

            OVRKeyEventArgs receivedController = null;
            KeyboardEventArgs receivedKey = null;
            var receivedKnob = -1;
            var receivedKnobValue = 0f;

            System.EventHandler<OVRKeyEventArgs> onController = (s, e) => receivedController = e;
            System.EventHandler<KeyboardEventArgs> onKey = (s, e) => receivedKey = e;
            System.Action<int, float> onKnob = (no, value) => { receivedKnob = no; receivedKnobValue = value; };

            SteamVR2Input.Instance.KeyDownEvent += onController;
            KeyboardAction.KeyDownEvent += onKey;
            context.Window.midiCCWrapper.knobUpdateFloatDelegate += onKnob;
            try
            {
                context.Inject(receiver, new uOSC.Message("/VMC/Ext/Con", 1, "VMCTestButton", 1, 0, 0, 0.1f, 0.2f, 0.3f));
                context.Inject(receiver, new uOSC.Message("/VMC/Ext/Key", 1, "A", 65));
                context.Inject(receiver, new uOSC.Message("/VMC/Ext/Midi/CC/Val", 3, 0.75f));
                yield return context.Step(3);
            }
            finally
            {
                SteamVR2Input.Instance.KeyDownEvent -= onController;
                KeyboardAction.KeyDownEvent -= onKey;
                context.Window.midiCCWrapper.knobUpdateFloatDelegate -= onKnob;
            }

            result.CheckThat("コントローラ入力の受信",
                receivedController != null && receivedController.Name == "VMCTestButton" && receivedController.IsLeft,
                $"/VMC/Ext/Con が反映されていません({receivedController?.Name ?? "受信なし"})");

            result.CheckThat("キーボード入力の受信",
                receivedKey != null && receivedKey.KeyCode == 65,
                $"/VMC/Ext/Key が反映されていません({receivedKey?.KeyCode.ToString() ?? "受信なし"})");

            result.CheckThat("MIDI入力の受信",
                receivedKnob == 3 && Mathf.Abs(receivedKnobValue - 0.75f) < 0.01f,
                $"/VMC/Ext/Midi/CC/Val が反映されていません(knob={receivedKnob} value={receivedKnobValue:F3})");

            //--- 9. リモートキャリブレーション ---
            context.Log("9. リモートキャリブレーションの受信");
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Calib/Ready"));
            yield return context.Step(5);
            result.CheckThat("キャリブレーション準備の受信",
                IKManager.Instance.CalibrationState == CalibrationState.WaitingForCalibrating,
                $"/VMC/Ext/Set/Calib/Ready でキャリブレーション待機に入りません(state={IKManager.Instance.CalibrationState})");

            //仕様の mode は PipeCommands.CalibrateType の値。0 = 通常(Default)
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Calib/Exec", (int)PipeCommands.CalibrateType.Default));
            //受信側は Invoke("EndCalibrate", 2f) で完了させるため、実時間で2秒ぶん待つ
            yield return context.WaitUntilOrTimeout(
                () => IKManager.Instance.CalibrationState == CalibrationState.Calibrated, 900);

            result.CheckThat("キャリブレーション実行の受信",
                IKManager.Instance.CalibrationState == CalibrationState.Calibrated
                && IKManager.Instance.LastCalibrateType == PipeCommands.CalibrateType.Default,
                $"/VMC/Ext/Set/Calib/Exec でキャリブレーションが完了しません" +
                $"(state={IKManager.Instance.CalibrationState} type={IKManager.Instance.LastCalibrateType})");

            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);
            result.CheckSnapshot(context, context.Capture("01_after_remote_calibration", includeSent: false));

            context.DisableSender();
        }
    }
}
