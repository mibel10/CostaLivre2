using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CostaLivre.Editor
{
    [InitializeOnLoad]
    internal static class CostaPolishValidationRunner
    {
        private const string Running = "Costa.Polish.Validation";

        static CostaPolishValidationRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Costa Livre/Validação/Polimento")]
        public static void Run()
        {
            string profile = Environment.GetEnvironmentVariable("COSTA_VALIDATION_PROFILE");
            if (string.IsNullOrEmpty(profile) || !Path.GetFullPath(Application.persistentDataPath)
                .StartsWith(Path.GetFullPath(profile) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Use um perfil XDG_CONFIG_HOME isolado e indique-o em COSTA_VALIDATION_PROFILE.");

            const string controller = "Assets/HarborPrototype/Resources/Harbor/DirectionalGait.controller";
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controller) == null)
                CostaLocomotionAuthoring.Rebuild();
            EditorSceneManager.OpenScene("Assets/Scenes/HarborRun.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false))
                return;
            SessionState.SetBool(Running, false);
            var world = UnityEngine.Object.FindAnyObjectByType<HarborRun>();
            if (world == null)
            {
                Finish(1);
                return;
            }
            world.ValidatePolish(Finish);
        }

        private static void Finish(int result)
        {
            if (Application.isBatchMode)
                EditorApplication.delayCall += () => EditorApplication.Exit(result);
            else
                EditorApplication.ExitPlaymode();
        }
    }
}
