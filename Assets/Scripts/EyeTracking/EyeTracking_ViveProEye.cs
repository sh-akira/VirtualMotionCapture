using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;
using ViveSR.anipal.Eye;

public class EyeTracking_ViveProEye : MonoBehaviour
{

    public GameObject MonitorPosition;
    public GameObject LookTarget;
    public Vector3 StartPos;

    public float ScaleX = 2.0f;
    public float ScaleY = 1.5f;
    public float OffsetX = 0.0f;
    public float OffsetY = 0.0f;
    public float CenterX = 0.5f;
    public float CenterY = 0.5f;
    public float Smoothing = 0.7f;
    private Vector3 oldPoint;
    private bool isFirst = true;
    public ControlWPFWindow controlWPFWindow;
    public FaceController faceController;
    public bool UseEyelidMovements = true;

    private Dictionary<EyeShape, float> EyeWeightings = new Dictionary<EyeShape, float>();

    // Use this for initialization
    void Start()
    {
        controlWPFWindow.ModelLoadedAction += ModelLoaded;
        controlWPFWindow.SetEyeTracking_ViveProEyeOffsetsAction += SetEyeTracking_ViveProEyeOffsets;
        controlWPFWindow.SetEyeTracking_ViveProEyeUseEyelidMovementsAction += SetEyeTracking_ViveProEyeUseEyelidMovements;
    }

    private void ModelLoaded(GameObject currentModel)
    {
        ModelInitialize(currentModel);
    }

    private void SetEyeTracking_ViveProEyeOffsets(PipeCommands.SetEyeTracking_ViveProEyeOffsets offsets)
    {
        ScaleX = offsets.ScaleHorizontal;
        ScaleY = offsets.ScaleVertical;
        OffsetX = offsets.OffsetHorizontal;
        OffsetY = offsets.OffsetVertical;
        if (MonitorPosition != null)
        {
            MonitorPosition.transform.localScale = new Vector3(ScaleX, ScaleY, 1);
            MonitorPosition.transform.localPosition = new Vector3(OffsetX, OffsetY, 0);
        }
    }

    private void SetEyeTracking_ViveProEyeUseEyelidMovements(PipeCommands.SetEyeTracking_ViveProEyeUseEyelidMovements useEyelidMovements)
    {
        UseEyelidMovements = useEyelidMovements.Use;
        if(UseEyelidMovements == false)
        {
            faceController.SetBlink_L(0.0f);
            faceController.SetBlink_R(0.0f);
        }
        faceController.ViveProEyeEnabled = UseEyelidMovements;
    }

    private void ModelInitialize(GameObject currentModel)
    {
        if (currentModel == null) return;
        var animator = currentModel.GetComponent<Animator>();
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        //モデルの頭の子に目線向ける先を設定
        if (MonitorPosition == null) MonitorPosition = new GameObject("ViveProEye_MonitorPosition");
        MonitorPosition.transform.parent = head;
        MonitorPosition.transform.localRotation = Quaternion.identity;
        MonitorPosition.transform.localScale = new Vector3(ScaleX, ScaleY, 1);
        MonitorPosition.transform.localPosition = new Vector3(OffsetX, OffsetY, 0);
        if (LookTarget == null) LookTarget = new GameObject("LookTarget");
        LookTarget.transform.parent = MonitorPosition.transform;
        LookTarget.transform.localRotation = Quaternion.identity;
        LookTarget.transform.localPosition = new Vector3(0, 0, 1f); //すべて0地点にすると目が荒ぶる
        var vrmLookAtHead = currentModel.GetComponent<VRM.VRMLookAtHead>();
        vrmLookAtHead.Target = LookTarget.transform;
        StartPos = LookTarget.transform.localPosition;
        isFirst = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (Camera.main == null) return;

        if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING &&
            SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT) return;

        //まぶた
        bool isLeftEyeActive = false;
        bool isRightEyeActive = false;
        float leftEyeOpenness = 1.0f;
        float rightEyeOpenness = 1.0f;
        if (SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.WORKING)
        {
            isLeftEyeActive = SRanipal_Eye.GetEyeOpenness(EyeIndex.LEFT, out leftEyeOpenness);
            isRightEyeActive = SRanipal_Eye.GetEyeOpenness(EyeIndex.RIGHT, out rightEyeOpenness);
        }
        else if (SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT)
        {
            isLeftEyeActive = true;
            isRightEyeActive = true;
        }

        if (isLeftEyeActive || isRightEyeActive)
        {
            EyeWeightings[EyeShape.Eye_Left_Blink] = 1 - leftEyeOpenness;
            EyeWeightings[EyeShape.Eye_Right_Blink] = 1 - rightEyeOpenness;
            UpdateEyeShapes(EyeWeightings);
        }
        else
        {
            for (int i = 0; i < (int)EyeShape.Max; ++i)
            {
                bool isBlink = ((EyeShape)i == EyeShape.Eye_Left_Blink || (EyeShape)i == EyeShape.Eye_Right_Blink);
                EyeWeightings[(EyeShape)i] = isBlink ? 1 : 0;
            }

            UpdateEyeShapes(EyeWeightings);

            return;
        }

        //目線
        Vector3 GazeOriginCombinedLocal, GazeDirectionCombinedLocal = Vector3.zero;
        if (SRanipal_Eye.GetGazeRay(GazeIndex.COMBINE, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
        else if (SRanipal_Eye.GetGazeRay(GazeIndex.LEFT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
        else if (SRanipal_Eye.GetGazeRay(GazeIndex.RIGHT, out GazeOriginCombinedLocal, out GazeDirectionCombinedLocal)) { }
        if (LookTarget != null) LookTarget.transform.localPosition = Smoothify(GazeDirectionCombinedLocal);

    }
    public void UpdateEyeShapes(Dictionary<EyeShape, float> eyeWeightings)
    {
        if (UseEyelidMovements == false) return;
        if (LookTarget == null) return;
        foreach (var weightings in eyeWeightings)
        {
            EyeShape eyeShape = weightings.Key;

            if (eyeShape == EyeShape.Eye_Left_Blink)
            {
                faceController.SetBlink_L(weightings.Value);
            }
            else if (eyeShape == EyeShape.Eye_Right_Blink)
            {
                faceController.SetBlink_R(weightings.Value);
            }
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
