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
            if (System.Globalization.CultureInfo.CurrentCulture.Name == "ja-JP")
            {
                ChangeLanguage("Japanese");
            }
            else if (System.Globalization.CultureInfo.CurrentCulture.Name == "zh-CN")
            {
                ChangeLanguage("Chinese");
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
        }

        public static string Get(string key)
        {
            return Application.Current.Resources.MergedDictionaries[0][key] as string;
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
