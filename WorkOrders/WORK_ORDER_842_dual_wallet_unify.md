# WORK ORDER 842 - Wood/Iron dual-wallet unification (spend/afford asymmetry)

**Status:** FIXED 2026-08-02 (`a7e4acb20`) — awaiting owner felt-verify. *(Status audit 2026-08-24: BUCKET CORRECTION — the prior line predated the commit and still advertised gates/commit as owed; verified at source in `git log`, `a7e4acb20` (2026-08-02) landed this work. Body unchanged. Prior line: IMPLEMENTED (pending gates - CompileGate + EditMode tests + DataRegression))*
**Author:** CLI edit-agent (RCA from captured data, CLAUDE.md par.12)
**Lane:** Combat/AI-adjacent economy - `EconomyService.cs` + upgrade panel VM/service (no scene files).
**Origin:** owner F8 2026-08-02 (P1), proving line captured:

```
[Flow:Upgrade] arcane-tower tier-1 UpgradeNext -> TryUpgrade FALSE (needed W800/F500/C0, have W985646/F988524/C994785)
```
Stack: `BuildingUpgradeVM.UpgradeNext` (BuildingUpgradeVM.cs:286 Fail branch) after
`BuildingUpgradeService.TryUpgrade` returned false against a ~985k-wood wallet.

---

## 1. RCA - two defects, both proven from the tree

### 1a. The captured FALSE itself: a STALE panel, not the wallet
The printed "have" values come from `ResourceLedger.Balance` (GameState wallet) - the SAME
wallet `BuildingUpgradeService.TryUpgrade` spends (`BuildingUpgradeService.cs:92`,
`ResourceLedger.TrySpend` -> `GameState.Wood/Resources.Food`). With W985646 >= W800 the
spend branch CANNOT refuse. The only reachable silent-false branch consistent with the
data is the next-tier guard: `if (def != null && targetTier == current + 1)`
(`BuildingUpgradeService.cs:48`) falling through to a bare `return false` when the VM's
cached `CurrentTier` is STALE. Proof of the staleness seam: the VM's change handlers were
`_modHandler = Raise;` / `_ecoHandler = _ => Raise();` (BuildingUpgradeVM.cs:122-130 pre-fix) -
they re-rendered the View from STALE tiles without `Rebuild()`. When a build timer completed
while the panel sat open (`CompletedUpgradeApplier -> ApplyTier -> ModifierService.Recompute
-> Changed`), the owned tier still showed as the gold "next" tile; tapping it sent the
ALREADY-OWNED targetTier into the service, whose guard silently returned false, and the VM
misreported it as "can't afford" with a full wallet - the exact captured line.

### 1b. The systemic seam the audit named: EconomyService dual wallet
`EconomyService` kept Wood/Iron in a divergent in-session pool (`_wood`/`_iron`, starter
200/80, reset every scene load) while `Grant` mirrored income INTO `GameState.Wood/Iron`
(old :311-317) - one-way. `CanAfford`/`TrySpend` (old :263-291) ran against the pool, so
Wood/Iron granted GAMESTATE-SIDE (dev tools, save load, promo) was riches the HUD showed
but every EconomyService-gated path (build placement `ChargeLedger`, shops, crafting,
walls, troop training) refused to spend. `WallRepairController.SpendMaterials` even
carried its own manual GameState debit mirror to paper over the seam (old :1008-1017).

## 2. Fix - one wallet, honest refusals

- **`EconomyService.cs` - unify authority (the WO's core):** `Wood`/`Iron` are now
  read-through properties over `GameState.Wood/Iron` (the SAME fields ResourceLedger
  spends), matching the Food/Crystals/Coins pattern. `CanAfford` reads the properties;
  `TrySpend` debits GameState (Save + ResourcesChanged, FlowTrace on the seam); `Grant`
  writes GameState once (no more pool+mirror double write; deliberately no Save on the
  hot income path - `GrantSpendable` stays the persist-now dev seam). The serialized
  `_wood`/`_iron` fields survive ONLY as the no-GameState fallback pool (EditMode tests /
  headless boots) - never authoritative when a save service exists. Obsolete
  `CanAfford(int)`/`Spend(int)` route through the unified wallet.
- **`WallRepairController.cs`:** the manual GameState debit mirror in `SpendMaterials` is
  REMOVED - post-unification it would charge repairs twice.
- **`BuildingUpgradeVM.cs`:** eco/modifier/level change handlers now `Rebuild()` before
  `Raise()` (fresh CurrentTier + affordability), and `UpgradeNext` carries a stale-grid
  guard: if the live `ModifierService.TierOf` moved since the last Rebuild, it resyncs
  with an honest status ("Tier N is already unlocked - grid refreshed") instead of firing
  a mismatched unlock. Both traced `[Flow:Upgrade]`.
- **`BuildingUpgradeService.cs`:** no silent false remains - the village-tier gate and the
  not-the-next-tier / no-def branches each emit a named `[Flow:Upgrade]` line (the
  captured branch now names itself in one read).
- **New-game seed note:** GameState new-game Wood/Iron stays 0/0 per the v32 owner ruling
  (free-first-build flags replace the seed, `StartingBudget`). The pool's phantom 200/80
  spillover on top of the freebies is gone WITH the pool - that is canon-correct, not a
  regression.

## 3. Regression coverage (the exact captured scenario)

- `Assets/Tests/EditMode/DevGrantSpendableTests.cs` -
  `gamestate_side_riches_are_spendable_through_the_economy_wo842`: set
  `state.Wood=985646` / `Food=988524` GameState-side ONLY -> `CanAfford(W800/F500)` true
  -> `TrySpend` succeeds -> `state.Wood==984846` and both views agree.
- `Assets/Editor/Regression/VillageEconomyRegression.cs` - probe **B3** (same scenario,
  headless, against the REAL services) added; **B2** flipped from FAIL-BY-DESIGN
  (documented divergence) to a PASS invariant: a plain `Grant(wood)` moves the economy
  view and the ledger identically (one store).
- Existing suites re-checked against the change: `EconomyServiceTests` (no GameState ->
  fallback pool, defaults still 200/80), `DevGrantSpendableTests` (GameState present ->
  read-through; grant lands once, no double count) - semantics preserved.

## 4. Files touched
- `Assets/_Modules/Village/EconomyService.cs`
- `Assets/_Modules/Village/Walls/WallRepairController.cs`
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs`
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeService.cs`
- `Assets/Tests/EditMode/DevGrantSpendableTests.cs`
- `Assets/Editor/Regression/VillageEconomyRegression.cs`

## 5. Acceptance criteria
- [ ] The captured scenario passes: GameState-side riches -> CanAfford true -> TrySpend
      succeeds -> stores agree (EditMode test + VillageEconomyRegression B3 green).
- [ ] A stale upgrade panel never fires a mismatched unlock; refusals name their real
      reason in `[Flow:Upgrade]` (no silent false in `BuildingUpgradeService.TryUpgrade`).
- [ ] Wall repair charges exactly once (no double debit).
- [ ] `CompileGate` green; EditMode suites green; `DataRegression` village-econ green.
- [ ] PO felt-verify: upgrade a city tier with a full wallet; spend wood in shop/build
      mode and watch HUD + upgrade panel move together.

## 6. Do NOT
- Do NOT reintroduce a second Wood/Iron store or a debit/credit "mirror" anywhere -
  `GameState.Wood/Iron` is the single wallet; EconomyService and ResourceLedger both
  read/write it.
- Do NOT seed new-game Wood/Iron (v32 free-first-build ruling stands).
- Do NOT silence VillageEconomyRegression B2/B3.
