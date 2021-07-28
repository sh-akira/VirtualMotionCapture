using System;
using UnityEngine;
using static VMC.ControlWPFWindow;

namespace VMC
{
    [Serializable]
    public class TrackerPositionData
    {
        public string Name;
        public StoreTransform ParentTransform;
        public StoreTransform TrackerTransform;
        public StoreTransform OffsetTransform;
        public StoreTransform ChildOffsetTransform;


        public TrackerPositionData SetOffsetAuto(string Name, Transform offsetObject)
        {
            this.Name = Name;
            return SetOffsetAuto(offsetObject);
        }
        public TrackerPositionData SetOffsetAuto(Transform offsetObject)
        {
            if (offsetObject.parent.parent.parent != null)
            {
                ChildOffsetTransform = new StoreTransform(offsetObject);
                offsetObject = offsetObject.parent;
            }
            OffsetTransform = new StoreTransform(offsetObject);
            TrackerTransform = new StoreTransform(offsetObject.parent);
            ParentTransform = new StoreTransform(offsetObject.parent.parent);
            return this;
        }
    }
}