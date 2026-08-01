# RESULT — WO-819 StructureSingleton common v2

**Shipped:** 2026-08-01, commit `c9a1bd73` (prereqs: `d108a74c` v1, `ff6a9533` CatalogRegistry.All).
**Gates:** COMPILE_GATE_OK (wave3b) + REGRESSION_OK (wave3b) incl. new `SINGLETON_TWINS_OK` oracle —
both markers postdate every edit.

## What shipped
- `repo.bakedTwins` on RepoProps + 9 catalog rows (both copies, v5, byte-identical).
- StructureSingleton v2: catalog-only twin knowledge (code map deleted), per-frame memoized IsBuilt,
  Enforce with WO-673 migration latch, sell -> baked twin RESURFACES (unlock-gated for barracks),
  SingletonResolved/Released events, DDOL bootstrap (waits for GameStateService, subscribes
  StructurePlaced), RemoveLayoutEntry -> NotifyRemoved hook.
- BuildModeController.IsSingletonBuilt delegates to the common (palette/arm/commit gates ride it).
- BarracksNpcInjector bespoke standdown + reseat scan DELETED; SingletonResolved subscription instead.
- Oracle: DataRegression.CheckSingletons (twin uniqueness, dual-copy parity, census parity, seam lint).

## PO felt-verify still open
- [ ] Place barracks -> baked twin gone same frame; second farm/barracks refused; palette shows Built.
- [ ] Sell barracks -> baked CastleBarracks returns + drillmaster reseats (NEW behavior: resurface is
      now mid-session, previously next-load only).
- [ ] No doubled vendors/farms/pet-houses across a save/load cycle.

## Deferred (tracked)
- AutoPilot `AssertSingletonUniqueness` fleet phase (specced in the architect report).
- FindObjectsByType cost rewrite — only if an F8 proves a hub-load hitch (823 out-of-scope table).
