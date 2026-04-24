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
#if UNITY_6000_0_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnItemGUI;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnItemGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnItemGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnItemGUI;
#endif

            FavoriteAssetsPreferences.Changed -= EditorApplication.RepaintHierarchyWindow;
            FavoriteAssetsPreferences.Changed += EditorApplication.RepaintHierarchyWindow;
        }

#if UNITY_6000_0_OR_NEWER
        private static void OnItemGUI(EntityId entityId, Rect selectionRect)
            => DrawOverlay(EditorUtility.EntityIdToObject(entityId), selectionRect);
#else
        private static void OnItemGUI(int instanceId, Rect selectionRect)
            => DrawOverlay(EditorUtility.InstanceIDToObject(instanceId), selectionRect);
#endif

        private static void DrawOverlay(UnityEngine.Object obj, Rect selectionRect)
        {
            if (!FavoriteAssetsSettings.ShowHierarchyStar) return;
            if (!FavoriteAssetsPreferences.HasAnySceneObject) return;

            if (obj is not GameObject go) return;

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
