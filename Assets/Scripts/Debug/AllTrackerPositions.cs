using System;
using System.Collections.Generic;
using static VMC.ControlWPFWindow;

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