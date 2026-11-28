using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

namespace CostaLivre.Editor
{
    public static class CostaPlatformBuild
    {
        [MenuItem("Costa Livre/Build/Android APK")]
        public static void Android() => Build(BuildTarget.Android, "Builds/Android/CostaLivre2.apk");

        [MenuItem("Costa Livre/Build/Windows (x64)")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CostaLivre2.exe");

        [MenuItem("Costa Livre/Build/Linux (x64)")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/CostaLivre2.x86_64");

        private static void Build(BuildTarget target, string defaultOutput)
        {
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new InvalidOperationException($"Unity build support for {target} is unavailable.");

            string output = Environment.GetEnvironmentVariable("COSTA_BUILD_OUTPUT");
            output = Path.GetFullPath(string.IsNullOrWhiteSpace(output) ? defaultOutput : output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            bool appBundle = EditorUserBuildSettings.buildAppBundle;
            StripCrossCompiledStp = target == BuildTarget.StandaloneWindows64 && Application.platform != RuntimePlatform.WindowsEditor;
            try
            {
                if (target == BuildTarget.Android)
                    EditorUserBuildSettings.buildAppBundle = false;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    target = target,
                    locationPathName = output,
                    options = BuildOptions.None,
                    extraScriptingDefines = StripCrossCompiledStp
                        ? new[] { "COSTA_STP_NO_DIRECT3D" } : Array.Empty<string>()
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"{target} build {report.summary.result}.");
                Debug.Log($"{target} build saved at {output}");
            }
            finally
            {
                StripCrossCompiledStp = false;
                EditorUserBuildSettings.buildAppBundle = appBundle;
            }
        }

        internal static bool StripCrossCompiledStp { get; private set; }
    }

    internal sealed class CostaCrossBuildStp : IPreprocessComputeShaders
    {
        public int callbackOrder => -100;

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> variants)
        {
            if (!CostaPlatformBuild.StripCrossCompiledStp ||
                shader.name is not ("StpSetup" or "StpPreTaa" or "StpTaa") ||
                !AssetDatabase.GetAssetPath(shader).StartsWith("Packages/com.unity.render-pipelines.core/Runtime/STP/", StringComparison.Ordinal))
                return;

            for (int i = variants.Count - 1; i >= 0; i--)
                if (variants[i].shaderCompilerPlatform == ShaderCompilerPlatform.D3D)
                    variants.RemoveAt(i);
        }
    }
}
