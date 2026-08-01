namespace VMC.Plugin
{
    /// <summary>
    /// 公式プラグインのエントリポイント。
    ///
    /// Mod(Mods/配下・ユーザー製作・VMCMod.VMCPlugin属性で識別)とは別系統で、
    /// こちらは Plugins/配下に置かれた公式プラグインをインターフェースで識別する。
    /// 実装クラスは MonoBehaviour を継承すること(PluginManager が AddComponent する)。
    ///
    /// 注意: AddComponent の時点で Awake が走るが、その時点ではまだ Initialize が
    /// 呼ばれていないため host を参照できない。初期化処理は Awake ではなく
    /// Initialize に書くこと。
    /// </summary>
    public interface IVMCPlugin
    {
        /// <summary>
        /// プラグインを一意に識別するID。設定の保存キーや、コントロールパネル側の
        /// プラグインとの対応付けに使うため、両者で同じ文字列にすること。
        /// 例: "mocopi" / "ViveSR" / "Tobii"
        /// </summary>
        string Id { get; }

        /// <summary>ログ表示用の名前(コントロールパネルの表示名は WPF 側が持つ)</summary>
        string DisplayName { get; }

        /// <summary>プラグインのバージョン</summary>
        string Version { get; }

        /// <summary>
        /// 本体の初期化中(設定の読み込み・適用より前)に一度だけ呼ばれる。
        /// 拡張点への登録はここで行う。
        /// </summary>
        void Initialize(IPluginHost host);
    }
}
