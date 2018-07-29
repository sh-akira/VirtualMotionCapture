using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace UnityNamedPipe
{
    public class NamedPipeServer : NamedPipeBase
    {
        private string currentPipeName = null;

        public async void Start(string pipeName)
        {
            currentPipeName = pipeName;
            NamedPipeServerStream serverStream = null;
            NamedPipeClientStream clientStream = null;
            while (DoStop == false) //切断時エラーで抜けるので次の接続のために再試行
            {
                try
                {
                    //初期化
                    serverStream = new NamedPipeServerStream(pipeName, PipeDirection.In, 1); //サーバー数1

                    await Task.Run(() => serverStream.WaitForConnection()); //接続が来るまで待つ UnityのMonoはAsync使えない

                    clientStream = new NamedPipeClientStream(".", pipeName + "receive", PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.None); //UnityのMonoはImpersonation使えない
                    try
                    {
                        clientStream.Connect(500); //UnityのMonoはAsync使えない
                    }
                    catch (TimeoutException) { }
                    if (clientStream.IsConnected == false) continue;

                    namedPipeReceiveStream = serverStream;
                    namedPipeSendStream = clientStream;

                    await RunningAsync();

                }
                finally
                {
                    if (serverStream != null && serverStream.IsConnected) serverStream.Disconnect();
                    serverStream?.Close();
                    clientStream?.Close();
                }
            }
        }

        private bool DoStop = false;

        public void Stop()
        {
            if (string.IsNullOrEmpty(currentPipeName)) return;
            DoStop = true;
            if (namedPipeReceiveStream != null && namedPipeReceiveStream.IsConnected)
            {
                namedPipeReceiveStream.Close();
                if (namedPipeSendStream != null && namedPipeSendStream.IsConnected)
                {
                    namedPipeSendStream.Close();
                }
                //ダミーで待機中のサーバーにつないで、切断することで待機を終わらせる
                using (var client = new NamedPipeClientStream(".", currentPipeName, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.None))
                {
                    try
                    {
                        client.Connect(100);
                    }
                    catch (Exception) { }
                }
            }
            else
            {
                //ダミーで待機中のサーバーにつないで、切断することで待機を終わらせる
                using (var client = new NamedPipeClientStream(".", currentPipeName, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.None))
                {
                    try
                    {
                        client.Connect(100);
                    }
                    catch (Exception) { }
                }
                namedPipeReceiveStream.Close();
                namedPipeSendStream.Close();
                //ダミーで待機中のサーバーにつないで、切断することで待機を終わらせる
                using (var client = new NamedPipeClientStream(".", currentPipeName, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.None))
                {
                    try
                    {
                        client.Connect(100);
                    }
                    catch (Exception) { }
                }
            }
        }
    }
}
