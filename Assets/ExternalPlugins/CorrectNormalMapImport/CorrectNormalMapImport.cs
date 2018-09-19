using System.Collections.Generic;
using UnityEngine;

namespace Yashinut.VRoid
{
    /// <summary>
    /// VRoid出力のVRMをランタイムで読み込んだときに、NormalMapを修正するクラス
    /// </summary>
    public class CorrectNormalMapImport
    {

        /// <summary>
        /// VRoid用VRMのNormalMapを修正する
        /// </summary>
        /// <param name="vrmForVRoid"></param>
        public static void CorrectNormalMap(GameObject vrmForVRoid)
        {
            var skinnedMeshRenderers = vrmForVRoid.GetComponentsInChildren<SkinnedMeshRenderer>();
            var materials = new List<Material>();

            //vrmデータ内で使用されているマテリアルを全て取得する
            foreach (var skinnedMesh in skinnedMeshRenderers)
            {
                materials.AddRange(skinnedMesh.materials);
            }

            var normalTextures = new List<Texture>();

            //全てのマテリアルからNormalMapに設定されているTextureを取得する。
            foreach (var material in materials)
            {
                //　VRM/MToonシェーダーのNormalMapを取得。
                var tex = material.GetTexture("_BumpMap");
                if (tex == null) continue;
                var defaultNormalMapTexture = ToTexture2D(tex);

                Object.Destroy(tex);
                //修正したNormalMapを取得
                var correctedNormalMapTexture = CorrectNormalMap(defaultNormalMapTexture);
                Object.Destroy(defaultNormalMapTexture);
                // 修正したNormalMapを設定
                material.SetTexture("_BumpMap", correctedNormalMapTexture);
            }
        }

        /// <summary>
        /// TextureをTexture2Dへ変換
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        private static Texture2D ToTexture2D(Texture texture)
        {
            var resultTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            var currentRenderTexture = RenderTexture.active;
            var renderTexture = new RenderTexture(texture.width, texture.height, 32);
            Graphics.Blit(texture, renderTexture);
            RenderTexture.active = renderTexture;
            resultTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            resultTexture.Apply();
            RenderTexture.active = currentRenderTexture;

            return resultTexture;
        }

        /// <summary>
        /// TextureTypeがNormalMapのときと同等になるように修正
        /// </summary>
        /// <param name="defaultNormalTexture"></param>
        /// <returns></returns>
        private static Texture2D CorrectNormalMap(Texture2D defaultNormalTexture)
        {
            var pixels = defaultNormalTexture.GetPixels();

            for (var i = 0; i < pixels.Length; i++)
            {
                //各ピクセルごとにNormalMap用の修正を行う。
                var x = 1f;
                var y = ChangeTextureType.DefaultToNormalMap[(int)(pixels[i].g * 255f)] / 255f;
                var z = ChangeTextureType.DefaultToNormalMap[(int)(pixels[i].g * 255f)] / 255f;
                var w = pixels[i].a;

                pixels[i] = new Color(x, y, z, w);
            }

            var resultTexture = new Texture2D(defaultNormalTexture.width, defaultNormalTexture.height, TextureFormat.RGBA32, false);
            resultTexture.SetPixels(pixels);
            resultTexture.Apply();
            return resultTexture;
        }
    }
}
