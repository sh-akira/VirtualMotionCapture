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
        public ETrackedDeviceClass CameraControllerType = ETrackedDeviceClass.Invalid;
        [System.NonSerialized]
        public string CameraControllerName = null;
        [System.NonSerialized]
        public List<GameObject> Trackers = new List<GameObject>();
        public List<GameObject> TrackersObject = new List<GameObject>();
        [System.NonSerialized]
        public List<GameObject> BaseStations = new List<GameObject>();
        public List<GameObject> BaseStationsObject = new List<GameObject>();
        public bool DisableBaseStationRotation = true;

        public ExternalReceiverForVMC externalReceiver;

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

        public Transform GetTrackerTransformByName(string name)
        {
            if (CameraControllerObject.name == name) return CameraControllerObject.transform;
            if (HMDObject.name == name) return HMDObject.transform;
            var controller = Controllers.FirstOrDefault(d => d.name == name);
            if (controller != null) return controller.transform;
            var tracker = Trackers.FirstOrDefault(d => d.name == name);
            if (tracker != null) return tracker.transform;
            var basestation = BaseStations.FirstOrDefault(d => d.name == name);
            if (basestation != null) return basestation.transform;
            return null;
        }

        // Update is called once per frame
        void Update()
        {
            if (IsOVRConnected)
            {
                OpenVRWrapper.Instance.PollingVREvents();
                var positions = OpenVRWrapper.Instance.GetTrackerPositions();

                //externalcamera.cfg用のコントローラー設定
                if (CameraControllerName != null && positions.SelectMany(d => d.Value).Any(d => d.Value == CameraControllerName))
                {
                    var cameracontroller = positions.SelectMany(d => d.Value).Where(d => d.Value == CameraControllerName).First();
                    CameraControllerObject.transform.SetPositionAndRotationLocal(cameracontroller);
                    foreach (var l in positions)
                    {
                        if (l.Value.Contains(cameracontroller))
                        {
                            CameraControllerType = l.Key;
                            l.Value.Remove(cameracontroller);
                        }
                    }
                }

                var hmdPositions = positions[ETrackedDeviceClass.HMD];
                if (hmdPositions.Any())
                {
                    HMDObject.transform.SetPositionAndRotationLocal(hmdPositions.FirstOrDefault());
                }

                var controllerPositions = positions[ETrackedDeviceClass.Controller];

                //add from ExternalReceiverForVMC
                foreach (var c in externalReceiver.virtualController) {
                    controllerPositions.Add(new KeyValuePair<SteamVR_Utils.RigidTransform, string>(c.Value, c.Key));
                }

                if (controllerPositions.Any())
                {
                    if (Controllers.Count != controllerPositions.Count) Controllers.Clear();
                    for (int i = 0; i < controllerPositions.Count && i < ControllersObject.Count; i++)
                    {
                        ControllersObject[i].transform.SetPositionAndRotationLocal(controllerPositions[i]);
                        if (Controllers.Contains(ControllersObject[i]) == false) Controllers.Add(ControllersObject[i]);
                    }
                }

                var trackerPositions = positions[ETrackedDeviceClass.GenericTracker];

                //add from ExternalReceiverForVMC
                foreach (var t in externalReceiver.virtualTracker)
                {
                    trackerPositions.Add(new KeyValuePair<SteamVR_Utils.RigidTransform, string>(t.Value, t.Key));
                }

                if (trackerPositions.Any())
                {
                    if (Trackers.Count != trackerPositions.Count) Trackers.Clear();
                    for (int i = 0; i < trackerPositions.Count && i < TrackersObject.Count; i++)
                    {
                        TrackersObject[i].transform.SetPositionAndRotationLocal(trackerPositions[i]);
                        if (Trackers.Contains(TrackersObject[i]) == false) Trackers.Add(TrackersObject[i]);
                    }
                }

                var baseStationPositions = positions[ETrackedDeviceClass.TrackingReference];
                if (baseStationPositions.Any())
                {
                    if (BaseStations.Count != baseStationPositions.Count) BaseStations.Clear();
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

    public static class TrackerTransformExtensions
    {
        private static Dictionary<string, Vector3> lastPositions = new Dictionary<string, Vector3>();

        public static event EventHandler<string> TrackerMovedEvent;

        private static void CheckPosition(string serial, Vector3 pos)
        {
            if (lastPositions.ContainsKey(serial) == false)
            {
                lastPositions.Add(serial, pos);
            }
            else
            {
                if (Vector3.Distance(lastPositions[serial], pos) > 0.1f)
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