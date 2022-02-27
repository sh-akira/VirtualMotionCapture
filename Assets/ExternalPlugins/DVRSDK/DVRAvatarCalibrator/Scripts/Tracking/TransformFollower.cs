using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DVRSDK.Utilities
{
    public class TransformFollower : MonoBehaviour
    {
        public Transform Target;

        public bool RotationLimitX;
        public bool RotationLimitY;
        public bool RotationLimitZ;

        void Update()
        {
            if (Target != null)
            {
                transform.position = Target.position;
                if (RotationLimitX == false && RotationLimitY == false && RotationLimitZ == false)
                {
                    transform.rotation = Target.rotation;
                }
                else
                {
                    var targetEuler = Target.rotation.eulerAngles;
                    transform.rotation = Quaternion.Euler(RotationLimitX ? 0 : targetEuler.x, RotationLimitY ? 0 : targetEuler.y, RotationLimitZ ? 0 : targetEuler.z);
                }
            }
        }
    }
}
