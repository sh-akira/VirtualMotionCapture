using UnityEngine;

namespace VMC.Plugin
{
    /// <summary>
    /// 外部デバイスのモーションをアバターへ流し込むための入口。
    /// </summary>
    public interface IMotionSourceFactory
    {
        /// <summary>
        /// boneParentTransform 以下のボーン階層を「外部デバイス由来のモーション」として
        /// アバターへ適用する VirtualAvatar を作り、本体の MotionManager へ登録する。
        /// 適用優先度は VRIK より後、VMCProtocol より前。
        /// </summary>
        IMotionSourceAvatar Create(Transform boneParentTransform);
    }

    /// <summary>
    /// 登録済みモーションソース1つ分のハンドル。
    /// </summary>
    public interface IMotionSourceAvatar
    {
        /// <summary>false の間はアバターへ反映されない</summary>
        bool Enable { get; set; }

        bool ApplyRootPosition { get; set; }
        bool ApplyRootRotation { get; set; }
        bool ApplySpine { get; set; }
        bool ApplyChest { get; set; }
        bool ApplyHead { get; set; }
        bool ApplyLeftArm { get; set; }
        bool ApplyRightArm { get; set; }
        bool ApplyLeftHand { get; set; }
        bool ApplyRightHand { get; set; }
        bool ApplyLeftLeg { get; set; }
        bool ApplyRightLeg { get; set; }
        bool ApplyLeftFoot { get; set; }
        bool ApplyRightFoot { get; set; }

        /// <summary>腰ボーンの位置ずれを補正する</summary>
        bool CorrectHipBone { get; set; }

        /// <summary>現在の向き・位置を基準位置として取り直す</summary>
        void Recenter();

        /// <summary>MotionManager から登録を解除する</summary>
        void Remove();
    }
}
