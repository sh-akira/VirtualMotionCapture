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
    public class NamedPipeClient : NamedPipeBase
    {
        public void Start(string pipeName)
        {
            var t = Task.Run(async () =>
            {
                NamedPipeClientStream clientStream = null;
                NamedPipeServerStream serverStream = null;
                while (DoStop == false) //切断時エラーで抜けるので次の接続のために再試行
                {
                    try
                    {
                        //初期化
                        clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.None); //UnityのMonoはImpersonation使えない
                        serverStream = new NamedPipeServerStream(pipeName + "receive", PipeDirection.In, 1); //サーバー数1
                        try
                        {
                            clientStream.Connect(500); //UnityのMonoはAsync使えない
                        }
                        catch (TimeoutException) { }
                        if (clientStream.IsConnected == false) continue;
                        serverStream.WaitForConnection(); //接続が来るまで待つ UnityのMonoはAsync使えない


                        namedPipeReceiveStream = serverStream;
                        namedPipeSendStream = clientStream;

                        await RunningAsync();
                    }
                    finally
                    {
                        if (serverStream != null && serverStream.IsConnected) serverStream.Disconnect();
                        serverStream?.Close();
                        serverStream?.Dispose();
                        clientStream?.Close();
                        clientStream?.Dispose();
                    }
                }
            });
        }

        private bool DoStop = false;

        public bool IsConnected { get { return namedPipeReceiveStream != null && namedPipeReceiveStream.IsConnected; } }

        public void Stop()
        {
            DoStop = true;
            //if (namedPipeReceiveStream != null && namedPipeReceiveStream.IsConnected) namedPipeReceiveStream.Disconnect();
            namedPipeReceiveStream?.Close();
            namedPipeReceiveStream?.Dispose();
            namedPipeSendStream?.Close();
            namedPipeSendStream?.Dispose();
        }
    }
}
