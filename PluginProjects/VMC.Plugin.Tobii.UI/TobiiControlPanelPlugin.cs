using System;
using System.Collections.Generic;
using System.Windows;
using VMC.ControlPanel.Plugin;
using VMC.Plugin.Commands;

namespace VMC.Plugin.Tobii.UI
{
    /// <summary>Tobii Eye Tracker のアイトラッキング(コントロールパネル側)</summary>
    public class TobiiControlPanelPlugin : ControlPanelPluginBase
    {
        public override string Id => "Tobii";
        public override string Version => "1.0.0";
        //表情・視線系は200番台
        public override int SortOrder => 220;
        public override string TitleResourceKey => "Tobii_Title";
        public override IEnumerable<Type> CommandTypes => TobiiCommands.Types;
        protected override string ResourceAssemblyName => "VMC.Plugin.Tobii.UI";

        public override void Initialize(IControlPanelHost host)
        {
            base.Initialize(host);
            PluginContext.Host = host;
        }

        public override Window CreateSettingWindow(Window owner) => new EyeTracking_TobiiSettingWindow();
    }
}
