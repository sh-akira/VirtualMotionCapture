using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// 異常系。壊れた入力を与えても落ちないことを確認する。
    ///
    /// VMCProtocolは他アプリからも送られてくるので、
    /// 引数の数や型が仕様と違うメッセージが届くことは普通にある。
    /// 例外を投げると、そのフレーム以降の受信処理が止まったり、
    /// uOSCの受信スレッドごと死んだりする。
    /// </summary>
    public sealed class Scenario_Robustness : VMCTestScenario
    {
        public override string Name => "Robustness";

        public override string Description => "壊れたOSC・不正なファイルを与えても落ちないか";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込みと受信機の用意");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var receiver = context.CreateReceiver(setting =>
            {
                setting.ApplyTracker = true;
                setting.ApplyBlendShape = true;
                setting.ApplyLookAt = true;
                setting.ApplyCamera = true;
                setting.ApplyLight = true;
                setting.ApplySetting = true;
                setting.ApplyControl = true;
                setting.ApplyStatus = true;
                setting.ApplyMidi = true;
                setting.ApplyControllerInput = true;
                setting.ApplyKeyboardInput = true;
            });
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            var healthySnapshot = context.Capture("01_before_malformed", includeSent: false);

            //--- 2. 壊れたメッセージを投げ込む ---
            context.Log("2. 壊れたOSCメッセージの注入");
            var malformed = BuildMalformedMessages();

            context.BeginErrorCapture();
            foreach (var message in malformed)
            {
                context.Inject(receiver, message);
                //1件ごとにフレームを進めて、遅延処理まで含めて確認する
                yield return context.Step(1);
            }
            yield return context.Step(10);
            var errors = context.EndErrorCapture();

            result.CheckThat("壊れたOSCで落ちないこと",
                errors.Count == 0,
                $"壊れたメッセージ{malformed.Count}件で {errors.Count}件のエラー/例外が出ました:\n        " +
                string.Join("\n        ", errors.GetRange(0, Mathf.Min(10, errors.Count))));

            //--- 3. 壊れた入力の後も正常に動くか ---
            context.Log("3. 復帰の確認");
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            yield return context.Step(20);

            var afterSnapshot = context.Capture("02_after_malformed", includeSent: false);
            var delta = VMCTestSnapshot.MaxBoneRotationDelta(healthySnapshot, afterSnapshot, out var movedBone);
            result.CheckThat("壊れた入力の後の復帰",
                delta > 15f,
                $"壊れたメッセージを受けた後、トラッキングが止まっています(最大回転差 {delta:F2}度 / {movedBone ?? "なし"})");

            //--- 4. 存在しない/壊れたVRMの読み込み ---
            context.Log("4. 不正なVRMファイルの読み込み");
            var brokenPath = context.OutputPath($"{Name}.broken.vrm");
            System.IO.File.WriteAllBytes(brokenPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

            var modelBefore = context.CurrentModel;
            var loadFailed = false;
            var loadTask = context.Window.ImportVRM(brokenPath);
            while (loadTask.IsCompleted == false) yield return context.Step(1);
            if (loadTask.IsFaulted) loadFailed = true;
            yield return context.Step(10);

            //壊れたVRMは読み込めなくてよいが、アプリが壊れてはいけない
            result.CheckThat("壊れたVRMで落ちないこと",
                context.CurrentModel != null,
                $"壊れたVRMを読み込もうとしてモデルが失われました(例外={loadFailed})");

            result.CheckThat("壊れたVRMで元のモデルが残ること",
                context.CurrentModel == modelBefore,
                "壊れたVRMの読み込みで、元のアバターが破棄されてしまいました");

            //--- 5. 存在しないパスの設定ファイル読み込み ---
            context.Log("5. 存在しない設定ファイルの読み込み");
            context.BeginErrorCapture();
            context.Inject(receiver, new uOSC.Message("/VMC/Ext/Set/Config", context.OutputPath("does_not_exist.json")));
            yield return context.Step(10);
            var configErrors = context.EndErrorCapture();

            result.CheckThat("存在しない設定ファイルで落ちないこと",
                configErrors.Count == 0,
                $"存在しない設定ファイルの指定で {configErrors.Count}件のエラーが出ました: " +
                string.Join(" / ", configErrors.GetRange(0, Mathf.Min(5, configErrors.Count))));
        }

        /// <summary>
        /// 仕様から外れたメッセージ。引数不足・型違い・想定外の値・未知のアドレス。
        /// </summary>
        private static List<uOSC.Message> BuildMalformedMessages()
        {
            var longString = new string('X', 4096);
            return new List<uOSC.Message>
            {
                //引数がまったく無い
                new uOSC.Message("/VMC/Ext/Hmd/Pos"),
                new uOSC.Message("/VMC/Ext/Bone/Pos"),
                new uOSC.Message("/VMC/Ext/Blend/Val"),
                new uOSC.Message("/VMC/Ext/Cam"),
                new uOSC.Message("/VMC/Ext/Set/Eye"),
                new uOSC.Message("/VMC/Ext/Set/Period"),
                new uOSC.Message("/VMC/Ext/Set/Res"),
                new uOSC.Message("/VMC/Ext/Light"),
                new uOSC.Message("/VMC/Ext/Con"),
                new uOSC.Message("/VMC/Ext/Key"),
                new uOSC.Message("/VMC/Ext/Midi/CC/Val"),
                new uOSC.Message("/VMC/Ext/OK"),
                new uOSC.Message("/VMC/Ext/Root/Pos"),
                new uOSC.Message("/VMC/Ext/Set/Calib/Exec"),

                //引数が足りない
                new uOSC.Message("/VMC/Ext/Hmd/Pos", "name", 1.0f),
                new uOSC.Message("/VMC/Ext/Bone/Pos", "Head", 0f, 0f),
                new uOSC.Message("/VMC/Ext/Cam", "Camera", 0f, 0f, 0f),
                new uOSC.Message("/VMC/Ext/Set/Period", 1, 1),
                new uOSC.Message("/VMC/Ext/Light", "Light", 0f),

                //型が違う
                new uOSC.Message("/VMC/Ext/Hmd/Pos", 1, 2, 3, 4, 5, 6, 7, 8),
                new uOSC.Message("/VMC/Ext/Bone/Pos", 12345, 0f, 0f, 0f, 0f, 0f, 0f, 1f),
                new uOSC.Message("/VMC/Ext/Blend/Val", 1, "notafloat"),
                new uOSC.Message("/VMC/Ext/Set/Period", "a", "b", "c", "d", "e", "f"),
                new uOSC.Message("/VMC/Ext/Set/Eye", "on", 0f, 0f, 0f),
                new uOSC.Message("/VMC/Ext/Set/Res", 12345),
                new uOSC.Message("/VMC/Ext/Set/Calib/Exec", "Ipose"),

                //値が異常
                new uOSC.Message("/VMC/Ext/Bone/Pos", "存在しないボーン名", 0f, 0f, 0f, 0f, 0f, 0f, 1f),
                new uOSC.Message("/VMC/Ext/Bone/Pos", "", 0f, 0f, 0f, 0f, 0f, 0f, 1f),
                new uOSC.Message("/VMC/Ext/Bone/Pos", "Head", float.NaN, float.NaN, float.NaN, 0f, 0f, 0f, 1f),
                new uOSC.Message("/VMC/Ext/Bone/Pos", "Head", float.PositiveInfinity, 0f, 0f, 0f, 0f, 0f, 1f),
                new uOSC.Message("/VMC/Ext/Hmd/Pos", longString, 0f, 0f, 0f, 0f, 0f, 0f, 1f),
                new uOSC.Message("/VMC/Ext/Blend/Val", "存在しない表情", 999f),
                new uOSC.Message("/VMC/Ext/Cam", "Camera", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f), //回転が全ゼロ
                new uOSC.Message("/VMC/Ext/Set/Calib/Exec", 99),                            //未定義のキャリブ種別
                new uOSC.Message("/VMC/Ext/Set/Period", -1, -1, -1, -1, -1, -1),

                //未知のアドレス
                new uOSC.Message("/VMC/Ext/UnknownAddress", 1, 2f, "three"),
                new uOSC.Message("/NotVMC/Something", 1),
                new uOSC.Message(""),
            };
        }
    }
}
