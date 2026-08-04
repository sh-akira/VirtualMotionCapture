using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// リフレクションでオブジェクトの全メンバーに「既定値ではない値」を詰める。
    /// シリアライズ往復テストで、既定値のままだと欠落に気付けないため、
    /// 全項目に別々の値を入れてから往復させるのに使う。
    /// </summary>
    public static class VMCTestObjectFiller
    {
        private const int MaxDepth = 6;

        public static object CreateFilled(Type type, int seed)
        {
            return CreateValue(type, ref seed, 0);
        }

        private static object CreateValue(Type type, ref int seed, int depth)
        {
            seed++;

            if (type == typeof(string)) return $"vmctest_{seed}";
            if (type == typeof(bool)) return seed % 2 == 0;
            if (type == typeof(int)) return 1000 + seed;
            if (type == typeof(uint)) return (uint)(1000 + seed);
            if (type == typeof(long)) return 100000L + seed;
            if (type == typeof(short)) return (short)(100 + seed);
            if (type == typeof(byte)) return (byte)(seed % 200 + 1);
            if (type == typeof(float)) return 1.5f + seed * 0.25f;
            if (type == typeof(double)) return 2.5d + seed * 0.5d;
            if (type == typeof(decimal)) return 3.5m + seed;
            if (type == typeof(char)) return (char)('A' + seed % 26);
            if (type == typeof(DateTime)) return new DateTime(2020, 1, 1).AddDays(seed);
            if (type == typeof(Guid)) return Guid.Empty;

            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                if (values.Length == 0) return Activator.CreateInstance(type);
                //既定値(先頭)以外を選ぶことで「初期化されていない」との区別を付ける
                return values.GetValue(values.Length > 1 ? 1 : 0);
            }

            var nullableUnderlying = Nullable.GetUnderlyingType(type);
            if (nullableUnderlying != null)
            {
                return CreateValue(nullableUnderlying, ref seed, depth);
            }

            if (type == typeof(Vector2)) return new Vector2(0.1f * seed, 0.2f * seed);
            if (type == typeof(Vector3)) return new Vector3(0.1f * seed, 0.2f * seed, 0.3f * seed);
            if (type == typeof(Vector4)) return new Vector4(0.1f * seed, 0.2f * seed, 0.3f * seed, 0.4f * seed);
            if (type == typeof(Quaternion)) return Quaternion.Euler(10f + seed, 20f + seed, 30f + seed);
            if (type == typeof(Color)) return new Color(0.1f, 0.2f, 0.3f, 1f);

            if (depth >= MaxDepth) return null;

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var array = Array.CreateInstance(elementType, 2);
                for (int i = 0; i < 2; i++)
                {
                    array.SetValue(CreateValue(elementType, ref seed, depth + 1), i);
                }
                return array;
            }

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments();

                if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IEnumerable<>))
                {
                    var listType = typeof(List<>).MakeGenericType(arguments[0]);
                    var list = (IList)Activator.CreateInstance(listType);
                    for (int i = 0; i < 2; i++)
                    {
                        list.Add(CreateValue(arguments[0], ref seed, depth + 1));
                    }
                    return list;
                }

                if (definition == typeof(Dictionary<,>))
                {
                    var dictionary = (IDictionary)Activator.CreateInstance(type);
                    for (int i = 0; i < 2; i++)
                    {
                        var key = CreateValue(arguments[0], ref seed, depth + 1);
                        if (key == null) continue;
                        dictionary[key] = CreateValue(arguments[1], ref seed, depth + 1);
                    }
                    return dictionary;
                }

                if (type.FullName != null && type.FullName.StartsWith("System.Tuple`"))
                {
                    var values = new object[arguments.Length];
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        values[i] = CreateValue(arguments[i], ref seed, depth + 1);
                    }
                    return Activator.CreateInstance(type, values);
                }
            }

            if (type.IsAbstract || type.IsInterface) return null;

            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch (Exception)
            {
                return null;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsInitOnly) continue;
                var value = CreateValue(field.FieldType, ref seed, depth + 1);
                if (value != null) field.SetValue(instance, value);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead == false || property.CanWrite == false) continue;
                if (property.GetIndexParameters().Length > 0) continue;
                var value = CreateValue(property.PropertyType, ref seed, depth + 1);
                if (value != null) property.SetValue(instance, value);
            }

            return instance;
        }
    }
}
