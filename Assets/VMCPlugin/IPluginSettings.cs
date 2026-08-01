using System;

namespace VMC.Plugin
{
    /// <summary>
    /// プラグイン単位の設定領域。
    ///
    /// 本体の設定ファイル(プロファイル)の中に保存されるため、
    /// ユーザーが設定プロファイルを切り替えるとプラグインの設定も一緒に切り替わる。
    /// 値は型ごとに JSON へ直列化して保持される。
    /// </summary>
    public interface IPluginSettings
    {
        /// <summary>保存済みの値を取得する。無い場合は defaultValue を返す。</summary>
        T Get<T>(string key, T defaultValue = default);

        /// <summary>保存済みの値の取得を試みる。</summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>値を保存する。ファイルへの書き出しは本体の保存タイミングで行われる。</summary>
        void Set<T>(string key, T value);

        /// <summary>キーが存在するか</summary>
        bool Contains(string key);

        /// <summary>キーを削除する</summary>
        void Remove(string key);
    }
}
