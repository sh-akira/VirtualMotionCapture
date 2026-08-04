using System;
using System.Collections.Generic;
using System.Windows;
using VMC.ControlPanel.Plugin;
using VMC.Plugin.Commands;

namespace VMC.Plugin.Mocopi.UI
{
    /// <summary>
    /// mocopi連携のコントロールパネル側プラグイン。
    /// 設定画面の「外部デバイス」欄にボタンとして並ぶ。
    /// </summary>
    public class MocopiControlPanelPlugin : ControlPanelPluginBase
    {
        public override string Id => "mocopi";
        public override string Version => "1.0.0";
        //モーション系は100番台
        public override int SortOrder => 100;
        public override string TitleResourceKey => "mocopi_Title";
        public override IEnumerable<Type> CommandTypes => MocopiCommands.Types;
        protected override string ResourceAssemblyName => "VMC.Plugin.Mocopi.UI";

        public override void Initialize(IControlPanelHost host)
        {
            base.Initialize(host);
            PluginContext.Host = host;
        }

        public override Window CreateSettingWindow(Window owner) => new MotionCapture_mocopiSettingWindow();
    }
}
