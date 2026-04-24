namespace Kynesis.Starred.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    /// <summary>
    /// An asset tray. Drag assets from the Project window or GameObjects from
    /// the Hierarchy / Prefab Stage. Scene-bound entries render only when the
    /// owning scene or prefab is open.
    /// </summary>
    public class FavoriteAssetsWindow : EditorWindow, IHasCustomMenu
    {
        private const float DragStartDistance = 6f;

        private VisualElement _list;
        private VisualElement _listContainer;
        private Label _emptyState;
        private VisualElement _insertMarker;

        private FavoriteEntry _pressedEntry;
        private Vector2 _mouseDownPos;
        private bool _reordering;
        private int _dropIndex = -1;

        [MenuItem("Tools/Starred/Favorites")]
        public static void Open()
        {
            var window = GetWindow<FavoriteAssetsWindow>();
            window.titleContent = new GUIContent("Favorites", EditorGUIUtility.IconContent("d_Favorite").image);
            window.minSize = new Vector2(220, 200);
            window.Show();
        }

        private void OnEnable()
        {
            FavoriteAssetsPreferences.Changed += Rebuild;
            Selection.selectionChanged        += ApplyCurrentHighlight;
            AssetChangeNotifier.Changed       += Rebuild;
            EditorApplication.hierarchyChanged += Rebuild;

            EditorSceneManager.sceneOpened           += OnSceneOpened;
            EditorSceneManager.sceneClosed           += OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            PrefabStage.prefabStageOpened  += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
        }

        private void OnDisable()
        {
            FavoriteAssetsPreferences.Changed -= Rebuild;
            Selection.selectionChanged        -= ApplyCurrentHighlight;
            AssetChangeNotifier.Changed       -= Rebuild;
            EditorApplication.hierarchyChanged -= Rebuild;

            EditorSceneManager.sceneOpened           -= OnSceneOpened;
            EditorSceneManager.sceneClosed           -= OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            PrefabStage.prefabStageOpened  -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Show star in Project"), FavoriteAssetsSettings.ShowProjectWindowStar,
                () => FavoriteAssetsSettings.ShowProjectWindowStar = !FavoriteAssetsSettings.ShowProjectWindowStar);
            menu.AddItem(new GUIContent("Show star in Hierarchy"), FavoriteAssetsSettings.ShowHierarchyStar,
                () => FavoriteAssetsSettings.ShowHierarchyStar = !FavoriteAssetsSettings.ShowHierarchyStar);
            menu.AddSeparator("");

            if (FavoriteAssetsPreferences.Entries.Count > 0)
                menu.AddItem(new GUIContent("Clear all favorites…"), false, PromptClearFavorites);
            else
                menu.AddDisabledItem(new GUIContent("Clear all favorites…"));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Open Preferences…"), false,
                () => SettingsService.OpenUserPreferences(FavoriteAssetsSettings.SettingsPath));
        }

        private static void PromptClearFavorites()
        {
            if (EditorUtility.DisplayDialog(
                    "Clear all favorites",
                    $"Remove all {FavoriteAssetsPreferences.Entries.Count} favorited entries? This cannot be undone.",
                    "Clear", "Cancel"))
            {
                FavoriteAssetsPreferences.Clear();
            }
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode _)   => Rebuild();
        private void OnSceneClosed(Scene scene)                    => Rebuild();
        private void OnActiveSceneChanged(Scene prev, Scene next)  => Rebuild();
        private void OnPrefabStageChanged(PrefabStage stage)       => Rebuild();

        private void CreateGUI()
        {
            var uxml = AssetTrayPaths.Find<VisualTreeAsset>("FavoriteAssetsWindow.uxml");
            var uss  = AssetTrayPaths.Find<StyleSheet>("AssetTray.uss");
            uxml.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(uss);

            _list          = rootVisualElement.Q<VisualElement>("list");
            _listContainer = rootVisualElement.Q<VisualElement>("root");
            _emptyState    = rootVisualElement.Q<Label>("empty-state");

            _insertMarker = new VisualElement();
            _insertMarker.AddToClassList("assettray-insert-marker");
            _insertMarker.style.position = Position.Absolute;
            _insertMarker.style.display = DisplayStyle.None;
            _insertMarker.pickingMode = PickingMode.Ignore;
            _list.Add(_insertMarker);

            RegisterDropZone(rootVisualElement);
            RegisterImguiDropFallback();
            Rebuild();
        }

        /// <summary>
        /// UITK DragEvents can be inconsistent for Hierarchy drags across Unity
        /// versions. This IMGUI container sits invisibly behind the window and
        /// catches the legacy DragAndDrop events as a safety net.
        /// </summary>
        private void RegisterImguiDropFallback()
        {
            var fallback = new IMGUIContainer(OnImguiDrop);
            fallback.style.position = Position.Absolute;
            fallback.style.left     = 0;
            fallback.style.right    = 0;
            fallback.style.top      = 0;
            fallback.style.bottom   = 0;
            fallback.pickingMode    = PickingMode.Ignore;
            rootVisualElement.Insert(0, fallback);
        }

        private void OnImguiDrop()
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!HasAnySupportedItemInDrag()) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                FavoriteAssetsPreferences.AddRange(DraggedEntries());
                evt.Use();
            }
        }

        // ---------- Rendering ----------

        private void Rebuild()
        {
            if (_list == null) return;
            _list.Clear();

            // Contextual (scene / prefab-stage) favorites float to the top so
            // they're visible while you're editing them. Asset favorites follow.
            var renderedCount = 0;
            foreach (var entry in FavoriteAssetsPreferences.Entries)
            {
                if (!entry.IsSceneObject) continue;
                var row = CreateSceneObjectRow(entry);
                if (row == null) continue;
                _list.Add(row);
                renderedCount++;
            }

            foreach (var entry in FavoriteAssetsPreferences.Entries)
            {
                if (!entry.IsAsset) continue;
                var row = CreateAssetRow(entry);
                if (row == null) continue;
                _list.Add(row);
                renderedCount++;
            }

            _emptyState.style.display = renderedCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            _list.Add(_insertMarker);
            _insertMarker.style.display = DisplayStyle.None;

            ApplyCurrentHighlight();
        }

        private VisualElement CreateAssetRow(FavoriteEntry entry)
        {
            var row = AssetTrayRow.CreateAssetRow(entry.Guid, out var asset, out var path, userData: entry);

            if (asset != null)
            {
                row.Add(AssetTrayRow.CreatePingButton(asset));
                row.Add(CreateRemoveButton(entry));
                WireAssetDrag(row, entry, asset);
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    AssetTrayRow.AppendAssetContextMenu(evt.menu, asset, entry.Guid, path);
                    evt.menu.AppendSeparator("");
                    evt.menu.AppendAction("Remove from Favorites", _ => FavoriteAssetsPreferences.RemoveEntry(entry));
                }));
            }
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
                    Selection.activeGameObject = go;
                }));
                row.Add(CreateRemoveButton(entry));
                WireSceneObjectDrag(row, entry, go);
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
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
                    evt.menu.AppendSeparator("");
                    evt.menu.AppendAction("Copy Hierarchy Path", _ => EditorGUIUtility.systemCopyBuffer = entry.HierarchyPath);
                    evt.menu.AppendSeparator("");
                    evt.menu.AppendAction("Remove from Favorites", _ => FavoriteAssetsPreferences.RemoveEntry(entry));
                }));
            }
            else
            {
                row.Add(CreateRemoveButton(entry));
            }

            return row;
        }

        private static Button CreateRemoveButton(FavoriteEntry entry)
        {
            var btn = new Button(() => FavoriteAssetsPreferences.RemoveEntry(entry))
            {
                text = "\u00D7",
                tooltip = "Remove from favorites",
            };
            btn.AddToClassList(AssetTrayRow.Classes.Action);
            btn.AddToClassList("assettray-row-action--remove");
            return btn;
        }

        // ---------- Drag from rows ----------

        private void WireAssetDrag(VisualElement row, FavoriteEntry entry, Object asset)
        {
            WireDragBehaviour(row, entry, asset,
                onDoubleClick:  () => AssetDatabase.OpenAsset(asset),
                onStartDragOut: () => StartDragOutAsset(asset));
        }

        private void WireSceneObjectDrag(VisualElement row, FavoriteEntry entry, GameObject go)
        {
            WireDragBehaviour(row, entry, go,
                onDoubleClick:  () => { Selection.activeGameObject = go; SceneView.FrameLastActiveSceneView(); },
                onStartDragOut: () => StartDragOutObject(go));
        }

        private void WireDragBehaviour(VisualElement row, FavoriteEntry entry, Object selectTarget, System.Action onDoubleClick, System.Action onStartDragOut)
        {
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.target is Button) return;
                if (evt.button != 0) return;

                if (evt.clickCount == 2)
                {
                    onDoubleClick();
                    evt.StopPropagation();
                    return;
                }

                // Single-click selects the target so it's inspected — clicking a
                // row and seeing nothing happen feels broken.
                if (selectTarget != null) Selection.activeObject = selectTarget;

                _pressedEntry = entry;
                _mouseDownPos = evt.mousePosition;
                row.CaptureMouse();
            });

            row.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (_pressedEntry != entry) return;

                if (!_reordering)
                {
                    if ((evt.mousePosition - _mouseDownPos).sqrMagnitude < DragStartDistance * DragStartDistance) return;

                    if (_list.worldBound.Contains(evt.mousePosition))
                    {
                        BeginReorder(row);
                    }
                    else
                    {
                        EndPress(row);
                        onStartDragOut();
                        return;
                    }
                }

                UpdateDropIndex(evt.mousePosition);
            });

            row.RegisterCallback<MouseUpEvent>(_ =>
            {
                if (_pressedEntry != entry) return;
                if (_reordering && _dropIndex >= 0)
                {
                    var fromIndex = FindEntryIndex(entry);
                    if (fromIndex >= 0) FavoriteAssetsPreferences.Move(fromIndex, _dropIndex);
                }
                EndPress(row);
            });
        }

        private void BeginReorder(VisualElement row)
        {
            _reordering = true;
            row.AddToClassList("assettray-row--dragging");
            _insertMarker.style.display = DisplayStyle.Flex;
        }

        private void EndPress(VisualElement row)
        {
            row.RemoveFromClassList("assettray-row--dragging");
            if (row.HasMouseCapture()) row.ReleaseMouse();
            _pressedEntry = null;
            _reordering = false;
            _dropIndex = -1;
            _insertMarker.style.display = DisplayStyle.None;
        }

        private void UpdateDropIndex(Vector2 mousePosition)
        {
            var localY = _list.WorldToLocal(mousePosition).y;
            var rows = CollectRows();

            var visibleIndex = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var mid = rows[i].layout.y + rows[i].layout.height * 0.5f;
                if (localY < mid) { visibleIndex = i; break; }
            }

            _dropIndex = VisibleIndexToEntryIndex(visibleIndex, rows);

            var markerY = visibleIndex < rows.Count
                ? rows[visibleIndex].layout.y
                : (rows.Count > 0 ? rows[rows.Count - 1].layout.yMax : 0f);

            _insertMarker.style.display = DisplayStyle.Flex;
            _insertMarker.style.top = markerY - 1f;
            _insertMarker.style.left = rows.Count > 0 ? rows[0].layout.x : 0f;
            _insertMarker.style.width = rows.Count > 0 ? rows[0].layout.width : _list.layout.width;
        }

        private static int VisibleIndexToEntryIndex(int visibleIndex, List<VisualElement> rows)
        {
            // Map a drop position in the visible row list back to the underlying
            // Entries index — some entries (unloaded scenes) may be hidden.
            var total = FavoriteAssetsPreferences.Entries.Count;
            if (visibleIndex >= rows.Count) return total;

            if (rows[visibleIndex].userData is not FavoriteEntry targetEntry) return total;
            return FindEntryIndex(targetEntry);
        }

        private static int FindEntryIndex(FavoriteEntry entry)
        {
            var entries = FavoriteAssetsPreferences.Entries;
            for (var i = 0; i < entries.Count; i++)
                if (entries[i] == entry) return i;
            return -1;
        }

        private List<VisualElement> CollectRows()
        {
            // Rows have a FavoriteEntry in userData; separators and the insert
            // marker don't, so filtering on userData picks real rows only.
            var rows = new List<VisualElement>();
            foreach (var child in _list.Children())
                if (child != _insertMarker && child.userData != null) rows.Add(child);
            return rows;
        }

        private static void StartDragOutAsset(Object asset)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { asset };
            DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(asset) };
            DragAndDrop.StartDrag(asset.name);
        }

        private static void StartDragOutObject(Object obj)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { obj };
            DragAndDrop.paths = System.Array.Empty<string>();
            DragAndDrop.StartDrag(obj.name);
        }

        // ---------- Selection highlight ----------

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

        // ---------- Drop zone (from Project window / Hierarchy) ----------

        private void RegisterDropZone(VisualElement zone)
        {
            zone.RegisterCallback<DragEnterEvent>(_ => zone.AddToClassList("assettray-list--drag-over"));
            zone.RegisterCallback<DragLeaveEvent>(_ => zone.RemoveFromClassList("assettray-list--drag-over"));
            zone.RegisterCallback<DragExitedEvent>(_ => zone.RemoveFromClassList("assettray-list--drag-over"));

            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = HasAnySupportedItemInDrag() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
            });

            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();
                FavoriteAssetsPreferences.AddRange(DraggedEntries());
                zone.RemoveFromClassList("assettray-list--drag-over");
                evt.StopPropagation();
            });
        }

        private static bool HasAnySupportedItemInDrag()
        {
            // Be permissive: some drag sources populate objectReferences only on
            // DragPerform, not during DragUpdated. Better to show a drop cursor
            // and filter in DraggedEntries than to silently reject.
            return (DragAndDrop.paths?.Length ?? 0) > 0
                || (DragAndDrop.objectReferences?.Length ?? 0) > 0;
        }

        private static IEnumerable<FavoriteEntry> DraggedEntries()
        {
            var seen = new HashSet<string>();

            // Project assets (drag from Project window).
            foreach (var path in DragAndDrop.paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;

                var entry = FavoriteEntry.ForAsset(guid);
                if (seen.Add(entry.LookupKey)) yield return entry;
            }

            // Scene / prefab-stage GameObjects (drag from Hierarchy).
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is not GameObject go || EditorUtility.IsPersistent(go)) continue;

                var scenePath = SceneObjectResolver.GetScenePath(go);
                if (string.IsNullOrEmpty(scenePath))
                {
                    Debug.LogWarning($"[FavoriteAssets] Can't favorite '{go.name}' — save its scene first.");
                    continue;
                }

                var entry = FavoriteEntry.ForSceneObject(scenePath, SceneObjectResolver.GetHierarchyPath(go));
                if (seen.Add(entry.LookupKey)) yield return entry;
            }
        }
    }
}
