# WORK ORDER 286 — BUG: Swapped hero FBX import settings (green T-pose + isReadable spam)

**Status:** READY TO IMPLEMENT
**Date:** 2026-06-06
**Author:** UI (creative/architecture lane)
**Priority:** 🔴 URGENT — heroes/companions render as a solid-green, spread-eagle
T-pose on the ground and spam console errors every frame. Blocks animation WOs (285).
**Lane:** Asset import / editor — **import settings + reimport only.** Likely no `.cs`
needed. NO `VillageSceneBuilder.cs`, NO `.unity` hand-edits.
**Implemented + build-verified by:** CLI (Unity importer + batchmode reimport, editor closed).
**Root cause owner note:** introduced by the hero FBX swap (2026-06-06) — the new
`.fbx` files kept the **old `.meta`** (to preserve GUIDs/refs), so the import config is
stale against the new Tripo meshes.

---

## 1. Symptoms (owner playtest screenshot 2026-06-06)

1. Hero/companion body is **solid green**, untextured.
2. Body is in **T-pose, lying spread-eagle** on the ground (not animating, not upright).
3. Red console error, repeating: `...rigo_node_ad84a6fa (isReadable is false,
   ReadWrite must be enabled in import settings)`.

## 2. Root cause (verified in repo)

All three come from the four swapped hero FBX (`Assets/Resources/Heroes/{Cleric,Knight,
Mage,Ranger}.fbx`) carrying stale import settings from the preserved old `.meta`:

- **isReadable = 0 on all four metas** (verified). `HeroBodySwapper` bakes each skinned
  mesh and reads `baked.vertices` / `sharedMesh.vertices` at runtime (HeroBodySwapper.cs
  ~L681 & L700) to find the lowest vertex and plant the hero's feet → throws the
  `isReadable is false` error when Read/Write is off.
- **Materials/textures not extracted** for the new meshes → `TripoMaterialFixer` applies
  its **species fallback tint (green)** instead of the real texture (it logs
  `loaded=False, tintActive=True`). Same failure family as DEF-267.
- **No valid Humanoid avatar** for the new rig → nothing retargets onto the mesh →
  **T-pose**; the flat/prone orientation is the un-overridden import pose.

## 3. Fix — reimport the four hero FBX with correct settings

For each of `Cleric.fbx`, `Knight.fbx`, `Mage.fbx`, `Ranger.fbx` in `Resources/Heroes/`:

1. **Model tab → Read/Write Enabled = ON** (fixes the `isReadable` runtime read + error spam).
2. **Rig tab → Animation Type = Humanoid**, **Avatar Definition = Create From This Model**;
   verify the avatar configures (all required bones map). If the Tripo rig won't map
   cleanly, note which bones fail.
3. **Materials/Textures — bind the correct base-color map per hero.** The new FBX each
   shipped **mismatched / multiple texture sets** with non-standard names; the wrong one is
   binding → garbled camo/oil-slick colors (owner screenshot 2026-06-06). Correct base-color
   per hero (bind to `_BaseMap`/`_MainTex`):

   | Hero | Folder | USE this base color | Ignore / stray set |
   |---|---|---|---|
   | Cleric | `Cleric_tex/` | `HumanCleric_basecolor.JPEG` (+ `_metallic`, `_normal`) | — |
   | Knight | `Knight.fbm/` | `knight_basecolor.JPEG` | `remesh_12_combined_Bake_*` (raw bake — do NOT bind) |
   | Mage | `Mage.fbm/` | `fantasywitch3dmodel_basecolor.JPEG` | (no normal/metallic shipped) |
   | Ranger | `Ranger.fbm/` | `archer_basecolor.PNG` (+ `archer_normal.PNG`) | `Motion_Dummy_Female_Pbr_*` (do NOT bind) |

   - **Color space:** base-color = sRGB ON. **Metallic + normal = sRGB OFF (linear).**
     Tag normal maps as **Texture Type = Normal map**. JPEG normals (`_normal.JPEG`) compress
     badly — prefer the PNG normal where one exists (Ranger), and if a normal looks wrong,
     drop it rather than ship a broken one (Mage has none — that's fine, base color only).
   - **Reflection/metallic:** if no metallic map (Mage), leave metallic low (the code already
     sets `_Smoothness ≈ 0.15`); don't let a misread metallic JPEG blow out the surface.
   - Goal: `TripoMaterialFixer` logs `loaded=True`, no green tint, and the hero shows its real
     texture (not the camo bake). Wire each model's `SetFallbackTexture(...)` to its correct
     `Resources/Heroes/<folder>/<hero>_basecolor` path as the safety net.
   - **Fix the hardcoded fallback paths:** `HeroBodySwapper.cs:456` and
     `StoryCompanionInjector.cs:313` hardcode `"Heroes/Cleric_tex/HumanCleric_basecolor"` for
     ALL classes — update to resolve the per-class base-color path from the table above so
     Knight/Mage/Ranger don't fall back to the Cleric texture.
4. **Rotate to stand upright + fix scale** (the screenshot shows the hero lying flat and
   oversized — both must be corrected):
   - **Stand up (pitch):** the new Tripo meshes import **lying flat / wrong up-axis**. The
     existing pipeline only applies a per-class **yaw** (`forwardYaw`, HeroBodySwapper ~L91)
     for facing — it does NOT pitch the model upright. Make the mesh stand by the cleanest
     of: enabling **Bake Axis Conversion** on the FBX importer, OR correcting the import
     rotation, OR adding a root pitch (e.g. ~-90° X) so the model is vertical before
     `forwardYaw` is applied. Confirm against the FBX's authored up-axis.
   - **Scale:** heroes use `useFileScale: 1, globalScale: 1` + a runtime `NormalizeHeight`
     that scales to a target height from the mesh **Y-bounds** (HeroBodySwapper ~L583-585).
     The giant size is a *symptom* of the lying-flat import: a prone mesh has a tiny Y-extent,
     so NormalizeHeight over-scales it. Once the model stands upright (above), verify
     NormalizeHeight produces the correct standing height; if the new FBX's native unit scale
     still differs, adjust `globalScale` so the final hero matches scale.
   - **Scale reference = the other characters already in the scene.** Match the heroes to
     the existing townsfolk / People-pack NPCs and the companion that are standing correctly
     in the same scene (e.g. the dwarf villager in the screenshot, the `Assets/Models/People`
     NPCs from DEF-91). The hero should read at a believable human height next to them — not
     taller/larger. Eyeball the hero head-height against a nearby standing NPC and match it.
5. Reimport (batchmode), then rebuild the hero animator controllers
   (`HeroAnimatorFactory.BuildAll`) so the heroes animate instead of T-posing — this is
   the WO-283 step applied to the refreshed meshes.

> Preferred: encode 1–4 in an `AssetPostprocessor` keyed to `Assets/Resources/Heroes/`
> (mirrors `ActionClipImporter` for `Assets/Action/`) so any future hero FBX dropped here
> auto-gets Read/Write + Humanoid + texture handling. Avoids this recurring on the next swap.

## 4. Acceptance criteria

- [ ] No `isReadable is false / ReadWrite must be enabled` errors in the console during play
      (grep Player.log clean).
- [ ] All 4 heroes render with their **correct base-color texture** (per the §3 table) —
      no solid-green tint AND no garbled camo/raw-bake colors. Stray texture sets
      (`remesh_*`, `Motion_Dummy_*`) are not bound.
- [ ] Normal/metallic maps are linear (sRGB off), normals tagged as Normal map; no JPEG
      normal artifacts. Per-class fallback paths fixed (no Cleric texture on other heroes).
- [ ] All 4 heroes stand **upright** (not lying/prone) and animate (idle at minimum), not T-pose.
- [ ] All 4 heroes are scaled to **match the other characters already in the scene** —
      head-height comparable to a nearby standing townsfolk/People NPC and the companion;
      no oversized/giant hero. Verify in a screenshot next to an NPC.
- [ ] `HeroBodySwapper` foot-grounding works (no exception from the vertex read).
- [ ] If an AssetPostprocessor is added: a re-dropped hero FBX auto-imports correct;
      brace-check the `.cs` (CLAUDE.md §1).
- [ ] Compile-gate + play smoke test green (CLI). RESULT notes any bones that failed to map.

## 5. Sequencing

Run this **before WO-285** (3D combat anims) and alongside/just after WO-283 — the heroes
must import valid (Read/Write + Humanoid avatar + textures) before any animation work shows
correctly. Recommend slotting at the **front** of `OVERNIGHT_QUEUE_2026-06-06.md`.

## 6. Notes

- Caused by the FBX swap preserving old metas — I flagged the avatar-mapping risk at swap
  time; Read/Write + texture extraction are the broader version of that same mismatch.
- Pre-swap originals (working metas/textures) for reference/diff:
  `Backups/hero_fbx_20260606_005717/`.
- Related: DEF-267 (Tripo colorless), DEF-102 (hero death pose), DEF-249 (oversized scale).
- Linear: workspace at free-issue limit — assign from this file until a slot frees.
