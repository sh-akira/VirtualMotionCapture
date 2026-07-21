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
            try
            {
                var gltfAnimation = data.GLTF.animations.FirstOrDefault();
                if (gltfAnimation != null && gltfAnimation.channels.Count > 0)
                {
                    var sampler = gltfAnimation.samplers[gltfAnimation.channels[0].sampler];
                    var times = data.GetArrayFromAccessor<float>(sampler.input);
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
                                FrameRate = Mathf.Round(1f / medianDelta * 100f) / 100f;
                                FrameCount = times.Length;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to detect frame rate: {ex}");
            }

            using var loader = new VrmAnimationImporter(data);
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
        /// 指定時刻のポーズをスケルトンに反映する
        /// </summary>
        public void Sample(float time)
        {
            if (animationState == null) return;
            animationState.enabled = true;
            animationState.weight = 1f;
            animationState.time = Mathf.Clamp(time, 0f, Length);
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
            if (VrmaInstance == null) yield break;
            foreach (var kv in VrmaInstance.ExpressionMap)
            {
                yield return new KeyValuePair<ExpressionKey, float>(kv.Key, kv.Value());
            }
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
