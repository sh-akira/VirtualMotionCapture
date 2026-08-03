using System;
using System.Collections.Generic;

namespace VMC.Plugin
{
    /// <summary>
    /// 公式プラグインのエントリポイント。Plugins/ 配下のDLLから実装クラスが探される。
    /// 実装クラスは MonoBehaviour を継承すること(PluginManager が AddComponent する)。
    ///
    /// ユーザー製作のMod(Mods/配下・VMCMod.VMCPlugin属性で識別)とは別系統。
    /// 違いは documents/plugins.md を参照。
    /// </summary>
    public interface IVMCPlugin
    {
        /// <summary>
        /// プラグインを一意に識別するID。設定の保存キーや、コントロールパネル側の
        /// プラグインとの対応付けに使うため、両者で同じ文字列にすること。
        /// 例: "mocopi" / "ViveSR.Eye" / "Tobii"
        /// </summary>
        string Id { get; }

        /// <summary>ログ表示用の名前(コントロールパネルの表示名はWPF側が持つ)</summary>
        string DisplayName { get; }

        /// <summary>プラグインのバージョン</summary>
        string Version { get; }

        /// <summary>
        /// コントロールパネルとやりとりする独自コマンドの型。
        /// 本体の共有アセンブリには入っていないので、受信時の型解決に使えるよう
        /// PluginManager が PipeCommands へ登録する。無ければ null か空でよい。
        /// </summary>
        IEnumerable<Type> CommandTypes { get; }

        /// <summary>
        /// 本体の初期化中(設定の読み込み・適用より前)に一度だけ呼ばれる。
        /// 拡張点への登録はここで行う。
        ///
        /// AddComponent の時点ではまだ host を受け取っていないため、
        /// 初期化を Awake に書かないこと。
        /// </summary>
        void Initialize(IPluginHost host);
    }
}
