using sh_akira;
using sh_akira.OVRTracking;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using UnityEngine;
using UnityNamedPipe;
using VRM;
using static Assets.Scripts.NativeMethods;
#if UNITY_EDITOR   // エディタ上でしか動きません。
using UnityEditor;
#endif

public class ControlWPFWindow : MonoBehaviour
{
    public TrackerHandler handler = null;

    public CameraLookTarget CalibrationCamera;

    public GameObject LeftHandCamera;
    public GameObject RightHandCamera;

    public Camera FreeCamera;
    public Camera FrontCamera;
    public Camera BackCamera;

    public Renderer BackgroundRenderer;

    public GameObject GridCanvas;

    public DynamicOVRLipSync LipSync;

    public FaceController faceController;

    public OVRControllerAction controllerAction;

    public MainThreadInvoker mainThreadInvoker;

    private NamedPipeServer server;
    private string pipeName = Guid.NewGuid().ToString();

    private GameObject CurrentModel = null;

    private RootMotion.FinalIK.VRIK vrik = null;

    private Camera currentCamera;
    private CameraLookTarget currentCameraLookTarget = null;

    private Animator animator = null;

    private enum MouseButtons
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }

    private uint defaultWindowStyle;
    private uint defaultExWindowStyle;

    // Use this for initialization
    void Start()
    {
#if UNITY_EDITOR   // エディタ上でしか動きません。
        EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;//UnityエディタでPlayやStopした時の状態変化イベント
        pipeName = "VMCTest";
#else
        pipeName = "VMCpipe" + Guid.NewGuid().ToString();
#endif
        server = new NamedPipeServer();
        server.ReceivedEvent += Server_Received;
        server.Start(pipeName);

        //start control panel
#if !UNITY_EDITOR
        ExecuteControlPanel();
#endif

        currentCamera = FreeCamera;

        CurrentSettings.BackgroundColor = BackgroundRenderer.material.color;
        CurrentSettings.CustomBackgroundColor = BackgroundRenderer.material.color;

        controllerAction.KeyDownEvent += ControllerAction_KeyDown;
        controllerAction.KeyUpEvent += ControllerAction_KeyUp;
        controllerAction.AxisChangedEvent += ControllerAction_AxisChanged;

        KeyboardAction.KeyDownEvent += KeyboardAction_KeyDown;
        KeyboardAction.KeyUpEvent += KeyboardAction_KeyUp;

    }

    private bool ControlPanelExecuted = false;
    private System.Diagnostics.Process controlPanelProcess = null;
    private void ExecuteControlPanel()
    {
        if (ControlPanelExecuted == false)
        {
            var path = Application.dataPath + "/../ControlPanel/VirtualMotionCaptureControlPanel.exe";
            controlPanelProcess = new System.Diagnostics.Process();
            controlPanelProcess.StartInfo.FileName = path;
            controlPanelProcess.StartInfo.Arguments = "/pipeName " + pipeName;
            controlPanelProcess.EnableRaisingEvents = true;
            controlPanelProcess.Exited += ControlPanelProcess_Exited;
            controlPanelProcess.Start();
            ControlPanelExecuted = true;
        }
    }

    private void ControlPanelProcess_Exited(object sender, EventArgs e)
    {
        ControlPanelExecuted = false;
        controlPanelProcess.Dispose();
    }

    private void OnApplicationQuit()
    {
        server.ReceivedEvent -= Server_Received;
        server?.Stop();
        KeyboardAction.KeyDownEvent -= KeyboardAction_KeyDown;
        KeyboardAction.KeyUpEvent -= KeyboardAction_KeyUp;

        controllerAction.KeyDownEvent -= ControllerAction_KeyDown;
        controllerAction.KeyUpEvent -= ControllerAction_KeyUp;
        controllerAction.AxisChangedEvent -= ControllerAction_AxisChanged;
    }

#if UNITY_EDITOR   // エディタ上でしか動きません。
    private void EditorApplication_playModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            server.ReceivedEvent -= Server_Received;
            server.Stop();
            //OpenVRWrapper.Instance.Close();
            //SteamVR.SafeDispose();
        }
    }
#endif

    private async void Server_Received(object sender, DataReceivedEventArgs e)
    {
        await mainThreadInvoker.InvokeAsync(async () =>
        {
            if (e.CommandType == typeof(PipeCommands.LoadVRM))
            {
                var d = (PipeCommands.LoadVRM)e.Data;
                await server.SendCommandAsync(new PipeCommands.ReturnLoadVRM { Data = LoadVRM() }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.ImportVRM))
            {
                var d = (PipeCommands.ImportVRM)e.Data;
                ImportVRM(d.Path, d.ImportForCalibration);
            }

            else if (e.CommandType == typeof(PipeCommands.Calibrate))
            {
                Calibrate();
            }
            else if (e.CommandType == typeof(PipeCommands.EndCalibrate))
            {
                EndCalibrate();
            }

            else if (e.CommandType == typeof(PipeCommands.SetLipSyncEnable))
            {
                var d = (PipeCommands.SetLipSyncEnable)e.Data;
                SetLipSyncEnable(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.GetLipSyncDevices))
            {
                var d = (PipeCommands.GetLipSyncDevices)e.Data;
                await server.SendCommandAsync(new PipeCommands.ReturnGetLipSyncDevices { Devices = GetLipSyncDevices() }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetLipSyncDevice))
            {
                var d = (PipeCommands.SetLipSyncDevice)e.Data;
                SetLipSyncDevice(d.device);
            }
            else if (e.CommandType == typeof(PipeCommands.SetLipSyncGain))
            {
                var d = (PipeCommands.SetLipSyncGain)e.Data;
                SetLipSyncGain(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetLipSyncMaxWeightEnable))
            {
                var d = (PipeCommands.SetLipSyncMaxWeightEnable)e.Data;
                SetLipSyncMaxWeightEnable(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.SetLipSyncWeightThreashold))
            {
                var d = (PipeCommands.SetLipSyncWeightThreashold)e.Data;
                SetLipSyncWeightThreashold(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetLipSyncMaxWeightEmphasis))
            {
                var d = (PipeCommands.SetLipSyncMaxWeightEmphasis)e.Data;
                SetLipSyncMaxWeightEmphasis(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.ChangeBackgroundColor))
            {
                var d = (PipeCommands.ChangeBackgroundColor)e.Data;
                ChangeBackgroundColor(d.r, d.g, d.b, d.isCustom);
            }
            else if (e.CommandType == typeof(PipeCommands.SetBackgroundTransparent))
            {
                SetBackgroundTransparent();
            }
            else if (e.CommandType == typeof(PipeCommands.SetWindowBorder))
            {
                var d = (PipeCommands.SetWindowBorder)e.Data;
                SetWindowBorder(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.SetWindowTopMost))
            {
                var d = (PipeCommands.SetWindowTopMost)e.Data;
                SetWindowTopMost(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.SetWindowClickThrough))
            {
                var d = (PipeCommands.SetWindowClickThrough)e.Data;
                SetWindowClickThrough(d.enable);
            }

            else if (e.CommandType == typeof(PipeCommands.ChangeCamera))
            {
                var d = (PipeCommands.ChangeCamera)e.Data;
                ChangeCamera(d.type);
            }
            else if (e.CommandType == typeof(PipeCommands.SetGridVisible))
            {
                var d = (PipeCommands.SetGridVisible)e.Data;
                SetGridVisible(d.enable);
            }

            else if (e.CommandType == typeof(PipeCommands.SetAutoBlinkEnable))
            {
                var d = (PipeCommands.SetAutoBlinkEnable)e.Data;
                SetAutoBlinkEnable(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.SetBlinkTimeMin))
            {
                var d = (PipeCommands.SetBlinkTimeMin)e.Data;
                SetBlinkTimeMin(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetBlinkTimeMax))
            {
                var d = (PipeCommands.SetBlinkTimeMax)e.Data;
                SetBlinkTimeMax(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetCloseAnimationTime))
            {
                var d = (PipeCommands.SetCloseAnimationTime)e.Data;
                SetCloseAnimationTime(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetOpenAnimationTime))
            {
                var d = (PipeCommands.SetOpenAnimationTime)e.Data;
                SetOpenAnimationTime(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetClosingTime))
            {
                var d = (PipeCommands.SetClosingTime)e.Data;
                SetClosingTime(d.value);
            }
            else if (e.CommandType == typeof(PipeCommands.SetDefaultFace))
            {
                var d = (PipeCommands.SetDefaultFace)e.Data;
                SetDefaultFace(d.face);
            }
            else if (e.CommandType == typeof(PipeCommands.LoadSettings))
            {
                LoadSettings();
            }
            else if (e.CommandType == typeof(PipeCommands.SaveSettings))
            {
                SaveSettings();
            }
            else if (e.CommandType == typeof(PipeCommands.SetControllerTouchPadPoints))
            {
                var d = (PipeCommands.SetControllerTouchPadPoints)e.Data;
                CurrentSettings.IsOculus = d.IsOculus;
                CurrentSettings.LeftCenterEnable = d.LeftCenterEnable;
                CurrentSettings.RightCenterEnable = d.RightCenterEnable;
                CurrentSettings.LeftTouchPadPoints = d.LeftPoints;
                CurrentSettings.RightTouchPadPoints = d.RightPoints;
            }
            else if (e.CommandType == typeof(PipeCommands.StartHandCamera))
            {
                var d = (PipeCommands.StartHandCamera)e.Data;
                StartHandCamera(d.IsLeft);
            }
            else if (e.CommandType == typeof(PipeCommands.EndHandCamera))
            {
                EndHandCamera();
            }
            else if (e.CommandType == typeof(PipeCommands.SetHandAngle))
            {
                var d = (PipeCommands.SetHandAngle)e.Data;
                SetHandAngle(d.LeftEnable, d.RightEnable, d.HandAngles);
            }
            else if (e.CommandType == typeof(PipeCommands.StartKeyConfig))
            {
                doKeyConfig = true;
                faceController.StartSetting();
            }
            else if (e.CommandType == typeof(PipeCommands.EndKeyConfig))
            {
                faceController.EndSetting();
                doKeyConfig = false;
            }
            else if (e.CommandType == typeof(PipeCommands.SetKeyActions))
            {
                var d = (PipeCommands.SetKeyActions)e.Data;
                CurrentSettings.KeyActions = d.KeyActions;
            }
            else if (e.CommandType == typeof(PipeCommands.GetFaceKeys))
            {
                await server.SendCommandAsync(new PipeCommands.ReturnFaceKeys { Keys = faceController.BlendShapeKeys }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetFace))
            {
                var d = (PipeCommands.SetFace)e.Data;
                faceController.SetFace(d.Keys, d.Strength, true);
            }
            else if (e.CommandType == typeof(PipeCommands.ExitControlPanel))
            {
                ControlPanelExecuted = false;
            }
            else if (e.CommandType == typeof(PipeCommands.SetHandRotations))
            {
                var d = (PipeCommands.SetHandRotations)e.Data;
                CurrentSettings.LeftHandRotation = d.LeftHandRotation;
                CurrentSettings.RightHandRotation = d.RightHandRotation;
                UpdateHandRotation();
            }
            else if (e.CommandType == typeof(PipeCommands.SetExternalCameraConfig))
            {
                var d = (PipeCommands.SetExternalCameraConfig)e.Data;
                //フリーカメラに変更
                ChangeCamera(CameraTypes.Free);
                //externalcamera.cfgは3つ目のコントローラー基準のポジション
                handler.CameraControllerIndex = d.ControllerIndex;
                //指定のコントローラーの子にして座標指定
                currentCamera.transform.SetParent(handler.CameraControllerObject.transform);
                currentCamera.transform.localPosition = new Vector3(d.x, d.y, d.z);
                currentCamera.transform.localRotation = Quaternion.Euler(d.rx, d.ry, d.rz);
                currentCamera.fieldOfView = d.fov;
                //コントローラーは動くのでカメラ位置の保存はできない
                //if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                //CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
            }
            else if (e.CommandType == typeof(PipeCommands.LoadCurrentSettings))
            {
                if (isFirstTimeExecute)
                {
                    isFirstTimeExecute = false;
                    //起動時は初期設定ロード
                    LoadSettings(true, true);
                }
                else
                {
                    LoadSettings(true, false);
                }
            }
        });
    }

    private bool isFirstTimeExecute = true;

    #region VRM

    private VRMData LoadVRM()
    {
        var path = WindowsDialogs.OpenFileDialog("VRMファイル選択", ".vrm");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        var vrmdata = new VRMData();
        vrmdata.FilePath = path;
        var context = new VRMImporterContext(path);

        var bytes = File.ReadAllBytes(path);

        // GLB形式でJSONを取得しParseします
        context.ParseVrm(bytes);

        // metaを取得
        var meta = context.ReadMeta(true);
        //サムネイル
        if (meta.Thumbnail != null)
        {
            vrmdata.ThumbnailPNGBytes = meta.Thumbnail.EncodeToPNG(); //Or SaveAsPng( memoryStream, texture.Width, texture.Height )
        }
        //Info
        vrmdata.Title = meta.Title;
        vrmdata.Version = meta.Version;
        vrmdata.Author = meta.Author;
        vrmdata.ContactInformation = meta.ContactInformation;
        vrmdata.Reference = meta.Reference;

        // Permission
        vrmdata.AllowedUser = (UnityNamedPipe.AllowedUser)meta.AllowedUser;
        vrmdata.ViolentUssage = (UnityNamedPipe.UssageLicense)meta.ViolentUssage;
        vrmdata.SexualUssage = (UnityNamedPipe.UssageLicense)meta.SexualUssage;
        vrmdata.CommercialUssage = (UnityNamedPipe.UssageLicense)meta.CommercialUssage;
        vrmdata.OtherPermissionUrl = meta.OtherPermissionUrl;

        // Distribution License
        vrmdata.LicenseType = (UnityNamedPipe.LicenseType)meta.LicenseType;
        vrmdata.OtherLicenseUrl = meta.OtherLicenseUrl;
        /*
        // ParseしたJSONをシーンオブジェクトに変換していく
        var now = Time.time;
        var go = await VRMImporter.LoadVrmAsync(context);

        var delta = Time.time - now;
        Debug.LogFormat("LoadVrmAsync {0:0.0} seconds", delta);
        //OnLoaded(go);
        */
        return vrmdata;
    }

    private async void ImportVRM(string path, bool ImportForCalibration)
    {
        CurrentSettings.VRMPath = path;
        var context = new VRMImporterContext(path);

        var bytes = File.ReadAllBytes(path);

        // GLB形式でJSONを取得しParseします
        context.ParseVrm(bytes);

        if (CurrentModel != null)
        {
            if (LeftHandCamera != null)
            {
                LeftHandCamera.transform.SetParent(null);
            }
            if (RightHandCamera != null)
            {
                RightHandCamera.transform.SetParent(null);
            }
            CurrentModel.transform.SetParent(null);
            CurrentModel.SetActive(false);
            Destroy(CurrentModel);
            CurrentModel = null;
        }
        // ParseしたJSONをシーンオブジェクトに変換していく
        CurrentModel = await VRMImporter.LoadVrmAsync(context);

        //モデルのSkinnedMeshRendererがカリングされないように、すべてのオプション変更
        foreach(var renderer in CurrentModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.updateWhenOffscreen = true;
        }

        //LipSync
        LipSync.ImportVRMmodel(CurrentModel);
        //まばたき
        faceController.ImportVRMmodel(CurrentModel);

        CurrentModel.transform.SetParent(transform, false);

        SetVRIK(CurrentModel);
        animator = CurrentModel.GetComponent<Animator>();
        if (animator != null)
        {//指のデフォルト角度取得
            FingerTransforms.Clear();
            FingerDefaultVectors.Clear();
            foreach (var bone in FingerBones)
            {
                var transform = animator.GetBoneTransform(bone);
                FingerTransforms.Add(transform);
                if (transform == null)
                {
                    FingerDefaultVectors.Add(Vector3.zero);
                }
                else
                {
                    FingerDefaultVectors.Add(new Vector3(transform.rotation.x, transform.rotation.y, transform.rotation.z));
                }
            }

            //設定用両手のカメラをモデルにアタッチ
            if (LeftHandCamera != null)
            {
                LeftHandCamera.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.LeftHand));
                LeftHandCamera.transform.localPosition = new Vector3(-0.05f, -0.09f, 0.1f);
                LeftHandCamera.transform.rotation = Quaternion.Euler(-140f, 0f, 90f);
                LeftHandCamera.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }
            if (RightHandCamera != null)
            {
                RightHandCamera.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand));
                RightHandCamera.transform.localPosition = new Vector3(0.05f, -0.09f, 0.1f);
                RightHandCamera.transform.rotation = Quaternion.Euler(-140f, 0f, -90f);
                RightHandCamera.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }

        }
        if (ImportForCalibration == false)
        {
            SetCameraLookTarget();
            //SetTrackersToVRIK();
        }
        else
        {
            if (animator != null)
            {
                if (CalibrationCamera != null)
                {
                    CalibrationCamera.Target = animator.GetBoneTransform(HumanBodyBones.Head);
                    CalibrationCamera.gameObject.SetActive(true);
                }
            }
        }
    }

    #endregion

    #region Calibration

    private void SetVRIK(GameObject model)
    {
        vrik = model.AddComponent<RootMotion.FinalIK.VRIK>();
        vrik.solver.IKPositionWeight = 0f;
        vrik.solver.leftArm.stretchCurve = new AnimationCurve();
        vrik.solver.rightArm.stretchCurve = new AnimationCurve();
        vrik.UpdateSolverExternal();
    }

    Transform bodyTracker = null;
    Transform leftFootTracker = null;
    Transform rightFootTracker = null;
    Transform leftHandTracker = null;
    Transform rightHandTracker = null;

    private void Calibrate()
    {
        Transform headTracker = handler.HMDObject.transform;// AddCalibrateTransform(handler.HMDObject.transform, TrackerNums.Zero);
        var controllerTransforms = (new Transform[] { handler.LeftControllerObject.transform, handler.RightControllerObject.transform }).Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.transform.position - headTracker.position), transform = d.transform }).OrderBy(d => d.pos.x).Select(d => d.transform);
        leftHandTracker = controllerTransforms.ElementAtOrDefault(0);// AddCalibrateTransform(handler.LeftControllerObject.transform, TrackerNums.Zero);
        rightHandTracker = controllerTransforms.ElementAtOrDefault(1);// AddCalibrateTransform(handler.RightControllerObject.transform, TrackerNums.Zero);
        var trackerTransforms = handler.Trackers.Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.transform.position - headTracker.position), transform = d.transform }).ToList();
        if (handler.Trackers.Count >= 3)
        {
            //トラッカー3つ以上あれば腰も設定
            bodyTracker = trackerTransforms.OrderByDescending(d => d.pos.y).Select(d => d.transform).First();// handler.Trackers[0].transform;// AddCalibrateTransform(handler.Trackers[0].transform, TrackerNums.Zero);
            leftFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[2].transform;// AddCalibrateTransform(handler.Trackers[2].transform, TrackerNums.Zero);
            rightFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[1].transform;// AddCalibrateTransform(handler.Trackers[1].transform, TrackerNums.Zero);
        }
        else if (handler.Trackers.Count >= 2)
        {
            //トラッカーが2つだけなら両足
            leftFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[1].transform;// AddCalibrateTransform(handler.Trackers[1].transform, TrackerNums.Zero);
            rightFootTracker = trackerTransforms.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.transform).First();// handler.Trackers[0].transform;// AddCalibrateTransform(handler.Trackers[0].transform, TrackerNums.Zero);
        }
        else if (handler.Trackers.Count >= 1)
        {
            //トラッカーが1つだけなら腰だけ
            bodyTracker = handler.Trackers[0].transform;// AddCalibrateTransform(handler.Trackers[0].transform, TrackerNums.Zero);
        }
        //DoCalibrate(vrik, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        //DoCalibrate2(vrik, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        vrik.solver.IKPositionWeight = 1.0f;
        var settings = new RootMotion.FinalIK.VRIKCalibrator.Settings() { headOffset = new Vector3(0f, -0.15f, -0.15f), handOffset = new Vector3(0f, -0.03f, -0.07f) };
        Calibrator.Calibrate(vrik, settings, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        Calibrator.Calibrate(vrik, settings, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        Calibrator.Calibrate(vrik, settings, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker);
        if (handler.Trackers.Count == 1)
        {
            vrik.solver.plantFeet = true;
            vrik.solver.locomotion.weight = 1.0f;
            var rootController = vrik.references.root.GetComponent<RootMotion.FinalIK.VRIKRootController>();
            if (rootController != null) GameObject.Destroy(rootController);
        }
        CurrentSettings.headTracker = StoreTransform.Create(headTracker);
        CurrentSettings.bodyTracker = StoreTransform.Create(bodyTracker);
        CurrentSettings.leftHandTracker = StoreTransform.Create(leftHandTracker);
        CurrentSettings.rightHandTracker = StoreTransform.Create(rightHandTracker);
        CurrentSettings.leftFootTracker = StoreTransform.Create(leftFootTracker);
        CurrentSettings.rightFootTracker = StoreTransform.Create(rightFootTracker);
    }

    private void EndCalibrate()
    {
        if (CalibrationCamera != null)
        {
            CalibrationCamera.gameObject.SetActive(false);
        }
        UpdateHandRotation();
        SetCameraLookTarget();
        //SetTrackersToVRIK();
    }

    #endregion

    #region LipSync

    private void SetLipSyncEnable(bool enable)
    {
        LipSync.EnableLipSync = enable;
        CurrentSettings.LipSyncEnable = enable;
    }

    private string[] GetLipSyncDevices()
    {
        return LipSync.GetMicrophoneDevices();
    }

    private void SetLipSyncDevice(string device)
    {
        LipSync.SetMicrophoneDevice(device);
        CurrentSettings.LipSyncDevice = device;
    }

    private void SetLipSyncGain(float gain)
    {
        if (gain < 1.0f) gain = 1.0f;
        if (gain > 256.0f) gain = 256.0f;
        LipSync.Gain = gain;
        CurrentSettings.LipSyncGain = gain;
    }

    private void SetLipSyncMaxWeightEnable(bool enable)
    {
        LipSync.MaxWeightEnable = enable;
        CurrentSettings.LipSyncMaxWeightEnable = enable;
    }

    private void SetLipSyncWeightThreashold(float threashold)
    {
        LipSync.WeightThreashold = threashold;
        CurrentSettings.LipSyncWeightThreashold = threashold;
    }

    private void SetLipSyncMaxWeightEmphasis(bool enable)
    {
        LipSync.MaxWeightEmphasis = enable;
        CurrentSettings.LipSyncMaxWeightEmphasis = enable;
    }

    #endregion

    #region Color

    private void ChangeBackgroundColor(float r, float g, float b, bool isCustom)
    {
        BackgroundRenderer.material.color = new Color(r, g, b, 1.0f);
        CurrentSettings.BackgroundColor = BackgroundRenderer.material.color;
        if (isCustom) CurrentSettings.CustomBackgroundColor = BackgroundRenderer.material.color;
        CurrentSettings.IsTransparent = false;
        SetDwmTransparent(false);
    }

    private void SetBackgroundTransparent()
    {
        CurrentSettings.IsTransparent = true;
#if !UNITY_EDITOR   // エディタ上では動きません。
        BackgroundRenderer.material.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        SetDwmTransparent(true);
#endif
    }

    void SetWindowBorder(bool enable)
    {
        CurrentSettings.HideBorder = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        var hwnd = GetUnityWindowHandle();
        //var hwnd = GetActiveWindow();
        if (enable)
        {
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE); //ウインドウ枠の削除
        }
        else
        {
            SetWindowLong(hwnd, GWL_STYLE, defaultWindowStyle);
        }
#endif
    }
    void SetWindowTopMost(bool enable)
    {
        CurrentSettings.IsTopMost = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        SetUnityWindowTopMost(enable);
#endif
    }

    void SetWindowClickThrough(bool enable)
    {
        CurrentSettings.HideBorder = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        var hwnd = GetUnityWindowHandle();
        //var hwnd = GetActiveWindow();
        if (enable)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT); //クリックを透過する
        }
        else
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, defaultExWindowStyle);
        }
#endif
    }

    void OnRenderImage(RenderTexture from, RenderTexture to)
    {
        Graphics.Blit(from, to, BackgroundRenderer.material);
    }

    #endregion

    #region CameraControl


    private Vector3 cameraMouseOldPos; // マウスの位置を保存する変数

    private void ChangeCamera(CameraTypes type)
    {
        if (type == CameraTypes.Free)
        {
            SetCameraEnable(FreeCamera);
        }
        else if (type == CameraTypes.Front)
        {
            SetCameraEnable(FrontCamera);
        }
        else if (type == CameraTypes.Back)
        {
            SetCameraEnable(BackCamera);
        }
        CurrentSettings.CameraType = type;
    }

    private void SetCameraEnable(Camera camera)
    {
        if (camera != null)
        {
            camera.gameObject.SetActive(true);
            if (currentCamera != null && currentCamera != camera) currentCamera.gameObject.SetActive(false);
            currentCamera = camera;
            currentCameraLookTarget = camera.gameObject.GetComponent<CameraLookTarget>();
        }

    }

    private Camera saveCurrentCamera = null;

    private void StartHandCamera(bool isLeft)
    {
        RightHandCamera.SetActive(isLeft == false);
        LeftHandCamera.SetActive(isLeft == true);
        saveCurrentCamera = currentCamera;
        if (saveCurrentCamera != null) saveCurrentCamera.gameObject.SetActive(false);
    }

    private void EndHandCamera()
    {
        RightHandCamera.SetActive(false);
        LeftHandCamera.SetActive(false);
        if (saveCurrentCamera != null) saveCurrentCamera.gameObject.SetActive(true);
        saveCurrentCamera = null;
    }

    private void SetCameraLookTarget()
    {
        animator = CurrentModel?.GetComponent<Animator>();
        if (animator != null)
        {
            var spineTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
            var calcPosition = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.Head).position, spineTransform.position, 0.5f);
            var gameObject = new GameObject("CameraLook");
            gameObject.transform.position = calcPosition;
            gameObject.transform.rotation = spineTransform.rotation;
            gameObject.transform.parent = CurrentModel.transform;
            var lookTarget = FrontCamera.GetComponent<CameraLookTarget>();
            if (lookTarget != null)
            {
                lookTarget.Target = gameObject.transform;
            }
            lookTarget = BackCamera.GetComponent<CameraLookTarget>();
            if (lookTarget != null)
            {
                lookTarget.Target = gameObject.transform;
            }
        }
    }

    private void SaveLookTarget(Camera camera)
    {
        if (camera == FrontCamera)
        {
            if (CurrentSettings.FrontCameraLookTargetSettings == null)
            {
                CurrentSettings.FrontCameraLookTargetSettings = LookTargetSettings.Create(currentCameraLookTarget);
            }
            else
            {
                CurrentSettings.FrontCameraLookTargetSettings.Set(currentCameraLookTarget);
            }
        }
        else if (camera == BackCamera)
        {
            if (CurrentSettings.BackCameraLookTargetSettings == null)
            {
                CurrentSettings.BackCameraLookTargetSettings = LookTargetSettings.Create(currentCameraLookTarget);
            }
            else
            {
                CurrentSettings.BackCameraLookTargetSettings.Set(currentCameraLookTarget);
            }
        }
    }

    // マウス関係のイベント
    private void CameraMouseEvent()
    {
        float delta = Input.GetAxis("Mouse ScrollWheel");
        if (delta != 0.0f)
        {
            if (currentCameraLookTarget == null) //フリーカメラ
            {
                currentCamera.transform.position += currentCamera.transform.forward * delta;
                if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
            }
            else //固定カメラ
            {
                currentCameraLookTarget.Distance += delta;
                SaveLookTarget(currentCamera);
            }
        }

        var mousePos = Input.mousePosition;

        // 押されたとき
        if (Input.GetMouseButtonDown((int)MouseButtons.Right) || Input.GetMouseButtonDown((int)MouseButtons.Center))
            cameraMouseOldPos = mousePos;

        Vector3 diff = mousePos - cameraMouseOldPos;

        // 差分の長さが極小数より小さかったら、ドラッグしていないと判断する
        if (diff.magnitude >= Vector3.kEpsilon)
        {

            if (Input.GetMouseButton((int)MouseButtons.Center))
            { // 注視点
                if (currentCameraLookTarget == null) //フリーカメラ
                {
                    currentCamera.transform.Translate(-diff * Time.deltaTime * 1.1f);
                    if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                    CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
                }
                else //固定カメラ
                {
                    currentCameraLookTarget.Offset += new Vector3(0, -diff.y, 0) * Time.deltaTime * 1.1f;
                    SaveLookTarget(currentCamera);
                }
            }
            else if (Input.GetMouseButton((int)MouseButtons.Right))
            { // 回転
                currentCamera.transform.RotateAround(currentCamera.transform.position, currentCamera.transform.right, -diff.y * Time.deltaTime * 30.0f);
                currentCamera.transform.RotateAround(currentCamera.transform.position, Vector3.up, diff.x * Time.deltaTime * 30.0f);
                if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
                CurrentSettings.FreeCameraTransform.SetRotation(currentCamera.transform);
            }

            this.cameraMouseOldPos = mousePos;
        }
        return;
    }

    private void SetGridVisible(bool enable)
    {
        GridCanvas?.SetActive(enable);
        CurrentSettings.ShowCameraGrid = enable;
    }

    #endregion

    #region BlinkControl
    void SetAutoBlinkEnable(bool enable)
    {
        faceController.EnableBlink = enable;
        CurrentSettings.AutoBlinkEnable = enable;
    }
    void SetBlinkTimeMin(float time)
    {
        faceController.BlinkTimeMin = time;
        CurrentSettings.BlinkTimeMin = time;
    }
    void SetBlinkTimeMax(float time)
    {
        faceController.BlinkTimeMax = time;
        CurrentSettings.BlinkTimeMax = time;
    }
    void SetCloseAnimationTime(float time)
    {
        faceController.CloseAnimationTime = time;
        CurrentSettings.CloseAnimationTime = time;
    }
    void SetOpenAnimationTime(float time)
    {
        faceController.OpenAnimationTime = time;
        CurrentSettings.OpenAnimationTime = time;
    }
    void SetClosingTime(float time)
    {
        faceController.ClosingTime = time;
        CurrentSettings.ClosingTime = time;
    }

    private Dictionary<string, BlendShapePreset> BlendShapeNameDictionary = new Dictionary<string, BlendShapePreset>
    {
        { "通常(NEUTRAL)", BlendShapePreset.Neutral },
        { "喜(JOY)", BlendShapePreset.Joy },
        { "怒(ANGRY)", BlendShapePreset.Angry },
        { "哀(SORROW)", BlendShapePreset.Sorrow },
        { "楽(FUN)", BlendShapePreset.Fun },
        { "上見(LOOKUP)", BlendShapePreset.LookUp },
        { "下見(LOOKDOWN)", BlendShapePreset.LookDown },
        { "左見(LOOKLEFT)", BlendShapePreset.LookLeft },
        { "右見(LOOKRIGHT)", BlendShapePreset.LookRight },
    };

    void SetDefaultFace(string face)
    {
        faceController.StopBlink = false;
        if (string.IsNullOrEmpty(face))
        {
        }
        else if (BlendShapeNameDictionary.ContainsKey(face))
        {
            faceController.DefaultFace = BlendShapeNameDictionary[face];
            faceController.FacePresetName = null;
        }
        else
        {
            faceController.DefaultFace = BlendShapePreset.Unknown;
            faceController.FacePresetName = face;
        }
    }
    #endregion

    #region HandFaceControll

    private bool doKeyConfig = false;

    private async void ControllerAction_KeyDown(object sender, OVRKeyEventArgs e)
    {
        //win.KeyDownEvent{ value = win, new KeyEventArgs((EVRButtonId)e.ButtonId, e.Axis.x, e.Axis.y, e.IsLeft));

        var config = new KeyConfig();
        config.type = KeyTypes.Controller;
        config.actionType = KeyActionTypes.Hand;
        config.keyCode = (int)e.ButtonId;
        config.isLeft = e.IsLeft;
        config.keyIndex = e.IsAxis == false ? -1 : e.ButtonId == Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad ? NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y) : 0;
        if (e.IsAxis)
        {
            if (e.IsLeft) lastLeftAxisPoint = config.keyIndex;
            else lastRightAxisPoint = config.keyIndex;
        }
        if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
        else CheckKey(config, true);
    }

    private async void ControllerAction_KeyUp(object sender, OVRKeyEventArgs e)
    {
        //win.KeyUpEvent{ value = win, new KeyEventArgs((EVRButtonId)e.ButtonId, e.Axis.x, e.Axis.y, e.IsLeft));
        var config = new KeyConfig();
        config.type = KeyTypes.Controller;
        config.actionType = KeyActionTypes.Hand;
        config.keyCode = (int)e.ButtonId;
        config.isLeft = e.IsLeft;
        config.keyIndex = e.IsAxis == false ? -1 : e.ButtonId == Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad ? NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y) : 0;
        if (e.IsAxis && config.keyIndex != (e.IsLeft ? lastLeftAxisPoint : lastRightAxisPoint))
        {//タッチパッド離した瞬間違うポイントだった場合
            var newindex = config.keyIndex;
            config.keyIndex = (e.IsLeft ? lastLeftAxisPoint : lastRightAxisPoint);
            //前のキーを離す
            if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            else CheckKey(config, false);
            config.keyIndex = newindex;
            //新しいキーを押す
            if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            else CheckKey(config, true);
        }
        if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
        else CheckKey(config, false);
    }

    private int lastLeftAxisPoint = -1;
    private int lastRightAxisPoint = -1;
    //タッチパッドやアナログスティックの変動
    private async void ControllerAction_AxisChanged(object sender, OVRKeyEventArgs e)
    {
        if (e.IsAxis == false) return;
        var newindex = NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y);
        if ((e.IsLeft ? lastLeftAxisPoint : lastRightAxisPoint) != newindex)
        {//ドラッグで隣の領域に入った場合
            var config = new KeyConfig();
            config.type = KeyTypes.Controller;
            config.actionType = KeyActionTypes.Hand;
            config.keyCode = (int)e.ButtonId;
            config.isLeft = e.IsLeft;
            config.keyIndex = (e.IsLeft ? lastLeftAxisPoint : lastRightAxisPoint);
            //前のキーを離す
            if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            else CheckKey(config, false);
            config.keyIndex = newindex;
            //新しいキーを押す
            if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            else CheckKey(config, true);
            if (e.IsLeft) lastLeftAxisPoint = newindex;
            else lastRightAxisPoint = newindex;
        }
    }


    private async void KeyboardAction_KeyDown(object sender, KeyboardEventArgs e)
    {
        var config = new KeyConfig();
        config.type = KeyTypes.Keyboard;
        config.actionType = KeyActionTypes.Face;
        config.keyCode = e.KeyCode;
        config.keyName = e.KeyName;
        if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
        else CheckKey(config, true);
    }

    private async void KeyboardAction_KeyUp(object sender, KeyboardEventArgs e)
    {
        var config = new KeyConfig();
        config.type = KeyTypes.Keyboard;
        config.actionType = KeyActionTypes.Face;
        config.keyCode = e.KeyCode;
        config.keyName = e.KeyName;
        if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
        else CheckKey(config, false);
    }

    private int NearestPointIndex(bool isLeft, float x, float y)
    {
        //Debug.Log($"SearchNearestPoint:{x},{y},{isLeft}");
        int index = 0;
        var points = isLeft ? CurrentSettings.LeftTouchPadPoints : CurrentSettings.RightTouchPadPoints;
        if (points == null) return 0; //未設定時は一つ
        var centerEnable = isLeft ? CurrentSettings.LeftCenterEnable : CurrentSettings.RightCenterEnable;
        if (centerEnable) //センターキー有効時
        {
            var point_distance = x * x + y * y;
            var r = 2.0f / 5.0f; //半径
            var r2 = r * r;
            if (point_distance < r2) //円内
            {
                index = points.Count + 1;
                return index;
            }
        }
        double maxlength = double.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            double length = Math.Sqrt(Math.Pow(x - p.x, 2) + Math.Pow(y - p.y, 2));
            if (maxlength > length)
            {
                maxlength = length;
                index = i + 1;
            }
        }
        return index;
    }

    private List<KeyConfig> CurrentKeyConfigs = new List<KeyConfig>();
    private List<KeyAction> CurrentKeyUpActions = new List<KeyAction>();

    private void CheckKey(KeyConfig config, bool isKeyDown)
    {
        if (isKeyDown)
        {
            //CurrentKeyConfigs.Clear();
            CurrentKeyConfigs.Add(config);
            Debug.Log("押:" + config.ToString());
            var doKeyActions = new List<KeyAction>();
            foreach (var action in CurrentSettings.KeyActions?.OrderBy(d => d.KeyConfigs.Count()))
            {//キーの少ない順に実行して、同時押しと被ったとき同時押しを後から実行して上書きさせる
             //if (action.KeyConfigs.Count == CurrentKeyConfigs.Count)
             //{ //別々の機能を同時に押す場合もあるのでキーの数は見てはいけない
                var enable = true;
                foreach (var key in action.KeyConfigs)
                {
                    if (CurrentKeyConfigs.Where(d => d.IsEqualKeyCode(key) == true).Any() == false)
                    {
                        //キーが含まれてないとき
                        enable = false;
                    }
                }
                if (enable)
                {//現在押してるキーの中にすべてのキーが含まれていた
                    if (action.IsKeyUp)
                    {
                        //キーを離す操作の時はキューに入れておく
                        CurrentKeyUpActions.Add(action);
                    }
                    else
                    {
                        doKeyActions.Add(action);
                    }
                }
                //}
            }
            if (doKeyActions.Any())
            {
                var tmpActions = new List<KeyAction>(doKeyActions);
                foreach (var action in tmpActions)
                {
                    foreach (var target in tmpActions.Where(d => d != action))
                    {
                        if (target.KeyConfigs.ContainsArray(action.KeyConfigs))
                        {//更に複数押しのキー設定が有効な場合、少ないほうは無効(上書きされてしまうため)
                            doKeyActions.Remove(action);
                        }
                    }
                }
                foreach (var action in doKeyActions)
                {//残った処理だけ実行
                    DoKeyAction(action);
                }
            }

        }
        else
        {
            CurrentKeyConfigs.RemoveAll(d => d.IsEqualKeyCode(config)); //たまに離し損ねるので、もし押しっぱなしなら削除
            Debug.Log("離:" + config.ToString());
            //キーを離すイベントのキューチェック
            var tmpActions = new List<KeyAction>(CurrentKeyUpActions);
            foreach (var action in tmpActions)
            {//1度押されたキーなので、現在押されてないキーなら離れたことになる
                var enable = true;
                foreach (var key in action.KeyConfigs)
                {
                    if (CurrentKeyConfigs.Where(d => d.IsEqualKeyCode(key) == true).Any() == true) //まだ押されてる
                    {
                        enable = false;
                    }
                }
                if (enable)
                {
                    DoKeyAction(action);
                    CurrentKeyUpActions.Remove(action);
                }
            }
        }
    }


    private void DoKeyAction(KeyAction action)
    {
        if (action.HandAction)
        {
            SetHandAngle(action.Hand == Hands.Left || action.Hand == Hands.Both, action.Hand == Hands.Right || action.Hand == Hands.Both, action.HandAngles);
        }
        else if (action.FaceAction)
        {
            faceController.SetFace(action.FaceNames, action.FaceStrength, action.StopBlink);
        }
        else if (action.FunctionAction)
        {
            switch (action.Function)
            {
                case Functions.ShowControlPanel:
                    ExecuteControlPanel();
                    break;
                case Functions.ColorGreen:
                    ChangeBackgroundColor(0.0f, 1.0f, 0.0f, false);
                    break;
                case Functions.ColorBlue:
                    ChangeBackgroundColor(0.0f, 0.0f, 1.0f, false);
                    break;
                case Functions.ColorWhite:
                    ChangeBackgroundColor(0.9375f, 0.9375f, 0.9375f, false);
                    break;
                case Functions.ColorCustom:
                    ChangeBackgroundColor(CurrentSettings.CustomBackgroundColor.r, CurrentSettings.CustomBackgroundColor.g, CurrentSettings.CustomBackgroundColor.b, true);
                    break;
                case Functions.ColorTransparent:
                    SetBackgroundTransparent();
                    break;
                case Functions.FrontCamera:
                    ChangeCamera(CameraTypes.Front);
                    break;
                case Functions.BackCamera:
                    ChangeCamera(CameraTypes.Back);
                    break;
                case Functions.FreeCamera:
                    ChangeCamera(CameraTypes.Free);
                    break;
            }
        }
    }


    private List<HumanBodyBones> FingerBones = new List<HumanBodyBones>
    {
        HumanBodyBones.LeftLittleDistal,
        HumanBodyBones.LeftLittleIntermediate,
        HumanBodyBones.LeftLittleProximal,
        HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftRingIntermediate,
        HumanBodyBones.LeftRingProximal,
        HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftMiddleIntermediate,
        HumanBodyBones.LeftMiddleProximal,
        HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftIndexIntermediate,
        HumanBodyBones.LeftIndexProximal,
        HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.LeftThumbIntermediate,
        HumanBodyBones.LeftThumbProximal,
        HumanBodyBones.RightLittleDistal,
        HumanBodyBones.RightLittleIntermediate,
        HumanBodyBones.RightLittleProximal,
        HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightRingIntermediate,
        HumanBodyBones.RightRingProximal,
        HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightMiddleIntermediate,
        HumanBodyBones.RightMiddleProximal,
        HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightIndexIntermediate,
        HumanBodyBones.RightIndexProximal,
        HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightThumbIntermediate,
        HumanBodyBones.RightThumbProximal,
    };

    private List<Transform> FingerTransforms = new List<Transform>();
    private List<Vector3> FingerDefaultVectors = new List<Vector3>();

    private void SetHandAngle(bool LeftEnable, bool RightEnable, List<int> angles)
    {
        if (animator != null)
        {
            var handBonesCount = FingerBones.Count / 2;
            if (LeftEnable)
            {
                for (int i = 0; i < handBonesCount; i += 3)
                {
                    if (i >= 12)
                    { //親指
                        var vector = FingerDefaultVectors[i + 2]; //第三関節
                        var angle = (-angles[(i / 3) * 4 + 2]) / 90.0f; //-90が1.0 -45は0.5 -180は2.0
                        var sideangle = angles[(i / 3) * 4 + 3];
                        var ax = angle * 0.0f;
                        var ay = angle * 0.0f + sideangle;
                        var az = (float)angles[(i / 3) * 4 + 2];
                        if (FingerTransforms[i + 2] != null) FingerTransforms[i + 2].localRotation = Quaternion.Euler(vector.x + ax, vector.y - ay, vector.z - az);

                        vector = FingerDefaultVectors[i + 1]; //第二関節
                        angle = (-angles[(i / 3) * 4 + 1]) / 90.0f;
                        ax = angle * 38f;
                        ay = angle * 38f;
                        az = angle * -15f;
                        if (FingerTransforms[i + 1] != null) FingerTransforms[i + 1].localRotation = Quaternion.Euler(vector.x + ax, vector.y - ay, vector.z - az);

                        vector = FingerDefaultVectors[i]; //第一関節
                        angle = (-angles[(i / 3) * 4]) / 90.0f;
                        ax = angle * 34f;
                        ay = angle * 56f;
                        az = angle * -7f;
                        if (FingerTransforms[i] != null) FingerTransforms[i].localRotation = Quaternion.Euler(vector.x + ax, vector.y - ay, vector.z - az);
                    }
                    else
                    {
                        var vector = FingerDefaultVectors[i + 2]; //第三関節
                        var angle = angles[(i / 3) * 4 + 2];
                        var sideangle = angles[(i / 3) * 4 + 3];
                        if (FingerTransforms[i + 2] != null) FingerTransforms[i + 2].localRotation = Quaternion.Euler(vector.x, vector.y - sideangle, vector.z - angle);

                        vector = FingerDefaultVectors[i + 1]; //第二関節
                        angle = angles[(i / 3) * 4 + 1];
                        if (FingerTransforms[i + 1] != null) FingerTransforms[i + 1].localRotation = Quaternion.Euler(vector.x, vector.y/* - sideangle*/, vector.z - angle);

                        vector = FingerDefaultVectors[i]; //第一関節
                        angle = angles[(i / 3) * 4];
                        if (FingerTransforms[i] != null) FingerTransforms[i].localRotation = Quaternion.Euler(vector.x, vector.y/* - sideangle*/, vector.z - angle);


                    }
                }
            }
            if (RightEnable)
            {
                for (int i = 0; i < handBonesCount; i += 3)
                {
                    if (i >= 12)
                    { //親指
                        var vector = FingerDefaultVectors[i + 2]; //第三関節
                        var angle = (-angles[(i / 3) * 4 + 2]) / 90.0f; //-90が1.0 -45は0.5 -180は2.0
                        var sideangle = angles[(i / 3) * 4 + 3];
                        var ax = angle * 0.0f;
                        var ay = angle * 0.0f + sideangle;
                        var az = (float)angles[(i / 3) * 4 + 2];
                        if (FingerTransforms[i + handBonesCount + 2] != null) FingerTransforms[i + handBonesCount + 2].localRotation = Quaternion.Euler(vector.x + ax, vector.y + ay, vector.z + az);

                        vector = FingerDefaultVectors[i + 1]; //第二関節
                        angle = (-angles[(i / 3) * 4 + 1]) / 90.0f;
                        ax = angle * 38f;
                        ay = angle * 38f;
                        az = angle * -15f;
                        if (FingerTransforms[i + handBonesCount + 1] != null) FingerTransforms[i + handBonesCount + 1].localRotation = Quaternion.Euler(vector.x + ax, vector.y + ay, vector.z + az);

                        vector = FingerDefaultVectors[i]; //第一関節
                        angle = (-angles[(i / 3) * 4]) / 90.0f;
                        ax = angle * 34f;
                        ay = angle * 56f;
                        az = angle * -7f;
                        if (FingerTransforms[i + handBonesCount] != null) FingerTransforms[i + handBonesCount].localRotation = Quaternion.Euler(vector.x + ax, vector.y + ay, vector.z + az);
                    }
                    else
                    {
                        var vector = FingerDefaultVectors[i + 2]; //第三関節
                        var angle = angles[(i / 3) * 4 + 2];
                        var sideangle = angles[(i / 3) * 4 + 3];
                        if (FingerTransforms[i + handBonesCount + 2] != null) FingerTransforms[i + handBonesCount + 2].localRotation = Quaternion.Euler(vector.x, vector.y + sideangle, vector.z + angle);

                        vector = FingerDefaultVectors[i + 1]; //第二関節
                        angle = angles[(i / 3) * 4 + 1];
                        if (FingerTransforms[i + handBonesCount + 1] != null) FingerTransforms[i + handBonesCount + 1].localRotation = Quaternion.Euler(vector.x, vector.y/* + sideangle*/, vector.z + angle);

                        vector = FingerDefaultVectors[i]; //第一関節
                        angle = angles[(i / 3) * 4];
                        if (FingerTransforms[i + handBonesCount] != null) FingerTransforms[i + handBonesCount].localRotation = Quaternion.Euler(vector.x, vector.y/* + sideangle*/, vector.z + angle);


                    }
                }
            }
        }
    }

    #endregion

    #region Setting

    [Serializable]
    private class StoreTransform
    {
        public Vector3 localPosition;
        public Vector3 position;
        public Quaternion localRotation;
        public Quaternion rotation;
        public Vector3 localScale;

        public StoreTransform() { }
        public StoreTransform(Transform orig) : this()
        {
            localPosition = orig.localPosition;
            position = orig.position;
            localRotation = orig.localRotation;
            rotation = orig.rotation;
            localScale = orig.localScale;
        }

        public static StoreTransform Create(Transform orig)
        {
            if (orig == null) return null;
            return new StoreTransform(orig);
        }

        public void SetPosition(Transform orig)
        {
            localPosition = orig.position;
            position = orig.position;
        }

        public void SetRotation(Transform orig)
        {
            localRotation = orig.localRotation;
            rotation = orig.rotation;
        }

        public Transform ToLocalTransform(Transform saveto)
        {
            saveto.localPosition = localPosition;
            saveto.localRotation = localRotation;
            saveto.localScale = localScale;
            return saveto;
        }

        public Transform ToWorldTransform(Transform saveto)
        {
            saveto.position = position;
            saveto.rotation = rotation;
            saveto.localScale = localScale;
            return saveto;
        }
    }

    [Serializable]
    private class LookTargetSettings
    {
        public Vector3 Offset;
        public float Distance;
        public static LookTargetSettings Create(CameraLookTarget target)
        {
            return new LookTargetSettings { Offset = target.Offset, Distance = target.Distance };
        }
        public void Set(CameraLookTarget target)
        {
            Offset = target.Offset; Distance = target.Distance;
        }
        public void ApplyTo(CameraLookTarget target)
        {
            target.Offset = Offset; target.Distance = Distance;
        }
        public void ApplyTo(Camera camera)
        {
            var target = camera.GetComponent<CameraLookTarget>();
            if (target != null) { target.Offset = Offset; target.Distance = Distance; }
        }
    }

    [Serializable]
    private class Settings
    {
        public string VRMPath = null;
        public StoreTransform headTracker = null;
        public StoreTransform bodyTracker = null;
        public StoreTransform leftHandTracker = null;
        public StoreTransform rightHandTracker = null;
        public StoreTransform leftFootTracker = null;
        public StoreTransform rightFootTracker = null;
        public Color BackgroundColor;
        public Color CustomBackgroundColor;
        public bool IsTransparent;
        public bool HideBorder;
        public bool IsTopMost;
        public StoreTransform FreeCameraTransform = null;
        public LookTargetSettings FrontCameraLookTargetSettings = null;
        public LookTargetSettings BackCameraLookTargetSettings = null;
        [OptionalField]
        public CameraTypes? CameraType = null;
        [OptionalField]
        public bool ShowCameraGrid = false;
        [OptionalField]
        public bool WindowClickThrough;
        [OptionalField]
        public bool LipSyncEnable;
        [OptionalField]
        public string LipSyncDevice;
        [OptionalField]
        public float LipSyncGain;
        [OptionalField]
        public bool LipSyncMaxWeightEnable;
        [OptionalField]
        public float LipSyncWeightThreashold;
        [OptionalField]
        public bool LipSyncMaxWeightEmphasis;
        [OptionalField]
        public bool AutoBlinkEnable = false;
        [OptionalField]
        public float BlinkTimeMin = 1.0f;
        [OptionalField]
        public float BlinkTimeMax = 10.0f;
        [OptionalField]
        public float CloseAnimationTime = 0.06f;
        [OptionalField]
        public float OpenAnimationTime = 0.03f;
        [OptionalField]
        public float ClosingTime = 0.1f;
        [OptionalField]
        public string DefaultFace = "通常(NEUTRAL)";

        [OptionalField]
        public bool IsOculus;
        [OptionalField]
        public bool LeftCenterEnable;
        [OptionalField]
        public bool RightCenterEnable;
        [OptionalField]
        public List<UPoint> LeftTouchPadPoints;
        [OptionalField]
        public List<UPoint> RightTouchPadPoints;
        [OptionalField]
        public List<KeyAction> KeyActions = null;
        [OptionalField]
        public float LeftHandRotation = -90.0f;
        [OptionalField]
        public float RightHandRotation = 90.0f;

        //初期値
        [OnDeserializing()]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            BlinkTimeMin = 1.0f;
            BlinkTimeMax = 10.0f;
            CloseAnimationTime = 0.06f;
            OpenAnimationTime = 0.03f;
            ClosingTime = 0.1f;
            DefaultFace = "通常(NEUTRAL)";
        }
    }

    private Settings CurrentSettings = new Settings();

    private void SaveSettings()
    {
        var path = WindowsDialogs.SaveFileDialog("設定保存先選択", ".json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(CurrentSettings)));
    }

    private async void LoadSettings(bool LoadDefault = false, bool IsFirstTime = false)
    {
        if (LoadDefault == false)
        {
            var path = WindowsDialogs.OpenFileDialog("設定読み込み先選択", ".json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            CurrentSettings = Json.Serializer.Deserialize<Settings>(File.ReadAllText(path));
        }
        else
        {
            if (IsFirstTime)
            {
                try
                {
                    var path = Application.dataPath + "/../default.json";
                    CurrentSettings = Json.Serializer.Deserialize<Settings>(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Application.dataPath + "/../exception.txt", ex.ToString() + ":" + ex.Message);
                }
            }
        }
        if (CurrentSettings != null)
        {
            if (string.IsNullOrWhiteSpace(CurrentSettings.VRMPath) == false)
            {
                await server.SendCommandAsync(new PipeCommands.LoadVRMPath { Path = CurrentSettings.VRMPath });
                ImportVRM(CurrentSettings.VRMPath, false);
            }
            if (CurrentSettings.BackgroundColor != null)
            {
                ChangeBackgroundColor(CurrentSettings.BackgroundColor.r, CurrentSettings.BackgroundColor.g, CurrentSettings.BackgroundColor.b, false);
            }
            if (CurrentSettings.CustomBackgroundColor != null)
            {
                await server.SendCommandAsync(new PipeCommands.LoadCustomBackgroundColor { r = CurrentSettings.CustomBackgroundColor.r, g = CurrentSettings.CustomBackgroundColor.g, b = CurrentSettings.CustomBackgroundColor.b });
            }
            if (CurrentSettings.IsTransparent)
            {
                SetBackgroundTransparent();
            }
            SetWindowBorder(CurrentSettings.HideBorder);
            await server.SendCommandAsync(new PipeCommands.LoadHideBorder { enable = CurrentSettings.HideBorder });
            SetWindowTopMost(CurrentSettings.IsTopMost);
            await server.SendCommandAsync(new PipeCommands.LoadIsTopMost { enable = CurrentSettings.IsTopMost });
            if (CurrentSettings.FreeCameraTransform != null)
            {
                CurrentSettings.FreeCameraTransform.ToLocalTransform(FreeCamera.transform);
            }
            if (CurrentSettings.FrontCameraLookTargetSettings != null)
            {
                CurrentSettings.FrontCameraLookTargetSettings.ApplyTo(FrontCamera);
            }
            if (CurrentSettings.BackCameraLookTargetSettings != null)
            {
                CurrentSettings.BackCameraLookTargetSettings.ApplyTo(BackCamera);
            }
            if (CurrentSettings.CameraType.HasValue)
            {
                ChangeCamera(CurrentSettings.CameraType.Value);
            }
            SetGridVisible(CurrentSettings.ShowCameraGrid);
            await server.SendCommandAsync(new PipeCommands.LoadShowCameraGrid { enable = CurrentSettings.ShowCameraGrid });
            SetWindowClickThrough(CurrentSettings.WindowClickThrough);
            await server.SendCommandAsync(new PipeCommands.LoadSetWindowClickThrough { enable = CurrentSettings.WindowClickThrough });
            SetLipSyncEnable(CurrentSettings.LipSyncEnable);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncEnable { enable = CurrentSettings.LipSyncEnable });
            SetLipSyncDevice(CurrentSettings.LipSyncDevice);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncDevice { device = CurrentSettings.LipSyncDevice });
            SetLipSyncGain(CurrentSettings.LipSyncGain);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncGain { gain = CurrentSettings.LipSyncGain });
            SetLipSyncMaxWeightEnable(CurrentSettings.LipSyncMaxWeightEnable);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncMaxWeightEnable { enable = CurrentSettings.LipSyncMaxWeightEnable });
            SetLipSyncWeightThreashold(CurrentSettings.LipSyncWeightThreashold);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncWeightThreashold { threashold = CurrentSettings.LipSyncWeightThreashold });
            SetLipSyncMaxWeightEmphasis(CurrentSettings.LipSyncMaxWeightEmphasis);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncMaxWeightEmphasis { enable = CurrentSettings.LipSyncMaxWeightEmphasis });

            SetAutoBlinkEnable(CurrentSettings.AutoBlinkEnable);
            await server.SendCommandAsync(new PipeCommands.LoadAutoBlinkEnable { enable = CurrentSettings.AutoBlinkEnable });
            SetBlinkTimeMin(CurrentSettings.BlinkTimeMin);
            await server.SendCommandAsync(new PipeCommands.LoadBlinkTimeMin { time = CurrentSettings.BlinkTimeMin });
            SetBlinkTimeMax(CurrentSettings.BlinkTimeMax);
            await server.SendCommandAsync(new PipeCommands.LoadBlinkTimeMax { time = CurrentSettings.BlinkTimeMax });
            SetCloseAnimationTime(CurrentSettings.CloseAnimationTime);
            await server.SendCommandAsync(new PipeCommands.LoadCloseAnimationTime { time = CurrentSettings.CloseAnimationTime });
            SetOpenAnimationTime(CurrentSettings.OpenAnimationTime);
            await server.SendCommandAsync(new PipeCommands.LoadOpenAnimationTime { time = CurrentSettings.OpenAnimationTime });
            SetClosingTime(CurrentSettings.ClosingTime);
            await server.SendCommandAsync(new PipeCommands.LoadClosingTime { time = CurrentSettings.ClosingTime });
            SetDefaultFace(CurrentSettings.DefaultFace);
            await server.SendCommandAsync(new PipeCommands.LoadDefaultFace { face = CurrentSettings.DefaultFace });

            await server.SendCommandAsync(new PipeCommands.LoadControllerTouchPadPoints
            {
                IsOculus = CurrentSettings.IsOculus,
                LeftPoints = CurrentSettings.LeftTouchPadPoints,
                LeftCenterEnable = CurrentSettings.LeftCenterEnable,
                RightPoints = CurrentSettings.RightTouchPadPoints,
                RightCenterEnable = CurrentSettings.RightCenterEnable
            });
            await server.SendCommandAsync(new PipeCommands.LoadKeyActions { KeyActions = CurrentSettings.KeyActions });
            await server.SendCommandAsync(new PipeCommands.LoadHandRotations { LeftHandRotation = CurrentSettings.LeftHandRotation, RightHandRotation = CurrentSettings.RightHandRotation });
            UpdateHandRotation();
        }
    }

    #endregion

    private void UpdateHandRotation()
    {
        if (vrik == null) return;
        Transform leftHandAdjusterTransform = vrik.solver.leftArm.target;
        Transform rightHandAdjusterTransform = vrik.solver.rightArm.target;
        if (leftHandAdjusterTransform == null || rightHandAdjusterTransform == null) return;
        var angles = leftHandAdjusterTransform.localRotation.eulerAngles;
        leftHandAdjusterTransform.localRotation = Quaternion.Euler(CurrentSettings.LeftHandRotation, angles.y, angles.z);
        angles = rightHandAdjusterTransform.localRotation.eulerAngles;
        rightHandAdjusterTransform.localRotation = Quaternion.Euler(CurrentSettings.RightHandRotation, angles.y, angles.z);
    }

    private void Awake()
    {
        defaultWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_STYLE);
        defaultExWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_EXSTYLE);
    }

    // Update is called once per frame
    void Update()
    {
        CameraMouseEvent();
        KeyboardAction.Update();
    }

    private int WindowX;
    private int WindowY;
    private Vector2 OldMousePos;

    void LateUpdate()
    {
        //Windowの移動操作
        //ドラッグ開始
        if (Input.GetMouseButtonDown(0))
        {
            var r = GetUnityWindowPosition();
            WindowX = r.left;
            WindowY = r.top;
            OldMousePos = GetWindowsMousePosition();
        }

        //ドラッグ中
        if (Input.GetMouseButton(0))
        {
            Vector2 pos = GetWindowsMousePosition();
            if (pos != OldMousePos)
            {
                WindowX += (int)(pos.x - OldMousePos.x);
                WindowY += (int)(pos.y - OldMousePos.y);
                SetUnityWindowPosition(WindowX, WindowY);
                OldMousePos = pos;
            }
        }
    }
}
