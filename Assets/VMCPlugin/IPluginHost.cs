using System;
using UnityEngine;

namespace VMC.Plugin
{
    /// <summary>
    /// プラグインから本体機能へアクセスするための窓口。
    /// 本体側(Assembly-CSharp)が実装し、Initialize でプラグインへ渡される。
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>表情・視線の制御</summary>
        IFaceControl FaceControl { get; }

        /// <summary>モーションソースの登録</summary>
        IMotionSourceFactory MotionSource { get; }

        /// <summary>コントロールパネルとの通信</summary>
        IPluginIpc Ipc { get; }

        /// <summary>現在読み込まれているモデル(未読み込みなら null)</summary>
        GameObject CurrentModel { get; }

        /// <summary>プラグイン単位の設定領域を取得する</summary>
        IPluginSettings GetSettings(string pluginId);

        /// <summary>
        /// 本体の設定(プロファイル)が読み込まれ、各機能へ適用されるタイミング。
        /// プラグインは保存済み設定をここで自身へ反映する。
        /// </summary>
        event Action SettingsApplied;
    }
}
