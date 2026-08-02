namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEditor;

    internal static class StarredSettings
    {
        public const string SettingsPath = "Preferences/Starred";

        [MenuItem(StarredText.PreferencesMenuPath, false, 50)]
        public static void OpenPreferences() =>
            SettingsService.OpenUserPreferences(SettingsPath);

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
    }
}
