using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackerPositionsExporter : MonoBehaviour
{
    ControlWPFWindow window = null;
    VRIK vrik = null;
    GameObject CurrentModel;
    private void Start()
    {
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();

        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            if (CurrentModel != null)
            {
                this.CurrentModel = CurrentModel;
                vrik = CurrentModel.GetComponent<VRIK>();
            }
        };

        KeyboardAction.KeyDownEvent += (object sender, KeyboardEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                if (e.KeyName == "S")
                {
                    Export();
                }
            }
        };
    }

    private void Export()
    {
        if (vrik == null && CurrentModel != null)
        {
            vrik = CurrentModel.GetComponent<VRIK>();
            Debug.Log("ExternalSender: VRIK Updated");
        }
        if (vrik == null) return;
        var Trackers = new List<TrackerPositionData>();
        AddIfNotNull(Trackers, "Head", vrik.solver.spine.headTarget);
        AddIfNotNull(Trackers, "Pelvis", vrik.solver.spine.pelvisTarget);
        AddIfNotNull(Trackers, "LeftArm", vrik.solver.leftArm.target);
        AddIfNotNull(Trackers, "RightArm", vrik.solver.rightArm.target);
        AddIfNotNull(Trackers, "LeftLeg", vrik.solver.leftLeg.target);
        AddIfNotNull(Trackers, "RightLeg", vrik.solver.rightLeg.target);

        var alldata = new AllTrackerPositions
        {
            RootTransform = new ControlWPFWindow.StoreTransform(vrik.references.root),
            Trackers = Trackers,
            VrikSolverData = new VRIKSolverData(vrik.solver),
        };

        var path = Application.dataPath + "/../SavedTrackerPositions/";
        if (System.IO.Directory.Exists(path) == false) System.IO.Directory.CreateDirectory(path);
        var json = sh_akira.Json.Serializer.ToReadable(sh_akira.Json.Serializer.Serialize(alldata));
        System.IO.File.WriteAllText(path + $"TrackerPositions_{System.DateTime.Now:yyMMdd-HHmmss-fff}.json",json);
    }

    private void AddIfNotNull(List<TrackerPositionData> trackers, string Name, Transform target)
    {
        if (target == null) return;
        trackers.Add(new TrackerPositionData().SetOffsetAuto(Name, target));
    }
}
