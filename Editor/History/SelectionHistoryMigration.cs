namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;

    internal static class SelectionHistoryMigration
    {
        [Serializable]
        public class SerializedData
        {
            public List<FavoriteEntry> Entries;
            public List<string> Guids;
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

            return null;
        }
    }
}
