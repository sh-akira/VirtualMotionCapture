using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public float Speed = 0f;

    void Update()
    {
        if (CameraMouseControl.Current != null)
        {
            if (Speed == 0)
            {
                transform.position = CameraMouseControl.Current.transform.position;
                transform.rotation = CameraMouseControl.Current.transform.rotation;
            } else
            {
                transform.position = Vector3.Slerp(transform.position, CameraMouseControl.Current.transform.position, Time.deltaTime * (21 - Speed));
                transform.rotation = Quaternion.Slerp(transform.rotation, CameraMouseControl.Current.transform.rotation, Time.deltaTime * (21 - Speed));
            }
        }   
    }
}
