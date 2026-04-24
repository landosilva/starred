namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;

    internal static class SelectionHistoryPreferences
    {
        private const string FilePath = "UserSettings/SelectionHistory.json";

        private static readonly List<FavoriteEntry> _entries = new();

        public static IReadOnlyList<FavoriteEntry> Entries => _entries;

        public static event Action Changed;

        static SelectionHistoryPreferences()
        {
            Load();
            FavoriteAssetsSettings.Changed += OnSettingChanged;
        }

        public static void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Save();
            Changed?.Invoke();
        }

        private static void OnSettingChanged()
        {
            if (_entries.Count <= FavoriteAssetsSettings.MaxHistoryEntries) return;
            TrimToMax();
            Save();
            Changed?.Invoke();
        }

        public static void Record(FavoriteEntry entry)
        {
            if (entry == null) return;
            if (!entry.IsAsset && !entry.IsSceneObject) return;

            var key = entry.LookupKey;
            var existingIndex = FindIndex(key);

            // No-op if already at the top.
            if (existingIndex == 0) return;
            if (existingIndex > 0) _entries.RemoveAt(existingIndex);

            _entries.Insert(0, entry);
            TrimToMax();

            Save();
            Changed?.Invoke();
        }

        private static int FindIndex(string key)
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].LookupKey == key) return i;
            return -1;
        }

        private static void TrimToMax()
        {
            var max = FavoriteAssetsSettings.MaxHistoryEntries;
            if (_entries.Count > max) _entries.RemoveRange(max, _entries.Count - max);
        }

        // ---------- Persistence ----------

        private static void Load()
        {
            try
            {
                var raw = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SerializedData>(raw);
                if (data == null) return;

                // v2 (current): typed entries.
                if (data.Entries != null && data.Entries.Count > 0)
                {
                    _entries.Clear();
                    _entries.AddRange(data.Entries);
                    if (_entries.Count > FavoriteAssetsSettings.MaxHistoryEntries) { TrimToMax(); Save(); }
                    return;
                }

                // v1: flat GUID list → wrap as asset entries.
                if (data.Guids != null && data.Guids.Count > 0)
                {
                    _entries.Clear();
                    foreach (var g in data.Guids)
                        if (!string.IsNullOrEmpty(g)) _entries.Add(FavoriteEntry.ForAsset(g));
                    TrimToMax();
                    Save();
                }
            }
            catch (FileNotFoundException)      { /* first run */ }
            catch (DirectoryNotFoundException) { /* first run */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[SelectionHistory] Failed to read preferences: {e.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var data = new SerializedData { Entries = new List<FavoriteEntry>(_entries) };
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SelectionHistory] Failed to save preferences: {e.Message}");
            }
        }

        [Serializable]
        private class SerializedData
        {
            public List<FavoriteEntry> Entries;

            // Legacy v1 field retained for one-way migration.
            public List<string> Guids;
        }
    }
}
