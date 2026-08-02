namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    public class FavoriteAssetsWindow : EditorWindow, IHasCustomMenu
    {
        private VisualElement _list;
        private Label _emptyState;
        private VisualElement _addOverlay;
        private Label _addOverlayLabel;
        private FavoriteRowReorder _reorder;

        [MenuItem(StarredText.FavoritesMenuPath, false, 1)]
        public static void Open()
        {
            FavoriteAssetsWindow window = GetWindow<FavoriteAssetsWindow>();
            window.titleContent = new GUIContent(StarredText.Favorites, EditorGUIUtility.IconContent("d_Favorite").image);
            window.minSize = new Vector2(220, 200);
            window.Show();
        }

        private void OnEnable()
        {
            FavoriteAssetsStore.Changed += Rebuild;
            Selection.selectionChanged += ApplyCurrentHighlight;
            AssetChangeNotifier.Changed += Rebuild;
            EditorApplication.hierarchyChanged += Rebuild;

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
        }

        private void OnDisable()
        {
            _reorder?.EndPress();

            FavoriteAssetsStore.Changed -= Rebuild;
            Selection.selectionChanged -= ApplyCurrentHighlight;
            AssetChangeNotifier.Changed -= Rebuild;
            EditorApplication.hierarchyChanged -= Rebuild;

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent(StarredText.ShowStarInProject), FavoriteAssetsSettings.ShowProjectWindowStar,
                () => FavoriteAssetsSettings.ShowProjectWindowStar = !FavoriteAssetsSettings.ShowProjectWindowStar);
            menu.AddItem(new GUIContent(StarredText.ShowStarInHierarchy), FavoriteAssetsSettings.ShowHierarchyStar,
                () => FavoriteAssetsSettings.ShowHierarchyStar = !FavoriteAssetsSettings.ShowHierarchyStar);
            menu.AddSeparator("");

            if (FavoriteAssetsStore.Entries.Count > 0)
                menu.AddItem(new GUIContent(StarredText.ClearAllFavorites), false, PromptClearFavorites);
            else
                menu.AddDisabledItem(new GUIContent(StarredText.ClearAllFavorites));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(StarredText.OpenPreferences), false, FavoriteAssetsSettings.OpenPreferences);
        }

        private static void PromptClearFavorites()
        {
            if (EditorUtility.DisplayDialog(
                    StarredText.ClearAllFavoritesTitle,
                    $"Remove all {FavoriteAssetsStore.Entries.Count} favorited entries? This cannot be undone.",
                    StarredText.Clear, StarredText.Cancel))
            {
                FavoriteAssetsStore.Clear();
            }
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode _) => Rebuild();
        private void OnSceneClosed(Scene scene) => Rebuild();
        private void OnActiveSceneChanged(Scene previous, Scene next) => Rebuild();
        private void OnPrefabStageChanged(PrefabStage stage) => Rebuild();

        private void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetTrayPaths.Find<VisualTreeAsset>(StarredPaths.FavoritesWindow);
            StyleSheet styleSheet = AssetTrayPaths.Find<StyleSheet>(StarredPaths.TrayStyles);
            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(styleSheet);

            _list = rootVisualElement.Q<VisualElement>(StarredPaths.List);
            _emptyState = rootVisualElement.Q<Label>(StarredPaths.EmptyState);

            _addOverlay = new VisualElement();
            _addOverlay.AddToClassList(AssetTrayRow.Classes.AddOverlay);
            _addOverlay.style.left = 4;
            _addOverlay.style.right = 4;
            _addOverlay.style.top = 4;
            _addOverlay.style.bottom = 4;
            _addOverlay.style.display = DisplayStyle.None;
            _addOverlay.pickingMode = PickingMode.Ignore;
            _addOverlayLabel = new Label();
            _addOverlayLabel.AddToClassList(AssetTrayRow.Classes.AddOverlayLabel);
            _addOverlay.Add(_addOverlayLabel);
            rootVisualElement.Add(_addOverlay);

            _reorder = new FavoriteRowReorder(this);
            VisualElement dragGhost = _reorder.CreateAndBindGhost(rootVisualElement, _list, _addOverlay);
            rootVisualElement.Add(dragGhost);
            _reorder.RegisterPointerHandlers();
            _reorder.RegisterImguiFallback(() => FavoriteAssetsStore.AddRange(ExternalAssetDropZone.DraggedEntries()));
            ExternalAssetDropZone.Register(rootVisualElement, rootVisualElement, _addOverlay, _addOverlayLabel, _reorder.EndPress);

            Rebuild();
        }

        private void Rebuild()
        {
            _reorder?.OnRebuildStarting();

            if (_list == null) return;
            _list.Clear();

            int sceneCount = 0;
            foreach (FavoriteEntry entry in FavoriteAssetsStore.Entries)
            {
                if (!entry.IsSceneObject) continue;
                VisualElement row = CreateSceneObjectRow(entry);
                if (row == null) continue;
                _list.Add(row);
                sceneCount++;
            }

            int renderedCount = sceneCount;
            foreach (FavoriteEntry entry in FavoriteAssetsStore.Entries)
            {
                if (!entry.IsAsset) continue;
                VisualElement row = CreateAssetRow(entry);
                if (row == null) continue;
                _list.Add(row);
                renderedCount++;
            }

            bool hasBothBlocks = sceneCount > 0 && renderedCount > sceneCount;
            if (hasBothBlocks)
            {
                VisualElement separator = new VisualElement();
                separator.AddToClassList(AssetTrayRow.Classes.Separator);
                separator.pickingMode = PickingMode.Ignore;
                _list.Insert(sceneCount, separator);
            }

            _emptyState.style.display = renderedCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            TrayRowBinder.ApplyEntryHighlight(_list);
        }

        private void ApplyCurrentHighlight() => TrayRowBinder.ApplyEntryHighlight(_list);

        private VisualElement CreateAssetRow(FavoriteEntry entry)
        {
            VisualElement row = AssetTrayRow.CreateAssetRow(entry.Guid, out UnityEngine.Object asset, out string path, userData: entry);

            if (asset != null)
                row.Add(AssetTrayRow.CreatePingButton(asset));

            row.Add(CreateRemoveButton(entry));
            row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
            {
                if (asset != null)
                {
                    menuEvent.menu.AppendAction(StarredText.Properties, _ => EditorUtility.OpenPropertyEditor(asset));
                    menuEvent.menu.AppendSeparator("");
                    AssetTrayRow.AppendAssetContextMenu(menuEvent.menu, asset, entry.Guid, path);
                    menuEvent.menu.AppendSeparator("");
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    menuEvent.menu.AppendAction(StarredText.CopyPath, _ => EditorGUIUtility.systemCopyBuffer = path);
                    menuEvent.menu.AppendAction(StarredText.CopyGuid, _ => EditorGUIUtility.systemCopyBuffer = entry.Guid);
                    menuEvent.menu.AppendSeparator("");
                }
                menuEvent.menu.AppendAction(StarredText.RemoveFromFavorites, _ => FavoriteAssetsStore.RemoveEntry(entry));
            }));
            return row;
        }

        private VisualElement CreateSceneObjectRow(FavoriteEntry entry)
        {
            VisualElement row = AssetTrayRow.CreateSceneObjectRow(entry, out GameObject gameObject);
            if (row == null) return null;

            if (gameObject != null)
            {
                row.Add(AssetTrayRow.CreatePingButton(() =>
                {
                    EditorGUIUtility.PingObject(gameObject);
                    Selection.activeGameObject = gameObject;
                }));
            }

            row.Add(CreateRemoveButton(entry));
            row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
            {
                if (gameObject != null)
                {
                    menuEvent.menu.AppendAction(StarredText.Properties, _ => EditorUtility.OpenPropertyEditor(gameObject));
                    menuEvent.menu.AppendSeparator("");
                    menuEvent.menu.AppendAction(StarredText.ShowInHierarchy, _ =>
                    {
                        EditorGUIUtility.PingObject(gameObject);
                        Selection.activeGameObject = gameObject;
                    });
                    menuEvent.menu.AppendAction(StarredText.FrameInSceneView, _ =>
                    {
                        Selection.activeGameObject = gameObject;
                        SceneView.FrameLastActiveSceneView();
                    });
                    menuEvent.menu.AppendSeparator("");
                }
                if (!string.IsNullOrEmpty(entry.HierarchyPath))
                {
                    menuEvent.menu.AppendAction(StarredText.CopyHierarchyPath, _ => EditorGUIUtility.systemCopyBuffer = entry.HierarchyPath);
                    menuEvent.menu.AppendSeparator("");
                }
                menuEvent.menu.AppendAction(StarredText.RemoveFromFavorites, _ => FavoriteAssetsStore.RemoveEntry(entry));
            }));

            return row;
        }

        private static Button CreateRemoveButton(FavoriteEntry entry)
        {
            Button button = new Button(() => FavoriteAssetsStore.RemoveEntry(entry))
            {
                text = "\u00D7",
                tooltip = StarredText.RemoveFromFavoritesTooltip,
            };
            button.AddToClassList(AssetTrayRow.Classes.Action);
            button.AddToClassList(AssetTrayRow.Classes.ActionRemove);
            return button;
        }
    }
}
