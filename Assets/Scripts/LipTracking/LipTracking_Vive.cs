using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ViveSR.anipal.Lip;
using VRM;

public class LipTracking_Vive : MonoBehaviour
{
    public FaceController faceController;

    private Dictionary<LipShape_v2, float> LipWeightings;
    public Dictionary<LipShape_v2, BlendShapeKey> LipShapeToBlendShapeMap = new Dictionary<LipShape_v2, BlendShapeKey>();
    public Dictionary<string, LipShape_v2> LipShapeNameToEnumMap = new Dictionary<string, LipShape_v2>();

    void Start()
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

    void Update()
    {
        if (SRanipal_Lip_Framework.Status != SRanipal_Lip_Framework.FrameworkStatus.WORKING) return;

        SRanipal_Lip_v2.GetLipWeightings(out LipWeightings);

        var keyvalues = new Dictionary<BlendShapeKey, float>();
        foreach(var weighting in LipWeightings)
        {
            if (LipShapeToBlendShapeMap.ContainsKey(weighting.Key))
            {
                keyvalues[LipShapeToBlendShapeMap[weighting.Key]] = weighting.Value;
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
        foreach (var map in LipShapeToBlendShapeMap)
        {
            dict.Add(map.Key.ToString(), map.Value.Name);
        }
        return dict;
    }

    public void SetLipShapeToBlendShapeStringMap(Dictionary<string, string> stringMap)
    {
        LipShapeToBlendShapeMap.Clear();
        foreach (var map in stringMap)
        {
            if (LipShapeNameToEnumMap.ContainsKey(map.Key))
            {
                var blendShapeKey = new BlendShapeKey(map.Value);
                LipShapeToBlendShapeMap[LipShapeNameToEnumMap[map.Key]] = blendShapeKey;
            }
        }
    }
}
