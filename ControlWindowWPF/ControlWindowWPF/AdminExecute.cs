using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// 管理者権限の管理をします
    /// </summary>
    public static class AdminExecute
    {
        /// <summary>
        /// 現在管理者権限で実行中か取得
        /// </summary>
        public static bool IsAdmin => (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);

        /// <summary>
        /// 自身を管理者権限で実行します
        /// </summary>
        /// <param name="args">実行時引数</param>
        /// <returns>実行結果</returns>
        public static (bool successExecute, int exitCode) RestartAsAdmin(string[] args)
        {
            var startInfo = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, CreateArgs(args))
            {
                UseShellExecute = true,
                Verb = "runas",
            };

            try
            {
                // 管理者権限で実行
                var process = Process.Start(startInfo);
                process.WaitForExit();
                return (true, process.ExitCode);
            }
            catch (Win32Exception ex)
            {
                // UACダイアログに"いいえ"を選択すると例外
                Console.WriteLine(ex.Message);
            }
            return (false, -1);
        }

        /// <summary>
        /// 実行時引数様にスペースエスケープします
        /// </summary>
        /// <param name="args">実行時引数</param>
        /// <returns>変換結果</returns>
        private static string CreateArgs(string[] args)
        {
            return string.Join(" ", args.Select(s => s.Contains(" ") ? $"\"{s}\"" : s));
        }
    }
}
