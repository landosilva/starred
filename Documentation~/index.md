# Starred — Documentation

A compact favorites tray plus selection history for the Unity Editor.

## Contents

- [Installation](#installation)
- [Favorites window](#favorites-window)
- [Selection History window](#selection-history-window)
- [Star overlays](#star-overlays)
- [Preferences](#preferences)
- [Persistence](#persistence)

## Installation

**Option 1 — Package Manager (recommended):**

In Unity, open **Window → Package Manager → +  → Add package from git URL…** and paste:

```
https://github.com/landosilva/starred.git
```

**Option 2 — Local package:**

Clone the repo and add it via **Add package from disk…**, pointing at the cloned folder's `package.json`.

Requires Unity **6.0** or newer.

## Favorites window

`Tools → Starred → Favorites`

A flat, ordered tray of things you want one-click access to.

**What you can drop in:**
- Any project asset (from the Project window).
- Any GameObject from the Hierarchy or from an open Prefab Stage.

**Row controls:**
- Single-click — select the target so Unity's Inspector shows it.
- Double-click — open the asset (or frame the GameObject in Scene View).
- Lens button — ping in Project / Hierarchy.
- × button — remove from favorites.
- Drag out of the window — drop into an Inspector object field or scene.

**Scene-bound entries** are stored with a scene-path + hierarchy-path reference. They only render while their owning scene or prefab is the active context — so favorites from other scenes don't clutter the tray. If the scene is open but the GameObject has been renamed or deleted, the row appears in a red "missing" state.

**Right-click** for Show in Project / Explorer / Hierarchy, Open / Frame, Copy Path / GUID / Hierarchy Path, Remove from Favorites.

## Selection History window

`Tools → Starred → History`

Auto-populated list of the last N things you selected — both assets and scene GameObjects. Most-recent-first; re-selecting bumps to the top.

Each row has a **star button** that toggles the entry in Favorites. The star fills gold when the entry is already favorited.

The size cap defaults to 16 and can be changed in Preferences or via the window's option menu (4 / 8 / 16 / 32).

## Star overlays

Whenever an asset is favorited, a small gold ★ is drawn on its row in the **Project** window (both list and grid views). Click the star to remove the favorite.

The same treatment applies in the **Hierarchy** for favorited scene / prefab-stage GameObjects.

Both overlays are independently toggleable in Preferences.

## Preferences

`Edit → Preferences → Favorites & History`

- **Show star in Project window** — toggle the Project ★ overlay.
- **Show star in Hierarchy** — toggle the Hierarchy ★ overlay.
- **Selection history max entries** — 4 / 8 / 16 / 32. Shrinking trims existing history.

Each window also exposes the same toggles (plus Clear / Open Preferences) through its 3-dot option menu in the window title bar.

## Persistence

- Favorites are stored in `UserSettings/FavoriteAssets.json` — per-user, GUID-based, survives asset rename/move.
- Selection history is stored in `UserSettings/SelectionHistory.json`.
- Preferences toggles live in `EditorPrefs` — per-user, per-machine.
