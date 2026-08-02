namespace Kynesis.Starred.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    internal static class SceneObjectResolver
    {
        public static GameObject Find(FavoriteEntry entry)
        {
            if (entry == null || !entry.IsSceneObject) return null;
            if (!IsSceneAvailable(entry.ScenePath)) return null;

            GameObject viaId = TryResolveByGlobalId(entry);
            if (viaId != null) return viaId;

            return TryResolveByHierarchyWalk(entry);
        }

        public static bool IsSceneAvailable(string scenePath)
        {
            Scene scene = FindLoadedScene(scenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        public static string GetGlobalObjectId(GameObject gameObject) =>
            gameObject == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();

        public static string GetScenePath(GameObject gameObject)
        {
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
            return stage != null ? stage.assetPath : gameObject.scene.path;
        }

        public static string GetHierarchyPath(GameObject gameObject)
        {
            List<string> parts = new List<string>();
            for (Transform transform = gameObject.transform; transform != null; transform = transform.parent) parts.Add(transform.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static FavoriteEntry BuildEntry(GameObject gameObject)
        {
            if (gameObject == null) return null;
            return FavoriteEntry.ForSceneObject(
                GetGlobalObjectId(gameObject),
                GetScenePath(gameObject),
                GetHierarchyPath(gameObject));
        }

        private static GameObject TryResolveByGlobalId(FavoriteEntry entry)
        {
            if (!GlobalObjectId.TryParse(entry.GlobalObjectId, out GlobalObjectId id)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
        }

        private static GameObject TryResolveByHierarchyWalk(FavoriteEntry entry)
        {
            Scene scene = FindLoadedScene(entry.ScenePath);
            return scene.IsValid() && scene.isLoaded ? FindInScene(scene, entry.HierarchyPath) : null;
        }

        private static Scene FindLoadedScene(string scenePath)
        {
            bool wantsPrefab = string.Equals(System.IO.Path.GetExtension(scenePath), ".prefab",
                System.StringComparison.OrdinalIgnoreCase);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                return FindPrefabStageScene(scenePath, wantsPrefab);

            if (wantsPrefab) return default;
            return FindOpenScene(scenePath);
        }

        private static Scene FindPrefabStageScene(string scenePath, bool wantsPrefab)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && wantsPrefab && stage.assetPath == scenePath) return stage.scene;
            return default;
        }

        private static Scene FindOpenScene(string scenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path == scenePath && scene.isLoaded) return scene;
            }
            return default;
        }

        private static GameObject FindInScene(Scene scene, string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath)) return null;

            string[] parts = hierarchyPath.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;

                Transform current = root.transform;
                for (int i = 1; i < parts.Length; i++)
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
