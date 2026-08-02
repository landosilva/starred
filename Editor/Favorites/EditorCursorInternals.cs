namespace Kynesis.Starred.Editor
{
    using System;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal static class EditorCursorInternals
    {
        private static FieldInfo _defaultCursorIdField;

        public static StyleCursor Build(MouseCursor cursor)
        {
            Type cursorType = typeof(UnityEngine.UIElements.Cursor);
            _defaultCursorIdField ??= cursorType.GetField("m_DefaultCursorId",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? cursorType.GetField("defaultCursorId",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            object boxed = new UnityEngine.UIElements.Cursor();
            _defaultCursorIdField?.SetValue(boxed, (int)cursor);
            return new StyleCursor((UnityEngine.UIElements.Cursor)boxed);
        }
    }
}
