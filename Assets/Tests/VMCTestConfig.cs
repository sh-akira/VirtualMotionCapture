using System;
using System.IO;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// 自動テストの設定。
    /// テスト用VRMはライセンスの都合でリポジトリに含めないため、
    /// プロジェクト直下の TestData/vmctest.json でパスを指定する。
    /// </summary>
    [Serializable]
    public class VMCTestConfig
    {
        /// <summary>プロジェクト直下からの相対パス、または絶対パス</summary>
        public string Vrm0Path = "";
        public string Vrm10Path = "";

        /// <summary>期待値(ゴールデン)の保存先</summary>
        public string GoldenDirectory = "TestData/Golden";

        /// <summary>実行結果・差分レポートの出力先</summary>
        public string OutputDirectory = "TestData/Results";

        /// <summary>trueにすると比較せずゴールデンを上書きする</summary>
        public bool UpdateGolden = false;

        //比較の許容誤差
        public float PositionTolerance = 0.001f;
        public float RotationToleranceDegrees = 0.2f;
        public float WeightTolerance = 0.002f;

        //モーション(VRMA/BVH)の往復はマッスル空間とglTFの量子化を通るため、通常より緩い許容誤差を使う
        public float MotionRotationToleranceDegrees = 2.0f;
        public float MotionWeightTolerance = 0.02f;

        /// <summary>
        /// VRMAファイルの往復で、末端(頭・手・足)の向きに許す誤差。
        /// ここが一致していれば見た目の姿勢は保たれている。厳しく見る。
        /// </summary>
        public float VrmaEndEffectorToleranceDegrees = 1.0f;

        /// <summary>
        /// VRMAファイルの往復で、ボーン単位のローカル回転に許す誤差。
        /// Humanoidのリターゲットは腕のツイストを上腕と手の間で配分し直すため
        /// (VRMのアバターとVRMAから作ったアバターでtwist設定が異なる)、
        /// 末端の向きが完全に一致していてもボーン単位では数度ずれる。実測で最大5度程度。
        /// </summary>
        public float VrmaFileToleranceDegrees = 8.0f;

        /// <summary>
        /// 記録→再生の総合誤差(実際のアバターの姿勢 vs 再生後)の許容誤差。
        /// Unity Humanoidのマッスル空間は可動範囲が限られており、
        /// 特に腕を下ろした姿勢の肩・上腕は元の回転をそのまま表現できないため大きめに取る。
        /// </summary>
        public float MotionRetargetToleranceDegrees = 15.0f;

        /// <summary>
        /// 指(特に親指)はマッスル空間の表現力がさらに低いため、別枠でさらに緩くする。
        /// </summary>
        public float MotionFingerToleranceDegrees = 25.0f;

        /// <summary>まばたき等の乱数を固定するシード</summary>
        public int Seed = 12345;

        /// <summary>1フレームあたりの進行時間(Time.captureDeltaTimeに設定して決定論化する)</summary>
        public float FixedDeltaTime = 1f / 60f;

        /// <summary>1シナリオあたりの実時間の上限。超えたら中断して失敗にする(ハング対策)</summary>
        public float TimeoutSeconds = 180f;

        public const string DefaultConfigPath = "TestData/vmctest.json";

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>プロジェクト直下を基準に絶対パス化する</summary>
        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }

        public string ResolvedGoldenDirectory => ResolvePath(GoldenDirectory);
        public string ResolvedOutputDirectory => ResolvePath(OutputDirectory);

        /// <summary>
        /// モデル種別("vrm0"/"vrm10")からVRMのフルパスを得る。未設定/不存在ならnull。
        /// </summary>
        public string GetModelPath(string modelKey)
        {
            var raw = modelKey == VMCTestModels.Vrm10 ? Vrm10Path : Vrm0Path;
            var full = ResolvePath(raw);
            if (string.IsNullOrEmpty(full) || File.Exists(full) == false) return null;
            return full;
        }

        public static VMCTestConfig Load(string path = null)
        {
            var fullPath = ResolvePath(path ?? DefaultConfigPath);
            if (File.Exists(fullPath) == false)
            {
                //初回は雛形を書き出して、パスを埋めてもらう
                var template = new VMCTestConfig();
                try
                {
                    template.Save(fullPath);
                    Debug.Log($"[VMCTest] 設定ファイルの雛形を作成しました。VRMのパスを記入してください: {fullPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VMCTest] 設定ファイルを作成できませんでした: {ex.Message}");
                }
                return template;
            }

            try
            {
                return JsonUtility.FromJson<VMCTestConfig>(File.ReadAllText(fullPath)) ?? new VMCTestConfig();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VMCTest] 設定ファイルの読み込みに失敗しました: {fullPath}\n{ex}");
                return new VMCTestConfig();
            }
        }

        public void Save(string path)
        {
            var fullPath = ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, JsonUtility.ToJson(this, true));
        }
    }

    public static class VMCTestModels
    {
        public const string Vrm0 = "vrm0";
        public const string Vrm10 = "vrm10";

        /// <summary>アバターを必要としないシナリオ用(1回だけ実行される)</summary>
        public const string None = "none";
    }
}
