using RootMotion;
using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using VRM;

namespace VMC
{

#if UNITY_EDITOR
    [Serializable]
    public class TrackerPosition
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public TrackerPosition() { }
        public TrackerPosition(Transform source)
        {
            if (source == null) return;
            Position = source.position;
            Rotation = source.rotation;
        }
    }

    public class TrackerPositions
    {
        public TrackerPosition Head;
        public TrackerPosition LeftHand;
        public TrackerPosition RightHand;
        public TrackerPosition Pelvis;
        public TrackerPosition LeftFoot;
        public TrackerPosition RightFoot;
    }
#endif

    public class FinalIKCalibrator
    {

        public static IEnumerator Calibrate(Transform handTrackerRoot, Transform footTrackerRoot, VRIK vrik, VRIKCalibrator.Settings settings, TrackingPoint HMDTrackingPoint, TrackingPoint PelvisTrackingPoint = null, TrackingPoint LeftHandTrackingPoint = null, TrackingPoint RightHandTrackingPoint = null, TrackingPoint LeftFootTrackingPoint = null, TrackingPoint RightFootTrackingPoint = null, TrackingPoint LeftElbowTrackingPoint = null, TrackingPoint RightElbowTrackingPoint = null, TrackingPoint LeftKneeTrackingPoint = null, TrackingPoint RightKneeTrackingPoint = null)
        {
            var currentModel = vrik.transform;

#if UNITY_EDITOR
            var trackerPositions = new TrackerPositions
            {
                Head = new TrackerPosition(HMDTrackingPoint?.TargetTransform),
                LeftHand = new TrackerPosition(LeftHandTrackingPoint?.TargetTransform),
                RightHand = new TrackerPosition(RightHandTrackingPoint?.TargetTransform),
                Pelvis = new TrackerPosition(PelvisTrackingPoint?.TargetTransform),
                LeftFoot = new TrackerPosition(LeftFootTrackingPoint?.TargetTransform),
                RightFoot = new TrackerPosition(RightFootTrackingPoint?.TargetTransform),
            };

            var trackerPositionsJson = JsonUtility.ToJson(trackerPositions);
            GUIUtility.systemCopyBuffer = trackerPositionsJson;
#endif

            vrik.enabled = false;
            yield return null;

            //それぞれのトラッカーを正しいルートに移動
            if (HMDTrackingPoint != null) HMDTrackingPoint.TargetTransform.parent = footTrackerRoot;
            else throw new Exception("Head tracker not found");
            if (LeftHandTrackingPoint != null) LeftHandTrackingPoint.TargetTransform.parent = handTrackerRoot;
            else throw new Exception("Left hand tracker not found");
            if (RightHandTrackingPoint != null) RightHandTrackingPoint.TargetTransform.parent = handTrackerRoot;
            else throw new Exception("Right hand tracker not found");
            if (PelvisTrackingPoint != null) PelvisTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (LeftFootTrackingPoint != null) LeftFootTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (RightFootTrackingPoint != null) RightFootTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (LeftElbowTrackingPoint != null) LeftElbowTrackingPoint.TargetTransform.parent = handTrackerRoot;
            if (RightElbowTrackingPoint != null) RightElbowTrackingPoint.TargetTransform.parent = handTrackerRoot;
            if (LeftKneeTrackingPoint != null) LeftKneeTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (RightKneeTrackingPoint != null) RightKneeTrackingPoint.TargetTransform.parent = footTrackerRoot;


            var headTarget = HMDTrackingPoint;

            var leftHandTarget = LeftHandTrackingPoint;
            var rightHandTarget = RightHandTrackingPoint;
            var leftHandTargetTransform = leftHandTarget.TargetTransform;
            var rightHandTargetTransform = rightHandTarget.TargetTransform;

            //IKの手のターゲットは手首なのでトラッカーに手首までのオフセットを設定

            if (LeftHandTrackingPoint.DeviceClass == ETrackedDeviceClass.Controller)
            {
                //コントローラーの場合手首までのオフセットを追加
                var offset = new GameObject("LeftWristOffset").transform;
                offset.parent = leftHandTargetTransform;
                offset.localPosition = new Vector3(-0.04f, 0.04f, -0.15f);
                offset.localRotation = Quaternion.Euler(60, 0, 90);
                offset.localScale = Vector3.one;
                leftHandTargetTransform = offset;
            }
            else if (LeftHandTrackingPoint.DeviceClass == ETrackedDeviceClass.GenericTracker)
            {
                //トラッカーの場合設定のオフセットを適用

                //お互いのトラッカー同士を向き合わせてオフセットを適用する
                var leftWristLookAtTransform = new GameObject("LeftWristLookAt").transform;
                leftWristLookAtTransform.SetParent(leftHandTargetTransform);
                leftWristLookAtTransform.localPosition = Vector3.zero;
                leftWristLookAtTransform.localRotation = Quaternion.identity;
                leftWristLookAtTransform.LookAt(rightHandTargetTransform);
                var leftWrist = new GameObject("LeftWristOffset").transform;
                leftWrist.parent = leftWristLookAtTransform;
                leftWrist.localPosition = new Vector3(0, Settings.Current.LeftHandTrackerOffsetToBottom, Settings.Current.LeftHandTrackerOffsetToBodySide);
                leftWrist.localRotation = Quaternion.Euler(Vector3.zero);
                leftHandTargetTransform = leftWrist;
            }

            if (RightHandTrackingPoint.DeviceClass == ETrackedDeviceClass.Controller)
            {
                var offset = new GameObject("RightWristOffset").transform;
                offset.parent = rightHandTargetTransform;
                offset.localPosition = new Vector3(0.04f, 0.04f, -0.15f);
                offset.localRotation = Quaternion.Euler(60, 0, -90);
                offset.localScale = Vector3.one;
                rightHandTargetTransform = offset;
            }
            else if (RightHandTrackingPoint.DeviceClass == ETrackedDeviceClass.GenericTracker)
            {
                //トラッカーの場合設定のオフセットを適用

                //お互いのトラッカー同士を向き合わせてオフセットを適用する
                var rightWristLookAtTransform = new GameObject("RightWristLookAt").transform;
                rightWristLookAtTransform.SetParent(rightHandTargetTransform);
                rightWristLookAtTransform.localPosition = Vector3.zero;
                rightWristLookAtTransform.localRotation = Quaternion.identity;
                rightWristLookAtTransform.LookAt(leftHandTargetTransform);
                var rightWrist = new GameObject("RightWristOffset").transform;
                rightWrist.parent = rightWristLookAtTransform;
                rightWrist.localPosition = new Vector3(0, Settings.Current.RightHandTrackerOffsetToBottom, Settings.Current.RightHandTrackerOffsetToBodySide);
                rightWrist.localRotation = Quaternion.Euler(Vector3.zero);
                rightHandTargetTransform = rightWrist;
            }


            //手の高さから身長を算出する
            // B5 頚窩高 1352.1
            // B1 身長 1654.7
            // ratio = B1 / B5 = 1.2238000147918053398417276828637
            var handHeight = (leftHandTargetTransform.position.y + rightHandTargetTransform.position.y) / 2f;
            var realHeight = handHeight * 1.2238f;

            Debug.Log($"UserHeight:{realHeight}");


            // トラッカー全体のスケールを手の位置に合わせる
            // スケールを動かしてから位置を取らないとモデルの位置がずれる
            var leftHand = vrik.references.leftHand;
            var rightHand = vrik.references.rightHand;
            var modelHandDistance = Vector3.Distance(leftHand.position, rightHand.position);
            var realHandDistance = Vector3.Distance(leftHandTargetTransform.position, rightHandTargetTransform.position);
            var wscale = modelHandDistance / realHandDistance;
            handTrackerRoot.localScale = new Vector3(wscale, wscale, wscale);
            footTrackerRoot.localScale = new Vector3(wscale, wscale, wscale);


            //モデルの体の中心を取っておく
            var modelcenterposition = Vector3.Lerp(vrik.references.leftHand.position, vrik.references.rightHand.position, 0.5f);
            modelcenterposition = new Vector3(modelcenterposition.x, vrik.references.root.position.y, modelcenterposition.z);
            var modelcenterdistance = Vector3.Distance(vrik.references.root.position, modelcenterposition);

            //両手のxをマイナス方向が正面なので体を回転させる
            Vector3 hmdForwardAngle = leftHandTargetTransform.rotation * new Vector3(-1, 0, 0);
            hmdForwardAngle.y = 0f;
            currentModel.rotation = Quaternion.LookRotation(hmdForwardAngle);

            // モデルのポジションを手と手の中心位置に移動
            var centerposition = Vector3.Lerp(leftHandTargetTransform.position, rightHandTargetTransform.position, 0.5f);
            currentModel.position = new Vector3(centerposition.x, currentModel.position.y, centerposition.z) + currentModel.forward * modelcenterdistance;

            //身長から腰の位置を算出する
            // B12 臍高 965.7
            // B1 身長 1654.7
            // ratio = B13 / B1 = 0.58361032211276968634797848552608
            //腰補正値 0.03303499793413632250187205544126 * B1
            var realPelvisHeight = realHeight * 0.58361f;
            var realPelvisPosition = new Vector3(centerposition.x, realPelvisHeight, centerposition.z) + currentModel.forward * (realHeight * 0.03f);

            Debug.Log($"realPelvisHeight:{realPelvisHeight}");


            var scaledPelvisPosition = new Vector3(centerposition.x, realPelvisHeight * wscale, centerposition.z) + currentModel.forward * (realHeight * 0.04f * wscale);

            Debug.Log($"wscale:{wscale}");
            Debug.Log($"scaledPelvisHeight:{scaledPelvisPosition.y}");

            //頭と腰と足と膝と胸のトラッカーを腰の高さにオフセット
            var modelPelvisHeight = vrik.references.pelvis.position.y;
            var footTrackerOffset = new Vector3(0, modelPelvisHeight - scaledPelvisPosition.y, 0);
            footTrackerRoot.position = footTrackerOffset;

            //手と肘トラッカーを手の高さにオフセット
            var modelHandHeight = (vrik.references.leftHand.position.y + vrik.references.rightHand.position.y) / 2f;
            var realHandHeight = (leftHandTargetTransform.position.y + rightHandTargetTransform.position.y) / 2f;
            var realLeftHandHeight = leftHandTargetTransform.position.y;
            var realRightHandHeight = rightHandTargetTransform.position.y;
            var handTrackerOffset = new Vector3(0, modelHandHeight - realHandHeight, 0);
            var leftHandTrackerOffset = new Vector3(0, modelHandHeight - realLeftHandHeight, 0);
            var rightHandTrackerOffset = new Vector3(0, modelHandHeight - realRightHandHeight, 0);


            vrik.enabled = true;


            // Head
            var headOffsetPosition = new Vector3(vrik.references.head.position.x, vrik.references.head.position.y + (realHeight * 0.01f * wscale), vrik.references.head.position.z);
            var headOffset = CreateTransform("HeadIKTarget", true, headTarget.TargetTransform, headOffsetPosition, vrik.references.head.rotation);
            vrik.solver.spine.headTarget = headOffset;
            vrik.solver.spine.positionWeight = 1f;
            vrik.solver.spine.rotationWeight = 1f;

            // 頭のトラッキングの補正度合いを変更
            vrik.solver.spine.minHeadHeight = 0;
            vrik.solver.spine.neckStiffness = 0.1f;
            vrik.solver.spine.headClampWeight = 0;
            vrik.solver.spine.maxRootAngle = 20;

            //手のひら正面向けてるのでアバターも向けておく
            vrik.references.leftHand.Rotate(new Vector3(-90, 0, 0));
            vrik.references.rightHand.Rotate(new Vector3(-90, 0, 0));

            // LeftHand
            handTrackerRoot.position = leftHandTrackerOffset;
            var leftHandOffset = CreateTransform("LeftHandIKTarget", true, leftHandTargetTransform, vrik.references.leftHand);
            vrik.solver.leftArm.target = leftHandOffset;
            vrik.solver.leftArm.positionWeight = 1f;
            vrik.solver.leftArm.rotationWeight = 1f;

            //肩が回りすぎないように
            vrik.solver.leftArm.shoulderRotationMode = IKSolverVR.Arm.ShoulderRotationMode.FromTo;
            vrik.solver.leftArm.shoulderRotationWeight = 0.3f;
            vrik.solver.leftArm.shoulderTwistWeight = 0.7f;


            // RightHand
            handTrackerRoot.position = rightHandTrackerOffset;
            var rightHandOffset = CreateTransform("RightHandIKTarget", true, rightHandTargetTransform, vrik.references.rightHand);
            vrik.solver.rightArm.target = rightHandOffset;
            vrik.solver.rightArm.positionWeight = 1f;
            vrik.solver.rightArm.rotationWeight = 1f;

            //肩が回りすぎないように
            vrik.solver.rightArm.shoulderRotationMode = IKSolverVR.Arm.ShoulderRotationMode.FromTo;
            vrik.solver.rightArm.shoulderRotationWeight = 0.25f;
            vrik.solver.rightArm.shoulderTwistWeight = 0.7f;


            //手の回転軸を少し上に補正
            handTrackerRoot.position = handTrackerOffset + Vector3.up * (realHeight * 0.0145f);

            // Pelvis
            var pelvisTargetTransform = PelvisTrackingPoint?.TargetTransform;
            if (pelvisTargetTransform != null)
            {
                var pelvisOffset = CreateTransform("PelvisIKTarget", true, pelvisTargetTransform, scaledPelvisPosition + footTrackerOffset, vrik.references.pelvis.rotation);


                //var PositionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //PositionSphere.transform.position = scaledPelvisPosition + footTrackerOffset;
                //PositionSphere.transform.localScale = new Vector3(1f, 0.1f, 0.1f);

                vrik.solver.spine.pelvisTarget = pelvisOffset;
                vrik.solver.spine.pelvisPositionWeight = 1f;
                vrik.solver.spine.pelvisRotationWeight = 1f;

                vrik.solver.plantFeet = false;
                vrik.solver.spine.neckStiffness = 0f;
                vrik.solver.spine.maxRootAngle = 180f;
            }

            // 腰のトラッキングを調整
            vrik.solver.spine.maintainPelvisPosition = 0; // アバターによって腰がグリングリンするのが直ります

            var leftFootTargetTransform = LeftFootTrackingPoint?.TargetTransform;
            var rightFootTargetTransform = RightFootTrackingPoint?.TargetTransform;

            if (leftFootTargetTransform != null || rightFootTargetTransform != null)
            {
                // 足トラッカーがあるときはLocomotion無効
                vrik.solver.locomotion.weight = 0.0f;

                if (pelvisTargetTransform != null)
                {
                    //足も腰もある時は頭のPositionを弱くして背骨を曲がりにくくする
                    vrik.solver.spine.positionWeight = 0.4f;
                }
            }
            else
            {
                // 足トラッカーが無いとき
            }

            if (leftFootTargetTransform != null)
            {
                var footBone = vrik.references.leftToes != null ? vrik.references.leftToes : vrik.references.leftFoot;
                var leftFootOffset = CreateTransform("LeftFootIKTarget", true, leftFootTargetTransform, footBone);
                vrik.solver.leftLeg.target = leftFootOffset;
                vrik.solver.leftLeg.positionWeight = 1f;
                vrik.solver.leftLeg.rotationWeight = 1f;

                var bendGoal = CreateTransform("LeftFootBendGoal", true, leftFootTargetTransform);
                bendGoal.position = footBone.position + currentModel.forward + currentModel.up;
                vrik.solver.leftLeg.bendGoal = bendGoal;
                vrik.solver.leftLeg.bendGoalWeight = 0.7f;
                //vrik.solver.leftLeg.bendToTargetWeight = 1.0f;
            }
            else
            {
                // アバターの足の位置についていくオブジェクト(FinalIKの処理順に影響を受けない)
                var leftFootFollow = CreateTransform("LeftFootFollowObject", true, null, vrik.references.leftFoot);
                var follower = leftFootFollow.gameObject.AddComponent<TransformFollower>();
                follower.Target = vrik.references.leftFoot;

                // 腰の子に膝のBendGoal設定用(足トラッカーが無いとき利用される)
                var bendGoalTarget = CreateTransform("LeftFootBendGoalTarget", true, leftFootFollow);
                bendGoalTarget.localPosition = new Vector3(0, 0.4f, 2); // 正面2m 高さ40cm
                bendGoalTarget.localRotation = Quaternion.identity;
                vrik.solver.leftLeg.bendGoal = bendGoalTarget;
                vrik.solver.leftLeg.bendGoalWeight = 1.0f;
            }

            if (rightFootTargetTransform != null)
            {
                var footBone = vrik.references.rightToes != null ? vrik.references.rightToes : vrik.references.rightFoot;
                var rightFootOffset = CreateTransform("RightFootIKTarget", true, rightFootTargetTransform, footBone);
                vrik.solver.rightLeg.target = rightFootOffset;
                vrik.solver.rightLeg.positionWeight = 1f;
                vrik.solver.rightLeg.rotationWeight = 1f;

                var bendGoal = CreateTransform("RightFootBendGoal", true, rightFootTargetTransform);
                bendGoal.position = footBone.position + currentModel.forward + currentModel.up;
                vrik.solver.rightLeg.bendGoal = bendGoal;
                vrik.solver.rightLeg.bendGoalWeight = 0.7f;
                //vrik.solver.rightLeg.bendToTargetWeight = 1.0f;
            }
            else
            {
                // アバターの足の位置についていくオブジェクト(FinalIKの処理順に影響を受けない)
                var rightFootFollow = CreateTransform("RightFootFollowObject", true, null, vrik.references.rightFoot);
                var follower = rightFootFollow.gameObject.AddComponent<TransformFollower>();
                follower.Target = vrik.references.rightFoot;

                // 腰の子に膝のBendGoal設定用(足トラッカーが無いとき利用される)
                var bendGoalTarget = CreateTransform("RightFootBendGoalTarget", true, rightFootFollow);
                bendGoalTarget.localPosition = new Vector3(0, 0.4f, 2); // 正面2m 高さ40cm
                bendGoalTarget.localRotation = Quaternion.identity;
                vrik.solver.rightLeg.bendGoal = bendGoalTarget;
                vrik.solver.rightLeg.bendGoalWeight = 1.0f;
            }

            // 腰トラッカーか両足トラッカーがある場合VRIKRootControllerを使用しないと
            // (特に)180度後ろを向いたときに正しい膝の方向計算ができません
            if (pelvisTargetTransform != null || (leftFootTargetTransform != null && rightFootTargetTransform != null))
            {
                var vrikRootController = vrik.references.root.gameObject.AddComponent<VRIKRootController>();
            }

            //wristRotationFix = currentModel.AddComponent<WristRotationFix>();
            //wristRotationFix.SetVRIK(vrik);

            vrik.solver.IKPositionWeight = 1.0f;

            vrik.UpdateSolverExternal();

            //DebugSphere(leftHandTargetTransform);
            //DebugSphere(rightHandTargetTransform);
            //DebugSphere(leftHand);
            //DebugSphere(rightHand);
            //DebugSphere(headOffset);
            //DebugSphere(vrik.references.head);
            //DebugSphere(pelvisTargetTransform);
            //DebugSphere(vrik.references.pelvis);
            //DebugSphere(leftFootTargetTransform);
            //DebugSphere(vrik.references.leftFoot);
            //DebugSphere(rightFootTargetTransform);
            //DebugSphere(vrik.references.rightFoot);

            yield return null;
        }

        private static Transform CreateTransform(string name, bool AddDestroy, Transform parent)
            => CreateTransform(name, AddDestroy, parent, null, null);
        private static Transform CreateTransform(string name, bool AddDestroy, Transform parent, Transform placeTransform)
            => CreateTransform(name, AddDestroy, parent, placeTransform != null ? placeTransform.position : null as Vector3?, placeTransform != null ? placeTransform.rotation : null as Quaternion?);
        private static Transform CreateTransform(string name, bool AddDestroy, Transform parent, Vector3? position, Quaternion? rotation)
        {
            var newGameObject = new GameObject(name);
            //if (AddDestroy) GeneratedGameObjects.Add(newGameObject);
            var t = newGameObject.transform;
            if (parent != null) t.SetParent(parent, false);
            if (position != null) t.position = position.Value;
            if (rotation != null) t.rotation = rotation.Value;
            return t;
        }

        private static void DebugSphere(Transform parent)
        {
            var PositionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            PositionSphere.transform.SetParent(parent, false);
            PositionSphere.transform.localPosition = Vector3.zero;
            PositionSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        }
    }
}
