using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using Tobii.Gaming;
using UnityEngine;
using UnityMemoryMappedFile;

public class EyeTracking_Tobii : MonoBehaviour
{

    public GameObject MonitorPosition;
    public GameObject LookTarget;
    public Vector3 StartPos;

    public float ScaleX = 0.5f;
    public float ScaleY = 0.2f;
    public float OffsetX = 0.0f;
    public float OffsetY = 0.0f;
    public float CenterX = 0.5f;
    public float CenterY = 0.5f;
    public float Smoothing = 0.7f;
    private Vector3 oldPoint;
    private bool isFirst = true;
    public ControlWPFWindow controlWPFWindow;

    // Use this for initialization
    void Start()
    {
        controlWPFWindow.ModelLoadedAction += ModelLoaded;
        controlWPFWindow.SetEyeTracking_TobiiOffsetsAction += SetEyeTracking_TobiiOffsets;
        controlWPFWindow.EyeTracking_TobiiCalibrationAction += EyeTracking_TobiiCalibration;
    }

    private void ModelLoaded(GameObject currentModel)
    {
        Calibration(currentModel, true);
    }

    private void SetEyeTracking_TobiiOffsets(PipeCommands.SetEyeTracking_TobiiOffsets offsets)
    {
        ScaleX = offsets.ScaleHorizontal;
        ScaleY = offsets.ScaleVertical;
        OffsetX = offsets.OffsetHorizontal;
        OffsetY = offsets.OffsetVertical;
    }

    private void EyeTracking_TobiiCalibration(GameObject currentModel)
    {
        Calibration(currentModel, false);
    }

    private void Calibration(GameObject currentModel, bool fromSetting)
    {
        if (currentModel == null) return;
        if (fromSetting == false && TobiiAPI.IsConnected == false) return;
        var animator = currentModel.GetComponent<Animator>();
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        //モデルの頭の前方50cm地点にモニターがあることにする
        if (MonitorPosition == null) MonitorPosition = new GameObject("Tobii_MonitorPosition");
        MonitorPosition.transform.parent = null;
        if (fromSetting)
        {
            var centerPos = controlWPFWindow.GetEyeTracking_TobiiLocalPosition(MonitorPosition.transform);
            CenterX = centerPos.x;
            CenterY = centerPos.y;
        }
        else
        {
            MonitorPosition.transform.position = head.position + head.forward * 0.5f; //頭の前方50cm
            MonitorPosition.transform.rotation = head.rotation;
            var gazePoint = GazeViewportToMonitorViewport(TobiiAPI.GetGazePoint().Viewport);
            CenterX = gazePoint.x;
            CenterY = gazePoint.y;
            controlWPFWindow.SetEyeTracking_TobiiPosition(MonitorPosition.transform, CenterX, CenterY);
        }
        if (LookTarget == null) LookTarget = new GameObject("LookTarget");
        LookTarget.transform.parent = MonitorPosition.transform;
        LookTarget.transform.localRotation = Quaternion.identity;
        LookTarget.transform.localPosition = new Vector3(0, 0, 0f);
        var vrmLookAtHead = currentModel.GetComponent<VRM.VRMLookAtHead>();
        vrmLookAtHead.Target = LookTarget.transform;
        StartPos = LookTarget.transform.localPosition;
        isFirst = true;
    }

    //viewportはウインドウ左下基準0～1.0
    private Vector2 GazeViewportToMonitorViewport(Vector2 viewport)
    {
        var monitorw = Screen.currentResolution.width;
        var monitorh = Screen.currentResolution.height;
        var windowrect = NativeMethods.GetUnityWindowPosition();
        var winx = windowrect.left;
        var winbottom = windowrect.bottom;
        var winw = windowrect.right - windowrect.left;
        var winh = windowrect.bottom - windowrect.top;
        var clientw = Screen.width;
        var clienth = Screen.height;
        var borderw = (winw - clientw) / 2;
        var titleh = winh - borderw - clienth;
        var clientx = winx + borderw;
        var clientbottom = (monitorh - winbottom) + borderw;
        var tmpx = clientw * viewport.x;
        var tmpy = clienth * viewport.y;
        var realx = tmpx + clientx;
        var realy = tmpy + clientbottom;
        var viewportx = realx / monitorw;
        var viewporty = realy / monitorh;
        return new Vector2(viewportx, viewporty);
    }

    // Update is called once per frame
    void Update()
    {
        if (TobiiAPI.IsConnected && LookTarget != null && MonitorPosition != null)
        {
            //var headPose = TobiiAPI.GetHeadPose();
            //if (headPose.IsRecent())
            //{
            //    MonitorPosition.transform.localRotation = Quaternion.Lerp(MonitorPosition.transform.localRotation, Quaternion.Inverse(headPose.Rotation), Time.unscaledDeltaTime * 10f);
            //}

            var gazePoint = GazeViewportToMonitorViewport(TobiiAPI.GetGazePoint().Viewport);

            Vector3 gazePointInWorld = new Vector3(StartPos.x + ((gazePoint.x - CenterX) * ScaleX) + OffsetX, StartPos.y + ((gazePoint.y - CenterY) * ScaleY) + OffsetY, StartPos.z);
            LookTarget.transform.localPosition = Smoothify(gazePointInWorld);
        }
    }
    private Vector3 Smoothify(Vector3 point)
    {
        if (isFirst)
        {
            oldPoint = point;
            isFirst = false;
        }

        var smoothedPoint = new Vector3(
            point.x * (1.0f - Smoothing) + oldPoint.x * Smoothing,
            point.y * (1.0f - Smoothing) + oldPoint.y * Smoothing,
            point.z);

        oldPoint = smoothedPoint;

        return smoothedPoint;
    }
}
