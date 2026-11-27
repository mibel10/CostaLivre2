using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
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
            try
            {
                if (target == BuildTarget.Android)
                    EditorUserBuildSettings.buildAppBundle = false;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    target = target,
                    locationPathName = output,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"{target} build {report.summary.result}.");
                Debug.Log($"{target} build saved at {output}");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = appBundle;
            }
        }
    }
}
