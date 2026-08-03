# WORK ORDER 834 — Blank-founding towns: baked default-town structures STAND DOWN until first player build

**Status: IMPLEMENTED (pending gates)** — edit-only agent 2026-08-02; orchestrator owns CompileGate +
DataRegression + EditMode run + commit.
**Silo:** BuildMode/Save-Schema
**Owner signal:** live F8, capture seq 592 ("Build Your Own" town loads full of baked buildings).

## RCA (data-proven, from the owner's Player.log at seq 592)

- `Player.log:7178` — `[Flow:Founding] choice = BUILD YOUR OWN (blank template + FTUE) — no state change.`
  The founding choice persists NOTHING; "blank founding" exists only as the ABSENCE of data.
- `Player.log:16571` — `[Flow:BaseLayout] LoadFromState: scene 'Main_Castle_Overworld' has an empty
  BaseLayout — default seed stands (no replay).` The visible town is BAKED SCENE CONTENT, not save data.
- `Player.log:18546/18564` — `[Flow:Singleton] resurface 'CastleBarracks' ... EnforceAll: swept 12
  singleton catalog row(s).` WO-819's baked twins surface precisely BECAUSE the player owns nothing
  (no placed instance -> Enforce takes the resurface branch on every singleton row).
- `Player.log:153985` — build-mode census `live=0/loader=0/persisted=0` (nothing player-placed, ever).

**Root cause:** a design gap between WO-748 (Default Town = template choice) and WO-819 (baked-twin
singleton common) compounded by Lever-1 (owner 2026-07-24 "stores pre-stand on a fresh hub"): every
surfacing path (StructureSingleton resurface, HubStructureVisualInjector pre-stand, the
CastleVendorNpcInjector Lever-1 baked-anchor fallback, the WO-724 barracks unlock poll) keys on
"nothing placed" — which is TRUE on a Build-Your-Own founding, so the "blank" town is fully furnished.
Nothing in the save distinguishes "chose blank" from "hasn't built yet".

## The mechanism (chosen; NO new founding flag)

New persisted set **`everBuiltStructureIds`** (catalog ids the player has EVER committed a placement
of; grows monotonically, never shrinks — selling keeps the id, which is what keeps WO-819
sell-resurface working). Save bump **v35 -> v36**, additive nullable + `MigrateToV36` + default-on-read.

**The pure surfacing rule** — `StructureSingleton.MayBakedTwinSurface(id, everBuilt, migrated)`
(pure static, unit-tested):

- `StrategicPlacementMigrated == false` -> **true**. The bake owns the town: a legacy pre-v30 save
  awaiting its one-shot migration, and — this is how the Default-Town founding load is covered with no
  flag — WO-748's Default Town works by CLEARING the marker so the WO-673 writer converts the live
  baked ring into records on the first hub load.
- else -> **true iff `everBuiltStructureIds` contains id** (OrdinalIgnoreCase).

**Why Default Town stays covered after its migration runs** (verified against the code, not comments):
`StrategicPlacementMigration.RunIfNeeded` (the ONLY code path that runs on a Default-Town founding and
on legacy-save migration; a Build-Your-Own game never enters it because `ResetToNewGame` sets the
marker TRUE) now marks its whole TEMPLATE GRANT as ever-built: every census itemId (BakedRows +
StationRows — including rows skipped for a missing catalog row/absent scene object, since the grant is
a right of the template, not of one bake) plus `barracks` (the WO-724 baked-barracks-at-unlock right is
part of the prebuilt town). So post-migration Default-Town saves surface/resurface exactly as today.

**Write path:** `GameState.MarkEverBuilt(id)` (idempotent) called from the two BaseLayout commit
seams: `BuildModeController.Place` (right after `state.BaseLayout.Add(data)`, before the
`StructurePlaced` event that drives `StructureSingleton.NotifyPlaced`) and
`StrategicPlacementMigration` (the template grant above). Persistence rides the SAME `Save()` that
carries the BaseLayout append (commit-or-revert together).

**Migrator seed (`MigrateToV36`)** for existing (pre-v36) saves:
`everBuiltStructureIds = BaseLayout itemIds  UNION  FreeBuildsUsed ids  UNION
(BaseLayout non-empty ? the frozen default-town template snapshot : nothing)`

- BaseLayout ids: what visibly stands as player-owned records.
- FreeBuildsUsed union (cheap + SOUND, adopted): the free-first-build flag burns at the committed
  placement and NEVER resets, so an id in FreeBuildsUsed was placed at least once — a pre-v36 player
  who placed a singleton and then SOLD it has the id in FreeBuildsUsed but not BaseLayout; without the
  union, their WO-819 sell-resurfaced twin would vanish at v36. With it, behavior is unchanged.
- Template-snapshot union only when BaseLayout is NON-EMPTY: an established town (Default-Town
  migrated ring, or any save with placements) keeps today's Lever-1 pre-stand + barracks-at-unlock
  VERBATIM ("existing towns keep their baked twins exactly as today"). An EMPTY-BaseLayout save is
  exactly the blank-founding save this WO fixes (the owner's seq-592 save: persisted=0) — it seeds
  only FreeBuildsUsed (typically empty) and goes truly blank. The snapshot list is HARDCODED in the
  migrator (migrations are point-in-time transforms; the v8 gate-0->gate-2 precedent).

**Gated call sites** (every surfacing path found in the tree — the first three are the F8 lines;
the last three are the Lever-1/WO-724 paths that would silently refurnish the town within seconds):

1. `StructureSingleton.Enforce` resurface branch — gate closed -> **actively STAND DOWN** the baked
   twins (they arrive ACTIVE from the scene bake; skipping resurface alone leaves them standing).
2. `StructureSingleton.EnforceAll` — one `[Flow:Singleton]` summary Step:
   `EnforceAll: swept N singleton catalog row(s) - surfaced=X suppressed=Y (blank-town gate)`.
   > ⚠ **SUPERSEDED 2026-08-03** (WO-853 session, owner F8 seq=651) — this trace format changed. The counts above overclaimed (`suppressed` counted any row that merely AUTHORED `bakedTwins`, so a session logged `suppressed=9` with zero actual standdowns). Live format is now `EnforceAll: swept N singleton catalog row(s) (T authoring baked twins) - surfaced=X suppressed=Y alreadyDown=Z (blank-town gate)`, where every count means work actually done. Body below stays frozen per CLAUDE.md §15.
3. `StrategicPlacementMigration.StanddownActiveForBaked` — now also true when the gate is closed
   (`HasRecord(id) || !MayBakedTwinSurface(id)`), so `HubStructureVisualInjector.TrySwap` stands the
   bake down at scene load (no N-frame furnished flash before the deferred EnforceAll).
4. `HubStructureVisualInjector.EnsureBarracksSurfaced` — early-return when gate closed.
5. `BarracksNpcInjector` WO-724 1 Hz unlock poll — early-return when gate closed (else it would
   resurface the barracks + log every second forever on a blank town).
6. `CastleVendorNpcInjector.ResolveBakedOrStationAnchor` (Lever-1 fallback) — returns no anchor when
   gate closed: it literally calls `ResurfaceStorefront` to pre-stand the store, which would undo the
   standdown every 2 s poll pass AND seat a vendor at it. On a blank town, vendors come online as
   buildings are placed (`NotifyBuildingPlaced` — the WO-707 ruling), exactly like the stated design.

## Net behavior

- Fresh **Build Your Own** town: ZERO baked functional twins visible (tree + well + walls/gates shell
  only), no pre-stand vendors, no baked barracks at unlock. First placement of X -> WO-819 proceeds
  unchanged (twin stands down same frame — it already is — and X is marked ever-built forever, so
  selling X resurfaces its baked twin per WO-819).
- Fresh **Default Town**: unchanged (founding load surfaces via marker=false; post-migration surfaces
  via the template grant).
- Existing saves: unchanged (migrator seed above); the one deliberate exception is an existing save
  with ZERO placements ever — i.e. the owner's captured save — which goes blank: that IS the fix.
- Legacy pre-v30 saves: unchanged (marker=false -> gate open until their one-shot migration, which
  then grants the template).

## Files touched

- `WorkOrders/WORK_ORDER_834_blank_town_baked_standdown.md` (this spec) + `CLI_LANES_WO_NUMBERS.md`
  (banner: next free = 833).
- `Assets/_Modules/Core/State/SaveSchema.cs` — CurrentVersion 36 + `everBuiltStructureIds` wire field.
- `Assets/_Modules/Core/State/SaveMigrator.cs` — `MigrateToV36` (seed rule above).
- `Assets/_Modules/Core/State/GameState.cs` — `EverBuiltStructureIds` + `MarkEverBuilt/HasEverBuilt`.
- `Assets/_Modules/Core/State/GameStateService.cs` — Snapshot / ApplyPersisted / ResetToNewGame plumbing.
- `Assets/_Modules/Village/BuildMode/StructureSingleton.cs` — the pure rule + gated Enforce +
  EnforceAll summary trace.
- `Assets/_Modules/Village/BuildMode/StrategicPlacementMigration.cs` — template grant +
  StanddownActiveForBaked gate.
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs` — MarkEverBuilt at the commit seam.
- `Assets/_Modules/Village/HubStructureVisualInjector.cs` — EnsureBarracksSurfaced gate.
- `Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs` — unlock-poll gate.
- `Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs` — Lever-1 fallback gate.
- `Assets/Editor/Regression/DataRegression.cs` — `CheckBlankTownGate` (BLANK_TOWN_GATE_OK).
- `Assets/Tests/EditMode/BlankTownGateTests.cs` — pure-rule truth table + MigrateToV36 seed tests.

## Acceptance criteria

- [ ] COMPILE_GATE_OK + REGRESSION_OK (incl. existing CheckSingletons asserts + new BLANK_TOWN_GATE_OK)
      + EditMode suite green.
- [ ] **PO felt:** New Game -> Build Your Own -> hub = tree/well/walls ONLY; no storefronts, no
      vendors, no barracks; palette shows everything buildable; place a farm -> it stands + vendor
      seats; sell it -> its baked twin resurfaces (id stays ever-built).
- [ ] **PO felt:** New Game -> Default Town -> founding load shows the baked ring; next hub load
      replays it movable; barracks still surfaces at unlock — UNCHANGED vs today.
- [ ] **PO felt:** the owner's existing seq-592 save loads BLANK (0 placements -> empty seed).
- [ ] WO-819 acceptance intact: place -> twin stands down same frame; sell -> twin resurfaces +
      drillmaster reseats; never two of a singleton.
- [ ] v30 migration intact: legacy marker-false save still migrates one-shot; handover still atomic
      on the next hub load.

## Do NOT touch

- `FoundingChoiceController` (verified: Build Your Own needs NO persistence call — the mechanism
  reads ResetToNewGame's marker + the empty everBuilt set), the WO-818 KayKit npcModel logic,
  ZoneManager, any `.unity` scene.
