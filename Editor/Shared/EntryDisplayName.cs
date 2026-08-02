namespace Kynesis.Starred.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    internal static class EntryDisplayName
    {
        public static void Capture(FavoriteEntry entry)
        {
            if (entry == null) return;

            if (entry.IsAsset)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
                UnityEngine.Object asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                {
                    entry.DisplayName = asset.name;
                    return;
                }
                if (string.IsNullOrEmpty(entry.DisplayName) && !string.IsNullOrEmpty(path))
                    entry.DisplayName = Path.GetFileNameWithoutExtension(path);
                return;
            }

            if (entry.IsSceneObject)
            {
                GameObject gameObject = SceneObjectResolver.Find(entry);
                if (gameObject != null)
                {
                    entry.DisplayName = gameObject.name;
                    return;
                }
                if (string.IsNullOrEmpty(entry.DisplayName))
                    entry.DisplayName = FavoriteEntry.LastSegment(entry.HierarchyPath);
            }
        }

        public static string Resolve(FavoriteEntry entry, UnityEngine.Object liveObject, string assetPath)
        {
            if (liveObject != null) return liveObject.name;

            string name = entry?.DisplayName;
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(assetPath))
                name = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(entry?.HierarchyPath))
                name = FavoriteEntry.LastSegment(entry.HierarchyPath);
            if (string.IsNullOrEmpty(name)) name = StarredText.Unknown;

            return $"{name}{StarredText.DeletedSuffix}";
        }
    }
}
