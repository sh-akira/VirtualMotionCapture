//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

public class MIDICCBlendShape : MonoBehaviour {
    public MidiCCWrapper midiCCWrapper;
    public string[] KnobToBlendShape = new string[MidiCCWrapper.KNOBS];
    ControlWPFWindow window = null;
    VRMBlendShapeProxy blendShapeProxy = null;

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
        //全ノブを調べる
        if (blendShapeProxy != null)
        {
            for (int i = 0; i < MidiCCWrapper.KNOBS; i++)
            {
                if (KnobToBlendShape[i] != null && KnobToBlendShape[i] != "")
                {
                    blendShapeProxy.AccumulateValue(KnobToBlendShape[i], midiCCWrapper.CCValue[i]);
                }
            }
            blendShapeProxy.Apply();
        }
    }
}
