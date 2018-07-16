using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class AnimationController
{
    public class AnimationItem
    {
        public float Time { get; set; } //アニメーションにかける時間
        public float StartValue { get; set; }
        public float EndValue { get; set; }
        public System.Action<float> SetAction { get; set; }
        public System.Func<float> TimeInitializer { get; set; }

        public void RunAction(float value)
        {
            SetAction?.Invoke(value);
        }

        public void Initialize()
        {
            if (TimeInitializer != null)
            {
                Time = TimeInitializer();
            }
        }
    }

    private bool isStart = false;
    private float startTime = 0.0f;
    public System.Action ResetAction { get; set; }

    public List<AnimationItem> AnimationItems = new List<AnimationItem>();

    public Dictionary<float, AnimationItem> CurrentAnimationItems = new Dictionary<float, AnimationItem>(); //Key:開始時間


    private AnimationItem EndLastItem = null;
    private AnimationItem CurrentItem = null;

    private void InitializeAnimation()
    {
        CurrentAnimationItems.Clear();
        EndLastItem = null;
        CurrentItem = null;
        var starttime = 0.0f;
        foreach (var item in AnimationItems)
        {
            item.Initialize();
            CurrentAnimationItems.Add(starttime, item);
            starttime += item.Time == 0.0f ? 0.0001f : item.Time;
        }
    }

    public void AddResetAction(System.Action resetAction)
    {
        ResetAction = resetAction;
    }

    public void AddWait(float? time, System.Func<float> timeInitializer = null)
    {
        AddAnimation(time, 0.0f, 0.0f, null, timeInitializer);
    }

    public void AddAnimation(float? time, float startValue, float endValue, System.Action<float> setAction, System.Func<float> timeInitializer = null)
    {
        AnimationItems.Add(new AnimationItem { Time = time ?? 0.0f, StartValue = startValue, EndValue = endValue, SetAction = setAction, TimeInitializer = timeInitializer });
    }

    public void Reset()
    {
        ResetAction?.Invoke();
    }

    public void ClearAnimations()
    {
        AnimationItems.Clear();
    }

    public bool Next()
    {
        if (isStart == false)
        {
            isStart = true;
            startTime = Time.time;
            InitializeAnimation();
        }

        var elapsedTime = Time.time - startTime;
        var addTime = 0.0f;
        AnimationItem lastitem = null;
        foreach (var item in CurrentAnimationItems)
        {
            addTime = item.Key + item.Value.Time; //すべてのアニメーションの時間+今のアニメーション時間
            if (addTime >= elapsedTime)
            {//経過時間がまだアニメーションの終了時間に届いていない間(アニメーション中)
                if (lastitem != null && EndLastItem != lastitem)
                {//前回のアニメーションが終わりまで行ってない場合があるので100％で実行
                    lastitem.RunAction(lastitem.EndValue);
                    EndLastItem = lastitem;
                }
                if (CurrentItem != item.Value)
                {//新しいアニメーションになったときには時間にかかわらずきちんと最初の値を使う
                    item.Value.RunAction(item.Value.StartValue);
                    CurrentItem = item.Value;
                }
                else
                {
                    var currentTime = item.Value.Time + (elapsedTime - addTime);
                    var setvalue = item.Value.StartValue + (item.Value.EndValue - item.Value.StartValue) * (currentTime / item.Value.Time);
                    item.Value.RunAction(setvalue);
                }
                lastitem = item.Value;
                return true;
            }
        }

        //最後までアニメーションしたとき
        if (lastitem != null)
        {//最後のアニメーションが終わりまで行ってない場合があるので100％で実行
            lastitem.RunAction(lastitem.EndValue);
        }
        isStart = false;
        return false;
    }
}