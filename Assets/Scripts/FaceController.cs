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
    public bool StopBlink = false;

    public float BlinkTimeMin = 1.0f;           //まばたきするまでの最短時間
    public float BlinkTimeMax = 10.0f;          //まばたきするまでの最長時間
    public float CloseAnimationTime = 0.06f;    //目を閉じるアニメーション時間
    public float OpenAnimationTime = 0.03f;     //目を開くアニメーション時間
    public float ClosingTime = 0.1f;            //目を閉じたままにする時間

    private bool IsSetting = false;

    public List<string> BlendShapeKeys; //読み込んだモデルの表情のキー一覧

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
            }
        }
    }
    public string FacePresetName = null;

    private AnimationController animationController;

    private Dictionary<BlendShapeKey, float> CurrentShapeKeys;
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
            animationController.AddResetAction(() => MixPreset(BlendShapePreset.Blink, 0.0f));
            animationController.AddWait(null, () => BlinkTimeMin + Random.value * (BlinkTimeMax - BlinkTimeMin));
            animationController.AddAnimation(CloseAnimationTime, 0.0f, 1.0f, v => MixPreset(BlendShapePreset.Blink, v));
            animationController.AddWait(ClosingTime);
            animationController.AddAnimation(OpenAnimationTime, 1.0f, 0.0f, v => MixPreset(BlendShapePreset.Blink, v));
        }
    }

    public void SetBlink_L(float value)
    {
        if (ViveProEyeEnabled == false)
        {
            MixPreset(BlendShapePreset.Blink, 0.0f);
        }
        MixPreset(BlendShapePreset.Blink_L, value);
    }
    public void SetBlink_R(float value)
    {
        if (ViveProEyeEnabled == false)
        {
            MixPreset(BlendShapePreset.Blink, 0.0f);
        }
        MixPreset(BlendShapePreset.Blink_R, value);
    }

    private void SetFaceNeutral()
    {
        //表情をデフォルトに戻す
        if (proxy != null)
        {
            var keys = new List<string>();
            var values = new List<float>();
            foreach(var keyname in BlendShapeKeys)
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
            CurrentShapeKeys = dict;
            proxy.SetValues(dict.ToList());
        }
    }

    public void MixPreset(BlendShapePreset preset, float value)
    {
        if (proxy == null) return;
        if (CurrentShapeKeys == null) return;
        var presetKey = new BlendShapeKey(preset);
        foreach (var shapekey in CurrentShapeKeys)
        {
            if (shapekey.Key.Equals(presetKey)) continue;
            proxy.AccumulateValue(shapekey.Key, shapekey.Value);
        }
        CurrentShapeKeys[presetKey] = value;
        proxy.AccumulateValue(preset, value);
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

                if (DefaultFace != BlendShapePreset.Neutral && proxy != null)
                {
                    if (DefaultFace != BlendShapePreset.Unknown)
                    {
                        SetFace(DefaultFace, 1.0f, StopBlink);
                    }
                    else if (string.IsNullOrEmpty(FacePresetName) == false)
                    {
                        SetFace(FacePresetName, 1.0f, StopBlink);
                    }
                }
            }
        }

    }
}
