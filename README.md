<div align="center">

<img src="Documentation~/Images/banner.png" alt="Starred" width="880" />

# Starred

**Favorites tray + selection history for the Unity Editor.**

[![Unity 2022.3+](https://img.shields.io/badge/Unity-2022.3%2B-000?logo=unity)](https://unity.com/releases/editor/qa/lts-releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![Release](https://img.shields.io/github/v/tag/landosilva/starred?label=release&color=gold)](https://github.com/landosilva/starred/releases)
[![Kynesis](https://img.shields.io/badge/Kynesis-★-black)](https://github.com/landosilva)

<img src="Documentation~/Images/hero-a.gif" alt="Favorites demo" width="380" /> <img src="Documentation~/Images/hero-b.gif" alt="History demo" width="380" />

</div>

---

## Install

**Package Manager (recommended):**
**Window → Package Manager → + → Add package from git URL…**

```
https://github.com/landosilva/starred.git
```

Pin to a release:

```
https://github.com/landosilva/starred.git#v0.1.6
```

Or clone and use **Add package from disk…** → `package.json`.

Requires **Unity 2022.3 LTS** or newer.

## Favorites

`Tools → Starred → Favorites`

<div align="center">
<img src="Documentation~/Images/favorites.gif" alt="Favorites tray" width="600" />
</div>

- Drop in project assets or scene GameObjects.
- Click to select. Double-click to open or frame.
- Drag out onto Inspector fields.
- Reorder by drag. Lens icon to ping in Project/Hierarchy.

Scene-bound entries use `scene-path + hierarchy-path`, so they only appear while their scene is loaded. Missing or renamed objects show red.

## Selection History

`Tools → Starred → History`

<div align="center">
<img src="Documentation~/Images/history.gif" alt="Selection history" width="600" />
</div>

Auto-populated, most-recent-first. Re-selecting bumps to the top. Each row has a ★ button to promote to Favorites. Size caps at 4 / 8 / 16 / 32.

## Star Overlays

Favorited items get a ★ overlay in the **Project** and **Hierarchy** windows. Click the overlay to unfavorite directly.

<div align="center">
<img src="Documentation~/Images/overlays-a.png" alt="Hierarchy overlay" width="380" /> <img src="Documentation~/Images/overlays-b.png" alt="Project overlay" width="380" />
</div>

Both overlays toggle independently in Preferences.

## Context Menu

Every row has a context menu:

- 👁 Show in Project / Hierarchy
- 🗂 Show in Explorer
- ↗️ Open — opens the asset in its default editor (scene, IDE, prefab stage…)
- 📷 Frame in Scene View
- 📋 Copy Path / GUID / Hierarchy Path
- ❌ Remove from Favorites

<div align="center">
<img src="Documentation~/Images/context-menu.png" alt="Context menu" width="520" />
</div>

## Preferences

- **macOS:** Unity → Settings → Starred
- **Windows:** Edit → Preferences → Starred

<div align="center">
<img src="Documentation~/Images/preferences.png" alt="Preferences" width="520" />
</div>

- **Show star in Project window** — toggle Project ★ overlay.
- **Show star in Hierarchy** — toggle Hierarchy ★ overlay.
- **Selection history max entries** — 4 / 8 / 16 / 32.

Also available on each window's ⋮ menu.

## Data

| What | Where | Scope |
| --- | --- | --- |
| Favorites | `UserSettings/FavoriteAssets.json` | Per-user, per-project. GUID-based. |
| History | `UserSettings/SelectionHistory.json` | Per-user, per-project. |
| Preferences | `EditorPrefs` | Per-user, per-machine. |

Nothing touches `Assets/`.

## Compatibility

| Unity | Status |
| --- | --- |
| 6000.0 LTS | ✅ Tested |
| 2022.3 LTS | ✅ Tested |
| Older | ❌ Not supported |

Editor-only. No runtime footprint, no dependencies.

## License

[MIT License](LICENSE.md)
