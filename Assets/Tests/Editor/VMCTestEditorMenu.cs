using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VMC.Tests;

namespace VMC.Tests.EditorTools
{
    /// <summary>
    /// Unity Editor から自動テストを起動するメニュー。
    /// 実行要求を一時ファイルに書いてから再生モードに入る
    /// (ドメインリロードで静的変数が消えるためファイル経由で渡す)。
    /// </summary>
    public static class VMCTestEditorMenu
    {
        private const string MenuRoot = "VMC/自動テスト/";

        [MenuItem(MenuRoot + "全シナリオを実行", priority = 0)]
        public static void RunAll() => Run(updateGolden: false);

        [MenuItem(MenuRoot + "全シナリオを実行(ゴールデンを更新)", priority = 1)]
        public static void RunAllAndUpdateGolden()
        {
            if (EditorUtility.DisplayDialog("ゴールデンの更新",
                    "現在の実行結果で期待値(ゴールデン)を上書きします。\n差分の検出は行われません。よろしいですか?",
                    "更新する", "キャンセル") == false)
            {
                return;
            }
            Run(updateGolden: true);
        }

        [MenuItem(MenuRoot + "VRM0.xのみ実行", priority = 20)]
        public static void RunVrm0() => Run(updateGolden: false, models: new[] { VMCTestModels.Vrm0 });

        [MenuItem(MenuRoot + "VRM1.0のみ実行", priority = 21)]
        public static void RunVrm10() => Run(updateGolden: false, models: new[] { VMCTestModels.Vrm10 });

        [MenuItem(MenuRoot + "設定ファイルを開く", priority = 40)]
        public static void OpenConfig()
        {
            var config = VMCTestConfig.Load();
            var path = VMCTestConfig.ResolvePath(VMCTestConfig.DefaultConfigPath);
            if (File.Exists(path) == false) config.Save(VMCTestConfig.DefaultConfigPath);
            EditorUtility.RevealInFinder(path);
            EditorUtility.OpenWithDefaultApp(path);
        }

        [MenuItem(MenuRoot + "結果フォルダを開く", priority = 41)]
        public static void OpenResults()
        {
            var directory = VMCTestConfig.Load().ResolvedOutputDirectory;
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory + Path.DirectorySeparatorChar);
        }

        [MenuItem(MenuRoot + "ゴールデンフォルダを開く", priority = 42)]
        public static void OpenGolden()
        {
            var directory = VMCTestConfig.Load().ResolvedGoldenDirectory;
            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory + Path.DirectorySeparatorChar);
        }

        private static void Run(bool updateGolden, string[] models = null, string[] scenarios = null)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[VMCTest] 再生モードを終了してから実行してください");
                return;
            }

            var config = VMCTestConfig.Load();
            var missing = new[] { VMCTestModels.Vrm0, VMCTestModels.Vrm10 }
                .Where(d => models == null || models.Contains(d))
                .Where(d => config.GetModelPath(d) == null)
                .ToList();

            if (missing.Count > 0)
            {
                Debug.LogWarning($"[VMCTest] VRMが見つからないためスキップされます: {string.Join(", ", missing)}\n" +
                                 $"{VMCTestConfig.ResolvePath(VMCTestConfig.DefaultConfigPath)} にパスを設定してください");
            }

            new VMCTestRequest
            {
                Scenarios = scenarios?.ToList() ?? new System.Collections.Generic.List<string>(),
                Models = models?.ToList() ?? new System.Collections.Generic.List<string>(),
                UpdateGolden = updateGolden,
                QuitWhenFinished = false,
            }.Save();

            EditorApplication.EnterPlaymode();
        }
    }
}
