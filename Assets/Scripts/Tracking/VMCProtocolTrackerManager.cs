using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

namespace VMC
{
    public class VMCProtocolTrackerManager : MonoBehaviour
    {
        public ControlWPFWindow controlWPFWindow;
        private ExternalReceiverForVMC[] externalReceivers => controlWPFWindow.externalMotionReceivers;

        private Dictionary<string, DeviceInfo> allDeviceInfo = new Dictionary<string, DeviceInfo>();

        private void Update()
        {
            if (externalReceivers != null)
            {
                foreach (var externalReceiver in externalReceivers)
                {
                    foreach (var c in externalReceiver.virtualHmdFiltered)
                    {
                        UpdateDeviceInfo(c.Key, c.Value, ETrackedDeviceClass.HMD);
                    }
                    foreach (var c in externalReceiver.virtualControllerFiltered)
                    {
                        UpdateDeviceInfo(c.Key, c.Value, ETrackedDeviceClass.Controller);
                    }
                    foreach (var t in externalReceiver.virtualTrackerFiltered)
                    {
                        UpdateDeviceInfo(t.Key, t.Value, ETrackedDeviceClass.GenericTracker);
                    }
                }
            }
        }

        private void UpdateDeviceInfo(string name, SteamVR_Utils.RigidTransform transform, ETrackedDeviceClass deviceClass)
        {
            if (allDeviceInfo.TryGetValue(name, out var deviceInfo) == false)
            {
                deviceInfo = new DeviceInfo();
                allDeviceInfo[name] = deviceInfo;
            }
            deviceInfo.UpdateDeviceInfo(transform, name);

            TrackingPointManager.Instance.ApplyPoint(name, deviceClass, deviceInfo.transform.pos, deviceInfo.transform.rot, deviceInfo.isOK);
        }
    }
}