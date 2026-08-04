using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// DeviceInfo(トラッキングの飛び検出・復帰補間)の検証。
    ///
    /// 実機のトラッカーが無くても、姿勢を直接与えるだけで全経路を通せる。
    /// ここは「エラーも警告も出ないまま姿勢が固定される」壊れ方をするため、
    /// 手動では極めて気付きにくい(実際に Application.targetFrameRate = -1 で踏んだ)。
    /// </summary>
    public sealed class Scenario_DeviceInfoTracking : VMCTestScenario
    {
        public override string Name => "DeviceInfoTracking";

        public override string Description => "トラッキングの飛び検出・復帰補間・一時停止";

        public override bool RequiresModel => false;

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.None };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.ResetSettings();

            var frameRate = context.FrameRate;
            var poseA = new SteamVR_Utils.RigidTransform(new Vector3(0.10f, 1.20f, 0.30f), Quaternion.Euler(10f, 20f, 30f));
            var poseB = new SteamVR_Utils.RigidTransform(new Vector3(-0.40f, 0.80f, -0.20f), Quaternion.Euler(-15f, 40f, 5f));

            //--- 1. 初回の姿勢がそのまま出るか ---
            context.Log("1. 初回の姿勢");
            var device = new DeviceInfo();
            device.UpdateDeviceInfo(poseA, "VMCTEST_DEVICE");

            result.CheckThat("初回の姿勢",
                Vector3.Distance(device.transform.pos, poseA.pos) < 0.0001f && device.isOK,
                $"初回に与えた姿勢が出てきません({device.transform.pos} / isOK={device.isOK})");

            //--- 2. 復帰補間の間は過去値から徐々に近づく ---
            //DeviceInfo は認識から LEAP_SECONDS(1秒) の間、飛び対策で過去値から補間する
            context.Log("2. 復帰補間中の挙動");
            device.UpdateDeviceInfo(poseB, "VMCTEST_DEVICE");
            var duringWarmup = device.transform.pos;
            result.CheckThat("復帰補間",
                Vector3.Distance(duringWarmup, poseB.pos) > 0.01f,
                $"認識直後なのに新しい姿勢がそのまま採用されています({duringWarmup})。飛び対策の補間が効いていません");

            //--- 3. ウォームアップ後は与えた姿勢がそのまま出る ---
            context.Log("3. ウォームアップ後の追従");
            //okTime = validFrames / Application.targetFrameRate なので、1秒ぶん呼べば補間が終わる
            for (int i = 0; i < frameRate + 10; i++)
            {
                device.UpdateDeviceInfo(poseB, "VMCTEST_DEVICE");
            }

            var afterWarmup = device.transform.pos;
            result.CheckThat("ウォームアップ後の追従",
                Vector3.Distance(afterWarmup, poseB.pos) < 0.001f,
                $"ウォームアップ後も姿勢が追従していません({afterWarmup} 期待 {poseB.pos})。" +
                $"Application.targetFrameRate({Application.targetFrameRate})が正の値か確認してください");

            //ウォームアップ後は次の姿勢が即座に反映されること
            device.UpdateDeviceInfo(poseA, "VMCTEST_DEVICE");
            result.CheckThat("ウォームアップ後の即時反映",
                Vector3.Distance(device.transform.pos, poseA.pos) < 0.001f,
                $"ウォームアップ後なのに姿勢の変化が即座に反映されません({device.transform.pos} 期待 {poseA.pos})");

            //--- 4. ゼロ姿勢(トラッキングロスト)は過去値に差し替えられる ---
            context.Log("4. トラッキングロストの検出");
            device.UpdateDeviceInfo(new SteamVR_Utils.RigidTransform(Vector3.zero, Quaternion.identity), "VMCTEST_DEVICE");

            result.CheckThat("トラッキングロストの検出",
                device.isOK == false && Vector3.Distance(device.transform.pos, poseA.pos) < 0.001f,
                $"ゼロ姿勢を受けたときに過去値へ差し替えられていません(isOK={device.isOK} pos={device.transform.pos})");

            //--- 5. 一時停止中は位置が固定される ---
            context.Log("5. トラッキング一時停止");
            var pausedDevice = new DeviceInfo();
            for (int i = 0; i < frameRate + 10; i++)
            {
                pausedDevice.UpdateDeviceInfo(poseA, "VMCTEST_PAUSED");
            }

            DeviceInfo.pauseTracking = true;
            try
            {
                pausedDevice.UpdateDeviceInfo(poseB, "VMCTEST_PAUSED");
                result.CheckThat("トラッキング一時停止",
                    Vector3.Distance(pausedDevice.transform.pos, poseA.pos) < 0.001f,
                    $"一時停止中なのに位置が動いています({pausedDevice.transform.pos} 期待 {poseA.pos})");
            }
            finally
            {
                DeviceInfo.pauseTracking = false;
            }

            //--- 6. 種別ごとの無効化 ---
            context.Log("6. 機器種別ごとの無効化");
            var trackerDevice = new DeviceInfo();
            for (int i = 0; i < frameRate + 10; i++)
            {
                trackerDevice.UpdateDeviceInfo(poseA, "VMCTEST_TRACKER");
            }

            DeviceInfo.trackerEnable = false;
            try
            {
                trackerDevice.UpdateDeviceInfo(poseB, "VMCTEST_TRACKER");
                //無効時は saveAndSwapZeroTransform に切り替わり、ゼロ以外はそのまま記録される
                result.CheckThat("機器種別の無効化",
                    trackerDevice.isOK,
                    "トラッカー無効時の処理でisOKが落ちています");
            }
            finally
            {
                DeviceInfo.trackerEnable = true;
            }

            yield break;
        }
    }
}
