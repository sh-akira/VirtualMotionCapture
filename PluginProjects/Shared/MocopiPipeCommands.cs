using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VMC.Plugin.Commands
{
    /// <summary>
    /// mocopiプラグインのコマンド定義。
    ///
    /// このファイルは本体側(VMC.Plugin.Mocopi)とコントロールパネル側
    /// (VMC.Plugin.Mocopi.UI)の両方にリンクしてコンパイルされる。
    /// 型は単純名で対応付けられ、直列化の契約名は名前空間から決まるので、
    /// 双方で同じ名前空間・同じ形にしておく必要がある。
    /// </summary>
    public static class MocopiCommands
    {
        /// <summary>PipeCommands へ登録するコマンド型の一覧</summary>
        public static IEnumerable<System.Type> Types => new[]
        {
            typeof(mocopi_GetSetting),
            typeof(mocopi_SetSetting),
            typeof(mocopi_Recenter),
        };
    }

    public class mocopi_GetSetting { }

    /// <summary>
    /// mocopiの設定。この形のまま設定ファイルへも保存される。
    ///
    /// 直列化はメンバーが欠けていても例外にはならないが、そのままだと
    /// bool は false、int は 0 になってしまう。あとからメンバーを増やしたときに
    /// 既存ユーザーの設定が意図しない値にならないよう、
    /// [OnDeserializing] で既定値を入れてから読み込ませる。
    /// </summary>
    public class mocopi_SetSetting
    {
        public mocopi_SetSetting() => SetDefaults();

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults()
        {
            enable = true;
            port = 12351;
            ApplyRootPosition = true;
            ApplyRootRotation = true;
            ApplyChest = true;
            ApplySpine = true;
            ApplyHead = true;
            ApplyLeftArm = true;
            ApplyRightArm = true;
            ApplyLeftHand = true;
            ApplyRightHand = true;
            ApplyLeftLeg = true;
            ApplyRightLeg = true;
            ApplyLeftFoot = true;
            ApplyRightFoot = true;
            CorrectHipBone = false;
        }

        public bool enable { get; set; }
        public int port { get; set; }

        public bool ApplyRootPosition { get; set; }
        public bool ApplyRootRotation { get; set; }
        public bool ApplyChest { get; set; }
        public bool ApplySpine { get; set; }
        public bool ApplyHead { get; set; }
        public bool ApplyLeftArm { get; set; }
        public bool ApplyRightArm { get; set; }
        public bool ApplyLeftHand { get; set; }
        public bool ApplyRightHand { get; set; }
        public bool ApplyLeftLeg { get; set; }
        public bool ApplyRightLeg { get; set; }
        public bool ApplyLeftFoot { get; set; }
        public bool ApplyRightFoot { get; set; }
        public bool CorrectHipBone { get; set; }
    }

    public class mocopi_Recenter { }
}
