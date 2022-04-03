//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class DeviceInfoDebugger : MonoBehaviour
    {
        public bool globalEnable = true;
        public bool hmdEnable = true;
        public bool controllerEnable = true;
        public bool trackerEnable = true;

        void Start()
        {

        }

        void Update()
        {
            DeviceInfo.globalEnable = globalEnable;
            DeviceInfo.hmdEnable = hmdEnable;
            DeviceInfo.controllerEnable = controllerEnable;
            DeviceInfo.trackerEnable = trackerEnable;
        }
    }
}