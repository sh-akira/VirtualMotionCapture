using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using VRM;
using DVRSDK.Utilities;

namespace DVRSDK.Avatar.Tracking
{
    public class FinalIKCalibrator
    {
        public float MaxModelHeight { get; set; } = 3.0f;
        public float MinModelHeight { get; set; } = 0.1f;
        public float ModelScale { get; private set; }

        private ITracker Trackers;
        private float trackersParentYOffset;
        private float avatarRootYOffset;

        private GameObject currentModel = null;

        private VRIK vrik;
        private VRIKRootController vrikRootController;
        private WristRotationFix wristRotationFix;

        private Vector3 modelHeadPosition;

        private List<GameObject> GeneratedGameObjects = new List<GameObject>();

        public FinalIKCalibrator(ITracker trackers) => Trackers = trackers;

        public FinalIKCalibrator(ITracker trackers, float minModelHeight, float maxModelHeight) : this(trackers)
        {
            MinModelHeight = minModelHeight;
            MaxModelHeight = maxModelHeight;
        }

        public void DoCalibration()
        {
            if (currentModel == null) throw new InvalidOperationException("Need LoadModel first");
            if (Trackers == null) throw new NullReferenceException("Need to set " + nameof(Trackers));
            if (Trackers.GetTrackerTarget(TrackerPositions.Head).TargetTransform == null) throw new InvalidOperationException("Need SteamVRTracker initialize first");
            Calibrate();
        }

        private void ResetAll()
        {
            if (currentModel != null)
            {
                currentModel.transform.localScale = Vector3.one;
                SetUpdateWhenOffscreen(false);
                currentModel.transform.position += new Vector3(0, avatarRootYOffset, 0);
                avatarRootYOffset = 0;

                if (vrik == null) vrik = currentModel.GetComponent<VRIK>(); // すでにVRIKが存在していた場合破棄できるように
            }
            if (vrik != null)
            {
                vrik.solver.Reset();
                vrik.solver.FixTransforms();
                UnityEngine.Object.DestroyImmediate(vrik);
            }
            if (vrikRootController != null)
            {
                UnityEngine.Object.DestroyImmediate(vrikRootController);
            }
            if (wristRotationFix != null)
            {
                UnityEngine.Object.DestroyImmediate(wristRotationFix);
            }
            foreach (var obj in GeneratedGameObjects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            GeneratedGameObjects.Clear();

            if (Trackers != null)
            {
                Trackers.TrackersParent.position += new Vector3(0, trackersParentYOffset, 0);
                trackersParentYOffset = 0;
            }
        }

        public void LoadModel(GameObject prefab)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            ResetAll();

            currentModel = prefab;
            var modelParent = currentModel.transform.parent;
            var modelParentPosition = modelParent == null ? Vector3.zero : modelParent.position;

            // モデルのSkinnedMeshRendererがカリングされないように、すべてのオプション変更
            SetUpdateWhenOffscreen(true);

            // モデルのデフォルト身長を取得しておく
            var animator = currentModel.GetComponent<Animator>();
            var modelHeadTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            modelHeadPosition = modelHeadTransform.position - modelParentPosition;
            ModelScale = 1.0f;
            if (MaxModelHeight > 0.0f)
            {
                if (modelHeadPosition.y > MaxModelHeight)
                {
                    ModelScale = MaxModelHeight / modelHeadPosition.y;

                }
            }
            if (MinModelHeight > 0.0f)
            {
                if (modelHeadPosition.y < MinModelHeight)
                {
                    ModelScale = MinModelHeight / modelHeadPosition.y;
                }
            }
            currentModel.transform.localScale = new Vector3(ModelScale, ModelScale, ModelScale);
            modelHeadPosition = modelHeadTransform.position - modelParentPosition;

            // HMD内に頭の影だけ表示できるように表示用オブジェクト複製
            CopyMeshRenderersForShadow();
        }

        private void SetUpdateWhenOffscreen(bool enable)
        {
            if (currentModel == null) return;
            foreach (var renderer in currentModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = enable;
            }
        }

        private void CopyMeshRenderersForShadow()
        {
            foreach (var renderer in currentModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.gameObject.layer == VRMFirstPerson.THIRDPERSON_ONLY_LAYER)
                {
                    var obj = UnityEngine.Object.Instantiate(renderer.gameObject, renderer.gameObject.transform.position, renderer.gameObject.transform.rotation);
                    obj.transform.parent = renderer.gameObject.transform;
                    obj.gameObject.layer = VRMFirstPerson.FIRSTPERSON_ONLY_LAYER;
                    var skin = obj.GetComponent<SkinnedMeshRenderer>();
                    skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

                    foreach (Transform child in obj.transform)
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                    GeneratedGameObjects.Add(obj);
                }
            }
            foreach (var renderer in currentModel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.layer == VRMFirstPerson.THIRDPERSON_ONLY_LAYER)
                {
                    var obj = UnityEngine.Object.Instantiate(renderer.gameObject, renderer.gameObject.transform.position, renderer.gameObject.transform.rotation);
                    obj.transform.parent = renderer.gameObject.transform;
                    obj.gameObject.layer = VRMFirstPerson.FIRSTPERSON_ONLY_LAYER;
                    var mesh = obj.GetComponent<MeshRenderer>();
                    mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

                    foreach (Transform child in obj.transform)
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                    GeneratedGameObjects.Add(obj);
                }
            }
        }

        public void FixLegDirection(GameObject targetHumanoidModel)
        {
            var avatarForward = targetHumanoidModel.transform.forward;
            var animator = targetHumanoidModel.GetComponent<Animator>();

            var leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var leftFootDefaultRotation = leftFoot.rotation;
            var leftFootTargetPosition = new Vector3(leftFoot.position.x, leftFoot.position.y, leftFoot.position.z);
            LookAtBones(leftFootTargetPosition + avatarForward * 0.01f, leftUpperLeg, leftLowerLeg);
            LookAtBones(leftFootTargetPosition, leftLowerLeg, leftFoot);
            leftFoot.rotation = leftFootDefaultRotation;

            var rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var rightFootDefaultRotation = rightFoot.rotation;
            var rightFootTargetPosition = new Vector3(rightFoot.position.x, rightFoot.position.y, rightFoot.position.z);
            LookAtBones(rightFootTargetPosition + avatarForward * 0.01f, rightUpperLeg, rightLowerLeg);
            LookAtBones(rightFootTargetPosition, rightLowerLeg, rightFoot);
            rightFoot.rotation = rightFootDefaultRotation;
        }

        private void LookAtBones(Vector3 lookTargetPosition, params Transform[] bones)
        {
            for (int i = 0; i < bones.Length - 1; i++)
            {
                bones[i].rotation = Quaternion.FromToRotation((bones[i].position - bones[i + 1].position).normalized, (bones[i].position - lookTargetPosition).normalized) * bones[i].rotation;
            }
        }

        private void Calibrate()
        {
            Trackers.TrackersParent.transform.localScale = Vector3.one;
            var headTarget = Trackers.GetTrackerTarget(TrackerPositions.Head);
            // この高さはHMD(目の高さ)
            var realHeight = headTarget.TargetTransform.position.y - Trackers.TrackersParent.position.y;
            if (realHeight <= 0.0f) realHeight = 1.55f;
            if (headTarget.UseDeviceType == TrackingDeviceType.HMD)
            {
                // 人体寸法データベース
                // https://www.airc.aist.go.jp/dhrt/91-92/data/list.html
                // A30 頭頂・内眼角距離
                realHeight += 0.1246f;
            }
            else if (headTarget.UseDeviceType == TrackingDeviceType.GenericTracker)
            {
                realHeight += 0.05f; // TODO: 暫定。おでこ想定。後々トラッカーの方向で取り付け位置を推測すること
            }
            Debug.Log($"UserHeight:{realHeight}");

            // 身長から腕の長さを算出する
            // B1 身長 1654.7
            // C7 上腕長 301.2
            // C8 前腕長 240.5
            // ratio = (C7 + C8) / B1 = 541.7 / 1654.7 = 0.32737052033601257025442678431136
            var realArmLength = realHeight * 0.327f;
            Debug.Log($"UserArmLength:{realArmLength}");
            // アバターの腕の長さ
            var animator = currentModel.GetComponent<Animator>();
            var leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
            var avatarArmLength = Vector3.Distance(leftUpperArm, leftHand);
            Debug.Log($"avatarArmLength:{avatarArmLength}");

            // 身長から肩峰幅(左右UpperArm間)を算出する
            // D7 肩峰幅 378.8
            // X1 調整値 80mm (左右合わせて、肩峰とUpperArm位置とのオフセット)
            // ratio = (D7 - X1) / B1 = (378.8 - 80) / 1654.7 = 0.18057653955399770351121049132773
            var realShoulderWidth = realHeight * 0.181f;
            Debug.Log($"UserShoulderWidth:{realShoulderWidth}");
            // アバターの肩峰幅
            var rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            var avatarShoulderWidth = Vector3.Distance(leftUpperArm, rightUpperArm);
            Debug.Log($"avatarShoulderWidth:{avatarShoulderWidth}");

            // 腕の比率からスケールを算出する
            // 首(身体の中心)～手首までの長さの比率です
            var hscale = (avatarArmLength + (avatarShoulderWidth / 2)) / (realArmLength + (realShoulderWidth / 2));
            Debug.Log($"avatar / user scale:{hscale}");
            Trackers.TrackersParent.localScale = new Vector3(hscale, hscale, hscale);

            var realEyePosition = headTarget.TargetTransform.position;
            if (headTarget.UseDeviceType == TrackingDeviceType.GenericTracker)
            {
                realEyePosition += new Vector3(0, -0.6246f, 0); // TODO: 暫定。おでこ想定。後々トラッカーの方向で取り付け位置を推測すること
            }
            var realEyeEularAngles = headTarget.TargetTransform.rotation.eulerAngles;
            var realEyeYRotation = Quaternion.Euler(0, realEyeEularAngles.y, 0);

            var vrmFirstPerson = currentModel.GetComponent<VRMFirstPerson>();
            var firstPersonTarget = new GameObject("FirstPersonTarget");
            GeneratedGameObjects.Add(firstPersonTarget);
            firstPersonTarget.transform.SetParent(vrmFirstPerson.FirstPersonBone, false);

            // FirstPersonOffsetがデフォルト値の時は参考にならないのでそれっぽい目の位置を設定する
            if (vrmFirstPerson.FirstPersonOffset == new Vector3(0, 0.06f, 0))
            {
                vrmFirstPerson.FirstPersonOffset = new Vector3(0, 0.04f, 0.085f);
            }
            firstPersonTarget.transform.localPosition = vrmFirstPerson.FirstPersonOffset;
            var firstPersonTargetEularAngles = firstPersonTarget.transform.rotation.eulerAngles;
            var firstPersonTargetYRotation = Quaternion.Euler(0, firstPersonTargetEularAngles.y, 0);

            // アバターを目線位置に移動
            var avatarRoot = currentModel.transform;
            var avatarRootDefaultPosition = avatarRoot.position;
            avatarRoot.rotation = avatarRoot.rotation * (realEyeYRotation * Quaternion.Inverse(firstPersonTargetYRotation));
            avatarRoot.position = avatarRoot.position + (realEyePosition - firstPersonTarget.transform.position);

            // VRIKアタッチ前にボーンの曲げ方向を補正して関節が正しい方向に曲がるようにする
            FixLegDirection(currentModel);

            vrik = currentModel.AddComponent<VRIK>();
            vrik.AutoDetectReferences();

            vrik.solver.FixTransforms();

            vrik.solver.IKPositionWeight = 0f;
            vrik.solver.leftArm.stretchCurve = new AnimationCurve();
            vrik.solver.rightArm.stretchCurve = new AnimationCurve();
            vrik.UpdateSolverExternal();

            // 足の歩き具合を調整
            //vrik.solver.leftLeg.swivelOffset = 15;
            //vrik.solver.rightLeg.swivelOffset = -15;
            vrik.solver.locomotion.footDistance = 0.06f;
            vrik.solver.locomotion.stepThreshold = 0.2f;
            vrik.solver.locomotion.angleThreshold = 45f;
            vrik.solver.locomotion.maxVelocity = 0.04f;
            vrik.solver.locomotion.velocityFactor = 0.04f;
            vrik.solver.locomotion.rootSpeed = 40;
            vrik.solver.locomotion.stepSpeed = 2;
            vrik.solver.locomotion.weight = 1.0f;

            /*
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            var upperchest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperchest != null)
            {
                vrik.references.chest = upperchest;
                if (chest != null) vrik.references.spine = chest;
            }
            */

            // Head
            var headOffset = CreateTransform("HeadIKTarget", true, headTarget.TargetTransform, vrik.references.head);
            vrik.solver.spine.headTarget = headOffset;

            // 頭のトラッキングの補正度合いを変更
            vrik.solver.spine.minHeadHeight = 0;
            vrik.solver.spine.neckStiffness = 0.1f;
            vrik.solver.spine.headClampWeight = 0;
            vrik.solver.spine.maxRootAngle = 20;

            // アバターの腰の位置についていくオブジェクト(FinalIKの処理順に影響を受けない)
            var pelvisFollow = CreateTransform("PelvisFollowObject", true, null, vrik.references.pelvis);
            pelvisFollow.gameObject.AddComponent<TransformFollower>().Target = vrik.references.pelvis;

            // LeftHand
            var leftHandTarget = Trackers.GetTrackerTarget(TrackerPositions.LeftHand);
            if (leftHandTarget != null)
            {
                var leftHandOffset = CreateTransform("LeftHandIKTarget", true, leftHandTarget.TargetTransform);
                leftHandOffset.localPosition = Trackers.GetIKOffsetPosition(leftHandTarget.TrackerPosition, leftHandTarget.UseDeviceType);
                leftHandOffset.localRotation = Trackers.GetIKOffsetRotation(leftHandTarget.TrackerPosition, leftHandTarget.UseDeviceType);
                vrik.solver.leftArm.target = leftHandOffset;
                vrik.solver.leftArm.positionWeight = 1f;
                vrik.solver.leftArm.rotationWeight = 1f;
            }
            else
            {
                var leftHandOffset = CreateTransform("LeftHandIKTarget", true, pelvisFollow);
                leftHandOffset.position += avatarRoot.right * -0.2f;
                leftHandOffset.localRotation = Quaternion.Euler(0, 0, 70);
                vrik.solver.leftArm.target = leftHandOffset;
                vrik.solver.leftArm.positionWeight = 1f;
                vrik.solver.leftArm.rotationWeight = 1f;
            }

            // RightHand
            var rightHandTarget = Trackers.GetTrackerTarget(TrackerPositions.RightHand);
            if (rightHandTarget != null)
            {
                var rightHandOffset = CreateTransform("RightHandIKTarget", true, rightHandTarget.TargetTransform);
                rightHandOffset.localPosition = Trackers.GetIKOffsetPosition(rightHandTarget.TrackerPosition, rightHandTarget.UseDeviceType);
                rightHandOffset.localRotation = Trackers.GetIKOffsetRotation(rightHandTarget.TrackerPosition, rightHandTarget.UseDeviceType);
                vrik.solver.rightArm.target = rightHandOffset;
                vrik.solver.rightArm.positionWeight = 1f;
                vrik.solver.rightArm.rotationWeight = 1f;
            }
            else
            {
                var rightHandOffset = CreateTransform("RightHandIKTarget", true, pelvisFollow);
                rightHandOffset.position += avatarRoot.right * 0.2f;
                rightHandOffset.localRotation = Quaternion.Euler(0, 0, -70);
                vrik.solver.rightArm.target = rightHandOffset;
                vrik.solver.rightArm.positionWeight = 1f;
                vrik.solver.rightArm.rotationWeight = 1f;
            }

            // Pelvis
            var waistTarget = Trackers.GetTrackerTarget(TrackerPositions.Waist);
            if (waistTarget != null)
            {
                var pelvisOffset = CreateTransform("PelvisIKTarget", true, waistTarget.TargetTransform, vrik.references.pelvis);
                // 腰トラッキングworkaround
                var waistTrackerY = waistTarget.TargetTransform.position.y;
                var pelvisOffsetY = pelvisOffset.position.y;
                pelvisOffset.position = new Vector3(pelvisOffset.position.x, pelvisOffsetY - Mathf.Abs(waistTrackerY - pelvisOffsetY), pelvisOffset.position.z);
                vrik.solver.spine.pelvisTarget = pelvisOffset;
                vrik.solver.spine.pelvisPositionWeight = 1f;
                vrik.solver.spine.pelvisRotationWeight = 1f;

                vrik.solver.plantFeet = false;
                vrik.solver.spine.neckStiffness = 0f;
                vrik.solver.spine.maxRootAngle = 180f;
            }

            // 腰のトラッキングを調整
            vrik.solver.spine.maintainPelvisPosition = 0; // アバターによって腰がグリングリンするのが直ります

            // 足のキャリブレーションはいったんモデルとトラッカーを同じ高さに戻してやる
            var leftFootTarget = Trackers.GetTrackerTarget(TrackerPositions.LeftFoot);
            var rightFootTarget = Trackers.GetTrackerTarget(TrackerPositions.RightFoot);

            //float avatarYOffset = 0f;
            if (leftFootTarget != null || rightFootTarget != null)
            {
                //if (avatarRoot.position.y < Trackers.TrackersParent.position.y)
                //{
                // アバターの足のほうが長いときは膝を曲げるためにアバターを0地点に高さ戻してキャリブレーション後に目線高さに戻す #1
                //avatarYOffset = Trackers.TrackersParent.position.y - avatarRoot.position.y;
                //avatarRoot.position += new Vector3(0, avatarYOffset, 0);
                //}
                //else
                {
                    // TODO 足が短くても長くてもアバターの足が床の高さになるようにしているので、MR合成モードのような場合はアバターじゃなくてトラッキングスペースで合わせる事

                    // アバターの足のほうが短いときはトラッカーとアバターをアバターの0地点になるように下げてからキャリブレーションする
                    // HMDの高さにモデルが持ち上がってるから足が浮いてるはず
                    trackersParentYOffset = avatarRoot.position.y - avatarRootDefaultPosition.y;
                    //avatarRootYOffset = trackersParentYOffset;
                    var yoffset = new Vector3(0, trackersParentYOffset, 0);
                    avatarRoot.position -= yoffset;
                    Trackers.TrackersParent.position -= yoffset;
                }

                // 足トラッカーがあるときはLocomotion無効
                vrik.solver.locomotion.weight = 0.0f;
            }
            else
            {
                // 足トラッカーが無いとき
            }

            if (leftFootTarget != null)
            {
                var footBone = vrik.references.leftToes != null ? vrik.references.leftToes : vrik.references.leftFoot;
                var leftFootOffset = CreateTransform("LeftFootIKTarget", true, leftFootTarget.TargetTransform, footBone);
                vrik.solver.leftLeg.target = leftFootOffset;
                vrik.solver.leftLeg.positionWeight = 1f;
                vrik.solver.leftLeg.rotationWeight = 1f;

                var bendGoal = CreateTransform("LeftFootBendGoal", true, leftFootTarget.TargetTransform);
                bendGoal.position = footBone.position + avatarRoot.forward + avatarRoot.up;
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

            if (rightFootTarget != null)
            {
                var footBone = vrik.references.rightToes != null ? vrik.references.rightToes : vrik.references.rightFoot;
                var rightFootOffset = CreateTransform("RightFootIKTarget", true, rightFootTarget.TargetTransform, footBone);
                vrik.solver.rightLeg.target = rightFootOffset;
                vrik.solver.rightLeg.positionWeight = 1f;
                vrik.solver.rightLeg.rotationWeight = 1f;

                var bendGoal = CreateTransform("RightFootBendGoal", true, rightFootTarget.TargetTransform);
                bendGoal.position = footBone.position + avatarRoot.forward + avatarRoot.up;
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

            //avatarRoot.position -= new Vector3(0, avatarYOffset, 0); // #1

            if (avatarRoot.position.y < avatarRootDefaultPosition.y)
            {
                // アバターがもとより低い位置に居るときは足が埋まるので上げる
                var currentTrackersParentYOffset = trackersParentYOffset;
                trackersParentYOffset = avatarRoot.position.y - avatarRootDefaultPosition.y;
                var yoffset = new Vector3(0, trackersParentYOffset, 0);
                avatarRoot.position -= yoffset;
                Trackers.TrackersParent.position -= yoffset;
                trackersParentYOffset += currentTrackersParentYOffset;
            }

            // 腰トラッカーか両足トラッカーがある場合VRIKRootControllerを使用しないと
            // (特に)180度後ろを向いたときに正しい膝の方向計算ができません
            if (waistTarget != null || (leftFootTarget != null && rightFootTarget != null))
            {
                vrikRootController = vrik.references.root.gameObject.AddComponent<VRIKRootController>();
            }

            // 手首がねじれないようにする
            // For Final IK 1.9
            //var leftTwistRelaxer = vrik.references.leftForearm.gameObject.AddComponent<RootMotion.FinalIK.TwistRelaxer>();
            //var rightTwistRelaxer = vrik.references.rightForearm.gameObject.AddComponent<RootMotion.FinalIK.TwistRelaxer>();
            //leftTwistRelaxer.ik = vrik;
            //leftTwistRelaxer.parent = vrik.references.leftUpperArm;
            //leftTwistRelaxer.child = vrik.references.leftHand;
            //leftTwistRelaxer.weight = 0.7f;
            //leftTwistRelaxer.parentChildCrossfade = 1.0f;
            //rightTwistRelaxer.ik = vrik;
            //rightTwistRelaxer.parent = vrik.references.rightUpperArm;
            //rightTwistRelaxer.child = vrik.references.rightHand;
            //rightTwistRelaxer.weight = 0.7f;
            //rightTwistRelaxer.parentChildCrossfade = 1.0f;

            // For Final IK 2.0 (*この処理はアバターによっては腕を体に近づけると暴れる)
            //var leftLowerArm = vrik.references.leftForearm;
            //var leftRelaxer = leftLowerArm.gameObject.AddComponent<TwistRelaxer>();
            //leftRelaxer.ik = vrik;
            //leftRelaxer.twistSolvers = new TwistSolver[] { new TwistSolver { transform = leftLowerArm } };
            //var rightLowerArm = vrik.references.rightForearm;
            //var rightRelaxer = rightLowerArm.gameObject.AddComponent<TwistRelaxer>();
            //rightRelaxer.ik = vrik;
            //rightRelaxer.twistSolvers = new TwistSolver[] { new TwistSolver { transform = rightLowerArm } };

            // Original
            wristRotationFix = currentModel.AddComponent<WristRotationFix>();
            wristRotationFix.SetVRIK(vrik);

            vrik.solver.IKPositionWeight = 1.0f;

            vrik.UpdateSolverExternal();
        }

        private Transform CreateTransform(string name, bool AddDestroy, Transform parent)
            => CreateTransform(name, AddDestroy, parent, null, null);
        private Transform CreateTransform(string name, bool AddDestroy, Transform parent, Transform placeTransform)
            => CreateTransform(name, AddDestroy, parent, placeTransform != null ? placeTransform.position : null as Vector3?, placeTransform != null ? placeTransform.rotation : null as Quaternion?);
        private Transform CreateTransform(string name, bool AddDestroy, Transform parent, Vector3? position, Quaternion? rotation)
        {
            var newGameObject = new GameObject(name);
            if (AddDestroy) GeneratedGameObjects.Add(newGameObject);
            var t = newGameObject.transform;
            if (parent != null) t.SetParent(parent, false);
            if (position != null) t.position = position.Value;
            if (rotation != null) t.rotation = rotation.Value;
            return t;
        }
    }
}
