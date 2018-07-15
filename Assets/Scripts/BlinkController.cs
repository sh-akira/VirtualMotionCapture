using System.Collections;
using System.Collections.Generic;
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

    Coroutine m_coroutine;

    public void ImportVRMmodel(GameObject vrmmodel)
    {
        VRMmodel = vrmmodel;
        proxy = null;
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            while (EnableBlink)
            {
                while (proxy == null)
                {
                    yield return null;
                }
                var nextBlinkTime = Time.time + BlinkTimeMin + Random.value * (BlinkTimeMax - BlinkTimeMin);
                while (nextBlinkTime > Time.time)
                {
                    yield return null;
                }

                // 閉じる
                var value = 0.0f;
                var closeSpeed = 1.0f / CloseAnimationTime;
                while (true)
                {
                    value += Time.deltaTime * closeSpeed;
                    if (value >= 1.0f)
                    {
                        break;
                    }

                    proxy.SetValue(BlendShapePreset.Blink, value);
                    yield return null;
                }
                proxy.SetValue(BlendShapePreset.Blink, 1.0f);

                // wait...
                yield return new WaitForSeconds(ClosingTime);

                // open
                value = 1.0f;
                var openSpeed = 1.0f / OpenAnimationTime;
                while (true)
                {
                    value -= Time.deltaTime * openSpeed;
                    if (value < 0)
                    {
                        break;
                    }

                    proxy.SetValue(BlendShapePreset.Blink, value);
                    yield return null;
                }
                proxy.SetValue(BlendShapePreset.Blink, 0);
            }
            yield return null; //EnableBlink == false
        }
    }

    private void Start()
    {
        m_coroutine = StartCoroutine(BlinkRoutine());
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
