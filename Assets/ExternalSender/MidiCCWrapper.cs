//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MidiCCWrapper : MonoBehaviour {
    const int KNOBS = 128;
    const float Threshold = 0.5f;

    //デリゲート反応用
    public bool CCAnyUpdate = false;
    public float[] CCValue = new float[KNOBS];
    public bool[] CCUpdateBit = new bool[KNOBS];

    //フレーム内処理用
    public bool[] CCBoolValueInFrame = new bool[KNOBS];

    //MIDIJack集約用プロキシ
    public Action<MidiJack.MidiChannel, int, float> noteOnDelegateProxy = null;
    public Action<MidiJack.MidiChannel, int> noteOffDelegateProxy = null;
    public Action<MidiJack.MidiChannel, int, float> knobDelegateProxy = null;

    //フレーム単位で処理するデリゲート
    public Action<int, float> knobUpdateFloatDelegate = null;
    public Action<int, bool> knobUpdateBoolDelegate = null;

    void Start () {
        MidiJack.MidiMaster.noteOnDelegate += (MidiJack.MidiChannel channel, int note, float velocity) =>
        {
            if (velocity != 0)
            {
                if (noteOnDelegateProxy != null)
                {
                    noteOnDelegateProxy.Invoke(channel, note, velocity);
                }
            }
            else {
                if (noteOffDelegateProxy != null)
                {
                    noteOffDelegateProxy.Invoke(channel, note);
                }
            }
        };
        MidiJack.MidiMaster.noteOffDelegate += (MidiJack.MidiChannel channel, int note) => {
            if (noteOffDelegateProxy != null) {
                noteOffDelegateProxy.Invoke(channel, note);
            }
        };

        MidiJack.MidiMaster.knobDelegate += (MidiJack.MidiChannel channel, int knobNo, float value) =>
        {
            if (knobDelegateProxy != null) {
                knobDelegateProxy.Invoke(channel, knobNo, value);
            }

            //範囲内かチェック
            if (0 <= knobNo && knobNo < KNOBS)
            {
                //値を記録する
                CCValue[knobNo] = value;
                CCUpdateBit[knobNo] = true;
                CCAnyUpdate = true;
            }
        };
    }

    void Update () {
        //どれかでも更新があったら
        if (CCAnyUpdate) {
            CCAnyUpdate = false;

            //全要素走査
            for (int i = 0; i < KNOBS; i++)
            {
                //更新があったら、通知する
                if (CCUpdateBit[i])
                {
                    CCUpdateBit[i] = false;

                    //値を直接通知する
                    if (knobUpdateFloatDelegate != null) {
                        knobUpdateFloatDelegate.Invoke(i, CCValue[i]);
                    }

                    //--------

                    //しきい値チェック

                    //しきい値以上、かつ直前がfalseなら
                    if ((CCValue[i] >= Threshold) && (CCBoolValueInFrame[i] == false))
                    {
                        //trueにして通知
                        CCBoolValueInFrame[i] = true;
                        if (knobUpdateBoolDelegate != null) {
                            knobUpdateBoolDelegate.Invoke(i, true);
                        }
                    }

                    //しきい値以下、かつ直前がtrueなら
                    if ((CCValue[i] < Threshold) && (CCBoolValueInFrame[i] == true))
                    {
                        //falseにして通知
                        CCBoolValueInFrame[i] = false;
                        if (knobUpdateBoolDelegate != null)
                        {
                            knobUpdateBoolDelegate.Invoke(i, false);
                        }
                    }

                }
            }
        }
    }
}
