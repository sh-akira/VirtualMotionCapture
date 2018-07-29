using System;
using System.Collections;
using System.Collections.Generic;
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

        public Dictionary<ETrackedDeviceClass, List<SteamVR_Utils.RigidTransform>> GetTrackerPositions()
        {
            var positions = new Dictionary<ETrackedDeviceClass, List<SteamVR_Utils.RigidTransform>>();
            positions.Add(ETrackedDeviceClass.HMD, new List<SteamVR_Utils.RigidTransform>());
            positions.Add(ETrackedDeviceClass.Controller, new List<SteamVR_Utils.RigidTransform>());
            positions.Add(ETrackedDeviceClass.GenericTracker, new List<SteamVR_Utils.RigidTransform>());
            TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            //TODO: TrackingUniverseStanding??
            openVR.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, allPoses);
            for (uint i = 0; i < allPoses.Length; i++)
            {
                var pose = allPoses[i];
                //0:HMD 1:LeftHand 2:RightHand ??
                var deviceClass = openVR.GetTrackedDeviceClass(i);
                if (pose.bDeviceIsConnected && (deviceClass == ETrackedDeviceClass.HMD || deviceClass == ETrackedDeviceClass.Controller || deviceClass == ETrackedDeviceClass.GenericTracker))
                {
                    positions[deviceClass].Add(new SteamVR_Utils.RigidTransform(pose.mDeviceToAbsoluteTracking));
                }
            }
            return positions;
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