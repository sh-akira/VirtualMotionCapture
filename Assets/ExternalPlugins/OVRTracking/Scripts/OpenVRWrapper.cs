using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace sh_akira.OVRTracking
{
    public class OpenVRWrapper : IDisposable
    {
        private static OpenVRWrapper instance;
        public static OpenVRWrapper Instance
        {
            get
            {
                if (instance == null) instance = new OpenVRWrapper();
                return instance;
            }
        }

        public event EventHandler<OVRConnectedEventArgs> OnOVRConnected;
        public event EventHandler<OVREventArgs> OnOVREvent;

        public CVRSystem openVR { get; set; } = null;
        public bool ConvertControllerToTracker = false;

        public bool Setup()
        {
            var error = EVRInitError.None;
            openVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

            if (error != EVRInitError.None)
            { //Error Init OpenVR
                Close();
                return false;
            }

            OnOVRConnected?.Invoke(this, new OVRConnectedEventArgs(true));

            return true;
        }

        public void PollingVREvents()
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
                            OnOVRConnected?.Invoke(this, new OVRConnectedEventArgs(false));
                            break;
                            //ほかにもイベントはいろいろある
                    }

                    OnOVREvent?.Invoke(this, new OVREventArgs(pEvent));
                }
            }
        }

        private string[] serialNumbers = null;
        private bool[] trackedHistory = null;

        public Dictionary<ETrackedDeviceClass, List<DeviceInfo>> GetTrackerPositions()
        {
            var positions = new Dictionary<ETrackedDeviceClass, List<DeviceInfo>>();

            if (openVR == null)
            {
                return positions;
            }

            positions.Add(ETrackedDeviceClass.HMD, new List<DeviceInfo>());
            positions.Add(ETrackedDeviceClass.Controller, new List<DeviceInfo>());
            positions.Add(ETrackedDeviceClass.GenericTracker, new List<DeviceInfo>());
            positions.Add(ETrackedDeviceClass.TrackingReference, new List<DeviceInfo>());
            TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            if (serialNumbers == null) serialNumbers = new string[OpenVR.k_unMaxTrackedDeviceCount];
            if (trackedHistory == null) trackedHistory = new bool[OpenVR.k_unMaxTrackedDeviceCount];
            
            //TODO: TrackingUniverseStanding??
            openVR.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, allPoses);
            for (uint i = 0; i < allPoses.Length; i++)
            {
                string postfix = "";
                var pose = allPoses[i];
                //0:HMD 1:LeftHand 2:RightHand ??
                var deviceClass = openVR.GetTrackedDeviceClass(i);
                if (pose.bDeviceIsConnected && (deviceClass == ETrackedDeviceClass.HMD || deviceClass == ETrackedDeviceClass.Controller || deviceClass == ETrackedDeviceClass.GenericTracker || deviceClass == ETrackedDeviceClass.TrackingReference))
                {
                    //過去一度でもトラッキングしたことがなく、現在も有効でない場合
                    if (trackedHistory[i] == false && pose.bPoseIsValid == false)
                    {
                        //無視する
                        continue;
                    }
                    trackedHistory[i] = true;

                    if (serialNumbers[i] == null)
                    {
                        serialNumbers[i] = GetTrackerSerialNumber(i);
                    }

                    //コントローラをトラッカーとして認識させるモード
                    if ((ConvertControllerToTracker == true) && (deviceClass == ETrackedDeviceClass.Controller))
                    {
                        deviceClass = ETrackedDeviceClass.GenericTracker;
                        postfix = "[Controller]"; //シリアルナンバー重複防止
                    }

                    positions[deviceClass].Add(new DeviceInfo(new SteamVR_Utils.RigidTransform(pose.mDeviceToAbsoluteTracking), serialNumbers[i] + postfix, pose, deviceClass));
                }
                else {
                    //接続切れたらシリアル番号キャッシュクリア
                    serialNumbers[i] = null;
                    //トラッキングもしてない
                    trackedHistory[i] = false;
                }
            }
            return positions;
        }

        public string GetTrackerSerialNumber(uint deviceIndex)
        {
            if (openVR == null)
            {
                return null;
            }

            var buffer = new StringBuilder();
            var error = default(ETrackedPropertyError);
            //Capacity取得
            var capacity = (int)openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, null, 0, ref error);
            if (capacity < 1) return null;// "No Serial Number";
            openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, buffer, (uint)buffer.EnsureCapacity(capacity), ref error);
            if (error != ETrackedPropertyError.TrackedProp_Success) return null;// "No Serial Number";
            return buffer.ToString();
        }

        /*
        //今まで一度もトラッキングされたことがないかどうかをチェックする(True=全く位置情報がない, False=過去一度でも位置情報がある)
        public bool GetNeverTracked(uint deviceIndex)
        {
            if (openVR == null)
            {
                return true;
            }

            var error = default(ETrackedPropertyError);
            //Prop_NeverTracked_Bool取得
            bool NeverTracked = openVR.GetBoolTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_NeverTracked_Bool, ref error);
            if (error != ETrackedPropertyError.TrackedProp_Success) return true;
            return NeverTracked;
        }
        */

        public bool GetIsSafeMode()
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
                Debug.LogError("GetIsSafeMode Failed: "+eVRSettingsError.ToString());
                return false;
            }
            return en;
        }

        public void Close()
        {
            openVR = null;
            OpenVR.Shutdown();
        }

        ~OpenVRWrapper()
        {
            Dispose();
        }

        public void Dispose()
        {
            Close();
            instance = null;
        }

    }
}