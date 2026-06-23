# Offset Forge

**A tiny Blender-style viewer for fixing model attachment offsets in Unity.**
Load a model → rotate X/Y/Z + position → read the exact offset → copy or save it. No play mode, no rebuilds, no guessing.

---

## The problem it solves

A weapon, shield, prop, or accessory won't sit right when you attach it to a hand, socket, or mount point — so you type rotation values blind and hit Play over and over to check. Offset Forge replaces that loop with a 60-second visual one.

It's also the **one step an AI assistant can't do for you**: a script can write the attach code, but it can't *look* at the model and know it needs `-45°` on Z. You set that once, by eye; the numbers are correct forever after.

## Features (v1.0)

- 🪟 **Single Editor window** — `Tools ▸ Offset Forge`. Zero scene changes, zero settings.
- 📦 **Load any model or prefab** — drag-and-drop or object-picker.
- 🔄 **Blender-style viewport** — orbit (left-drag), zoom (scroll), pan (middle/Alt-drag).
- 🎚️ **Rotation X/Y/Z + Position X/Y/Z** — type or drag; live two-way with the model. Optional 5°/15° snap.
- 🔢 **Exact live readout** — euler + local position, to 2 decimals.
- 📋 **Copy** — clipboard as a paste-ready `Vector3` / `Quaternion.Euler(...)`.
- 💾 **Save to JSON** — append/update per-model offsets in a flat, human- and AI-readable file.
- 🚫 **Zero runtime footprint** — Editor-only assembly; adds nothing to your build. Never modifies your assets.

## Install

1. Unity **2021.3 LTS or newer** (tested through Unity 6).
2. Import the package. Everything lives under `Assets/OffsetForge/`.
3. Open `Tools ▸ Offset Forge`. Done.

## Quick start

> Full walkthrough in [`Documentation/Documentation.txt`](Documentation/Documentation.txt).

1. `Tools ▸ Offset Forge`
2. Drop a prefab/model into the **Model** field.
3. Orbit and dial **Rotation/Position** until it sits right.
4. **Copy** the value into your code/Inspector, or **Save to JSON** for a data-driven setup.

```csharp
// Copy/paste result:
t.localRotation = Quaternion.Euler(135f, 170f, -45f);
t.localPosition = new Vector3(-0.05f, 0f, 0f);
```

## Data-driven use

`offsets.json` is flat and version-control friendly — keep it next to your prefabs as the source of truth for "how this thing attaches." An optional, dependency-free `OffsetTable` loader is included under `Runtime/` (delete it if you bring your own).

## Roadmap (not in v1 — kept deliberately small)

- Socket/bone preview (align against a real hand bone, not just world axes)
- Multi-offset presets per model
- A separate companion tool for mesh **decimation / LOD** (its own listing — out of scope here on purpose)

## Support

samanthadenelle@gmail.com — include your Unity version and the model/format you were aligning.

## License

Sold under the Unity Asset Store EULA. © 2026 Samantha DeNelle.
