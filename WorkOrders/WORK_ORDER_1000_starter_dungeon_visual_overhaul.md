> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit 6c740b08 rewrote
> `KayKitChallengeOutpostBuilder.cs` (+929 lines) and rebaked `KayKitChallengeOutpost.unity`
> from 88,484 bytes to 569,188 bytes.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 1000 — Starter dungeon (KayKit Challenge Outpost) visual overhaul

**Status:** FIXED (reconciled 2026-08-08; owner felt-verification outstanding) · **Silo:** World/art/dungeon · **For:** CLAUDE CLI · **Date:** 2026-08-07

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*
**PO:** Samantha (owner) · **Author:** UI seat · **UI-seat block:** 1000–1099 (new; 860–899 closed)
**Owner:** *"STARTER DUNGEON IS HORRIBLY NOT DONE WELL VISUALLY."* Bring it up to the **Healer's Cottage** bar.

## 0. What's broken (grounded — all in ONE builder)
The cave-portal starter dungeon is `KayKitChallengeOutpost.unity`, built by
**`Assets/Editor/KayKitChallengeOutpostBuilder.cs`** (`Build()`), reached via `CavePortalRepointInjector.cs:49`
(`NewTarget="KayKitChallengeOutpost"`). Every visual fault is in that builder:
- **Bright daylight + visible sky:** one directional `Sun` at intensity 1.1 (L52-57) and **`RenderSettings` is never touched** → default bright-blue procedural skybox + bright skybox ambient. That's the daylight wash and the blue sky over the walls.
- **No ceiling:** `Build()` lays only floor + ring walls (L66-73) — no ceiling pass, so the sky leaks and it reads as a pit in a field.
- **Flat-color primitive cubes, no texture:** walls/floor are `CreatePrimitive(Cube)` via `MakeBox` (L257-272) with solid-color URP/Lit mats (`EnsureMats` L312-320) — no `_BaseMap`. The "barrels/crates" are 1m brown `_crate` cubes (`PlaceBreakables` L234-250), not meshes.
- **Torches emit no light:** `DressTorches` (L186-201) places KayKit torch FBX but **adds NO Light component** — pure decoration, zero illumination.

## 1. The bar to copy — Healer's Cottage (`DungeonSceneBuilder.cs`) already does it right
Port these proven techniques into the outpost builder (reuse the methods/values, don't reinvent):
- `ConfigureAmbient()` (L1987-2000): `ambientMode=Flat`, `ambientLight≈(0.05,0.05,0.055)`, `RenderSettings.fog=true`, dark-blue linear fog `#0a0a10` 14→42m, near-black camera background (L2067), faint 0.18 directional (L2018-2029).
- `BuildCeiling` (L643-658): closes each room top with KayKit `ceiling_tile.fbx` — **no sky leak.**
- `FloorPiece`/`WallPiece` (L1944-1969) + `LoadModel`/`InstantiateModel` (L1758-1799): real **KayKit Dungeon Remastered** textured wall/floor pieces (atlas `Assets/Models/KayKit/dungeon/dungeon_texture.png`).
- `LitFixture` warm pooled point lights (braziers/candles) that actually cast mood against 0.05 ambient.

## 2. The overhaul (edit `KayKitChallengeOutpostBuilder.cs`, then re-bake the scene)
1. **Kill the daylight / enclose the top (the #1 fix).** Add a `ConfigureAmbient()`-equivalent: flat ambient ~0.05, dark-blue linear fog 14→42m, near-black camera bg, and **replace/kill the bright skybox** (dark solid or none). Drop the `Sun` to a faint ~0.15 fill (or remove). **Add a ceiling pass** (KayKit `ceiling_tile.fbx`, mirror `BuildCeiling`) so no sky shows.
2. **Textured stone shell.** Replace the flat-color cube walls + floor with **KayKit Dungeon Remastered** textured wall/floor pieces (reuse `FloorPiece`/`WallPiece`). Keep the corner KayKit towers. (If tiling the atlas on big pieces rainbows, use the kit's own modular pieces at their authored scale, as Healer's Cottage does — do not stretch the atlas over a cube.)
3. **LIGHTING = candle VFX (owner ruling).** Light the room with the **`Env_Candle` `VfxEmitter`** prefabs (WO-884/885) on the wall brackets: the candle flame + its **flicker child Light** become the PRIMARY illumination. Against 0.05 ambient they pool warm, living light. Replace the dead decorative torches (or add the candle emitter + a warm point light to each). This makes the relight an application of the common VFX facade, not one-off scene lights.
4. **Mist / smoke / haze (owner ruling).** Add **low-lying ground fog/haze** across the floor + faint **drifting smoke** through the candlelight, via `VfxEmitter` (Smoke & Steam recipes / `Env_GroundFog`). **SUBTLE** — per the WO-890 subtlety ruling: atmosphere and depth, never a plume that hides the room.
5. **Real props.** Replace the brown crate cubes with real KayKit **`barrel_*` / `crate_*` / `chest_*` FBX** (as `DungeonDresser.cs` FloorTokens L42-46 does); keep the `BreakableContainer.lootTableId` wiring. Ground them, arrange with intent (clusters, against walls), add a little variety.

## 3. Acceptance criteria
**Felt (owner closes):**
- [ ] The dungeon reads as an **enclosed interior** — NO blue sky, NO field/trees visible over the walls (ceiling + fog + dark skybox).
- [ ] Walls + floor are **textured stone** (KayKit), not flat brown cubes.
- [ ] It's **moody and candle-lit** — low ambient, the candle-VFX flicker Lights pool warm light with real shadow falloff (torches finally matter).
- [ ] Subtle **fog/haze** adds depth without hiding the room.
- [ ] Barrels/crates are **real props**, grounded and arranged — not floating cubes.
- [ ] It hits the **Healer's Cottage bar** (compare side by side).
**Engineering:**
- [ ] Change is in `KayKitChallengeOutpostBuilder.cs` + re-bake `KayKitChallengeOutpost.unity` in batchmode (never hand-edit the .unity, §3).
- [ ] Reuse `DungeonSceneBuilder`'s proven methods/values (ambient/fog/ceiling/textured pieces) — don't reinvent.
- [ ] `COMPILE_GATE_OK` + the scene-build marker + `UI_CAPTURE_OK`. **Headless-capture the rebuilt dungeon and open the PNG** — confirm enclosed + textured + candle-lit + foggy before handing to owner.

## 4. Follow-up (note, not this WO)
The composed sibling `dg_starter_loop` (`GraphDungeonComposer`/`DungeonBaker`/`DefaultDungeonRoomsBuilder`, reached via the EAST world arch) has the **same disease** — open-sky, no ceiling, `RoomForgeMaterials` strips textures to flat stone. Apply the same enclose+texture+candlelight pass there in a follow-up WO so both player-reachable dungeons match.

## RESULT
`WorkOrders/WORK_ORDER_1000_starter_dungeon_visual_overhaul.RESULT.md` — before/after screenshots.
