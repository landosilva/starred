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
            StarredSettings.Changed += OnSettingChanged;
        }

        public static void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Commit();
        }

        private static void OnSettingChanged()
        {
            if (_entries.Count <= StarredSettings.MaxHistoryEntries) return;
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
            int max = StarredSettings.MaxHistoryEntries;
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
            HashSet<string> seen = new HashSet<string>();
            bool cleaned = false;
            foreach (FavoriteEntry entry in entries)
            {
                if (entry == null || (!entry.IsAsset && !entry.IsSceneObject))
                {
                    cleaned = true;
                    continue;
                }
                if (!seen.Add(entry.LookupKey))
                {
                    cleaned = true;
                    continue;
                }
                _entries.Add(entry);
            }

            bool namesFilled = false;
            foreach (FavoriteEntry entry in _entries)
            {
                string previous = entry.DisplayName;
                EntryDisplayName.Capture(entry);
                if (entry.DisplayName != previous) namesFilled = true;
            }

            bool trimmed = _entries.Count > StarredSettings.MaxHistoryEntries;
            TrimToMax();
            if (needsRewrite || cleaned || namesFilled || trimmed) Save();
        }

        private static void Save()
        {
            EditorJsonFile.Save(FilePath, new SelectionHistoryMigration.SerializedData { Entries = new List<FavoriteEntry>(_entries) });
        }
    }
}
