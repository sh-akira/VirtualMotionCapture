using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using VMC;
using System;

public class VRIKSpineOffset : MonoBehaviour
{
    public VRIK ik;
    public Vector3 spineFix;
    private Guid? eventId = null;

    private void OnEnable()
    {
        eventId = IKManager.Instance.AddOnPostUpdate(50, AfterVRIK);
    }

    private void OnDisable()
    {
        if (eventId != null) IKManager.Instance.RemoveOnPostUpdate(eventId.Value);
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
