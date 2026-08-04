using System;
using System.Windows;

namespace VMC.ControlPanel.Plugin
{
    /// <summary>
    /// IControlPanelPlugin の共通部分。
    /// リソース辞書の場所と受け取った窓口の保持だけを面倒見る。
    /// </summary>
    public abstract class ControlPanelPluginBase : IControlPanelPlugin
    {
        public abstract string Id { get; }
        public abstract string Version { get; }
        public abstract int SortOrder { get; }
        public abstract string TitleResourceKey { get; }
        public abstract System.Collections.Generic.IEnumerable<Type> CommandTypes { get; }
        public abstract Window CreateSettingWindow(Window owner);

        /// <summary>リソース辞書を持つアセンブリ名(通常はプラグインのアセンブリ名)</summary>
        protected abstract string ResourceAssemblyName { get; }

        /// <summary>本体から受け取った窓口。設定ウインドウから使う。</summary>
        public IControlPanelHost Host { get; private set; }

        public virtual void Initialize(IControlPanelHost host) => Host = host;

        /// <summary>
        /// Resources/&lt;言語&gt;.xaml を読む。対応していない言語は英語にフォールバックする。
        /// Plugins/配下から読み込まれたアセンブリでも解決できるよう絶対pack URIを使う。
        /// </summary>
        public virtual ResourceDictionary GetLocalization(string language)
        {
            var name = language == "Japanese" || language == "Chinese" || language == "Korean" ? language : "English";
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/{ResourceAssemblyName};component/Resources/{name}.xaml", UriKind.Absolute)
            };
        }
    }
}
