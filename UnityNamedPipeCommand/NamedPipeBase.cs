using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UnityNamedPipe
{
    public class NamedPipeBase
    {
        protected NamedPipeClientStream namedPipeSendStream;
        protected NamedPipeServerStream namedPipeReceiveStream;

        public bool IsConnected = false;

        public EventHandler<DataReceivedEventArgs> ReceivedEvent;

        protected async Task RunningAsync()
        {
            IsConnected = true;
            bool isCommandName = true;
            Type commandType = null;
            string requestId = null;
            try
            {
                //受信
                while (IsConnected)
                {
                    if (isCommandName)
                    {//1つ目の通信はコマンドの種別とID
                        isCommandName = false;
                        var items = (await ReadString(namedPipeReceiveStream)).Split('|');
                        commandType = PipeCommands.GetCommandType(items[0]);
                        requestId = items[1];
                    }
                    else
                    {//2つ目の通信はコマンドの中身のバイナリ
                        isCommandName = true;
                        var lengthBytes = new byte[4];
                        await namedPipeReceiveStream.ReadAsync(lengthBytes, 0, 4);
                        var length = BitConverter.ToInt32(lengthBytes, 0);
                        var dataBytes = new byte[length];
                        await namedPipeReceiveStream.ReadAsync(dataBytes, 0, length);
                        var data = BinarySerializer.Deserialize(dataBytes, commandType);
                        if (WaitReceivedDictionary.ContainsKey(requestId))
                        {
                            WaitReceivedDictionary[requestId] = data;
                        }
                        else
                        {
                            ReceivedEvent?.Invoke(this, new DataReceivedEventArgs(commandType, requestId, data));
                        }
                    }
                }
            }
            catch (Exception)
            {
                //エラー発生時はそのまま抜ける
            }
        }

        protected ConcurrentDictionary<string, object> WaitReceivedDictionary = new ConcurrentDictionary<string, object>();

        private AsyncLock SendLock = new AsyncLock();

        public async Task<string> SendCommandAsync(object command, string requestId = null)
        {
            using (await SendLock.LockAsync())
            {
                if (namedPipeSendStream == null) return null;
                namedPipeSendStream.WaitForPipeDrain();
                if (string.IsNullOrEmpty(requestId)) requestId = Guid.NewGuid().ToString();
                //sendType
                await WriteStringAsync(namedPipeSendStream, $"{command.GetType().Name}|{requestId}");
                //sendCommand
                var data = BinarySerializer.Serialize(command);
                var lengthBytes = BitConverter.GetBytes(data.Length);
                await namedPipeSendStream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                await namedPipeSendStream.WriteAsync(data, 0, data.Length);
                namedPipeSendStream.WaitForPipeDrain();
                return requestId;
            }
        }

        public async Task SendCommandWaitAsync(object command, Action<object> returnAction)
        {
            var requestId = await SendCommandAsync(command);
            if (requestId == null) return;
            WaitReceivedDictionary.TryAdd(requestId, null);
            while (WaitReceivedDictionary[requestId] == null)
            {
                await Task.Delay(10);
            }
            object value; //・・・・
            WaitReceivedDictionary.TryRemove(requestId, out value);
            returnAction(value);
        }

        private async Task<string> ReadString(Stream stream)
        {
            byte[] inOneBuffer = new byte[1];
            await stream.ReadAsync(inOneBuffer, 0, 1);
            int len = inOneBuffer[0] * 256;
            await stream.ReadAsync(inOneBuffer, 0, 1);
            len += inOneBuffer[0];
            byte[] inBuffer = new byte[len];
            await stream.ReadAsync(inBuffer, 0, len);
            return Encoding.UTF8.GetString(inBuffer);
        }

        private async Task WriteStringAsync(Stream stream, string text)
        {
            byte[] outBuffer = Encoding.UTF8.GetBytes(text);
            int len = outBuffer.Length;
            byte[] outTwoBuffer = new byte[2]
                {
                (byte)(len / 256),
                (byte)(len & 255)
            };
            await stream.WriteAsync(outTwoBuffer, 0, 2);
            await stream.WriteAsync(outBuffer, 0, len);
        }
    }
}
