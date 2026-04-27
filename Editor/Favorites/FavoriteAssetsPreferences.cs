namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;

    internal static class FavoriteAssetsPreferences
    {
        private const string FilePath = "UserSettings/FavoriteAssets.json";

        private static readonly List<FavoriteEntry> _entries = new();
        private static readonly HashSet<string> _lookup      = new(); // LookupKey de-dup
        private static readonly HashSet<string> _assetGuids  = new(); // fast path for asset checks

        public static IReadOnlyList<FavoriteEntry> Entries => _entries;

        public static bool HasAnySceneObject { get; private set; }

        public static event Action Changed;

        static FavoriteAssetsPreferences() => Load();

        // ---------- Queries ----------

        public static bool Contains(string guid) =>
            !string.IsNullOrEmpty(guid) && _assetGuids.Contains(guid);

        public static bool ContainsSceneObject(string globalObjectId) =>
            !string.IsNullOrEmpty(globalObjectId) && _lookup.Contains($"s:{globalObjectId}");

        public static bool Contains(FavoriteEntry entry)
        {
            if (entry == null) return false;
            if (entry.IsAsset) return Contains(entry.Guid);
            if (entry.IsSceneObject) return ContainsSceneObject(entry.GlobalObjectId);
            return false;
        }

        public static void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            _lookup.Clear();
            _assetGuids.Clear();
            HasAnySceneObject = false;
            Commit();
        }

        public static void Toggle(FavoriteEntry entry)
        {
            if (entry == null) return;
            if (Contains(entry))
            {
                if (entry.IsAsset) Remove(entry.Guid);
                else RemoveSceneObject(entry.GlobalObjectId);
            }
            else
            {
                if (entry.IsAsset) Add(entry.Guid);
                else if (TryAdd(entry)) Commit();
            }
        }

        // ---------- Mutations ----------

        public static void Add(string guid)
        {
            if (TryAdd(FavoriteEntry.ForAsset(guid))) Commit();
        }

        public static void AddRange(IEnumerable<FavoriteEntry> entries)
        {
            var changed = false;
            foreach (var e in entries)
                if (TryAdd(e)) changed = true;
            if (changed) Commit();
        }

        public static void Remove(string guid)
        {
            if (!_assetGuids.Contains(guid)) return;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].IsAsset && _entries[i].Guid == guid)
                {
                    RemoveAt(i);
                    Commit();
                    return;
                }
            }
        }

        public static void RemoveEntry(FavoriteEntry entry)
        {
            var index = _entries.IndexOf(entry);
            if (index < 0) return;
            RemoveAt(index);
            Commit();
        }

        public static void RemoveSceneObject(string globalObjectId)
        {
            if (string.IsNullOrEmpty(globalObjectId)) return;
            var key = $"s:{globalObjectId}";
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].LookupKey == key)
                {
                    RemoveAt(i);
                    Commit();
                    return;
                }
            }
        }

        public static void Move(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _entries.Count) return;

            var entry = _entries[fromIndex];
            toIndex = ClampToKindRange(entry.IsSceneObject, toIndex);

            var normalized = toIndex > fromIndex ? toIndex - 1 : toIndex;
            if (normalized == fromIndex) return;

            _entries.RemoveAt(fromIndex);
            _entries.Insert(normalized, entry);
            Commit();
        }

        private static int ClampToKindRange(bool isSceneObject, int toIndex)
        {
            var firstAsset = FirstAssetIndex();
            return isSceneObject
                ? Mathf.Clamp(toIndex, 0, firstAsset)
                : Mathf.Clamp(toIndex, firstAsset, _entries.Count);
        }

        private static int FirstAssetIndex()
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].IsAsset) return i;
            return _entries.Count;
        }

        private static bool TryAdd(FavoriteEntry entry)
        {
            if (entry == null) return false;
            if (!entry.IsAsset && !entry.IsSceneObject) return false;
            if (!_lookup.Add(entry.LookupKey)) return false;

            // Preserve the kind-sorted invariant: scene objects first (contextual
            // favorites float to the top), asset favorites after.
            if (entry.IsSceneObject)
            {
                _entries.Insert(FirstAssetIndex(), entry);
                HasAnySceneObject = true;
            }
            else
            {
                _entries.Add(entry);
                _assetGuids.Add(entry.Guid);
            }
            return true;
        }

        private static void RemoveAt(int index)
        {
            var entry = _entries[index];
            _entries.RemoveAt(index);
            _lookup.Remove(entry.LookupKey);
            if (entry.IsAsset) _assetGuids.Remove(entry.Guid);
            if (entry.IsSceneObject) HasAnySceneObject = RecomputeHasSceneObject();
        }

        private static bool RecomputeHasSceneObject()
        {
            foreach (var e in _entries)
                if (e.IsSceneObject) return true;
            return false;
        }

        private static void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        // ---------- Persistence ----------

        private static void Load()
        {
            try
            {
                var raw = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SerializedData>(raw);
                if (data == null) return;

                // v6 (current): typed entries (assets + scene objects).
                if (data.Entries != null && data.Entries.Count > 0)
                {
                    RepopulateFrom(data.Entries);
                    return;
                }

                // v5: flat Guids list → asset entries.
                if (data.Guids != null && data.Guids.Count > 0)
                {
                    var entries = new List<FavoriteEntry>(data.Guids.Count);
                    foreach (var g in data.Guids)
                        if (!string.IsNullOrEmpty(g)) entries.Add(FavoriteEntry.ForAsset(g));
                    RepopulateFrom(entries);
                    Save();
                    return;
                }

                // v4: workspaces → flatten asset guids.
                if (data.Workspaces != null && data.Workspaces.Count > 0)
                {
                    var entries = new List<FavoriteEntry>();
                    var seen = new HashSet<string>();
                    foreach (var w in data.Workspaces)
                    {
                        if (w.Guids == null) continue;
                        foreach (var g in w.Guids)
                            if (!string.IsNullOrEmpty(g) && seen.Add(g)) entries.Add(FavoriteEntry.ForAsset(g));
                    }
                    RepopulateFrom(entries);
                    Save();
                    return;
                }

                // v2: legacy folder schema → flatten asset guids.
                if (data.LegacyEntries != null && data.LegacyEntries.Count > 0)
                {
                    var entries = new List<FavoriteEntry>();
                    foreach (var le in data.LegacyEntries)
                    {
                        if (!string.IsNullOrEmpty(le.Guid) && string.IsNullOrEmpty(le.FolderName))
                            entries.Add(FavoriteEntry.ForAsset(le.Guid));
                        else if (!string.IsNullOrEmpty(le.FolderName) && le.FolderGuids != null)
                            foreach (var g in le.FolderGuids) entries.Add(FavoriteEntry.ForAsset(g));
                    }
                    RepopulateFrom(entries);
                    Save();
                }
            }
            catch (FileNotFoundException)      { /* first run */ }
            catch (DirectoryNotFoundException) { /* first run */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[FavoriteAssets] Failed to read preferences: {e.Message}");
            }
        }

        private static void RepopulateFrom(IEnumerable<FavoriteEntry> entries)
        {
            _entries.Clear();
            _lookup.Clear();
            _assetGuids.Clear();
            HasAnySceneObject = false;
            foreach (var e in entries) TryAdd(e);
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
                Debug.LogWarning($"[FavoriteAssets] Failed to save preferences: {e.Message}");
            }
        }

        [Serializable]
        private class SerializedData
        {
            public List<FavoriteEntry> Entries;

            // Legacy fields retained for one-way migration.
            public List<string> Guids;
            public List<LegacyWorkspace> Workspaces;

            // v2 ("Entries" meant something different then — stashed under a distinct name).
            public List<LegacyEntry> LegacyEntries;
        }

        [Serializable]
        private class LegacyWorkspace
        {
            public string Name;
            public List<string> Guids;
        }

        [Serializable]
        private class LegacyEntry
        {
            public string Guid;
            public string FolderName;
            public bool Expanded;
            public List<string> FolderGuids;
        }
    }
}
