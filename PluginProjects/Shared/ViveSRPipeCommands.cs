using System.Collections.Generic;

namespace VMC.Plugin.Commands
{
    /// <summary>
    /// VIVE Pro Eye / VIVE Facial Tracker プラグインのコマンド定義。
    /// 本体側(VMC.Plugin.ViveSR)とコントロールパネル側(VMC.Plugin.ViveSR.UI)の
    /// 両方にリンクしてコンパイルされる。
    /// </summary>
    public static class ViveSRCommands
    {
        public static IEnumerable<System.Type> Types => new[]
        {
            typeof(GetEyeTracking_ViveProEyeOffsets),
            typeof(SetEyeTracking_ViveProEyeOffsets),
            typeof(GetEyeTracking_ViveProEyeUseEyelidMovements),
            typeof(SetEyeTracking_ViveProEyeUseEyelidMovements),
            typeof(GetEyeTracking_ViveProEyeEnable),
            typeof(SetEyeTracking_ViveProEyeEnable),
            typeof(GetViveLipTrackingBlendShape),
            typeof(SetViveLipTrackingBlendShape),
            typeof(GetViveLipTrackingEnable),
            typeof(SetViveLipTrackingEnable),
        };
    }

    public class GetEyeTracking_ViveProEyeOffsets { }
    public class SetEyeTracking_ViveProEyeOffsets
    {
        public float ScaleHorizontal { get; set; }
        public float ScaleVertical { get; set; }
        public float OffsetHorizontal { get; set; }
        public float OffsetVertical { get; set; }
    }

    public class GetEyeTracking_ViveProEyeUseEyelidMovements { }
    public class SetEyeTracking_ViveProEyeUseEyelidMovements
    {
        public bool Use { get; set; }
    }

    public class GetEyeTracking_ViveProEyeEnable { }
    public class SetEyeTracking_ViveProEyeEnable
    {
        public bool enable { get; set; }
    }

    public class GetViveLipTrackingBlendShape { }
    public class SetViveLipTrackingBlendShape
    {
        public List<string> LipShapes { get; set; }
        public Dictionary<string, string> LipShapesToBlendShapeMap { get; set; }
    }

    public class GetViveLipTrackingEnable { }
    public class SetViveLipTrackingEnable
    {
        public bool enable { get; set; }
    }
}
