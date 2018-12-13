using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Photo
{
    public static byte[] TakePNGPhoto(Camera camera, Resolution resolution, bool transparentBackground)
    {
        var renderTexture = new RenderTexture(resolution.width, resolution.height, 24, RenderTextureFormat.ARGB32);
        var oldtarget = camera.targetTexture;
        camera.targetTexture = renderTexture;
        var oldClearFlags = camera.clearFlags;
        var oldBackgroundColor = camera.backgroundColor;
        if (transparentBackground)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
        }
        var photoTexture = new Texture2D(resolution.width, resolution.height, TextureFormat.ARGB32, false);
        camera.Render();
        var oldactive = RenderTexture.active;
        RenderTexture.active = renderTexture;
        photoTexture.ReadPixels(new Rect(0, 0, resolution.width, resolution.height), 0, 0);
        camera.targetTexture = oldtarget;
        RenderTexture.active = oldactive;
        if (transparentBackground)
        {
            camera.backgroundColor = oldBackgroundColor;
            camera.clearFlags = oldClearFlags;
        }
        Object.Destroy(renderTexture);
        return photoTexture.EncodeToPNG();
    }
}
