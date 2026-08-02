namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    internal static class FavoriteStarDrawer
    {
        private const float IconSize = 12f;
        private const float RightPadding = 2f;
        private const float GridHeightThreshold = 20f;

        private static readonly Color StarColor = new Color32(250, 200, 70, 255);

        private static GUIStyle _style;

        public static Rect Draw(Rect selectionRect)
        {
            Rect rect = ComputeStarRect(selectionRect);
            EnsureStyle();

            Color previous = GUI.color;
            GUI.color = StarColor;
            GUI.Label(rect, "\u2605", _style);
            GUI.color = previous;

            return rect;
        }

        private static Rect ComputeStarRect(Rect selectionRect)
        {
            bool isGridView = selectionRect.height > GridHeightThreshold;
            float x = selectionRect.xMax - IconSize - RightPadding;
            float y = isGridView
                ? selectionRect.y + RightPadding
                : selectionRect.y + (selectionRect.height - IconSize) * 0.5f;
            return new Rect(x, y, IconSize, IconSize);
        }

        private static void EnsureStyle()
        {
            _style ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }
    }
}
