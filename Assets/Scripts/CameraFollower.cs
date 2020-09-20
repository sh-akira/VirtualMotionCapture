using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    void Update()
    {
        if (CameraMouseControl.Current != null)
        {
            transform.position = CameraMouseControl.Current.transform.position;
            transform.rotation = CameraMouseControl.Current.transform.rotation;
        }   
    }
}
