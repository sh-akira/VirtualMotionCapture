using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// 設定オブジェクトなどを再帰的に比較する。
    /// JSON文字列の比較だと -0 と 0 の表記差や、色空間変換による最下位ビットの揺れで
    /// 落ちてしまうため、数値は許容誤差つきで比較し、違いは項目名で報告する。
    /// </summary>
    public static class VMCTestObjectComparer
    {
        private const int MaxDepth = 12;

        public static List<string> Compare(object expected, object actual, float tolerance, int maxDifferences = 30)
        {
            var differences = new List<string>();
            CompareValue("", expected, actual, tolerance, differences, maxDifferences, 0);
            return differences;
        }

        private static void CompareValue(string path, object expected, object actual, float tolerance,
            List<string> differences, int maxDifferences, int depth)
        {
            if (differences.Count >= maxDifferences) return;

            if (expected == null && actual == null) return;
            if (expected == null || actual == null)
            {
                differences.Add($"{Name(path)}: {Describe(expected)} -> {Describe(actual)}");
                return;
            }

            var type = expected.GetType();
            if (type != actual.GetType())
            {
                differences.Add($"{Name(path)}: 型が違います {type.Name} -> {actual.GetType().Name}");
                return;
            }

            if (type == typeof(float))
            {
                if (NearlyEqual((float)expected, (float)actual, tolerance) == false)
                {
                    differences.Add($"{Name(path)}: {(float)expected:R} -> {(float)actual:R}");
                }
                return;
            }
            if (type == typeof(double))
            {
                if (NearlyEqual((float)(double)expected, (float)(double)actual, tolerance) == false)
                {
                    differences.Add($"{Name(path)}: {(double)expected:R} -> {(double)actual:R}");
                }
                return;
            }
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
            {
                if (expected.Equals(actual) == false)
                {
                    differences.Add($"{Name(path)}: {expected} -> {actual}");
                }
                return;
            }

            //Unityの構造体はプロパティに派生値(Quaternion.eulerAngles等)を持つので、成分だけを比べる
            if (type == typeof(Vector2)) { CompareFloats(path, new[] { "x", "y" }, ToFloats((Vector2)expected), ToFloats((Vector2)actual), tolerance, differences); return; }
            if (type == typeof(Vector3)) { CompareFloats(path, new[] { "x", "y", "z" }, ToFloats((Vector3)expected), ToFloats((Vector3)actual), tolerance, differences); return; }
            if (type == typeof(Vector4)) { CompareFloats(path, new[] { "x", "y", "z", "w" }, ToFloats((Vector4)expected), ToFloats((Vector4)actual), tolerance, differences); return; }
            if (type == typeof(Quaternion)) { CompareFloats(path, new[] { "x", "y", "z", "w" }, ToFloats((Quaternion)expected), ToFloats((Quaternion)actual), tolerance, differences); return; }
            if (type == typeof(Color)) { CompareFloats(path, new[] { "r", "g", "b", "a" }, ToFloats((Color)expected), ToFloats((Color)actual), tolerance, differences); return; }

            if (depth >= MaxDepth) return;

            //Tuple はプロパティ(Item1, Item2, ...)で値を持つ
            if (type.IsGenericType && type.FullName != null && type.FullName.StartsWith("System.Tuple`"))
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                             .Where(p => p.Name.StartsWith("Item"))
                                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    CompareValue($"{path}.{property.Name}", property.GetValue(expected), property.GetValue(actual),
                        tolerance, differences, maxDifferences, depth + 1);
                }
                return;
            }

            if (expected is IEnumerable expectedEnumerable)
            {
                var expectedItems = expectedEnumerable.Cast<object>().ToList();
                var actualItems = ((IEnumerable)actual).Cast<object>().ToList();
                if (expectedItems.Count != actualItems.Count)
                {
                    differences.Add($"{Name(path)}: 要素数 {expectedItems.Count} -> {actualItems.Count}");
                    return;
                }
                for (int i = 0; i < expectedItems.Count; i++)
                {
                    CompareValue($"{path}[{i}]", expectedItems[i], actualItems[i],
                        tolerance, differences, maxDifferences, depth + 1);
                }
                return;
            }

            //Settingsは public フィールド、PipeCommands は public プロパティで値を持つので両方見る。
            //読み取り専用プロパティは派生値のことが多いので対象外にする。
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                      .OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (field.IsStatic) continue;
                CompareValue($"{path}.{field.Name}", field.GetValue(expected), field.GetValue(actual),
                    tolerance, differences, maxDifferences, depth + 1);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                         .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                object expectedValue;
                object actualValue;
                try
                {
                    expectedValue = property.GetValue(expected);
                    actualValue = property.GetValue(actual);
                }
                catch (Exception)
                {
                    continue; //取得できないプロパティは比較対象外
                }
                CompareValue($"{path}.{property.Name}", expectedValue, actualValue,
                    tolerance, differences, maxDifferences, depth + 1);
            }
        }

        private static float[] ToFloats(Vector2 v) => new[] { v.x, v.y };
        private static float[] ToFloats(Vector3 v) => new[] { v.x, v.y, v.z };
        private static float[] ToFloats(Vector4 v) => new[] { v.x, v.y, v.z, v.w };
        private static float[] ToFloats(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
        private static float[] ToFloats(Color c) => new[] { c.r, c.g, c.b, c.a };

        private static void CompareFloats(string path, string[] names, float[] expected, float[] actual,
            float tolerance, List<string> differences)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (NearlyEqual(expected[i], actual[i], tolerance) == false)
                {
                    differences.Add($"{Name(path)}.{names[i]}: {expected[i]:R} -> {actual[i]:R}");
                }
            }
        }

        private static bool NearlyEqual(float a, float b, float tolerance)
        {
            if (float.IsNaN(a) && float.IsNaN(b)) return true;
            if (a == b) return true; //-0 と 0 もここで一致扱いになる
            var scale = Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));
            return Math.Abs(a - b) <= tolerance * scale;
        }

        private static string Name(string path) => string.IsNullOrEmpty(path) ? "(ルート)" : path.TrimStart('.');

        private static string Describe(object value) => value == null ? "null" : value.ToString();
    }
}
