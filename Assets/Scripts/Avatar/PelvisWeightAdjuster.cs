using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class PelvisWeightAdjuster : MonoBehaviour
    {
        public VRIK vrik;

        private void Update()
        {
            if (vrik == null) return;

            var HeadIKTarget = vrik.solver.spine.headTarget;
            var PelvisIKTarget = vrik.solver.spine.pelvisTarget;

            if (PelvisIKTarget == null || HeadIKTarget == null) return;


            var PelvisLocalHeadUpPosition = PelvisIKTarget.InverseTransformPoint(HeadIKTarget.position + HeadIKTarget.up);

            var yzHeadUpPosition = PelvisIKTarget.TransformPoint(new Vector3(0, PelvisLocalHeadUpPosition.y, PelvisLocalHeadUpPosition.z));

            //後ろがマイナス、前がプラス。真上0～真下180度
            var signedAngle = Vector3.SignedAngle(PelvisIKTarget.up, yzHeadUpPosition - HeadIKTarget.position, PelvisIKTarget.right);

            var PelvisWeight = 1.0f;
            
            if (signedAngle < 0)
            { //後ろに曲がった時
                PelvisWeight = Mathf.Lerp(1.0f, 0.3f, (-signedAngle) / 110f);
            }
            else
            { //前に曲がった時
                PelvisWeight = Mathf.Lerp(1.0f, 0.9f, (signedAngle) / 120f);
            }

            vrik.solver.spine.pelvisPositionWeight = PelvisWeight;
        }
    }
}