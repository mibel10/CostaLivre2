using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CostaLivre.Editor
{
    public static class CostaUwpBuild
    {
        public static void BuildUwp()
        {
            BuildTarget target = BuildTarget.WSAPlayer;
            BuildTargetGroup group = BuildTargetGroup.WSA;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new InvalidOperationException("Unity UWP (WSAPlayer) build support is unavailable.");

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                if (!switched)
                    throw new InvalidOperationException("Failed to switch active build target to WSAPlayer.");
            }

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WindowsStoreApps, ScriptingImplementation.IL2CPP);
            PlayerSettings.WSA.packageName = "CostaLivre2";
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClient, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClientServer, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, true);

            EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;
            EditorUserBuildSettings.wsaSubtarget = WSASubtarget.AnyDevice;
            EditorUserBuildSettings.wsaArchitecture = "x64";

            string output = Environment.GetEnvironmentVariable("COSTA_UWP_OUTPUT");
            output = Path.GetFullPath(string.IsNullOrWhiteSpace(output) ? "Builds/UWP" : output);
            Directory.CreateDirectory(output);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            var savedProps = new List<(SerializedObject Obj, string PropName, int IntValue, bool BoolValue, bool IsBool)>();
            try
            {
                void SetIntProp(SerializedObject so, string propName, int newValue)
                {
                    var prop = so.FindProperty(propName);
                    if (prop != null && prop.intValue != newValue)
                    {
                        savedProps.Add((so, propName, prop.intValue, false, false));
                        prop.intValue = newValue;
                    }
                }

                void SetBoolProp(SerializedObject so, string propName, bool newValue)
                {
                    var prop = so.FindProperty(propName);
                    if (prop != null && prop.boolValue != newValue)
                    {
                        savedProps.Add((so, propName, 0, prop.boolValue, true));
                        prop.boolValue = newValue;
                    }
                }

                var touchedObjects = new List<SerializedObject>();
                foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null)
                        continue;
                    var so = new SerializedObject(asset);
                    SetIntProp(so, "m_GPUResidentDrawerMode", 0);
                    SetBoolProp(so, "m_GPUResidentDrawerEnableOcclusionCullingInCameras", false);
                    SetBoolProp(so, "m_UseSRPBatcher", false);
                    SetBoolProp(so, "m_EnableLODCrossFade", false);
                    if (so.ApplyModifiedPropertiesWithoutUndo())
                    {
                        EditorUtility.SetDirty(asset);
                        touchedObjects.Add(so);
                    }
                }

                foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
                {
                    var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (renderer == null)
                        continue;
                    var so = new SerializedObject(renderer);
                    SetIntProp(so, "m_RenderingMode", 0);
                    SetBoolProp(so, "m_UseNativeRenderPass", false);
                    if (so.ApplyModifiedPropertiesWithoutUndo())
                    {
                        EditorUtility.SetDirty(renderer);
                        touchedObjects.Add(so);
                    }
                }

                if (touchedObjects.Count > 0)
                    AssetDatabase.SaveAssets();

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    target = target,
                    targetGroup = group,
                    locationPathName = output,
                    options = BuildOptions.None
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"UWP build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

                Debug.Log($"UWP Visual Studio project generated at {output}");
            }
            finally
            {
                if (savedProps.Count > 0)
                {
                    var restoredObjects = new HashSet<SerializedObject>();
                    foreach (var entry in savedProps)
                    {
                        var prop = entry.Obj.FindProperty(entry.PropName);
                        if (prop != null)
                        {
                            if (entry.IsBool)
                                prop.boolValue = entry.BoolValue;
                            else
                                prop.intValue = entry.IntValue;
                            restoredObjects.Add(entry.Obj);
                        }
                    }
                    foreach (var so in restoredObjects)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(so.targetObject);
                    }
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
