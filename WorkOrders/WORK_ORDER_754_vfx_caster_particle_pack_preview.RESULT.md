# WORK ORDER 754 — RESULT (rebuild 2026-07-24)

**Status:** CODE REBUILT — owner felt-verify  
**File:** `Assets/Editor/VfxCasterWindow.cs` (full rewrite of preview path)

## Problem

Most prefabs (Lana Flamethrower, Spells cast, multi-layer Hovl) failed or showed one layer / grey boxes / black shells in the booth. Root cause: **PreviewRenderUtility + partial Simulate** is a bad fit for URP multi-layer pack VFX. D:\flames works because effects live in a **real scene under URP**.

## Architecture (new)

1. **Library** — all VFX pack roots + auto-discover + carousel (unchanged intent).  
2. **Stage** — `__VFX_Caster_Stage__` DontSave object in the open scene.  
3. **Playback** — every `ParticleSystem` Play + `Simulate(dt)` (all layers; sibling-safe).  
4. **Materials** — Built-in `Particles/*` remapped to **URP Particles/Unlit** on the **instance only**.  
5. **Helpers** — MeshRenderer cubes hidden (Lana grey boxes).  
6. **View** — dedicated stage **Camera** (UniversalAdditionalCameraData) renders to window RT with void clear.

## How to verify

1. Recompile; open **Defenders > Animation > VFX Caster**.  
2. Rescan.  
3. **Lana Flamethrower** — fire stream, **no grey box**; dig-in shows mat fixes / hid meshes.  
4. **ParticlePack FlameThrower** — continuous multi-layer stream.  
5. **Spells Casting_Fire_2** — fewer black shells after URP remap.  
6. Hierarchy shows `__VFX_Caster_Stage__`; **Frame Scene** focuses the effect.  

## Notes

- Prefab assets on disk are **not** modified.  
- Script-driven demo-only FX may still need full Play Mode.  
- Push HELD unless owner asks.  
