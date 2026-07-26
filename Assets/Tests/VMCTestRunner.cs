using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// Editorメニュー / コマンドラインからランナーへ渡す実行要求
    /// </summary>
    [Serializable]
    public class VMCTestRequest
    {
        public List<string> Scenarios = new List<string>();
        public List<string> Models = new List<string>();
        public bool UpdateGolden;
        public string ConfigPath = VMCTestConfig.DefaultConfigPath;
        public bool QuitWhenFinished;

        /// <summary>Editorメニューから実行要求を渡すための一時ファイル(ランナーが読んだら消す)</summary>
        public const string RequestFilePath = "Temp/vmctest.request.json";

        public static string FullRequestFilePath => VMCTestConfig.ResolvePath(RequestFilePath);

        public void Save()
        {
            var path = FullRequestFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        public static VMCTestRequest ConsumeFile()
        {
            var path = FullRequestFilePath;
            if (File.Exists(path) == false) return null;
            try
            {
                var request = JsonUtility.FromJson<VMCTestRequest>(File.ReadAllText(path));
                File.Delete(path);
                return request;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VMCTest] 実行要求の読み込みに失敗しました: {ex}");
                return null;
            }
        }

        /// <summary>
        /// コマンドライン引数から実行要求を作る。
        ///   -vmctest                          テストを実行する
        ///   -vmctest-scenarios A,B            実行するシナリオ名(省略時は全部)
        ///   -vmctest-models vrm0,vrm10        対象モデル(省略時は全部)
        ///   -vmctest-updategolden             比較せずゴールデンを更新する
        ///   -vmctest-config <path>            設定ファイルのパス
        ///   -vmctest-noquit                   終了後にアプリを終了しない
        /// </summary>
        public static VMCTestRequest FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Any(d => string.Equals(d, "-vmctest", StringComparison.OrdinalIgnoreCase)) == false) return null;

            var request = new VMCTestRequest { QuitWhenFinished = true };

            string GetValue(string key)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
                }
                return null;
            }

            var scenarios = GetValue("-vmctest-scenarios");
            if (string.IsNullOrWhiteSpace(scenarios) == false)
            {
                request.Scenarios = scenarios.Split(',').Select(d => d.Trim()).Where(d => d.Length > 0).ToList();
            }

            var models = GetValue("-vmctest-models");
            if (string.IsNullOrWhiteSpace(models) == false)
            {
                request.Models = models.Split(',').Select(d => d.Trim()).Where(d => d.Length > 0).ToList();
            }

            var configPath = GetValue("-vmctest-config");
            if (string.IsNullOrWhiteSpace(configPath) == false) request.ConfigPath = configPath;

            request.UpdateGolden = args.Any(d => string.Equals(d, "-vmctest-updategolden", StringComparison.OrdinalIgnoreCase));
            if (args.Any(d => string.Equals(d, "-vmctest-noquit", StringComparison.OrdinalIgnoreCase))) request.QuitWhenFinished = false;

            return request;
        }
    }

    /// <summary>
    /// シナリオを順番に実行するランナー。
    /// 実機のVR機器もコントロールパネル(WPF)も無しで、本番のシーンをそのまま動かして検証する。
    /// </summary>
    public class VMCTestRunner : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }

        private VMCTestRequest request;

        /// <summary>
        /// シーン読み込み後、実行要求があればランナーを起動する。
        /// (コマンドライン -vmctest / Editorメニューが書いた一時ファイル)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            var request = VMCTestRequest.FromCommandLine() ?? VMCTestRequest.ConsumeFile();
            if (request == null) return;
            Start(request);
        }

        public static VMCTestRunner Start(VMCTestRequest request)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[VMCTest] すでにテストが実行中です");
                return null;
            }
            var runnerObject = new GameObject("VMCTestRunner");
            DontDestroyOnLoad(runnerObject);
            var runner = runnerObject.AddComponent<VMCTestRunner>();
            runner.request = request;
            return runner;
        }

        /// <summary>登録済みのシナリオ一覧</summary>
        public static IReadOnlyList<VMCTestScenario> AllScenarios { get; } = new VMCTestScenario[]
        {
            new Scenario_BasicVMCProtocol(),
            new Scenario_VMCProtocolBoneRoundTrip(),
            new Scenario_VMCProtocolSendCoverage(),
            new Scenario_VMCProtocolSpecCompliance(),
            new Scenario_VMCProtocolControlMessages(),
            new Scenario_MotionVrmaRoundTrip(),
            new Scenario_SettingsSaveLoad(),
            new Scenario_ModelSwitch(),
            new Scenario_PipeCommandsSerialization(),
            new Scenario_SettingsMigration(),
            new Scenario_DeviceInfoTracking(),
            new Scenario_Robustness(),
            new Scenario_KeyActions(),
            new Scenario_FaceMixing(),
            new Scenario_MultipleReceivers(),
            new Scenario_BvhExport(),
            new Scenario_RenderingAndStability(),
            new Scenario_FaceHardwareInputs(),
            new Scenario_MocopiReceive(),
            new Scenario_VMTSend(),
        };

        private IEnumerator Start()
        {
            IsRunning = true;
            var results = new List<VMCTestResult>();

            try
            {
                //アプリ側の初期化(ControlWPFWindowのAwake/Start、ExternalSenderのStart)を待つ
                yield return WaitForApplicationReady();

                var config = VMCTestConfig.Load(request.ConfigPath);
                if (request.UpdateGolden) config.UpdateGolden = true;

                var scenarios = AllScenarios
                    .Where(d => request.Scenarios == null || request.Scenarios.Count == 0 || request.Scenarios.Contains(d.Name))
                    .ToList();

                if (scenarios.Count == 0)
                {
                    Debug.LogError($"[VMCTest] 実行対象のシナリオがありません: {string.Join(",", request.Scenarios ?? new List<string>())}");
                }

                foreach (var scenario in scenarios)
                {
                    var models = scenario.Models
                        .Where(d => request.Models == null || request.Models.Count == 0 || request.Models.Contains(d))
                        .ToList();

                    foreach (var model in models)
                    {
                        yield return RunOne(scenario, model, config, results);
                    }
                }

                WriteReport(config, results);
            }
            finally
            {
                IsRunning = false;
            }

            if (request.QuitWhenFinished)
            {
                var failed = results.Any(d => d.Passed == false && d.Skipped == false);
                Debug.Log($"[VMCTest] 終了します (exit code {(failed ? 1 : 0)})");
                Application.Quit(failed ? 1 : 0);
            }
        }

        private static IEnumerator WaitForApplicationReady()
        {
            //ControlWPFWindowが現れるまで、最大10秒待つ
            for (int i = 0; i < 600; i++)
            {
                if (GameObject.Find("ControlWPFWindow") != null) break;
                yield return null;
            }
            //各コンポーネントのStartが一巡するのを待つ
            for (int i = 0; i < 10; i++) yield return null;
        }

        private IEnumerator RunOne(VMCTestScenario scenario, string model, VMCTestConfig config, List<VMCTestResult> results)
        {
            var result = new VMCTestResult { Scenario = scenario.Name, Model = model };
            results.Add(result);

            if (scenario.RequiresModel && config.GetModelPath(model) == null)
            {
                result.Skipped = true;
                result.SkipReason = $"{model} のVRMが見つかりません。{VMCTestConfig.DefaultConfigPath} にパスを設定してください";
                Debug.LogWarning($"[VMCTest] SKIP {result.Title}: {result.SkipReason}");
                yield break;
            }

            Debug.Log($"[VMCTest] ---- 開始: {result.Title} ----");

            var context = new VMCTestContext(config)
            {
                ScenarioName = scenario.Name,
                ModelKey = model,
            };

            if (context.Initialize() == false)
            {
                result.Error = "テストコンテキストの初期化に失敗しました";
                context.Dispose();
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + Mathf.Max(1f, config.TimeoutSeconds);
            yield return Drive(scenario.Run(context, result), deadline, () => context.CurrentStep, ex =>
            {
                result.Error = ex.ToString();
                Debug.LogError($"[VMCTest] {result.Title} で中断しました (工程: {context.CurrentStep})\n{ex}");
            });

            context.Dispose();

            var verdict = result.Skipped ? "SKIP" : (result.Passed ? "PASS" : "FAIL");
            Debug.Log($"[VMCTest] ---- 終了: {result.Title} : {verdict} ----");
        }

        /// <summary>
        /// コルーチンを自前で回す。
        /// Unityに任せると入れ子のコルーチン内の例外を捕まえられないため、
        /// IEnumeratorのスタックを自分で管理して全階層の例外を1箇所で受ける。
        /// あわせて実時間の上限を監視し、進まなくなったら中断する
        /// (awaitが返らない等でUnityごと固まるのを防ぐ)。
        /// </summary>
        private static IEnumerator Drive(IEnumerator routine, float deadlineRealtime, Func<string> currentStep, Action<Exception> onError)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);

            while (stack.Count > 0)
            {
                if (Time.realtimeSinceStartup > deadlineRealtime)
                {
                    onError(new TimeoutException($"制限時間を超えたため中断しました。止まった工程: {currentStep()}"));
                    yield break;
                }

                var current = stack.Peek();
                bool moved;
                object yielded = null;
                try
                {
                    moved = current.MoveNext();
                    if (moved) yielded = current.Current;
                }
                catch (Exception ex)
                {
                    onError(ex);
                    yield break;
                }

                if (moved == false)
                {
                    stack.Pop();
                    continue;
                }

                if (yielded is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return yielded;
            }
        }

        private static void WriteReport(VMCTestConfig config, IReadOnlyList<VMCTestResult> results)
        {
            var report = VMCTestReport.Build(results);
            Debug.Log(report);

            try
            {
                var directory = config.ResolvedOutputDirectory;
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "report.txt"), report);
                Debug.Log($"[VMCTest] レポートを書き出しました: {Path.Combine(directory, "report.txt")}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VMCTest] レポートの書き出しに失敗しました: {ex.Message}");
            }
        }
    }
}
