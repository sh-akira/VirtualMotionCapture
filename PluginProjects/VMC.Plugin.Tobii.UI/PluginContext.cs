using UnityMemoryMappedFile;
using VMC.ControlPanel.Plugin;

namespace VMC.Plugin.Tobii.UI
{
    /// <summary>
    /// コントロールパネル本体から受け取った窓口を、設定ウインドウから使えるように保持する。
    /// 本体の Globals.Client の置き換え。
    /// </summary>
    internal static class PluginContext
    {
        public static IControlPanelHost Host { get; set; }

        public static MemoryMappedFileClient Client => Host?.Client;
    }
}
