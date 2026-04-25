# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.4] - 2026-04-25

### Added

- **"Drop to add" overlay** appears the moment an external drag enters the Favorites window — green-tinted when at least one item isn't already favorited, amber-tinted ("Already in Favorites") when every dragged item is a duplicate.
- **Forbidden-zone overlay** during reorder — the block you can't cross into is dimmed with a slate / blue-accent overlay and a small italic label ("Project assets only" / "Contextual items only"). The insert marker turns red and the cursor switches to "not allowed" right at the boundary.
- **Visible separator line** between contextual and asset blocks at all times (not only during reorder).
- **Drag ghost** follows the cursor during in-window reorder — icon + name pill matching the row you picked up. Native drag takes over once you leave the window.
- **Context prefix on scene-bound rows** — each contextual row now renders as `[sceneIcon] SceneName › [objIcon] ObjectName` (prefab-stage entries show the prefab icon).

### Fixed

- **Unity 6 unfocused-window first-click drag** — a fresh click on an inactive Favorites / History window now starts the drag immediately. Press detection moved off per-row pointer capture (which fails during Unity's focus transition) onto window-root events plus an IMGUI press fallback, mirroring how Unity's Project / Hierarchy work.
- **External drag misread as reorder** — a leftover press from a prior click could hijack an incoming Project-asset drag. Press state now scrubs aggressively on `DragEnter`, on stale `PointerMove` (left button not held), and on IMGUI `DragUpdated`.
- **Stuck overlay after a rejected drop** (e.g. dragging a SceneAsset onto the Scene View, which Unity blocks). Replaced the timer-based fallback with a payload-identity check on `DragAndDrop.objectReferences` — overlay clears the moment a different drag arrives.
- **Flicker on drag re-entry** — `DragEnter` / `DragLeave` were bubbling from child rows. Guarded with `evt.target == zone` so overlay only toggles on real window-level enter/leave.
- **Selection now applies on release**, not on press, so `Selection.selectionChanged` no longer interrupts the gesture (which was breaking drag-to-Scene on unfocused windows).
- Prefab-stage detection hardened — uses `Path.GetExtension` instead of `EndsWith` to avoid false positives on assets named `*prefab.unity`.

### Changed

- **Contextual favorites are reorder-only.** Scene / prefab-stage entries no longer initiate a drag-out — there's no meaningful "instantiate" for an existing scene object. Reorder, click-to-select, double-click-to-frame, and the context menu all still work.
- **Reorder is constrained to its own block.** Dragging a contextual row down past the separator is rejected (visual feedback as above); dragging an asset row up into the contextual block is rejected the same way.
- Drag detection on the History window restructured to mirror Favorites' single root-level press handler (was per-row + IMGUI; now matches Favorites for consistency).
- Internal code cleanup — removed dead fields (`_listContainer`), no-op helpers (`NoteDragSignal`), and ~25 lines of duplicated wiring helpers in `SelectionHistoryWindow`.

## [0.1.3] - 2026-04-24

### Added

- **Drag from History → Favorites** promotes a history entry straight into Favorites. History rows also drag out onto the Scene View / Inspector the same way Favorites do.
- **Reorder within Favorites** — drag a favorited row inside the window to change its position.
- **Forbidden-zone overlay**: when you start reordering, the block you can't cross into (scene-bound vs asset favorites) is dimmed out and labelled, with a red insert-marker and "not-allowed" cursor at the boundary.
- **Visible separator** between scene-bound and asset favorites, so the grouping is obvious even outside a reorder.
- **Context prefix on scene-bound rows** — each row now renders as `[sceneIcon] SceneName › [objIcon] ObjectName` (prefab stage entries show the prefab icon instead).
- **Drag ghost** follows the cursor during in-window reorder (native drag takes over once you leave the window).

### Changed

- **History clicks no longer reshuffle history** — selecting an entry from the History window doesn't bump it to the top. `SelectionHistoryTracker.SuppressNext()` / `Select()` scope the suppression to Starred-originated selection changes only.
- **Selection happens on release, not press**. Firing `Selection.activeObject` on press was interrupting the event stream (Inspector repaints, focus jumps), which broke drag-to-Scene on unfocused windows and caused "click-only" behaviour. Pointer events + an IMGUI press fallback now drive all drag detection; selection is applied on the matching release when no drag happened.
- **Contextual favorites are reorder-only** — scene / prefab-stage entries no longer initiate a drag-out (there's no meaningful "instantiate" for an already-existing scene object).
- Drag code moved off per-row pointer capture (which fails during Unity's focus transition) onto window-root events plus an IMGUI press fallback. This lets you click-and-drag on an unfocused Favorites / History window on first click, matching Unity's Project / Hierarchy behaviour.

### Fixed

- **Unity 6: first click on unfocused window** no longer swallows the drag. Previously the click registered as a plain selection and the following move did nothing.
- **External drag-in misread as reorder** — a leftover press from a prior click could hijack an incoming Project-asset drag into a reorder of the last-touched favorite. Press state is now scrubbed on `DragEnter` / IMGUI `DragUpdated` and on any `PointerMove` that reports the left button isn't physically held.
- **Stuck "dragging" highlight / dead reorder input** after a `Rebuild` mid-drag — state reset at the top of `Rebuild`, plus defensive re-scrubbing.
- **Drop-zone hover highlight stuck on** after a drag left the window on Unity 6 — `DragExited` / `MouseLeave` both reliably clear it now.

## [0.1.2] - 2026-04-24

### Fixed

- Favorites and Selection History rows now turn red **immediately** when their target disappears:
  - Deleted, moved or renamed project assets — via a new `AssetChangeNotifier` (`AssetPostprocessor`) that fires after the AssetDatabase has settled.
  - Scene GameObjects deleted or renamed in an **unsaved** scene — via `EditorApplication.hierarchyChanged`.
- Missing-asset labels now differentiate between `(deleted)` (path known, file removed) and `(unknown asset)` (GUID does not resolve).

### Changed

- **Preferences pane** renamed from *Favorites & History* to **Starred**, now split into bold **Favorites** and **History** sections with the relevant `Tools → Starred → …` menu path shown under each. Label column widened so labels no longer truncate.
- README: new *Design philosophy* section, fuller License blurb, menu paths updated for Unity 6 (macOS `Unity → Settings`) and Unity 2022 (`Edit → Preferences`).
- `HierarchyFavoriteOverlay` internals collapsed — the Unity 6 / 2022.3 API split now wraps a single shared draw method instead of duplicating the body inside `#if`.
- Selection-history size choices (`4 / 8 / 16 / 32`) are now exposed once from `FavoriteAssetsSettings` and reused by `SelectionHistoryWindow`.

## [0.1.1] - 2026-04-24

### Added

- Unity **2022.3 LTS** compatibility. The Hierarchy star overlay now compiles against both the legacy `hierarchyWindowItemOnGUI` / `InstanceIDToObject` API (2022.3) and the new `hierarchyWindowItemByEntityIdOnGUI` / `EntityIdToObject` API (Unity 6) via a `UNITY_6000_0_OR_NEWER` switch.

### Changed

- `package.json` minimum Unity version lowered from `6000.0` to `2022.3`.
- README polish: restructured for launch with banner, badges, per-feature sections and image slots under `Documentation~/Images/`.

## [0.1.0] - 2026-04-24

### Added

- **Favorites window** (`Tools → Starred → Favorites`) — a compact tray for pinning project assets and scene / prefab-stage GameObjects. Drag in from the Project window or the Hierarchy, reorder by drag, drag out to drop into the Inspector, lens to ping, double-click to open / frame.
- **Selection History window** (`Tools → Starred → History`) — auto-populated list of the last things you selected (both assets and scene GameObjects). Click the row to inspect, click the star to promote into Favorites.
- **Contextual entries** — scene / prefab-stage GameObjects are stored with a scene-path + hierarchy-path reference. Rows only render while the owning scene or prefab is the active context, so other scenes don't pollute the view.
- **Gold-star overlays** in the Project window (list + grid) and in the Hierarchy. Click the star to unfavorite.
- **Preferences pane** (`Edit → Preferences → Favorites & History`) — toggle the stars individually and pick the Selection History size (4 / 8 / 16 / 32).
- **Window option menus** — the 3-dot menu on each window mirrors the relevant preferences plus Clear / Open Preferences shortcuts.
- **Selection-aware highlighting** — the row matching whatever you have selected in Unity gets a subtle tint in both windows.
- **Right-click context menus** on rows — Show in Project / Explorer / Hierarchy, Open / Frame in Scene View, Copy Path / GUID / Hierarchy Path, Remove from Favorites.
