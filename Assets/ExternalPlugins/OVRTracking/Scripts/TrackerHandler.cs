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
        public TrackingWatcher HMDObjectTrackingWatcher;

        public List<GameObject> Controllers = new List<GameObject>();
        public List<GameObject> ControllersObject = new List<GameObject>();
        [System.NonSerialized]
        public List<TrackingWatcher> ControllersObjectTrackingWatcher = null;

        public GameObject CameraControllerObject;
        [System.NonSerialized]
        public ETrackedDeviceClass CameraControllerType = ETrackedDeviceClass.Invalid;
        [System.NonSerialized]
        public string CameraControllerName = null;

        [System.NonSerialized]
        public List<GameObject> Trackers = new List<GameObject>();
        public List<GameObject> TrackersObject = new List<GameObject>();
        [System.NonSerialized]
        public List<TrackingWatcher> TrackersObjectTrackingWatcher = null;


        [System.NonSerialized]
        public List<GameObject> BaseStations = new List<GameObject>();
        public List<GameObject> BaseStationsObject = new List<GameObject>();
        public bool DisableBaseStationRotation = true;

        public ExternalReceiverForVMC externalReceiver;

        // Use this for initialization
        void Start()
        {
            //Watcherを用意
            HMDObjectTrackingWatcher = HMDObject.AddComponent<TrackingWatcher>();

            //指定サイズでListを生成
            ControllersObjectTrackingWatcher = new List<TrackingWatcher>(new TrackingWatcher[ControllersObject.Count]);
            for (int i = 0; i < ControllersObject.Count; i++)
            {
                ControllersObjectTrackingWatcher[i] = ControllersObject[i].AddComponent<TrackingWatcher>();
            }

            TrackersObjectTrackingWatcher = new List<TrackingWatcher>(new TrackingWatcher[TrackersObject.Count]);
            for (int i = 0; i < TrackersObject.Count; i++)
            {
                TrackersObjectTrackingWatcher[i] = TrackersObject[i].AddComponent<TrackingWatcher>();
            }

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
            Dictionary<ETrackedDeviceClass, List<DeviceInfo>> positions;
            if (IsOVRConnected)
            {
                OpenVRWrapper.Instance.ConvertControllerToTracker = ControlWPFWindow.CurrentSettings.HandleControllerAsTracker;
                OpenVRWrapper.Instance.PollingVREvents();
                positions = OpenVRWrapper.Instance.GetTrackerPositions();
            }
            else
            {
                positions = new Dictionary<ETrackedDeviceClass, List<DeviceInfo>>();
                positions.Add(ETrackedDeviceClass.HMD, new List<DeviceInfo>());
                positions.Add(ETrackedDeviceClass.Controller, new List<DeviceInfo>());
                positions.Add(ETrackedDeviceClass.GenericTracker, new List<DeviceInfo>());
                positions.Add(ETrackedDeviceClass.TrackingReference, new List<DeviceInfo>());
            }
            //externalcamera.cfg用のコントローラー設定
            if (CameraControllerName != null && positions.SelectMany(d => d.Value).Any(d => d.serialNumber == CameraControllerName))
            {
                var cameracontroller = positions.SelectMany(d => d.Value).Where(d => d.serialNumber == CameraControllerName).First();
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
                DeviceInfo hmdInfo = hmdPositions.FirstOrDefault();
                HMDObject.transform.SetPositionAndRotationLocal(hmdInfo);
                HMDObjectTrackingWatcher.IsOK(hmdInfo.isOK);
            }

            var controllerPositions = positions[ETrackedDeviceClass.Controller];

            //add from ExternalReceiverForVMC
            if (externalReceiver != null)
            {
                foreach (var c in externalReceiver.virtualControllerFiltered)
                {
                    controllerPositions.Add(new DeviceInfo(c.Value, c.Key));
                }
            }

            if (controllerPositions.Any())
            {
                if (Controllers.Count != controllerPositions.Count) Controllers.Clear();
                for (int i = 0; i < controllerPositions.Count && i < ControllersObject.Count; i++)
                {
                    DeviceInfo deviceInfo = controllerPositions[i];
                    ControllersObject[i].transform.SetPositionAndRotationLocal(deviceInfo);
                    ControllersObjectTrackingWatcher[i].IsOK(deviceInfo.isOK);

                    if (Controllers.Contains(ControllersObject[i]) == false) Controllers.Add(ControllersObject[i]);
                }
            }
            else {
                Controllers.Clear(); //残ってしまったものを削除
            }

            var trackerPositions = positions[ETrackedDeviceClass.GenericTracker];

            //add from ExternalReceiverForVMC
            if (externalReceiver != null)
            {
                foreach (var t in externalReceiver.virtualTrackerFiltered)
                {
                    trackerPositions.Add(new DeviceInfo(t.Value, t.Key));
                }
            }

            if (trackerPositions.Any())
            {
                if (Trackers.Count != trackerPositions.Count) Trackers.Clear();
                for (int i = 0; i < trackerPositions.Count && i < TrackersObject.Count; i++)
                {
                    DeviceInfo deviceInfo = trackerPositions[i];
                    TrackersObject[i].transform.SetPositionAndRotationLocal(deviceInfo);
                    TrackersObjectTrackingWatcher[i].IsOK(deviceInfo.isOK);

                    if (Trackers.Contains(TrackersObject[i]) == false) Trackers.Add(TrackersObject[i]);
                }
            }
            else {
                Trackers.Clear(); //残ってしまったものを削除
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
            else {
                BaseStations.Clear(); //残ってしまったものを削除
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

        private static void CheckPosition(DeviceInfo device)
        {
            if (lastPositions.ContainsKey(device.serialNumber) == false)
            {
                lastPositions.Add(device.serialNumber, device.transform.pos);
            }
            else
            {
                if (Vector3.Distance(lastPositions[device.serialNumber], device.transform.pos) > 0.1f)
                {
                    TrackerMovedEvent?.Invoke(null, device.serialNumber);
                    lastPositions[device.serialNumber] = device.transform.pos;
                }
            }
        }

        public static void SetPositionAndRotation(this Transform t, DeviceInfo mat)
        {
            if (mat != null)
            {
                CheckPosition(mat);

                t.SetPositionAndRotation(mat.transform.pos, mat.transform.rot);
                t.name = mat.serialNumber;
            }
        }

        public static void SetPositionAndRotationLocal(this Transform t, DeviceInfo mat)
        {
            if (mat != null)
            {
                CheckPosition(mat);

                t.localPosition = mat.transform.pos;
                t.localRotation = mat.transform.rot;
                t.name = mat.serialNumber;
            }
        }
    }
}