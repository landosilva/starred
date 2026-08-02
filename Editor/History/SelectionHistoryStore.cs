namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;

    internal static class SelectionHistoryStore
    {
        private const string FilePath = StarredPaths.HistoryFile;

        private static readonly List<FavoriteEntry> _entries = new();

        public static IReadOnlyList<FavoriteEntry> Entries => _entries;

        public static event Action Changed;

        static SelectionHistoryStore()
        {
            Load();
            FavoriteAssetsSettings.Changed += OnSettingChanged;
        }

        public static void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Commit();
        }

        private static void OnSettingChanged()
        {
            if (_entries.Count <= FavoriteAssetsSettings.MaxHistoryEntries) return;
            TrimToMax();
            Commit();
        }

        public static void Record(FavoriteEntry entry)
        {
            if (entry == null) return;
            if (!entry.IsAsset && !entry.IsSceneObject) return;

            string key = entry.LookupKey;
            int existingIndex = FindIndex(key);

            if (existingIndex == 0) return;
            if (existingIndex > 0) _entries.RemoveAt(existingIndex);

            EntryDisplayName.Capture(entry);
            _entries.Insert(0, entry);
            TrimToMax();

            Commit();
        }

        private static int FindIndex(string key)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].LookupKey == key) return i;
            return -1;
        }

        private static void TrimToMax()
        {
            int max = FavoriteAssetsSettings.MaxHistoryEntries;
            if (_entries.Count > max) _entries.RemoveRange(max, _entries.Count - max);
        }

        private static void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (!EditorJsonFile.TryLoad(FilePath, out SelectionHistoryMigration.SerializedData data)) return;
            List<FavoriteEntry> entries = SelectionHistoryMigration.ToCurrentEntries(data, out bool needsRewrite);
            if (entries == null) return;
            _entries.Clear();
            _entries.AddRange(entries);
            bool trimmed = _entries.Count > FavoriteAssetsSettings.MaxHistoryEntries;
            TrimToMax();
            if (needsRewrite || trimmed) Save();
        }

        private static void Save()
        {
            EditorJsonFile.Save(FilePath, new SelectionHistoryMigration.SerializedData { Entries = new List<FavoriteEntry>(_entries) });
        }
    }
}
