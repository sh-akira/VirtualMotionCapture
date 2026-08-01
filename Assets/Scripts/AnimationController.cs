using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class AnimationController
    {
        public class AnimationItem
        {
            public float Time { get; set; } //アニメーションにかける時間
            public float StartTime { get; set; } //シーケンス先頭からこのアニメーションが始まるまでの時間
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
        private int currentIndex = 0; //今実行中のアニメーションの位置
        public System.Action ResetAction { get; set; }

        public List<AnimationItem> AnimationItems = new List<AnimationItem>();

        private void InitializeAnimation()
        {
            currentIndex = 0;
            var starttime = 0.0f;
            foreach (var item in AnimationItems)
            {
                item.Initialize();
                item.StartTime = starttime;
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
            StopAnimations();
            ResetAction?.Invoke();
        }

        public void ClearAnimations()
        {
            AnimationItems.Clear();
        }

        public void StopAnimations()
        {
            isStart = false;
            currentIndex = 0;
        }

        public bool Next()
        {
            if (isStart == false)
            {
                isStart = true;
                startTime = CurrentTime;
                InitializeAnimation();
            }

            var elapsedTime = CurrentTime - startTime;

            //処理落ちで飛び越したアニメーションは、順番に終了値を適用してから先へ進む。
            //飛ばしたままにすると中間状態(まばたきなら目を閉じたまま)で固まってしまう
            while (currentIndex < AnimationItems.Count)
            {
                var skipItem = AnimationItems[currentIndex];
                if (skipItem.StartTime + skipItem.Time >= elapsedTime) break;
                skipItem.RunAction(skipItem.EndValue);
                currentIndex++;
            }

            //最後まで到達したとき。上のループですべてのアニメーションが終了値まで進んでいるので、
            //どれだけ処理落ちしても最終状態(まばたきなら目を開いた状態)で終わる
            if (currentIndex >= AnimationItems.Count)
            {
                isStart = false;
                currentIndex = 0;
                return false;
            }

            //処理落ちしていても、その時点の経過時間に対応する値を適用する
            //(先頭の値に戻すと、飛び越した分だけアニメーションが巻き戻ってしまう)
            var item = AnimationItems[currentIndex];
            var rate = item.Time > 0.0f ? Mathf.Clamp01((elapsedTime - item.StartTime) / item.Time) : 1.0f;
            item.RunAction(item.StartValue + (item.EndValue - item.StartValue) * rate);
            return true;
        }

        #region 自動テスト用フック

        /// <summary>
        /// 現在時刻の取得元。処理落ち(フレーム落ち)を決定論的に再現するために自動テストから差し替える。
        /// 通常の動作ではnullで、Time.realtimeSinceStartupが使われる。
        /// </summary>
        internal static System.Func<float> TestTimeProvider = null;

        private static float CurrentTime => TestTimeProvider != null ? TestTimeProvider() : Time.realtimeSinceStartup;

        #endregion
    }
}
