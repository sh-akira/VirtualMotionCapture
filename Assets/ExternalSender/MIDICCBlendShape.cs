//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

public class MIDICCBlendShape : MonoBehaviour {
    public MidiCCWrapper midiCCWrapper;
    public string[] KnobToBlendShape = new string[MidiCCWrapper.KNOBS]; //BlendShapeとKnobを紐付けるキー配列

    ControlWPFWindow window = null;
    VRMBlendShapeProxy blendShapeProxy = null;

    //キーが存在するか
    bool available = false;

    void Start()
    {
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            if (CurrentModel != null)
            {
                blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
            }
        };
    }

    void Update () {
        available = false;

        //全ノブを調べる
        if (blendShapeProxy != null)
        {
            for (int i = 0; i < MidiCCWrapper.KNOBS; i++)
            {
                //キーが登録されている場合
                if (KnobToBlendShape[i] != null && KnobToBlendShape[i] != "")
                {
                    //表情を反映する
                    blendShapeProxy.AccumulateValue(KnobToBlendShape[i], midiCCWrapper.CCValue[i]);
                    available = true;
                }
            }
        }
    }
}
