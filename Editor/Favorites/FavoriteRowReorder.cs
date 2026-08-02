namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal sealed class FavoriteRowReorder
    {
        private const float DragStartDistance = 6f;

        private enum DragState
        {
            Idle,
            Pressed,
            Reordering,
            GhostReturning,
        }

        private readonly EditorWindow _host;
        private VisualElement _root;
        private VisualElement _list;
        private VisualElement _addOverlay;
        private VisualElement _dragGhost;
        private readonly DragGhostAnimator _ghostAnimator;
        private readonly List<VisualElement> _rowBuffer = new List<VisualElement>();

        private DragState _dragState = DragState.Idle;
        private FavoriteEntry _pressedEntry;
        private VisualElement _pressedRow;
        private UnityEngine.Object _pressedSelectTarget;
        private Vector2 _mouseDownPos;
        private int _dropIndex = -1;

        public bool IsGhostAnimating => _ghostAnimator.IsAnimating;
        public bool IsCommittingMove { get; set; }

        public FavoriteRowReorder(EditorWindow host)
        {
            _host = host;
            _ghostAnimator = new DragGhostAnimator(host);
            _ghostAnimator.OnEnterGhostReturning = () => _dragState = DragState.GhostReturning;
            _ghostAnimator.OnEnded = () =>
            {
                _dropIndex = -1;
                _dragState = DragState.Idle;
            };
        }

        public VisualElement CreateAndBindGhost(VisualElement root, VisualElement list, VisualElement addOverlay = null)
        {
            _root = root;
            _list = list;
            _addOverlay = addOverlay;
            _dragGhost = DragGhostAnimator.CreateGhostElement();
            _ghostAnimator.Bind(root, list, _dragGhost);
            return _dragGhost;
        }

        public void RegisterPointerHandlers()
        {
            _root.pickingMode = PickingMode.Position;
            _root.focusable = true;
            _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
            _root.RegisterCallback<PointerUpEvent>(OnRootPointerUp, TrickleDown.TrickleDown);
            _root.RegisterCallback<MouseLeaveEvent>(OnRootMouseLeave);
        }

        public void RegisterImguiFallback(Action onImguiDrop)
        {
            IMGUIContainer fallback = new IMGUIContainer(() => OnImguiEvents(onImguiDrop));
            fallback.style.position = Position.Absolute;
            fallback.style.left = 0;
            fallback.style.right = 0;
            fallback.style.top = 0;
            fallback.style.bottom = 0;
            fallback.pickingMode = PickingMode.Ignore;
            _root.Insert(0, fallback);
        }

        public void OnRebuildStarting()
        {
            if (!IsCommittingMove)
            {
                ResetReorderInternals();
                _ghostAnimator.EndDrop();
            }
        }

        public void Cancel()
        {
            EndPress();
        }

        public void EndPress()
        {
            _ghostAnimator.EndDrop();
            ResetReorderInternals();
        }

        private void OnRootPointerDown(PointerDownEvent pointerDownEvent)
        {
            if (EditorWindow.focusedWindow != _host) _host.Focus();

            if (pointerDownEvent.button != 0) return;
            if (_ghostAnimator.IsAnimating) { pointerDownEvent.StopPropagation(); return; }
            if (pointerDownEvent.target is Button) return;

            VisualElement row = TrayRowBinder.FindRowAncestor(pointerDownEvent.target as VisualElement);
            if (row?.userData is not FavoriteEntry entry) return;

            TrayRowBinding binding = TrayRowBinder.BindFor(entry, useHistorySelection: false);
            if (binding == null) return;

            if (pointerDownEvent.clickCount == 2)
            {
                binding.OnDoubleClick();
                pointerDownEvent.StopPropagation();
                return;
            }

            EnterPressed(entry, row, binding.SelectTarget, pointerDownEvent.position);
        }

        private void EnterPressed(FavoriteEntry entry, VisualElement row, UnityEngine.Object selectTarget, Vector2 mousePos)
        {
            _dragState = DragState.Pressed;
            _pressedEntry = entry;
            _pressedRow = row;
            _pressedSelectTarget = selectTarget;
            _mouseDownPos = mousePos;
        }

        private void OnRootPointerMove(PointerMoveEvent pointerMoveEvent)
        {
            if (_pressedEntry == null) return;

            if ((pointerMoveEvent.pressedButtons & 1) == 0)
            {
                EndPress();
                return;
            }

            if (ExternalAssetDropZone.HasAnySupportedItemInDrag())
            {
                EndPress();
                return;
            }

            Vector2 mousePosition = (Vector2)pointerMoveEvent.position;
            if ((mousePosition - _mouseDownPos).sqrMagnitude < DragStartDistance * DragStartDistance) return;

            if (_dragState != DragState.Reordering && _pressedRow != null) BeginReorder(_pressedRow);
            UpdateDropIndex(mousePosition);
        }

        private void OnRootMouseLeave(MouseLeaveEvent mouseLeaveEvent)
        {
            if (_pressedEntry == null) return;
            EndPress();
        }

        private void OnRootPointerUp(PointerUpEvent pointerUpEvent)
        {
            if (_pressedEntry == null) return;
            if ((pointerUpEvent.pressedButtons & 1) != 0) return;
            if (pointerUpEvent.button != 0) return;

            bool rowIsLive = _pressedRow != null && _pressedRow.parent == _list;
            if (rowIsLive && _dragState == DragState.Reordering && _dropIndex >= 0)
            {
                FavoriteEntry entry = _pressedEntry;
                int fromIndex = ReorderGeometry.FindEntryIndex(entry);
                if (fromIndex >= 0)
                {
                    IsCommittingMove = true;
                    try { FavoriteAssetsStore.Move(fromIndex, _dropIndex); }
                    finally { IsCommittingMove = false; }
                    _pressedEntry = null;
                    _pressedRow = null;
                    _pressedSelectTarget = null;
                    _ghostAnimator.StartDrop(entry);
                    return;
                }
            }

            if (rowIsLive && _dragState == DragState.Pressed && _pressedSelectTarget != null)
                SelectionWithoutProjectJump.Select(_pressedSelectTarget);

            EndPress();
        }

        private void OnImguiEvents(Action onImguiDrop)
        {
            Event imguiEvent = Event.current;
            if (imguiEvent == null) return;

            switch (imguiEvent.type)
            {
                case EventType.MouseDown when imguiEvent.button == 0:
                    HandleImguiPress(imguiEvent);
                    break;
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    HandleImguiDrop(imguiEvent, onImguiDrop);
                    break;
            }
        }

        private void HandleImguiPress(Event imguiEvent)
        {
            if (_pressedEntry != null) return;
            if (_ghostAnimator.IsAnimating) return;
            if (EditorWindow.focusedWindow != _host) _host.Focus();

            VisualElement target = _root.panel?.Pick(imguiEvent.mousePosition);
            if (target is Button) return;

            VisualElement row = TrayRowBinder.FindRowAncestor(target);
            if (row?.userData is not FavoriteEntry entry) return;

            TrayRowBinding binding = TrayRowBinder.BindFor(entry, useHistorySelection: false);
            if (binding == null) return;

            if (imguiEvent.clickCount == 2)
            {
                binding.OnDoubleClick();
                return;
            }

            EnterPressed(entry, row, binding.SelectTarget, imguiEvent.mousePosition);
        }

        private void HandleImguiDrop(Event imguiEvent, Action onImguiDrop)
        {
            if (!ExternalAssetDropZone.HasAnySupportedItemInDrag()) return;

            if (_pressedEntry != null) EndPress();

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (imguiEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                onImguiDrop?.Invoke();
                imguiEvent.Use();
            }
        }

        private void BeginReorder(VisualElement row)
        {
            _dragState = DragState.Reordering;
            row.AddToClassList(AssetTrayRow.Classes.Dragging);
            _root.style.cursor = EditorCursorInternals.Build(MouseCursor.MoveArrow);
            _root.AddToClassList(AssetTrayRow.Classes.WithDrag);
            _ghostAnimator.BeginFromRow(row);
        }

        private void UpdateDropIndex(Vector2 mousePosition)
        {
            float localY = _list.WorldToLocal(mousePosition).y;
            List<VisualElement> rows = ReorderGeometry.CollectRows(_list, _rowBuffer);

            int uncampedIndex = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                float rowMidY = rows[i].layout.y + rows[i].layout.height * 0.5f;
                if (localY < rowMidY) { uncampedIndex = i; break; }
            }

            int visibleIndex = ReorderGeometry.ClampToBlock(_pressedEntry, uncampedIndex, rows);

            _dropIndex = ReorderGeometry.VisibleIndexToEntryIndex(visibleIndex, rows);

            float localMouseY = _list.WorldToLocal(mousePosition).y;
            ReorderGeometry.ApplyReorderShift(rows, _pressedEntry, visibleIndex);
            ReorderGeometry.PositionDragGhost(_dragGhost, _root, _list, rows, _pressedEntry, localMouseY);
        }

        private void ResetReorderInternals()
        {
            _pressedEntry = null;
            _pressedRow = null;
            _pressedSelectTarget = null;
            _dragState = DragState.Idle;
            _dropIndex = -1;
            if (_root != null)
            {
                _root.style.cursor = new StyleCursor(StyleKeyword.Null);
                _root.RemoveFromClassList(AssetTrayRow.Classes.ListDragOver);
                _root.RemoveFromClassList(AssetTrayRow.Classes.WithDrag);
                ExternalAssetDropZone.SyncOverlayActiveClass(_root, _addOverlay);
            }
            _ghostAnimator.ClearTooltipSuppressionWithoutRestore();
        }
    }
}
