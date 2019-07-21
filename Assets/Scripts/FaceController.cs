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
                        proxy.SetValue(defaultFace, 0.0f);
                    }
                    else if (string.IsNullOrEmpty(FacePresetName) == false)
                    {
                        proxy.SetValue(FacePresetName, 0.0f);
                    }
                }
                defaultFace = value;
            }
        }
    }
    public string FacePresetName = null;

    private AnimationController animationController;

    public void ImportVRMmodel(GameObject vrmmodel)
    {
        VRMmodel = vrmmodel;
        proxy = null;
    }

    private void Start()
    {
        CreateAnimation();
    }

    private void CreateAnimation()
    {
        if (animationController == null) animationController = new AnimationController();
        if (proxy != null)
        {
            animationController.ClearAnimations();
            animationController.AddResetAction(() => proxy.SetValue(BlendShapePreset.Blink, 0.0f));
            animationController.AddWait(null, () => BlinkTimeMin + Random.value * (BlinkTimeMax - BlinkTimeMin));
            animationController.AddAnimation(CloseAnimationTime, 0.0f, 1.0f, v => proxy.SetValue(BlendShapePreset.Blink, v));
            animationController.AddWait(ClosingTime);
            animationController.AddAnimation(OpenAnimationTime, 1.0f, 0.0f, v => proxy.SetValue(BlendShapePreset.Blink, v));
        }
    }

    public void SetBlink_L(float value)
    {
        if (ViveProEyeEnabled == false)
        {
            proxy.ImmediatelySetValue(BlendShapePreset.Blink, 0.0f);
            ViveProEyeEnabled = true;
        }
        proxy.ImmediatelySetValue(BlendShapePreset.Blink_L, value);
    }
    public void SetBlink_R(float value)
    {
        if (ViveProEyeEnabled == false)
        {
            proxy.ImmediatelySetValue(BlendShapePreset.Blink, 0.0f);
            ViveProEyeEnabled = true;
        }
        proxy.ImmediatelySetValue(BlendShapePreset.Blink_R, value);
    }

    private void SetFaceNeutral()
    {
        //表情をデフォルトに戻す
        if (proxy != null)
        {
            var NeutralKey = new BlendShapeKey(BlendShapePreset.Neutral);
            proxy.SetValues(BlendShapeKeys.Select(d => { var k = new BlendShapeKey(d); return new KeyValuePair<BlendShapeKey, float>(k, k.Equals(NeutralKey) ? 1.0f : 0.0f); }));
            proxy.Apply();
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

    public void SetFace(List<string> keys, List<float> strength, bool stopBlink)
    {
        if (proxy != null)
        {
            StopBlink = stopBlink;
            var NeutralKey = new BlendShapeKey(BlendShapePreset.Neutral);
            var dict = new Dictionary<BlendShapeKey, float>();
            foreach (var key in BlendShapeKeys)
            {
                dict.Add(new BlendShapeKey(key), 0.0f);
            }
            dict[NeutralKey] = 1.0f;
            for (int i = 0; i < keys.Count; i++)
            {
                dict[new BlendShapeKey(keys[i])] = strength[i];
            }
            proxy.SetValues(dict.ToList());
            proxy.Apply();
        }
    }

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
            }
            if (IsSetting == false)
            {
                if (EnableBlink && ViveProEyeEnabled == false)
                {
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
                    animationController?.Reset();
                }

                if (DefaultFace != BlendShapePreset.Neutral && proxy != null)
                {
                    if (DefaultFace != BlendShapePreset.Unknown)
                    {
                        proxy.SetValue(DefaultFace, 1.0f);
                    }
                    else if (string.IsNullOrEmpty(FacePresetName) == false)
                    {
                        proxy.SetValue(FacePresetName, 1.0f);
                    }
                }
            }
        }

    }
}
