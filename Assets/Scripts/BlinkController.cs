using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRM;

public class BlinkController : MonoBehaviour
{
    private GameObject VRMmodel;

    private VRMBlendShapeProxy proxy;

    public bool EnableBlink = false;

    public float BlinkTimeMin = 1.0f;           //まばたきするまでの最短時間
    public float BlinkTimeMax = 10.0f;          //まばたきするまでの最長時間
    public float CloseAnimationTime = 0.06f;    //目を閉じるアニメーション時間
    public float OpenAnimationTime = 0.03f;     //目を開くアニメーション時間
    public float ClosingTime = 0.1f;            //目を閉じたままにする時間

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
        animationController.ClearAnimations();
        animationController.AddResetAction(() => proxy.SetValue(BlendShapePreset.Blink, 0.0f));
        animationController.AddWait(null, () => BlinkTimeMin + Random.value * (BlinkTimeMax - BlinkTimeMin));
        animationController.AddAnimation(CloseAnimationTime, 0.0f, 1.0f, v => proxy.SetValue(BlendShapePreset.Blink, v));
        animationController.AddWait(ClosingTime);
        animationController.AddAnimation(OpenAnimationTime, 1.0f, 0.0f, v => proxy.SetValue(BlendShapePreset.Blink, v));
    }

    // Update is called once per frame
    void Update()
    {
        if (EnableBlink)
        {
            if (VRMmodel != null)
            {
                if (proxy == null)
                {
                    proxy = VRMmodel.GetComponent<VRMBlendShapeProxy>();
                }
            }
            if (animationController?.Next() == false)
            {//最後まで行ったら値更新のためにアニメーション作り直す
                CreateAnimation();
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
