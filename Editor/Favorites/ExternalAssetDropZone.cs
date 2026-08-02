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
            return (DragAndDrop.paths?.Length ?? 0) > 0
                || (DragAndDrop.objectReferences?.Length ?? 0) > 0;
        }

        public static IEnumerable<FavoriteEntry> DraggedEntries()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (string path in DragAndDrop.paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                FavoriteEntry entry = FavoriteEntry.ForAsset(guid);
                if (seen.Add(entry.LookupKey)) yield return entry;
            }
            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is not GameObject gameObject || EditorUtility.IsPersistent(gameObject)) continue;
                FavoriteEntry entry = SceneObjectResolver.BuildEntry(gameObject);
                if (entry == null || string.IsNullOrEmpty(entry.ScenePath))
                {
                    StarredLog.Warning($"Can't favorite '{gameObject.name}' — save its scene first.");
                    continue;
                }
                if (string.IsNullOrEmpty(entry.GlobalObjectId)) continue;
                if (seen.Add(entry.LookupKey)) yield return entry;
            }
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
                if (addOverlay.style.display == DisplayStyle.None && HasAnySupportedItemInDrag())
                    ShowAddOverlay(root, addOverlay, addOverlayLabel);
                DragAndDrop.visualMode = HasAnySupportedItemInDrag()
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                dragUpdatedEvent.StopPropagation();
            });

            zone.RegisterCallback<DragPerformEvent>(dragPerformEvent =>
            {
                DragAndDrop.AcceptDrag();
                FavoriteAssetsStore.AddRange(DraggedEntries());
                HideAddOverlay(root, addOverlay);
                dragPerformEvent.StopPropagation();
            });
        }

        private static void ShowAddOverlay(VisualElement root, VisualElement addOverlay, Label addOverlayLabel)
        {
            if (addOverlay == null) return;

            bool allDuplicate = true;
            bool anyItem = false;
            foreach (FavoriteEntry entry in DraggedEntries())
            {
                anyItem = true;
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
