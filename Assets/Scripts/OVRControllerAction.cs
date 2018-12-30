using sh_akira;
using sh_akira.OVRTracking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

public class OVRControllerAction : MonoBehaviour
{
   // public InputField inputField;

    public EventHandler<OVRKeyEventArgs> KeyDownEvent;
    public EventHandler<OVRKeyEventArgs> KeyUpEvent;
    public EventHandler<OVRKeyEventArgs> AxisChangedEvent;

    // Use this for initialization
    void Start()
    {
        OpenVRWrapper.Instance.OnOVREvent += Instance_OnOVREvent;

    }

    private void Instance_OnOVREvent(object sender, OVREventArgs e)
    {

        var l = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Leftmost);
        var r = SteamVR_Controller.GetDeviceIndex(SteamVR_Controller.DeviceRelation.Rightmost);

        Debug.Log($"{((EVREventType)e.pEvent.eventType)}|device:{e.pEvent.trackedDeviceIndex} data:{Json.Serializer.Serialize(e.pEvent.data)}");
        switch ((EVREventType)e.pEvent.eventType)
        {
            case EVREventType.VREvent_None: break;
            case EVREventType.VREvent_TrackedDeviceActivated: break;
            case EVREventType.VREvent_TrackedDeviceDeactivated: break;
            case EVREventType.VREvent_TrackedDeviceUpdated: break;
            case EVREventType.VREvent_TrackedDeviceUserInteractionStarted: break;
            case EVREventType.VREvent_TrackedDeviceUserInteractionEnded: break;
            case EVREventType.VREvent_IpdChanged: break;
            case EVREventType.VREvent_EnterStandbyMode: break;
            case EVREventType.VREvent_LeaveStandbyMode: break;
            case EVREventType.VREvent_TrackedDeviceRoleChanged: break;
            case EVREventType.VREvent_WatchdogWakeUpRequested: break;
            case EVREventType.VREvent_LensDistortionChanged: break;
            case EVREventType.VREvent_PropertyChanged: break;
            case EVREventType.VREvent_WirelessDisconnect: break;
            case EVREventType.VREvent_WirelessReconnect: break;
            case EVREventType.VREvent_ButtonPress: break;
            case EVREventType.VREvent_ButtonUnpress: break;
            case EVREventType.VREvent_ButtonTouch: break;
            case EVREventType.VREvent_ButtonUntouch: break;
            case EVREventType.VREvent_DualAnalog_Press: break;
            case EVREventType.VREvent_DualAnalog_Unpress: break;
            case EVREventType.VREvent_DualAnalog_Touch: break;
            case EVREventType.VREvent_DualAnalog_Untouch: break;
            case EVREventType.VREvent_DualAnalog_Move: break;
            case EVREventType.VREvent_DualAnalog_ModeSwitch1: break;
            case EVREventType.VREvent_DualAnalog_ModeSwitch2: break;
            case EVREventType.VREvent_DualAnalog_Cancel: break;
            case EVREventType.VREvent_MouseMove: break;
            case EVREventType.VREvent_MouseButtonDown: break;
            case EVREventType.VREvent_MouseButtonUp: break;
            case EVREventType.VREvent_FocusEnter: break;
            case EVREventType.VREvent_FocusLeave: break;
            case EVREventType.VREvent_Scroll: break;
            case EVREventType.VREvent_TouchPadMove: break;
            case EVREventType.VREvent_OverlayFocusChanged: break;
            case EVREventType.VREvent_InputFocusCaptured: break;
            case EVREventType.VREvent_InputFocusReleased: break;
            case EVREventType.VREvent_SceneFocusLost: break;
            case EVREventType.VREvent_SceneFocusGained: break;
            case EVREventType.VREvent_SceneApplicationChanged: break;
            case EVREventType.VREvent_SceneFocusChanged: break;
            case EVREventType.VREvent_InputFocusChanged: break;
            case EVREventType.VREvent_SceneApplicationSecondaryRenderingStarted: break;
            case EVREventType.VREvent_HideRenderModels: break;
            case EVREventType.VREvent_ShowRenderModels: break;
            case EVREventType.VREvent_ConsoleOpened: break;
            case EVREventType.VREvent_ConsoleClosed: break;
            case EVREventType.VREvent_OverlayShown: break;
            case EVREventType.VREvent_OverlayHidden: break;
            case EVREventType.VREvent_DashboardActivated: break;
            case EVREventType.VREvent_DashboardDeactivated: break;
            case EVREventType.VREvent_DashboardThumbSelected: break;
            case EVREventType.VREvent_DashboardRequested: break;
            case EVREventType.VREvent_ResetDashboard: break;
            case EVREventType.VREvent_RenderToast: break;
            case EVREventType.VREvent_ImageLoaded: break;
            case EVREventType.VREvent_ShowKeyboard: break;
            case EVREventType.VREvent_HideKeyboard: break;
            case EVREventType.VREvent_OverlayGamepadFocusGained: break;
            case EVREventType.VREvent_OverlayGamepadFocusLost: break;
            case EVREventType.VREvent_OverlaySharedTextureChanged: break;
            //case EVREventType.VREvent_DashboardGuideButtonDown: break;
            //case EVREventType.VREvent_DashboardGuideButtonUp: break;
            case EVREventType.VREvent_ScreenshotTriggered: break;
            case EVREventType.VREvent_ImageFailed: break;
            case EVREventType.VREvent_DashboardOverlayCreated: break;
            case EVREventType.VREvent_RequestScreenshot: break;
            case EVREventType.VREvent_ScreenshotTaken: break;
            case EVREventType.VREvent_ScreenshotFailed: break;
            case EVREventType.VREvent_SubmitScreenshotToDashboard: break;
            case EVREventType.VREvent_ScreenshotProgressToDashboard: break;
            case EVREventType.VREvent_PrimaryDashboardDeviceChanged: break;
            case EVREventType.VREvent_Notification_Shown: break;
            case EVREventType.VREvent_Notification_Hidden: break;
            case EVREventType.VREvent_Notification_BeginInteraction: break;
            case EVREventType.VREvent_Notification_Destroyed: break;
            case EVREventType.VREvent_Quit: break;
            case EVREventType.VREvent_ProcessQuit: break;
            case EVREventType.VREvent_QuitAborted_UserPrompt: break;
            case EVREventType.VREvent_QuitAcknowledged: break;
            case EVREventType.VREvent_DriverRequestedQuit: break;
            case EVREventType.VREvent_ChaperoneDataHasChanged: break;
            case EVREventType.VREvent_ChaperoneUniverseHasChanged: break;
            case EVREventType.VREvent_ChaperoneTempDataHasChanged: break;
            case EVREventType.VREvent_ChaperoneSettingsHaveChanged: break;
            case EVREventType.VREvent_SeatedZeroPoseReset: break;
            case EVREventType.VREvent_AudioSettingsHaveChanged: break;
            case EVREventType.VREvent_BackgroundSettingHasChanged: break;
            case EVREventType.VREvent_CameraSettingsHaveChanged: break;
            case EVREventType.VREvent_ReprojectionSettingHasChanged: break;
            case EVREventType.VREvent_ModelSkinSettingsHaveChanged: break;
            case EVREventType.VREvent_EnvironmentSettingsHaveChanged: break;
            case EVREventType.VREvent_PowerSettingsHaveChanged: break;
            case EVREventType.VREvent_EnableHomeAppSettingsHaveChanged: break;
            case EVREventType.VREvent_SteamVRSectionSettingChanged: break;
            case EVREventType.VREvent_LighthouseSectionSettingChanged: break;
            case EVREventType.VREvent_NullSectionSettingChanged: break;
            case EVREventType.VREvent_UserInterfaceSectionSettingChanged: break;
            case EVREventType.VREvent_NotificationsSectionSettingChanged: break;
            case EVREventType.VREvent_KeyboardSectionSettingChanged: break;
            case EVREventType.VREvent_PerfSectionSettingChanged: break;
            case EVREventType.VREvent_DashboardSectionSettingChanged: break;
            case EVREventType.VREvent_WebInterfaceSectionSettingChanged: break;
            case EVREventType.VREvent_StatusUpdate: break;
            case EVREventType.VREvent_WebInterface_InstallDriverCompleted: break;
            case EVREventType.VREvent_MCImageUpdated: break;
            case EVREventType.VREvent_FirmwareUpdateStarted: break;
            case EVREventType.VREvent_FirmwareUpdateFinished: break;
            case EVREventType.VREvent_KeyboardClosed: break;
            case EVREventType.VREvent_KeyboardCharInput: break;
            case EVREventType.VREvent_KeyboardDone: break;
            case EVREventType.VREvent_ApplicationTransitionStarted: break;
            case EVREventType.VREvent_ApplicationTransitionAborted: break;
            case EVREventType.VREvent_ApplicationTransitionNewAppStarted: break;
            case EVREventType.VREvent_ApplicationListUpdated: break;
            case EVREventType.VREvent_ApplicationMimeTypeLoad: break;
            case EVREventType.VREvent_ApplicationTransitionNewAppLaunchComplete: break;
            case EVREventType.VREvent_ProcessConnected: break;
            case EVREventType.VREvent_ProcessDisconnected: break;
            case EVREventType.VREvent_Compositor_MirrorWindowShown: break;
            case EVREventType.VREvent_Compositor_MirrorWindowHidden: break;
            case EVREventType.VREvent_Compositor_ChaperoneBoundsShown: break;
            case EVREventType.VREvent_Compositor_ChaperoneBoundsHidden: break;
            case EVREventType.VREvent_TrackedCamera_StartVideoStream: break;
            case EVREventType.VREvent_TrackedCamera_StopVideoStream: break;
            case EVREventType.VREvent_TrackedCamera_PauseVideoStream: break;
            case EVREventType.VREvent_TrackedCamera_ResumeVideoStream: break;
            case EVREventType.VREvent_TrackedCamera_EditingSurface: break;
            case EVREventType.VREvent_PerformanceTest_EnableCapture: break;
            case EVREventType.VREvent_PerformanceTest_DisableCapture: break;
            case EVREventType.VREvent_PerformanceTest_FidelityLevel: break;
            case EVREventType.VREvent_MessageOverlay_Closed: break;
            case EVREventType.VREvent_MessageOverlayCloseRequested: break;
            case EVREventType.VREvent_Input_HapticVibration: break;
            case EVREventType.VREvent_VendorSpecific_Reserved_Start: break;
            case EVREventType.VREvent_VendorSpecific_Reserved_End: break;
        }
    }

    EVRButtonId[] buttonIds_vive = new EVRButtonId[] {
        EVRButtonId.k_EButton_ApplicationMenu,
        EVRButtonId.k_EButton_Grip,
        EVRButtonId.k_EButton_SteamVR_Touchpad,
        //EVRButtonId.k_EButton_SteamVR_Trigger
    };

    EVRButtonId[] axisIds_vive = new EVRButtonId[] {
        EVRButtonId.k_EButton_SteamVR_Touchpad,
        //EVRButtonId.k_EButton_SteamVR_Trigger
    };

    EVRButtonId[] buttonIds_oculus = new EVRButtonId[] {
        EVRButtonId.k_EButton_SteamVR_Trigger, //人差し指トリガー
        EVRButtonId.k_EButton_SteamVR_Touchpad, //スティック
        EVRButtonId.k_EButton_Grip, //中指トリガー
        EVRButtonId.k_EButton_A, //A/Xボタン
        EVRButtonId.k_EButton_ApplicationMenu, //B/Yボタン
    };

    EVRButtonId[] axisIds_oculus = new EVRButtonId[] {
        EVRButtonId.k_EButton_SteamVR_Trigger, //人差し指トリガー
        EVRButtonId.k_EButton_SteamVR_Touchpad, //スティック
        EVRButtonId.k_EButton_A, //A/Xボタン
        EVRButtonId.k_EButton_ApplicationMenu, //B/Yボタン
    };

    public bool IsOculus
    {
        get
        {
            if (OpenVRWrapper.Instance.openVR == null)
            {
                return false;
            }
            var deviceId = OpenVR.k_unTrackedDeviceIndex_Hmd;
            var prop = ETrackedDeviceProperty.Prop_TrackingSystemName_String;
            var error = ETrackedPropertyError.TrackedProp_Success;
            var capactiy = OpenVRWrapper.Instance.openVR.GetStringTrackedDeviceProperty(deviceId, prop, null, 0, ref error);
            var name = "";
            if (capactiy > 1)
            {
                var result = new System.Text.StringBuilder((int)capactiy);
                OpenVRWrapper.Instance.openVR.GetStringTrackedDeviceProperty(deviceId, prop, result, capactiy, ref error);
                name = result.ToString();
            }
            name = (error != ETrackedPropertyError.TrackedProp_Success) ? error.ToString() : "<unknown>";
            return name.ToLower().Contains("oculus");
        }
    }

    void Update()
    {
        if (OpenVRWrapper.Instance.openVR == null)
        {
            OpenVRWrapper.Instance.Setup();
        }
        else
        {
            var leftid = OpenVRWrapper.Instance.openVR.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            var rightid = OpenVRWrapper.Instance.openVR.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);

            if (leftid != OpenVR.k_unTrackedDeviceIndexInvalid) CheckControllerStatus((int)leftid, true);
            if (rightid != OpenVR.k_unTrackedDeviceIndexInvalid) CheckControllerStatus((int)rightid, false);
        }
    }

    private Dictionary<int, Dictionary<EVRButtonId, Vector2>> LastAxis = new Dictionary<int, Dictionary<EVRButtonId, Vector2>>();

    private void CheckControllerStatus(int index, bool isLeft)
    {
        var name = isLeft ? " Left:" : "Right:";
        /*
        foreach (var buttonId in buttonIds_oculus)
        {
            if (SteamVR_Controller.Input(index).GetPressDown(buttonId))
            {
                inputField.text = (name + buttonId + " GetPressDown") + "\n" + inputField.text;
            }
            if (SteamVR_Controller.Input(index).GetPressUp(buttonId))
            {
                inputField.text = (name + buttonId + " GetPressUp") + "\n" + inputField.text;
            }
        }
        if (SteamVR_Controller.Input(index).GetHairTriggerDown())
        {
            inputField.text = (name + EVRButtonId.k_EButton_SteamVR_Trigger + " GetHairTriggerDown") + "\n" + inputField.text;
        }

        if (SteamVR_Controller.Input(index).GetHairTriggerUp())
        {
            inputField.text = (name + EVRButtonId.k_EButton_SteamVR_Trigger + " GetHairTriggerUp") + "\n" + inputField.text;
        }

        foreach (var buttonId in axisIds_oculus)
        {
            if (SteamVR_Controller.Input(index).GetTouchDown(buttonId))
            {
                var axis = SteamVR_Controller.Input(index).GetAxis(buttonId);
                inputField.text = (name + buttonId + " GetTouchDown axis: " + axis) + "\n" + inputField.text;
            }
            if (SteamVR_Controller.Input(index).GetTouchUp(buttonId))
            {
                var axis = SteamVR_Controller.Input(index).GetAxis(buttonId);
                inputField.text = (name + buttonId + " GetTouchUp axis: " + axis) + "\n" + inputField.text;
            }
            if (SteamVR_Controller.Input(index).GetTouch(buttonId))
            {
                var axis = SteamVR_Controller.Input(index).GetAxis(buttonId);
                inputField.text = (name + buttonId + " GetTouch axis: " + axis) + "\n" + inputField.text;
            }
        }
        */

        var buttonIds = IsOculus ? buttonIds_oculus : buttonIds_vive;
        var axisIds = IsOculus ? axisIds_oculus : axisIds_vive;
        foreach (var buttonId in buttonIds)
        {
            if (SteamVR_Controller.Input(index).GetPressDown(buttonId))
            {
                Debug.Log(name + buttonId + " Press down");
                KeyDownEvent?.Invoke(this, new OVRKeyEventArgs(buttonId, Vector2.zero, isLeft, false, false));
            }
            if (SteamVR_Controller.Input(index).GetPressUp(buttonId))
            {
                Debug.Log(name + buttonId + " Press up");
                KeyUpEvent?.Invoke(this, new OVRKeyEventArgs(buttonId, Vector2.zero, isLeft, false, false));
            }
            //if (SteamVR_Controller.Input(index).GetPress(buttonId))
            //    Debug.Log(name + buttonId);
        }

        if (SteamVR_Controller.Input(index).GetHairTriggerDown())
        {
            Debug.Log(name + EVRButtonId.k_EButton_SteamVR_Trigger + " Press down");
            KeyDownEvent?.Invoke(this, new OVRKeyEventArgs(EVRButtonId.k_EButton_SteamVR_Trigger, Vector2.zero, isLeft, false, false));
        }

        if (SteamVR_Controller.Input(index).GetHairTriggerUp())
        {
            Debug.Log(name + EVRButtonId.k_EButton_SteamVR_Trigger + " Press down");
            KeyUpEvent?.Invoke(this, new OVRKeyEventArgs(EVRButtonId.k_EButton_SteamVR_Trigger, Vector2.zero, isLeft, false, false));
        }

        if (LastAxis.ContainsKey(index) == false) LastAxis.Add(index, new Dictionary<EVRButtonId, Vector2>());

        foreach (var buttonId in axisIds)
        {
            if (SteamVR_Controller.Input(index).GetTouchDown(buttonId))
            {
                Debug.Log(name + buttonId + " touch down");
                var axis = SteamVR_Controller.Input(index).GetAxis(buttonId);
                Debug.Log(name + "axis: " + axis);
                KeyDownEvent?.Invoke(this, new OVRKeyEventArgs(buttonId, axis, isLeft, !IsOculus, IsOculus));
            }
            if (SteamVR_Controller.Input(index).GetTouchUp(buttonId))
            {
                Debug.Log(name + buttonId + " touch up");
                var axis = SteamVR_Controller.Input(index).GetAxis(buttonId);
                if (LastAxis[index].ContainsKey(buttonId) == false) LastAxis[index].Add(buttonId, axis);
                else axis = LastAxis[index][buttonId];
                Debug.Log(name + "axis: " + axis);
                KeyUpEvent?.Invoke(this, new OVRKeyEventArgs(buttonId, axis, isLeft, !IsOculus, IsOculus));
            }
            if (buttonId == EVRButtonId.k_EButton_SteamVR_Touchpad)
            {
                if (SteamVR_Controller.Input(index).GetTouch(buttonId))
                {
                    var axis = SteamVR_Controller.Input(index).GetAxis(buttonId);
                    if (LastAxis[index].ContainsKey(buttonId) == false) LastAxis[index].Add(buttonId, axis);
                    else LastAxis[index][buttonId] = axis;
                    //Debug.Log(name + "axis: " + axis);
                    AxisChangedEvent?.Invoke(this, new OVRKeyEventArgs(buttonId, axis, isLeft, true, false));
                }
            }
        }
    }
}

public class OVRKeyEventArgs : EventArgs
{
    public EVRButtonId ButtonId { get; }
    public Vector2 Axis { get; }
    public bool IsLeft { get; }
    public bool IsAxis { get; }
    public bool IsTouch { get; }

    public OVRKeyEventArgs(EVRButtonId buttonId, Vector2 axis, bool isLeft, bool isAxis, bool isTouch) : base()
    {
        ButtonId = buttonId; Axis = axis; IsLeft = isLeft; IsAxis = isAxis; IsTouch = isTouch;
    }
}