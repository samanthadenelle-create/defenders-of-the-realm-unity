# WORK ORDER 819 — StructureSingleton common v2 (catalog-driven, zero-code enforcement)

**Status: IMPLEMENTED this session (2026-08-01) — awaiting gate + PO felt-verify**
**Owner ruling (verbatim):** "HOW MANY TIMES DO i NNEED TO SAY THERE SHOULD ONLY EVER BE ONE, CAN WE
NOT CREATE A CLASS THAT CONFIRMS SINGLIETON CLASS TYPE" + "i wan an architect to implement as a common"
+ "that gets called on anything that should be a singleton type"
**Silo:** BuildMode/Architecture

## Why
Singleton-ness was enforced piecemeal (palette records check, vendor self-eviction, barracks bespoke
standdown) and every new building re-leaked the rule: two farms, two barracks, doubled vendors.
v1 (d108a74c) centralized it but kept a code map of baked twins and a race-prone bootstrap.
v2 is the architect-audited final shape: **a catalog row with `repo.singleton: true` (+
`repo.bakedTwins` if a legacy baked twin exists) is fully enforced with ZERO code.**

## What shipped
- `RepoProps.bakedTwins` (string[]) — baked scene-root names representing the row; catalog data,
  both copies, 9 rows (workshop, lumbermill, farm, pet-house, forge, arcane-tower, market, jeweler,
  barracks -> CastleBarracks).
- `StructureSingleton` rewrite: catalog-backed `BakedTwinsOf` (code map DELETED); per-frame memoized
  `IsBuilt` union (records -> active baked twin -> live PlacedStructure -> live Building);
  `Enforce(id)` stands down baked twins when placed wins AND resurfaces them when the last
  representation dies (sell -> bake returns, matching next-load behavior); WO-673 migration latch
  honored (no mid-session flip for migration-managed ids); `SingletonResolved` / `SingletonReleased`
  events for the NPC layer.
- Bootstrap v2: DDOL MonoBehaviour, waits for GameStateService (<=300 frames), record-keyed sweep on
  hub load; subscribes to `BuildModeController.StructurePlaced` (same-frame standdown on placement);
  `RemoveLayoutEntry` hooks `NotifyRemoved` (sell path).
- `BuildModeController.IsSingletonBuilt` delegates to the common (palette card, arm refusal, commit
  re-check all ride it). `BarracksNpcInjector` bespoke standdown + placed-scan DELETED — it subscribes
  to `SingletonResolved`; the 1 Hz poll survives only for the WO-724 unlock flip.
- Oracle: `DataRegression.CheckSingletons` — twin resolvability + uniqueness, dual-copy byte parity of
  singleton/bakedTwins fields, migration-census parity pins, seam-routing reflection asserts.

## Acceptance criteria
- [ ] COMPILE_GATE_OK + REGRESSION_OK (incl. CheckSingletons) post-merge.
- [ ] Place barracks -> baked CastleBarracks inactive same frame; second arm refused; palette shows Built.
- [ ] Sell it -> baked twin resurfaces (unlock-gated), palette un-Builds, drillmaster reseats.
- [ ] Farm/pet-house singleton leaks (the shipped F8s) stay closed.
- [ ] FUTURE: AutoPilot `AssertSingletonUniqueness` phase (separate lane; specced in the architect report).

## Do NOT touch
- `CastleVendorNpcInjector` role-level eviction (NPC concern, stays); `HubStructureVisualInjector`
  visual swaps (consolidation is a follow-up, not this WO); `StrategicPlacementMigration.BakedRows`
  (migration census stays; parity is oracle-pinned).
