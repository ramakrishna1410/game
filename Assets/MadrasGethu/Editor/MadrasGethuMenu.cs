using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MadrasGethu.EditorTools
{
    /// <summary>Adds "Madras Gethu" to the Unity menu bar.</summary>
    public static class MadrasGethuMenu
    {
        const string SceneFolder = "Assets/MadrasGethu/Scenes";
        const string ScenePath = SceneFolder + "/KasimeduArena.unity";
        public const string PracticeScenePath = SceneFolder + "/PracticeYard.unity";

        [MenuItem("Madras Gethu/Create Kasimedu Arena Scene", priority = 0)]
        public static void CreateArenaScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureSceneFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Game Bootstrap");
            bootstrap.AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Selection.activeGameObject = bootstrap;
            EditorUtility.DisplayDialog("Madras Gethu",
                "Kasimedu Arena scene created at\n" + ScenePath + "\n\nPress Play to fight!", "Vaa, poalaam!");
        }

        [MenuItem("Madras Gethu/Open Kasimedu Arena Scene", priority = 1)]
        public static void OpenArenaScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                CreateArenaScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Madras Gethu/Create Practice Yard Scene", priority = 2)]
        public static void CreatePracticeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureSceneFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Game Bootstrap (Practice)");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.practiceMode = true;
            EditorSceneManager.SaveScene(scene, PracticeScenePath);

            // Use the same characters as the arena (set by Setup Characters), so the dummy isn't a block.
            bool copied = CopyCharactersFromArena(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PracticeScenePath);
            AddSceneToBuildSettings(PracticeScenePath);

            Selection.activeGameObject = go;
            EditorUtility.DisplayDialog("Madras Gethu",
                "Practice Yard created at\n" + PracticeScenePath +
                (copied ? "\n\nYour characters were copied from the arena scene." : "\n\n(No characters set up yet: placeholder bodies are used.)") +
                "\n\nPress Play.  R = reset dummy · T = switch dummy · G = infinite Gethu on/off", "Practice pannalaam!");
        }

        [MenuItem("Madras Gethu/Open Practice Yard Scene", priority = 3)]
        public static void OpenPracticeScene()
        {
            if (!System.IO.File.Exists(PracticeScenePath))
            {
                CreatePracticeScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PracticeScenePath);
        }

        /// <summary>Copies the character models and animator from the arena scene's bootstrap.</summary>
        static bool CopyCharactersFromArena(GameBootstrap target)
        {
            if (!System.IO.File.Exists(ScenePath)) return false;
            var arena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            GameBootstrap source = null;
            foreach (var root in arena.GetRootGameObjects())
            {
                source = root.GetComponentInChildren<GameBootstrap>(true);
                if (source != null) break;
            }
            bool found = source != null && source.playerModelPrefab != null;
            if (source != null)
            {
                target.playerModelPrefab = source.playerModelPrefab;
                target.enemyModelPrefab = source.enemyModelPrefab;
                target.thadiyanModelPrefab = source.thadiyanModelPrefab;
                target.bossModelPrefab = source.bossModelPrefab;
                target.fighterAnimator = source.fighterAnimator;
                EditorUtility.SetDirty(target);
            }
            EditorSceneManager.CloseScene(arena, true);
            return found;
        }

        static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/MadrasGethu", "Scenes");
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == path) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(path, true),
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
