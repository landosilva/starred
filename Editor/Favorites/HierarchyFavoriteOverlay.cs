namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class HierarchyFavoriteOverlay
    {
        static HierarchyFavoriteOverlay()
        {
            // Domain reload re-runs this ctor; guard against double-registration.
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnItemGUI;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnItemGUI;

            FavoriteAssetsPreferences.Changed -= EditorApplication.RepaintHierarchyWindow;
            FavoriteAssetsPreferences.Changed += EditorApplication.RepaintHierarchyWindow;
        }

        private static void OnItemGUI(EntityId entityId, Rect selectionRect)
        {
            if (!FavoriteAssetsSettings.ShowHierarchyStar) return;
            if (!FavoriteAssetsPreferences.HasAnySceneObject) return;

            if (EditorUtility.EntityIdToObject(entityId) is not GameObject go) return;

            var scenePath = SceneObjectResolver.GetScenePath(go);
            if (string.IsNullOrEmpty(scenePath)) return;

            var hierarchyPath = SceneObjectResolver.GetHierarchyPath(go);
            if (!FavoriteAssetsPreferences.ContainsSceneObject(scenePath, hierarchyPath)) return;

            var starRect = FavoriteStarDrawer.Draw(selectionRect);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && starRect.Contains(e.mousePosition))
            {
                FavoriteAssetsPreferences.RemoveSceneObject(scenePath, hierarchyPath);
                e.Use();
            }
        }
    }
}
