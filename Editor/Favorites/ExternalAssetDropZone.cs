namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal static class ExternalAssetDropZone
    {
        public static bool HasAnySupportedItemInDrag()
        {
            List<FavoriteEntry> entries = CollectDraggedEntries(logWarnings: false);
            return entries.Count > 0;
        }

        public static IEnumerable<FavoriteEntry> DraggedEntries() =>
            CollectDraggedEntries(logWarnings: true);

        public static void PerformDrop()
        {
            List<FavoriteEntry> entries = CollectDraggedEntries(logWarnings: true);
            if (entries.Count == 0) return;
            DragAndDrop.AcceptDrag();
            FavoriteAssetsStore.AddRange(entries);
        }

        private static List<FavoriteEntry> CollectDraggedEntries(bool logWarnings)
        {
            List<FavoriteEntry> entries = new List<FavoriteEntry>();
            HashSet<string> seen = new HashSet<string>();

            string[] paths = DragAndDrop.paths ?? Array.Empty<string>();
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                FavoriteEntry entry = FavoriteEntry.ForAsset(guid);
                if (seen.Add(entry.LookupKey)) entries.Add(entry);
            }

            UnityEngine.Object[] objectReferences = DragAndDrop.objectReferences ?? Array.Empty<UnityEngine.Object>();
            foreach (UnityEngine.Object draggedObject in objectReferences)
            {
                if (draggedObject is not GameObject gameObject || EditorUtility.IsPersistent(gameObject)) continue;
                FavoriteEntry entry = SceneObjectResolver.BuildEntry(gameObject);
                if (entry == null || string.IsNullOrEmpty(entry.ScenePath))
                {
                    if (logWarnings)
                        StarredLog.Warning($"Can't favorite '{gameObject.name}' — save its scene first.");
                    continue;
                }
                if (string.IsNullOrEmpty(entry.GlobalObjectId)) continue;
                if (seen.Add(entry.LookupKey)) entries.Add(entry);
            }

            return entries;
        }

        public static void Register(
            VisualElement zone,
            VisualElement root,
            VisualElement addOverlay,
            Label addOverlayLabel,
            Action endPress)
        {
            zone.RegisterCallback<DragEnterEvent>(dragEnterEvent =>
            {
                if (dragEnterEvent.target != zone) return;
                endPress();
                if (HasAnySupportedItemInDrag())
                    ShowAddOverlay(root, addOverlay, addOverlayLabel);
            });
            zone.RegisterCallback<DragLeaveEvent>(dragLeaveEvent =>
            {
                if (dragLeaveEvent.target != zone) return;
                HideAddOverlay(root, addOverlay);
            });
            zone.RegisterCallback<DragExitedEvent>(_ => HideAddOverlay(root, addOverlay));

            zone.RegisterCallback<DragUpdatedEvent>(dragUpdatedEvent =>
            {
                bool supported = HasAnySupportedItemInDrag();
                if (addOverlay.style.display == DisplayStyle.None && supported)
                    ShowAddOverlay(root, addOverlay, addOverlayLabel);
                DragAndDrop.visualMode = supported
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                dragUpdatedEvent.StopPropagation();
            });

            zone.RegisterCallback<DragPerformEvent>(dragPerformEvent =>
            {
                PerformDrop();
                HideAddOverlay(root, addOverlay);
                dragPerformEvent.StopPropagation();
            });
        }

        private static void ShowAddOverlay(VisualElement root, VisualElement addOverlay, Label addOverlayLabel)
        {
            if (addOverlay == null) return;

            List<FavoriteEntry> entries = CollectDraggedEntries(logWarnings: false);
            bool anyItem = entries.Count > 0;
            bool allDuplicate = anyItem;
            foreach (FavoriteEntry entry in entries)
            {
                if (!FavoriteAssetsStore.Contains(entry)) { allDuplicate = false; break; }
            }
            bool duplicate = anyItem && allDuplicate;

            addOverlayLabel.text = duplicate ? StarredText.AlreadyInFavorites : StarredText.DropToAdd;
            addOverlay.EnableInClassList(AssetTrayRow.Classes.AddOverlayDuplicate, duplicate);
            addOverlay.style.display = DisplayStyle.Flex;
            addOverlay.BringToFront();
            root.AddToClassList(AssetTrayRow.Classes.OverlayActive);
        }

        private static void HideAddOverlay(VisualElement root, VisualElement addOverlay)
        {
            if (addOverlay != null) addOverlay.style.display = DisplayStyle.None;
            SyncOverlayActiveClass(root, addOverlay);
        }

        public static void SyncOverlayActiveClass(VisualElement root, VisualElement addOverlay)
        {
            if (root == null) return;
            bool anyVisible = addOverlay != null && addOverlay.style.display == DisplayStyle.Flex;
            if (!anyVisible) root.RemoveFromClassList(AssetTrayRow.Classes.OverlayActive);
            else root.AddToClassList(AssetTrayRow.Classes.OverlayActive);
        }
    }
}
