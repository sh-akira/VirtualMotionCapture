using System;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    /// <summary>
    /// mocopi / VIVE / Tobii が本体機能だった頃の設定を、プラグインの設定領域へ移す。
    ///
    /// 旧フィールド(Settings.mocopi_* 等)は設定ファイルの互換のため残してあるが、
    /// 移行後はプラグイン側の値が正となる。移行は設定ファイル1つにつき一度だけ行う。
    /// </summary>
    internal static class PluginSettingsMigration
    {
        private const string MigratedKey = "_migrated/DevicePlugins";

        public static void Migrate(Settings settings)
        {
            if (settings == null) return;
            if (settings.PluginSettings == null) settings.PluginSettings = new Dictionary<string, string>();
            if (settings.PluginSettings.ContainsKey(MigratedKey)) return;

            try
            {
                MigrateMocopi(settings);
                MigrateViveSR(settings);
                MigrateTobii(settings);
            }
            catch (Exception ex)
            {
                //移行に失敗しても起動は続ける(プラグイン側の既定値で動く)
                Debug.LogWarning($"[Plugin] 旧設定の移行に失敗しました: {ex.Message}");
            }

            settings.PluginSettings[MigratedKey] = "true";
        }

        private static void Set<T>(Settings settings, string pluginId, string key, T value)
        {
            settings.PluginSettings[pluginId + "/" + key] = sh_akira.Json.Serializer.Serialize(value);
        }

        private static void MigrateMocopi(Settings s)
        {
            const string id = "mocopi";
            Set(s, id, "Enable", s.mocopi_Enable);
            Set(s, id, "Port", s.mocopi_Port);
            Set(s, id, "ApplyRootPosition", s.mocopi_ApplyRootPosition);
            Set(s, id, "ApplyRootRotation", s.mocopi_ApplyRootRotation);
            Set(s, id, "ApplyChest", s.mocopi_ApplyChest);
            Set(s, id, "ApplySpine", s.mocopi_ApplySpine);
            Set(s, id, "ApplyHead", s.mocopi_ApplyHead);
            Set(s, id, "ApplyLeftArm", s.mocopi_ApplyLeftArm);
            Set(s, id, "ApplyRightArm", s.mocopi_ApplyRightArm);
            Set(s, id, "ApplyLeftHand", s.mocopi_ApplyLeftHand);
            Set(s, id, "ApplyRightHand", s.mocopi_ApplyRightHand);
            Set(s, id, "ApplyLeftLeg", s.mocopi_ApplyLeftLeg);
            Set(s, id, "ApplyRightLeg", s.mocopi_ApplyRightLeg);
            Set(s, id, "ApplyLeftFoot", s.mocopi_ApplyLeftFoot);
            Set(s, id, "ApplyRightFoot", s.mocopi_ApplyRightFoot);
            Set(s, id, "CorrectHipBone", s.mocopi_CorrectHipBone);
        }

        private static void MigrateViveSR(Settings s)
        {
            const string id = "ViveSR";
            Set(s, id, "EyeEnable", s.EyeTracking_ViveProEyeEnable);
            Set(s, id, "EyeScaleHorizontal", s.EyeTracking_ViveProEyeScaleHorizontal);
            Set(s, id, "EyeScaleVertical", s.EyeTracking_ViveProEyeScaleVertical);
            Set(s, id, "EyeOffsetHorizontal", s.EyeTracking_ViveProEyeOffsetHorizontal);
            Set(s, id, "EyeOffsetVertical", s.EyeTracking_ViveProEyeOffsetVertical);
            Set(s, id, "UseEyelidMovements", s.EyeTracking_ViveProEyeUseEyelidMovements);
            Set(s, id, "LipEnable", s.LipTracking_ViveEnable);
            if (s.LipShapesToBlendShapeMap != null)
            {
                Set(s, id, "LipShapesToBlendShapeMap", s.LipShapesToBlendShapeMap);
            }
        }

        private static void MigrateTobii(Settings s)
        {
            const string id = "Tobii";
            Set(s, id, "ScaleHorizontal", s.EyeTracking_TobiiScaleHorizontal);
            Set(s, id, "ScaleVertical", s.EyeTracking_TobiiScaleVertical);
            Set(s, id, "OffsetHorizontal", s.EyeTracking_TobiiOffsetHorizontal);
            Set(s, id, "OffsetVertical", s.EyeTracking_TobiiOffsetVertical);
            Set(s, id, "CenterX", s.EyeTracking_TobiiCenterX);
            Set(s, id, "CenterY", s.EyeTracking_TobiiCenterY);
            if (s.EyeTracking_TobiiPosition != null)
            {
                Set(s, id, "MonitorPosition", s.EyeTracking_TobiiPosition);
            }
        }
    }
}
