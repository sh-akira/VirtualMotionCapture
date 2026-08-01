using System;
using System.Windows;
using VMC.ControlPanel.Plugin;
using VMC.Plugin.Commands;

namespace VMC.Plugin.Mocopi.UI
{
    /// <summary>
    /// mocopi連携のコントロールパネル側プラグイン。
    /// 設定画面の「外部デバイス」欄にボタンとして並ぶ。
    /// </summary>
    public class MocopiControlPanelPlugin : IControlPanelPlugin
    {
        public string Id => "mocopi";

        public string Version => "1.0.0";
        public System.Collections.Generic.IEnumerable<System.Type> CommandTypes => MocopiCommands.Types;

        //モーション系は100番台
        public int SortOrder => 100;

        public string TitleResourceKey => "Plugin_mocopi_Title";

        public void Initialize(IControlPanelHost host) => PluginContext.Host = host;

        public ResourceDictionary GetLocalization(string language)
        {
            //対応していない言語では英語にフォールバックする
            var name = language == "Japanese" || language == "Chinese" || language == "Korean" ? language : "English";
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/VMC.Plugin.Mocopi.UI;component/Resources/{name}.xaml", UriKind.Absolute)
            };
        }

        public Window CreateSettingWindow(Window owner) => new MotionCapture_mocopiSettingWindow();
    }
}
