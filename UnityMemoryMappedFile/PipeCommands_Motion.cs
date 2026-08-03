using System;
using System.Collections.Generic;

namespace UnityMemoryMappedFile
{
    public partial class PipeCommands
    {
        // ===== モーション再生 =====

        public class Motion_GetSetting { }

        public class Motion_SetSetting
        {
            public List<string> MotionFiles { get; set; }
            public int RepeatMode { get; set; } // 0:1ショット 1:1ファイルループ 2:リストループ

            public bool ApplyRootPosition { get; set; }
            public bool ApplyRootRotation { get; set; }
            public bool ApplySpine { get; set; }
            public bool ApplyChest { get; set; }
            public bool ApplyHead { get; set; }
            public bool ApplyLeftArm { get; set; }
            public bool ApplyRightArm { get; set; }
            public bool ApplyLeftHand { get; set; }
            public bool ApplyRightHand { get; set; }
            public bool ApplyLeftLeg { get; set; }
            public bool ApplyRightLeg { get; set; }
            public bool ApplyLeftFoot { get; set; }
            public bool ApplyRightFoot { get; set; }
            public bool ApplyLeftFinger { get; set; }
            public bool ApplyRightFinger { get; set; }
            public bool ApplyEye { get; set; }
            //VRMAに含まれる表情・視線を再生時に適用するか
            public bool ApplyExpression { get; set; }
            public bool ApplyLookAt { get; set; }

            // 記録設定
            public int RecordFps { get; set; }
            public int RecordCountdown { get; set; }
            public bool RecordMotion { get; set; }
            public bool RecordExpressionPreset { get; set; }
            public bool RecordExpressionCustom { get; set; }
            public bool RecordLookAt { get; set; }
        }

        public class Motion_LoadFile
        {
            public string Path { get; set; }
        }

        public class Motion_ReturnLoadFile
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public MotionFileInfo Info { get; set; }
        }

        public class Motion_RemoveFile
        {
            public int Index { get; set; }
        }

        public class Motion_GetFileList { }

        public class Motion_ReturnFileList
        {
            public List<MotionFileInfo> Files { get; set; }
        }

        public class Motion_Play
        {
            public int Index { get; set; }
        }

        public class Motion_Pause { }

        public class Motion_Stop { }

        public class Motion_Seek
        {
            public float Seconds { get; set; }
        }

        public class Motion_FrameStep
        {
            public int Delta { get; set; }
        }

        public class Motion_SetRepeatMode
        {
            public int RepeatMode { get; set; }
        }

        // Unity→WPF push
        public class Motion_PlaybackStatus
        {
            public int Index { get; set; }
            public float Time { get; set; }
            public float Length { get; set; }
            public int State { get; set; } // 0:停止 1:再生中 2:一時停止 3:ポーズ適用中
        }

        // ===== モーション記録 =====

        //記録設定のみを更新する(再生・記録の両ウインドウを同時に開いた際の相互上書きを防ぐため分離)
        public class Motion_SetRecordSetting
        {
            public int RecordFps { get; set; }
            public int RecordCountdown { get; set; }
            public bool RecordMotion { get; set; }
            public bool RecordExpressionPreset { get; set; }
            public bool RecordExpressionCustom { get; set; }
            public bool RecordLookAt { get; set; }
        }

        public class Motion_StartRecording { }

        public class Motion_StopRecording { }

        // Unity→WPF push
        public class Motion_RecordingStatus
        {
            public int State { get; set; } // 0:停止 1:カウントダウン中 2:記録中 3:記録済み(編集可)
            public float Time { get; set; }
            public float Countdown { get; set; }
            public int FrameCount { get; set; }
            public float Fps { get; set; }
        }

        public class Motion_PreviewSeek
        {
            public int Frame { get; set; }
        }

        public class Motion_PreviewPlay
        {
            public int StartFrame { get; set; }
            public int EndFrame { get; set; }
        }

        public class Motion_PreviewPause { }

        public class Motion_PreviewStop { }

        // Unity→WPF push
        public class Motion_PreviewStatus
        {
            public int Frame { get; set; }
            public bool Playing { get; set; }
        }

        public class Motion_SaveRecording
        {
            public string Path { get; set; }
            public int Format { get; set; } // 0:VRMA 1:BVH
            public int StartFrame { get; set; }
            public int EndFrame { get; set; }
        }

        public class Motion_ReturnSaveRecording
        {
            public bool Success { get; set; }
            public string Error { get; set; }
        }
    }

    public class MotionFileInfo
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public float Length { get; set; }
        public float FrameRate { get; set; }
        public int FrameCount { get; set; }
        public bool IsVrma { get; set; }
    }
}
