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

            Scene scene = FindLoadedScene(entry.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return null;

            GameObject atHierarchyPath = FindInScene(scene, entry.HierarchyPath);
            if (!GlobalObjectId.TryParse(entry.GlobalObjectId, out GlobalObjectId target)) return atHierarchyPath;
            if (atHierarchyPath != null && GlobalObjectId.GetGlobalObjectIdSlow(atHierarchyPath).Equals(target))
                return atHierarchyPath;

            GameObject byGlobalObjectId = FindByGlobalObjectId(scene, target);
            return byGlobalObjectId != null ? byGlobalObjectId : atHierarchyPath;
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

        private static GameObject FindByGlobalObjectId(Scene scene, GlobalObjectId target)
        {
            List<GameObject> gameObjects = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    gameObjects.Add(transform.gameObject);
            }

            GlobalObjectId[] identifiers = new GlobalObjectId[gameObjects.Count];
            GlobalObjectId.GetGlobalObjectIdsSlow(gameObjects.ToArray(), identifiers);
            for (int i = 0; i < identifiers.Length; i++)
            {
                if (identifiers[i].Equals(target)) return gameObjects[i];
            }
            return null;
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
