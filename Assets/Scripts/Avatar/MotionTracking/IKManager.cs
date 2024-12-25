using sh_akira;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;
using Valve.VR;
using RootMotion.FinalIK;
using VRM;

namespace VMC
{
    public class IKManager : MonoBehaviour
    {
        private static IKManager instance;
        public static IKManager Instance => instance;



        public CalibrationState CalibrationState = CalibrationState.Uncalibrated;
        public PipeCommands.CalibrateType LastCalibrateType = PipeCommands.CalibrateType.Ipose; //最後に行ったキャリブレーションの種類
        public PipeCommands.CalibrationResult CalibrationResult = new PipeCommands.CalibrationResult { Type = PipeCommands.CalibrateType.Invalid }; //初期値は失敗

        private PipeCommands.CalibrateType currentSelectCalibrateType = PipeCommands.CalibrateType.Ipose;

        [SerializeField]
        private ControlWPFWindow controlWPFWindow;
        private System.Threading.SynchronizationContext context = null;
        public HandController HandController;

        public CameraLookTarget CalibrationCamera;

        public WristRotationFix wristRotationFix;

        public Transform HandTrackerRoot;
        public Transform PelvisTrackerRoot;

        private VirtualAvatar virtualAvatar;

        public VRIK vrik = null;

        public Transform generatedObject;

        private Animator animator => virtualAvatar?.animator;

        private SortedDictionary<int, List<(Guid eventId, Action action)>> OnPostUpdateEvents = new SortedDictionary<int, List<(Guid eventId, Action action)>>();

        private const float LeftLowerArmAngle = -30f;
        private const float RightLowerArmAngle = -30f;
        private const float LeftUpperArmAngle = -30f;
        private const float RightUpperArmAngle = -30f;
        private const float LeftHandAngle = -30f;
        private const float RightHandAngle = -30f;

        private void Awake()
        {
            instance = this;
            context = System.Threading.SynchronizationContext.Current;
            StartCoroutine(AfterUpdateCoroutine());
        }

        private void Start()
        {
            virtualAvatar = new VirtualAvatar(transform, MotionSource.VRIK);
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);

            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
            controlWPFWindow.server.ReceivedEvent += Server_Received;


            virtualAvatar.ApplyRootRotation = true;
            virtualAvatar.ApplyRootPosition = true;
            virtualAvatar.ApplySpine = true;
            virtualAvatar.ApplyChest = true;
            virtualAvatar.ApplyHead = true;
            virtualAvatar.ApplyLeftArm = true;
            virtualAvatar.ApplyRightArm = true;
            virtualAvatar.ApplyLeftHand = true;
            virtualAvatar.ApplyRightHand = true;
            virtualAvatar.ApplyLeftLeg = true;
            virtualAvatar.ApplyRightLeg = true;
            virtualAvatar.ApplyLeftFoot = true;
            virtualAvatar.ApplyRightFoot = true;
            virtualAvatar.ApplyEye = false;
            virtualAvatar.ApplyLeftFinger = true;
            virtualAvatar.ApplyRightFinger = true;

            virtualAvatar.IgnoreDefaultBone = false;
        }
        private void OnCurrentModelChanged(GameObject model)
        {
            if (model != null)
            {
                CalibrationState = CalibrationState.Uncalibrated; //キャリブレーション状態を"未キャリブレーション"に設定
            }
        }
        private void OnModelUnloading(GameObject model)
        {
            RemoveComponents();
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.InitializeCalibration))
                {
                    IKManager.Instance.ModelCalibrationInitialize();
                }
                else if (e.CommandType == typeof(PipeCommands.SelectCalibrateMode))
                {
                    var d = (PipeCommands.SelectCalibrateMode)e.Data;
                    currentSelectCalibrateType = d.CalibrateType;
                    SetCalibratePoseToCurrentModel();
                }
                else if (e.CommandType == typeof(PipeCommands.Calibrate))
                {
                    var d = (PipeCommands.Calibrate)e.Data;
                    StartCoroutine(Calibrate(d.CalibrateType));
                }
                else if (e.CommandType == typeof(PipeCommands.EndCalibrate))
                {
                    EndCalibrate();
                }
                else if (e.CommandType == typeof(PipeCommands.GetTrackerSerialNumbers))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.ReturnTrackerSerialNumbers { List = GetTrackerSerialNumbers(), CurrentSetting = GetCurrentTrackerSettings() }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetTrackerSerialNumbers))
                {
                    var d = (PipeCommands.SetTrackerSerialNumbers)e.Data;
                    SetTrackerSerialNumbers(d);

                }
                else if (e.CommandType == typeof(PipeCommands.GetCalibrationSetting))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.SetCalibrationSetting
                    {
                        EnableOverrideBodyHeight = Settings.Current.EnableOverrideBodyHeight,
                        OverrideBodyHeight = (int)(Settings.Current.OverrideBodyHeight * 1000),
                        PelvisOffsetAdjustY = (int)(Settings.Current.PelvisOffsetAdjustY * 1000),
                        PelvisOffsetAdjustZ = (int)(Settings.Current.PelvisOffsetAdjustZ * 1000),
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetCalibrationSetting))
                {
                    var d = (PipeCommands.SetCalibrationSetting)e.Data;
                    Settings.Current.EnableOverrideBodyHeight = d.EnableOverrideBodyHeight;
                    Settings.Current.OverrideBodyHeight = d.OverrideBodyHeight / 1000f;
                    Settings.Current.PelvisOffsetAdjustY = d.PelvisOffsetAdjustY / 1000f;
                    Settings.Current.PelvisOffsetAdjustZ = d.PelvisOffsetAdjustZ / 1000f;
                }
                else if (e.CommandType == typeof(PipeCommands.SetHandFreeOffset))
                {
                    var d = (PipeCommands.SetHandFreeOffset)e.Data;
                    Settings.Current.LeftHandPositionX = d.LeftHandPositionX / 1000f;
                    Settings.Current.LeftHandPositionY = d.LeftHandPositionY / 1000f;
                    Settings.Current.LeftHandPositionZ = d.LeftHandPositionZ / 1000f;
                    Settings.Current.LeftHandRotationX = d.LeftHandRotationX;
                    Settings.Current.LeftHandRotationY = d.LeftHandRotationY;
                    Settings.Current.LeftHandRotationZ = d.LeftHandRotationZ;
                    Settings.Current.RightHandPositionX = d.RightHandPositionX / 1000f;
                    Settings.Current.RightHandPositionY = d.RightHandPositionY / 1000f;
                    Settings.Current.RightHandPositionZ = d.RightHandPositionZ / 1000f;
                    Settings.Current.RightHandRotationX = d.RightHandRotationX;
                    Settings.Current.RightHandRotationY = d.RightHandRotationY;
                    Settings.Current.RightHandRotationZ = d.RightHandRotationZ;
                    Settings.Current.SwivelOffset = d.SwivelOffset;
                    SetHandFreeOffset();
                }
                else if (e.CommandType == typeof(PipeCommands.GetTrackerOffsets))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.SetTrackerOffsets
                    {
                        LeftHandTrackerOffsetToBodySide = Settings.Current.LeftHandTrackerOffsetToBodySide,
                        LeftHandTrackerOffsetToBottom = Settings.Current.LeftHandTrackerOffsetToBottom,
                        RightHandTrackerOffsetToBodySide = Settings.Current.RightHandTrackerOffsetToBodySide,
                        RightHandTrackerOffsetToBottom = Settings.Current.RightHandTrackerOffsetToBottom
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetTrackerOffsets))
                {
                    var d = (PipeCommands.SetTrackerOffsets)e.Data;
                    Settings.Current.LeftHandTrackerOffsetToBodySide = d.LeftHandTrackerOffsetToBodySide;
                    Settings.Current.LeftHandTrackerOffsetToBottom = d.LeftHandTrackerOffsetToBottom;
                    Settings.Current.RightHandTrackerOffsetToBodySide = d.RightHandTrackerOffsetToBodySide;
                    Settings.Current.RightHandTrackerOffsetToBottom = d.RightHandTrackerOffsetToBottom;
                }
                else if (e.CommandType == typeof(PipeCommands.SetHandAngle))
                {
                    var d = (PipeCommands.SetHandAngle)e.Data;
                    HandController.SetHandEulerAngles(d.LeftEnable, d.RightEnable, HandController.CalcHandEulerAngles(d.HandAngles));
                }

            }, null);
        }

        private void RemoveComponents()
        {
            if (virtualAvatar != null)
            {
                var currentVRIKTimingManager = virtualAvatar.GetComponent<VRIKTimingManager>();
                if (currentVRIKTimingManager != null) DestroyImmediate(currentVRIKTimingManager);
                var rootController = virtualAvatar.GetComponent<VRIKRootController>();
                if (rootController != null) DestroyImmediate(rootController);
                var currentvrik = virtualAvatar.GetComponent<VRIK>();
                if (currentvrik != null)
                {
                    currentvrik.solver.OnPostUpdate -= OnPostUpdate;
                    DestroyImmediate(currentvrik);
                }
            }
        }

        public void ModelCalibrationInitialize()
        {
            CalibrationState = CalibrationState.WaitingForCalibrating; //キャリブレーション状態を"キャリブレーション待機中"に設定

            if (virtualAvatar != null)
            {
                RemoveComponents();
                MotionManager.Instance.ResetVirtualAvatarPose(virtualAvatar);
            }

            //SetVRIK(CurrentModel);
            if (animator != null)
            {
                SetCalibratePoseToCurrentModel();

                //wristRotationFix.SetVRIK(vrik);

                HandController.SetDefaultAngle(animator);

                HandController.SetNaturalPose();

                //トラッカーのスケールリセット
                HandTrackerRoot.localPosition = Vector3.zero;
                HandTrackerRoot.localScale = Vector3.one;
                PelvisTrackerRoot.localPosition = Vector3.zero;
                PelvisTrackerRoot.localScale = Vector3.one;

                //トラッカー位置の表示
                TrackingPointManager.Instance.SetTrackingPointPositionVisible(true);

                if (CalibrationCamera != null)
                {
                    CalibrationCamera.Target = animator.GetBoneTransform(HumanBodyBones.Head);
                    CalibrationCamera.gameObject.SetActive(true);
                }
            }
        }

        public void ModelInitialize()
        {

            SetVRIK(virtualAvatar);

            if (animator != null)
            {
                wristRotationFix.SetVRIK(vrik);

                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).eulerAngles = new Vector3(LeftLowerArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm).eulerAngles = new Vector3(RightLowerArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).eulerAngles = new Vector3(LeftUpperArmAngle, 0, 0);
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).eulerAngles = new Vector3(RightUpperArmAngle, 0, 0);

                HandController.SetDefaultAngle(animator);

                //初期の指を自然に閉じたポーズにする
                HandController.SetNaturalPose();
            }
            //SetTrackersToVRIK();
        }


        private void SetCalibratePoseToCurrentModel()
        {
            if (animator != null)
            {
                if (currentSelectCalibrateType == PipeCommands.CalibrateType.Ipose)
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftShoulder).localEulerAngles = new Vector3(0, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightShoulder).localEulerAngles = new Vector3(0, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(0, 0, 80);
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(0, 0, 5);
                    animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(0, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(0, 0, -80);
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(0, 0, -5);
                    animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(0, 0, 0);
                }
                else
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(LeftLowerArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(RightLowerArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(LeftUpperArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(RightUpperArmAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(LeftHandAngle, 0, 0);
                    animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(RightHandAngle, 0, 0);
                }
            }
        }


        public void ResetTrackerScale()
        {
            //jsonが正しくデコードできていなければ無視する
            if (Settings.Current == null)
            {
                return;
            }

            //トラッカーのルートスケールを初期値に戻す
            HandTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            PelvisTrackerRoot.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            HandTrackerRoot.position = Vector3.zero;
            PelvisTrackerRoot.position = Vector3.zero;

            //スケール変更時の位置オフセット設定
            var handTrackerOffset = HandTrackerRoot.GetComponent<ScalePositionOffset>();
            var footTrackerOffset = PelvisTrackerRoot.GetComponent<ScalePositionOffset>();
            handTrackerOffset.ResetTargetAndPosition();
            footTrackerOffset.ResetTargetAndPosition();
        }
        #region Calibration

        public void FixLegDirection(VirtualAvatar targetHumanoidModel)
        {
            var avatarForward = targetHumanoidModel.transform.forward;
            var animator = targetHumanoidModel.GetComponent<Animator>();

            var leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var leftFootDefaultRotation = leftFoot.rotation;
            var leftFootTargetPosition = new Vector3(leftFoot.position.x, leftFoot.position.y, leftFoot.position.z);
            LookAtBones(leftFootTargetPosition + avatarForward * 0.03f, leftUpperLeg, leftLowerLeg);
            LookAtBones(leftFootTargetPosition, leftLowerLeg, leftFoot);
            leftFoot.rotation = leftFootDefaultRotation;

            var rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var rightFootDefaultRotation = rightFoot.rotation;
            var rightFootTargetPosition = new Vector3(rightFoot.position.x, rightFoot.position.y, rightFoot.position.z);
            LookAtBones(rightFootTargetPosition + avatarForward * 0.03f, rightUpperLeg, rightLowerLeg);
            LookAtBones(rightFootTargetPosition, rightLowerLeg, rightFoot);
            rightFoot.rotation = rightFootDefaultRotation;
        }

        public void FixArmDirection(VirtualAvatar targetHumanoidModel)
        {
            var avatarForward = targetHumanoidModel.transform.forward;
            var animator = targetHumanoidModel.GetComponent<Animator>();

            var leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var leftHandDefaultRotation = leftHand.rotation;
            var leftHandTargetPosition = new Vector3(leftHand.position.x, leftHand.position.y, leftHand.position.z);
            LookAtBones(leftHandTargetPosition + avatarForward * 0.01f, leftShoulder, leftUpperArm);
            LookAtBones(leftHandTargetPosition - avatarForward * 0.01f, leftUpperArm, leftLowerArm);
            LookAtBones(leftHandTargetPosition, leftLowerArm, leftHand);
            leftHand.rotation = leftHandDefaultRotation;

            var rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            var rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var rightHandDefaultRotation = rightHand.rotation;
            var rightHandTargetPosition = new Vector3(rightHand.position.x, rightHand.position.y, rightHand.position.z);
            LookAtBones(rightHandTargetPosition + avatarForward * 0.01f, rightShoulder, rightUpperArm);
            LookAtBones(rightHandTargetPosition - avatarForward * 0.01f, rightUpperArm, rightLowerArm);
            LookAtBones(rightHandTargetPosition, rightLowerArm, rightHand);
            rightHand.rotation = rightHandDefaultRotation;
        }

        private void LookAtBones(Vector3 lookTargetPosition, params Transform[] bones)
        {
            for (int i = 0; i < bones.Length - 1; i++)
            {
                bones[i].rotation = Quaternion.FromToRotation((bones[i].position - bones[i + 1].position).normalized, (bones[i].position - lookTargetPosition).normalized) * bones[i].rotation;
            }
        }

        private void SetVRIK(VirtualAvatar virtualAvatar)
        {
            //膝のボーンの曲がる方向で膝の向きが決まってしまうため、強制的に膝のボーンを少し前に曲げる
            var leftOffset = Vector3.zero;
            var rightOffset = Vector3.zero;
            if (animator != null && Settings.Current.FixKneeRotation)
            {
                //leftOffset = fixKneeBone(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot));
                //rightOffset = fixKneeBone(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot));
                //fixPelvisBone(animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.Hips));
                FixLegDirection(virtualAvatar);
            }

            if (animator != null && Settings.Current.FixElbowRotation)
            {
                FixArmDirection(virtualAvatar);
            }

            vrik = virtualAvatar.AddComponent<VRIK>();
            virtualAvatar.AddComponent<VRIKTimingManager>();
            vrik.AutoDetectReferences();

            //親指の方向の検出に失敗すると腕の回転もおかしくなる
            vrik.solver.leftArm.palmToThumbAxis = new Vector3(0, 0, 1);
            vrik.solver.rightArm.palmToThumbAxis = new Vector3(0, 0, 1);

            vrik.solver.FixTransforms();

            vrik.solver.IKPositionWeight = 0f;
            vrik.solver.leftArm.stretchCurve = new AnimationCurve();
            vrik.solver.rightArm.stretchCurve = new AnimationCurve();
            vrik.UpdateSolverExternal();

            vrik.solver.OnPostUpdate += OnPostUpdate;

            //膝のボーンの曲がる方向で膝の向きが決まってしまうため、強制的に膝のボーンを少し前に曲げる
            //if (animator != null)
            //{
            //    unfixKneeBone(leftOffset, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            //    unfixKneeBone(rightOffset, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot));
            //}
            //if (animator != null)
            //{
            //    var leftWrist = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).gameObject;
            //    var rightWrist = animator.GetBoneTransform(HumanBodyBones.RightLowerArm).gameObject;
            //    var leftRelaxer = leftWrist.AddComponent<TwistRelaxer>();
            //    var rightRelaxer = rightWrist.AddComponent<TwistRelaxer>();
            //    leftRelaxer.ik = vrik;
            //    rightRelaxer.ik = vrik;
            //}
        }

        private List<Tuple<string, string>> GetTrackerSerialNumbers()
        {
            var list = new List<Tuple<string, string>>();
            foreach (var trackingPoint in TrackingPointManager.Instance.GetTrackingPoints())
            {
                if (trackingPoint.DeviceClass == ETrackedDeviceClass.HMD)
                {
                    list.Add(Tuple.Create("HMD", trackingPoint.Name));
                }
                else if (trackingPoint.DeviceClass == ETrackedDeviceClass.Controller)
                {
                    list.Add(Tuple.Create("コントローラー", trackingPoint.Name));
                }
                else if (trackingPoint.DeviceClass == ETrackedDeviceClass.GenericTracker)
                {
                    list.Add(Tuple.Create("トラッカー", trackingPoint.Name));
                }
                else
                {
                    list.Add(Tuple.Create("Unknown", trackingPoint.Name));
                }
            }
            return list;
        }

        private PipeCommands.SetTrackerSerialNumbers GetCurrentTrackerSettings()
        {
            var deviceDictionary = new Dictionary<ETrackedDeviceClass, string>
        {
            {ETrackedDeviceClass.HMD, "HMD"},
            {ETrackedDeviceClass.Controller, "コントローラー"},
            {ETrackedDeviceClass.GenericTracker, "トラッカー"},
            {ETrackedDeviceClass.TrackingReference, "ベースステーション"},
            {ETrackedDeviceClass.Invalid, "割り当てしない"},
        };
            return new PipeCommands.SetTrackerSerialNumbers
            {
                Head = Tuple.Create(deviceDictionary[Settings.Current.Head.Item1], Settings.Current.Head.Item2),
                LeftHand = Tuple.Create(deviceDictionary[Settings.Current.LeftHand.Item1], Settings.Current.LeftHand.Item2),
                RightHand = Tuple.Create(deviceDictionary[Settings.Current.RightHand.Item1], Settings.Current.RightHand.Item2),
                Pelvis = Tuple.Create(deviceDictionary[Settings.Current.Pelvis.Item1], Settings.Current.Pelvis.Item2),
                LeftFoot = Tuple.Create(deviceDictionary[Settings.Current.LeftFoot.Item1], Settings.Current.LeftFoot.Item2),
                RightFoot = Tuple.Create(deviceDictionary[Settings.Current.RightFoot.Item1], Settings.Current.RightFoot.Item2),
                LeftElbow = Tuple.Create(deviceDictionary[Settings.Current.LeftElbow.Item1], Settings.Current.LeftElbow.Item2),
                RightElbow = Tuple.Create(deviceDictionary[Settings.Current.RightElbow.Item1], Settings.Current.RightElbow.Item2),
                LeftKnee = Tuple.Create(deviceDictionary[Settings.Current.LeftKnee.Item1], Settings.Current.LeftKnee.Item2),
                RightKnee = Tuple.Create(deviceDictionary[Settings.Current.RightKnee.Item1], Settings.Current.RightKnee.Item2),
            };
        }

        private void SetTrackerSerialNumbers(PipeCommands.SetTrackerSerialNumbers data)
        {
            var deviceDictionary = new Dictionary<string, ETrackedDeviceClass>
        {
            {"HMD", ETrackedDeviceClass.HMD },
            {"コントローラー", ETrackedDeviceClass.Controller },
            {"トラッカー", ETrackedDeviceClass.GenericTracker },
            {"ベースステーション", ETrackedDeviceClass.TrackingReference },
            {"割り当てしない", ETrackedDeviceClass.Invalid },
        };

            Settings.Current.Head = Tuple.Create(deviceDictionary[data.Head.Item1], data.Head.Item2);
            Settings.Current.LeftHand = Tuple.Create(deviceDictionary[data.LeftHand.Item1], data.LeftHand.Item2);
            Settings.Current.RightHand = Tuple.Create(deviceDictionary[data.RightHand.Item1], data.RightHand.Item2);
            Settings.Current.Pelvis = Tuple.Create(deviceDictionary[data.Pelvis.Item1], data.Pelvis.Item2);
            Settings.Current.LeftFoot = Tuple.Create(deviceDictionary[data.LeftFoot.Item1], data.LeftFoot.Item2);
            Settings.Current.RightFoot = Tuple.Create(deviceDictionary[data.RightFoot.Item1], data.RightFoot.Item2);
            Settings.Current.LeftElbow = Tuple.Create(deviceDictionary[data.LeftElbow.Item1], data.LeftElbow.Item2);
            Settings.Current.RightElbow = Tuple.Create(deviceDictionary[data.RightElbow.Item1], data.RightElbow.Item2);
            Settings.Current.LeftKnee = Tuple.Create(deviceDictionary[data.LeftKnee.Item1], data.LeftKnee.Item2);
            Settings.Current.RightKnee = Tuple.Create(deviceDictionary[data.RightKnee.Item1], data.RightKnee.Item2);
            SetVRIKTargetTrackers();
        }

        private enum TargetType
        {
            Head, Pelvis, LeftArm, RightArm, LeftLeg, RightLeg, LeftElbow, RightElbow, LeftKnee, RightKnee
        }

        private TrackingPoint GetTrackerTransformBySerialNumber(Tuple<ETrackedDeviceClass, string> serial, TargetType setTo, Transform headTracker = null)
        {
            var manager = TrackingPointManager.Instance;
            if (serial.Item1 == ETrackedDeviceClass.HMD)
            {
                if (string.IsNullOrEmpty(serial.Item2))
                {
                    return manager.GetTrackingPoints(ETrackedDeviceClass.HMD).FirstOrDefault();
                }
                else if (manager.TryGetTrackingPoint(serial.Item2, out var hmdTrackingPoint))
                {
                    return hmdTrackingPoint;
                }
            }
            else if (serial.Item1 == ETrackedDeviceClass.Controller)
            {
                var controllers = manager.GetTrackingPoints(ETrackedDeviceClass.Controller).Where(d => d.Name.Contains("LIV Virtual Camera") == false);
                TrackingPoint ret = null;
                foreach (var controller in controllers)
                {
                    if (controller != null && controller.Name == serial.Item2)
                    {
                        if (setTo == TargetType.LeftArm || setTo == TargetType.RightArm)
                        {
                            ret = controller;
                            break;
                        }
                        return controller;
                    }
                }
                if (ret == null)
                {
                    var controllerTrackingPoints = controllers.Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.TargetTransform.position - headTracker.position), trackingPoint = d })
                                                           .OrderBy(d => d.pos.x)
                                                           .Select(d => d.trackingPoint);
                    if (setTo == TargetType.LeftArm) ret = controllerTrackingPoints.ElementAtOrDefault(0);
                    if (setTo == TargetType.RightArm) ret = controllerTrackingPoints.ElementAtOrDefault(1);
                }
                return ret;
            }
            else if (serial.Item1 == ETrackedDeviceClass.GenericTracker)
            {
                foreach (var tracker in manager.GetTrackingPoints(ETrackedDeviceClass.GenericTracker).Where(d => d.Name.Contains("LIV Virtual Camera") == false && !(Settings.Current.VirtualMotionTrackerEnable && d.Name.Contains($"VMT_{Settings.Current.VirtualMotionTrackerNo}"))))
                {
                    if (tracker != null && tracker.Name == serial.Item2)
                    {
                        return tracker;
                    }
                }
                if (string.IsNullOrEmpty(serial.Item2) == false) return null; //Serialあるのに見つからなかったらnull

                var trackerIds = new List<string>();

                if (Settings.Current.Head.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.Head.Item2);
                if (Settings.Current.LeftHand.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftHand.Item2);
                if (Settings.Current.RightHand.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightHand.Item2);
                if (Settings.Current.Pelvis.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.Pelvis.Item2);
                if (Settings.Current.LeftFoot.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftFoot.Item2);
                if (Settings.Current.RightFoot.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightFoot.Item2);
                if (Settings.Current.LeftElbow.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftElbow.Item2);
                if (Settings.Current.RightElbow.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightElbow.Item2);
                if (Settings.Current.LeftKnee.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.LeftKnee.Item2);
                if (Settings.Current.RightKnee.Item1 == ETrackedDeviceClass.GenericTracker) trackerIds.Add(Settings.Current.RightKnee.Item2);

                //ここに来るときは腰か足のトラッカー自動認識になってるとき
                //割り当てられていないトラッカーリスト
                var autoTrackers = manager.GetTrackingPoints(ETrackedDeviceClass.GenericTracker).Where(d => d.TrackingWatcher.ok).Where(d => trackerIds.Contains(d.Name) == false).Select((d, i) => new { index = i, pos = headTracker.InverseTransformDirection(d.TargetTransform.position - headTracker.position), trackingPoint = d });
                if (autoTrackers.Any())
                {
                    var count = autoTrackers.Count();
                    if (count >= 3)
                    {
                        if (setTo == TargetType.Pelvis)
                        { //腰は一番高い位置にあるトラッカー
                            return autoTrackers.OrderByDescending(d => d.pos.y).Select(d => d.trackingPoint).First();
                        }
                    }
                    if (count >= 2)
                    {
                        if (setTo == TargetType.LeftLeg)
                        {
                            return autoTrackers.OrderBy(d => d.pos.y).Take(2).OrderBy(d => d.pos.x).Select(d => d.trackingPoint).First();
                        }
                        else if (setTo == TargetType.RightLeg)
                        {
                            return autoTrackers.OrderBy(d => d.pos.y).Take(2).OrderByDescending(d => d.pos.x).Select(d => d.trackingPoint).First();
                        }
                    }
                }
            }
            return null;
        }

        private void SetVRIKTargetTrackers()
        {
            if (vrik == null) { return; } //まだmodelがない

            vrik.solver.spine.headTarget = GetTrackerTransformBySerialNumber(Settings.Current.Head, TargetType.Head)?.TargetTransform;
            vrik.solver.spine.headClampWeight = 0.38f;

            vrik.solver.spine.pelvisTarget = GetTrackerTransformBySerialNumber(Settings.Current.Pelvis, TargetType.Pelvis, vrik.solver.spine.headTarget)?.TargetTransform;
            if (vrik.solver.spine.pelvisTarget != null)
            {
                vrik.solver.spine.pelvisPositionWeight = 1f;
                vrik.solver.spine.pelvisRotationWeight = 1f;
                vrik.solver.plantFeet = false;
                vrik.solver.spine.neckStiffness = 0f;
                vrik.solver.spine.maxRootAngle = 180f;
            }
            else
            {
                vrik.solver.spine.pelvisPositionWeight = 0f;
                vrik.solver.spine.pelvisRotationWeight = 0f;
                vrik.solver.plantFeet = true;
                vrik.solver.spine.neckStiffness = 1f;
                vrik.solver.spine.maxRootAngle = 0f;
            }

            vrik.solver.leftArm.target = GetTrackerTransformBySerialNumber(Settings.Current.LeftHand, TargetType.LeftArm, vrik.solver.spine.headTarget)?.TargetTransform;
            if (vrik.solver.leftArm.target != null)
            {
                vrik.solver.leftArm.positionWeight = 1f;
                vrik.solver.leftArm.rotationWeight = 1f;
            }
            else
            {
                vrik.solver.leftArm.positionWeight = 0f;
                vrik.solver.leftArm.rotationWeight = 0f;
            }

            vrik.solver.rightArm.target = GetTrackerTransformBySerialNumber(Settings.Current.RightHand, TargetType.RightArm, vrik.solver.spine.headTarget)?.TargetTransform;
            if (vrik.solver.rightArm.target != null)
            {
                vrik.solver.rightArm.positionWeight = 1f;
                vrik.solver.rightArm.rotationWeight = 1f;
            }
            else
            {
                vrik.solver.rightArm.positionWeight = 0f;
                vrik.solver.rightArm.rotationWeight = 0f;
            }

            vrik.solver.leftLeg.target = GetTrackerTransformBySerialNumber(Settings.Current.LeftFoot, TargetType.LeftLeg, vrik.solver.spine.headTarget)?.TargetTransform;
            if (vrik.solver.leftLeg.target != null)
            {
                vrik.solver.leftLeg.positionWeight = 1f;
                vrik.solver.leftLeg.rotationWeight = 1f;
            }
            else
            {
                vrik.solver.leftLeg.positionWeight = 0f;
                vrik.solver.leftLeg.rotationWeight = 0f;
            }

            vrik.solver.rightLeg.target = GetTrackerTransformBySerialNumber(Settings.Current.RightFoot, TargetType.RightLeg, vrik.solver.spine.headTarget)?.TargetTransform;
            if (vrik.solver.rightLeg.target != null)
            {
                vrik.solver.rightLeg.positionWeight = 1f;
                vrik.solver.rightLeg.rotationWeight = 1f;
            }
            else
            {
                vrik.solver.rightLeg.positionWeight = 0f;
                vrik.solver.rightLeg.rotationWeight = 0f;
            }
        }

        private Transform leftHandFreeOffsetRotation;
        private Transform rightHandFreeOffsetRotation;
        private Transform leftHandFreeOffsetPosition;
        private Transform rightHandFreeOffsetPosition;

        public IEnumerator Calibrate(PipeCommands.CalibrateType calibrateType)
        {
            LastCalibrateType = calibrateType;//最後に実施したキャリブレーションタイプとして記録


            //開始状態を格納
            CalibrationResult = new PipeCommands.CalibrationResult
            {
                Type = calibrateType
            };


            if (animator == null)
            {
                Debug.LogError("[Calib Fail] No avatar found. (animator == null)");
                yield break;
            }

            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.LeftHand).localEulerAngles = new Vector3(0, 0, 0);
            animator.GetBoneTransform(HumanBodyBones.RightHand).localEulerAngles = new Vector3(0, 0, 0);

            SetVRIK(virtualAvatar);
            wristRotationFix.SetVRIK(vrik);

            var headTracker = GetTrackerTransformBySerialNumber(Settings.Current.Head, TargetType.Head);
            var leftHandTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftHand, TargetType.LeftArm, headTracker?.TargetTransform);
            var rightHandTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightHand, TargetType.RightArm, headTracker?.TargetTransform);
            var bodyTracker = GetTrackerTransformBySerialNumber(Settings.Current.Pelvis, TargetType.Pelvis, headTracker?.TargetTransform);
            var leftFootTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftFoot, TargetType.LeftLeg, headTracker?.TargetTransform);
            var rightFootTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightFoot, TargetType.RightLeg, headTracker?.TargetTransform);
            var leftElbowTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftElbow, TargetType.LeftElbow, headTracker?.TargetTransform);
            var rightElbowTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightElbow, TargetType.RightElbow, headTracker?.TargetTransform);
            var leftKneeTracker = GetTrackerTransformBySerialNumber(Settings.Current.LeftKnee, TargetType.LeftKnee, headTracker?.TargetTransform);
            var rightKneeTracker = GetTrackerTransformBySerialNumber(Settings.Current.RightKnee, TargetType.RightKnee, headTracker?.TargetTransform);

            ClearChildren(headTracker, leftHandTracker, rightHandTracker, bodyTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);

            var settings = new RootMotion.FinalIK.VRIKCalibrator.Settings();

            yield return new WaitForEndOfFrame();

            var leftHandOffset = Vector3.zero;
            var rightHandOffset = Vector3.zero;

            //トラッカー
            //xをプラス方向に動かすとトラッカーの左(LEDを上に見たとき)に進む
            //yをプラス方向に動かすとトラッカーの上(LED方向)に進む
            //zをマイナス方向に動かすとトラッカーの底面に向かって進む

            if (Settings.Current.LeftHand.Item1 == ETrackedDeviceClass.GenericTracker)
            {
                //角度補正(左手なら右のトラッカーに向けた)後
                //xを＋方向は体の正面に向かって進む
                //yを＋方向は体の上(天井方向)に向かって進む
                //zを＋方向は体中心(左手なら右手の方向)に向かって進む
                leftHandOffset = new Vector3(1.0f, Settings.Current.LeftHandTrackerOffsetToBottom, Settings.Current.LeftHandTrackerOffsetToBodySide); // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
            }
            if (Settings.Current.RightHand.Item1 == ETrackedDeviceClass.GenericTracker)
            {
                //角度補正(左手なら右のトラッカーに向けた)後
                //xを－方向は体の正面に向かって進む
                //yを＋方向は体の上(天井方向)に向かって進む
                //zを＋方向は体中心(左手なら右手の方向)に向かって進む
                rightHandOffset = new Vector3(1.0f, Settings.Current.RightHandTrackerOffsetToBottom, Settings.Current.RightHandTrackerOffsetToBodySide); // Vector3 (IsEnable, ToTrackerBottom, ToBodySide)
            }

            TrackingPointManager.Instance.ClearTrackingWatcher();

            foreach (Transform child in generatedObject)
            {
                DestroyImmediate(child.gameObject);
            }

            var trackerPositions = new TrackerPositions
            {
                Head = new TrackerPosition(headTracker),
                LeftHand = new TrackerPosition(leftHandTracker),
                RightHand = new TrackerPosition(rightHandTracker),
                Pelvis = new TrackerPosition(bodyTracker),
                LeftFoot = new TrackerPosition(leftFootTracker),
                RightFoot = new TrackerPosition(rightFootTracker),
                LeftElbow = new TrackerPosition(leftElbowTracker),
                RightElbow = new TrackerPosition(rightElbowTracker),
                LeftKnee = new TrackerPosition(leftKneeTracker),
                RightKnee = new TrackerPosition(rightKneeTracker),
            };

            try
            {
                var trackerPositionsJson = JsonUtility.ToJson(trackerPositions);
                string path = Path.GetFullPath(Application.dataPath + "/../TrackerPositions.json");
                var directoryName = Path.GetDirectoryName(path);
                if (Directory.Exists(directoryName) == false) Directory.CreateDirectory(directoryName);
                File.WriteAllText(path, Json.Serializer.ToReadable(trackerPositionsJson));
            }
            catch { }

            if (calibrateType == PipeCommands.CalibrateType.Ipose || calibrateType == PipeCommands.CalibrateType.Tpose)
            {
                yield return FinalIKCalibrator.Calibrate(calibrateType == PipeCommands.CalibrateType.Ipose ? FinalIKCalibrator.CalibrateMode.Ipose : FinalIKCalibrator.CalibrateMode.Tpose, HandTrackerRoot, PelvisTrackerRoot, vrik, settings, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker, generatedObject);
            }
            else if (calibrateType == PipeCommands.CalibrateType.FixedHand)
            {
                yield return Calibrator.CalibrateFixedHand(HandTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
            }
            else if (calibrateType == PipeCommands.CalibrateType.FixedHandWithGround)
            {
                yield return Calibrator.CalibrateFixedHandWithGround(HandTrackerRoot, PelvisTrackerRoot, vrik, settings, leftHandOffset, rightHandOffset, headTracker, bodyTracker, leftHandTracker, rightHandTracker, leftFootTracker, rightFootTracker, leftElbowTracker, rightElbowTracker, leftKneeTracker, rightKneeTracker);
            }

            vrik.solver.IKPositionWeight = 1.0f;
            if (leftFootTracker == null && rightFootTracker == null)
            {
                vrik.solver.plantFeet = true;
                vrik.solver.locomotion.weight = 1.0f;
                var rootController = vrik.references.root.GetComponent<RootMotion.FinalIK.VRIKRootController>();
                if (rootController != null) GameObject.Destroy(rootController);
            }

            vrik.solver.locomotion.footDistance = 0.08f;
            vrik.solver.locomotion.stepThreshold = 0.05f;
            vrik.solver.locomotion.angleThreshold = 10f;
            vrik.solver.locomotion.maxVelocity = 0.04f;
            vrik.solver.locomotion.velocityFactor = 0.04f;
            vrik.solver.locomotion.rootSpeed = 40;
            vrik.solver.locomotion.stepSpeed = 2;
            vrik.solver.locomotion.offset = new Vector3(0, 0, 0.03f);

            Settings.Current.headTracker = StoreTransform.Create(headTracker?.TargetTransform);
            Settings.Current.bodyTracker = StoreTransform.Create(bodyTracker?.TargetTransform);
            Settings.Current.leftHandTracker = StoreTransform.Create(leftHandTracker?.TargetTransform);
            Settings.Current.rightHandTracker = StoreTransform.Create(rightHandTracker?.TargetTransform);
            Settings.Current.leftFootTracker = StoreTransform.Create(leftFootTracker?.TargetTransform);
            Settings.Current.rightFootTracker = StoreTransform.Create(rightFootTracker?.TargetTransform);
            Settings.Current.leftElbowTracker = StoreTransform.Create(leftElbowTracker?.TargetTransform);
            Settings.Current.rightElbowTracker = StoreTransform.Create(rightElbowTracker?.TargetTransform);
            Settings.Current.leftKneeTracker = StoreTransform.Create(leftKneeTracker?.TargetTransform);
            Settings.Current.rightKneeTracker = StoreTransform.Create(rightKneeTracker?.TargetTransform);

            var calibratedLeftHandTransform = leftHandTracker?.TargetTransform?.OfType<Transform>().FirstOrDefault();
            var calibratedRightHandTransform = rightHandTracker?.TargetTransform?.OfType<Transform>().FirstOrDefault();

            if (calibratedLeftHandTransform != null && calibratedRightHandTransform != null)
            {
                leftHandFreeOffsetRotation = new GameObject(nameof(leftHandFreeOffsetRotation)).transform;
                rightHandFreeOffsetRotation = new GameObject(nameof(rightHandFreeOffsetRotation)).transform;
                leftHandFreeOffsetRotation.SetParent(leftHandTracker?.TargetTransform);
                rightHandFreeOffsetRotation.SetParent(rightHandTracker?.TargetTransform);
                leftHandFreeOffsetRotation.localPosition = Vector3.zero;
                leftHandFreeOffsetRotation.localRotation = Quaternion.identity;
                leftHandFreeOffsetRotation.localScale = Vector3.one;
                rightHandFreeOffsetRotation.localPosition = Vector3.zero;
                rightHandFreeOffsetRotation.localRotation = Quaternion.identity;
                rightHandFreeOffsetRotation.localScale = Vector3.one;

                leftHandFreeOffsetPosition = new GameObject(nameof(leftHandFreeOffsetPosition)).transform;
                rightHandFreeOffsetPosition = new GameObject(nameof(rightHandFreeOffsetPosition)).transform;
                leftHandFreeOffsetPosition.SetParent(leftHandFreeOffsetRotation);
                rightHandFreeOffsetPosition.SetParent(rightHandFreeOffsetRotation);
                leftHandFreeOffsetPosition.localPosition = Vector3.zero;
                leftHandFreeOffsetPosition.localRotation = Quaternion.identity;
                leftHandFreeOffsetPosition.localScale = Vector3.one;
                rightHandFreeOffsetPosition.localPosition = Vector3.zero;
                rightHandFreeOffsetPosition.localRotation = Quaternion.identity;
                rightHandFreeOffsetPosition.localScale = Vector3.one;

                calibratedLeftHandTransform.parent = leftHandFreeOffsetPosition;
                calibratedRightHandTransform.parent = rightHandFreeOffsetPosition;
            }

            yield return null;

            if (CalibrationResult.Type == PipeCommands.CalibrateType.Invalid)
            {
                CalibrationState = CalibrationState.Uncalibrated; //キャリブレーションタイプがInvalidになっているときはキャリブレーション失敗
            }
            else
            {
                CalibrationState = CalibrationState.Calibrating; //キャリブレーション状態を"キャリブレーション中"に設定
            }
        }

        private void ClearChildren(params TrackingPoint[] Parents) => ClearChildren(Parents.Select(d => d?.TargetTransform).ToArray());

        private void ClearChildren(params Transform[] Parents)
        {
            foreach (var parent in Parents)
            {
                if (parent != null)
                {
                    foreach (Transform child in parent)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        public void EndCalibrate()
        {
            //トラッカー位置の非表示
            TrackingPointManager.Instance.SetTrackingPointPositionVisible(false);

            if (CalibrationCamera != null)
            {
                CalibrationCamera.gameObject.SetActive(false);
            }
            SetHandFreeOffset();
            //SetTrackersToVRIK();

            //直前がキャリブレーション実行中なら
            if (CalibrationState == CalibrationState.Calibrating)
            {
                CalibrationState = CalibrationState.Calibrated; //キャリブレーション状態を"キャリブレーション完了"に設定

                context.Post(async (_) =>
                {
                    //最終結果を送信
                    await controlWPFWindow.server.SendCommandAsync(CalibrationResult);
                }, null);
            }
            else
            {
                //キャンセルされたなど
                CalibrationState = CalibrationState.Uncalibrated; //キャリブレーション状態を"未キャリブレーション"に設定

                RemoveComponents();
                MotionManager.Instance.ResetVirtualAvatarPose(virtualAvatar);
                ModelInitialize();
            }
        }

        public void SetHandFreeOffset()
        {
            if (vrik == null) return;
            if (leftHandFreeOffsetRotation == null) return;
            if (rightHandFreeOffsetRotation == null) return;
            if (leftHandFreeOffsetPosition == null) return;
            if (rightHandFreeOffsetPosition == null) return;

            // Beat Saber compatible

            leftHandFreeOffsetRotation.localRotation = Quaternion.Euler(
                Settings.Current.LeftHandRotationX,
                -Settings.Current.LeftHandRotationY,
                Settings.Current.LeftHandRotationZ
            );
            leftHandFreeOffsetPosition.localPosition = new Vector3(
                -Settings.Current.LeftHandPositionX,
                Settings.Current.LeftHandPositionY,
                Settings.Current.LeftHandPositionZ
            );

            rightHandFreeOffsetRotation.localRotation = Quaternion.Euler(
                Settings.Current.RightHandRotationX,
                Settings.Current.RightHandRotationY,
                Settings.Current.RightHandRotationZ
            );
            rightHandFreeOffsetPosition.localPosition = new Vector3(
                Settings.Current.RightHandPositionX,
                Settings.Current.RightHandPositionY,
                Settings.Current.RightHandPositionZ
            );

            vrik.solver.leftArm.swivelOffset = Settings.Current.SwivelOffset;
            vrik.solver.rightArm.swivelOffset = -Settings.Current.SwivelOffset;
        }

        #endregion

        public Guid AddOnPostUpdate(int priority, Action action)
        {
            if (OnPostUpdateEvents.ContainsKey(priority) == false) OnPostUpdateEvents.Add(priority, new List<(Guid eventId, Action action)>());
            var eventId = Guid.NewGuid();
            OnPostUpdateEvents[priority].Add((eventId, action));
            return eventId;
        }

        public void RemoveOnPostUpdate(Guid eventId)
        {
            foreach(var list in OnPostUpdateEvents.Values)
            {
                foreach(var value in list)
                {
                    if (value.eventId == eventId)
                    {
                        list.Remove(value);
                        return;
                    }
                }
            }
        }

        private void OnPostUpdate()
        {
            foreach (var list in OnPostUpdateEvents.Values)
            {
                foreach (var value in list)
                {
                    if (value.action != null)
                    {
                        value.action.Invoke();
                    }
                }
            }
        }

        private IEnumerator AfterUpdateCoroutine()
        {
            while (true)
            {
                yield return null;
                // run after Update()

                if (vrik != null) continue;
                //VRIKが無い時に他のモーションソースを動かすために手動で実行する
                OnPostUpdate();
            }
        }

    }
    public enum CalibrationState
    {
        Uncalibrated = 0,
        WaitingForCalibrating = 1,
        Calibrating = 2,
        Calibrated = 3,
    }
}
