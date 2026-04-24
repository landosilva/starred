namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class SelectionHistoryTracker
    {
        static SelectionHistoryTracker()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            var active = Selection.activeObject;
            if (active == null) return;

            var assetPath = AssetDatabase.GetAssetPath(active);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                    SelectionHistoryPreferences.Record(FavoriteEntry.ForAsset(guid));
                return;
            }

            if (active is GameObject go)
            {
                var scenePath = SceneObjectResolver.GetScenePath(go);
                if (string.IsNullOrEmpty(scenePath)) return; // unsaved scene — skip
                SelectionHistoryPreferences.Record(
                    FavoriteEntry.ForSceneObject(scenePath, SceneObjectResolver.GetHierarchyPath(go)));
            }
        }
    }
}
