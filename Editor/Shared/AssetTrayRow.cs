namespace Kynesis.Starred.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal static class AssetTrayRow
    {
        internal static class Classes
        {
            public const string Row = "assettray-row";
            public const string Icon = "assettray-row-icon";
            public const string Label = "assettray-row-label";
            public const string Ping = "assettray-row-ping";
            public const string Action = "assettray-row-action";
            public const string Missing = "assettray-row--missing";
            public const string Current = "assettray-row--current";
            public const string Inactive = "assettray-row--inactive";
            public const string ContextIcon = "assettray-row-context-icon";
            public const string ContextLabel = "assettray-row-context-label";
            public const string ContextSeparator = "assettray-row-context-separator";
            public const string Dragging = "assettray-row--dragging";
            public const string WithDrag = "assettray-with-drag";
            public const string ListDragOver = "assettray-list--drag-over";
            public const string DragGhost = "assettray-drag-ghost";
            public const string AddOverlay = "assettray-add-overlay";
            public const string AddOverlayLabel = "assettray-add-overlay-label";
            public const string AddOverlayDuplicate = "assettray-add-overlay--duplicate";
            public const string OverlayActive = "assettray-overlay-active";
            public const string Separator = "assettray-separator";
            public const string ActionRemove = "assettray-row-action--remove";
            public const string ActionStar = "assettray-row-action--star";
            public const string ActionStarOn = "assettray-row-action--star-on";
        }

        public static VisualElement CreateShell(object userData, Texture icon, string labelText, string tooltip, bool missing)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList(Classes.Row);
            row.userData = userData;
            if (missing) row.AddToClassList(Classes.Missing);

            Image iconElement = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
            iconElement.AddToClassList(Classes.Icon);
            row.Add(iconElement);

            Label label = new Label(labelText) { tooltip = tooltip };
            label.AddToClassList(Classes.Label);
            row.Add(label);

            return row;
        }

        public static VisualElement CreateAssetRow(string guid, out UnityEngine.Object asset, out string path, object userData = null)
        {
            path = AssetDatabase.GUIDToAssetPath(guid);
            asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
            FavoriteEntry entry = userData as FavoriteEntry;

            if (entry != null && asset != null)
                EntryDisplayName.Capture(entry);

            Texture icon = asset != null
                ? AssetDatabase.GetCachedIcon(path)
                : EditorGUIUtility.IconContent(StarredPaths.WarningIcon).image;
            string labelText = EntryDisplayName.Resolve(entry, asset, path);
            string tooltip = asset != null
                ? path
                : string.IsNullOrEmpty(path)
                    ? $"{StarredText.DeletedPrefix}GUID: {guid}"
                    : $"{StarredText.DeletedPrefix}{path}";

            return CreateShell(userData ?? guid, icon, labelText, tooltip, asset == null);
        }

        public static VisualElement CreateSceneObjectRow(FavoriteEntry entry, out GameObject gameObject)
        {
            gameObject = SceneObjectResolver.Find(entry);
            if (gameObject != null) EntryDisplayName.Capture(entry);

            string objectName = EntryDisplayName.Resolve(entry, gameObject, null);
            string tooltip = gameObject != null
                ? $"{entry.ScenePath} → {entry.HierarchyPath}"
                : $"{StarredText.DeletedPrefix}{entry.ScenePath} → {entry.HierarchyPath}";
            Texture objectIcon = gameObject != null
                ? EditorGUIUtility.ObjectContent(gameObject, gameObject.GetType()).image
                : EditorGUIUtility.IconContent(StarredPaths.WarningIcon).image;

            bool isPrefabStage = string.Equals(System.IO.Path.GetExtension(entry.ScenePath), ".prefab",
                System.StringComparison.OrdinalIgnoreCase);
            Texture contextIcon = EditorGUIUtility.IconContent(isPrefabStage ? "Prefab Icon" : "SceneAsset Icon").image;
            string contextName = System.IO.Path.GetFileNameWithoutExtension(entry.ScenePath);

            VisualElement row = new VisualElement();
            row.AddToClassList(Classes.Row);
            row.userData = entry;
            if (gameObject == null) row.AddToClassList(Classes.Missing);
            else if (!gameObject.activeInHierarchy) row.AddToClassList(Classes.Inactive);

            Image contextIconElement = new Image { image = contextIcon, scaleMode = ScaleMode.ScaleToFit };
            contextIconElement.AddToClassList(Classes.Icon);
            contextIconElement.AddToClassList(Classes.ContextIcon);
            row.Add(contextIconElement);

            Label contextLabelElement = new Label(contextName);
            contextLabelElement.AddToClassList(Classes.ContextLabel);
            row.Add(contextLabelElement);

            Label separator = new Label("›");
            separator.AddToClassList(Classes.ContextSeparator);
            row.Add(separator);

            Image mainIcon = new Image { image = objectIcon, scaleMode = ScaleMode.ScaleToFit };
            mainIcon.AddToClassList(Classes.Icon);
            row.Add(mainIcon);

            Label mainLabel = new Label(objectName) { tooltip = tooltip };
            mainLabel.AddToClassList(Classes.Label);
            row.Add(mainLabel);

            return row;
        }

        public static Button CreatePingButton(UnityEngine.Object asset)
        {
            return CreatePingButton(() =>
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }, StarredText.ShowInProject);
        }

        public static Button CreatePingButton(Action onClick, string tooltip)
        {
            Button button = new Button(onClick) { tooltip = tooltip };
            button.AddToClassList(Classes.Ping);
            button.Add(new Image { image = EditorGUIUtility.IconContent("d_Search Icon").image });
            return button;
        }

        public static void AppendAssetContextMenu(DropdownMenu menu, UnityEngine.Object asset, string guid, string path)
        {
            menu.AppendAction(StarredText.ShowInProject, _ => { EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; });
            menu.AppendAction(StarredText.ShowInExplorer, _ => EditorUtility.RevealInFinder(path));
            menu.AppendAction(StarredText.Open, _ => AssetDatabase.OpenAsset(asset));
            menu.AppendSeparator("");
            menu.AppendAction(StarredText.CopyPath, _ => EditorGUIUtility.systemCopyBuffer = path);
            menu.AppendAction(StarredText.CopyGuid, _ => EditorGUIUtility.systemCopyBuffer = guid);
        }

        public static string GetCurrentSelectionGuid()
        {
            UnityEngine.Object active = Selection.activeObject;
            if (active == null) return null;
            string path = AssetDatabase.GetAssetPath(active);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        public static void ApplyCurrentHighlight(VisualElement list, Func<object, bool> isCurrent)
        {
            if (list == null) return;
            foreach (VisualElement child in list.Children())
            {
                if (child.userData == null) continue;
                child.EnableInClassList(Classes.Current, isCurrent(child.userData));
            }
        }
    }
}
