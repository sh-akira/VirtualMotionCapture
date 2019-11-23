//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace sh_akira.OVRTracking
{
    public class DeviceInfo
    {
        const float MAX_CHECK_SECONDS = 60f; //最大カウント時間
        const float LEAP_SECONDS = 1f; //トラッキング復帰までのなめらか時間

        //最後に正常だった位置情報
        static Dictionary<string, SteamVR_Utils.RigidTransform> lastValidTransform = new Dictionary<string, SteamVR_Utils.RigidTransform>();
        //前回の正常性情報
        static Dictionary<string, bool> lastStatus = new Dictionary<string, bool>();
        //正常に取得できているフレーム数
        static Dictionary<string, int> validFrames = new Dictionary<string, int>();

        //デバイス取得の度にnewされるのでデータ保持不可

        public string serialNumber { get; set; }
        public SteamVR_Utils.RigidTransform transform { get; set; }

        //内部で使用する用のステータス状態
        private TrackedDevicePose_t trackingStatus;

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

            saveAndSwapLastValidTransform();
        }

        public DeviceInfo(SteamVR_Utils.RigidTransform rigidTransform, string serial, TrackedDevicePose_t result)
        {
            transform = rigidTransform;
            serialNumber = serial;
            trackingStatus = result;

            saveAndSwapLastValidTransform();
        }

        //正常かどうかを判断する
        private bool IsTrackingOK()
        {
            return trackingStatus.eTrackingResult == ETrackingResult.Running_OK;
        }

        //最後に正常だった姿勢を保存する
        private void saveAndSwapLastValidTransform()
        {
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
                float okTime = (float)validFrames[serialNumber] / (float)Application.targetFrameRate;

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

                //値保存・スワップ処理(時間を考慮する)
                if (IsTrackingOK())
                {
                    //正常な時間が十分経ったら現在の値を保存する
                    if (okTime > LEAP_SECONDS)
                    {
                        lastValidTransform[serialNumber] = transform;
                    }
                    else {
                        //復帰してまだ時間が足りない場合、過去の値からゆっくりと戻す
                        float a = Mathf.Clamp(okTime / LEAP_SECONDS, 0f, 1f);
                        Vector3 pos = Vector3.Lerp(lastValidTransform[serialNumber].pos, transform.pos, a);
                        Quaternion rot = Quaternion.Lerp(lastValidTransform[serialNumber].rot,transform.rot, a);

                        transform = new SteamVR_Utils.RigidTransform(pos, rot);

                        //これにより加速度的に戻る & 飛びから復帰してまた飛んだとき対策
                        lastValidTransform[serialNumber] = transform;
                    }
                }
                else {
                    //正常ではない場合、
                    if (lastValidTransform.ContainsKey(serialNumber)) {
                        //過去に記録したデータが有るならば、過去のデータに差し替える
                        transform = lastValidTransform[serialNumber];
                    }
                    
                }
                //過去正常性情報を更新する
                lastStatus[serialNumber] = IsTrackingOK();
            }
        }
    }
}