using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// VMCProtocolの「送信側アプリ」が送ってくるOSCメッセージを組み立てる。
    /// 実機のVR機器の代わりにこれを流し込むことで、HMD/コントローラ/トラッカーの動きを再現する。
    /// アドレスと引数の並びは protocol.vmc.info の仕様に合わせている。
    /// </summary>
    public static class VMCTestOscBuilder
    {
        public static uOSC.Message Hmd(string serial, Vector3 position, Quaternion rotation)
            => Transform("/VMC/Ext/Hmd/Pos", serial, position, rotation);

        public static uOSC.Message Controller(string serial, Vector3 position, Quaternion rotation)
            => Transform("/VMC/Ext/Con/Pos", serial, position, rotation);

        public static uOSC.Message Tracker(string serial, Vector3 position, Quaternion rotation)
            => Transform("/VMC/Ext/Tra/Pos", serial, position, rotation);

        public static uOSC.Message Bone(string boneName, Vector3 localPosition, Quaternion localRotation)
            => Transform("/VMC/Ext/Bone/Pos", boneName, localPosition, localRotation);

        public static uOSC.Message Root(Vector3 position, Quaternion rotation)
            => Transform("/VMC/Ext/Root/Pos", "root", position, rotation);

        private static uOSC.Message Transform(string address, string name, Vector3 position, Quaternion rotation)
        {
            //受信側は values[n] is float で型チェックしているため、必ずfloatとしてボックス化する
            return new uOSC.Message(address, name,
                position.x, position.y, position.z,
                rotation.x, rotation.y, rotation.z, rotation.w);
        }

        public static uOSC.Message BlendShapeValue(string name, float value)
            => new uOSC.Message("/VMC/Ext/Blend/Val", name, value);

        public static uOSC.Message BlendShapeApply()
            => new uOSC.Message("/VMC/Ext/Blend/Apply");

        /// <summary>外部アイトラッキング。位置は頭ボーンからの相対位置</summary>
        public static uOSC.Message Eye(bool enable, Vector3 localPositionFromHead)
            => new uOSC.Message("/VMC/Ext/Set/Eye", enable ? 1 : 0,
                localPositionFromHead.x, localPositionFromHead.y, localPositionFromHead.z);

        /// <summary>表情の一括送信(Val×n + Apply)</summary>
        public static IEnumerable<uOSC.Message> BlendShapes(IEnumerable<KeyValuePair<string, float>> values)
        {
            foreach (var pair in values)
            {
                yield return BlendShapeValue(pair.Key, pair.Value);
            }
            yield return BlendShapeApply();
        }
    }

    /// <summary>
    /// 組み立てたOSCメッセージをExternalReceiverForVMCへ流し込む。
    /// uOscServerのonDataReceivedをそのまま叩くため、UDPの到着タイミングに左右されず
    /// フレーム単位で決定論的に再現できる(実機VR機器もネットワークも不要)。
    /// </summary>
    public static class VMCTestOscInjector
    {
        public static void Inject(ExternalReceiverForVMC receiver, uOSC.Message message)
        {
            var server = receiver.GetComponent<uOSC.uOscServer>();
            if (server == null || server.onDataReceived == null)
            {
                Debug.LogError("[VMCTest] uOscServerが見つかりません");
                return;
            }
            server.onDataReceived.Invoke(message);
        }

        public static void Inject(ExternalReceiverForVMC receiver, IEnumerable<uOSC.Message> messages)
        {
            foreach (var message in messages)
            {
                Inject(receiver, message);
            }
        }
    }

    /// <summary>
    /// ExternalSenderの送信内容をキャプチャする。
    /// Bundleは実際にUDPへ書き出すのと同じバイト列にシリアライズしてから
    /// uOSCのParserで読み戻すため、OSCのエンコード/デコードも含めて検証できる。
    /// </summary>
    public sealed class VMCTestSendCapture : IDisposable
    {
        private readonly List<uOSC.Message> messages = new List<uOSC.Message>();
        private readonly uOSC.Parser parser = new uOSC.Parser();
        private bool disposed;

        public IReadOnlyList<uOSC.Message> Messages => messages;

        public VMCTestSendCapture()
        {
            ExternalSender.SendHook += OnSend;
        }

        public void Clear()
        {
            messages.Clear();
        }

        private void OnSend(object packet)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    if (packet is uOSC.Bundle bundle)
                    {
                        bundle.Write(stream);
                    }
                    else if (packet is uOSC.Message message)
                    {
                        message.Write(stream);
                    }
                    else
                    {
                        return;
                    }

                    var buffer = stream.ToArray();
                    if (buffer.Length == 0) return;
                    int position = 0;
                    parser.Parse(buffer, ref position, buffer.Length);
                }

                while (parser.messageCount > 0)
                {
                    messages.Add(parser.Dequeue());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VMCTest] 送信キャプチャに失敗しました: {ex}");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ExternalSender.SendHook -= OnSend;
        }
    }
}
