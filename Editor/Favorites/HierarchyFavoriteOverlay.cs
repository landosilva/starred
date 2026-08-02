namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class HierarchyFavoriteOverlay
    {
        static HierarchyFavoriteOverlay()
        {
#if UNITY_6000_0_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnItemGUI;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnItemGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnItemGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnItemGUI;
#endif

            FavoriteAssetsStore.Changed -= EditorApplication.RepaintHierarchyWindow;
            FavoriteAssetsStore.Changed += EditorApplication.RepaintHierarchyWindow;
        }

#if UNITY_6000_0_OR_NEWER
        private static void OnItemGUI(EntityId entityId, Rect selectionRect)
            => DrawOverlay(EditorUtility.EntityIdToObject(entityId), selectionRect);
#else
        private static void OnItemGUI(int instanceId, Rect selectionRect)
            => DrawOverlay(EditorUtility.InstanceIDToObject(instanceId), selectionRect);
#endif

        private static void DrawOverlay(UnityEngine.Object unityObject, Rect selectionRect)
        {
            if (!StarredSettings.ShowHierarchyStar) return;
            if (!FavoriteAssetsStore.HasAnySceneObject) return;

            if (unityObject is not GameObject gameObject) return;

            string globalObjectId = SceneObjectResolver.GetGlobalObjectId(gameObject);
            if (string.IsNullOrEmpty(globalObjectId)) return;
            if (!FavoriteAssetsStore.ContainsSceneObject(globalObjectId)) return;

            FavoriteStarHitTest.DrawAndHandleClick(selectionRect, () => FavoriteAssetsStore.RemoveSceneObject(globalObjectId));
        }
    }
}
