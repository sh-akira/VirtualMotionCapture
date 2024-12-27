using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VMC;

public class BoneBendGoal : MonoBehaviour
{
    public string Name;

    public Transform UpperBone; // UpperArm等
    public Transform LowerBone; // LowerArm等
    public Transform EndBone; // Hand等
    public Transform BendGoal;

    private Guid eventId;

    public void Start()
    {
        if (Name == null) Name = name;
        eventId = IKManager.Instance.AddOnPostUpdate(1, OnPostUpdate);
    }

    public void SetBones(string name, Transform upperBone, Transform lowerBone, Transform endBone, Transform bendGoal)
    {
        Name = name;
        UpperBone = upperBone;
        LowerBone = lowerBone;
        EndBone = endBone;
        BendGoal = bendGoal;
    }

    void OnDestroy()
    {
        IKManager.Instance.RemoveOnPostUpdate(eventId);
    }

    private void OnPostUpdate()
    {
        if (enabled == false) return;
        if (IKManager.Instance.vrik == null) return;

        Quaternion currentEndBoneRotation = EndBone.rotation;

        // 回転軸
        Vector3 bendAxis = (EndBone.position - UpperBone.position);
        //LowerBoneから回転軸までの垂線
        Vector3 lowerBonePerpendicularAxis = PerpendicularAxis(UpperBone.position, EndBone.position, LowerBone.position);
        //BendGoalから回転軸までの垂線
        Vector3 bendGoalPerpendicularAxis = PerpendicularAxis(UpperBone.position, EndBone.position, BendGoal.position);

        //二つの垂線間の角度
        float angle = Vector3.SignedAngle(lowerBonePerpendicularAxis, bendGoalPerpendicularAxis, bendAxis);

        UpperBone.Rotate(bendAxis, angle, Space.World);
        EndBone.rotation = currentEndBoneRotation;
    }

    // 線分ABまでの点Pからの垂線ベクトルを取得
    private Vector3 PerpendicularAxis(Vector3 a, Vector3 b, Vector3 p)
    {
        return a + Vector3.Project(p - a, b - a) - p;
    }
}