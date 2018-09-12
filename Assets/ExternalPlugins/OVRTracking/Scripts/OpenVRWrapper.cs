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

        public bool Setup()
        {
            var error = EVRInitError.None;
            openVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

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

        public Dictionary<ETrackedDeviceClass, List<KeyValuePair<SteamVR_Utils.RigidTransform, string>>> GetTrackerPositions()
        {
            var positions = new Dictionary<ETrackedDeviceClass, List<KeyValuePair<SteamVR_Utils.RigidTransform, string>>>();
            positions.Add(ETrackedDeviceClass.HMD, new List<KeyValuePair<SteamVR_Utils.RigidTransform, string>>());
            positions.Add(ETrackedDeviceClass.Controller, new List<KeyValuePair<SteamVR_Utils.RigidTransform, string>>());
            positions.Add(ETrackedDeviceClass.GenericTracker, new List<KeyValuePair<SteamVR_Utils.RigidTransform, string>>());
            positions.Add(ETrackedDeviceClass.TrackingReference, new List<KeyValuePair<SteamVR_Utils.RigidTransform, string>>());
            TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            if (serialNumbers == null) serialNumbers = new string[OpenVR.k_unMaxTrackedDeviceCount];
            //TODO: TrackingUniverseStanding??
            openVR.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, allPoses);
            for (uint i = 0; i < allPoses.Length; i++)
            {
                var pose = allPoses[i];
                //0:HMD 1:LeftHand 2:RightHand ??
                var deviceClass = openVR.GetTrackedDeviceClass(i);
                if (pose.bDeviceIsConnected && (deviceClass == ETrackedDeviceClass.HMD || deviceClass == ETrackedDeviceClass.Controller || deviceClass == ETrackedDeviceClass.GenericTracker || deviceClass == ETrackedDeviceClass.TrackingReference))
                {
                    if (serialNumbers[i] == null)
                    {
                        serialNumbers[i] = GetTrackerSerialNumber(i);
                    }
                    positions[deviceClass].Add(new KeyValuePair<SteamVR_Utils.RigidTransform, string>(new SteamVR_Utils.RigidTransform(pose.mDeviceToAbsoluteTracking), serialNumbers[i]));
                }
            }
            return positions;
        }

        public string GetTrackerSerialNumber(uint deviceIndex)
        {
            var buffer = new StringBuilder();
            var error = default(ETrackedPropertyError);
            //Capacity取得
            var capacity = (int)openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, null, 0, ref error);
            if (capacity < 1) return null;// "No Serial Number";
            openVR.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, buffer, (uint)buffer.EnsureCapacity(capacity), ref error);
            if (error != ETrackedPropertyError.TrackedProp_Success) return null;// "No Serial Number";
            return buffer.ToString();
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