namespace Kynesis.Starred.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Resolves scene/prefab-stage GameObjects by (scenePath, hierarchyPath).
    /// ScenePath points to a <c>.unity</c> file for regular scenes, or to a
    /// <c>.prefab</c> file for objects favorited while inside a Prefab Stage.
    /// </summary>
    internal static class SceneObjectResolver
    {
        public static GameObject Find(string scenePath, string hierarchyPath)
        {
            var scene = FindLoadedScene(scenePath);
            return scene.IsValid() && scene.isLoaded ? FindInScene(scene, hierarchyPath) : null;
        }

        public static bool IsSceneAvailable(string scenePath)
        {
            var scene = FindLoadedScene(scenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        public static string GetScenePath(GameObject go)
        {
            var stage = PrefabStageUtility.GetPrefabStage(go);
            return stage != null ? stage.assetPath : go.scene.path;
        }

        public static string GetHierarchyPath(GameObject go)
        {
            var parts = new List<string>();
            for (var t = go.transform; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Scene FindLoadedScene(string scenePath)
        {
            // Contexts are mutually exclusive: while a Prefab Stage is open,
            // regular-scene entries are hidden, and vice versa. This matches
            // the user's mental model of "what am I currently editing".
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var wantsPrefab = scenePath != null && scenePath.EndsWith(".prefab");

            if (stage != null)
            {
                if (wantsPrefab && stage.assetPath == scenePath) return stage.scene;
                return default;
            }

            if (wantsPrefab) return default;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == scenePath && s.isLoaded) return s;
            }
            return default;
        }

        private static GameObject FindInScene(Scene scene, string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath)) return null;

            var parts = hierarchyPath.Split('/');
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;

                var current = root.transform;
                for (var i = 1; i < parts.Length; i++)
                {
                    current = current.Find(parts[i]);
                    if (current == null) return null;
                }
                return current.gameObject;
            }
            return null;
        }
    }
}
