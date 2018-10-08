using System;
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
        public List<GameObject> Controllers = new List<GameObject>();
        public List<GameObject> ControllersObject = new List<GameObject>();
        public GameObject CameraControllerObject;
        [System.NonSerialized]
        public int CameraControllerIndex = -1;
        [System.NonSerialized]
        public List<GameObject> Trackers = new List<GameObject>();
        public List<GameObject> TrackersObject = new List<GameObject>();
        [System.NonSerialized]
        public List<GameObject> BaseStations = new List<GameObject>();
        public List<GameObject> BaseStationsObject = new List<GameObject>();
        public bool DisableBaseStationRotation = true;

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
                    HMDObject.transform.SetPositionAndRotationLocal(hmdPositions.FirstOrDefault());
                }
                var controllerPositions = positions[ETrackedDeviceClass.Controller];
                if (controllerPositions.Any())
                {
                    //externalcamera.cfg用のコントローラー設定
                    if (CameraControllerIndex >= 0 && CameraControllerIndex < controllerPositions.Count)
                    {
                        CameraControllerObject.transform.SetPositionAndRotationLocal(controllerPositions[CameraControllerIndex]);
                        controllerPositions.RemoveAt(CameraControllerIndex);
                    }
                    for (int i = 0; i < controllerPositions.Count && i < ControllersObject.Count; i++)
                    {
                        ControllersObject[i].transform.SetPositionAndRotationLocal(controllerPositions[i]);
                        if (Controllers.Contains(ControllersObject[i]) == false) Controllers.Add(ControllersObject[i]);
                    }
                }
                var trackerPositions = positions[ETrackedDeviceClass.GenericTracker];
                if (trackerPositions.Any())
                {
                    for (int i = 0; i < trackerPositions.Count && i < TrackersObject.Count; i++)
                    {
                        TrackersObject[i].transform.SetPositionAndRotationLocal(trackerPositions[i]);
                        if (Trackers.Contains(TrackersObject[i]) == false) Trackers.Add(TrackersObject[i]);
                    }
                }

                var baseStationPositions = positions[ETrackedDeviceClass.TrackingReference];
                if (baseStationPositions.Any())
                {
                    for (int i = 0; i < baseStationPositions.Count && i < BaseStationsObject.Count; i++)
                    {
                        BaseStationsObject[i].transform.SetPositionAndRotationLocal(baseStationPositions[i]);
                        if (DisableBaseStationRotation)
                        {
                            BaseStationsObject[i].transform.rotation = Quaternion.Euler(0, BaseStationsObject[i].transform.eulerAngles.y, 0);
                        }
                        if (BaseStations.Contains(BaseStationsObject[i]) == false) BaseStations.Add(BaseStationsObject[i]);
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
        private static Dictionary<string, Vector3> lastPositions = new Dictionary<string, Vector3>();

        public static event EventHandler<string> TrackerMovedEvent;

        private static void CheckPosition(string serial,Vector3 pos)
        {
            if (lastPositions.ContainsKey(serial) == false)
            {
                lastPositions.Add(serial, pos);
            }
            else
            {
                if(Vector3.Distance(lastPositions[serial],pos) > 0.1f)
                {
                    TrackerMovedEvent?.Invoke(null, serial);
                    lastPositions[serial] = pos;
                }
            }
        }

        public static void SetPositionAndRotation(this Transform t, KeyValuePair<SteamVR_Utils.RigidTransform, string> mat)
        {
            if (mat.Key != null)
            {
                CheckPosition(mat.Value, mat.Key.pos);
                t.SetPositionAndRotation(mat.Key.pos, mat.Key.rot);
                t.name = mat.Value;
            }
        }

        public static void SetPositionAndRotationLocal(this Transform t, KeyValuePair<SteamVR_Utils.RigidTransform, string> mat)
        {
            if (mat.Key != null)
            {
                CheckPosition(mat.Value, mat.Key.pos);
                t.localPosition = mat.Key.pos;
                t.localRotation = mat.Key.rot;
                t.name = mat.Value;
            }
        }
    }
}