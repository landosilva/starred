namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Shared row-construction helpers used by both the Favorites window and the
    /// Selection History window. Keeps visuals and behaviour in sync.
    /// </summary>
    internal static class AssetTrayRow
    {
        internal static class Classes
        {
            public const string Row     = "assettray-row";
            public const string Icon    = "assettray-row-icon";
            public const string Label   = "assettray-row-label";
            public const string Ping    = "assettray-row-ping";
            public const string Action  = "assettray-row-action";
            public const string Missing = "assettray-row--missing";
            public const string Current = "assettray-row--current";
        }

        public static VisualElement CreateShell(object userData, Texture icon, string labelText, string tooltip, bool missing)
        {
            var row = new VisualElement();
            row.AddToClassList(Classes.Row);
            row.userData = userData;
            if (missing) row.AddToClassList(Classes.Missing);

            var iconElement = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
            iconElement.AddToClassList(Classes.Icon);
            row.Add(iconElement);

            var label = new Label(labelText) { tooltip = tooltip };
            label.AddToClassList(Classes.Label);
            row.Add(label);

            return row;
        }

        public static VisualElement CreateAssetRow(string guid, out UnityEngine.Object asset, out string path, object userData = null)
        {
            path  = AssetDatabase.GUIDToAssetPath(guid);
            asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);

            var icon = asset != null
                ? AssetDatabase.GetCachedIcon(path)
                : EditorGUIUtility.IconContent("console.warnicon.sml").image;
            var labelText = asset != null
                ? asset.name
                : string.IsNullOrEmpty(path) ? "(unknown asset)" : "(deleted)";
            var tooltip   = string.IsNullOrEmpty(path) ? $"Unknown GUID: {guid}" : path;

            return CreateShell(userData ?? guid, icon, labelText, tooltip, asset == null);
        }

        /// <summary>
        /// Builds the visual shell for a scene-bound entry and resolves the
        /// backing GameObject. Returns null if the entry's scene / prefab-stage
        /// isn't the active context — callers should skip the row entirely in
        /// that case.
        /// </summary>
        public static VisualElement CreateSceneObjectRow(FavoriteEntry entry, out GameObject go)
        {
            go = null;
            if (!SceneObjectResolver.IsSceneAvailable(entry.ScenePath)) return null;

            go = SceneObjectResolver.Find(entry.ScenePath, entry.HierarchyPath);
            var objectName = go != null ? go.name : LastSegment(entry.HierarchyPath);
            var tooltip    = $"{entry.ScenePath} → {entry.HierarchyPath}";
            var objectIcon = go != null
                ? EditorGUIUtility.ObjectContent(go, go.GetType()).image
                : EditorGUIUtility.IconContent("console.warnicon.sml").image;

            var isPrefabStage = string.Equals(System.IO.Path.GetExtension(entry.ScenePath), ".prefab",
                System.StringComparison.OrdinalIgnoreCase);
            var contextIcon = EditorGUIUtility.IconContent(isPrefabStage ? "Prefab Icon" : "SceneAsset Icon").image;
            var contextName = System.IO.Path.GetFileNameWithoutExtension(entry.ScenePath);

            var row = new VisualElement();
            row.AddToClassList(Classes.Row);
            row.userData = entry;
            if (go == null) row.AddToClassList(Classes.Missing);

            var ctxIcon = new Image { image = contextIcon, scaleMode = ScaleMode.ScaleToFit };
            ctxIcon.AddToClassList(Classes.Icon);
            ctxIcon.AddToClassList("assettray-row-context-icon");
            row.Add(ctxIcon);

            var ctxLabel = new Label(contextName);
            ctxLabel.AddToClassList("assettray-row-context-label");
            row.Add(ctxLabel);

            var separator = new Label("›");
            separator.AddToClassList("assettray-row-context-separator");
            row.Add(separator);

            var mainIcon = new Image { image = objectIcon, scaleMode = ScaleMode.ScaleToFit };
            mainIcon.AddToClassList(Classes.Icon);
            row.Add(mainIcon);

            var mainLabel = new Label(objectName) { tooltip = tooltip };
            mainLabel.AddToClassList(Classes.Label);
            row.Add(mainLabel);

            return row;
        }

        private static string LastSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(empty)";
            var slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        public static Button CreatePingButton(UnityEngine.Object asset)
        {
            return CreatePingButton(() =>
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            });
        }

        public static Button CreatePingButton(Action onClick)
        {
            var btn = new Button(onClick) { tooltip = "Show in Project" };
            btn.AddToClassList(Classes.Ping);
            btn.Add(new Image { image = EditorGUIUtility.IconContent("d_Search Icon").image });
            return btn;
        }

        public static void AppendAssetContextMenu(DropdownMenu menu, UnityEngine.Object asset, string guid, string path)
        {
            menu.AppendAction("Show in Project", _ => { EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; });
            menu.AppendAction("Show in Explorer", _ => EditorUtility.RevealInFinder(path));
            menu.AppendAction("Open", _ => AssetDatabase.OpenAsset(asset));
            menu.AppendSeparator("");
            menu.AppendAction("Copy Path", _ => EditorGUIUtility.systemCopyBuffer = path);
            menu.AppendAction("Copy GUID", _ => EditorGUIUtility.systemCopyBuffer = guid);
        }

        public static string GetCurrentSelectionGuid()
        {
            var active = Selection.activeObject;
            if (active == null) return null;
            var path = AssetDatabase.GetAssetPath(active);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        public static void ApplyCurrentHighlight(VisualElement list, Func<object, bool> isCurrent)
        {
            if (list == null) return;
            foreach (var child in list.Children())
            {
                if (child.userData == null) continue;
                child.EnableInClassList(Classes.Current, isCurrent(child.userData));
            }
        }

        public static void StartDragOutAsset(UnityEngine.Object asset)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { asset };
            DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(asset) };
            DragAndDrop.StartDrag(asset.name);
        }

        public static void StartDragOutObject(UnityEngine.Object obj)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { obj };
            DragAndDrop.paths = Array.Empty<string>();
            DragAndDrop.StartDrag(obj.name);
        }
    }
}
