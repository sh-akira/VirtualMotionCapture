using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRSDK.Avatar.Tracking
{
    public class CalibrationData
    {
        public ulong pelvisId;
        public Vector3 pelvisOffset;
        public Vector3 pelvisRotation;
        public ulong leftFootId;
        public Vector3 leftFootOffset;
        public Vector3 leftFootRotation;
        public ulong rightFootId;
        public Vector3 rightFootOffset;
        public Vector3 rightFootRotation;
        public bool pelvisEnable;
        public bool footEnable;
    }
}
