using System.Collections;
using System.Collections.Generic;
using UniGLTF;
using UnityEngine;
using VRM;

public class PositionFixedCamera : MonoBehaviour
{

    public Transform Target;

    private Vector3 relativePosition = new Vector3(0, 0, -1f);

    void Update()
    {
        if (Target != null)
        {
            // カメラとプレイヤーとの間の距離を調整
            transform.position = Target.position + relativePosition;
        }
    }

    public void UpdatePosition()
    {
        relativePosition = transform.position - Target.position;
    }
}
