namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    public class SelectionHistoryWindow : EditorWindow, IHasCustomMenu
    {
        private VisualElement _list;
        private Label _emptyState;
        private bool _pressed;
        private Object _pressSelectTarget;

        [MenuItem(StarredText.HistoryMenuPath, false, 2)]
        public static void Open()
        {
            SelectionHistoryWindow window = GetWindow<SelectionHistoryWindow>();
            window.titleContent = new GUIContent(StarredText.History, EditorGUIUtility.IconContent("d_UnityEditor.HistoryWindow").image);
            window.minSize = new Vector2(220, 200);
            window.Show();
        }

        private void OnEnable()
        {
            SelectionHistoryStore.Changed += Rebuild;
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
            SelectionHistoryStore.Changed -= Rebuild;
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
            int currentMax = FavoriteAssetsSettings.MaxHistoryEntries;
            foreach (int choice in FavoriteAssetsSettings.MaxHistoryEntriesChoices)
            {
                int capturedChoice = choice;
                menu.AddItem(new GUIContent($"{StarredText.MaxEntriesPrefix}{choice}"), currentMax == choice,
                    () => FavoriteAssetsSettings.MaxHistoryEntries = capturedChoice);
            }
            menu.AddSeparator("");

            if (SelectionHistoryStore.Entries.Count > 0)
                menu.AddItem(new GUIContent(StarredText.ClearHistory), false, SelectionHistoryStore.Clear);
            else
                menu.AddDisabledItem(new GUIContent(StarredText.ClearHistory));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(StarredText.OpenPreferences), false, FavoriteAssetsSettings.OpenPreferences);
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode _) => Rebuild();
        private void OnSceneClosed(Scene scene) => Rebuild();
        private void OnActiveSceneChanged(Scene previous, Scene next) => Rebuild();
        private void OnPrefabStageChanged(PrefabStage stage) => Rebuild();

        private void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetTrayPaths.Find<VisualTreeAsset>(StarredPaths.HistoryWindow);
            StyleSheet styleSheet = AssetTrayPaths.Find<StyleSheet>(StarredPaths.TrayStyles);
            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(styleSheet);

            _list = rootVisualElement.Q<VisualElement>(StarredPaths.List);
            _emptyState = rootVisualElement.Q<Label>(StarredPaths.EmptyState);

            rootVisualElement.pickingMode = PickingMode.Position;
            rootVisualElement.focusable = true;
            rootVisualElement.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnRootPointerUp, TrickleDown.TrickleDown);

            RegisterImguiPressFallback();

            Rebuild();
        }

        private void OnRootPointerDown(PointerDownEvent pointerDownEvent)
        {
            if (focusedWindow != this) Focus();

            if (pointerDownEvent.button != 0) return;
            if (pointerDownEvent.target is Button) return;

            VisualElement row = TrayRowBinder.FindRowAncestor(pointerDownEvent.target as VisualElement);
            if (row?.userData is not FavoriteEntry entry) return;

            TrayRowBinding binding = TrayRowBinder.BindFor(entry, useHistorySelection: true);
            if (binding == null) return;

            if (pointerDownEvent.clickCount == 2)
            {
                binding.OnDoubleClick();
                pointerDownEvent.StopPropagation();
                return;
            }

            _pressed = true;
            _pressSelectTarget = binding.SelectTarget;
        }

        private void RegisterImguiPressFallback()
        {
            IMGUIContainer fallback = new IMGUIContainer(OnImguiPress);
            fallback.style.position = Position.Absolute;
            fallback.style.left = 0;
            fallback.style.right = 0;
            fallback.style.top = 0;
            fallback.style.bottom = 0;
            fallback.pickingMode = PickingMode.Ignore;
            rootVisualElement.Insert(0, fallback);
        }

        private void OnImguiPress()
        {
            Event imguiEvent = Event.current;
            if (imguiEvent == null) return;
            if (imguiEvent.type != EventType.MouseDown || imguiEvent.button != 0) return;

            if (_pressed) return;
            if (focusedWindow != this) Focus();

            VisualElement target = rootVisualElement.panel?.Pick(imguiEvent.mousePosition);
            if (target is Button) return;

            VisualElement row = TrayRowBinder.FindRowAncestor(target);
            if (row?.userData is not FavoriteEntry entry) return;

            TrayRowBinding binding = TrayRowBinder.BindFor(entry, useHistorySelection: true);
            if (binding == null) return;

            if (imguiEvent.clickCount == 2)
            {
                binding.OnDoubleClick();
                return;
            }

            _pressed = true;
            _pressSelectTarget = binding.SelectTarget;
        }

        private void OnRootPointerUp(PointerUpEvent pointerUpEvent)
        {
            if (!_pressed) return;
            if ((pointerUpEvent.pressedButtons & 1) != 0) return;
            if (pointerUpEvent.button != 0) return;

            if (_pressSelectTarget != null) SelectionHistoryTracker.SelectWithoutRecording(_pressSelectTarget);

            ResetPress();
        }

        private void ResetPress()
        {
            _pressed = false;
            _pressSelectTarget = null;
        }

        private void Rebuild()
        {
            if (_list == null) return;
            _list.Clear();

            int renderedCount = 0;
            foreach (FavoriteEntry entry in SelectionHistoryStore.Entries)
            {
                VisualElement row = CreateRow(entry);
                if (row == null) continue;
                _list.Add(row);
                renderedCount++;
            }

            _emptyState.style.display = renderedCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            TrayRowBinder.ApplyEntryHighlight(_list);
        }

        private void ApplyCurrentHighlight() => TrayRowBinder.ApplyEntryHighlight(_list);

        private VisualElement CreateRow(FavoriteEntry entry)
        {
            return entry.IsAsset ? CreateAssetRow(entry)
                 : entry.IsSceneObject ? CreateSceneObjectRow(entry)
                 : null;
        }

        private VisualElement CreateAssetRow(FavoriteEntry entry)
        {
            VisualElement row = AssetTrayRow.CreateAssetRow(entry.Guid, out UnityEngine.Object asset, out string path, userData: entry);

            if (asset != null)
            {
                row.Add(AssetTrayRow.CreatePingButton(() =>
                {
                    EditorGUIUtility.PingObject(asset);
                    SelectionHistoryTracker.SelectWithoutRecording(asset);
                }));
            }

            row.Add(CreateStarButton(entry));
            row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
            {
                AppendStarMenuEntry(menuEvent.menu, entry);
                if (asset != null)
                {
                    menuEvent.menu.AppendSeparator("");
                    AssetTrayRow.AppendAssetContextMenu(menuEvent.menu, asset, entry.Guid, path);
                }
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
                    SelectionHistoryTracker.SelectWithoutRecording(gameObject);
                }));
                row.Add(CreateStarButton(entry));

                row.AddManipulator(new ContextualMenuManipulator(menuEvent =>
                {
                    AppendStarMenuEntry(menuEvent.menu, entry);
                    menuEvent.menu.AppendSeparator("");
                    menuEvent.menu.AppendAction(StarredText.ShowInHierarchy, _ =>
                    {
                        EditorGUIUtility.PingObject(gameObject);
                        SelectionHistoryTracker.SelectWithoutRecording(gameObject);
                    });
                    menuEvent.menu.AppendAction(StarredText.FrameInSceneView, _ =>
                    {
                        SelectionHistoryTracker.SelectWithoutRecording(gameObject);
                        SceneView.FrameLastActiveSceneView();
                    });
                }));
            }
            else
            {
                row.Add(CreateStarButton(entry));
            }
            return row;
        }

        private static Button CreateStarButton(FavoriteEntry entry)
        {
            bool isFavorite = FavoriteAssetsStore.Contains(entry);
            Button button = new Button(() => FavoriteAssetsStore.Toggle(entry))
            {
                text = isFavorite ? "\u2605" : "\u2606",
                tooltip = isFavorite ? StarredText.RemoveFromFavorites : StarredText.AddToFavorites,
            };
            button.AddToClassList(AssetTrayRow.Classes.Action);
            button.AddToClassList(AssetTrayRow.Classes.ActionStar);
            if (isFavorite) button.AddToClassList(AssetTrayRow.Classes.ActionStarOn);
            return button;
        }

        private static void AppendStarMenuEntry(DropdownMenu menu, FavoriteEntry entry)
        {
            bool isFavorite = FavoriteAssetsStore.Contains(entry);
            menu.AppendAction(isFavorite ? StarredText.RemoveFromFavorites : StarredText.AddToFavorites,
                _ => FavoriteAssetsStore.Toggle(entry));
        }
    }
}
