# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
