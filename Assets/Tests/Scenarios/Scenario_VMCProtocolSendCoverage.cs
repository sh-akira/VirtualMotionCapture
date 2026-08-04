using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// VMCProtocolの送信網羅。
    /// ExternalSenderが送りうる全アドレスが実際に送信されているかを確認する。
    ///
    /// ボーンや表情のように「値が正しいか」は他のシナリオで見ているので、
    /// ここは「そもそも送られているか」だけを見る。
    /// (送信されていないメッセージは、受信側VMCが自分の値を使い続けるので気付きにくい)
    /// </summary>
    public sealed class Scenario_VMCProtocolSendCoverage : VMCTestScenario
    {
        public override string Name => "VMCProtocolSendCoverage";

        public override string Description => "ExternalSenderが送りうる全アドレスが実際に送信されるか";

        //モデルとトラッカーがあれば毎フレーム送られるもの
        private static readonly string[] PerFrameAddresses =
        {
            "/VMC/Ext/OK",
            "/VMC/Ext/T",
            "/VMC/Ext/Root/Pos",
            "/VMC/Ext/Bone/Pos",
            "/VMC/Ext/Blend/Val",
            "/VMC/Ext/Blend/Apply",
            "/VMC/Ext/Cam",
            "/VMC/Ext/Hmd/Pos",
            "/VMC/Ext/Hmd/Pos/Local",
            "/VMC/Ext/Con/Pos",
            "/VMC/Ext/Con/Pos/Local",
            "/VMC/Ext/Tra/Pos",
            "/VMC/Ext/Tra/Pos/Local",
        };

        //低頻度(1秒間隔 / 要求時に即時)で送られるもの
        private static readonly string[] LowRateAddresses =
        {
            "/VMC/Ext/Rcv",
            "/VMC/Ext/Light",
            "/VMC/Ext/Setting/Color",
            "/VMC/Ext/Setting/Win",
            "/VMC/Ext/Config",
            "/VMC/Ext/Opt",
            "/VMC/Ext/VRM",
        };

        //入力イベントの発生時に送られるもの
        private static readonly string[] InputAddresses =
        {
            "/VMC/Ext/Con",
            "/VMC/Ext/Key",
            "/VMC/Ext/Midi/Note",
            "/VMC/Ext/Midi/CC/Val",
            "/VMC/Ext/Midi/CC/Bit",
        };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            var vrmPath = context.Config.GetModelPath(context.ModelKey);

            context.Log("1. VRM読み込みとトラッカー受信");
            context.ResetSettings();
            yield return context.LoadModel(vrmPath);

            var receiver = context.CreateReceiver(setting => setting.ApplyTracker = true);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();

            //--- 2. 毎フレーム送信の網羅 ---
            //カメラは意図的に触らない。ここで /VMC/Ext/Cam が出ないなら、
            //送信側がカメラを掴めていない(Start()の実行順に依存する不具合)
            context.Log("2. 毎フレーム送信の確認");
            context.EnableSender();
            yield return context.Step(3);
            context.ClearSent();
            yield return context.Step(6);

            CheckAddresses(context, result, "毎フレーム送信", PerFrameAddresses);

            //--- 3. 低頻度送信の網羅 ---
            context.Log("3. 低頻度送信の確認");
            //VRMのメタ情報を通知する(/VMC/Ext/VRM の送信条件)
            var metaTask = context.Window.LoadVRMMetaAsync(vrmPath);
            yield return context.Await(metaTask);
            context.Window.VRMmetaLoadedAction?.Invoke(metaTask.Result);
            context.Sender.optionString = "VMCTest_Option";
            yield return context.Step(2);

            context.ClearSent();
            context.Sender.SendPerLowRate(); //即時送信を要求
            yield return context.Step(2);

            CheckAddresses(context, result, "低頻度送信", LowRateAddresses);

            //--- 4. 入力イベント送信の網羅 ---
            context.Log("4. 入力イベント送信の確認");
            context.ClearSent();

            SteamVR2Input.Instance.KeyDownEvent?.Invoke(this,
                new OVRKeyEventArgs("VMCTestButton", new Vector3(0.1f, 0.2f, 0.3f), true, false, false));
            KeyboardAction.KeyDownEvent?.Invoke(this, new KeyboardEventArgs(65));
            context.Window.midiCCWrapper.noteOnDelegateProxy?.Invoke(MidiChannel.Ch1, 60, 0.8f);
            context.Window.midiCCWrapper.knobUpdateFloatDelegate?.Invoke(3, 0.5f);
            context.Window.midiCCWrapper.knobUpdateBoolDelegate?.Invoke(4, true);
            yield return context.Step(3);

            CheckAddresses(context, result, "入力イベント送信", InputAddresses);

            context.DisableSender();
        }

        private static void CheckAddresses(VMCTestContext context, VMCTestResult result, string label, string[] expected)
        {
            var actual = new HashSet<string>(context.SendCapture.Messages.Select(d => d.address));
            var missing = expected.Where(d => actual.Contains(d) == false).ToList();

            Debug.Log($"[VMCTest] {label}: 送信された {actual.Count} 種類 / 期待 {expected.Length} 種類\n" +
                      $"  実際: {string.Join(", ", actual.OrderBy(d => d, System.StringComparer.Ordinal))}");

            result.CheckThat($"{label}の網羅",
                missing.Count == 0,
                $"送信されていないアドレスがあります: {string.Join(", ", missing)}");
        }
    }
}
