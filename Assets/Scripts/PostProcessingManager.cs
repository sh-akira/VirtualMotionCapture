//gpsnmeajp
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;


namespace VMC
{
    public class PostProcessingManager : MonoBehaviour
    {
        PostProcessVolume postProcessVolume;
        private void Start()
        {
            postProcessVolume = gameObject.AddComponent<PostProcessVolume>();
        }

        public void Apply(Settings d)
        {
            postProcessVolume.enabled = d.PPS_Enable;

            postProcessVolume.isGlobal = true;
            var sp = postProcessVolume.sharedProfile;
            if (sp == null)
            {
                sp = ScriptableObject.CreateInstance<PostProcessProfile>();
            }

            var bloom = sp.GetSetting<Bloom>();
            if (bloom == null)
            {
                bloom = sp.AddSettings<Bloom>();
            }
            bloom.active = true;
            bloom.enabled.overrideState = true;
            bloom.enabled.value = d.PPS_Bloom_Enable;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = d.PPS_Bloom_Intensity;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = d.PPS_Bloom_Threshold;
            bloom.fastMode.overrideState = true;
            bloom.fastMode.value = true;
            bloom.color.overrideState = true;
            bloom.color.value = new Color(d.PPS_Bloom_Color_r, d.PPS_Bloom_Color_g, d.PPS_Bloom_Color_b, d.PPS_Bloom_Color_a);

            var dof = sp.GetSetting<DepthOfField>();
            if (dof == null)
            {
                dof = sp.AddSettings<DepthOfField>();
            }
            dof.active = true;
            dof.enabled.overrideState = true;
            dof.enabled.value = d.PPS_DoF_Enable;
            dof.focusDistance.overrideState = true;
            dof.focusDistance.value = d.PPS_DoF_FocusDistance;
            dof.aperture.overrideState = true;
            dof.aperture.value = d.PPS_DoF_Aperture;
            dof.focalLength.overrideState = true;
            dof.focalLength.value = d.PPS_DoF_FocusLength;

            dof.kernelSize.overrideState = true;
            switch (d.PPS_DoF_MaxBlurSize)
            {
                case 0:
                    dof.kernelSize.value = KernelSize.Small;
                    break;
                case 1:
                    dof.kernelSize.value = KernelSize.Medium;
                    break;
                case 2:
                    dof.kernelSize.value = KernelSize.Large;
                    break;
                case 3:
                    dof.kernelSize.value = KernelSize.VeryLarge;
                    break;
                default:
                    dof.kernelSize.value = KernelSize.Small;
                    break;
            }


            var cg = sp.GetSetting<ColorGrading>();
            if (cg == null)
            {
                cg = sp.AddSettings<ColorGrading>();
            }
            cg.active = true;
            cg.enabled.overrideState = true;
            cg.enabled.value = d.PPS_CG_Enable;
            cg.temperature.overrideState = true;
            cg.temperature.value = d.PPS_CG_Temperature;
            cg.saturation.overrideState = true;
            cg.saturation.value = d.PPS_CG_Saturation;
            cg.contrast.overrideState = true;
            cg.contrast.value = d.PPS_CG_Contrast;
            cg.gamma.overrideState = true;
            cg.gamma.value = new Vector4(0, 0, 0, d.PPS_CG_Gamma);
            cg.colorFilter.overrideState = true;
            cg.colorFilter.value = new Color(d.PPS_CG_ColorFilter_r, d.PPS_CG_ColorFilter_g, d.PPS_CG_ColorFilter_b, d.PPS_CG_ColorFilter_a);

            var vg = sp.GetSetting<Vignette>();
            if (vg == null)
            {
                vg = sp.AddSettings<Vignette>();
            }
            vg.active = true;
            vg.enabled.overrideState = true;
            vg.enabled.value = d.PPS_Vignette_Enable;
            vg.intensity.overrideState = true;
            vg.intensity.value = d.PPS_Vignette_Intensity;
            vg.smoothness.overrideState = true;
            vg.smoothness.value = d.PPS_Vignette_Smoothness;
            vg.roundness.overrideState = true;
            vg.roundness.value = d.PPS_Vignette_Roundness;
            vg.color.overrideState = true;
            vg.color.value = new Color(d.PPS_Vignette_Color_r, d.PPS_Vignette_Color_g, d.PPS_Vignette_Color_b, d.PPS_Vignette_Color_a);

            var ca = sp.GetSetting<ChromaticAberration>();
            if (ca == null)
            {
                ca = sp.AddSettings<ChromaticAberration>();
            }
            ca.active = true;
            ca.enabled.overrideState = true;
            ca.enabled.value = d.PPS_CA_Enable;
            ca.intensity.overrideState = true;
            ca.intensity.value = d.PPS_CA_Intensity;
            ca.fastMode.overrideState = true;
            ca.fastMode.value = d.PPS_CA_FastMode;


            postProcessVolume.sharedProfile = sp;

            if (d.TurnOffAmbientLight)
            {
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            }
        }
    }
}