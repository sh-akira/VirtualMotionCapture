using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using UniVRM10;

namespace VMC
{
    /// <summary>
    /// UniVRMで読み込んだモーションファイル(VRMA/BVH)のラッパー
    /// legacy AnimationClipを手動サンプリングして任意時刻のポーズを取り出す
    /// </summary>
    public class LoadedMotion : IDisposable
    {
        public string FilePath { get; private set; }
        public string Name { get; private set; }
        public float Length { get; private set; }
        public float FrameRate { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsVrma { get; private set; }

        public GameObject Root { get; private set; }
        public Animator Animator { get; private set; }
        public Vrm10AnimationInstance VrmaInstance { get; private set; } //BVHの場合null

        private Animation animation;
        private AnimationState animationState;
        private HumanPoseHandler poseHandler;

        //表情・視線はUniVRMのVrmAnimationImporterがVrm10AnimationInstanceのfieldへSetCurveする方式のため、
        //Animation.Sample()では値が駆動されず(かつエディタ警告が出る)。glTFのVRMC_vrm_animationから直接カーブを読み、
        //自前で任意時刻を評価する(該当チャンネルはインポート前にglTFから除去して警告と二重処理を防ぐ)。
        private readonly Dictionary<ExpressionKey, AnimationCurve> expressionCurves = new Dictionary<ExpressionKey, AnimationCurve>();
        //VRMA仕様: 視線はlookAtノードのローカル「回転(quaternion)」で表す(Extrinsic ZXY, Y=yaw, X=pitch)
        private AnimationCurve lookAtRotX, lookAtRotY, lookAtRotZ, lookAtRotW;
        private bool hasLookAt;
        private float currentTime;

        public bool HasLookAt => hasLookAt;

        public UnityMemoryMappedFile.MotionFileInfo ToInfo()
        {
            return new UnityMemoryMappedFile.MotionFileInfo
            {
                FilePath = FilePath,
                Name = Name,
                Length = Length,
                FrameRate = FrameRate,
                FrameCount = FrameCount,
                IsVrma = IsVrma,
            };
        }

        public static async Task<LoadedMotion> LoadAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || File.Exists(path) == false)
            {
                throw new FileNotFoundException(path);
            }

            var motion = new LoadedMotion();
            motion.FilePath = path;
            motion.Name = Path.GetFileNameWithoutExtension(path);

            try
            {
                if (Path.GetExtension(path).ToLower() == ".bvh")
                {
                    motion.LoadBvh(path);
                }
                else
                {
                    await motion.LoadVrmaAsync(path);
                }

                //マニュアルサンプリングの準備
                motion.animation = motion.Root.GetComponent<Animation>();
                if (motion.animation == null)
                {
                    throw new InvalidDataException("No animation found in file");
                }
                motion.animation.playAutomatically = false;
                motion.animation.Stop();
                foreach (AnimationState state in motion.animation)
                {
                    motion.animationState = state;
                    break;
                }
                if (motion.animationState == null)
                {
                    throw new InvalidDataException("No animation found in file");
                }
                motion.animationState.wrapMode = WrapMode.ClampForever;
                motion.Length = motion.animationState.clip.length;

                if (motion.FrameRate <= 0f) motion.FrameRate = 30f;
                if (motion.FrameCount <= 0) motion.FrameCount = Mathf.Max(1, Mathf.RoundToInt(motion.Length * motion.FrameRate) + 1);

                motion.Animator = motion.Root.GetComponent<Animator>();
                if (motion.Animator == null || motion.Animator.avatar == null || motion.Animator.avatar.isValid == false)
                {
                    throw new InvalidDataException("No humanoid avatar found in file");
                }
                motion.poseHandler = new HumanPoseHandler(motion.Animator.avatar, motion.Animator.transform);

                motion.Sample(0f);
            }
            catch
            {
                //読み込み途中で生成したGameObjectが残らないように破棄する
                motion.Dispose();
                throw;
            }

            return motion;
        }

        /// <summary>
        /// 実体(Vrm10AnimationInstance等)を生成せず、軽量にメタ情報だけ読み取る。
        /// 起動時の一覧表示用(遅延読み込み)。VRMAの本読み込みで出るエディタ警告も回避できる。
        /// </summary>
        public static UnityMemoryMappedFile.MotionFileInfo ReadInfo(string path)
        {
            if (string.IsNullOrEmpty(path) || File.Exists(path) == false)
            {
                throw new FileNotFoundException(path);
            }

            var info = new UnityMemoryMappedFile.MotionFileInfo
            {
                FilePath = path,
                Name = Path.GetFileNameWithoutExtension(path),
                FrameRate = 30f,
            };

            if (Path.GetExtension(path).ToLower() == ".bvh")
            {
                var context = new UniHumanoid.BvhImporterContext();
                context.Parse(path, File.ReadAllText(path)); //Load()はしない(階層生成なし=軽量)
                info.IsVrma = false;
                var frameSec = context.Bvh.FrameTime.TotalSeconds;
                info.FrameCount = context.Bvh.FrameCount;
                if (frameSec > 0)
                {
                    info.FrameRate = (float)(1.0 / frameSec);
                    info.Length = (float)((context.Bvh.FrameCount - 1) * frameSec);
                }
            }
            else
            {
                using var data = new AutoGltfFileParser(path).Parse();
                info.IsVrma = true;
                DetectVrmaTimes(data, out var frameRate, out var frameCount, out var length);
                if (frameRate > 0) info.FrameRate = frameRate;
                info.FrameCount = frameCount;
                info.Length = length;
            }

            if (info.FrameRate <= 0f) info.FrameRate = 30f;
            if (info.FrameCount <= 0) info.FrameCount = Mathf.Max(1, Mathf.RoundToInt(info.Length * info.FrameRate) + 1);
            return info;
        }

        /// <summary>
        /// VRMA(glTFアニメーション)のサンプラー時刻からフレームレート/フレーム数/長さを推定する
        /// </summary>
        private static void DetectVrmaTimes(GltfData data, out float frameRate, out int frameCount, out float length)
        {
            frameRate = 0f;
            frameCount = 0;
            length = 0f;
            try
            {
                var gltfAnimation = data.GLTF.animations.FirstOrDefault();
                if (gltfAnimation != null && gltfAnimation.channels.Count > 0)
                {
                    var sampler = gltfAnimation.samplers[gltfAnimation.channels[0].sampler];
                    var times = data.GetArrayFromAccessor<float>(sampler.input);
                    if (times.Length > 0)
                    {
                        frameCount = times.Length;
                        length = times[times.Length - 1];
                    }
                    if (times.Length > 1)
                    {
                        var deltas = new List<float>();
                        for (int i = 1; i < times.Length; i++)
                        {
                            var delta = times[i] - times[i - 1];
                            if (delta > 0f) deltas.Add(delta);
                        }
                        if (deltas.Count > 0)
                        {
                            deltas.Sort();
                            var medianDelta = deltas[deltas.Count / 2];
                            if (medianDelta > 0f)
                            {
                                frameRate = Mathf.Round(1f / medianDelta * 100f) / 100f;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to detect frame rate: {ex}");
            }
        }

        private void LoadBvh(string path)
        {
            var context = new UniHumanoid.BvhImporterContext();
            context.Parse(path, File.ReadAllText(path));
            context.Load();

            Root = context.Root;
            IsVrma = false;

            //フレームレートはファイルのFrame Timeから自動判定
            if (context.Bvh.FrameTime.TotalSeconds > 0)
            {
                FrameRate = (float)(1.0 / context.Bvh.FrameTime.TotalSeconds);
            }
            FrameCount = context.Bvh.FrameCount;
        }

        private async Task LoadVrmaAsync(string path)
        {
            using var data = new AutoGltfFileParser(path).Parse();

            //フレームレートはglTFアニメーションのサンプラー時刻から自動判定
            DetectVrmaTimes(data, out var detectedFrameRate, out var detectedFrameCount, out _);
            if (detectedFrameRate > 0f)
            {
                FrameRate = detectedFrameRate;
                FrameCount = detectedFrameCount;
            }

            //表情・視線のカーブを自前で読み取り、対応チャンネルをglTFから除去してからインポートする
            //(VrmAnimationImporterがVrm10AnimationInstanceへSetCurveする処理を回避=エディタ警告と視線チャンネルのダングリングを防ぐ)
            BuildExpressionAndLookAtCurves(data);

            //UniVRM 0.131でVrmAnimationImporterのコンストラクタがGltfData→VrmAnimationDataに変更された
            using var loader = new VrmAnimationImporter(new VrmAnimationData(data));
            var instance = await loader.LoadAsync(new RuntimeOnlyAwaitCaller());
            Root = instance.gameObject;
            IsVrma = true;

            VrmaInstance = instance.GetComponent<Vrm10AnimationInstance>();
            if (VrmaInstance != null && VrmaInstance.BoxMan != null)
            {
                VrmaInstance.ShowBoxMan(false);
            }
        }

        /// <summary>
        /// VRMC_vrm_animationの表情・視線を自前カーブとして読み取り、対応するアニメーションチャンネルをglTFから除去する。
        /// (VrmAnimationImporterはノードindexでチャンネルを探すため、チャンネルを消せばSetCurve処理自体が走らず警告も出ない)
        /// </summary>
        private void BuildExpressionAndLookAtCurves(GltfData data)
        {
            expressionCurves.Clear();
            lookAtRotX = lookAtRotY = lookAtRotZ = lookAtRotW = null;
            hasLookAt = false;

            if (UniGLTF.Extensions.VRMC_vrm_animation.GltfDeserializer.TryGet(data.GLTF.extensions, out var vrma) == false) return;
            var gltfAnimation = data.GLTF.animations.FirstOrDefault();
            if (gltfAnimation == null) return;

            var channelsToRemove = new List<int>();

            //表情
            if (vrma.Expressions != null)
            {
                var preset = vrma.Expressions.Preset;
                if (preset != null)
                {
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.happy), preset.Happy, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.angry), preset.Angry, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.sad), preset.Sad, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.relaxed), preset.Relaxed, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.surprised), preset.Surprised, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.aa), preset.Aa, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.ih), preset.Ih, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.ou), preset.Ou, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.ee), preset.Ee, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.oh), preset.Oh, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.blink), preset.Blink, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.blinkLeft), preset.BlinkLeft, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.blinkRight), preset.BlinkRight, channelsToRemove);
                    AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateFromPreset(ExpressionPreset.neutral), preset.Neutral, channelsToRemove);
                }
                if (vrma.Expressions.Custom != null)
                {
                    foreach (var kv in vrma.Expressions.Custom)
                    {
                        AddExpressionCurve(data, gltfAnimation, ExpressionKey.CreateCustom(kv.Key), kv.Value, channelsToRemove);
                    }
                }
            }

            //視線(VRMA仕様: 注視点ノードのローカル回転チャンネル。translationではない)
            if (vrma.LookAt != null && vrma.LookAt.Node.HasValue)
            {
                var channelIndex = FindRotationChannel(gltfAnimation, vrma.LookAt.Node.Value);
                if (channelIndex >= 0)
                {
                    var channel = gltfAnimation.channels[channelIndex];
                    var sampler = gltfAnimation.samplers[channel.sampler];
                    var input = data.GetArrayFromAccessor<float>(sampler.input);
                    var output = data.FlatternFloatArrayFromAccessor(sampler.output); //VEC4(x,y,z,w)
                    lookAtRotX = new AnimationCurve();
                    lookAtRotY = new AnimationCurve();
                    lookAtRotZ = new AnimationCurve();
                    lookAtRotW = new AnimationCurve();
                    for (int j = 0; j < input.Length; j++)
                    {
                        var t = input[j];
                        lookAtRotX.AddKey(new Keyframe(t, output[j * 4 + 0]));
                        lookAtRotY.AddKey(new Keyframe(t, output[j * 4 + 1]));
                        lookAtRotZ.AddKey(new Keyframe(t, output[j * 4 + 2]));
                        lookAtRotW.AddKey(new Keyframe(t, output[j * 4 + 3]));
                    }
                    hasLookAt = true;
                    channelsToRemove.Add(channelIndex);
                }
            }

            //後ろから除去してindexのずれを防ぐ
            foreach (var idx in channelsToRemove.Distinct().OrderByDescending(x => x))
            {
                gltfAnimation.channels.RemoveAt(idx);
            }
        }

        private void AddExpressionCurve(GltfData data, glTFAnimation gltfAnimation, ExpressionKey key, UniGLTF.Extensions.VRMC_vrm_animation.Expression expression, List<int> channelsToRemove)
        {
            if (expression == null || expression.Node.HasValue == false) return;
            var channelIndex = FindTranslationChannel(gltfAnimation, expression.Node.Value);
            if (channelIndex < 0) return;

            var channel = gltfAnimation.channels[channelIndex];
            var sampler = gltfAnimation.samplers[channel.sampler];
            var input = data.GetArrayFromAccessor<float>(sampler.input);
            var output = data.FlatternFloatArrayFromAccessor(sampler.output);
            var curve = new AnimationCurve();
            for (int j = 0; j < input.Length; j++)
            {
                //VRMAの表情はtranslationのX成分に重みが格納される(軸変換されない生の値)
                curve.AddKey(new Keyframe(input[j], output[j * 3]));
            }
            expressionCurves[key] = curve;
            channelsToRemove.Add(channelIndex);
        }

        private static int FindTranslationChannel(glTFAnimation gltfAnimation, int node)
        {
            return FindChannel(gltfAnimation, node, "translation");
        }

        private static int FindRotationChannel(glTFAnimation gltfAnimation, int node)
        {
            return FindChannel(gltfAnimation, node, "rotation");
        }

        private static int FindChannel(glTFAnimation gltfAnimation, int node, string path)
        {
            for (int i = 0; i < gltfAnimation.channels.Count; i++)
            {
                var channel = gltfAnimation.channels[i];
                if (channel.target.node == node && channel.target.path == path)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 指定時刻のポーズをスケルトンに反映する
        /// </summary>
        public void Sample(float time)
        {
            currentTime = Mathf.Clamp(time, 0f, Length);
            if (animationState == null) return;
            animationState.enabled = true;
            animationState.weight = 1f;
            animationState.time = currentTime;
            animation.Sample();
            animationState.enabled = false;
        }

        /// <summary>
        /// 現在のスケルトンのポーズを取得する(Sample後に呼ぶ)
        /// </summary>
        public void GetHumanPose(ref HumanPose pose)
        {
            poseHandler.GetHumanPose(ref pose);
        }

        /// <summary>
        /// 現在の表情の値一覧を取得する(VRMAのみ / Sample後に呼ぶ)
        /// </summary>
        public IEnumerable<KeyValuePair<ExpressionKey, float>> GetExpressionWeights()
        {
            foreach (var kv in expressionCurves)
            {
                yield return new KeyValuePair<ExpressionKey, float>(kv.Key, kv.Value.Evaluate(currentTime));
            }
        }

        /// <summary>
        /// 現在の視線のyaw/pitch(度)を取得する(VRMAで視線情報がある場合のみ / Sample後に呼ぶ)。
        /// VRMA仕様: lookAtノードのローカル回転をExtrinsic ZXYのオイラー角に分解し、Y=yaw / X=pitch とする。
        /// </summary>
        public bool TryGetLookAtYawPitch(out float yaw, out float pitch)
        {
            yaw = 0f;
            pitch = 0f;
            if (hasLookAt == false) return false;

            //glTFの生quaternion(エクスポート時にX反転済み)を評価し、X反転を戻してUnityのローカル回転へ
            var q = new Quaternion(
                lookAtRotX.Evaluate(currentTime),
                -lookAtRotY.Evaluate(currentTime),
                -lookAtRotZ.Evaluate(currentTime),
                lookAtRotW.Evaluate(currentTime));
            if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f) return false;
            q = Quaternion.Normalize(q);

            //UnityのeulerAnglesはZXY順(仕様のExtrinsic ZXYと一致)。Y=yaw, X=pitch。
            var e = q.eulerAngles;
            yaw = Mathf.DeltaAngle(0f, e.y);
            pitch = Mathf.DeltaAngle(0f, e.x);
            return true;
        }

        public void Dispose()
        {
            poseHandler?.Dispose();
            poseHandler = null;
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
                Root = null;
            }
        }
    }
}
