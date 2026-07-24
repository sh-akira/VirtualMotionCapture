using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMC
{
    public class MotionManager : MonoBehaviour
    {
        [SerializeField]
        private ControlWPFWindow controlWPFWindow;

        private GameObject currentModel;

        [SerializeField]
        private List<VirtualAvatar> VirtualAvatars = new List<VirtualAvatar>();

        private static MotionManager instance;
        public static MotionManager Instance => instance;

        private Dictionary<HumanBodyBones, Pose> defaultPoses;

        private Guid eventId;

        private void Awake()
        {
            instance = this;
            if (controlWPFWindow == null) controlWPFWindow = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
        }

        private void Start()
        {
            eventId = IKManager.Instance.AddOnPostUpdate(100, ApplyMotion);
        }

        private void OnDestroy()
        {
            IKManager.Instance.RemoveOnPostUpdate(eventId);
        }

        public void AddVirtualAvatar(VirtualAvatar virtualAvatar)
        {
            if (VirtualAvatars.Contains(virtualAvatar) == false)
            {
                if (currentModel != null)
                {
                    var currentPose = GetModelPoses(currentModel);
                    SetModelPoses(currentModel, defaultPoses);
                    virtualAvatar.ImportAvatar(currentModel);
                    SetModelPoses(currentModel, currentPose);

                }
                VirtualAvatars.Add(virtualAvatar);

                VirtualAvatars = VirtualAvatars.OrderBy(d => (int)d.MotionSource).ToList();
            }
        }

        public void RemoveVirtualAvatar(VirtualAvatar virtualAvatar)
        {
            if (VirtualAvatars.Contains(virtualAvatar) == true)
            {
                VirtualAvatars.Remove(virtualAvatar);
            }
        }


        private void OnCurrentModelChanged(GameObject model)
        {
            if (model != null)
            {
                currentModel = model;

                defaultPoses = GetModelPoses(model);

                foreach (var virtualAvatar in VirtualAvatars)
                {
                    virtualAvatar.ImportAvatar(model);
                }
            }
        }

        public Dictionary<HumanBodyBones, Pose> GetModelPoses(GameObject model)
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

        public void SetModelPoses(GameObject model, Dictionary<HumanBodyBones, Pose> poses)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) return;

            foreach(var kv in poses)
            {
                var bone = kv.Key;
                var pose = kv.Value;

                var boneTransform = bone == VirtualAvatar.HumanBodyBonesRoot ? model.transform : animator.GetBoneTransform(bone);
                if (boneTransform == null) continue;
                boneTransform.localPosition = pose.position;
                boneTransform.localRotation = pose.rotation;
            }
        }

        public void ResetVirtualAvatarPose(VirtualAvatar virtualAvatar) => SetModelPoses(virtualAvatar.animator.gameObject, defaultPoses);

        private void OnModelUnloading(GameObject model)
        {
            //前回の生成物の削除
            if (currentModel != null)
            {
                currentModel = null;
            }
        }

        private void ApplyMotion()
        {
            if (currentModel == null) return;

            //無効になってる時は適用しない
            if (enabled == false) return;

            VMCEvents.BeforeApplyMotion?.Invoke(currentModel);
            
            Transform ikHeadBone = null;

            foreach (var virtualAvatar in VirtualAvatars)
            {
                if (virtualAvatar.BoneTransformCache == null) continue;
                if (virtualAvatar.MotionSource == MotionSource.VRIK) ikHeadBone = virtualAvatar.BoneTransformCache[HumanBodyBones.Head].cloneBone;
                if (virtualAvatar.Enable == false) continue;

                //キャリブレーション中は適用しない
                if (virtualAvatar.MotionSource != MotionSource.VRIK &&
                    (IKManager.Instance.CalibrationState == CalibrationState.WaitingForCalibrating ||
                     IKManager.Instance.CalibrationState == CalibrationState.Calibrating)) return;

                if (ikHeadBone == null) continue;

                Transform headBone = virtualAvatar.BoneTransformCache[HumanBodyBones.Head].modelBone;
                Transform hipBone = null;
                Transform spineBone = null;
                Vector3 defaultHeadPosition = ikHeadBone.position;
                Quaternion defaultHeadRotation = ikHeadBone.rotation;

                foreach (var (bone, (cloneBone, modelBone)) in virtualAvatar.BoneTransformCache)
                {
                    bool apply = false;
                    switch (bone)
                    {
                        case VirtualAvatar.HumanBodyBonesRoot:
                        case HumanBodyBones.Hips:
                            if (virtualAvatar.IgnoreDefaultBone && IsDefaultPose(virtualAvatar, bone, cloneBone))
                            {
                                apply = false;
                            }
                            else
                            {
                                if ((virtualAvatar.MotionSource == MotionSource.VRIK && bone == VirtualAvatar.HumanBodyBonesRoot) ||
                                (virtualAvatar.MotionSource != MotionSource.VRIK && bone == HumanBodyBones.Hips))
                                {
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
                                }
                                else if ((virtualAvatar.MotionSource == MotionSource.VRIK && bone == HumanBodyBones.Hips) ||
                                    (virtualAvatar.MotionSource == MotionSource.VMCProtocol && bone == VirtualAvatar.HumanBodyBonesRoot))
                                {
                                    if (virtualAvatar.ApplyRootRotation)
                                    {
                                        modelBone.localRotation = cloneBone.localRotation;
                                    }
                                    if (virtualAvatar.ApplyRootPosition)
                                    {
                                        modelBone.localPosition = cloneBone.localPosition;
                                    }
                                }
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

                    if (apply) 
                    {
                        if (virtualAvatar.IgnoreDefaultBone && IsDefaultPose(virtualAvatar, bone, cloneBone))
                        {
                            continue;
                        }
                        modelBone.localPosition = cloneBone.localPosition;
                        modelBone.localRotation = cloneBone.localRotation; 
                    }
                }

                if (virtualAvatar.CorrectHipBone && virtualAvatar.ApplyHead == false && hipBone != null && spineBone != null)
                {
                    //頭の回転無効の時、VR機器優先するために最後に元の位置に戻るように腰を動かす
                    var rotdiff = defaultHeadRotation * Quaternion.Inverse(headBone.rotation);
                    spineBone.rotation = rotdiff * spineBone.rotation;
                    var posdiff = defaultHeadPosition - headBone.position;
                    hipBone.position = posdiff + hipBone.position;
                }
            }

            VMCEvents.AfterApplyMotion?.Invoke(currentModel);
        }

        private bool IsDefaultPose(VirtualAvatar virtualAvatar, HumanBodyBones bone, Transform cloneBone)
        {
            if (cloneBone == null) return true;
            if (virtualAvatar.GetPoseChanged(bone) == true)
            {
                // 過去に変動していたら現在の値に関わらずデフォルトじゃない扱い
                return false;
            }
            var pose = defaultPoses[bone];
            bool isDefault = ((cloneBone.localRotation == pose.rotation && cloneBone.localPosition == pose.position) ||
                              (cloneBone.localRotation == Quaternion.identity && cloneBone.localPosition == Vector3.zero));
            if (isDefault == false)
            {
                // ボーンの変動を見つけたとき、デフォルトじゃない扱いする
                virtualAvatar.SetPoseChanged(bone);
            }
            return isDefault;
        }
    }    
}
