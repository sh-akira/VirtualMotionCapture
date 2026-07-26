using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public class MemoryMappedFileBase : IDisposable
    {
        private const long capacity = 104857600L;

        private MemoryMappedFile receiver;
        private MemoryMappedViewAccessor receiverAccessor;

        private MemoryMappedFile sender;
        private MemoryMappedViewAccessor senderAccessor;

        private CancellationTokenSource readCts;

        private string currentPipeName = null;

        public EventHandler<DataReceivedEventArgs> ReceivedEvent;

        public bool IsConnected = false;

        /// <summary>Stop()済みか。破棄後のアクセサに触らないための番人</summary>
        private volatile bool stopped = false;

        protected async void StartInternal(string pipeName, bool isServer)
        {
            currentPipeName = pipeName;
            readCts = new CancellationTokenSource();
            if (isServer)
            {
                receiver = MemoryMappedFile.CreateOrOpen(pipeName + "_receiver", capacity);
                sender = MemoryMappedFile.CreateOrOpen(pipeName + "_sender", capacity);
            }
            else
            {
                while (true)
                {
                    try
                    {
                        receiver = MemoryMappedFile.OpenExisting(pipeName + "_sender"); //サーバーと逆方向
                        sender = MemoryMappedFile.OpenExisting(pipeName + "_receiver"); //サーバーと逆方向
                        break;
                    }
                    catch (System.IO.FileNotFoundException) { }
                    if (readCts.Token.IsCancellationRequested) return;
                    await Task.Delay(100);
                }
            }
            receiverAccessor = receiver.CreateViewAccessor();
            senderAccessor = sender.CreateViewAccessor();
            var t = Task.Run(() => ReadThread());
            IsConnected = true;
        }

        public async void ReadThread()
        {
            try
            {
                while (true)
                {
                    while (receiverAccessor != null && receiverAccessor?.ReadByte(0) != 1)
                    {
                        if (readCts.Token.IsCancellationRequested) return;
                        Thread.Sleep(1);// await Task.Delay(1);
                    }
                    if (receiverAccessor == null) {
                        break;
                    }
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread DataReceived");
                    //相手からデータが来た=生きているので、送信待ちの打ち切り状態を解除する
                    peerNotResponding = false;
                    long position = 1;
                    //CommandType
                    var length = receiverAccessor.ReadInt32(position);
                    position += sizeof(int);
                    var typeNameArray = new byte[length];
                    receiverAccessor.ReadArray(position, typeNameArray, 0, typeNameArray.Length);
                    position += typeNameArray.Length;
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread GetCommandType");
                    //RequestID
                    length = receiverAccessor.ReadInt32(position);
                    position += sizeof(int);
                    var requestIdArray = new byte[length];
                    receiverAccessor.ReadArray(position, requestIdArray, 0, requestIdArray.Length);
                    position += requestIdArray.Length;
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread GetRequestID");
                    //Data
                    length = receiverAccessor.ReadInt32(position);
                    position += sizeof(int);
                    var dataArray = new byte[length];
                    receiverAccessor.ReadArray(position, dataArray, 0, dataArray.Length);
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread GetData");
                    //Write finish flag
                    receiverAccessor.Write(0, (byte)0);
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread Write finish flag");

                    var commandType = PipeCommands.GetCommandType(Encoding.UTF8.GetString(typeNameArray));
                    var requestId = Encoding.UTF8.GetString(requestIdArray);
                    var data = BinarySerializer.Deserialize(dataArray, commandType);
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread Parsed Type:{commandType.Name} requestId = {requestId}");
                    if (WaitReceivedDictionary.ContainsKey(requestId))
                    {
                        System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread ContainsKey");
                        WaitReceivedDictionary[requestId] = data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase ReadThread Raise Event");
                        ReceivedEvent?.Invoke(this, new DataReceivedEventArgs(commandType, requestId, data));
                    }

                }
            }
            catch (NullReferenceException) { }
            //Stop()と競合してアクセサが破棄された場合。
            //ここで例外が外へ抜けると読み取りスレッドが死に、
            //以降、相手からの完了フラグが二度とクリアされなくなって送信側が固まる。
            catch (ObjectDisposedException) { }
        }

        protected ConcurrentDictionary<string, object> WaitReceivedDictionary = new ConcurrentDictionary<string, object>();

        private AsyncLock SendLock = new AsyncLock();

        public async Task<string> SendCommandAsync(object command, string requestId = null, bool needWait = false, int timeoutMs = DefaultSendTimeoutMs)
        {
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommandAsync WaitLock [{command.GetType().Name}]");
            return await Task.Run(async () =>
            {
                using (await SendLock.LockAsync())
                {
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommandAsync EnterLock [{command.GetType().Name}]");
                    return SendCommand(command, requestId, needWait, timeoutMs);
                }
            });
        }

        /// <summary>
        /// 相手が完了フラグをクリアするのを待つ上限(ミリ秒)。
        /// 相手(コントロールパネル)が落ちている・応答しない場合に、
        /// ここで無限に待つと同期呼び出し元(OnApplicationQuit等)ごと固まってしまう。
        /// </summary>
        public const int DefaultSendTimeoutMs = 3000;

        /// <summary>相手が応答しないと判断した状態。以降の送信は即座に諦める(相手から受信すると解除)</summary>
        private volatile bool peerNotResponding = false;

        public string SendCommand(object command, string requestId = null, bool needWait = false, int timeoutMs = DefaultSendTimeoutMs)
        {
            try
            {
                return SendCommandInternal(command, requestId, needWait, timeoutMs);
            }
            catch (ObjectDisposedException)
            {
                //Stop()と競合してアクセサが破棄された。終了処理中なので送信を諦める
                System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand Disposed [{command.GetType().Name}]");
                return null;
            }
            catch (NullReferenceException)
            {
                //Stop()と競合してアクセサがnullになった。同上
                System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand Closed [{command.GetType().Name}]");
                return null;
            }
        }

        private string SendCommandInternal(object command, string requestId, bool needWait, int timeoutMs)
        {
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand Enter [{command.GetType().Name}]");
            if (IsConnected == false) return null;
            if (stopped) return null;
            //一度タイムアウトした相手には、毎回待たされないように即座に諦める
            if (peerNotResponding) return null;
            if (string.IsNullOrEmpty(requestId)) requestId = Guid.NewGuid().ToString();
            var typeNameArray = Encoding.UTF8.GetBytes(command.GetType().Name);
            var requestIdArray = Encoding.UTF8.GetBytes(requestId);
            var dataArray = BinarySerializer.Serialize(command);
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand StartWait [{command.GetType().Name}]");
            var waitStartTick = Environment.TickCount;
            while (senderAccessor != null && senderAccessor.ReadByte(0) == 1) // Wait finish flag
            {
                if (readCts.Token.IsCancellationRequested) return null;
                if (stopped) return null; //待っている間に終了処理が走った
                //相手が受信していない場合に無限に待たない。
                //待ち続けると、同期でSendCommandを呼ぶ箇所(OnApplicationQuit等)でアプリが固まる
                if (unchecked(Environment.TickCount - waitStartTick) > timeoutMs)
                {
                    peerNotResponding = true;
                    System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand Timeout [{command.GetType().Name}]");
                    return null;
                }
                Thread.Sleep(1);// await Task.Delay(1);
            }
            //Need to wait requestID before send (because sometime return data very fast)
            if (needWait) WaitReceivedDictionary.TryAdd(requestId, null);
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand EndWait [{command.GetType().Name}]");
            long position = 1;
            //CommandType
            senderAccessor.Write(position, typeNameArray.Length);
            position += sizeof(int);
            senderAccessor.WriteArray(position, typeNameArray, 0, typeNameArray.Length);
            position += typeNameArray.Length;
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand WriteCommandType [{command.GetType().Name}]");
            //RequestID
            senderAccessor.Write(position, requestIdArray.Length);
            position += sizeof(int);
            senderAccessor.WriteArray(position, requestIdArray, 0, requestIdArray.Length);
            position += requestIdArray.Length;
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand WriteRequestID [{command.GetType().Name}]");
            //Data
            senderAccessor.Write(position, dataArray.Length);
            position += sizeof(int);
            senderAccessor.WriteArray(position, dataArray, 0, dataArray.Length);
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand WriteData [{command.GetType().Name}]");
            //Write finish flag
            senderAccessor.Write(0, (byte)1);
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommand Write finish flag [{command.GetType().Name}]");

            return requestId;
        }

        public async Task SendCommandWaitAsync(object command, Action<object> returnAction)
        {
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommandWaitAsync Enter [{command.GetType().Name}]");
            var requestId = await SendCommandAsync(command, null, true);
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommandWaitAsync Return SendCommandAsync [{command.GetType().Name}] id:{requestId}");
            if (requestId == null) return;
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommandWaitAsync StartWait [{command.GetType().Name}]");
            while (WaitReceivedDictionary[requestId] == null)
            {
                await Task.Delay(10);
            }
            System.Diagnostics.Debug.WriteLine($"MemoryMappedFileBase SendCommandWaitAsync WaitEnd [{command.GetType().Name}]");
            object value; //・・・・
            WaitReceivedDictionary.TryRemove(requestId, out value);
            returnAction(value);
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            IsConnected = false;
            readCts?.Cancel();

            //参照を外してからDisposeする。
            //読み書きスレッドは「nullチェック → アクセス」の順で触るため、
            //Disposeしてからnullにすると、その隙間で
            //ObjectDisposedException(Cannot access a closed accessor)が発生する。
            var closingReceiverAccessor = receiverAccessor;
            var closingSenderAccessor = senderAccessor;
            var closingReceiver = receiver;
            var closingSender = sender;
            receiverAccessor = null;
            senderAccessor = null;
            receiver = null;
            sender = null;

            closingReceiverAccessor?.Dispose();
            closingSenderAccessor?.Dispose();
            closingReceiver?.Dispose();
            closingSender?.Dispose();
        }


        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}