using System;
using UnityEngine;

namespace DVRSDK.Avatar.Tracking
{
    public interface ITracker
    {
        Transform TrackersParent { get; }
        TrackerTarget GetTrackerTarget(TrackerPositions trackerPosition);
        Vector3 GetIKOffsetPosition(TrackerPositions targetPosition, TrackingDeviceType deviceType);
        Quaternion GetIKOffsetRotation(TrackerPositions targetPosition, TrackingDeviceType deviceType);
    }

    [Serializable]
    public class TrackerTarget
    {
        [Header("取り付ける先")]
        public TrackerPositions TrackerPosition;
        [Header("デバイスタイプを指定します")]
        public TrackingDeviceType UseDeviceType = TrackingDeviceType.GenericTracker;
        [Header("指定した場合そちらの位置情報を使用します")]
        public Transform SourceTransform;

        public Transform TargetTransform { get; set; }
        public ulong InputSourceHandle { get; set; }
        public string SerialNumber { get; set; }
        public int DeviceIndex { get; set; }
        public bool PoseIsValid { get; set; }
        public float BatteryPercentage { get; set; } = -1f;
    }

    public enum TrackingDeviceType
    {
        Invalid = 0,
        HMD = 1,
        Controller = 2,
        GenericTracker = 3,
        TrackingReference = 4,
        DisplayRedirect = 5,
        Max = 6,
    }

    /// <summary>
    /// SteamVRで割り当て可能なトラッカー位置(拡張SteamVR_Input_Sources)
    /// ElbowやKneeが現在のバージョンに含まれていない為独自に定義
    /// </summary>
    public enum TrackerPositions
    {
        Any,
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot,
        LeftShoulder,
        RightShoulder,
        Waist,
        Chest,
        Head,
        Gamepad,
        Camera,
        Keyboard,
        Treadmill,
        LeftElbow,
        RightElbow,
        LeftKnee,
        RightKnee,
        Stylus,
    }
}
