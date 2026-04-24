namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Draws the gold favorite star in IMGUI contexts (Project window +
    /// Hierarchy item callbacks). Used by both overlays to keep their visuals
    /// identical and avoid duplicated style / color / hit-test logic.
    /// </summary>
    internal static class FavoriteStarDrawer
    {
        private const float IconSize = 12f;
        private const float RightPadding = 2f;
        private const float GridHeightThreshold = 20f;

        private static readonly Color StarColor = new Color32(250, 200, 70, 255);

        private static GUIStyle _style;

        /// <summary>
        /// Draws the star at the right edge of <paramref name="selectionRect"/>
        /// and returns the rect it occupies so callers can hit-test against it.
        /// </summary>
        public static Rect Draw(Rect selectionRect)
        {
            var rect = ComputeStarRect(selectionRect);
            EnsureStyle();

            var prev = GUI.color;
            GUI.color = StarColor;
            GUI.Label(rect, "\u2605", _style);
            GUI.color = prev;

            return rect;
        }

        private static Rect ComputeStarRect(Rect selectionRect)
        {
            var isGridView = selectionRect.height > GridHeightThreshold;
            var x = selectionRect.xMax - IconSize - RightPadding;
            var y = isGridView
                ? selectionRect.y + RightPadding
                : selectionRect.y + (selectionRect.height - IconSize) * 0.5f;
            return new Rect(x, y, IconSize, IconSize);
        }

        private static void EnsureStyle()
        {
            _style ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
                padding   = new RectOffset(0, 0, 0, 0),
                margin    = new RectOffset(0, 0, 0, 0),
            };
        }
    }
}
