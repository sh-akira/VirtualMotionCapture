using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRM;

namespace VMC
{
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

        public List<BlendShapeClip> BlendShapeClips; //読み込んだモデルの表情のキー一覧

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
                            SetFace(BlendShapeKey.CreateUnknown(FacePresetName), 0.0f, StopBlink);
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
                            SetFace(BlendShapeKey.CreateUnknown(FacePresetName), 1.0f, StopBlink);
                        }
                    }
                }
            }
        }
        public string FacePresetName = null;

        private AnimationController animationController;

        private Dictionary<BlendShapeKey, float> CurrentShapeKeys;
        private Dictionary<string, Dictionary<BlendShapeKey, float>> AccumulateShapeKeys = new Dictionary<string, Dictionary<BlendShapeKey, float>>();
        private Dictionary<string, Dictionary<BlendShapeKey, float>> OverwriteShapeKeys = new Dictionary<string, Dictionary<BlendShapeKey, float>>();
        private BlendShapeKey NeutralKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.Neutral);

        private Dictionary<string, BlendShapeKey> BlendShapeKeyString = new Dictionary<string, BlendShapeKey>();
        private Dictionary<string, string> KeyUpperCaseDictionary = new Dictionary<string, string>();
        public string GetCaseSensitiveKeyName(string upperCase)
        {
            if (KeyUpperCaseDictionary.Count == 0)
            {
                foreach (var presetName in System.Enum.GetNames(typeof(BlendShapePreset)))
                {
                    KeyUpperCaseDictionary[presetName.ToUpper()] = presetName;
                }
            }
            return KeyUpperCaseDictionary.ContainsKey(upperCase) ? KeyUpperCaseDictionary[upperCase] : upperCase;
        }

        public void ImportVRMmodel(GameObject vrmmodel)
        {
            VRMmodel = vrmmodel;
            proxy = null;
            InitializeProxy();
        }

        private void Start()
        {
            var dict = new Dictionary<BlendShapeKey, float>();
            foreach (var clip in BlendShapeClips)
            {
                dict.Add(clip.Key, 0.0f);
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
                var keys = new List<BlendShapeKey>();
                var values = new List<float>();
                foreach (var clip in BlendShapeClips)
                {
                    var shapekey = clip.Key;
                    if (shapekey.Equals(NeutralKey))
                    {
                        values.Add(1.0f);
                    }
                    else
                    {
                        values.Add(0.0f);
                    }
                    keys.Add(shapekey);
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
            SetFace(BlendShapeKey.CreateFromPreset(preset), strength, stopBlink);
        }

        public void SetFace(BlendShapeKey key, float strength, bool stopBlink)
        {
            SetFace(new List<BlendShapeKey> { key }, new List<float> { strength }, stopBlink);
        }

        public void SetFace(List<string> keys, List<float> strength, bool stopBlink)
        {
            if (proxy != null)
            {
                if (keys.Any(d => BlendShapeKeyString.ContainsKey(d) == false))
                {
                    var convertKeys = new List<BlendShapeKey>();
                    var convertValues = new List<float>();
                    for (int i = 0; i < keys.Count; i++)
                    {
                        var caseSensitiveKeyName = GetCaseSensitiveKeyName(keys[i]);
                        if (BlendShapeKeyString.ContainsKey(caseSensitiveKeyName))
                        {
                            convertKeys.Add(BlendShapeKeyString[caseSensitiveKeyName]);
                            convertValues.Add(strength[i]);
                        }
                    }
                    SetFace(convertKeys, convertValues, stopBlink);
                }
                else
                {
                    SetFace(keys.Select(d => BlendShapeKeyString[d]).ToList(), strength, stopBlink);
                }
            }
        }

        public void SetFace(List<BlendShapeKey> keys, List<float> strength, bool stopBlink)
        {
            if (proxy != null)
            {
                StopBlink = stopBlink;
                var dict = new Dictionary<BlendShapeKey, float>();
                foreach (var clip in BlendShapeClips)
                {
                    dict.Add(clip.Key, 0.0f);
                }
                //dict[NeutralKey] = 1.0f;
                for (int i = 0; i < keys.Count; i++)
                {
                    dict[keys[i]] = strength[i];
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
            MixPresets(presetName, presets.Select(d => BlendShapeKey.CreateFromPreset(d)).ToArray(), values);
        }

        public void MixPreset(string presetName, BlendShapeKey preset, float value)
        {
            MixPresets(presetName, new[] { preset }, new[] { value });
        }

        public void MixPresets(string presetName, string[] keys, float[] values)
        {
            if (keys.Any(d => BlendShapeKeyString.ContainsKey(d) == false))
            {
                var convertKeys = new List<BlendShapeKey>();
                var convertValues = new List<float>();
                for (int i = 0; i < keys.Length; i++)
                {
                    var caseSensitiveKeyName = GetCaseSensitiveKeyName(keys[i]);
                    if (BlendShapeKeyString.ContainsKey(caseSensitiveKeyName))
                    {
                        convertKeys.Add(BlendShapeKeyString[caseSensitiveKeyName]);
                        convertValues.Add(values[i]);
                    }
                }
                MixPresets(presetName, convertKeys.ToArray(), convertValues.ToArray());
            }
            else
            {
                MixPresets(presetName, keys.Select(d => BlendShapeKeyString[d]).ToArray(), values);
            }
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

        public void OverwritePresets(string presetName, BlendShapeKey[] presets, float[] values)
        {
            if (proxy == null) return;
            if (CurrentShapeKeys == null) return;

            if (OverwriteShapeKeys.ContainsKey(presetName) == false)
            {
                OverwriteShapeKeys.Add(presetName, new Dictionary<BlendShapeKey, float>());
            }
            var presetDictionary = OverwriteShapeKeys[presetName];
            presetDictionary.Clear();
            //上書きしたい表情を追加する
            for (int i = 0; i < presets.Length; i++)
            {
                var presetKey = presets[i];
                presetDictionary.Add(presetKey, values[i]);
            }
        }

        private void AccumulateBlendShapes()
        {
            if (proxy == null) return;
            var accumulatedValues = new Dictionary<BlendShapeKey, float>();
            //ベースの表情を設定する(使わない表情には全て0が入っている)
            foreach (var shapeKey in CurrentShapeKeys)
            {
                accumulatedValues[shapeKey.Key] = shapeKey.Value;
            }

            BeforeApply?.Invoke(); //MixPresetsする最後のチャンス

            //追加表情を合成する(最大値は超えないようにする)
            foreach (var presets in AccumulateShapeKeys)
            {
                foreach (var preset in presets.Value)
                {
                    if (accumulatedValues.ContainsKey(preset.Key)) // waidayo等から別のモデルのBlendShapeが送られてくる場合があるので存在チェックする
                    {
                        var value = accumulatedValues[preset.Key];
                        value += preset.Value;
                        if (value > 1.0f) value = 1.0f;
                        accumulatedValues[preset.Key] = value;
                    }
                }
            }

            //上書き表情を合成する(最大値は超えないようにする)
            foreach (var presets in OverwriteShapeKeys)
            {
                foreach (var preset in presets.Value)
                {
                    if (accumulatedValues.ContainsKey(preset.Key)) // waidayo等から別のモデルのBlendShapeが送られてくる場合があるので存在チェックする
                    {
                        var value = preset.Value;
                        if (value > 1.0f) value = 1.0f;
                        accumulatedValues[preset.Key] = value;
                    }
                }
            }

            //全ての表情をSetValuesで1度に反映させる
            proxy.SetValues(accumulatedValues);

            //SetValuesは内部でApplyまで行うためApply不要
        }

        private void InitializeProxy()
        {
            proxy = VRMmodel.GetComponent<VRMBlendShapeProxy>();
            //すべての表情の名称一覧を取得
            if (proxy != null)
            {
                BlendShapeClips = proxy.BlendShapeAvatar.Clips;
                foreach (var clip in BlendShapeClips)
                {
                    if (clip.Preset == BlendShapePreset.Unknown)
                    {
                        //非プリセット(Unknown)であれば、Unknown用の名前変数を参照する
                        BlendShapeKeyString[clip.BlendShapeName] = clip.Key;
                        KeyUpperCaseDictionary[clip.BlendShapeName.ToUpper()] = clip.BlendShapeName;
                    }
                    else
                    {
                        //プリセットであればENUM値をToStringした値を利用する
                        BlendShapeKeyString[clip.Preset.ToString()] = clip.Key;
                        KeyUpperCaseDictionary[clip.Preset.ToString().ToUpper()] = clip.Preset.ToString();
                    }
                }
            }
            SetFaceNeutral();
        }

        private bool isReset = false;

        // Update is called once per frame
        void Update()
        {
            if (VRMmodel != null)
            {
                if (proxy == null)
                {
                    InitializeProxy();
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
}