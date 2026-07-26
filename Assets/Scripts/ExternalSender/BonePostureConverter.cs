using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

namespace VMC
{
    /// <summary>
    /// VMCProtocolで受信したオリジナル(非正規化)ボーンのローカル回転を、
    /// ControlRigへ与える正規化ローカル回転へ変換する。
    ///
    /// 【送信側は変換不要】
    /// 送信は Vrm10Instance.Humanoid.GetBoneTransform をそのまま読めばオリジナル姿勢が得られる。
    ///
    /// 【受信側だけ変換が要る理由】
    /// VMCは受信したボーンを VirtualAvatar のクローン → MotionManager → animator.GetBoneTransform
    /// という経路で適用するが、ControlRig生成時の animator は正規化ボーンを指す。
    /// また ControlRig の Process() は毎フレーム正規化ボーンからオリジナルボーンを上書きするため、
    /// オリジナルボーンへ直接書いても次のフレームで消える。
    /// (VRIK・キャリブレーション・モーション再生も全て正規化ボーン前提)
    /// そのため、プロトコルの境界でだけ正規化空間へ持ち上げる。
    ///
    /// 変換式は UniVRM の <see cref="BoneInitialRotation.NormalizedLocalRotation"/> と同一。
    /// 初期回転(Tポーズ時のローカル/ワールド回転)の取得もその型に任せている。
    ///
    /// VRM0.x由来のモデルは元から正規化されているため変換は恒等写像になり、挙動は変わらない。
    /// </summary>
    public class BonePostureConverter
    {
        private readonly Dictionary<HumanBodyBones, BoneInitialRotation> initialRotations
            = new Dictionary<HumanBodyBones, BoneInitialRotation>();

        /// <summary>変換が不要か(全ボーンの初期回転が単位=既に正規化済み)</summary>
        public bool IsIdentity { get; private set; } = true;

        /// <summary>
        /// モデル読み込み直後(Tポーズかつ ControlRig 構築直後)に呼ぶこと。
        /// VRIK等がボーンを動かした後では初期回転が取れない。
        /// </summary>
        public static BonePostureConverter Capture(Vrm10Instance vrm10Instance)
        {
            var converter = new BonePostureConverter();
            if (vrm10Instance == null || vrm10Instance.Humanoid == null) return converter;

            foreach (var (boneTransform, bone) in vrm10Instance.Humanoid.BoneMap)
            {
                if (boneTransform == null) continue;

                //Tポーズ時のローカル回転・ワールド回転をUniVRMの型に記録させる
                var initial = new BoneInitialRotation(boneTransform);
                converter.initialRotations[bone] = initial;

                if (Quaternion.Angle(initial.InitialLocalRotation, Quaternion.identity) > 0.001f
                    || Quaternion.Angle(initial.InitialGlobalRotation, Quaternion.identity) > 0.001f)
                {
                    converter.IsIdentity = false;
                }
            }
            return converter;
        }

        /// <summary>
        /// 受信したオリジナルのローカル回転を、ControlRigへ与える正規化ローカル回転へ変換する。
        /// BoneInitialRotation.NormalizedLocalRotation は Transform の現在値を読むため、
        /// ここでは同じ式に受信値を当てはめる。
        /// </summary>
        public Quaternion ToNormalizedLocalRotation(HumanBodyBones bone, Quaternion originalLocalRotation)
        {
            if (initialRotations.TryGetValue(bone, out var initial) == false) return originalLocalRotation;

            return initial.InitialGlobalRotation
                 * Quaternion.Inverse(initial.InitialLocalRotation)
                 * originalLocalRotation
                 * Quaternion.Inverse(initial.InitialGlobalRotation);
        }
    }
}
