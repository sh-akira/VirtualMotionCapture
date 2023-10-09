using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class MotionManager : MonoBehaviour
    {
        [SerializeField]
        private ControlWPFWindow controlWPFWindow;

        private GameObject currentModel;

        private VRIK vrik;
        private IKSolver ikSolver;

        private List<VirtualAvatar> VirtualAvatars = new List<VirtualAvatar>();

        private static MotionManager instance;
        public static MotionManager Instance => instance;

        private Dictionary<HumanBodyBones, Pose> defaultPose;

        private void Awake()
        {
            instance = this;
            if (controlWPFWindow == null) controlWPFWindow = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
        }

        public void AddVirtualAvatar(VirtualAvatar virtualAvatar)
        {
            if (VirtualAvatars.Contains(virtualAvatar) == false)
            {
                if (currentModel != null)
                {
                    var currentPose = GetModelPose(currentModel);
                    SetModelPose(currentModel, defaultPose);
                    virtualAvatar.ImportAvatar(currentModel);
                    SetModelPose(currentModel, currentPose);

                }
                VirtualAvatars.Add(virtualAvatar);
            }
        }


        private void OnCurrentModelChanged(GameObject model)
        {
            if (model != null)
            {
                currentModel = model;

                defaultPose = GetModelPose(model);

                foreach (var virtualAvatar in VirtualAvatars)
                {
                    virtualAvatar.ImportAvatar(model);
                }
            }
        }

        public Dictionary<HumanBodyBones, Pose> GetModelPose(GameObject model)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) return null;

            var poses = new Dictionary<HumanBodyBones, Pose>();
            var rootPose = new Pose(model.transform.localPosition, model.transform.localRotation);
            poses.Add(VirtualAvatar.HumanBodyBonesRoot, rootPose);

            foreach(var bone in VirtualAvatar.ReverseBodyBones)
            {
                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform == null) continue;
                var pose = new Pose(boneTransform.localPosition, boneTransform.localRotation);
                poses.Add(bone, pose);
            }

            return poses;
        }

        public void SetModelPose(GameObject model, Dictionary<HumanBodyBones, Pose> poses)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) return;
            var rootPose = poses[VirtualAvatar.HumanBodyBonesRoot];
            model.transform.localPosition = rootPose.position;
            model.transform.localRotation = rootPose.rotation;

            foreach(var kv in poses)
            {
                var bone = kv.Key;
                var pose = kv.Value;
                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform == null) continue;
                boneTransform.transform.localPosition = pose.position;
                boneTransform.transform.localRotation = pose.rotation;
            }
        }

        private void OnModelUnloading(GameObject model)
        {
            //前回の生成物の削除
            if (currentModel != null)
            {
                if (ikSolver != null)
                {
                    ikSolver.OnPostUpdate -= ApplyMotion;
                }
                ikSolver = null;
                vrik = null;
                currentModel = null;
            }
        }
        private void Update()
        {
            if (currentModel == null) return;
            if (vrik != null) return;

            if (ikSolver != null)
            {
                ikSolver.OnPostUpdate -= ApplyMotion;
            }

            vrik = currentModel.GetComponent<VRIK>();

            if (vrik != null)
            {
                ikSolver = vrik.GetIKSolver();
                ikSolver.OnPostUpdate += ApplyMotion;
            }
        }


        private void ApplyMotion()
        {
            if (currentModel == null) return;

            //無効になってる時は適用しない
            if (enabled == false) return;

            //キャリブレーション中は適用しない
            if (controlWPFWindow.calibrationState == ControlWPFWindow.CalibrationState.WaitingForCalibrating ||
                controlWPFWindow.calibrationState == ControlWPFWindow.CalibrationState.Calibrating) return;


            foreach (var virtualAvatar in VirtualAvatars)
            {
                if (virtualAvatar.Enable == false) continue;
                if (virtualAvatar.BoneTransformCache == null) continue;

                Transform headBone = virtualAvatar.BoneTransformCache[0].modelBone;
                Transform hipBone = null;
                Transform spineBone = null;
                Vector3 defaultHeadPosition = headBone.position;
                Quaternion defaultHeadRotation = headBone.rotation;

                foreach (var (bone, (cloneBone, modelBone)) in virtualAvatar.BoneTransformCache)
                {
                    bool apply = false;
                    switch (bone)
                    {
                        case HumanBodyBones.Hips:
                            hipBone = modelBone;
                            if (virtualAvatar.ApplyRootRotation)
                            {
                                modelBone.localRotation = cloneBone.localRotation;
                                modelBone.Rotate(new Vector3(0, virtualAvatar.CenterOffsetRotationY, 0), Space.World);
                            }
                            if (virtualAvatar.ApplyRootPosition)
                            {
                                modelBone.localPosition = cloneBone.localPosition + virtualAvatar.CenterOffsetPosition; //Root位置だけは同期
                            }
                            break;
                        case HumanBodyBones.Spine:
                            spineBone = modelBone;
                            apply = virtualAvatar.ApplySpine;
                            break;
                        case HumanBodyBones.Chest:
                        case HumanBodyBones.UpperChest:
                            apply = virtualAvatar.ApplyChest;
                            break;
                        case HumanBodyBones.Neck:
                        case HumanBodyBones.Head:
                        case HumanBodyBones.Jaw:
                            apply = virtualAvatar.ApplyHead;
                            break;
                        case HumanBodyBones.LeftShoulder:
                        case HumanBodyBones.LeftUpperArm:
                        case HumanBodyBones.LeftLowerArm:
                            apply = virtualAvatar.ApplyLeftArm;
                            break;
                        case HumanBodyBones.RightShoulder:
                        case HumanBodyBones.RightUpperArm:
                        case HumanBodyBones.RightLowerArm:
                            apply = virtualAvatar.ApplyRightArm;
                            break;
                        case HumanBodyBones.LeftHand:
                            apply = virtualAvatar.ApplyLeftHand;
                            break;
                        case HumanBodyBones.RightHand:
                            apply = virtualAvatar.ApplyRightHand;
                            break;
                        case HumanBodyBones.LeftUpperLeg:
                        case HumanBodyBones.LeftLowerLeg:
                            apply = virtualAvatar.ApplyLeftLeg;
                            break;
                        case HumanBodyBones.RightUpperLeg:
                        case HumanBodyBones.RightLowerLeg:
                            apply = virtualAvatar.ApplyRightLeg;
                            break;
                        case HumanBodyBones.LeftFoot:
                        case HumanBodyBones.LeftToes:
                            apply = virtualAvatar.ApplyLeftFoot;
                            break;
                        case HumanBodyBones.RightFoot:
                        case HumanBodyBones.RightToes:
                            apply = virtualAvatar.ApplyRightFoot;
                            break;
                        case HumanBodyBones.LeftEye:
                        case HumanBodyBones.RightEye:
                            apply = virtualAvatar.ApplyEye;
                            break;
                        case HumanBodyBones.LeftThumbProximal:
                        case HumanBodyBones.LeftThumbIntermediate:
                        case HumanBodyBones.LeftThumbDistal:
                        case HumanBodyBones.LeftIndexProximal:
                        case HumanBodyBones.LeftIndexIntermediate:
                        case HumanBodyBones.LeftIndexDistal:
                        case HumanBodyBones.LeftMiddleProximal:
                        case HumanBodyBones.LeftMiddleIntermediate:
                        case HumanBodyBones.LeftMiddleDistal:
                        case HumanBodyBones.LeftRingProximal:
                        case HumanBodyBones.LeftRingIntermediate:
                        case HumanBodyBones.LeftRingDistal:
                        case HumanBodyBones.LeftLittleProximal:
                        case HumanBodyBones.LeftLittleIntermediate:
                        case HumanBodyBones.LeftLittleDistal:
                            apply = virtualAvatar.ApplyLeftFinger;
                            break;
                        case HumanBodyBones.RightThumbProximal:
                        case HumanBodyBones.RightThumbIntermediate:
                        case HumanBodyBones.RightThumbDistal:
                        case HumanBodyBones.RightIndexProximal:
                        case HumanBodyBones.RightIndexIntermediate:
                        case HumanBodyBones.RightIndexDistal:
                        case HumanBodyBones.RightMiddleProximal:
                        case HumanBodyBones.RightMiddleIntermediate:
                        case HumanBodyBones.RightMiddleDistal:
                        case HumanBodyBones.RightRingProximal:
                        case HumanBodyBones.RightRingIntermediate:
                        case HumanBodyBones.RightRingDistal:
                        case HumanBodyBones.RightLittleProximal:
                        case HumanBodyBones.RightLittleIntermediate:
                        case HumanBodyBones.RightLittleDistal:
                            apply = virtualAvatar.ApplyRightFinger;
                            break;
                        case HumanBodyBones.LastBone:
                        default:
                            break;
                    }

                    if (apply) modelBone.localRotation = cloneBone.localRotation;
                }

                if (virtualAvatar.ApplyHead == false && hipBone != null && spineBone != null)
                {
                    //頭の回転無効の時、VR機器優先するために最後に元の位置に戻るように腰を動かす
                    var rotdiff = defaultHeadRotation * Quaternion.Inverse(headBone.rotation);
                    spineBone.rotation = rotdiff * spineBone.rotation;
                    var posdiff = defaultHeadPosition - headBone.position;
                    hipBone.position = posdiff + hipBone.position;
                }
            }
        }
    }
}
