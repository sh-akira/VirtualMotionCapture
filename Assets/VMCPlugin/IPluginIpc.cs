using System;
using System.Threading.Tasks;
using UnityMemoryMappedFile;

namespace VMC.Plugin
{
    /// <summary>
    /// コントロールパネル(WPF)との通信。
    ///
    /// プラグイン独自のコマンドはプラグイン側のDLLに定義し、IVMCPlugin.CommandTypes で
    /// 登録する。対応付けは型の単純名で行われるので、コントロールパネル側のプラグインと
    /// 同じ名前・同じ名前空間の型を用意すること(共有ソースをリンクするのが確実)。
    /// </summary>
    public interface IPluginIpc
    {
        /// <summary>
        /// コントロールパネルからコマンドを受信したときに呼ばれる。
        /// Unityのメインスレッドとは限らないため、Unity APIを触る場合は Post を使うこと。
        /// </summary>
        event EventHandler<DataReceivedEventArgs> Received;

        /// <summary>コントロールパネルへコマンドを送る。応答を返す場合は requestId を指定する。</summary>
        Task SendCommandAsync(object command, string requestId = null);

        /// <summary>Unityのメインスレッドで処理を実行する</summary>
        void Post(Action action);
    }
}
