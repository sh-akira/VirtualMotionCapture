using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace VMC
{
    public class ScalePositionOffset : MonoBehaviour
    {

        private Transform LeftHandTarget = null;
        private Transform RightHandTarget = null;
        private Transform ScaledLeftHandTarget = null;
        private Transform ScaledRightHandTarget = null;

        private ScalePositionOffset DirectPosition;

        private Vector3 StartPosition;

        private void Start()
        {
            StartPosition = transform.position;
        }

        public void SetTargets(Transform leftHand, Transform rightHand, Transform scaledLeftHand, Transform scaledRightHand)
        {
            LeftHandTarget = leftHand;
            RightHandTarget = rightHand;
            ScaledLeftHandTarget = scaledLeftHand;
            ScaledRightHandTarget = scaledRightHand;
        }

        public void SetDirectPosition(ScalePositionOffset directPosition)
        {
            DirectPosition = directPosition;
        }

        public void ResetTargetAndPosition()
        {
            LeftHandTarget = null;
            RightHandTarget = null;
            ScaledLeftHandTarget = null;
            ScaledRightHandTarget = null;
            DirectPosition = null;
            transform.position = StartPosition;
        }

        // Update is called once per frame
        private void Update()
        {
            return;
            if (DirectPosition != null)
            {
                transform.position = DirectPosition.transform.position;
            }
            else
            {
                if (LeftHandTarget != null)
                {
                    //var realPosition = Vector3.Lerp(LeftHandTarget.position, RightHandTarget.position, 0.5f);
                    //var scaledPosition = Vector3.Lerp(ScaledLeftHandTarget.position, ScaledRightHandTarget.position, 0.5f);
                    //var offset = realPosition - scaledPosition;
                    //transform.position += new Vector3(offset.x, 0, offset.z);

                    var realx = (decimal)LeftHandTarget.position.x + ((decimal)RightHandTarget.position.x - (decimal)LeftHandTarget.position.x) * 0.5m;
                    var realz = (decimal)LeftHandTarget.position.z + ((decimal)RightHandTarget.position.z - (decimal)LeftHandTarget.position.z) * 0.5m;
                    var scaledx = (decimal)ScaledLeftHandTarget.position.x + ((decimal)ScaledRightHandTarget.position.x - (decimal)ScaledLeftHandTarget.position.x) * 0.5m;
                    var scaledz = (decimal)ScaledLeftHandTarget.position.z + ((decimal)ScaledRightHandTarget.position.z - (decimal)ScaledLeftHandTarget.position.z) * 0.5m;

                    transform.position = new Vector3((float)((decimal)transform.position.x + realx - scaledx), transform.position.y, (float)((decimal)transform.position.z + realz - scaledz));
                }
            }
        }
    }
}