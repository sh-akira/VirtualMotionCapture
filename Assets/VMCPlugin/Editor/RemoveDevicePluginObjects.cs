using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VMC.Plugin.EditorTools
{
    /// <summary>
    /// mocopi / VIVE / Tobii が本体機能だった頃のシーン上のGameObjectを取り除く。
    /// プラグイン化に伴い、これらは実行時にプラグインが自分で生成するようになったため
    /// シーンには不要(スクリプトを消したままだと Missing Script が残る)。
    ///
    /// 一度実行すれば済む片付け用。移行後は使わない。
    /// </summary>
    public static class RemoveDevicePluginObjects
    {
        private static readonly string[] TargetNames = { "mocopiConnector", "EyeTracking", "LipTracking" };

        [MenuItem("VMC/Plugin/シーンから旧デバイス機能のGameObjectを削除")]
        public static void Execute()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var removed = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (transform == null) continue;
                    if (TargetNames.Contains(transform.gameObject.name) == false) continue;

                    Debug.Log($"[Plugin] シーンから削除: {transform.gameObject.name}");
                    Object.DestroyImmediate(transform.gameObject);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[Plugin] {removed} 個のGameObjectを削除しました");
        }

        /// <summary>batchmode から -executeMethod で呼ぶ用</summary>
        public static void ExecuteBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/VirtualMotionCapture.unity");
            Execute();
        }
    }
}
