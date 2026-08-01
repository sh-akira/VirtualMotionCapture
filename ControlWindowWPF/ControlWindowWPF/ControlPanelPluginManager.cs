using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using UnityMemoryMappedFile;
using VMC.ControlPanel.Plugin;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// コントロールパネル側の公式プラグイン(ControlPanel/Plugins/配下)のローダ。
    ///
    /// 一覧はこちらのフォルダ走査が一次情報になる(設定ウインドウを生成するのに
    /// 実体のクラスが要るため)。Unity側にも同じIDのプラグインが入っているかは
    /// GetPluginList で問い合わせて突き合わせる。
    /// </summary>
    public static class ControlPanelPluginManager
    {
        private static readonly List<LoadedPlugin> plugins = new List<LoadedPlugin>();

        public static IReadOnlyList<LoadedPlugin> Plugins => plugins;

        public class LoadedPlugin
        {
            public IControlPanelPlugin Instance;
            public string AssemblyPath;
            /// <summary>Unity側にも同じIDのプラグインが入っているか</summary>
            public bool UnitySideAvailable;
        }

        public static string PluginsPath => Path.Combine(Globals.GetCurrentAppDir(), "Plugins");

        private static readonly ControlPanelHost Host = new ControlPanelHost();

        /// <summary>プラグインへ渡すコントロールパネル本体の窓口</summary>
        private class ControlPanelHost : IControlPanelHost
        {
            public MemoryMappedFileClient Client => Globals.Client;
            public string CurrentLanguage => Globals.CurrentLanguage;
            public string GetLocalizedString(string key) => LanguageSelector.GetFromAll(key);
        }

        /// <summary>
        /// Plugins/ 以下を走査してプラグインを読み込み、リソース辞書を登録する。
        /// アプリ起動時に一度だけ呼ぶ。
        /// </summary>
        public static void Load()
        {
            plugins.Clear();

            if (Directory.Exists(PluginsPath) == false) return;

            foreach (var dllFile in Directory.GetFiles(PluginsPath, "*.dll", SearchOption.AllDirectories))
            {
                LoadAssembly(dllFile);
            }

            plugins.Sort((a, b) => a.Instance.SortOrder.CompareTo(b.Instance.SortOrder));

            ApplyLocalization(Globals.CurrentLanguage);
        }

        private static void LoadAssembly(string dllFile)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                var types = assembly.GetTypes()
                    .Where(x => x.IsPublic && x.IsAbstract == false && typeof(IControlPanelPlugin).IsAssignableFrom(x));

                foreach (var type in types)
                {
                    var instance = (IControlPanelPlugin)Activator.CreateInstance(type);
                    if (plugins.Any(d => d.Instance.Id == instance.Id))
                    {
                        //同じIDが二重に入っている状態は事故なので、後勝ちにせず弾く
                        continue;
                    }
                    instance.Initialize(Host);
                    plugins.Add(new LoadedPlugin { Instance = instance, AssemblyPath = dllFile });
                }
            }
            catch (BadImageFormatException)
            {
                //ネイティブDLLなので無視してよい
            }
            catch (Exception)
            {
                //1つのプラグインが壊れていてもコントロールパネルは起動させる
            }
        }

        /// <summary>
        /// Unity側に読み込まれているプラグインと突き合わせる。
        /// 片方にしか入っていないものはボタンを無効化して気づけるようにする。
        /// </summary>
        public static async System.Threading.Tasks.Task CheckUnitySideAsync()
        {
            if (plugins.Count == 0) return;
            if (Globals.Client == null) return;

            try
            {
                await Globals.Client.SendCommandWaitAsync(new PipeCommands.GetPluginList(), d =>
                {
                    var data = (PipeCommands.ReturnPluginList)d;
                    var ids = new HashSet<string>((data.PluginList ?? new List<PluginItem>()).Select(p => p.Id));
                    foreach (var plugin in plugins)
                    {
                        plugin.UnitySideAvailable = ids.Contains(plugin.Instance.Id);
                    }
                });
            }
            catch (Exception)
            {
                //応答が無い場合は判定しない(全て有効のまま扱う)
                foreach (var plugin in plugins) plugin.UnitySideAvailable = true;
            }
        }

        private static readonly List<ResourceDictionary> appliedDictionaries = new List<ResourceDictionary>();

        /// <summary>
        /// プラグインのリソース辞書を差し替える。
        /// 本体のリソース辞書は MergedDictionaries[0] 固定なので、
        /// プラグインの辞書はその後ろに積む。
        /// </summary>
        public static void ApplyLocalization(string language)
        {
            var merged = Application.Current?.Resources?.MergedDictionaries;
            if (merged == null) return;

            foreach (var dictionary in appliedDictionaries)
            {
                merged.Remove(dictionary);
            }
            appliedDictionaries.Clear();

            foreach (var plugin in plugins)
            {
                try
                {
                    var dictionary = plugin.Instance.GetLocalization(language);
                    if (dictionary == null) continue;
                    merged.Add(dictionary);
                    appliedDictionaries.Add(dictionary);
                }
                catch (Exception)
                {
                    //リソースが無くてもボタン名が空になるだけなので続行する
                }
            }
        }
    }
}
