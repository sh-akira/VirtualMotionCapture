using RootMotion.FinalIK;
using sh_akira;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UnityEngine;
using UnityMemoryMappedFile;
using Valve.VR;
using VMCMod;
using VRM;
using static VMC.NativeMethods;
#if UNITY_EDITOR   // エディタ上でしか動きません。
using UnityEditor;
#endif

namespace VMC
{
    public class ControlWPFWindow : MonoBehaviour
    {
        public bool IsBeta = false;
        public bool IsPreRelease = false;

        public string VersionString;
        private string baseVersionString;

        public Transform LeftWristTransform = null;
        public Transform RightWristTransform = null;

        public CameraLookTarget CalibrationCamera;

        public Renderer BackgroundRenderer;

        public GameObject GridCanvas;

        public DynamicOVRLipSync LipSync;

        public FaceController faceController;
        public HandController handController;

        public WristRotationFix wristRotationFix;

        public Transform HandTrackerRoot;
        public Transform PelvisTrackerRoot;

        public GameObject ExternalMotionSenderObject;
        private ExternalSender externalMotionSender;

        public GameObject ExternalMotionReceiverObject;
        public ExternalReceiverForVMC[] externalMotionReceivers;

        public MemoryMappedFileServer server;
        private string pipeName = Guid.NewGuid().ToString();

        private GameObject CurrentModel = null;

        private RootMotion.FinalIK.VRIK vrik = null;

        private Animator animator = null;

        private int CurrentWindowNum = 1;

        public int CriticalErrorCount = 0;

        public VMTClient vmtClient;

        public PostProcessingManager postProcessingManager;

        private uint defaultWindowStyle;
        private uint defaultExWindowStyle;

        private System.Threading.SynchronizationContext context = null;

        public Action<GameObject> AdditionalSettingAction = null;
        public Action<VRMData> VRMmetaLodedAction = null;
        public Action<string> VRMRemoteLoadedAction = null;

        public Action<GameObject> EyeTracking_TobiiCalibrationAction = null;
        public Action<PipeCommands.SetEyeTracking_TobiiOffsets> SetEyeTracking_TobiiOffsetsAction = null;
        public Action<PipeCommands.SetEyeTracking_ViveProEyeOffsets> SetEyeTracking_ViveProEyeOffsetsAction = null;
        public Action<PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements> SetEyeTracking_ViveProEyeUseEyelidMovementsAction = null;
        public Action<Dictionary<string, string>> SetLipShapeToBlendShapeStringMapAction = null;
        public Func<List<string>> GetLipShapesStringListFunc = null;

        public Behaviour EyeTracking_ViveProEyeComponent = null;
        public Behaviour SRanipal_Eye_FrameworkComponent = null;
        public Behaviour LipTracking_ViveComponent = null;
        public Behaviour SRanipal_Lip_FrameworkComponent = null;

        public MIDICCBlendShape midiCCBlendShape;

        public Transform generatedObject;

        public enum CalibrationState
        {
            Uncalibrated = 0,
            WaitingForCalibrating = 1,
            Calibrating = 2,
            Calibrated = 3,
        }

        public CalibrationState calibrationState = CalibrationState.Uncalibrated;
        public PipeCommands.CalibrateType lastCalibrateType = PipeCommands.CalibrateType.Ipose; //最後に行ったキャリブレーションの種類
        private PipeCommands.CalibrateType currentSelectCalibrateType = PipeCommands.CalibrateType.Ipose;

        public string lastLoadedConfigPath = "";

        public EasyDeviceDiscoveryProtocolManager easyDeviceDiscoveryProtocolManager;

        public ModManager modManager;

        private void Awake()
        {
            Application.targetFrameRate = 60;

#if UNITY_EDITOR   // エディタ上でしか動きません。
            pipeName = "VMCTest";
#else
            //Debug.unityLogger.logEnabled = false;
            pipeName = "VMCpipe" + Guid.NewGuid().ToString();
#endif

#if !UNITY_EDITOR
            //start control panel
            ExecuteControlPanel();
#endif

            context = System.Threading.SynchronizationContext.Current;

            baseVersionString = VersionString.Split('f').First();
            defaultWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_STYLE);
            defaultExWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_EXSTYLE);

            server = new MemoryMappedFileServer();
            server.ReceivedEvent += Server_Received;
            server.Start(pipeName);

            externalMotionSender = ExternalMotionSenderObject.GetComponent<ExternalSender>();
            externalMotionReceivers = ExternalMotionReceiverObject.GetComponentsInChildren<ExternalReceiverForVMC>(true);
        }

        void Start()
        {
            Settings.Current.BackgroundColor = BackgroundRenderer.material.color;
            Settings.Current.CustomBackgroundColor = BackgroundRenderer.material.color;
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
            var buildString = "";
            if (IsBeta)
            {
                buildString = "b" + VersionString.Split('b').Last();
            }
            else if (IsPreRelease)
            {
                buildString = "r" + VersionString.Split('r').Last().Split('b').First();
            }
            else
            {
                buildString = "f" + VersionString.Split('f').Last().Split('r').First();
            }
            NativeMethods.SetUnityWindowTitle($"{Application.productName} {baseVersionString + buildString} ({setWindowNum})");
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

        private bool doStatusStringUpdated = false;
        private async void StatusStringUpdatedEvent(string e)
        {
            if (doStatusStringUpdated)
            {
                await server.SendCommandAsync(new PipeCommands.StatusStringChanged { StatusString = e });
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
            // アプリが終了したらコントロールパネルも終了する。
            server?.SendCommandAsync(new PipeCommands.QuitApplication { });

            server.ReceivedEvent -= Server_Received;
            server?.Dispose();

            Application.logMessageReceived -= LogMessageHandler;
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.SetIsBeta))
                {
                    var d = (PipeCommands.SetIsBeta)e.Data;
                    IsBeta = d.IsBeta;
                    IsPreRelease = d.IsPreRelease;

                    //エラー情報をWPFに飛ばす
                    Application.logMessageReceived += LogMessageHandler;

                    if (IsPreRelease)
                    {
                        modManager.ImportMods();
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.LoadVRM))
                {
                    var d = (PipeCommands.LoadVRM)e.Data;
                    await server.SendCommandAsync(new PipeCommands.ReturnLoadVRM { Data = LoadVRM(d.Path) }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadRemoteVRM))
                {
                    var d = (PipeCommands.LoadRemoteVRM)e.Data;
                    VRMRemoteLoadedAction?.Invoke(d.Path);
                }
                else if (e.CommandType == typeof(PipeCommands.ImportVRM))
                {
                    var d = (PipeCommands.ImportVRM)e.Data;
                    var t = ImportVRM(d.Path, d.ImportForCalibration, d.UseCurrentFixSetting ? Settings.Current.EnableNormalMapFix : d.EnableNormalMapFix, d.UseCurrentFixSetting ? Settings.Current.DeleteHairNormalMap : d.DeleteHairNormalMap);

                    //メタ情報をOSC送信する
                    VRMmetaLodedAction?.Invoke(LoadVRM(d.Path));
                }
                else if (e.CommandType == typeof(PipeCommands.SelectCalibrateMode))
                {
                    var d = (PipeCommands.SelectCalibrateMode)e.Data;
                    currentSelectCalibrateType = d.CalibrateType;
                    SetCalibratePoseToCurrentModel();
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
                    HideWindowBorder(d.enable);
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
                    var d = (PipeCommands.LoadSettings)e.Data;
                    LoadSettings(d.Path);
                    //イベントを登録(何度呼び出しても1回のみ)
                    RegisterEventCallBack();
                }
                else if (e.CommandType == typeof(PipeCommands.SaveSettings))
                {
                    var d = (PipeCommands.SaveSettings)e.Data;
                    SaveSettings(d.Path);
                }
                else if (e.CommandType == typeof(PipeCommands.SetControllerTouchPadPoints))
                {
                    var d = (PipeCommands.SetControllerTouchPadPoints)e.Data;
                    if (d.isStick)
                    {
                        Settings.Current.LeftThumbStickPoints = d.LeftPoints;
                        Settings.Current.RightThumbStickPoints = d.RightPoints;
                    }
                    else
                    {
                        Settings.Current.LeftCenterEnable = d.LeftCenterEnable;
                        Settings.Current.RightCenterEnable = d.RightCenterEnable;
                        Settings.Current.LeftTouchPadPoints = d.LeftPoints;
                        Settings.Current.RightTouchPadPoints = d.RightPoints;
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.SetHandAngle))
                {
                    var d = (PipeCommands.SetHandAngle)e.Data;
                    handController.SetHandEulerAngles(d.LeftEnable, d.RightEnable, handController.CalcHandEulerAngles(d.HandAngles));
                }
                else if (e.CommandType == typeof(PipeCommands.GetFaceKeys))
                {
                    await server.SendCommandAsync(new PipeCommands.ReturnFaceKeys { Keys = faceController.BlendShapeClips.Select(d => d.BlendShapeName).ToList() }, e.RequestId);
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
                else if (e.CommandType == typeof(PipeCommands.SetHandFreeOffset))
                {
                    var d = (PipeCommands.SetHandFreeOffset)e.Data;
                    Settings.Current.LeftHandPositionX = d.LeftHandPositionX / 1000f;
                    Settings.Current.LeftHandPositionY = d.LeftHandPositionY / 1000f;
                    Settings.Current.LeftHandPositionZ = d.LeftHandPositionZ / 1000f;
                    Settings.Current.LeftHandRotationX = d.LeftHandRotationX;
                    Settings.Current.LeftHandRotationY = d.LeftHandRotationY;
                    Settings.Current.LeftHandRotationZ = d.LeftHandRotationZ;
                    Settings.Current.RightHandPositionX = d.RightHandPositionX / 1000f;
                    Settings.Current.RightHandPositionY = d.RightHandPositionY / 1000f;
                    Settings.Current.RightHandPositionZ = d.RightHandPositionZ / 1000f;
                    Settings.Current.RightHandRotationX = d.RightHandRotationX;
                    Settings.Current.RightHandRotationY = d.RightHandRotationY;
                    Settings.Current.RightHandRotationZ = d.RightHandRotationZ;
                    Settings.Current.SwivelOffset = d.SwivelOffset;
                    SetHandFreeOffset();
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
                        LeftHandTrackerOffsetToBodySide = Settings.Current.LeftHandTrackerOffsetToBodySide,
                        LeftHandTrackerOffsetToBottom = Settings.Current.LeftHandTrackerOffsetToBottom,
                        RightHandTrackerOffsetToBodySide = Settings.Current.RightHandTrackerOffsetToBodySide,
                        RightHandTrackerOffsetToBottom = Settings.Current.RightHandTrackerOffsetToBottom
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetTrackerOffsets))
                {
                    var d = (PipeCommands.SetTrackerOffsets)e.Data;
                    Settings.Current.LeftHandTrackerOffsetToBodySide = d.LeftHandTrackerOffsetToBodySide;
                    Settings.Current.LeftHandTrackerOffsetToBottom = d.LeftHandTrackerOffsetToBottom;
                    Settings.Current.RightHandTrackerOffsetToBodySide = d.RightHandTrackerOffsetToBodySide;
                    Settings.Current.RightHandTrackerOffsetToBottom = d.RightHandTrackerOffsetToBottom;

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
                    Settings.Current.ScreenWidth = d.Width;
                    Settings.Current.ScreenHeight = d.Height;
                    Settings.Current.ScreenRefreshRate = d.RefreshRate;
                    Screen.SetResolution(d.Width, d.Height, false, d.RefreshRate);
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
                    //イベントを登録(何度呼び出しても1回のみ)
                    RegisterEventCallBack();

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
                else if (e.CommandType == typeof(PipeCommands.SetEyeTracking_TobiiOffsets))
                {
                    var d = (PipeCommands.SetEyeTracking_TobiiOffsets)e.Data;
                    SetEyeTracking_TobiiOffsets(d);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEyeTracking_TobiiOffsets))
                {
                    await server.SendCommandAsync(new PipeCommands.SetEyeTracking_TobiiOffsets
                    {
                        OffsetHorizontal = Settings.Current.EyeTracking_TobiiOffsetHorizontal,
                        OffsetVertical = Settings.Current.EyeTracking_TobiiOffsetVertical,
                        ScaleHorizontal = Settings.Current.EyeTracking_TobiiScaleHorizontal,
                        ScaleVertical = Settings.Current.EyeTracking_TobiiScaleVertical
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.EyeTracking_TobiiCalibration))
                {
                    EyeTracking_TobiiCalibrationAction?.Invoke(CurrentModel);
                }
                else if (e.CommandType == typeof(PipeCommands.SetEyeTracking_ViveProEyeOffsets))
                {
                    var d = (PipeCommands.SetEyeTracking_ViveProEyeOffsets)e.Data;
                    SetEyeTracking_ViveProEyeOffsets(d);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEyeTracking_ViveProEyeOffsets))
                {
                    await server.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeOffsets
                    {
                        OffsetHorizontal = Settings.Current.EyeTracking_ViveProEyeOffsetHorizontal,
                        OffsetVertical = Settings.Current.EyeTracking_ViveProEyeOffsetVertical,
                        ScaleHorizontal = Settings.Current.EyeTracking_ViveProEyeScaleHorizontal,
                        ScaleVertical = Settings.Current.EyeTracking_ViveProEyeScaleVertical
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements))
                {
                    var d = (PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements)e.Data;
                    SetEyeTracking_ViveProEyeUseEyelidMovements(d);
                }
                else if (e.CommandType == typeof(PipeCommands.SetEyeTracking_ViveProEyeEnable))
                {
                    var d = (PipeCommands.SetEyeTracking_ViveProEyeEnable)e.Data;
                    Settings.Current.EyeTracking_ViveProEyeEnable = d.enable;
                    SetEyeTracking_ViveProEyeEnable(d.enable);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEyeTracking_ViveProEyeUseEyelidMovements))
                {
                    await server.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements
                    {
                        Use = Settings.Current.EyeTracking_ViveProEyeUseEyelidMovements,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEyeTracking_ViveProEyeEnable))
                {
                    await server.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeEnable
                    {
                        enable = Settings.Current.EyeTracking_ViveProEyeEnable,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.LoadCurrentSettings))
                {
                    if (isFirstTimeExecute)
                    {
                        isFirstTimeExecute = false;
                        CurrentWindowNum = SetWindowTitle();
                        //起動時は初期設定ロード
                        LoadSettings(null);
                        //イベントを登録(何度呼び出しても1回のみ)
                        RegisterEventCallBack();
                    }
                    else
                    {
                        //現在の設定を再適用する
                        ApplySettings();
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.EnableExternalMotionSender))
                {
                    var d = (PipeCommands.EnableExternalMotionSender)e.Data;
                    SetExternalMotionSenderEnable(d.enable);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEnableExternalMotionSender))
                {
                    await server.SendCommandAsync(new PipeCommands.EnableExternalMotionSender
                    {
                        enable = Settings.Current.ExternalMotionSenderEnable
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.ChangeExternalMotionSenderAddress))
                {
                    var d = (PipeCommands.ChangeExternalMotionSenderAddress)e.Data;
                    ChangeExternalMotionSenderAddress(d.address, d.port, d.PeriodStatus, d.PeriodRoot, d.PeriodBone, d.PeriodBlendShape, d.PeriodCamera, d.PeriodDevices, d.OptionString, d.ResponderEnable);

                }
                else if (e.CommandType == typeof(PipeCommands.GetExternalMotionSenderAddress))
                {
                    await server.SendCommandAsync(new PipeCommands.ChangeExternalMotionSenderAddress
                    {
                        address = Settings.Current.ExternalMotionSenderAddress,
                        port = Settings.Current.ExternalMotionSenderPort,
                        PeriodStatus = Settings.Current.ExternalMotionSenderPeriodStatus,
                        PeriodRoot = Settings.Current.ExternalMotionSenderPeriodRoot,
                        PeriodBone = Settings.Current.ExternalMotionSenderPeriodBone,
                        PeriodBlendShape = Settings.Current.ExternalMotionSenderPeriodBlendShape,
                        PeriodCamera = Settings.Current.ExternalMotionSenderPeriodCamera,
                        PeriodDevices = Settings.Current.ExternalMotionSenderPeriodDevices,
                        OptionString = Settings.Current.ExternalMotionSenderOptionString,
                        ResponderEnable = Settings.Current.ExternalMotionSenderResponderEnable
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.EnableExternalMotionReceiver))
                {
                    var d = (PipeCommands.EnableExternalMotionReceiver)e.Data;
                    SetExternalMotionReceiverEnable(d.enable, d.index);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEnableExternalMotionReceiver))
                {
                    var d = (PipeCommands.GetEnableExternalMotionReceiver)e.Data;
                    await server.SendCommandAsync(new PipeCommands.EnableExternalMotionReceiver
                    {
                        enable = Settings.Current.ExternalMotionReceiverEnableList[d.index],
                        index = d.index
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.ChangeExternalMotionReceiverPort))
                {
                    var d = (PipeCommands.ChangeExternalMotionReceiverPort)e.Data;
                    ChangeExternalMotionReceiverPort(d.ports, d.RequesterEnable);

                }
                else if (e.CommandType == typeof(PipeCommands.GetExternalMotionReceiverPort))
                {
                    await server.SendCommandAsync(new PipeCommands.ChangeExternalMotionReceiverPort
                    {
                        ports = Settings.Current.ExternalMotionReceiverPortList.ToArray(),
                        RequesterEnable = Settings.Current.ExternalMotionReceiverRequesterEnable
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetMidiCCBlendShape))
                {
                    var bs = Settings.Current.MidiCCBlendShape;
                    await server.SendCommandAsync(new PipeCommands.SetMidiCCBlendShape
                    {
                        BlendShapes = Settings.Current.MidiCCBlendShape,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetMidiCCBlendShape))
                {
                    var d = (PipeCommands.SetMidiCCBlendShape)e.Data;
                    SetMidiCCBlendShape(d.BlendShapes);
                }
                else if (e.CommandType == typeof(PipeCommands.GetMidiEnable))
                {
                    await server.SendCommandAsync(new PipeCommands.MidiEnable
                    {
                        enable = Settings.Current.MidiEnable,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.MidiEnable))
                {
                    var d = (PipeCommands.MidiEnable)e.Data;
                    SetMidiEnable(d.enable);
                }
                else if (e.CommandType == typeof(PipeCommands.EnableTrackingFilter))
                {
                    var d = (PipeCommands.EnableTrackingFilter)e.Data;
                    SetTrackingFilterEnable(d.globalEnable, d.hmdEnable, d.controllerEnable, d.trackerEnable);
                }
                else if (e.CommandType == typeof(PipeCommands.GetPauseTracking))
                {
                    await server.SendCommandAsync(new PipeCommands.PauseTracking
                    {
                        enable = DeviceInfo.pauseTracking
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.PauseTracking))
                {
                    var d = (PipeCommands.PauseTracking)e.Data;
                    DeviceInfo.pauseTracking = d.enable;
                }
                else if (e.CommandType == typeof(PipeCommands.GetEnableTrackingFilter))
                {
                    await server.SendCommandAsync(new PipeCommands.EnableTrackingFilter
                    {
                        globalEnable = Settings.Current.TrackingFilterEnable,
                        hmdEnable = Settings.Current.TrackingFilterHmdEnable,
                        controllerEnable = Settings.Current.TrackingFilterControllerEnable,
                        trackerEnable = Settings.Current.TrackingFilterTrackerEnable,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.EnableModelModifier))
                {
                    var d = (PipeCommands.EnableModelModifier)e.Data;
                    SetModelModifierEnable(d.fixKneeRotation, d.fixElbowRotation);
                }
                else if (e.CommandType == typeof(PipeCommands.GetEnableModelModifier))
                {
                    await server.SendCommandAsync(new PipeCommands.EnableModelModifier
                    {
                        fixKneeRotation = Settings.Current.FixKneeRotation,
                        fixElbowRotation = Settings.Current.FixElbowRotation,
                    }, e.RequestId);
                }
                //------------------------
                else if (e.CommandType == typeof(PipeCommands.GetStatusString))
                {
                    string statusStringBuf = "";
                    //有効な場合だけ送る
                    if (externalMotionReceivers[0].isActiveAndEnabled)
                    {
                        statusStringBuf = externalMotionReceivers?[0]?.statusString;
                    }
                    await server.SendCommandAsync(new PipeCommands.SetStatusString
                    {
                        StatusString = statusStringBuf,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.StatusStringChangedRequest))
                {
                    var d = (PipeCommands.StatusStringChangedRequest)e.Data;
                    doStatusStringUpdated = d.doSend;
                }
                else if (e.CommandType == typeof(PipeCommands.EnableHandleControllerAsTracker))
                {
                    var d = (PipeCommands.EnableHandleControllerAsTracker)e.Data;
                    SetHandleControllerAsTracker(d.HandleControllerAsTracker);
                }
                else if (e.CommandType == typeof(PipeCommands.GetHandleControllerAsTracker))
                {
                    await server.SendCommandAsync(new PipeCommands.EnableHandleControllerAsTracker
                    {
                        HandleControllerAsTracker = Settings.Current.HandleControllerAsTracker
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetQualitySettings))
                {
                    await server.SendCommandAsync(new PipeCommands.SetQualitySettings
                    {
                        antiAliasing = Settings.Current.AntiAliasing,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetQualitySettings))
                {
                    var d = (PipeCommands.SetQualitySettings)e.Data;
                    SetQualitySettings(d);
                }
                else if (e.CommandType == typeof(PipeCommands.GetVirtualMotionTracker))
                {
                    await server.SendCommandAsync(new PipeCommands.SetVirtualMotionTracker
                    {
                        enable = vmtClient.GetEnable(),
                        no = vmtClient.GetNo()
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetVirtualMotionTracker))
                {
                    var d = (PipeCommands.SetVirtualMotionTracker)e.Data;
                    SetVMT(d.enable, d.no);
                }
                else if (e.CommandType == typeof(PipeCommands.SetupVirtualMotionTracker))
                {
                    var d = (PipeCommands.SetupVirtualMotionTracker)e.Data;
                    var ret = d.install ? await VMTServer.InstallVMT() : await VMTServer.UninstallVMT();
                    await server.SendCommandAsync(new PipeCommands.ResultSetupVirtualMotionTracker
                    {
                        result = ret,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetViveLipTrackingBlendShape))
                {
                    if (GetLipShapesStringListFunc != null)
                    {
                        await server.SendCommandAsync(new PipeCommands.SetViveLipTrackingBlendShape
                        {
                            LipShapes = GetLipShapesStringListFunc(),
                            LipShapesToBlendShapeMap = Settings.Current.LipShapesToBlendShapeMap,
                        }, e.RequestId);
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.GetViveLipTrackingEnable))
                {
                    await server.SendCommandAsync(new PipeCommands.SetViveLipTrackingEnable
                    {
                        enable = Settings.Current.LipTracking_ViveEnable,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetViveLipTrackingEnable))
                {
                    var d = (PipeCommands.SetViveLipTrackingEnable)e.Data;
                    Settings.Current.LipTracking_ViveEnable = d.enable;
                    SetLipTracking_ViveEnable(d.enable);
                }
                else if (e.CommandType == typeof(PipeCommands.SetViveLipTrackingBlendShape))
                {
                    var d = (PipeCommands.SetViveLipTrackingBlendShape)e.Data;
                    Settings.Current.LipShapesToBlendShapeMap = d.LipShapesToBlendShapeMap;
                    SetLipShapeToBlendShapeStringMapAction?.Invoke(d.LipShapesToBlendShapeMap);
                }
                else if (e.CommandType == typeof(PipeCommands.GetAdvancedGraphicsOption))
                {
                    LoadAdvancedGraphicsOption();
                }
                else if (e.CommandType == typeof(PipeCommands.SetAdvancedGraphicsOption))
                {
                    var d = (PipeCommands.SetAdvancedGraphicsOption)e.Data;

                    Settings.Current.PPS_Enable = d.PPS_Enable;

                    Settings.Current.PPS_Bloom_Enable = d.Bloom_Enable;
                    Settings.Current.PPS_Bloom_Intensity = d.Bloom_Intensity;
                    Settings.Current.PPS_Bloom_Threshold = d.Bloom_Threshold;

                    Settings.Current.PPS_DoF_Enable = d.DoF_Enable;
                    Settings.Current.PPS_DoF_FocusDistance = d.DoF_FocusDistance;
                    Settings.Current.PPS_DoF_Aperture = d.DoF_Aperture;
                    Settings.Current.PPS_DoF_FocusLength = d.DoF_FocusLength;
                    Settings.Current.PPS_DoF_MaxBlurSize = d.DoF_MaxBlurSize;

                    Settings.Current.PPS_CG_Enable = d.CG_Enable;
                    Settings.Current.PPS_CG_Temperature = d.CG_Temperature;
                    Settings.Current.PPS_CG_Saturation = d.CG_Saturation;
                    Settings.Current.PPS_CG_Contrast = d.CG_Contrast;
                    Settings.Current.PPS_CG_Gamma = d.CG_Gamma;

                    Settings.Current.PPS_Vignette_Enable = d.Vignette_Enable;
                    Settings.Current.PPS_Vignette_Intensity = d.Vignette_Intensity;
                    Settings.Current.PPS_Vignette_Smoothness = d.Vignette_Smoothness;
                    Settings.Current.PPS_Vignette_Roundness = d.Vignette_Roundness;

                    Settings.Current.PPS_CA_Enable = d.CA_Enable;
                    Settings.Current.PPS_CA_Intensity = d.CA_Intensity;
                    Settings.Current.PPS_CA_FastMode = d.CA_FastMode;

                    Settings.Current.PPS_Bloom_Color_a = d.Bloom_Color_a;
                    Settings.Current.PPS_Bloom_Color_r = d.Bloom_Color_r;
                    Settings.Current.PPS_Bloom_Color_g = d.Bloom_Color_g;
                    Settings.Current.PPS_Bloom_Color_b = d.Bloom_Color_b;

                    Settings.Current.PPS_CG_ColorFilter_a = d.CG_ColorFilter_a;
                    Settings.Current.PPS_CG_ColorFilter_r = d.CG_ColorFilter_r;
                    Settings.Current.PPS_CG_ColorFilter_g = d.CG_ColorFilter_g;
                    Settings.Current.PPS_CG_ColorFilter_b = d.CG_ColorFilter_b;

                    Settings.Current.PPS_Vignette_Color_a = d.Vignette_Color_a;
                    Settings.Current.PPS_Vignette_Color_r = d.Vignette_Color_r;
                    Settings.Current.PPS_Vignette_Color_g = d.Vignette_Color_g;
                    Settings.Current.PPS_Vignette_Color_b = d.Vignette_Color_b;

                    Settings.Current.TurnOffAmbientLight = d.TurnOffAmbientLight;

                    SetAdvancedGraphicsOption();
                }
                else if (e.CommandType == typeof(PipeCommands.ExternalReceiveBones))
                {
                    var d = (PipeCommands.ExternalReceiveBones)e.Data;
                    SetExternalBonesReceiverEnable(d.ReceiveBonesEnable);
                }

                else if (e.CommandType == typeof(PipeCommands.GetExternalReceiveBones))
                {
                    await server.SendCommandAsync(new PipeCommands.ExternalReceiveBones
                    {
                        ReceiveBonesEnable = Settings.Current.ExternalBonesReceiverEnable
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetModIsLoaded))
                {
                    await server.SendCommandAsync(new PipeCommands.ReturnModIsLoaded
                    {
                        IsLoaded = modManager.IsModLoaded,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetModList))
                {
                    await server.SendCommandAsync(new PipeCommands.ReturnModList
                    {
                        ModList = GetModList(),
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.ModSettingEvent))
                {
                    var d = (PipeCommands.ModSettingEvent)e.Data;
                    modManager.InvokeSetting(d.InstanceId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetLogNotifyLevel))
                {
                    var d = (PipeCommands.SetLogNotifyLevel)e.Data;
                    notifyLogLevel = d.type;
                }

                else if (e.CommandType == typeof(PipeCommands.Alive))
                {
                    await server.SendCommandAsync(new PipeCommands.Alive { });
                }
            }, null);
        }

        private List<ModItem> GetModList()
        {
            var modList = new List<ModItem>();

            foreach (var attribute in modManager.GetModsList())
            {
                var item = new ModItem
                {
                    Name = attribute.Name,
                    Version = attribute.Version,
                    Author = attribute.Author,
                    AuthorURL = attribute.AuthorURL,
                    Description = attribute.Description,
                    PluginURL = attribute.PluginURL,
                    InstanceId = attribute.InstanceId,
                    AssemblyPath = attribute.AssemblyPath,
                };
                modList.Add(item);
            }

            return modList;
        }

        public Transform MainDirectionalLightTransform;
        public Light MainDirectionalLight;

        private void SetLightAngle(float x, float y)
        {
            if (MainDirectionalLightTransform != null)
            {
                MainDirectionalLightTransform.eulerAngles = new Vector3(x, y, MainDirectionalLightTransform.eulerAngles.z);
                Settings.Current.LightRotationX = x;
                Settings.Current.LightRotationY = y;

                VMCEvents.OnLightChanged?.Invoke();
            }
        }

        private void ChangeLightColor(float a, float r, float g, float b)
        {
            if (MainDirectionalLight != null)
            {
                Settings.Current.LightColor = new Color(r, g, b, a);
                MainDirectionalLight.color = Settings.Current.LightColor;

                VMCEvents.OnLightChanged?.Invoke();
            }
        }

        private void SetQualitySettings(PipeCommands.SetQualitySettings setting)
        {
            Settings.Current.AntiAliasing = setting.antiAliasing;
            QualitySettings.antiAliasing = setting.antiAliasing;
        }

        private void SetVMT(bool enable, int no)
        {
            vmtClient.SetNo(no);
            vmtClient.SetEnable(enable);
            vmtClient.SendRoomMatrixTemporary();

            Settings.Current.VirtualMotionTrackerNo = no;
            Settings.Current.VirtualMotionTrackerEnable = enable;
        }

        private async void LoadAdvancedGraphicsOption()
        {
            SetAdvancedGraphicsOption();
            await server.SendCommandAsync(new PipeCommands.SetAdvancedGraphicsOption
            {
                PPS_Enable = Settings.Current.PPS_Enable,

                Bloom_Enable = Settings.Current.PPS_Bloom_Enable,
                Bloom_Intensity = Settings.Current.PPS_Bloom_Intensity,
                Bloom_Threshold = Settings.Current.PPS_Bloom_Threshold,

                DoF_Enable = Settings.Current.PPS_DoF_Enable,
                DoF_FocusDistance = Settings.Current.PPS_DoF_FocusDistance,
                DoF_Aperture = Settings.Current.PPS_DoF_Aperture,
                DoF_FocusLength = Settings.Current.PPS_DoF_FocusLength,
                DoF_MaxBlurSize = Settings.Current.PPS_DoF_MaxBlurSize,

                CG_Enable = Settings.Current.PPS_CG_Enable,
                CG_Temperature = Settings.Current.PPS_CG_Temperature,
                CG_Saturation = Settings.Current.PPS_CG_Saturation,
                CG_Contrast = Settings.Current.PPS_CG_Contrast,
                CG_Gamma = Settings.Current.PPS_CG_Gamma,

                Vignette_Enable = Settings.Current.PPS_Vignette_Enable,
                Vignette_Intensity = Settings.Current.PPS_Vignette_Intensity,
                Vignette_Smoothness = Settings.Current.PPS_Vignette_Smoothness,
                Vignette_Roundness = Settings.Current.PPS_Vignette_Roundness,

                CA_Enable = Settings.Current.PPS_CA_Enable,
                CA_Intensity = Settings.Current.PPS_CA_Intensity,
                CA_FastMode = Settings.Current.PPS_CA_FastMode,

                Bloom_Color_a = Settings.Current.PPS_Bloom_Color_a,
                Bloom_Color_r = Settings.Current.PPS_Bloom_Color_r,
                Bloom_Color_g = Settings.Current.PPS_Bloom_Color_g,
                Bloom_Color_b = Settings.Current.PPS_Bloom_Color_b,

                CG_ColorFilter_a = Settings.Current.PPS_CG_ColorFilter_a,
                CG_ColorFilter_r = Settings.Current.PPS_CG_ColorFilter_r,
                CG_ColorFilter_g = Settings.Current.PPS_CG_ColorFilter_g,
                CG_ColorFilter_b = Settings.Current.PPS_CG_ColorFilter_b,

                Vignette_Color_a = Settings.Current.PPS_Vignette_Color_a,
                Vignette_Color_r = Settings.Current.PPS_Vignette_Color_r,
                Vignette_Color_g = Settings.Current.PPS_Vignette_Color_g,
                Vignette_Color_b = Settings.Current.PPS_Vignette_Color_b,

                TurnOffAmbientLight = Settings.Current.TurnOffAmbientLight
            });
        }

        private void SetAdvancedGraphicsOption()
        {
            postProcessingManager.Apply(Settings.Current);
        }

        private bool isFirstTimeExecute = true;

        #region VRM

        public VRMData LoadVRM(string path)
        {
            if (string.IsNullOrEmpty(path) || File.Exists(path) == false)
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

        public async Task ImportVRM(string path, bool ImportForCalibration, bool EnableNormalMapFix, bool DeleteHairNormalMap)
        {
            if (ImportForCalibration == false)
            {
                calibrationState = CalibrationState.Uncalibrated; //キャリブレーション状態を"未キャリブレーション"に設定
                Settings.Current.VRMPath = path;
                var context = new VRMImporterContext();

                var bytes = File.ReadAllBytes(path);

                // GLB形式でJSONを取得しParseします
                context.ParseGlb(bytes);

                // ParseしたJSONをシーンオブジェクトに変換していく
                //CurrentModel = await VRMImporter.LoadVrmAsync(context);
                await context.LoadAsyncTask();
                context.ShowMeshes();

                //BlendShape目線制御時の表情とのぶつかりを防ぐ
                if (context.GLTF.extensions.VRM.firstPerson.lookAtType == LookAtType.BlendShape)
                {
                    var applyer = context.Root.GetComponent<VRMLookAtBlendShapeApplyer>();
                    applyer.enabled = false;

                    var vmcapplyer = context.Root.AddComponent<VMC_VRMLookAtBlendShapeApplyer>();
                    vmcapplyer.OnImported(context);
                    vmcapplyer.faceController = faceController;
                }

                LoadNewModel(context.Root);
            }
            else
            {
                calibrationState = CalibrationState.WaitingForCalibrating; //キャリブレーション状態を"キャリブレーション待機中"に設定

                if (CurrentModel != null)
                {
                    var currentvrik = CurrentModel.GetComponent<VRIK>();
                    if (currentvrik != null) Destroy(currentvrik);
                    var rootController = CurrentModel.GetComponent<VRIKRootController>();
                    if (rootController != null) Destroy(rootController);
                }
                LoadDefaultCurrentModelTransforms();
                //SetVRIK(CurrentModel);
                if (animator != null)
                {
                    SetCalibratePoseToCurrentModel();

                    //wristRotationFix.SetVRIK(vrik);

                    handController.SetDefaultAngle(animator);

                    //トラッカーのスケールリセット
                    HandTrackerRoot.localPosition = Vector3.zero;
                    HandTrackerRoot.localScale = Vector3.one;
                    PelvisTrackerRoot.localPosition = Vector3.zero;
                    PelvisTrackerRoot.localScale = Vector3.one;

                    //トラッカー位置の表示
                    TrackingPointManager.Instance.SetTrackingPointPositionVisible(true);

                    if (CalibrationCamera != null)
                    {
                        CalibrationCamera.Target = animator.GetBoneTransform(HumanBodyBones.Head);
                        CalibrationCamera.gameObject.SetActive(true);
                    }
                }
            }
        }

        private void SetCalibratePoseToCurrentModel()
        {
            if (animator != null)
            {
                if (currentSelectCalibrateType == PipeCommands.CalibrateType.Ipose)
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftShoulder).localEulerAngles = new Vector3(0, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightShoulder).localEulerAngles = new Vector3(0, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(0, 0, 80);
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(0, 0, 5);
                    animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(0, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(0, 0, -80);
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(0, 0, -5);
                    animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(0, 0, 0);
                }
                else
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(LeftLowerArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(RightLowerArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(LeftUpperArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(RightUpperArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(LeftHandAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(RightHandAngle, 0, 0);
                }
            }
        }

            public void LoadNewModel(GameObject model)
        {
            if (CurrentModel != null)
            {
                VMCEvents.OnModelUnloading?.Invoke(CurrentModel);
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

            //Settings.Current.EnableNormalMapFix = EnableNormalMapFix;
            //Settings.Current.DeleteHairNormalMap = DeleteHairNormalMap;
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
                wristRotationFix.SetVRIK(vrik);

                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).eulerAngles = new Vector3(LeftLowerArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm).eulerAngles = new Vector3(RightLowerArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).eulerAngles = new Vector3(LeftUpperArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).eulerAngles = new Vector3(RightUpperArmAngle, 0, 0);

                handController.SetDefaultAngle(animator);
            }
            //SetTrackersToVRIK();

            VMCEvents.OnModelLoaded?.Invoke(CurrentModel);
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

        public void FixLegDirection(GameObject targetHumanoidModel)
        {
            var avatarForward = targetHumanoidModel.transform.forward;
            var animator = targetHumanoidModel.GetComponent<Animator>();

            var leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var leftFootDefaultRotation = leftFoot.rotation;
            var leftFootTargetPosition = new Vector3(leftFoot.position.x, leftFoot.position.y, leftFoot.position.z);
            LookAtBones(leftFootTargetPosition + avatarForward * 0.03f, leftUpperLeg, leftLowerLeg);
            LookAtBones(leftFootTargetPosition, leftLowerLeg, leftFoot);
            leftFoot.rotation = leftFootDefaultRotation;

            var rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var rightFootDefaultRotation = rightFoot.rotation;
            var rightFootTargetPosition = new Vector3(rightFoot.position.x, rightFoot.position.y, rightFoot.position.z);
            LookAtBones(rightFootTargetPosition + avatarForward * 0.03f, rightUpperLeg, rightLowerLeg);
            LookAtBones(rightFootTargetPosition, rightLowerLeg, rightFoot);
            rightFoot.rotation = rightFootDefaultRotation;
        }

        public void FixArmDirection(GameObject targetHumanoidModel)
        {
            var avatarForward = targetHumanoidModel.transform.forward;
            var animator = targetHumanoidModel.GetComponent<Animator>();

            var leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var leftHandDefaultRotation = leftHand.rotation;
            var leftHandTargetPosition = new Vector3(leftHand.position.x, leftHand.position.y, leftHand.position.z);
            LookAtBones(leftHandTargetPosition + avatarForward * 0.01f, leftShoulder, leftUpperArm);
            LookAtBones(leftHandTargetPosition - avatarForward * 0.01f, leftUpperArm, leftLowerArm);
            LookAtBones(leftHandTargetPosition, leftLowerArm, leftHand);
            leftHand.rotation = leftHandDefaultRotation;

            var rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            var rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var rightHandDefaultRotation = rightHand.rotation;
            var rightHandTargetPosition = new Vector3(rightHand.position.x, rightHand.position.y, rightHand.position.z);
            LookAtBones(rightHandTargetPosition + avatarForward * 0.01f, rightShoulder, rightUpperArm);
            LookAtBones(rightHandTargetPosition - avatarForward * 0.01f, rightUpperArm, rightLowerArm);
            LookAtBones(rightHandTargetPosition, rightLowerArm, rightHand);
            rightHand.rotation = rightHandDefaultRotation;
        }

        private void LookAtBones(Vector3 lookTargetPosition, params Transform[] bones)
        {
            for (int i = 0; i < bones.Length - 1; i++)
            {
                bones[i].rotation = Quaternion.FromToRotation((bones[i].position - bones[i + 1].position).normalized, (bones[i].position - lookTargetPosition).normalized) * bones[i].rotation;
            }
        }

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
            if (animator != null && Settings.Current.FixKneeRotation)
            {
                //leftOffset = fixKneeBone(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot));
                //rightOffset = fixKneeBone(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot));
                //fixPelvisBone(animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.Hips));
                FixLegDirection(model);
            }

            if (animator != null && Settings.Current.FixElbowRotation)
            {
                FixArmDirection(model);
            }

            vrik = model.AddComponent<RootMotion.FinalIK.VRIK>();
            vrik.AutoDetectReferences();

            //親指の方向の検出に失敗すると腕の回転もおかしくなる
            vrik.solver.leftArm.palmToThumbAxis = new Vector3(0, 0, 1);
            vrik.solver.rightArm.palmToThumbAxis = new Vector3(0, 0, 1);

            vrik.solver.FixTransforms();

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

        private List<Tuple<string, string>> GetTrackerSerialNumbers()
        {
            var list = new List<Tuple<string, string>>();
            foreach (var trackingPoint in TrackingPointManager.Instance.GetTrackingPoints())
            {
                if (trackingPoint.DeviceClass == ETrackedDeviceClass.HMD)
                {
                    list.Add(Tuple.Create("HMD", trackingPoint.Name));
                }
                else if (trackingPoint.DeviceClass == ETrackedDeviceClass.Controller)
                {
                    list.Add(Tuple.Create("コントローラー", trackingPoint.Name));
                }
                else if (trackingPoint.DeviceClass == ETrackedDeviceClass.GenericTracker)
                {
                    list.Add(Tuple.Create("トラッカー", trackingPoint.Name));
                }
                else
                {
                    list.Add(Tuple.Create("Unknown", trackingPoint.Name));
                }
            }
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
                Head = Tuple.Create(deviceDictionary[Settings.Current.Head.Item1], Settings.Current.Head.Item2),
                LeftHand = Tuple.Create(deviceDictionary[Settings.Current.LeftHand.Item1], Settings.Current.LeftHand.Item2),
                RightHand = Tuple.Create(deviceDictionary[Settings.Current.RightHand.Item1], Settings.Current.RightHand.Item2),
                Pelvis = Tuple.Create(deviceDictionary[Settings.Current.Pelvis.Item1], Settings.Current.Pelvis.Item2),
                LeftFoot = Tuple.Create(deviceDictionary[Settings.Current.LeftFoot.Item1], Settings.Current.LeftFoot.Item2),
                RightFoot = Tuple.Create(deviceDictionary[Settings.Current.RightFoot.Item1], Settings.Current.RightFoot.Item2),
                LeftElbow = Tuple.Create(deviceDictionary[Settings.Current.LeftElbow.Item1], Settings.Current.LeftElbow.Item2),
                RightElbow = Tuple.Create(deviceDictionary[Settings.Current.RightElbow.Item1], Settings.Current.RightElbow.Item2),
                LeftKnee = Tuple.Create(deviceDictionary[Settings.Current.LeftKnee.Item1], Settings.Current.LeftKnee.Item2),
                RightKnee = Tuple.Create(deviceDictionary[Settings.Current.RightKnee.Item1], Settings.Current.RightKnee.Item2),
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

            Settings.Current.Head = Tuple.Create(deviceDictionary[data.Head.Item1], data.Head.Item2);
            Settings.Current.LeftHand = Tuple.Create(deviceDictionary[data.LeftHand.Item1], data.LeftHand.Item2);
            Settings.Current.RightHand = Tuple.Create(deviceDictionary[data.RightHand.Item1], data.RightHand.Item2);
            Settings.Current.Pelvis = Tuple.Create(deviceDictionary[data.Pelvis.Item1], data.Pelvis.Item2);
            Settings.Current.LeftFoot = Tuple.Create(deviceDictionary[data.LeftFoot.Item1], data.LeftFoot.Item2);
            Settings.Current.RightFoot = Tuple.Create(deviceDictionary[data.RightFoot.Item1], data.RightFoot.Item2);
            Settings.Current.LeftElbow = Tuple.Create(deviceDictionary[data.LeftElbow.Item1], data.LeftElbow.Item2);
            Settings.Current.RightElbow = Tuple.Create(deviceDictionary[data.RightElbow.Item1], data.RightElbow.Item2);
            Settings.Current.LeftKnee = Tuple.Create(deviceDictionary[data.LeftKnee.Item1], data.LeftKnee.Item2);
            Settings.Current.RightKnee = Tuple.Create(deviceDictionary[data.RightKnee.Item1], data.RightKnee.Item2);
            SetVRIKTargetTrackers();
        }

        private enum TargetType
        {
            Head, Pelvis, LeftArm, RightArm, LeftLeg, RightLeg, LeftElbow, RightElbow, LeftKnee, RightKnee
        }

        private TrackingPoint GetTrackerTransformBySerialNumber(Tuple<ETrackedDeviceClass, string> serial, TargetType setTo, Transform headTracker = null)
        {
            var manager = TrackingPointManager.Instance;
            if (serial.Item1 == ETrackedDeviceClass.HMD)
            {
                if (string.IsNullOrEmpty(serial.Item2)) 
                {
                    return manager.GetTrackingPoints(ETrackedDeviceClass.HMD).FirstOrDefault();
                }
                else if (manager.TryGetTrackingPoint(serial.Item2, out var hmdTrackingPoint))
                {
                    return hmdTrackingPoint;
                }
            }
            else if (serial.Item1 == ETrackedDeviceClass.Controller)
            {
                var controllers = manager.GetTrackingPoints(ETrackedDeviceClass.Controller).Where(d => d.Name.Contains("LIV Virtual Camera") == false);
                TrackingPoint ret = null;
                foreach (var controller in controllers)
                {
                    if (controller != null && controller.Name == serial.Item2)
                    {
                        if (setTo == TargetType.LeftArm || setTo == TargetType.RightArm)
                        {
                            ret = controller;
                            break;
                        }
                        return controller;
                    }
                }
                if (ret == null)
                {
                    var controllerTrackingPoints = controllers.Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.TargetTransform.position - headTracker.position), trackingPoint = d })
                                                           .OrderBy(d => d.pos.x)
                                                           .Select(d => d.trackingPoint);
                    if (setTo == TargetType.LeftArm) ret = controllerTrackingPoints.ElementAtOrDefault(0);
                    if (setTo == TargetType.RightArm) ret = controllerTrackingPoints.ElementAtOrDefault(1);
                }
                return ret;
            }
            else if (serial.Item1 == ETrackedDeviceClass.GenericTracker)
            {
                foreach (var tracker in manager.GetTrackingPoints(ETrackedDeviceClass.GenericTracker).Where(d => d.Name.Contains("LIV Virtual Camera") == false && !(Settings.Current.VirtualMotionTrackerEnable && d.Name.Contains($"VMT_{Settings.Current.VirtualMotionTrackerNo}"))))
                {
                    if (tracker != null && tracker.Name == serial.Item2)
                    {
                        return tracker;
                    }
                }
                if (string.IsNullOrEmpty(serial.Item2) == false) return null; //Serialあるのに見つからなかったらnull

                var trackerIds = new List<string>();

                if (Settings.Current.Head.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.Head.Item2);
                if (Settings.Current.LeftHand.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftHand.Item2);
                if (Settings.Current.RightHand.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightHand.Item2);
                if (Settings.Current.Pelvis.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.Pelvis.Item2);
                if (Settings.Current.LeftFoot.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftFoot.Item2);
                if (Settings.Current.RightFoot.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightFoot.Item2);
                if (Settings.Current.LeftElbow.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftElbow.Item2);
                if (Settings.Current.RightElbow.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightElbow.Item2);
                if (Settings.Current.LeftKnee.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftKnee.Item2);
                if (Settings.Current.RightKnee.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightKnee.Item2);

                //ここに来るときは腰か足のトラッカー自動認識になってるとき
                //割り当てられていないトラッカーリスト
                var autoTrackers = manager.GetTrackingPoints(ETrackedDeviceClass.GenericTracker).Where(d => trackerIds.Contains(d.Name) == false).Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.TargetTransform.position - headTracker.position), trackingPoint = d });
                if (autoTrackers.Any())
                {
                    var count = autoTrackers.Count();
                    if (count >= 3)
                    {
                        if (setTo == TargetType.Pelvis)
                        { //腰は一番高い位置にあるトラッカー
                            return autoTrackers.OrderByDescending(d => d.pos.y).Select(d => d.trackingPoint).First();
                        }
                    }
                    if (count >= 2)
                    {
                        if (setTo == TargetType.LeftLeg)
                        {
                            return autoTrackers.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.trackingPoint).First();
                        }
                        else if (setTo == TargetType.RightLeg)
                        {
                            return autoTrackers.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.trackingPoint).First();
                        }
                    }
                }
            }
            return null;
        }

        private void SetVRIKTargetTrackers()
        {
            if (vrik == null) { return; } //まだmodelがない

            vrik.solver.spine.headTarget = GetTrackerTransformBySerialNumber(Settings.Current.Head, TargetType.Head)?.TargetTransform;
            vrik.solver.spine.headClampWeight = 0.38f;

            vrik.solver.spine.pelvisTarget = GetTrackerTransformBySerialNumber(Settings.Current.Pelvis, TargetType.Pelvis, vrik.solver.spine.headTarget)?.TargetTransform;
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

            vrik.solver.leftArm.target = GetTrackerTransformBySerialNumber(Settings.Current.LeftHand, TargetType.LeftArm, vrik.solver.spine.headTarget)?.TargetTransform;
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

            vrik.solver.rightArm.target = GetTrackerTransformBySerialNumber(Settings.Current.RightHand, TargetType.RightArm, vrik.solver.spine.headTarget)?.TargetTransform;
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

            vrik.solver.leftLeg.target = GetTrackerTransformBySerialNumber(Settings.Current.LeftFoot, TargetType.LeftLeg, vrik.solver.spine.headTarget)?.TargetTransform;
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

            vrik.solver.rightLeg.target = GetTrackerTransformBySerialNumber(Settings.Current.RightFoot, TargetType.RightLeg, vrik.solver.spine.headTarget)?.TargetTransform;
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

        private Transform leftHandFreeOffsetRotation;
        private Transform rightHandFreeOffsetRotation;
        private Transform leftHandFreeOffsetPosition;
        private Transform rightHandFreeOffsetPosition;

        public IEnumerator Calibrate(PipeCommands.CalibrateType calibrateType)
        {
            lastCalibrateType = calibrateType;//最後に実施したキャリブレーションタイプとして記録

            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(0, 0, 0);
            var lefthand = animator.GetBoneTransform(HumanBodyBones.LeftHand); lefthand.localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(0, 0, 0);

            SetVRIK(CurrentModel);
            wristRotationFix.SetVRIK(vrik);

            //animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(LeftHandAngle, 0, 0);
            //animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(RightHandAngle, 0, 0);

            //var leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            //var leftRelaxer = leftLowerArm.gameObject.AddComponent<TwistRelaxer>();
            //leftRelaxer.ik = vrik;
            //leftRelaxer.twistSolvers = new TwistSolver[] { new TwistSolver { transform = leftLowerArm } };
            //var rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            //var rightRelaxer = rightLowerArm.gameObject.AddComponent<TwistRelaxer>();
            //rightRelaxer.ik = vrik;
            //rightRelaxer.twistSolvers = new TwistSolver[] { new TwistSolver { transform = rightLowerArm } };

            var headTracker = GetTrackerTransformBySerialNumber(Settings.Current.Head, TargetType.Head);
            var leftHandTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftHand, TargetType.LeftArm, headTracker?.TargetTransform);
            var rightHandTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightHand, TargetType.RightArm, headTracker?.TargetTransform);
            var bodyTracker = GetTrackerTransformBySerialNumber(Settings.Current.Pelvis, TargetType.Pelvis, headTracker?.TargetTransform);
            var leftFootTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftFoot, TargetType.LeftLeg, headTracker?.TargetTransform);
            var rightFootTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightFoot, TargetType.RightLeg, headTracker?.TargetTransform);
            var leftElbowTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftElbow, TargetType.LeftElbow, headTracker?.TargetTransform);
            var rightElbowTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightElbow, TargetType.RightElbow, headTracker?.TargetTransform);
            var leftKneeTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftKnee, TargetType.LeftKnee, headTracker?.TargetTransform);
            var rightKneeTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightKnee, TargetType.RightKnee, headTracker?.TargetTransform);

            ClearChildren(headTracker, leftHandTracker, rightHandTracker, bodyTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);

            var settings = new RootMotion.FinalIK.VRIKCalibrator.Settings();

            yield return new WaitForEndOfFrame();

            var leftHandOffset = Vector3.zero;
            var rightHandOffset = Vector3.zero;

            //トラッカー
            //xをプラス方向に動かすとトラッカーの左(LEDを上に見たとき)に進む
            //yをプラス方向に動かすとトラッカーの上(LED方向)に進む
            //zをマイナス方向に動かすとトラッカーの底面に向かって進む

            if (Settings.Current.LeftHand.Item1 == ETrackedDeviceClass.GenericTracker)
            {
                //角度補正(左手なら右のトラッカーに向けた)後
                //xを＋方向は体の正面に向かって進む
                //yを＋方向は体の上(天井方向)に向かって進む
                //zを＋方向は体中心(左手なら右手の方向)に向かって進む
                leftHandOffset = new Vector3(1.0f, Settings.Current.LeftHandTrackerOffsetToBottom, Settings.Current.LeftHandTrackerOffsetToBodySide); // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
            }
            if (Settings.Current.RightHand.Item1 == ETrackedDeviceClass.GenericTracker)
            {
                //角度補正(左手なら右のトラッカーに向けた)後
                //xを－方向は体の正面に向かって進む
                //yを＋方向は体の上(天井方向)に向かって進む
                //zを＋方向は体中心(左手なら右手の方向)に向かって進む
                rightHandOffset = new Vector3(1.0f, Settings.Current.RightHandTrackerOffsetToBottom, Settings.Current.RightHandTrackerOffsetToBodySide); // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
            }

            TrackingPointManager.Instance.ClearTrackingWatcher();

            foreach (Transform child in generatedObject)
            {
                DestroyImmediate(child.gameObject);
            }

            var trackerPositions = new TrackerPositions
            {
                Head = new TrackerPosition(headTracker),
                LeftHand = new TrackerPosition(leftHandTracker),
                RightHand = new TrackerPosition(rightHandTracker),
                Pelvis = new TrackerPosition(bodyTracker),
                LeftFoot = new TrackerPosition(leftFootTracker),
                RightFoot = new TrackerPosition(rightFootTracker),
                LeftElbow = new TrackerPosition(leftElbowTracker),
                RightElbow = new TrackerPosition(rightElbowTracker),
                LeftKnee = new TrackerPosition(leftKneeTracker),
                RightKnee = new TrackerPosition(rightKneeTracker),
            };

            try
            {
                var trackerPositionsJson = JsonUtility.ToJson(trackerPositions);
                string path = Path.GetFullPath(Application.dataPath + "/../TrackerPositions.json");
                var directoryName = Path.GetDirectoryName(path);
                if (Directory.Exists(directoryName) == false) Directory.CreateDirectory(directoryName);
                File.WriteAllText(path, Json.Serializer.ToReadable(trackerPositionsJson));
            }
            catch { }

            if (calibrateType == PipeCommands.CalibrateType.Ipose || calibrateType == PipeCommands.CalibrateType.Tpose)
            {
                yield return FinalIKCalibrator.Calibrate(calibrateType == PipeCommands.CalibrateType.Ipose ? FinalIKCalibrator.CalibrateMode.Ipose : FinalIKCalibrator.CalibrateMode.Tpose, HandTrackerRoot, PelvisTrackerRoot, vrik, settings, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker, generatedObject);
            }
            else if (calibrateType == PipeCommands.CalibrateType.FixedHand)
            {
                yield return Calibrator.CalibrateFixedHand(HandTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
            }
            else if (calibrateType == PipeCommands.CalibrateType.FixedHandWithGround)
            {
                yield return Calibrator.CalibrateFixedHandWithGround(HandTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
            }

            vrik.solver.IKPositionWeight = 1.0f;
            if (leftFootTracker == null && rightFootTracker == null)
            {
                vrik.solver.plantFeet = true;
                vrik.solver.locomotion.weight = 1.0f;
                var rootController = vrik.references.root.GetComponent<RootMotion.FinalIK.VRIKRootController>();
                if (rootController != null) GameObject.Destroy(rootController);
            }

            vrik.solver.locomotion.footDistance = 0.08f;
            vrik.solver.locomotion.stepThreshold = 0.05f;
            vrik.solver.locomotion.angleThreshold = 10f;
            vrik.solver.locomotion.maxVelocity = 0.04f;
            vrik.solver.locomotion.velocityFactor = 0.04f;
            vrik.solver.locomotion.rootSpeed = 40;
            vrik.solver.locomotion.stepSpeed = 2;
            vrik.solver.locomotion.offset = new Vector3(0, 0, 0.03f);

            Settings.Current.headTracker = StoreTransform.Create(headTracker?.TargetTransform);
            Settings.Current.bodyTracker = StoreTransform.Create(bodyTracker?.TargetTransform);
            Settings.Current.leftHandTracker = StoreTransform.Create(leftHandTracker?.TargetTransform);
            Settings.Current.rightHandTracker = StoreTransform.Create(rightHandTracker?.TargetTransform);
            Settings.Current.leftFootTracker = StoreTransform.Create(leftFootTracker?.TargetTransform);
            Settings.Current.rightFootTracker = StoreTransform.Create(rightFootTracker?.TargetTransform);
            Settings.Current.leftElbowTracker = StoreTransform.Create(leftElbowTracker?.TargetTransform);
            Settings.Current.rightElbowTracker = StoreTransform.Create(rightElbowTracker?.TargetTransform);
            Settings.Current.leftKneeTracker = StoreTransform.Create(leftKneeTracker?.TargetTransform);
            Settings.Current.rightKneeTracker = StoreTransform.Create(rightKneeTracker?.TargetTransform);

            var calibratedLeftHandTransform = leftHandTracker.TargetTransform?.OfType<Transform>().FirstOrDefault();
            var calibratedRightHandTransform = rightHandTracker.TargetTransform?.OfType<Transform>().FirstOrDefault();

            if (calibratedLeftHandTransform != null && calibratedRightHandTransform != null)
            {
                leftHandFreeOffsetRotation = new GameObject(nameof(leftHandFreeOffsetRotation)).transform;
                rightHandFreeOffsetRotation = new GameObject(nameof(rightHandFreeOffsetRotation)).transform;
                leftHandFreeOffsetRotation.SetParent(leftHandTracker?.TargetTransform);
                rightHandFreeOffsetRotation.SetParent(rightHandTracker?.TargetTransform);
                leftHandFreeOffsetRotation.localPosition = Vector3.zero;
                leftHandFreeOffsetRotation.localRotation = Quaternion.identity;
                leftHandFreeOffsetRotation.localScale = Vector3.one;
                rightHandFreeOffsetRotation.localPosition = Vector3.zero;
                rightHandFreeOffsetRotation.localRotation = Quaternion.identity;
                rightHandFreeOffsetRotation.localScale = Vector3.one;

                leftHandFreeOffsetPosition = new GameObject(nameof(leftHandFreeOffsetPosition)).transform;
                rightHandFreeOffsetPosition = new GameObject(nameof(rightHandFreeOffsetPosition)).transform;
                leftHandFreeOffsetPosition.SetParent(leftHandFreeOffsetRotation);
                rightHandFreeOffsetPosition.SetParent(rightHandFreeOffsetRotation);
                leftHandFreeOffsetPosition.localPosition = Vector3.zero;
                leftHandFreeOffsetPosition.localRotation = Quaternion.identity;
                leftHandFreeOffsetPosition.localScale = Vector3.one;
                rightHandFreeOffsetPosition.localPosition = Vector3.zero;
                rightHandFreeOffsetPosition.localRotation = Quaternion.identity;
                rightHandFreeOffsetPosition.localScale = Vector3.one;

                calibratedLeftHandTransform.parent = leftHandFreeOffsetPosition;
                calibratedRightHandTransform.parent = rightHandFreeOffsetPosition;
            }

            SetHandFreeOffset();

            calibrationState = CalibrationState.Calibrating; //キャリブレーション状態を"キャリブレーション中"に設定(ここまで来なければ失敗している)
        }

        private void ClearChildren(params TrackingPoint[] Parents) => ClearChildren(Parents.Select(d => d?.TargetTransform).ToArray());

        private void ClearChildren(params Transform[] Parents)
        {
            foreach (var parent in Parents)
            {
                if (parent != null)
                {
                    foreach (Transform child in parent)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        public void EndCalibrate()
        {
            //トラッカー位置の非表示
            TrackingPointManager.Instance.SetTrackingPointPositionVisible(false);

            if (CalibrationCamera != null)
            {
                CalibrationCamera.gameObject.SetActive(false);
            }
            SetHandFreeOffset();
            //SetTrackersToVRIK();

            //直前がキャリブレーション実行中なら
            if (calibrationState == CalibrationState.Calibrating)
            {
                calibrationState = CalibrationState.Calibrated; //キャリブレーション状態を"キャリブレーション完了"に設定
            }
            else
            {
                //キャンセルされたなど
                calibrationState = CalibrationState.Uncalibrated; //キャリブレーション状態を"未キャリブレーション"に設定

                //IKを初期化
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(0, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(0, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(0, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(0, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(0, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(0, 0, 0);

                SetVRIK(CurrentModel);
                wristRotationFix.SetVRIK(vrik);

                animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(LeftHandAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(RightHandAngle, 0, 0);
            }
        }

        #endregion

        #region LipSync

        private void SetLipSyncEnable(bool enable)
        {
            LipSync.EnableLipSync = enable;
            Settings.Current.LipSyncEnable = enable;
        }

        private string[] GetLipSyncDevices()
        {
            return LipSync.GetMicrophoneDevices();
        }

        private void SetLipSyncDevice(string device)
        {
            LipSync.SetMicrophoneDevice(device);
            Settings.Current.LipSyncDevice = device;
        }

        private void SetLipSyncGain(float gain)
        {
            if (gain < 1.0f) gain = 1.0f;
            if (gain > 256.0f) gain = 256.0f;
            LipSync.Gain = gain;
            Settings.Current.LipSyncGain = gain;
        }

        private void SetLipSyncMaxWeightEnable(bool enable)
        {
            LipSync.MaxWeightEnable = enable;
            Settings.Current.LipSyncMaxWeightEnable = enable;
        }

        private void SetLipSyncWeightThreashold(float threashold)
        {
            LipSync.WeightThreashold = threashold;
            Settings.Current.LipSyncWeightThreashold = threashold;
        }

        private void SetLipSyncMaxWeightEmphasis(bool enable)
        {
            LipSync.MaxWeightEmphasis = enable;
            Settings.Current.LipSyncMaxWeightEmphasis = enable;
        }

        #endregion

        #region Color

        private void ChangeBackgroundColor(float r, float g, float b, bool isCustom)
        {
            BackgroundRenderer.material.color = new Color(r, g, b, 1.0f);
            Settings.Current.BackgroundColor = BackgroundRenderer.material.color;
            if (isCustom) Settings.Current.CustomBackgroundColor = BackgroundRenderer.material.color;
            Settings.Current.IsTransparent = false;
            SetDwmTransparent(false);
        }

        private void SetBackgroundTransparent()
        {
            Settings.Current.IsTransparent = true;
#if !UNITY_EDITOR   // エディタ上では動きません。
        BackgroundRenderer.material.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        SetDwmTransparent(true);
#endif
        }

        private bool lastHideWindowBorder = false;
        void HideWindowBorder(bool enable)
        {
            if (lastHideWindowBorder == enable) return;
            lastHideWindowBorder = enable;
            Settings.Current.HideBorder = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        var hwnd = GetUnityWindowHandle();
        //var hwnd = GetActiveWindow();
        if (enable)
        {
            var clientrect = GetUnityWindowClientPosition();
            SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE); //ウインドウ枠の削除
            SetUnityWindowSize(clientrect.right - clientrect.left, clientrect.bottom - clientrect.top);
        }
        else
        {
            var windowrect = GetUnityWindowPosition();
            SetWindowLong(hwnd, GWL_STYLE, defaultWindowStyle);
            Screen.SetResolution(windowrect.right - windowrect.left, windowrect.bottom - windowrect.top, false);
        }
#endif
        }
        void SetWindowTopMost(bool enable)
        {
            Settings.Current.IsTopMost = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        SetUnityWindowTopMost(enable);
#endif
        }

        void SetWindowClickThrough(bool enable)
        {
            Settings.Current.WindowClickThrough = enable;
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



        private void SetGridVisible(bool enable)
        {
            GridCanvas?.SetActive(enable);
            Settings.Current.ShowCameraGrid = enable;
        }

        #endregion

        #region BlinkControl
        void SetAutoBlinkEnable(bool enable)
        {
            faceController.EnableBlink = enable;
            Settings.Current.AutoBlinkEnable = enable;
        }
        void SetBlinkTimeMin(float time)
        {
            faceController.BlinkTimeMin = time;
            Settings.Current.BlinkTimeMin = time;
        }
        void SetBlinkTimeMax(float time)
        {
            faceController.BlinkTimeMax = time;
            Settings.Current.BlinkTimeMax = time;
        }
        void SetCloseAnimationTime(float time)
        {
            faceController.CloseAnimationTime = time;
            Settings.Current.CloseAnimationTime = time;
        }
        void SetOpenAnimationTime(float time)
        {
            faceController.OpenAnimationTime = time;
            Settings.Current.OpenAnimationTime = time;
        }
        void SetClosingTime(float time)
        {
            faceController.ClosingTime = time;
            Settings.Current.ClosingTime = time;
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



        public void DoKeyAction(KeyAction action)
        {
            if (action.HandAction)
            {
                handController.SetHandAngle(action.Hand == Hands.Left || action.Hand == Hands.Both, action.Hand == Hands.Right || action.Hand == Hands.Both, action.HandAngles, action.HandChangeTime);
            }
            else if (action.FaceAction)
            {
                foreach (var externalMotionReceiver in externalMotionReceivers)
                {
                    externalMotionReceiver.DisableBlendShapeReception = action.DisableBlendShapeReception;
                }
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
                        ChangeBackgroundColor(Settings.Current.CustomBackgroundColor.r, Settings.Current.CustomBackgroundColor.g, Settings.Current.CustomBackgroundColor.b, true);
                        break;
                    case Functions.ColorTransparent:
                        SetBackgroundTransparent();
                        break;
                    case Functions.FrontCamera:
                        CameraManager.Current.ChangeCamera(CameraTypes.Front);
                        break;
                    case Functions.BackCamera:
                        CameraManager.Current.ChangeCamera(CameraTypes.Back);
                        break;
                    case Functions.FreeCamera:
                        CameraManager.Current.ChangeCamera(CameraTypes.Free);
                        break;
                    case Functions.PositionFixedCamera:
                        CameraManager.Current.ChangeCamera(CameraTypes.PositionFixed);
                        break;
                    case Functions.PauseTracking:
                        DeviceInfo.pauseTracking = !DeviceInfo.pauseTracking;
                        break;
                    case Functions.ShowCalibrationWindow:
                        server?.SendCommandAsync(new PipeCommands.ShowCalibrationWindow { });
                        break;
                    case Functions.ShowPhotoWindow:
                        server?.SendCommandAsync(new PipeCommands.ShowPhotoWindow { });
                        break;
                }
            }
        }


        #endregion

        #region EyeTracking


        private void SetEyeTracking_TobiiOffsets(PipeCommands.SetEyeTracking_TobiiOffsets offsets)
        {
            Settings.Current.EyeTracking_TobiiOffsetHorizontal = offsets.OffsetHorizontal;
            Settings.Current.EyeTracking_TobiiOffsetVertical = offsets.OffsetVertical;
            Settings.Current.EyeTracking_TobiiScaleHorizontal = offsets.ScaleHorizontal;
            Settings.Current.EyeTracking_TobiiScaleVertical = offsets.ScaleVertical;
            SetEyeTracking_TobiiOffsetsAction?.Invoke(offsets);
        }

        public void SetEyeTracking_TobiiPosition(Transform position, float centerX, float centerY)
        {
            Settings.Current.EyeTracking_TobiiPosition = StoreTransform.Create(position);
            Settings.Current.EyeTracking_TobiiCenterX = centerX;
            Settings.Current.EyeTracking_TobiiCenterY = centerY;
        }

        public Vector2 GetEyeTracking_TobiiLocalPosition(Transform saveto)
        {
            if (Settings.Current.EyeTracking_TobiiPosition != null) Settings.Current.EyeTracking_TobiiPosition.ToLocalTransform(saveto);
            return new Vector2(Settings.Current.EyeTracking_TobiiCenterX, Settings.Current.EyeTracking_TobiiCenterY);
        }
        private void SetEyeTracking_ViveProEyeOffsets(PipeCommands.SetEyeTracking_ViveProEyeOffsets offsets)
        {
            Settings.Current.EyeTracking_ViveProEyeOffsetHorizontal = offsets.OffsetHorizontal;
            Settings.Current.EyeTracking_ViveProEyeOffsetVertical = offsets.OffsetVertical;
            Settings.Current.EyeTracking_ViveProEyeScaleHorizontal = offsets.ScaleHorizontal;
            Settings.Current.EyeTracking_ViveProEyeScaleVertical = offsets.ScaleVertical;
            SetEyeTracking_ViveProEyeOffsetsAction?.Invoke(offsets);
        }
        private void SetEyeTracking_ViveProEyeUseEyelidMovements(PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements useEyelidMovements)
        {
            Settings.Current.EyeTracking_ViveProEyeUseEyelidMovements = useEyelidMovements.Use;
            SetEyeTracking_ViveProEyeUseEyelidMovementsAction?.Invoke(useEyelidMovements);
        }


        #endregion

        #region ExternalMotionSender

        private void SetExternalMotionSenderEnable(bool enable)
        {
            if (IsPreRelease == false) return;
            Settings.Current.ExternalMotionSenderEnable = enable;
            ExternalMotionSenderObject.SetActive(enable);
        }

        private void SetExternalMotionReceiverEnable(bool enable, int index)
        {
            Settings.Current.ExternalMotionReceiverEnableList[index] = enable;
            externalMotionReceivers[index].SetObjectActive(enable);
        }

        private void SetExternalBonesReceiverEnable(bool enable)
        {
            Settings.Current.ExternalBonesReceiverEnable = enable;
            foreach (var externalMotionReceiver in externalMotionReceivers)
            {
                externalMotionReceiver.receiveBonesFlag = enable;
            }
        }

        private void ChangeExternalMotionSenderAddress(string address, int port, int pstatus, int proot, int pbone, int pblendshape, int pcamera, int pdevices, string optionstring, bool responderEnable)
        {
            Settings.Current.ExternalMotionSenderAddress = address;
            Settings.Current.ExternalMotionSenderPort = port;
            Settings.Current.ExternalMotionSenderPeriodStatus = pstatus;
            Settings.Current.ExternalMotionSenderPeriodRoot = proot;
            Settings.Current.ExternalMotionSenderPeriodBone = pbone;
            Settings.Current.ExternalMotionSenderPeriodBlendShape = pblendshape;
            Settings.Current.ExternalMotionSenderPeriodCamera = pcamera;
            Settings.Current.ExternalMotionSenderPeriodDevices = pdevices;
            Settings.Current.ExternalMotionSenderOptionString = optionstring;
            Settings.Current.ExternalMotionSenderResponderEnable = responderEnable;

            externalMotionSender.periodStatus = pstatus;
            externalMotionSender.periodRoot = proot;
            externalMotionSender.periodBone = pbone;
            externalMotionSender.periodBlendShape = pblendshape;
            externalMotionSender.periodCamera = pcamera;
            externalMotionSender.periodDevices = pdevices;
            externalMotionSender.ChangeOSCAddress(address, port);
            externalMotionSender.optionString = optionstring;
            easyDeviceDiscoveryProtocolManager.responderEnable = responderEnable;
        }

        public void ChangeExternalMotionSenderAddress(string address, int port)
        {
            Settings.Current.ExternalMotionSenderAddress = address;
            Settings.Current.ExternalMotionSenderPort = port;

            externalMotionSender.ChangeOSCAddress(address, port);
        }

        private void ChangeExternalMotionReceiverPort(int[] ports, bool requesterEnable)
        {
            Settings.Current.ExternalMotionReceiverPortList = ports.ToList();
            for (int index = 0; index < externalMotionReceivers.Length; index++)
            {
                externalMotionReceivers[index].ChangeOSCPort(ports[index]);
            }

            Settings.Current.ExternalMotionReceiverRequesterEnable = requesterEnable;
            easyDeviceDiscoveryProtocolManager.requesterEnable = requesterEnable;
        }

        private void WaitOneFrameAction(Action action)
        {
            StartCoroutine(WaitOneFrameCoroutine(action));
        }

        private IEnumerator WaitOneFrameCoroutine(Action action)
        {
            yield return null;
            action?.Invoke();
        }

        #endregion

        private void SetTrackingFilterEnable(bool global, bool hmd, bool controller, bool tracker)
        {
            DeviceInfo.globalEnable = global;
            DeviceInfo.hmdEnable = hmd;
            DeviceInfo.controllerEnable = controller;
            DeviceInfo.trackerEnable = tracker;
            Settings.Current.TrackingFilterEnable = global;
            Settings.Current.TrackingFilterHmdEnable = hmd;
            Settings.Current.TrackingFilterControllerEnable = controller;
            Settings.Current.TrackingFilterTrackerEnable = tracker;
        }

        private void SetModelModifierEnable(bool fixKneeRotation, bool fixElbowRotation)
        {
            Settings.Current.FixKneeRotation = fixKneeRotation;
            Settings.Current.FixElbowRotation = fixElbowRotation;
        }

        private void SetHandleControllerAsTracker(bool handleCasT)
        {
            Settings.Current.HandleControllerAsTracker = handleCasT;
        }

        #region Setting


        [Serializable]
        public class CommonSettings
        {
            public string LoadSettingFilePathOnStart = ""; //起動時に読み込む設定ファイルパス

            //初期値
            [OnDeserializing()]
            internal void OnDeserializingMethod(StreamingContext context)
            {
                LoadSettingFilePathOnStart = "";
            }
        }

        public static CommonSettings CurrentCommonSettings = new CommonSettings();

        //共通設定の書き込み
        private void SaveCommonSettings()
        {
            string path = Path.GetFullPath(Application.dataPath + "/../Settings/common.json");
            var directoryName = Path.GetDirectoryName(path);
            if (Directory.Exists(directoryName) == false) Directory.CreateDirectory(directoryName);
            File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(CurrentCommonSettings)));
        }

        //共通設定の読み込み
        public void LoadCommonSettings()
        {
            string path = Path.GetFullPath(Application.dataPath + "/../Settings/common.json");
            if (!File.Exists(path))
            {
                return;
            }
            CurrentCommonSettings = Json.Serializer.Deserialize<CommonSettings>(File.ReadAllText(path)); //設定を読み込み
        }

        private NotifyLogTypes notifyLogLevel = NotifyLogTypes.Warning;
        private async void LogMessageHandler(string cond, string trace, LogType type)
        {
            NotifyLogTypes notifyType = NotifyLogTypes.Log;
            switch (type)
            {
                case LogType.Assert: notifyType = NotifyLogTypes.Assert; CriticalErrorCount++; break;
                case LogType.Error: notifyType = NotifyLogTypes.Error; CriticalErrorCount++; break;
                case LogType.Exception: notifyType = NotifyLogTypes.Exception; CriticalErrorCount++; break;
                case LogType.Log: notifyType = NotifyLogTypes.Log; break;
                case LogType.Warning: notifyType = NotifyLogTypes.Warning; break;
                default: notifyType = NotifyLogTypes.Log; break;
            }

            if (notifyLogLevel < notifyType)
            {
                return; //Logはうるさいので飛ばさない
            }

            //あまりにも致命的エラーが多すぎる場合は強制終了する
            if (CriticalErrorCount > 10000)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                Debug.Log("CriticalErrorCount over");
            }

            await server.SendCommandAsync(new PipeCommands.LogNotify
            {
                condition = cond,
                stackTrace = trace,
                type = notifyType,
                errorCount = CriticalErrorCount,
            });
        }

        private bool IsRegisteredEventCallBack = false;
        private void RegisterEventCallBack()
        {
            if (IsRegisteredEventCallBack == false)
            {
                IsRegisteredEventCallBack = true;
                TrackingPointManager.Instance.TrackerMovedEvent += TransformExtensions_TrackerMovedEvent;
                ExternalReceiverForVMC.StatusStringUpdated += StatusStringUpdatedEvent;
            }
        }

        private void SaveSettings(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Settings.Current.AAA_SavedVersion = baseVersionString;

            File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(Settings.Current)));

            //ファイルが正常に書き込めたので、現在共通設定に記録されているパスと違う場合、共通設定に書き込む
            if (CurrentCommonSettings.LoadSettingFilePathOnStart != path)
            {
                CurrentCommonSettings.LoadSettingFilePathOnStart = path;
                SaveCommonSettings();
                Debug.Log("Save last loaded file of " + path);
            }
        }

        //設定の読み込み
        public void LoadSettings(string path = null)
        {
            //設定パスがnull or 存在しないなら、default読み込み
            //パスが渡されていれば2回目以降の読み込み
            if (string.IsNullOrEmpty(path) || (!File.Exists(path)))
            {
                //共通設定を読み込み
                LoadCommonSettings();

                //初回読み込みファイルが存在しなければdefault.jsonを
                if (string.IsNullOrEmpty(CurrentCommonSettings.LoadSettingFilePathOnStart) || (!File.Exists(CurrentCommonSettings.LoadSettingFilePathOnStart)))
                {
                    path = Application.dataPath + "/../default.json";
                    Debug.Log("Load default.json");
                }
                else
                {
                    //存在すればそのPathを読みに行こうとする
                    path = CurrentCommonSettings.LoadSettingFilePathOnStart;
                    Debug.Log("Load last loaded file of " + path);
                }
            }

            //設定の読み込みを試みる
            try
            {
                path = Path.GetFullPath(path); //フルパスに変換
                Settings.Current = Json.Serializer.Deserialize<Settings>(File.ReadAllText(path)); //設定を読み込み
                float divide = 0;
                //腰情報を読み込む
                if (float.TryParse(File.ReadAllText(Application.dataPath + "/../PelvisTrackerOffsetDivide.txt"), out divide))
                {
                    Calibrator.pelvisOffsetDivide = divide;//腰オフセット分割数を記録
                }
            }
            catch (Exception ex)
            {
                //読み込めなかったときはエラーをファイルとして出力
                File.WriteAllText(Application.dataPath + "/../exception.txt", ex.ToString() + ":" + ex.Message);
                Debug.LogError(ex.ToString() + ":" + ex.Message);
            }

            Debug.Log("Loaded config: " + path);

            //スケールを元に戻す
            ResetTrackerScale();
            //設定を適用する
            ApplySettings();

            //有効なJSONが取得できたかチェック
            if (Settings.Current != null)
            {
                lastLoadedConfigPath = path; //パスを記録

                //ファイルが正常に存在したので、現在共通設定に記録されているパスと違う場合、共通設定に書き込む
                if (CurrentCommonSettings.LoadSettingFilePathOnStart != path)
                {
                    CurrentCommonSettings.LoadSettingFilePathOnStart = path;
                    SaveCommonSettings();
                    Debug.Log("Save last loaded file of " + path);
                }
            }

            //設定の変更を通知
            VMCEvents.OnLoadedConfigPathChanged?.Invoke(path);
        }

        private void ResetTrackerScale()
        {
            //jsonが正しくデコードできていなければ無視する
            if (Settings.Current == null)
            {
                return;
            }

            //トラッカーのルートスケールを初期値に戻す
            HandTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            PelvisTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            HandTrackerRoot.position = Vector3.zero;
            PelvisTrackerRoot.position = Vector3.zero;

            //スケール変更時の位置オフセット設定
            var handTrackerOffset = HandTrackerRoot.GetComponent<ScalePositionOffset>();
            var footTrackerOffset = PelvisTrackerRoot.GetComponent<ScalePositionOffset>();
            handTrackerOffset.ResetTargetAndPosition();
            footTrackerOffset.ResetTargetAndPosition();
        }

        //Settings.Currentを各種設定に適用
        private async void ApplySettings()
        {
            //VRMのパスが有効で、存在するなら読み込む
            if (string.IsNullOrWhiteSpace(Settings.Current.VRMPath) == false
                && File.Exists(Settings.Current.VRMPath))
            {
                await server.SendCommandAsync(new PipeCommands.LoadVRMPath { Path = Settings.Current.VRMPath });
                await ImportVRM(Settings.Current.VRMPath, false, Settings.Current.EnableNormalMapFix, Settings.Current.DeleteHairNormalMap);

                //メタ情報をOSC送信する
                VRMmetaLodedAction?.Invoke(LoadVRM(Settings.Current.VRMPath));
            }

            //SetResolutionは強制的にウインドウ枠を復活させるのでBorder設定の前にやっておく必要がある
            if (Screen.resolutions.Any(d => d.width == Settings.Current.ScreenWidth && d.height == Settings.Current.ScreenHeight && d.refreshRate == Settings.Current.ScreenRefreshRate))
            {
                UpdateActionQueue.Enqueue(() => Screen.SetResolution(Settings.Current.ScreenWidth, Settings.Current.ScreenHeight, false, Settings.Current.ScreenRefreshRate));
            }

            if (Settings.Current.BackgroundColor != null)
            {
                UpdateActionQueue.Enqueue(() => ChangeBackgroundColor(Settings.Current.BackgroundColor.r, Settings.Current.BackgroundColor.g, Settings.Current.BackgroundColor.b, false));
            }

            if (Settings.Current.CustomBackgroundColor != null)
            {
                await server.SendCommandAsync(new PipeCommands.LoadCustomBackgroundColor { r = Settings.Current.CustomBackgroundColor.r, g = Settings.Current.CustomBackgroundColor.g, b = Settings.Current.CustomBackgroundColor.b });
            }

            if (Settings.Current.IsTransparent)
            {
                UpdateActionQueue.Enqueue(() => SetBackgroundTransparent());
            }

            UpdateActionQueue.Enqueue(() => HideWindowBorder(Settings.Current.HideBorder));
            await server.SendCommandAsync(new PipeCommands.LoadHideBorder { enable = Settings.Current.HideBorder });

            UpdateActionQueue.Enqueue(() => SetWindowTopMost(Settings.Current.IsTopMost));
            await server.SendCommandAsync(new PipeCommands.LoadIsTopMost { enable = Settings.Current.IsTopMost });

            await server.SendCommandAsync(new PipeCommands.LoadCameraFOV { fov = Settings.Current.CameraFOV });
            await server.SendCommandAsync(new PipeCommands.LoadCameraSmooth { speed = Settings.Current.CameraSmooth });

            SetGridVisible(Settings.Current.ShowCameraGrid);
            await server.SendCommandAsync(new PipeCommands.LoadShowCameraGrid { enable = Settings.Current.ShowCameraGrid });
            await server.SendCommandAsync(new PipeCommands.LoadCameraMirror { enable = Settings.Current.CameraMirrorEnable });
            SetWindowClickThrough(Settings.Current.WindowClickThrough);
            await server.SendCommandAsync(new PipeCommands.LoadSetWindowClickThrough { enable = Settings.Current.WindowClickThrough });
            SetLipSyncDevice(Settings.Current.LipSyncDevice);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncDevice { device = Settings.Current.LipSyncDevice });
            SetLipSyncGain(Settings.Current.LipSyncGain);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncGain { gain = Settings.Current.LipSyncGain });
            SetLipSyncMaxWeightEnable(Settings.Current.LipSyncMaxWeightEnable);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncMaxWeightEnable { enable = Settings.Current.LipSyncMaxWeightEnable });
            SetLipSyncWeightThreashold(Settings.Current.LipSyncWeightThreashold);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncWeightThreashold { threashold = Settings.Current.LipSyncWeightThreashold });
            SetLipSyncMaxWeightEmphasis(Settings.Current.LipSyncMaxWeightEmphasis);
            await server.SendCommandAsync(new PipeCommands.LoadLipSyncMaxWeightEmphasis { enable = Settings.Current.LipSyncMaxWeightEmphasis });

            SetAutoBlinkEnable(Settings.Current.AutoBlinkEnable);
            await server.SendCommandAsync(new PipeCommands.LoadAutoBlinkEnable { enable = Settings.Current.AutoBlinkEnable });
            SetBlinkTimeMin(Settings.Current.BlinkTimeMin);
            await server.SendCommandAsync(new PipeCommands.LoadBlinkTimeMin { time = Settings.Current.BlinkTimeMin });
            SetBlinkTimeMax(Settings.Current.BlinkTimeMax);
            await server.SendCommandAsync(new PipeCommands.LoadBlinkTimeMax { time = Settings.Current.BlinkTimeMax });
            SetCloseAnimationTime(Settings.Current.CloseAnimationTime);
            await server.SendCommandAsync(new PipeCommands.LoadCloseAnimationTime { time = Settings.Current.CloseAnimationTime });
            SetOpenAnimationTime(Settings.Current.OpenAnimationTime);
            await server.SendCommandAsync(new PipeCommands.LoadOpenAnimationTime { time = Settings.Current.OpenAnimationTime });
            SetClosingTime(Settings.Current.ClosingTime);
            await server.SendCommandAsync(new PipeCommands.LoadClosingTime { time = Settings.Current.ClosingTime });
            SetDefaultFace(Settings.Current.DefaultFace);
            await server.SendCommandAsync(new PipeCommands.LoadDefaultFace { face = Settings.Current.DefaultFace });

            await server.SendCommandAsync(new PipeCommands.LoadControllerTouchPadPoints
            {
                IsOculus = Settings.Current.IsOculus,
                LeftPoints = Settings.Current.LeftTouchPadPoints,
                LeftCenterEnable = Settings.Current.LeftCenterEnable,
                RightPoints = Settings.Current.RightTouchPadPoints,
                RightCenterEnable = Settings.Current.RightCenterEnable
            });
            await server.SendCommandAsync(new PipeCommands.LoadControllerStickPoints
            {
                LeftPoints = Settings.Current.LeftThumbStickPoints,
                RightPoints = Settings.Current.RightThumbStickPoints,
            });

            KeyAction.KeyActionsUpgrade(Settings.Current.KeyActions);

            if (string.IsNullOrWhiteSpace(Settings.Current.AAA_SavedVersion))
            {
                //before 0.47 _SaveVersion is null.

                //v0.48 BlendShapeKey case sensitive.
                foreach (var keyAction in Settings.Current.KeyActions)
                {
                    if (keyAction.FaceNames != null && keyAction.FaceNames.Count > 0)
                    {
                        keyAction.FaceNames = keyAction.FaceNames.Select(d => faceController.GetCaseSensitiveKeyName(d)).ToList();
                    }
                }
            }

            SteamVR2Input.EnableSkeletal = Settings.Current.EnableSkeletal;

            await server.SendCommandAsync(new PipeCommands.LoadSkeletalInputEnable { enable = Settings.Current.EnableSkeletal });

            await server.SendCommandAsync(new PipeCommands.LoadKeyActions { KeyActions = Settings.Current.KeyActions });
            await server.SendCommandAsync(new PipeCommands.SetHandFreeOffset
            {
                LeftHandPositionX = (int)Mathf.Round(Settings.Current.LeftHandPositionX * 1000),
                LeftHandPositionY = (int)Mathf.Round(Settings.Current.LeftHandPositionY * 1000),
                LeftHandPositionZ = (int)Mathf.Round(Settings.Current.LeftHandPositionZ * 1000),
                LeftHandRotationX = (int)Settings.Current.LeftHandRotationX,
                LeftHandRotationY = (int)Settings.Current.LeftHandRotationY,
                LeftHandRotationZ = (int)Settings.Current.LeftHandRotationZ,
                RightHandPositionX = (int)Mathf.Round(Settings.Current.RightHandPositionX * 1000),
                RightHandPositionY = (int)Mathf.Round(Settings.Current.RightHandPositionY * 1000),
                RightHandPositionZ = (int)Mathf.Round(Settings.Current.RightHandPositionZ * 1000),
                RightHandRotationX = (int)Settings.Current.RightHandRotationX,
                RightHandRotationY = (int)Settings.Current.RightHandRotationY,
                RightHandRotationZ = (int)Settings.Current.RightHandRotationZ,
                SwivelOffset = Settings.Current.SwivelOffset,
            });
            SetHandFreeOffset();

            await server.SendCommandAsync(new PipeCommands.LoadLipSyncEnable { enable = Settings.Current.LipSyncEnable });
            SetLipSyncEnable(Settings.Current.LipSyncEnable);

            await server.SendCommandAsync(new PipeCommands.SetLightAngle { X = Settings.Current.LightRotationX, Y = Settings.Current.LightRotationY });
            SetLightAngle(Settings.Current.LightRotationX, Settings.Current.LightRotationY);
            await server.SendCommandAsync(new PipeCommands.ChangeLightColor { a = Settings.Current.LightColor.a, r = Settings.Current.LightColor.r, g = Settings.Current.LightColor.g, b = Settings.Current.LightColor.b });
            ChangeLightColor(Settings.Current.LightColor.a, Settings.Current.LightColor.r, Settings.Current.LightColor.g, Settings.Current.LightColor.b);

            SetExternalMotionSenderEnable(Settings.Current.ExternalMotionSenderEnable);
            ChangeExternalMotionSenderAddress(Settings.Current.ExternalMotionSenderAddress, Settings.Current.ExternalMotionSenderPort, Settings.Current.ExternalMotionSenderPeriodStatus, Settings.Current.ExternalMotionSenderPeriodRoot, Settings.Current.ExternalMotionSenderPeriodBone, Settings.Current.ExternalMotionSenderPeriodBlendShape, Settings.Current.ExternalMotionSenderPeriodCamera, Settings.Current.ExternalMotionSenderPeriodDevices, Settings.Current.ExternalMotionSenderOptionString, Settings.Current.ExternalMotionSenderResponderEnable);

            if (Settings.Current.ExternalMotionReceiverPortList == null) Settings.Current.ExternalMotionReceiverPortList = new List<int>() { Settings.Current.ExternalMotionReceiverPort, Settings.Current.ExternalMotionReceiverPort + 1 };
            ChangeExternalMotionReceiverPort(Settings.Current.ExternalMotionReceiverPortList.ToArray(), Settings.Current.ExternalMotionReceiverRequesterEnable);
            if (Settings.Current.ExternalMotionReceiverEnableList == null) Settings.Current.ExternalMotionReceiverEnableList = new List<bool>() { Settings.Current.ExternalMotionReceiverEnable, false };
            for (int index = 0; index < Settings.Current.ExternalMotionReceiverEnableList.Count; index++)
            {
                SetExternalMotionReceiverEnable(Settings.Current.ExternalMotionReceiverEnableList[index], index);
            }

            SetMidiCCBlendShape(Settings.Current.MidiCCBlendShape);
            SetMidiEnable(Settings.Current.MidiEnable);

            SetEyeTracking_TobiiOffsetsAction?.Invoke(new PipeCommands.SetEyeTracking_TobiiOffsets
            {
                OffsetHorizontal = Settings.Current.EyeTracking_TobiiOffsetHorizontal,
                OffsetVertical = Settings.Current.EyeTracking_TobiiOffsetVertical,
                ScaleHorizontal = Settings.Current.EyeTracking_TobiiScaleHorizontal,
                ScaleVertical = Settings.Current.EyeTracking_TobiiScaleVertical
            });

            SetEyeTracking_ViveProEyeOffsetsAction?.Invoke(new PipeCommands.SetEyeTracking_ViveProEyeOffsets
            {
                OffsetHorizontal = Settings.Current.EyeTracking_ViveProEyeOffsetHorizontal,
                OffsetVertical = Settings.Current.EyeTracking_ViveProEyeOffsetVertical,
                ScaleHorizontal = Settings.Current.EyeTracking_ViveProEyeScaleHorizontal,
                ScaleVertical = Settings.Current.EyeTracking_ViveProEyeScaleVertical
            });

            SetEyeTracking_ViveProEyeUseEyelidMovementsAction?.Invoke(new PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements
            {
                Use = Settings.Current.EyeTracking_ViveProEyeUseEyelidMovements
            });
            SetEyeTracking_ViveProEyeEnable(Settings.Current.EyeTracking_ViveProEyeEnable);

            SetTrackingFilterEnable(Settings.Current.TrackingFilterEnable, Settings.Current.TrackingFilterHmdEnable, Settings.Current.TrackingFilterControllerEnable, Settings.Current.TrackingFilterTrackerEnable);

            SetModelModifierEnable(Settings.Current.FixKneeRotation, Settings.Current.FixElbowRotation);
            SetHandleControllerAsTracker(Settings.Current.HandleControllerAsTracker);
            SetQualitySettings(new PipeCommands.SetQualitySettings
            {
                antiAliasing = Settings.Current.AntiAliasing,
            });
            SetVMT(Settings.Current.VirtualMotionTrackerEnable, Settings.Current.VirtualMotionTrackerNo);

            SetLipShapeToBlendShapeStringMapAction?.Invoke(Settings.Current.LipShapesToBlendShapeMap);
            SetLipTracking_ViveEnable(Settings.Current.LipTracking_ViveEnable);

            SetExternalBonesReceiverEnable(Settings.Current.ExternalBonesReceiverEnable);

            LoadAdvancedGraphicsOption();

            AdditionalSettingAction?.Invoke(null);

            await server.SendCommandAsync(new PipeCommands.SetWindowNum { Num = CurrentWindowNum });
        }

        private void SetEyeTracking_ViveProEyeEnable(bool enable)
        {
            if (EyeTracking_ViveProEyeComponent != null) EyeTracking_ViveProEyeComponent.enabled = enable;
            if (SRanipal_Eye_FrameworkComponent != null) SRanipal_Eye_FrameworkComponent.enabled = enable;
        }

        private void SetLipTracking_ViveEnable(bool enable)
        {
            if (LipTracking_ViveComponent != null) LipTracking_ViveComponent.enabled = enable;
            if (SRanipal_Lip_FrameworkComponent != null) SRanipal_Lip_FrameworkComponent.enabled = enable;
        }

        #endregion

        private void SetMidiCCBlendShape(List<string> blendshapes)
        {
            Settings.Current.MidiCCBlendShape = blendshapes;
            midiCCBlendShape.KnobToBlendShape = blendshapes.ToArray();
        }

        private void SetMidiEnable(bool enable)
        {
            Settings.Current.MidiEnable = enable;
            InputManager.Current.midiCCWrapper.gameObject.SetActive(enable);
        }

        private void SetHandFreeOffset()
        {
            if (vrik == null) return;
            if (leftHandFreeOffsetRotation == null) return;
            if (rightHandFreeOffsetRotation == null) return;
            if (leftHandFreeOffsetPosition == null) return;
            if (rightHandFreeOffsetPosition == null) return;

            // Beat Saber compatible

            leftHandFreeOffsetRotation.localRotation = Quaternion.Euler(
                Settings.Current.LeftHandRotationX,
                -Settings.Current.LeftHandRotationY,
                Settings.Current.LeftHandRotationZ
            );
            leftHandFreeOffsetPosition.localPosition = new Vector3(
                -Settings.Current.LeftHandPositionX,
                Settings.Current.LeftHandPositionY,
                Settings.Current.LeftHandPositionZ
            );

            rightHandFreeOffsetRotation.localRotation = Quaternion.Euler(
                Settings.Current.RightHandRotationX,
                Settings.Current.RightHandRotationY,
                Settings.Current.RightHandRotationZ
            );
            rightHandFreeOffsetPosition.localPosition = new Vector3(
                Settings.Current.RightHandPositionX,
                Settings.Current.RightHandPositionY,
                Settings.Current.RightHandPositionZ
            );

            vrik.solver.leftArm.swivelOffset = Settings.Current.SwivelOffset;
            vrik.solver.rightArm.swivelOffset = -Settings.Current.SwivelOffset;
        }

        private ConcurrentQueue<Action> UpdateActionQueue = new ConcurrentQueue<Action>();

        // Update is called once per frame
        void Update()
        {
            KeyboardAction.Update();

            //if (Input.GetKeyDown(KeyCode.P))
            //{
            //    TakePhoto(16000, true);
            //}

            Action action;
            if (UpdateActionQueue.TryDequeue(out action)) action();
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
}