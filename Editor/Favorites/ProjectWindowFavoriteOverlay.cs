namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class ProjectWindowFavoriteOverlay
    {
        static ProjectWindowFavoriteOverlay()
        {
            EditorApplication.projectWindowItemOnGUI -= OnItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnItemGUI;

            FavoriteAssetsStore.Changed -= EditorApplication.RepaintProjectWindow;
            FavoriteAssetsStore.Changed += EditorApplication.RepaintProjectWindow;
        }

        private static void OnItemGUI(string guid, Rect selectionRect)
        {
            if (string.IsNullOrEmpty(guid)) return;
            if (!FavoriteAssetsSettings.ShowProjectWindowStar) return;
            if (!FavoriteAssetsStore.Contains(guid)) return;

            FavoriteStarHitTest.DrawAndHandleClick(selectionRect, () => FavoriteAssetsStore.Remove(guid));
        }
    }
}
