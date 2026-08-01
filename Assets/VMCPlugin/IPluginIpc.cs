using System;
using System.Threading.Tasks;
using UnityMemoryMappedFile;

namespace VMC.Plugin
{
    /// <summary>
    /// コントロールパネル(WPF)との通信。
    ///
    /// 送受信するコマンド型は UnityMemoryMappedFile.PipeCommands の入れ子型である必要がある
    /// (受信側の型解決が PipeCommands のネスト型を走査する実装のため)。
    /// プラグイン専用のコマンドは PipeCommands_&lt;プラグイン名&gt;.cs として
    /// 共有アセンブリ側に partial で足すこと。
    /// </summary>
    public interface IPluginIpc
    {
        /// <summary>
        /// コントロールパネルからコマンドを受信したときに呼ばれる。
        /// Unity のメインスレッドとは限らないため、Unity API を触る場合は Post を使うこと。
        /// </summary>
        event EventHandler<DataReceivedEventArgs> Received;

        /// <summary>コントロールパネルへコマンドを送る。応答を返す場合は requestId を指定する。</summary>
        Task SendCommandAsync(object command, string requestId = null);

        /// <summary>Unity のメインスレッドで処理を実行する</summary>
        void Post(Action action);
    }
}
