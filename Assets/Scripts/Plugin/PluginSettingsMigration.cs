using System;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    /// <summary>
    /// mocopi / VIVE / Tobii が本体機能だった頃の設定を、プラグインの設定領域へ移す。
    ///
    /// 旧フィールド(Settings.mocopi_* 等)は設定ファイルの互換のため残してある。
    /// 移行後はプラグイン側の値が正となり、旧フィールドは読み書きされない
    /// (設定ファイル内の値はそのまま保持され、古いバージョンでも読める)。
    ///
    /// 本体側にプラグイン固有の知識が残るのはこのファイルだけで、
    /// 役目は設定ファイル1つにつき一度きりの移行に限られる。
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
            //mocopiプラグインは設定を mocopi_SetSetting の形そのままで1キーに保存する
            Set(s, "mocopi", "Setting", new MocopiSetting
            {
                enable = s.mocopi_Enable,
                port = s.mocopi_Port,
                ApplyRootPosition = s.mocopi_ApplyRootPosition,
                ApplyRootRotation = s.mocopi_ApplyRootRotation,
                ApplyChest = s.mocopi_ApplyChest,
                ApplySpine = s.mocopi_ApplySpine,
                ApplyHead = s.mocopi_ApplyHead,
                ApplyLeftArm = s.mocopi_ApplyLeftArm,
                ApplyRightArm = s.mocopi_ApplyRightArm,
                ApplyLeftHand = s.mocopi_ApplyLeftHand,
                ApplyRightHand = s.mocopi_ApplyRightHand,
                ApplyLeftLeg = s.mocopi_ApplyLeftLeg,
                ApplyRightLeg = s.mocopi_ApplyRightLeg,
                ApplyLeftFoot = s.mocopi_ApplyLeftFoot,
                ApplyRightFoot = s.mocopi_ApplyRightFoot,
                CorrectHipBone = s.mocopi_CorrectHipBone,
            });
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

            var position = s.EyeTracking_TobiiPosition;
            if (position != null)
            {
                //プラグイン側が持つ形(TobiiPlugin.StoredTransform)に合わせて書き出す。
                //旧データは親なしのTransformなので、localの値がそのままワールド値になる
                Set(s, id, "MonitorPosition", new TobiiMonitorPosition
                {
                    px = position.localPosition.x,
                    py = position.localPosition.y,
                    pz = position.localPosition.z,
                    rx = position.localRotation.x,
                    ry = position.localRotation.y,
                    rz = position.localRotation.z,
                    rw = position.localRotation.w,
                });
            }
        }

        /// <summary>TobiiPlugin.StoredTransform と同じ形</summary>
        [Serializable]
        private class TobiiMonitorPosition
        {
            public float px, py, pz;
            public float rx, ry, rz, rw;
        }

        /// <summary>
        /// VMC.Plugin.Commands.mocopi_SetSetting と同じ形。
        /// JSONはメンバー名で対応付けるので、名前を揃えておけば読み込める。
        /// </summary>
        [Serializable]
        private class MocopiSetting
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
    }
}
