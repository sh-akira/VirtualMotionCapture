using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VMC.Plugin
{
    /// <summary>
    /// プラグインフォルダ内のネイティブDLLを読み込めるようにするヘルパ。
    ///
    /// Unity の DllImport はネイティブDLLを exe 直下や Plugins フォルダから探すため、
    /// Plugins/&lt;プラグイン名&gt;/ に置いたDLLはそのままでは解決できない。
    /// プラグインの Initialize から PreloadFrom を呼んで、先に絶対パスでロードしておく。
    /// (一度プロセスにロードされていれば、以降の DllImport は同じモジュールを使う)
    /// </summary>
    public static class NativeLibraryLoader
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectoryW(string lpPathName);

        /// <summary>
        /// 指定ディレクトリ直下のネイティブDLLをすべて先読みする。
        /// マネージドDLLが混ざっていても LoadLibrary が失敗するだけなので無視してよい。
        /// </summary>
        /// <returns>読み込めたDLLの数</returns>
        public static int PreloadFrom(string directory)
        {
            if (Directory.Exists(directory) == false) return 0;

            //依存DLL同士の解決のため、検索パスにも追加しておく
            SetDllDirectoryW(directory);

            var loaded = 0;
            foreach (var dll in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (LoadLibraryW(dll) != IntPtr.Zero) loaded++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Plugin] ネイティブDLLの先読みに失敗しました: {dll} ({ex.Message})");
                }
            }
            return loaded;
        }

        /// <summary>
        /// 指定アセンブリが置かれているディレクトリを返す。
        /// プラグインから自身のフォルダを知るために使う。
        /// </summary>
        public static string GetAssemblyDirectory(Type typeInAssembly)
        {
            var location = typeInAssembly.Assembly.Location;
            return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
        }
    }
}
