using sh_akira;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace VMC
{

    [Serializable]
    public class CommonSettings
    {
        public string LoadSettingFilePathOnStart = ""; //起動時に読み込む設定ファイルパス
        [OptionalField]
        public bool LaunchSteamVROnStartup = true; //起動時にSteamVRを初期化する

        //初期値
        [OnDeserializing()]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            LoadSettingFilePathOnStart = "";
            LaunchSteamVROnStartup = true;
        }


        public static CommonSettings Current = new CommonSettings();

        //共通設定の書き込み
        public static void Save()
        {
            string path = Path.GetFullPath(Application.dataPath + "/../Settings/common.json");
            var directoryName = Path.GetDirectoryName(path);
            if (Directory.Exists(directoryName) == false) Directory.CreateDirectory(directoryName);
            File.WriteAllText(path, Json.Serializer.ToReadable(Json.Serializer.Serialize(Current)));
        }

        //共通設定の読み込み
        public static void Load()
        {
            string path = Path.GetFullPath(Application.dataPath + "/../Settings/common.json");
            if (!File.Exists(path))
            {
                return;
            }
            Current = Json.Serializer.Deserialize<CommonSettings>(File.ReadAllText(path)); //設定を読み込み
        }
    }

}
