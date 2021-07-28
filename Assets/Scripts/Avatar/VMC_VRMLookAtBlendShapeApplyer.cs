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

    private BlendShapeKey[] presets = new[] { BlendShapeKey.CreateFromPreset(BlendShapePreset.LookLeft), BlendShapeKey.CreateFromPreset(BlendShapePreset.LookRight), BlendShapeKey.CreateFromPreset(BlendShapePreset.LookUp), BlendShapeKey.CreateFromPreset(BlendShapePreset.LookDown) };
    private float[] blendShapeValues = new float[4];

    void ApplyRotations(float yaw, float pitch)
    {
#pragma warning disable 0618
        if (yaw < 0)
        {
            // Left
            blendShapeValues[1] = 0;
            blendShapeValues[0] = Mathf.Clamp(Horizontal.Map(-yaw), 0, 1.0f);
        }
        else
        {
            // Right
            blendShapeValues[0] = 0;
            blendShapeValues[1] = Mathf.Clamp(Horizontal.Map(yaw), 0, 1.0f);
        }

        if (pitch < 0)
        {
            // Down
            blendShapeValues[2] = 0;
            blendShapeValues[3] = Mathf.Clamp(VerticalDown.Map(-pitch), 0, 1.0f);
        }
        else
        {
            // Up
            blendShapeValues[3] = 0;
            blendShapeValues[2] = Mathf.Clamp(VerticalUp.Map(pitch), 0, 1.0f);
        }
        faceController.MixPresets(nameof(VMC_VRMLookAtBlendShapeApplyer), presets, blendShapeValues);
#pragma warning restore 0618
    }
}
