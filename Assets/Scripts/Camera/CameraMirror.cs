using UnityEngine;

namespace VMC
{
    public class CameraMirror : MonoBehaviour
    {
        private Camera cam;

        public bool MirrorEnable = false;

        void Start()
        {
            cam = GetComponent<Camera>();
        }

        void OnPreCull()
        {
            if (cam != null)
            {
                cam.ResetWorldToCameraMatrix();
                cam.ResetProjectionMatrix();
                cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(new Vector3(MirrorEnable ? -1 : 1, 1, 1));
            }
        }

        void OnPreRender()
        {
            GL.invertCulling = MirrorEnable;
        }

        void OnPostRender()
        {
            GL.invertCulling = false;
        }
    }
}