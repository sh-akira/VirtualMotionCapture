using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// Virtual Motion Tracker(VMT)への送信。
    ///
    /// VMTドライバが無くても、送信内容をフックで捕まえれば
    /// 「有効化したら送るか」「無効化したら停止パケットを送るか」
    /// 「トラッカー番号と姿勢が正しいか」を確認できる。
    /// </summary>
    public sealed class Scenario_VMTSend : VMCTestScenario
    {
        public override string Name => "VMTSend";

        public override string Description => "Virtual Motion Trackerへの送信内容";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        private const int TrackerNo = 3;

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var vmt = context.Window.vmtClient;
            if (vmt == null)
            {
                result.CheckThat("VMTClientの参照", false, "ControlWPFWindow.vmtClient が設定されていません");
                yield break;
            }

            var captured = new List<(string Address, object[] Values)>();
            Action<string, object[]> hook = (address, values) => captured.Add((address, values));
            VMTClient.SendHook += hook;

            try
            {
                //--- 2. 無効の間は何も送らない ---
                context.Log("2. 無効時は送信しないこと");
                vmt.SetEnable(false);
                yield return context.Step(5);
                captured.Clear();
                yield return context.Step(10);

                result.CheckThat("VMT無効時",
                    captured.Count == 0,
                    $"VMTが無効なのに {captured.Count} 件送信されています: " +
                    string.Join(", ", captured.Select(d => d.Address).Distinct()));

                //--- 3. 有効にすると毎フレーム送る ---
                context.Log("3. 有効時の送信");
                vmt.SetNo(TrackerNo);
                vmt.SetEnable(true);
                yield return context.Step(3);
                captured.Clear();
                yield return context.Step(5);

                var roomMessages = captured.Where(d => d.Address == "/VMT/Room/Unity").ToList();
                result.CheckThat("VMT有効時の送信",
                    roomMessages.Count > 0,
                    "VMTを有効にしても /VMT/Room/Unity が送信されていません");

                if (roomMessages.Count > 0)
                {
                    var values = roomMessages.Last().Values;
                    result.CheckThat("VMTの引数",
                        values.Length == 10
                        && values[0] is int no && no == TrackerNo
                        && values[1] is int enable && enable == 1,
                        $"VMTの引数が想定と違います(数={values.Length} " +
                        $"no={(values.Length > 0 ? values[0] : null)} enable={(values.Length > 1 ? values[1] : null)} " +
                        $"期待 no={TrackerNo} enable=1)");

                    //送っている姿勢がControlCameraのローカル姿勢と一致するか
                    var camera = CameraManager.Current.ControlCamera.transform;
                    if (values.Length == 10)
                    {
                        var sentPosition = new Vector3((float)values[3], (float)values[4], (float)values[5]);
                        var sentRotation = new Quaternion((float)values[6], (float)values[7], (float)values[8], (float)values[9]);
                        result.CheckThat("VMTの姿勢",
                            Vector3.Distance(sentPosition, camera.localPosition) < 0.01f
                            && Quaternion.Angle(sentRotation, camera.localRotation) < 1f,
                            $"VMTに送っている姿勢がカメラと一致しません(送信 {sentPosition} / カメラ {camera.localPosition})");
                    }
                }

                //--- 4. 無効にすると停止パケットを1回送る ---
                context.Log("4. 無効化時の停止パケット");
                captured.Clear();
                vmt.SetEnable(false);
                yield return context.Step(5);

                var disableMessages = captured.Where(d => d.Address == "/VMT/Room/Unity").ToList();
                result.CheckThat("VMT無効化の通知",
                    disableMessages.Count == 1
                    && disableMessages[0].Values.Length == 10
                    && disableMessages[0].Values[1] is int off && off == 0,
                    $"無効化したときに enable=0 の停止パケットが1回だけ送られていません" +
                    $"({disableMessages.Count}件)");

                //無効化後は送信が止まること
                captured.Clear();
                yield return context.Step(10);
                result.CheckThat("VMT無効化後の停止",
                    captured.Count == 0,
                    $"無効化した後も {captured.Count} 件送信され続けています");

                //--- 5. トラッカー番号の変更 ---
                context.Log("5. トラッカー番号の変更");
                vmt.SetNo(7);
                vmt.SetEnable(true);
                yield return context.Step(3);
                captured.Clear();
                yield return context.Step(5);

                var renumbered = captured.Where(d => d.Address == "/VMT/Room/Unity").ToList();
                result.CheckThat("トラッカー番号の変更",
                    renumbered.Count > 0 && renumbered.Last().Values[0] is int newNo && newNo == 7,
                    $"トラッカー番号の変更が反映されていません(" +
                    $"{(renumbered.Count > 0 ? renumbered.Last().Values[0] : null)} 期待 7)");

                vmt.SetEnable(false);
                yield return context.Step(3);
            }
            finally
            {
                VMTClient.SendHook -= hook;
                vmt.SetEnable(false);
            }
        }
    }
}
