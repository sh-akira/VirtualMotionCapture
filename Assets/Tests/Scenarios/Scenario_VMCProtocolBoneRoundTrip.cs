using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// VMCProtocolのボーン受信 → 送信の同値性。
    ///
    /// 既知のボーン姿勢を /VMC/Ext/Bone/Pos で送り込み、
    /// アバターに適用された結果が同じ値で送信されて出てくるかを確認する。
    /// (VMCを2台数珠つなぎにしたときに姿勢が変質しないことの確認)
    /// </summary>
    public sealed class Scenario_VMCProtocolBoneRoundTrip : VMCTestScenario
    {
        public override string Name => "VMCProtocolBoneRoundTrip";

        public override string Description => "ボーンをVMCProtocolで受信し、同じ値が送信されるか";

        /// <summary>送り込むボーンの回転(ローカル回転をこの角度だけ回す)</summary>
        private static readonly (HumanBodyBones bone, Vector3 euler)[] Perturbations =
        {
            (HumanBodyBones.Head, new Vector3(10f, 20f, 0f)),
            (HumanBodyBones.Spine, new Vector3(5f, 0f, 8f)),
            (HumanBodyBones.LeftUpperArm, new Vector3(0f, 0f, 35f)),
            (HumanBodyBones.RightUpperArm, new Vector3(0f, 0f, -35f)),
            (HumanBodyBones.LeftLowerArm, new Vector3(0f, 25f, 0f)),
            (HumanBodyBones.LeftIndexProximal, new Vector3(0f, 0f, 20f)),
            (HumanBodyBones.RightHand, new Vector3(0f, 15f, 0f)),
            (HumanBodyBones.LeftUpperLeg, new Vector3(12f, 0f, 0f)),
        };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            //--- 2. ボーン受信用の受信機を作る ---
            context.Log("2. ボーン受信用の受信機を作成");
            var receiver = context.CreateReceiver(setting =>
            {
                setting.ApplyTracker = false;   //トラッカーは使わない(ボーン受信のみを見る)
                setting.ApplyBlendShape = false;
                setting.ApplyLookAt = false;
                setting.FixHandBone = false;    //手首の補正を入れると1:1で戻らなくなるため切る
                setting.UseBonePosition = false; //位置は受信しない(VMCProtocolの通常運用と同じ)
                setting.IgnoreDefaultBone = false; //送った値がそのまま反映されるようにする
            });

            //--- 3. 既知の姿勢を送り込む ---
            context.Log("3. ボーン姿勢の送り込み");
            var animator = context.CurrentModel.GetComponent<Animator>();
            //VMCProtocolが送受信するのはオリジナル(非正規化)ボーンなので、
            //送り込む姿勢もそこから作る(animator.GetBoneTransformはControlRigの正規化ボーン)
            var humanoid = context.CurrentModel.GetComponent<UniVRM10.Vrm10Instance>()?.Humanoid;
            var sentRotations = new Dictionary<string, Quaternion>();
            var messages = new List<uOSC.Message>();

            var rootTransform = animator.transform;
            messages.Add(VMCTestOscBuilder.Root(rootTransform.localPosition, rootTransform.localRotation));

            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                var boneTransform = humanoid != null ? humanoid.GetBoneTransform(bone) : animator.GetBoneTransform(bone);
                if (boneTransform == null) continue;

                var rotation = boneTransform.localRotation;
                foreach (var perturbation in Perturbations)
                {
                    if (perturbation.bone == bone)
                    {
                        rotation = rotation * Quaternion.Euler(perturbation.euler);
                        break;
                    }
                }

                sentRotations[bone.ToString()] = rotation;
                messages.Add(VMCTestOscBuilder.Bone(bone.ToString(), boneTransform.localPosition, rotation));
            }

            context.Inject(receiver, messages);
            yield return context.Step(10);

            //--- 4. 受信した姿勢がアバターに反映されているか ---
            context.Log("4. 受信結果の確認");
            var receivedSnapshot = context.Capture("01_received", includeSent: false);
            result.CheckSnapshot(context, receivedSnapshot);

            var receiveDifferences = new List<string>();
            float maxReceiveError = 0f;
            foreach (var perturbation in Perturbations)
            {
                var name = perturbation.bone.ToString();
                if (sentRotations.TryGetValue(name, out var expected) == false) continue;
                var actual = receivedSnapshot.GetProtocolBone(name);
                if (actual == null)
                {
                    receiveDifferences.Add($"{name} がアバターに存在しません");
                    continue;
                }
                var angle = Quaternion.Angle(expected, actual.Rotation);
                maxReceiveError = Mathf.Max(maxReceiveError, angle);
                if (angle > context.Config.RotationToleranceDegrees)
                {
                    receiveDifferences.Add($"{name} {angle:F2}度ずれ");
                }
            }

            result.CheckThat("ボーンの受信",
                receiveDifferences.Count == 0,
                $"送り込んだボーン姿勢がアバターに反映されていません(最大 {maxReceiveError:F2}度): {string.Join(", ", receiveDifferences)}");

            //--- 5. 同じ値が送信されて出てくるか ---
            context.Log("5. 送信内容の確認");
            context.EnableSender();
            yield return context.Step(3);
            context.ClearSent();
            yield return context.Step(4);

            var sentSnapshot = context.Capture("02_sent", includeSent: true);
            result.CheckSnapshot(context, sentSnapshot);

            //(a) 送信内容が現在のアバターの状態と一致していること
            var stateDifferences = sentSnapshot.VerifySentBonesMatchState(
                context.Config.PositionTolerance, context.Config.RotationToleranceDegrees);
            result.CheckThat("送信ボーンと状態の一致",
                stateDifferences.Count == 0,
                $"送信されたボーン姿勢が実際のアバターと食い違っています: {string.Join(" / ", Head(stateDifferences, 5))}");

            //(b) 受信した値そのものが送信されて出てくること(受信→送信の同値性)
            var roundTripDifferences = new List<string>();
            float maxRoundTripError = 0f;
            int compared = 0;
            foreach (var message in sentSnapshot.Sent)
            {
                if (message.Address != "/VMC/Ext/Bone/Pos") continue;
                if (message.Args.Count != 8 || message.Args[0].T != "s") continue;
                if (sentRotations.TryGetValue(message.Args[0].S, out var expected) == false) continue;

                var actual = new Quaternion(message.Args[4].F, message.Args[5].F, message.Args[6].F, message.Args[7].F);
                var angle = Quaternion.Angle(expected, actual);
                maxRoundTripError = Mathf.Max(maxRoundTripError, angle);
                compared++;
                if (angle > context.Config.RotationToleranceDegrees)
                {
                    roundTripDifferences.Add($"{message.Args[0].S} {angle:F2}度");
                }
            }

            result.CheckThat("受信と送信の同値性",
                compared > 0 && roundTripDifferences.Count == 0,
                compared == 0
                    ? "送信内容に /VMC/Ext/Bone/Pos が含まれていません"
                    : $"{compared}本中 {roundTripDifferences.Count}本で受信値と送信値が違います(最大 {maxRoundTripError:F2}度): {string.Join(", ", Head(roundTripDifferences, 8))}");

            context.DisableSender();
        }

        private static IEnumerable<string> Head(List<string> list, int count)
            => list.GetRange(0, Mathf.Min(count, list.Count));
    }
}
