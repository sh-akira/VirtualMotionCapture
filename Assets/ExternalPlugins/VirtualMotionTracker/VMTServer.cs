
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Valve.VR;

public class VMTServer : MonoBehaviour
{

    private static string driverVersion = null;
    private static string installPath = null;

    private void Start()
    {
        var server = GetComponent<uOSC.uOscServer>();
        server.onDataReceived.AddListener(OnDataReceived);
    }


    void OnDataReceived(uOSC.Message message)
    {
        //有効なとき以外処理しない
        if (this.isActiveAndEnabled)
        {
            //仮想コントローラー V2.3
            if (message.address == "/VMT/Out/Alive"
                && (message.values[0] is string)
            )
            {
                driverVersion = (string)message.values[0];
                if (message.values.Length > 1 && message.values[1] is string)
                {
                    installPath = (string)message.values[1];
                }
            }
        }
    }

    public static async Task<string> InstallVMT()
    {
        if (string.IsNullOrEmpty(driverVersion) == false)
        {
            return "Please uninstall VMT before install.\nインストールを続ける前に、VMTをアンインストールしてください";
        }

        try
        {
            string driverPath_rel = @"C:\VirtualMotionTracker\vmt";
            string driverPath = System.IO.Path.GetFullPath(driverPath_rel);
            var runtimePath = OpenVR.RuntimePath();

            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.WorkingDirectory = runtimePath + @"\bin\win64";
            process.StartInfo.FileName = runtimePath + @"\bin\win64\vrpathreg.exe";
            process.StartInfo.Arguments = "adddriver \"" + driverPath + "\"";
            process.StartInfo.UseShellExecute = false;
            await Task.Run(() =>
            {
                process.Start();
                process.WaitForExit();
            });
        }
        catch (Exception ex)
        {
            return "Error:" + ex.Message + "\n" + ex.StackTrace;
        }
        return null;
    }

    public static async Task<string> UninstallVMT()
    {
        string driverPath_rel = @"C:\VirtualMotionTracker\vmt";
        string driverPath = System.IO.Path.GetFullPath(driverPath_rel);

        if (string.IsNullOrEmpty(installPath))
        {
            //インストールパスが受信できていない場合少し待つ
            await Task.Delay(2000);
        }

        if (string.IsNullOrEmpty(installPath) == false)
        {
            //場所がわかっている
            driverPath = installPath;
        }

        try
        {
            var runtimePath = OpenVR.RuntimePath();
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.WorkingDirectory = runtimePath + @"\bin\win64";
            process.StartInfo.FileName = runtimePath + @"\bin\win64\vrpathreg.exe";
            process.StartInfo.Arguments = "removedriver \"" + driverPath + "\"";
            process.StartInfo.UseShellExecute = false;
            await Task.Run(() =>
            {
                process.Start();
                process.WaitForExit();
            });
        }
        catch (Exception ex)
        {
            return "Error:" + ex.Message + "\n" + ex.StackTrace;
        }
        return null;
    }

}