namespace Kynesis.Starred.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal static class StarredSettingsProvider
    {
        private static readonly GUIContent[] MaxHistoryLabels =
        {
            new("4"), new("8"), new("16"), new("32"),
        };

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(FavoriteAssetsSettings.SettingsPath, SettingsScope.User)
            {
                label = StarredText.Starred,
                keywords = new HashSet<string> { "starred", "favorite", "favorites", "star", "project", "history", "selection" },
                guiHandler = _ => OnGUI(),
            };
        }

        private static void OnGUI()
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250f;
            try
            {
                DrawSectionHeader(StarredText.Favorites, StarredText.FavoritesMenuHint);
                using (new EditorGUI.IndentLevelScope())
                {
                    FavoriteAssetsSettings.ShowProjectWindowStar = EditorGUILayout.Toggle(
                        new GUIContent(StarredText.ShowStarInProjectWindow, StarredText.ShowStarInProjectWindowTooltip),
                        FavoriteAssetsSettings.ShowProjectWindowStar);

                    FavoriteAssetsSettings.ShowHierarchyStar = EditorGUILayout.Toggle(
                        new GUIContent(StarredText.ShowStarInHierarchy, StarredText.ShowStarInHierarchyTooltip),
                        FavoriteAssetsSettings.ShowHierarchyStar);
                }

                EditorGUILayout.Space(6f);

                DrawSectionHeader(StarredText.History, StarredText.HistoryMenuHint);
                using (new EditorGUI.IndentLevelScope())
                {
                    FavoriteAssetsSettings.MaxHistoryEntries = EditorGUILayout.IntPopup(
                        new GUIContent(StarredText.SelectionHistoryMaxEntries, StarredText.SelectionHistoryMaxEntriesTooltip),
                        FavoriteAssetsSettings.MaxHistoryEntries,
                        MaxHistoryLabels,
                        FavoriteAssetsSettings.MaxHistoryEntriesChoices);
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        private static GUIStyle _pathStyle;

        private static GUIStyle PathStyle
        {
            get
            {
                if (_pathStyle != null) return _pathStyle;
                _pathStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Italic,
                    wordWrap = false,
                };
                return _pathStyle;
            }
        }

        private static void DrawSectionHeader(string title, string menuPath)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(menuPath, PathStyle);
        }
    }
}
