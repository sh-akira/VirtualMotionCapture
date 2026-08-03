using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VMC.Plugin;

namespace VMC
{
    /// <summary>
    /// 公式プラグイン(Plugins/配下)のローダ。
    ///
    /// ユーザー製作のMod(Mods/配下・ModManager)とは意図的に分けている:
    ///  - Mod はコントロールパネル接続後・プレリリース版のみ読み込まれるが、
    ///    プラグインは設定の適用より前に読み込む必要がある
    ///  - Mod を読み込むと VRoid Hub 連携が無効化されるが、公式プラグインは対象外
    ///  - Mod は属性で識別するのに対し、プラグインは IVMCPlugin の実装で識別する
    /// </summary>
    public class PluginManager : MonoBehaviour
    {
        /// <summary>
        /// プラグインの置き場所。ビルド版ではexeの隣、エディタではリポジトリ直下を指す。
        /// Awakeの実行順に依らず使えるよう都度求める。
        /// </summary>
        private static string PluginsPath => Path.GetFullPath(Application.dataPath + "/../Plugins/");

        private readonly List<LoadedPlugin> loadedPlugins = new List<LoadedPlugin>();

        public IReadOnlyList<LoadedPlugin> LoadedPlugins => loadedPlugins;

        public class LoadedPlugin
        {
            public string Id;
            public string DisplayName;
            public string Version;
            public string AssemblyPath;
            public IVMCPlugin Instance;
        }

        /// <summary>
        /// Plugins/ 以下を走査してプラグインを読み込む。
        /// ControlWPFWindow の初期化中(設定の適用より前)に一度だけ呼ばれる。
        /// </summary>
        public void LoadPlugins(IPluginHost host)
        {
            if (Directory.Exists(PluginsPath) == false)
            {
                //初回起動時に置き場所が分かるよう、空でも作っておく
                try { Directory.CreateDirectory(PluginsPath); } catch { }
                return;
            }

            Debug.Log("Start Loading Plugins");

            //プラグインごとのフォルダ(直下のDLLも一応拾う)
            var directories = new List<string> { PluginsPath };
            directories.AddRange(Directory.GetDirectories(PluginsPath, "*", SearchOption.TopDirectoryOnly));

            foreach (var directory in directories)
            {
                //ネイティブDLLはDllImportの探索パスに入らないため、先に絶対パスで読み込んでおく。
                //ネイティブDLLは native/ サブフォルダに置く決まりなので、
                //ここで拾う直下の *.dll はマネージドDLLだけになる
                NativeLibraryLoader.PreloadFrom(directory);

                foreach (var dllFile in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    LoadPluginAssembly(dllFile, host);
                }
            }

            Debug.Log($"Loaded {loadedPlugins.Count} plugin(s)");
        }

        private void LoadPluginAssembly(string dllFile, IPluginHost host)
        {
            Type[] pluginTypes;
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                pluginTypes = assembly.GetTypes()
                    .Where(x => x.IsPublic && x.IsAbstract == false && typeof(IVMCPlugin).IsAssignableFrom(x))
                    .ToArray();
            }
            catch (BadImageFormatException)
            {
                //ネイティブDLLなので無視してよい
                return;
            }
            catch (ReflectionTypeLoadException ex)
            {
                //SDKの依存が欠けている場合など。他のプラグインは読み込めるようにして続行する
                Debug.LogError($"[Plugin] 型の読み込みに失敗しました: {dllFile}");
                foreach (var loaderException in ex.LoaderExceptions.Take(3))
                {
                    Debug.LogError($"[Plugin]   {loaderException.Message}");
                }
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Plugin] 読み込みに失敗しました: {dllFile} ({ex.Message})");
                return;
            }

            foreach (var type in pluginTypes)
            {
                try
                {
                    if (typeof(MonoBehaviour).IsAssignableFrom(type) == false)
                    {
                        Debug.LogError($"[Plugin] {type.FullName} は MonoBehaviour を継承していないため読み込めません");
                        continue;
                    }

                    var plugin = (IVMCPlugin)gameObject.AddComponent(type);

                    if (loadedPlugins.Any(d => d.Id == plugin.Id))
                    {
                        Debug.LogError($"[Plugin] ID '{plugin.Id}' が重複しているため読み込みを中止しました: {dllFile}");
                        Destroy((MonoBehaviour)plugin);
                        continue;
                    }

                    //受信したコマンドを型解決できるよう、Initializeより前に登録しておく
                    UnityMemoryMappedFile.PipeCommands.RegisterPluginCommandTypes(plugin.CommandTypes);

                    plugin.Initialize(host);

                    loadedPlugins.Add(new LoadedPlugin
                    {
                        Id = plugin.Id,
                        DisplayName = plugin.DisplayName,
                        Version = plugin.Version,
                        AssemblyPath = dllFile,
                        Instance = plugin,
                    });

                    Debug.Log($"[Plugin] {plugin.DisplayName} {plugin.Version} を読み込みました");
                }
                catch (Exception ex)
                {
                    //1つのプラグインの失敗で本体が起動しなくなるのは避ける
                    Debug.LogError($"[Plugin] 初期化に失敗しました: {type.FullName}");
                    Debug.LogException(ex);
                }
            }
        }
    }
}
