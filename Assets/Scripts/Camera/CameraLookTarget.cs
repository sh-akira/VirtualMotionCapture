using System.Collections;
using System.Collections.Generic;
using UniGLTF;
using UnityEngine;
using VRM;

namespace VMC
{
    public class CameraLookTarget : MonoBehaviour
    {

        public Transform Target;
        public Vector3 Offset = new Vector3(0, 0.05f, 0);
        public float Distance = 0.7f;

        void Update()
        {
            if (Target != null)
            {
                var lookAt = Target.position + Offset;

                // カメラとプレイヤーとの間の距離を調整
                transform.position = lookAt - (Target.transform.forward) * (-Distance);

                // 注視点の設定
                transform.LookAt(lookAt);
            }
        }
    }
}