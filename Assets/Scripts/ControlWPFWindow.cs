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
using System.Reflection;
using System.Threading.Tasks;
using VMCMod;
#if UNITY_EDITOR   // エディタ上でしか動きません。
using UnityEditor;
#endif

public class ControlWPFWindow : MonoBehaviour
{
    public bool IsBeta = false;
    public bool IsPreRelease = false;

    public string VersionString;
    private string baseVersionString;

    public TrackerHandler handler = null;
    public Transform LeftWristTransform = null;
    public Transform RightWristTransform = null;

    public CameraLookTarget CalibrationCamera;

    public GameObject LeftHandCamera;
    public GameObject RightHandCamera;

    public CameraMouseControl FreeCamera;
    public CameraMouseControl FrontCamera;
    public CameraMouseControl BackCamera;
    public CameraMouseControl PositionFixedCamera;

    public Renderer BackgroundRenderer;

    public GameObject GridCanvas;

    public DynamicOVRLipSync LipSync;

    public FaceController faceController;
    public HandController handController;

    public SteamVR2Input steamVR2Input;

    public WristRotationFix wristRotationFix;

    public Transform HandTrackerRoot;
    public Transform HeadTrackerRoot;
    public Transform PelvisTrackerRoot;
    public Transform RealTrackerRoot;

    public GameObject ExternalMotionSenderObject;
    private ExternalSender externalMotionSender;

    public GameObject ExternalMotionReceiverObject;
    private ExternalReceiverForVMC externalMotionReceiver;

    public MemoryMappedFileServer server;
    private string pipeName = Guid.NewGuid().ToString();

    private GameObject CurrentModel = null;

    private RootMotion.FinalIK.VRIK vrik = null;

    public Camera ControlCamera;
    public CameraMouseControl CurrentCameraControl;


    private Animator animator = null;

    private int CurrentWindowNum = 1;

    public int CriticalErrorCount = 0;

    public VMTClient vmtClient;

    public PostProcessingManager postProcessingManager;

    public enum MouseButtons
    {
        Left = 0,
        Right = 1,
        Center = 2,
    }

    private uint defaultWindowStyle;
    private uint defaultExWindowStyle;

    private System.Threading.SynchronizationContext context = null;

    public Action<GameObject> ModelLoadedAction = null;
    public Action<GameObject> AdditionalSettingAction = null;
    public Action<Camera> CameraChangedAction = null;
    public Action<VRMData> VRMmetaLodedAction = null;
    public Action<string> VRMRemoteLoadedAction = null;
    public Action LightChangedAction = null;
    public Action LoadedConfigPathChangedAction = null;

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

    public MidiCCWrapper midiCCWrapper;

    public MIDICCBlendShape midiCCBlendShape;

    public enum CalibrationState
    {
        Uncalibrated = 0,
        WaitingForCalibrating = 1,
        Calibrating = 2,
        Calibrated = 3,
    }

    public CalibrationState calibrationState = CalibrationState.Uncalibrated;
    public PipeCommands.CalibrateType lastCalibrateType = PipeCommands.CalibrateType.Default; //最後に行ったキャリブレーションの種類

    public string lastLoadedConfigPath = "";

    public EasyDeviceDiscoveryProtocolManager easyDeviceDiscoveryProtocolManager;

    public ModManager modManager;

    // Use this for initialization
    void Start()
    {
        context = System.Threading.SynchronizationContext.Current;

#if UNITY_EDITOR   // エディタ上でしか動きません。
        pipeName = "VMCTest";
#else
        //Debug.unityLogger.logEnabled = false;
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
        
        CurrentSettings.BackgroundColor = BackgroundRenderer.material.color;
        CurrentSettings.CustomBackgroundColor = BackgroundRenderer.material.color;

        steamVR2Input.KeyDownEvent += ControllerAction_KeyDown;
        steamVR2Input.KeyUpEvent += ControllerAction_KeyUp;
        steamVR2Input.AxisChangedEvent += ControllerAction_AxisChanged;

        KeyboardAction.KeyDownEvent += KeyboardAction_KeyDown;
        KeyboardAction.KeyUpEvent += KeyboardAction_KeyUp;

        CameraChangedAction?.Invoke(ControlCamera);

        externalMotionSender = ExternalMotionSenderObject.GetComponent<ExternalSender>();
        externalMotionReceiver = ExternalMotionReceiverObject.GetComponent<ExternalReceiverForVMC>();

        midiCCWrapper.noteOnDelegateProxy += async (channel, note, velocity) =>
        {
            Debug.Log("MidiNoteOn:" + channel + "/" + note + "/" + velocity);

            var config = new KeyConfig();
            config.type = KeyTypes.Midi;
            config.actionType = KeyActionTypes.Face;
            config.keyCode = (int)channel;
            config.keyIndex = note;
            config.keyName = MidiName(channel, note);
            if (doKeyConfig || doKeySend) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            if (!doKeyConfig) CheckKey(config, true);
        };

        midiCCWrapper.noteOffDelegateProxy += (channel, note) =>
        {
            Debug.Log("MidiNoteOff:" + channel + "/" + note);

            var config = new KeyConfig();
            config.type = KeyTypes.Midi;
            config.actionType = KeyActionTypes.Face;
            config.keyCode = (int)channel;
            config.keyIndex = note;
            config.keyName = MidiName(channel, note);
            if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            if (!doKeyConfig) CheckKey(config, false);
        };
        midiCCWrapper.knobUpdateBoolDelegate += async (int knobNo, bool value) =>
        {
            MidiJack.MidiChannel channel = MidiJack.MidiChannel.Ch1; //仮でCh1
            Debug.Log("MidiCC:" + channel + "/" + knobNo + "/" + value);

            var config = new KeyConfig();
            config.type = KeyTypes.MidiCC;
            config.actionType = KeyActionTypes.Face;
            config.keyCode = (int)channel;
            config.keyIndex = knobNo;
            config.keyName = MidiName(channel, knobNo);

            if (value)
            {
                if (doKeyConfig || doKeySend) await server.SendCommandAsync(new PipeCommands.KeyDown { Config = config });
            }
            else
            {
                if (doKeyConfig || doKeySend) { }//  await server.SendCommandAsync(new PipeCommands.KeyUp { Config = config });
            }
            if (!doKeyConfig) CheckKey(config, value);
        };

        midiCCWrapper.knobDelegateProxy += (MidiJack.MidiChannel channel, int knobNo, float value) =>
        {
            CheckKnobUpdated(channel, knobNo, value);
        };

        ModelLoadedAction += gameObject => VMCEvents.OnModelLoaded?.Invoke(gameObject);
        CameraChangedAction += camera => VMCEvents.OnCameraChanged?.Invoke(camera);
    }

    private string MidiName(MidiJack.MidiChannel channel, int note)
    {
        return $"MIDI Ch{(int)channel + 1} {note}";
    }

    private float[] lastKnobUpdatedSendTime = new float[MidiCCWrapper.KNOBS];

    private async void CheckKnobUpdated(MidiJack.MidiChannel channel, int knobNo, float value)
    {
        if (doKeySend == false) return;
        if (lastKnobUpdatedSendTime[knobNo] + 3f < Time.realtimeSinceStartup)
        {
            lastKnobUpdatedSendTime[knobNo] = Time.realtimeSinceStartup;
            await server?.SendCommandAsync(new PipeCommands.MidiCCKnobUpdate { channel = (int)channel, knobNo = knobNo, value = value });
        }
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
        Assets.Scripts.NativeMethods.SetUnityWindowTitle($"{Application.productName} {baseVersionString + buildString} ({setWindowNum})");
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
        KeyboardAction.KeyDownEvent -= KeyboardAction_KeyDown;
        KeyboardAction.KeyUpEvent -= KeyboardAction_KeyUp;

        steamVR2Input.KeyDownEvent -= ControllerAction_KeyDown;
        steamVR2Input.KeyUpEvent -= ControllerAction_KeyUp;
        steamVR2Input.AxisChangedEvent -= ControllerAction_AxisChanged;

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
                var t = ImportVRM(d.Path, d.ImportForCalibration, d.UseCurrentFixSetting ? CurrentSettings.EnableNormalMapFix : d.EnableNormalMapFix, d.UseCurrentFixSetting ? CurrentSettings.DeleteHairNormalMap : d.DeleteHairNormalMap);

                //メタ情報をOSC送信する
                VRMmetaLodedAction?.Invoke(LoadVRM(d.Path));
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
            else if (e.CommandType == typeof(PipeCommands.SetCameraSmooth))
            {
                var d = (PipeCommands.SetCameraSmooth)e.Data;
                SetCameraSmooth(d.value);
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
                    CurrentSettings.LeftThumbStickPoints = d.LeftPoints;
                    CurrentSettings.RightThumbStickPoints = d.RightPoints;
                }
                else
                {
                    CurrentSettings.LeftCenterEnable = d.LeftCenterEnable;
                    CurrentSettings.RightCenterEnable = d.RightCenterEnable;
                    CurrentSettings.LeftTouchPadPoints = d.LeftPoints;
                    CurrentSettings.RightTouchPadPoints = d.RightPoints;
                }
            }
            else if (e.CommandType == typeof(PipeCommands.SetSkeletalInputEnable))
            {
                var d = (PipeCommands.SetSkeletalInputEnable)e.Data;
                CurrentSettings.EnableSkeletal = d.enable;
                steamVR2Input.EnableSkeletal = CurrentSettings.EnableSkeletal;
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
                CurrentSettings.LeftHandPositionX = d.LeftHandPositionX / 1000f;
                CurrentSettings.LeftHandPositionY = d.LeftHandPositionY / 1000f;
                CurrentSettings.LeftHandPositionZ = d.LeftHandPositionZ / 1000f;
                CurrentSettings.LeftHandRotationX = d.LeftHandRotationX;
                CurrentSettings.LeftHandRotationY = d.LeftHandRotationY;
                CurrentSettings.LeftHandRotationZ = d.LeftHandRotationZ;
                CurrentSettings.RightHandPositionX = d.RightHandPositionX / 1000f;
                CurrentSettings.RightHandPositionY = d.RightHandPositionY / 1000f;
                CurrentSettings.RightHandPositionZ = d.RightHandPositionZ / 1000f;
                CurrentSettings.RightHandRotationX = d.RightHandRotationX;
                CurrentSettings.RightHandRotationY = d.RightHandRotationY;
                CurrentSettings.RightHandRotationZ = d.RightHandRotationZ;
                CurrentSettings.SwivelOffset = d.SwivelOffset;
                SetHandFreeOffset();
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
                var rposition = tracker.InverseTransformPoint(ControlCamera.transform.position);
                var rrotation = (Quaternion.Inverse(tracker.rotation) * ControlCamera.transform.rotation).eulerAngles;
                await server.SendCommandAsync(new PipeCommands.SetExternalCameraConfig
                {
                    x = rposition.x,
                    y = rposition.y,
                    z = rposition.z,
                    rx = rrotation.x,
                    ry = rrotation.y,
                    rz = rrotation.z,
                    fov = ControlCamera.fieldOfView,
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
                    OffsetHorizontal = CurrentSettings.EyeTracking_TobiiOffsetHorizontal,
                    OffsetVertical = CurrentSettings.EyeTracking_TobiiOffsetVertical,
                    ScaleHorizontal = CurrentSettings.EyeTracking_TobiiScaleHorizontal,
                    ScaleVertical = CurrentSettings.EyeTracking_TobiiScaleVertical
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
                    OffsetHorizontal = CurrentSettings.EyeTracking_ViveProEyeOffsetHorizontal,
                    OffsetVertical = CurrentSettings.EyeTracking_ViveProEyeOffsetVertical,
                    ScaleHorizontal = CurrentSettings.EyeTracking_ViveProEyeScaleHorizontal,
                    ScaleVertical = CurrentSettings.EyeTracking_ViveProEyeScaleVertical
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
                CurrentSettings.EyeTracking_ViveProEyeEnable = d.enable;
                SetEyeTracking_ViveProEyeEnable(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.GetEyeTracking_ViveProEyeUseEyelidMovements))
            {
                await server.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements
                {
                    Use = CurrentSettings.EyeTracking_ViveProEyeUseEyelidMovements,
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.GetEyeTracking_ViveProEyeEnable))
            {
                await server.SendCommandAsync(new PipeCommands.SetEyeTracking_ViveProEyeEnable
                {
                    enable = CurrentSettings.EyeTracking_ViveProEyeEnable,
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
                    ApplyCurrentSettings();
                }
            }
            else if (e.CommandType == typeof(PipeCommands.ImportCameraPlus))
            {
                var d = (PipeCommands.ImportCameraPlus)e.Data;
                ImportCameraPlus(d);
            }
            else if (e.CommandType == typeof(PipeCommands.ExportCameraPlus))
            {
                await server.SendCommandAsync(new PipeCommands.ReturnExportCameraPlus
                {
                    x = CurrentSettings.FreeCameraTransform.localPosition.x,
                    y = CurrentSettings.FreeCameraTransform.localPosition.y,
                    z = CurrentSettings.FreeCameraTransform.localPosition.z,
                    rx = CurrentSettings.FreeCameraTransform.localRotation.eulerAngles.x,
                    ry = CurrentSettings.FreeCameraTransform.localRotation.eulerAngles.y,
                    rz = CurrentSettings.FreeCameraTransform.localRotation.eulerAngles.z,
                    fov = ControlCamera.fieldOfView
                }, e.RequestId);
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
                    enable = CurrentSettings.ExternalMotionSenderEnable
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
                    address = CurrentSettings.ExternalMotionSenderAddress,
                    port = CurrentSettings.ExternalMotionSenderPort,
                    PeriodStatus = CurrentSettings.ExternalMotionSenderPeriodStatus,
                    PeriodRoot = CurrentSettings.ExternalMotionSenderPeriodRoot,
                    PeriodBone = CurrentSettings.ExternalMotionSenderPeriodBone,
                    PeriodBlendShape = CurrentSettings.ExternalMotionSenderPeriodBlendShape,
                    PeriodCamera = CurrentSettings.ExternalMotionSenderPeriodCamera,
                    PeriodDevices = CurrentSettings.ExternalMotionSenderPeriodDevices,
                    OptionString = CurrentSettings.ExternalMotionSenderOptionString,
                    ResponderEnable = CurrentSettings.ExternalMotionSenderResponderEnable
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.EnableExternalMotionReceiver))
            {
                var d = (PipeCommands.EnableExternalMotionReceiver)e.Data;
                SetExternalMotionReceiverEnable(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.GetEnableExternalMotionReceiver))
            {
                await server.SendCommandAsync(new PipeCommands.EnableExternalMotionReceiver
                {
                    enable = CurrentSettings.ExternalMotionReceiverEnable
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.ChangeExternalMotionReceiverPort))
            {
                var d = (PipeCommands.ChangeExternalMotionReceiverPort)e.Data;
                ChangeExternalMotionReceiverPort(d.port, d.RequesterEnable);

            }
            else if (e.CommandType == typeof(PipeCommands.GetExternalMotionReceiverPort))
            {
                await server.SendCommandAsync(new PipeCommands.ChangeExternalMotionReceiverPort
                {
                    port = CurrentSettings.ExternalMotionReceiverPort,
                    RequesterEnable = CurrentSettings.ExternalMotionReceiverRequesterEnable
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.GetMidiCCBlendShape))
            {
                var bs = CurrentSettings.MidiCCBlendShape;
                await server.SendCommandAsync(new PipeCommands.SetMidiCCBlendShape
                {
                    BlendShapes = CurrentSettings.MidiCCBlendShape,
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
                    enable = CurrentSettings.MidiEnable,
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
                    globalEnable = CurrentSettings.TrackingFilterEnable,
                    hmdEnable = CurrentSettings.TrackingFilterHmdEnable,
                    controllerEnable = CurrentSettings.TrackingFilterControllerEnable,
                    trackerEnable = CurrentSettings.TrackingFilterTrackerEnable,
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.EnableModelModifier))
            {
                var d = (PipeCommands.EnableModelModifier)e.Data;
                SetModelModifierEnable(d.fixKneeRotation);
            }
            else if (e.CommandType == typeof(PipeCommands.GetEnableModelModifier))
            {
                await server.SendCommandAsync(new PipeCommands.EnableModelModifier
                {
                    fixKneeRotation = CurrentSettings.FixKneeRotation,
                }, e.RequestId);
            }
            //------------------------
            else if (e.CommandType == typeof(PipeCommands.GetStatusString))
            {
                string statusStringBuf = "";
                //有効な場合だけ送る
                if (externalMotionReceiver.isActiveAndEnabled)
                {
                    statusStringBuf = externalMotionReceiver?.statusString;
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
                    HandleControllerAsTracker = CurrentSettings.HandleControllerAsTracker
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.GetQualitySettings))
            {
                await server.SendCommandAsync(new PipeCommands.SetQualitySettings
                {
                    antiAliasing = CurrentSettings.AntiAliasing,
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
                        LipShapesToBlendShapeMap = CurrentSettings.LipShapesToBlendShapeMap,
                    }, e.RequestId);
                }
            }
            else if (e.CommandType == typeof(PipeCommands.GetViveLipTrackingEnable))
            {
                await server.SendCommandAsync(new PipeCommands.SetViveLipTrackingEnable
                {
                    enable = CurrentSettings.LipTracking_ViveEnable,
                }, e.RequestId);
            }
            else if (e.CommandType == typeof(PipeCommands.SetViveLipTrackingEnable))
            {
                var d = (PipeCommands.SetViveLipTrackingEnable)e.Data;
                CurrentSettings.LipTracking_ViveEnable = d.enable;
                SetLipTracking_ViveEnable(d.enable);
            }
            else if (e.CommandType == typeof(PipeCommands.SetViveLipTrackingBlendShape))
            {
                var d = (PipeCommands.SetViveLipTrackingBlendShape)e.Data;
                CurrentSettings.LipShapesToBlendShapeMap = d.LipShapesToBlendShapeMap;
                SetLipShapeToBlendShapeStringMapAction?.Invoke(d.LipShapesToBlendShapeMap);
            }
            else if (e.CommandType == typeof(PipeCommands.GetAdvancedGraphicsOption))
            {
                LoadAdvancedGraphicsOption();
            }
            else if (e.CommandType == typeof(PipeCommands.SetAdvancedGraphicsOption))
            {
                var d = (PipeCommands.SetAdvancedGraphicsOption)e.Data;

                CurrentSettings.PPS_Enable = d.PPS_Enable;

                CurrentSettings.PPS_Bloom_Enable = d.Bloom_Enable;
                CurrentSettings.PPS_Bloom_Intensity = d.Bloom_Intensity;
                CurrentSettings.PPS_Bloom_Threshold = d.Bloom_Threshold;

                CurrentSettings.PPS_DoF_Enable = d.DoF_Enable;
                CurrentSettings.PPS_DoF_FocusDistance = d.DoF_FocusDistance;
                CurrentSettings.PPS_DoF_Aperture = d.DoF_Aperture;
                CurrentSettings.PPS_DoF_FocusLength = d.DoF_FocusLength;
                CurrentSettings.PPS_DoF_MaxBlurSize = d.DoF_MaxBlurSize;

                CurrentSettings.PPS_CG_Enable = d.CG_Enable;
                CurrentSettings.PPS_CG_Temperature = d.CG_Temperature;
                CurrentSettings.PPS_CG_Saturation = d.CG_Saturation;
                CurrentSettings.PPS_CG_Contrast = d.CG_Contrast;
                CurrentSettings.PPS_CG_Gamma = d.CG_Gamma;

                CurrentSettings.PPS_Vignette_Enable = d.Vignette_Enable;
                CurrentSettings.PPS_Vignette_Intensity = d.Vignette_Intensity;
                CurrentSettings.PPS_Vignette_Smoothness = d.Vignette_Smoothness;
                CurrentSettings.PPS_Vignette_Roundness = d.Vignette_Roundness;

                CurrentSettings.PPS_CA_Enable = d.CA_Enable;
                CurrentSettings.PPS_CA_Intensity = d.CA_Intensity;
                CurrentSettings.PPS_CA_FastMode = d.CA_FastMode;

                CurrentSettings.PPS_Bloom_Color_a = d.Bloom_Color_a;
                CurrentSettings.PPS_Bloom_Color_r = d.Bloom_Color_r;
                CurrentSettings.PPS_Bloom_Color_g = d.Bloom_Color_g;
                CurrentSettings.PPS_Bloom_Color_b = d.Bloom_Color_b;

                CurrentSettings.PPS_CG_ColorFilter_a = d.CG_ColorFilter_a;
                CurrentSettings.PPS_CG_ColorFilter_r = d.CG_ColorFilter_r;
                CurrentSettings.PPS_CG_ColorFilter_g = d.CG_ColorFilter_g;
                CurrentSettings.PPS_CG_ColorFilter_b = d.CG_ColorFilter_b;

                CurrentSettings.PPS_Vignette_Color_a = d.Vignette_Color_a;
                CurrentSettings.PPS_Vignette_Color_r = d.Vignette_Color_r;
                CurrentSettings.PPS_Vignette_Color_g = d.Vignette_Color_g;
                CurrentSettings.PPS_Vignette_Color_b = d.Vignette_Color_b;

                CurrentSettings.TurnOffAmbientLight = d.TurnOffAmbientLight;

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
                    ReceiveBonesEnable = CurrentSettings.ExternalBonesReceiverEnable
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
            CurrentSettings.LightRotationX = x;
            CurrentSettings.LightRotationY = y;

            LightChangedAction?.Invoke();
        }
    }

    private void ChangeLightColor(float a, float r, float g, float b)
    {
        if (MainDirectionalLight != null)
        {
            CurrentSettings.LightColor = new Color(r, g, b, a);
            MainDirectionalLight.color = CurrentSettings.LightColor;

            LightChangedAction?.Invoke();
        }
    }

    private void SetQualitySettings(PipeCommands.SetQualitySettings setting)
    {
        CurrentSettings.AntiAliasing = setting.antiAliasing;
        QualitySettings.antiAliasing = setting.antiAliasing;
    }

    private void SetVMT(bool enable, int no)
    {
        vmtClient.SetNo(no);
        vmtClient.SetEnable(enable);
        vmtClient.SendRoomMatrixTemporary();

        CurrentSettings.VirtualMotionTrackerNo = no;
        CurrentSettings.VirtualMotionTrackerEnable = enable;
    }

    private async void LoadAdvancedGraphicsOption()
    {
        SetAdvancedGraphicsOption();
        await server.SendCommandAsync(new PipeCommands.SetAdvancedGraphicsOption
        {
            PPS_Enable = CurrentSettings.PPS_Enable,

            Bloom_Enable = CurrentSettings.PPS_Bloom_Enable,
            Bloom_Intensity = CurrentSettings.PPS_Bloom_Intensity,
            Bloom_Threshold = CurrentSettings.PPS_Bloom_Threshold,

            DoF_Enable = CurrentSettings.PPS_DoF_Enable,
            DoF_FocusDistance = CurrentSettings.PPS_DoF_FocusDistance,
            DoF_Aperture = CurrentSettings.PPS_DoF_Aperture,
            DoF_FocusLength = CurrentSettings.PPS_DoF_FocusLength,
            DoF_MaxBlurSize = CurrentSettings.PPS_DoF_MaxBlurSize,

            CG_Enable = CurrentSettings.PPS_CG_Enable,
            CG_Temperature = CurrentSettings.PPS_CG_Temperature,
            CG_Saturation = CurrentSettings.PPS_CG_Saturation,
            CG_Contrast = CurrentSettings.PPS_CG_Contrast,
            CG_Gamma = CurrentSettings.PPS_CG_Gamma,

            Vignette_Enable = CurrentSettings.PPS_Vignette_Enable,
            Vignette_Intensity = CurrentSettings.PPS_Vignette_Intensity,
            Vignette_Smoothness = CurrentSettings.PPS_Vignette_Smoothness,
            Vignette_Roundness = CurrentSettings.PPS_Vignette_Roundness,

            CA_Enable = CurrentSettings.PPS_CA_Enable,
            CA_Intensity = CurrentSettings.PPS_CA_Intensity,
            CA_FastMode = CurrentSettings.PPS_CA_FastMode,

            Bloom_Color_a = CurrentSettings.PPS_Bloom_Color_a,
            Bloom_Color_r = CurrentSettings.PPS_Bloom_Color_r,
            Bloom_Color_g = CurrentSettings.PPS_Bloom_Color_g,
            Bloom_Color_b = CurrentSettings.PPS_Bloom_Color_b,

            CG_ColorFilter_a = CurrentSettings.PPS_CG_ColorFilter_a,
            CG_ColorFilter_r = CurrentSettings.PPS_CG_ColorFilter_r,
            CG_ColorFilter_g = CurrentSettings.PPS_CG_ColorFilter_g,
            CG_ColorFilter_b = CurrentSettings.PPS_CG_ColorFilter_b,

            Vignette_Color_a = CurrentSettings.PPS_Vignette_Color_a,
            Vignette_Color_r = CurrentSettings.PPS_Vignette_Color_r,
            Vignette_Color_g = CurrentSettings.PPS_Vignette_Color_g,
            Vignette_Color_b = CurrentSettings.PPS_Vignette_Color_b,

            TurnOffAmbientLight = CurrentSettings.TurnOffAmbientLight
    });
    }

    private void SetAdvancedGraphicsOption() {
        postProcessingManager.Apply(CurrentSettings);
    }

    private bool isFirstTimeExecute = true;

    #region VRM

    public VRMData LoadVRM(string path)
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

    public async Task ImportVRM(string path, bool ImportForCalibration, bool EnableNormalMapFix, bool DeleteHairNormalMap)
    {
        if (ImportForCalibration == false)
        {
            calibrationState = CalibrationState.Uncalibrated; //キャリブレーション状態を"未キャリブレーション"に設定
            CurrentSettings.VRMPath = path;
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

        ModelLoadedAction?.Invoke(CurrentModel);
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
        if (animator != null && CurrentSettings.FixKneeRotation)
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
            foreach (var tracker in handler.Trackers.Where(d => d != handler.CameraControllerObject && d.name.Contains("LIV Virtual Camera") == false && !(CurrentSettings.VirtualMotionTrackerEnable && d.name.Contains($"VMT_{CurrentSettings.VirtualMotionTrackerNo}"))))
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
        if (vrik == null) { return; } //まだmodelがない

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

    private Transform leftHandFreeOffsetRotation;
    private Transform rightHandFreeOffsetRotation;
    private Transform leftHandFreeOffsetPosition;
    private Transform rightHandFreeOffsetPosition;

    public IEnumerator Calibrate(PipeCommands.CalibrateType calibrateType)
    {
        lastCalibrateType = calibrateType;//最後に実施したキャリブレーションタイプとして記録

        SetVRIK(CurrentModel);
        wristRotationFix.SetVRIK(vrik);

        //var leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        //var leftRelaxer = leftLowerArm.gameObject.AddComponent<TwistRelaxer>();
        //leftRelaxer.ik = vrik;
        //leftRelaxer.twistSolvers = new TwistSolver[] { new TwistSolver { transform = leftLowerArm } };
        //var rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        //var rightRelaxer = rightLowerArm.gameObject.AddComponent<TwistRelaxer>();
        //rightRelaxer.ik = vrik;
        //rightRelaxer.twistSolvers = new TwistSolver[] { new TwistSolver { transform = rightLowerArm } };

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

        ClearChildren(headTracker, leftHandTracker, rightHandTracker, bodyTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);

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


        var calibratedLeftHandTransform = leftHandTracker.GetChild(0);
        var calibratedRightHandTransform = rightHandTracker.GetChild(0);

        leftHandFreeOffsetRotation = new GameObject(nameof(leftHandFreeOffsetRotation)).transform;
        rightHandFreeOffsetRotation = new GameObject(nameof(rightHandFreeOffsetRotation)).transform;
        leftHandFreeOffsetRotation.SetParent(leftHandTracker);
        rightHandFreeOffsetRotation.SetParent(rightHandTracker);
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

        SetHandFreeOffset();

        calibrationState = CalibrationState.Calibrating; //キャリブレーション状態を"キャリブレーション中"に設定(ここまで来なければ失敗している)
    }

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
        RealTrackerRoot.gameObject.SetActive(false);

        if (CalibrationCamera != null)
        {
            CalibrationCamera.gameObject.SetActive(false);
        }
        SetHandFreeOffset();
        SetCameraLookTarget();
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
        }
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

    private bool lastHideWindowBorder = false;
    void HideWindowBorder(bool enable)
    {
        if (lastHideWindowBorder == enable) return;
        lastHideWindowBorder = enable;
        CurrentSettings.HideBorder = enable;
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
        CurrentSettings.IsTopMost = enable;
#if !UNITY_EDITOR   // エディタ上では動きません。
        SetUnityWindowTopMost(enable);
#endif
    }

    void SetWindowClickThrough(bool enable)
    {
        CurrentSettings.WindowClickThrough = enable;
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


    public void ChangeCamera(CameraTypes type)
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

    private void SetCameraEnable(CameraMouseControl camera)
    {
        if (camera != null)
        {
            var virtualCam = ControlCamera.GetComponent<VirtualCamera>();
            if (virtualCam != null)
            {
                virtualCam.enabled = CurrentSettings.WebCamEnabled;
            }
            camera.gameObject.SetActive(true);
            if (CurrentCameraControl != null && CurrentCameraControl != camera) CurrentCameraControl.gameObject.SetActive(false);
            camera.GetComponent<CameraMouseControl>().enabled = true;
            CurrentCameraControl = camera;
            SetCameraMirrorEnable(CurrentSettings.CameraMirrorEnable);

            CameraChangedAction?.Invoke(ControlCamera);
        }
    }

    private void SetCameraMirrorEnable(bool mirrorEnable)
    {
        var mirror = ControlCamera.GetComponent<CameraMirror>();
        if (mirror != null) mirror.MirrorEnable = mirrorEnable;
    }

    private Camera saveCurrentCamera = null;

    private void StartHandCamera(bool isLeft)
    {
        RightHandCamera.SetActive(isLeft == false);
        LeftHandCamera.SetActive(isLeft == true);
        saveCurrentCamera = ControlCamera;
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
        CurrentCameraControl.transform.SetParent(handler.CameraControllerObject.transform);
        CurrentCameraControl.transform.localPosition = new Vector3(d.x, d.y, d.z);
        CurrentCameraControl.transform.localRotation = Quaternion.Euler(d.rx, d.ry, d.rz);
        ControlCamera.fieldOfView = d.fov;
        //コントローラーは動くのでカメラ位置の保存はできない
        //if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(currentCamera.transform);
        //CurrentSettings.FreeCameraTransform.SetPosition(currentCamera.transform);
    }

    private async void ImportCameraPlus(PipeCommands.ImportCameraPlus d)
    {
        ChangeCamera(CameraTypes.Free);
        CurrentSettings.FreeCameraTransform.localPosition = new Vector3(d.x, d.y, d.z);
        CurrentSettings.FreeCameraTransform.localRotation = Quaternion.Euler(d.rx, d.ry, d.rz);
        ControlCamera.fieldOfView = d.fov;
        CurrentSettings.FreeCameraTransform.ToLocalTransform(FreeCamera.transform);
        var control = FreeCamera.GetComponent<CameraMouseControl>();
        control.CameraAngle = -FreeCamera.transform.rotation.eulerAngles;
        control.CameraDistance = Vector3.Distance(FreeCamera.transform.localPosition, Vector3.zero);
        control.CameraTarget = FreeCamera.transform.localPosition + FreeCamera.transform.rotation * Vector3.forward * control.CameraDistance;
        await server.SendCommandAsync(new PipeCommands.LoadCameraFOV { fov = d.fov });
    }
    
    private void SetCameraFOV(float fov)
    {
        CurrentSettings.CameraFOV = fov;
        FrontCamera.SetCameraFOV(fov);
        BackCamera.SetCameraFOV(fov);
        FreeCamera.SetCameraFOV(fov);
        PositionFixedCamera.SetCameraFOV(fov);
        ControlCamera.fieldOfView = fov;
    }

    private void SetCameraSmooth(float speed)
    {
        CurrentSettings.CameraSmooth = speed;
        ControlCamera.GetComponent<CameraFollower>().Speed = speed;
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
        config.keyCode = -2;
        config.keyName = e.Name;
        config.isLeft = e.IsLeft;
        bool isStick = e.Name.Contains("Stick");
        config.keyIndex = e.IsAxis == false ? -1 : NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y, isStick);
        config.isTouch = e.IsTouch;
        if (e.IsAxis)
        {
            if (config.keyIndex < 0) return;
            if (e.IsLeft)
            {
                if (isStick) lastStickLeftAxisPoint = config.keyIndex;
                else lastTouchpadLeftAxisPoint = config.keyIndex;
            }
            else
            {
                if (isStick) lastStickRightAxisPoint = config.keyIndex;
                else lastTouchpadRightAxisPoint = config.keyIndex;
            }
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
        config.keyCode = -2;
        config.keyName = e.Name;
        config.isLeft = e.IsLeft;
        bool isStick = e.Name.Contains("Stick");
        config.keyIndex = e.IsAxis == false ? -1 : NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y, isStick);
        config.isTouch = e.IsTouch;
        if (e.IsAxis && config.keyIndex != (isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint)))
        {//タッチパッド離した瞬間違うポイントだった場合
            var newindex = config.keyIndex;
            config.keyIndex = (isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint));
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

    private int lastTouchpadLeftAxisPoint = -1;
    private int lastTouchpadRightAxisPoint = -1;
    private int lastStickLeftAxisPoint = -1;
    private int lastStickRightAxisPoint = -1;

    private bool isSendingKey = false;
    //タッチパッドやアナログスティックの変動
    private async void ControllerAction_AxisChanged(object sender, OVRKeyEventArgs e)
    {
        if (e.IsAxis == false) return;
        var keyName = e.Name;
        if (keyName.Contains("Trigger")) return; //トリガーは現時点ではアナログ入力無効
        if (keyName.Contains("Position")) keyName = keyName.Replace("Position", "Touch"); //ポジションはいったんタッチと同じにする
        bool isStick = keyName.Contains("Stick");
        var newindex = NearestPointIndex(e.IsLeft, e.Axis.x, e.Axis.y, isStick);
        if ((isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint)) != newindex)
        {//ドラッグで隣の領域に入った場合
            var config = new KeyConfig();
            config.type = KeyTypes.Controller;
            config.actionType = KeyActionTypes.Hand;
            config.keyCode = -2;
            config.keyName = keyName;
            config.isLeft = e.IsLeft;
            config.keyIndex = (isStick ? (e.IsLeft ? lastStickLeftAxisPoint : lastStickRightAxisPoint) : (e.IsLeft ? lastTouchpadLeftAxisPoint : lastTouchpadRightAxisPoint));
            config.isTouch = true;// e.IsTouch; //ポジションはいったんタッチと同じにする
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
            if (e.IsLeft)
            {
                if (isStick) lastStickLeftAxisPoint = newindex;
                else lastTouchpadLeftAxisPoint = newindex;
            }
            else
            {
                if (isStick) lastStickRightAxisPoint = newindex;
                else lastTouchpadRightAxisPoint = newindex;
            }
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

    private int NearestPointIndex(bool isLeft, float x, float y, bool isStick)
    {
        //Debug.Log($"SearchNearestPoint:{x},{y},{isLeft}");
        int index = 0;
        var points = isStick ? (isLeft ? CurrentSettings.LeftThumbStickPoints : CurrentSettings.RightThumbStickPoints) : (isLeft ? CurrentSettings.LeftTouchPadPoints : CurrentSettings.RightTouchPadPoints);
        if (points == null) return 0; //未設定時は一つ
        var centerEnable = isLeft ? CurrentSettings.LeftCenterEnable : CurrentSettings.RightCenterEnable;
        if (centerEnable || isStick) //センターキー有効時(タッチパッド) / スティックの場合はセンター無効にする
        {
            var point_distance = x * x + y * y;
            var r = 2.0f / 5.0f; //半径
            var r2 = r * r;
            if (point_distance < r2) //円内
            {
                if (isStick) return -1;
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
            externalMotionReceiver.DisableBlendShapeReception = action.DisableBlendShapeReception;
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
        CurrentSettings.EyeTracking_TobiiOffsetHorizontal = offsets.OffsetHorizontal;
        CurrentSettings.EyeTracking_TobiiOffsetVertical = offsets.OffsetVertical;
        CurrentSettings.EyeTracking_TobiiScaleHorizontal = offsets.ScaleHorizontal;
        CurrentSettings.EyeTracking_TobiiScaleVertical = offsets.ScaleVertical;
        SetEyeTracking_TobiiOffsetsAction?.Invoke(offsets);
    }

    public void SetEyeTracking_TobiiPosition(Transform position, float centerX, float centerY)
    {
        CurrentSettings.EyeTracking_TobiiPosition = StoreTransform.Create(position);
        CurrentSettings.EyeTracking_TobiiCenterX = centerX;
        CurrentSettings.EyeTracking_TobiiCenterY = centerY;
    }

    public Vector2 GetEyeTracking_TobiiLocalPosition(Transform saveto)
    {
        if (CurrentSettings.EyeTracking_TobiiPosition != null) CurrentSettings.EyeTracking_TobiiPosition.ToLocalTransform(saveto);
        return new Vector2(CurrentSettings.EyeTracking_TobiiCenterX, CurrentSettings.EyeTracking_TobiiCenterY);
    }
    private void SetEyeTracking_ViveProEyeOffsets(PipeCommands.SetEyeTracking_ViveProEyeOffsets offsets)
    {
        CurrentSettings.EyeTracking_ViveProEyeOffsetHorizontal = offsets.OffsetHorizontal;
        CurrentSettings.EyeTracking_ViveProEyeOffsetVertical = offsets.OffsetVertical;
        CurrentSettings.EyeTracking_ViveProEyeScaleHorizontal = offsets.ScaleHorizontal;
        CurrentSettings.EyeTracking_ViveProEyeScaleVertical = offsets.ScaleVertical;
        SetEyeTracking_ViveProEyeOffsetsAction?.Invoke(offsets);
    }
    private void SetEyeTracking_ViveProEyeUseEyelidMovements(PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements useEyelidMovements)
    {
        CurrentSettings.EyeTracking_ViveProEyeUseEyelidMovements = useEyelidMovements.Use;
        SetEyeTracking_ViveProEyeUseEyelidMovementsAction?.Invoke(useEyelidMovements);
    }


    #endregion

    #region ExternalMotionSender

    private void SetExternalMotionSenderEnable(bool enable)
    {
        if (IsPreRelease == false) return;
        CurrentSettings.ExternalMotionSenderEnable = enable;
        ExternalMotionSenderObject.SetActive(enable);
        WaitOneFrameAction(() => ModelLoadedAction?.Invoke(CurrentModel));
        WaitOneFrameAction(() => CameraChangedAction?.Invoke(ControlCamera));
    }

    private void SetExternalMotionReceiverEnable(bool enable)
    {
        CurrentSettings.ExternalMotionReceiverEnable = enable;
        externalMotionReceiver.SetObjectActive(enable);
        WaitOneFrameAction(() => ModelLoadedAction?.Invoke(CurrentModel));
        WaitOneFrameAction(() => CameraChangedAction?.Invoke(ControlCamera));
    }

    private void SetExternalBonesReceiverEnable(bool enable)
    {
        CurrentSettings.ExternalBonesReceiverEnable = enable;
        externalMotionReceiver.receiveBonesFlag = enable;
    }

    private void ChangeExternalMotionSenderAddress(string address, int port, int pstatus, int proot, int pbone, int pblendshape, int pcamera, int pdevices, string optionstring, bool responderEnable)
    {
        CurrentSettings.ExternalMotionSenderAddress = address;
        CurrentSettings.ExternalMotionSenderPort = port;
        CurrentSettings.ExternalMotionSenderPeriodStatus = pstatus;
        CurrentSettings.ExternalMotionSenderPeriodRoot = proot;
        CurrentSettings.ExternalMotionSenderPeriodBone = pbone;
        CurrentSettings.ExternalMotionSenderPeriodBlendShape = pblendshape;
        CurrentSettings.ExternalMotionSenderPeriodCamera = pcamera;
        CurrentSettings.ExternalMotionSenderPeriodDevices = pdevices;
        CurrentSettings.ExternalMotionSenderOptionString = optionstring;
        CurrentSettings.ExternalMotionSenderResponderEnable = responderEnable;

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
        CurrentSettings.ExternalMotionSenderAddress = address;
        CurrentSettings.ExternalMotionSenderPort = port;

        externalMotionSender.ChangeOSCAddress(address, port);
    }

    private void ChangeExternalMotionReceiverPort(int port, bool requesterEnable)
    {
        CurrentSettings.ExternalMotionReceiverPort = port;
        externalMotionReceiver.ChangeOSCPort(port);

        CurrentSettings.ExternalMotionReceiverRequesterEnable = requesterEnable;
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
        CurrentSettings.TrackingFilterEnable = global;
        CurrentSettings.TrackingFilterHmdEnable = hmd;
        CurrentSettings.TrackingFilterControllerEnable = controller;
        CurrentSettings.TrackingFilterTrackerEnable = tracker;
    }

    private void SetModelModifierEnable(bool fixKneeRotation)
    {
        CurrentSettings.FixKneeRotation = fixKneeRotation;
    }

    private void SetHandleControllerAsTracker(bool handleCasT)
    {
        CurrentSettings.HandleControllerAsTracker = handleCasT;
    }

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
        [OptionalField]
        public string AAA_0 = null;
        [OptionalField]
        public string AAA_1 = null;
        [OptionalField]
        public string AAA_2 = null;
        [OptionalField]
        public string AAA_3 = null;
        [OptionalField]
        public string AAA_SavedVersion = null;
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
        public List<UPoint> LeftThumbStickPoints;
        [OptionalField]
        public List<UPoint> RightThumbStickPoints;
        [OptionalField]
        public List<KeyAction> KeyActions = null;
        [OptionalField]
        public float LeftHandRotation = 0; //unused
        [OptionalField]
        public float RightHandRotation = 0; //unused
        [OptionalField]
        public float LeftHandPositionX;
        [OptionalField]
        public float LeftHandPositionY;
        [OptionalField]
        public float LeftHandPositionZ;
        [OptionalField]
        public float LeftHandRotationX;
        [OptionalField]
        public float LeftHandRotationY;
        [OptionalField]
        public float LeftHandRotationZ;
        [OptionalField]
        public float RightHandPositionX;
        [OptionalField]
        public float RightHandPositionY;
        [OptionalField]
        public float RightHandPositionZ;
        [OptionalField]
        public float RightHandRotationX;
        [OptionalField]
        public float RightHandRotationY;
        [OptionalField]
        public float RightHandRotationZ;
        [OptionalField]
        public int SwivelOffset;

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
        public float CameraSmooth = 0.0f;

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

        //EyeTracking
        [OptionalField]
        public float EyeTracking_TobiiScaleHorizontal;
        [OptionalField]
        public float EyeTracking_TobiiScaleVertical;
        [OptionalField]
        public float EyeTracking_TobiiOffsetHorizontal;
        [OptionalField]
        public float EyeTracking_TobiiOffsetVertical;
        [OptionalField]
        public StoreTransform EyeTracking_TobiiPosition;
        [OptionalField]
        public float EyeTracking_TobiiCenterX;
        [OptionalField]
        public float EyeTracking_TobiiCenterY;
        [OptionalField]
        public float EyeTracking_ViveProEyeScaleHorizontal;
        [OptionalField]
        public float EyeTracking_ViveProEyeScaleVertical;
        [OptionalField]
        public float EyeTracking_ViveProEyeOffsetHorizontal;
        [OptionalField]
        public float EyeTracking_ViveProEyeOffsetVertical;
        [OptionalField]
        public bool EyeTracking_ViveProEyeUseEyelidMovements;
        [OptionalField]
        public bool EyeTracking_ViveProEyeEnable;

        //ExternalMotionSender
        [OptionalField]
        public bool ExternalMotionSenderEnable;
        [OptionalField]
        public string ExternalMotionSenderAddress;
        [OptionalField]
        public int ExternalMotionSenderPort;
        [OptionalField]
        public int ExternalMotionSenderPeriodStatus;
        [OptionalField]
        public int ExternalMotionSenderPeriodRoot;
        [OptionalField]
        public int ExternalMotionSenderPeriodBone;
        [OptionalField]
        public int ExternalMotionSenderPeriodBlendShape;
        [OptionalField]
        public int ExternalMotionSenderPeriodCamera;
        [OptionalField]
        public int ExternalMotionSenderPeriodDevices;
        [OptionalField]
        public bool ExternalMotionSenderResponderEnable;
        [OptionalField]
        public bool ExternalMotionReceiverEnable;
        [OptionalField]
        public int ExternalMotionReceiverPort;
        [OptionalField]
        public bool ExternalMotionReceiverRequesterEnable;
        [OptionalField]
        public string ExternalMotionSenderOptionString;
        [OptionalField]
        public List<string> MidiCCBlendShape;
        [OptionalField]
        public bool MidiEnable;
        [OptionalField]
        public Dictionary<string,string> LipShapesToBlendShapeMap;
        [OptionalField]
        public bool LipTracking_ViveEnable;

        [OptionalField]
        public bool ExternalBonesReceiverEnable;

        [OptionalField]
        public bool EnableSkeletal;

        [OptionalField]
        public bool TrackingFilterEnable;
        [OptionalField]
        public bool TrackingFilterHmdEnable;
        [OptionalField]
        public bool TrackingFilterControllerEnable;
        [OptionalField]
        public bool TrackingFilterTrackerEnable;

        [OptionalField]
        public bool FixKneeRotation;

        [OptionalField]
        public bool HandleControllerAsTracker;

        [OptionalField]
        public int AntiAliasing;

        [OptionalField]
        public bool VirtualMotionTrackerEnable;
        [OptionalField]
        public int VirtualMotionTrackerNo;


        [OptionalField]
        public bool PPS_Enable;
        [OptionalField]
        public bool PPS_Bloom_Enable;
        [OptionalField]
        public float PPS_Bloom_Intensity;
        [OptionalField]
        public float PPS_Bloom_Threshold;

        [OptionalField]
        public bool PPS_DoF_Enable;
        [OptionalField]
        public float PPS_DoF_FocusDistance;
        [OptionalField]
        public float PPS_DoF_Aperture;
        [OptionalField]
        public float PPS_DoF_FocusLength;
        [OptionalField]
        public int PPS_DoF_MaxBlurSize;

        [OptionalField]
        public bool PPS_CG_Enable;
        [OptionalField]
        public float PPS_CG_Temperature;
        [OptionalField]
        public float PPS_CG_Saturation;
        [OptionalField]
        public float PPS_CG_Contrast;
        [OptionalField]
        public float PPS_CG_Gamma;

        [OptionalField]
        public bool PPS_Vignette_Enable;
        [OptionalField]
        public float PPS_Vignette_Intensity;
        [OptionalField]
        public float PPS_Vignette_Smoothness;
        [OptionalField]
        public float PPS_Vignette_Roundness;

        [OptionalField]
        public bool PPS_CA_Enable;
        [OptionalField]
        public float PPS_CA_Intensity;
        [OptionalField]
        public bool PPS_CA_FastMode;

        [OptionalField]
        public float PPS_Bloom_Color_a;
        [OptionalField]
        public float PPS_Bloom_Color_r;
        [OptionalField]
        public float PPS_Bloom_Color_g;
        [OptionalField]
        public float PPS_Bloom_Color_b;

        [OptionalField]
        public float PPS_CG_ColorFilter_a;
        [OptionalField]
        public float PPS_CG_ColorFilter_r;
        [OptionalField]
        public float PPS_CG_ColorFilter_g;
        [OptionalField]
        public float PPS_CG_ColorFilter_b;

        [OptionalField]
        public float PPS_Vignette_Color_a;
        [OptionalField]
        public float PPS_Vignette_Color_r;
        [OptionalField]
        public float PPS_Vignette_Color_g;
        [OptionalField]
        public float PPS_Vignette_Color_b;

        [OptionalField]
        public bool TurnOffAmbientLight;

        //初期値
        [OnDeserializing()]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            AAA_0 = "========================================";
            AAA_1 = " Virtual Motion Capture Setting File";
            AAA_2 = " See more : vmc.info";
            AAA_3 = "========================================";

            AAA_SavedVersion = null;

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
            CameraSmooth = 0f;

            LightColor = Color.white;
            LightRotationX = 130;
            LightRotationY = 43;

            ScreenWidth = 0;
            ScreenHeight = 0;
            ScreenRefreshRate = 0;

            EyeTracking_TobiiScaleHorizontal = 0.5f;
            EyeTracking_TobiiScaleVertical = 0.2f;
            EyeTracking_ViveProEyeScaleHorizontal = 2.0f;
            EyeTracking_ViveProEyeScaleVertical = 1.5f;
            EyeTracking_ViveProEyeUseEyelidMovements = false;
            EyeTracking_ViveProEyeEnable = false;

            EnableSkeletal = true;

            ExternalMotionSenderEnable = false;
            ExternalMotionSenderAddress = "127.0.0.1";
            ExternalMotionSenderPort = 39539;
            ExternalMotionSenderPeriodStatus = 1;
            ExternalMotionSenderPeriodRoot = 1;
            ExternalMotionSenderPeriodBone = 1;
            ExternalMotionSenderPeriodBlendShape = 1;
            ExternalMotionSenderPeriodCamera = 1;
            ExternalMotionSenderPeriodDevices = 1;
            ExternalMotionSenderOptionString = "";
            ExternalMotionSenderResponderEnable = false;

            ExternalMotionReceiverEnable = false;
            ExternalMotionReceiverPort = 39540;
            ExternalMotionReceiverRequesterEnable = true;

            MidiCCBlendShape = new List<string>(Enumerable.Repeat(default(string), MidiCCWrapper.KNOBS));
            MidiEnable = false;

            LipShapesToBlendShapeMap = new Dictionary<string, string>();
            LipTracking_ViveEnable = false;

            TrackingFilterEnable = true;
            TrackingFilterHmdEnable = true;
            TrackingFilterControllerEnable = true;
            TrackingFilterTrackerEnable = true;

            FixKneeRotation = true;

            HandleControllerAsTracker = false;

            AntiAliasing = 2;

            VirtualMotionTrackerEnable = false;
            VirtualMotionTrackerNo = 50;

            PPS_Enable = false;
            PPS_Bloom_Enable = false;
            PPS_Bloom_Intensity = 2.7f;
            PPS_Bloom_Threshold = 0.5f;

            PPS_DoF_Enable = false;
            PPS_DoF_FocusDistance = 1.65f;
            PPS_DoF_Aperture = 16f;
            PPS_DoF_FocusLength = 16.4f;
            PPS_DoF_MaxBlurSize = 3;

            PPS_CG_Enable = false;
            PPS_CG_Temperature = 0f;
            PPS_CG_Saturation = 0f;
            PPS_CG_Contrast = 0f;
            PPS_CG_Gamma = 0f;

            PPS_Vignette_Enable = false;
            PPS_Vignette_Intensity = 0.65f;
            PPS_Vignette_Smoothness = 0.35f;
            PPS_Vignette_Roundness = 1f;

            PPS_CA_Enable = false;
            PPS_CA_Intensity = 1f;
            PPS_CA_FastMode = false;

            PPS_Bloom_Color_a = 1f;
            PPS_Bloom_Color_r = 1f;
            PPS_Bloom_Color_g = 1f;
            PPS_Bloom_Color_b = 1f;

            PPS_CG_ColorFilter_a = 1f;
            PPS_CG_ColorFilter_r = 1f;
            PPS_CG_ColorFilter_g = 1f;
            PPS_CG_ColorFilter_b = 1f;

            PPS_Vignette_Color_a = 1f;
            PPS_Vignette_Color_r = 0f;
            PPS_Vignette_Color_g = 0f;
            PPS_Vignette_Color_b = 0f;

            TurnOffAmbientLight = false;
            ExternalBonesReceiverEnable = false;
        }
    }

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

    public static Settings CurrentSettings = new Settings();
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

        if (notifyLogLevel <  notifyType)
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
            TrackerTransformExtensions.TrackerMovedEvent += TransformExtensions_TrackerMovedEvent;
            ExternalReceiverForVMC.StatusStringUpdated += StatusStringUpdatedEvent;

            //エラー情報をWPFに飛ばす
            Application.logMessageReceived += LogMessageHandler;
        }
    }

    private void SaveSettings(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        CurrentSettings.AAA_SavedVersion = baseVersionString;

        File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(CurrentSettings)));

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
            CurrentSettings = Json.Serializer.Deserialize<Settings>(File.ReadAllText(path)); //設定を読み込み
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
        ApplyCurrentSettings();

        //有効なJSONが取得できたかチェック
        if (CurrentSettings != null)
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
        LoadedConfigPathChangedAction?.Invoke();
    }

    private void ResetTrackerScale()
    {
        //jsonが正しくデコードできていなければ無視する
        if (CurrentSettings == null)
        {
            return;
        }

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

    //CurrentSettingsを各種設定に適用
    private async void ApplyCurrentSettings()
    {
        //VRMのパスが有効で、存在するなら読み込む
        if (string.IsNullOrWhiteSpace(CurrentSettings.VRMPath) == false
            && File.Exists(CurrentSettings.VRMPath))
        {
            await server.SendCommandAsync(new PipeCommands.LoadVRMPath { Path = CurrentSettings.VRMPath });
            await ImportVRM(CurrentSettings.VRMPath, false, CurrentSettings.EnableNormalMapFix, CurrentSettings.DeleteHairNormalMap);

            //メタ情報をOSC送信する
            VRMmetaLodedAction?.Invoke(LoadVRM(CurrentSettings.VRMPath));
        }

        //SetResolutionは強制的にウインドウ枠を復活させるのでBorder設定の前にやっておく必要がある
        if (Screen.resolutions.Any(d => d.width == CurrentSettings.ScreenWidth && d.height == CurrentSettings.ScreenHeight && d.refreshRate == CurrentSettings.ScreenRefreshRate))
        {
            UpdateActionQueue.Enqueue(() => Screen.SetResolution(CurrentSettings.ScreenWidth, CurrentSettings.ScreenHeight, false, CurrentSettings.ScreenRefreshRate));
        }

        if (CurrentSettings.BackgroundColor != null)
        {
            UpdateActionQueue.Enqueue(() => ChangeBackgroundColor(CurrentSettings.BackgroundColor.r, CurrentSettings.BackgroundColor.g, CurrentSettings.BackgroundColor.b, false));
        }

        if (CurrentSettings.CustomBackgroundColor != null)
        {
            await server.SendCommandAsync(new PipeCommands.LoadCustomBackgroundColor { r = CurrentSettings.CustomBackgroundColor.r, g = CurrentSettings.CustomBackgroundColor.g, b = CurrentSettings.CustomBackgroundColor.b });
        }

        if (CurrentSettings.IsTransparent)
        {
            UpdateActionQueue.Enqueue(() => SetBackgroundTransparent());
        }

        UpdateActionQueue.Enqueue(() => HideWindowBorder(CurrentSettings.HideBorder));
        await server.SendCommandAsync(new PipeCommands.LoadHideBorder { enable = CurrentSettings.HideBorder });

        UpdateActionQueue.Enqueue(() => SetWindowTopMost(CurrentSettings.IsTopMost));
        await server.SendCommandAsync(new PipeCommands.LoadIsTopMost { enable = CurrentSettings.IsTopMost });

        SetCameraFOV(CurrentSettings.CameraFOV);
        SetCameraSmooth(CurrentSettings.CameraSmooth);
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
        await server.SendCommandAsync(new PipeCommands.LoadCameraSmooth { speed = CurrentSettings.CameraSmooth });

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
        await server.SendCommandAsync(new PipeCommands.LoadControllerStickPoints
        {
            LeftPoints = CurrentSettings.LeftThumbStickPoints,
            RightPoints = CurrentSettings.RightThumbStickPoints,
        });

        KeyAction.KeyActionsUpgrade(CurrentSettings.KeyActions);

        if (string.IsNullOrWhiteSpace(CurrentSettings.AAA_SavedVersion))
        {
            //before 0.47 _SaveVersion is null.

            //v0.48 BlendShapeKey case sensitive.
            foreach (var keyAction in CurrentSettings.KeyActions)
            {
                if (keyAction.FaceNames != null && keyAction.FaceNames.Count > 0)
                {
                    keyAction.FaceNames = keyAction.FaceNames.Select(d => faceController.GetCaseSensitiveKeyName(d)).ToList();
                }
            }
        }

        steamVR2Input.EnableSkeletal = CurrentSettings.EnableSkeletal;

        await server.SendCommandAsync(new PipeCommands.LoadSkeletalInputEnable { enable = CurrentSettings.EnableSkeletal });

        await server.SendCommandAsync(new PipeCommands.LoadKeyActions { KeyActions = CurrentSettings.KeyActions });
        await server.SendCommandAsync(new PipeCommands.SetHandFreeOffset
        {
            LeftHandPositionX = (int)Mathf.Round(CurrentSettings.LeftHandPositionX * 1000),
            LeftHandPositionY = (int)Mathf.Round(CurrentSettings.LeftHandPositionY * 1000),
            LeftHandPositionZ = (int)Mathf.Round(CurrentSettings.LeftHandPositionZ * 1000),
            LeftHandRotationX = (int)CurrentSettings.LeftHandRotationX,
            LeftHandRotationY = (int)CurrentSettings.LeftHandRotationY,
            LeftHandRotationZ = (int)CurrentSettings.LeftHandRotationZ,
            RightHandPositionX = (int)Mathf.Round(CurrentSettings.RightHandPositionX * 1000),
            RightHandPositionY = (int)Mathf.Round(CurrentSettings.RightHandPositionY * 1000),
            RightHandPositionZ = (int)Mathf.Round(CurrentSettings.RightHandPositionZ * 1000),
            RightHandRotationX = (int)CurrentSettings.RightHandRotationX,
            RightHandRotationY = (int)CurrentSettings.RightHandRotationY,
            RightHandRotationZ = (int)CurrentSettings.RightHandRotationZ,
            SwivelOffset = CurrentSettings.SwivelOffset,
        });
        SetHandFreeOffset();

        await server.SendCommandAsync(new PipeCommands.LoadLipSyncEnable { enable = CurrentSettings.LipSyncEnable });
        SetLipSyncEnable(CurrentSettings.LipSyncEnable);

        await server.SendCommandAsync(new PipeCommands.SetLightAngle { X = CurrentSettings.LightRotationX, Y = CurrentSettings.LightRotationY });
        SetLightAngle(CurrentSettings.LightRotationX, CurrentSettings.LightRotationY);
        await server.SendCommandAsync(new PipeCommands.ChangeLightColor { a = CurrentSettings.LightColor.a, r = CurrentSettings.LightColor.r, g = CurrentSettings.LightColor.g, b = CurrentSettings.LightColor.b });
        ChangeLightColor(CurrentSettings.LightColor.a, CurrentSettings.LightColor.r, CurrentSettings.LightColor.g, CurrentSettings.LightColor.b);

        SetExternalMotionSenderEnable(CurrentSettings.ExternalMotionSenderEnable);
        ChangeExternalMotionSenderAddress(CurrentSettings.ExternalMotionSenderAddress, CurrentSettings.ExternalMotionSenderPort, CurrentSettings.ExternalMotionSenderPeriodStatus, CurrentSettings.ExternalMotionSenderPeriodRoot, CurrentSettings.ExternalMotionSenderPeriodBone, CurrentSettings.ExternalMotionSenderPeriodBlendShape, CurrentSettings.ExternalMotionSenderPeriodCamera, CurrentSettings.ExternalMotionSenderPeriodDevices, CurrentSettings.ExternalMotionSenderOptionString, CurrentSettings.ExternalMotionSenderResponderEnable);
        
        ChangeExternalMotionReceiverPort(CurrentSettings.ExternalMotionReceiverPort, CurrentSettings.ExternalMotionReceiverRequesterEnable);
        SetExternalMotionReceiverEnable(CurrentSettings.ExternalMotionReceiverEnable);

        SetMidiCCBlendShape(CurrentSettings.MidiCCBlendShape);
        SetMidiEnable(CurrentSettings.MidiEnable);

        SetEyeTracking_TobiiOffsetsAction?.Invoke(new PipeCommands.SetEyeTracking_TobiiOffsets
        {
            OffsetHorizontal = CurrentSettings.EyeTracking_TobiiOffsetHorizontal,
            OffsetVertical = CurrentSettings.EyeTracking_TobiiOffsetVertical,
            ScaleHorizontal = CurrentSettings.EyeTracking_TobiiScaleHorizontal,
            ScaleVertical = CurrentSettings.EyeTracking_TobiiScaleVertical
        });

        SetEyeTracking_ViveProEyeOffsetsAction?.Invoke(new PipeCommands.SetEyeTracking_ViveProEyeOffsets
        {
            OffsetHorizontal = CurrentSettings.EyeTracking_ViveProEyeOffsetHorizontal,
            OffsetVertical = CurrentSettings.EyeTracking_ViveProEyeOffsetVertical,
            ScaleHorizontal = CurrentSettings.EyeTracking_ViveProEyeScaleHorizontal,
            ScaleVertical = CurrentSettings.EyeTracking_ViveProEyeScaleVertical
        });

        SetEyeTracking_ViveProEyeUseEyelidMovementsAction?.Invoke(new PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements
        {
            Use = CurrentSettings.EyeTracking_ViveProEyeUseEyelidMovements
        });
        SetEyeTracking_ViveProEyeEnable(CurrentSettings.EyeTracking_ViveProEyeEnable);

        SetTrackingFilterEnable(CurrentSettings.TrackingFilterEnable, CurrentSettings.TrackingFilterHmdEnable, CurrentSettings.TrackingFilterControllerEnable, CurrentSettings.TrackingFilterTrackerEnable);

        SetModelModifierEnable(CurrentSettings.FixKneeRotation);
        SetHandleControllerAsTracker(CurrentSettings.HandleControllerAsTracker);
        SetQualitySettings(new PipeCommands.SetQualitySettings
        {
            antiAliasing = CurrentSettings.AntiAliasing,
        });
        SetVMT(CurrentSettings.VirtualMotionTrackerEnable, CurrentSettings.VirtualMotionTrackerNo);

        SetLipShapeToBlendShapeStringMapAction?.Invoke(CurrentSettings.LipShapesToBlendShapeMap);
        SetLipTracking_ViveEnable(CurrentSettings.LipTracking_ViveEnable);

        SetExternalBonesReceiverEnable(CurrentSettings.ExternalBonesReceiverEnable);

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
        CurrentSettings.MidiCCBlendShape = blendshapes;
        midiCCBlendShape.KnobToBlendShape = blendshapes.ToArray();
    }

    private void SetMidiEnable(bool enable)
    {
        CurrentSettings.MidiEnable = enable;
        midiCCWrapper.gameObject.SetActive(enable);
    }

    private void UpdateWebCamConfig()
    {
        SetCameraEnable(CurrentCameraControl);
        VirtualCamera.Buffering_Global = CurrentSettings.WebCamBuffering;
        VirtualCamera.MirrorMode_Global = CurrentSettings.WebCamMirroring ? VirtualCamera.EMirrorMode.MirrorHorizontally : VirtualCamera.EMirrorMode.Disabled;
        VirtualCamera.ResizeMode_Global = CurrentSettings.WebCamResize ? VirtualCamera.EResizeMode.LinearResize : VirtualCamera.EResizeMode.Disabled;
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
            CurrentSettings.LeftHandRotationX,
            -CurrentSettings.LeftHandRotationY,
            CurrentSettings.LeftHandRotationZ
        );
        leftHandFreeOffsetPosition.localPosition = new Vector3(
            -CurrentSettings.LeftHandPositionX,
            CurrentSettings.LeftHandPositionY,
            CurrentSettings.LeftHandPositionZ
        );

        rightHandFreeOffsetRotation.localRotation = Quaternion.Euler(
            CurrentSettings.RightHandRotationX,
            CurrentSettings.RightHandRotationY,
            CurrentSettings.RightHandRotationZ
        );
        rightHandFreeOffsetPosition.localPosition = new Vector3(
            CurrentSettings.RightHandPositionX,
            CurrentSettings.RightHandPositionY,
            CurrentSettings.RightHandPositionZ
        );

        vrik.solver.leftArm.swivelOffset = CurrentSettings.SwivelOffset;
        vrik.solver.rightArm.swivelOffset = -CurrentSettings.SwivelOffset;
    }

    private void Awake()
    {
        baseVersionString = VersionString.Split('f').First();
        defaultWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_STYLE);
        defaultExWindowStyle = GetWindowLong(GetUnityWindowHandle(), GWL_EXSTYLE);
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
        StartCoroutine(Photo.TakePNGPhoto(ControlCamera, res, transparentBackground, bytes =>
        {
            File.WriteAllBytes(Path.Combine(directory, filename), bytes);
            if (transparentBackground) BackgroundRenderer.gameObject.SetActive(true);
        }));
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
