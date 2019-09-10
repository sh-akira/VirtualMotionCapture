using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityMemoryMappedFile;

namespace VirtualMotionCaptureControlPanel
{
    public class Globals
    {
        public static MemoryMappedFileClient Client;

        public static void Connect(string pipeName)
        {
            Client = new MemoryMappedFileClient();
            Client.Start(pipeName);
        }

        public static string CurrentLanguage = "Japanese";

        public static string CurrentVRMFilePath;

        public static bool LeftControllerCenterEnable = false;
        public static List<UPoint> LeftControllerPoints = new List<UPoint>();
        public static bool RightControllerCenterEnable = false;
        public static List<UPoint> RightControllerPoints = new List<UPoint>();

        public static List<UPoint> LeftControllerStickPoints = new List<UPoint>();
        public static List<UPoint> RightControllerStickPoints = new List<UPoint>();

        public static bool EnableSkeletal = true;

        public static float LeftHandRotation = -90.0f;
        public static float RightHandRotation = 90.0f;

        public static List<KeyAction> KeyActions = new List<KeyAction>();
        
        public static string GetCurrentAppDir()
        {
            var path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (path.Last() == '\\') path = path.Substring(0, path.Length - 1);
            path += "\\";
            return path;
        }

        public static bool CheckFileNameIsValid(string filename)
        {
            System.Text.RegularExpressions.Regex r =
                new System.Text.RegularExpressions.Regex(
                    "[\\x00-\\x1f<>:\"/\\\\|?*]" +
                    "|^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9]|CLOCK\\$)(\\.|$)" +
                    "|[\\. ]$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            //マッチしたら、不正なファイル名
            if (r.IsMatch(filename))
            {
                return false;
            }
            return true;
        }
    }
}
