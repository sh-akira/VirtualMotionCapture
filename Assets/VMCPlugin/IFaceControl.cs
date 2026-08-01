using System;
using UnityEngine;

namespace VMC.Plugin
{
    /// <summary>
    /// 表情・視線の制御。本体の FaceController を抽象化したもの。
    ///
    /// UniVRM の型を露出させないため、視線は「見る先のワールド座標」を渡す形にしている。
    /// (ボーン目線・Expression目線のどちらかは本体側が面倒を見る)
    /// </summary>
    public interface IFaceControl
    {
        /// <summary>
        /// 表情がモデルへ適用される直前に呼ばれる。
        /// 視線の上書き(SetLookAtPosition)はこのタイミングで行うこと。
        /// </summary>
        event Action BeforeApply;

        /// <summary>左まぶたを閉じる量(0=開き, 1=閉じ)</summary>
        void SetBlink_L(float value);

        /// <summary>右まぶたを閉じる量(0=開き, 1=閉じ)</summary>
        void SetBlink_R(float value);

        /// <summary>
        /// 表情キー名と重みの組を混ぜ込む。
        /// presetName は混ぜ込み元の識別名で、同じ名前での再呼び出しは上書きになる。
        /// </summary>
        void MixPresets(string presetName, string[] keys, float[] values);

        /// <summary>
        /// 目線を指定したワールド座標へ向ける。BeforeApply の中から呼ぶこと。
        /// (LookAtTarget 未使用時のみ有効)
        /// </summary>
        void SetLookAtPosition(Vector3 worldPosition);

        /// <summary>
        /// まぶたを外部デバイスが制御していることを本体に伝える。
        /// true の間、本体の自動まばたきは抑制される。
        /// </summary>
        bool ExternalEyelidControlEnabled { get; set; }
    }
}
