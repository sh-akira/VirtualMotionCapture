using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if !UNITY_ANDROID
using Valve.VR;
#endif

namespace DVRSDK.Avatar.Tracking
{
    public class SteamVRTracker : MonoBehaviour, ITracker
    {
        [Header("親に指定したいTransform。未指定の場合このオブジェクトの子に配置します")]
        [SerializeField]
        private Transform trackersParent = null;
        public Transform TrackersParent => trackersParent;

        // 外部からTransformを指定されたときはそちらをそのまま使用する。無い場合は自動で作成して割り当てる
        [Header("使用したい部位をすべて定義します")]
        public TrackerTarget[] TrackerTargets = new TrackerTarget[]
        {
            new TrackerTarget { TrackerPosition = TrackerPositions.Head, UseDeviceType = TrackingDeviceType.HMD },
            new TrackerTarget { TrackerPosition = TrackerPositions.LeftHand, UseDeviceType = TrackingDeviceType.Controller },
            new TrackerTarget { TrackerPosition = TrackerPositions.RightHand, UseDeviceType = TrackingDeviceType.Controller },
            new TrackerTarget { TrackerPosition = TrackerPositions.Waist },
            new TrackerTarget { TrackerPosition = TrackerPositions.LeftFoot },
            new TrackerTarget { TrackerPosition = TrackerPositions.RightFoot },
            new TrackerTarget { TrackerPosition = TrackerPositions.LeftElbow },
            new TrackerTarget { TrackerPosition = TrackerPositions.RightElbow },
            new TrackerTarget { TrackerPosition = TrackerPositions.LeftKnee },
            new TrackerTarget { TrackerPosition = TrackerPositions.RightKnee },
            new TrackerTarget { TrackerPosition = TrackerPositions.Chest },
        };

        /// <summary>
        /// 指定部位のTrackerTargetを取得。無い場合はnull
        /// </summary>
        /// <param name="trackerPosition">部位</param>
        /// <returns>TrackerTarget</returns>
        public TrackerTarget GetTrackerTarget(TrackerPositions trackerPosition) => TrackerTargets.FirstOrDefault(d => d.TrackerPosition == trackerPosition && ((d.PoseIsValid && (d.InputSourceHandle != 0 || d.SerialNumber != null)) || d.SourceTransform != null));

        public Vector3 GetIKOffsetPosition(TrackerPositions targetPosition, TrackingDeviceType deviceType)
        {
            if (targetPosition == TrackerPositions.LeftHand && deviceType == TrackingDeviceType.Controller)
            {
                return new Vector3(-0.04f, 0.04f, -0.15f);
            }
            else if (targetPosition == TrackerPositions.RightHand && deviceType == TrackingDeviceType.Controller)
            {
                return new Vector3(0.04f, 0.04f, -0.15f);
            }
            else
            {
                return Vector3.zero;
            }
        }

        public Quaternion GetIKOffsetRotation(TrackerPositions targetPosition, TrackingDeviceType deviceType)
        {
            if (targetPosition == TrackerPositions.LeftHand && deviceType == TrackingDeviceType.Controller)
            {
                return Quaternion.Euler(-30, 0, 90);
            }
            else if (targetPosition == TrackerPositions.RightHand && deviceType == TrackingDeviceType.Controller)
            {
                return Quaternion.Euler(-30, 0, -90);
            }
            else
            {
                return Quaternion.identity;
            }
        }

#if !UNITY_ANDROID
        private ETrackingUniverseOrigin universeOrigin = ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated;

        #region SteamVR2xPoseAction

        /*
         * SteamVR Plugin 2.x におけるトラッカー座標取得手順
         *
         * 1. PoseのActionをSteamVR_Inputに定義する
         *
         * 2. SteamVR側でトラッカーに定義したPoseのActionを割り当てる(バインディングする)
         *
         * 3. 割り当てたバインディングをデフォルトバインディングとしてアプリに戻す
         *
         * 4. 定義した名前"Pose"からActionのフルパスを取得
         *    SteamVR_Input.GetAction<SteamVR_Action_Pose>("Pose"); // "/actions/default/in/Pose"
         *
         * 5. ActionのフルパスからActionのハンドルを取得
         *    OpenVR.Input.GetActionHandle(fullPath.ToLowerInvariant(), ref actionHandle);
         *
         * 6. 使いたい部位のInputSourceのフルパスを定義から選ぶ
         *    OpenVR.k_pchPathUserShoulderLeft や OpenVR.k_pchPathUserWaist等
         *
         * 7. InputSourceのフルパスからInputSourceのハンドルを取得
         *    OpenVR.Input.GetInputSourceHandle(path, ref inputSourceHandle);
         *
         * 8. ActionとInputSourceのハンドルからPoseデータを取得
         *    var eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;
         *    var fPredictedSecondsFromNow = framesAhead * (Time.timeScale / SteamVR.instance.hmd_DisplayFrequency);
         *    var poseActionData = new InputPoseActionData_t();
         *    var poseActionData_size = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));
         *    OpenVR.Input.GetPoseActionDataRelativeToNow(actionHandle, eOrigin, fPredictedSecondsFromNow, ref poseActionData, poseActionData_size, inputSourceHandle);
         *
         * 9. 取得したInputPoseActionDataがActiveな時だけ生MatrixからUnity座標を取得する
         *    if (poseActionData.bActive)
         *    {
         *        transform.localPosition = poseActionData.pose.mDeviceToAbsoluteTracking.GetPosition();
         *        transform.localRotation = poseActionData.pose.mDeviceToAbsoluteTracking.GetRotation();
         *    }
         *
         * 10. 以降毎Updateごとに8,9を繰り返す
         *
         */

        // "/actions/default/in/Pose"

        [Header("SteamVR Input 2.xで定義したPoseのActionを指定")]
        public SteamVR_Action_Pose PoseAction = SteamVR_Input.GetAction<SteamVR_Action_Pose>("Pose");

        private static float framesAhead = 2;
        private uint poseActionData_size = 0;
        private ulong actionHandle = 0;

        // enum TrackerPositionsとstring InputSourcePathとの対応表
        private readonly string[] TrackerPositionsToPath = new string[]
        {
            "/unrestricted",
            OpenVR.k_pchPathUserHandLeft,
            OpenVR.k_pchPathUserHandRight,
            OpenVR.k_pchPathUserFootLeft,
            OpenVR.k_pchPathUserFootRight,
            OpenVR.k_pchPathUserShoulderLeft,
            OpenVR.k_pchPathUserShoulderRight,
            OpenVR.k_pchPathUserWaist,
            OpenVR.k_pchPathUserChest,
            OpenVR.k_pchPathUserHead,
            OpenVR.k_pchPathUserGamepad,
            OpenVR.k_pchPathUserCamera,
            OpenVR.k_pchPathUserKeyboard,
            OpenVR.k_pchPathUserTreadmill,
            OpenVR.k_pchPathUserElbowLeft,
            OpenVR.k_pchPathUserElbowRight,
            OpenVR.k_pchPathUserKneeLeft,
            OpenVR.k_pchPathUserKneeRight,
            OpenVR.k_pchPathUserStylus,
        };

        /// <summary>
        /// ActionのフルパスからHandle取得
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        private ulong GetActionHandle(string fullPath)
        {
            ulong newHandle = 0;
            EVRInputError err = OpenVR.Input.GetActionHandle(fullPath.ToLowerInvariant(), ref newHandle);
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetActionHandle (" + fullPath.ToLowerInvariant() + ") error: " + err.ToString());
            return newHandle;
        }

        /// <summary>
        /// 指定したTrackerPositionsのInputSourceHandleを取得
        /// </summary>
        /// <param name="trackerPosition"></param>
        /// <returns></returns>
        private ulong GetTrackerInputSourceHandle(TrackerPositions trackerPosition)
            => GetTrackerInputSourceHandle(TrackerPositionsToPath[(int)trackerPosition]);

        /// <summary>
        /// 指定したInputSourcePathのInputSourceHandleを取得
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private ulong GetTrackerInputSourceHandle(string path)
        {
            ulong handle = 0;
            EVRInputError err = OpenVR.Input.GetInputSourceHandle(path, ref handle);

            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetInputSourceHandle (" + path + ") error: " + err.ToString());

            return handle;
        }

        /// <summary>
        /// 指定したInputSourceHandleからトラッカーのPoseを取得
        /// </summary>
        /// <param name="inputSourceHandle"></param>
        /// <returns></returns>
        private InputPoseActionData_t GetPoseByHandle(ulong inputSourceHandle)
        {
            var poseActionData = new InputPoseActionData_t();
            EVRInputError err = OpenVR.Input.GetPoseActionDataRelativeToNow(actionHandle, universeOrigin, framesAhead * (Time.timeScale / SteamVR.instance.hmd_DisplayFrequency), ref poseActionData, poseActionData_size, inputSourceHandle);

            if (err != EVRInputError.None)
            {
                Debug.LogError("<b>[SteamVR]</b> GetPoseActionDataRelativeToNow error: " + err.ToString() + " ActionHandle: " + actionHandle.ToString() + ". Input source: " + inputSourceHandle.ToString());
            }
            return poseActionData;
        }

        #endregion

        #region SteamVR1xTrackerIndex

        /*
         * SteamVR Plugin 1.x におけるトラッカー座標取得手順
         *
         * 1. OpenVR.k_unMaxTrackedDeviceCount分の配列を用意する
         *    TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
         *
         * 2. 全数(64個分)のデータを取得する
         *    var eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;
         *    var fPredictedSecondsFromNow = framesAhead * (Time.timeScale / SteamVR.instance.hmd_DisplayFrequency);
         *    OpenVR.System.GetDeviceToAbsoluteTrackingPose(eOrigin, fPredictedSecondsFromNow, allPoses);
         *
         * 3. 全数チェックを行う
         *    for (uint index = 0; index < OpenVR.k_unMaxTrackedDeviceCount; index++) { }
         *
         * 4. デバイスの種別が欲しい場合はindexから取得する
         *    OpenVR.System.GetTrackedDeviceClass(index); //ETrackedDeviceClass.HMD, Controller, GenericTracker ...
         *
         * 5. トラッカーの電源が切れるとindexがずれて実質的にシリアルナンバーの比較が必須なためProp_SerialNumber_Stringを取得する
         *    var buffer = new StringBuilder();
         *    var error = default(ETrackedPropertyError);
         *    var capacity = (int)OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, null, 0, ref error);
         *    OpenVR.System.GetStringTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_SerialNumber_String, buffer, (uint)buffer.EnsureCapacity(capacity), ref error);
         *    return buffer.ToString();
         *
         * 6. デバイスがアクティブな時だけ生MatrixからUnity座標を取得する
         *    if (allPoses[index].bDeviceIsConnected
         *    {
         *        transform.localPosition = allPoses[index].mDeviceToAbsoluteTracking.GetPosition();
         *        transform.localRotation = allPoses[index].mDeviceToAbsoluteTracking.GetRotation();
         *    }
         *
         * 7. 以降毎Updateごとに2～6を繰り返す
         *
         */

        private TrackedDevicePose_t[] allPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private TrackedDevice[] trackedDevices;
        private int currentTrackerCount = 0;

        private void GetAllDevicePose()
        {
            int trackerCount = 0;

            OpenVR.System.GetDeviceToAbsoluteTrackingPose(universeOrigin, framesAhead * (Time.timeScale / SteamVR.instance.hmd_DisplayFrequency), allPoses);
            for (uint index = 0; index < OpenVR.k_unMaxTrackedDeviceCount; index++)
            {
                var pose = allPoses[index];
                var trackedDevice = trackedDevices[index];

                var deviceClass = OpenVR.System.GetTrackedDeviceClass(index);
                trackedDevice.DeviceClass = deviceClass;

                if (IsPositionTrackedDevice(index) && (deviceClass == ETrackedDeviceClass.HMD || deviceClass == ETrackedDeviceClass.Controller || deviceClass == ETrackedDeviceClass.GenericTracker))
                {
                    trackedDevice.SerialNumber = GetTrackerSerialNumber(index);
                    trackedDevice.Pose = pose;
                    trackedDevice.IsActive = pose.bDeviceIsConnected;
                }
                else
                {
                    if (trackedDevice.IsActive)
                    {
                        trackedDevice.IsActive = false;
                        trackedDevice.SerialNumber = null;
                    }
                }

                if (deviceClass == ETrackedDeviceClass.Controller)
                {
                    trackedDevice.ControllerRole = OpenVR.System.GetControllerRoleForTrackedDeviceIndex(index);
                }

                if (deviceClass == ETrackedDeviceClass.GenericTracker && pose.bDeviceIsConnected)
                {
                    trackerCount++;
                }
            }

            currentTrackerCount = trackerCount;
        }

        private string GetTrackerSerialNumber(uint deviceIndex)
        {
            var buffer = new StringBuilder();
            var error = default(ETrackedPropertyError);

            // Capacity取得
            var capacity = (int)OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, null, 0, ref error);
            if (capacity < 1) return null;

            // Capacity分の文字列取得
            OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_SerialNumber_String, buffer, (uint)buffer.EnsureCapacity(capacity), ref error);
            if (error != ETrackedPropertyError.TrackedProp_Success) return null;

            return buffer.ToString();
        }

        private float GetTrackerBatteryPercentage(uint deviceIndex)
        {
            var error = default(ETrackedPropertyError);
            var percentage = OpenVR.System.GetFloatTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
            if (error != ETrackedPropertyError.TrackedProp_Success) return -1f;

            return percentage;
        }

        private int GetDeviceIndex(string serialNumber) => serialNumber == null ? -1 : Array.FindIndex(trackedDevices, d => d.SerialNumber == serialNumber);
        private int GetDeviceIndex(ETrackedDeviceClass deviceClass) => Array.FindIndex(trackedDevices, d => d.DeviceClass == deviceClass);

        #endregion

        private void Awake()
        {
            // 変数の初期化
            universeOrigin = SteamVR_Settings.instance.trackingSpace;
            poseActionData_size = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));
            trackedDevices = Enumerable.Range(0, allPoses.Length).Select(i => new TrackedDevice { Index = i }).ToArray();
        }

        private void Start()
        {
            // ポーズの初期化
            actionHandle = GetActionHandle(PoseAction.fullPath);
            // 全デバイス情報を1度取得しておく
            GetAllDevicePose();
            // 起動時はロールで初期化しておく(HMDとコントローラーは最低限自動で動く)
            AttachRoleDevices();
        }

        int skipFrames = 144;
        private void Update()
        {
            GetAllDevicePose();
            if (skipFrames > 0)
            {
                skipFrames--;
                return;
            }
            UpdateAllTrackerData();
        }

        private void UpdateAllTrackerData()
        {
            // 全トラッカーの位置データ更新
            foreach (var trackerTarget in TrackerTargets)
            {
                if (trackerTarget.SourceTransform != null) // スキップ設定、外部CameraRig等ですでに位置データ処理しているときを想定
                {
                    trackerTarget.TargetTransform.localPosition = trackerTarget.SourceTransform.localPosition;
                    trackerTarget.TargetTransform.localRotation = trackerTarget.SourceTransform.localRotation;
                }
                else
                {
                    if (trackerTarget.InputSourceHandle != 0) // Roleでトラッカー位置取得するとき
                    {
                        var poseActionData = GetPoseByHandle(trackerTarget.InputSourceHandle);
                        if (poseActionData.bActive)
                        {
                            trackerTarget.PoseIsValid = poseActionData.pose.bPoseIsValid;
                            ApplyPoseToTransform(poseActionData.pose, trackerTarget.TargetTransform);
                        }

                        trackerTarget.BatteryPercentage = -1f;
                    }
                    else if (trackerTarget.SerialNumber != null) // Index番号でトラッカー位置取得するとき
                    {
                        // シリアルナンバーがとっておいた値と違うときIndexが変わってるため再取得(ここに入ることはないはず)
                        if (trackerTarget.DeviceIndex < 0 || trackerTarget.SerialNumber != trackedDevices[trackerTarget.DeviceIndex].SerialNumber)
                        {
                            trackerTarget.DeviceIndex = GetDeviceIndex(trackerTarget.SerialNumber);
                        }
                        // DeviceIndexが-1の時デバイスがいなくなっているので何もしない(勝手に無くなることはないはず)
                        if (trackerTarget.DeviceIndex < 0) continue;

                        var trackedDevice = trackedDevices[trackerTarget.DeviceIndex];
                        if (trackedDevice.IsActive)
                        {
                            trackerTarget.PoseIsValid = trackedDevice.Pose.bPoseIsValid;
                            ApplyPoseToTransform(trackedDevice.Pose, trackerTarget.TargetTransform);
                        }

                        trackerTarget.BatteryPercentage = GetTrackerBatteryPercentage((uint)trackerTarget.DeviceIndex);
                    }
                }
            }
        }

        private void ApplyPoseToTransform(TrackedDevicePose_t pose, Transform targetTransform)
        {
            targetTransform.localPosition = pose.mDeviceToAbsoluteTracking.GetPosition();
            targetTransform.localRotation = pose.mDeviceToAbsoluteTracking.GetRotation();
        }

        /// <summary>
        /// ポジショントラッキング対応のデバイスかどうかを返す
        /// </summary>
        /// <param name="index">SteamVRのデバイス番号</param>
        private bool IsPositionTrackedDevice(uint index)
        {
            ETrackedPropertyError errorCode = ETrackedPropertyError.TrackedProp_Success;

            // ポジショントラッキング対応のデバイスはこのプロパティに対応してないが、エラー時はfalseを返してくるので問題はない
            return !OpenVR.System.GetBoolTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_NeverTracked_Bool, ref errorCode);
        }
#endif

        /// <summary>
        /// 自動で指定したデバイスを指定した部位に割り当て
        /// </summary>
        /// <param name="worldForwardVector">HmdTransform.forward</param>
        /// <param name="worldUpVector">HmdTransform.up</param>
        public void AutoAttachTrackerTargets(Vector3? worldForwardVector = null, Vector3? worldUpVector = null)
        {
#if !UNITY_ANDROID
            // 全てのトラッカーの割り当てを解除
            foreach (var trackerTarget in TrackerTargets)
            {
                SetTrackerTarget(trackerTarget, null);
            }

            // OrderBy: 1,2,3,4
            // OrderByDescending: 4,3,2,1

            int detectTrackerCount = currentTrackerCount;

            if (detectTrackerCount <= 3) // 認識しているトラッカーが3個以下の時はロールを使わない
            {
                var hmds = trackedDevices.Where(d => d.IsActive && d.DeviceClass == ETrackedDeviceClass.HMD).ToList(); // HMDは一つのみ
                var controllers = trackedDevices.Where(d => d.IsActive && d.DeviceClass == ETrackedDeviceClass.Controller).ToList();
                var trackers = trackedDevices.Where(d => d.IsActive && d.DeviceClass == ETrackedDeviceClass.GenericTracker).ToList();

                // 頭
                var trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.Head);
                var headDevice = AttachIndexDevice(trackerTarget, hmds, controllers, trackers,
                    c => c.OrderByDescending(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().y),
                    t => t.OrderByDescending(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().y));

                var forward = worldForwardVector ?? (headDevice != null ? trackerTarget.TargetTransform.forward : Vector3.forward);
                var up = worldUpVector ?? (headDevice != null ? trackerTarget.TargetTransform.up : Vector3.up);

                // 左手
                trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.LeftHand);
                AttachIndexDevice(trackerTarget, hmds, controllers, trackers,
                    c => c.OrderBy(d => RecenterPoint(forward, up, d.Pose.mDeviceToAbsoluteTracking.GetPosition()).x),
                    t => t.OrderBy(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().x));

                // 右手
                trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.RightHand);
                AttachIndexDevice(trackerTarget, hmds, controllers, trackers,
                    c => c.OrderByDescending(d => RecenterPoint(forward, up, d.Pose.mDeviceToAbsoluteTracking.GetPosition()).x),
                    t => t.OrderByDescending(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().x));

                // トラッカー残数が1で腰がトラッカーの時は足の処理をスキップ
                trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.Waist);
                if (trackers.Count != 1 || trackerTarget == null || trackerTarget.UseDeviceType != TrackingDeviceType.GenericTracker)
                {
                    // 左足
                    trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.LeftFoot);
                    AttachIndexDevice(trackerTarget, hmds, controllers, trackers,
                        c => c.OrderBy(d => RecenterPoint(forward, up, d.Pose.mDeviceToAbsoluteTracking.GetPosition()).x),
                        t => t.OrderBy(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().y)
                                .Take(2)
                                .OrderBy(d => RecenterPoint(forward, up, d.Pose.mDeviceToAbsoluteTracking.GetPosition()).x));

                    // 右足
                    trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.RightFoot);
                    AttachIndexDevice(trackerTarget, hmds, controllers, trackers,
                        c => c.OrderByDescending(d => RecenterPoint(forward, up, d.Pose.mDeviceToAbsoluteTracking.GetPosition()).x),
                        t => t.OrderBy(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().y)
                                .Take(2)
                                .OrderByDescending(d => RecenterPoint(forward, up, d.Pose.mDeviceToAbsoluteTracking.GetPosition()).x));
                }

                // 腰
                trackerTarget = TrackerTargets.FirstOrDefault(d => d.TrackerPosition == TrackerPositions.Waist);
                AttachIndexDevice(trackerTarget, hmds, controllers, trackers,
                    c => c.OrderByDescending(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().y),
                    t => t.OrderByDescending(d => d.Pose.mDeviceToAbsoluteTracking.GetPosition().y));

            }
            else // 認識しているトラッカーが3個より多いときはロールを優先する
            {
                AttachRoleDevices();
            }

            UpdateAllTrackerData(); // 割り当てたらすぐに全トラッカーの処理をする
#endif
        }

#if !UNITY_ANDROID
        // forwardVectorとupVectorには正面方向(hmdTransform.forward, hmdTransform.up)を入れる
        private Vector3 RecenterPoint(Vector3 forwardVector, Vector3 upVector, Vector3 pos)
        {
            var rotation = Quaternion.LookRotation(forwardVector, upVector);
            var frontRotation = Quaternion.identity;
            var diffRotation = frontRotation * Quaternion.Inverse(rotation);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, diffRotation, Vector3.one);
            //Matrix4x4 inverse = matrix.inverse;
            return matrix.MultiplyPoint3x4(pos);
        }

        private TrackedDevice SetTrackerTarget(TrackerTarget trackerTarget, int deviceIndex) => SetTrackerTarget(trackerTarget, trackedDevices[deviceIndex]);

        private TrackedDevice SetTrackerTarget(TrackerTarget trackerTarget, TrackedDevice tracker)
        {
            trackerTarget.InputSourceHandle = 0;
            trackerTarget.SerialNumber = tracker == null ? null : tracker.SerialNumber;
            trackerTarget.DeviceIndex = tracker == null ? -1 : tracker.Index;
            return tracker;
        }

        private void SetTrackerTarget(TrackerTarget trackerTarget, ulong inputSourceHandle)
        {
            trackerTarget.InputSourceHandle = inputSourceHandle;
            trackerTarget.SerialNumber = null;
            trackerTarget.DeviceIndex = -1;
        }

        private TrackedDevice AttachIndexDevice(TrackerTarget trackerTarget, List<TrackedDevice> hmds, List<TrackedDevice> controllers, List<TrackedDevice> trackers,
            Func<IEnumerable<TrackedDevice>, IEnumerable<TrackedDevice>> controllerSelector,
            Func<IEnumerable<TrackedDevice>, IEnumerable<TrackedDevice>> trackerSelector)
        {
            if (trackerTarget == null) return null;

            TrackedDevice device = null;

            if (trackerTarget.UseDeviceType == TrackingDeviceType.HMD)
            { // HMDの時(基本HMDは1つしか認識されないためそのまま割り当てる)
                var hmd = hmds.FirstOrDefault();
                device = SetTrackerTarget(trackerTarget, hmd);
                if (device != null) hmds.Remove(device);
            }
            else if (trackerTarget.UseDeviceType == TrackingDeviceType.Controller)
            { // コントローラー選択時は最も高い位置にあるコントローラーを頭とする
                var controller = controllerSelector(controllers).FirstOrDefault();
                device = SetTrackerTarget(trackerTarget, controller);
                if (device != null) controllers.Remove(device);
            }
            else
            { // トラッカー選択時は最も高い位置にあるトラッカーを頭とする
                var tracker = trackerSelector(trackers).FirstOrDefault();
                device = SetTrackerTarget(trackerTarget, tracker);
                if (device != null) trackers.Remove(device);
            }

            return device;
        }

        private void AttachRoleDevices()
        {
            foreach (var trackerTarget in TrackerTargets)
            {
                if (trackerTarget.TargetTransform == null)
                {
                    var newTarget = new GameObject(trackerTarget.TrackerPosition.ToString());
                    if (trackersParent == null) trackersParent = transform;
                    newTarget.transform.SetParent(TrackersParent, false);
                    trackerTarget.TargetTransform = newTarget.transform;
                }

                // HMDの場合はロールで取れないのでインデックスとシリアルナンバーを使う
                if (trackerTarget.UseDeviceType == TrackingDeviceType.HMD)
                {
                    SetTrackerTarget(trackerTarget, GetDeviceIndex((ETrackedDeviceClass)trackerTarget.UseDeviceType));
                }
                else // コントローラーとトラッカーはロールで問題ないはず
                {
                    SetTrackerTarget(trackerTarget, GetTrackerInputSourceHandle(trackerTarget.TrackerPosition));
                }
            }
        }
#endif
    }

#if !UNITY_ANDROID
    public class TrackedDevice
    {
        public TrackedDevicePose_t Pose;
        public ETrackedDeviceClass DeviceClass;
        public ETrackedControllerRole ControllerRole;
        public bool IsActive;
        public int Index; // 配列のIndexと同義
        public string SerialNumber;
    }
#endif
}
