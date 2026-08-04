using System.Collections.Generic;

namespace VMC.Plugin.Commands
{
    /// <summary>
    /// Tobii アイトラッキングプラグインのコマンド定義。
    /// 本体側(VMC.Plugin.Tobii)とコントロールパネル側(VMC.Plugin.Tobii.UI)の
    /// 両方にリンクしてコンパイルされる。
    /// </summary>
    public static class TobiiCommands
    {
        public static IEnumerable<System.Type> Types => new[]
        {
            typeof(GetEyeTracking_TobiiOffsets),
            typeof(SetEyeTracking_TobiiOffsets),
            typeof(EyeTracking_TobiiCalibration),
            typeof(GetEyeTracking_TobiiEnable),
            typeof(SetEyeTracking_TobiiEnable),
        };
    }

    public class GetEyeTracking_TobiiEnable { }
    public class SetEyeTracking_TobiiEnable
    {
        public bool enable { get; set; }
    }

    public class GetEyeTracking_TobiiOffsets { }
    public class SetEyeTracking_TobiiOffsets
    {
        public float ScaleHorizontal { get; set; }
        public float ScaleVertical { get; set; }
        public float OffsetHorizontal { get; set; }
        public float OffsetVertical { get; set; }
    }

    public class EyeTracking_TobiiCalibration { }
}
