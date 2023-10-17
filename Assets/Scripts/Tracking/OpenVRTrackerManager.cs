using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace VMC
{
    public class OpenVRTrackerManager : MonoBehaviour
    {
        public static OpenVRTrackerManager Instance;

        public CVRSystem openVR { get; set; } = null;

        private bool isOVRConnected = false;

        public Action OpenVREventAction = null;
        public bool isDashboardActivated = false;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Setup();
        }

        private void OnDestroy()
        {
            Close();
        }

        private bool Setup()
        {
            CommonSettings.Load();
            if (CommonSettings.Current.LaunchSteamVROnStartup == false) return false;

            var error = EVRInitError.None;
            openVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

            if (error == EVRInitError.Init_HmdNotFound)
            {
                Close();
                //HMD require fallback
                openVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
            }

            if (error != EVRInitError.None)
            { //Error Init OpenVR
                Close();
                System.IO.File.WriteAllText(Application.dataPath + "/../OpenVRInitError.txt", error.ToString());
                return false;
            }

            isOVRConnected = true;

            return true;
        }

        private void PollingVREvents()
        {
            if (openVR != null)
            {
                var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Valve.VR.VREvent_t));
                VREvent_t pEvent = new VREvent_t();
                while (openVR.PollNextEvent(ref pEvent, size))
                {//Receive VREvent
                    EVREventType type = (EVREventType)pEvent.eventType;
                    switch (type)
                    {
                        case EVREventType.VREvent_Quit:
                            isOVRConnected = false;
                            break;
                    }

                }
            }
        }

        /*
         * SteamVR Plugin 1.x におけるトラッカー座標取得手順
         *
         * 1. OpenVR.k_unMaxTrackedDeviceCount分の配列を用意する
         *    TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
         *
         * 2. 全数(64個分)のデータを取得する
         *    var eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;
         *    var fPredictedSecondsFromNow = framesAhead * (Time.timeScale / SteamVR.instance.hmd_DisplayFrequency);
         *    openVR.GetDeviceToAbsoluteTrackingPose(eOrigin, fPredictedSecondsFromNow, allPoses);
         *
         * 3. 全数チェックを行う
         *    for (uint index = 0; index < OpenVR.k_unMaxTrackedDeviceCount; index++) { }
         *
         * 4. デバイスの種別が欲しい場合はindexから取得する
         *    openVR.GetTrackedDeviceClass(index); //ETrackedDeviceClass.HMD, Controller, GenericTracker ...
         *
         * 5. トラッカーの電源が切れるとindexがずれて実質的にシリアルナンバーの比較が必須なためProp_SerialNumber_Stringを取得する
         *    var buffer = new StringBuilder();
         *    var error = default(ETrackedPropertyError);
         *    var capacity = (int)openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, null, 0, ref error);
         *    openVR.GetStringTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_SerialNumber_String, buffer, (uint)buffer.EnsureCapacity(capacity), ref error);
         *    return buffer.ToString();
         *
         * 6. デバイスがアクティブな時だけ生MatrixからUnity座標を取得する
         *    if (allPoses[index].bDeviceIsConnected
         *    {
         *        transform.localPosition = allPoses[index].mDeviceToAbsoluteTracking.GetPosition();
         *        transform.localRotation = allPoses[index].mDeviceToAbsoluteTracking.GetRotation();
         *    }
         *
         * 7. 以降毎Updateごとに2～6を繰り返す
         *
         */

        private ETrackingUniverseOrigin universeOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;
        private static float framesAhead = 2;

        private TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private DeviceInfo[] allDeviceInfo = new DeviceInfo[OpenVR.k_unMaxTrackedDeviceCount];
        private string[] serialNumbers = new string[OpenVR.k_unMaxTrackedDeviceCount];

        private void GetAllDevicePose()
        {

            openVR.GetDeviceToAbsoluteTrackingPose(universeOrigin, 0, allPoses);
            for (uint index = 0; index < OpenVR.k_unMaxTrackedDeviceCount; index++)
            {
                var postfix = "";
                var pose = allPoses[index];

                var deviceClass = openVR.GetTrackedDeviceClass(index);

                if (IsPositionTrackedDevice(index) && (deviceClass == ETrackedDeviceClass.HMD || deviceClass == ETrackedDeviceClass.Controller || deviceClass == ETrackedDeviceClass.GenericTracker))
                {
                    if (serialNumbers[index] == null)
                    {
                        serialNumbers[index] = GetTrackerSerialNumber(index);
                    }

                    //コントローラをトラッカーとして認識させるモード
                    if ((Settings.Current.HandleControllerAsTracker == true) && (deviceClass == ETrackedDeviceClass.Controller))
                    {
                        deviceClass = ETrackedDeviceClass.GenericTracker;
                        postfix = "[Controller]"; //シリアルナンバー重複防止
                    }

                    var name = serialNumbers[index] + postfix;

                    //トラッカー飛び対策
                    var deviceInfo = allDeviceInfo[index];
                    if (deviceInfo == null)
                    {
                        allDeviceInfo[index] = new DeviceInfo();
                        deviceInfo = allDeviceInfo[index];
                    }
                    deviceInfo.UpdateDeviceInfo(new SteamVR_Utils.RigidTransform(pose.mDeviceToAbsoluteTracking), name, pose, deviceClass);

                    TrackingPointManager.Instance.ApplyPoint(name, deviceClass, deviceInfo.transform.pos, deviceInfo.transform.rot, deviceInfo.isOK);
                }
            }
        }

        private string GetTrackerSerialNumber(uint deviceIndex)
        {
            var buffer = new StringBuilder();
            var error = default(ETrackedPropertyError);

            // Capacity取得
            var capacity = (int)openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, null, 0, ref error);
            if (capacity < 1) return null;

            // Capacity分の文字列取得
            openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, buffer, (uint)buffer.EnsureCapacity(capacity), ref error);
            if (error != ETrackedPropertyError.TrackedProp_Success) return null;

            return buffer.ToString();
        }

        private bool IsPositionTrackedDevice(uint index)
        {
            ETrackedPropertyError errorCode = ETrackedPropertyError.TrackedProp_Success;

            // ポジショントラッキング対応のデバイスはこのプロパティに対応してないが、エラー時はfalseを返してくるので問題はない
            return !openVR.GetBoolTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_NeverTracked_Bool, ref errorCode);
        }

        public bool IsSafeMode()
        {
            if (openVR == null)
            {
                return false;
            }

            CVRSettings cVRSettings = OpenVR.Settings;
            EVRSettingsError eVRSettingsError = EVRSettingsError.None;
            bool en = cVRSettings.GetBool(OpenVR.k_pch_SteamVR_Section, OpenVR.k_pch_SteamVR_EnableSafeMode, ref eVRSettingsError);

            if (eVRSettingsError != EVRSettingsError.None)
            {
                Debug.LogError("GetIsSafeMode Failed: " + eVRSettingsError.ToString());
                return false;
            }
            return en;
        }

        public bool IsDashboardVisible()
        {
            if (openVR == null)
            {
                return false;
            }

            return OpenVR.Overlay?.IsDashboardVisible() ?? false;
        }

        //コントローラ状態を調べる
        public void GetControllerSerial(out string LeftHandSerial, out string RightHandSerial)
        {
            LeftHandSerial = null;
            RightHandSerial = null;

            if (openVR == null)
            {
                return;
            }

            uint leftHandIndex = openVR.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            uint rightHandIndex = openVR.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);

            if (serialNumbers == null || (leftHandIndex == OpenVR.k_unTrackedDeviceIndexInvalid) || (rightHandIndex == OpenVR.k_unTrackedDeviceIndexInvalid))
            {
                return;
            }

            try
            {
                LeftHandSerial = serialNumbers[leftHandIndex];
                RightHandSerial = serialNumbers[rightHandIndex];
            }
            catch (IndexOutOfRangeException)
            {
                return;
            }
        }

        private void Close()
        {
            isOVRConnected = false;
            openVR = null;
            OpenVR.Shutdown();
        }

        private void Update()
        {
            if (isOVRConnected)
            {
                PollingVREvents();
                GetAllDevicePose();

                bool dashboardVisible = IsDashboardVisible();
                if (isDashboardActivated != dashboardVisible) {
                    isDashboardActivated = dashboardVisible;
                    OpenVREventAction?.Invoke();
                }
            }
        }
    }
}