using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniVRM10;
//using VRM;

namespace VMC
{
    public class FaceController : MonoBehaviour
    {
        private GameObject VRMmodel;

        private Vrm10RuntimeExpression vrm10RuntimeExpression;

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

        public IReadOnlyList<ExpressionKey> BlendShapeClips = new List<ExpressionKey>();    //読み込んだモデルの表情のキー一覧

        public System.Action BeforeApply;

        private ExpressionPreset defaultFace = ExpressionPreset.neutral;
        public ExpressionPreset DefaultFace
        {
            get { return defaultFace; }
            set
            {
                if (defaultFace != value)
                {
                    //前回の表情を消しておく
                    if (vrm10RuntimeExpression != null)
                    {
                        if (defaultFace != ExpressionPreset.custom)
                        {
                            SetFace(defaultFace, 0.0f, StopBlink);
                        }
                        else if (string.IsNullOrEmpty(FacePresetName) == false)
                        {
                            SetFace(ExpressionKey.CreateCustom(FacePresetName), 0.0f, StopBlink);
                        }
                    }
                    defaultFace = value;
                    //新しい表情を設定する
                    if (vrm10RuntimeExpression != null)
                    {
                        if (defaultFace != ExpressionPreset.custom)
                        {
                            SetFace(defaultFace, 1.0f, StopBlink);
                        }
                        else if (string.IsNullOrEmpty(FacePresetName) == false)
                        {
                            SetFace(ExpressionKey.CreateCustom(FacePresetName), 1.0f, StopBlink);
                        }
                    }
                }
            }
        }
        public string FacePresetName = null;

        private AnimationController animationController;

        private Dictionary<ExpressionKey, float> CurrentShapeKeys;
        private Dictionary<string, Dictionary<ExpressionKey, float>> AccumulateShapeKeys = new Dictionary<string, Dictionary<ExpressionKey, float>>();
        private Dictionary<string, Dictionary<ExpressionKey, float>> OverwriteShapeKeys = new Dictionary<string, Dictionary<ExpressionKey, float>>();
        private ExpressionKey NeutralKey = ExpressionKey.CreateFromPreset(ExpressionPreset.neutral);

        private Dictionary<string, ExpressionKey> BlendShapeKeyString = new Dictionary<string, ExpressionKey>();
        private Dictionary<string, string> KeyUpperCaseDictionary = new Dictionary<string, string>();
        public string GetCaseSensitiveKeyName(string upperCase)
        {
            if (KeyUpperCaseDictionary.Count == 0)
            {
                foreach (var presetName in System.Enum.GetNames(typeof(ExpressionPreset)))
                {
                    KeyUpperCaseDictionary[presetName.ToUpper()] = presetName;
                }
            }
            return KeyUpperCaseDictionary.ContainsKey(upperCase) ? KeyUpperCaseDictionary[upperCase] : upperCase;
        }

        private void Start()
        {
            var dict = new Dictionary<ExpressionKey, float>();
            foreach (var clip in BlendShapeClips)
            {
                dict.Add(clip, 0.0f);
            }
            CurrentShapeKeys = dict;

            CreateAnimation();
        }

        private void OnEnable()
        {
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
        }

        private void OnDisable()
        {
            VMCEvents.OnCurrentModelChanged -= OnCurrentModelChanged;
        }

        private void OnCurrentModelChanged(GameObject model)
        {
            VRMmodel = model;
            vrm10RuntimeExpression = null;
            InitializeProxy();
        }

        private void CreateAnimation()
        {
            if (animationController == null) animationController = new AnimationController();
            if (vrm10RuntimeExpression != null)
            {
                animationController.ClearAnimations();
                animationController.AddResetAction(() => MixPreset("Blink", ExpressionPreset.blink, 0.0f));
                animationController.AddWait(null, () => BlinkTimeMin + Random.value * (BlinkTimeMax - BlinkTimeMin));
                animationController.AddAnimation(CloseAnimationTime, 0.0f, 1.0f, v => MixPreset("Blink", ExpressionPreset.blink, v));
                animationController.AddWait(ClosingTime);
                animationController.AddAnimation(OpenAnimationTime, 1.0f, 0.0f, v => MixPreset("Blink", ExpressionPreset.blink, v));
            }
        }

        public void SetBlink_L(float value)
        {
            if (ViveProEyeEnabled == false)
            {
                MixPreset("Blink", ExpressionPreset.blink, 0.0f);
            }
            if (StopBlink)
            {
                MixPreset("Blink_L", ExpressionPreset.blinkLeft, 0.0f);
            }
            else
            {
                MixPreset("Blink_L", ExpressionPreset.blinkLeft, value);
            }
        }
        public void SetBlink_R(float value)
        {
            if (ViveProEyeEnabled == false)
            {
                MixPreset("Blink", ExpressionPreset.blink, 0.0f);
            }
            if (StopBlink)
            {
                MixPreset("Blink_R", ExpressionPreset.blinkLeft, 0.0f);
            }
            else
            {
                MixPreset("Blink_R", ExpressionPreset.blinkRight, value);
            }
        }

        private void SetFaceNeutral()
        {
            //表情をデフォルトに戻す
            if (vrm10RuntimeExpression != null)
            {
                var keys = new List<ExpressionKey>();
                var values = new List<float>();
                foreach (var clip in BlendShapeClips)
                {
                    var shapekey = clip;
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

        public void SetFace(ExpressionPreset preset, float strength, bool stopBlink)
        {
            SetFace(ExpressionKey.CreateFromPreset(preset), strength, stopBlink);
        }

        public void SetFace(ExpressionKey key, float strength, bool stopBlink)
        {
            SetFace(new List<ExpressionKey> { key }, new List<float> { strength }, stopBlink);
        }

        public void SetFace(List<string> keys, List<float> strength, bool stopBlink)
        {
            if (vrm10RuntimeExpression != null)
            {
                if (keys.Any(d => BlendShapeKeyString.ContainsKey(d) == false))
                {
                    var convertKeys = new List<ExpressionKey>();
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

        public void SetFace(List<ExpressionKey> keys, List<float> strength, bool stopBlink)
        {
            if (vrm10RuntimeExpression != null)
            {
                StopBlink = stopBlink;
                var dict = new Dictionary<ExpressionKey, float>();
                foreach (var clip in BlendShapeClips)
                {
                    dict.Add(clip, 0.0f);
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

        public void MixPreset(string presetName, ExpressionPreset preset, float value)
        {
            MixPresets(presetName, new[] { preset }, new[] { value });
        }

        public void MixPresets(string presetName, ExpressionPreset[] presets, float[] values)
        {
            MixPresets(presetName, presets.Select(d => ExpressionKey.CreateFromPreset(d)).ToArray(), values);
        }

        public void MixPreset(string presetName, ExpressionKey preset, float value)
        {
            MixPresets(presetName, new[] { preset }, new[] { value });
        }

        public void MixPresets(string presetName, string[] keys, float[] values)
        {
            if (keys.Any(d => BlendShapeKeyString.ContainsKey(d) == false))
            {
                var convertKeys = new List<ExpressionKey>();
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

        public void MixPresets(string presetName, ExpressionKey[] presets, float[] values)
        {
            if (vrm10RuntimeExpression == null) return;
            if (CurrentShapeKeys == null) return;

            if (AccumulateShapeKeys.ContainsKey(presetName) == false)
            {
                AccumulateShapeKeys.Add(presetName, new Dictionary<ExpressionKey, float>());
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

        public void OverwritePresets(string presetName, ExpressionKey[] presets, float[] values)
        {
            if (vrm10RuntimeExpression == null) return;
            if (CurrentShapeKeys == null) return;

            if (OverwriteShapeKeys.ContainsKey(presetName) == false)
            {
                OverwriteShapeKeys.Add(presetName, new Dictionary<ExpressionKey, float>());
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
            if (vrm10RuntimeExpression == null) return;
            var accumulatedValues = new Dictionary<ExpressionKey, float>();
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

            //全ての表情をSetWeightsで1度に反映させる
            vrm10RuntimeExpression.SetWeights(accumulatedValues);
        }

        private void InitializeProxy()
        {
            var vrm10Instance = VRMmodel.GetComponent<Vrm10Instance>();
            vrm10RuntimeExpression = vrm10Instance.Runtime.Expression;

            //すべての表情の名称一覧を取得
            if (vrm10RuntimeExpression != null)
            {
                BlendShapeClips = vrm10RuntimeExpression.ExpressionKeys;
                foreach (var clip in BlendShapeClips)
                {
                    BlendShapeKeyString[clip.Name] = clip;
                    KeyUpperCaseDictionary[clip.Name.ToUpper()] = clip.Name;
                }

                // VRM 0.x compatibility
                BlendShapeKeyString.Add("Neutral", ExpressionKey.Neutral);
                BlendShapeKeyString.Add("A", ExpressionKey.Aa);
                BlendShapeKeyString.Add("I", ExpressionKey.Ih);
                BlendShapeKeyString.Add("U", ExpressionKey.Ou);
                BlendShapeKeyString.Add("E", ExpressionKey.Ee);
                BlendShapeKeyString.Add("O", ExpressionKey.Oh);
                BlendShapeKeyString.Add("Blink", ExpressionKey.Blink);
                BlendShapeKeyString.Add("Joy", ExpressionKey.Happy);
                BlendShapeKeyString.Add("Angry", ExpressionKey.Angry);
                BlendShapeKeyString.Add("Sorrow", ExpressionKey.Sad);
                BlendShapeKeyString.Add("Fun", ExpressionKey.Relaxed);
                BlendShapeKeyString.Add("LookUp", ExpressionKey.LookUp);
                BlendShapeKeyString.Add("LookDown", ExpressionKey.LookDown);
                BlendShapeKeyString.Add("LookLeft", ExpressionKey.LookLeft);
                BlendShapeKeyString.Add("LookRight", ExpressionKey.LookRight);
                BlendShapeKeyString.Add("Blink_L", ExpressionKey.BlinkLeft);
                BlendShapeKeyString.Add("Blink_R", ExpressionKey.BlinkRight);
            }
            SetFaceNeutral();
        }

        private bool isReset = false;

        // Update is called once per frame
        void Update()
        {
            if (VRMmodel == null) return;

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
