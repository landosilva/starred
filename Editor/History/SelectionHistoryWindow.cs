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

            Rebuild();
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

        private static VisualElement CreateRow(FavoriteEntry entry)
        {
            return entry.IsAsset ? CreateAssetRow(entry)
                 : entry.IsSceneObject ? CreateSceneObjectRow(entry)
                 : null;
        }

        private static VisualElement CreateAssetRow(FavoriteEntry entry)
        {
            var row = AssetTrayRow.CreateAssetRow(entry.Guid, out var asset, out var path, userData: entry);
            if (asset == null) return row;

            row.Add(AssetTrayRow.CreatePingButton(asset));
            row.Add(CreateStarButton(entry));
            WireAssetInteractions(row, asset);

            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                AppendStarMenuEntry(evt.menu, entry);
                evt.menu.AppendSeparator("");
                AssetTrayRow.AppendAssetContextMenu(evt.menu, asset, entry.Guid, path);
            }));
            return row;
        }

        private static VisualElement CreateSceneObjectRow(FavoriteEntry entry)
        {
            var row = AssetTrayRow.CreateSceneObjectRow(entry, out var go);
            if (row == null) return null;

            if (go != null)
            {
                row.Add(AssetTrayRow.CreatePingButton(() =>
                {
                    EditorGUIUtility.PingObject(go);
                    Selection.activeGameObject = go;
                }));
                row.Add(CreateStarButton(entry));
                WireSceneObjectInteractions(row, go);

                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    AppendStarMenuEntry(evt.menu, entry);
                    evt.menu.AppendSeparator("");
                    evt.menu.AppendAction("Show in Hierarchy", _ =>
                    {
                        EditorGUIUtility.PingObject(go);
                        Selection.activeGameObject = go;
                    });
                    evt.menu.AppendAction("Frame in Scene View", _ =>
                    {
                        Selection.activeGameObject = go;
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

        private static void WireAssetInteractions(VisualElement row, Object asset)
        {
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target is Button) return;
                if (evt.button != 0) return;

                if (evt.clickCount == 2)
                {
                    AssetDatabase.OpenAsset(asset);
                    evt.StopPropagation();
                    return;
                }

                Selection.activeObject = asset;
            });
        }

        private static void WireSceneObjectInteractions(VisualElement row, GameObject go)
        {
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target is Button) return;
                if (evt.button != 0) return;

                if (evt.clickCount == 2)
                {
                    Selection.activeGameObject = go;
                    SceneView.FrameLastActiveSceneView();
                    evt.StopPropagation();
                    return;
                }

                Selection.activeGameObject = go;
            });
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

            AssetTrayRow.ApplyCurrentHighlight(_list, data =>
            {
                if (data is not FavoriteEntry entry) return false;
                if (entry.IsAsset) return entry.Guid == selectedGuid;
                if (entry.IsSceneObject && selectedGo != null)
                    return SceneObjectResolver.GetScenePath(selectedGo) == entry.ScenePath
                        && SceneObjectResolver.GetHierarchyPath(selectedGo) == entry.HierarchyPath;
                return false;
            });
        }
    }
}
