using DVRSDK.Encrypt;
using DVRSDK.Serializer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DVRSDK.Utilities
{
    public class DotNetSettingsStore : ISettingsStore
    {
        private const string _settingFileName = "Settings.json";

        public string GetString(string key)
        {
            var dict = LoadDictionary();
            if (dict == null) return null;
            if (dict.ContainsKey(key) == false) return null;
            return dict[key];
        }

        public void SetString(string key, string value)
        {
            var dict = LoadDictionary();
            if (dict == null) dict = new Dictionary<string, string>();
            dict[key] = value;
            SaveDictionary(dict);
        }

        public bool RemoveString(string key)
        {
            var dict = LoadDictionary();
            if (dict == null) return false;
            if (dict.ContainsKey(key) == false) return false;
            dict.Remove(key);
            SaveDictionary(dict);
            return true;
        }

        private Dictionary<string, string> LoadDictionary()
        {
            var path = GetSettingFilePath();
            if (File.Exists(path) == false) return null;
            var json = File.ReadAllText(path);
            return new DotNetDataContractJsonSerializer().Deserialize<Dictionary<string, string>>(json);
        }

        private void SaveDictionary(Dictionary<string, string> dict)
        {
            if (dict == null) return;
            var json = new DotNetDataContractJsonSerializer().Serialize(dict);
            File.WriteAllText(GetSettingFilePath(), json);
        }

        private string GetSettingFilePath()
        {
            var exePath = Assembly.GetEntryAssembly().Location;
            return Path.Combine(Path.GetDirectoryName(exePath), _settingFileName);
        }

        public string GetInternalStoragePath()
        {
            var path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (path.Last() == '\\') path = path.Substring(0, path.Length - 1);
            path += "\\";
            return path;
        }

        public IEncrypter GetEncrypter()
        {
            return new DotNetEncrypter();
        }
    }
}
