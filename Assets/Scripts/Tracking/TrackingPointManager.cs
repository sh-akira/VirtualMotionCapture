using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;

namespace VMC
{
    public class TrackingPointManager : MonoBehaviour
    {
        public static TrackingPointManager Instance;

        private void Awake()
        {
            Instance = this;
        }

        public event EventHandler<string> TrackerMovedEvent;

        private Dictionary<string, TrackingPoint> AllTrackingPoints = new Dictionary<string, TrackingPoint>();
        private Dictionary<string, TrackingPoint> HmdTrackingPoints = new Dictionary<string, TrackingPoint>();
        private Dictionary<string, TrackingPoint> ControllerTrackingPoints = new Dictionary<string, TrackingPoint>();
        private Dictionary<string, TrackingPoint> TrackerTrackingPoints = new Dictionary<string, TrackingPoint>();

        public TrackingPoint ApplyPoint(string name, ETrackedDeviceClass deviceClass, Vector3 position, Quaternion rotation, bool isOK)
        {
            //ignore "LIV Virtual Camera"

            if (AllTrackingPoints.TryGetValue(name, out var trackingPoint) == false)
            {
                trackingPoint = new TrackingPoint(name, deviceClass);
                var targetGameObject = new GameObject(name);
                trackingPoint.TrackingWatcher = targetGameObject.AddComponent<TrackingWatcher>();
                trackingPoint.TargetTransform = targetGameObject.transform;
                trackingPoint.TargetTransform.SetParent(transform, false);

                AllTrackingPoints[name] = trackingPoint;
                if (deviceClass == ETrackedDeviceClass.HMD)
                {
                    HmdTrackingPoints[name] = trackingPoint;
                }
                else if (deviceClass == ETrackedDeviceClass.Controller)
                {
                    ControllerTrackingPoints[name] = trackingPoint;
                }
                else if (deviceClass == ETrackedDeviceClass.GenericTracker)
                {
                    TrackerTrackingPoints[name] = trackingPoint;
                }
            }

            if (trackingPoint.SetPositionAndRotationLocal(position, rotation))
            {
                TrackerMovedEvent?.Invoke(this, name);
            }
            trackingPoint.TrackingWatcher.IsOK(isOK);

            return trackingPoint;
        }

        public bool TryGetTrackingPoint(string name, out TrackingPoint trackingPoint)
        {
            return AllTrackingPoints.TryGetValue(name, out trackingPoint);
        }

        public TrackingPoint GetTrackingPoint(string name)
        {
            TryGetTrackingPoint(name, out var trackingPoint);
            return trackingPoint;
        }

        public Transform GetTransform(string name)
        {
            return GetTrackingPoint(name)?.TargetTransform;
        }

        public IEnumerable<TrackingPoint> GetTrackingPoints()
        {
            return AllTrackingPoints.Values;
        }

        public IEnumerable<TrackingPoint> GetTrackingPoints(ETrackedDeviceClass deviceClass)
        {
            if (deviceClass == ETrackedDeviceClass.HMD)
            {
                return HmdTrackingPoints.Values;
            }
            else if (deviceClass == ETrackedDeviceClass.Controller)
            {
                return ControllerTrackingPoints.Values;
            }
            else if (deviceClass == ETrackedDeviceClass.GenericTracker)
            {
                return TrackerTrackingPoints.Values;
            }
            return GetTrackingPoints().Where(d => d.DeviceClass == deviceClass);
        }

        public TrackingPoint GetHmdTrackingPoint() => GetTrackingPoints(ETrackedDeviceClass.HMD).FirstOrDefault();
        public IEnumerable<TrackingPoint> GetControllerTrackingPoints() => GetTrackingPoints(ETrackedDeviceClass.Controller);
        public IEnumerable<TrackingPoint> GetTrackerTrackingPoints() => GetTrackingPoints(ETrackedDeviceClass.GenericTracker);

        public void ClearTrackingWatcher()
        {
            foreach (var trackingPoint in GetTrackingPoints())
            {
                trackingPoint.TrackingWatcher?.Clear();
            }
        }

        public void SetTrackingPointPositionVisible(bool visible)
        {
            foreach (var trackingPoint in GetTrackingPoints())
            {
                if (trackingPoint.PositionSphere != null)
                {
                    Destroy(trackingPoint.PositionSphere);
                }

                if (visible)
                {
                    trackingPoint.PositionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    trackingPoint.PositionSphere.transform.SetParent(trackingPoint.TargetTransform, false);
                    trackingPoint.PositionSphere.transform.localPosition = Vector3.zero;
                    trackingPoint.PositionSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                }
            }
        }
    }


    public class TrackingPoint
    {
        public string Name { get; set; }
        public Transform TargetTransform { get; set; }
        public ETrackedDeviceClass DeviceClass { get; set; }
        public TrackingWatcher TrackingWatcher { get; set; }
        public GameObject PositionSphere { get; set; }

        private Vector3 lastMovedPosition;

        public TrackingPoint(string name, ETrackedDeviceClass deviceClass)
        {
            Name = name;
            DeviceClass = deviceClass;
        }

        /// <summary>
        /// ローカル座標でTransform適用
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns>移動していたらtrue</returns>
        public bool SetPositionAndRotationLocal(Vector3 position, Quaternion rotation)
        {
            bool moved = false;

            if (Vector3.Distance(lastMovedPosition, position) > 0.1f)
            {
                moved = true;
                lastMovedPosition = position;
            }

            TargetTransform.localPosition = position;
            TargetTransform.localRotation = rotation;

            return moved;
        }
    }

}