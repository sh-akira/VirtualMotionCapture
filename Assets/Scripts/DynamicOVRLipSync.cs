using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRM;

[RequireComponent(typeof(AudioSource))]
//OVRLipSync.csより後に起動しないとContextが取得できないのでExecutionOrderに気を付ける

public class DynamicOVRLipSync : OVRLipSyncContextBase
{
    private const bool EnableLowLatency = true;
    private int head = 0;
    private const int micFrequency = 44100;
    private const int lengthSeconds = 1;
    private float[] processBuffer = new float[1024];
    private float[] microphoneBuffer = new float[lengthSeconds * micFrequency];

    // smoothing amount
    public int SmoothAmount = 100;
    private GameObject VRMmodel;

    private VRMBlendShapeProxy proxy;

    public bool EnableLipSync = false;

    public float MaxLevel = 1.0f;

    public float Gain = 1.0f;
    public bool AudioMute = true;
    public bool DelayCompensate = false;
    public bool MaxWeightEmphasis = false;

    public float WeightThreashold = 0.00f;
    public bool MaxWeightEnable = false;

    private float sourceVolume = 100;
    private bool micSelected = false;

    public string selectedDevice = null;

    public string[] GetMicrophoneDevices() => Microphone.devices;
    public void SetMicrophoneDevice(string device)
    {
        StopMicrophone();
        micSelected = false;
        if (GetMicrophoneDevices().Contains(device) == false) device = null;
        selectedDevice = device;
        if (string.IsNullOrEmpty(device)) return;
        micSelected = true;
    }

    public void ImportVRMmodel(GameObject vrmmodel)
    {
        VRMmodel = vrmmodel;
        proxy = null;
    }

    // Use this for initialization
    void Start()
    {
        Smoothing = SmoothAmount;

        audioSource.loop = true;    // Set the AudioClip to loop
        audioSource.mute = false;

        if (Microphone.devices.Length != 0 && string.IsNullOrWhiteSpace(selectedDevice))
        {
            selectedDevice = Microphone.devices.Last().ToString();
            micSelected = true;
            GetMicCaps();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (EnableLipSync)
        {
            if (Context != 0)
            {
                if (proxy == null)
                {
                    if (VRMmodel != null) proxy = VRMmodel.GetComponent<VRMBlendShapeProxy>();
                }
                else
                {
                    // get the current viseme frame
                    OVRLipSync.Frame frame = GetCurrentPhonemeFrame();
                    if (frame != null)
                    {
                        //あ OVRLipSync.Viseme.aa; BlendShapePreset.A;
                        //い OVRLipSync.Viseme.ih; BlendShapePreset.I;
                        //う OVRLipSync.Viseme.ou; BlendShapePreset.U;
                        //え OVRLipSync.Viseme.E;  BlendShapePreset.E;
                        //お OVRLipSync.Viseme.oh; BlendShapePreset.O;
                        var presets = new BlendShapePreset[] {
                            BlendShapePreset.A,
                            BlendShapePreset.I,
                            BlendShapePreset.U,
                            BlendShapePreset.E,
                            BlendShapePreset.O,
                        };
                        var visemes = new float[] {
                            frame.Visemes[(int)OVRLipSync.Viseme.aa],
                            frame.Visemes[(int)OVRLipSync.Viseme.ih],
                            frame.Visemes[(int)OVRLipSync.Viseme.ou],
                            frame.Visemes[(int)OVRLipSync.Viseme.E],
                            frame.Visemes[(int)OVRLipSync.Viseme.oh],
                        };

                        int maxindex = 0;
                        float maxvisemes = 0;
                        for (int i = 0; i < presets.Length; i++)
                        {
                            if (visemes[i] < WeightThreashold) visemes[i] = 0;
                            if (maxvisemes < visemes[i])
                            {
                                maxindex = i;
                                maxvisemes = visemes[i];
                            }
                        }

                        if (MaxWeightEmphasis)
                        {
                            visemes[maxindex] = Mathf.Clamp(visemes[maxindex] * 3, 0.0f, 1.0f);
                        }

                        if (MaxWeightEnable)
                        {
                            for (int i = 0; i < presets.Length; i++)
                            {
                                if (i != maxindex) visemes[i] = 0.0f;
                            }
                        }

                        for (int i = 0; i < presets.Length; i++)
                        {
                            visemes[i] *= MaxLevel;
                            proxy.SetValue(presets[i], visemes[i]);
                        }


                        //Debug.Log("Visemes:" + string.Join(",", frame.Visemes.Select(d => d.ToString())));
                    }
                }
            }

            if (string.IsNullOrEmpty(selectedDevice) == false)
            {
                audioSource.volume = (sourceVolume / 100);
                if (!Microphone.IsRecording(selectedDevice))
                    StartMicrophone();

                if (EnableLowLatency)
                {
                    var position = Microphone.GetPosition(selectedDevice);
                    if (position < 0 || head == position)
                    {
                        return;
                    }

                    audioSource.clip.GetData(microphoneBuffer, 0);
                    while (GetDataLength(microphoneBuffer.Length, head, position) > processBuffer.Length)
                    {
                        var remain = microphoneBuffer.Length - head;
                        if (remain < processBuffer.Length)
                        {
                            Array.Copy(microphoneBuffer, head, processBuffer, 0, remain);
                            Array.Copy(microphoneBuffer, 0, processBuffer, remain, processBuffer.Length - remain);
                        }
                        else
                        {
                            Array.Copy(microphoneBuffer, head, processBuffer, 0, processBuffer.Length);
                        }

                        OVRLipSync.ProcessFrame(Context, processBuffer, Frame);

                        head += processBuffer.Length;
                        if (head > microphoneBuffer.Length)
                        {
                            head -= microphoneBuffer.Length;
                        }
                    }
                }
            }
        }
    }

    static int GetDataLength(int bufferLength, int head, int tail)
    {
        if (head < tail)
        {
            return tail - head;
        }
        else
        {
            return bufferLength - head + tail;
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (EnableLowLatency == false)
        {
            // Do not spatialize if we are not initialized, or if there is no
            // audio source attached to game object
            if ((OVRLipSync.IsInitialized() != OVRLipSync.Result.Success) || audioSource == null)
                return;

            // increase the gain of the input to get a better signal input
            for (int i = 0; i < data.Length; ++i)
                data[i] = data[i] * Gain;

            // Send data into Phoneme context for processing (if context is not 0)
            lock (this)
            {
                if (Context != 0)
                {
                    //OVRLipSync.Flags flags = 0;

                    // Set flags to feed into process
                    //if (DelayCompensate == true)
                    //    flags |= OVRLipSync.Flags.DelayCompensateAudio;

                    OVRLipSync.Frame frame = this.Frame;

                    OVRLipSync.ProcessFrameInterleaved(Context, data,/* flags,*/ frame);
                }
            }

        }
        // Turn off output (so that we don't get feedback from mics too close to speakers)
        if (AudioMute == true)
        {
            for (int i = 0; i < data.Length; ++i)
                data[i] = data[i] * 0.0f;
        }
    }

    private int minFreq, maxFreq;
    public void GetMicCaps()
    {
        if (micSelected == false) return;

        //Gets the frequency of the device
        Microphone.GetDeviceCaps(selectedDevice, out minFreq, out maxFreq);

        if (minFreq == 0 && maxFreq == 0)
        {
            Debug.LogWarning("GetMicCaps warning:: min and max frequencies are 0");
            minFreq = 44100;
            maxFreq = 44100;
        }

    }

    public void StartMicrophone()
    {
        if (micSelected == false) return;

        //Starts recording
        audioSource.clip = Microphone.Start(selectedDevice, true, 1, micFrequency);

        // Wait until the recording has started
        while (!(Microphone.GetPosition(selectedDevice) > 0)) { }

        // Play the audio source
        audioSource.Play();
    }

    public void StopMicrophone()
    {
        if (micSelected == false) return;

        // Overriden with a clip to play? Don't stop the audio source
        if ((audioSource != null) && (audioSource.clip != null) && (audioSource.clip.name == "Microphone"))
            audioSource.Stop();

        Microphone.End(selectedDevice);
    }


    void OnDisable()
    {
        StopMicrophone();
    }
}
