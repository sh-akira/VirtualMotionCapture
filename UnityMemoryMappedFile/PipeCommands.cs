using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public partial class PipeCommands
    {
        private static Dictionary<string, Type> commandTypeCache = new Dictionary<string, Type>();

        public static Type GetCommandType(string commandStr)
        {
            if (commandTypeCache.TryGetValue(commandStr,out Type value))
            {
                return value;
            }
            var commands = typeof(PipeCommands).GetNestedTypes(System.Reflection.BindingFlags.Public);
            foreach (var command in commands)
            {
                if (command.Name == commandStr)
                {
                    commandTypeCache[commandStr] = command;
                    return command;
                }
            }
            return null;
        }

        public class SetIsBeta
        {
            public bool IsPreRelease { get; set; }
            public bool IsBeta { get; set; }
        }

        public class LoadVRM
        {
            public string Path { get; set; }
        }

        public class LoadRemoteVRM
        {
            public string Path { get; set; }
        }

        public class ReturnLoadVRM
        {
            public VRMData Data { get; set; }
        }

        public class ImportVRM
        {
            public string Path { get; set; }
            public bool ImportForCalibration { get; set; }
            public bool EnableNormalMapFix { get; set; }
            public bool DeleteHairNormalMap { get; set; }
            public bool UseCurrentFixSetting { get; set; }
        }

        public class LoadVRMPath
        {
            public string Path { get; set; }
        }

        public class Calibrate
        {
            public CalibrateType CalibrateType { get; set; }
        }

        public class SelectCalibrateMode
        {
            public CalibrateType CalibrateType { get; set; }
        }

        public enum CalibrateType
        {
            Invalid = -1,
            Ipose = 0,
            Tpose = 1,
            FixedHandWithGround = 2,
            FixedHand = 3
        }

        public class EndCalibrate { }

        public class QuitApplication { }

        public class SetFloatValueBase { public float value { get; set; } }

        public class SetLipSyncEnable { public bool enable { get; set; } }
        public class GetLipSyncDevices { }
        public class ReturnGetLipSyncDevices { public string[] Devices { get; set; } }
        public class SetLipSyncDevice { public string device { get; set; } }
        public class SetLipSyncGain : SetFloatValueBase { }
        public class SetLipSyncMaxWeightEnable { public bool enable { get; set; } }
        public class SetLipSyncWeightThreashold : SetFloatValueBase { }
        public class SetLipSyncMaxWeightEmphasis { public bool enable { get; set; } }

        public class ChangeBackgroundColor
        {
            public float r { get; set; }
            public float g { get; set; }
            public float b { get; set; }
            public bool isCustom { get; set; }
        }
        public class SetBackgroundTransparent { }
        public class SetWindowBorder { public bool enable { get; set; } }
        public class SetWindowTopMost { public bool enable { get; set; } }
        public class SetWindowClickThrough { public bool enable { get; set; } }

        public class SetAutoBlinkEnable { public bool enable { get; set; } }
        public class SetBlinkTimeMin : SetFloatValueBase { }
        public class SetBlinkTimeMax : SetFloatValueBase { }
        public class SetCloseAnimationTime : SetFloatValueBase { }
        public class SetOpenAnimationTime : SetFloatValueBase { }
        public class SetClosingTime : SetFloatValueBase { }
        public class SetDefaultFace { public string face { get; set; } }

        public class SaveSettings { public string Path { get; set; } }
        public class LoadSettings { public string Path { get; set; } }
        public class LoadCurrentSettings { }
        public class LoadCustomBackgroundColor
        {
            public float r { get; set; }
            public float g { get; set; }
            public float b { get; set; }
        }
        public class LoadHideBorder { public bool enable { get; set; } }
        public class LoadIsTopMost { public bool enable { get; set; } }
        public class LoadSetWindowClickThrough { public bool enable { get; set; } }
        public class LoadShowCameraGrid { public bool enable { get; set; } }
        public class LoadCameraMirror { public bool enable { get; set; } }
        public class LoadCameraFOV { public float fov { get; set; } }
        public class LoadCameraSmooth { public float speed { get; set; } }
        public class LoadLipSyncEnable { public bool enable { get; set; } }
        public class LoadLipSyncDevice { public string device { get; set; } }
        public class LoadLipSyncGain { public float gain { get; set; } }
        public class LoadLipSyncMaxWeightEnable { public bool enable { get; set; } }
        public class LoadLipSyncWeightThreashold { public float threashold { get; set; } }
        public class LoadLipSyncMaxWeightEmphasis { public bool enable { get; set; } }
        public class LoadAutoBlinkEnable { public bool enable { get; set; } }
        public class LoadBlinkTimeMin { public float time { get; set; } }
        public class LoadBlinkTimeMax { public float time { get; set; } }
        public class LoadCloseAnimationTime { public float time { get; set; } }
        public class LoadOpenAnimationTime { public float time { get; set; } }
        public class LoadClosingTime { public float time { get; set; } }
        public class LoadDefaultFace { public string face { get; set; } }

        public class LoadControllerTouchPadPoints
        {
            public bool IsOculus { get; set; }
            public List<UPoint> LeftPoints { get; set; }
            public bool LeftCenterEnable { get; set; }
            public List<UPoint> RightPoints { get; set; }
            public bool RightCenterEnable { get; set; }
        }
        public class LoadControllerStickPoints
        {
            public List<UPoint> LeftPoints { get; set; }
            public List<UPoint> RightPoints { get; set; }
        }
        public class LoadKeyActions { public List<KeyAction> KeyActions { get; set; } }

        public class ChangeCamera { public CameraTypes type { get; set; } }
        public class SetGridVisible { public bool enable { get; set; } }
        public class SetCameraMirror { public bool enable { get; set; } }
        public class SetCameraFOV : SetFloatValueBase { }
        public class SetCameraSmooth : SetFloatValueBase { }
        public class ResetCamera { }

        public class KeyDown
        {
            public KeyConfig Config { get; set; }
        }

        public class KeyUp
        {
            public KeyConfig Config { get; set; }
        }

        public class SetControllerTouchPadPoints
        {
            public bool isStick { get; set; }
            public List<UPoint> LeftPoints { get; set; }
            public bool LeftCenterEnable { get; set; }
            public List<UPoint> RightPoints { get; set; }
            public bool RightCenterEnable { get; set; }
        }

        public class SetSkeletalInputEnable { public bool enable { get; set; } }
        public class LoadSkeletalInputEnable { public bool enable { get; set; } }

        public class StartHandCamera
        {
            public bool IsLeft { get; set; }
        }

        public class EndHandCamera { }

        public class SetHandAngle
        {
            public bool LeftEnable { get; set; }
            public bool RightEnable { get; set; }
            public List<int> HandAngles { get; set; } //小指:第1関節,第3関節,第3関節,第3関節横,薬指:・・・・親指:・・・第3関節横 (20個)
        }

        public class StartKeyConfig { }
        public class EndKeyConfig { }

        public class StartKeySend { }
        public class EndKeySend { }

        public class SetKeyActions
        {
            public List<KeyAction> KeyActions { get; set; }
        }

        public class SetFace
        {
            public List<string> Keys { get; set; }
            public List<float> Strength { get; set; }
        }

        public class GetFaceKeys { }
        public class ReturnFaceKeys
        {
            public List<string> Keys { get; set; }
        }


        public class SetHandFreeOffset
        {
            public int LeftHandPositionX { get; set; }
            public int LeftHandPositionY { get; set; }
            public int LeftHandPositionZ { get; set; }
            public int LeftHandRotationX { get; set; }
            public int LeftHandRotationY { get; set; }
            public int LeftHandRotationZ { get; set; }
            public int RightHandPositionX { get; set; }
            public int RightHandPositionY { get; set; }
            public int RightHandPositionZ { get; set; }
            public int RightHandRotationX { get; set; }
            public int RightHandRotationY { get; set; }
            public int RightHandRotationZ { get; set; }
            public int SwivelOffset { get; set; }
        }

        public class SetExternalCameraConfig
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
            public float rx { get; set; }
            public float ry { get; set; }
            public float rz { get; set; }
            public float fov { get; set; }
            public string ControllerName { get; set; }
        }
        public class GetExternalCameraConfig
        {
            public string ControllerName { get; set; }
        }


        public class ExitControlPanel { }

        public class GetTrackerSerialNumbers { }
        public class ReturnTrackerSerialNumbers
        {
            public List<Tuple<string, string>> List { get; set; }
            public SetTrackerSerialNumbers CurrentSetting { get; set; }
        }

        public class TrackerMoved
        {
            public string SerialNumber { get; set; }
        }

        public class SetTrackerSerialNumbers
        {
            public Tuple<string, string> Head { get; set; }
            public Tuple<string, string> LeftHand { get; set; }
            public Tuple<string, string> RightHand { get; set; }
            public Tuple<string, string> Pelvis { get; set; }
            public Tuple<string, string> LeftFoot { get; set; }
            public Tuple<string, string> RightFoot { get; set; }
            public Tuple<string, string> LeftElbow { get; set; }
            public Tuple<string, string> RightElbow { get; set; }
            public Tuple<string, string> LeftKnee { get; set; }
            public Tuple<string, string> RightKnee { get; set; }
        }

        public class GetTrackerOffsets { }
        public class SetTrackerOffsets
        {
            public float LeftHandTrackerOffsetToBottom { get; set; }
            public float LeftHandTrackerOffsetToBodySide { get; set; }
            public float RightHandTrackerOffsetToBottom { get; set; }
            public float RightHandTrackerOffsetToBodySide { get; set; }
        }

        public class GetVirtualWebCamConfig { }
        public class SetVirtualWebCamConfig
        {
            public bool Enabled { get; set; }
            public bool Resize { get; set; }
            public bool Mirroring { get; set; }
            public int Buffering { get; set; }
        }

        public class GetResolutions { }
        public class ReturnResolutions
        {
            public List<Tuple<int, int, int>> List { get; set; }
        }
        public class SetResolution
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
        }

        public class SetWindowNum
        {
            public int Num { get; set; }
        }

        public class TakePhoto
        {
            public int Width { get; set; }
            public bool TransparentBackground { get; set; }
            public string Directory { get; set; }
        }

        public class SetLightAngle
        {
            public float X { get; set; }
            public float Y { get; set; }
        }

        public class ChangeLightColor
        {
            public float a { get; set; }
            public float r { get; set; }
            public float g { get; set; }
            public float b { get; set; }
        }

        public class TrackerMovedRequest
        {
            public bool doSend { get; set; }
        }

        public class GetEyeTracking_TobiiOffsets { }
        public class SetEyeTracking_TobiiOffsets
        {
            public float ScaleHorizontal { get; set; }
            public float ScaleVertical { get; set; }
            public float OffsetHorizontal { get; set; }
            public float OffsetVertical { get; set; }
        }

        public class EyeTracking_TobiiCalibration { }

        public class GetEyeTracking_ViveProEyeOffsets { }
        public class SetEyeTracking_ViveProEyeOffsets
        {
            public float ScaleHorizontal { get; set; }
            public float ScaleVertical { get; set; }
            public float OffsetHorizontal { get; set; }
            public float OffsetVertical { get; set; }
        }
        public class GetEyeTracking_ViveProEyeUseEyelidMovements { }
        public class SetEyeTracking_ViveProEyeUseEyelidMovements
        {
            public bool Use { get; set; }
        }
        public class GetEyeTracking_ViveProEyeEnable { }
        public class SetEyeTracking_ViveProEyeEnable
        {
            public bool enable { get; set; }
        }

        public class ImportCameraPlus
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
            public float rx { get; set; }
            public float ry { get; set; }
            public float rz { get; set; }
            public float fov { get; set; }
        }
        public class ExportCameraPlus { }
        public class ReturnExportCameraPlus
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
            public float rx { get; set; }
            public float ry { get; set; }
            public float rz { get; set; }
            public float fov { get; set; }
        }

        public class GetEnableExternalMotionSender { }
        public class EnableExternalMotionSender
        {
            public bool enable { get; set; }
        }
        public class GetExternalMotionSenderAddress { }
        public class ChangeExternalMotionSenderAddress
        {
            public string address { get; set; }
            public int port { get; set; }

            public int PeriodStatus { get; set; }
            public int PeriodRoot { get; set; }
            public int PeriodBone { get; set; }
            public int PeriodBlendShape { get; set; }
            public int PeriodCamera { get; set; }
            public int PeriodDevices { get; set; }

            public string OptionString { get; set; } //OK
            public bool ResponderEnable { get; set; } 
        }

        public class GetEnableExternalMotionReceiver
        {
            public int index { get; set; }
        }
        public class EnableExternalMotionReceiver
        {
            public bool enable { get; set; }
            public int index { get; set; }
        }
        public class GetExternalMotionReceiverPort { }
        public class ChangeExternalMotionReceiverPort
        {
            public int[] ports { get; set; }
            public bool RequesterEnable { get; set; }
        }
        public class GetExternalReceiveBones { }
        public class ExternalReceiveBones
        {
            public bool ReceiveBonesEnable { get; set; }
        }
        public class GetMidiCCBlendShape { }
        public class SetMidiCCBlendShape
        {
            public List<string> BlendShapes { get; set; }
        }
        public class MidiCCKnobUpdate
        {
            public int channel { get; set; }
            public int knobNo { get; set; }
            public float value { get; set; }
        }

        public class GetMidiEnable { }
        public class MidiEnable
        {
            public bool enable { get; set; }
        }

        public class GetEnableTrackingFilter { }
        public class EnableTrackingFilter
        {
            public bool globalEnable { get; set; }
            public bool hmdEnable { get; set; }
            public bool controllerEnable { get; set; }
            public bool trackerEnable { get; set; }
        }

        public class GetPauseTracking { }
        public class PauseTracking
        {
            public bool enable { get; set; }
        }

        public class GetEnableModelModifier { }
        public class EnableModelModifier
        {
            public bool fixKneeRotation { get; set; }
            public bool fixElbowRotation { get; set; }
        }
        public class GetVirtualMotionTracker { }
        public class SetVirtualMotionTracker
        {
            public bool enable { get; set; }
            public int no { get; set; }
        }

        public class SetupVirtualMotionTracker
        {
            public bool install { get; set; }
        }

        public class ResultSetupVirtualMotionTracker
        {
            public string result { get; set; }
        }

        //-------------
        public class GetStatusString { }
        public class SetStatusString
        {
            public string StatusString { get; set; }
        }

        public class StatusStringChanged
        {
            public string StatusString { get; set; }
        }

        public class StatusStringChangedRequest
        {
            public bool doSend { get; set; }
        }

        public class GetHandleControllerAsTracker { }
        public class EnableHandleControllerAsTracker
        {
            public bool HandleControllerAsTracker { get; set; }
        }


        public class GetQualitySettings { }
        public class SetQualitySettings
        {
            public int antiAliasing { get; set; }
        }

        public class GetViveLipTrackingBlendShape { }
        public class SetViveLipTrackingBlendShape
        {
            public List<string> LipShapes { get; set; }
            public Dictionary<string, string> LipShapesToBlendShapeMap { get; set; }
        }
        public class GetViveLipTrackingEnable { }
        public class SetViveLipTrackingEnable
        {
            public bool enable { get; set; }
        }

        public class GetAdvancedGraphicsOption { }
        public class SetAdvancedGraphicsOption
        {
            public bool PPS_Enable { get; set; }
            public bool Bloom_Enable { get; set; }
            public float Bloom_Intensity { get; set; }
            public float Bloom_Threshold { get; set; }

            public bool DoF_Enable { get; set; }
            public float DoF_FocusDistance { get; set; }
            public float DoF_Aperture { get; set; }
            public float DoF_FocusLength { get; set; }
            public int DoF_MaxBlurSize { get; set; }

            public bool CG_Enable { get; set; }
            public float CG_Temperature { get; set; }
            public float CG_Saturation { get; set; }
            public float CG_Contrast { get; set; }
            public float CG_Gamma { get; set; }

            public bool Vignette_Enable { get; set; }
            public float Vignette_Intensity { get; set; }
            public float Vignette_Smoothness { get; set; }
            public float Vignette_Roundness { get; set; }

            public bool CA_Enable { get; set; }
            public float CA_Intensity { get; set; }
            public bool CA_FastMode { get; set; }

            public float Bloom_Color_a { get; set; }
            public float Bloom_Color_r { get; set; }
            public float Bloom_Color_g { get; set; }
            public float Bloom_Color_b { get; set; }

            public float CG_ColorFilter_a { get; set; }
            public float CG_ColorFilter_r { get; set; }
            public float CG_ColorFilter_g { get; set; }
            public float CG_ColorFilter_b { get; set; }

            public float Vignette_Color_a { get; set; }
            public float Vignette_Color_r { get; set; }
            public float Vignette_Color_g { get; set; }
            public float Vignette_Color_b { get; set; }

            public bool TurnOffAmbientLight { get; set; }

        }

        public const int ErrorCountMax = 10000;
        public class LogNotify {
            public string condition { get; set; }
            public string stackTrace { get; set; }
            public NotifyLogTypes type { get; set; }
            public int errorCount { get; set; }
        }

        public class SetLogNotifyLevel
        {
            public NotifyLogTypes type { get; set; }
        }

        public class Alive { }

        public class GetModIsLoaded { }
        public class ReturnModIsLoaded
        {
            public bool IsLoaded { get; set; }
        }

        public class GetModList { }
        public class ReturnModList
        {
            public List<ModItem> ModList { get; set; }
        }

        public class ModSettingEvent
        {
            public string InstanceId { get; set; }
        }
        public class ShowCalibrationWindow { }
        public class ShowPhotoWindow { }

        public class VRMLoadStatus
        {
            public bool Valid { get; set; }
        }
        public class CalibrationResult
        {
            public CalibrateType Type { get; set; }
            public float UserHeight { get; set; }
            public string Message { get; set; }
        }

    }

    public class ModItem
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string AuthorURL { get; set; }
        public string Description { get; set; }
        public string PluginURL { get; set; }
        public string InstanceId { get; set; }
        public string AssemblyPath { get; set; }
    }

    public enum NotifyLogTypes
    {
        Exception = 0,
        Error = 1,
        Assert = 2,
        Warning = 3,
        Log = 4,
    }

    public enum CameraTypes
    {
        Free, Front, Back, PositionFixed
    }

    public struct UPoint
    {
        public float x;
        public float y;
    }

    public enum KeyTypes
    {
        Controller,
        Keyboard,
        Mouse,
        Midi,
        MidiCC,
    }

    public enum KeyActionTypes
    {
        Face,
        Hand,
    }

    [Serializable]
    public class KeyConfig
    {
        public static string Language = "Japanese";

        public KeyTypes type;
        public KeyActionTypes actionType;
        public int keyCode;
        public string keyName;
        public bool isLeft;
        public int keyIndex;
        [OptionalField]
        public bool isOculus;
        [OptionalField]
        public bool isTouch;

        public bool IsEqual(KeyConfig k)
        {
            return type == k.type && actionType == k.actionType && keyCode == k.keyCode && isLeft == k.isLeft && keyIndex == k.keyIndex && isOculus == k.isOculus && isTouch == k.isTouch && keyName == k.keyName;
        }

        public bool IsEqualKeyCode(KeyConfig k)
        {
            return type == k.type && keyCode == k.keyCode && isLeft == k.isLeft && keyIndex == k.keyIndex && isOculus == k.isOculus && isTouch == k.isTouch && keyName == k.keyName;
        }

        private static string[] KeyCodeString = new string[] {
            "",
            "左クリック",
            "右クリック",
            "コントロールブレイク",
            "中クリック",
            "マウス第一拡張",
            "マウス第二拡張",
            "未定義",
            "BackSpace",
            "Tab",
            "予約済",
            "予約済",
            "Clear",
            "Enter",
            "未定義",
            "未定義",
            "Shift",
            "Ctrl",
            "Alt",
            "Pause",
            "CapsLock",
            "IME かな",
            "未定義",
            "IME Junja",
            "IME ファイナル",
            "IME 漢字",
            "未定義",
            "Esc",
            "IME 変換",
            "IME 無変換",
            "IME 使用可能",
            "IME モード変更要求",
            "スペース",
            "Page Up",
            "Page Down",
            "End",
            "Home",
            "←",
            "↑",
            "→",
            "↓",
            "Select",
            "Print",
            "Execute",
            "PrintScreen",
            "Insert",
            "Delete",
            "Help",
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "未定義",
            "未定義",
            "未定義",
            "未定義",
            "未定義",
            "未定義",
            "未定義",
            "A",
            "B",
            "C",
            "D",
            "E",
            "F",
            "G",
            "H",
            "I",
            "J",
            "K",
            "L",
            "M",
            "N",
            "O",
            "P",
            "Q",
            "R",
            "S",
            "T",
            "U",
            "V",
            "W",
            "X",
            "Y",
            "Z",
            "左Windows",
            "右Windows",
            "アプリケーション",
            "予約済",
            "Sleep",
            "テンキー0",
            "テンキー1",
            "テンキー2",
            "テンキー3",
            "テンキー4",
            "テンキー5",
            "テンキー6",
            "テンキー7",
            "テンキー8",
            "テンキー9",
            "テンキー*",
            "テンキー+",
            "区切り記号",
            "テンキー-",
            "テンキー.",
            "テンキー/",
            "F1",
            "F2",
            "F3",
            "F4",
            "F5",
            "F6",
            "F7",
            "F8",
            "F9",
            "F10",
            "F11",
            "F12",
            "F13",
            "F14",
            "F15",
            "F16",
            "F17",
            "F18",
            "F19",
            "F20",
            "F21",
            "F22",
            "F23",
            "F24",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "NumLock",
            "ScrollLock",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "未割当",
            "左Shift",
            "右Shift",
            "左Ctrl",
            "右Ctrl",
            "左Alt",
            "右Alt",
            "ブラウザー戻る",
            "ブラウザー進む",
            "ブラウザー更新",
            "ブラウザー停止",
            "ブラウザー検索",
            "ブラウザーお気に入り",
            "ブラウザー開始/ホーム",
            "音量ミュート",
            "音量ダウン",
            "音量アップ",
            "次のトラック",
            "前のトラック",
            "メディア停止",
            "メディア再生/一時停止",
            "メール",
            "メディア選択",
            "アプリケーション1",
            "アプリケーション2",
            "予約済",
            "予約済",
            "[:*]",
            "[;+]",
            "[,<]",
            "[-=]",
            "[.>]",
            "[/?]",
            "[@`]",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "予約済",
            "未割当",
            "未割当",
            "未割当",
            "[[{]",
            "[\\|]",
            "[]}]",
            "[^~]",
            "OEM8",
            "予約済",
            "OEM固有",
            "[＼_]",
            "OEM固有",
            "OEM固有",
            "IME PROCESS",
            "OEM固有",
            "仮想キー下位ワード",
            "未割当",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "英数",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "OEM固有",
            "Attn",
            "CrSel",
            "ExSel",
            "Erase EOF",
            "Play",
            "Zoom",
            "予約済",
            "PA1",
            "Clear",
            ""
        };

        private static string[] KeyCodeString_English = new string[] {
            "",
            "LeftClick",
            "RightClick",
            "ControlBreak",
            "CenterClick",
            "MouseAdditional1",
            "MouseAdditional2",
            "Undefined",
            "BackSpace",
            "Tab",
            "Reserved",
            "Reserved",
            "Clear",
            "Enter",
            "Undefined",
            "Undefined",
            "Shift",
            "Ctrl",
            "Alt",
            "Pause",
            "CapsLock",
            "IME Kana",
            "Undefined",
            "IME Junja",
            "IME Final",
            "IME Kanji",
            "Undefined",
            "Esc",
            "IME Henkan",
            "IME Muhenkan",
            "IME CanUse",
            "IME ChangeModeRequest",
            "Space",
            "Page Up",
            "Page Down",
            "End",
            "Home",
            "←",
            "↑",
            "→",
            "↓",
            "Select",
            "Print",
            "Execute",
            "PrintScreen",
            "Insert",
            "Delete",
            "Help",
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "Undefined",
            "Undefined",
            "Undefined",
            "Undefined",
            "Undefined",
            "Undefined",
            "Undefined",
            "A",
            "B",
            "C",
            "D",
            "E",
            "F",
            "G",
            "H",
            "I",
            "J",
            "K",
            "L",
            "M",
            "N",
            "O",
            "P",
            "Q",
            "R",
            "S",
            "T",
            "U",
            "V",
            "W",
            "X",
            "Y",
            "Z",
            "LeftWindows",
            "RightWindows",
            "Application",
            "Reserved",
            "Sleep",
            "NumPad0",
            "NumPad1",
            "NumPad2",
            "NumPad3",
            "NumPad4",
            "NumPad5",
            "NumPad6",
            "NumPad7",
            "NumPad8",
            "NumPad9",
            "NumPad*",
            "NumPad+",
            "SeparateSymbol",
            "NumPad-",
            "NumPad.",
            "NumPad/",
            "F1",
            "F2",
            "F3",
            "F4",
            "F5",
            "F6",
            "F7",
            "F8",
            "F9",
            "F10",
            "F11",
            "F12",
            "F13",
            "F14",
            "F15",
            "F16",
            "F17",
            "F18",
            "F19",
            "F20",
            "F21",
            "F22",
            "F23",
            "F24",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NumLock",
            "ScrollLock",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "LeftShift",
            "RightShift",
            "LeftCtrl",
            "RightCtrl",
            "LeftAlt",
            "RightAlt",
            "BrowserBack",
            "BrowserNext",
            "BrowserRefresh",
            "BrowserStop",
            "BrowserSearch",
            "BrowserFavorite",
            "BrowserHome",
            "VolumeMute",
            "VolumeDown",
            "VolumeUp",
            "NextTrack",
            "PrevTrack",
            "MediaStop",
            "MediaPlay/Pause",
            "Mail",
            "MediaSelect",
            "Application1",
            "Application2",
            "Reserved",
            "Reserved",
            "[:*]",
            "[;+]",
            "[,<]",
            "[-=]",
            "[.>]",
            "[/?]",
            "[@`]",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "Reserved",
            "NoAssign",
            "NoAssign",
            "NoAssign",
            "[[{]",
            "[\\|]",
            "[]}]",
            "[^~]",
            "OEM8",
            "Reserved",
            "OEM",
            "[＼_]",
            "OEM",
            "OEM",
            "IME PROCESS",
            "OEM",
            "VirtualKeyBottomWord",
            "NoAssign",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "Eisuu",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "OEM",
            "Attn",
            "CrSel",
            "ExSel",
            "Erase EOF",
            "Play",
            "Zoom",
            "Reserved",
            "PA1",
            "Clear",
            ""
        };
        public override string ToString()
        {
            if (Language == "Japanese")
            {
                var isLeftStr = type == KeyTypes.Controller ? (isLeft ? "左" : "右") : "";
                var keyCodeStr = type == KeyTypes.Controller ? keyName : type == KeyTypes.Keyboard ? KeyCodeString[keyCode] : keyCode.ToString();
                var indexStr = keyIndex > 0 ? $"{keyIndex}" : "";
                var keyTypesString = type == KeyTypes.Controller ? "コントローラー" : type == KeyTypes.Keyboard ? "キーボード" : type == KeyTypes.Midi ? "MIDI " : type == KeyTypes.MidiCC ? "MIDI CC " : "マウス";
                var isTouchStr = type == KeyTypes.Controller ? (isTouch ? "タッチ" : "") : "";
                return $"{isLeftStr}{keyTypesString}[{keyCodeStr}{indexStr}{isTouchStr}]";
            }
            else
            {
                var isLeftStr = type == KeyTypes.Controller ? (isLeft ? "Left" : "Right") : "";
                var keyCodeStr = type == KeyTypes.Controller ? keyName : type == KeyTypes.Keyboard ? KeyCodeString_English[keyCode] : keyCode.ToString();
                var indexStr = keyIndex > 0 ? $"{keyIndex}" : "";
                var keyTypesString = type == KeyTypes.Controller ? "Controller" : type == KeyTypes.Keyboard ? "Keyboard" : type == KeyTypes.Midi ? "MIDI " : type == KeyTypes.MidiCC ? "MIDI CC " : "Mouse";
                var isTouchStr = type == KeyTypes.Controller ? (isTouch ? "Touch" : "") : "";
                return $"{isLeftStr}{keyTypesString}[{keyCodeStr}{indexStr}{isTouchStr}]";
            }
        }
    }

    public class KeyAction
    {
        public List<KeyConfig> KeyConfigs { get; set; }
        public bool IsKeyUp { get; set; }
        public string Name { get; set; }
        public bool OnlyPress { get; set; }
        public bool HandAction { get; set; }
        public List<int> HandAngles { get; set; }
        public Hands Hand { get; set; }
        public bool FaceAction { get; set; }
        public List<string> FaceNames { get; set; }
        public List<float> FaceStrength { get; set; }
        public bool FunctionAction { get; set; }
        public Functions Function { get; set; }
        public bool StopBlink { get; set; }
        public bool SoftChange { get; set; }
        public bool DisableBlendShapeReception { get; set; }

        public float HandChangeTime { get; set; } = 0.1f;
        public float LipSyncMaxLevel { get; set; } = 1.0f;

        public static void KeyActionsUpgrade(List<KeyAction> keyActions)
        {
            //古いバージョンで保存したVIVE/Oculus用のキーコンフィグをアップグレード
            var newKeyActions = new List<KeyAction>();
            foreach (var action in keyActions)
            {
                var newKeyConfigs = new List<KeyConfig>();
                foreach (var config in action.KeyConfigs)
                {
                    if (config.type == KeyTypes.Controller && config.keyCode != -2)
                    {
                        if (config.keyIndex < 0)
                        {//通常キー

                            config.keyName = config.isTouch ? "Touch" : "Click";
                            if (config.keyCode == (int)EVRButtonId.k_EButton_ApplicationMenu)
                            {
                                if (config.isOculus)
                                    config.keyName += config.isLeft ? "Ybutton" : "Bbutton";
                                else
                                    config.keyName += "Menu";
                            }
                            else if (config.keyCode == (int)EVRButtonId.k_EButton_Grip)
                            {
                                config.keyName += "Grip";
                            }
                            else if (config.keyCode == (int)EVRButtonId.k_EButton_SteamVR_Touchpad)
                            {
                                if (config.isOculus)
                                    config.keyName += "ThumbStick";
                                else
                                    config.keyName += "Trackpad";
                            }
                            else if (config.keyCode == (int)EVRButtonId.k_EButton_SteamVR_Trigger)
                            {
                                config.keyName += "Trigger";
                            }
                            else if (config.keyCode == (int)EVRButtonId.k_EButton_A)
                            {
                                config.keyName += config.isLeft ? "Xbutton" : "Abutton";
                            }
                        }
                        else
                        {//タッチパッド分割ボタン
                            if (config.isOculus)
                                config.keyName = "TouchThumbStick";
                            else
                                config.keyName = "TouchTrackpad";
                            config.isTouch = true;
                        }
                        config.keyCode = -2;
                    }
                }
            }
        }
    }

    public enum Functions
    {
        ShowControlPanel = 0,
        ColorGreen = 1,
        ColorBlue = 2,
        ColorWhite = 3,
        ColorCustom = 4,
        ColorTransparent = 5,
        FrontCamera = 6,
        BackCamera = 7,
        FreeCamera = 8,
        PositionFixedCamera = 9,
        PauseTracking = 10,
        ShowCalibrationWindow = 11,
        ShowPhotoWindow = 12,
    }

    public enum Hands
    {
        Left,
        Right,
        Both
    }

    public enum EVRButtonId
    {
        k_EButton_System = 0,
        k_EButton_ApplicationMenu = 1,
        k_EButton_Grip = 2,
        k_EButton_DPad_Left = 3,
        k_EButton_DPad_Up = 4,
        k_EButton_DPad_Right = 5,
        k_EButton_DPad_Down = 6,
        k_EButton_A = 7,
        k_EButton_ProximitySensor = 31,
        k_EButton_Axis0 = 32,
        k_EButton_Axis1 = 33,
        k_EButton_Axis2 = 34,
        k_EButton_Axis3 = 35,
        k_EButton_Axis4 = 36,
        k_EButton_SteamVR_Touchpad = 32,
        k_EButton_SteamVR_Trigger = 33,
        //k_EButton_Dashboard_Back = 2,
        k_EButton_Max = 64,
    }

    public class VRMData
    {
        public string FilePath { get; set; }

        public string ExporterVersion { get; set; }

        // Info
        public string Title { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string ContactInformation { get; set; }
        public string Reference { get; set; }
        public byte[] ThumbnailPNGBytes { get; set; }

        // Permission
        public AllowedUser AllowedUser { get; set; }
        public UssageLicense ViolentUssage { get; set; }
        public UssageLicense SexualUssage { get; set; }
        public UssageLicense CommercialUssage { get; set; }
        public string OtherPermissionUrl { get; set; }

        // Distribution License
        public LicenseType LicenseType { get; set; }
        public string OtherLicenseUrl { get; set; }
    }

    public enum AllowedUser
    {
        OnlyAuthor,
        ExplicitlyLicensedPerson,
        Everyone,
    }

    public enum LicenseType
    {
        Redistribution_Prohibited,
        CC0,
        CC_BY,
        CC_BY_NC,
        CC_BY_SA,
        CC_BY_NC_SA,
        CC_BY_ND,
        CC_BY_NC_ND,
        Other
    }

    public enum UssageLicense
    {
        Disallow,
        Allow,
    }

}
