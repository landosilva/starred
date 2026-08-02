namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;

    internal static class FavoriteAssetsMigration
    {
        [Serializable]
        public class SerializedData
        {
            public List<FavoriteEntry> Entries;
            public List<string> Guids;
            public List<LegacyWorkspace> Workspaces;
            public List<LegacyEntry> LegacyEntries;
        }

        [Serializable]
        public class LegacyWorkspace
        {
            public string Name;
            public List<string> Guids;
        }

        [Serializable]
        public class LegacyEntry
        {
            public string Guid;
            public string FolderName;
            public bool Expanded;
            public List<string> FolderGuids;
        }

        public static List<FavoriteEntry> ToCurrentEntries(SerializedData data, out bool needsRewrite)
        {
            needsRewrite = false;
            if (data == null) return null;

            if (data.Entries != null && data.Entries.Count > 0)
                return new List<FavoriteEntry>(data.Entries);

            if (data.Guids != null && data.Guids.Count > 0)
            {
                needsRewrite = true;
                List<FavoriteEntry> entries = new List<FavoriteEntry>(data.Guids.Count);
                foreach (string guid in data.Guids)
                    if (!string.IsNullOrEmpty(guid)) entries.Add(FavoriteEntry.ForAsset(guid));
                return entries;
            }

            if (data.Workspaces != null && data.Workspaces.Count > 0)
            {
                needsRewrite = true;
                List<FavoriteEntry> entries = new List<FavoriteEntry>();
                HashSet<string> seen = new HashSet<string>();
                foreach (LegacyWorkspace workspace in data.Workspaces)
                {
                    if (workspace.Guids == null) continue;
                    foreach (string guid in workspace.Guids)
                        if (!string.IsNullOrEmpty(guid) && seen.Add(guid)) entries.Add(FavoriteEntry.ForAsset(guid));
                }
                return entries;
            }

            if (data.LegacyEntries != null && data.LegacyEntries.Count > 0)
            {
                needsRewrite = true;
                List<FavoriteEntry> entries = new List<FavoriteEntry>();
                foreach (LegacyEntry legacyEntry in data.LegacyEntries)
                {
                    if (!string.IsNullOrEmpty(legacyEntry.Guid) && string.IsNullOrEmpty(legacyEntry.FolderName))
                        entries.Add(FavoriteEntry.ForAsset(legacyEntry.Guid));
                    else if (!string.IsNullOrEmpty(legacyEntry.FolderName) && legacyEntry.FolderGuids != null)
                        foreach (string guid in legacyEntry.FolderGuids) entries.Add(FavoriteEntry.ForAsset(guid));
                }
                return entries;
            }

            return null;
        }
    }
}
