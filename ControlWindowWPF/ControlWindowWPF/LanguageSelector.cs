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
        public static void ChangeLanguage(string language)
        {
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri($"/VirtualMotionCaptureControlPanel;component/Resources/{language}.xaml", UriKind.Relative);
            Application.Current.Resources.MergedDictionaries[0] = dictionary;
        }
    }
}
