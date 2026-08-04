using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC.Tests
{
    /// <summary>
    /// コントロールパネル(WPF)との通信に使う全コマンド型のシリアライズ往復。
    ///
    /// PipeCommands は DataContractSerializer でやり取りされる。
    /// メンバーの追加漏れ・[OptionalField]の付け忘れ・新しいenum値の追加などで
    /// 値が欠落したり、旧バージョンとの通信が例外になったりする。
    /// 全型に既定値以外の値を詰めて往復させ、値が保たれるかを一括で確認する。
    /// </summary>
    public sealed class Scenario_PipeCommandsSerialization : VMCTestScenario
    {
        public override string Name => "PipeCommandsSerialization";

        public override string Description => "WPFとの通信コマンド全型のシリアライズ往復";

        public override bool RequiresModel => false;

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.None };

        private const float FloatTolerance = 1e-4f;

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. コマンド型の収集");

            //PipeCommands のネストクラス + 同じアセンブリの公開データ型
            var assembly = typeof(PipeCommands).Assembly;
            var types = new List<Type>();
            types.AddRange(typeof(PipeCommands).GetNestedTypes(BindingFlags.Public));
            types.AddRange(assembly.GetTypes().Where(t => t.IsPublic && t.IsClass && t.IsAbstract == false
                && t.Namespace == typeof(PipeCommands).Namespace
                && t != typeof(PipeCommands)
                && t.GetConstructor(Type.EmptyTypes) != null));

            types = types.Where(t => t.IsClass && t.IsAbstract == false && t.GetConstructor(Type.EmptyTypes) != null)
                         .Distinct()
                         .OrderBy(t => t.FullName, StringComparer.Ordinal)
                         .ToList();

            Debug.Log($"[VMCTest] シリアライズ往復の対象: {types.Count} 型");

            result.CheckThat("コマンド型の収集",
                types.Count > 50,
                $"コマンド型が想定より少ないです({types.Count}型)。収集条件が壊れていないか確認してください");

            //--- 2. 往復 ---
            context.Log($"2. {types.Count}型のシリアライズ往復");
            var failures = new List<string>();
            var mismatches = new List<string>();
            int tested = 0;

            foreach (var type in types)
            {
                object filled;
                try
                {
                    filled = VMCTestObjectFiller.CreateFilled(type, 1);
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.Name}: 値の生成に失敗 {ex.GetType().Name} {ex.Message}");
                    continue;
                }
                if (filled == null)
                {
                    failures.Add($"{type.Name}: インスタンスを生成できません");
                    continue;
                }

                object restored;
                try
                {
                    var bytes = BinarySerializer.Serialize(filled);
                    restored = BinarySerializer.Deserialize(bytes, type);
                }
                catch (Exception ex)
                {
                    //ここで落ちる型は、実際の通信でも例外になる
                    failures.Add($"{type.Name}: {ex.GetType().Name} {ex.Message}");
                    continue;
                }

                tested++;
                var differences = VMCTestObjectComparer.Compare(filled, restored, FloatTolerance, 5);
                if (differences.Count > 0)
                {
                    mismatches.Add($"{type.Name}: {string.Join(" / ", differences)}");
                }

                //型数が多いので、たまにフレームを回してエディタが固まらないようにする
                if (tested % 40 == 0) yield return null;
            }

            result.CheckThat("シリアライズの例外",
                failures.Count == 0,
                $"シリアライズできない型があります({failures.Count}件): " +
                string.Join("\n        ", failures.Take(15)));

            result.CheckThat("シリアライズ往復の値",
                mismatches.Count == 0,
                $"往復で値が変わる型があります({mismatches.Count}件): " +
                string.Join("\n        ", mismatches.Take(15)));

            Debug.Log($"[VMCTest] シリアライズ往復: {tested}型を検証 / 例外 {failures.Count}件 / 値の不一致 {mismatches.Count}件");
        }
    }
}
