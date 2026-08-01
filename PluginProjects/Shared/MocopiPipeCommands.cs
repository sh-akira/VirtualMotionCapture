using System.Collections.Generic;

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

    public class mocopi_SetSetting
    {
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
