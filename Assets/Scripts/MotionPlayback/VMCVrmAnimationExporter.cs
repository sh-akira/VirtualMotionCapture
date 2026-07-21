using System;
using System.Collections.Generic;
using System.Linq;
using UniGLTF;
using UnityEngine;
using UniVRM10;

namespace VMC
{
    /// <summary>
    /// VRM Animation(.vrma)のエクスポート
    /// UniVRMのVrmAnimationExporterはHumanoidボーンのみ対応のため、
    /// 表情(Expression)と視線(LookAt)の書き出しを追加した拡張版
    /// (座標系変換等の仕様はUniVRMのVrmAnimationExporter/VrmAnimationImporterに準拠)
    /// </summary>
    public class VMCVrmAnimationExporter : gltfExporter
    {
        public VMCVrmAnimationExporter(
                ExportingGltfData data,
                GltfExportSettings settings)
        : base(data, settings)
        {
            settings.InverseAxis = Axes.X;
        }

        readonly List<float> m_times = new List<float>();

        class PositionExporter
        {
            public List<Vector3> Values = new List<Vector3>();
            public Transform Node;
            readonly Transform m_root;

            public PositionExporter(Transform bone, Transform root)
            {
                Node = bone;
                m_root = root;
            }

            public void Add()
            {
                var p = m_root.worldToLocalMatrix.MultiplyPoint(Node.position);
                // reverse-X
                Values.Add(new Vector3(-p.x, p.y, p.z));
            }
        }
        PositionExporter m_position;
        public void SetPositionBoneAndParent(Transform bone, Transform parent)
        {
            m_position = new PositionExporter(bone, parent);
        }

        class RotationExporter
        {
            public List<Quaternion> Values = new List<Quaternion>();
            public readonly Transform Node;
            public Transform m_parent;

            public RotationExporter(Transform bone, Transform parent)
            {
                Node = bone;
                m_parent = parent;
            }

            public void Add()
            {
                var q = Quaternion.Inverse(m_parent.rotation) * Node.rotation;
                // reverse-X
                Values.Add(new Quaternion(q.x, -q.y, -q.z, q.w));
            }
        }
        readonly Dictionary<HumanBodyBones, RotationExporter> m_rotations = new Dictionary<HumanBodyBones, RotationExporter>();
        public void AddRotationBoneAndParent(HumanBodyBones bone, Transform transform, Transform parent)
        {
            m_rotations.Add(bone, new RotationExporter(transform, parent));
        }

        /// <summary>
        /// 表情の重みチャンネル
        /// VRMAの仕様では表情はノードのX方向のtranslationとして記録される
        /// (VrmAnimationImporterは軸変換せずaccessorの生の値を重みとして読むため、変換なしで書き込む)
        /// </summary>
        class ExpressionExporter
        {
            public List<Vector3> Values = new List<Vector3>();
            public readonly ExpressionKey Key;
            public readonly Transform Node;
            public Func<float> GetWeight;

            public ExpressionExporter(ExpressionKey key, Transform node, Func<float> getWeight)
            {
                Key = key;
                Node = node;
                GetWeight = getWeight;
            }

            public void Add()
            {
                Values.Add(new Vector3(GetWeight(), 0, 0));
            }
        }
        readonly List<ExpressionExporter> m_expressions = new List<ExpressionExporter>();
        public void AddExpression(ExpressionKey key, Transform node, Func<float> getWeight)
        {
            m_expressions.Add(new ExpressionExporter(key, node, getWeight));
        }

        /// <summary>
        /// 視線(LookAt)の注視点ノード
        /// </summary>
        PositionExporter m_lookAt;
        public void SetLookAt(Transform node, Transform root)
        {
            m_lookAt = new PositionExporter(node, root);
        }

        public void AddFrame(TimeSpan time)
        {
            m_times.Add((float)time.TotalSeconds);
            m_position.Add();
            foreach (var kv in m_rotations)
            {
                kv.Value.Add();
            }
            foreach (var expression in m_expressions)
            {
                expression.Add();
            }
            m_lookAt?.Add();
        }

        public void Export(Action<VMCVrmAnimationExporter> addFrames)
        {
            base.Export();

            addFrames(this);

            //
            // export
            //
            var gltfAnimation = new glTFAnimation
            {
            };
            _data.Gltf.animations.Add(gltfAnimation);

            // Nodes には 右手左手変換後のコピーが入っているため名前で逆引きする
            var names = Nodes.Select(x => x.name).ToList();

            // time values
            var input = _data.ExtendBufferAndGetAccessorIndex(m_times.ToArray());

            void AddChannel(int outputAccessor, string nodeName, string path)
            {
                var sampler = gltfAnimation.samplers.Count;
                gltfAnimation.samplers.Add(new glTFAnimationSampler
                {
                    input = input,
                    output = outputAccessor,
                    interpolation = "LINEAR",
                });

                gltfAnimation.channels.Add(new glTFAnimationChannel
                {
                    sampler = sampler,
                    target = new glTFAnimationTarget
                    {
                        node = names.IndexOf(nodeName),
                        path = path,
                    },
                });
            }

            {
                var output = _data.ExtendBufferAndGetAccessorIndex(m_position.Values.ToArray());
                AddChannel(output, m_position.Node.name, "translation");
            }

            foreach (var kv in m_rotations)
            {
                var output = _data.ExtendBufferAndGetAccessorIndex(kv.Value.Values.ToArray());
                AddChannel(output, kv.Value.Node.name, "rotation");
            }

            foreach (var expression in m_expressions)
            {
                var output = _data.ExtendBufferAndGetAccessorIndex(expression.Values.ToArray());
                AddChannel(output, expression.Node.name, "translation");
            }

            if (m_lookAt != null)
            {
                var output = _data.ExtendBufferAndGetAccessorIndex(m_lookAt.Values.ToArray());
                AddChannel(output, m_lookAt.Node.name, "translation");
            }

            // VRMC_vrm_animation
            var vrmAnimation = VrmAnimationUtil.Create(m_rotations.ToDictionary(kv => kv.Key, kv => kv.Value.Node), names);

            // 表情
            if (m_expressions.Count > 0)
            {
                vrmAnimation.Expressions = new UniGLTF.Extensions.VRMC_vrm_animation.Expressions
                {
                    Preset = new UniGLTF.Extensions.VRMC_vrm_animation.Preset(),
                };
                foreach (var expression in m_expressions)
                {
                    var node = new UniGLTF.Extensions.VRMC_vrm_animation.Expression { Node = names.IndexOf(expression.Node.name) };
                    switch (expression.Key.Preset)
                    {
                        case ExpressionPreset.happy: vrmAnimation.Expressions.Preset.Happy = node; break;
                        case ExpressionPreset.angry: vrmAnimation.Expressions.Preset.Angry = node; break;
                        case ExpressionPreset.sad: vrmAnimation.Expressions.Preset.Sad = node; break;
                        case ExpressionPreset.relaxed: vrmAnimation.Expressions.Preset.Relaxed = node; break;
                        case ExpressionPreset.surprised: vrmAnimation.Expressions.Preset.Surprised = node; break;
                        case ExpressionPreset.aa: vrmAnimation.Expressions.Preset.Aa = node; break;
                        case ExpressionPreset.ih: vrmAnimation.Expressions.Preset.Ih = node; break;
                        case ExpressionPreset.ou: vrmAnimation.Expressions.Preset.Ou = node; break;
                        case ExpressionPreset.ee: vrmAnimation.Expressions.Preset.Ee = node; break;
                        case ExpressionPreset.oh: vrmAnimation.Expressions.Preset.Oh = node; break;
                        case ExpressionPreset.blink: vrmAnimation.Expressions.Preset.Blink = node; break;
                        case ExpressionPreset.blinkLeft: vrmAnimation.Expressions.Preset.BlinkLeft = node; break;
                        case ExpressionPreset.blinkRight: vrmAnimation.Expressions.Preset.BlinkRight = node; break;
                        case ExpressionPreset.neutral: vrmAnimation.Expressions.Preset.Neutral = node; break;
                        case ExpressionPreset.custom:
                            if (vrmAnimation.Expressions.Custom == null)
                            {
                                vrmAnimation.Expressions.Custom = new Dictionary<string, UniGLTF.Extensions.VRMC_vrm_animation.Expression>();
                            }
                            vrmAnimation.Expressions.Custom[expression.Key.Name] = node;
                            break;
                    }
                }
            }

            // 視線
            if (m_lookAt != null)
            {
                vrmAnimation.LookAt = new UniGLTF.Extensions.VRMC_vrm_animation.LookAt
                {
                    Node = names.IndexOf(m_lookAt.Node.name),
                };
            }

            UniGLTF.Extensions.VRMC_vrm_animation.GltfSerializer.SerializeTo(
                    ref _data.Gltf.extensions
                    , vrmAnimation);
        }
    }
}
