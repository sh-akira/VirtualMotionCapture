using Mocopi.Receiver;
using Mocopi.Receiver.Core;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC
{
    public class MocopiConnector : MonoBehaviour
    {
        public int Port = 12351;

        [SerializeField]
        private ControlWPFWindow controlWPFWindow;
        private System.Threading.SynchronizationContext context = null;

        private MocopiUdpReceiver udpReceiver;
        private MocopiAvatar mocopiAvatar;
        private GameObject currentModel;

        private Animator modelAnimator;
        private Animator mocopiAnimator;
        private Transform cloneRootTransform;

        private VRIK vrik;
        private IKSolver ikSolver;

        private List<(HumanBodyBones humanBodyBone, Transform source, Transform target)> boneTransformCache;

        private Vector3 centerOffsetPosition;
        private float centerOffsetRotationY;

        private bool isFrameArrived;

        private void Awake()
        {
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
            controlWPFWindow.AdditionalSettingAction += ApplySettings;
            enabled = false;
        }

        private void Start()
        {
            context = System.Threading.SynchronizationContext.Current;
            controlWPFWindow.server.ReceivedEvent += Server_Received;
        }

        private void OnEnable()
        {
            StartUdpReceiver();
        }

        private void OnDisable()
        {
            isFrameArrived = false;
            StopUdpReceiver();
        }
        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.mocopi_GetSetting))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.mocopi_SetSetting
                    {
                        enable = Settings.Current.mocopi_Enable,
                        port = Settings.Current.mocopi_Port,
                        ApplyHead = Settings.Current.mocopi_ApplyHead,
                        ApplyChest = Settings.Current.mocopi_ApplyChest,
                        ApplyRightArm = Settings.Current.mocopi_ApplyRightArm,
                        ApplyLeftArm = Settings.Current.mocopi_ApplyLeftArm,
                        ApplySpine = Settings.Current.mocopi_ApplySpine,
                        ApplyRightHand = Settings.Current.mocopi_ApplyRightHand,
                        ApplyLeftHand = Settings.Current.mocopi_ApplyLeftHand,
                        ApplyRightLeg = Settings.Current.mocopi_ApplyRightLeg,
                        ApplyLeftLeg = Settings.Current.mocopi_ApplyLeftLeg,
                        ApplyRightFoot = Settings.Current.mocopi_ApplyRightFoot,
                        ApplyLeftFoot = Settings.Current.mocopi_ApplyLeftFoot,
                        ApplyRootPosition = Settings.Current.mocopi_ApplyRootPosition,
                        ApplyRootRotation = Settings.Current.mocopi_ApplyRootRotation,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.mocopi_SetSetting))
                {
                    var d = (PipeCommands.mocopi_SetSetting)e.Data;
                    SetSetting(d);
                }
                else if (e.CommandType == typeof(PipeCommands.mocopi_Recenter))
                {
                    Recenter();
                }

            }, null);
        }

        private void SetSetting(PipeCommands.mocopi_SetSetting setting)
        {
            Settings.Current.mocopi_ApplyHead = setting.ApplyHead;
            Settings.Current.mocopi_ApplyChest = setting.ApplyChest;
            Settings.Current.mocopi_ApplyRightArm = setting.ApplyRightArm;
            Settings.Current.mocopi_ApplyLeftArm = setting.ApplyLeftArm;
            Settings.Current.mocopi_ApplySpine = setting.ApplySpine;
            Settings.Current.mocopi_ApplyRightHand = setting.ApplyRightHand;
            Settings.Current.mocopi_ApplyLeftHand = setting.ApplyLeftHand;
            Settings.Current.mocopi_ApplyRightLeg = setting.ApplyRightLeg;
            Settings.Current.mocopi_ApplyLeftLeg = setting.ApplyLeftLeg;
            Settings.Current.mocopi_ApplyRightFoot = setting.ApplyRightFoot;
            Settings.Current.mocopi_ApplyLeftFoot = setting.ApplyLeftFoot;
            Settings.Current.mocopi_ApplyRootPosition = setting.ApplyRootPosition;
            Settings.Current.mocopi_ApplyRootRotation = setting.ApplyRootRotation;

            if (Settings.Current.mocopi_Enable != setting.enable || Settings.Current.mocopi_Port != setting.port)
            {
                Settings.Current.mocopi_Enable = setting.enable;
                Settings.Current.mocopi_Port = setting.port;
                ApplySettings(null);
            }
        }

        private void ApplySettings(GameObject gameObject)
        {
            if (enabled == false && Settings.Current.mocopi_Enable == true)
            {
                Port = Settings.Current.mocopi_Port;
            }
            enabled = Settings.Current.mocopi_Enable;
            ChangePort(Settings.Current.mocopi_Port);
        }

        private void OnCurrentModelChanged(GameObject model)
        {
            if (model != null)
            {
                if (enabled)
                {
                    StopUdpReceiver();
                }

                //ボーンのみのクローンを作成し、mocopiのモーションをそちらに適用させる
                currentModel = model;
                modelAnimator = model.GetComponent<Animator>();

                var (cloneAvatar, cloneRoot) = CreateCopyAvatar(model, transform);
                mocopiAnimator = gameObject.AddComponent<Animator>();
                mocopiAnimator.avatar = cloneAvatar;
                cloneRootTransform = cloneRoot;

                mocopiAvatar = gameObject.AddComponent<MocopiAvatar>();
                mocopiAvatar.MotionSmoothness = 1.0f;
                boneTransformCache = null;

                if (enabled)
                {
                    StartUdpReceiver();
                }
            }
        }
        private void OnModelUnloading(GameObject model)
        {
            //前回の生成物の削除
            if (currentModel != null)
            {
                DestroyImmediate(cloneRootTransform.gameObject);
                // Destroy SkeletonRoot
                foreach (Transform child in transform)
                {
                    DestroyImmediate(child.gameObject);
                }
                DestroyImmediate(mocopiAvatar);
                DestroyImmediate(mocopiAnimator);

                if (ikSolver != null)
                {
                    ikSolver.OnPostUpdate -= ApplyMotion;
                }
                ikSolver = null;
                vrik = null;
                currentModel = null;
                isFrameArrived = false;
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

        private void StartUdpReceiver()
        {
            if (udpReceiver == null)
            {
                udpReceiver = new MocopiUdpReceiver(Port);
            }

            if (mocopiAvatar != null)
            {
                udpReceiver.OnReceiveFrameData += mocopiAvatar.UpdateSkeleton;
                udpReceiver.OnReceiveSkeletonDefinition += InitializeSkeleton;
            }
            udpReceiver?.UdpStart();
        }


        private void StopUdpReceiver()
        {
            if (udpReceiver == null) return;
            udpReceiver.UdpStop();

            if (mocopiAvatar != null)
            {
                udpReceiver.OnReceiveFrameData -= mocopiAvatar.UpdateSkeleton;
                udpReceiver.OnReceiveSkeletonDefinition -= InitializeSkeleton;
            }
            isFrameArrived = false;
            udpReceiver = null;
        }
        public void InitializeSkeleton(int[] boneIds, int[] parentBoneIds, float[] rotationsX, float[] rotationsY, float[] rotationsZ, float[] rotationsW, float[] positionsX, float[] positionsY, float[] positionsZ)
        {
            if (isFrameArrived == false)
            {
                mocopiAvatar.InitializeSkeleton(boneIds, parentBoneIds, rotationsX, rotationsY, rotationsZ, rotationsW, positionsX, positionsY, positionsZ);
                isFrameArrived = true;
            }
        }

        public void ChangePort(int port)
        {
            if (Port != port)
            {
                Port = port;
                if (udpReceiver == null) return;
                StopUdpReceiver();
                StartUdpReceiver();
            }
        }

        private void Recenter()
        {
            if (currentModel == null || mocopiAvatar == null || modelAnimator == null || mocopiAnimator == null) return;

            var mocopiHipBone = mocopiAnimator.GetBoneTransform(HumanBodyBones.Hips);
            centerOffsetPosition = -new Vector3(mocopiHipBone.localPosition.x, 0, mocopiHipBone.localPosition.z);
            centerOffsetRotationY = -mocopiHipBone.localRotation.eulerAngles.y;
        }

        private void ApplyMotion()
        {
            if (mocopiAvatar == null || modelAnimator == null || mocopiAnimator == null) return;

            //無効になってる時は適用しない
            if (enabled == false) return;

            //まだ1度も受信して無い時は適用しない
            if (isFrameArrived == false) return;

            //キャリブレーション中は適用しない
            if (controlWPFWindow.calibrationState == ControlWPFWindow.CalibrationState.WaitingForCalibrating ||
                controlWPFWindow.calibrationState == ControlWPFWindow.CalibrationState.Calibrating) return;

            if (boneTransformCache == null)
            {
                boneTransformCache = new List<(HumanBodyBones humanBodyBone, Transform source, Transform target)>();

                var ReverseBodyBones = new HumanBodyBones[] {
                    HumanBodyBones.Head ,
                    HumanBodyBones.Neck ,
                    HumanBodyBones.LeftShoulder ,
                    HumanBodyBones.RightShoulder ,
                    HumanBodyBones.LeftUpperArm ,
                    HumanBodyBones.RightUpperArm ,
                    HumanBodyBones.LeftLowerArm ,
                    HumanBodyBones.RightLowerArm ,
                    HumanBodyBones.UpperChest ,
                    HumanBodyBones.Chest ,
                    HumanBodyBones.Spine ,
                    HumanBodyBones.LeftHand ,
                    HumanBodyBones.RightHand ,
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

                foreach (HumanBodyBones bone in ReverseBodyBones)
                {
                    if (bone == HumanBodyBones.LastBone) continue;

                    var mocopiBone = mocopiAnimator.GetBoneTransform(bone);
                    if (mocopiBone == null) continue;

                    var modelBone = modelAnimator.GetBoneTransform(bone);
                    if (modelBone == null) continue;

                    boneTransformCache.Add((bone, mocopiBone, modelBone));
                }
            }

            Transform headBone = boneTransformCache[0].target;
            Transform hipBone = null;
            Transform spineBone = null;
            Vector3 defaultHeadPosition = headBone.position;
            Quaternion defaultHeadRotation = headBone.rotation;

            foreach ((var bone, var source, var target) in boneTransformCache)
            {
                bool apply = false;
                switch (bone)
                {
                    case HumanBodyBones.Hips:
                        hipBone = target;
                        if (Settings.Current.mocopi_ApplyRootRotation)
                        {
                            target.localRotation = source.localRotation;
                            target.Rotate(new Vector3(0, centerOffsetRotationY, 0), Space.World);
                        }
                        if (Settings.Current.mocopi_ApplyRootPosition)
                        {
                            target.localPosition = source.localPosition + centerOffsetPosition; //Root位置だけは同期
                        }
                        break;
                    case HumanBodyBones.Spine:
                        spineBone = target;
                        apply = Settings.Current.mocopi_ApplySpine;
                        break;
                    case HumanBodyBones.Chest:
                    case HumanBodyBones.UpperChest:
                        apply = Settings.Current.mocopi_ApplyChest;
                        break;
                    case HumanBodyBones.Neck:
                    case HumanBodyBones.Head:
                        apply = Settings.Current.mocopi_ApplyHead;
                        break;
                    case HumanBodyBones.LeftShoulder:
                    case HumanBodyBones.LeftUpperArm:
                    case HumanBodyBones.LeftLowerArm:
                        apply = Settings.Current.mocopi_ApplyLeftArm;
                        break;
                    case HumanBodyBones.RightShoulder:
                    case HumanBodyBones.RightUpperArm:
                    case HumanBodyBones.RightLowerArm:
                        apply = Settings.Current.mocopi_ApplyRightArm;
                        break;
                    case HumanBodyBones.LeftHand:
                        apply = Settings.Current.mocopi_ApplyLeftHand;
                        break;
                    case HumanBodyBones.RightHand:
                        apply = Settings.Current.mocopi_ApplyRightHand;
                        break;
                    case HumanBodyBones.LeftUpperLeg:
                    case HumanBodyBones.LeftLowerLeg:
                        apply = Settings.Current.mocopi_ApplyLeftLeg;
                        break;
                    case HumanBodyBones.RightUpperLeg:
                    case HumanBodyBones.RightLowerLeg:
                        apply = Settings.Current.mocopi_ApplyRightLeg;
                        break;
                    case HumanBodyBones.LeftFoot:
                    case HumanBodyBones.LeftToes:
                        apply = Settings.Current.mocopi_ApplyLeftFoot;
                        break;
                    case HumanBodyBones.RightFoot:
                    case HumanBodyBones.RightToes:
                        apply = Settings.Current.mocopi_ApplyRightFoot;
                        break;
                }

                if (apply) target.localRotation = source.localRotation;
            }

            if (Settings.Current.mocopi_ApplyHead == false && hipBone != null && spineBone != null)
            {
                //頭の回転無効の時、VR機器優先するために最後に元の位置に戻るように腰を動かす
                var rotdiff = defaultHeadRotation * Quaternion.Inverse(headBone.rotation);
                spineBone.rotation = rotdiff * spineBone.rotation;
                var posdiff = defaultHeadPosition - headBone.position;
                hipBone.position = posdiff + hipBone.position;
            }
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
    }
}