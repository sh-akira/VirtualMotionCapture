using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VMC.Plugin
{
    /// <summary>
    /// プラグインが同梱するネイティブDLLを読み込めるようにするヘルパー。
    ///
    /// Unity の DllImport はネイティブDLLを exe 直下や Plugins フォルダから探すため、
    /// プラグインのフォルダに置いたDLLはそのままでは解決できない。
    /// 先に絶対パスでプロセスへ読み込んでおけば、以降の DllImport は同じモジュールを使う。
    ///
    /// ネイティブDLLは Plugins/&lt;プラグイン名&gt;/native/ に置く決まりにしている。
    /// マネージドDLLと同じ場所に混ぜないことで、
    /// 「どちらなのかをファイルの中身から判別する」処理が不要になる。
    /// </summary>
    public static class NativeLibraryLoader
    {
        /// <summary>ネイティブDLLを置くサブフォルダ名</summary>
        public const string NativeDirectoryName = "native";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectoryW(string lpPathName);

        /// <summary>
        /// プラグインフォルダ直下の native/ にあるネイティブDLLをすべて先読みする。
        /// native/ が無ければ何もしない(ネイティブDLLを使わないプラグイン)。
        /// </summary>
        /// <param name="pluginDirectory">プラグインのフォルダ(native/ の親)</param>
        /// <returns>読み込めたDLLの数</returns>
        public static int PreloadFrom(string pluginDirectory)
        {
            var nativeDirectory = Path.Combine(pluginDirectory, NativeDirectoryName);
            if (Directory.Exists(nativeDirectory) == false) return 0;

            //ネイティブDLL同士の依存を解決できるよう、検索パスにも追加しておく
            SetDllDirectoryW(nativeDirectory);

            var loaded = 0;
            foreach (var dll in Directory.GetFiles(nativeDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                if (LoadLibraryW(dll) != IntPtr.Zero)
                {
                    loaded++;
                }
                else
                {
                    Debug.LogWarning($"[Plugin] ネイティブDLLを読み込めませんでした: {dll} " +
                                     $"(Win32エラー {Marshal.GetLastWin32Error()})");
                }
            }
            return loaded;
        }
    }
}
