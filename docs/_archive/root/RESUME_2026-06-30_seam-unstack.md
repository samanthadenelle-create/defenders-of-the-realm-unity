# RESUME — 2026-06-30 — Castle↔OuterWorld seam: un-stack OuterWorld

> Save point for the owner's office switch. Read this first to resume. Dated/frozen.
> **Status 2026-07-03:** still parked; encounter-return 7.1km confirmed 6/6 by fleet 07-03; publisher critique ranks closing it #1. This doc remains the resume path (FROZEN-VALID).

> ## 🟢 LATEST — 2026-06-30 (afternoon, THIS machine) — ROLLBACK RECONCILED + TERRAIN FIXED + COMMITTED
> **This supersedes the office-session "UN-STACK" block below for CURRENT state.** The un-stack
> (`WorldGeometry.cs`) is NOT on this machine — only the seam-flag + overnight features were; the office
> un-stack work lives in `stash@{0}` (entangled with the carve).
> - **Framing correction:** the 8:24 PM revert `78a8a1a5` was the **BAD/meltdown** state (depressed ground).
>   The GOOD ~10 PM fix was **never committed** (owner told CLI to commit+lock; it didn't) — only survived in
>   `stash@{0}`. Reconstructed + committed today.
> - **Rollback:** OuterWorld scenes/navmesh/builder-code = known-good. The binary terrain was git-restored
>   and would NOT load → **re-baked** instead (the correct mechanism for a generated binary asset).
> - **Depression fix — committed `37ae7cb1`:** `CastleDepressionDepth -3f → 0f` + `ExteriorTerrainBuilder.
>   BuildExterior` rebake → `TERRAINDIAG surfaceY @x=0..70 = 0.000` (was −3.000); 0 terrain-null errors;
>   crossing OK, 4/5 gates; owner felt-verified ("the right one").
> - **TRUE ROOT — committed `825f6af2`:** `.gitattributes *.asset text eol=lf` was EOL-corrupting the BINARY
>   `ExteriorTerrainData.asset` on commit (−26 CR bytes → "Unknown error occurred while loading" → null terrain).
>   Fixed: force `*TerrainData.asset / NavMesh-*.asset / LightingData.asset binary`; terrain re-stored verbatim
>   (3,415,548 B, byte-exact round-trip). Memory: `gitattributes-binary-asset-eol-corruption`.
> - **NOT pushed** (local `37ae7cb1`, `825f6af2`). NEXT: owner push OK → then selectively "bring a little back"
>   from `stash@{0}` (carve + overnight features, one verified slice at a time).

> ## ✅ UPDATE — UN-STACK IMPLEMENTED + BAKED + BUILT (2026-06-30, office session)
> The un-stack is DONE in code and bakes/builds clean; a fleet is verifying it at the time of writing.
> - **NEW `Assets/_Modules/Core/World/WorldGeometry.cs`** — single source of truth. `OuterWorldOffset = (0,0,-2000)`;
>   `SouthGateSeamLanding` (south-frame); `ToOuterWorldWorld/Local` helpers.
> - **`ZoneManager` is now FRAME-AWARE** (`ResolveLocal`): OuterWorld-side positions classify in local space
>   (subtract offset), castle-side classify raw. This is the WO-483 landmine, handled — callers unchanged.
> - **6 builder/injector files offset** via `WorldGeometry.ToOuterWorldWorld(...)`: ExteriorTerrainBuilder
>   (`TerrainCenterZ = OuterWorldOffset.z`, const→static readonly), OuterWorldBuilder (anchors/nodes),
>   OuterWorldCavePortalBuilder (caves/portals), OuterWorldBoundaryInjector (ring), RaidOutpostSystem (outposts),
>   RuntimeRegionGate (seam landing: `_landingWorld = ToWorld(_landing) + OuterWorldOffset` at the 3 dest sites;
>   castle-side entry/threshold STAY at origin).
> - Verified: `COMPILE_GATE_OK` · `BakeFullWorld` → castle `GATE_NAV_OK`, **zero mine-node zone-mismatch warnings**
>   (frame-aware classifier holds), caves = `Cave_Skull` at offset · `[build] SUCCESS`.
> - **PENDING:** fleet runtime proof (SPAWN_TO_GATE + hero warps on-mesh at −2000, no ping-pong) → then owner felt-test.
> - Open watch (post-bake, non-blocking): `CavePathEndZ/StartZ` were offset as absolute world-Z — verify the cave
>   corridor isn't double-shifted (cosmetic). `TagManager.asset` parse line = pre-existing noise (unmodified vs HEAD).

## WHERE WE ARE (the seam work this morning)
The owner woke unable to walk south across the castle→OuterWorld seam (worked at 9pm). RCA traced it to
the **overnight world-bake work** (uncommitted on top of HEAD `78a8a1a5`, the 20:24 "known-good" revert).
The overnight assignment = the **survival-crafting + no-auto-heal + full Castle→Outerworld→Outpost→Dungeon→
Portal chain** WO. Its FEATURE half (potions, no-auto-heal, talents, HeroAbilities) is wanted/keep; its
WORLD/SEAM half (carve-for-continuity) regressed the working warp crossings + left magenta nav planes.

## DONE + VERIFIED THIS SESSION (data-backed)
1. **Seam nav fixed.** `FeatureFlags.CastleEditorBridgeSeam` default `true → false` (its own comment said
   it should be OFF). On rebake, `CastleHubBuilder.cs:1525` calls `RemoveCastleBridgeSeam()` → destroyed the
   stacked editor deck (`Bridge_Deck_Visual` / `Floor_Bridge_Nav`) that split the south navmesh.
   - Proof: bake `GATE_NAV_OK :: EXITABLE — PathComplete, hero within 1.4m of the seam`.
   - Fleet (4 bots, build verified): `crossing OK (reachable by proximity)`, `6/8 gates reached` (was 2/5),
     `magenta=0`, `SPAWN_TO_GATE_OK [South] PATH-COMPLETE`.
2. **Caves shrunk.** `OuterWorldCavePortalBuilder.CaveMouthPrefab` → `_M/Prefabs_M/Nature_M/Stones_M/
   Cave_Skull.prefab` at native scale (was the giant rail-hill tunnel / island-cave shell at 1.5×). All 3
   cave mouths placed as Cave_Skull. (Owner asked: "very simple item, not so large.")
3. **Magenta castle planes** — the bake rebuilt the nav floors invisible + RemoveCastleBridgeSeam took out
   the bridge planes; `CastleNavPlaneScrub.HidePlanes` ran (`hid 0 (2 not found)` = already clean). Magenta is
   headless-blind → owner felt-verifies it's gone.
- Pipeline run: `COMPILE_GATE_OK` → `BakeFullWorld` → scrub → `[build] SUCCESS` (05:09) → fleet.

## THE LIVE BUG (why the owner still can't cross) — and the decision
Felt-test: walk south → land in OuterWorld → **auto-yanked back to the castle exit → repeat (ping-pong).**
- **Data-proven** (Player.log, captured at the slip): two consecutive paired-crossing warps —
  `crossing 'rgate_castle_to_outerworld' -> spawned at partner (-4.37,0,-62.60)` (castle) then
  `... (-4.37,0.5,-66.00)` (OuterWorld). The endpoints are only **~3.4m apart** because **MainCastle_Hall and
  OuterWorld are baked at the SAME origin (stacked).** The `HeroLinkCrossing` re-arm guard (`_crossArmed`,
  re-arm-when-clear-of-all) is defeated by the overlap → ping-pong.
- This same stacked-origin root also causes the chronic **dual-navmesh-at-origin** error.
- **CORRECTION (map agent):** the ~7km "hero NOT returned" is a **RED HERRING — not a bug, do NOT touch.**
  `BattleArena.ArenaCentre = (5000,0,5000)` is a deliberate far-off staging arena; encounter returns use
  `EncounterParams.ReturnPosition` captured LIVE from the hero's position (`OverworldEncounterSpawner.cs:698`),
  so they self-correct and move with the offset automatically.

### DECISION (owner-ratified 2026-06-30): UN-STACK OUTERWORLD — bake it ~1km away
Chosen over (a) a band-aid 2s post-cross lockout, and (b) collapsing OuterWorld into gates-off-the-castle.
Rationale: OuterWorld is canon-designated as the explorable real-time-combat region (roaming reps → isolated
arena), so it earns its place; un-stacking fixes ping-pong + dual-navmesh + the 7km return AT THE ROOT.
This is the long-deferred **WO-453 un-stack**. NOT a band-aid.

## IN-FLIGHT
- A read-only **mapping agent** (`Explore`) is running to map the un-stack surface: every coord that assumes
  OuterWorld-at-origin (cave/outpost/mine-node positions, `ZoneManager` extents, the 4-gate landings via
  `RuntimeRegionGate.ToWorld()`+`FallbackLandingNoGeom`/`WorldGeometry.SouthGateSeamLanding`, return coords),
  the single cleanest offset insertion point (ideally: offset the OuterWorld root before the navmesh bake),
  and landmines (4-side rotation, WO-483 origin-centering, WorldSceneLoader additive load). **Its report is
  the change set** — read it first on resume.

## NEXT STEPS (exact, in order) when resuming
1. Read the mapping agent's report → confirm the single-offset insertion point.
2. Define ONE offset constant (e.g. OuterWorld at +`(0,0,-1000)` or a clear direction) as the source of truth.
3. Apply it: OuterWorld bake (root offset before bake), cave/portal/outpost/mine coords, the seam LANDING in
   `RuntimeRegionGate` (so entry↔dest are ~1km apart), and the return coords. Watch the 4-gate rotation + WO-483.
4. `CompileGate` → `BakeFullWorld` → headless verify: OuterWorld navmesh at the offset, no origin overlap,
   `SPAWN_TO_GATE_OK` holds, crossing lands clean (no ping-pong), encounter-return resolves < a few metres.
5. Build → **owner felt-test** the south walk (and W/N/E) + caves + clean castle.
6. Commit by EXPLICIT PATH (seam/world files only — `FeatureFlags.cs`, `OuterWorldCavePortalBuilder.cs`, the
   bake-touched scenes/navmesh, the un-stack edits). **Do NOT commit the overnight feature files** (potions/
   talents/abilities — separate decision). **Push only on owner OK** after felt-verify.

## TREE STATE (important)
- **Uncommitted, local, on THIS machine.** Mixed: my verified seam/cave fixes + the overnight feature work
  (KEEP) + the baked scenes. **Nothing pushed.** Compiles clean (`COMPILE_GATE_OK`).
- If resuming on a DIFFERENT machine, this local work is NOT there yet — it would need a push/sync first
  (ask the owner before pushing un-felt-verified work; the overnight feature tree is entangled).
- Built exe at `Builds/Windows/DefendersOfTheRealm.exe` has the seam-nav + cave fix but STILL the ping-pong
  (un-stack not yet applied) — don't re-test the seam on it.

## KEY FILES / DATA
- `Assets/_Modules/Core/FeatureFlags.cs:279` (CastleEditorBridgeSeam=false)
- `Assets/Editor/CastleHubBuilder.cs:1481 RewireAndRebakeCurrentCastle` (no-regen rebake, respects the flag) /
  `:1522` flag branch / `:1716 AddCastleBridgeSeam` / `:1871 RemoveCastleBridgeSeam`
- `Assets/Editor/OuterWorldCavePortalBuilder.cs:57 CaveMouthPrefab` (Cave_Skull) / `:76 CavePositions`
- `Assets/_Modules/Village/World/RuntimeRegionGate.cs` — `FallbackLandingNoGeom (-4.37,0.5,-66)` `:59`,
  `ResolveOuterWorldLanding()`, `ToWorld()` 4-side rotation
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs:905-922` paired-crossing warp + `_crossArmed` guard
- `Assets/Editor/WorldBakeOrchestrator.cs BakeFullWorld` (Outer→Exterior→Cave→NavMesh→Castle)
- Logs: `Builds/world-bake.log` (GATE_NAV_OK), Player.log under `AppData/LocalLow/DeNelle/Defenders of the Realm/`
- Run cycle: `run-unity-method.ps1 -Method <X>` / `build-windows.ps1` / `run-autopilot-fleet.ps1` (editor must be
  CLOSED for batchmode; lock = `Temp/UnityLockfile`, currently absent).
