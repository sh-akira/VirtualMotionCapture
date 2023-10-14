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
        [OptionalField]
        public List<string> MidiCCBlendShape;
        [OptionalField]
        public bool MidiEnable;
        [OptionalField]
        public Dictionary<string, string> LipShapesToBlendShapeMap;
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
        public bool FixElbowRotation;

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

        [OptionalField]
        public bool mocopi_Enable;
        [OptionalField]
        public int mocopi_Port;
        [OptionalField]
        public bool mocopi_ApplyRootPosition;
        [OptionalField]
        public bool mocopi_ApplyRootRotation;
        [OptionalField]
        public bool mocopi_ApplyChest;
        [OptionalField]
        public bool mocopi_ApplySpine;
        [OptionalField]
        public bool mocopi_ApplyHead;
        [OptionalField]
        public bool mocopi_ApplyLeftArm;
        [OptionalField]
        public bool mocopi_ApplyRightArm;
        [OptionalField]
        public bool mocopi_ApplyLeftHand;
        [OptionalField]
        public bool mocopi_ApplyRightHand;
        [OptionalField]
        public bool mocopi_ApplyLeftLeg;
        [OptionalField]
        public bool mocopi_ApplyRightLeg;
        [OptionalField]
        public bool mocopi_ApplyLeftFoot;
        [OptionalField]
        public bool mocopi_ApplyRightFoot;


        [OptionalField]
        public bool EnableOverrideBodyHeight;
        [OptionalField]
        public float OverrideBodyHeight;
        [OptionalField]
        public float PelvisOffsetAdjustY;
        [OptionalField]
        public float PelvisOffsetAdjustZ;


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
            ExternalMotionReceiverEnableList = null;
            ExternalMotionReceiverPort = 39540;
            ExternalMotionReceiverPortList = null;
            ExternalMotionReceiverDelayMsList = null;
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
            FixElbowRotation = true;

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

            mocopi_Enable = true;
            mocopi_Port = 12351;
            mocopi_ApplyRootPosition = true;
            mocopi_ApplyRootRotation = true;
            mocopi_ApplyChest = true;
            mocopi_ApplySpine = true;
            mocopi_ApplyHead = true;
            mocopi_ApplyLeftArm = true;
            mocopi_ApplyRightArm = true;
            mocopi_ApplyLeftHand = true;
            mocopi_ApplyRightHand = true;
            mocopi_ApplyLeftLeg = true;
            mocopi_ApplyRightLeg = true;
            mocopi_ApplyLeftFoot = true;
            mocopi_ApplyRightFoot = true;

            EnableOverrideBodyHeight = false;
            OverrideBodyHeight = 1.7f;
            PelvisOffsetAdjustY = 0;
            PelvisOffsetAdjustZ = 0;
        }
    }
}
