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

        [MenuItem("Madras Gethu/Create Kasimedu Arena Scene", priority = 0)]
        public static void CreateArenaScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/MadrasGethu", "Scenes");

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
