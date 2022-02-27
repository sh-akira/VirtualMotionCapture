using DVRSDK.Encrypt;
using UnityEngine;

namespace DVRSDK.Utilities
{
    public class UnitySettingStore : ISettingsStore
    {
        readonly string internalStoragePath;

        public UnitySettingStore()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var getFilesDir = currentActivity.Call<AndroidJavaObject>("getFilesDir"))
            {
                internalStoragePath = getFilesDir.Call<string>("getCanonicalPath");
            }
#else
            // Windowsはユーザーがファイルを見れるので注意
            internalStoragePath = Application.persistentDataPath;
#endif
        }

        public string GetString(string key)
        {
            return PlayerPrefs.GetString(key);
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }

        public bool RemoveString(string key)
        {
            var ret = PlayerPrefs.GetString(key) != null;
            PlayerPrefs.DeleteKey(key);
            return ret;
        }

        public string GetInternalStoragePath()
        {
            return internalStoragePath;
        }

        public IEncrypter GetEncrypter()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new UnityAndroidEncrypter();
#else
            return new DotNetEncrypter();
#endif
        }
    }
}
