using UnityEngine;

namespace DVRSDK.Utilities
{
    [ExecuteInEditMode]
    public class ShowBones : MonoBehaviour
    {
        [SerializeField]
        private Material _material;

        private void OnRenderObject()
        {
            if (_material)
            {
                _material.SetPass(0);
                GL.PushMatrix();
                GL.MultMatrix(transform.localToWorldMatrix);
                GL.Begin(GL.LINES);
                DrawBones(transform);
                GL.End();
                GL.PopMatrix();
            }
        }

        private static void DrawBones(Transform transform)
        {
            var position = transform.position;
            var childCount = transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                var childPosition = child.position;

                GL.Vertex3(position.x, position.y, position.z);
                GL.Vertex3(childPosition.x, childPosition.y, childPosition.z);
                DrawBones(child);
            }
        }
    }
}
