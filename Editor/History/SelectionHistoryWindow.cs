namespace Kynesis.Starred.Editor
{
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    /// <summary>
    /// Auto-populated list of the last Project assets and scene GameObjects the
    /// user selected. Click the star to promote an entry into Favorites.
    /// </summary>
    public class SelectionHistoryWindow : EditorWindow, IHasCustomMenu
    {
        private VisualElement _list;
        private Label _emptyState;

        // Press state — set when a row is clicked, consumed on release to
        // apply Selection. No drag-out: history is read-only navigation.
        private bool _pressed;
        private Object _pressSelectTarget;

        [MenuItem("Tools/Starred/History")]
        public static void Open()
        {
            var window = GetWindow<SelectionHistoryWindow>();
            window.titleContent = new GUIContent("History", EditorGUIUtility.IconContent("d_UnityEditor.HistoryWindow").image);
            window.minSize = new Vector2(220, 200);
            window.Show();
        }

        private void OnEnable()
        {
            SelectionHistoryPreferences.Changed += Rebuild;
            FavoriteAssetsPreferences.Changed   += Rebuild;
            Selection.selectionChanged          += ApplyCurrentHighlight;
            AssetChangeNotifier.Changed         += Rebuild;
            EditorApplication.hierarchyChanged  += Rebuild;

            EditorSceneManager.sceneOpened                  += OnSceneOpened;
            EditorSceneManager.sceneClosed                  += OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            PrefabStage.prefabStageOpened  += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
        }

        private void OnDisable()
        {
            SelectionHistoryPreferences.Changed -= Rebuild;
            FavoriteAssetsPreferences.Changed   -= Rebuild;
            Selection.selectionChanged          -= ApplyCurrentHighlight;
            AssetChangeNotifier.Changed         -= Rebuild;
            EditorApplication.hierarchyChanged  -= Rebuild;

            EditorSceneManager.sceneOpened                  -= OnSceneOpened;
            EditorSceneManager.sceneClosed                  -= OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            PrefabStage.prefabStageOpened  -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            var currentMax = FavoriteAssetsSettings.MaxHistoryEntries;
            foreach (var choice in FavoriteAssetsSettings.MaxHistoryEntriesChoices)
            {
                var capturedChoice = choice;
                menu.AddItem(new GUIContent($"Max entries/{choice}"), currentMax == choice,
                    () => FavoriteAssetsSettings.MaxHistoryEntries = capturedChoice);
            }
            menu.AddSeparator("");

            if (SelectionHistoryPreferences.Entries.Count > 0)
                menu.AddItem(new GUIContent("Clear history"), false, SelectionHistoryPreferences.Clear);
            else
                menu.AddDisabledItem(new GUIContent("Clear history"));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Open Preferences…"), false,
                () => SettingsService.OpenUserPreferences(FavoriteAssetsSettings.SettingsPath));
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode _)  => Rebuild();
        private void OnSceneClosed(Scene scene)                   => Rebuild();
        private void OnActiveSceneChanged(Scene prev, Scene next) => Rebuild();
        private void OnPrefabStageChanged(PrefabStage stage)      => Rebuild();

        private void CreateGUI()
        {
            var uxml = AssetTrayPaths.Find<VisualTreeAsset>("SelectionHistoryWindow.uxml");
            var uss  = AssetTrayPaths.Find<StyleSheet>("AssetTray.uss");
            uxml.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(uss);

            _list       = rootVisualElement.Q<VisualElement>("list");
            _emptyState = rootVisualElement.Q<Label>("empty-state");

            rootVisualElement.pickingMode = PickingMode.Position;
            rootVisualElement.focusable = true;

            // Press / up handled at the root so pointer capture (which fails
            // silently during Unity's focus transition on both 2022 and 6)
            // isn't in the path. TrickleDown on Down/Up also keeps child
            // buttons from swallowing the events.
            rootVisualElement.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnRootPointerUp, TrickleDown.TrickleDown);

            RegisterImguiPressFallback();

            Rebuild();
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (focusedWindow != this) Focus();

            if (evt.button != 0) return;
            if (evt.target is Button) return;

            var row = FindRowAncestor(evt.target as VisualElement);
            if (row?.userData is not FavoriteEntry entry) return;

            var binding = BindFor(entry);
            if (binding == null) return;

            if (evt.clickCount == 2)
            {
                binding.OnDoubleClick();
                evt.StopPropagation();
                return;
            }

            _pressed = true;
            _pressSelectTarget = binding.SelectTarget;
        }

        // IMGUI reliably receives input on unfocused EditorWindows (that's how
        // Unity's Project / Hierarchy work on first click), so we use it as a
        // safety net for the initial press.
        private void RegisterImguiPressFallback()
        {
            var fallback = new IMGUIContainer(OnImguiPress);
            fallback.style.position = Position.Absolute;
            fallback.style.left     = 0;
            fallback.style.right    = 0;
            fallback.style.top      = 0;
            fallback.style.bottom   = 0;
            fallback.pickingMode    = PickingMode.Ignore;
            rootVisualElement.Insert(0, fallback);
        }

        private void OnImguiPress()
        {
            var evt = Event.current;
            if (evt == null) return;
            if (evt.type != EventType.MouseDown || evt.button != 0) return;

            if (_pressed) return;
            if (focusedWindow != this) Focus();

            var target = rootVisualElement.panel?.Pick(evt.mousePosition);
            if (target is Button) return;

            var row = FindRowAncestor(target);
            if (row?.userData is not FavoriteEntry entry) return;

            var binding = BindFor(entry);
            if (binding == null) return;

            if (evt.clickCount == 2)
            {
                binding.OnDoubleClick();
                return;
            }

            _pressed = true;
            _pressSelectTarget = binding.SelectTarget;
        }

        private static VisualElement FindRowAncestor(VisualElement element)
        {
            while (element != null && element.userData is not FavoriteEntry)
                element = element.parent;
            return element;
        }

        private sealed class RowBinding
        {
            public Object SelectTarget;
            public System.Action OnDoubleClick;
        }

        private static RowBinding BindFor(FavoriteEntry entry)
        {
            if (entry.IsAsset)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.Guid);
                var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) return null;
                return new RowBinding
                {
                    SelectTarget = asset,
                    OnDoubleClick = () => AssetDatabase.OpenAsset(asset),
                };
            }

            if (entry.IsSceneObject)
            {
                var go = SceneObjectResolver.Find(entry);
                if (go == null) return null;
                return new RowBinding
                {
                    SelectTarget = go,
                    OnDoubleClick = () => { SelectionHistoryTracker.Select(go); SceneView.FrameLastActiveSceneView(); },
                };
            }

            return null;
        }

        private void OnRootPointerUp(PointerUpEvent evt)
        {
            if (!_pressed) return;
            // Ignore the synthetic release UITK fires during focus transitions
            // (see the matching guard in FavoriteAssetsWindow).
            if ((evt.pressedButtons & 1) != 0) return;
            if (evt.button != 0) return;

            // Select on release — Selection.selectionChanged on press would
            // repaint mid-click and feel jumpy.
            if (_pressSelectTarget != null) SelectionHistoryTracker.Select(_pressSelectTarget);

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

            var renderedCount = 0;
            foreach (var entry in SelectionHistoryPreferences.Entries)
            {
                var row = CreateRow(entry);
                if (row == null) continue;  // scene entry whose context isn't active
                _list.Add(row);
                renderedCount++;
            }

            _emptyState.style.display = renderedCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            ApplyCurrentHighlight();
        }

        private VisualElement CreateRow(FavoriteEntry entry)
        {
            return entry.IsAsset ? CreateAssetRow(entry)
                 : entry.IsSceneObject ? CreateSceneObjectRow(entry)
                 : null;
        }

        private VisualElement CreateAssetRow(FavoriteEntry entry)
        {
            var row = AssetTrayRow.CreateAssetRow(entry.Guid, out var asset, out var path, userData: entry);
            if (asset == null) return row;

            row.Add(AssetTrayRow.CreatePingButton(() =>
            {
                EditorGUIUtility.PingObject(asset);
                SelectionHistoryTracker.Select(asset);
            }));
            row.Add(CreateStarButton(entry));

            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                AppendStarMenuEntry(evt.menu, entry);
                evt.menu.AppendSeparator("");
                AssetTrayRow.AppendAssetContextMenu(evt.menu, asset, entry.Guid, path);
            }));
            return row;
        }

        private VisualElement CreateSceneObjectRow(FavoriteEntry entry)
        {
            var row = AssetTrayRow.CreateSceneObjectRow(entry, out var go);
            if (row == null) return null;

            if (go != null)
            {
                row.Add(AssetTrayRow.CreatePingButton(() =>
                {
                    EditorGUIUtility.PingObject(go);
                    SelectionHistoryTracker.Select(go);
                }));
                row.Add(CreateStarButton(entry));

                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    AppendStarMenuEntry(evt.menu, entry);
                    evt.menu.AppendSeparator("");
                    evt.menu.AppendAction("Show in Hierarchy", _ =>
                    {
                        EditorGUIUtility.PingObject(go);
                        SelectionHistoryTracker.Select(go);
                    });
                    evt.menu.AppendAction("Frame in Scene View", _ =>
                    {
                        SelectionHistoryTracker.Select(go);
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
            var isFav = FavoriteAssetsPreferences.Contains(entry);
            var btn = new Button(() => FavoriteAssetsPreferences.Toggle(entry))
            {
                text = isFav ? "\u2605" : "\u2606",
                tooltip = isFav ? "Remove from Favorites" : "Add to Favorites",
            };
            btn.AddToClassList(AssetTrayRow.Classes.Action);
            btn.AddToClassList("assettray-row-action--star");
            if (isFav) btn.AddToClassList("assettray-row-action--star-on");
            return btn;
        }

        private static void AppendStarMenuEntry(DropdownMenu menu, FavoriteEntry entry)
        {
            var isFav = FavoriteAssetsPreferences.Contains(entry);
            menu.AppendAction(isFav ? "Remove from Favorites" : "Add to Favorites",
                _ => FavoriteAssetsPreferences.Toggle(entry));
        }

        private void ApplyCurrentHighlight()
        {
            var selectedGuid = AssetTrayRow.GetCurrentSelectionGuid();
            var selectedGo   = Selection.activeGameObject;
            // Compute the selection's GlobalObjectId once — GetGlobalObjectIdSlow
            // is a slow call (per its name), and the highlight predicate runs
            // for every visible row.
            var selectedGoId = selectedGo != null ? SceneObjectResolver.GetGlobalObjectId(selectedGo) : null;

            AssetTrayRow.ApplyCurrentHighlight(_list, data =>
            {
                if (data is not FavoriteEntry entry) return false;
                if (entry.IsAsset) return entry.Guid == selectedGuid;
                if (entry.IsSceneObject && !string.IsNullOrEmpty(selectedGoId))
                    return entry.GlobalObjectId == selectedGoId;
                return false;
            });
        }
    }
}
