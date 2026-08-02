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
                //マネージドDLLをLoadLibraryしても意味が無いので飛ばす
                if (IsManagedAssembly(dll)) continue;

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
        /// .NETのアセンブリ(マネージドDLL)かどうかをPEヘッダから判定する。
        ///
        /// ネイティブDLLに Assembly.LoadFrom を試すと、例外を捕まえても
        /// Monoが "Could not load image ..." をコンソールへ出してしまうため、
        /// 読み込む前にこれで振り分ける。
        /// </summary>
        public static bool IsManagedAssembly(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 0x40) return false;

                    //DOSヘッダ(MZ)からPEヘッダの位置を得る
                    if (reader.ReadUInt16() != 0x5A4D) return false;   //"MZ"
                    stream.Position = 0x3C;
                    var peOffset = reader.ReadUInt32();
                    if (peOffset + 24 >= stream.Length) return false;

                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550) return false; //"PE\0\0"

                    //COFFヘッダ20バイトを飛ばしてオプショナルヘッダへ
                    stream.Position = peOffset + 4 + 20;
                    var magic = reader.ReadUInt16();
                    int dataDirectoryOffset;
                    if (magic == 0x10B) dataDirectoryOffset = 96;       //PE32
                    else if (magic == 0x20B) dataDirectoryOffset = 112; //PE32+
                    else return false;

                    //データディレクトリの15番目(index 14)がCLIヘッダ。RVAが0でなければマネージド
                    var cliHeaderPosition = peOffset + 4 + 20 + dataDirectoryOffset + (14 * 8);
                    if (cliHeaderPosition + 8 > stream.Length) return false;
                    stream.Position = cliHeaderPosition;
                    return reader.ReadUInt32() != 0;
                }
            }
            catch (Exception)
            {
                //読めないファイルはマネージドでないものとして扱う
                return false;
            }
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
