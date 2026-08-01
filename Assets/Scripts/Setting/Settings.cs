using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using UnityMemoryMappedFile;
using Valve.VR;

namespace VMC
{
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

    /// <summary>
    /// キャリブレーション実行時の1つのトラッカーの姿勢(トラッキング機器から報告される生のローカル姿勢)
    /// </summary>
    [Serializable]
    public class CalibrationTrackerPose
    {
        public string Name;      //シリアル番号等のデバイス名(TrackingPointの識別子)
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// キャリブレーション実行時のトラッカー姿勢一式。
    /// 別のアバターを読み込んだ時にこの姿勢を再現してキャリブレーションを再実行することで、
    /// 再度Tポーズを取らなくても同じ基準でキャリブレーションできる。
    /// </summary>
    [Serializable]
    public class CalibrationSnapshot
    {
        public int CalibrateType;  //PipeCommands.CalibrateType (未知の値でも壊れないようint)
        public List<CalibrationTrackerPose> Poses = new List<CalibrationTrackerPose>();
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
    public class VMCProtocolReceiverSettings
    {
        public bool Enable = false;
        public int Port = 0;
        public int DelayMs = 0;

        public string Name = "Receiver";

        public bool ApplyRootRotation = true;
        public bool ApplyRootPosition = true;
        public bool ApplySpine = true;
        public bool ApplyChest = true;
        public bool ApplyHead = true;
        public bool ApplyLeftArm = true;
        public bool ApplyRightArm = true;
        public bool ApplyLeftHand = true;
        public bool ApplyRightHand = true;
        public bool ApplyLeftLeg = true;
        public bool ApplyRightLeg = true;
        public bool ApplyLeftFoot = true;
        public bool ApplyRightFoot = true;
        public bool ApplyEye = false;
        public bool ApplyLeftFinger = true;
        public bool ApplyRightFinger = true;

        public bool FixHandBone = true;
        public bool UseBonePosition = false;

        /// <summary>
        /// 送信元が正規化(ControlRig)ボーン姿勢を送ってくる場合にtrueにする。
        /// 仕様の推奨はオリジナル(非正規化)ボーンなので既定はfalse。
        /// </summary>
        [OptionalField]
        public bool UseNormalizedBone = false;
        [OptionalField]
        public bool CorrectHipBone = false;
        [OptionalField]
        public bool IgnoreDefaultBone = true;

        public bool ApplyBlendShape = true;
        public bool ApplyLookAt = true;
        public bool ApplyTracker = true;
        public bool ApplyCamera = true;
        public bool ApplyLight = true;
        public bool ApplyMidi = true;
        public bool ApplyStatus = true;
        public bool ApplyControl = true;
        public bool ApplySetting = true;
        [OptionalField]
        public bool ApplyControllerInput = true;
        [OptionalField]
        public bool ApplyKeyboardInput = false;


        //初期値
        [OnDeserializing()]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            Name = "Receiver";

            ApplyRootRotation = true;
            ApplyRootPosition = true;
            ApplySpine = true;
            ApplyChest = true;
            ApplyHead = true;
            ApplyLeftArm = true;
            ApplyRightArm = true;
            ApplyLeftHand = true;
            ApplyRightHand = true;
            ApplyLeftLeg = true;
            ApplyRightLeg = true;
            ApplyLeftFoot = true;
            ApplyRightFoot = true;
            ApplyLeftFinger = true;
            ApplyRightFinger = true;

            FixHandBone = true;
            IgnoreDefaultBone = true;
            UseNormalizedBone = false;

            ApplyBlendShape = true;
            ApplyLookAt = true;
            ApplyTracker = true;
            ApplyCamera = true;
            ApplyLight = true;
            ApplyMidi = true;
            ApplyStatus = true;
            ApplyControl = true;
            ApplySetting = true;
            ApplyControllerInput = true;
            ApplyKeyboardInput = false;
        }

        public VMCProtocolReceiverSettings Import(PipeCommands.SetVMCProtocolReceiverSetting setting)
        {
            Enable = setting.Enable;
            Port = setting.Port;
            DelayMs = setting.DelayMs;

            Name = setting.Name;

            ApplyRootRotation = setting.ApplyRootRotation;
            ApplyRootPosition = setting.ApplyRootPosition;
            ApplySpine = setting.ApplySpine;
            ApplyChest = setting.ApplyChest;
            ApplyHead = setting.ApplyHead;
            ApplyLeftArm = setting.ApplyLeftArm;
            ApplyRightArm = setting.ApplyRightArm;
            ApplyLeftHand = setting.ApplyLeftHand;
            ApplyRightHand = setting.ApplyRightHand;
            ApplyLeftLeg = setting.ApplyLeftLeg;
            ApplyRightLeg = setting.ApplyRightLeg;
            ApplyLeftFoot = setting.ApplyLeftFoot;
            ApplyRightFoot = setting.ApplyRightFoot;
            ApplyEye = setting.ApplyEye;
            ApplyLeftFinger = setting.ApplyLeftFinger;
            ApplyRightFinger = setting.ApplyRightFinger;
            FixHandBone = setting.CorrectHandBone;
            UseBonePosition = setting.UseBonePosition;
            CorrectHipBone = setting.CorrectHipBone;
            IgnoreDefaultBone = setting.IgnoreDefaultBone;

            ApplyBlendShape = setting.ApplyBlendShape;
            ApplyLookAt = setting.ApplyLookAt;
            ApplyTracker = setting.ApplyTracker;
            ApplyCamera = setting.ApplyCamera;
            ApplyLight = setting.ApplyLight;
            ApplyMidi = setting.ApplyMidi;
            ApplyStatus = setting.ApplyStatus;
            ApplyControl = setting.ApplyControl;
            ApplySetting = setting.ApplySetting;
            ApplyControllerInput = setting.ApplyControllerInput;
            ApplyKeyboardInput = setting.ApplyKeyboardInput;
            UseNormalizedBone = setting.UseNormalizedBone;

            return this;
        }

        public PipeCommands.SetVMCProtocolReceiverSetting Export(int index)
        {
            var setting = new PipeCommands.SetVMCProtocolReceiverSetting
            {

                Index = index,
                Enable = Enable,
                Port = Port,
                Name = Name,

                ApplyRootRotation = ApplyRootRotation,
                ApplyRootPosition = ApplyRootPosition,
                ApplySpine = ApplySpine,
                ApplyChest = ApplyChest,
                ApplyHead = ApplyHead,
                ApplyLeftArm = ApplyLeftArm,
                ApplyRightArm = ApplyRightArm,
                ApplyLeftHand = ApplyLeftHand,
                ApplyRightHand = ApplyRightHand,
                ApplyLeftLeg = ApplyLeftLeg,
                ApplyRightLeg = ApplyRightLeg,
                ApplyLeftFoot = ApplyLeftFoot,
                ApplyRightFoot = ApplyRightFoot,
                ApplyEye = ApplyEye,
                ApplyLeftFinger = ApplyLeftFinger,
                ApplyRightFinger = ApplyRightFinger,

                DelayMs = DelayMs,

                CorrectHandBone = FixHandBone,
                CorrectHipBone = CorrectHipBone,
                UseBonePosition = UseBonePosition,
                IgnoreDefaultBone = IgnoreDefaultBone,

                ApplyBlendShape = ApplyBlendShape,
                ApplyLookAt = ApplyLookAt,
                ApplyTracker = ApplyTracker,
                ApplyCamera = ApplyCamera,
                ApplyLight = ApplyLight,
                ApplyMidi = ApplyMidi,
                ApplyStatus = ApplyStatus,
                ApplyControl = ApplyControl,
                ApplySetting = ApplySetting,
                ApplyControllerInput = ApplyControllerInput,
                ApplyKeyboardInput = ApplyKeyboardInput,
                UseNormalizedBone = UseNormalizedBone,
            };
            return setting;
        }
    }

    [Serializable]
    public class Settings
    {
        public static Settings Current = new Settings();

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
        [OptionalField]
        public StoreTransform chestTracker = null;
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
        public Tuple<ETrackedDeviceClass, string> Chest = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));

        [OptionalField]
        public float LeftHandTrackerOffsetToBottom = 0.02f;
        [OptionalField]
        public float LeftHandTrackerOffsetToBodySide = 0.05f;
        [OptionalField]
        public float RightHandTrackerOffsetToBottom = 0.02f;
        [OptionalField]
        public float RightHandTrackerOffsetToBodySide = 0.05f;

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
        public List<bool> ExternalMotionReceiverEnableList;
        [OptionalField]
        public int ExternalMotionReceiverPort;
        [OptionalField]
        public List<int> ExternalMotionReceiverPortList;
        [OptionalField]
        public List<int> ExternalMotionReceiverDelayMsList;
        [OptionalField]
        public bool ExternalMotionReceiverRequesterEnable;
        [OptionalField]
        public string ExternalMotionSenderOptionString;

        /// <summary>
        /// 正規化(ControlRig)ボーン姿勢を送信する。
        /// VMCProtocolの仕様ではオリジナル(非正規化)ボーンが推奨で、
        /// 正規化ボーンの送信は「既定で無効のオプション」と定められているため既定はfalse。
        /// </summary>
        [OptionalField]
        public bool ExternalMotionSenderUseNormalizedBone = false;

        /// <summary>
        /// 表情をVRM1.0形式の名称(happy/aa等)でも送信する。
        /// 仕様ではVRM0.x形式の送信が必須で、VRM1.0形式はオプション。
        /// </summary>
        [OptionalField]
        public bool ExternalMotionSenderSendVRM1Expression = false;
        [OptionalField]
        public List<string> MidiCCBlendShape;
        [OptionalField]
        public bool MidiEnable;

        [OptionalField]
        public bool ExternalBonesReceiverEnable;

        [OptionalField]
        public List<VMCProtocolReceiverSettings> VMCProtocolReceiverSettingsList;

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
        public bool FixElbowRotation;

        [OptionalField]
        public bool HandleControllerAsTracker;

        [OptionalField]
        public bool TrackerReassignmentWhenChestAvailable;

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


        /// <summary>
        /// プラグイン(Plugins/配下)の設定。"プラグインID/キー" → JSON文字列。
        /// プロファイル切り替えでプラグインの設定も一緒に切り替わるよう、ここに保存する。
        /// </summary>
        [OptionalField]
        public Dictionary<string, string> PluginSettings;

        //モーション再生
        [OptionalField]
        public List<string> MotionPlayback_MotionFiles;
        [OptionalField]
        public int MotionPlayback_RepeatMode;
        [OptionalField]
        public bool MotionPlayback_ApplyRootPosition;
        [OptionalField]
        public bool MotionPlayback_ApplyRootRotation;
        [OptionalField]
        public bool MotionPlayback_ApplySpine;
        [OptionalField]
        public bool MotionPlayback_ApplyChest;
        [OptionalField]
        public bool MotionPlayback_ApplyHead;
        [OptionalField]
        public bool MotionPlayback_ApplyLeftArm;
        [OptionalField]
        public bool MotionPlayback_ApplyRightArm;
        [OptionalField]
        public bool MotionPlayback_ApplyLeftHand;
        [OptionalField]
        public bool MotionPlayback_ApplyRightHand;
        [OptionalField]
        public bool MotionPlayback_ApplyLeftLeg;
        [OptionalField]
        public bool MotionPlayback_ApplyRightLeg;
        [OptionalField]
        public bool MotionPlayback_ApplyLeftFoot;
        [OptionalField]
        public bool MotionPlayback_ApplyRightFoot;
        [OptionalField]
        public bool MotionPlayback_ApplyLeftFinger;
        [OptionalField]
        public bool MotionPlayback_ApplyRightFinger;
        [OptionalField]
        public bool MotionPlayback_ApplyEye;

        //モーション記録
        [OptionalField]
        public int MotionRecord_Fps;
        [OptionalField]
        public int MotionRecord_CountdownSeconds;
        [OptionalField]
        public bool MotionRecord_SaveMotion;
        [OptionalField]
        public bool MotionRecord_SaveExpressionPreset;
        [OptionalField]
        public bool MotionRecord_SaveExpressionCustom;
        [OptionalField]
        public bool MotionRecord_SaveLookAt;


        [OptionalField]
        public bool EnableOverrideBodyHeight;
        [OptionalField]
        public float OverrideBodyHeight;
        [OptionalField]
        public float PelvisOffsetAdjustY;
        [OptionalField]
        public float PelvisOffsetAdjustZ;

        [OptionalField]
        public int WristRotationFix_UpperArmWeight = 200; // /1000
        [OptionalField]
        public int WristRotationFix_ForearmWeight = 570; // /1000
        [OptionalField]
        public int WristRotationFix_MaxAccumulatedTwist = 300;

        //キャリブレーション自動再適用
        //キャリブレーション実行時のトラッカー姿勢を記録しておき、別のアバターを読み込んだ時に
        //同じトラッカー姿勢でキャリブレーションを再実行することで、再度Tポーズを取る手間を省く
        [OptionalField]
        public bool EnableAutoCalibrationOnModelLoad = true;
        [OptionalField]
        public CalibrationSnapshot LastCalibrationSnapshot = null;


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
            Chest = Tuple.Create(ETrackedDeviceClass.GenericTracker, default(string));

            LeftHandTrackerOffsetToBottom = 0.02f;
            LeftHandTrackerOffsetToBodySide = 0.05f;
            RightHandTrackerOffsetToBottom = 0.02f;
            RightHandTrackerOffsetToBodySide = 0.05f;

            PositionFixedCameraTransform = null;

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
            //仕様上、正規化ボーンの送信は「既定で無効のオプション」
            ExternalMotionSenderUseNormalizedBone = false;
            ExternalMotionSenderSendVRM1Expression = false;
            ExternalMotionSenderResponderEnable = false;

            ExternalMotionReceiverEnable = false;
            ExternalMotionReceiverEnableList = null;
            ExternalMotionReceiverPort = 39540;
            ExternalMotionReceiverPortList = null;
            ExternalMotionReceiverDelayMsList = null;
            ExternalMotionReceiverRequesterEnable = true;

            MidiCCBlendShape = new List<string>(Enumerable.Repeat(default(string), MidiCCWrapper.KNOBS));
            MidiEnable = false;


            TrackingFilterEnable = true;
            TrackingFilterHmdEnable = true;
            TrackingFilterControllerEnable = true;
            TrackingFilterTrackerEnable = true;

            FixKneeRotation = true;
            FixElbowRotation = true;

            HandleControllerAsTracker = false;

            TrackerReassignmentWhenChestAvailable = false;

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

            VMCProtocolReceiverSettingsList = new List<VMCProtocolReceiverSettings>();


            MotionPlayback_MotionFiles = new List<string>();
            MotionPlayback_RepeatMode = 0;
            MotionPlayback_ApplyRootPosition = true;
            MotionPlayback_ApplyRootRotation = true;
            MotionPlayback_ApplySpine = true;
            MotionPlayback_ApplyChest = true;
            MotionPlayback_ApplyHead = true;
            MotionPlayback_ApplyLeftArm = true;
            MotionPlayback_ApplyRightArm = true;
            MotionPlayback_ApplyLeftHand = true;
            MotionPlayback_ApplyRightHand = true;
            MotionPlayback_ApplyLeftLeg = true;
            MotionPlayback_ApplyRightLeg = true;
            MotionPlayback_ApplyLeftFoot = true;
            MotionPlayback_ApplyRightFoot = true;
            MotionPlayback_ApplyLeftFinger = true;
            MotionPlayback_ApplyRightFinger = true;
            MotionPlayback_ApplyEye = true;

            MotionRecord_Fps = 30;
            MotionRecord_CountdownSeconds = 3;
            MotionRecord_SaveMotion = true;
            MotionRecord_SaveExpressionPreset = true;
            MotionRecord_SaveExpressionCustom = true;
            MotionRecord_SaveLookAt = true;

            EnableOverrideBodyHeight = false;
            OverrideBodyHeight = 1.7f;
            PelvisOffsetAdjustY = 0;
            PelvisOffsetAdjustZ = 0;

            WristRotationFix_UpperArmWeight = 200;
            WristRotationFix_ForearmWeight = 570;
            WristRotationFix_MaxAccumulatedTwist = 300;

            EnableAutoCalibrationOnModelLoad = true;
            LastCalibrationSnapshot = null;
        }

        /// <summary>
        /// 指定したバージョンより前の設定ファイルかどうか(指定バージョンは含まない)
        /// </summary>
        /// <param name="major"></param>
        /// <param name="minor"></param>
        /// <returns></returns>
        public bool IsSettingVersionBefore(int major, int minor)
        {
            if (major < 0 || minor < 0 || (major == 0 && minor < 48))
                throw new ArgumentOutOfRangeException(nameof(minor), "over 0.48 only");

            if (string.IsNullOrWhiteSpace(AAA_SavedVersion))
            {
                //before 0.47 _SaveVersion is null.
                return major > 0 || (major == 0 && minor > 47);
            }
            else
            {
                var split = AAA_SavedVersion.Replace("v", "").Split('.');
                int pmajor, pminor;
                if (split.Length == 2 && int.TryParse(split[0], out pmajor) && int.TryParse(split[1], out pminor))
                {
                    return major > pmajor || (major == pmajor && minor > pminor);
                }
                else
                {
                    // parse failed
                    return false;
                }
            }
        }
    }
}
