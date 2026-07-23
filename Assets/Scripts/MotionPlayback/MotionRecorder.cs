using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniGLTF;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;

namespace VMC
{
    /// <summary>
    /// モデルのモーションを記録してVRMA/BVHに書き出す
    /// Vrm10Instance(DefaultExecutionOrder 11000)の処理後の最終ポーズを記録するため12000
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public class MotionRecorder : MonoBehaviour
    {
        public enum RecordState
        {
            Stopped = 0,
            Countdown = 1,
            Recording = 2,
            Recorded = 3,
        }

        private ControlWPFWindow controlWPFWindow;
        private FaceController faceController;
        private System.Threading.SynchronizationContext context = null;

        private RecordState state = RecordState.Stopped;
        private float countdownRemain = 0f;
        private float recordStartTime = 0f;
        private float recordFps = 30f;

        //記録データ
        private readonly List<float[]> recordedMuscles = new List<float[]>();
        private readonly List<Vector3> recordedBodyPositions = new List<Vector3>();
        private readonly List<Quaternion> recordedBodyRotations = new List<Quaternion>();
        private readonly List<float[]> recordedExpressions = new List<float[]>();
        private readonly List<Vector2> recordedLookAt = new List<Vector2>(); //x:yaw y:pitch
        private List<ExpressionKey> recordedExpressionKeys = new List<ExpressionKey>();

        private GameObject currentModel;
        private Vrm10Instance currentVrm10Instance;
        private HumanPoseHandler modelPoseHandler;
        private Animator modelPoseHandlerAnimator;
        private HumanPose humanPose = new HumanPose();

        //プレビュー用
        private VirtualAvatar virtualAvatar;
        private HumanPoseHandler previewHandler;
        private Animator previewHandlerAnimator;
        private bool previewPlaying = false;
        private float previewTime = 0f;
        private int previewStartFrame = 0;
        private int previewEndFrame = 0;
        private int previewCurrentFrame = 0;

        private float lastStatusSendTime = 0f;
        private const string ExpressionPresetName = "MotionRecorder";

        //VRMAのプリセットとして書き出せる表情(look系はVRMAのExpressionプリセットに存在しない)
        private static readonly HashSet<ExpressionPreset> VrmaSupportedPresets = new HashSet<ExpressionPreset>
        {
            ExpressionPreset.happy, ExpressionPreset.angry, ExpressionPreset.sad, ExpressionPreset.relaxed,
            ExpressionPreset.surprised, ExpressionPreset.aa, ExpressionPreset.ih, ExpressionPreset.ou,
            ExpressionPreset.ee, ExpressionPreset.oh, ExpressionPreset.blink, ExpressionPreset.blinkLeft,
            ExpressionPreset.blinkRight, ExpressionPreset.neutral,
        };

        private void Awake()
        {
            context = System.Threading.SynchronizationContext.Current;
            controlWPFWindow = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
        }

        private void Start()
        {
            faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
            controlWPFWindow.server.ReceivedEvent += Server_Received;

            //VirtualAvatarはモデル変更時に親Transformの子を全て破棄するため専用GameObjectを親にする
            var avatarRoot = new GameObject("AvatarRoot").transform;
            avatarRoot.SetParent(transform, false);
            virtualAvatar = new VirtualAvatar(avatarRoot, MotionSource.MotionPlayback);
            virtualAvatar.Enable = false;
            virtualAvatar.IgnoreDefaultBone = false;
            SetAllApplyFlags(virtualAvatar);
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);
        }

        private void SetAllApplyFlags(VirtualAvatar avatar)
        {
            avatar.ApplyRootPosition = true;
            avatar.ApplyRootRotation = true;
            avatar.ApplySpine = true;
            avatar.ApplyChest = true;
            avatar.ApplyHead = true;
            avatar.ApplyLeftArm = true;
            avatar.ApplyRightArm = true;
            avatar.ApplyLeftHand = true;
            avatar.ApplyRightHand = true;
            avatar.ApplyLeftLeg = true;
            avatar.ApplyRightLeg = true;
            avatar.ApplyLeftFoot = true;
            avatar.ApplyRightFoot = true;
            avatar.ApplyLeftFinger = true;
            avatar.ApplyRightFinger = true;
            avatar.ApplyEye = true;
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.Motion_GetSetting))
                {
                    //ウインドウを開き直した際に記録済み状態を復元できるように現在の状態を通知する
                    SendRecordingStatus();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_SetRecordSetting))
                {
                    var d = (PipeCommands.Motion_SetRecordSetting)e.Data;
                    Settings.Current.MotionRecord_Fps = d.RecordFps;
                    Settings.Current.MotionRecord_CountdownSeconds = d.RecordCountdown;
                    Settings.Current.MotionRecord_SaveMotion = d.RecordMotion;
                    Settings.Current.MotionRecord_SaveExpressionPreset = d.RecordExpressionPreset;
                    Settings.Current.MotionRecord_SaveExpressionCustom = d.RecordExpressionCustom;
                    Settings.Current.MotionRecord_SaveLookAt = d.RecordLookAt;
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_StartRecording))
                {
                    StartRecording();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_StopRecording))
                {
                    StopRecording();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_PreviewSeek))
                {
                    var d = (PipeCommands.Motion_PreviewSeek)e.Data;
                    PreviewSeek(d.Frame);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_PreviewPlay))
                {
                    var d = (PipeCommands.Motion_PreviewPlay)e.Data;
                    PreviewPlay(d.StartFrame, d.EndFrame);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_PreviewPause))
                {
                    previewPlaying = false;
                    SendPreviewStatus();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_PreviewStop))
                {
                    PreviewStop();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_SaveRecording))
                {
                    var d = (PipeCommands.Motion_SaveRecording)e.Data;
                    var ret = new PipeCommands.Motion_ReturnSaveRecording();
                    try
                    {
                        SaveRecording(d.Path, d.Format, d.StartFrame, d.EndFrame);
                        ret.Success = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to save recording: {ex}");
                        ret.Success = false;
                        ret.Error = ex.Message;
                    }
                    await controlWPFWindow.server.SendCommandAsync(ret, e.RequestId);
                }
            }, null);
        }

        private void OnCurrentModelChanged(GameObject model)
        {
            currentModel = model;
            currentVrm10Instance = model != null ? model.GetComponent<Vrm10Instance>() : null;
            modelPoseHandler?.Dispose();
            modelPoseHandler = null;
            modelPoseHandlerAnimator = null;
        }

        private void OnModelUnloading(GameObject model)
        {
            if (state == RecordState.Recording || state == RecordState.Countdown)
            {
                StopRecording();
            }
            PreviewStop();
            currentModel = null;
            currentVrm10Instance = null;
            modelPoseHandler?.Dispose();
            modelPoseHandler = null;
            modelPoseHandlerAnimator = null;
            previewHandler?.Dispose();
            previewHandler = null;
            previewHandlerAnimator = null;
        }

        public void StartRecording()
        {
            if (state == RecordState.Recording || state == RecordState.Countdown) return;
            if (currentModel == null)
            {
                Debug.LogWarning("MotionRecorder: No model loaded");
                return;
            }

            PreviewStop();

            recordFps = Mathf.Clamp(Settings.Current.MotionRecord_Fps, 1, 240);
            countdownRemain = Mathf.Max(0, Settings.Current.MotionRecord_CountdownSeconds);

            recordedMuscles.Clear();
            recordedBodyPositions.Clear();
            recordedBodyRotations.Clear();
            recordedExpressions.Clear();
            recordedLookAt.Clear();
            recordedExpressionKeys.Clear();

            if (countdownRemain > 0f)
            {
                state = RecordState.Countdown;
            }
            else
            {
                BeginCapture();
            }
            SendRecordingStatus();
        }

        private void BeginCapture()
        {
            //記録する表情キー一覧を確定する
            if (currentVrm10Instance != null)
            {
                foreach (var key in currentVrm10Instance.Runtime.Expression.ExpressionKeys)
                {
                    if (key.Preset == ExpressionPreset.custom || VrmaSupportedPresets.Contains(key.Preset))
                    {
                        recordedExpressionKeys.Add(key);
                    }
                }
            }

            recordStartTime = Time.time;
            state = RecordState.Recording;
        }

        public void StopRecording()
        {
            if (state == RecordState.Countdown)
            {
                state = RecordState.Stopped;
            }
            else if (state == RecordState.Recording)
            {
                state = recordedMuscles.Count > 0 ? RecordState.Recorded : RecordState.Stopped;
            }
            SendRecordingStatus();
        }

        private void Update()
        {
            if (state == RecordState.Countdown)
            {
                countdownRemain -= Time.deltaTime;
                if (countdownRemain <= 0f)
                {
                    BeginCapture();
                }
                if (Time.realtimeSinceStartup - lastStatusSendTime > 0.1f)
                {
                    SendRecordingStatus();
                }
            }

            //プレビュー再生
            if (previewPlaying && recordedMuscles.Count > 0)
            {
                previewTime += Time.deltaTime;
                var range = Mathf.Max(1, previewEndFrame - previewStartFrame + 1);
                var frame = previewStartFrame + Mathf.FloorToInt(previewTime * recordFps) % range;
                if (frame != previewCurrentFrame)
                {
                    ApplyPreviewFrame(frame);
                }
                if (Time.realtimeSinceStartup - lastStatusSendTime > 0.1f)
                {
                    SendPreviewStatus();
                }
            }
        }

        private void LateUpdate()
        {
            if (state == RecordState.Recording)
            {
                //フレームレートに合わせて記録する(処理落ち時は同じポーズを複数フレームに記録)
                var expectedFrames = Mathf.FloorToInt((Time.time - recordStartTime) * recordFps) + 1;
                if (recordedMuscles.Count < expectedFrames)
                {
                    CaptureFrame();
                    while (recordedMuscles.Count < expectedFrames)
                    {
                        DuplicateLastFrame();
                    }
                }

                if (Time.realtimeSinceStartup - lastStatusSendTime > 0.1f)
                {
                    SendRecordingStatus();
                }
            }
        }

        private void CaptureFrame()
        {
            if (EnsureModelPoseHandler() == false) return;

            modelPoseHandler.GetHumanPose(ref humanPose);
            recordedMuscles.Add((float[])humanPose.muscles.Clone());
            recordedBodyPositions.Add(humanPose.bodyPosition);
            recordedBodyRotations.Add(humanPose.bodyRotation);

            //表情
            var expressionValues = new float[recordedExpressionKeys.Count];
            if (currentVrm10Instance != null)
            {
                var actualWeights = currentVrm10Instance.Runtime.Expression.ActualWeights;
                for (int i = 0; i < recordedExpressionKeys.Count; i++)
                {
                    if (actualWeights.TryGetValue(recordedExpressionKeys[i], out var weight))
                    {
                        expressionValues[i] = weight;
                    }
                }
            }
            recordedExpressions.Add(expressionValues);

            //視線
            if (currentVrm10Instance != null)
            {
                var lookAt = currentVrm10Instance.Runtime.LookAt;
                recordedLookAt.Add(new Vector2(lookAt.Yaw, lookAt.Pitch));
            }
            else
            {
                recordedLookAt.Add(Vector2.zero);
            }
        }

        private void DuplicateLastFrame()
        {
            recordedMuscles.Add(recordedMuscles[recordedMuscles.Count - 1]);
            recordedBodyPositions.Add(recordedBodyPositions[recordedBodyPositions.Count - 1]);
            recordedBodyRotations.Add(recordedBodyRotations[recordedBodyRotations.Count - 1]);
            recordedExpressions.Add(recordedExpressions[recordedExpressions.Count - 1]);
            recordedLookAt.Add(recordedLookAt[recordedLookAt.Count - 1]);
        }

        private bool EnsureModelPoseHandler()
        {
            if (currentModel == null) return false;
            var animator = currentModel.GetComponent<Animator>();
            if (animator == null || animator.avatar == null) return false;
            if (modelPoseHandler == null || modelPoseHandlerAnimator != animator)
            {
                modelPoseHandler?.Dispose();
                modelPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                modelPoseHandlerAnimator = animator;
            }
            return true;
        }

        #region Preview

        private bool EnsurePreviewHandler()
        {
            if (virtualAvatar?.animator == null || virtualAvatar.animator.avatar == null) return false;
            if (previewHandler == null || previewHandlerAnimator != virtualAvatar.animator)
            {
                previewHandler?.Dispose();
                previewHandler = new HumanPoseHandler(virtualAvatar.animator.avatar, virtualAvatar.animator.transform);
                previewHandlerAnimator = virtualAvatar.animator;
            }
            return true;
        }

        public void PreviewSeek(int frame)
        {
            if (state != RecordState.Recorded) return;
            previewPlaying = false;
            ApplyPreviewFrame(frame);
            SendPreviewStatus();
        }

        public void PreviewPlay(int startFrame, int endFrame)
        {
            if (state != RecordState.Recorded) return;
            previewStartFrame = Mathf.Clamp(startFrame, 0, recordedMuscles.Count - 1);
            previewEndFrame = Mathf.Clamp(endFrame, previewStartFrame, recordedMuscles.Count - 1);
            previewTime = 0f;
            previewPlaying = true;
            virtualAvatar.Enable = true;
            ApplyPreviewFrame(previewStartFrame);
            SendPreviewStatus();
        }

        public void PreviewStop()
        {
            previewPlaying = false;
            if (virtualAvatar != null)
            {
                virtualAvatar.Enable = false;
            }
            faceController?.OverwritePresets(ExpressionPresetName, Array.Empty<ExpressionKey>(), Array.Empty<float>());
            SendPreviewStatus();
        }

        private void ApplyPreviewFrame(int frame)
        {
            if (recordedMuscles.Count == 0) return;
            frame = Mathf.Clamp(frame, 0, recordedMuscles.Count - 1);
            previewCurrentFrame = frame;

            virtualAvatar.Enable = true;
            if (EnsurePreviewHandler())
            {
                humanPose.muscles = recordedMuscles[frame];
                humanPose.bodyPosition = recordedBodyPositions[frame];
                humanPose.bodyRotation = recordedBodyRotations[frame];
                previewHandler.SetHumanPose(ref humanPose);
            }

            if (recordedExpressionKeys.Count > 0 && faceController != null)
            {
                faceController.OverwritePresets(ExpressionPresetName, recordedExpressionKeys.ToArray(), recordedExpressions[frame]);
            }
        }

        #endregion

        #region Save

        private void SaveRecording(string path, int format, int startFrame, int endFrame)
        {
            if (state != RecordState.Recorded || recordedMuscles.Count == 0)
            {
                throw new InvalidOperationException("No recorded motion");
            }
            if (virtualAvatar?.animator == null || virtualAvatar.animator.avatar == null)
            {
                throw new InvalidOperationException("No model loaded");
            }

            startFrame = Mathf.Clamp(startFrame, 0, recordedMuscles.Count - 1);
            endFrame = Mathf.Clamp(endFrame, startFrame, recordedMuscles.Count - 1);

            //プレビューを止めてから書き出し用にスケルトンを使う
            previewPlaying = false;

            try
            {
                if (format == 0)
                {
                    SaveVrma(path, startFrame, endFrame);
                }
                else
                {
                    SaveBvh(path, startFrame, endFrame);
                }
            }
            finally
            {
                //書き出し中にスケルトンを動かしたため、プレビュー表示中だった場合は元のフレームに戻す
                if (virtualAvatar.Enable)
                {
                    ApplyPreviewFrame(previewCurrentFrame);
                }
            }
        }

        private void ApplyFrameToSkeleton(int frame, bool applyMotion)
        {
            if (EnsurePreviewHandler() == false) return;
            if (applyMotion)
            {
                humanPose.muscles = recordedMuscles[frame];
                humanPose.bodyPosition = recordedBodyPositions[frame];
                humanPose.bodyRotation = recordedBodyRotations[frame];
                previewHandler.SetHumanPose(ref humanPose);
            }
            else
            {
                //モーションを保存しない場合はクローンのバインドポーズ(VRMのTポーズ=アバターのレスト基準)にする。
                //マッスルゼロ姿勢はTポーズと異なるため、レストとして使うと再生時に全関節がずれる
                virtualAvatar.RestoreBindPose();
            }
        }

        private void SaveBvh(string path, int startFrame, int endFrame)
        {
            var animator = virtualAvatar.animator;
            //オフセットとレスト回転をアバターのレスト基準(バインドTポーズ)で取得するため、バインドポーズに戻してから階層を構築する
            virtualAvatar.RestoreBindPose();
            var writer = new BvhWriter(animator, animator.transform);
            for (int i = startFrame; i <= endFrame; i++)
            {
                ApplyFrameToSkeleton(i, true);
                writer.AddFrame();
            }
            File.WriteAllText(path, writer.Write(1f / recordFps, 0, endFrame - startFrame));
        }

        private void SaveVrma(string path, int startFrame, int endFrame)
        {
            var animator = virtualAvatar.animator;
            var exportRoot = animator.gameObject;

            var saveMotion = Settings.Current.MotionRecord_SaveMotion;
            var savePreset = Settings.Current.MotionRecord_SaveExpressionPreset;
            var saveCustom = Settings.Current.MotionRecord_SaveExpressionCustom;
            var saveLookAt = Settings.Current.MotionRecord_SaveLookAt;

            //表情・視線用の一時ノードを作成する
            var expressionNodes = new Dictionary<int, Transform>(); //recordedExpressionKeysのindex -> node
            Transform lookAtNode = null;
            var tempNodes = new List<GameObject>();
            try
            {
                //視線ノードは表情ノードより「先」に作る(=glTFノードindexが表情より小さくなる)。
                //公式VrmAnimationImporterは表情ノードをRemoveAtで削除しindexを詰めるが視線チャンネルは補正しないため、
                //視線を表情より後(高index)に置くと削除で視線チャンネルのtarget.nodeがずれて公式実装で壊れる。
                //(表情/視線ノードはexportRoot直下=ルート除外によりトップレベル扱いになりchildrenは汚さない)
                if (saveLookAt)
                {
                    var node = new GameObject("VMC_LookAtTarget");
                    node.transform.SetParent(exportRoot.transform, false);
                    tempNodes.Add(node);
                    lookAtNode = node.transform;
                }

                for (int i = 0; i < recordedExpressionKeys.Count; i++)
                {
                    var key = recordedExpressionKeys[i];
                    var isCustom = key.Preset == ExpressionPreset.custom;
                    if (isCustom && saveCustom == false) continue;
                    if (isCustom == false && savePreset == false) continue;

                    var node = new GameObject($"VMC_Expression_{(isCustom ? key.Name : key.Preset.ToString())}");
                    node.transform.SetParent(exportRoot.transform, false);
                    tempNodes.Add(node);
                    expressionNodes[i] = node.transform;
                }

                //バインドポーズ(VRMのTポーズ=アバターのレスト基準)に戻してからエクスポータを準備する。
                //Prepare時のノードのローカル回転がVRMAのレストとして書き出され、再インポート時のアバターTポーズになるため、
                //VRMのTポーズと一致させる必要がある(マッスルゼロ姿勢だと全関節がずれる)
                virtualAvatar.RestoreBindPose();

                var data = new ExportingGltfData();
                using (var exporter = new VMCVrmAnimationExporter(data, new GltfExportSettings()))
                {
                    exporter.Prepare(exportRoot);
                    exporter.Export(vrma =>
                    {
                        //Humanoidボーンを登録する
                        var map = new Dictionary<HumanBodyBones, Transform>();
                        foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                        {
                            if (bone == HumanBodyBones.LastBone) continue;
                            var t = animator.GetBoneTransform(bone);
                            if (t == null) continue;
                            map.Add(bone, t);
                        }

                        vrma.SetPositionBoneAndParent(map[HumanBodyBones.Hips], exportRoot.transform);

                        foreach (var kv in map)
                        {
                            var vrmBone = Vrm10HumanoidBoneSpecification.ConvertFromUnityBone(kv.Key);
                            var parent = GetParentBone(map, vrmBone) ?? exportRoot.transform;
                            vrma.AddRotationBoneAndParent(kv.Key, kv.Value, parent);
                        }

                        //表情を登録する
                        int currentFrame = startFrame;
                        foreach (var kv in expressionNodes)
                        {
                            var keyIndex = kv.Key;
                            vrma.AddExpression(recordedExpressionKeys[keyIndex], kv.Value, () => recordedExpressions[currentFrame][keyIndex]);
                        }

                        //視線を登録する
                        if (lookAtNode != null)
                        {
                            vrma.SetLookAt(lookAtNode, exportRoot.transform);
                        }

                        //全フレームをサンプリングする
                        var frameTime = TimeSpan.FromSeconds(1.0 / recordFps);
                        var time = default(TimeSpan);
                        for (int i = startFrame; i <= endFrame; i++, time += frameTime)
                        {
                            currentFrame = i;
                            ApplyFrameToSkeleton(i, saveMotion);
                            if (lookAtNode != null)
                            {
                                //VRMA仕様: 視線はノードのローカル回転(Extrinsic ZXY, Y=yaw, X=pitch)で表す。
                                //recordedLookAt=(yaw, pitch)。UnityのQuaternion.Euler(x,y,z)はZ→X→Y適用=Extrinsic ZXYと一致。
                                lookAtNode.localRotation = Quaternion.Euler(recordedLookAt[i].y, recordedLookAt[i].x, 0f);
                            }
                            vrma.AddFrame(time);
                        }
                    });
                }

                File.WriteAllBytes(path, data.ToGlbBytes());
            }
            finally
            {
                foreach (var node in tempNodes)
                {
                    DestroyImmediate(node);
                }
                //スケルトンをバインドポーズに戻す
                virtualAvatar.RestoreBindPose();
            }
        }

        private static Transform GetParentBone(Dictionary<HumanBodyBones, Transform> map, Vrm10HumanoidBones bone)
        {
            while (true)
            {
                if (bone == Vrm10HumanoidBones.Hips)
                {
                    break;
                }
                var parentBone = Vrm10HumanoidBoneSpecification.GetDefine(bone).ParentBone.Value;
                var unityParentBone = Vrm10HumanoidBoneSpecification.ConvertToUnityBone(parentBone);
                if (map.TryGetValue(unityParentBone, out var found))
                {
                    return found;
                }
                bone = parentBone;
            }
            return null;
        }

        #endregion

        private async void SendRecordingStatus()
        {
            lastStatusSendTime = Time.realtimeSinceStartup;
            await controlWPFWindow.server.SendCommandAsync(new PipeCommands.Motion_RecordingStatus
            {
                State = (int)state,
                Time = state == RecordState.Recording ? Time.time - recordStartTime : recordedMuscles.Count / recordFps,
                Countdown = Mathf.Max(0f, countdownRemain),
                FrameCount = recordedMuscles.Count,
                Fps = recordFps,
            });
        }

        private async void SendPreviewStatus()
        {
            lastStatusSendTime = Time.realtimeSinceStartup;
            await controlWPFWindow.server.SendCommandAsync(new PipeCommands.Motion_PreviewStatus
            {
                Frame = previewCurrentFrame,
                Playing = previewPlaying,
            });
        }
    }
}
