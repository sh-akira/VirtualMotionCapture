using System;
using System.Collections.Generic;

namespace VMC
{
    [Serializable]
    public class AllTrackerPositions
    {
        public StoreTransform RootTransform;
        public List<TrackerPositionData> Trackers;

        public VRIKSolverData VrikSolverData;
    }
}