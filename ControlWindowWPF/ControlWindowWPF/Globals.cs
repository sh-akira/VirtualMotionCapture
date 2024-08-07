using sh_akira;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
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

        public static FreeOffsetItem FreeOffset = new FreeOffsetItem();

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


        [Serializable]
        public class CommonSettingsWPF
        {
            public string CurrentPathOnSettingFileDialog = ""; //設定ファイルダイアログパス
            public string CurrentPathOnVRMFileDialog = ""; //VRMファイルダイアログパス
            public string CurrentPathOnExternalCameraFileDialog = ""; //ExternalCameraダイアログパス
            public string CurrentPathOnCameraPlusFileDialog = ""; //CameraPlusダイアログパス
            public string CurrentPathOnPhotoFileDialog = ""; //写真撮影ダイアログパス

            public PipeCommands.CalibrateType LastCalibrateType = PipeCommands.CalibrateType.Ipose;
            public bool EnableCalibrationEndSound = false;

            public bool FirewallChecked = false;

            //初期値
            [OnDeserializing()]
            internal void OnDeserializingMethod(StreamingContext context)
            {
                CurrentPathOnSettingFileDialog = "";
                CurrentPathOnVRMFileDialog = "";
                CurrentPathOnExternalCameraFileDialog = "";
                CurrentPathOnCameraPlusFileDialog = "";
                CurrentPathOnPhotoFileDialog = "";
                LastCalibrateType = PipeCommands.CalibrateType.Ipose;
                EnableCalibrationEndSound = false;
                FirewallChecked = false;
            }
        }

        public static CommonSettingsWPF CurrentCommonSettingsWPF = new CommonSettingsWPF();

        public static string ExistDirectoryOrNull(string path) {
            return Directory.Exists(path) ? path : null;
        }

        //共通設定の書き込み
        public static void SaveCommonSettings()
        {
            try
            {
                string path = Path.GetFullPath(GetCurrentAppDir() + "/../Settings/commonWPF.json");
                var directoryName = Path.GetDirectoryName(path);
                if (Directory.Exists(directoryName) == false) Directory.CreateDirectory(directoryName);
                File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(CurrentCommonSettingsWPF)));
            }
            catch (Exception) { }
        }

        //共通設定の読み込み
        public static void LoadCommonSettings()
        {
            string path = Path.GetFullPath(GetCurrentAppDir() + "/../Settings/commonWPF.json");
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                CurrentCommonSettingsWPF = Json.Serializer.Deserialize<CommonSettingsWPF>(File.ReadAllText(path)); //設定を読み込み
            }
            catch (Exception) {
                //エラー発生時は初期値にする
                CurrentCommonSettingsWPF = new CommonSettingsWPF();
                SaveCommonSettings();
            }
        }


    }
}
