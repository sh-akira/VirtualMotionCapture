using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;
using VMC.Plugin.Commands;
using ViveSR.anipal.Eye;
using VMC.Plugin;

// namespace に ViveSR を含めると SDK の ViveSR.anipal.* と衝突するため Vive にしている
namespace VMC.Plugin.Vive
{
    /// <summary>
    /// VIVE Pro Eye / Focus 3 / Droolon F1 のアイトラッキング。
    /// 元は本体の EyeTracking_ViveProEye.cs。
    /// </summary>
    public class ViveProEyePlugin : MonoBehaviour, IVMCPlugin
    {
        public string Id => "ViveSR.Eye";
        public string DisplayName => "VIVE Pro Eye";
        public string Version => "1.0.0";
        public System.Collections.Generic.IEnumerable<System.Type> CommandTypes => ViveSRCommands.Types;

        private IPluginHost host;
        private IPluginSettings settings;
        private SRanipal_Eye_Framework framework;

        private GameObject monitorPosition;
        private GameObject lookTarget;

        private float scaleX = 2.0f;
        private float scaleY = 1.5f;
        private float offsetX = 0.0f;
        private float offsetY = 0.0f;
        private float smoothing = 0.7f;

        private Vector3 oldPoint;
        private bool isFirst = true;
        private bool useEyelidMovements = true;
        private bool isEnabled = false;

        private GameObject currentModel;
        private Action faceBeforeApply;

        private readonly Dictionary<EyeShape, float> eyeWeightings = new Dictionary<EyeShape, float>();

        public void Initialize(IPluginHost host)
        {
            this.host = host;
            settings = host.GetSettings("ViveSR");

            //SRanipalのフレームワークはこのGameObjectに載せる。
            //有効化するまでデバイスへ接続しに行かないよう enabled=false で始める
            framework = gameObject.AddComponent<SRanipal_Eye_Framework>();
            framework.enabled = false;

            VMCEvents.OnModelLoaded += OnModelLoaded;
            host.Ipc.Received += OnReceived;
            host.SettingsApplied += ApplySettings;
        }

        private void OnDestroy()
        {
            VMCEvents.OnModelLoaded -= OnModelLoaded;
            if (host != null)
            {
                host.Ipc.Received -= OnReceived;
                host.SettingsApplied -= ApplySettings;
                if (faceBeforeApply != null) host.FaceControl.BeforeApply -= faceBeforeApply;
            }
        }

        #region 設定

        private void OnReceived(object sender, DataReceivedEventArgs e)
        {
            host.Ipc.Post(async () =>
            {
                if (e.CommandType == typeof(GetEyeTracking_ViveProEyeOffsets))
                {
                    await host.Ipc.SendCommandAsync(new SetEyeTracking_ViveProEyeOffsets
                    {
                        OffsetHorizontal = offsetX,
                        OffsetVertical = offsetY,
                        ScaleHorizontal = scaleX,
                        ScaleVertical = scaleY,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(SetEyeTracking_ViveProEyeOffsets))
                {
                    var d = (SetEyeTracking_ViveProEyeOffsets)e.Data;
                    settings.Set("EyeOffsetHorizontal", d.OffsetHorizontal);
                    settings.Set("EyeOffsetVertical", d.OffsetVertical);
                    settings.Set("EyeScaleHorizontal", d.ScaleHorizontal);
                    settings.Set("EyeScaleVertical", d.ScaleVertical);
                    ApplyOffsets();
                }
                else if (e.CommandType == typeof(GetEyeTracking_ViveProEyeUseEyelidMovements))
                {
                    await host.Ipc.SendCommandAsync(new SetEyeTracking_ViveProEyeUseEyelidMovements
                    {
                        Use = useEyelidMovements,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(SetEyeTracking_ViveProEyeUseEyelidMovements))
                {
                    var d = (SetEyeTracking_ViveProEyeUseEyelidMovements)e.Data;
                    settings.Set("UseEyelidMovements", d.Use);
                    ApplyUseEyelidMovements();
                }
                else if (e.CommandType == typeof(GetEyeTracking_ViveProEyeEnable))
                {
                    await host.Ipc.SendCommandAsync(new SetEyeTracking_ViveProEyeEnable
                    {
                        enable = isEnabled,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(SetEyeTracking_ViveProEyeEnable))
                {
                    var d = (SetEyeTracking_ViveProEyeEnable)e.Data;
                    settings.Set("EyeEnable", d.enable);
                    ApplyEnable();
                }
            });
        }

        private void ApplySettings()
        {
            ApplyOffsets();
            ApplyUseEyelidMovements();
            ApplyEnable();
        }

        private void ApplyOffsets()
        {
            scaleX = settings.Get("EyeScaleHorizontal", 2.0f);
            scaleY = settings.Get("EyeScaleVertical", 1.5f);
            offsetX = settings.Get("EyeOffsetHorizontal", 0.0f);
            offsetY = settings.Get("EyeOffsetVertical", 0.0f);

            if (monitorPosition != null)
            {
                monitorPosition.transform.localScale = new Vector3(scaleX, scaleY, 1);
                monitorPosition.transform.localPosition = new Vector3(offsetX, offsetY, 0);
            }
        }

        private void ApplyUseEyelidMovements()
        {
            useEyelidMovements = settings.Get("UseEyelidMovements", false);
            if (useEyelidMovements == false)
            {
                host.FaceControl.SetBlink_L(0.0f);
                host.FaceControl.SetBlink_R(0.0f);
            }
            host.FaceControl.ExternalEyelidControlEnabled = useEyelidMovements;
        }

        private void ApplyEnable()
        {
            isEnabled = settings.Get("EyeEnable", false);
            if (framework != null) framework.enabled = isEnabled;
        }

        #endregion

        private void OnModelLoaded(GameObject model)
        {
            if (model == null) return;
            if (currentModel == model) return;
            currentModel = model;

            var animator = model.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);

            //モデルの頭の子に目線を向ける先を作る
            if (monitorPosition == null) monitorPosition = new GameObject("ViveProEye_MonitorPosition");
            monitorPosition.transform.parent = head;
            monitorPosition.transform.localRotation = Quaternion.identity;
            monitorPosition.transform.localScale = new Vector3(scaleX, scaleY, 1);
            monitorPosition.transform.localPosition = new Vector3(offsetX, offsetY, 0);

            if (lookTarget == null) lookTarget = new GameObject("LookTarget");
            lookTarget.transform.parent = monitorPosition.transform;
            lookTarget.transform.localRotation = Quaternion.identity;
            lookTarget.transform.localPosition = new Vector3(0, 0, 1f); //すべて0地点にすると目が荒ぶる

            if (faceBeforeApply != null) host.FaceControl.BeforeApply -= faceBeforeApply;
            faceBeforeApply = () =>
            {
                if (IsWorking() == false || isEnabled == false) return;
                if (lookTarget == null) return;
                //アイトラッキングが動作している時だけLookTargetの方向を目線に反映する
                host.FaceControl.SetLookAtPosition(lookTarget.transform.position);
            };
            host.FaceControl.BeforeApply += faceBeforeApply;

            isFirst = true;
        }

        private static bool IsWorking()
        {
            return SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.WORKING;
        }

        private void Update()
        {
            if (isEnabled == false) return;
            if (IsWorking() == false) return;

            //まぶた
            var leftEyeOpenness = 1.0f;
            var rightEyeOpenness = 1.0f;
            var isLeftEyeActive = SRanipal_Eye.GetEyeOpenness(EyeIndex.LEFT, out leftEyeOpenness);
            var isRightEyeActive = SRanipal_Eye.GetEyeOpenness(EyeIndex.RIGHT, out rightEyeOpenness);

            if (isLeftEyeActive || isRightEyeActive)
            {
                eyeWeightings[EyeShape.Eye_Left_Blink] = 1 - leftEyeOpenness;
                eyeWeightings[EyeShape.Eye_Right_Blink] = 1 - rightEyeOpenness;
                UpdateEyeShapes(eyeWeightings);
            }
            else
            {
                //目の情報が取れない間は閉じた状態にしておく
                for (int i = 0; i < (int)EyeShape.Max; ++i)
                {
                    var isBlink = (EyeShape)i == EyeShape.Eye_Left_Blink || (EyeShape)i == EyeShape.Eye_Right_Blink;
                    eyeWeightings[(EyeShape)i] = isBlink ? 1 : 0;
                }
                UpdateEyeShapes(eyeWeightings);
                return;
            }

            //目線
            Vector3 gazeOrigin, gazeDirection = Vector3.zero;
            if (SRanipal_Eye.GetGazeRay(GazeIndex.COMBINE, out gazeOrigin, out gazeDirection)) { }
            else if (SRanipal_Eye.GetGazeRay(GazeIndex.LEFT, out gazeOrigin, out gazeDirection)) { }
            else if (SRanipal_Eye.GetGazeRay(GazeIndex.RIGHT, out gazeOrigin, out gazeDirection)) { }
            if (lookTarget != null) lookTarget.transform.localPosition = Smoothify(gazeDirection);
        }

        private void UpdateEyeShapes(Dictionary<EyeShape, float> weightings)
        {
            if (useEyelidMovements == false) return;
            if (lookTarget == null) return;

            foreach (var weighting in weightings)
            {
                if (weighting.Key == EyeShape.Eye_Left_Blink)
                {
                    host.FaceControl.SetBlink_L(weighting.Value);
                }
                else if (weighting.Key == EyeShape.Eye_Right_Blink)
                {
                    host.FaceControl.SetBlink_R(weighting.Value);
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
                point.x * (1.0f - smoothing) + oldPoint.x * smoothing,
                point.y * (1.0f - smoothing) + oldPoint.y * smoothing,
                point.z);

            oldPoint = smoothedPoint;
            return smoothedPoint;
        }
    }
}
