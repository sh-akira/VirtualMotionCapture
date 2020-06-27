//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRM;

public class MIDICCBlendShape : MonoBehaviour
{
    public MidiCCWrapper midiCCWrapper;
    public string[] KnobToBlendShape = new string[MidiCCWrapper.KNOBS]; //BlendShapeとKnobを紐付けるキー配列

    FaceController faceController = null;

    //キーが存在するか
    bool available = false;

    private Dictionary<string, BlendShapeKey> nameToKeyDictionary = new Dictionary<string, BlendShapeKey>();
    private Dictionary<string, float> nameToValueDictionary = new Dictionary<string, float>();

    void Start()
    {
        faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
    }

    void Update()
    {
        available = false;

        //全ノブを調べる
        if (faceController != null)
        {
            int valuecount = 0;
            for (int i = 0; i < MidiCCWrapper.KNOBS; i++)
            {
                //キーが登録されている場合
                if (KnobToBlendShape[i] != null && KnobToBlendShape[i] != "")
                {
                    //表情を反映する
                    var key = KnobToBlendShape[i];
                    if (nameToValueDictionary.ContainsKey(key) == false) UpdateDictionary();
                    nameToValueDictionary[key] = midiCCWrapper.CCValue[i];
                    available = true;
                    valuecount++;
                }
            }
            if (nameToKeyDictionary.Count != valuecount) UpdateDictionary();
            if (valuecount > 0) faceController.MixPresets(nameof(MIDICCBlendShape), nameToKeyDictionary.Values.ToArray(), nameToValueDictionary.Values.ToArray());
        }
    }

    private void UpdateDictionary()
    {
        nameToKeyDictionary.Clear();
        nameToValueDictionary.Clear();
        var keys = KnobToBlendShape.Where(d => d != null && d != "");
        foreach (var key in keys)
        {
            nameToKeyDictionary.Add(key, new BlendShapeKey(key));
            nameToValueDictionary.Add(key, 0);
        }
    }
}
