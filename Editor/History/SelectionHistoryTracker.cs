namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    internal static class SelectionHistoryTracker
    {
        private static bool _suppressNext;

        static SelectionHistoryTracker()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Skip the next <c>Selection.selectionChanged</c> event — callers that
        /// programmatically change the selection (e.g. clicking a row inside a
        /// Starred window) use this so the click doesn't reshuffle history.
        /// </summary>
        public static void SuppressNext() => _suppressNext = true;

        /// <summary>
        /// Select <paramref name="target"/> without recording the change in the
        /// history list. Use this when a click inside a history row just means
        /// "navigate there" — the entry is already in the list and shouldn't
        /// get re-ranked under the user's cursor.
        /// </summary>
        public static void Select(UnityEngine.Object target)
        {
            SuppressNext();
            Selection.activeObject = target;
        }

        private static void OnSelectionChanged()
        {
            if (_suppressNext)
            {
                _suppressNext = false;
                return;
            }

            var active = Selection.activeObject;
            if (active == null) return;

            var assetPath = AssetDatabase.GetAssetPath(active);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                    SelectionHistoryPreferences.Record(FavoriteEntry.ForAsset(guid));
                return;
            }

            if (active is GameObject go)
            {
                var entry = SceneObjectResolver.BuildEntry(go);
                if (entry == null || string.IsNullOrEmpty(entry.ScenePath)) return; // unsaved scene — skip
                SelectionHistoryPreferences.Record(entry);
            }
        }
    }
}
