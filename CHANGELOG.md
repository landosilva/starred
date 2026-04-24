# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
