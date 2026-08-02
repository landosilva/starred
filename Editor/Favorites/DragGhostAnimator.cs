namespace Kynesis.Starred.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    internal sealed class DragGhostAnimator
    {
        private const int DropAnimationMs = 120;
        private static readonly StyleList<TimeValue> ZeroTransitionDuration =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f) });

        private readonly EditorWindow _host;
        private VisualElement _root;
        private VisualElement _list;
        private VisualElement _dragGhost;
        private VisualElement _ghostHiddenRow;
        private bool _ghostAnimating;
        private IVisualElementScheduledItem _ghostFailsafe;
        private Dictionary<VisualElement, string> _suppressedTooltips;

        public Action OnEnterGhostReturning;
        public Action OnEnded;

        public bool IsAnimating => _ghostAnimating;
        public VisualElement HiddenRow => _ghostHiddenRow;

        public DragGhostAnimator(EditorWindow host)
        {
            _host = host;
        }

        public void Bind(VisualElement root, VisualElement list, VisualElement dragGhost)
        {
            _root = root;
            _list = list;
            _dragGhost = dragGhost;
        }

        public static VisualElement CreateGhostElement()
        {
            VisualElement ghost = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            ghost.AddToClassList(AssetTrayRow.Classes.Row);
            ghost.AddToClassList(AssetTrayRow.Classes.DragGhost);

            IStyle s = ghost.style;
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
            s.backgroundColor = new StyleColor(new Color(0.30f, 0.31f, 0.36f, 1f));
            return ghost;
        }

        public void BeginFromRow(VisualElement row)
        {
            SuppressRowTooltips();
            ShowFromRow(row);
            row.style.visibility = Visibility.Hidden;
            _ghostHiddenRow = row;
        }

        public void ShowFromRow(VisualElement row)
        {
            if (_dragGhost == null || row == null) return;
            _dragGhost.Clear();

            _dragGhost.EnableInClassList(AssetTrayRow.Classes.Inactive,
                row.ClassListContains(AssetTrayRow.Classes.Inactive));
            _dragGhost.EnableInClassList(AssetTrayRow.Classes.Missing,
                row.ClassListContains(AssetTrayRow.Classes.Missing));

            foreach (VisualElement child in row.Children())
            {
                VisualElement clone = CloneForGhost(child);
                if (clone != null) _dragGhost.Add(clone);
            }

            Rect rect = row.worldBound;
            Vector2 topLeft = _root.WorldToLocal(new Vector2(rect.x, rect.y));
            _dragGhost.style.left = topLeft.x;
            _dragGhost.style.top = topLeft.y;
            _dragGhost.style.width = rect.width;
            _dragGhost.style.height = rect.height;
            _dragGhost.style.display = DisplayStyle.Flex;
            _dragGhost.BringToFront();
        }

        public void SuppressRowTooltips()
        {
            _suppressedTooltips = new Dictionary<VisualElement, string>();
            if (_list == null) return;
            _list.Query<VisualElement>().ForEach(element =>
            {
                if (string.IsNullOrEmpty(element.tooltip)) return;
                _suppressedTooltips[element] = element.tooltip;
                element.tooltip = string.Empty;
            });
        }

        public void RestoreRowTooltips()
        {
            if (_suppressedTooltips == null) return;
            foreach (KeyValuePair<VisualElement, string> pair in _suppressedTooltips)
                pair.Key.tooltip = pair.Value;
            _suppressedTooltips = null;
        }

        public void ClearTooltipSuppressionWithoutRestore()
        {
            _suppressedTooltips = null;
        }

        public void StartDrop(FavoriteEntry entry)
        {
            if (_dragGhost == null || _dragGhost.style.display == DisplayStyle.None || entry == null)
            {
                EndDrop();
                return;
            }

            VisualElement newRow = null;
            foreach (VisualElement child in _list.Children())
            {
                if (child.userData is FavoriteEntry rowEntry && rowEntry == entry) { newRow = child; break; }
            }
            if (newRow == null)
            {
                EndDrop();
                return;
            }

            if (newRow != _ghostHiddenRow)
            {
                newRow.AddToClassList(AssetTrayRow.Classes.Dragging);
                newRow.style.visibility = Visibility.Hidden;
                _ghostHiddenRow = newRow;
            }

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

        public void EndDrop()
        {
            _ghostAnimating = false;
            _ghostFailsafe?.Pause();
            _ghostFailsafe = null;
            EditorApplication.update -= RepaintDuringDrop;
            if (_dragGhost != null) _dragGhost.UnregisterCallback<TransitionEndEvent>(OnGhostTransitionEnd);
            Hide();

            if (_ghostHiddenRow != null)
            {
                _ghostHiddenRow.RemoveFromClassList(AssetTrayRow.Classes.Dragging);
                _ghostHiddenRow.style.visibility = new StyleEnum<Visibility>(StyleKeyword.Null);
                _ghostHiddenRow = null;
            }

            if (_root != null)
            {
                _root.style.cursor = new StyleCursor(StyleKeyword.Null);
                _root.RemoveFromClassList(AssetTrayRow.Classes.WithDrag);
            }

            if (_list != null)
            {
                foreach (VisualElement child in _list.Children())
                    child.style.translate = new StyleTranslate(StyleKeyword.Null);
            }

            RestoreRowTooltips();
            OnEnded?.Invoke();
        }

        public void Hide()
        {
            if (_dragGhost == null) return;
            _dragGhost.style.display = DisplayStyle.None;
            _dragGhost.Clear();
            _dragGhost.style.transitionDuration = ZeroTransitionDuration;
        }

        private void BeginGhostSlide(VisualElement targetRow)
        {
            if (_dragGhost == null || targetRow == null) { EndDrop(); return; }

            Rect rect = targetRow.worldBound;
            Vector2 topLeft = _root.WorldToLocal(new Vector2(rect.x, rect.y));

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

            _ghostAnimating = true;
            OnEnterGhostReturning?.Invoke();
            _dragGhost.RegisterCallback<TransitionEndEvent>(OnGhostTransitionEnd);

            _dragGhost.style.top = topLeft.y;
            _dragGhost.style.left = topLeft.x;

            EditorApplication.update -= RepaintDuringDrop;
            EditorApplication.update += RepaintDuringDrop;

            _ghostFailsafe?.Pause();
            _ghostFailsafe = _dragGhost.schedule.Execute(() =>
            {
                if (_ghostAnimating) EndDrop();
            }).StartingIn(DropAnimationMs + 50);
        }

        private void RepaintDuringDrop() => _host.Repaint();

        private void OnGhostTransitionEnd(TransitionEndEvent transitionEvent)
        {
            if (transitionEvent.target != _dragGhost) return;
            EndDrop();
        }

        private static VisualElement CloneForGhost(VisualElement source)
        {
            if (source is Button) return null;

            VisualElement clone;
            if (source is Image image)
                clone = new Image { image = image.image, scaleMode = image.scaleMode };
            else if (source is Label label)
                clone = new Label(label.text);
            else
            {
                clone = new VisualElement();
                foreach (VisualElement child in source.Children())
                {
                    VisualElement clonedChild = CloneForGhost(child);
                    if (clonedChild != null) clone.Add(clonedChild);
                }
            }
            foreach (string className in source.GetClasses()) clone.AddToClassList(className);
            return clone;
        }
    }
}
