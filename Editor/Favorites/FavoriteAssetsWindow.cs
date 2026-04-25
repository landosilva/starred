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
        private Label _emptyState;
        private VisualElement _insertMarker;
        private VisualElement _forbiddenOverlay;
        private Label _forbiddenOverlayLabel;
        private VisualElement _addOverlay;
        private Label _addOverlayLabel;
        private VisualElement _dragGhost;
        private Image _dragGhostIcon;
        private Label _dragGhostLabel;

        private FavoriteEntry _pressedEntry;
        private VisualElement _pressedRow;
        private System.Action _pressedStartDragOut;
        private Object _pressedSelectTarget;
        private Vector2 _mouseDownPos;
        private bool _reordering;
        private bool _dropForbidden;
        private int _dropIndex = -1;

        // Set when the user drags one of our own rows past the window edge.
        // If the native drag re-enters the window we resume reorder mode on it
        // instead of treating the drop as a duplicate "add".
        private FavoriteEntry _draggingOwnEntry;
        // The object we placed into DragAndDrop when the own-drag started —
        // used to verify on re-entry that the active drag is still ours and
        // not a fresh external drag (e.g. user picked up something different
        // from the Project window after a rejected drop).
        private Object _draggingOwnPayload;

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

            _list       = rootVisualElement.Q<VisualElement>("list");
            _emptyState = rootVisualElement.Q<Label>("empty-state");

            _forbiddenOverlay = new VisualElement();
            _forbiddenOverlay.AddToClassList("assettray-forbidden-overlay");
            _forbiddenOverlay.style.position = Position.Absolute;
            _forbiddenOverlay.style.display = DisplayStyle.None;
            _forbiddenOverlay.pickingMode = PickingMode.Ignore;
            _forbiddenOverlayLabel = new Label();
            _forbiddenOverlayLabel.AddToClassList("assettray-forbidden-overlay-label");
            _forbiddenOverlay.Add(_forbiddenOverlayLabel);
            _list.Add(_forbiddenOverlay);

            _insertMarker = new VisualElement();
            _insertMarker.AddToClassList("assettray-insert-marker");
            _insertMarker.style.position = Position.Absolute;
            _insertMarker.style.display = DisplayStyle.None;
            _insertMarker.pickingMode = PickingMode.Ignore;
            _list.Add(_insertMarker);

            _addOverlay = new VisualElement();
            _addOverlay.AddToClassList("assettray-add-overlay");
            _addOverlay.style.left = 4;
            _addOverlay.style.right = 4;
            _addOverlay.style.top = 4;
            _addOverlay.style.bottom = 4;
            _addOverlay.style.display = DisplayStyle.None;
            _addOverlay.pickingMode = PickingMode.Ignore;
            _addOverlayLabel = new Label();
            _addOverlayLabel.AddToClassList("assettray-add-overlay-label");
            _addOverlay.Add(_addOverlayLabel);
            rootVisualElement.Add(_addOverlay);

            _dragGhost = new VisualElement();
            _dragGhost.AddToClassList("assettray-drag-ghost");
            _dragGhost.style.display = DisplayStyle.None;
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhostIcon = new Image { scaleMode = ScaleMode.ScaleToFit };
            _dragGhostIcon.AddToClassList("assettray-drag-ghost-icon");
            _dragGhost.Add(_dragGhostIcon);
            _dragGhostLabel = new Label();
            _dragGhostLabel.AddToClassList("assettray-drag-ghost-label");
            _dragGhost.Add(_dragGhostLabel);
            rootVisualElement.Add(_dragGhost);

            RegisterDropZone(rootVisualElement);
            RegisterImguiDropFallback();
            RegisterRootDragHandlers();
            Rebuild();
        }

        // Pointer capture on a nested row silently fails during Unity's focus
        // transition, so a click on an unfocused window never starts a drag.
        // We bypass that by handling move / up / leave at the window root —
        // those events fire regardless of capture state — and just remember
        // which row was pressed.
        private void RegisterRootDragHandlers()
        {
            rootVisualElement.pickingMode = PickingMode.Position;
            rootVisualElement.focusable = true;

            // Detect the press at the root in TrickleDown so the first click on
            // an unfocused window is handled even if UITK drops the row-level
            // event during the focus transition. Same reason Unity's IMGUI
            // windows (Project, Hierarchy) work on first click.
            rootVisualElement.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
            // PointerUp in TrickleDown so child buttons calling StopPropagation
            // can't leave the window with stale _pressedEntry.
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnRootPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<MouseLeaveEvent>(OnRootMouseLeave);
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

            _pressedEntry = entry;
            _pressedRow = row;
            _pressedStartDragOut = binding.OnStartDragOut;
            _pressedSelectTarget = binding.SelectTarget;
            _mouseDownPos = evt.position;
        }

        private sealed class RowBinding
        {
            public Object SelectTarget;
            public System.Action OnDoubleClick;
            public System.Action OnStartDragOut;
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
                    OnStartDragOut = () => AssetTrayRow.StartDragOutAsset(asset),
                };
            }

            if (entry.IsSceneObject)
            {
                var go = SceneObjectResolver.Find(entry.ScenePath, entry.HierarchyPath);
                if (go == null) return null;
                return new RowBinding
                {
                    SelectTarget = go,
                    OnDoubleClick = () => { Selection.activeGameObject = go; SceneView.FrameLastActiveSceneView(); },
                    // Contextual favorites only support reorder — there's no
                    // meaningful "instantiate" for an existing scene object.
                    OnStartDragOut = null,
                };
            }

            return null;
        }

        private static VisualElement FindRowAncestor(VisualElement element)
        {
            while (element != null && element.userData is not FavoriteEntry)
                element = element.parent;
            return element;
        }

        private void OnRootPointerMove(PointerMoveEvent evt)
        {
            if (_pressedEntry == null) return;

            // Stale press detection. The left-button bit is clear whenever the
            // mouse isn't physically held — that means we missed a release
            // somewhere (synthetic PointerUp skipped, focus bounce, etc.) and
            // any ongoing move would otherwise be misread as reorder.
            if ((evt.pressedButtons & 1) == 0)
            {
                EndPress(_pressedRow);
                return;
            }

            // External DragAndDrop in flight — DragEnter usually already
            // cleared press state, but event ordering across Unity versions
            // isn't strict, so guard here too.
            if (_draggingOwnEntry == null && HasAnySupportedItemInDrag())
            {
                EndPress(_pressedRow);
                return;
            }

            var pos = (Vector2)evt.position;
            if ((pos - _mouseDownPos).sqrMagnitude < DragStartDistance * DragStartDistance) return;

            // Cursor is still inside the window — reorder mode.
            if (!_reordering && _pressedRow != null) BeginReorder(_pressedRow);
            UpdateDropIndex(pos);
        }

        private void OnRootMouseLeave(MouseLeaveEvent evt)
        {
            if (_pressedEntry == null) return;
            if ((evt.mousePosition - _mouseDownPos).sqrMagnitude < DragStartDistance * DragStartDistance) return;

            if (_pressedStartDragOut == null)
            {
                // Contextual entry dragged outside — nothing to instantiate.
                // Cancel the gesture so state doesn't leak.
                EndPress(_pressedRow);
                return;
            }

            // Hand off to a native DragAndDrop. Remember the entry so a re-entry
            // resumes reorder rather than adding a duplicate.
            _draggingOwnEntry = _pressedEntry;
            _draggingOwnPayload = _pressedSelectTarget;
            var startDragOut = _pressedStartDragOut;
            EndPress(_pressedRow);
            startDragOut();
            // Mouse just left — overlay should be gone. It re-appears via
            // DragEnter if the user comes back into the window with the same
            // drag payload.
        }

        // Returns true if the DragAndDrop currently carries the payload we
        // started — i.e. the active drag is still ours, not a different one
        // started after our drag ended without a clean DragExited (e.g. a
        // rejected drop on the Scene View).
        private bool IsOurDragStillActive()
        {
            if (_draggingOwnPayload == null) return false;
            var refs = DragAndDrop.objectReferences;
            if (refs == null) return false;
            foreach (var r in refs)
                if (r == _draggingOwnPayload) return true;
            return false;
        }

        private void OnRootPointerUp(PointerUpEvent evt)
        {
            if (_pressedEntry == null) return;
            // Synthetic PointerUp from the focus transition still reports the
            // left-button bit as pressed — a real release on left reports
            // pressedButtons = 0 for that bit. Ignore the synthetic one.
            if ((evt.pressedButtons & 1) != 0) return;
            if (evt.button != 0) return;

            // Only act if the pressed row is still live — Rebuild mid-drag may
            // have destroyed it, in which case we just clear state.
            var rowIsLive = _pressedRow != null && _pressedRow.parent == _list;
            if (rowIsLive && _reordering && _dropIndex >= 0 && !_dropForbidden)
            {
                var fromIndex = FindEntryIndex(_pressedEntry);
                if (fromIndex >= 0) FavoriteAssetsPreferences.Move(fromIndex, _dropIndex);
            }
            else if (!_reordering && _pressedSelectTarget != null)
            {
                // Release without drag — this is a plain click. Select now
                // rather than on press, so Selection.selectionChanged doesn't
                // repaint and break the event stream mid-gesture.
                Selection.activeObject = _pressedSelectTarget;
            }
            EndPress(_pressedRow);
        }

        /// <summary>
        /// An invisible IMGUIContainer sits behind the UITK tree as an
        /// event-level safety net. IMGUI reliably receives input on unfocused
        /// EditorWindows (that's how Unity's own Project / Hierarchy windows
        /// let you click-and-drag without a prior focus click), so we use it
        /// for the initial press and for external drag-and-drop payloads that
        /// UITK's DragEvents don't always deliver.
        /// </summary>
        private void RegisterImguiDropFallback()
        {
            var fallback = new IMGUIContainer(OnImguiEvents);
            fallback.style.position = Position.Absolute;
            fallback.style.left     = 0;
            fallback.style.right    = 0;
            fallback.style.top      = 0;
            fallback.style.bottom   = 0;
            fallback.pickingMode    = PickingMode.Ignore;
            rootVisualElement.Insert(0, fallback);
        }

        private void OnImguiEvents()
        {
            var evt = Event.current;
            if (evt == null) return;

            switch (evt.type)
            {
                case EventType.MouseDown when evt.button == 0:
                    HandleImguiPress(evt);
                    break;
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    HandleImguiDrop(evt);
                    break;
                // Note: not handling IMGUI EventType.DragExited here. Some
                // Unity versions also fire it whenever the drag leaves the
                // window (not only at drag end), which would clear
                // _draggingOwnEntry mid-drag and break re-entry. Stale own-
                // drag state is corrected on the next DragEnter via the
                // payload-identity check (IsOurDragStillActive).
            }
        }

        private void HandleImguiPress(Event evt)
        {
            // If UITK already delivered a PointerDown for this click, skip —
            // this IMGUI path is a fallback for the unfocused-window case.
            if (_pressedEntry != null) return;
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

            _pressedEntry = entry;
            _pressedRow = row;
            _pressedStartDragOut = binding.OnStartDragOut;
            _pressedSelectTarget = binding.SelectTarget;
            _mouseDownPos = evt.mousePosition;
        }

        private void HandleImguiDrop(Event evt)
        {
            if (!HasAnySupportedItemInDrag()) return;

            // External drag is in-flight — scrub any lingering press so a stale
            // _pressedEntry can't be misread as reorder in a concurrent
            // PointerMove.
            if (_pressedEntry != null && _draggingOwnEntry == null)
                EndPress(_pressedRow);

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
            // Rows are about to be destroyed — any in-flight reorder whose
            // MouseUp hasn't fired would otherwise leak state (press, cursor,
            // insert marker) onto the freshly rebuilt rows.
            ResetDragState();

            if (_list == null) return;
            _list.Clear();

            // Contextual (scene / prefab-stage) favorites float to the top so
            // they're visible while you're editing them. Asset favorites follow.
            var sceneCount = 0;
            foreach (var entry in FavoriteAssetsPreferences.Entries)
            {
                if (!entry.IsSceneObject) continue;
                var row = CreateSceneObjectRow(entry);
                if (row == null) continue;
                _list.Add(row);
                sceneCount++;
            }

            var renderedCount = sceneCount;
            foreach (var entry in FavoriteAssetsPreferences.Entries)
            {
                if (!entry.IsAsset) continue;
                var row = CreateAssetRow(entry);
                if (row == null) continue;
                _list.Add(row);
                renderedCount++;
            }

            // Visible separator between the two blocks so the "contextual on
            // top, assets below" rule is obvious even outside a reorder.
            var hasBothBlocks = sceneCount > 0 && renderedCount > sceneCount;
            if (hasBothBlocks)
            {
                var separator = new VisualElement();
                separator.AddToClassList("assettray-separator");
                separator.pickingMode = PickingMode.Ignore;
                _list.Insert(sceneCount, separator);
            }

            _emptyState.style.display = renderedCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // Re-attach overlay + marker that were cleared with the list.
            _list.Add(_forbiddenOverlay);
            _forbiddenOverlay.style.display = DisplayStyle.None;
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

        // UITK's Cursor.defaultCursorId is internal, so we poke it via reflection
        // to let USS-style code request a built-in Editor cursor by enum.
        private static System.Reflection.FieldInfo _defaultCursorIdField;

        private static StyleCursor BuildCursor(MouseCursor cursor)
        {
            var cursorType = typeof(UnityEngine.UIElements.Cursor);
            _defaultCursorIdField ??= cursorType.GetField("m_DefaultCursorId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? cursorType.GetField("defaultCursorId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            object boxed = new UnityEngine.UIElements.Cursor();
            _defaultCursorIdField?.SetValue(boxed, (int)cursor);
            return new StyleCursor((UnityEngine.UIElements.Cursor)boxed);
        }

        private void BeginReorder(VisualElement row)
        {
            _reordering = true;
            row.AddToClassList("assettray-row--dragging");
            _insertMarker.style.display = DisplayStyle.Flex;
            rootVisualElement.style.cursor = BuildCursor(MouseCursor.MoveArrow);

            var dragged = _pressedEntry ?? _draggingOwnEntry;
            ShowForbiddenOverlay(dragged);
            ShowDragGhost(row, dragged);
        }

        private void ShowDragGhost(VisualElement row, FavoriteEntry entry)
        {
            if (_dragGhost == null || row == null) return;

            // Mirror the row's icon + visible label so the ghost reads the
            // same as what the user clicked on.
            Texture icon = null;
            string label = null;
            foreach (var child in row.Children())
            {
                if (child is Image img && icon == null) icon = img.image;
                else if (child is Label l && label == null) label = l.text;
            }

            _dragGhostIcon.image = icon;
            _dragGhostLabel.text = label ?? "";
            _dragGhost.style.display = DisplayStyle.Flex;
        }

        private void PositionDragGhost(Vector2 mousePosition)
        {
            if (_dragGhost == null || _dragGhost.style.display == DisplayStyle.None) return;
            var local = rootVisualElement.WorldToLocal(mousePosition);
            _dragGhost.style.left = local.x + 12f;
            _dragGhost.style.top  = local.y + 8f;
        }

        private void ShowForbiddenOverlay(FavoriteEntry dragged)
        {
            if (_forbiddenOverlay == null || dragged == null) return;

            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (var child in _list.Children())
            {
                if (child.userData is not FavoriteEntry e) continue;
                var forbidden = dragged.IsSceneObject ? e.IsAsset : e.IsSceneObject;
                if (!forbidden) continue;
                if (child.layout.y < minY) minY = child.layout.y;
                if (child.layout.yMax > maxY) maxY = child.layout.yMax;
            }

            if (float.IsPositiveInfinity(minY))
            {
                _forbiddenOverlay.style.display = DisplayStyle.None;
                return;
            }

            // When dragging an asset entry the forbidden block is the scene
            // block above it — extend the overlay down so it visually
            // swallows the separator line. Going the other way, the overlay
            // sits flush with the first asset row (no extra reach needed).
            if (dragged.IsAsset) maxY += 10f;

            _forbiddenOverlayLabel.text = dragged.IsSceneObject
                ? "Project assets only"
                : "Contextual items only";
            _forbiddenOverlay.style.top = minY;
            _forbiddenOverlay.style.height = maxY - minY;
            _forbiddenOverlay.style.display = DisplayStyle.Flex;
        }

        private void EndPress(VisualElement row)
        {
            row?.RemoveFromClassList("assettray-row--dragging");
            ResetDragState();
        }

        private void ResetDragState()
        {
            _pressedEntry = null;
            _pressedRow = null;
            _pressedStartDragOut = null;
            _pressedSelectTarget = null;
            _reordering = false;
            _dropForbidden = false;
            _dropIndex = -1;
            if (_insertMarker != null)
            {
                _insertMarker.style.display = DisplayStyle.None;
                _insertMarker.RemoveFromClassList("assettray-insert-marker--forbidden");
            }
            if (rootVisualElement != null)
            {
                rootVisualElement.style.cursor = new StyleCursor(StyleKeyword.Null);
                // DragEnter/DragLeave can pair unevenly across Unity versions,
                // so scrub the hover class unconditionally.
                rootVisualElement.RemoveFromClassList("assettray-list--drag-over");
            }

            if (_forbiddenOverlay != null) _forbiddenOverlay.style.display = DisplayStyle.None;
            if (_dragGhost != null) _dragGhost.style.display = DisplayStyle.None;

            // Any row still wearing the dragging class (e.g. because Rebuild
            // ran mid-drag and the row survived) is cleaned up here.
            if (_list != null)
            {
                foreach (var child in _list.Children())
                    child.RemoveFromClassList("assettray-row--dragging");
            }
        }

        private void UpdateDropIndex(Vector2 mousePosition)
        {
            var localY = _list.WorldToLocal(mousePosition).y;
            var rows = CollectRows();

            var rawIndex = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var mid = rows[i].layout.y + rows[i].layout.height * 0.5f;
                if (localY < mid) { rawIndex = i; break; }
            }

            // Scene-bound favorites float to the top; asset favorites follow.
            // Constrain the drop so the dragged entry stays inside its own block.
            var draggedEntry = _pressedEntry ?? _draggingOwnEntry;
            var visibleIndex = ClampToBlock(draggedEntry, rawIndex, rows);
            SetDropForbidden(rawIndex != visibleIndex);
            PositionDragGhost(mousePosition);

            _dropIndex = VisibleIndexToEntryIndex(visibleIndex, rows);

            var markerY = visibleIndex < rows.Count
                ? rows[visibleIndex].layout.y
                : (rows.Count > 0 ? rows[rows.Count - 1].layout.yMax : 0f);

            _insertMarker.style.display = DisplayStyle.Flex;
            _insertMarker.style.top = markerY - 1f;
            _insertMarker.style.left = rows.Count > 0 ? rows[0].layout.x : 0f;
            _insertMarker.style.width = rows.Count > 0 ? rows[0].layout.width : _list.layout.width;
        }

        private void SetDropForbidden(bool forbidden)
        {
            if (_dropForbidden == forbidden) return;
            _dropForbidden = forbidden;

            _insertMarker.EnableInClassList("assettray-insert-marker--forbidden", forbidden);
            rootVisualElement.style.cursor = BuildCursor(forbidden ? MouseCursor.ArrowMinus : MouseCursor.MoveArrow);
        }

        private static int ClampToBlock(FavoriteEntry entry, int visibleIndex, List<VisualElement> rows)
        {
            if (entry == null) return visibleIndex;

            var boundary = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].userData is FavoriteEntry e && e.IsAsset)
                {
                    boundary = i;
                    break;
                }
            }

            return entry.IsSceneObject
                ? Mathf.Clamp(visibleIndex, 0, boundary)
                : Mathf.Clamp(visibleIndex, boundary, rows.Count);
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
            zone.RegisterCallback<DragEnterEvent>(evt =>
            {
                // Drag events bubble from children — only treat this as a real
                // window-level entry when the target is the root itself.
                if (evt.target != zone) return;

                if (_draggingOwnEntry != null && IsOurDragStillActive())
                {
                    ShowForbiddenOverlay(_draggingOwnEntry);
                }
                else
                {
                    if (_draggingOwnEntry != null) ClearOwnDrag();
                    EndPress(_pressedRow);
                    ShowAddOverlay();
                }
            });
            zone.RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (evt.target != zone) return;
                HideAddOverlay();
                if (_draggingOwnEntry != null && _forbiddenOverlay != null)
                    _forbiddenOverlay.style.display = DisplayStyle.None;
            });
            zone.RegisterCallback<DragExitedEvent>(_ =>
            {
                HideAddOverlay();
                // Drag ended (successfully or cancelled) — any remembered
                // "own entry" is stale now.
                ClearOwnDrag();
            });
            // Safety net: on Unity 6 some drags exit the window without firing
            // DragLeave / DragExited. MouseLeave is dispatched reliably.
            zone.RegisterCallback<MouseLeaveEvent>(_ => zone.RemoveFromClassList("assettray-list--drag-over"));

            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_draggingOwnEntry != null)
                {
                    // Re-show overlay if a missed DragEnter left it hidden.
                    if (_forbiddenOverlay.style.display == DisplayStyle.None)
                        ShowForbiddenOverlay(_draggingOwnEntry);

                    // The user is reorganising one of our own rows via a native
                    // drag — show the reorder marker and a Move cursor instead
                    // of the "copy / add" affordance.
                    _insertMarker.style.display = DisplayStyle.Flex;
                    UpdateDropIndex(evt.mousePosition);
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                }
                else
                {
                    if (_addOverlay.style.display == DisplayStyle.None && HasAnySupportedItemInDrag())
                        ShowAddOverlay();
                    DragAndDrop.visualMode = HasAnySupportedItemInDrag() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                }
                evt.StopPropagation();
            });

            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();

                if (_draggingOwnEntry != null)
                {
                    var fromIndex = FindEntryIndex(_draggingOwnEntry);
                    if (fromIndex >= 0 && _dropIndex >= 0 && !_dropForbidden)
                        FavoriteAssetsPreferences.Move(fromIndex, _dropIndex);
                    ClearOwnDrag();
                }
                else
                {
                    FavoriteAssetsPreferences.AddRange(DraggedEntries());
                }

                HideAddOverlay();
                evt.StopPropagation();
            });
        }


        private void ShowAddOverlay()
        {
            if (_addOverlay == null) return;

            var allDuplicate = true;
            var anyItem = false;
            foreach (var entry in DraggedEntries())
            {
                anyItem = true;
                if (!FavoriteAssetsPreferences.Contains(entry)) { allDuplicate = false; break; }
            }
            // Some drag sources only populate references on DragPerform; fall
            // back to the additive label until we know better.
            var duplicate = anyItem && allDuplicate;

            _addOverlayLabel.text = duplicate ? "Already in Favorites" : "Drop to add to Favorites";
            _addOverlay.EnableInClassList("assettray-add-overlay--duplicate", duplicate);
            _addOverlay.style.display = DisplayStyle.Flex;
            _addOverlay.BringToFront();
        }

        private void HideAddOverlay()
        {
            if (_addOverlay != null) _addOverlay.style.display = DisplayStyle.None;
        }

        private void ClearOwnDrag()
        {
            if (_draggingOwnEntry == null) return;
            _draggingOwnEntry = null;
            _draggingOwnPayload = null;
            _dropIndex = -1;
            SetDropForbidden(false);
            if (_insertMarker != null) _insertMarker.style.display = DisplayStyle.None;
            if (_forbiddenOverlay != null) _forbiddenOverlay.style.display = DisplayStyle.None;
            if (rootVisualElement != null) rootVisualElement.style.cursor = new StyleCursor(StyleKeyword.Null);
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
