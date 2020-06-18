//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

public class MIDICCBlendShape : MonoBehaviour
{
    public MidiCCWrapper midiCCWrapper;
    public string[] KnobToBlendShape = new string[MidiCCWrapper.KNOBS]; //BlendShapeとKnobを紐付けるキー配列

    FaceController faceController = null;

    //キーが存在するか
    bool available = false;

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
            for (int i = 0; i < MidiCCWrapper.KNOBS; i++)
            {
                //キーが登録されている場合
                if (KnobToBlendShape[i] != null && KnobToBlendShape[i] != "")
                {
                    //表情を反映する
                    faceController.MixPreset(nameof(MIDICCBlendShape), KnobToBlendShape[i], midiCCWrapper.CCValue[i]);
                    available = true;
                }
            }
        }
    }
}
