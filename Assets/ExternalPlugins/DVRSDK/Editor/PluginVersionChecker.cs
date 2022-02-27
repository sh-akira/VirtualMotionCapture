#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DVRSDK.Editor
{
    [InitializeOnLoad]
    public class PluginVersionChecker : AssetPostprocessor
    {
        static PluginVersionChecker()
        {
            EditorApplication.wantsToQuit += Quit;
            UpdateDefineSymbols();
        }

        private static List<string> InitializeSymbols()
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';').ToList();

            return symbols;
        }

        private static void UpdateDefineSymbols()
        {
            var symbols = InitializeSymbols();
            var version = GetVRMVersion();

            // Importer
            symbols.Remove("UNIVRM_0_68_IMPORTER");
            symbols.Remove("UNIVRM_0_77_IMPORTER");
            symbols.Remove("UNIVRM_LEGACY_IMPORTER");
            if (version.Value.major == 0 && version.Value.minor < 68)
            {
                symbols.Add("UNIVRM_LEGACY_IMPORTER");
            }
            else if (version.Value.major == 0 && version.Value.minor < 77)
            {
                symbols.Add("UNIVRM_0_68_IMPORTER");
            }
            else
            {
                symbols.Add("UNIVRM_0_77_IMPORTER");
            }

            // Exporter
            symbols.Remove("UNIVRM_0_71_EXPORTER");
            symbols.Remove("UNIVRM_0_75_EXPORTER");
            symbols.Remove("UNIVRM_0_79_EXPORTER");
            symbols.Remove("UNIVRM_LEGACY_EXPORTER");
            if (version.Value.major == 0 && version.Value.minor < 71)
            {
                symbols.Add("UNIVRM_LEGACY_EXPORTER");
            }
            else if (version.Value.major == 0 && version.Value.minor < 75)
            {
                symbols.Add("UNIVRM_0_71_EXPORTER");
            }
            else if (version.Value.major == 0 && version.Value.minor < 79)
            {
                symbols.Add("UNIVRM_0_75_EXPORTER");
            }
            else
            {
                symbols.Add("UNIVRM_0_79_EXPORTER");
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", symbols));

            EditorApplication.UnlockReloadAssemblies();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (str.Contains("VRMImporter"))
                {
                    UpdateDefineSymbols();
                }
            }
        }

        private static (int major, int minor, int patch)? GetVRMVersion()
        {
            var vrmVersionType = Type.GetType("VRM.VRMVersion");
            if (vrmVersionType == null) vrmVersionType = Type.GetType("VRM.VRMVersion, VRM");

            if (vrmVersionType != null)
            {
                int major = GetPublicConstantValue<int>(vrmVersionType, "MAJOR");
                int minor = GetPublicConstantValue<int>(vrmVersionType, "MINOR");
                int patch = GetPublicConstantValue<int>(vrmVersionType, "PATCH");

                return (major, minor, patch);
            }
            else
            {
                return null;
            }
        }

        private static T GetPublicConstantValue<T>(Type type, string constantName)
        {
            return (T)type.GetField(constantName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetRawConstantValue();
        }

        static bool Quit()
        {
            var symbols = InitializeSymbols();
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", symbols));

            return true;
        }
    }
}
#endif
