using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ViveSR.anipal.Lip;

namespace VMC
{
    public class LipTracking_Vive : MonoBehaviour
    {
        public FaceController faceController;
        public ControlWPFWindow controlWPFWindow;

        private Dictionary<LipShape_v2, float> LipWeightings;
        public Dictionary<LipShape_v2, string> LipShapeToStringKeyMap = new Dictionary<LipShape_v2, string>();
        public Dictionary<string, LipShape_v2> LipShapeNameToEnumMap = new Dictionary<string, LipShape_v2>();


        void Awake()
        {
            controlWPFWindow.SetLipShapeToBlendShapeStringMapAction += SetLipShapeToBlendShapeStringMap;
            controlWPFWindow.GetLipShapesStringListFunc = GetLipShapesStringList;
            controlWPFWindow.LipTracking_ViveComponent = this;
            controlWPFWindow.SRanipal_Lip_FrameworkComponent = GetComponent<SRanipal_Lip_Framework>();
            enabled = false;
        }

        void Update()
        {
            if (SRanipal_Lip_Framework.Status != SRanipal_Lip_Framework.FrameworkStatus.WORKING) return;

            if (LipWeightings == null)
            {
                if (!SRanipal_Lip_Framework.Instance.EnableLip)
                {
                    enabled = false;
                    return;
                }

                //Get All Shapes
                SRanipal_Lip_v2.GetLipWeightings(out LipWeightings);
                foreach (var weighting in LipWeightings)
                {
                    if (Enum.IsDefined(typeof(LipShape_v2), weighting.Key))
                    {
                        LipShapeNameToEnumMap[weighting.Key.ToString()] = weighting.Key;
                    }
                }
            }

            SRanipal_Lip_v2.GetLipWeightings(out LipWeightings);

            var keyvalues = new Dictionary<string, float>();
            foreach (var weighting in LipWeightings)
            {
                if (LipShapeToStringKeyMap.ContainsKey(weighting.Key))
                {
                    keyvalues[LipShapeToStringKeyMap[weighting.Key]] = weighting.Value;
                }
            }
            if (keyvalues.Any())
            {
                faceController.MixPresets(nameof(LipTracking_Vive), keyvalues.Keys.ToArray(), keyvalues.Values.ToArray());
            }
        }

        public List<string> GetLipShapesStringList()
        {
            return LipShapeNameToEnumMap.Keys.ToList();
        }

        public Dictionary<string, string> GetLipShapeToBlendShapeStringMap()
        {
            var dict = new Dictionary<string, string>();
            foreach (var map in LipShapeToStringKeyMap)
            {
                dict.Add(map.Key.ToString(), map.Value);
            }
            return dict;
        }

        public void SetLipShapeToBlendShapeStringMap(Dictionary<string, string> stringMap)
        {
            LipShapeToStringKeyMap.Clear();
            foreach (var map in stringMap)
            {
                if (LipShapeNameToEnumMap.ContainsKey(map.Key))
                {
                    LipShapeToStringKeyMap[LipShapeNameToEnumMap[map.Key]] = map.Value;
                }
            }
        }
    }
}