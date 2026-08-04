using System.Collections.Generic;
using UniVRM10;

namespace VMC
{
    /// <summary>
    /// VRM0.xとVRM1.0のプリセット表情名の相互変換
    /// VMCProtocolの仕様上、VRM1.0使用時もVRM0形式での送信を必ず実装する必要がある
    /// 対応表: https://protocol.vmc.info/marionette-spec (VRM0系とVRM1系の非互換性に関する警告)
    /// </summary>
    public static class VRM10CompatibleNames
    {
        private static readonly Dictionary<ExpressionPreset, string> PresetToVRM0 = new Dictionary<ExpressionPreset, string>
        {
            { ExpressionPreset.happy,      "Joy" },
            { ExpressionPreset.angry,      "Angry" },
            { ExpressionPreset.sad,        "Sorrow" },
            { ExpressionPreset.relaxed,    "Fun" },
            { ExpressionPreset.aa,         "A" },
            { ExpressionPreset.ih,         "I" },
            { ExpressionPreset.ou,         "U" },
            { ExpressionPreset.ee,         "E" },
            { ExpressionPreset.oh,         "O" },
            { ExpressionPreset.blink,      "Blink" },
            { ExpressionPreset.blinkLeft,  "Blink_L" },
            { ExpressionPreset.blinkRight, "Blink_R" },
            { ExpressionPreset.lookUp,     "LookUp" },
            { ExpressionPreset.lookDown,   "LookDown" },
            { ExpressionPreset.lookLeft,   "LookLeft" },
            { ExpressionPreset.lookRight,  "LookRight" },
            { ExpressionPreset.neutral,    "Neutral" },
            // surprisedはVRM0.xにプリセットが無いためVRM1.0名のまま扱う
        };

        /// <summary>
        /// VRM1.0プリセット→VRM0.x名称の対応表(受信側の互換キー登録用)
        /// </summary>
        public static IReadOnlyDictionary<ExpressionPreset, string> PresetToVrm0Names => PresetToVRM0;

        /// <summary>
        /// プリセット表情はVRM0.xの名称(Joy, A, Blink_L等)、カスタム表情は元の名称を返す
        /// </summary>
        public static string GetVRM0CompatibleName(ExpressionKey key)
        {
            if (PresetToVRM0.TryGetValue(key.Preset, out var name))
            {
                return name;
            }
            return key.Name;
        }
    }
}
