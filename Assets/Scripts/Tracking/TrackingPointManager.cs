using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace VMC
{
    public class TrackingPointManager : MonoBehaviour
    {
        public static TrackingPointManager Instance;

        private void Awake()
        {
            Instance = this;
        }

        private Dictionary<string, TrackingPoint> TrackingPoints = new Dictionary<string, TrackingPoint>();

        public TrackingPoint ApplyPoint(string name, Vector3 position, Quaternion rotation)
        {
            //ignore "LIV Virtual Camera"

            if (TrackingPoints.TryGetValue(name, out var trackingPoint) == false)
            {
                trackingPoint = new TrackingPoint(name);
                TrackingPoints[name] = trackingPoint;
                
            }
            trackingPoint.TargetTransform.localPosition = position;
            trackingPoint.TargetTransform.localRotation = rotation;

            return trackingPoint;
        }

        public TrackingPoint GetTrackingPoint(string name)
        {
            TrackingPoints.TryGetValue(name, out var trackingPoint);
            return trackingPoint;
        }

        public Transform GetTransform(string name)
        {
            return GetTrackingPoint(name)?.TargetTransform;
        }
    }


    public class TrackingPoint
    {
        public string Name { get; set; }
        public Transform TargetTransform { get; set; }

        public TrackingPoint(string name) => Name = name;
    }

}