using RootMotion;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static RootMotion.FinalIK.VRIKCalibrator;

public class Calibrator
{

    /// <summary>
    /// Calibrates VRIK to the specified trackers using the VRIKTrackerCalibrator.Settings.
    /// </summary>
    /// <param name="ik">Reference to the VRIK component.</param>
    /// <param name="settings">Calibration settings.</param>
    /// <param name="HMDTransform">The HMD.</param>
    /// <param name="PelvisTransform">(Optional) A tracker placed anywhere on the body of the player, preferrably close to the pelvis, on the belt area.</param>
    /// <param name="LeftHandTransform">(Optional) A tracker or hand controller device placed anywhere on or in the player's left hand.</param>
    /// <param name="RightHandTransform">(Optional) A tracker or hand controller device placed anywhere on or in the player's right hand.</param>
    /// <param name="LeftFootTransform">(Optional) A tracker placed anywhere on the ankle or toes of the player's left leg.</param>
    /// <param name="RightFootTransform">(Optional) A tracker placed anywhere on the ankle or toes of the player's right leg.</param>
    public static void Calibrate(VRIK ik, Settings settings, Transform HMDTransform, Transform PelvisTransform = null, Transform LeftHandTransform = null, Transform RightHandTransform = null, Transform LeftFootTransform = null, Transform RightFootTransform = null)
    {
        if (!ik.solver.initiated)
        {
            Debug.LogError("Can not calibrate before VRIK has initiated.");
            return;
        }

        if (HMDTransform == null)
        {
            Debug.LogError("Can not calibrate VRIK without the head tracker.");
            return;
        }

        // Head
        Transform hmdAdjusterTransform = ik.solver.spine.headTarget == null ? (new GameObject("hmdAdjuster")).transform : ik.solver.spine.headTarget;
        hmdAdjusterTransform.parent = HMDTransform;
        hmdAdjusterTransform.position = HMDTransform.position + HMDTransform.rotation * Quaternion.LookRotation(settings.headTrackerForward, settings.headTrackerUp) * settings.headOffset;
        hmdAdjusterTransform.rotation = ik.references.head.rotation;
        ik.solver.spine.headTarget = hmdAdjusterTransform;

        // Size
        float sizeF = hmdAdjusterTransform.position.y / ik.references.head.position.y;
        ik.references.root.localScale = Vector3.one * sizeF;

        // Root position and rotation
        ik.references.root.position = new Vector3(hmdAdjusterTransform.position.x, ik.references.root.position.y, hmdAdjusterTransform.position.z);
        Vector3 hmdForwardAngle = HMDTransform.rotation * settings.headTrackerForward;
        hmdForwardAngle.y = 0f;
        ik.references.root.rotation = Quaternion.LookRotation(hmdForwardAngle);

        // Body
        if (PelvisTransform != null)
        {
            Transform pelvisAdjusterTransform = ik.solver.spine.pelvisTarget == null ? (new GameObject("pelvisAdjuster")).transform : ik.solver.spine.pelvisTarget;
            pelvisAdjusterTransform.parent = PelvisTransform;
            pelvisAdjusterTransform.position = ik.references.pelvis.position;
            pelvisAdjusterTransform.rotation = ik.references.pelvis.rotation;
            ik.solver.spine.pelvisTarget = pelvisAdjusterTransform;
            ik.solver.spine.pelvisPositionWeight = 1f;
            ik.solver.spine.pelvisRotationWeight = 1f;

            ik.solver.plantFeet = false;
            ik.solver.spine.neckStiffness = 0f;
            ik.solver.spine.maxRootAngle = 180f;
        }
        else if (LeftFootTransform != null && RightFootTransform != null)
        {
            ik.solver.spine.maxRootAngle = 0f;
        }

        // Left Hand
        if (LeftHandTransform != null)
        {
            Transform leftHandAdjusterTransform = ik.solver.leftArm.target == null ? (new GameObject("leftHandAdjuster")).transform : ik.solver.leftArm.target;
            leftHandAdjusterTransform.parent = LeftHandTransform;
            leftHandAdjusterTransform.position = LeftHandTransform.position + LeftHandTransform.rotation * Quaternion.LookRotation(settings.handTrackerForward, settings.handTrackerUp) * settings.handOffset;
            Vector3 leftHandUp = Vector3.Cross(ik.solver.leftArm.wristToPalmAxis, ik.solver.leftArm.palmToThumbAxis);
            leftHandAdjusterTransform.rotation = QuaTools.MatchRotation(LeftHandTransform.rotation * Quaternion.LookRotation(settings.handTrackerForward, settings.handTrackerUp), settings.handTrackerForward, settings.handTrackerUp, ik.solver.leftArm.wristToPalmAxis, leftHandUp);
            ik.solver.leftArm.target = leftHandAdjusterTransform;
        }
        else
        {
            ik.solver.leftArm.positionWeight = 0f;
            ik.solver.leftArm.rotationWeight = 0f;
        }

        // Right Hand
        if (RightHandTransform != null)
        {
            Transform rightHandAdjusterTransform = ik.solver.rightArm.target == null ? (new GameObject("rightHandAdjuster")).transform : ik.solver.rightArm.target;
            rightHandAdjusterTransform.parent = RightHandTransform;
            rightHandAdjusterTransform.position = RightHandTransform.position + RightHandTransform.rotation * Quaternion.LookRotation(settings.handTrackerForward, settings.handTrackerUp) * settings.handOffset;
            Vector3 rightHandUp = -Vector3.Cross(ik.solver.rightArm.wristToPalmAxis, ik.solver.rightArm.palmToThumbAxis);
            rightHandAdjusterTransform.rotation = QuaTools.MatchRotation(RightHandTransform.rotation * Quaternion.LookRotation(settings.handTrackerForward, settings.handTrackerUp), settings.handTrackerForward, settings.handTrackerUp, ik.solver.rightArm.wristToPalmAxis, rightHandUp);
            ik.solver.rightArm.target = rightHandAdjusterTransform;
        }
        else
        {
            ik.solver.rightArm.positionWeight = 0f;
            ik.solver.rightArm.rotationWeight = 0f;
        }

        // Legs
        if (LeftFootTransform != null) CalibrateLeg(settings, LeftFootTransform, ik.solver.leftLeg, (ik.references.leftToes != null ? ik.references.leftToes : ik.references.leftFoot), ik.references.root.forward, true);
        if (RightFootTransform != null) CalibrateLeg(settings, RightFootTransform, ik.solver.rightLeg, (ik.references.rightToes != null ? ik.references.rightToes : ik.references.rightFoot), ik.references.root.forward, false);

        // Root controller
        bool addRootController = PelvisTransform != null || (LeftFootTransform != null && RightFootTransform != null);
        var rootController = ik.references.root.GetComponent<VRIKRootController>();

        if (addRootController)
        {
            if (rootController == null) rootController = ik.references.root.gameObject.AddComponent<VRIKRootController>();
            rootController.pelvisTarget = ik.solver.spine.pelvisTarget;
            rootController.leftFootTarget = ik.solver.leftLeg.target;
            rootController.rightFootTarget = ik.solver.rightLeg.target;
            rootController.Calibrate();
        }
        else
        {
            if (rootController != null) GameObject.Destroy(rootController);
        }

        // Additional solver settings
        ik.solver.spine.minHeadHeight = 0f;
        ik.solver.locomotion.weight = PelvisTransform == null && LeftFootTransform == null && RightFootTransform == null ? 1f : 0f;
    }

    private static void CalibrateLeg(Settings settings, Transform FootTransform, IKSolverVR.Leg leg, Transform lastBone, Vector3 rootForward, bool isLeft)
    {
        Transform footAdjusterTransform = leg.target == null ? new GameObject(isLeft ? "leftFootAdjuster" : "rightFootAdjuster").transform : leg.target;
        footAdjusterTransform.parent = FootTransform;

        // Space of the tracker heading
        Quaternion frontQuaternion = FootTransform.rotation * Quaternion.LookRotation(settings.footTrackerForward, settings.footTrackerUp);
        Vector3 frontVector = frontQuaternion * Vector3.forward;
        frontVector.y = 0f;
        frontQuaternion = Quaternion.LookRotation(frontVector);

        // Target position
        float inwardOffset = isLeft ? settings.footInwardOffset : -settings.footInwardOffset;
        footAdjusterTransform.position = FootTransform.position + frontQuaternion * new Vector3(inwardOffset, 0f, settings.footForwardOffset);
        footAdjusterTransform.position = new Vector3(footAdjusterTransform.position.x, lastBone.position.y, footAdjusterTransform.position.z);

        // Target rotation
        footAdjusterTransform.rotation = lastBone.rotation;

        // Rotate target forward towards tracker forward
        Vector3 footForward = AxisTools.GetAxisVectorToDirection(lastBone, rootForward);
        if (Vector3.Dot(lastBone.rotation * footForward, rootForward) < 0f) footForward = -footForward;
        Vector3 fLocal = Quaternion.Inverse(Quaternion.LookRotation(footAdjusterTransform.rotation * footForward)) * frontVector;
        float angle = Mathf.Atan2(fLocal.x, fLocal.z) * Mathf.Rad2Deg;
        float headingOffset = isLeft ? settings.footHeadingOffset : -settings.footHeadingOffset;
        footAdjusterTransform.rotation = Quaternion.AngleAxis(angle + headingOffset, Vector3.up) * footAdjusterTransform.rotation;
        leg.target = footAdjusterTransform;
        leg.positionWeight = 1f;
        leg.rotationWeight = 1f;

        // Bend goal
        /*
        Transform bendGoal = leg.bendGoal == null ? (new GameObject(name + " Leg Bend Goal")).transform : leg.bendGoal;
        bendGoal.position = lastBone.position + frontQuaternion * Vector3.forward + frontQuaternion * Vector3.up;// * 0.5f;
        bendGoal.parent = FootTransform;
        leg.bendGoal = bendGoal;
        leg.bendGoalWeight = 1f;
        */
        leg.bendGoal = null;
        leg.bendGoalWeight = 0f;
    }
}
