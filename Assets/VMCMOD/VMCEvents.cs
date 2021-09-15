using System;
using UnityEngine;

namespace VMC
{
    public class VMCEvents
    {
        public static Action<GameObject> OnModelLoaded = null;
        public static Action<Camera> OnCameraChanged = null;
    }
}