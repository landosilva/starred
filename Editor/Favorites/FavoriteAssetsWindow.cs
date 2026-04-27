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
        private VisualElement _addOverlay;
        private Label _addOverlayLabel;
        private VisualElement _dragGhost;

        // Explicit drag-state machine. Every interaction transitions between
        // exactly these states so we can never end up in an undefined mix.
        //
        //   Idle ─PointerDown on row─→ Pressed
        //   Pressed ─release / mouse leave─→ Idle
        //   Pressed ─move past 6px─→ Reordering
        //   Reordering ─release─→ GhostReturning      (always — no-op or real Move alike)
        //   Reordering ─mouse leave─→ Idle             (cancelled)
        //   GhostReturning ─TransitionEnd / failsafe─→ Idle
        //   GhostReturning ─external Rebuild─→ Idle    (cancelled; PointerDown is blocked)
        //
        // Click input is suppressed while in GhostReturning so a fresh press
        // can't strand the row underneath the ghost.
        private enum DragState
        {
            Idle,
            Pressed,
            Reordering,
            GhostReturning,
        }

        private DragState _dragState = DragState.Idle;

        private FavoriteEntry _pressedEntry;
        private VisualElement _pressedRow;
        private Object _pressedSelectTarget;
        private Vector2 _mouseDownPos;
        private int _dropIndex = -1;

        private Dictionary<VisualElement, string> _suppressedTooltips;
        private bool _ghostAnimating;
        private IVisualElementScheduledItem _ghostFailsafe;
        // Set while we're committing a reorder Move whose Rebuild we want to
        // ride through with the ghost intact: Move → Commit → Changed →
        // Rebuild fires synchronously, and Rebuild's normal teardown would
        // otherwise hide the ghost before StartGhostDrop could re-aim it at
        // the freshly-built destination row.
        private bool _committingMove;
        // The row we hid (visibility: Hidden) when the ghost took over. Tracked
        // separately from _pressedRow because the press fields are cleared as
        // soon as Move commits, but the row underneath the ghost must stay
        // hidden until the slide-in lands — at which point EndGhostDrop
        // restores it. Surviving a Rebuild is fine: the detached element no
        // longer has a parent, so the restore is a harmless no-op.
        private VisualElement _ghostHiddenRow;

        // Reused per-frame during drag to avoid GC churn on hot paths.
        private readonly List<VisualElement> _rowBuffer = new List<VisualElement>();
        // Cached "zero duration" StyleList — avoids allocating a fresh List<TimeValue>
        // every time we reset the ghost transition.
        private static readonly StyleList<TimeValue> ZeroTransitionDuration =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f) });

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
            // Defensive: window may close mid-animation.
            EditorApplication.update          -= RepaintDuringDrop;

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

            _dragGhost = BuildDragGhost();
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
            // While the drop animation is in flight the row sits invisible
            // under the ghost; accepting a fresh click here would either kill
            // the ghost mid-flight (jarring) or — worse — leave the just-
            // pressed row hidden if the new gesture takes a path that skips
            // visibility restore. Swallow the click until the ghost lands.
            if (_ghostAnimating) { evt.StopPropagation(); return; }
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

            EnterPressed(entry, row, binding.SelectTarget, evt.position);
        }

        private sealed class RowBinding
        {
            public Object SelectTarget;
            public System.Action OnDoubleClick;
        }

        // ---------- State machine entry points ----------

        private void EnterPressed(FavoriteEntry entry, VisualElement row, Object selectTarget, Vector2 mousePos)
        {
            _dragState = DragState.Pressed;
            _pressedEntry = entry;
            _pressedRow = row;
            _pressedSelectTarget = selectTarget;
            _mouseDownPos = mousePos;
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
                    OnDoubleClick = () => { Selection.activeGameObject = go; SceneView.FrameLastActiveSceneView(); },
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
                EndPress();
                return;
            }

            // External DragAndDrop in flight — kill any stale press so the
            // incoming drag isn't hijacked into a reorder.
            if (HasAnySupportedItemInDrag())
            {
                EndPress();
                return;
            }

            var pos = (Vector2)evt.position;
            if ((pos - _mouseDownPos).sqrMagnitude < DragStartDistance * DragStartDistance) return;

            // Cursor is still inside the window — reorder mode.
            if (_dragState != DragState.Reordering && _pressedRow != null) BeginReorder(_pressedRow);
            UpdateDropIndex(pos);
        }

        private void OnRootMouseLeave(MouseLeaveEvent evt)
        {
            // Drag-out is intentionally not supported (kept Starred scoped to
            // the favorites window). Cancel any in-flight press so state
            // doesn't leak when the user releases outside.
            if (_pressedEntry == null) return;
            EndPress();
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
            if (rowIsLive && _dragState == DragState.Reordering && _dropIndex >= 0)
            {
                var entry = _pressedEntry;
                var fromIndex = FindEntryIndex(entry);
                // Move's own ClampToKindRange enforces the contextual / asset
                // boundary at the data layer — we don't need a parallel guard.
                if (fromIndex >= 0)
                {
                    // Suppress Rebuild's ghost teardown for the duration of
                    // the commit so the ghost survives into StartGhostDrop
                    // and can animate to the new row's position.
                    _committingMove = true;
                    try { FavoriteAssetsPreferences.Move(fromIndex, _dropIndex); }
                    finally { _committingMove = false; }
                    // Move may be a no-op (drop at same position) in which
                    // case Rebuild won't fire and the press fields won't be
                    // cleared. Clear them explicitly so the next mouse move
                    // can't be misread as a stale press → EndPress cancel.
                    _pressedEntry = null;
                    _pressedRow = null;
                    _pressedSelectTarget = null;
                    StartGhostDrop(entry);
                    return;
                }
            }
            // No Selection.activeObject on click — that would highlight the
            // row in the Project / Hierarchy too. The Properties button on
            // each row handles "show me this in the inspector" without that
            // side effect; the lens button handles "ping it in Project".
            EndPress();
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
            }
        }

        private void HandleImguiPress(Event evt)
        {
            // If UITK already delivered a PointerDown for this click, skip —
            // this IMGUI path is a fallback for the unfocused-window case.
            if (_pressedEntry != null) return;
            // Mirror OnRootPointerDown: ignore clicks while the drop
            // animation is in flight so the press can't strand the
            // invisible row underneath the ghost.
            if (_ghostAnimating) return;
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

            EnterPressed(entry, row, binding.SelectTarget, evt.mousePosition);
        }

        private void HandleImguiDrop(Event evt)
        {
            if (!HasAnySupportedItemInDrag()) return;

            // External drag is in-flight — scrub any lingering press so a
            // stale _pressedEntry can't be misread as reorder in a
            // concurrent PointerMove.
            if (_pressedEntry != null) EndPress();

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
            // Rows are about to be destroyed — clear press / overlay state so
            // it doesn't leak onto the freshly rebuilt rows. Skip the row-
            // translate cleanup though: touching the old rows fires a 0.14s
            // transition which we'd then immediately interrupt with
            // _list.Clear(), making the user see a small "settle" jump.
            // The Move-commit path rides through with the reorder visuals
            // intact — EndGhostDrop will tear them down on landing, so the
            // user sees one continuous animation regardless of whether the
            // drop changed position or not. External Rebuilds (asset import,
            // hierarchy change) can still fire mid-drop, so the non-commit
            // path kills any in-flight ghost cleanly.
            if (!_committingMove)
            {
                ResetReorderInternals();
                EndGhostDrop();
            }

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

            // Re-attach the insert marker that was cleared with the list.
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
                    evt.menu.AppendAction("Properties…", _ => EditorUtility.OpenPropertyEditor(asset));
                    evt.menu.AppendSeparator("");
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
                    evt.menu.AppendAction("Properties…", _ => EditorUtility.OpenPropertyEditor(go));
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
            // PointerDown is suppressed while a previous drop is animating,
            // so we can never reach this with a ghost in flight.
            _dragState = DragState.Reordering;
            row.AddToClassList("assettray-row--dragging");
            rootVisualElement.style.cursor = BuildCursor(MouseCursor.MoveArrow);
            rootVisualElement.AddToClassList("assettray-with-drag");

            SuppressRowTooltips();
            ShowDragGhost(row);
            // Original row stays in DOM (so its slot reserves the space) but
            // is invisible — the ghost is what the user sees following the
            // cursor. Tracked on _ghostHiddenRow so EndGhostDrop can restore
            // it even after _pressedRow has been cleared.
            row.style.visibility = Visibility.Hidden;
            _ghostHiddenRow = row;
        }

        // The ghost is a clone of the dragged row that floats above every
        // other row during reorder; the original row goes invisible (keeps
        // its slot) so the ghost owns the cursor's z-axis. Layout / colour
        // are set inline so no row-level CSS can wash them out.
        private static VisualElement BuildDragGhost()
        {
            var ghost = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            ghost.AddToClassList("assettray-row");
            ghost.AddToClassList("assettray-drag-ghost");

            var s = ghost.style;
            s.position = Position.Absolute;
            s.display = DisplayStyle.None;
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.overflow = Overflow.Hidden;
            s.paddingLeft = 6;
            s.paddingRight = 6;
            s.paddingTop = 4;
            s.paddingBottom = 4;
            s.borderTopLeftRadius = 3;
            s.borderTopRightRadius = 3;
            s.borderBottomLeftRadius = 3;
            s.borderBottomRightRadius = 3;
            // Distinctly brighter than the window bg so the ghost reads as
            // "lifted" against any list contents underneath.
            s.backgroundColor = new StyleColor(new Color(0.30f, 0.31f, 0.36f, 1f));
            return ghost;
        }

        // Clones the row's visual content into the ghost element so it reads
        // identical to the dragged row, then positions the ghost at the row's
        // current location. PointerMove will follow up by translating it to
        // track the cursor.
        private void ShowDragGhost(VisualElement row)
        {
            if (_dragGhost == null || row == null) return;
            _dragGhost.Clear();

            // Mirror the row's modifier classes so the ghost inherits the
            // same visual treatment (inactive scene object, missing asset).
            _dragGhost.EnableInClassList("assettray-row--inactive",
                row.ClassListContains("assettray-row--inactive"));
            _dragGhost.EnableInClassList(AssetTrayRow.Classes.Missing,
                row.ClassListContains(AssetTrayRow.Classes.Missing));

            foreach (var child in row.Children())
            {
                var clone = CloneForGhost(child);
                if (clone != null) _dragGhost.Add(clone);
            }

            var rect = row.worldBound;
            var topLeft = rootVisualElement.WorldToLocal(new Vector2(rect.x, rect.y));
            _dragGhost.style.left = topLeft.x;
            _dragGhost.style.top = topLeft.y;
            _dragGhost.style.width = rect.width;
            _dragGhost.style.height = rect.height;
            _dragGhost.style.display = DisplayStyle.Flex;
            // Defensive: keep ghost as the last child of root so it always
            // renders on top, even if other elements were appended later.
            _dragGhost.BringToFront();
        }

        private static VisualElement CloneForGhost(VisualElement source)
        {
            // Skip interactive children (buttons) — the ghost is decorative.
            if (source is Button) return null;

            VisualElement clone;
            if (source is Image img)
                clone = new Image { image = img.image, scaleMode = img.scaleMode };
            else if (source is Label lbl)
                clone = new Label(lbl.text);
            else
            {
                clone = new VisualElement();
                foreach (var child in source.Children())
                {
                    var c = CloneForGhost(child);
                    if (c != null) clone.Add(c);
                }
            }
            foreach (var cls in source.GetClasses()) clone.AddToClassList(cls);
            return clone;
        }

        private void SuppressRowTooltips()
        {
            _suppressedTooltips = new Dictionary<VisualElement, string>();
            if (_list == null) return;
            _list.Query<VisualElement>().ForEach(el =>
            {
                if (string.IsNullOrEmpty(el.tooltip)) return;
                _suppressedTooltips[el] = el.tooltip;
                el.tooltip = string.Empty;
            });
        }

        private void RestoreRowTooltips()
        {
            if (_suppressedTooltips == null) return;
            foreach (var kv in _suppressedTooltips)
                kv.Key.tooltip = kv.Value;
            _suppressedTooltips = null;
        }

        // Cancel path — Pressed state never made it to a committed drop.
        // EndGhostDrop is the canonical reorder-visuals teardown (it knows
        // how to restore the hidden row, cursor, hover class, neighbor
        // translates, and tooltips); ResetReorderInternals clears the
        // press fields and any external-drag-zone state on top of that.
        private void EndPress()
        {
            EndGhostDrop();
            ResetReorderInternals();
        }

        private void HideDragGhost()
        {
            if (_dragGhost == null) return;
            _dragGhost.style.display = DisplayStyle.None;
            _dragGhost.Clear();
            // Reset transition so the next drag's instant repositioning
            // isn't animated.
            _dragGhost.style.transitionDuration = ZeroTransitionDuration;
        }

        // ---------- Smooth drop animation ----------
        //
        // After a reorder commits, the ghost is at the cursor position and
        // the new row is already at its logical destination. We slide the
        // ghost from cursor → destination via a CSS transition, then hide
        // the ghost. The row underneath stays visible the whole time so it
        // can never get stuck invisible.
        //
        // Lifecycle:
        //   StartGhostDrop → registers TransitionEndEvent + a failsafe timer
        //   EndGhostDrop   → idempotent teardown; fires from TransitionEnd,
        //                    failsafe timeout, external Rebuild, or release-
        //                    without-reorder. Restores every visual that
        //                    BeginReorder set up.
        //
        // The TransitionEndEvent is the canonical signal — the failsafe
        // timer kicks in only if the transition doesn't actually run (e.g.
        // cursor was already at the destination, so no value change).

        private const int DropAnimationMs = 120;

        private void StartGhostDrop(FavoriteEntry entry)
        {
            if (_dragGhost == null || _dragGhost.style.display == DisplayStyle.None || entry == null)
            {
                EndGhostDrop();
                return;
            }

            VisualElement newRow = null;
            foreach (var child in _list.Children())
            {
                if (child.userData is FavoriteEntry e && e == entry) { newRow = child; break; }
            }
            if (newRow == null)
            {
                EndGhostDrop();
                return;
            }

            // Layout for fresh rows isn't computed until the next layout
            // pass — wait for it before reading position.
            if (newRow.layout.height > 0f) BeginGhostSlide(newRow);
            else
            {
                EventCallback<GeometryChangedEvent> onLayout = null;
                onLayout = _ =>
                {
                    newRow.UnregisterCallback<GeometryChangedEvent>(onLayout);
                    BeginGhostSlide(newRow);
                };
                newRow.RegisterCallback<GeometryChangedEvent>(onLayout);
            }
        }

        private void BeginGhostSlide(VisualElement targetRow)
        {
            if (_dragGhost == null || targetRow == null) { EndGhostDrop(); return; }

            var rect = targetRow.worldBound;
            var topLeft = rootVisualElement.WorldToLocal(new Vector2(rect.x, rect.y));

            // (Re-)apply transition config so a previous drop's reset can't
            // bleed into this one.
            _dragGhost.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName>
            {
                new StylePropertyName("top"),
                new StylePropertyName("left"),
            });
            _dragGhost.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue>
            {
                new TimeValue(DropAnimationMs / 1000f, TimeUnit.Second),
            });
            _dragGhost.style.transitionTimingFunction = new StyleList<EasingFunction>(new List<EasingFunction>
            {
                new EasingFunction(EasingMode.EaseOut),
            });

            _dragState = DragState.GhostReturning;
            _ghostAnimating = true;
            _dragGhost.RegisterCallback<TransitionEndEvent>(OnGhostTransitionEnd);

            // Trigger the transition.
            _dragGhost.style.top = topLeft.y;
            _dragGhost.style.left = topLeft.x;

            // Unity's Editor doesn't repaint when idle, which means UITK's
            // schedule + transition events stop firing if the user isn't
            // moving the mouse. Pump Repaint() while the ghost is in flight
            // so the animation completes on its own; unsubscribe in
            // EndGhostDrop so we don't tax the editor when idle.
            EditorApplication.update -= RepaintDuringDrop;
            EditorApplication.update += RepaintDuringDrop;

            // Failsafe in case TransitionEndEvent doesn't fire (e.g. ghost
            // was already at the destination, so no transition runs).
            _ghostFailsafe?.Pause();
            _ghostFailsafe = _dragGhost.schedule.Execute(() =>
            {
                if (_ghostAnimating) EndGhostDrop();
            }).StartingIn(DropAnimationMs + 50);
        }

        private void RepaintDuringDrop() => Repaint();

        private void OnGhostTransitionEnd(TransitionEndEvent evt)
        {
            // One event fires per animated property (top, left). The first
            // is enough — EndGhostDrop unregisters before the second.
            if (evt.target != _dragGhost) return;
            EndGhostDrop();
        }

        // Canonical "transition to Idle" path: clears all ghost-related
        // state, hides the ghost, and resets _dragState. Idempotent — safe
        // to call from any path, even if no animation was in flight.
        private void EndGhostDrop()
        {
            _ghostAnimating = false;
            _ghostFailsafe?.Pause();
            _ghostFailsafe = null;
            EditorApplication.update -= RepaintDuringDrop;
            if (_dragGhost != null) _dragGhost.UnregisterCallback<TransitionEndEvent>(OnGhostTransitionEnd);
            HideDragGhost();

            // Restore everything BeginReorder + UpdateDropIndex set up. This
            // is the canonical "reorder finished" path — the no-op-Move case
            // doesn't go through Rebuild's ResetReorderInternals, so all
            // residual reorder visuals must be cleared here or they leak
            // (hover suppression, MoveArrow cursor, neighbor translates,
            // tooltip suppression).

            // Hidden row → restore. Both the inline style AND the
            // .assettray-row--dragging class pin visibility to hidden, so
            // clear both. Detached rows (destroyed by a Rebuild between
            // hide and now) accept the writes as harmless no-ops.
            if (_ghostHiddenRow != null)
            {
                _ghostHiddenRow.RemoveFromClassList("assettray-row--dragging");
                _ghostHiddenRow.style.visibility = new StyleEnum<Visibility>(StyleKeyword.Null);
                _ghostHiddenRow = null;
            }

            // Window-level reorder visuals (cursor + hover suppression).
            if (rootVisualElement != null)
            {
                rootVisualElement.style.cursor = new StyleCursor(StyleKeyword.Null);
                rootVisualElement.RemoveFromClassList("assettray-with-drag");
            }

            // Neighbor row translates from ApplyReorderShift.
            if (_list != null)
            {
                foreach (var child in _list.Children())
                    child.style.translate = new StyleTranslate(StyleKeyword.Null);
            }

            // Tooltip suppression set up in BeginReorder.
            RestoreRowTooltips();

            _dropIndex = -1;
            if (_insertMarker != null) _insertMarker.style.display = DisplayStyle.None;

            _dragState = DragState.Idle;
        }

        // Lighter cleanup used by Rebuild: clears window-level state but
        // leaves the row visuals alone, since the rows are about to be
        // destroyed by _list.Clear() and we don't want to fire transitions
        // on doomed elements.
        private void ResetReorderInternals()
        {
            _pressedEntry = null;
            _pressedRow = null;
            _pressedSelectTarget = null;
            // Idle is the safe baseline; if a higher-level path needs to
            // transition to Reordering or GhostReturning, it does so after
            // calling ResetReorderInternals.
            _dragState = DragState.Idle;
            _dropIndex = -1;
            if (_insertMarker != null) _insertMarker.style.display = DisplayStyle.None;
            if (rootVisualElement != null)
            {
                rootVisualElement.style.cursor = new StyleCursor(StyleKeyword.Null);
                rootVisualElement.RemoveFromClassList("assettray-list--drag-over");
                rootVisualElement.RemoveFromClassList("assettray-with-drag");
            }
            UpdateOverlayActiveClass();
            // Drop the tooltip-suppression dictionary without re-applying —
            // the rows it points at are about to be destroyed.
            _suppressedTooltips = null;
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
            // Constrain the drop so the dragged entry stays inside its own
            // block — the dragged row's translate is also clamped, so the
            // user can't visually cross the boundary.
            var visibleIndex = ClampToBlock(_pressedEntry, rawIndex, rows);

            _dropIndex = VisibleIndexToEntryIndex(visibleIndex, rows);

            var localMouseY = _list.WorldToLocal(mousePosition).y;
            ApplyReorderShift(rows, _pressedEntry, visibleIndex);
            PositionDragGhost(rows, _pressedEntry, localMouseY);

            // Insert marker is hidden during animated reorder — the row gap
            // itself is the affordance now.
            _insertMarker.style.display = DisplayStyle.None;
        }

        // Slides every row that sits between the dragged entry and the drop
        // position by one row's worth of vertical space, opening a clean gap
        // where the dragged row will land. The dragged row itself is hidden
        // (visibility: hidden) — the ghost layer represents it.
        private static void ApplyReorderShift(List<VisualElement> rows, FavoriteEntry dragged, int dropIndex)
        {
            if (dragged == null || rows.Count == 0) return;

            var draggedIndex = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].userData is FavoriteEntry e && e == dragged) { draggedIndex = i; break; }
            }
            if (draggedIndex < 0) return;

            // Use the actual gap between consecutive rows (which includes the
            // 1px top + 1px bottom margins) — translating by `layout.height`
            // alone leaves a 2px micro-gap that the user reads as a "settle".
            var rowSpacing = rows.Count > 1
                ? rows[1].layout.y - rows[0].layout.y
                : rows[0].layout.height;

            for (var i = 0; i < rows.Count; i++)
            {
                if (i == draggedIndex) continue;

                var offset = 0f;
                if (dropIndex < draggedIndex && i >= dropIndex && i < draggedIndex) offset = rowSpacing;
                else if (dropIndex > draggedIndex && i > draggedIndex && i < dropIndex) offset = -rowSpacing;

                rows[i].style.translate = new StyleTranslate(new Translate(0, offset, 0));
            }
        }

        private void PositionDragGhost(List<VisualElement> rows, FavoriteEntry dragged, float localMouseY)
        {
            if (_dragGhost == null || dragged == null || rows.Count == 0) return;

            // Find dragged row's slot for height + block bounds.
            VisualElement draggedRow = null;
            var blockTop = float.PositiveInfinity;
            var blockBottom = float.NegativeInfinity;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].userData is not FavoriteEntry e) continue;
                if (e == dragged) draggedRow = rows[i];
                if (e.IsSceneObject != dragged.IsSceneObject) continue;
                if (rows[i].layout.y < blockTop) blockTop = rows[i].layout.y;
                if (rows[i].layout.yMax > blockBottom) blockBottom = rows[i].layout.yMax;
            }
            if (draggedRow == null) return;

            var rowHeight = draggedRow.layout.height;
            var halfRow = rowHeight * 0.5f;
            // Clamp the ghost to its own block so it can't visually cross
            // into the other one.
            var clampedY = Mathf.Clamp(localMouseY, blockTop + halfRow, blockBottom - halfRow);

            // The ghost lives on rootVisualElement, but our coordinates are
            // _list-local. Round-trip through world space to land in the
            // ghost's parent space regardless of any wrappers (ScrollView).
            var rowWorld = _list.LocalToWorld(new Vector2(0, clampedY - halfRow));
            var rootLocal = rootVisualElement.WorldToLocal(rowWorld);
            var rowRectWorld = draggedRow.LocalToWorld(new Vector2(0, 0));
            var rowRectRoot = rootVisualElement.WorldToLocal(rowRectWorld);
            _dragGhost.style.top = rootLocal.y;
            _dragGhost.style.left = rowRectRoot.x;
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

        // Returns a *shared* buffer to avoid per-frame allocations. Callers
        // must consume the result before the next CollectRows() call.
        private List<VisualElement> CollectRows()
        {
            // Rows have a FavoriteEntry in userData; separators and the insert
            // marker don't, so filtering on userData picks real rows only.
            _rowBuffer.Clear();
            foreach (var child in _list.Children())
                if (child != _insertMarker && child.userData != null) _rowBuffer.Add(child);
            return _rowBuffer;
        }

        // ---------- Selection highlight ----------

        private void ApplyCurrentHighlight()
        {
            var selectedGuid = AssetTrayRow.GetCurrentSelectionGuid();
            var selectedGo   = Selection.activeGameObject;
            // GetGlobalObjectIdSlow is a slow call (per its name); compute once
            // before the per-row predicate.
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

        // ---------- Drop zone (from Project window / Hierarchy) ----------

        private void RegisterDropZone(VisualElement zone)
        {
            zone.RegisterCallback<DragEnterEvent>(evt =>
            {
                // Drag events bubble from children — only treat this as a real
                // window-level entry when the target is the root itself.
                if (evt.target != zone) return;
                EndPress();
                ShowAddOverlay();
            });
            zone.RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (evt.target != zone) return;
                HideAddOverlay();
            });
            zone.RegisterCallback<DragExitedEvent>(_ => HideAddOverlay());

            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_addOverlay.style.display == DisplayStyle.None && HasAnySupportedItemInDrag())
                    ShowAddOverlay();
                DragAndDrop.visualMode = HasAnySupportedItemInDrag()
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
            });

            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();
                FavoriteAssetsPreferences.AddRange(DraggedEntries());
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
            rootVisualElement.AddToClassList("assettray-overlay-active");
        }

        private void HideAddOverlay()
        {
            if (_addOverlay != null) _addOverlay.style.display = DisplayStyle.None;
            UpdateOverlayActiveClass();
        }

        // Keep the assettray-overlay-active class in sync with whether the
        // add overlay is currently visible.
        private void UpdateOverlayActiveClass()
        {
            if (rootVisualElement == null) return;
            var anyVisible = _addOverlay != null && _addOverlay.style.display == DisplayStyle.Flex;
            if (!anyVisible) rootVisualElement.RemoveFromClassList("assettray-overlay-active");
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

                var entry = SceneObjectResolver.BuildEntry(go);
                if (entry == null || string.IsNullOrEmpty(entry.ScenePath))
                {
                    Debug.LogWarning($"[FavoriteAssets] Can't favorite '{go.name}' — save its scene first.");
                    continue;
                }
                if (string.IsNullOrEmpty(entry.GlobalObjectId)) continue;
                if (seen.Add(entry.LookupKey)) yield return entry;
            }
        }
    }
}
