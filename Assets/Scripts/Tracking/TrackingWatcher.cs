using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class TrackingWatcher : MonoBehaviour
    {
        const float FallRate = -10f; //Weightを落とす速度
        const float RecoverRate = 10f; //Weightを復帰させる速度()

        public bool enable = false; //機能を使用するか

        public bool ok = false; //デバッグ用
        public bool action = false;//デバッグ用
        public float weight = 1f;

        Action<float> SetWeightAction = null;

        //Tracker Handlerから呼び出される。今トラッキングが正常かどうか
        public void IsOK(bool ok)
        {
            this.ok = ok;
        }

        //IKをキャリブレーションするときにWeightを設定するためのActionが渡される。
        public void SetActionOfSetWeight(Action<float> action)
        {
            this.SetWeightAction = action;
            this.action = true;
        }

        //キャリブレーション開始時にActionを破棄する
        public void Clear()
        {
            this.SetWeightAction = null;
            this.action = false;
            weight = 1f;

            Debug.Log(transform.name + " : Clear!");
        }

        void Update()
        {
            //Actionがなければ実行しない
            if (SetWeightAction == null || !enable)
            {
                return;
            }

            //状態に合わせて重みを変える
            if (ok)
            {
                weight += RecoverRate * Time.deltaTime;
            }
            else
            {
                weight += FallRate * Time.deltaTime;
            }
            weight = Mathf.Clamp01(weight);
            SetWeightAction?.Invoke(weight);
        }
    }
}