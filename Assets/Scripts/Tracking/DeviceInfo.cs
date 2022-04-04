//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace VMC
{
    //デバイス情報
    public class DeviceInfo
    {
        const float MAX_CHECK_SECONDS = 60f; //最大カウント時間
        const float LEAP_SECONDS = 1f; //トラッキング復帰までのなめらか時間

        //有効無効(全体の情報をstaticとして保持する)
        public static bool globalEnable = true;
        public static bool hmdEnable = true;
        public static bool controllerEnable = true;
        public static bool trackerEnable = true;
        public static bool pauseTracking = false;

        //最後に正常だった全位置情報
        private SteamVR_Utils.RigidTransform lastValidTransform;
        //前回の全正常性情報
        private bool lastStatus = true;
        //正常に取得できている全フレーム数
        private int validFrames;

        public bool isOK { get; private set; } //外部公開用の飛び検出情報(true=正常, false=遮断)

        public string serialNumber { get; set; }
        public SteamVR_Utils.RigidTransform transform { get; set; }
        public ETrackedDeviceClass trackedDeviceClass { get; set; }

        //内部で使用する用のステータス状態
        private TrackedDevicePose_t trackingStatus;
        private float okTime = 0;
        private bool isFirstTime = true;

        public DeviceInfo() { }

        //互換性(仮想デバイス含む)
        public void UpdateDeviceInfo(SteamVR_Utils.RigidTransform rigidTransform, string serial)
        {
            transform = rigidTransform;
            serialNumber = serial;

            trackingStatus = new TrackedDevicePose_t();
            trackingStatus.bDeviceIsConnected = true;
            trackingStatus.bPoseIsValid = true;
            trackingStatus.eTrackingResult = ETrackingResult.Running_OK;

            trackedDeviceClass = ETrackedDeviceClass.GenericTracker;
            isOK = true; //外部向けには一旦問題無しとする

            if (isFirstTime)
            {
                lastValidTransform = transform;
                isFirstTime = false;
            }

            updateValues();
        }

        public void UpdateDeviceInfo(SteamVR_Utils.RigidTransform rigidTransform, string serial, TrackedDevicePose_t result,ETrackedDeviceClass deviceClass)
        {
            transform = rigidTransform;
            serialNumber = serial;
            trackingStatus = result;
            trackedDeviceClass = deviceClass;
            isOK = true; //外部向けには一旦問題無しとする

            if (isFirstTime)
            {
                lastValidTransform = transform;
                isFirstTime = false;
            }

            updateValues();
        }

        //内部向け、正常かどうかを判断する
        private bool IsTrackingOK()
        {
            return trackingStatus.eTrackingResult == ETrackingResult.Running_OK && pauseTracking == false;
        }

        private void measureValidFrames()
        {
            //正常フレーム数測定
            if (IsTrackingOK())
            {
                //正常フレーム数を加算する(初期値は0)
                //チェックする最大時間以下なら加算する(それ以上はオーバーフロー防止のため加算しない)
                if (okTime < MAX_CHECK_SECONDS)
                {
                    validFrames++;
                }
            }
            else
            {
                //正常ではない場合、
                //正常フレーム数は0にする
                validFrames = 0;
            }
        }

        //トラッキング正常性を用いた処理
        private void saveAndSwapInvalidTransform()
        {
            //0を検出したら、過去の値を使う
            if (transform.pos == Vector3.zero && transform.rot == Quaternion.identity)
            {
                transform = lastValidTransform;
                isOK = false; //外部向けに問題ありと通知する
                return;
            }

            //値保存・スワップ処理(時間を考慮する)
            if (IsTrackingOK())
            {
                //正常な時間が十分経ったら現在の値を保存する
                if (okTime > LEAP_SECONDS)
                {
                    lastValidTransform = transform;
                }
                else
                {
                    //復帰してまだ時間が足りない場合、過去の値からゆっくりと戻す
                    float a = Mathf.Clamp(okTime / LEAP_SECONDS, 0f, 1f);
                    Vector3 pos = Vector3.Lerp(lastValidTransform.pos, transform.pos, a);
                    Quaternion rot = Quaternion.Lerp(lastValidTransform.rot, transform.rot, a);

                    transform = new SteamVR_Utils.RigidTransform(pos, rot);

                    //これにより加速度的に戻る & 飛びから復帰してまた飛んだとき対策
                    lastValidTransform = transform;
                }
                return;
            }
            else
            {
                //正常ではない場合、
                //過去に記録したデータが有るならば、過去のデータに差し替える
                Vector3 pos = lastValidTransform.pos;

                //回転情報は部分的に反映する(飛びの影響を受けづらいのと、不自然さ防止)
                Quaternion rot = Quaternion.Lerp(lastValidTransform.rot, transform.rot, pauseTracking ? 0.0f : 0.5f);

                transform = new SteamVR_Utils.RigidTransform(pos, rot);
                isOK = false; //外部向けに問題ありと通知する
                return;
            }
        }

        //単純な0検出
        private void saveAndSwapZeroTransform()
        {
            if (transform.pos == Vector3.zero && transform.rot == Quaternion.identity)
            {
                //0を検出したら、過去の値を使う
                transform = lastValidTransform;
                isOK = false; //外部向けに問題ありと通知する
            }
            else
            {
                //0出ない場合は記録する
                lastValidTransform = transform;
            }
        }

        void updateOkTime()
        {
            okTime = (float)validFrames / (float)Application.targetFrameRate;
        }

        //最後に正常だった姿勢を保存する
        private void updateValues()
        {
            bool process = true;
            if (!globalEnable)
            {
                process = false;
            }
            if (trackedDeviceClass == ETrackedDeviceClass.HMD && !hmdEnable)
            {
                process = false;
            }
            if (trackedDeviceClass == ETrackedDeviceClass.Controller && !controllerEnable)
            {
                process = false;
            }
            if (trackedDeviceClass == ETrackedDeviceClass.GenericTracker && !trackerEnable)
            {
                process = false;
            }

            if (serialNumber != null)
            {

                //OK時間を算出
                updateOkTime();

                //正常性情報のログ出力
                if (lastStatus != IsTrackingOK())
                {
                    if (IsTrackingOK())
                    {
                        Debug.Log("Tracking Recovering... [" + serialNumber + "] " + okTime + " sec OK");
                    }
                    else
                    {
                        Debug.Log("Tracking Lost!     [" + serialNumber + "] " + okTime + " sec OK");
                    }
                }
                
                measureValidFrames();
                if (process || pauseTracking)
                {
                    saveAndSwapInvalidTransform();
                }
                else {
                    saveAndSwapZeroTransform();
                }

                //過去正常性情報を更新する
                lastStatus = IsTrackingOK();
            }
        }
    }
}