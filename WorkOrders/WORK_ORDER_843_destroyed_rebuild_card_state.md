# WORK ORDER 843 - Destroyed structure's build card stays "Built" (cannot rebuild)

**Status:** IMPLEMENTED (pending gates - CompileGate + DataRegression destroyed-structure suite)
**Author:** CLI edit-agent (RCA from captured data + owner screenshot, CLAUDE.md par.12)
**Lane:** World/BuildMode - `StructureSingleton.cs` / `BuildModeController.cs` / `Destructible.cs` (no scene files).
**Origin:** owner F8 2026-08-02 - *"lumber mill destroyed no option to rebuild it"* (build-mode
screenshot: Lumbermill card greyed with a "Built" chip post-destruction) + follow-up F8 seq 618 -
*"Says lumbermill destroyed but its clearly still here so singleton is working"* (the WO-819
baked twin resurfaced and stands in for the destroyed mill).

---

## 1. RCA - the card reads the resurfaced BAKED TWIN as "built"

The WO-753 death path is NOT the leak - it clears its state correctly:
- `Destructible.NotifyBroken` (Destructible.cs:176-183) frees the grid cell, forgets the
  loader entry, DROPS the persisted BaseLayout record (`RemovePersistedLayoutRecord`,
  :233-248) and burns the free-build flag. Verified: no stale `PlacedStructureData` remains.

The leak is the BUILT-STATE QUERY the card consults:
- `BuildPaletteUI.BuildCard` :431 -> `BuildModeController.IsSingletonBuilt` :1917 ->
  `StructureSingleton.IsBuilt(entry)` - whose check #2 (StructureSingleton.cs:122-124,
  v2 2026-08-01) counts **any ACTIVE baked twin** (`GameObject.Find(bakedName)`) as built.
- After destruction, `EnforceAll` (next hub load) finds no placed instance, the WO-834 gate
  is open (`collector_lumbermill` is in the MONOTONIC `EverBuiltStructureIds` - by design,
  WO-819 sell->resurface contract), so the baked twin `Lumbermill_Wood_Storefront`
  RESURFACES (:246-249). The standing twin then satisfies IsBuilt check #2 -> the card
  renders the "Built" chip (BuildPaletteUI:600-613) and `Arm` refuses (:1929).
- Same state, contradictory verdicts: the twin is a VISUAL STAND-IN for a structure the
  player no longer owns, but the card treats it as the owned copy. Sell had the identical
  latent regression since singleton v2 (2026-08-01) - the felt-verified WO-819 sell loop
  predates v2.
- `everBuiltStructureIds` is NOT wrongly consumed by the card check and is NOT cleared
  anywhere by this fix (monotonic per WO-834; it is what keeps the twin resurfacing).

## 2. Fix - at the root state query, not the label

- **`StructureSingleton.cs`:** new public `IsPlayerBuilt(string/CatalogEntry)` (per-frame
  memoized like IsBuilt) = a PLAYER-owned representation exists (BaseLayout record, live
  PlacedStructure, live non-baked-twin Building) - an active baked twin deliberately does
  NOT count. `IsBuilt` keeps its enforcement semantics (twin counts) for
  Enforce/stand-down/resurface. Memo invalidated alongside the IsBuilt memo in
  `EnforceInternal` + domain reset.
- **`BuildModeController.IsSingletonBuilt`:** delegates to `IsPlayerBuilt` - the card and
  the arm/place gate read BUILDABLE-at-full-cost when only the twin stands. Committing the
  rebuild fires `NotifyPlaced -> Enforce` (placed wins) which stands the twin down, so
  only-ever-ONE still holds; free-build flags stay burned (v32 law - `BurnFreeBuild` on
  death is untouched and regression-pinned).
- **`Destructible.NotifyBroken`:** now mirrors the SELL path's singleton notify via new
  `StructureSingletonBootstrap.NotifyRemovedDeferred(itemId)` (one frame deferred, because
  the dying object's Destroy is end-of-frame and would still count as placed) - the twin
  resurfaces IMMEDIATELY after destruction instead of waiting for the next hub load, the
  memoized card state refreshes, and `SingletonReleased` fires (injector parity with sell).
- **Coherence (owner confusion datum, seq 618):** when a twin will stand in
  (`repo.bakedTwins` + `MayBakedTwinSurface`), the death toast now says so: "the old
  village <name> stands in for it - rebuild your own at full cost from Build mode."
  Traced in `[Flow:Destroy]`.

## 3. Regression coverage

`Assets/Editor/Regression/DestroyedStructureRegression.cs` - new probe **D (WO-843)**,
runs in the DataRegression batch (registered at DataRegression.cs:305):
1. persisted record -> `IsPlayerBuilt` AND `IsBuilt` true (placed singleton stays "Built");
2. record dropped + ACTIVE twin `Lumbermill_Wood_Storefront` standing (the exact captured
   state) -> `IsBuilt` still true (enforcement) but `IsPlayerBuilt` FALSE (card buildable) -
   fails with "THE CAPTURED BUG" if the regression returns;
3. `Destructible.BurnFreeBuild` double-invoke -> `FreeBuildsUsed` holds the id exactly
   once (rebuild charges full cost, no freebie);
4. `MayBakedTwinSurface` stays true for an ever-built id on a migrated save (the WO-819
   resurface contract is not "fixed away").

## 4. Files touched
- `Assets/_Modules/Village/BuildMode/StructureSingleton.cs`
- `Assets/_Modules/Village/BuildMode/BuildModeController.cs`
- `Assets/_Modules/Village/Vfx/Destructible.cs`
- `Assets/Editor/Regression/DestroyedStructureRegression.cs`

## 5. Acceptance criteria
- [ ] Destroy a player-built singleton (e.g. Lumbermill): the baked twin resurfaces, the
      build card shows its full COST (no "Built" chip, no FREE), arming + placing works,
      and placing stands the twin down (one mill standing).
- [ ] Sell path unchanged felt-wise but now also rebuildable while the twin stands (same
      query).
- [ ] Death toast names the stand-in when one will surface.
- [ ] `DESTROYED_STRUCTURE_OK` marker green incl. probe D; `CompileGate` green.
- [ ] PO felt-verify closes.

## 6. Do NOT
- Do NOT clear `EverBuiltStructureIds` on destruction/sell - it is monotonic by design
  (WO-834) and drives the twin resurface.
- Do NOT make `IsBuilt` (enforcement) ignore twins - only the CARD/arm gate uses the
  player-built query.
- Do NOT unburn free-build flags - destroyed = rebuild at FULL cost (v32 + WO-753).
