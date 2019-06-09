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
using UnityMemoryMappedFile;
using VRM;
using static Assets.Scripts.NativeMethods;
using RootMotion.FinalIK;
using Valve.VR;
#if UNITY_EDITOR   // エディタ上でしか動きません。
using UnityEditor;
#endif

public class ControlWPFWindow : MonoBehaviour
{
    public string VersionString;

    public TrackerHandler handler = null;
    public Transform LeftWristTransform = null;
    public Transform RightWristTransform = null;

    public CameraLookTarget CalibrationCamera;

    public GameObject LeftHandCamera;
    public GameObject RightHandCamera;

    public Camera FreeCamera;
    public Camera FrontCamera;
    public Camera BackCamera;
    public Camera PositionFixedCamera;

    public Renderer BackgroundRenderer;

    public GameObject GridCanvas;

    public DynamicOVRLipSync LipSync;

    public FaceController faceController;
    public HandController handController;

    public OVRControllerAction controllerAction;

    public WristRotationFix wristRotationFix;

    public Transform HandTrackerRoot;
    public Transform HeadTrackerRoot;
    public Transform PelvisTrackerRoot;
    public Transform RealTrackerRoot;

    public MemoryMappedFileServer server;
    private string pipeName = Guid.NewGuid().ToString();

    private GameObject CurrentModel = null;

    private RootMotion.FinalIK.VRIK vrik = null;

    private Camera currentCamera;

    private Animator animator = null;

    private bool IsOculus { get { return controllerAction.IsOculus; } }

    private int CurrentWindowNum = 1;

    public enum MouseButtons
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }

    private uint defaultWindowStyle;
    private uint defaultExWindowStyle;

    private System.Threading.SynchronizationContext context = null;

    // Use this for initialization
    void Start()
    {
        Debug.unityLogger.logEnabled = false;
        context = System.Threading.SynchronizationContext.Current;

#if UNITY_EDITOR   // エディタ上でしか動きません。
        pipeName = "VMCTest";
#else
        CurrentWindowNum = SetWindowTitle();
        pipeName = "VMCpipe" + Guid.NewGuid().ToString();
#endif
        server = new MemoryMappedFileServer();
        server.ReceivedEvent += Server_Received;
        server.Start(pipeName);

        //start control panel
#if !UNITY_EDITOR
        ExecuteControlPanel();
#endif

        Application.targetFrameRate = 60;

        currentCamera = FreeCamera;

        CurrentSettings.BackgroundColor = BackgroundRenderer.material.color;
        CurrentSettings.CustomBackgroundColor = BackgroundRenderer.material.color;

        controllerAction.KeyDownEvent += ControllerAction_KeyDown;
        controllerAction.KeyUpEvent += ControllerAction_KeyUp;
        controllerAction.AxisChangedEvent += ControllerAction_AxisChanged;

        KeyboardAction.KeyDownEvent += KeyboardAction_KeyDown;
        KeyboardAction.KeyUpEvent += KeyboardAction_KeyUp;
    }

    private int SetWindowTitle()
    {
        int setWindowNum = 1;
        var allWindowList = GetAllWindowHandle();
        var numlist = allWindowList.Where(p => p.Value.StartsWith(Application.productName + " ") && p.Value.EndsWith(")") && p.Value.Contains('(')).Select(t => int.Parse(t.Value.Split('(').Last().Replace(")", ""))).OrderBy(d => d);
        while (numlist.Contains(setWindowNum))
        {
            setWindowNum++;
        }
        Assets.Scripts.NativeMethods.SetUnityWindowTitle($"{Application.productName} {VersionString} ({setWindowNum})");
        return setWindowNum;
    }

    private int doSendTrackerMoved = 0;
    private Dictionary<string, DateTime> trackerMovedLastSendTime = new Dictionary<string, DateTime>();
    private async void TransformExtensions_TrackerMovedEvent(object sender, string e)
    {
        if (doSendTrackerMoved > 0)
        {
            if (trackerMovedLastSendTime.ContainsKey(e) == false)
            {
                trackerMovedLastSendTime.Add(e, DateTime.Now);
            }
            else if (DateTime.Now - trackerMovedLastSendTime[e] < TimeSpan.FromSeconds(1))
            {
                return;
            }
            await server.SendCommandAsync(new PipeCommands.TrackerMoved { SerialNumber = e });
            trackerMovedLastSendTime[e] = DateTime.Now;
        }
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
        server?.Dispose();
        KeyboardAction.KeyDownEvent -= KeyboardAction_KeyDown;
        KeyboardAction.KeyUpEvent -= KeyboardAction_KeyUp;

        controllerAction.KeyDownEvent -= ControllerAction_KeyDown;
        controllerAction.KeyUpEvent -= ControllerAction_KeyUp;
        controllerAction.AxisChangedEvent -= ControllerAction_AxisChanged;
    }

    private void Server_Received(object sender, DataReceivedEventArgs e)
    {
        context.Post(async s =>
        {
            if (e.CommandType == typeof(PipeCommands.LoadVRM))
            {
                var d = (PipeCommands.LoadVRM)e.Data;
                await server.SendCommandAsync(new PipeCommands.ReturnLoadVRM { Data = LoadVRM(d.Path) }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.ImportVRM))
            {
                var d = (PipeCommands.ImportVRM)e.Data;
                ImportVRM(d.Path, d.ImportForCalibration, d.UseCurrentFixSetting ? CurrentSettings.EnableNormalMapFix : d.EnableNormalMapFix, d.UseCurrentFixSetting ? CurrentSettings.DeleteHairNormalMap : d.DeleteHairNormalMap);
            }

            else if (e.CommandType == typeof(PipeCommands.Calibrate))
            {
                var d = (PipeCommands.Calibrate)e.Data;
                StartCoroutine(Calibrate(d.CalibrateType));
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
            else if (e.CommandType == typeof(PipeCommands.SetCameraMirror))
            {
                var d = (PipeCommands.SetCameraMirror)e.Data;
                SetCameraMirror(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.SetCameraFOV))
            {
                var d = (PipeCommands.SetCameraFOV)e.Data;
                SetCameraFOV(d.value);
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
                var d = (PipeCommands.LoadSettings)e.Data;
                LoadSettings(false, false, d.Path);
            }
            else if (e.CommandType == typeof(PipeCommands.SaveSettings))
            {
                var d = (PipeCommands.SaveSettings)e.Data;
                SaveSettings(d.Path);
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
                handController.SetHandEulerAngles(d.LeftEnable, d.RightEnable, handController.CalcHandEulerAngles(d.HandAngles));
            }
            else if (e.CommandType == typeof(PipeCommands.StartKeyConfig))
            {
                doKeyConfig = true;
                faceController.StartSetting();
                CurrentKeyConfigs.Clear();
            }
            else if (e.CommandType == typeof(PipeCommands.EndKeyConfig))
            {
                faceController.EndSetting();
                doKeyConfig = false;
                CurrentKeyConfigs.Clear();
            }
            else if (e.CommandType == typeof(PipeCommands.StartKeySend))
            {
                doKeySend = true;
                CurrentKeyConfigs.Clear();
            }
            else if (e.CommandType == typeof(PipeCommands.EndKeySend))
            {
                doKeySend = false;
                CurrentKeyConfigs.Clear();
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
                StartCoroutine(SetExternalCameraConfig(d));
            }
            else if (e.CommandType == typeof(PipeCommands.GetExternalCameraConfig))
            {
                var d = (PipeCommands.GetExternalCameraConfig)e.Data;
                var tracker = handler.GetTrackerTransformByName(d.ControllerName);
                //InverseTransformPoint  Thanks: えむにわ(@m2wasabi)
                var rposition = tracker.InverseTransformPoint(currentCamera.transform.position);
                var rrotation = (Quaternion.Inverse(tracker.rotation) * currentCamera.transform.rotation).eulerAngles;
                await server.SendCommandAsync(new PipeCommands.SetExternalCameraConfig
                {
                    x = rposition.x,
                    y = rposition.y,
                    z = rposition.z,
                    rx = rrotation.x,
                    ry = rrotation.y,
                    rz = rrotation.z,
                    fov = currentCamera.fieldOfView,
                    ControllerName = d.ControllerName
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.GetTrackerSerialNumbers))
            {
                await server.SendCommandAsync(new PipeCommands.ReturnTrackerSerialNumbers { List = GetTrackerSerialNumbers(), CurrentSetting = GetCurrentTrackerSettings() }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetTrackerSerialNumbers))
            {
                var d = (PipeCommands.SetTrackerSerialNumbers)e.Data;
                SetTrackerSerialNumbers(d);

            }
            else if (e.CommandType == typeof(PipeCommands.GetTrackerOffsets))
            {
                await server.SendCommandAsync(new PipeCommands.SetTrackerOffsets
                {
                    LeftHandTrackerOffsetToBodySide = CurrentSettings.LeftHandTrackerOffsetToBodySide,
                    LeftHandTrackerOffsetToBottom = CurrentSettings.LeftHandTrackerOffsetToBottom,
                    RightHandTrackerOffsetToBodySide = CurrentSettings.RightHandTrackerOffsetToBodySide,
                    RightHandTrackerOffsetToBottom = CurrentSettings.RightHandTrackerOffsetToBottom
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetTrackerOffsets))
            {
                var d = (PipeCommands.SetTrackerOffsets)e.Data;
                CurrentSettings.LeftHandTrackerOffsetToBodySide = d.LeftHandTrackerOffsetToBodySide;
                CurrentSettings.LeftHandTrackerOffsetToBottom = d.LeftHandTrackerOffsetToBottom;
                CurrentSettings.RightHandTrackerOffsetToBodySide = d.RightHandTrackerOffsetToBodySide;
                CurrentSettings.RightHandTrackerOffsetToBottom = d.RightHandTrackerOffsetToBottom;

            }
            else if (e.CommandType == typeof(PipeCommands.GetVirtualWebCamConfig))
            {
                await server.SendCommandAsync(new PipeCommands.SetVirtualWebCamConfig
                {
                    Enabled = CurrentSettings.WebCamEnabled,
                    Resize = CurrentSettings.WebCamResize,
                    Mirroring = CurrentSettings.WebCamMirroring,
                    Buffering = CurrentSettings.WebCamBuffering,
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetVirtualWebCamConfig))
            {
                var d = (PipeCommands.SetVirtualWebCamConfig)e.Data;
                CurrentSettings.WebCamEnabled = d.Enabled;
                CurrentSettings.WebCamResize = d.Resize;
                CurrentSettings.WebCamMirroring = d.Mirroring;
                CurrentSettings.WebCamBuffering = d.Buffering;
                UpdateWebCamConfig();
            }
            else if (e.CommandType == typeof(PipeCommands.GetResolutions))
            {
                await server.SendCommandAsync(new PipeCommands.ReturnResolutions
                {
                    List = new List<Tuple<int, int, int>>(Screen.resolutions.Select(r => Tuple.Create(r.width, r.height, r.refreshRate))),
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetResolution))
            {
                var d = (PipeCommands.SetResolution)e.Data;
                CurrentSettings.ScreenWidth = d.Width;
                CurrentSettings.ScreenHeight = d.Height;
                CurrentSettings.ScreenRefreshRate = d.RefreshRate;
                Screen.SetResolution(d.Width, d.Height, false, d.RefreshRate);
            }
            else if (e.CommandType == typeof(PipeCommands.TakePhoto))
            {
                var d = (PipeCommands.TakePhoto)e.Data;
                TakePhoto(d.Width, d.TransparentBackground, d.Directory);
            }
            else if (e.CommandType == typeof(PipeCommands.SetLightAngle))
            {
                var d = (PipeCommands.SetLightAngle)e.Data;
                SetLightAngle(d.X, d.Y);
            }
            else if (e.CommandType == typeof(PipeCommands.ChangeLightColor))
            {
                var d = (PipeCommands.ChangeLightColor)e.Data;
                ChangeLightColor(d.a, d.r, d.g, d.b);
            }
            else if (e.CommandType == typeof(PipeCommands.TrackerMovedRequest))
            {
                var d = (PipeCommands.TrackerMovedRequest)e.Data;
                if (d.doSend)
                {
                    doSendTrackerMoved++;
                }
                else
                {
                    doSendTrackerMoved--;
                }
            }
            else if (e.CommandType == typeof(PipeCommands.LoadCurrentSettings))
            {
                if (isFirstTimeExecute)
                {
                    isFirstTimeExecute = false;
                    //起動時は初期設定ロード
                    LoadSettings(true, true);
                    TransformExtensions.TrackerMovedEvent += TransformExtensions_TrackerMovedEvent;
                }
                else
                {
                    LoadSettings(true, false);
                }
            }
        }, null);
    }

    public Transform MainDirectionalLightTransform;
    public Light MainDirectionalLight;

    private void SetLightAngle(float x, float y)
    {
        if (MainDirectionalLightTransform != null)
        {
            MainDirectionalLightTransform.eulerAngles = new Vector3(x, y, MainDirectionalLightTransform.eulerAngles.z);
            CurrentSettings.LightRotationX = x;
            CurrentSettings.LightRotationY = y;
        }
    }

    private void ChangeLightColor(float a, float r, float g, float b)
    {
        if (MainDirectionalLight != null)
        {
            CurrentSettings.LightColor = new Color(r, g, b, a);
            MainDirectionalLight.color = CurrentSettings.LightColor;
        }
    }

    private bool isFirstTimeExecute = true;

    #region VRM

    private VRMData LoadVRM(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        var vrmdata = new VRMData();
        vrmdata.FilePath = path;
        var context = new VRMImporterContext();

        var bytes = File.ReadAllBytes(path);

        // GLB形式でJSONを取得しParseします
        context.ParseGlb(bytes);

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
        vrmdata.AllowedUser = (UnityMemoryMappedFile.AllowedUser)meta.AllowedUser;
        vrmdata.ViolentUssage = (UnityMemoryMappedFile.UssageLicense)meta.ViolentUssage;
        vrmdata.SexualUssage = (UnityMemoryMappedFile.UssageLicense)meta.SexualUssage;
        vrmdata.CommercialUssage = (UnityMemoryMappedFile.UssageLicense)meta.CommercialUssage;
        vrmdata.OtherPermissionUrl = meta.OtherPermissionUrl;

        // Distribution License
        vrmdata.LicenseType = (UnityMemoryMappedFile.LicenseType)meta.LicenseType;
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
    private const float LeftLowerArmAngle = -30f;
    private const float RightLowerArmAngle = -30f;
    private const float LeftUpperArmAngle = -30f;
    private const float RightUpperArmAngle = -30f;
    private const float LeftHandAngle = -30f;
    private const float RightHandAngle = -30f;

    private async void ImportVRM(string path, bool ImportForCalibration, bool EnableNormalMapFix, bool DeleteHairNormalMap)
    {
        if (ImportForCalibration == false)
        {
            CurrentSettings.VRMPath = path;
            var context = new VRMImporterContext();

            var bytes = File.ReadAllBytes(path);

            // GLB形式でJSONを取得しParseします
            context.ParseGlb(bytes);

            // ParseしたJSONをシーンオブジェクトに変換していく
            //CurrentModel = await VRMImporter.LoadVrmAsync(context);
            await context.LoadAsyncTask();
            context.ShowMeshes();

            LoadNewModel(context.Root);
        }
        else
        {
            if (CurrentModel != null)
            {
                var currentvrik = CurrentModel.GetComponent<VRIK>();
                if (currentvrik != null) Destroy(currentvrik);
            }
            LoadDefaultCurrentModelTransforms();
            //SetVRIK(CurrentModel);
            if (animator != null)
            {
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(LeftLowerArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(RightLowerArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(LeftUpperArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(RightUpperArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(LeftHandAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(RightHandAngle, 0, 0);

                //wristRotationFix.SetVRIK(vrik);

                handController.SetDefaultAngle(animator);

                //トラッカー位置の表示
                RealTrackerRoot.gameObject.SetActive(true);
                foreach (Transform t in RealTrackerRoot)
                {
                    t.localPosition = new Vector3(0, -100f, 0);
                }

                if (CalibrationCamera != null)
                {
                    CalibrationCamera.Target = animator.GetBoneTransform(HumanBodyBones.Head);
                    CalibrationCamera.gameObject.SetActive(true);
                }
            }
        }
    }

    public void LoadNewModel(GameObject model)
    {
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
        CurrentModel = model;

        ModelInitialize();
    }

    public void ModelInitialize()
    {

        SaveDefaultCurrentModelTransforms();

        //CurrentSettings.EnableNormalMapFix = EnableNormalMapFix;
        //CurrentSettings.DeleteHairNormalMap = DeleteHairNormalMap;
        //if (EnableNormalMapFix)
        //{
        //    //VRoidモデルのNormalMapテカテカを修正する
        //    Yashinut.VRoid.CorrectNormalMapImport.CorrectNormalMap(CurrentModel, DeleteHairNormalMap);
        //}

        //モデルのSkinnedMeshRendererがカリングされないように、すべてのオプション変更
        foreach (var renderer in CurrentModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.updateWhenOffscreen = true;
        }

        //LipSync
        LipSync.ImportVRMmodel(CurrentModel);
        //まばたき
        faceController.ImportVRMmodel(CurrentModel);

        //CurrentModel.transform.SetParent(transform, false);

        animator = CurrentModel.GetComponent<Animator>();

        SetVRIK(CurrentModel);

        if (animator != null)
        {
            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).eulerAngles = new Vector3(LeftLowerArmAngle, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm).eulerAngles = new Vector3(RightLowerArmAngle, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).eulerAngles = new Vector3(LeftUpperArmAngle, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm).eulerAngles = new Vector3(RightUpperArmAngle, 0, 0);
            wristRotationFix.SetVRIK(vrik);

            handController.SetDefaultAngle(animator);

            //設定用両手のカメラをモデルにアタッチ
            if (LeftHandCamera != null)
            {
                LeftHandCamera.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.LeftHand));
                LeftHandCamera.transform.localPosition = new Vector3(-0.07f, -0.13f, 0.14f);
                LeftHandCamera.transform.localRotation = Quaternion.Euler(-140f, 0f, 90f);
                LeftHandCamera.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }
            if (RightHandCamera != null)
            {
                RightHandCamera.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand));
                RightHandCamera.transform.localPosition = new Vector3(0.07f, -0.13f, 0.14f);
                RightHandCamera.transform.localRotation = Quaternion.Euler(-140f, 0f, -90f);
                RightHandCamera.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }

        }
        SetCameraLookTarget();
        //SetTrackersToVRIK();
    }
    /*
    private Vector3 DefaultModelPosition;
    private Quaternion DefaultModelRotation;
    private Vector3 DefaultModelScale;
    private Dictionary<HumanBodyBones, Quaternion> DefaultRotations;

    public void SaveDefaultCurrentModelTransforms()
    {
        DefaultModelPosition = CurrentModel.transform.position;
        DefaultModelRotation = CurrentModel.transform.rotation;
        DefaultModelScale = CurrentModel.transform.localScale;
        DefaultRotations = new Dictionary<HumanBodyBones, Quaternion>();
        var animator = CurrentModel.GetComponent<Animator>();
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var t = animator.GetBoneTransform((HumanBodyBones)i);
            if (t != null)
            {
                DefaultRotations.Add((HumanBodyBones)i, t.rotation);
            }
        }
    }

    public void LoadDefaultCurrentModelTransforms()
    {
        if (DefaultRotations == null) return;
        CurrentModel.transform.position = DefaultModelPosition;
        CurrentModel.transform.rotation = DefaultModelRotation;
        CurrentModel.transform.localScale = DefaultModelScale;
        foreach (var pair in DefaultRotations)
        {
            var t = animator.GetBoneTransform(pair.Key);
            if (t != null)
            {
                t.rotation = pair.Value;
            }
        }
    }
    */

    private GameObject PositionSavedModel;
    private Vector3 DefaultModelPosition;
    private Quaternion DefaultModelRotation;
    private Vector3 DefaultModelScale;
    private Dictionary<Transform, Vector3> DefaultPositions;
    private Dictionary<Transform, Quaternion> DefaultRotations;
    private Dictionary<Transform, Vector3> DefaultScales;
    private Dictionary<VRMSpringBoneColliderGroup.SphereCollider, Vector4> DefaultColliders;

    public void SaveDefaultCurrentModelTransforms()
    {
        PositionSavedModel = CurrentModel;
        DefaultModelPosition = CurrentModel.transform.position;
        DefaultModelRotation = CurrentModel.transform.rotation;
        DefaultModelScale = CurrentModel.transform.localScale;
        DefaultPositions = new Dictionary<Transform, Vector3>();
        DefaultRotations = new Dictionary<Transform, Quaternion>();
        DefaultScales = new Dictionary<Transform, Vector3>();
        DefaultColliders = new Dictionary<VRMSpringBoneColliderGroup.SphereCollider, Vector4>();
        var allTransforms = CurrentModel.transform.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            DefaultPositions.Add(t, t.position);
            DefaultRotations.Add(t, t.rotation);
            DefaultScales.Add(t, t.localScale);
        }

        //VRMモデルのコライダー
        var springBoneColiderGroups = CurrentModel.GetComponentsInChildren<VRM.VRMSpringBoneColliderGroup>();
        foreach (var springBoneColiderGroup in springBoneColiderGroups)
        {
            foreach (var collider in springBoneColiderGroup.Colliders)
            {
                DefaultColliders.Add(collider, new Vector4(collider.Offset.x, collider.Offset.y, collider.Offset.z, collider.Radius));
            }
        }
    }

    public void LoadDefaultCurrentModelTransforms()
    {
        if (PositionSavedModel != CurrentModel || CurrentModel == null) return;
        CurrentModel.transform.localScale = DefaultModelScale;
        CurrentModel.transform.rotation = DefaultModelRotation;
        CurrentModel.transform.position = DefaultModelPosition;
        var animator = CurrentModel.GetComponent<Animator>();
        foreach (var pair in DefaultScales)
        {
            var t = pair.Key;
            if (t != null)
            {
                t.localScale = pair.Value;
            }
        }
        foreach (var pair in DefaultRotations)
        {
            var t = pair.Key;
            if (t != null)
            {
                t.rotation = pair.Value;
            }
        }
        foreach (var pair in DefaultPositions)
        {
            var t = pair.Key;
            if (t != null)
            {
                t.position = pair.Value;
            }
        }

        //VRMモデルのコライダー
        var springBoneColiderGroups = CurrentModel.GetComponentsInChildren<VRM.VRMSpringBoneColliderGroup>();
        foreach (var springBoneColiderGroup in springBoneColiderGroups)
        {
            foreach (var collider in springBoneColiderGroup.Colliders)
            {
                if (DefaultColliders.ContainsKey(collider))
                {
                    var col = DefaultColliders[collider];
                    collider.Offset = new Vector3(col.x, col.y, col.z);
                    collider.Radius = col.w;
                }
            }
        }
    }

    #endregion

    #region Calibration

    private Vector3 fixKneeBone(Transform UpperLeg, Transform Knee, Transform Ankle)
    {
        var a = UpperLeg.position;
        var b = Ankle.position;
        var z = Mathf.Max(a.z, b.z) + 0.001f;
        var x = Mathf.Lerp(a.x, b.x, 0.5f);
        var offset = Knee.position - new Vector3(x, Knee.position.y, z);
        Knee.position -= offset;
        Ankle.position += offset;
        return offset;
    }

    private Vector3 fixPelvisBone(Transform Spine, Transform Pelvis)
    {
        if (Spine.position.z < Pelvis.position.z)
        {
            return Vector3.zero;
        }

        var offset = new Vector3(0, 0, Pelvis.position.z - Spine.position.z + 0.1f);
        Pelvis.position -= offset;
        foreach (var child in Pelvis.GetComponentsInChildren<Transform>(true))
        {
            //child.position += offset;
        }
        return offset;
    }


    private void unfixKneeBone(Vector3 offset, Transform Knee, Transform Ankle)
    {
        //return;
        Knee.position += offset;
        Ankle.position -= offset;
    }

    private void SetVRIK(GameObject model)
    {
        //膝のボーンの曲がる方向で膝の向きが決まってしまうため、強制的に膝のボーンを少し前に曲げる
        var leftOffset = Vector3.zero;
        var rightOffset = Vector3.zero;
        if (animator != null)
        {
            leftOffset = fixKneeBone(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            rightOffset = fixKneeBone(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot));
            fixPelvisBone(animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.Hips));
        }

        vrik = model.AddComponent<RootMotion.FinalIK.VRIK>();
        vrik.solver.IKPositionWeight = 0f;
        vrik.solver.leftArm.stretchCurve = new AnimationCurve();
        vrik.solver.rightArm.stretchCurve = new AnimationCurve();
        vrik.UpdateSolverExternal();

        //膝のボーンの曲がる方向で膝の向きが決まってしまうため、強制的に膝のボーンを少し前に曲げる
        //if (animator != null)
        //{
        //    unfixKneeBone(leftOffset, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot));
        //    unfixKneeBone(rightOffset, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot));
        //}
        //if (animator != null)
        //{
        //    var leftWrist = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).gameObject;
        //    var rightWrist = animator.GetBoneTransform(HumanBodyBones.RightLowerArm).gameObject;
        //    var leftRelaxer = leftWrist.AddComponent<TwistRelaxer>();
        //    var rightRelaxer = rightWrist.AddComponent<TwistRelaxer>();
        //    leftRelaxer.ik = vrik;
        //    rightRelaxer.ik = vrik;
        //}
    }

    Transform bodyTracker = null;
    Transform leftFootTracker = null;
    Transform rightFootTracker = null;
    Transform leftHandTracker = null;
    Transform rightHandTracker = null;
    Transform leftElbowTracker = null;
    Transform rightElbowTracker = null;
    Transform leftKneeTracker = null;
    Transform rightKneeTracker = null;

    private List<Tuple<string, string>> GetTrackerSerialNumbers()
    {
        var list = new List<Tuple<string, string>>();
        if (handler.HMDObject != null)
        {
            list.Add(Tuple.Create("HMD", handler.HMDObject.transform.name));
        }
        //if (handler.CameraControllerType == ETrackedDeviceClass.HMD) list.Add(Tuple.Create("HMD", handler.CameraControllerObject.transform.name));
        foreach (var controller in handler.Controllers)
        {
            list.Add(Tuple.Create("コントローラー", controller.transform.name));
        }
        if (handler.CameraControllerType == ETrackedDeviceClass.Controller) list.Add(Tuple.Create("コントローラー", handler.CameraControllerObject.transform.name));
        foreach (var tracker in handler.Trackers)
        {
            list.Add(Tuple.Create("トラッカー", tracker.transform.name));
        }
        if (handler.CameraControllerType == ETrackedDeviceClass.GenericTracker) list.Add(Tuple.Create("トラッカー", handler.CameraControllerObject.transform.name));
        return list;
    }

    private PipeCommands.SetTrackerSerialNumbers GetCurrentTrackerSettings()
    {
        var deviceDictionary = new Dictionary<ETrackedDeviceClass, string>
        {
            {ETrackedDeviceClass.HMD, "HMD"},
            {ETrackedDeviceClass.Controller, "コントローラー"},
            {ETrackedDeviceClass.GenericTracker, "トラッカー"},
            {ETrackedDeviceClass.TrackingReference, "ベースステーション"},
            {ETrackedDeviceClass.Invalid, "割り当てしない"},
        };
        return new PipeCommands.SetTrackerSerialNumbers
        {
            Head = Tuple.Create(deviceDictionary[CurrentSettings.Head.Item1], CurrentSettings.Head.Item2),
            LeftHand = Tuple.Create(deviceDictionary[CurrentSettings.LeftHand.Item1], CurrentSettings.LeftHand.Item2),
            RightHand = Tuple.Create(deviceDictionary[CurrentSettings.RightHand.Item1], CurrentSettings.RightHand.Item2),
            Pelvis = Tuple.Create(deviceDictionary[CurrentSettings.Pelvis.Item1], CurrentSettings.Pelvis.Item2),
            LeftFoot = Tuple.Create(deviceDictionary[CurrentSettings.LeftFoot.Item1], CurrentSettings.LeftFoot.Item2),
            RightFoot = Tuple.Create(deviceDictionary[CurrentSettings.RightFoot.Item1], CurrentSettings.RightFoot.Item2),
            LeftElbow = Tuple.Create(deviceDictionary[CurrentSettings.LeftElbow.Item1], CurrentSettings.LeftElbow.Item2),
            RightElbow = Tuple.Create(deviceDictionary[CurrentSettings.RightElbow.Item1], CurrentSettings.RightElbow.Item2),
            LeftKnee = Tuple.Create(deviceDictionary[CurrentSettings.LeftKnee.Item1], CurrentSettings.LeftKnee.Item2),
            RightKnee = Tuple.Create(deviceDictionary[CurrentSettings.RightKnee.Item1], CurrentSettings.RightKnee.Item2),
        };
    }

    private void SetTrackerSerialNumbers(PipeCommands.SetTrackerSerialNumbers data)
    {
        var deviceDictionary = new Dictionary<string, ETrackedDeviceClass>
        {
            {"HMD", ETrackedDeviceClass.HMD },
            {"コントローラー", ETrackedDeviceClass.Controller },
            {"トラッカー", ETrackedDeviceClass.GenericTracker },
            {"ベースステーション", ETrackedDeviceClass.TrackingReference },
            {"割り当てしない", ETrackedDeviceClass.Invalid },
        };

        CurrentSettings.Head = Tuple.Create(deviceDictionary[data.Head.Item1], data.Head.Item2);
        CurrentSettings.LeftHand = Tuple.Create(deviceDictionary[data.LeftHand.Item1], data.LeftHand.Item2);
        CurrentSettings.RightHand = Tuple.Create(deviceDictionary[data.RightHand.Item1], data.RightHand.Item2);
        CurrentSettings.Pelvis = Tuple.Create(deviceDictionary[data.Pelvis.Item1], data.Pelvis.Item2);
        CurrentSettings.LeftFoot = Tuple.Create(deviceDictionary[data.LeftFoot.Item1], data.LeftFoot.Item2);
        CurrentSettings.RightFoot = Tuple.Create(deviceDictionary[data.RightFoot.Item1], data.RightFoot.Item2);
        CurrentSettings.LeftElbow = Tuple.Create(deviceDictionary[data.LeftElbow.Item1], data.LeftElbow.Item2);
        CurrentSettings.RightElbow = Tuple.Create(deviceDictionary[data.RightElbow.Item1], data.RightElbow.Item2);
        CurrentSettings.LeftKnee = Tuple.Create(deviceDictionary[data.LeftKnee.Item1], data.LeftKnee.Item2);
        CurrentSettings.RightKnee = Tuple.Create(deviceDictionary[data.RightKnee.Item1], data.RightKnee.Item2);
        SetVRIKTargetTrackers();
    }

    private enum TargetType
    {
        Head, Pelvis, LeftArm, RightArm, LeftLeg, RightLeg, LeftElbow, RightElbow, LeftKnee, RightKnee
    }

    private Transform GetTrackerTransformBySerialNumber(Tuple<ETrackedDeviceClass, string> serial, TargetType setTo, Transform headTracker = null)
    {
        if (serial.Item1 == ETrackedDeviceClass.HMD && handler.HMDObject != null)
        {
            if (handler.HMDObject.transform.name == serial.Item2 || string.IsNullOrEmpty(serial.Item2))
            {
                return handler.HMDObject.transform;
            }
        }
        else if (serial.Item1 == ETrackedDeviceClass.Controller)
        {
            var controllers = handler.Controllers.Where(d => d != handler.CameraControllerObject && d.name.Contains("LIV Virtual Camera") == false);
            Transform ret = null;
            foreach (var controller in controllers)
            {
                if (controller != null && controller.transform.name == serial.Item2)
                {
                    if (setTo == TargetType.LeftArm || setTo == TargetType.RightArm)
                    {
                        ret = controller.transform;
                        break;
                    }
                    return controller.transform;
                }
            }
            if (ret == null)
            {
                var controllerTransforms = controllers.Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.transform.position - headTracker.position), transform = d.transform })
                                                       .OrderBy(d => d.pos.x)
                                                       .Select(d => d.transform);
                if (setTo == TargetType.LeftArm) ret = controllerTransforms.ElementAtOrDefault(0);
                if (setTo == TargetType.RightArm) ret = controllerTransforms.ElementAtOrDefault(1);
            }
            return ret;
        }
        else if (serial.Item1 == ETrackedDeviceClass.GenericTracker)
        {
            foreach (var tracker in handler.Trackers.Where(d => d != handler.CameraControllerObject && d.name.Contains("LIV Virtual Camera") == false))
            {
                if (tracker != null && tracker.transform.name == serial.Item2)
                {
                    return tracker.transform;
                }
            }
            if (string.IsNullOrEmpty(serial.Item2) == false) return null; //Serialあるのに見つからなかったらnull

            var trackerIds = new List<string>();

            if (CurrentSettings.Head.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.Head.Item2);
            if (CurrentSettings.LeftHand.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.LeftHand.Item2);
            if (CurrentSettings.RightHand.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.RightHand.Item2);
            if (CurrentSettings.Pelvis.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.Pelvis.Item2);
            if (CurrentSettings.LeftFoot.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.LeftFoot.Item2);
            if (CurrentSettings.RightFoot.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.RightFoot.Item2);
            if (CurrentSettings.LeftElbow.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.LeftElbow.Item2);
            if (CurrentSettings.RightElbow.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.RightElbow.Item2);
            if (CurrentSettings.LeftKnee.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.LeftKnee.Item2);
            if (CurrentSettings.RightKnee.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(CurrentSettings.RightKnee.Item2);

            //ここに来るときは腰か足のトラッカー自動認識になってるとき
            //割り当てられていないトラッカーリスト
            var autoTrackers = handler.Trackers.Where(d => trackerIds.Contains(d.transform.name) == false).Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.transform.position - headTracker.position), transform = d.transform });
            if (autoTrackers.Any())
            {
                var count = autoTrackers.Count();
                if (count >= 3)
                {
                    if (setTo == TargetType.Pelvis)
                    { //腰は一番高い位置にあるトラッカー
                        return autoTrackers.OrderByDescending(d => d.pos.y).Select(d => d.transform).First();
                    }
                }
                if (count >= 2)
                {
                    if (setTo == TargetType.LeftLeg)
                    {
                        return autoTrackers.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.transform).First();
                    }
                    else if (setTo == TargetType.RightLeg)
                    {
                        return autoTrackers.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.transform).First();
                    }
                }
            }
        }
        return null;
    }

    private void SetVRIKTargetTrackers()
    {
        vrik.solver.spine.headTarget = GetTrackerTransformBySerialNumber(CurrentSettings.Head, TargetType.Head);
        vrik.solver.spine.headClampWeight = 0.38f;

        vrik.solver.spine.pelvisTarget = GetTrackerTransformBySerialNumber(CurrentSettings.Pelvis, TargetType.Pelvis, vrik.solver.spine.headTarget);
        if (vrik.solver.spine.pelvisTarget != null)
        {
            vrik.solver.spine.pelvisPositionWeight = 1f;
            vrik.solver.spine.pelvisRotationWeight = 1f;
            vrik.solver.plantFeet = false;
            vrik.solver.spine.neckStiffness = 0f;
            vrik.solver.spine.maxRootAngle = 180f;
        }
        else
        {
            vrik.solver.spine.pelvisPositionWeight = 0f;
            vrik.solver.spine.pelvisRotationWeight = 0f;
            vrik.solver.plantFeet = true;
            vrik.solver.spine.neckStiffness = 1f;
            vrik.solver.spine.maxRootAngle = 0f;
        }

        vrik.solver.leftArm.target = GetTrackerTransformBySerialNumber(CurrentSettings.LeftHand, TargetType.LeftArm, vrik.solver.spine.headTarget);
        if (vrik.solver.leftArm.target != null)
        {
            vrik.solver.leftArm.positionWeight = 1f;
            vrik.solver.leftArm.rotationWeight = 1f;
        }
        else
        {
            vrik.solver.leftArm.positionWeight = 0f;
            vrik.solver.leftArm.rotationWeight = 0f;
        }

        vrik.solver.rightArm.target = GetTrackerTransformBySerialNumber(CurrentSettings.RightHand, TargetType.RightArm, vrik.solver.spine.headTarget);
        if (vrik.solver.rightArm.target != null)
        {
            vrik.solver.rightArm.positionWeight = 1f;
            vrik.solver.rightArm.rotationWeight = 1f;
        }
        else
        {
            vrik.solver.rightArm.positionWeight = 0f;
            vrik.solver.rightArm.rotationWeight = 0f;
        }

        vrik.solver.leftLeg.target = GetTrackerTransformBySerialNumber(CurrentSettings.LeftFoot, TargetType.LeftLeg, vrik.solver.spine.headTarget);
        if (vrik.solver.leftLeg.target != null)
        {
            vrik.solver.leftLeg.positionWeight = 1f;
            vrik.solver.leftLeg.rotationWeight = 1f;
        }
        else
        {
            vrik.solver.leftLeg.positionWeight = 0f;
            vrik.solver.leftLeg.rotationWeight = 0f;
        }

        vrik.solver.rightLeg.target = GetTrackerTransformBySerialNumber(CurrentSettings.RightFoot, TargetType.RightLeg, vrik.solver.spine.headTarget);
        if (vrik.solver.rightLeg.target != null)
        {
            vrik.solver.rightLeg.positionWeight = 1f;
            vrik.solver.rightLeg.rotationWeight = 1f;
        }
        else
        {
            vrik.solver.rightLeg.positionWeight = 0f;
            vrik.solver.rightLeg.rotationWeight = 0f;
        }
    }


    private IEnumerator Calibrate(PipeCommands.CalibrateType calibrateType)
    {

        SetVRIK(CurrentModel);
        wristRotationFix.SetVRIK(vrik);

        Transform headTracker = GetTrackerTransformBySerialNumber(CurrentSettings.Head, TargetType.Head);
        leftHandTracker = GetTrackerTransformBySerialNumber(CurrentSettings.LeftHand, TargetType.LeftArm, headTracker);
        rightHandTracker = GetTrackerTransformBySerialNumber(CurrentSettings.RightHand, TargetType.RightArm, headTracker);
        bodyTracker = GetTrackerTransformBySerialNumber(CurrentSettings.Pelvis, TargetType.Pelvis, headTracker);
        leftFootTracker = GetTrackerTransformBySerialNumber(CurrentSettings.LeftFoot, TargetType.LeftLeg, headTracker);
        rightFootTracker = GetTrackerTransformBySerialNumber(CurrentSettings.RightFoot, TargetType.RightLeg, headTracker);
        leftElbowTracker = GetTrackerTransformBySerialNumber(CurrentSettings.LeftElbow, TargetType.LeftElbow, headTracker);
        rightElbowTracker = GetTrackerTransformBySerialNumber(CurrentSettings.RightElbow, TargetType.RightElbow, headTracker);
        leftKneeTracker = GetTrackerTransformBySerialNumber(CurrentSettings.LeftKnee, TargetType.LeftKnee, headTracker);
        rightKneeTracker = GetTrackerTransformBySerialNumber(CurrentSettings.RightKnee, TargetType.RightKnee, headTracker);


        var settings = new RootMotion.FinalIK.VRIKCalibrator.Settings();

        yield return new WaitForEndOfFrame();

        var leftHandOffset = Vector3.zero;
        var rightHandOffset = Vector3.zero;

        //トラッカー
        //xをプラス方向に動かすとトラッカーの左(LEDを上に見たとき)に進む
        //yをプラス方向に動かすとトラッカーの上(LED方向)に進む
        //zをマイナス方向に動かすとトラッカーの底面に向かって進む

        if (CurrentSettings.LeftHand.Item1 == ETrackedDeviceClass.GenericTracker)
        {
            //角度補正(左手なら右のトラッカーに向けた)後
            //xを＋方向は体の正面に向かって進む
            //yを＋方向は体の上(天井方向)に向かって進む
            //zを＋方向は体中心(左手なら右手の方向)に向かって進む
            leftHandOffset = new Vector3(1.0f, CurrentSettings.LeftHandTrackerOffsetToBottom, CurrentSettings.LeftHandTrackerOffsetToBodySide); // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
        }
        if (CurrentSettings.RightHand.Item1 == ETrackedDeviceClass.GenericTracker)
        {
            //角度補正(左手なら右のトラッカーに向けた)後
            //xを－方向は体の正面に向かって進む
            //yを＋方向は体の上(天井方向)に向かって進む
            //zを＋方向は体中心(左手なら右手の方向)に向かって進む
            rightHandOffset = new Vector3(1.0f, CurrentSettings.RightHandTrackerOffsetToBottom, CurrentSettings.RightHandTrackerOffsetToBodySide); // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
        }
        if (calibrateType == PipeCommands.CalibrateType.Default)
        {
            yield return Calibrator.CalibrateScaled(RealTrackerRoot, HandTrackerRoot, HeadTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
        }
        else if (calibrateType == PipeCommands.CalibrateType.FixedHand)
        {
            yield return Calibrator.CalibrateFixedHand(RealTrackerRoot, HandTrackerRoot, HeadTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
        }
        else if (calibrateType == PipeCommands.CalibrateType.FixedHandWithGround)
        {
            yield return Calibrator.CalibrateFixedHandWithGround(RealTrackerRoot, HandTrackerRoot, HeadTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
        }

        vrik.solver.IKPositionWeight = 1.0f;
        if (leftFootTracker == null && rightFootTracker == null)
        {
            vrik.solver.plantFeet = true;
            vrik.solver.locomotion.weight = 1.0f;
            var rootController = vrik.references.root.GetComponent<RootMotion.FinalIK.VRIKRootController>();
            if (rootController != null) GameObject.Destroy(rootController);
        }

        vrik.solver.locomotion.footDistance = 0.06f;
        vrik.solver.locomotion.stepThreshold = 0.2f;
        vrik.solver.locomotion.angleThreshold = 45f;
        vrik.solver.locomotion.maxVelocity = 0.04f;
        vrik.solver.locomotion.velocityFactor = 0.04f;
        vrik.solver.locomotion.rootSpeed = 40;
        vrik.solver.locomotion.stepSpeed = 2;

        CurrentSettings.headTracker = StoreTransform.Create(headTracker);
        CurrentSettings.bodyTracker = StoreTransform.Create(bodyTracker);
        CurrentSettings.leftHandTracker = StoreTransform.Create(leftHandTracker);
        CurrentSettings.rightHandTracker = StoreTransform.Create(rightHandTracker);
        CurrentSettings.leftFootTracker = StoreTransform.Create(leftFootTracker);
        CurrentSettings.rightFootTracker = StoreTransform.Create(rightFootTracker);
        CurrentSettings.leftElbowTracker = StoreTransform.Create(leftElbowTracker);
        CurrentSettings.rightElbowTracker = StoreTransform.Create(rightElbowTracker);
        CurrentSettings.leftKneeTracker = StoreTransform.Create(leftKneeTracker);
        CurrentSettings.rightKneeTracker = StoreTransform.Create(rightKneeTracker);
    }

    private void EndCalibrate()
    {
        //トラッカー位置の非表示
        RealTrackerRoot.gameObject.SetActive(false);

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
        else if (type == CameraTypes.PositionFixed)
        {
            SetCameraEnable(PositionFixedCamera);
        }
        CurrentSettings.CameraType = type;
    }

    private void SetCameraEnable(Camera camera)
    {
        if (camera != null)
        {
            var virtualCam = camera.gameObject.GetComponent<VirtualCamera>();
            if (virtualCam != null)
            {
                virtualCam.enabled = CurrentSettings.WebCamEnabled;
            }
            camera.gameObject.SetActive(true);
            if (currentCamera != null && currentCamera != camera) currentCamera.gameObject.SetActive(false);
            camera.GetComponent<CameraMouseControl>().enabled = true;
            currentCamera = camera;
            SetCameraMirrorEnable(CurrentSettings.CameraMirrorEnable);
        }
    }

    private void SetCameraMirrorEnable(bool mirrorEnable)
    {
        var mirror = currentCamera.GetComponent<CameraMirror>();
        if (mirror != null) mirror.MirrorEnable = mirrorEnable;
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
            gameObject.transform.parent =/* bodyTracker == null ? animator.GetBoneTransform(HumanBodyBones.Spine) :*/ CurrentModel.transform;
            var lookTarget = FrontCamera.GetComponent<CameraMouseControl>();
            if (lookTarget != null)
            {
                lookTarget.LookTarget = gameObject.transform;
            }
            lookTarget = BackCamera.GetComponent<CameraMouseControl>();
            if (lookTarget != null)
            {
                lookTarget.LookTarget = gameObject.transform;
            }
            var positionFixedCamera = PositionFixedCamera.GetComponent<CameraMouseControl>();
            if (positionFixedCamera != null)
            {
                positionFixedCamera.PositionFixedTarget = gameObject.transform;
            }
        }
    }

    private void SetGridVisible(bool enable)
    {
        GridCanvas?.SetActive(enable);
        CurrentSettings.ShowCameraGrid = enable;
    }

    private void SetCameraMirror(bool enable)
    {
        CurrentSettings.CameraMirrorEnable = enable;
        SetCameraMirrorEnable(enable);
    }

    private IEnumerator SetExternalCameraConfig(PipeCommands.SetExternalCameraConfig d)
    {
        //フリーカメラに変更
        ChangeCamera(CameraTypes.Free);
        FreeCamera.GetComponent<CameraMouseControl>().enabled = false;
        //externalcamera.cfgは3つ目のコントローラー基準のポジション
        handler.CameraControllerName = d.ControllerName;
        yield return null;
        //指定のコントローラーの子にして座標指定
        currentCamera.transform.SetParent(handler.CameraControllerObject.transform);
        currentCamera.transform.localPosition = new Vector3(d.x, d.y, d.z);
        currentCamera.transform.localRotation = Quaternion.Euler(d.rx, d.ry, d.rz);
        currentCamera.fieldOfView = d.fov;
        //コントローラーは動くのでカメラ位置の保存はできない
        //if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
        //CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
    }

    private CameraMouseControl frontCameraMouseControl = null;
    private CameraMouseControl backCameraMouseControl = null;
    private CameraMouseControl freeCameraMouseControl = null;
    private CameraMouseControl positionFixedCameraMouseControl = null;

    private void SetCameraFOV(float fov)
    {
        if (frontCameraMouseControl == null)
        {


            frontCameraMouseControl = FrontCamera.GetComponent<CameraMouseControl>();
            backCameraMouseControl = BackCamera.GetComponent<CameraMouseControl>();
            freeCameraMouseControl = FreeCamera.GetComponent<CameraMouseControl>();
            positionFixedCameraMouseControl = PositionFixedCamera.GetComponent<CameraMouseControl>();
        }
        CurrentSettings.CameraFOV = fov;
        frontCameraMouseControl.SetCameraFOV(fov);
        backCameraMouseControl.SetCameraFOV(fov);
        freeCameraMouseControl.SetCameraFOV(fov);
        positionFixedCameraMouseControl.SetCameraFOV(fov);
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
    private bool doKeySend = false;

    private async void ControllerAction_KeyDown(object sender, OVRKeyEventArgs e)
    {
        //win.KeyDownEvent{ value = win, new KeyEventArgs((EVRButtonId)e.ButtonId, e.Axis.x, e.Axis.y, e.IsLeft));

        var config = new KeyConfig();
        config.type = KeyTypes.Controller;
        config.actionType = KeyActionTypes.Hand;
        config.keyCode = (int)e.ButtonId;
        config.isLeft = e.IsLeft;
        config.keyIndex = e.IsAxis == false ? -1 : e.ButtonId == Valve.VR.EVRButtonId.k_EButton_SteamVR_Touchpad ? NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y) : 0;
        config.isOculus = IsOculus;
        config.isTouch = e.IsTouch;
        if (e.IsAxis)
        {
            if (config.keyIndex < 0) return;
            if (e.IsLeft) lastLeftAxisPoint = config.keyIndex;
            else lastRightAxisPoint = config.keyIndex;
        }
        if (doKeyConfig || doKeySend) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
        if (!doKeyConfig) CheckKey(config, true);
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
        config.isOculus = IsOculus;
        config.isTouch = e.IsTouch;
        if (e.IsAxis && config.keyIndex != (e.IsLeft ? lastLeftAxisPoint : lastRightAxisPoint))
        {//タッチパッド離した瞬間違うポイントだった場合
            var newindex = config.keyIndex;
            config.keyIndex = (e.IsLeft ? lastLeftAxisPoint : lastRightAxisPoint);
            //前のキーを離す
            if (doKeyConfig) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            else CheckKey(config, false);
            config.keyIndex = newindex;
            if (config.keyIndex < 0) return;
            //新しいキーを押す
            if (doKeyConfig) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            else CheckKey(config, true);
        }
        if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
        if (!doKeyConfig) CheckKey(config, false);
    }

    private int lastLeftAxisPoint = -1;
    private int lastRightAxisPoint = -1;

    private bool isSendingKey = false;
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
            config.isOculus = IsOculus;
            config.isTouch = e.IsTouch;
            //前のキーを離す
            if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            if (!doKeyConfig) CheckKey(config, false);
            config.keyIndex = newindex;
            //新しいキーを押す
            if (doKeyConfig || doKeySend)
            {
                if (isSendingKey == false)
                {
                    isSendingKey = true;
                    await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
                    isSendingKey = false;
                }
            }
            if (!doKeyConfig) CheckKey(config, true);
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
        if (doKeyConfig || doKeySend) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
        if (!doKeyConfig) CheckKey(config, true);
    }

    private /*async*/ void KeyboardAction_KeyUp(object sender, KeyboardEventArgs e)
    {
        var config = new KeyConfig();
        config.type = KeyTypes.Keyboard;
        config.actionType = KeyActionTypes.Face;
        config.keyCode = e.KeyCode;
        config.keyName = e.KeyName;
        if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
        if (!doKeyConfig) CheckKey(config, false);
    }

    private int NearestPointIndex(bool isLeft, float x, float y)
    {
        //Debug.Log($"SearchNearestPoint:{x},{y},{isLeft}");
        int index = 0;
        var points = isLeft ? CurrentSettings.LeftTouchPadPoints : CurrentSettings.RightTouchPadPoints;
        if (points == null) return 0; //未設定時は一つ
        var centerEnable = isLeft ? CurrentSettings.LeftCenterEnable : CurrentSettings.RightCenterEnable;
        if (centerEnable || IsOculus) //センターキー有効時(Oculusの場合はスティックなので、センター無効にする)
        {
            var point_distance = x * x + y * y;
            var r = 2.0f / 5.0f; //半径
            var r2 = r * r;
            if (point_distance < r2) //円内
            {
                if (IsOculus) return -1;
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
        if (CurrentSettings.KeyActions == null) return;
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
                    //手の操作の場合、別の手の操作キーが押されたままだったらそちらを優先して、離す処理は飛ばす
                    var skipKeyUp = false;

                    var doKeyActions = new List<KeyAction>();
                    //手の操作時は左手と右手は分けて処理しないと、右がおしっぱで左を離したときに戻らなくなる
                    foreach (var downaction in CurrentSettings.KeyActions?.OrderBy(d => d.KeyConfigs.Count()).Where(d => d.FaceAction == action.FaceAction && d.HandAction == action.HandAction && d.Hand == action.Hand && d.FunctionAction == action.FunctionAction))
                    {//キーの少ない順に実行して、同時押しと被ったとき同時押しを後から実行して上書きさせる
                     //if (action.KeyConfigs.Count == CurrentKeyConfigs.Count)
                     //{ //別々の機能を同時に押す場合もあるのでキーの数は見てはいけない
                        var downenable = true;
                        foreach (var key in downaction.KeyConfigs)
                        {
                            if (CurrentKeyConfigs.Where(d => d.IsEqualKeyCode(key) == true).Any() == false)
                            {
                                //キーが含まれてないとき
                                downenable = false;
                            }
                        }
                        if (downenable)
                        {//現在押してるキーの中にすべてのキーが含まれていた
                            if (downaction.IsKeyUp)
                            {
                            }
                            else
                            {
                                doKeyActions.Add(downaction);
                            }
                        }
                        //}
                    }
                    if (doKeyActions.Any())
                    {
                        skipKeyUp = true; //優先処理があったので、KeyUpのActionは無効
                        var tmpDownActions = new List<KeyAction>(doKeyActions);
                        foreach (var downaction in tmpDownActions)
                        {
                            foreach (var target in tmpActions.Where(d => d != downaction))
                            {
                                if (target.KeyConfigs.ContainsArray(downaction.KeyConfigs))
                                {//更に複数押しのキー設定が有効な場合、少ないほうは無効(上書きされてしまうため)
                                    doKeyActions.Remove(downaction);
                                }
                            }
                        }
                        foreach (var downaction in doKeyActions)
                        {//残った処理だけ実行
                            DoKeyAction(downaction);
                        }
                    }
                    if (skipKeyUp == false) DoKeyAction(action);
                    CurrentKeyUpActions.Remove(action);
                }
            }
        }
    }


    private void DoKeyAction(KeyAction action)
    {
        if (action.HandAction)
        {
            handController.SetHandAngle(action.Hand == Hands.Left || action.Hand == Hands.Both, action.Hand == Hands.Right || action.Hand == Hands.Both, action.HandAngles, action.HandChangeTime);
        }
        else if (action.FaceAction)
        {
            LipSync.MaxLevel = action.LipSyncMaxLevel;
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
                case Functions.PositionFixedCamera:
                    ChangeCamera(CameraTypes.PositionFixed);
                    break;
            }
        }
    }


    #endregion

    #region Setting

    [Serializable]
    public class StoreTransform
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

        public void SetPosition(Vector3 orig)
        {
            localPosition = orig;
            position = orig;
        }

        public void SetRotation(Transform orig)
        {
            localRotation = orig.localRotation;
            rotation = orig.rotation;
        }

        public void SetPositionAndRotation(Transform orig)
        {
            SetPosition(orig);
            SetRotation(orig);
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
    public class LookTargetSettings
    {
        public Vector3 Offset;
        public float Distance;
        public static LookTargetSettings Create(CameraMouseControl target)
        {
            return new LookTargetSettings { Offset = target.LookOffset, Distance = target.CameraDistance };
        }
        public void Set(CameraMouseControl target)
        {
            Offset = target.LookOffset; Distance = target.CameraDistance;
        }
        public void ApplyTo(CameraMouseControl target)
        {
            target.LookOffset = Offset; target.CameraDistance = Distance;
        }
        public void ApplyTo(Camera camera)
        {
            var target = camera.GetComponent<CameraMouseControl>();
            if (target != null) { target.LookOffset = Offset; target.CameraDistance = Distance; }
        }
    }

    [Serializable]
    public class Settings
    {
        public string VRMPath = null;
        public StoreTransform headTracker = null;
        public StoreTransform bodyTracker = null;
        public StoreTransform leftHandTracker = null;
        public StoreTransform rightHandTracker = null;
        public StoreTransform leftFootTracker = null;
        public StoreTransform rightFootTracker = null;
        [OptionalField]
        public StoreTransform leftElbowTracker = null;
        [OptionalField]
        public StoreTransform rightElbowTracker = null;
        [OptionalField]
        public StoreTransform leftKneeTracker = null;
        [OptionalField]
        public StoreTransform rightKneeTracker = null;
        public Color BackgroundColor;
        public Color CustomBackgroundColor;
        public bool IsTransparent;
        public bool HideBorder;
        public bool IsTopMost;
        public StoreTransform FreeCameraTransform = null;
        public LookTargetSettings FrontCameraLookTargetSettings = null;
        public LookTargetSettings BackCameraLookTargetSettings = null;
        [OptionalField]
        public StoreTransform PositionFixedCameraTransform = null;
        [OptionalField]
        public CameraTypes? CameraType = null;
        [OptionalField]
        public bool ShowCameraGrid = false;
        [OptionalField]
        public bool CameraMirrorEnable = false;
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
        public float RightHandRotation = -90.0f;

        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> Head = Tuple.Create(ETrackedDeviceClass.HMD, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> LeftHand = Tuple.Create(ETrackedDeviceClass.Controller, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> RightHand = Tuple.Create(ETrackedDeviceClass.Controller, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> Pelvis = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> LeftFoot = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> RightFoot = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> LeftElbow = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> RightElbow = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> LeftKnee = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
        [OptionalField]
        public Tuple<ETrackedDeviceClass, string> RightKnee = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));

        [OptionalField]
        public float LeftHandTrackerOffsetToBottom = 0.02f;
        [OptionalField]
        public float LeftHandTrackerOffsetToBodySide = 0.05f;
        [OptionalField]
        public float RightHandTrackerOffsetToBottom = 0.02f;
        [OptionalField]
        public float RightHandTrackerOffsetToBodySide = 0.05f;

        [OptionalField]
        public bool EnableNormalMapFix = true;
        [OptionalField]
        public bool DeleteHairNormalMap = true;

        [OptionalField]
        public bool WebCamEnabled = false;
        [OptionalField]
        public bool WebCamResize = false;
        [OptionalField]
        public bool WebCamMirroring = false;
        [OptionalField]
        public int WebCamBuffering = 0;

        [OptionalField]
        public float CameraFOV = 60.0f;

        [OptionalField]
        public Color LightColor;
        [OptionalField]
        public float LightRotationX;
        [OptionalField]
        public float LightRotationY;

        [OptionalField]
        public int ScreenWidth = 0;
        [OptionalField]
        public int ScreenHeight = 0;
        [OptionalField]
        public int ScreenRefreshRate = 0;

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

            Head = Tuple.Create(ETrackedDeviceClass.HMD, default(string));
            LeftHand = Tuple.Create(ETrackedDeviceClass.Controller, default(string));
            RightHand = Tuple.Create(ETrackedDeviceClass.Controller, default(string));
            Pelvis = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
            LeftFoot = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
            RightFoot = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
            LeftElbow = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
            RightElbow = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
            LeftKnee = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));
            RightKnee = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));

            LeftHandTrackerOffsetToBottom = 0.02f;
            LeftHandTrackerOffsetToBodySide = 0.05f;
            RightHandTrackerOffsetToBottom = 0.02f;
            RightHandTrackerOffsetToBodySide = 0.05f;

            PositionFixedCameraTransform = null;

            EnableNormalMapFix = true;
            DeleteHairNormalMap = true;

            CameraMirrorEnable = false;

            WebCamEnabled = false;
            WebCamResize = false;
            WebCamMirroring = false;
            WebCamBuffering = 0;

            CameraFOV = 60.0f;

            LightColor = Color.white;
            LightRotationX = 130;
            LightRotationY = 43;

            ScreenWidth = 0;
            ScreenHeight = 0;
            ScreenRefreshRate = 0;
        }
    }

    public static Settings CurrentSettings = new Settings();

    private void SaveSettings(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(CurrentSettings)));
    }

    private async void LoadSettings(bool LoadDefault = false, bool IsFirstTime = false, string path = null)
    {
        if (LoadDefault == false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            CurrentSettings = Json.Serializer.Deserialize<Settings>(File.ReadAllText(path));
            //スケールを元に戻す
            //トラッカーのルートスケールを初期値に戻す
            HandTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            HeadTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            PelvisTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            HandTrackerRoot.position = Vector3.zero;
            HeadTrackerRoot.position = Vector3.zero;
            PelvisTrackerRoot.position = Vector3.zero;
            //スケール変更時の位置オフセット設定
            var handTrackerOffset = HandTrackerRoot.GetComponent<ScalePositionOffset>();
            var headTrackerOffset = HeadTrackerRoot.GetComponent<ScalePositionOffset>();
            var footTrackerOffset = PelvisTrackerRoot.GetComponent<ScalePositionOffset>();
            handTrackerOffset.ResetTargetAndPosition();
            headTrackerOffset.ResetTargetAndPosition();
            footTrackerOffset.ResetTargetAndPosition();
        }
        else
        {
            if (IsFirstTime)
            {
                try
                {
                    path = Application.dataPath + "/../default.json";
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
                ImportVRM(CurrentSettings.VRMPath, false, CurrentSettings.EnableNormalMapFix, CurrentSettings.DeleteHairNormalMap);
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

            SetCameraFOV(CurrentSettings.CameraFOV);
            FreeCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();
            FrontCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();
            BackCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();
            PositionFixedCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();


            if (CurrentSettings.FreeCameraTransform != null)
            {
                CurrentSettings.FreeCameraTransform.ToLocalTransform(FreeCamera.transform);
                var control = FreeCamera.GetComponent<CameraMouseControl>();
                control.CameraAngle = -FreeCamera.transform.rotation.eulerAngles;
                control.CameraDistance = Vector3.Distance(FreeCamera.transform.position, Vector3.zero);
                control.CameraTarget = FreeCamera.transform.position + FreeCamera.transform.rotation * Vector3.forward * control.CameraDistance;
            }
            if (CurrentSettings.FrontCameraLookTargetSettings != null)
            {
                CurrentSettings.FrontCameraLookTargetSettings.ApplyTo(FrontCamera);
            }
            if (CurrentSettings.BackCameraLookTargetSettings != null)
            {
                CurrentSettings.BackCameraLookTargetSettings.ApplyTo(BackCamera);
            }
            if (CurrentSettings.PositionFixedCameraTransform != null)
            {
                CurrentSettings.PositionFixedCameraTransform.ToLocalTransform(PositionFixedCamera.transform);
                var control = PositionFixedCamera.GetComponent<CameraMouseControl>();
                control.CameraAngle = -PositionFixedCamera.transform.rotation.eulerAngles;
                control.CameraDistance = Vector3.Distance(PositionFixedCamera.transform.position, Vector3.zero);
                control.CameraTarget = PositionFixedCamera.transform.position + PositionFixedCamera.transform.rotation * Vector3.forward * control.CameraDistance;
                control.UpdateRelativePosition();
            }
            await server.SendCommandAsync(new PipeCommands.LoadCameraFOV { fov = CurrentSettings.CameraFOV });

            UpdateWebCamConfig();
            if (CurrentSettings.CameraType.HasValue)
            {
                ChangeCamera(CurrentSettings.CameraType.Value);
            }
            SetGridVisible(CurrentSettings.ShowCameraGrid);
            await server.SendCommandAsync(new PipeCommands.LoadShowCameraGrid { enable = CurrentSettings.ShowCameraGrid });
            SetCameraMirrorEnable(CurrentSettings.CameraMirrorEnable);
            await server.SendCommandAsync(new PipeCommands.LoadCameraMirror { enable = CurrentSettings.CameraMirrorEnable });
            SetWindowClickThrough(CurrentSettings.WindowClickThrough);
            await server.SendCommandAsync(new PipeCommands.LoadSetWindowClickThrough { enable = CurrentSettings.WindowClickThrough });
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

            await server.SendCommandAsync(new PipeCommands.LoadLipSyncEnable { enable = CurrentSettings.LipSyncEnable });
            SetLipSyncEnable(CurrentSettings.LipSyncEnable);

            await server.SendCommandAsync(new PipeCommands.SetLightAngle { X = CurrentSettings.LightRotationX, Y = CurrentSettings.LightRotationY });
            SetLightAngle(CurrentSettings.LightRotationX, CurrentSettings.LightRotationY);
            await server.SendCommandAsync(new PipeCommands.ChangeLightColor { a = CurrentSettings.LightColor.a, r = CurrentSettings.LightColor.r, g = CurrentSettings.LightColor.g, b = CurrentSettings.LightColor.b });
            ChangeLightColor(CurrentSettings.LightColor.a, CurrentSettings.LightColor.r, CurrentSettings.LightColor.g, CurrentSettings.LightColor.b);

            if (Screen.resolutions.Any(d => d.width == CurrentSettings.ScreenWidth && d.height == CurrentSettings.ScreenHeight && d.refreshRate == CurrentSettings.ScreenRefreshRate))
            {
                Screen.SetResolution(CurrentSettings.ScreenWidth, CurrentSettings.ScreenHeight, false, CurrentSettings.ScreenRefreshRate);
            }

            await server.SendCommandAsync(new PipeCommands.SetWindowNum { Num = CurrentWindowNum });
        }
    }

    #endregion

    private void UpdateWebCamConfig()
    {
        SetCameraEnable(currentCamera);
        VirtualCamera.Buffering_Global = CurrentSettings.WebCamBuffering;
        VirtualCamera.MirrorMode_Global = CurrentSettings.WebCamMirroring ? VirtualCamera.EMirrorMode.MirrorHorizontally : VirtualCamera.EMirrorMode.Disabled;
        VirtualCamera.ResizeMode_Global = CurrentSettings.WebCamResize ? VirtualCamera.EResizeMode.LinearResize : VirtualCamera.EResizeMode.Disabled;
    }

    private void UpdateHandRotation()
    {
        //return; // return for debug
        if (vrik == null) return;
        Transform leftHandAdjusterTransform = vrik.solver.leftArm.target;
        Transform rightHandAdjusterTransform = vrik.solver.rightArm.target;
        if (leftHandAdjusterTransform == null || rightHandAdjusterTransform == null) return;
        var angles = leftHandAdjusterTransform.localEulerAngles;
        leftHandAdjusterTransform.localEulerAngles = new Vector3(CurrentSettings.LeftHandRotation, angles.y, angles.z);
        angles = rightHandAdjusterTransform.localEulerAngles;
        rightHandAdjusterTransform.localEulerAngles = new Vector3(CurrentSettings.RightHandRotation, angles.y, angles.z);
    }

    private void Awake()
    {
        defaultWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_STYLE);
        defaultExWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_EXSTYLE);
    }

    // Update is called once per frame
    void Update()
    {
        KeyboardAction.Update();

        //if (Input.GetKeyDown(KeyCode.P))
        //{
        //    TakePhoto(16000, true);
        //}
    }

    private void TakePhoto(int width, bool transparentBackground, string directory = null)
    {
        Debug.Log($"Resolution:{(int)Screen.currentResolution.width}x{(int)Screen.currentResolution.height}");
        var res = new Resolution { width = width, height = (int)((double)width / (double)Screen.currentResolution.width * (double)Screen.currentResolution.height) };
        if (string.IsNullOrWhiteSpace(directory)) directory = Application.dataPath + "/../Photos";
        if (Directory.Exists(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }
        var filename = $"VirtualMotionCapture_{res.width}x{res.height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.fff}.png";
        if (transparentBackground) BackgroundRenderer.gameObject.SetActive(false);
        File.WriteAllBytes(Path.Combine(directory, filename), Photo.TakePNGPhoto(currentCamera, res, transparentBackground));
        if (transparentBackground) BackgroundRenderer.gameObject.SetActive(true);
        Debug.Log($"Save Photo: {filename}");
    }

    private int WindowX;
    private int WindowY;
    private Vector2 OldMousePos;
    private bool isWindowDragging = false;

    void LateUpdate()
    {
        //Windowの移動操作
        //ドラッグ開始
        if (Input.GetMouseButtonDown((int)MouseButtons.Left) && Input.GetKey(KeyCode.LeftAlt) == false && Input.GetKey(KeyCode.RightAlt) == false)
        {
            var r = GetUnityWindowPosition();
            WindowX = r.left;
            WindowY = r.top;
            OldMousePos = GetWindowsMousePosition();
            isWindowDragging = true;
        }

        //ドラッグ中
        if (Input.GetMouseButton((int)MouseButtons.Left) && isWindowDragging)
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

        if (Input.GetMouseButtonUp((int)MouseButtons.Left) && isWindowDragging)
        {
            isWindowDragging = false;
        }
    }
}
