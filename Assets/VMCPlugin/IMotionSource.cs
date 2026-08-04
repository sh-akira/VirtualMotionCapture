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
        /// アバターへ適用する VirtualAvatar を作り、本体へ登録する。
        /// 適用優先度は VRIK より後、VMCProtocol より前。
        ///
        /// 返された VirtualAvatar の Enable と Apply* で反映を制御する。
        /// 使い終わったら Remove を呼ぶこと。
        /// </summary>
        VirtualAvatar Create(Transform boneParentTransform);

        /// <summary>Create で作った VirtualAvatar の登録を解除する</summary>
        void Remove(VirtualAvatar virtualAvatar);
    }
}
