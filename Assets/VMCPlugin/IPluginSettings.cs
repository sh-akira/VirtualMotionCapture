namespace VMC.Plugin
{
    /// <summary>
    /// プラグイン単位の設定領域。
    ///
    /// 本体の設定ファイル(プロファイル)の中に保存されるため、
    /// ユーザーが設定プロファイルを切り替えるとプラグインの設定も一緒に切り替わる。
    /// 値は型ごとにJSONへ直列化して保持される。
    /// </summary>
    public interface IPluginSettings
    {
        /// <summary>保存済みの値を取得する。無い場合や読めない場合は defaultValue を返す。</summary>
        T Get<T>(string key, T defaultValue = default);

        /// <summary>値を保存する。ファイルへの書き出しは本体の保存タイミングで行われる。</summary>
        void Set<T>(string key, T value);
    }
}
