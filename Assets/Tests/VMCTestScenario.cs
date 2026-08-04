using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>1つの検証項目(スナップショット1件の比較)の結果</summary>
    public sealed class VMCTestCheck
    {
        public string Label;
        public bool Passed;
        public bool GoldenCreated;
        public List<string> Differences = new List<string>();
    }

    /// <summary>シナリオ×モデル1回分の実行結果</summary>
    public sealed class VMCTestResult
    {
        public string Scenario;
        public string Model;
        public bool Skipped;
        public string SkipReason;
        public string Error;
        public readonly List<VMCTestCheck> Checks = new List<VMCTestCheck>();

        public bool Passed => Skipped == false && Error == null && Checks.All(d => d.Passed);

        public string Title => $"{Scenario} [{Model}]";

        /// <summary>
        /// ゴールデンに依存しない不変条件の検査。
        /// 「そもそも動いているか」はゴールデン比較では検出できない
        /// (壊れた状態がそのまま期待値として保存されてしまう)ので、これで担保する。
        /// </summary>
        public VMCTestCheck CheckThat(string label, bool condition, string failureMessage)
        {
            var check = new VMCTestCheck { Label = label, Passed = condition };
            if (condition == false)
            {
                check.Differences.Add(failureMessage);
                Debug.LogError($"[VMCTest] {Title} {label}: {failureMessage}");
            }
            Checks.Add(check);
            return check;
        }

        /// <summary>
        /// スナップショットをゴールデンと比較する。
        /// ゴールデンが無い場合、または UpdateGolden 指定時は、現在の値をゴールデンとして保存する。
        /// </summary>
        public VMCTestCheck CheckSnapshot(VMCTestContext context, VMCTestSnapshot actual)
        {
            var check = new VMCTestCheck { Label = actual.Label };
            Checks.Add(check);

            var config = context.Config;

            //実行結果は毎回残す(差分調査用)
            try
            {
                actual.Save(config.ResolvedOutputDirectory);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VMCTest] 実行結果の保存に失敗しました: {ex.Message}");
            }

            var goldenDirectory = config.ResolvedGoldenDirectory;
            var expected = VMCTestSnapshot.Load(goldenDirectory, actual.FileName);

            if (config.UpdateGolden || expected == null)
            {
                actual.Save(goldenDirectory);
                check.Passed = true;
                check.GoldenCreated = true;
                Debug.Log($"[VMCTest] ゴールデンを保存しました: {Path.Combine(goldenDirectory, actual.FileName)}");
                return check;
            }

            check.Differences = actual.CompareTo(expected, config);
            check.Passed = check.Differences.Count == 0;
            return check;
        }
    }

    /// <summary>
    /// テストシナリオの基底。
    /// Runはコルーチンで、途中でthrowすると失敗として記録される。
    /// </summary>
    public abstract class VMCTestScenario
    {
        public abstract string Name { get; }

        public virtual string Description => "";

        /// <summary>このシナリオを実行するモデル種別</summary>
        public virtual IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0, VMCTestModels.Vrm10 };

        /// <summary>
        /// アバターを必要とするか。falseにすると Models を <see cref="VMCTestModels.None"/> 1つにして、
        /// VRMが未設定でもスキップせずに実行する。
        /// </summary>
        public virtual bool RequiresModel => true;

        public abstract IEnumerator Run(VMCTestContext context, VMCTestResult result);
    }

    /// <summary>実行結果全体のレポート</summary>
    public static class VMCTestReport
    {
        public static string Build(IReadOnlyList<VMCTestResult> results)
        {
            var builder = new StringBuilder();
            var passed = results.Count(d => d.Passed);
            var skipped = results.Count(d => d.Skipped);
            var failed = results.Count - passed - skipped;

            builder.AppendLine("==== VMC 自動テスト結果 ====");
            builder.AppendLine($"合計 {results.Count} / 成功 {passed} / 失敗 {failed} / スキップ {skipped}");
            builder.AppendLine();

            foreach (var result in results)
            {
                if (result.Skipped)
                {
                    builder.AppendLine($"[SKIP] {result.Title} : {result.SkipReason}");
                    continue;
                }

                builder.AppendLine($"[{(result.Passed ? "PASS" : "FAIL")}] {result.Title}");

                if (result.Error != null)
                {
                    builder.AppendLine($"    例外: {result.Error}");
                }

                foreach (var check in result.Checks)
                {
                    if (check.GoldenCreated)
                    {
                        builder.AppendLine($"    - {check.Label}: ゴールデンを新規作成");
                        continue;
                    }
                    if (check.Passed)
                    {
                        builder.AppendLine($"    - {check.Label}: 一致");
                        continue;
                    }

                    builder.AppendLine($"    - {check.Label}: {check.Differences.Count}件の差分");
                    foreach (var difference in check.Differences.Take(20))
                    {
                        builder.AppendLine($"        {difference}");
                    }
                    if (check.Differences.Count > 20)
                    {
                        builder.AppendLine($"        ... 他 {check.Differences.Count - 20} 件");
                    }
                }
            }

            return builder.ToString();
        }
    }
}
