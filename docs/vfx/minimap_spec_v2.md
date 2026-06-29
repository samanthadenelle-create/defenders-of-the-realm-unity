# Overworld Minimap — spec (V2 navigation polish, QUEUED — NOT V1)
2026-06-23 · owner asked "could help if not too expensive" · **queued behind the V1 push, do not ship now**

## Cost (the owner's question)
- **(a) live second Camera → RenderTexture: NOT recommended.** Renders the world every frame → real mobile fill/draw cost. Reserve for V3+ (live shading/threat overlay).
- **(b) static/baked map + transformed icon dots: RECOMMENDED — near-zero per-frame cost** (just RectTransform moves; no GPU). This is the pattern `VillageHudController`'s town mini-map already uses.

## Cheap design (extend the CompassHud pattern)
- Small corner uGUI panel (~120×120), code-built (mirror `CompassHud` bootstrap, runtime-spawned, no scene wiring).
- Backdrop: static dark-glass Image (or a baked 256×256 top-down map sprite later; fallback = solid glass).
- Hero dot centred (or world→map transformed); rep/enemy dots from `OverworldEncounterSpawner.Instance` reps (cull >150u); optional region/threat tint from `ZoneManager.DangerTierAt(pos)` (visual only).
- Fixed linear world→map projection (mirror `VillageHudController.ProjectMiniMap`), clamp dots to panel.

## Reuse vs new
- REUSE: CompassHud uGUI build + hero-resolve reflection · `OverworldEncounterSpawner` rep list · `ZoneManager` danger tier · `HudTheme` glyphs/`ElarionUiKit` palette · safe-area inset.
- NEW: `Assets/_Modules/HUD/OverworldMinimapHud.cs` (~180 lines: Build/UpdateMap/ProjectWorldToMap/Cull/ApplyThreatTint) + a tiny bootstrap (~30). Gate behind `ff.overworldencounter`.

## Headless-verifiable
Pure data/reflection (hero pos + rep list) — assert dot count/positions update; no play-mode needed.

**Fold in after the V1 overworld push is felt-stable.**
