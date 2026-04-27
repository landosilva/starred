namespace Kynesis.Starred.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Resolves scene / prefab-stage GameObjects for favorite entries.
    /// Identity is keyed on Unity's <see cref="GlobalObjectId"/> so favorites
    /// survive rename and reparent; <see cref="FavoriteEntry.HierarchyPath"/>
    /// is a fallback when the ID can't be resolved (and the source of the
    /// row's display label).
    /// </summary>
    internal static class SceneObjectResolver
    {
        public static GameObject Find(FavoriteEntry entry)
        {
            if (entry == null || !entry.IsSceneObject) return null;
            if (!IsSceneAvailable(entry.ScenePath)) return null;

            // Primary: GlobalObjectId. Stable across rename / reparent / scene
            // reload (for saved scenes).
            if (GlobalObjectId.TryParse(entry.GlobalObjectId, out var id))
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (obj is GameObject viaId) return viaId;
            }

            // Fallback: walk the hierarchy by path. Useful for transient or
            // unsaved scenes where the GlobalObjectId might not have stuck.
            var scene = FindLoadedScene(entry.ScenePath);
            return scene.IsValid() && scene.isLoaded ? FindInScene(scene, entry.HierarchyPath) : null;
        }

        public static bool IsSceneAvailable(string scenePath)
        {
            var scene = FindLoadedScene(scenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        public static string GetGlobalObjectId(GameObject go) =>
            go == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();

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

        public static FavoriteEntry BuildEntry(GameObject go)
        {
            if (go == null) return null;
            return FavoriteEntry.ForSceneObject(
                GetGlobalObjectId(go),
                GetScenePath(go),
                GetHierarchyPath(go));
        }

        private static Scene FindLoadedScene(string scenePath)
        {
            // Contexts are mutually exclusive: while a Prefab Stage is open,
            // regular-scene entries are hidden, and vice versa. This matches
            // the user's mental model of "what am I currently editing".
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var wantsPrefab = string.Equals(System.IO.Path.GetExtension(scenePath), ".prefab",
                System.StringComparison.OrdinalIgnoreCase);

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
