using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace VMC
{
    public class VirtualAvatar
    {
        private Transform parent;
        private GameObject currentModel;
        private Avatar avatar;

        public MotionSource MotionSource;
        public Transform RootTransform;
        public Transform transform => RootTransform;
        public Animator animator;

        public Vector3 CenterOffsetPosition;
        public float CenterOffsetRotationY;

        public bool ApplyRootRotation;
        public bool ApplyRootPosition;
        public bool ApplySpine;
        public bool ApplyChest;
        public bool ApplyHead;
        public bool ApplyLeftArm;
        public bool ApplyRightArm;
        public bool ApplyLeftHand;
        public bool ApplyRightHand;
        public bool ApplyLeftLeg;
        public bool ApplyRightLeg;
        public bool ApplyLeftFoot;
        public bool ApplyRightFoot;
        public bool ApplyEye;
        public bool ApplyLeftFinger;
        public bool ApplyRightFinger;

        public bool Enable = true;
        public bool IgnoreDefaultBone = true;

        public Dictionary<HumanBodyBones, (Transform cloneBone, Transform modelBone)> boneTransformCache;
        public Dictionary<HumanBodyBones, (Transform cloneBone, Transform modelBone)> BoneTransformCache => InitializeBoneTransformCache();


        public const HumanBodyBones HumanBodyBonesRoot = (HumanBodyBones)(-1);
        public static HumanBodyBones[] ReverseBodyBones = new HumanBodyBones[] {
                HumanBodyBones.Head ,
                HumanBodyBones.Neck ,
                HumanBodyBones.LeftEye ,
                HumanBodyBones.RightEye ,
                HumanBodyBones.Jaw ,
                HumanBodyBones.LeftShoulder ,
                HumanBodyBones.RightShoulder ,
                HumanBodyBones.LeftUpperArm ,
                HumanBodyBones.RightUpperArm ,
                HumanBodyBones.LeftLowerArm ,
                HumanBodyBones.RightLowerArm ,
                HumanBodyBones.UpperChest ,
                HumanBodyBones.LeftHand ,
                HumanBodyBones.RightHand ,
                HumanBodyBones.LeftThumbProximal ,
                HumanBodyBones.LeftThumbIntermediate ,
                HumanBodyBones.LeftThumbDistal ,
                HumanBodyBones.LeftIndexProximal ,
                HumanBodyBones.LeftIndexIntermediate ,
                HumanBodyBones.LeftIndexDistal ,
                HumanBodyBones.LeftMiddleProximal ,
                HumanBodyBones.LeftMiddleIntermediate ,
                HumanBodyBones.LeftMiddleDistal ,
                HumanBodyBones.LeftRingProximal ,
                HumanBodyBones.LeftRingIntermediate ,
                HumanBodyBones.LeftRingDistal ,
                HumanBodyBones.LeftLittleProximal ,
                HumanBodyBones.LeftLittleIntermediate ,
                HumanBodyBones.LeftLittleDistal ,
                HumanBodyBones.RightThumbProximal ,
                HumanBodyBones.RightThumbIntermediate ,
                HumanBodyBones.RightThumbDistal ,
                HumanBodyBones.RightIndexProximal ,
                HumanBodyBones.RightIndexIntermediate ,
                HumanBodyBones.RightIndexDistal ,
                HumanBodyBones.RightMiddleProximal ,
                HumanBodyBones.RightMiddleIntermediate ,
                HumanBodyBones.RightMiddleDistal ,
                HumanBodyBones.RightRingProximal ,
                HumanBodyBones.RightRingIntermediate ,
                HumanBodyBones.RightRingDistal ,
                HumanBodyBones.RightLittleProximal ,
                HumanBodyBones.RightLittleIntermediate ,
                HumanBodyBones.RightLittleDistal ,
                HumanBodyBones.Chest ,
                HumanBodyBones.Spine ,
                HumanBodyBones.Hips ,
                HumanBodyBones.LeftUpperLeg ,
                HumanBodyBones.RightUpperLeg ,
                HumanBodyBones.LeftLowerLeg ,
                HumanBodyBones.RightLowerLeg ,
                HumanBodyBones.LeftFoot ,
                HumanBodyBones.RightFoot ,
                HumanBodyBones.LeftToes ,
                HumanBodyBones.RightToes
            };

        public VirtualAvatar(Transform boneParentTransform, MotionSource motionSource)
        {
            parent = boneParentTransform;
            MotionSource = motionSource;
        }
        public (Transform cloneBone, Transform modelBone) GetBoneTransformPair(HumanBodyBones bone)
        {
            if (BoneTransformCache == null) return (null, null);
            if (BoneTransformCache.ContainsKey(bone) == false) return (null, null);
            return BoneTransformCache[bone];
        }

        public Transform GetCloneBoneTransform(HumanBodyBones bone) => GetBoneTransformPair(bone).cloneBone;
        public Transform GetModelBoneTransform(HumanBodyBones bone) => GetBoneTransformPair(bone).modelBone;

        public void ImportAvatar(GameObject model)
        {
            if (avatar != null)
            {
                if(animator != null) GameObject.DestroyImmediate(animator);
                if (RootTransform != null) GameObject.DestroyImmediate(RootTransform.gameObject);
                // Destroy SkeletonRoot
                foreach (Transform child in parent)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }

            currentModel = model;

            var (cloneAvatar, cloneRoot) = CreateCopyAvatar(model, parent);
            avatar = cloneAvatar;
            RootTransform = cloneRoot;
            animator = parent.gameObject.AddComponent<Animator>();
            animator.avatar = avatar;

            InitializeBoneTransformCache(true);
        }

        private Dictionary<HumanBodyBones, (Transform cloneBone, Transform modelBone)> InitializeBoneTransformCache(bool force = false)
        {
            if (boneTransformCache != null && boneTransformCache.Count != 0 && force == false) return boneTransformCache;

            if (currentModel == null) return null;

            if (boneTransformCache == null)
            {
                boneTransformCache = new Dictionary<HumanBodyBones, (Transform cloneBone, Transform modelBone)>();
            }
            else
            {
                boneTransformCache.Clear();
            }

            var modelAnimator = currentModel.GetComponent<Animator>();

            if (modelAnimator == null) return null;

            boneTransformCache.Add(HumanBodyBonesRoot, (animator.transform, modelAnimator.transform));

            foreach (HumanBodyBones bone in ReverseBodyBones)
            {
                if (bone == HumanBodyBones.LastBone) continue;

                var cloneBone = animator.GetBoneTransform(bone);
                if (cloneBone == null) continue;

                var modelBone = modelAnimator.GetBoneTransform(bone);
                if (modelBone == null) continue;

                boneTransformCache.Add(bone, (cloneBone, modelBone));
            }
            return boneTransformCache.Count == 0 ? null : boneTransformCache;
        }

        public void Recenter()
        {
            if (animator == null) return;

            var hipBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            CenterOffsetPosition = -new Vector3(hipBone.position.x, 0, hipBone.position.z);
            CenterOffsetRotationY = -hipBone.rotation.eulerAngles.y;
        }

        /// <summary>
        /// 骨だけコピーしたAvatarを作成する
        /// </summary>
        /// <param name="model">コピー元モデル</param>
        /// <param name="parent">コピー先の親</param>
        /// <returns></returns>
        private (Avatar avatar, Transform root) CreateCopyAvatar(GameObject model, Transform parent)
        {
            var skeletonBones = new List<SkeletonBone>();
            var humanBones = new List<HumanBone>();
            var animator = model.GetComponent<Animator>();

            //同じボーン構造のスケルトンをクローンしてSkeletonBoneのマッピングをする
            var root = animator.GetBoneTransform(HumanBodyBones.Hips).parent;
            var rootClone = CloneTransform(root, parent);
            CopySkeleton(root, rootClone, ref skeletonBones);

            //HumanBoneと実際のボーンの名称のマッピングをする
            GetHumanBones(animator, ref humanBones);

            HumanDescription humanDescription = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeletonBones.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0.0f,
                hasTranslationDoF = false
            };

            var avatar = AvatarBuilder.BuildHumanAvatar(parent.gameObject, humanDescription);

            return (avatar, rootClone);
        }

        private void GetHumanBones(Animator animator, ref List<HumanBone> humanBones)
        {
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;

                var boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform == null) continue;

                var humanBone = new HumanBone()
                {
                    humanName = HumanTrait.BoneName[(int)bone],
                    boneName = boneTransform.name,
                };
                humanBone.limit.useDefaultValues = true;

                humanBones.Add(humanBone);
            }
        }

        private void CopySkeleton(Transform current, Transform cloneCurrent, ref List<SkeletonBone> skeletons)
        {
            SkeletonBone skeletonBone = new SkeletonBone()
            {
                name = cloneCurrent.name,
                position = cloneCurrent.localPosition,
                rotation = cloneCurrent.localRotation,
                scale = cloneCurrent.localScale,
            };
            skeletons.Add(skeletonBone);

            foreach (Transform child in current)
            {
                var childClone = CloneTransform(child, cloneCurrent);
                CopySkeleton(child, childClone, ref skeletons);
            }
        }

        private Transform CloneTransform(Transform source, Transform parent)
        {
            var clone = new GameObject(source.name).transform;
            clone.parent = parent;
            clone.localPosition = source.localPosition;
            clone.localRotation = source.localRotation;
            clone.localScale = source.localScale;

            return clone;
        }

        public T AddComponent<T>() where T : Component => parent.gameObject.AddComponent<T>();
        public T GetComponent<T>() where T : Component => parent.gameObject.GetComponent<T>();
    }
    public enum MotionSource
    {
        VRIK,
        VMCProtocol,
        mocopi,
    }
}
