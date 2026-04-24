namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal static class FavoriteAssetsSettings
    {
        public const string SettingsPath = "Preferences/Favorites & History";

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

        private static readonly int[] MaxHistoryChoices = { 4, 8, 16, 32 };
        private static readonly GUIContent[] MaxHistoryLabels =
        {
            new("4"), new("8"), new("16"), new("32"),
        };

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Favorites & History",
                keywords = new HashSet<string> { "favorite", "favorites", "star", "project", "history", "selection" },
                guiHandler = _ => OnGUI(),
            };
        }

        private static void OnGUI()
        {
            // IMGUI inside a SettingsProvider picks up the native Preferences
            // look — label column on the left, controls aligned on the right.
            ShowProjectWindowStar = EditorGUILayout.Toggle(
                new GUIContent("Show star in Project window",
                    "Draws a small gold star on favorited assets in the Project window. Click the star to remove the favorite."),
                ShowProjectWindowStar);

            ShowHierarchyStar = EditorGUILayout.Toggle(
                new GUIContent("Show star in Hierarchy",
                    "Draws a small gold star on favorited GameObjects in the Hierarchy. Click the star to remove the favorite."),
                ShowHierarchyStar);

            EditorGUILayout.Space();

            MaxHistoryEntries = EditorGUILayout.IntPopup(
                new GUIContent("Selection history max entries",
                    "Maximum number of recent selections the Selection History window remembers."),
                MaxHistoryEntries,
                MaxHistoryLabels,
                MaxHistoryChoices);
        }
    }
}
