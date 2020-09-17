//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;
using UnityEngine.Rendering.PostProcessing;

public class PostProcessingManager : MonoBehaviour
{
    PostProcessVolume postProcessVolume;
    private void Start()
    {
        postProcessVolume = GetComponent<PostProcessVolume>();
    }

    public void Apply(PipeCommands.SetPostProcessing d)
    {
        var sp = postProcessVolume.sharedProfile;

        var bloom = sp.GetSetting<Bloom>();
        bloom.active = true;
        bloom.enabled.value = d.Bloom_Enable;
        bloom.intensity.value = d.Bloom_Intensity;
        bloom.threshold.value = d.Bloom_Threshold;

        var dof = sp.GetSetting<DepthOfField>();
        dof.active = true;
        dof.enabled.value = d.DoF_Enable;
        dof.focusDistance.value = d.DoF_FocusDistance;
        dof.aperture.value = d.DoF_Aperture;
        dof.focalLength.value = d.DoF_FocusLength;

        switch (d.DoF_MaxBlurSize) {
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
        cg.active = true;
        cg.enabled.value = d.CG_Enable;
        cg.saturation.value = d.CG_Saturation;
        cg.contrast.value = d.CG_Contrast;

        var vg = sp.GetSetting<Vignette>();
        vg.active = true;
        vg.enabled.value = d.Vignette_Enable;
        vg.intensity.value = d.Vignette_Intensity;
        vg.smoothness.value = d.Vignette_Smoothness;
        vg.roundness.value = d.Vignette_Rounded;

        var ca = sp.GetSetting<ChromaticAberration>();
        ca.active = true;
        ca.enabled.value = d.CA_Enable;
        ca.intensity.value = d.CA_Intensity;


        postProcessVolume.sharedProfile = sp;


    }
}
