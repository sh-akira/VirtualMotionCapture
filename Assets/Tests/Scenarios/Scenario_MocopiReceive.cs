using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMC.Tests
{
    /// <summary>
    /// mocopi 受信の検証。
    ///
    /// MocopiConnector は UDP で受け取った内容を MocopiAvatar へ渡すだけなので、
    /// 実機のmocopiが無くても、受け口(InitializeSkeleton / UpdateSkeleton)へ
    /// 直接スケルトンを流し込めば同じ経路を通せる。
    /// </summary>
    public sealed class Scenario_MocopiReceive : VMCTestScenario
    {
        public override string Name => "MocopiReceive";

        public override string Description => "mocopiのスケルトン受信とアバターへの適用";

        public override IReadOnlyList<string> Models => new[] { VMCTestModels.Vrm0 };

        //mocopiのボーン構成(27本)。親は mocopi のスケルトン定義に従う
        private static readonly int[] ParentBoneIds =
        {
            -1, 0, 1, 2, 3, 4, 5, 6,   //0:root 1-7:torso
            7, 8, 9,                   //8-9:neck 10:head
            7, 11, 12, 13,             //11-14:左肩・腕・手
            7, 15, 16, 17,             //15-18:右肩・腕・手
            0, 19, 20, 21,             //19-22:左脚
            0, 23, 24, 25,             //23-26:右脚
        };

        public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
        {
            context.Log("1. VRM読み込み");
            context.ResetSettings();
            //mocopiは既定でUDPを開くのでポートは掴ませないが、コンポーネント自体は有効にする
            Settings.Current.mocopi_Enable = false;
            yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));

            var connector = Object.FindObjectOfType<MocopiConnector>(true);
            if (connector == null)
            {
                result.CheckThat("MocopiConnectorの存在", false, "MocopiConnector がシーンに見つかりません");
                yield break;
            }
            //モデル読み込み時に MocopiAvatar が作られるまで待つ
            yield return context.Step(10);

            //--- 2. スケルトン定義を流し込む ---
            context.Log("2. スケルトン定義の送信");
            var boneIds = Enumerable.Range(0, ParentBoneIds.Length).ToArray();
            BuildRestSkeleton(out var rotX, out var rotY, out var rotZ, out var rotW,
                out var posX, out var posY, out var posZ);

            context.BeginErrorCapture();
            connector.InitializeSkeleton(boneIds, ParentBoneIds, rotX, rotY, rotZ, rotW, posX, posY, posZ);
            yield return context.Step(10);
            var initErrors = context.EndErrorCapture();

            result.CheckThat("スケルトン定義の受信",
                initErrors.Count == 0,
                $"スケルトン定義の受信でエラーが出ました({initErrors.Count}件): " +
                string.Join(" / ", initErrors.Take(3)));

            var beforeFrame = context.Capture("01_mocopi_rest", includeSent: false);

            //--- 3. フレームデータで姿勢を動かす ---
            context.Log("3. フレームデータの送信");
            //肩から先を大きく回して、アバターが追従するか見る
            var frameRotations = BuildPoseRotations();

            context.BeginErrorCapture();
            for (int i = 0; i < 30; i++)
            {
                connector.UpdateSkeletonForTest(i, boneIds,
                    frameRotations.x, frameRotations.y, frameRotations.z, frameRotations.w,
                    posX, posY, posZ);
                yield return context.Step(1);
            }
            var frameErrors = context.EndErrorCapture();

            result.CheckThat("フレームデータの受信",
                frameErrors.Count == 0,
                $"フレームデータの受信でエラーが出ました({frameErrors.Count}件): " +
                string.Join(" / ", frameErrors.Take(3)));

            var afterFrame = context.Capture("02_mocopi_posed", includeSent: false);
            result.CheckSnapshot(context, afterFrame);

            var delta = VMCTestSnapshot.MaxBoneRotationDelta(beforeFrame, afterFrame, out var movedBone);
            result.CheckThat("mocopiでアバターが動くこと",
                delta > 10f,
                $"mocopiのフレームデータを送ってもアバターが動きません(最大回転差 {delta:F2}度 / {movedBone ?? "なし"})");

            Debug.Log($"[VMCTest] mocopi適用後の変化: 最大 {delta:F2}度 @ {movedBone}");
        }

        /// <summary>直立したレスト姿勢のスケルトン定義</summary>
        private static void BuildRestSkeleton(out float[] rotX, out float[] rotY, out float[] rotZ, out float[] rotW,
            out float[] posX, out float[] posY, out float[] posZ)
        {
            int count = ParentBoneIds.Length;
            rotX = new float[count];
            rotY = new float[count];
            rotZ = new float[count];
            rotW = new float[count];
            posX = new float[count];
            posY = new float[count];
            posZ = new float[count];

            //各ボーンの親からのオフセット(概ね人体の比率)
            var offsets = new Vector3[count];
            offsets[0] = new Vector3(0f, 1.0f, 0f);   //root(腰)
            for (int i = 1; i <= 7; i++) offsets[i] = new Vector3(0f, 0.08f, 0f);   //背骨
            offsets[8] = new Vector3(0f, 0.07f, 0f);  //neck_1
            offsets[9] = new Vector3(0f, 0.05f, 0f);  //neck_2
            offsets[10] = new Vector3(0f, 0.08f, 0f); //head
            offsets[11] = new Vector3(-0.05f, 0.05f, 0f); //左肩
            offsets[12] = new Vector3(-0.13f, 0f, 0f);
            offsets[13] = new Vector3(-0.26f, 0f, 0f);
            offsets[14] = new Vector3(-0.24f, 0f, 0f);
            offsets[15] = new Vector3(0.05f, 0.05f, 0f);  //右肩
            offsets[16] = new Vector3(0.13f, 0f, 0f);
            offsets[17] = new Vector3(0.26f, 0f, 0f);
            offsets[18] = new Vector3(0.24f, 0f, 0f);
            offsets[19] = new Vector3(-0.09f, -0.05f, 0f); //左脚
            offsets[20] = new Vector3(0f, -0.42f, 0f);
            offsets[21] = new Vector3(0f, -0.40f, 0f);
            offsets[22] = new Vector3(0f, -0.07f, 0.12f);
            offsets[23] = new Vector3(0.09f, -0.05f, 0f);  //右脚
            offsets[24] = new Vector3(0f, -0.42f, 0f);
            offsets[25] = new Vector3(0f, -0.40f, 0f);
            offsets[26] = new Vector3(0f, -0.07f, 0.12f);

            for (int i = 0; i < count; i++)
            {
                rotX[i] = 0f; rotY[i] = 0f; rotZ[i] = 0f; rotW[i] = 1f;
                posX[i] = offsets[i].x;
                posY[i] = offsets[i].y;
                posZ[i] = offsets[i].z;
            }
        }

        /// <summary>腕を横に上げた姿勢の回転</summary>
        private static (float[] x, float[] y, float[] z, float[] w) BuildPoseRotations()
        {
            int count = ParentBoneIds.Length;
            var x = new float[count];
            var y = new float[count];
            var z = new float[count];
            var w = new float[count];
            for (int i = 0; i < count; i++) { w[i] = 1f; }

            void Set(int index, Quaternion q)
            {
                x[index] = q.x; y[index] = q.y; z[index] = q.z; w[index] = q.w;
            }

            Set(12, Quaternion.Euler(0f, 0f, 55f));  //左上腕
            Set(16, Quaternion.Euler(0f, 0f, -55f)); //右上腕
            Set(10, Quaternion.Euler(12f, 20f, 0f)); //頭
            Set(3, Quaternion.Euler(8f, 0f, 0f));    //背骨
            return (x, y, z, w);
        }
    }
}
