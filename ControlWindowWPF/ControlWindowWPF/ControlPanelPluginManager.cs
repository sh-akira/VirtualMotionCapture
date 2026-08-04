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
            /// <summary>Unity側にも同じIDのプラグインが入っているか</summary>
            public bool UnitySideAvailable;
        }

        public static string PluginsPath => Path.Combine(Globals.GetCurrentAppDir(), "Plugins");

        private static readonly ControlPanelHost Host = new ControlPanelHost();

        /// <summary>プラグインへ渡すコントロールパネル本体の窓口</summary>
        private class ControlPanelHost : IControlPanelHost
        {
            public MemoryMappedFileClient Client => Globals.Client;
        }

        /// <summary>
        /// Plugins/ 以下を走査してプラグインを読み込み、リソース辞書を登録する。
        /// アプリ起動時に一度だけ呼ぶ。
        /// </summary>
        public static void Load()
        {
            plugins.Clear();

            if (Directory.Exists(PluginsPath) == false) return;

            //XAML(pack URI)の解決は名前でのアセンブリ読み込みを経由するため、
            //Plugins/配下から読んだアセンブリを名前で引けるようにしておく。
            //これが無いとプラグインのウインドウもリソース辞書も開けない。
            //解決ハンドラの中でファイル走査をすると再入する恐れがあるので、先に一覧を作っておく
            var dllFiles = Directory.GetFiles(PluginsPath, "*.dll", SearchOption.AllDirectories);

            pluginAssemblyPaths.Clear();
            foreach (var dll in dllFiles)
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (pluginAssemblyPaths.ContainsKey(name) == false) pluginAssemblyPaths[name] = dll;
            }
            AppDomain.CurrentDomain.AssemblyResolve -= ResolvePluginAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += ResolvePluginAssembly;

            foreach (var dllFile in dllFiles)
            {
                LoadAssembly(dllFile);
            }

            plugins.Sort((a, b) => a.Instance.SortOrder.CompareTo(b.Instance.SortOrder));

            ApplyLocalization(Globals.CurrentLanguage);
        }

        /// <summary>アセンブリ名 → Plugins/配下のDLLパス</summary>
        private static readonly Dictionary<string, string> pluginAssemblyPaths = new Dictionary<string, string>();

        private static Assembly ResolvePluginAssembly(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;

            //既に読み込み済みならそれを返す(LoadFromで読んだものは名前検索に引っかからない)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == name) return assembly;
            }

            if (pluginAssemblyPaths.TryGetValue(name, out var path) == false) return null;

            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
                return null;
            }
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
                    //受信したコマンドを型解決できるよう、Initializeより前に登録しておく
                    PipeCommands.RegisterPluginCommandTypes(instance.CommandTypes);

                    instance.Initialize(Host);
                    plugins.Add(new LoadedPlugin { Instance = instance });
                }
            }
            catch (BadImageFormatException)
            {
                //ネイティブDLLなので無視してよい
            }
            catch (ReflectionTypeLoadException ex)
            {
                //依存DLLが足りない場合はどの型で失敗したかが分からないと切り分けられない
                var reasons = ex.LoaderExceptions.Select(x => x.Message).Distinct();
                LogLoadFailure(dllFile, string.Join(" / ", reasons));
            }
            catch (Exception ex)
            {
                //1つのプラグインが壊れていてもコントロールパネルは起動させる
                LogLoadFailure(dllFile, ex.Message);
            }
        }

        /// <summary>読み込みに失敗した理由を残す。握り潰すと原因が追えなくなるため</summary>
        private static void LogLoadFailure(string dllFile, string reason)
        {
            System.Diagnostics.Debug.WriteLine($"[Plugin] 読み込みに失敗しました: {dllFile} ({reason})");
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
