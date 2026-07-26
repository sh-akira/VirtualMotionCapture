using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;
using ViveSR.anipal.Eye;

namespace VMC
{
    public class EyeTracking_ViveProEye : MonoBehaviour
    {

        public GameObject MonitorPosition;
        public GameObject LookTarget;
        public Vector3 StartPos;

        public float ScaleX = 2.0f;
        public float ScaleY = 1.5f;
        public float OffsetX = 0.0f;
        public float OffsetY = 0.0f;
        public float CenterX = 0.5f;
        public float CenterY = 0.5f;
        public float Smoothing = 0.7f;
        private Vector3 oldPoint;
        private bool isFirst = true;
        public ControlWPFWindow controlWPFWindow;
        public FaceController faceController;
        private Action faceBeforeApply;
        public bool UseEyelidMovements = true;

        private GameObject currentModel;

        private Dictionary<EyeShape, float> EyeWeightings = new Dictionary<EyeShape, float>();

        // Use this for initialization
        void Awake()
        {
            VMCEvents.OnModelLoaded += ModelLoaded;
            controlWPFWindow.SetEyeTracking_ViveProEyeOffsetsAction += SetEyeTracking_ViveProEyeOffsets;
            controlWPFWindow.SetEyeTracking_ViveProEyeUseEyelidMovementsAction += SetEyeTracking_ViveProEyeUseEyelidMovements;
            controlWPFWindow.EyeTracking_ViveProEyeComponent = this;
            controlWPFWindow.SRanipal_Eye_FrameworkComponent = GetComponent<SRanipal_Eye_Framework>();
            enabled = false;
        }

        private void ModelLoaded(GameObject currentModel)
        {
            ModelInitialize(currentModel);
        }

        private void SetEyeTracking_ViveProEyeOffsets(PipeCommands.SetEyeTracking_ViveProEyeOffsets offsets)
        {
            ScaleX = offsets.ScaleHorizontal;
            ScaleY = offsets.ScaleVertical;
            OffsetX = offsets.OffsetHorizontal;
            OffsetY = offsets.OffsetVertical;
            if (MonitorPosition != null)
            {
                MonitorPosition.transform.localScale = new Vector3(ScaleX, ScaleY, 1);
                MonitorPosition.transform.localPosition = new Vector3(OffsetX, OffsetY, 0);
            }
        }

        private void SetEyeTracking_ViveProEyeUseEyelidMovements(PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements useEyelidMovements)
        {
            UseEyelidMovements = useEyelidMovements.Use;
            if (UseEyelidMovements == false)
            {
                faceController.SetBlink_L(0.0f);
                faceController.SetBlink_R(0.0f);
            }
            faceController.ViveProEyeEnabled = UseEyelidMovements;
        }

        private void ModelInitialize(GameObject currentModel)
        {
            if (currentModel == null) return;
            if (this.currentModel == currentModel) return;
            this.currentModel = currentModel;
            var animator = currentModel.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            //モデルの頭の子に目線向ける先を設定
            if (MonitorPosition == null) MonitorPosition = new GameObject("ViveProEye_MonitorPosition");
            MonitorPosition.transform.parent = head;
            MonitorPosition.transform.localRotation = Quaternion.identity;
            MonitorPosition.transform.localScale = new Vector3(ScaleX, ScaleY, 1);
            MonitorPosition.transform.localPosition = new Vector3(OffsetX, OffsetY, 0);
            if (LookTarget == null) LookTarget = new GameObject("LookTarget");
            LookTarget.transform.parent = MonitorPosition.transform;
            LookTarget.transform.localRotation = Quaternion.identity;
            LookTarget.transform.localPosition = new Vector3(0, 0, 1f); //すべて0地点にすると目が荒ぶる
            var vrm10Instance = currentModel.GetComponent<Vrm10Instance>();
            if (faceBeforeApply != null) faceController.BeforeApply -= faceBeforeApply;
            faceBeforeApply = () =>
            {
                if ((SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING &&
                    SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT) ||
                    SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT || enabled == false) return;
                if (LookTarget == null) return;
                if (vrm10Instance == null) return;
                //アイトラッキングが動作している時だけLookTargetの方向を目線に反映する
                //(LookAtTarget未使用時のみ有効。ボーン/Expressionどちらの目線タイプもRuntimeが処理する)
                var lookAt = vrm10Instance.Runtime.LookAt;
                var (yaw, pitch) = lookAt.CalculateYawPitchFromLookAtPosition(LookTarget.transform.position);
                lookAt.SetYawPitchManually(yaw, pitch);
            };
            faceController.BeforeApply += faceBeforeApply;
            StartPos = LookTarget.transform.localPosition;
            isFirst = true;
        }

        // Update is called once per frame
        void Update()
        {
            if (Camera.main == null) return;

            if ((SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING &&
                SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT) ||
                SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT) return;

            //まぶた
            bool isLeftEyeActive = false;
            bool isRightEyeActive = false;
            float leftEyeOpenness = 1.0f;
            float rightEyeOpenness = 1.0f;
            if (SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.WORKING)
            {
                isLeftEyeActive = SRanipal_Eye.GetEyeOpenness(EyeIndex.LEFT, out leftEyeOpenness);
                isRightEyeActive = SRanipal_Eye.GetEyeOpenness(EyeIndex.RIGHT, out rightEyeOpenness);
            }

            if (isLeftEyeActive || isRightEyeActive)
            {
                EyeWeightings[EyeShape.Eye_Left_Blink] = 1 - leftEyeOpenness;
                EyeWeightings[EyeShape.Eye_Right_Blink] = 1 - rightEyeOpenness;
                UpdateEyeShapes(EyeWeightings);
            }
            else
            {
                for (int i = 0; i < (int)EyeShape.Max; ++i)
                {
                    bool isBlink = ((EyeShape)i == EyeShape.Eye_Left_Blink || (EyeShape)i == EyeShape.Eye_Right_Blink);
                    EyeWeightings[(EyeShape)i] = isBlink ? 1 : 0;
                }

                UpdateEyeShapes(EyeWeightings);

                return;
            }

            //目線
            Vector3 GazeOriginCombinedLocal, GazeDirectionCombinedLocal = Vector3.zero;
            if (SRanipal_Eye.GetGazeRay(GazeIndex.COMBINE, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
            else if (SRanipal_Eye.GetGazeRay(GazeIndex.LEFT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
            else if (SRanipal_Eye.GetGazeRay(GazeIndex.RIGHT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
            if (LookTarget != null) LookTarget.transform.localPosition = Smoothify(GazeDirectionCombinedLocal);

        }
        public void UpdateEyeShapes(Dictionary<EyeShape, float> eyeWeightings)
        {
            if (UseEyelidMovements == false) return;
            if (LookTarget == null) return;
            foreach (var weightings in eyeWeightings)
            {
                EyeShape eyeShape = weightings.Key;

                if (eyeShape == EyeShape.Eye_Left_Blink)
                {
                    faceController.SetBlink_L(weightings.Value);
                }
                else if (eyeShape == EyeShape.Eye_Right_Blink)
                {
                    faceController.SetBlink_R(weightings.Value);
                }
            }
        }

        private Vector3 Smoothify(Vector3 point)
        {
            if (isFirst)
            {
                oldPoint = point;
                isFirst = false;
            }

            var smoothedPoint = new Vector3(
                point.x * (1.0f - Smoothing) + oldPoint.x * Smoothing,
                point.y * (1.0f - Smoothing) + oldPoint.y * Smoothing,
                point.z);

            oldPoint = smoothedPoint;

            return smoothedPoint;
        }

        #region 自動テスト用フック

        /// <summary>
        /// まぶたの開き具合(0=閉じ,1=開き)と視線方向を直接与えて、
        /// 実機と同じ経路でまばたきと目線に反映する。
        /// </summary>
        internal void Test_ApplyEyeState(float leftOpenness, float rightOpenness, Vector3 gazeDirectionLocal)
        {
            EyeWeightings[EyeShape.Eye_Left_Blink] = 1 - leftOpenness;
            EyeWeightings[EyeShape.Eye_Right_Blink] = 1 - rightOpenness;
            UpdateEyeShapes(EyeWeightings);
            if (LookTarget != null) LookTarget.transform.localPosition = Smoothify(gazeDirectionLocal);
        }

        /// <summary>LookTargetの現在位置(スムージング後)</summary>
        internal Vector3 Test_LookTargetLocalPosition
            => LookTarget != null ? LookTarget.transform.localPosition : Vector3.zero;

        #endregion
    }
}