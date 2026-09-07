# WO-1570 RESULT — 2026-09-07 (edit-only lane; uncommitted, awaiting gate)

**Verdict: the reported defect is NOT a retired-key leak. The data and the spend are
both honest; the Manage VIEW drops the resource words.**

Proven at source this session:
- `building-tiers.json:67` authors `costWood 2600 / costGold 970` — live keys
  (`BuildingTierCatalog.cs:61-62`). `costFood` matches 0 rows in BOTH canonical twins,
  so the legacy alias at `:64` prices nothing.
- "Stone" = the live player word for the Food wallet slot
  (`TownBankCapacity.cs:332,369`); "Gold" = Coins. Neither is retired.
- Tier-2 -> Stone charge is OWNER RULING 22 (WO-2005): the charge is right, the
  authoring is wrong. Not touched.
- **Spend path unaffected:** `BuildingUpgradeService.TryUpgrade` debits Stone 2600 via
  `ResourceLedger` + Coins 970. The button matches the charge.
- The captured screen is Manage (`ManageVmProjection.cs:306`,
  `ManageScreenVM.cs:4859`), not a StructureCardVM surface.

Real defect, handed to the Manage lane:
`ManageScreenVM.CostVms` (:5070) builds `Label` but sets `IconKey = null`;
`ManageWorkspacePanel.BuildCostRow` (:1288-1291) paints icon + `AmountText` only and
never `Label` -> bare "2600 970". The CTA concatenates the price -> "GOL..." truncation.

No oracle was missing at the data level: `CostBasketSeparationRegression` `[invariant]`
/ `[regular]` (:759-828) already walks every structure's every upgrade step and
`[tiers-basket]` (:384) every building-tiers tier. No duplicate added. Delivered:
- `StructureCardVM.cs` — `CurrentLevel`, `EffectiveCostParts`, `NextTierCostParts`,
  `UpgradeStats(Label,Current,Next)`, `UpgradeSeconds`, `UpgradeTimeText`,
  `UpgradeButtonLabel`; every word now from `TownBankCapacity.DisplayName` — the
  pre-existing hand-typed tuple in `PlacementCostWords` was re-pointed too (identical
  output; `CostBasketSeparationRegression.cs:662` reads `CostWords`). `NextTierStats`
  derived from the rows. `FlowTrace.Once` names the basket keys and BRANCHES on whether
  `UpgradeCostFor` took the authored `repo.upgradeCost[0]` or the `CostFor x fromLevel`
  scaler, so it can never name the wrong authority. `UpgradeStats` stays EMPTY when
  nothing is measured rather than inventing copy.
- `BuildEconomyRegression.cs` — `[card-cost-words]` lint pinning the above.

Gate checks (local): braces 70/70 and 578/578, 0 NUL, 0 non-ASCII added lines. No
canonical JSON edited, so no LF-count / `json.load` proof applied. Unity gate + commit
not run (lane is no-Unity, no-git).
