using RootMotion.FinalIK;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMC
{
    public class TrackerPositionsImporter : MonoBehaviour
    {
        ControlWPFWindow window = null;
        private VRIK vrik = null;
        GameObject CurrentModel;
        private Transform RootObject;

        private bool isLeftShiftKeyDown = false;
        private bool isRightShiftKeyDown = false;

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
                    if (e.KeyName == "左Shift")
                    {
                        isLeftShiftKeyDown = true;
                    }
                    if (e.KeyName == "右Shift")
                    {
                        isRightShiftKeyDown = true;
                    }
                    if (e.KeyName == "P")
                    {
                        Import(isLeftShiftKeyDown || isRightShiftKeyDown);
                    }
                }
            };

            KeyboardAction.KeyUpEvent += (object sender, KeyboardEventArgs e) =>
            {
                if (this.isActiveAndEnabled)
                {
                    if (e.KeyName == "左Shift")
                    {
                        isLeftShiftKeyDown = false;
                    }
                    if (e.KeyName == "右Shift")
                    {
                        isRightShiftKeyDown = false;
                    }
                }
            };

            RootObject = new GameObject("TrackerImporter").transform;
        }

        private void Import(bool createObject)
        {
            if (vrik == null && CurrentModel != null)
            {
                vrik = CurrentModel.GetComponent<VRIK>();
                Debug.Log("ExternalSender: VRIK Updated");
            }
            if (vrik == null) return;
            var path = Application.dataPath + "/../SavedTrackerPositions/";
            var filename = WindowsDialogs.OpenFileDialog("Import tracker positions", ".json");
            if (string.IsNullOrEmpty(filename)) return;
            var json = System.IO.File.ReadAllText(filename);
            var alldata = sh_akira.Json.Serializer.Deserialize<AllTrackerPositions>(json);

            foreach (Transform child in RootObject)
            {
                Destroy(child.gameObject);
            }

            var Trackers = alldata.Trackers;
            AddIfNotNull(Trackers, createObject, "Head", ref vrik.solver.spine.headTarget);
            AddIfNotNull(Trackers, createObject, "Pelvis", ref vrik.solver.spine.pelvisTarget);
            AddIfNotNull(Trackers, createObject, "LeftArm", ref vrik.solver.leftArm.target);
            AddIfNotNull(Trackers, createObject, "RightArm", ref vrik.solver.rightArm.target);
            AddIfNotNull(Trackers, createObject, "LeftLeg", ref vrik.solver.leftLeg.target);
            AddIfNotNull(Trackers, createObject, "RightLeg", ref vrik.solver.rightLeg.target);

            alldata.RootTransform.ToLocalTransform(vrik.references.root);
            alldata.VrikSolverData.ApplyTo(vrik.solver);
        }

        private void AddIfNotNull(List<TrackerPositionData> trackers, bool createObject, string Name, ref Transform target)
        {
            var tracker = trackers.FirstOrDefault(d => d.Name == Name);
            if (tracker == null) return;
            var root = new GameObject(Name + "Root").transform;
            root.parent = RootObject;
            var trackerobj = new GameObject(Name + "Object").transform;
            trackerobj.parent = root;
            var offset = new GameObject(Name + "Offset").transform;
            offset.parent = trackerobj;
            tracker.ParentTransform.ToLocalTransform(root);
            tracker.TrackerTransform.ToLocalTransform(trackerobj);
            tracker.OffsetTransform.ToLocalTransform(offset);
            if (tracker.ChildOffsetTransform != null)
            {
                var childoffset = new GameObject(Name + "ChildOffset").transform;
                childoffset.parent = offset;
                tracker.ChildOffsetTransform.ToLocalTransform(childoffset);
                target = childoffset;
            }
            else
            {
                target = offset;
            }

            if (createObject)
            {
                var offsetSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                offsetSphere.transform.parent = target;
                offsetSphere.transform.localPosition = new Vector3(0f, 0f, 0f);
                offsetSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

                var offsetParentCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                offsetParentCube.transform.parent = target.parent;
                offsetParentCube.transform.localPosition = new Vector3(0f, 0f, 0f);
                offsetParentCube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            }
        }
    }
}