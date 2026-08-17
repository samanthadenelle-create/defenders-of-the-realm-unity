<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-17
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-17) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 745 — Room Forge: Regression Oracle + FlowTrace Instrumentation

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-18 (next-free 745 per the 2026-07-18 banner refresh; RECORD THIS MINT —
bump banner next-free to 746 in the WO-743 canon-close commit, which is the current banner's
active editor)
**Seat:** UI/design (Cowork session, owner-directed) — spec only
**Owner (PO):** Sam
**Priority:** P1 — gates trusting the Room Forge pipeline for real dungeon authoring
**Silo:** Editor / Dungeons · **Effort:** M
**Depends on:** WO-740 merge (DONE) · WO-741 prefab library (DONE, commit a4fb5cf0) · run after
742/743 close or in parallel on a clean lane (file-disjoint from 742/743 except README/canon)
**Canon:** `docs/INSTRUMENTATION_STANDARD.md` (FlowTrace/Guard authoring law) ·
`ARCHITECTURE_PRINCIPLES.md` §2c (tests are the permission gate) · CLAUDE.md §12
(instrument-don't-guess) · `WORK_ORDER_PROGRAM_740_743_room_forge_into_mainline.md`

---

## 1. Goal

The Room Forge pipeline (RoomSocket / RoomPrefabMeta / DungeonComposeLayout / RoomForgeWindow /
DungeonBaker / DefaultDungeonRoomsBuilder) is implemented and merged. It currently has ZERO
regression coverage and logs via bare `Debug.Log("[DungeonBaker] ...")` instead of the FlowTrace
standard. Add (a) a headless regression suite wired into the standard oracle chain and (b)
FlowTrace/Guard instrumentation, and (c) fix the two contract holes found in code review below —
they are what the tests must pin.

## 2. Code-review findings to FIX (verified from source, 2026-07-18)

1. **Soft hard-gate.** `DungeonBaker.BakeFromFile` counts `mateFail` but still seals, bakes
   NavMesh, saves the scene and calls `EnsureInBuildSettings` (`DungeonBaker.cs:252-261`), only
   logging the error after. Contract fix: on `mateFail > 0` (or any missing-instance/missing-
   socket/type-mismatch), do NOT save the scene and do NOT touch Build Settings — bake aborts
   with the failure summary. (Optional: save to `Assets/Scenes/DungeonCompose/_FAILED_<id>.unity`
   OUTSIDE build settings for debugging, behind an editor pref, default off.)
2. **Order-dependent mate nudge.** The planar nudge slides the whole "to" room to close a gap
   (`DungeonBaker.cs:181-187`). A later connection can move a room an earlier connection already
   mated; the earlier `matedTo` stamp survives while the geometry drifts apart. Fix: after all
   connections process, run a RE-VERIFY pass — every connection must still satisfy
   `dist <= maxMateDistance && align >= threshold`; any drift = mate failure (feeds fix 1). Add a
   room-bounds overlap check (AABB from `RoomPrefabMeta.FootprintWorld` at final positions;
   overlap beyond a small tolerance = failure).

## 3. FlowTrace instrumentation (per INSTRUMENTATION_STANDARD)

Convert baker/forge logging to the FlowTrace band pattern (keep human-readable text, add the
band + stable keys so the F8/headless harvest can filter):

- **`[Flow:DungeonBake]`** in `DungeonBaker`: layout loaded (id, rooms, connections, cellSize,
  rules snapshot) · per-room instantiate (instId, prefab | PLACEHOLDER + why) · per-mate attempt
  (connId, dist, align, nudge magnitude, OK/FAIL + fail reason enum: missing-instance /
  missing-socket / type-mismatch / distance / alignment / drift / overlap) · seal events (socket,
  WALL vs SECRET) · pacing ratios · navmesh result (walkable sample + path-connectivity result,
  see §4 case 8) · scene save path · one machine-parseable summary line
  (`id= rooms= matesOk= matesFail= sealed= saved=`), which the regression suite asserts on.
- **`[Flow:RoomForge]`** in `RoomForgeWindow` save path + `DefaultDungeonRoomsBuilder`: room
  saved (roomId, archetype, footprint, socket count/types), catalog write (path, entry count),
  dual-copy write result.
- **Guards** (never throw past the tool): JSON parse fail, empty layout, missing prefab folder —
  loud in log with the band, actionable message, no half-baked scene left open.
- Editor-only tooling may log loudly; any FUTURE runtime loader must follow the player-quiet law
  (loud only to db/log). Note it in the README so the seam is not forgotten.

## 4. Regression suite — `Assets/Editor/Regression/RoomForgeRegression.cs`

Marker `ROOMFORGE_REGRESSION_OK` / `ROOMFORGE_REGRESSION_FAIL`, runnable standalone
(`run-unity-method DeNelle.Editor.Regression.RoomForgeRegression.RunAll`) AND wired into
`DataRegression.RunAll` as `[room-forge]` (same pattern as the `[ui-mvvm]` ratchet). Must run
headless/batchmode (`-nographics` is fine — no rendering asserted). Synthetic scenes are built
in-memory in a NEW empty scene and discarded; the suite never opens or saves shipping scenes.

Cases (each its own labelled check; suite reports pass/fail per case):

1. **Catalog integrity:** `rooms-catalog.json` parses; every `RoomCatalogEntry.prefabPath`
   resolves to a prefab; the prefab's `RoomSocket` components match the catalog row (socket ids,
   types, count) and `RoomPrefabMeta.roomId == entry.id`; 17/17 rooms from WO-741 present.
2. **Dual-copy law:** `dungeon-layouts/*.json` + `rooms-catalog.json` byte-identical between
   `StreamingAssets/Data/Canonical/` and `Resources/Data/Canonical/` (DATAWEB drift is a standing
   red — do not add to it).
3. **TypesCompatible matrix:** Door-Door, Arch-Arch, Door-Arch (both directions), StairUp-
   StairDown (both) = compatible; Door-StairUp, Door-StairDown, Arch-Stair* = incompatible.
   (Expose the private statics to the suite via `internal` + `InternalsVisibleTo`, or a small
   public pure facade `DungeonBakerChecks` — do NOT duplicate the logic in the test.)
4. **Mate math on synthetic rooms** (two placeholder rooms, procedurally socketed): exact touch
   OK · within `maxMateDistance` OK · off-by-slightly → nudge closes it, OK, nudge magnitude
   traced · beyond nudge reach → FAIL · facing same direction (align < threshold) → FAIL ·
   yaw 90/180/270 rotated rooms mate correctly when sockets oppose.
5. **Seal behavior:** unmated normal socket → `matedTo == "SEALED_WALL"` + a `Seal_<id>` cube
   scaled to `halfWidth * 2` · unmated `isSecret` socket → `matedTo == "SEALED_SECRET"` and NO
   geometry spawned · `rules.sealUnmated == false` → sockets left open and counted in the
   summary.
6. **Hard gate (fix 1):** a layout with one forced type-mismatch → bake produces NO scene file
   and NO Build Settings entry; summary line reports the failure reason.
7. **Re-verify + overlap (fix 2):** a 3-room branch layout authored so the second connection's
   nudge pulls a room off the first connection → bake FAILS with reason `drift`; a layout with
   two rooms placed on the same cell → FAILS with reason `overlap`.
8. **NavMesh connectivity:** bake the default spine; `NavMesh.CalculatePath` from first room
   centre to last room centre returns `PathComplete` (stronger than the current single
   `SamplePosition` at origin).
9. **Sample layouts green:** `d4_sunken_crypt_spine.json` and `demo_branching_kit.json` bake
   with `matesOk == connections.Count`, `matesFail == 0`, sealed count == expected constant
   (pin the number once 742 lands and assert it — catches silent socket edits).
10. **Determinism + hygiene:** baking the same JSON twice yields identical room positions/yaw
    (compare transforms) and identical summary counts; repeated bakes do not duplicate the
    scene's Build Settings entry.

Baseline: suite starts at 0 known failures — no pre-exister allowance for a brand-new pipeline.

## 5. Files to edit

- `Assets/Editor/RoomForge/DungeonBaker.cs` — fixes 1+2, FlowTrace conversion, summary line,
  path-connectivity check.
- `Assets/Editor/RoomForge/RoomForgeWindow.cs` + `DefaultDungeonRoomsBuilder.cs` — `[Flow:RoomForge]` traces on save/catalog writes.
- `Assets/Editor/Regression/RoomForgeRegression.cs` — NEW suite.
- `Assets/Editor/Regression/DataRegression.cs` (or its registrar) — add `[room-forge]` to RunAll.
- `Assets/_Modules/Dungeons/RoomForge/README.md` — verify section + runtime-quiet note.
- (If facade route chosen) `Assets/Editor/RoomForge/DungeonBakerChecks.cs` — NEW pure helpers.

## 6. Do NOT touch

- No behavior changes beyond §2 fixes (no new features, no endless composer, no KayKit art
  dressing, no runtime DungeonController hook — separate WOs).
- No hand-edit of shipping `.unity` scenes; suite works only in throwaway scenes.
- No `Assets/Models/KayKit/**` direct references from the suite (gitignored on fresh clone —
  the suite must pass with the pack ABSENT; placeholder path covers it).
- Healers-cottage `DungeonLayout` wall-run format stays untouched.
- Brace/NUL gate on every .cs; sole-committer discipline; explicit paths.

## 7. Acceptance

1. CompileGate `COMPILE_GATE_OK` after all edits.
2. `RoomForgeRegression.RunAll` headless → `ROOMFORGE_REGRESSION_OK`, all 10 cases pass, on a
   checkout WITH and WITHOUT the KayKit pack present.
3. `DataRegression.RunAll` includes `[room-forge]`; overall baseline unchanged otherwise
   (3 known pre-existers only).
4. A deliberately broken layout (type mismatch) demonstrably produces no scene + no Build
   Settings entry + a `[Flow:DungeonBake]` FAIL summary (paste the captured line in RESULT —
   §12: the captured line proves the gate).
5. Re-bake of the WO-742 demo layout still green end-to-end after the gate hardening.
6. RESULT file `WorkOrders/WORK_ORDER_745.RESULT.md` with evidence; banner records 745 and
   next-free 746 in the same canon commit.
