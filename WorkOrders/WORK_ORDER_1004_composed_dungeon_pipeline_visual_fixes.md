> ## RECONCILED 2026-08-08 - true status is PARTIAL (mostly done)
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: issues 1 and 2 fixed by fab50709
> (`RoomForgeMaterials.cs:29` + `StripAllTextures` + `ROOM_SURFACES_OK`); issue 3 fixed by 94c23be3
> (`DungeonBaker.cs:331-338` fog). Residual: sec. 1.3 is only a SEAT - `DungeonDresser.cs:351` is a
> marker for the Env_Candle wick flame, not a light.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 1004 — Composed-dungeon (Pipeline A) visual fixes: rainbow floor, stray markers, enclose + relight

**Status:** PARTIAL - mostly done; sec. 1.3 candle light still a seat (reconciled 2026-08-08) · **Silo:** Dungeons/art/pipeline · **For:** CLAUDE CLI · **Date:** 2026-08-07
**PO:** Samantha (owner) · **Author:** UI seat · **UI-seat block:** 1000–1099
**Owner (felt-test 2026-08-07):** "dungeon issues" on the composed dungeon (`dg_starter_loop`).
**Fix at the PIPELINE level** (the bake), so every composed dungeon — incl. the WO-1001 deep dungeons — comes out clean by default. Complements WO-1000 (which fixed the hand-coded outpost) and WO-1001 (which extends this pipeline).

## 0. The three issues (from the screenshot)
1. **RAINBOW FLOOR** — a floor tile shows the raw KayKit atlas as multicolored stripes ("rainbow on a cube"). `RoomForgeMaterials.cs` is meant to strip the atlas to solid stone (`WallStone`/`FloorStone`/`AccentStone`, L36-39) — a floor piece slipped past the strip.
2. **STRAY PLACEHOLDER MARKERS** — small purple/magenta + green squares floating on the wall tops. Debug/socket/POI markers (or magenta = broken material) leaking into the built scene.
3. **OPEN-SKY GREYBOX** — flat untextured walls, no ceiling, bright daylight/blue sky. The WO-1000 enclose+relight treatment never reached the composed pipeline.

## 1. Fixes (in the Pipeline-A bake — composer/baker/dresser/materials)
1. **Kill the rainbow floor.** In `RoomForgeMaterials.cs`, ensure the atlas-strip runs on **every** room surface — floors, walls, accents, AND any stair/prop piece — so no mesh ever renders the raw KayKit atlas. Audit `DefaultDungeonRoomsBuilder` floor pieces + `DungeonBaker`/`DungeonDresser` for any piece that keeps the atlas material; force them all to the solid stone mats (or a properly-UV'd stone texture). **No surface shows the rainbow.**
2. **Remove the stray markers.** Identify the purple/green squares (start with `RoomSocket`/socket gizmos, `PoiBeacon`, `DungeonDresser` tokens, and magenta-broken materials from missing shaders/textures under URP). **They must not render in a built scene** — either strip them at bake time (they're author/debug aids) or fix the broken material. If any are magenta = a URP shader/texture miss, re-shade like the VFX URP-proof pass.
3. **Enclose + relight (extend WO-1000 to the pipeline).** Bake these into every composed dungeon (mirror `DungeonSceneBuilder`'s proven values — WO-1000 §1):
   - **Ceiling** pass (KayKit `ceiling_tile` or a dark cap) so no sky leaks.
   - **Kill daylight:** `RenderSettings` — flat ambient ~0.05, dark-blue linear fog 14→42m, near-black camera bg, dark/no skybox; drop any directional to a faint fill (`DungeonBaker` currently sets ambient 0.08 + a 0.35 directional but **no skybox override**, so sky still shows — override it).
   - **Candle-VFX lighting + haze (precise — from the D:\flames sandbox study):** light the rooms with **`Env_Candle`** `VfxEmitter` — the **subtle TinyFlames wick mirror** (`Assets/Resources/VFX/Env/Env_Candle.prefab`, scale ~0.45, wick-only, NOT a double-mesh, NOT a big flame) — seated on the KayKit torch/sconce props, **replacing the dead/oversized torch fire.** Each candle's flicker Light is the illumination. **Big / room fire (hazards) is a SEPARATE recipe (MediumFlames / WildFire), never the candle** — keep candle light and hazard fire distinct. Add subtle ground fog/haze (WO-890 rule — never a plume). ⚠ **URP soft-particle flags:** the soft candle layers need URP **depth texture** (flipped in WO-759) **+ opaque texture / HDR ON**, or the flame renders flat vs the sandbox — verify `Assets/Settings/DeNelle-URP.asset` has them on.
   - Textured stone shell where feasible (or clean solid stone — anything but flat daylight-lit greybox).

## 2. Acceptance
- [ ] **No rainbow surfaces** anywhere in a composed dungeon (floors/walls/stairs all solid/clean stone).
- [ ] **No stray purple/green/magenta markers** render in the built scene.
- [ ] Composed dungeons are **enclosed** (ceiling, no sky), **moody** (dark ambient + fog, candle-VFX lights), not daylight greybox — hits the WO-1000 bar.
- [ ] Applies at the **pipeline** level: re-baking `dg_starter_loop` (and any WO-1001 dungeon) produces a clean, enclosed, relit scene with no per-scene hand-fixing.
- [ ] `COMPILE_GATE_OK` + bake markers + `UI_CAPTURE_OK` — **headless-capture a re-baked composed dungeon, open the PNG**, confirm all three issues gone.
**Owner felt-close:** the composed dungeon reads like a real dungeon (enclosed, lit, clean surfaces), no rainbow, no floating debug squares.

## 3. Wins to keep (verified landed, do not touch)
The **wide compass strip** + **analog joystick** (WO-899) are live and correct in this build — leave them.

## 4. RESULT
`WorkOrders/WORK_ORDER_1004_composed_dungeon_pipeline_visual_fixes.RESULT.md` — the rainbow-strip fix, what the stray markers were + how removed, and before/after screenshots of a re-baked composed dungeon.
