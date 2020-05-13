using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRM;

public class FaceController : MonoBehaviour
{
    private GameObject VRMmodel;

    private VRMBlendShapeProxy proxy;

    public bool EnableBlink = false;
    public bool ViveProEyeEnabled = false;

    private bool stopBlink = false;
    public bool StopBlink
    {
        get
        {
            return stopBlink;
        }
        set
        {
            if (value == true)
            {
                animationController?.Reset();
            }
            stopBlink = value;
        }
    }

    public float BlinkTimeMin = 1.0f;           //まばたきするまでの最短時間
    public float BlinkTimeMax = 10.0f;          //まばたきするまでの最長時間
    public float CloseAnimationTime = 0.06f;    //目を閉じるアニメーション時間
    public float OpenAnimationTime = 0.03f;     //目を開くアニメーション時間
    public float ClosingTime = 0.1f;            //目を閉じたままにする時間

    private bool IsSetting = false;

    public List<string> BlendShapeKeys; //読み込んだモデルの表情のキー一覧

    public System.Action BeforeApply;

    private BlendShapePreset defaultFace = BlendShapePreset.Neutral;
    public BlendShapePreset DefaultFace
    {
        get { return defaultFace; }
        set
        {
            if (defaultFace != value)
            {
                //前回の表情を消しておく
                if (proxy != null)
                {
                    if (defaultFace != BlendShapePreset.Unknown)
                    {
                        SetFace(defaultFace, 0.0f, StopBlink);
                    }
                    else if (string.IsNullOrEmpty(FacePresetName) == false)
                    {
                        SetFace(FacePresetName, 0.0f, StopBlink);
                    }
                }
                defaultFace = value;
                //新しい表情を設定する
                if (proxy != null)
                {
                    if (defaultFace != BlendShapePreset.Unknown)
                    {
                        SetFace(defaultFace, 1.0f, StopBlink);
                    }
                    else if (string.IsNullOrEmpty(FacePresetName) == false)
                    {
                        SetFace(FacePresetName, 1.0f, StopBlink);
                    }
                }
            }
        }
    }
    public string FacePresetName = null;

    private AnimationController animationController;

    private Dictionary<BlendShapeKey, float> CurrentShapeKeys;
    private Dictionary<string, Dictionary<BlendShapeKey, float>> AccumulateShapeKeys = new Dictionary<string, Dictionary<BlendShapeKey, float>>();
    private BlendShapeKey NeutralKey = new BlendShapeKey(BlendShapePreset.Neutral);

    public void ImportVRMmodel(GameObject vrmmodel)
    {
        VRMmodel = vrmmodel;
        proxy = null;
    }

    private void Start()
    {
        var dict = new Dictionary<BlendShapeKey, float>();
        foreach (var key in BlendShapeKeys)
        {
            dict.Add(new BlendShapeKey(key), 0.0f);
        }
        CurrentShapeKeys = dict;

        CreateAnimation();
    }

    private void CreateAnimation()
    {
        if (animationController == null) animationController = new AnimationController();
        if (proxy != null)
        {
            animationController.ClearAnimations();
            animationController.AddResetAction(() => MixPreset("Blink", BlendShapePreset.Blink, 0.0f));
            animationController.AddWait(null, () => BlinkTimeMin + Random.value * (BlinkTimeMax - BlinkTimeMin));
            animationController.AddAnimation(CloseAnimationTime, 0.0f, 1.0f, v => MixPreset("Blink", BlendShapePreset.Blink, v));
            animationController.AddWait(ClosingTime);
            animationController.AddAnimation(OpenAnimationTime, 1.0f, 0.0f, v => MixPreset("Blink", BlendShapePreset.Blink, v));
        }
    }

    public void SetBlink_L(float value)
    {
        if (ViveProEyeEnabled == false)
        {
            MixPreset("Blink", BlendShapePreset.Blink, 0.0f);
        }
        if (StopBlink)
        {
            MixPreset("Blink_L", BlendShapePreset.Blink_L, 0.0f);
        }
        else
        {
            MixPreset("Blink_L", BlendShapePreset.Blink_L, value);
        }
    }
    public void SetBlink_R(float value)
    {
        if (ViveProEyeEnabled == false)
        {
            MixPreset("Blink", BlendShapePreset.Blink, 0.0f);
        }
        if (StopBlink)
        {
            MixPreset("Blink_R", BlendShapePreset.Blink_L, 0.0f);
        }
        else
        {
            MixPreset("Blink_R", BlendShapePreset.Blink_R, value);
        }
    }

    private void SetFaceNeutral()
    {
        //表情をデフォルトに戻す
        if (proxy != null)
        {
            var keys = new List<string>();
            var values = new List<float>();
            foreach (var keyname in BlendShapeKeys)
            {
                var shapekey = new BlendShapeKey(keyname);
                if (shapekey.Equals(NeutralKey))
                {
                    values.Add(1.0f);
                }
                else
                {
                    values.Add(0.0f);
                }
                keys.Add(keyname);
            }
            SetFace(keys, values, StopBlink);
        }
    }

    public void StartSetting()
    {
        IsSetting = true;
        SetFaceNeutral();
    }

    public void EndSetting()
    {
        SetFaceNeutral();
        IsSetting = false;
    }

    public void SetFace(BlendShapePreset preset, float strength, bool stopBlink)
    {
        SetFace(new BlendShapeKey(preset), strength, stopBlink);
    }

    public void SetFace(BlendShapeKey key, float strength, bool stopBlink)
    {
        SetFace(new List<string> { key.Name }, new List<float> { strength }, stopBlink);
    }

    public void SetFace(string key, float strength, bool stopBlink)
    {
        SetFace(new List<string> { key }, new List<float> { strength }, stopBlink);
    }

    public void SetFace(List<string> keys, List<float> strength, bool stopBlink)
    {
        if (proxy != null)
        {
            StopBlink = stopBlink;
            var dict = new Dictionary<BlendShapeKey, float>();
            foreach (var key in BlendShapeKeys)
            {
                dict.Add(new BlendShapeKey(key), 0.0f);
            }
            //dict[NeutralKey] = 1.0f;
            for (int i = 0; i < keys.Count; i++)
            {
                dict[new BlendShapeKey(keys[i])] = strength[i];
            }
            //現在のベースの表情を更新する
            CurrentShapeKeys = dict;
        }
    }

    public void MixPreset(string presetName, BlendShapePreset preset, float value)
    {
        MixPresets(presetName, new[] { preset }, new[] { value });
    }

    public void MixPresets(string presetName, BlendShapePreset[] presets, float[] values)
    {
        if (proxy == null) return;
        if (CurrentShapeKeys == null) return;

        MixPresets(presetName, presets.Select(d => new BlendShapeKey(d)).ToArray(), values);
    }

    public void MixPresets(string presetName, BlendShapeKey[] presets, float[] values)
    {
        if (proxy == null) return;
        if (CurrentShapeKeys == null) return;

        if (AccumulateShapeKeys.ContainsKey(presetName) == false)
        {
            AccumulateShapeKeys.Add(presetName, new Dictionary<BlendShapeKey, float>());
        }
        var presetDictionary = AccumulateShapeKeys[presetName];
        presetDictionary.Clear();
        //Mixしたい表情を合成する
        for (int i = 0; i < presets.Length; i++)
        {
            var presetKey = presets[i];
            presetDictionary.Add(presetKey, values[i]);
        }
    }

    private void AccumulateBlendShapes()
    {
        if (proxy == null) return;
        foreach (var shapeKey in CurrentShapeKeys)
        {
            proxy.AccumulateValue(shapeKey.Key, shapeKey.Value);
        }
        foreach (var presets in AccumulateShapeKeys)
        {
            foreach (var preset in presets.Value)
            {
                proxy.AccumulateValue(preset.Key, preset.Value);
            }
        }

        BeforeApply?.Invoke();

        proxy.Apply();
    }

    private bool isReset = false;

    // Update is called once per frame
    void Update()
    {
        if (VRMmodel != null)
        {
            if (proxy == null)
            {
                proxy = VRMmodel.GetComponent<VRMBlendShapeProxy>();
                //すべての表情の名称一覧を取得
                if (proxy != null) BlendShapeKeys = proxy.BlendShapeAvatar.Clips.Select(d => BlendShapeKey.CreateFrom(d).Name).ToList();
                SetFaceNeutral();
            }
            if (IsSetting == false)
            {
                if (EnableBlink && ViveProEyeEnabled == false)
                {
                    isReset = false;
                    if (StopBlink == false)
                    {
                        if (animationController?.Next() == false)
                        {//最後まで行ったら値更新のためにアニメーション作り直す
                            CreateAnimation();
                        }
                    }
                }
                else
                {
                    if (isReset == false)
                    {
                        isReset = true;
                        animationController?.Reset();
                    }
                }

            }

            AccumulateBlendShapes();
        }

    }
}
