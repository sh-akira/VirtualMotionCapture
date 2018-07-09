using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;

namespace sh_akira.OVRTracking
{
    public class TrackerHandler : MonoBehaviour
    {
        public GameObject HMDObject;
        public GameObject LeftControllerObject;
        public GameObject RightControllerObject;
        [System.NonSerialized]
        public List<GameObject> Trackers = new List<GameObject>();
        public List<GameObject> TrackersObject = new List<GameObject>();

        // Use this for initialization
        void Start()
        {
            OpenVRWrapper.Instance.OnOVRConnected += OpenVR_OnOVRConnected;
            OpenVRWrapper.Instance.Setup();
        }

        private void OpenVR_OnOVRConnected(object sender, OVRConnectedEventArgs e)
        {
            IsOVRConnected = e.Connected;
        }

        private bool IsOVRConnected = false;

        // Update is called once per frame
        void Update()
        {
            if (IsOVRConnected)
            {
                OpenVRWrapper.Instance.PollingVREvents();
                var positions = OpenVRWrapper.Instance.GetTrackerPositions();
                var hmdPositions = positions[ETrackedDeviceClass.HMD];
                if (hmdPositions.Any())
                {
                    HMDObject.transform.SetPositionAndRotation(hmdPositions.FirstOrDefault());
                }
                var controllerPositions = positions[ETrackedDeviceClass.Controller];
                if (controllerPositions.Any())
                {
                    LeftControllerObject.transform.SetPositionAndRotation(controllerPositions.FirstOrDefault());
                    if (controllerPositions.Count > 1)
                    {
                        RightControllerObject.transform.SetPositionAndRotation(controllerPositions[1]);
                    }
                }
                var trackerPositions = positions[ETrackedDeviceClass.GenericTracker];
                if (trackerPositions.Any())
                {

                    for (int i = 0; i < trackerPositions.Count && i < TrackersObject.Count; i++)
                    {
                        TrackersObject[i].transform.SetPositionAndRotation(trackerPositions[i]);
                        if (Trackers.Contains(TrackersObject[i]) == false) Trackers.Add(TrackersObject[i]);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            //OpenVRWrapper.Instance.Close();
        }
    }

    public static class TransformExtensions
    {
        public static void SetPositionAndRotation(this Transform t, SteamVR_Utils.RigidTransform mat)
        {
            if (mat != null) t.SetPositionAndRotation(mat.pos, mat.rot);
        }
    }
}