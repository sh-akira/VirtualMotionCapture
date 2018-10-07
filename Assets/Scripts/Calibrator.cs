using RootMotion;
using RootMotion.FinalIK;
using System;
using System.Collections;
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
        if (LeftFootTransform != null) CalibrateLeg(settings, LeftFootTransform, ik.solver.leftLeg, (ik.references.leftToes != null ? ik.references.leftToes : ik.references.leftFoot), hmdForwardAngle, ik.references.root.forward, true);
        if (RightFootTransform != null) CalibrateLeg(settings, RightFootTransform, ik.solver.rightLeg, (ik.references.rightToes != null ? ik.references.rightToes : ik.references.rightFoot), hmdForwardAngle, ik.references.root.forward, false);

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

    private static void CalibrateLeg(Settings settings, Transform FootTransform, IKSolverVR.Leg leg, Transform lastBone, Vector3 hmdForwardAngle, Vector3 rootForward, bool isLeft)
    {
        Transform footAdjusterTransform = leg.target == null ? new GameObject(isLeft ? "leftFootAdjuster" : "rightFootAdjuster").transform : leg.target;
        footAdjusterTransform.parent = FootTransform;

        // Space of the tracker heading
        Quaternion frontQuaternion = Quaternion.LookRotation(hmdForwardAngle);

        // Target position
        float inwardOffset = isLeft ? settings.footInwardOffset : -settings.footInwardOffset;
        footAdjusterTransform.position = FootTransform.position + frontQuaternion * new Vector3(inwardOffset, 0f, settings.footForwardOffset);
        footAdjusterTransform.position = new Vector3(footAdjusterTransform.position.x, lastBone.position.y, footAdjusterTransform.position.z);

        // Target rotation
        footAdjusterTransform.rotation = lastBone.rotation;

        // Rotate target forward towards tracker forward
        Vector3 footForward = AxisTools.GetAxisVectorToDirection(lastBone, rootForward);
        if (Vector3.Dot(lastBone.rotation * footForward, rootForward) < 0f) footForward = -footForward;
        Vector3 fLocal = Quaternion.Inverse(Quaternion.LookRotation(footAdjusterTransform.rotation * footForward)) * hmdForwardAngle;
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

    public static IEnumerator CalibrateScaled(Transform handTrackerRoot, Transform headTrackerRoot, Transform footTrackerRoot, VRIK ik, Settings settings, Vector3 LeftHandOffset, Vector3 RightHandOffset, Transform HMDTransform, Transform PelvisTransform = null, Transform LeftHandTransform = null, Transform RightHandTransform = null, Transform LeftFootTransform = null, Transform RightFootTransform = null)
    {
        if (!ik.solver.initiated)
        {
            Debug.LogError("Can not calibrate before VRIK has initiated.");
            yield break;
        }

        if (HMDTransform == null)
        {
            Debug.LogError("Can not calibrate VRIK without the head tracker.");
            yield break;
        }

        //トラッカーのルートスケールを初期値に戻す
        handTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        headTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        footTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        //それぞれのトラッカーを正しいルートに移動
        if (HMDTransform != null) HMDTransform.parent = headTrackerRoot;
        if (LeftHandTransform != null) LeftHandTransform.parent = handTrackerRoot;
        if (RightHandTransform != null) RightHandTransform.parent = handTrackerRoot;
        if (PelvisTransform != null) PelvisTransform.parent = footTrackerRoot;
        if (LeftFootTransform != null) LeftFootTransform.parent = footTrackerRoot;
        if (RightFootTransform != null) RightFootTransform.parent = footTrackerRoot;

        //コントローラーの場合手首までのオフセットを追加
        if (LeftHandOffset == Vector3.zero)
        {
            var offset = new GameObject("LeftWristOffset");
            offset.transform.parent = LeftHandTransform;
            offset.transform.localPosition = new Vector3(-0.04f, 0.04f, -0.15f);
            offset.transform.localRotation = Quaternion.Euler(60, 0, 90);
            offset.transform.localScale = Vector3.one;
            LeftHandTransform = offset.transform;
        }
        if (RightHandOffset == Vector3.zero)
        {
            var offset = new GameObject("RightWristOffset");
            offset.transform.parent = RightHandTransform;
            offset.transform.localPosition = new Vector3(0.04f, 0.04f, -0.15f);
            offset.transform.localRotation = Quaternion.Euler(60, 0, -90);
            offset.transform.localScale = Vector3.one;
            RightHandTransform = offset.transform;
        }


        // モデルのポジションを手と手の中心位置に移動
        var centerposition = Vector3.Lerp(LeftHandTransform.position, RightHandTransform.position, 0.5f);
        ik.references.root.position = new Vector3(centerposition.x, ik.references.root.position.y, centerposition.z);

        yield return new WaitForEndOfFrame();

        //それぞれのトラッカーに手のオフセットを適用
        //トラッカーの角度は 0,-90,-90 が正位置(手の甲に付けてLED部を外側に向けたとき)

        //トラッカー zをマイナス方向に動かすとトラッカーの底面に向かって進む
        //角度補正LookAt(左手なら右のトラッカーに向けた)後 zを＋方向は体中心(左手なら右手の方向)に向かって進む
        //xを－方向は体の正面に向かって進む

        //先にトラッカー底面に向かってのオフセットを両手に適用する
        var leftWrist = new GameObject("LeftWristOffset").transform;
        leftWrist.parent = LeftHandTransform;
        leftWrist.localPosition = new Vector3(0, 0, -LeftHandOffset.y);
        leftWrist.localRotation = Quaternion.Euler(Vector3.zero);

        var rightWrist = new GameObject("RightWristOffset").transform;
        rightWrist.parent = RightHandTransform;
        rightWrist.localPosition = new Vector3(0, 0, -RightHandOffset.y);
        rightWrist.localRotation = Quaternion.Euler(Vector3.zero);

        //お互いのトラッカー同士を向き合わせてオフセットを適用する
        leftWrist.LookAt(rightWrist ?? RightHandTransform);
        leftWrist.localPosition += leftWrist.localRotation * new Vector3(0, 0, LeftHandOffset.z);
        rightWrist.LookAt(leftWrist ?? LeftHandTransform);
        rightWrist.localPosition += rightWrist.localRotation * new Vector3(0, 0, RightHandOffset.z);

        //両手のxをマイナス方向が正面なので体を回転させる
        Vector3 hmdForwardAngle = leftWrist.rotation * new Vector3(-1, 0, 0);
        hmdForwardAngle.y = 0f;
        ik.references.root.rotation = Quaternion.LookRotation(hmdForwardAngle);


        //回転した分トラッカーも回転させる
        if (LeftHandOffset != Vector3.zero) // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
        {
            leftWrist.eulerAngles = new Vector3(0, -90, 90); //トラッカー補正後の角度
            leftWrist.RotateAround(leftWrist.position, Vector3.up, ik.references.root.rotation.eulerAngles.y); //体の回転分を引く
            LeftHandTransform = leftWrist;
        }
        if (RightHandOffset != Vector3.zero) // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
        {
            rightWrist.eulerAngles = new Vector3(180, -90, 90); //トラッカー補正後の角度
            rightWrist.RotateAround(rightWrist.position, Vector3.up, ik.references.root.rotation.eulerAngles.y); //体の回転分を引く
            RightHandTransform = rightWrist;
        }

        yield return new WaitForEndOfFrame();

        //モデルを手の高さで比較してスケールアップさせる
        //なるべく現実の身長に近づけて現実のコントローラー座標とのずれをなくす
        //ずれが大きいとexternalcamera.cfgとの合成時に手がずれすぎておかしくなる
        var modelHandHeight = (ik.references.leftHand.position.y + ik.references.rightHand.position.y) / 2f;
        var realHandHeight = (LeftHandTransform.position.y + RightHandTransform.position.y) / 2f;
        var hscale = realHandHeight / modelHandHeight;
        ik.references.root.localScale = new Vector3(hscale, hscale, hscale);

        //VRMモデルのコライダーがスケールについてこないため、すべて再設定
        var springBoneColiderGroups = ik.references.root.GetComponentsInChildren<VRM.VRMSpringBoneColliderGroup>();
        foreach (var springBoneColiderGroup in springBoneColiderGroups)
        {
            foreach (var colider in springBoneColiderGroup.Colliders)
            {
                colider.Offset *= hscale;
                colider.Radius *= hscale;
            }
        }


        // 手のトラッカー全体のスケールを手の位置に合わせる
        var modelHandDistance = Vector3.Distance(ik.references.leftHand.position, ik.references.rightHand.position);
        var realHandDistance = Vector3.Distance(LeftHandTransform.position, RightHandTransform.position);
        var wscale = modelHandDistance / realHandDistance;
        modelHandHeight = (ik.references.leftHand.position.y + ik.references.rightHand.position.y) / 2f;
        realHandHeight = (LeftHandTransform.position.y + RightHandTransform.position.y) / 2f;
        hscale = modelHandHeight / realHandHeight;
        handTrackerRoot.localScale = new Vector3(wscale, hscale, wscale);

        // モデルのポジションを再度手と手の中心位置に移動
        centerposition = Vector3.Lerp(LeftHandTransform.position, RightHandTransform.position, 0.5f);
        ik.references.root.position = new Vector3(centerposition.x, ik.references.root.position.y, centerposition.z);
        //hmdForwardAngle = HMDTransform.rotation * settings.headTrackerForward;
        //hmdForwardAngle.y = 0f;
        ik.references.root.rotation = Quaternion.LookRotation(hmdForwardAngle);

        // 頭のトラッカー全体のスケールを頭の位置に合わせる
        var modelHeadHeight = ik.references.head.position.y;
        var realHeadHeight = HMDTransform.position.y;
        var headHscale = modelHeadHeight / realHeadHeight;
        headTrackerRoot.localScale = new Vector3(wscale, hscale, wscale);

        // 腰のトラッカー全体のスケールを腰の位置に合わせる
        if (PelvisTransform != null)
        {
            var modelPelvisHeight = ik.references.pelvis.position.y;
            var realPelvisHeight = PelvisTransform.position.y;
            var pelvisHscale = modelPelvisHeight / realPelvisHeight;
            footTrackerRoot.localScale = new Vector3(wscale, pelvisHscale, wscale);
        }

        //腰の子に膝のBendGoal設定用
        var bendGoalTarget = new GameObject("BendGoalTarget");
        bendGoalTarget.transform.parent = ik.references.pelvis;
        bendGoalTarget.transform.localScale = Vector3.one;
        bendGoalTarget.transform.localPosition = new Vector3(0, 0, 1); //正面1m
        bendGoalTarget.transform.localRotation = Quaternion.Euler(Vector3.zero);


        yield return new WaitForEndOfFrame();
        //yield break;

        // Head
        Transform hmdAdjusterTransform = ik.solver.spine.headTarget == null ? (new GameObject("hmdAdjuster")).transform : ik.solver.spine.headTarget;
        hmdAdjusterTransform.parent = HMDTransform;
        hmdAdjusterTransform.position = ik.references.head.position; //HMDTransform.position + HMDTransform.rotation * Quaternion.LookRotation(settings.headTrackerForward, settings.headTrackerUp) * settings.headOffset;
        //hmdAdjusterTransform.localPosition = new Vector3(0, hmdAdjusterTransform.localPosition.y, -0.15f);
        hmdAdjusterTransform.rotation = ik.references.head.rotation;
        ik.solver.spine.headTarget = hmdAdjusterTransform;
        ik.solver.spine.headClampWeight = 0.38f;

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

        yield return new WaitForEndOfFrame();

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
        if (LeftFootTransform != null) CalibrateLeg(settings, LeftFootTransform, ik.solver.leftLeg, (ik.references.leftToes != null ? ik.references.leftToes : ik.references.leftFoot), hmdForwardAngle, ik.references.root.forward, true);
        if (RightFootTransform != null) CalibrateLeg(settings, RightFootTransform, ik.solver.rightLeg, (ik.references.rightToes != null ? ik.references.rightToes : ik.references.rightFoot), hmdForwardAngle, ik.references.root.forward, false);
        ik.solver.leftLeg.bendGoal = bendGoalTarget.transform;
        ik.solver.leftLeg.bendGoalWeight = 1.0f;
        ik.solver.rightLeg.bendGoal = bendGoalTarget.transform;
        ik.solver.rightLeg.bendGoalWeight = 1.0f;

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
}
