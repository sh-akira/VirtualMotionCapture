using System;
using System.Windows;
using VMC.ControlPanel.Plugin;
using VMC.Plugin.Commands;

namespace VMC.Plugin.Tobii.UI
{
    /// <summary>Tobii Eye Tracker のアイトラッキング(コントロールパネル側)</summary>
    public class TobiiControlPanelPlugin : IControlPanelPlugin
    {
        public string Id => "Tobii";

        public string Version => "1.0.0";
        public System.Collections.Generic.IEnumerable<System.Type> CommandTypes => TobiiCommands.Types;

        //表情・視線系は200番台
        public int SortOrder => 220;

        public string TitleResourceKey => "Plugin_Tobii_Title";

        public void Initialize(IControlPanelHost host) => PluginContext.Host = host;

        public ResourceDictionary GetLocalization(string language)
        {
            //対応していない言語では英語にフォールバックする
            var name = language == "Japanese" || language == "Chinese" || language == "Korean" ? language : "English";
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/VMC.Plugin.Tobii.UI;component/Resources/{name}.xaml", UriKind.Absolute)
            };
        }

        public Window CreateSettingWindow(Window owner) => new EyeTracking_TobiiSettingWindow();
    }
}
