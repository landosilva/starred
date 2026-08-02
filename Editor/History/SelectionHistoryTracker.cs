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

        internal static void SuppressNext() => _suppressNext = true;

        public static void SelectWithoutRecording(UnityEngine.Object target)
        {
            SuppressNext();
            SelectionWithoutProjectJump.Select(target);
        }

        private static void OnSelectionChanged()
        {
            if (_suppressNext)
            {
                _suppressNext = false;
                return;
            }

            UnityEngine.Object active = Selection.activeObject;
            if (active == null) return;

            string assetPath = AssetDatabase.GetAssetPath(active);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!string.IsNullOrEmpty(guid))
                    SelectionHistoryStore.Record(FavoriteEntry.ForAsset(guid));
                return;
            }

            if (active is GameObject gameObject)
            {
                FavoriteEntry entry = SceneObjectResolver.BuildEntry(gameObject);
                if (entry == null || string.IsNullOrEmpty(entry.ScenePath)) return;
                SelectionHistoryStore.Record(entry);
            }
        }
    }
}
