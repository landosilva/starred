namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    internal static class FavoriteAssetsStore
    {
        private const string FilePath = StarredPaths.FavoritesFile;

        private static readonly List<FavoriteEntry> _entries = new();
        private static readonly HashSet<string> _lookupKeys = new();
        private static readonly HashSet<string> _assetGuidIndex = new();

        public static IReadOnlyList<FavoriteEntry> Entries => _entries;

        public static bool HasAnySceneObject { get; private set; }

        public static event Action Changed;

        static FavoriteAssetsStore() => Load();

        public static bool Contains(string guid) =>
            !string.IsNullOrEmpty(guid) && _assetGuidIndex.Contains(guid);

        public static bool ContainsSceneObject(string globalObjectId) =>
            !string.IsNullOrEmpty(globalObjectId) && _lookupKeys.Contains($"s:{globalObjectId}");

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
            _lookupKeys.Clear();
            _assetGuidIndex.Clear();
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

        public static void Add(string guid)
        {
            if (TryAdd(FavoriteEntry.ForAsset(guid))) Commit();
        }

        public static void AddRange(IEnumerable<FavoriteEntry> entries)
        {
            bool changed = false;
            foreach (FavoriteEntry entry in entries)
                if (TryAdd(entry)) changed = true;
            if (changed) Commit();
        }

        public static void Remove(string guid)
        {
            if (!_assetGuidIndex.Contains(guid)) return;
            for (int i = 0; i < _entries.Count; i++)
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
            int index = _entries.IndexOf(entry);
            if (index < 0) return;
            RemoveAt(index);
            Commit();
        }

        public static void RemoveSceneObject(string globalObjectId)
        {
            if (string.IsNullOrEmpty(globalObjectId)) return;
            string key = $"s:{globalObjectId}";
            for (int i = 0; i < _entries.Count; i++)
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

            FavoriteEntry entry = _entries[fromIndex];
            toIndex = ClampToKindRange(entry.IsSceneObject, toIndex);

            int normalized = toIndex > fromIndex ? toIndex - 1 : toIndex;
            if (normalized == fromIndex) return;

            _entries.RemoveAt(fromIndex);
            _entries.Insert(normalized, entry);
            Commit();
        }

        private static int ClampToKindRange(bool isSceneObject, int toIndex)
        {
            int firstAsset = FirstAssetIndex();
            return isSceneObject
                ? Mathf.Clamp(toIndex, 0, firstAsset)
                : Mathf.Clamp(toIndex, firstAsset, _entries.Count);
        }

        private static int FirstAssetIndex()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].IsAsset) return i;
            return _entries.Count;
        }

        private static bool TryAdd(FavoriteEntry entry)
        {
            if (entry == null) return false;
            if (!entry.IsAsset && !entry.IsSceneObject) return false;
            if (!_lookupKeys.Add(entry.LookupKey)) return false;

            EntryDisplayName.Capture(entry);

            if (entry.IsSceneObject)
            {
                _entries.Insert(FirstAssetIndex(), entry);
                HasAnySceneObject = true;
            }
            else
            {
                _entries.Add(entry);
                _assetGuidIndex.Add(entry.Guid);
            }
            return true;
        }

        private static void RemoveAt(int index)
        {
            FavoriteEntry entry = _entries[index];
            _entries.RemoveAt(index);
            _lookupKeys.Remove(entry.LookupKey);
            if (entry.IsAsset) _assetGuidIndex.Remove(entry.Guid);
            if (entry.IsSceneObject) HasAnySceneObject = RecomputeHasSceneObject();
        }

        private static bool RecomputeHasSceneObject()
        {
            foreach (FavoriteEntry entry in _entries)
                if (entry.IsSceneObject) return true;
            return false;
        }

        private static void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (!EditorJsonFile.TryLoad(FilePath, out FavoriteAssetsMigration.SerializedData data)) return;
            List<FavoriteEntry> entries = FavoriteAssetsMigration.ToCurrentEntries(data, out bool needsRewrite);
            if (entries == null) return;
            RepopulateFrom(entries);
            bool namesFilled = false;
            foreach (FavoriteEntry entry in _entries)
            {
                string previous = entry.DisplayName;
                EntryDisplayName.Capture(entry);
                if (entry.DisplayName != previous) namesFilled = true;
            }
            if (needsRewrite || namesFilled) Save();
        }

        private static void RepopulateFrom(IEnumerable<FavoriteEntry> entries)
        {
            _entries.Clear();
            _lookupKeys.Clear();
            _assetGuidIndex.Clear();
            HasAnySceneObject = false;
            foreach (FavoriteEntry entry in entries) TryAdd(entry);
        }

        private static void Save()
        {
            EditorJsonFile.Save(FilePath, new FavoriteAssetsMigration.SerializedData { Entries = new List<FavoriteEntry>(_entries) });
        }
    }
}
