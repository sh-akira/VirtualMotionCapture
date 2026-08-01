using System;
using System.Windows;
using VMC.ControlPanel.Plugin;

namespace VMC.Plugin.ViveSR.UI
{
    /// <summary>
    /// SRanipal SDK を使う2機能は同じDLLに入っているが、デバイスとしては別物なので
    /// 「外部デバイス」欄には2つのボタンとして並べる。
    /// </summary>
    internal static class ViveSRLocalization
    {
        public static ResourceDictionary Get(string language)
        {
            //対応していない言語では英語にフォールバックする
            var name = language == "Japanese" || language == "Chinese" || language == "Korean" ? language : "English";
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/VMC.Plugin.ViveSR.UI;component/Resources/{name}.xaml", UriKind.Absolute)
            };
        }
    }

    /// <summary>VIVE Pro Eye / Focus 3 / Droolon F1 のアイトラッキング</summary>
    public class ViveProEyeControlPanelPlugin : IControlPanelPlugin
    {
        public string Id => "ViveSR.Eye";
        public string Version => "1.0.0";
        //表情・視線系は200番台
        public int SortOrder => 200;
        public string TitleResourceKey => "Plugin_ViveSR.Eye_Title";

        public void Initialize(IControlPanelHost host) => PluginContext.Host = host;

        public ResourceDictionary GetLocalization(string language) => ViveSRLocalization.Get(language);

        public Window CreateSettingWindow(Window owner) => new EyeTracking_ViveProEyeSettingWindow();
    }

    /// <summary>VIVE Facial Tracker のリップトラッキング</summary>
    public class ViveFacialTrackerControlPanelPlugin : IControlPanelPlugin
    {
        public string Id => "ViveSR.Lip";
        public string Version => "1.0.0";
        public int SortOrder => 210;
        public string TitleResourceKey => "Plugin_ViveSR.Lip_Title";

        public void Initialize(IControlPanelHost host) => PluginContext.Host = host;

        //同じ辞書を2回マージすることになるが、キーが同じなので実害は無い
        public ResourceDictionary GetLocalization(string language) => ViveSRLocalization.Get(language);

        public Window CreateSettingWindow(Window owner) => new LipTracking_ViveSettingWindow();
    }
}
