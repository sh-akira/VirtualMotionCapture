using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VMC
{
    /// <summary>
    /// BVHファイルの書き出し(UniVRM/UniHumanoidにBVHエクスポータが無いため自前実装)
    /// ボーン構成はVRMのHumanoidボーンを使用する
    /// UniHumanoidのBvhImporterContextで読み込んだ際に元のモーションに戻るよう、
    /// インポータの逆変換(X反転・チャンネル順 Yrotation Xrotation Zrotation)で書き出す
    /// </summary>
    public class BvhWriter
    {
        //単位はセンチメートル(BVHの慣例)
        private const float Scale = 100f;

        private class BvhJoint
        {
            public HumanBodyBones Bone;
            public Transform Transform;
            public BvhJoint Parent;
            public List<BvhJoint> Children = new List<BvhJoint>();
            public Vector3 Offset; //レストポーズでの親ジョイントからの相対位置
            public Quaternion RestWorldRotation; //レスト(バインド)ポーズでのワールド回転
            public Quaternion CurrentDelta; //AddFrame内でのレストからのワールド差分回転(親の計算に使用)
        }

        private readonly Animator animator;
        private readonly Transform root;
        private BvhJoint hipsJoint;
        private readonly List<BvhJoint> jointOrder = new List<BvhJoint>();
        private readonly List<float[]> frames = new List<float[]>();

        /// <summary>
        /// animator: T-Poseのヒューマノイド(レストポーズがT-Poseであること)
        /// </summary>
        public BvhWriter(Animator animator, Transform root)
        {
            this.animator = animator;
            this.root = root;
            BuildHierarchy();
        }

        private void BuildHierarchy()
        {
            var boneMap = new Dictionary<HumanBodyBones, BvhJoint>();
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                var t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                boneMap[bone] = new BvhJoint { Bone = bone, Transform = t };
            }

            //Humanoidの親子関係を構築(存在しない中間ボーンはスキップして最も近い祖先に繋ぐ)
            foreach (var kv in boneMap)
            {
                if (kv.Key == HumanBodyBones.Hips) continue;
                var parentJoint = FindParentJoint(kv.Key, boneMap);
                if (parentJoint == null) continue;
                kv.Value.Parent = parentJoint;
                parentJoint.Children.Add(kv.Value);
            }

            hipsJoint = boneMap[HumanBodyBones.Hips];
            hipsJoint.Offset = root.InverseTransformPoint(hipsJoint.Transform.position);

            //オフセット計算とジョイント順(Traverse順=チャンネル順)確定
            jointOrder.Clear();
            TraverseJoint(hipsJoint);

            //レスト(バインド)ポーズでのワールド回転を記録する。
            //BvhWriterはバインドポーズの状態で構築される前提(呼び出し側でRestoreBindPose済み)
            foreach (var joint in jointOrder)
            {
                joint.RestWorldRotation = joint.Transform.rotation;
            }
        }

        private void TraverseJoint(BvhJoint joint)
        {
            jointOrder.Add(joint);
            foreach (var child in joint.Children)
            {
                child.Offset = child.Transform.position - joint.Transform.position;
                TraverseJoint(child);
            }
        }

        private BvhJoint FindParentJoint(HumanBodyBones bone, Dictionary<HumanBodyBones, BvhJoint> boneMap)
        {
            var parentIndex = HumanTrait.GetParentBone((int)bone);
            while (parentIndex >= 0)
            {
                var parentBone = (HumanBodyBones)parentIndex;
                if (boneMap.TryGetValue(parentBone, out var joint)) return joint;
                parentIndex = HumanTrait.GetParentBone(parentIndex);
            }
            return null;
        }

        /// <summary>
        /// 現在のスケルトンのポーズを1フレームとして記録する
        /// </summary>
        public void AddFrame()
        {
            var values = new List<float>();
            foreach (var joint in jointOrder)
            {
                if (joint == hipsJoint)
                {
                    //ROOTは位置チャンネル(ルート相対、X反転、cm)
                    var p = root.InverseTransformPoint(joint.Transform.position) * Scale;
                    values.Add(-p.x);
                    values.Add(p.y);
                    values.Add(p.z);
                }

                //BVHはレスト回転が単位(identity)である前提のため、絶対ローカル回転ではなく
                //「レストポーズからのワールド差分回転」を親ジョイント相対で書き出す。
                //D_j = W_j(f) * W_j(rest)^-1、L_j = D_parent^-1 * D_j
                //(こうするとインポート時に各ボーンのレスト基準回転がクローンと一致し、姿勢が崩れない)
                var delta = joint.Transform.rotation * Quaternion.Inverse(joint.RestWorldRotation);
                joint.CurrentDelta = delta;
                var parentDelta = joint.Parent != null ? joint.Parent.CurrentDelta : Quaternion.identity;
                var q = Quaternion.Inverse(parentDelta) * delta;

                //X反転(インポータのReverseX()の逆変換 = 同じ変換)
                q.ToAngleAxis(out var angle, out var axis);
                var bvhRotation = float.IsNaN(axis.x) ? Quaternion.identity : Quaternion.AngleAxis(-angle, new Vector3(-axis.x, axis.y, axis.z));

                //チャンネル順 Yrotation Xrotation Zrotation は
                //Ry*Rx*Rz = Quaternion.Euler(x,y,z) と等価のため、eulerAnglesでそのまま分解できる
                var euler = bvhRotation.eulerAngles;
                values.Add(NormalizeAngle(euler.y));
                values.Add(NormalizeAngle(euler.x));
                values.Add(NormalizeAngle(euler.z));
            }
            frames.Add(values.ToArray());
        }

        private static float NormalizeAngle(float angle)
        {
            //-180～180に正規化
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// BVH形式の文字列を生成する
        /// </summary>
        public string Write(float frameTime, int startFrame, int endFrame)
        {
            var sb = new StringBuilder();
            sb.AppendLine("HIERARCHY");
            WriteJoint(sb, hipsJoint, 0);

            startFrame = Mathf.Clamp(startFrame, 0, frames.Count - 1);
            endFrame = Mathf.Clamp(endFrame, startFrame, frames.Count - 1);
            var frameCount = endFrame - startFrame + 1;

            sb.AppendLine("MOTION");
            sb.AppendLine($"Frames: {frameCount}");
            sb.AppendLine($"Frame Time: {frameTime.ToString("0.########", CultureInfo.InvariantCulture)}");
            for (int i = startFrame; i <= endFrame; i++)
            {
                sb.AppendLine(string.Join(" ", frames[i].Select(v => v.ToString("0.####", CultureInfo.InvariantCulture))));
            }
            return sb.ToString();
        }

        private void WriteJoint(StringBuilder sb, BvhJoint joint, int depth)
        {
            var indent = new string(' ', depth * 2);
            var type = joint == hipsJoint ? "ROOT" : "JOINT";
            sb.AppendLine($"{indent}{type} {GetJointName(joint.Bone)}");
            sb.AppendLine($"{indent}{{");
            var childIndent = new string(' ', (depth + 1) * 2);
            //オフセットはX反転・cm
            var offset = joint.Offset * Scale;
            sb.AppendLine($"{childIndent}OFFSET {(-offset.x).ToString("0.####", CultureInfo.InvariantCulture)} {offset.y.ToString("0.####", CultureInfo.InvariantCulture)} {offset.z.ToString("0.####", CultureInfo.InvariantCulture)}");
            if (joint == hipsJoint)
            {
                sb.AppendLine($"{childIndent}CHANNELS 6 Xposition Yposition Zposition Yrotation Xrotation Zrotation");
            }
            else
            {
                sb.AppendLine($"{childIndent}CHANNELS 3 Yrotation Xrotation Zrotation");
            }

            if (joint.Children.Count == 0)
            {
                sb.AppendLine($"{childIndent}End Site");
                sb.AppendLine($"{childIndent}{{");
                //末端の長さは不明のため、親からのオフセットと同方向に短い終端を置く
                var endOffset = joint.Offset.sqrMagnitude > 0.000001f ? joint.Offset.normalized * 0.05f * Scale : new Vector3(0, 0.05f * Scale, 0);
                sb.AppendLine($"{childIndent}  OFFSET {(-endOffset.x).ToString("0.####", CultureInfo.InvariantCulture)} {endOffset.y.ToString("0.####", CultureInfo.InvariantCulture)} {endOffset.z.ToString("0.####", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"{childIndent}}}");
            }
            else
            {
                foreach (var child in joint.Children)
                {
                    WriteJoint(sb, child, depth + 1);
                }
            }
            sb.AppendLine($"{indent}}}");
        }

        private static string GetJointName(HumanBodyBones bone)
        {
            //UniHumanoidのSkeletonEstimator等がボーン名から推定しやすいUnity Humanoid名を使用する
            return bone.ToString();
        }
    }
}
