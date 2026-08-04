using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UniVRM10;

namespace VMC.Tests
{
    [Serializable]
    public class VMCTestPose
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [Serializable]
    public class VMCTestWeight
    {
        public string Name;
        public float Value;
    }

    /// <summary>OSCの引数1個。TはOSCの型タグ(i/f/s/b)</summary>
    [Serializable]
    public class VMCTestOscArg
    {
        public string T;
        public float F;
        public int I;
        public string S;

        public static VMCTestOscArg From(object value)
        {
            if (value is float f) return new VMCTestOscArg { T = "f", F = f };
            if (value is int i) return new VMCTestOscArg { T = "i", I = i };
            if (value is string s) return new VMCTestOscArg { T = "s", S = s };
            if (value is byte[] b) return new VMCTestOscArg { T = "b", I = b.Length };
            return new VMCTestOscArg { T = "?", S = value?.ToString() };
        }

        public override string ToString()
        {
            switch (T)
            {
                case "f": return F.ToString("F6");
                case "i": return I.ToString();
                case "s": return S;
                default: return $"{T}:{S}";
            }
        }
    }

    [Serializable]
    public class VMCTestOscMessage
    {
        public string Address;
        public List<VMCTestOscArg> Args = new List<VMCTestOscArg>();

        public static VMCTestOscMessage From(uOSC.Message message)
        {
            var result = new VMCTestOscMessage { Address = message.address };
            if (message.values != null)
            {
                foreach (var value in message.values)
                {
                    result.Args.Add(VMCTestOscArg.From(value));
                }
            }
            return result;
        }

        /// <summary>同じ対象を指すメッセージかどうか(アドレス + 先頭のstring引数)</summary>
        public string Key => Args.Count > 0 && Args[0].T == "s" ? $"{Address}/{Args[0].S}" : Address;

        public override string ToString() => $"{Address} [{string.Join(", ", Args.Select(d => d.ToString()))}]";
    }

    /// <summary>
    /// ある時点のアバター状態と、その時点でVMCProtocolとして送信された内容のスナップショット。
    /// ボーン姿勢・表情・視線・送信OSCを1つの形式にまとめることで、
    /// 「受信 → 内部状態 → 送信 → VRMA書き出し」を同じ比較器で検証できるようにしている。
    /// </summary>
    [Serializable]
    public class VMCTestSnapshot
    {
        public string Scenario;
        public string Model;
        public string Label;
        public int Frame;

        public VMCTestPose RootPose;

        /// <summary>正規化(ControlRig)ボーン。VMC内部の処理はこちらで統一されている</summary>
        public List<VMCTestPose> Bones = new List<VMCTestPose>();

        /// <summary>
        /// オリジナル(非正規化)ボーン。VMCProtocolが送受信するのはこちら。
        /// VRM0.x由来のモデルでは Bones と一致する。
        /// </summary>
        public List<VMCTestPose> OriginalBones = new List<VMCTestPose>();
        public List<VMCTestWeight> Expressions = new List<VMCTestWeight>();

        public bool HasLookAt;
        public float LookAtYaw;
        public float LookAtPitch;

        /// <summary>この区間にExternalSenderが送信したOSCメッセージ(アドレス+先頭引数で重複排除済み、最後の値)</summary>
        public List<VMCTestOscMessage> Sent = new List<VMCTestOscMessage>();

        public string FileName => $"{Scenario}.{Model}.{Label}.json";

        #region Capture

        /// <summary>
        /// 現在のアバターの状態をスナップショットに取る。
        /// ExternalSenderが送っている情報と同じものを、同じ経路(Animator.GetBoneTransform / ActualWeights)から取得する。
        /// </summary>
        public static VMCTestSnapshot Capture(string scenario, string model, string label, int frame, GameObject currentModel)
        {
            var snapshot = new VMCTestSnapshot
            {
                Scenario = scenario,
                Model = model,
                Label = label,
                Frame = frame,
            };

            if (currentModel == null) return snapshot;

            var animator = currentModel.GetComponent<Animator>();
            if (animator != null)
            {
                snapshot.RootPose = new VMCTestPose
                {
                    Name = "root",
                    Position = animator.transform.position,
                    Rotation = animator.transform.rotation,
                };

                foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (bone == HumanBodyBones.LastBone) continue;
                    var boneTransform = animator.GetBoneTransform(bone);
                    if (boneTransform == null) continue;
                    snapshot.Bones.Add(new VMCTestPose
                    {
                        Name = bone.ToString(),
                        Position = boneTransform.localPosition,
                        Rotation = boneTransform.localRotation,
                    });
                }
            }

            var vrm10Instance = currentModel.GetComponent<Vrm10Instance>();
            if (vrm10Instance != null)
            {
                //VMCProtocolが送受信するオリジナル(非正規化)ボーン
                if (vrm10Instance.Humanoid != null)
                {
                    foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                    {
                        if (bone == HumanBodyBones.LastBone) continue;
                        var boneTransform = vrm10Instance.Humanoid.GetBoneTransform(bone);
                        if (boneTransform == null) continue;
                        snapshot.OriginalBones.Add(new VMCTestPose
                        {
                            Name = bone.ToString(),
                            Position = boneTransform.localPosition,
                            Rotation = boneTransform.localRotation,
                        });
                    }
                }

                Vrm10Runtime runtime = null;
                try { runtime = vrm10Instance.Runtime; }
                catch (Exception) { /* 初期化前は取得できない */ }

                if (runtime != null)
                {
                    if (runtime.Expression != null)
                    {
                        //VMCProtocolの送信と同じくVRM0.x互換名で記録する(VRM0.x/VRM1.0で同じゴールデンを使うため)
                        foreach (var pair in runtime.Expression.ActualWeights.OrderBy(d => d.Key.ToString(), StringComparer.Ordinal))
                        {
                            snapshot.Expressions.Add(new VMCTestWeight
                            {
                                Name = VRM10CompatibleNames.GetVRM0CompatibleName(pair.Key),
                                Value = pair.Value,
                            });
                        }
                    }

                    if (runtime.LookAt != null)
                    {
                        snapshot.HasLookAt = true;
                        snapshot.LookAtYaw = runtime.LookAt.Yaw;
                        snapshot.LookAtPitch = runtime.LookAt.Pitch;
                    }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// 実行のたびに必ず変わる(または環境依存の)送信内容。比較対象から外す。
        /// </summary>
        public static readonly HashSet<string> IgnoredSentAddresses = new HashSet<string>
        {
            "/VMC/Ext/T",       //起動からの経過秒
            "/VMC/Ext/VRM",     //VRMの絶対パス
            "/VMC/Ext/Config",  //設定ファイルの絶対パス
            "/VMC/Ext/Remote",
            "/VMC/Ext/Opt",
        };

        public void SetSentMessages(IEnumerable<uOSC.Message> messages)
        {
            //同じ対象に対する複数フレーム分の送信は最後の値だけを残す(フレーム境界のゆらぎを吸収する)
            var latest = new Dictionary<string, VMCTestOscMessage>();
            var order = new List<string>();
            foreach (var message in messages)
            {
                if (IgnoredSentAddresses.Contains(message.address)) continue;
                var converted = VMCTestOscMessage.From(message);
                if (latest.ContainsKey(converted.Key) == false) order.Add(converted.Key);
                latest[converted.Key] = converted;
            }
            Sent = order.Select(d => latest[d]).OrderBy(d => d.Key, StringComparer.Ordinal).ToList();
        }

        #endregion

        #region IO

        public void Save(string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, FileName), JsonUtility.ToJson(this, true));
        }

        public static VMCTestSnapshot Load(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path) == false) return null;
            return JsonUtility.FromJson<VMCTestSnapshot>(File.ReadAllText(path));
        }

        #endregion

        #region 検査用ヘルパー

        public VMCTestPose GetBone(string boneName) => Bones.FirstOrDefault(d => d.Name == boneName);

        /// <summary>
        /// VMCProtocolが送受信するボーン(オリジナル)。
        /// 記録されていない場合(VRM1.0でないなど)は正規化ボーンで代用する。
        /// </summary>
        public VMCTestPose GetProtocolBone(string boneName)
            => OriginalBones.FirstOrDefault(d => d.Name == boneName) ?? GetBone(boneName);

        public float GetExpression(string name)
        {
            var entry = Expressions.FirstOrDefault(d => d.Name == name);
            return entry != null ? entry.Value : 0f;
        }

        /// <summary>
        /// 送信された /VMC/Ext/Bone/Pos が、実際のアバターのボーン姿勢と一致しているかを検査する。
        /// (VMCProtocolの受信結果と送信内容が食い違っていないことの確認)
        /// 戻り値は食い違いの一覧。空なら一致。
        /// </summary>
        public List<string> VerifySentBonesMatchState(float positionTolerance, float rotationToleranceDegrees)
        {
            var differences = new List<string>();
            int compared = 0;

            foreach (var message in Sent)
            {
                if (message.Address != "/VMC/Ext/Bone/Pos") continue;
                if (message.Args.Count != 8 || message.Args[0].T != "s") continue;

                //仕様上、送信されるのはオリジナル(非正規化)ボーンなのでそちらと比べる
                var bone = GetProtocolBone(message.Args[0].S);
                if (bone == null)
                {
                    differences.Add($"送信された {message.Args[0].S} がアバターに存在しません");
                    continue;
                }

                var sentPosition = new Vector3(message.Args[1].F, message.Args[2].F, message.Args[3].F);
                var sentRotation = new Quaternion(message.Args[4].F, message.Args[5].F, message.Args[6].F, message.Args[7].F);

                var distance = Vector3.Distance(bone.Position, sentPosition);
                if (distance > positionTolerance)
                {
                    differences.Add($"{bone.Name} の送信位置が状態と不一致 距離{distance:F5}");
                }
                var angle = Quaternion.Angle(bone.Rotation, sentRotation);
                if (angle > rotationToleranceDegrees)
                {
                    differences.Add($"{bone.Name} の送信回転が状態と不一致 角度{angle:F3}度");
                }
                compared++;
            }

            if (compared == 0)
            {
                differences.Add("/VMC/Ext/Bone/Pos が1件も送信されていません");
            }
            return differences;
        }

        /// <summary>
        /// ボーンの「回転だけ」を比較する。
        /// VRMA/BVHの往復はマッスル空間とglTFの量子化を通るため位置は一致しない。
        /// </summary>
        public static List<string> CompareBoneRotations(VMCTestSnapshot expected, VMCTestSnapshot actual,
            float toleranceDegrees, out float maxAngle, out string worstBone)
            => CompareBoneRotations(expected, actual, toleranceDegrees, null, out maxAngle, out worstBone);

        /// <summary>
        /// 末端ボーンまでの親子チェーン。
        /// ローカル回転を掛け合わせるとルートから見た向きが得られる。
        /// 個々のボーンの回転が違っても末端の向きが同じなら、見た目の姿勢は同じ。
        /// (Humanoidのリターゲットは腕のツイストを上腕と手の間で配分し直すため、
        ///  ボーン単位の比較だけでは「見た目が同じか」を判定できない)
        /// </summary>
        public static readonly (string Name, string[] Chain)[] EndEffectorChains =
        {
            ("頭", new[]{ "Hips","Spine","Chest","UpperChest","Neck","Head" }),
            ("左手", new[]{ "Hips","Spine","Chest","UpperChest","LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand" }),
            ("右手", new[]{ "Hips","Spine","Chest","UpperChest","RightShoulder","RightUpperArm","RightLowerArm","RightHand" }),
            ("左足", new[]{ "Hips","LeftUpperLeg","LeftLowerLeg","LeftFoot" }),
            ("右足", new[]{ "Hips","RightUpperLeg","RightLowerLeg","RightFoot" }),
        };

        /// <summary>チェーン上のローカル回転を掛け合わせて、ルートから見た向きを求める(無いボーンは飛ばす)</summary>
        public Quaternion GetAccumulatedRotation(string[] chain)
        {
            var rotation = Quaternion.identity;
            foreach (var boneName in chain)
            {
                var bone = GetBone(boneName);
                if (bone == null) continue;
                rotation = rotation * bone.Rotation;
            }
            return rotation;
        }

        /// <summary>末端ボーンの向き(=見た目の姿勢)を比較する</summary>
        public static List<string> CompareEndEffectors(VMCTestSnapshot expected, VMCTestSnapshot actual,
            float toleranceDegrees, out float maxAngle, out string worst)
        {
            var differences = new List<string>();
            maxAngle = 0f;
            worst = null;

            foreach (var (name, chain) in EndEffectorChains)
            {
                var angle = Quaternion.Angle(expected.GetAccumulatedRotation(chain), actual.GetAccumulatedRotation(chain));
                if (angle > maxAngle)
                {
                    maxAngle = angle;
                    worst = name;
                }
                if (angle > toleranceDegrees)
                {
                    differences.Add($"{name} {angle:F2}度");
                }
            }
            return differences;
        }

        /// <summary>指かどうか(マッスル空間の表現力が特に低いので別枠で扱う)</summary>
        public static bool IsFingerBone(string boneName)
            => boneName.Contains("Thumb") || boneName.Contains("Index") || boneName.Contains("Middle")
            || boneName.Contains("Ring") || boneName.Contains("Little");

        public static List<string> CompareBoneRotations(VMCTestSnapshot expected, VMCTestSnapshot actual,
            float toleranceDegrees, Func<string, bool> boneFilter, out float maxAngle, out string worstBone)
        {
            var differences = new List<string>();
            maxAngle = 0f;
            worstBone = null;

            foreach (var boneExpected in expected.Bones)
            {
                if (boneFilter != null && boneFilter(boneExpected.Name) == false) continue;
                var boneActual = actual.GetBone(boneExpected.Name);
                if (boneActual == null)
                {
                    differences.Add($"{boneExpected.Name} が存在しません");
                    continue;
                }
                var angle = Quaternion.Angle(boneExpected.Rotation, boneActual.Rotation);
                if (angle > maxAngle)
                {
                    maxAngle = angle;
                    worstBone = boneExpected.Name;
                }
                if (angle > toleranceDegrees)
                {
                    differences.Add($"{boneExpected.Name} {angle:F2}度");
                }
            }
            return differences;
        }

        /// <summary>表情の重みを比較する</summary>
        public static List<string> CompareExpressions(VMCTestSnapshot expected, VMCTestSnapshot actual,
            float tolerance, out float maxDelta, out string worstKey)
        {
            var differences = new List<string>();
            maxDelta = 0f;
            worstKey = null;

            foreach (var entry in expected.Expressions)
            {
                var value = actual.GetExpression(entry.Name);
                var delta = Mathf.Abs(entry.Value - value);
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                    worstKey = entry.Name;
                }
                if (delta > tolerance)
                {
                    differences.Add($"{entry.Name} {entry.Value:F3} -> {value:F3}");
                }
            }
            return differences;
        }

        /// <summary>2つのスナップショットの間で最も大きく回転したボーンの角度(度)</summary>
        public static float MaxBoneRotationDelta(VMCTestSnapshot a, VMCTestSnapshot b, out string boneName)
        {
            boneName = null;
            float max = 0f;
            if (a == null || b == null) return 0f;

            foreach (var boneA in a.Bones)
            {
                var boneB = b.GetBone(boneA.Name);
                if (boneB == null) continue;
                var angle = Quaternion.Angle(boneA.Rotation, boneB.Rotation);
                if (angle > max)
                {
                    max = angle;
                    boneName = boneA.Name;
                }
            }
            return max;
        }

        #endregion

        #region Compare

        /// <summary>
        /// ゴールデンとの差分を列挙する。空リストなら一致。
        /// </summary>
        public List<string> CompareTo(VMCTestSnapshot expected, VMCTestConfig config)
        {
            var differences = new List<string>();
            if (expected == null)
            {
                differences.Add("期待値(ゴールデン)が存在しません");
                return differences;
            }

            ComparePose(differences, "Root", expected.RootPose, RootPose, config);

            CompareByName(differences, "Bone", expected.Bones, Bones, d => d.Name,
                (diffs, name, e, a) => ComparePose(diffs, $"Bone[{name}]", e, a, config));

            CompareByName(differences, "Expression", expected.Expressions, Expressions, d => d.Name,
                (diffs, name, e, a) =>
                {
                    if (Mathf.Abs(e.Value - a.Value) > config.WeightTolerance)
                    {
                        diffs.Add($"Expression[{name}] weight {e.Value:F4} -> {a.Value:F4}");
                    }
                });

            if (expected.HasLookAt != HasLookAt)
            {
                differences.Add($"LookAt の有無が異なります {expected.HasLookAt} -> {HasLookAt}");
            }
            else if (HasLookAt)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(expected.LookAtYaw, LookAtYaw)) > config.RotationToleranceDegrees)
                {
                    differences.Add($"LookAt.Yaw {expected.LookAtYaw:F3} -> {LookAtYaw:F3}");
                }
                if (Mathf.Abs(Mathf.DeltaAngle(expected.LookAtPitch, LookAtPitch)) > config.RotationToleranceDegrees)
                {
                    differences.Add($"LookAt.Pitch {expected.LookAtPitch:F3} -> {LookAtPitch:F3}");
                }
            }

            CompareByName(differences, "Sent", expected.Sent, Sent, d => d.Key,
                (diffs, key, e, a) => CompareOsc(diffs, key, e, a, config));

            return differences;
        }

        private static void CompareByName<T>(List<string> differences, string category, List<T> expected, List<T> actual,
            Func<T, string> keySelector, Action<List<string>, string, T, T> compare)
        {
            expected = expected ?? new List<T>();
            actual = actual ?? new List<T>();

            var expectedMap = new Dictionary<string, T>();
            foreach (var item in expected) expectedMap[keySelector(item)] = item;
            var actualMap = new Dictionary<string, T>();
            foreach (var item in actual) actualMap[keySelector(item)] = item;

            foreach (var pair in expectedMap)
            {
                if (actualMap.TryGetValue(pair.Key, out var actualItem) == false)
                {
                    differences.Add($"{category}[{pair.Key}] が無くなりました");
                    continue;
                }
                compare(differences, pair.Key, pair.Value, actualItem);
            }
            foreach (var pair in actualMap)
            {
                if (expectedMap.ContainsKey(pair.Key) == false)
                {
                    differences.Add($"{category}[{pair.Key}] が増えました");
                }
            }
        }

        private static void ComparePose(List<string> differences, string label, VMCTestPose expected, VMCTestPose actual, VMCTestConfig config)
        {
            if (expected == null && actual == null) return;
            if (expected == null || actual == null)
            {
                differences.Add($"{label} の有無が異なります");
                return;
            }

            var distance = Vector3.Distance(expected.Position, actual.Position);
            if (distance > config.PositionTolerance)
            {
                differences.Add($"{label}.Position 距離{distance:F5} {Format(expected.Position)} -> {Format(actual.Position)}");
            }

            var angle = Quaternion.Angle(expected.Rotation, actual.Rotation);
            if (angle > config.RotationToleranceDegrees)
            {
                differences.Add($"{label}.Rotation 角度{angle:F3}度 {Format(expected.Rotation)} -> {Format(actual.Rotation)}");
            }
        }

        private static void CompareOsc(List<string> differences, string key, VMCTestOscMessage expected, VMCTestOscMessage actual, VMCTestConfig config)
        {
            if (expected.Args.Count != actual.Args.Count)
            {
                differences.Add($"Sent[{key}] 引数の数 {expected.Args.Count} -> {actual.Args.Count}");
                return;
            }

            for (int i = 0; i < expected.Args.Count; i++)
            {
                var e = expected.Args[i];
                var a = actual.Args[i];
                if (e.T != a.T)
                {
                    differences.Add($"Sent[{key}] 引数{i} の型 {e.T} -> {a.T}");
                    continue;
                }
                switch (e.T)
                {
                    case "f":
                        //位置と回転が混在するため、より緩い方(位置)の許容誤差で比較する
                        if (Mathf.Abs(e.F - a.F) > config.PositionTolerance)
                        {
                            differences.Add($"Sent[{key}] 引数{i} {e.F:F6} -> {a.F:F6}");
                        }
                        break;
                    case "i":
                        if (e.I != a.I) differences.Add($"Sent[{key}] 引数{i} {e.I} -> {a.I}");
                        break;
                    case "s":
                        if (e.S != a.S) differences.Add($"Sent[{key}] 引数{i} \"{e.S}\" -> \"{a.S}\"");
                        break;
                }
            }
        }

        private static string Format(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";
        private static string Format(Quaternion q) => $"({q.x:F4}, {q.y:F4}, {q.z:F4}, {q.w:F4})";

        #endregion
    }
}
