using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ControlWPFWindow;

[Serializable]
public class AllTrackerPositions
{
    public StoreTransform RootTransform;
    public List<TrackerPositionData> Trackers;

    public VRIKSolverData VrikSolverData;
}
