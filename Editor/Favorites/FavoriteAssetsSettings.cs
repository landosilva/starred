namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal static class FavoriteAssetsSettings
    {
        public const string SettingsPath = "Preferences/Starred";

        private const string ShowProjectWindowStarKey = "FavoriteAssets.ShowProjectWindowStar";
        private const string ShowHierarchyStarKey     = "FavoriteAssets.ShowHierarchyStar";
        private const string MaxHistoryEntriesKey     = "FavoriteAssets.MaxHistoryEntries";

        public const int DefaultMaxHistoryEntries = 16;

        public static event Action Changed;

        public static bool ShowProjectWindowStar
        {
            get => EditorPrefs.GetBool(ShowProjectWindowStarKey, defaultValue: true);
            set
            {
                if (ShowProjectWindowStar == value) return;
                EditorPrefs.SetBool(ShowProjectWindowStarKey, value);
                EditorApplication.RepaintProjectWindow();
                Changed?.Invoke();
            }
        }

        public static bool ShowHierarchyStar
        {
            get => EditorPrefs.GetBool(ShowHierarchyStarKey, defaultValue: true);
            set
            {
                if (ShowHierarchyStar == value) return;
                EditorPrefs.SetBool(ShowHierarchyStarKey, value);
                EditorApplication.RepaintHierarchyWindow();
                Changed?.Invoke();
            }
        }

        public static int MaxHistoryEntries
        {
            get => EditorPrefs.GetInt(MaxHistoryEntriesKey, DefaultMaxHistoryEntries);
            set
            {
                if (MaxHistoryEntries == value) return;
                EditorPrefs.SetInt(MaxHistoryEntriesKey, value);
                Changed?.Invoke();
            }
        }

        public static readonly int[] MaxHistoryEntriesChoices = { 4, 8, 16, 32 };
        private static readonly GUIContent[] MaxHistoryLabels =
        {
            new("4"), new("8"), new("16"), new("32"),
        };

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Starred",
                keywords = new HashSet<string> { "starred", "favorite", "favorites", "star", "project", "history", "selection" },
                guiHandler = _ => OnGUI(),
            };
        }

        private static void OnGUI()
        {
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250f;
            try
            {
                DrawSectionHeader("Favorites", "Tools → Starred → Favorites");
                using (new EditorGUI.IndentLevelScope())
                {
                    ShowProjectWindowStar = EditorGUILayout.Toggle(
                        new GUIContent("Show star in Project window",
                            "Draws a small gold star on favorited assets in the Project window. Click the star to remove the favorite."),
                        ShowProjectWindowStar);

                    ShowHierarchyStar = EditorGUILayout.Toggle(
                        new GUIContent("Show star in Hierarchy",
                            "Draws a small gold star on favorited GameObjects in the Hierarchy. Click the star to remove the favorite."),
                        ShowHierarchyStar);
                }

                EditorGUILayout.Space(6f);

                DrawSectionHeader("History", "Tools → Starred → History");
                using (new EditorGUI.IndentLevelScope())
                {
                    MaxHistoryEntries = EditorGUILayout.IntPopup(
                        new GUIContent("Selection history max entries",
                            "Maximum number of recent selections the Selection History window remembers."),
                        MaxHistoryEntries,
                        MaxHistoryLabels,
                        MaxHistoryEntriesChoices);
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
