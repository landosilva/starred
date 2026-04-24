namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class ProjectWindowFavoriteOverlay
    {
        static ProjectWindowFavoriteOverlay()
        {
            // Domain reload re-runs this ctor; guard against double-registration.
            EditorApplication.projectWindowItemOnGUI -= OnItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnItemGUI;

            FavoriteAssetsPreferences.Changed -= EditorApplication.RepaintProjectWindow;
            FavoriteAssetsPreferences.Changed += EditorApplication.RepaintProjectWindow;
        }

        private static void OnItemGUI(string guid, Rect selectionRect)
        {
            if (string.IsNullOrEmpty(guid)) return;
            if (!FavoriteAssetsSettings.ShowProjectWindowStar) return;
            if (!FavoriteAssetsPreferences.Contains(guid)) return;

            var starRect = FavoriteStarDrawer.Draw(selectionRect);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && starRect.Contains(e.mousePosition))
            {
                FavoriteAssetsPreferences.Remove(guid);
                e.Use();
            }
        }
    }
}
