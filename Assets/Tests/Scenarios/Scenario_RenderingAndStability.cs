using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;

namespace VMC.Tests
{
    /// <summary>
    /// 描画まわりと長時間安定性。
    ///   - 写真撮影(PNG書き出し・透過背景)
    ///   - スプリングボーンが動くか / 発散しないか
    ///   - アバターを何度も入れ替えたときのリーク
    ///   - 1フレームの処理時間
    /// </summary>
    public sealed class Scenario_RenderingAndStability : VMCTestScenario
    {
        public override string Name => "RenderingAndStability";

        public override string Description => "写真撮影・スプリングボーン・モデル入れ替えのリーク・処理時間";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        /// <summary>アバターを入れ替える回数</summary>
        private const int ReloadCount = 8;

        /// <summary>1フレームの処理時間の上限(ms)。致命的な劣化だけを捕まえるゆるい値</summary>
        private const float FrameBudgetMs = 200f;

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込みとキャリブレーション");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var receiver = context.CreateReceiver(setting => setting.ApplyTracker = true);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.WaitTrackingWarmup();
            yield return context.Calibrate(PipeCommands.CalibrateType.Ipose);
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.IPose);
            yield return context.Step(20);

            //--- 2. 写真撮影 ---
            context.Log("2. 写真撮影");
            yield return TakePhoto(context, result, transparent: false, label: "opaque");
            yield return TakePhoto(context, result, transparent: true, label: "transparent");

            //--- 3. スプリングボーン ---
            context.Log("3. スプリングボーン");
            yield return CheckSpringBones(context, result, receiver);

            //--- 4. 1フレームの処理時間 ---
            context.Log("4. 処理時間の測定");
            yield return MeasureFrameTime(context, result);

            //--- 5. モデル入れ替えのリーク ---
            context.Log($"5. アバターを{ReloadCount}回入れ替え");
            yield return CheckReloadLeak(context, result);
        }

        private static IEnumerator TakePhoto(VMCTestContext context, VMCTestResult result, bool transparent, string label)
        {
            var camera = CameraManager.Current != null ? CameraManager.Current.ControlCamera : null;
            if (camera == null)
            {
                result.CheckThat($"写真撮影({label})", false, "ControlCameraが見つかりません");
                yield break;
            }

            byte[] png = null;
            var resolution = new Resolution { width = 640, height = 360 };
            context.BeginErrorCapture();
            yield return Photo.TakePNGPhoto(camera, resolution, transparent, bytes => png = bytes);
            yield return context.Step(2);
            var errors = context.EndErrorCapture();

            var path = context.OutputPath($"{nameof(Scenario_RenderingAndStability)}.{label}.png");
            if (png != null) File.WriteAllBytes(path, png);

            //PNGのシグネチャとIHDRの幅を検証する(真っ黒でも生成はされるので、形式と寸法だけ見る)
            var validSignature = png != null && png.Length > 8
                && png[0] == 0x89 && png[1] == (byte)'P' && png[2] == (byte)'N' && png[3] == (byte)'G';
            var width = png != null && png.Length > 20
                ? (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]
                : 0;

            result.CheckThat($"写真撮影({label})",
                validSignature && width == resolution.width && errors.Count == 0,
                $"PNGが正しく生成されていません(bytes={png?.Length ?? 0} signature={validSignature} width={width} " +
                $"errors={errors.Count})");
        }

        private static IEnumerator CheckSpringBones(VMCTestContext context, VMCTestResult result, ExternalReceiverForVMC receiver)
        {
            var vrm10Instance = context.CurrentModel.GetComponent<Vrm10Instance>();
            var springBones = CollectSpringBones(context.CurrentModel, vrm10Instance);

            if (springBones.Count == 0)
            {
                Debug.Log("[VMCTest] このモデルにはスプリングボーンがありません。検査をスキップします");
                yield break;
            }
            Debug.Log($"[VMCTest] スプリングボーン {springBones.Count} 本を検査します");

            var before = springBones.Select(d => d.localRotation).ToList();

            //大きく動かして揺れを起こす
            context.InjectTrackerRig(receiver, VMCTestTrackerRig.TPose);
            yield return context.Step(5);
            var during = springBones.Select(d => d.localRotation).ToList();

            //十分に時間を置いて落ち着かせる
            yield return context.Step(180);
            var settled = springBones.Select(d => d.localRotation).ToList();

            var moved = MaxAngle(before, during);
            result.CheckThat("スプリングボーンが動くこと",
                moved > 0.5f,
                $"アバターを大きく動かしてもスプリングボーンが揺れていません(最大 {moved:F3}度)");

            //発散(NaN/無限大)していないこと
            var broken = springBones.Where(d =>
                float.IsNaN(d.localRotation.x) || float.IsInfinity(d.localRotation.x) ||
                float.IsNaN(d.localPosition.x) || float.IsInfinity(d.localPosition.x) ||
                d.localPosition.magnitude > 1000f).ToList();
            result.CheckThat("スプリングボーンが発散しないこと",
                broken.Count == 0,
                $"スプリングボーンの値が壊れています({broken.Count}本 例: {broken.FirstOrDefault()?.name})");

            //静止後は落ち着いていること(揺れ続けない)
            yield return context.Step(30);
            var afterSettle = springBones.Select(d => d.localRotation).ToList();
            var residual = MaxAngle(settled, afterSettle);
            result.CheckThat("スプリングボーンが収束すること",
                residual < 1.0f,
                $"静止しているのにスプリングボーンが揺れ続けています(最大 {residual:F3}度/30フレーム)");

            Debug.Log($"[VMCTest] スプリングボーン: 揺れ幅 最大{moved:F2}度 / 収束後の残留 {residual:F3}度");
        }

        private static IEnumerator MeasureFrameTime(VMCTestContext context, VMCTestResult result)
        {
            const int samples = 120;
            //最初の数フレームは読み込み直後で不安定なので捨てる
            yield return context.Step(10);

            var start = Time.realtimeSinceStartup;
            yield return context.Step(samples);
            var elapsed = Time.realtimeSinceStartup - start;
            var perFrameMs = elapsed / samples * 1000f;

            Debug.Log($"[VMCTest] 1フレームの処理時間: 平均 {perFrameMs:F2} ms ({samples}フレーム測定)");

            result.CheckThat("処理時間",
                perFrameMs < FrameBudgetMs,
                $"1フレームの処理時間が {perFrameMs:F2} ms で上限 {FrameBudgetMs} ms を超えています");
        }

        private static IEnumerator CheckReloadLeak(VMCTestContext context, VMCTestResult result)
        {
            var vrmPath = context.Config.GetModelPath(context.ModelKey);

            //まず1回入れ替えて、初回だけ生成されるものを含めない状態にする
            yield return context.SwitchModel(vrmPath);
            yield return context.Step(10);
            yield return UnloadAndCollect(context);

            var baselineObjects = CountSceneObjects();
            var baselineMemory = System.GC.GetTotalMemory(false);

            for (int i = 0; i < ReloadCount; i++)
            {
                yield return context.SwitchModel(vrmPath);
                yield return context.Step(5);
            }
            yield return context.Step(10);
            yield return UnloadAndCollect(context);

            var afterObjects = CountSceneObjects();
            var afterMemory = System.GC.GetTotalMemory(false);
            var objectGrowth = afterObjects - baselineObjects;
            var memoryGrowthMb = (afterMemory - baselineMemory) / 1024f / 1024f;

            Debug.Log($"[VMCTest] {ReloadCount}回入れ替え後: GameObject {baselineObjects} -> {afterObjects} " +
                      $"(+{objectGrowth}) / マネージドメモリ +{memoryGrowthMb:F1} MB");

            //1回の入れ替えにつき数個までの増加は許容(遅延破棄やキャッシュのため)。
            //リークしていれば入れ替え回数に比例して増える
            var allowedGrowth = ReloadCount * 5;
            result.CheckThat("モデル入れ替えのリーク",
                objectGrowth < allowedGrowth,
                $"アバターを{ReloadCount}回入れ替えたらシーン上のGameObjectが {objectGrowth} 個増えました" +
                $"(許容 {allowedGrowth} 個未満)。破棄漏れの可能性があります");

            //読み込み直後もアバターが正常であること
            result.CheckThat("入れ替え後のモデル",
                context.CurrentModel != null && context.CurrentModel.GetComponent<Animator>() != null,
                "繰り返し入れ替えた後にアバターが壊れています");
        }

        private static IEnumerator UnloadAndCollect(VMCTestContext context)
        {
            var unload = Resources.UnloadUnusedAssets();
            while (unload.isDone == false) yield return null;
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            yield return context.Step(2);
        }

        private static int CountSceneObjects()
        {
            //シーンに属する(=非アセットの)GameObjectだけを数える
            return Object.FindObjectsOfType<GameObject>(true).Length;
        }

        private static List<Transform> CollectSpringBones(GameObject model, Vrm10Instance vrm10Instance)
        {
            var result = new List<Transform>();
            if (vrm10Instance == null) return result;

            var springBone = vrm10Instance.SpringBone;
            if (springBone == null || springBone.Springs == null) return result;

            foreach (var spring in springBone.Springs)
            {
                if (spring?.Joints == null) continue;
                foreach (var joint in spring.Joints)
                {
                    if (joint == null || joint.transform == null) continue;
                    result.Add(joint.transform);
                }
            }
            return result;
        }

        private static float MaxAngle(List<Quaternion> a, List<Quaternion> b)
        {
            float max = 0f;
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                max = Mathf.Max(max, Quaternion.Angle(a[i], b[i]));
            }
            return max;
        }
    }
}
