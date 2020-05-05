//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace sh_akira.OVRTracking
{
    //デバイス情報
    public class DeviceInfo
    {
        const float MAX_CHECK_SECONDS = 60f; //最大カウント時間
        const float LEAP_SECONDS = 1f; //トラッキング復帰までのなめらか時間

        //-----------全デバイス情報----------------------

        //有効無効(全体の情報をstaticとして保持する)
        public static bool globalEnable = true;
        public static bool hmdEnable = true;
        public static bool controllerEnable = true;
        public static bool trackerEnable = true;

        //最後に正常だった全位置情報
        static Dictionary<string, SteamVR_Utils.RigidTransform> lastValidTransform = new Dictionary<string, SteamVR_Utils.RigidTransform>();
        //前回の全正常性情報
        static Dictionary<string, bool> lastStatus = new Dictionary<string, bool>();
        //正常に取得できている全フレーム数
        static Dictionary<string, int> validFrames = new Dictionary<string, int>();

        //全記録情報のリセット
        static void Reset()
        {
            lastValidTransform.Clear();
            lastStatus.Clear();
            validFrames.Clear();
        }

        //-----------デバイスごとの情報----------------------

        //デバイス取得の度にnewされるのでデータ保持不可
        public bool isOK { get; private set; } //外部公開用の飛び検出情報(true=正常, false=遮断)

        public string serialNumber { get; set; }
        public SteamVR_Utils.RigidTransform transform { get; set; }
        public ETrackedDeviceClass trackedDeviceClass { get; set; }

        //内部で使用する用のステータス状態
        private TrackedDevicePose_t trackingStatus;
        float okTime = 0;

        public DeviceInfo() { }

        //互換性(仮想デバイス含む)
        public DeviceInfo(SteamVR_Utils.RigidTransform rigidTransform, string serial)
        {
            transform = rigidTransform;
            serialNumber = serial;

            trackingStatus = new TrackedDevicePose_t();
            trackingStatus.bDeviceIsConnected = true;
            trackingStatus.bPoseIsValid = true;
            trackingStatus.eTrackingResult = ETrackingResult.Running_OK;

            trackedDeviceClass = ETrackedDeviceClass.GenericTracker;
            isOK = true; //外部向けには一旦問題無しとする

            updateValues();
        }

        public DeviceInfo(SteamVR_Utils.RigidTransform rigidTransform, string serial, TrackedDevicePose_t result,ETrackedDeviceClass deviceClass)
        {
            transform = rigidTransform;
            serialNumber = serial;
            trackingStatus = result;
            trackedDeviceClass = deviceClass;
            isOK = true; //外部向けには一旦問題無しとする

            updateValues();
        }

        //内部向け、正常かどうかを判断する
        private bool IsTrackingOK()
        {
            return trackingStatus.eTrackingResult == ETrackingResult.Running_OK;
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
                    validFrames[serialNumber]++;
                }
            }
            else
            {
                //正常ではない場合、
                //正常フレーム数は0にする
                validFrames[serialNumber] = 0;
            }
        }

        //トラッキング正常性を用いた処理
        private void saveAndSwapInvalidTransform()
        {
            //0を検出したら、過去の値を使う
            if (transform.pos == Vector3.zero && transform.rot == Quaternion.identity)
            {
                transform = lastValidTransform[serialNumber];
                isOK = false; //外部向けに問題ありと通知する
                return;
            }

            //値保存・スワップ処理(時間を考慮する)
            if (IsTrackingOK())
            {
                //正常な時間が十分経ったら現在の値を保存する
                if (okTime > LEAP_SECONDS)
                {
                    lastValidTransform[serialNumber] = transform;
                }
                else
                {
                    //復帰してまだ時間が足りない場合、過去の値からゆっくりと戻す
                    float a = Mathf.Clamp(okTime / LEAP_SECONDS, 0f, 1f);
                    Vector3 pos = Vector3.Lerp(lastValidTransform[serialNumber].pos, transform.pos, a);
                    Quaternion rot = Quaternion.Lerp(lastValidTransform[serialNumber].rot, transform.rot, a);

                    transform = new SteamVR_Utils.RigidTransform(pos, rot);

                    //これにより加速度的に戻る & 飛びから復帰してまた飛んだとき対策
                    lastValidTransform[serialNumber] = transform;
                }
                return;
            }
            else
            {
                //正常ではない場合、
                //過去に記録したデータが有るならば、過去のデータに差し替える
                Vector3 pos = lastValidTransform[serialNumber].pos;

                //回転情報は部分的に反映する(飛びの影響を受けづらいのと、不自然さ防止)
                Quaternion rot = Quaternion.Lerp(lastValidTransform[serialNumber].rot, transform.rot, 0.5f);

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
                transform = lastValidTransform[serialNumber];
                isOK = false; //外部向けに問題ありと通知する
            }
            else
            {
                //0出ない場合は記録する
                lastValidTransform[serialNumber] = transform;
            }
        }

        void updateOkTime()
        {
            okTime = (float)validFrames[serialNumber] / (float)Application.targetFrameRate;
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
                //正常性Frameの初期値=0
                if (!validFrames.ContainsKey(serialNumber))
                {
                    validFrames[serialNumber] = 0;
                }
                //初期正常値はlost
                if (!lastStatus.ContainsKey(serialNumber))
                {
                    lastStatus[serialNumber] = false;
                }
                //初期位置は今の値
                if (!lastValidTransform.ContainsKey(serialNumber))
                {
                    lastValidTransform[serialNumber] = transform;
                }

                //OK時間を算出
                updateOkTime();

                //正常性情報のログ出力
                if (lastStatus[serialNumber] != IsTrackingOK())
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
                if (process)
                {
                    saveAndSwapInvalidTransform();
                }
                else {
                    saveAndSwapZeroTransform();
                }

                //過去正常性情報を更新する
                lastStatus[serialNumber] = IsTrackingOK();
            }
        }
    }
}