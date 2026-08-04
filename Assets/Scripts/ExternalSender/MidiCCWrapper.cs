//gpsnmeajp
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Minis;

namespace VMC
{
    /// <summary>
    /// MIDIチャンネル(0-15)。従来のMidiJack.MidiChannelと同じ値・並び(Ch1=0 ... Ch16=15, All=16)。
    /// MidiJackからMinisへ移行した後も、下流(InputManager/ExternalSender)のシグネチャを維持するために定義。
    /// </summary>
    public enum MidiChannel
    {
        Ch1,    // 0
        Ch2,    // 1
        Ch3,
        Ch4,
        Ch5,
        Ch6,
        Ch7,
        Ch8,
        Ch9,
        Ch10,
        Ch11,
        Ch12,
        Ch13,
        Ch14,
        Ch15,
        Ch16,
        All     // 16
    }

    /// <summary>
    /// MIDI入力の集約ラッパー。
    /// 旧MidiJackは毎フレームMIDIポートへ再接続する実装で、新しいWindows MIDIサービス上ではフリーズの原因になるため、
    /// Unity Input System上に構築された後継のMinis(デバイス数変化時のみ接続)へ移行した。
    /// MidiJackのAPI(MidiMaster.*Delegate)に触れていたのは本クラスのみで、外部インターフェースは従来通り。
    /// </summary>
    public class MidiCCWrapper : MonoBehaviour
    {
        public const int KNOBS = 128; //最大ノブ数
        public const float Threshold = 0.5f; //bool判定しきい値

        //集約用デリゲートプロキシ(入力を即時通知する)
        public Action<MidiChannel, int, float> noteOnDelegateProxy = null;
        public Action<MidiChannel, int> noteOffDelegateProxy = null;
        public Action<MidiChannel, int, float> knobDelegateProxy = null;

        //フレーム単位にまるめて変化を通知するデリゲート
        public Action<int, float> knobUpdateFloatDelegate = null;
        public Action<int, bool> knobUpdateBoolDelegate = null;

        //デリゲートを使わず現在値を取得するインターフェース
        public float[] CCValue = new float[KNOBS];
        public bool[] CCBoolValueInFrame = new bool[KNOBS];

        //変化検出用の内部変数
        private bool CCAnyUpdate = false;
        private bool[] CCUpdateBit = new bool[KNOBS];

        void Start()
        {
            //既に接続済みのMIDIデバイスをフック
            foreach (var device in InputSystem.devices)
            {
                TryHookDevice(device);
            }
            //以降に接続されるMIDIデバイスをフック(Minisはデバイス数変化時のみ接続する)
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(UnityEngine.InputSystem.InputDevice device, UnityEngine.InputSystem.InputDeviceChange change)
        {
            if (change == UnityEngine.InputSystem.InputDeviceChange.Added)
            {
                TryHookDevice(device);
            }
        }

        private void TryHookDevice(UnityEngine.InputSystem.InputDevice device)
        {
            if (device is MidiDevice midi == false) return;

            //MinisのMidiDeviceは1チャンネル=1デバイス。チャンネルは固定なのでフック時に確定させる
            var channel = (MidiChannel)midi.channel;

            midi.onWillNoteOn += (MidiNoteControl note, float velocity) =>
            {
                //velocity 0 のNoteOnはNoteOff扱い(MIDI慣習。旧MidiJack実装と同じ挙動)
                if (velocity != 0f)
                {
                    noteOnDelegateProxy?.Invoke(channel, note.noteNumber, velocity);
                }
                else
                {
                    noteOffDelegateProxy?.Invoke(channel, note.noteNumber);
                }
            };

            midi.onWillNoteOff += (MidiNoteControl note) =>
            {
                noteOffDelegateProxy?.Invoke(channel, note.noteNumber);
            };

            midi.onWillControlChange += (MidiValueControl control, float value) =>
            {
                KnobUpdated(channel, control.controlNumber, value);
            };
        }

        public void KnobUpdated(MidiChannel channel, int knobNo, float value)
        {
            if (knobDelegateProxy != null)
            {
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
        }

        void Update()
        {
            //どれかでも更新があったら
            if (CCAnyUpdate)
            {
                CCAnyUpdate = false;

                //全要素走査
                for (int i = 0; i < KNOBS; i++)
                {
                    //更新があったら、通知する
                    if (CCUpdateBit[i])
                    {
                        CCUpdateBit[i] = false;

                        //値を直接通知する
                        if (knobUpdateFloatDelegate != null)
                        {
                            knobUpdateFloatDelegate.Invoke(i, CCValue[i]);
                        }

                        //--------

                        //しきい値チェック

                        //しきい値以上、かつ直前がfalseなら
                        if ((CCValue[i] >= Threshold) && (CCBoolValueInFrame[i] == false))
                        {
                            //trueにして通知
                            CCBoolValueInFrame[i] = true;
                            if (knobUpdateBoolDelegate != null)
                            {
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
}
