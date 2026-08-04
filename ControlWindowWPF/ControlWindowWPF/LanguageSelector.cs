using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VirtualMotionCaptureControlPanel
{
    public static class LanguageSelector
    {
        public static void SetAutoLanguage()
        {
            //Check all language file
            ChangeLanguage("Japanese");
            ChangeLanguage("Chinese");
            ChangeLanguage("Korean");
            ChangeLanguage("English");

            if (System.Globalization.CultureInfo.CurrentCulture.Name == "ja-JP")
            {
                ChangeLanguage("Japanese");
            }
            else if (System.Globalization.CultureInfo.CurrentCulture.Name == "zh-CN")
            {
                ChangeLanguage("Chinese");
            }
            else if (System.Globalization.CultureInfo.CurrentCulture.Name == "ko-KR")
            {
                ChangeLanguage("Korean");
            }
            else
            {
                ChangeLanguage("English");
            }
        }

        public static void ChangeLanguage(string language)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri($"/VirtualMotionCaptureControlPanel;component/Resources/{language}.xaml", UriKind.Relative);
            Application.Current.Resources.MergedDictionaries[0] = dictionary;
            Globals.CurrentLanguage = language;
            UnityMemoryMappedFile.KeyConfig.Language = language;
            //プラグインの辞書は[0]の後ろに積んでいるので、こちらも一緒に差し替える
            ControlPanelPluginManager.ApplyLocalization(language);
        }

        public static string Get(string key)
        {
            return Application.Current.Resources.MergedDictionaries[0][key] as string;
        }

        /// <summary>
        /// 本体とプラグインの両方の辞書からキーを探す。
        /// Get は本体の辞書([0])しか見ないため、プラグインのキーはこちらを使う。
        /// </summary>
        public static string GetFromAll(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
            {
                if (dictionary.Contains(key)) return dictionary[key] as string;
            }
            return null;
        }

        public static string GetByTypeName(string typename)
        {
            if (typename == "HMD") return Get("HMD");
            if (typename == "コントローラー") return Get("Controller");
            if (typename == "トラッカー") return Get("Tracker");
            return Get("NoAssign");
        }
    }
}
