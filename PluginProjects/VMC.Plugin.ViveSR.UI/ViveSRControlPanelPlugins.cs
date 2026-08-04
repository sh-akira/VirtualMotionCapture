using System;
using System.Collections.Generic;
using System.Windows;
using VMC.ControlPanel.Plugin;
using VMC.Plugin.Commands;

namespace VMC.Plugin.ViveSR.UI
{
    /// <summary>
    /// SRanipal SDK を使う2機能は同じDLLに入っているが、デバイスとしては別物なので
    /// 「外部デバイス」欄には2つのボタンとして並べる。
    /// </summary>
    public abstract class ViveSRControlPanelPluginBase : ControlPanelPluginBase
    {
        public override string Version => "1.0.0";
        public override IEnumerable<Type> CommandTypes => ViveSRCommands.Types;
        protected override string ResourceAssemblyName => "VMC.Plugin.ViveSR.UI";

        public override void Initialize(IControlPanelHost host)
        {
            base.Initialize(host);
            PluginContext.Host = host;
        }
    }

    /// <summary>VIVE Pro Eye / Focus 3 / Droolon F1 のアイトラッキング</summary>
    public class ViveProEyeControlPanelPlugin : ViveSRControlPanelPluginBase
    {
        public override string Id => "ViveSR.Eye";
        //表情・視線系は200番台
        public override int SortOrder => 200;
        public override string TitleResourceKey => "ViveSR.Eye_Title";

        public override Window CreateSettingWindow(Window owner) => new EyeTracking_ViveProEyeSettingWindow();
    }

    /// <summary>VIVE Facial Tracker のリップトラッキング</summary>
    public class ViveFacialTrackerControlPanelPlugin : ViveSRControlPanelPluginBase
    {
        public override string Id => "ViveSR.Lip";
        public override int SortOrder => 210;
        public override string TitleResourceKey => "ViveSR.Lip_Title";

        public override Window CreateSettingWindow(Window owner) => new LipTracking_ViveSettingWindow();
    }
}
