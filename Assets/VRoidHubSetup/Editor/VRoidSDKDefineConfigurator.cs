using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace VMC.EditorTools
{
    /// <summary>
    /// VRoid SDK(Assets/VRoidSDK)の有無を検出し、スクリプト定義シンボル VMC_VROIDSDK を自動でON/OFFする。
    ///
    /// - SDKあり  → VMC_VROIDSDK 定義 → VRoidSDKConnector 等が有効
    /// - SDKなし  → VMC_VROIDSDK 未定義 → SDK依存コードが除外されビルドが通る
    ///
    /// このアセンブリはSDLにもAssembly-CSharpにも依存しない独立Editorアセンブリ(VMC.VRoidSDKSetup.Editor.asmdef)に
    /// 置いてあるため、Assembly-CSharpがSDK欠如で一時的にコンパイルエラーになっても本スクリプトは動作し、
    /// 定義を修正して自己修復できる。
    /// </summary>
    [InitializeOnLoad]
    public static class VRoidSDKDefineConfigurator
    {
        private const string Define = "VMC_VROIDSDK";
        // SDK同梱の目印となるDLL(再配布しないSDK本体の一部)
        private const string MarkerRelativePath = "VRoidSDK/Bin/Pixiv.VroidSdk.dll";

        static VRoidSDKDefineConfigurator()
        {
            var present = File.Exists(Path.Combine(Application.dataPath, MarkerRelativePath));
            // このプロジェクトはWindowsスタンドアロン。念のため現在選択中グループも合わせて更新する。
            Apply(NamedBuildTarget.Standalone, present);
            var selected = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (selected != BuildTargetGroup.Standalone && selected != BuildTargetGroup.Unknown)
            {
                Apply(NamedBuildTarget.FromBuildTargetGroup(selected), present);
            }
        }

        private static void Apply(NamedBuildTarget target, bool present)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var has = defines.Contains(Define);
            if (present == has) return; // 変更不要
            if (present) defines.Add(Define);
            else defines.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            Debug.Log($"[VRoidSDK] {(present ? "detected" : "not found")}. Scripting define '{Define}' -> {(present ? "ON" : "OFF")} ({target.TargetName}).");
        }
    }
}
