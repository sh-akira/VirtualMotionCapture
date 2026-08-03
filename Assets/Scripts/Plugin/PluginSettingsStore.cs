using System;
using UnityEngine;
using VMC.Plugin;

namespace VMC
{
    /// <summary>
    /// プラグイン設定を本体の設定ファイル(プロファイル)へ保存する実装。
    ///
    /// Settings.Current.PluginSettings に "プラグインID/キー" → JSON文字列 の形で持つ。
    /// Settings.Current はプロファイル切り替えで差し替わるため、値はキャッシュせず
    /// 毎回 Settings.Current を見に行く。
    /// </summary>
    internal class PluginSettingsStore : IPluginSettings
    {
        private readonly string prefix;

        public PluginSettingsStore(string pluginId)
        {
            prefix = pluginId + "/";
        }

        private static System.Collections.Generic.Dictionary<string, string> Store
        {
            get
            {
                if (Settings.Current.PluginSettings == null)
                {
                    Settings.Current.PluginSettings = new System.Collections.Generic.Dictionary<string, string>();
                }
                return Settings.Current.PluginSettings;
            }
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (Store.TryGetValue(prefix + key, out var json) == false) return defaultValue;
            if (string.IsNullOrEmpty(json)) return defaultValue;
            try
            {
                return sh_akira.Json.Serializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                //壊れた値で起動できなくなるのは避けたいので、既定値へフォールバックする
                Debug.LogWarning($"[Plugin] 設定の読み込みに失敗しました: {prefix + key} ({ex.Message})");
                return defaultValue;
            }
        }

        public void Set<T>(string key, T value)
        {
            try
            {
                Store[prefix + key] = sh_akira.Json.Serializer.Serialize(value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Plugin] 設定の保存に失敗しました: {prefix + key} ({ex.Message})");
            }
        }
    }
}
