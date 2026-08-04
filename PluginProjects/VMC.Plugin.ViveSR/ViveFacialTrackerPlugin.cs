using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMemoryMappedFile;
using VMC.Plugin.Commands;
using ViveSR.anipal.Lip;
using VMC.Plugin;

namespace VMC.Plugin.Vive
{
    /// <summary>
    /// VIVE Facial Tracker のリップトラッキング。
    /// 元は本体の LipTracking_Vive.cs。
    /// </summary>
    public class ViveFacialTrackerPlugin : MonoBehaviour, IVMCPlugin
    {
        public string Id => "ViveSR.Lip";
        public string DisplayName => "VIVE Facial Tracker";
        public string Version => "1.0.0";
        public System.Collections.Generic.IEnumerable<System.Type> CommandTypes => ViveSRCommands.Types;

        private IPluginHost host;
        private IPluginSettings settings;
        private SRanipal_Lip_Framework framework;

        private bool isEnabled = false;

        private Dictionary<LipShape_v2, float> lipWeightings;

        /// <summary>SRanipalのシェイプ → モデルの表情キー</summary>
        private readonly Dictionary<LipShape_v2, string> lipShapeToStringKeyMap = new Dictionary<LipShape_v2, string>();

        /// <summary>デバイスから報告されたシェイプ名 → enum</summary>
        private readonly Dictionary<string, LipShape_v2> lipShapeNameToEnumMap = new Dictionary<string, LipShape_v2>();

        public void Initialize(IPluginHost host)
        {
            this.host = host;
            settings = host.GetSettings("ViveSR");

            framework = gameObject.AddComponent<SRanipal_Lip_Framework>();
            framework.enabled = false;

            host.Ipc.Received += OnReceived;
            host.SettingsApplied += ApplySettings;
        }

        private void OnDestroy()
        {
            if (host == null) return;
            host.Ipc.Received -= OnReceived;
            host.SettingsApplied -= ApplySettings;
        }

        #region 設定

        private void OnReceived(object sender, DataReceivedEventArgs e)
        {
            host.Ipc.Post(async () =>
            {
                if (e.CommandType == typeof(GetViveLipTrackingBlendShape))
                {
                    await host.Ipc.SendCommandAsync(new SetViveLipTrackingBlendShape
                    {
                        LipShapes = lipShapeNameToEnumMap.Keys.ToList(),
                        LipShapesToBlendShapeMap = GetLipShapeToBlendShapeStringMap(),
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(SetViveLipTrackingBlendShape))
                {
                    var d = (SetViveLipTrackingBlendShape)e.Data;
                    settings.Set("LipShapesToBlendShapeMap", d.LipShapesToBlendShapeMap);
                    SetLipShapeToBlendShapeStringMap(d.LipShapesToBlendShapeMap);
                }
                else if (e.CommandType == typeof(GetViveLipTrackingEnable))
                {
                    await host.Ipc.SendCommandAsync(new SetViveLipTrackingEnable
                    {
                        enable = isEnabled,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(SetViveLipTrackingEnable))
                {
                    var d = (SetViveLipTrackingEnable)e.Data;
                    settings.Set("LipEnable", d.enable);
                    ApplyEnable();
                }
            });
        }

        private void ApplySettings()
        {
            SetLipShapeToBlendShapeStringMap(
                settings.Get("LipShapesToBlendShapeMap", new Dictionary<string, string>()));
            ApplyEnable();
        }

        private void ApplyEnable()
        {
            isEnabled = settings.Get("LipEnable", false);
            if (framework != null) framework.enabled = isEnabled;
        }

        private Dictionary<string, string> GetLipShapeToBlendShapeStringMap()
        {
            var dict = new Dictionary<string, string>();
            foreach (var map in lipShapeToStringKeyMap)
            {
                dict.Add(map.Key.ToString(), map.Value);
            }
            return dict;
        }

        private void SetLipShapeToBlendShapeStringMap(Dictionary<string, string> stringMap)
        {
            lipShapeToStringKeyMap.Clear();
            if (stringMap == null) return;

            foreach (var map in stringMap)
            {
                //デバイスから報告されたシェイプ名にしか割り当てない
                if (lipShapeNameToEnumMap.ContainsKey(map.Key))
                {
                    lipShapeToStringKeyMap[lipShapeNameToEnumMap[map.Key]] = map.Value;
                }
            }
        }

        #endregion

        private void Update()
        {
            if (isEnabled == false) return;
            if (SRanipal_Lip_Framework.Status != SRanipal_Lip_Framework.FrameworkStatus.WORKING) return;

            if (lipWeightings == null)
            {
                if (SRanipal_Lip_Framework.Instance.EnableLip == false) return;

                //最初の1回でデバイスが持つシェイプ名の一覧を作る
                SRanipal_Lip_v2.GetLipWeightings(out lipWeightings);
                foreach (var weighting in lipWeightings)
                {
                    if (Enum.IsDefined(typeof(LipShape_v2), weighting.Key))
                    {
                        lipShapeNameToEnumMap[weighting.Key.ToString()] = weighting.Key;
                    }
                }
                //一覧が出来てから割り当てを反映し直す
                SetLipShapeToBlendShapeStringMap(
                    settings.Get("LipShapesToBlendShapeMap", new Dictionary<string, string>()));
            }

            SRanipal_Lip_v2.GetLipWeightings(out lipWeightings);

            var keyvalues = new Dictionary<string, float>();
            foreach (var weighting in lipWeightings)
            {
                if (lipShapeToStringKeyMap.ContainsKey(weighting.Key))
                {
                    keyvalues[lipShapeToStringKeyMap[weighting.Key]] = weighting.Value;
                }
            }
            if (keyvalues.Any())
            {
                host.FaceControl.MixPresets("LipTracking_Vive", keyvalues.Keys.ToArray(), keyvalues.Values.ToArray());
            }
        }
    }
}
