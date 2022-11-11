using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class TransformAdjustFollower : MonoBehaviour
    {
        private const float AdjustLimit = 0.018f;
        private const float LerpDistance = 0.009f;

        private Transform adjustTargetPoint;
        private float defaultHeight;
        private Vector3 defaultUp;
        private bool upAdjust;
        private bool downAdjust;

        public void Initialize(Transform realTargetPoint, bool upAdjust, bool downAdjust)
        {
            adjustTargetPoint = realTargetPoint;
            defaultHeight = adjustTargetPoint.position.y;
            defaultUp = adjustTargetPoint.up;
            transform.rotation = adjustTargetPoint.rotation;
            transform.position = adjustTargetPoint.position;
            this.upAdjust = upAdjust;
            this.downAdjust = downAdjust;
        }

        private void Update()
        {
            if (adjustTargetPoint == null) return;

            //回転はそのまま
            transform.rotation = adjustTargetPoint.rotation;

            float x = adjustTargetPoint.position.x;
            float y = adjustTargetPoint.position.y;
            float z = adjustTargetPoint.position.z;
            float yDistance = y - defaultHeight;

            //回転具合で補正
            var adjustHeight = defaultHeight;
            //後ろがマイナス、前がプラス。真上0～真下180度
            var signedAngle = Vector3.SignedAngle(defaultUp, adjustTargetPoint.up, adjustTargetPoint.right);

            if (signedAngle < 0)
            { //後ろに曲がった時
                adjustHeight = Mathf.Lerp(y, defaultHeight, (-signedAngle) / 30f);
            }
            else
            { //前に曲がった時
                adjustHeight = Mathf.Lerp(y, defaultHeight, (signedAngle) / 45f);
            }

            if ((yDistance > 0 && upAdjust) || (yDistance < 0 && downAdjust))
            {
                yDistance = Mathf.Abs(yDistance);

                //0～AdjustLimit間はYをデフォルトから動かさない
                float lerpT = 0;

                if (yDistance > LerpDistance + AdjustLimit)
                { //LerpDistance～は補正しない
                    lerpT = 1;
                }
                else if (yDistance > AdjustLimit)
                { //AdjustLimit～LerpDistance間は線形ではなく滑らかに補間する
                  //f(x) = (2x-1)/(1+(2x-1)^2) + 1/2
                    float coefficient = 2f / (100f * LerpDistance);
                    coefficient = (coefficient * (yDistance - AdjustLimit) * 100f - 1f);
                    lerpT = coefficient / (1 + coefficient * coefficient) + 0.5f;
                }

                y = Mathf.Lerp(adjustHeight, y, lerpT);
            }


            //補正後の位置設定
            transform.position = new Vector3(x, y, z);

        }
    }
}