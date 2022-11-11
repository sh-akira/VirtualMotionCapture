using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;

public class VRIKSpineOffset : MonoBehaviour
{
    public VRIK ik;
    public Vector3 spineFix;

    private void OnEnable()
    {
        ik.solver.OnPostUpdate += AfterVRIK;
    }

    private void OnDisable()
    {
        ik.solver.OnPostUpdate -= AfterVRIK;
    }

    private void AfterVRIK()
    {
        Vector3 headPos = ik.references.head.position;
        Quaternion headRot = ik.references.head.rotation;

        ik.references.spine.localRotation *= Quaternion.Euler(spineFix);
        ik.references.chest.localRotation *= Quaternion.Euler(-spineFix);

        ik.references.chest.rotation = Quaternion.FromToRotation(ik.references.head.position - ik.references.chest.position, headPos - ik.references.chest.position) * ik.references.chest.rotation;
        ik.references.head.rotation = headRot;
    }
}
