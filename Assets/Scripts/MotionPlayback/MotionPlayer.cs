using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;

namespace VMC
{
    /// <summary>
    /// モーションファイル(VRMA/BVH)の再生
    /// VirtualAvatar(MotionSource.MotionPlayback)経由でモデルに適用するため、
    /// VR機器(VRIK)やVMCProtocolより優先される
    /// </summary>
    public class MotionPlayer : MonoBehaviour
    {
        public enum PlayState
        {
            Stopped = 0,
            Playing = 1,
            Paused = 2,
            PoseHold = 3,
        }

        private ControlWPFWindow controlWPFWindow;
        private FaceController faceController;
        private System.Threading.SynchronizationContext context = null;

        private VirtualAvatar virtualAvatar;
        private Vrm10Instance currentVrm10Instance; //視線(LookAt)適用用

        //遅延読み込み: 起動時はメタ情報(Info)だけ保持し、実体(LoadedMotion)は初回再生時に生成する
        private class MotionEntry
        {
            public string FilePath;
            public UnityMemoryMappedFile.MotionFileInfo Info; //軽量メタ(名前/長さ/FPS/フレーム数)
            public LoadedMotion Loaded;                       //本読み込み後の実体(未読込はnull)
            public bool IsLoading;
        }
        private readonly List<MotionEntry> entries = new List<MotionEntry>();

        private PlayState state = PlayState.Stopped;
        private int currentIndex = -1;
        private float currentTime = 0f;

        private HumanPose humanPose = new HumanPose();
        private HumanPoseHandler cloneHandler;
        private Animator cloneHandlerAnimator; //cloneHandler作成時のAnimator(モデル変更検出用)

        private float lastStatusSendTime = 0f;
        private const string ExpressionPresetName = "MotionPlayer";

        //設定ファイル未読み込み時はOnDeserializingが走らずnullのため、ここで初期化する
        private List<string> MotionFilePaths => Settings.Current.MotionPlayback_MotionFiles ?? (Settings.Current.MotionPlayback_MotionFiles = new List<string>());

        private void Awake()
        {
            context = System.Threading.SynchronizationContext.Current;
            controlWPFWindow = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
            controlWPFWindow.AdditionalSettingAction += ApplySettings;
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
        }

        private void Start()
        {
            faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
            controlWPFWindow.server.ReceivedEvent += Server_Received;

            //VirtualAvatarはモデル変更時に親Transformの子を全て破棄するため、
            //モーションのスケルトンとは別の専用GameObjectを親にする
            var avatarRoot = new GameObject("AvatarRoot").transform;
            avatarRoot.SetParent(transform, false);
            virtualAvatar = new VirtualAvatar(avatarRoot, MotionSource.MotionPlayback);
            virtualAvatar.Enable = false;
            virtualAvatar.IgnoreDefaultBone = false;
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);
            SetVirtualAvatarSetting();
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.Motion_GetSetting))
                {
                    await controlWPFWindow.server.SendCommandAsync(CreateSettingCommand(), e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_SetSetting))
                {
                    var d = (PipeCommands.Motion_SetSetting)e.Data;
                    SetSetting(d);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_LoadFile))
                {
                    var d = (PipeCommands.Motion_LoadFile)e.Data;
                    var ret = new PipeCommands.Motion_ReturnLoadFile();
                    try
                    {
                        var entry = await LoadMotionAsync(d.Path);
                        ret.Success = true;
                        ret.Info = entry.Info;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to load motion: {d.Path}\n{ex}");
                        ret.Success = false;
                        ret.Error = ex.Message;
                    }
                    await controlWPFWindow.server.SendCommandAsync(ret, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_RemoveFile))
                {
                    var d = (PipeCommands.Motion_RemoveFile)e.Data;
                    RemoveMotion(d.Index);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_GetFileList))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.Motion_ReturnFileList
                    {
                        Files = entries.Select(m => m.Info).ToList()
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_Play))
                {
                    var d = (PipeCommands.Motion_Play)e.Data;
                    Play(d.Index);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_Pause))
                {
                    Pause();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_Stop))
                {
                    Stop();
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_Seek))
                {
                    var d = (PipeCommands.Motion_Seek)e.Data;
                    Seek(d.Seconds);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_FrameStep))
                {
                    var d = (PipeCommands.Motion_FrameStep)e.Data;
                    FrameStep(d.Delta);
                }
                else if (e.CommandType == typeof(PipeCommands.Motion_SetRepeatMode))
                {
                    var d = (PipeCommands.Motion_SetRepeatMode)e.Data;
                    Settings.Current.MotionPlayback_RepeatMode = d.RepeatMode;
                }
            }, null);
        }

        private PipeCommands.Motion_SetSetting CreateSettingCommand()
        {
            return new PipeCommands.Motion_SetSetting
            {
                MotionFiles = new List<string>(MotionFilePaths),
                RepeatMode = Settings.Current.MotionPlayback_RepeatMode,
                ApplyRootPosition = Settings.Current.MotionPlayback_ApplyRootPosition,
                ApplyRootRotation = Settings.Current.MotionPlayback_ApplyRootRotation,
                ApplySpine = Settings.Current.MotionPlayback_ApplySpine,
                ApplyChest = Settings.Current.MotionPlayback_ApplyChest,
                ApplyHead = Settings.Current.MotionPlayback_ApplyHead,
                ApplyLeftArm = Settings.Current.MotionPlayback_ApplyLeftArm,
                ApplyRightArm = Settings.Current.MotionPlayback_ApplyRightArm,
                ApplyLeftHand = Settings.Current.MotionPlayback_ApplyLeftHand,
                ApplyRightHand = Settings.Current.MotionPlayback_ApplyRightHand,
                ApplyLeftLeg = Settings.Current.MotionPlayback_ApplyLeftLeg,
                ApplyRightLeg = Settings.Current.MotionPlayback_ApplyRightLeg,
                ApplyLeftFoot = Settings.Current.MotionPlayback_ApplyLeftFoot,
                ApplyRightFoot = Settings.Current.MotionPlayback_ApplyRightFoot,
                ApplyLeftFinger = Settings.Current.MotionPlayback_ApplyLeftFinger,
                ApplyRightFinger = Settings.Current.MotionPlayback_ApplyRightFinger,
                ApplyEye = Settings.Current.MotionPlayback_ApplyEye,
                ApplyExpression = Settings.Current.MotionPlayback_ApplyExpression,
                ApplyLookAt = Settings.Current.MotionPlayback_ApplyLookAt,
                RecordFps = Settings.Current.MotionRecord_Fps,
                RecordCountdown = Settings.Current.MotionRecord_CountdownSeconds,
                RecordMotion = Settings.Current.MotionRecord_SaveMotion,
                RecordExpressionPreset = Settings.Current.MotionRecord_SaveExpressionPreset,
                RecordExpressionCustom = Settings.Current.MotionRecord_SaveExpressionCustom,
                RecordLookAt = Settings.Current.MotionRecord_SaveLookAt,
            };
        }

        private void SetSetting(PipeCommands.Motion_SetSetting setting)
        {
            Settings.Current.MotionPlayback_RepeatMode = setting.RepeatMode;
            Settings.Current.MotionPlayback_ApplyRootPosition = setting.ApplyRootPosition;
            Settings.Current.MotionPlayback_ApplyRootRotation = setting.ApplyRootRotation;
            Settings.Current.MotionPlayback_ApplySpine = setting.ApplySpine;
            Settings.Current.MotionPlayback_ApplyChest = setting.ApplyChest;
            Settings.Current.MotionPlayback_ApplyHead = setting.ApplyHead;
            Settings.Current.MotionPlayback_ApplyLeftArm = setting.ApplyLeftArm;
            Settings.Current.MotionPlayback_ApplyRightArm = setting.ApplyRightArm;
            Settings.Current.MotionPlayback_ApplyLeftHand = setting.ApplyLeftHand;
            Settings.Current.MotionPlayback_ApplyRightHand = setting.ApplyRightHand;
            Settings.Current.MotionPlayback_ApplyLeftLeg = setting.ApplyLeftLeg;
            Settings.Current.MotionPlayback_ApplyRightLeg = setting.ApplyRightLeg;
            Settings.Current.MotionPlayback_ApplyLeftFoot = setting.ApplyLeftFoot;
            Settings.Current.MotionPlayback_ApplyRightFoot = setting.ApplyRightFoot;
            Settings.Current.MotionPlayback_ApplyLeftFinger = setting.ApplyLeftFinger;
            Settings.Current.MotionPlayback_ApplyRightFinger = setting.ApplyRightFinger;
            Settings.Current.MotionPlayback_ApplyEye = setting.ApplyEye;
            Settings.Current.MotionPlayback_ApplyExpression = setting.ApplyExpression;
            Settings.Current.MotionPlayback_ApplyLookAt = setting.ApplyLookAt;
            //記録設定はMotion_SetRecordSetting(MotionRecorder)で更新するためここでは適用しない
            //(再生・記録の両ウインドウを同時に開いた際に古い値で上書きされるのを防ぐ)

            SetVirtualAvatarSetting();
        }

        private void SetVirtualAvatarSetting()
        {
            if (virtualAvatar == null) return;
            virtualAvatar.ApplyRootPosition = Settings.Current.MotionPlayback_ApplyRootPosition;
            virtualAvatar.ApplyRootRotation = Settings.Current.MotionPlayback_ApplyRootRotation;
            virtualAvatar.ApplySpine = Settings.Current.MotionPlayback_ApplySpine;
            virtualAvatar.ApplyChest = Settings.Current.MotionPlayback_ApplyChest;
            virtualAvatar.ApplyHead = Settings.Current.MotionPlayback_ApplyHead;
            virtualAvatar.ApplyLeftArm = Settings.Current.MotionPlayback_ApplyLeftArm;
            virtualAvatar.ApplyRightArm = Settings.Current.MotionPlayback_ApplyRightArm;
            virtualAvatar.ApplyLeftHand = Settings.Current.MotionPlayback_ApplyLeftHand;
            virtualAvatar.ApplyRightHand = Settings.Current.MotionPlayback_ApplyRightHand;
            virtualAvatar.ApplyLeftLeg = Settings.Current.MotionPlayback_ApplyLeftLeg;
            virtualAvatar.ApplyRightLeg = Settings.Current.MotionPlayback_ApplyRightLeg;
            virtualAvatar.ApplyLeftFoot = Settings.Current.MotionPlayback_ApplyLeftFoot;
            virtualAvatar.ApplyRightFoot = Settings.Current.MotionPlayback_ApplyRightFoot;
            virtualAvatar.ApplyLeftFinger = Settings.Current.MotionPlayback_ApplyLeftFinger;
            virtualAvatar.ApplyRightFinger = Settings.Current.MotionPlayback_ApplyRightFinger;
            virtualAvatar.ApplyEye = Settings.Current.MotionPlayback_ApplyEye;
        }

        private async void ApplySettings(GameObject gameObject)
        {
            SetVirtualAvatarSetting();

            //設定ファイルに保存されたモーションファイルを登録する(メタ情報のみ。実体は初回再生時に読み込む)
            var files = Settings.Current.MotionPlayback_MotionFiles;
            if (files == null) return;
            var pathsToLoad = files.Where(p => entries.Any(m => m.FilePath == p) == false).ToList();
            foreach (var path in pathsToLoad)
            {
                try
                {
                    await LoadMotionAsync(path);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load motion: {path}\n{ex}");
                }
            }
        }

        /// <summary>
        /// モーションファイルを一覧に登録する(メタ情報のみ読み取り、実体はまだ生成しない)
        /// </summary>
        private async Task<MotionEntry> LoadMotionAsync(string path)
        {
            var exist = entries.FirstOrDefault(m => m.FilePath == path);
            if (exist != null) return exist;

            //メタ情報の読み取りはファイルパースを伴うため別スレッドで実行
            var info = await Task.Run(() => LoadedMotion.ReadInfo(path));

            exist = entries.FirstOrDefault(m => m.FilePath == path);
            if (exist != null) return exist;

            var entry = new MotionEntry { FilePath = path, Info = info };
            entries.Add(entry);
            if (MotionFilePaths.Contains(path) == false)
            {
                MotionFilePaths.Add(path);
            }
            return entry;
        }

        /// <summary>
        /// 実体(LoadedMotion)を必要になった時点で生成する(遅延読み込み)
        /// </summary>
        private async Task<LoadedMotion> EnsureLoadedAsync(MotionEntry entry)
        {
            if (entry.Loaded != null) return entry.Loaded;
            if (entry.IsLoading) return null;
            entry.IsLoading = true;
            try
            {
                var motion = await LoadedMotion.LoadAsync(entry.FilePath);
                motion.Root.transform.SetParent(transform, false);
                entry.Loaded = motion;
                entry.Info = motion.ToInfo(); //実体から得た正確なメタで更新
                return motion;
            }
            finally
            {
                entry.IsLoading = false;
            }
        }

        private void RemoveMotion(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            if (currentIndex == index)
            {
                Stop();
                currentIndex = -1;
            }
            var entry = entries[index];
            MotionFilePaths.Remove(entry.FilePath);
            entries.RemoveAt(index);
            entry.Loaded?.Dispose();
            if (currentIndex > index) currentIndex--;
        }

        public void Play(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            if (state == PlayState.Paused && index == currentIndex)
            {
                //一時停止からの再開
                state = PlayState.Playing;
            }
            else
            {
                currentIndex = index;
                currentTime = 0f;
                state = PlayState.Playing;
            }
            virtualAvatar.Enable = true;
            ApplyCurrentFrame();
            SendStatus();
        }

        public void PlayByPath(string path)
        {
            var index = entries.FindIndex(m => m.FilePath == path);
            if (index < 0) return;
            Play(index);
        }

        public void Pause()
        {
            if (state != PlayState.Playing) return;
            state = PlayState.Paused;
            SendStatus();
        }

        public void Stop()
        {
            if (state == PlayState.Stopped) return;
            state = PlayState.Stopped;
            currentTime = 0f;
            virtualAvatar.Enable = false;
            ClearExpressions();
            SendStatus();
        }

        public void Seek(float seconds)
        {
            if (currentIndex < 0 || currentIndex >= entries.Count) return;
            currentTime = Mathf.Clamp(seconds, 0f, entries[currentIndex].Info.Length);
            if (state == PlayState.Stopped)
            {
                state = PlayState.Paused;
                virtualAvatar.Enable = true;
            }
            ApplyCurrentFrame();
            SendStatus();
        }

        /// <summary>
        /// 1フレーム進める/戻す(ファイルのフレームレートに従う)
        /// </summary>
        public void FrameStep(int delta)
        {
            if (currentIndex < 0 || currentIndex >= entries.Count)
            {
                if (entries.Count == 0) return;
                currentIndex = 0;
            }
            var info = entries[currentIndex].Info;
            if (state == PlayState.Playing)
            {
                state = PlayState.Paused;
            }
            if (state == PlayState.Stopped)
            {
                state = PlayState.Paused;
                virtualAvatar.Enable = true;
            }
            var fps = info.FrameRate > 0 ? info.FrameRate : 30f;
            currentTime = Mathf.Clamp(currentTime + delta / fps, 0f, info.Length);
            ApplyCurrentFrame();
            SendStatus();
        }

        /// <summary>
        /// モーションの1フレームを抜き出したポーズを適用する(ショートカットキー用)
        /// </summary>
        public async void ApplyPoseByPath(string path, int frame)
        {
            try
            {
                await ApplyPoseByPathAsync(path, frame);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to apply pose: {path}\n{ex}");
            }
        }

        /// <summary>
        /// ApplyPoseByPathの待機可能版(完了を待ちたい呼び出し元向け)
        /// </summary>
        public async Task ApplyPoseByPathAsync(string path, int frame)
        {
            var entry = await LoadMotionAsync(path);
            currentIndex = entries.IndexOf(entry);
            var fps = entry.Info.FrameRate > 0 ? entry.Info.FrameRate : 30f;
            currentTime = Mathf.Clamp(frame / fps, 0f, entry.Info.Length);
            state = PlayState.PoseHold;
            virtualAvatar.Enable = true;
            //実体を読み込んでから適用する
            await EnsureLoadedAsync(entry);
            ApplyCurrentFrame();
            SendStatus();
        }

        private void OnCurrentModelChanged(GameObject model)
        {
            currentVrm10Instance = model != null ? model.GetComponent<Vrm10Instance>() : null;
        }

        private void OnModelUnloading(GameObject model)
        {
            currentVrm10Instance = null;
            cloneHandler?.Dispose();
            cloneHandler = null;
            cloneHandlerAnimator = null;
            if (state != PlayState.Stopped)
            {
                state = PlayState.Stopped;
                virtualAvatar.Enable = false;
                ClearExpressions();
            }
        }

        private void Update()
        {
            if (state == PlayState.Playing)
            {
                if (currentIndex < 0 || currentIndex >= entries.Count)
                {
                    Stop();
                    return;
                }
                var length = entries[currentIndex].Info.Length;
                currentTime += Time.deltaTime;
                if (currentTime >= length)
                {
                    switch (Settings.Current.MotionPlayback_RepeatMode)
                    {
                        case 1: //1ファイルループ
                            currentTime = length > 0f ? currentTime % length : 0f;
                            break;
                        case 2: //リストのループ再生
                            currentIndex = (currentIndex + 1) % entries.Count;
                            currentTime = 0f;
                            break;
                        default: //1ショット
                            Stop();
                            return;
                    }
                }
                ApplyCurrentFrame();

                if (Time.realtimeSinceStartup - lastStatusSendTime > 0.1f)
                {
                    SendStatus();
                }
            }
        }

        private void ApplyCurrentFrame()
        {
            if (currentIndex < 0 || currentIndex >= entries.Count) return;
            var entry = entries[currentIndex];

            //遅延読み込み: 実体が未生成なら読み込みを開始し、このフレームはスキップ(読込完了後のフレームから適用)
            if (entry.Loaded == null)
            {
                _ = EnsureLoadedAsync(entry);
                return;
            }
            var motion = entry.Loaded;

            motion.Sample(currentTime);

            //ポーズをVirtualAvatarのクローンスケルトンへ転写する(HumanPose経由でリターゲット)
            if (EnsureCloneHandler())
            {
                motion.GetHumanPose(ref humanPose);
                cloneHandler.SetHumanPose(ref humanPose);
            }

            //表情(VRMAのみ)
            //適用オフの場合はモーションのみ再生し、表情はVMCProtocol受信等の他の入力に任せる
            if (faceController != null)
            {
                var weights = (motion.IsVrma && Settings.Current.MotionPlayback_ApplyExpression)
                    ? motion.GetExpressionWeights().ToArray()
                    : Array.Empty<KeyValuePair<ExpressionKey, float>>();
                if (weights.Length > 0)
                {
                    faceController.OverwritePresets(ExpressionPresetName, weights.Select(kv => kv.Key).ToArray(), weights.Select(kv => kv.Value).ToArray());
                }
                else
                {
                    //リストループで表情なしのモーションに切り替わった際等に前の表情が残らないようにする
                    ClearExpressions();
                }
            }

            //視線(VRMAに視線情報がある場合のみ / 適用オフならVMCProtocol受信等の他の入力に任せる)
            //SetYawPitchManuallyはLookAtTargetType!=SpecifiedTransformのときのみ有効(アイトラッキングと同じ挙動)
            if (currentVrm10Instance != null && motion.IsVrma && Settings.Current.MotionPlayback_ApplyLookAt
                && motion.HasLookAt && motion.TryGetLookAtYawPitch(out var yaw, out var pitch))
            {
                try
                {
                    currentVrm10Instance.Runtime.LookAt.SetYawPitchManually(yaw, pitch);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to apply lookat: {ex.Message}");
                }
            }
        }

        private bool EnsureCloneHandler()
        {
            if (virtualAvatar?.animator == null || virtualAvatar.animator.avatar == null) return false;
            if (cloneHandler == null || cloneHandlerAnimator != virtualAvatar.animator)
            {
                cloneHandler?.Dispose();
                cloneHandler = new HumanPoseHandler(virtualAvatar.animator.avatar, virtualAvatar.animator.transform);
                cloneHandlerAnimator = virtualAvatar.animator;
            }
            return true;
        }

        private void ClearExpressions()
        {
            faceController?.OverwritePresets(ExpressionPresetName, Array.Empty<ExpressionKey>(), Array.Empty<float>());
        }

        private async void SendStatus()
        {
            lastStatusSendTime = Time.realtimeSinceStartup;
            var length = (currentIndex >= 0 && currentIndex < entries.Count) ? entries[currentIndex].Info.Length : 0f;
            await controlWPFWindow.server.SendCommandAsync(new PipeCommands.Motion_PlaybackStatus
            {
                Index = currentIndex,
                Time = currentTime,
                Length = length,
                State = (int)state,
            });
        }

        #region 自動テスト用フック

        /// <summary>登録済みモーションのフレーム数</summary>
        internal int Test_GetFrameCount(string path)
        {
            var entry = entries.FirstOrDefault(m => m.FilePath == path);
            return entry != null ? entry.Info.FrameCount : 0;
        }

        #endregion
    }
}
