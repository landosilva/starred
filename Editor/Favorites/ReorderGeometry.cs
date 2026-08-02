namespace Kynesis.Starred.Editor
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal static class ReorderGeometry
    {
        public static int ClampToBlock(FavoriteEntry entry, int visibleIndex, List<VisualElement> rows)
        {
            if (entry == null) return visibleIndex;
            int boundary = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].userData is FavoriteEntry rowEntry && rowEntry.IsAsset)
                {
                    boundary = i;
                    break;
                }
            }
            return entry.IsSceneObject
                ? Mathf.Clamp(visibleIndex, 0, boundary)
                : Mathf.Clamp(visibleIndex, boundary, rows.Count);
        }

        public static int VisibleIndexToEntryIndex(int visibleIndex, List<VisualElement> rows)
        {
            int total = FavoriteAssetsStore.Entries.Count;
            if (visibleIndex >= rows.Count) return total;
            if (rows[visibleIndex].userData is not FavoriteEntry targetEntry) return total;
            return FindEntryIndex(targetEntry);
        }

        public static int FindEntryIndex(FavoriteEntry entry)
        {
            IReadOnlyList<FavoriteEntry> entries = FavoriteAssetsStore.Entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] == entry) return i;
            return -1;
        }

        public static void ApplyReorderShift(List<VisualElement> rows, FavoriteEntry dragged, int dropIndex)
        {
            if (dragged == null || rows.Count == 0) return;
            int draggedIndex = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].userData is FavoriteEntry rowEntry && rowEntry == dragged) { draggedIndex = i; break; }
            }
            if (draggedIndex < 0) return;
            float rowSpacing = rows.Count > 1
                ? rows[1].layout.y - rows[0].layout.y
                : rows[0].layout.height;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == draggedIndex) continue;
                float offset = 0f;
                if (dropIndex < draggedIndex && i >= dropIndex && i < draggedIndex) offset = rowSpacing;
                else if (dropIndex > draggedIndex && i > draggedIndex && i < dropIndex) offset = -rowSpacing;
                rows[i].style.translate = new StyleTranslate(new Translate(0, offset, 0));
            }
        }

        public static void PositionDragGhost(
            VisualElement dragGhost, VisualElement root, VisualElement list,
            List<VisualElement> rows, FavoriteEntry dragged, float localMouseY)
        {
            if (dragGhost == null || dragged == null || rows.Count == 0) return;
            VisualElement draggedRow = null;
            float blockTop = float.PositiveInfinity;
            float blockBottom = float.NegativeInfinity;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].userData is not FavoriteEntry rowEntry) continue;
                if (rowEntry == dragged) draggedRow = rows[i];
                if (rowEntry.IsSceneObject != dragged.IsSceneObject) continue;
                if (rows[i].layout.y < blockTop) blockTop = rows[i].layout.y;
                if (rows[i].layout.yMax > blockBottom) blockBottom = rows[i].layout.yMax;
            }
            if (draggedRow == null) return;
            float rowHeight = draggedRow.layout.height;
            float halfRow = rowHeight * 0.5f;
            float clampedY = Mathf.Clamp(localMouseY, blockTop + halfRow, blockBottom - halfRow);
            Vector2 rowWorld = list.LocalToWorld(new Vector2(0, clampedY - halfRow));
            Vector2 rootLocal = root.WorldToLocal(rowWorld);
            Vector2 rowRectWorld = draggedRow.LocalToWorld(new Vector2(0, 0));
            Vector2 rowRectRoot = root.WorldToLocal(rowRectWorld);
            dragGhost.style.top = rootLocal.y;
            dragGhost.style.left = rowRectRoot.x;
        }

        public static List<VisualElement> CollectRows(VisualElement list, List<VisualElement> buffer)
        {
            buffer.Clear();
            foreach (VisualElement child in list.Children())
                if (child.userData != null) buffer.Add(child);
            return buffer;
        }
    }
}
