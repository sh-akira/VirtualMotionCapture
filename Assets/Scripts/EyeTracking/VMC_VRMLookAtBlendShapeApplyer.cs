#pragma warning disable 0414, 0649
using UnityEngine;
using VRM;

public class VMC_VRMLookAtBlendShapeApplyer : MonoBehaviour, IVRMComponent
{
    public bool DrawGizmo = true;

    [SerializeField, Header("Degree Mapping")]
    public CurveMapper Horizontal = new CurveMapper(90.0f, 1.0f);

    [SerializeField]
    public CurveMapper VerticalDown = new CurveMapper(90.0f, 1.0f);

    [SerializeField]
    public CurveMapper VerticalUp = new CurveMapper(90.0f, 1.0f);

    [SerializeField]
    public bool m_notSetValueApply;

    public FaceController faceController;

    public void OnImported(VRMImporterContext context)
    {
        var gltfFirstPerson = context.GLTF.extensions.VRM.firstPerson;
        Horizontal.Apply(gltfFirstPerson.lookAtHorizontalOuter);
        VerticalDown.Apply(gltfFirstPerson.lookAtVerticalDown);
        VerticalUp.Apply(gltfFirstPerson.lookAtVerticalUp);
    }

    VRMLookAtHead m_head;

    private void Start()
    {
        m_head = GetComponent<VRMLookAtHead>();
        if (faceController == null) faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
        if (m_head == null)
        {
            enabled = false;
            return;
        }
        m_head.YawPitchChanged += ApplyRotations;
    }

    void ApplyRotations(float yaw, float pitch)
    {
#pragma warning disable 0618
        if (yaw < 0)
        {
            // Left
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookRight, 0); // clear first
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookLeft, Mathf.Clamp(Horizontal.Map(-yaw), 0, 1.0f));
        }
        else
        {
            // Right
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookLeft, 0); // clear first
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookRight, Mathf.Clamp(Horizontal.Map(yaw), 0, 1.0f));
        }

        if (pitch < 0)
        {
            // Down
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookUp, 0); // clear first
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookDown, Mathf.Clamp(VerticalDown.Map(-pitch), 0, 1.0f));
        }
        else
        {
            // Up
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookDown, 0); // clear first
            faceController.MixPreset(nameof(VMC_VRMLookAtBlendShapeApplyer), BlendShapePreset.LookUp, Mathf.Clamp(VerticalUp.Map(pitch), 0, 1.0f));
        }
#pragma warning restore 0618
    }
}
