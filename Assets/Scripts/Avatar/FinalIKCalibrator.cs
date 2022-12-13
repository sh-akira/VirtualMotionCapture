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

    [Serializable]
    public class TrackerPosition
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public ETrackedDeviceClass DeviceClass;

        public TrackerPosition() { }
        public TrackerPosition(TrackingPoint source)
        {
            if (source == null) return;
            Position = source.TargetTransform.position;
            Rotation = source.TargetTransform.rotation;
            DeviceClass = source.DeviceClass;
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
        public TrackerPosition LeftElbow;
        public TrackerPosition RightElbow;
        public TrackerPosition LeftKnee;
        public TrackerPosition RightKnee;
    }

    public class FinalIKCalibrator
    {

        public enum CalibrateMode
        {
            Ipose,
            Tpose,
        }

        /// <summary>
        /// 通常モード、I/Tポーズキャリブレーション
        /// </summary>
        public static IEnumerator Calibrate(CalibrateMode calibrateMode, Transform handTrackerRoot, Transform footTrackerRoot, VRIK vrik, VRIKCalibrator.Settings settings, TrackingPoint HMDTrackingPoint, TrackingPoint PelvisTrackingPoint = null, TrackingPoint LeftHandTrackingPoint = null, TrackingPoint RightHandTrackingPoint = null, TrackingPoint LeftFootTrackingPoint = null, TrackingPoint RightFootTrackingPoint = null, TrackingPoint LeftElbowTrackingPoint = null, TrackingPoint RightElbowTrackingPoint = null, TrackingPoint LeftKneeTrackingPoint = null, TrackingPoint RightKneeTrackingPoint = null, Transform generatedObject = null)
        {
            var currentModel = vrik.transform;

#if UNITY_EDITOR
            var trackerPositions = new TrackerPositions
            {
                Head = new TrackerPosition(HMDTrackingPoint),
                LeftHand = new TrackerPosition(LeftHandTrackingPoint),
                RightHand = new TrackerPosition(RightHandTrackingPoint),
                Pelvis = new TrackerPosition(PelvisTrackingPoint),
                LeftFoot = new TrackerPosition(LeftFootTrackingPoint),
                RightFoot = new TrackerPosition(RightFootTrackingPoint),
            };

            var trackerPositionsJson = JsonUtility.ToJson(trackerPositions);
            GUIUtility.systemCopyBuffer = trackerPositionsJson;
#endif

            vrik.enabled = false;
            yield return null;

            //それぞれのトラッカーを正しいルートに移動
            if (HMDTrackingPoint != null) HMDTrackingPoint.TargetTransform.parent = footTrackerRoot;
            else { Debug.LogError("Head tracker not found"); yield break; }
            if (LeftHandTrackingPoint != null) LeftHandTrackingPoint.TargetTransform.parent = handTrackerRoot;
            else { Debug.LogError("Left hand tracker not found"); yield break; }
            if (RightHandTrackingPoint != null) RightHandTrackingPoint.TargetTransform.parent = handTrackerRoot;
            else { Debug.LogError("Right hand tracker not found"); yield break; }
            if (PelvisTrackingPoint != null) PelvisTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (LeftFootTrackingPoint != null) LeftFootTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (RightFootTrackingPoint != null) RightFootTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (LeftElbowTrackingPoint != null) LeftElbowTrackingPoint.TargetTransform.parent = handTrackerRoot;
            if (RightElbowTrackingPoint != null) RightElbowTrackingPoint.TargetTransform.parent = handTrackerRoot;
            if (LeftKneeTrackingPoint != null) LeftKneeTrackingPoint.TargetTransform.parent = footTrackerRoot;
            if (RightKneeTrackingPoint != null) RightKneeTrackingPoint.TargetTransform.parent = footTrackerRoot;


            var headTarget = HMDTrackingPoint;

            var leftHandTargetTransform = LeftHandTrackingPoint.TargetTransform;
            var rightHandTargetTransform = RightHandTrackingPoint.TargetTransform;

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
                if (calibrateMode == CalibrateMode.Ipose)
                {
                    leftWrist.localPosition = new Vector3(0, Settings.Current.LeftHandTrackerOffsetToBodySide, Settings.Current.LeftHandTrackerOffsetToBottom);
                }
                else if (calibrateMode == CalibrateMode.Tpose)
                {
                    leftWrist.localPosition = new Vector3(0, Settings.Current.LeftHandTrackerOffsetToBottom, Settings.Current.LeftHandTrackerOffsetToBodySide);
                }
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
                if (calibrateMode == CalibrateMode.Ipose)
                {
                    rightWrist.localPosition = new Vector3(0, Settings.Current.RightHandTrackerOffsetToBodySide, Settings.Current.RightHandTrackerOffsetToBottom);
                }
                else if (calibrateMode == CalibrateMode.Tpose)
                {
                    rightWrist.localPosition = new Vector3(0, Settings.Current.RightHandTrackerOffsetToBottom, Settings.Current.RightHandTrackerOffsetToBodySide);
                }
                rightWrist.localRotation = Quaternion.Euler(Vector3.zero);
                rightHandTargetTransform = rightWrist;
            }

            float realHeight = 1.7f;

            //頭がHMDの場合
            if (headTarget.DeviceClass == ETrackedDeviceClass.HMD)
            {
                //目の高さから身長を算出する
                // A30 頭頂・内眼角距離 124.6
                // B1 身長 1654.7
                // realHeight = (HMDHeight / (B1 - A30)) * A30 + HMDHeight
                var hmdHeight = headTarget.TargetTransform.position.y;
                realHeight = (hmdHeight / (1.6547f - 0.1246f)) * 0.1246f + hmdHeight;
            }
            else
            {
                //手の高さから身長を算出する
                if (calibrateMode == CalibrateMode.Ipose)
                {
                    // B21 橈骨茎突高 797.7
                    // B1 身長 1654.7
                    // ratio = B1 / B21 = 2.0743387238310141657264635828006
                    // 補正値 0.93694267481427684002272492175445
                    var handHeight = (leftHandTargetTransform.position.y + rightHandTargetTransform.position.y) / 2f;
                    realHeight = handHeight * 2.07434f * 0.93694f;
                }
                else if (calibrateMode == CalibrateMode.Tpose)
                {
                    // B5 頚窩高 1352.1
                    // B1 身長 1654.7
                    // ratio = B1 / B5 = 1.2238000147918053398417276828637
                    var handHeight = (leftHandTargetTransform.position.y + rightHandTargetTransform.position.y) / 2f;
                    realHeight = handHeight * 1.2238f;
                }
            }
            Debug.Log($"UserHeight:{realHeight}");

            // トラッカー全体のスケールを手の位置に合わせる
            // スケールを動かしてから位置を取らないとモデルの位置がずれる
            var leftHand = vrik.references.leftHand;
            var rightHand = vrik.references.rightHand;
            var offsetScale = 1.0f;
            if (calibrateMode == CalibrateMode.Ipose)
            {
                var leftUpperArm = vrik.references.leftUpperArm;
                var rightUpperArm = vrik.references.rightUpperArm;
                var modelHandDistance = Vector3.Distance(leftHand.position, leftUpperArm.position) + Vector3.Distance(leftUpperArm.position, rightUpperArm.position) + Vector3.Distance(rightUpperArm.position, rightHand.position);

                Debug.Log($"modelHandDistance:{modelHandDistance}");

                //身長比 (realHeight / B1)
                var realHeightRatio = realHeight / 1.6547f;
                //C7 上腕長 301.2
                var realUpperArmLength = realHeightRatio * 0.3012f;
                //C8 前腕長 240.5
                var realLowerArmLength = realHeightRatio * 0.2405f;
                //D7 肩峰幅 378.8
                var realShoulderWidth = realHeightRatio * 0.3788f;

                var realHandDistance = (realShoulderWidth + realUpperArmLength * 2 + realLowerArmLength * 2);

                Debug.Log($"realHandDistance:{realHandDistance} realUpperArmLength:{realUpperArmLength} realLowerArmLength:{realLowerArmLength} realShoulderWidth:{realShoulderWidth}");

                //補正値 1.15
                var realScale = modelHandDistance / realHandDistance;
                offsetScale = realScale * 1.15f;
            }
            else if (calibrateMode == CalibrateMode.Tpose)
            {
                var modelHandDistance = Vector3.Distance(leftHand.position, rightHand.position);
                var realHandDistance = Vector3.Distance(leftHandTargetTransform.position, rightHandTargetTransform.position);
                var realScale = modelHandDistance / realHandDistance;
                offsetScale = realScale * 1.00f;
            }
            handTrackerRoot.localScale = new Vector3(offsetScale, offsetScale, offsetScale);
            footTrackerRoot.localScale = new Vector3(offsetScale, offsetScale, offsetScale);

            Debug.Log($"wscale:{offsetScale}");

            //モデルの体の中心を取っておく
            var handcenterposition = Vector3.Lerp(vrik.references.leftHand.position, vrik.references.rightHand.position, 0.5f);
            handcenterposition = new Vector3(handcenterposition.x, vrik.references.root.position.y, handcenterposition.z);
            var pelviscenterposition = new Vector3(vrik.references.pelvis.position.x, vrik.references.root.position.y, vrik.references.pelvis.position.z);
            var modelpelviscenterdistance = Vector3.Distance(pelviscenterposition, vrik.references.root.position) * ((pelviscenterposition.z - vrik.references.root.position.z >= 0) ? 1 : -1);

            //両手間のベクトルと上方向の外積が体の正面なので体を回転させる
            Vector3 hmdForwardAngle = Vector3.Cross(Vector3.up, leftHandTargetTransform.position - rightHandTargetTransform.position);
            currentModel.rotation = Quaternion.LookRotation(hmdForwardAngle);


            // リアル手と手の中心から少し後ろに下げた位置
            var scaledCenterPosition = Vector3.Lerp(leftHandTargetTransform.position, rightHandTargetTransform.position, 0.5f) - hmdForwardAngle.normalized * (realHeight * offsetScale * 0.052f);

            //頭がHMDの場合
            if (headTarget.DeviceClass == ETrackedDeviceClass.HMD)
            {
                scaledCenterPosition = headTarget.TargetTransform.position - hmdForwardAngle.normalized * (realHeight * offsetScale * 0.043f);
            }

            //身長から腰の位置を算出する
            // B13 上前腸骨棘高 891.9
            // B14 恥骨結合上縁高 809.2
            // B1 身長 1654.7
            // ratio = (B13 + (B14 - B13) * 0.?) / B1 = 0.51402066839910557805040188553816
            //腰補正値 0.95
            var realPelvisHeight = realHeight * (809.2f + (891.9f - 809.2f) * 0.67f) / 1654.7f * 0.95f;

            Debug.Log($"realPelvisHeight:{realPelvisHeight}");

            var scaledPelvisPosition = new Vector3(scaledCenterPosition.x, realPelvisHeight * offsetScale, scaledCenterPosition.z);

            Debug.Log($"scaledPelvisHeight:{scaledPelvisPosition.y}");

            //アバターの腰のXZ位置をリアルの腰の位置に合わせる
            currentModel.position = new Vector3(scaledPelvisPosition.x, currentModel.position.y, scaledPelvisPosition.z) - currentModel.forward * modelpelviscenterdistance;


            //計算された腰の回転軸位置にオブジェクトを設定
            var pelvisTargetTransform = PelvisTrackingPoint?.TargetTransform;

            if (pelvisTargetTransform != null)
            {
                var pelvisRotatePoint = new GameObject("PelvisRotatePoint").transform;
                pelvisRotatePoint.SetParent(pelvisTargetTransform);
                pelvisRotatePoint.position = scaledPelvisPosition;
                pelvisRotatePoint.rotation = Quaternion.identity;
                pelvisTargetTransform = pelvisRotatePoint;
            }

            //HipボーンはChestボーンとUpperLegに繋がっているので、その場で回転させるとChestを下に引っ張り、UpperLegを上に引っ張ることになるので、UpperLegを中心に回転するようにオフセット設定してかかとが浮かないようにする
            var UpperLegOffset = vrik.references.pelvis.position - Vector3.Lerp(vrik.references.leftThigh.position, vrik.references.rightThigh.position, 0.5f);

            //頭と腰と足と膝と胸のトラッカーを腰の回転軸(UpperLeg)の高さにオフセット
            var modelPelvisHeight = (vrik.references.leftThigh.position.y + vrik.references.rightThigh.position.y) / 2;
            var footTrackerOffset = new Vector3(0, modelPelvisHeight - scaledPelvisPosition.y, 0);
            footTrackerRoot.position = footTrackerOffset;

            if (calibrateMode == CalibrateMode.Ipose)
            {
                //腕をおろしているのでアバターもおろしておく
                vrik.references.leftShoulder.localEulerAngles = new Vector3(0, 0, 0);
                vrik.references.leftUpperArm.localEulerAngles = new Vector3(0, 0, 80);
                vrik.references.leftForearm.localEulerAngles = new Vector3(0, 0, 5);
                vrik.references.leftHand.localEulerAngles = new Vector3(0, 0, 0);
                vrik.references.rightShoulder.localEulerAngles = new Vector3(0, 0, 0);
                vrik.references.rightUpperArm.localEulerAngles = new Vector3(0, 0, -80);
                vrik.references.rightForearm.localEulerAngles = new Vector3(0, 0, -5);
                vrik.references.rightHand.localEulerAngles = new Vector3(0, 0, 0);
            }
            else if (calibrateMode == CalibrateMode.Tpose)
            {
                //手のひら正面向けてるのでアバターも向けておく
                vrik.references.leftHand.Rotate(new Vector3(-90, 0, 0));
                vrik.references.rightHand.Rotate(new Vector3(-90, 0, 0));
            }

            //手と肘トラッカーを手の高さにオフセット
            var modelHandHeight = (vrik.references.leftHand.position.y + vrik.references.rightHand.position.y) / 2f;
            var realHandHeight = (leftHandTargetTransform.position.y + rightHandTargetTransform.position.y) / 2f;
            var handTrackerOffset = new Vector3(0, modelHandHeight - realHandHeight, 0);
            var realLeftHandHeight = leftHandTargetTransform.position.y;
            var realRightHandHeight = rightHandTargetTransform.position.y;
            var TposeOffset = realHeight * 0.025f;
            var leftHandTrackerOffset = new Vector3(0, modelHandHeight - realLeftHandHeight + TposeOffset, 0);
            var rightHandTrackerOffset = new Vector3(0, modelHandHeight - realRightHandHeight + TposeOffset, 0);
            if (calibrateMode == CalibrateMode.Ipose)
            {
                handTrackerRoot.position = handTrackerOffset;
            }

            // Head
            //頭の位置は1cm前後後ろに下げる
            var headTargetTransform = headTarget.TargetTransform;
            var headOffsetPosition = new Vector3(vrik.references.head.position.x, vrik.references.head.position.y, vrik.references.head.position.z - (realHeight * 0.01f * offsetScale));
            var headOffset = CreateTransform("HeadIKTarget", true, headTargetTransform, headOffsetPosition, vrik.references.head.rotation);
            vrik.solver.spine.headTarget = headOffset;
            vrik.solver.spine.positionWeight = 1f;
            vrik.solver.spine.rotationWeight = 1f;

            // 頭のトラッキングの補正度合いを変更
            vrik.solver.spine.minHeadHeight = 0;
            vrik.solver.spine.neckStiffness = 0.1f;
            vrik.solver.spine.headClampWeight = 0;
            vrik.solver.spine.maxRootAngle = 20;

            // LeftHand
            if (calibrateMode == CalibrateMode.Tpose)
            {
                handTrackerRoot.position = leftHandTrackerOffset;
            }
            var leftHandOffset = CreateTransform("LeftHandIKTarget", true, leftHandTargetTransform, vrik.references.leftHand);
            leftHandOffset.localPosition = Vector3.zero;

            vrik.solver.leftArm.target = leftHandOffset;
            vrik.solver.leftArm.positionWeight = 1f;
            vrik.solver.leftArm.rotationWeight = 1f;

            //肩が回りすぎないように
            vrik.solver.leftArm.shoulderRotationMode = IKSolverVR.Arm.ShoulderRotationMode.FromTo;
            vrik.solver.leftArm.shoulderRotationWeight = 0.3f;
            vrik.solver.leftArm.shoulderTwistWeight = 0.7f;


            // RightHand
            if (calibrateMode == CalibrateMode.Tpose)
            {
                handTrackerRoot.position = rightHandTrackerOffset;
            }
            var rightHandOffset = CreateTransform("RightHandIKTarget", true, rightHandTargetTransform, vrik.references.rightHand);
            rightHandOffset.localPosition = Vector3.zero;
            
            vrik.solver.rightArm.target = rightHandOffset;
            vrik.solver.rightArm.positionWeight = 1f;
            vrik.solver.rightArm.rotationWeight = 1f;

            //肩が回りすぎないように
            vrik.solver.rightArm.shoulderRotationMode = IKSolverVR.Arm.ShoulderRotationMode.FromTo;
            vrik.solver.rightArm.shoulderRotationWeight = 0.25f;
            vrik.solver.rightArm.shoulderTwistWeight = 0.7f;

            //手の回転軸を少し上に補正
            //handTrackerRoot.position = handTrackerOffset + Vector3.up * (realHeight * 0.0145f);

            // Pelvis
            if (pelvisTargetTransform != null)
            {
                //実際の回転位置のY方向を補正して付いてくるオブジェクト(上下方向補正)
                var PelvisAdjustFollower = new GameObject("PelvisAdjustFollower");
                PelvisAdjustFollower.AddComponent<TransformAdjustFollower>().Initialize(pelvisTargetTransform, true, true);
                pelvisTargetTransform = PelvisAdjustFollower.transform;
                pelvisTargetTransform.SetParent(generatedObject);

                //pelvisTargetTransformは腰の回転軸位置(UpperLeg)に居るので、子のIKターゲットはHip位置にオフセットして指定
                var pelvisOffset = CreateTransform("PelvisIKTarget", true, pelvisTargetTransform, pelvisTargetTransform.position + UpperLegOffset, vrik.references.pelvis.rotation);

                vrik.solver.spine.pelvisTarget = pelvisOffset;
                vrik.solver.spine.pelvisPositionWeight = 1f;
                vrik.solver.spine.pelvisRotationWeight = 1f;

                vrik.solver.plantFeet = false;
                vrik.solver.spine.neckStiffness = 0f;
                vrik.solver.spine.maxRootAngle = 180f;
                vrik.solver.spine.minHeadHeight = -100f;

                //頭が腰に近づいたときに猫背になりすぎないように (Final IK v2.1～)
                vrik.solver.spine.useAnimatedHeadHeightWeight = 1.0f;
                vrik.solver.spine.useAnimatedHeadHeightRange = 0.005f;
                vrik.solver.spine.animatedHeadHeightBlend = 0.08f;
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
                    vrik.solver.spine.positionWeight = 1.0f;
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
                leftFootFollow.SetParent(generatedObject);
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
                rightFootFollow.SetParent(generatedObject);
                var follower = rightFootFollow.gameObject.AddComponent<TransformFollower>();
                follower.Target = vrik.references.rightFoot;

                // 腰の子に膝のBendGoal設定用(足トラッカーが無いとき利用される)
                var bendGoalTarget = CreateTransform("RightFootBendGoalTarget", true, rightFootFollow);
                bendGoalTarget.localPosition = new Vector3(0, 0.4f, 2); // 正面2m 高さ40cm
                bendGoalTarget.localRotation = Quaternion.identity;
                vrik.solver.rightLeg.bendGoal = bendGoalTarget;
                vrik.solver.rightLeg.bendGoalWeight = 1.0f;
            }

            var leftElbowTargetTransform = LeftElbowTrackingPoint?.TargetTransform;
            var rightElbowTargetTransform = RightElbowTrackingPoint?.TargetTransform;

            // Left Elbow
            if (leftElbowTargetTransform != null)
            {
                var leftArmBendGoalTarget = CreateTransform("LeftArmBendGoalTarget", true, leftElbowTargetTransform, vrik.references.leftForearm);
                if (calibrateMode == CalibrateMode.Ipose)
                {
                    leftArmBendGoalTarget.position += -currentModel.forward * 0.1f;
                }
                if (calibrateMode == CalibrateMode.Tpose)
                {
                    leftArmBendGoalTarget.position += -currentModel.up * 0.1f;
                }
                if (vrik.solver.leftArm.bendGoal != null) GameObject.Destroy(vrik.solver.leftArm.bendGoal.gameObject);
                vrik.solver.leftArm.bendGoal = leftArmBendGoalTarget;
                vrik.solver.leftArm.bendGoalWeight = 1.0f;
            }

            // Right Elbow
            if (rightElbowTargetTransform != null)
            {
                var rightArmBendGoalTarget = CreateTransform("RightArmBendGoalTarget", true, rightElbowTargetTransform, vrik.references.rightForearm);
                if (calibrateMode == CalibrateMode.Ipose)
                {
                    rightArmBendGoalTarget.position += -currentModel.forward * 0.1f;
                }
                if (calibrateMode == CalibrateMode.Tpose)
                {
                    rightArmBendGoalTarget.position += -currentModel.up * 0.1f;
                }
                if (vrik.solver.rightArm.bendGoal != null) GameObject.Destroy(vrik.solver.rightArm.bendGoal.gameObject);
                vrik.solver.rightArm.bendGoal = rightArmBendGoalTarget;
                vrik.solver.rightArm.bendGoalWeight = 1.0f;
            }

            var leftKneeTargetTransform = LeftKneeTrackingPoint?.TargetTransform;
            var rightKneeTargetTransform = RightKneeTrackingPoint?.TargetTransform;

            // Left Knee
            if (leftKneeTargetTransform != null)
            {
                //膝が内曲がりになる時があるので前方にオフセット
                var leftCalfOffset = new GameObject("leftCalfOffset").transform;
                leftCalfOffset.parent = vrik.references.leftCalf;
                leftCalfOffset.position = vrik.references.leftCalf.position + currentModel.forward * 0.1f;

                var leftLegBendGoalTarget = CreateTransform("LeftLegBendGoalTarget", true, leftKneeTargetTransform, leftCalfOffset);
                if (vrik.solver.leftLeg.bendGoal != null) GameObject.Destroy(vrik.solver.leftLeg.bendGoal.gameObject);

                var boneBendGoal = currentModel.gameObject.AddComponent<BoneBendGoal>();
                boneBendGoal.SetVRIK(vrik);
                boneBendGoal.SetBones("LeftLeg", vrik.references.leftThigh, leftCalfOffset, vrik.references.leftFoot, leftLegBendGoalTarget);
            }

            // Right Knee
            if (rightKneeTargetTransform != null)
            {
                //膝が内曲がりになる時があるので前方にオフセット
                var rightCalfOffset = new GameObject("rightCalfOffset").transform;
                rightCalfOffset.parent = vrik.references.rightCalf;
                rightCalfOffset.position = vrik.references.rightCalf.position + currentModel.forward * 0.1f;

                var rightLegBendGoalTarget = CreateTransform("RightLegBendGoalTarget", true, rightKneeTargetTransform, rightCalfOffset);
                if (vrik.solver.rightLeg.bendGoal != null) GameObject.Destroy(vrik.solver.rightLeg.bendGoal.gameObject);

                var boneBendGoal = currentModel.gameObject.AddComponent<BoneBendGoal>();
                boneBendGoal.SetVRIK(vrik);
                boneBendGoal.SetBones("RightLeg", vrik.references.rightThigh, rightCalfOffset, vrik.references.rightFoot, rightLegBendGoalTarget);
            }

            //TrackingWatcherにWeight設定用アクションを設定
            SetTrackingWatcher(HMDTrackingPoint, weight =>
            {
                //Do noting
            });
            SetTrackingWatcher(LeftHandTrackingPoint, weight =>
            {
                vrik.solver.leftArm.positionWeight = weight;
                vrik.solver.leftArm.rotationWeight = weight;
            });
            SetTrackingWatcher(RightHandTrackingPoint, weight =>
            {
                vrik.solver.rightArm.positionWeight = weight;
                vrik.solver.rightArm.rotationWeight = weight;
            });
            SetTrackingWatcher(PelvisTrackingPoint, weight =>
            {
                vrik.solver.spine.pelvisPositionWeight = weight;
                vrik.solver.spine.pelvisRotationWeight = weight;
            });
            SetTrackingWatcher(LeftFootTrackingPoint, weight =>
            {
                //Do noting
            });
            SetTrackingWatcher(RightFootTrackingPoint, weight =>
            {
                //Do noting
            });
            SetTrackingWatcher(LeftElbowTrackingPoint, weight =>
            {
                vrik.solver.leftArm.bendGoalWeight = weight;
            });
            SetTrackingWatcher(RightElbowTrackingPoint, weight =>
            {
                vrik.solver.rightArm.bendGoalWeight = weight;
            });
            SetTrackingWatcher(LeftKneeTrackingPoint, weight =>
            {
                vrik.solver.leftLeg.bendGoalWeight = weight;
            });
            SetTrackingWatcher(RightKneeTrackingPoint, weight =>
            {
                vrik.solver.rightLeg.bendGoalWeight = weight;
            });
            // 腰トラッカーか両足トラッカーがある場合VRIKRootControllerを使用しないと
            // (特に)180度後ろを向いたときに正しい膝の方向計算ができません
            if (pelvisTargetTransform != null || (leftFootTargetTransform != null && rightFootTargetTransform != null))
            {
                var vrikRootController = vrik.references.root.gameObject.AddComponent<VRIKRootController>();
            }

            if (pelvisTargetTransform != null)
            {
                var pelvisWeightAdjuster = vrik.references.root.gameObject.GetComponent<PelvisWeightAdjuster>();
                if (pelvisWeightAdjuster == null) pelvisWeightAdjuster = vrik.references.root.gameObject.AddComponent<PelvisWeightAdjuster>();
                pelvisWeightAdjuster.vrik = vrik;
            }

            //wristRotationFix = currentModel.AddComponent<WristRotationFix>();
            //wristRotationFix.SetVRIK(vrik);

            vrik.enabled = true;

            vrik.solver.IKPositionWeight = 1.0f;

            //頭の位置をかかとの影響がない程度まで上に上げる
            vrik.UpdateSolverExternal();
            var baseFootHeight = vrik.references.leftFoot.position.y;
            var headTargetPosition = headOffset.position;
            var defaultHeadTargetPosition = headTargetPosition;
            var headStep = new Vector3(0, 0.005f, 0);
            while (vrik.references.leftFoot.position.y - baseFootHeight < 0.005f && headTargetPosition.y - defaultHeadTargetPosition.y < 0.4f)
            {
                headTargetPosition += headStep;
                headOffset.position = headTargetPosition;
                vrik.UpdateSolverExternal();
            }
            headOffset.position -= headStep;

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


        private static void SetTrackingWatcher(TrackingPoint trackingPoint, Action<float> action)
        {
            if (trackingPoint == null) return;
            trackingPoint.TargetTransform.GetComponent<TrackingWatcher>()?.SetActionOfSetWeight(action);
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

        private static GameObject DebugSphere(Transform parent)
        {
            var PositionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            PositionSphere.transform.SetParent(parent, false);
            PositionSphere.transform.localPosition = Vector3.zero;
            PositionSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            return PositionSphere;
        }

        private static GameObject DebugSphere(Vector3 position)
        {
            var PositionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            PositionSphere.transform.position = position;
            PositionSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            return PositionSphere;
        }
    }
}
