namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEngine;

    internal static class FavoriteStarHitTest
    {
        public static void DrawAndHandleClick(Rect selectionRect, Action onRemove)
        {
            Rect starRect = FavoriteStarDrawer.Draw(selectionRect);
            Event imguiEvent = Event.current;
            if (imguiEvent.type != EventType.MouseDown || imguiEvent.button != 0) return;
            if (!starRect.Contains(imguiEvent.mousePosition)) return;
            onRemove();
            imguiEvent.Use();
        }
    }
}
