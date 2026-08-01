using System.Windows;
using UnityMemoryMappedFile;

namespace VMC.ControlPanel.Plugin
{
    /// <summary>
    /// プラグインからコントロールパネル本体の機能を使うための窓口。
    /// プラグインが exe を直接参照しなくて済むようにするためのもの。
    /// </summary>
    public interface IControlPanelHost
    {
        /// <summary>Unity側との通信クライアント</summary>
        MemoryMappedFileClient Client { get; }

        /// <summary>現在の言語("Japanese" / "English" / "Chinese" / "Korean")</summary>
        string CurrentLanguage { get; }

        /// <summary>本体・プラグイン両方の辞書から文字列を引く</summary>
        string GetLocalizedString(string key);
    }

    /// <summary>
    /// コントロールパネル側の公式プラグイン。
    /// ControlPanel/Plugins/ 配下のDLLから、このインターフェースを実装したクラスが探される。
    ///
    /// 設定画面の「外部デバイス」欄にボタンとして並び、押すと CreateSettingWindow が
    /// 返したウインドウが開く。設定画面そのものに項目を足すことはしない
    /// (プラグインが増えても設定画面のレイアウトが崩れないようにするため)。
    /// </summary>
    public interface IControlPanelPlugin
    {
        /// <summary>
        /// プラグインを一意に識別するID。Unity側プラグインの IVMCPlugin.Id と
        /// 同じ文字列にすること(両方揃っているかの突き合わせに使う)。
        /// </summary>
        string Id { get; }

        /// <summary>プラグインのバージョン</summary>
        string Version { get; }

        /// <summary>
        /// 「外部デバイス」欄での並び順。小さいほど先に表示される。
        /// モーション系100番台・表情系200番台、のように緩く決めておくと後から挿しやすい。
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// ボタンに表示する名前のリソースキー。
        /// GetLocalization が返す ResourceDictionary に含めておくこと。
        /// キーの衝突を避けるため "Plugin_&lt;Id&gt;_" を前置する規約とする。
        /// </summary>
        string TitleResourceKey { get; }

        /// <summary>
        /// 指定言語のリソース辞書を返す。language は "Japanese" / "English" /
        /// "Chinese" / "Korean" のいずれか。対応しない言語では英語を返してよい。
        /// </summary>
        ResourceDictionary GetLocalization(string language);

        /// <summary>
        /// 読み込み直後に一度だけ呼ばれる。通信クライアント等はここで受け取る。
        /// </summary>
        void Initialize(IControlPanelHost host);

        /// <summary>設定ウインドウを生成する。表示はコントロールパネル側が行う。</summary>
        Window CreateSettingWindow(Window owner);
    }
}
