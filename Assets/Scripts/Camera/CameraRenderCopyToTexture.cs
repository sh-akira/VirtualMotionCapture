using UnityEngine;

namespace VMC
{
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
}