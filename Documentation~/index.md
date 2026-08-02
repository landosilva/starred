# Starred Documentation

A favorites tray and selection history for the Unity Editor.

## Contents

- [Installation](#installation)
- [Favorites window](#favorites-window)
- [Selection History window](#selection-history-window)
- [Star overlays](#star-overlays)
- [Preferences](#preferences)
- [Persistence](#persistence)

## Installation

**Unity Asset Store:** [Starred](https://assetstore.unity.com/packages/tools/utilities/starred-376068)

**OpenUPM (recommended for updates):** Installs through Package Manager so you get **Updates** and version history. A plain git URL install does not.

Via [openupm-cli](https://openupm.com/docs/getting-started.html):

```
openupm add com.kynesis.starred
```

Or add the OpenUPM scoped registry, then install `com.kynesis.starred` from Package Manager:

| | |
| --- | --- |
| Name | `package.openupm.com` |
| URL | `https://package.openupm.com` |
| Scope | `com.kynesis` |

Package page: [com.kynesis.starred](https://openupm.com/packages/com.kynesis.starred/)

**Git URL:** In Unity, open **Window → Package Manager → + → Add package from git URL…** and paste:

```
https://github.com/landosilva/starred.git
```

Pin to a release:

```
https://github.com/landosilva/starred.git#v0.2.0
```

**Local package:** Clone the repo and add it via **Add package from disk…**, pointing at the cloned folder's `package.json`.

Requires Unity **2022.3 LTS** or newer.

## Favorites window

`Tools → Starred → Favorites`

A flat, ordered tray for things you want quick access to.

**What you can drop in:**
- Any project asset (from the Project window).
- Any GameObject from the Hierarchy or from an open Prefab Stage.

**Row controls:**
- Single click: select the target so Unity's Inspector shows it.
- Double click: open the asset (or frame the GameObject in Scene View).
- Lens button: ping in Project / Hierarchy.
- × button: remove from favorites.
- Drag out of the window: drop into an Inspector object field or scene.

**Scene entries** are stored with a scene path plus hierarchy path. They only show while their owning scene or prefab is the active context, so favorites from other scenes do not clutter the tray. If the scene is open but the GameObject has been renamed or deleted, the row appears in a red "missing" state.

**Right click** for Show in Project / Explorer / Hierarchy, Open / Frame, Copy Path / GUID / Hierarchy Path, Remove from Favorites.

## Selection History window

`Tools → Starred → History`

Auto filled list of the last N things you selected, both assets and scene GameObjects. Newest first. Selecting an item again moves it to the top.

Each row has a **star button** that toggles the entry in Favorites. The star fills gold when the entry is already favorited.

The size cap defaults to 16 and can be changed in Preferences or via the window's option menu (4 / 8 / 16 / 32).

## Star overlays

Whenever an asset is favorited, a small gold ★ is drawn on its row in the **Project** window (both list and grid views). Click the star to remove the favorite.

The same treatment applies in the **Hierarchy** for favorited scene and prefab stage GameObjects.

Both overlays can be toggled independently in Preferences.

## Preferences

**Unity → Settings → Starred** (macOS, Unity 6), or **Edit → Preferences → Starred** (Windows, Unity 2022).

- **Show star in Project window:** toggle the Project ★ overlay.
- **Show star in Hierarchy:** toggle the Hierarchy ★ overlay.
- **Selection history max entries:** 4 / 8 / 16 / 32. Shrinking trims existing history.

Each window also exposes the same toggles (plus Clear / Open Preferences) through its three dot option menu in the window title bar.

## Persistence

- Favorites are stored in `UserSettings/FavoriteAssets.json`. Per user, GUID based, survives asset rename or move.
- Selection history is stored in `UserSettings/SelectionHistory.json`.
- Preferences toggles live in `EditorPrefs`. Per user, per machine.
