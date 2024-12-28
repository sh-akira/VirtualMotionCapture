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
using UniGLTF;
using VRMShaders;
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

        public Renderer BackgroundRenderer;

        public GameObject GridCanvas;

        public DynamicOVRLipSync LipSync;

        public FaceController faceController;

        public GameObject ExternalMotionSenderObject;
        private ExternalSender externalMotionSender;

        public GameObject ExternalMotionReceiverObject;
        public List<ExternalReceiverForVMC> externalMotionReceivers = new List<ExternalReceiverForVMC>();
        public MidiCCWrapper midiCCWrapper;

        public MemoryMappedFileServer server;
        private string pipeName = Guid.NewGuid().ToString();

        private GameObject CurrentModel = null;

        private int CurrentWindowNum = 1;

        public int CriticalErrorCount = 0;
        public bool IsCriticalErrorCountOver = false;

        public VMTClient vmtClient;

        public PostProcessingManager postProcessingManager;

        private uint defaultWindowStyle;
        private uint defaultExWindowStyle;

        private System.Threading.SynchronizationContext context = null;

        public Action<GameObject> AdditionalSettingAction = null;
        public Action<UnityMemoryMappedFile.VRMData> VRMmetaLodedAction = null;
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
        }

        void Start()
        {
            Settings.Current.BackgroundColor = BackgroundRenderer.material.color;
            Settings.Current.CustomBackgroundColor = BackgroundRenderer.material.color;

            OpenVRTrackerManager.Instance.OpenVREventAction += async () =>
            {
                await server.SendCommandAsync(new PipeCommands.OpenVRStatus { DashboardOpened = OpenVRTrackerManager.Instance.isDashboardActivated });
            };
        }

        private int SetWindowTitle()
        {
            int setWindowNum = 1;
#if !UNITY_EDITOR
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
#endif
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
            server?.SendCommand(new PipeCommands.QuitApplication { });

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
                    var t = ImportVRM(d.Path);

                    //メタ情報をOSC送信する
                    VRMmetaLodedAction?.Invoke(LoadVRM(d.Path));
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
                else if (e.CommandType == typeof(PipeCommands.GetResolutions))
                {
                    await server.SendCommandAsync(new PipeCommands.ReturnResolutions
                    {
                        List = new List<Tuple<int, int>>(Screen.resolutions.Select(r => (r.width, r.height)).Distinct().Select(r => new Tuple<int, int>(r.width, r.height))),
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetResolution))
                {
                    var d = (PipeCommands.SetResolution)e.Data;
                    Settings.Current.ScreenWidth = d.Width;
                    Settings.Current.ScreenHeight = d.Height;
                    ResizeWindow(d.Width, d.Height);
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
                else if (e.CommandType == typeof(PipeCommands.SetVMCProtocolReceiverSetting))
                {
                    var d = (PipeCommands.SetVMCProtocolReceiverSetting)e.Data;
                    SetVMCProtocolReceiverSetting(d);
                }
                else if (e.CommandType == typeof(PipeCommands.GetVMCProtocolReceiverSetting))
                {
                    var d = (PipeCommands.GetVMCProtocolReceiverSetting)e.Data;
                    if (d.Index == -1)
                    {
                        var newsetting = new VMCProtocolReceiverSettings();
                        newsetting.Port = 39539;
                        newsetting.Name = $"Receiver {Settings.Current.VMCProtocolReceiverSettingsList.Count + 1}";
                        Settings.Current.VMCProtocolReceiverSettingsList.Add(newsetting);
                        newsetting.Port = Settings.Current.VMCProtocolReceiverSettingsList.Max(d => d.Port) + 1;
                        AddVMCProtocolReceiver(newsetting);
                        d.Index = Settings.Current.VMCProtocolReceiverSettingsList.Count - 1;
                    }
                    await server.SendCommandAsync(
                        Settings.Current.VMCProtocolReceiverSettingsList[d.Index].Export(d.Index)
                    , e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.GetVMCProtocolReceiverList))
                {
                    var d = (PipeCommands.GetVMCProtocolReceiverList)e.Data;
                    await server.SendCommandAsync(new PipeCommands.SetVMCProtocolReceiverList
                    {
                        Items = Settings.Current.VMCProtocolReceiverSettingsList.Select(d => Tuple.Create(d.Enable, d.Name, d.Port)).ToList(),
                    }
                    , e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.RemoveVMCProtocolReceiver))
                {
                    var d = (PipeCommands.RemoveVMCProtocolReceiver)e.Data;
                    RemoveVMCProtocolReceiver(d.Index);
                }
                else if (e.CommandType == typeof(PipeCommands.VMCProtocolReceiverRecenter))
                {
                    var d = (PipeCommands.VMCProtocolReceiverRecenter)e.Data;
                    externalMotionReceivers[d.Index].Recenter();
                }
                else if (e.CommandType == typeof(PipeCommands.SetVMCProtocolReceiverEnable))
                {
                    var d = (PipeCommands.SetVMCProtocolReceiverEnable)e.Data;
                    SetVMCProtocolReceiverEnable(d.Index, d.Enable);
                }
                else if (e.CommandType == typeof(PipeCommands.ChangeExternalMotionReceiverRequester))
                {
                    var d = (PipeCommands.ChangeExternalMotionReceiverRequester)e.Data;
                    SetExternalMotionReceiverRequester(d.Enable);

                }
                else if (e.CommandType == typeof(PipeCommands.GetExternalMotionReceiverRequester))
                {
                    await server.SendCommandAsync(new PipeCommands.ChangeExternalMotionReceiverRequester
                    {
                        Enable = Settings.Current.ExternalMotionReceiverRequesterEnable
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
                    if (externalMotionReceivers.Any() && externalMotionReceivers[0].isActiveAndEnabled)
                    {
                        statusStringBuf = externalMotionReceivers[0]?.statusString;
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
                else if (e.CommandType == typeof(PipeCommands.SetLaunchSteamVROnStartup))
                {
                    var d = (PipeCommands.SetLaunchSteamVROnStartup)e.Data;
                    CommonSettings.Current.LaunchSteamVROnStartup = d.Enable;
                    CommonSettings.Save();
                }
                else if (e.CommandType == typeof(PipeCommands.GetLaunchSteamVROnStartup))
                {
                    await server.SendCommandAsync(new PipeCommands.SetLaunchSteamVROnStartup
                    {
                        Enable = CommonSettings.Current.LaunchSteamVROnStartup,
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

        public UnityMemoryMappedFile.VRMData LoadVRM(string path)
        {
            if (string.IsNullOrEmpty(path) || File.Exists(path) == false)
            {
                return null;
            }

            var vrmdata = new UnityMemoryMappedFile.VRMData();
            vrmdata.FilePath = path;

            using (GltfData data = new AutoGltfFileParser(path).Parse())
            {
                VRM.VRMData vrmData = new VRM.VRMData(data);
                using (var context = new VRMImporterContext(vrmData))
                {

                    // metaを取得
                    var meta = context.ReadMeta(true);

                    // サムネイル
                    if (meta.Thumbnail != null)
                    {
                        vrmdata.ThumbnailPNGBytes = meta.Thumbnail.EncodeToPNG(); //Or SaveAsPng( memoryStream, texture.Width, texture.Height )
                    }

                    // Info
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
                }
            }

            return vrmdata;
        }

        public async Task ImportVRM(string path)
        {
            await server.SendCommandAsync(new PipeCommands.VRMLoadStatus { Valid = false });

            Settings.Current.VRMPath = path;

            using (GltfData data = new AutoGltfFileParser(path).Parse())
            {
                VRM.VRMData vrmData = new VRM.VRMData(data);
                using (var context = new VRMImporterContext(vrmData))
                {
                    // ParseしたJSONをシーンオブジェクトに変換していく
                    var runtimeGltfInstance = await context.LoadAsync(new RuntimeOnlyAwaitCaller());
                    runtimeGltfInstance.ShowMeshes();

                    // BlendShape目線制御時の表情とのぶつかりを防ぐ
                    if (context.VRM.firstPerson.lookAtType == LookAtType.BlendShape)
                    {
                        var applyer = runtimeGltfInstance.Root.GetComponent<VRMLookAtBlendShapeApplyer>();
                        applyer.enabled = false;

                        var vmcapplyer = runtimeGltfInstance.Root.AddComponent<VMC_VRMLookAtBlendShapeApplyer>();
                        vmcapplyer.OnImported(context);
                        vmcapplyer.faceController = faceController;
                    }

                    LoadNewModel(runtimeGltfInstance.Root);
                    await server.SendCommandAsync(new PipeCommands.VRMLoadStatus { Valid = true });
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

            VMCEvents.OnCurrentModelChanged?.Invoke(CurrentModel);

            //モデルのSkinnedMeshRendererがカリングされないように、すべてのオプション変更
            foreach (var renderer in CurrentModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
            }

            IKManager.Instance.ModelInitialize();

            VMCEvents.OnModelLoaded?.Invoke(CurrentModel);
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
        private int? windowBorderWidth = null;
        private int? windowBorderHeight = null;

        void HideWindowBorder(bool enable)
        {
            if (lastHideWindowBorder == enable) return;
            lastHideWindowBorder = enable;
            Settings.Current.HideBorder = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
            var hwnd = GetUnityWindowHandle();
            var clientrect = GetUnityWindowClientPosition();
            if (windowBorderWidth.HasValue == false)
            {
                var windowrect = GetUnityWindowPosition();
                windowBorderWidth = windowrect.width - clientrect.width;
                windowBorderHeight = windowrect.height - clientrect.height;
            }
            if (enable)
            {
                SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE); //ウインドウ枠の削除
                SetUnityWindowFrameChanged();
                WaitOneFrameAction(() => SetUnityWindowSize(clientrect.width, clientrect.height));
            }
            else
            {
                SetWindowLong(hwnd, GWL_STYLE, defaultWindowStyle);
                SetUnityWindowFrameChanged();
                WaitOneFrameAction(() => SetUnityWindowSize(clientrect.width + windowBorderWidth.Value, clientrect.height + windowBorderHeight.Value));
            }
#endif
        }

        private void ResizeWindow(int width, int height)
        {
#if !UNITY_EDITOR
            var clientrect = GetUnityWindowClientPosition();
            var windowrect = GetUnityWindowPosition();
            if (windowBorderWidth.HasValue == false)
            {
                windowBorderWidth = windowrect.width - clientrect.width;
                windowBorderHeight = windowrect.height - clientrect.height;
            }
            if (clientrect.width == windowrect.width)
            {
                SetUnityWindowSize(width, height);
            }
            else
            {
                SetUnityWindowSize(width + windowBorderWidth.Value, height + windowBorderHeight.Value);
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
                IKManager.Instance.HandController.SetHandAngle(action.Hand == Hands.Left || action.Hand == Hands.Both, action.Hand == Hands.Right || action.Hand == Hands.Both, action.HandAngles, action.HandChangeTime);
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
            if (responderEnable) easyDeviceDiscoveryProtocolManager.gameObject.SetActive(true);
        }

        public void ChangeExternalMotionSenderAddress(string address, int port)
        {
            Settings.Current.ExternalMotionSenderAddress = address;
            Settings.Current.ExternalMotionSenderPort = port;

            externalMotionSender.ChangeOSCAddress(address, port);
        }

        private void AddVMCProtocolReceiver(VMCProtocolReceiverSettings setting)
        {
            var obj = new GameObject("ExternalReceiver " + setting.Name);
            obj.transform.parent = ExternalMotionReceiverObject.transform;
            var receiver = obj.AddComponent<ExternalReceiverForVMC>();
            receiver.externalSender = externalMotionSender;
            receiver.MIDICCWrapper = midiCCWrapper;
            receiver.eddp = easyDeviceDiscoveryProtocolManager;
            receiver.CurrentModel = CurrentModel;
            receiver.Initialize();

            externalMotionReceivers.Add(receiver);
            externalMotionSender.externalReceiver = externalMotionReceivers.FirstOrDefault();
            easyDeviceDiscoveryProtocolManager.externalReceiver = externalMotionSender.externalReceiver;
            receiver.SetSetting(setting);
            receiver.ChangeOSCPort(setting.Port);
            receiver.SetObjectActive(setting.Enable);
        }

        private void RemoveVMCProtocolReceiver(int index)
        {
            DestroyImmediate(externalMotionReceivers[index].gameObject);
            externalMotionReceivers.RemoveAt(index);
            Settings.Current.VMCProtocolReceiverSettingsList.RemoveAt(index);
            if (index == 0)
            {
                externalMotionSender.externalReceiver = externalMotionReceivers.FirstOrDefault();
                easyDeviceDiscoveryProtocolManager.externalReceiver = externalMotionSender.externalReceiver;
            }
        }

        private void SetVMCProtocolReceiverSetting(PipeCommands.SetVMCProtocolReceiverSetting d)
        {
            int index = d.Index;
            bool changePort = d.Port != Settings.Current.VMCProtocolReceiverSettingsList[index].Port;
            var setting = Settings.Current.VMCProtocolReceiverSettingsList[index].Import(d);

            externalMotionReceivers[index].SetSetting(setting);
            externalMotionReceivers[index].SetObjectActive(setting.Enable);

            if (changePort)
            {
                externalMotionReceivers[index].ChangeOSCPort(setting.Port);
            }
        }

        private void SetVMCProtocolReceiverEnable(int index, bool enable)
        {
            var setting = Settings.Current.VMCProtocolReceiverSettingsList[index];
            setting.Enable = enable;

            externalMotionReceivers[index].SetSetting(setting);
            externalMotionReceivers[index].SetObjectActive(setting.Enable);
        }

        private void SetExternalMotionReceiverRequester(bool requesterEnable)
        {
            Settings.Current.ExternalMotionReceiverRequesterEnable = requesterEnable;
            easyDeviceDiscoveryProtocolManager.requesterEnable = requesterEnable;
            if (requesterEnable) easyDeviceDiscoveryProtocolManager.gameObject.SetActive(true);
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


        private NotifyLogTypes notifyLogLevel = NotifyLogTypes.Warning;
        private async void LogMessageHandler(string cond, string trace, LogType type)
        {
            NotifyLogTypes notifyType = NotifyLogTypes.Warning;
            switch (type)
            {
                case LogType.Assert: notifyType = NotifyLogTypes.Assert; CriticalErrorCount++; break;
                case LogType.Error: notifyType = NotifyLogTypes.Error; CriticalErrorCount++; break;
                case LogType.Exception: notifyType = NotifyLogTypes.Exception; CriticalErrorCount++; break;
                case LogType.Log: notifyType = NotifyLogTypes.Log; break;
                case LogType.Warning: notifyType = NotifyLogTypes.Warning; break;
                default: notifyType = NotifyLogTypes.Log; break;
            }

            //通知レベルがLog以外の時かつ、Warning以下かつ、*から始まらないものはうるさいので飛ばさない
            if (cond.StartsWith("*"))
            {
                cond = cond.Substring(1);
            }
            else if (notifyLogLevel != NotifyLogTypes.Log && notifyLogLevel <= notifyType)
            {
                return;
            }

            //あまりにも致命的エラーが多すぎる場合は強制終了する
            if ((!IsCriticalErrorCountOver) && CriticalErrorCount > PipeCommands.ErrorCountMax)
            {
                IsCriticalErrorCountOver = true;

                //最後のエラーをファイルとして出力
                string message = "[" + type.ToString() + "] " + cond + "\n\n" + trace + "\n\n" + DateTime.Now.ToString();
                File.WriteAllText(Application.dataPath + "/../CriticalErrorCountOver.txt", message);
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

            if (
                (type == LogType.Error && cond.StartsWith("[Calib Fail]")) ||
                (type == LogType.Exception && cond.StartsWith("NullReferenceException"))
                )
            {
                //状態を失敗で上書き
                IKManager.Instance.CalibrationResult = new PipeCommands.CalibrationResult
                {
                    Type = PipeCommands.CalibrateType.Invalid,
                    Message = cond,
                    UserHeight = -1
                };

                //エラー送信
                await server.SendCommandAsync(IKManager.Instance.CalibrationResult);
            }
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
            if (CommonSettings.Current.LoadSettingFilePathOnStart != path)
            {
                CommonSettings.Current.LoadSettingFilePathOnStart = path;
                CommonSettings.Save();
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
                CommonSettings.Load();

                //初回読み込みファイルが存在しなければdefault.jsonを
                if (string.IsNullOrEmpty(CommonSettings.Current.LoadSettingFilePathOnStart) || (!File.Exists(CommonSettings.Current.LoadSettingFilePathOnStart)))
                {
                    path = Application.dataPath + "/../default.json";
                    Debug.Log("Load default.json");
                }
                else
                {
                    //存在すればそのPathを読みに行こうとする
                    path = CommonSettings.Current.LoadSettingFilePathOnStart;
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
            IKManager.Instance.ResetTrackerScale();
            //設定を適用する
            ApplySettings();

            //有効なJSONが取得できたかチェック
            if (Settings.Current != null)
            {
                lastLoadedConfigPath = path; //パスを記録

                //ファイルが正常に存在したので、現在共通設定に記録されているパスと違う場合、共通設定に書き込む
                if (CommonSettings.Current.LoadSettingFilePathOnStart != path)
                {
                    CommonSettings.Current.LoadSettingFilePathOnStart = path;
                    CommonSettings.Save();
                    Debug.Log("Save last loaded file of " + path);
                }
            }

            //設定の変更を通知
            VMCEvents.OnLoadedConfigPathChanged?.Invoke(path);
        }

        //Settings.Currentを各種設定に適用
        private async void ApplySettings()
        {
            //VRMのパスが有効で、存在するなら読み込む
            if (string.IsNullOrWhiteSpace(Settings.Current.VRMPath) == false
                && File.Exists(Settings.Current.VRMPath))
            {
                await server.SendCommandAsync(new PipeCommands.LoadVRMPath { Path = Settings.Current.VRMPath });
                await ImportVRM(Settings.Current.VRMPath);

                //メタ情報をOSC送信する
                VRMmetaLodedAction?.Invoke(LoadVRM(Settings.Current.VRMPath));
            }

            //SetResolutionは強制的にウインドウ枠を復活させるのでBorder設定の前にやっておく必要がある
            if (Screen.resolutions.Any(d => d.width == Settings.Current.ScreenWidth && d.height == Settings.Current.ScreenHeight))
            {
                UpdateActionQueue.Enqueue(() => ResizeWindow(Settings.Current.ScreenWidth, Settings.Current.ScreenHeight));
            }

            if (Settings.Current.BackgroundColor != null)
            {
                UpdateActionQueue.Enqueue(() => ChangeBackgroundColor(Settings.Current.BackgroundColor.r, Settings.Current.BackgroundColor.g, Settings.Current.BackgroundColor.b, false));
            }

            if (Settings.Current.IsTransparent)
            {
                UpdateActionQueue.Enqueue(() => SetBackgroundTransparent());
            }

            if (Settings.Current.CustomBackgroundColor != null)
            {
                await server.SendCommandAsync(new PipeCommands.LoadCustomBackgroundColor { r = Settings.Current.CustomBackgroundColor.r, g = Settings.Current.CustomBackgroundColor.g, b = Settings.Current.CustomBackgroundColor.b });
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

            if (Settings.Current.IsSettingVersionBefore(0, 48))
            {
                //v0.48 BlendShapeKey case sensitive.
                foreach (var keyAction in Settings.Current.KeyActions)
                {
                    if (keyAction.FaceNames != null && keyAction.FaceNames.Count > 0)
                    {
                        keyAction.FaceNames = keyAction.FaceNames.Select(d => faceController.GetCaseSensitiveKeyName(d)).ToList();
                    }
                }
            }

            if (Settings.Current.IsSettingVersionBefore(0, 56))
            {
                //v0.56 Configure multiple VMCProtocol receivers

                if (Settings.Current.ExternalMotionReceiverPortList == null) Settings.Current.ExternalMotionReceiverPortList = new List<int>() { Settings.Current.ExternalMotionReceiverPort, Settings.Current.ExternalMotionReceiverPort + 1 };
                if (Settings.Current.ExternalMotionReceiverDelayMsList == null) Settings.Current.ExternalMotionReceiverDelayMsList = new List<int>() { 0, 0 };
                if (Settings.Current.ExternalMotionReceiverEnableList == null) Settings.Current.ExternalMotionReceiverEnableList = new List<bool>() { Settings.Current.ExternalMotionReceiverEnable, false };

                for (int i = 0; i < Settings.Current.ExternalMotionReceiverEnableList.Count; i++)
                {
                    Settings.Current.VMCProtocolReceiverSettingsList.Add(new VMCProtocolReceiverSettings
                    {
                        Enable = Settings.Current.ExternalMotionReceiverEnableList[i],
                        Port = Settings.Current.ExternalMotionReceiverPortList[i],
                        DelayMs = Settings.Current.ExternalMotionReceiverDelayMsList[i],
                        Name = $"Receiver {i + 1}",
                        ApplyRootRotation = false,
                        ApplyRootPosition = false,
                        ApplySpine = false,
                        ApplyChest = false,
                        ApplyHead = false,
                        ApplyLeftArm = false,
                        ApplyRightArm = false,
                        ApplyLeftHand = false,
                        ApplyRightHand = false,
                        ApplyLeftLeg = false,
                        ApplyRightLeg = false,
                        ApplyLeftFoot = false,
                        ApplyRightFoot = false,
                        ApplyEye = false,
                        ApplyLeftFinger = false,
                        ApplyRightFinger = false,
                    }); ;
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
            IKManager.Instance.SetHandFreeOffset();

            await server.SendCommandAsync(new PipeCommands.LoadLipSyncEnable { enable = Settings.Current.LipSyncEnable });
            SetLipSyncEnable(Settings.Current.LipSyncEnable);

            await server.SendCommandAsync(new PipeCommands.SetLightAngle { X = Settings.Current.LightRotationX, Y = Settings.Current.LightRotationY });
            SetLightAngle(Settings.Current.LightRotationX, Settings.Current.LightRotationY);
            await server.SendCommandAsync(new PipeCommands.ChangeLightColor { a = Settings.Current.LightColor.a, r = Settings.Current.LightColor.r, g = Settings.Current.LightColor.g, b = Settings.Current.LightColor.b });
            ChangeLightColor(Settings.Current.LightColor.a, Settings.Current.LightColor.r, Settings.Current.LightColor.g, Settings.Current.LightColor.b);

            SetExternalMotionSenderEnable(Settings.Current.ExternalMotionSenderEnable);
            ChangeExternalMotionSenderAddress(Settings.Current.ExternalMotionSenderAddress, Settings.Current.ExternalMotionSenderPort, Settings.Current.ExternalMotionSenderPeriodStatus, Settings.Current.ExternalMotionSenderPeriodRoot, Settings.Current.ExternalMotionSenderPeriodBone, Settings.Current.ExternalMotionSenderPeriodBlendShape, Settings.Current.ExternalMotionSenderPeriodCamera, Settings.Current.ExternalMotionSenderPeriodDevices, Settings.Current.ExternalMotionSenderOptionString, Settings.Current.ExternalMotionSenderResponderEnable);

            foreach(var receiver in externalMotionReceivers)
            {
                DestroyImmediate(receiver.gameObject);
            }
            externalMotionReceivers.Clear();

            foreach(var receiverSetting in Settings.Current.VMCProtocolReceiverSettingsList)
            {
                AddVMCProtocolReceiver(receiverSetting);
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