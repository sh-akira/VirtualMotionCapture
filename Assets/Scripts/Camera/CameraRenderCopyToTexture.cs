using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRenderCopyToTexture : MonoBehaviour
{
    [SerializeField]
    private RenderTexture targetTexture = null;

    void OnPostRender()
    {
        if (targetTexture != null)
            Graphics.Blit(null, targetTexture);
    }
}
