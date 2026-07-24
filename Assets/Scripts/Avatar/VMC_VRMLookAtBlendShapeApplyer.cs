#pragma warning disable 0414, 0649
using UnityEngine;
using UniVRM10;

namespace VMC
{
    public class VMC_VRMLookAtBlendShapeApplyer : MonoBehaviour
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

        public void OnImported(VRM10ObjectLookAt vrm10ObjectLookAt)
        {
            Horizontal = vrm10ObjectLookAt.HorizontalOuter;
            VerticalDown = vrm10ObjectLookAt.VerticalDown;
            VerticalUp = vrm10ObjectLookAt.VerticalUp;
        }

        //VRMLookAtHead m_head;

        private void Start()
        {
            Vrm10RuntimeLookAt lookAt = GetComponent<Vrm10Instance>().Runtime.LookAt;
            //m_head = GetComponent<VRMLookAtHead>();
            if (faceController == null) faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
            //if (m_head == null)
            {
                enabled = false;
                return;
            }
            //m_head.YawPitchChanged += ApplyRotations;
        }

        private ExpressionKey[] presets = new[] { ExpressionKey.LookLeft, ExpressionKey.LookRight, ExpressionKey.LookUp, ExpressionKey.LookDown };
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
            faceController.OverwritePresets(nameof(VMC_VRMLookAtBlendShapeApplyer), presets, blendShapeValues);
#pragma warning restore 0618
        }
    }
}