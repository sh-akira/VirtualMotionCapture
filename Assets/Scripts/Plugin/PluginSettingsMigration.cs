using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace VMC
{
    /// <summary>
    /// mocopi / VIVE / Tobii が本体機能だった頃の設定を、プラグインの設定領域へ移す。
    ///
    /// 本体の Settings からは旧フィールドを削除済みなので、読み込んだ設定ファイルの
    /// 生JSONを直接読んで拾う。本体側にプラグイン固有の知識が残るのはこのファイルだけで、
    /// 役目は一度きりの移行に限られる。
    /// </summary>
    internal static class PluginSettingsMigration
    {
        private const string MigratedKey = "_migrated/DevicePlugins";

        public static void Migrate(Settings settings, string rawJson)
        {
            if (settings == null) return;
            if (settings.PluginSettings == null) settings.PluginSettings = new Dictionary<string, string>();
            if (settings.PluginSettings.ContainsKey(MigratedKey)) return;

            try
            {
                if (string.IsNullOrEmpty(rawJson) == false)
                {
                    var legacy = sh_akira.Json.Serializer.Deserialize<LegacySettings>(rawJson);
                    if (legacy != null)
                    {
                        MigrateMocopi(settings, legacy);
                        MigrateViveSR(settings, legacy);
                        MigrateTobii(settings, legacy);
                    }
                }
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

        private static void MigrateMocopi(Settings s, LegacySettings l)
        {
            const string id = "mocopi";
            Set(s, id, "Enable", l.mocopi_Enable);
            Set(s, id, "Port", l.mocopi_Port);
            Set(s, id, "ApplyRootPosition", l.mocopi_ApplyRootPosition);
            Set(s, id, "ApplyRootRotation", l.mocopi_ApplyRootRotation);
            Set(s, id, "ApplyChest", l.mocopi_ApplyChest);
            Set(s, id, "ApplySpine", l.mocopi_ApplySpine);
            Set(s, id, "ApplyHead", l.mocopi_ApplyHead);
            Set(s, id, "ApplyLeftArm", l.mocopi_ApplyLeftArm);
            Set(s, id, "ApplyRightArm", l.mocopi_ApplyRightArm);
            Set(s, id, "ApplyLeftHand", l.mocopi_ApplyLeftHand);
            Set(s, id, "ApplyRightHand", l.mocopi_ApplyRightHand);
            Set(s, id, "ApplyLeftLeg", l.mocopi_ApplyLeftLeg);
            Set(s, id, "ApplyRightLeg", l.mocopi_ApplyRightLeg);
            Set(s, id, "ApplyLeftFoot", l.mocopi_ApplyLeftFoot);
            Set(s, id, "ApplyRightFoot", l.mocopi_ApplyRightFoot);
            Set(s, id, "CorrectHipBone", l.mocopi_CorrectHipBone);
        }

        private static void MigrateViveSR(Settings s, LegacySettings l)
        {
            const string id = "ViveSR";
            Set(s, id, "EyeEnable", l.EyeTracking_ViveProEyeEnable);
            Set(s, id, "EyeScaleHorizontal", l.EyeTracking_ViveProEyeScaleHorizontal);
            Set(s, id, "EyeScaleVertical", l.EyeTracking_ViveProEyeScaleVertical);
            Set(s, id, "EyeOffsetHorizontal", l.EyeTracking_ViveProEyeOffsetHorizontal);
            Set(s, id, "EyeOffsetVertical", l.EyeTracking_ViveProEyeOffsetVertical);
            Set(s, id, "UseEyelidMovements", l.EyeTracking_ViveProEyeUseEyelidMovements);
            Set(s, id, "LipEnable", l.LipTracking_ViveEnable);
            if (l.LipShapesToBlendShapeMap != null)
            {
                Set(s, id, "LipShapesToBlendShapeMap", l.LipShapesToBlendShapeMap);
            }
        }

        private static void MigrateTobii(Settings s, LegacySettings l)
        {
            const string id = "Tobii";
            Set(s, id, "ScaleHorizontal", l.EyeTracking_TobiiScaleHorizontal);
            Set(s, id, "ScaleVertical", l.EyeTracking_TobiiScaleVertical);
            Set(s, id, "OffsetHorizontal", l.EyeTracking_TobiiOffsetHorizontal);
            Set(s, id, "OffsetVertical", l.EyeTracking_TobiiOffsetVertical);
            Set(s, id, "CenterX", l.EyeTracking_TobiiCenterX);
            Set(s, id, "CenterY", l.EyeTracking_TobiiCenterY);

            var position = l.EyeTracking_TobiiPosition;
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

        /// <summary>
        /// 設定ファイルから旧フィールドだけを拾うための入れ物。
        /// 知らないメンバーは無視されるので、これだけ書いておけば読める。
        ///
        /// DataContractJsonSerializer はインスタンスを初期化子を通さずに作るため、
        /// 既定値は Settings と同じく [OnDeserializing] で設定する
        /// (キーが無い設定ファイルで 0/false に化けるのを防ぐ)。
        /// </summary>
        [Serializable]
        private class LegacySettings
        {
            [OnDeserializing()]
            internal void OnDeserializingMethod(StreamingContext context)
            {
                mocopi_Enable = true;
                mocopi_Port = 12351;
                mocopi_ApplyRootPosition = true;
                mocopi_ApplyRootRotation = true;
                mocopi_ApplyChest = true;
                mocopi_ApplySpine = true;
                mocopi_ApplyHead = true;
                mocopi_ApplyLeftArm = true;
                mocopi_ApplyRightArm = true;
                mocopi_ApplyLeftHand = true;
                mocopi_ApplyRightHand = true;
                mocopi_ApplyLeftLeg = true;
                mocopi_ApplyRightLeg = true;
                mocopi_ApplyLeftFoot = true;
                mocopi_ApplyRightFoot = true;
                mocopi_CorrectHipBone = false;

                EyeTracking_ViveProEyeEnable = false;
                EyeTracking_ViveProEyeScaleHorizontal = 2.0f;
                EyeTracking_ViveProEyeScaleVertical = 1.5f;
                EyeTracking_ViveProEyeOffsetHorizontal = 0.0f;
                EyeTracking_ViveProEyeOffsetVertical = 0.0f;
                EyeTracking_ViveProEyeUseEyelidMovements = false;
                LipTracking_ViveEnable = false;
                LipShapesToBlendShapeMap = null;

                EyeTracking_TobiiScaleHorizontal = 0.5f;
                EyeTracking_TobiiScaleVertical = 0.2f;
                EyeTracking_TobiiOffsetHorizontal = 0.0f;
                EyeTracking_TobiiOffsetVertical = 0.0f;
                EyeTracking_TobiiCenterX = 0.5f;
                EyeTracking_TobiiCenterY = 0.5f;
                EyeTracking_TobiiPosition = null;
            }

            [OptionalField]
            public bool mocopi_Enable = true;
            [OptionalField]
            public int mocopi_Port = 12351;
            [OptionalField]
            public bool mocopi_ApplyRootPosition = true;
            [OptionalField]
            public bool mocopi_ApplyRootRotation = true;
            [OptionalField]
            public bool mocopi_ApplyChest = true;
            [OptionalField]
            public bool mocopi_ApplySpine = true;
            [OptionalField]
            public bool mocopi_ApplyHead = true;
            [OptionalField]
            public bool mocopi_ApplyLeftArm = true;
            [OptionalField]
            public bool mocopi_ApplyRightArm = true;
            [OptionalField]
            public bool mocopi_ApplyLeftHand = true;
            [OptionalField]
            public bool mocopi_ApplyRightHand = true;
            [OptionalField]
            public bool mocopi_ApplyLeftLeg = true;
            [OptionalField]
            public bool mocopi_ApplyRightLeg = true;
            [OptionalField]
            public bool mocopi_ApplyLeftFoot = true;
            [OptionalField]
            public bool mocopi_ApplyRightFoot = true;
            [OptionalField]
            public bool mocopi_CorrectHipBone = false;

            [OptionalField]
            public bool EyeTracking_ViveProEyeEnable = false;
            [OptionalField]
            public float EyeTracking_ViveProEyeScaleHorizontal = 2.0f;
            [OptionalField]
            public float EyeTracking_ViveProEyeScaleVertical = 1.5f;
            [OptionalField]
            public float EyeTracking_ViveProEyeOffsetHorizontal = 0.0f;
            [OptionalField]
            public float EyeTracking_ViveProEyeOffsetVertical = 0.0f;
            [OptionalField]
            public bool EyeTracking_ViveProEyeUseEyelidMovements = false;
            [OptionalField]
            public bool LipTracking_ViveEnable = false;
            [OptionalField]
            public Dictionary<string, string> LipShapesToBlendShapeMap = null;

            [OptionalField]
            public float EyeTracking_TobiiScaleHorizontal = 0.5f;
            [OptionalField]
            public float EyeTracking_TobiiScaleVertical = 0.2f;
            [OptionalField]
            public float EyeTracking_TobiiOffsetHorizontal = 0.0f;
            [OptionalField]
            public float EyeTracking_TobiiOffsetVertical = 0.0f;
            [OptionalField]
            public float EyeTracking_TobiiCenterX = 0.5f;
            [OptionalField]
            public float EyeTracking_TobiiCenterY = 0.5f;
            [OptionalField]
            public StoreTransform EyeTracking_TobiiPosition = null;
        }

        /// <summary>TobiiPlugin.StoredTransform と同じ形</summary>
        [Serializable]
        private class TobiiMonitorPosition
        {
            public float px, py, pz;
            public float rx, ry, rz, rw;
        }
    }
}
