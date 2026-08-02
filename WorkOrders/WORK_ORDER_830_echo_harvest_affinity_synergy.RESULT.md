# WORK ORDER 830 — RESULT (IMPLEMENTED 2026-08-02, PENDING GATES)

**Implementer:** edit-only agent (no Unity runs/gates/commits — per lane rules the committer
batch-gates + registers the oracles). **Verification owed:** CompileGate + DataRegression run
(register the two suites below), then headless capture of the card + PO felt-verify.

## What shipped (per the three 2026-08-02 owner-ruling banners)

1. **Player-picked harvest resource per Echo.** The card is a 5-chip RESOURCE PICKER
   (Wood/Iron/Food/Gold/Crystals — `EchoAssignments.PickableResources`); affinity is a match
   BONUS ("(best -- this Echo's calling)" text flag), never a lock. Pair synergies + the hidden
   tri key off ACTUAL assignments (both members on their affinity resources).
2. **Repairs removed — Maren harvests Crystals.** Crystals is the doubled affinity (Bran+Maren);
   Forge pair re-derived as Iron+Crystals (Doran+Maren). WallRepairController untouched.
3. **Hidden tri-synergy never disclosed.** Applied inside `AggregateHarvestMultiplier()` only;
   excluded from `ReadoutFor().BonusPct`, every card/roster string, and the synergy line. One
   internal `FlowTrace.Step("Echo","hidden tri-synergy ACTIVE ...")` on the activation EDGE
   (not per-frame) for headless verify. The `EchoLaneBonuses.HarvestBonusMult` mirror carries
   the applied value — verified write-only (no UI reader).

## echoLanes token grammar (v33-extended, read-migrated, NO schema bump)

`idle` | `<lane>:<level>` (harvest/crafting/defense/exploration) | `<resource>:<level>`
(wood/iron/food/gold/crystals — the WO-830 primary form; lane reads Harvest, resource preserved).
Read migration: pre-v33 bare `wood`/`iron`/`food` -> Harvest at that resource L1; v33 generic
`harvest:N` -> Harvest at the echo's AFFINITY resource (default-on-read); bare token -> L1;
unknown -> Idle. Writes from the picker are always explicit `<resource>:<level>`. Grammar
documented on `SaveSchema.PersistedState.EchoLanes` (banner law) + the `EchoAssignments.cs` header.

## Balance math (Sec.3f re-tune — the numbers and why)

Knobs (echoes-balance.json, BOTH copies byte-identical): `preferredLaneMatchBonus 0.75 -> 0.40`,
base 0.15, perLevel 0.05, sixSet 0.20, 3 crossBonuses @ `bonus 0.10`, `hiddenTriSynergyBonus 0.25`.

- Old reality (2 of 6 matchable, all assigned Harvest L1):
  specSum = 6x0.15 + 2x0.75 + 0.20 = 2.60 -> agg = 6 x 3.60 = **21.6**.
- New all-matched L1 at the OLD 0.75 would be 6x(1 + 0.9 + 4.5 + 0.2) = **39.6** (absurd — the
  WO's inflation warning).
- New all-matched L1 at 0.40: per-echo sum 6x(0.15+0.40)=3.30; +pairs 0.30; +sixSet 0.20 =
  disclosed 3.80; +hidden tri 0.25 = applied 4.05 -> agg = 6 x 5.05 = **30.3 applied / 28.8
  disclosed**. ~1.4x the old fully-assigned ceiling — deliberate: the old ceiling had 4 echoes
  that could never match; the full-roster+full-synergy endgame should beat it, and the hidden
  tri's applied>displayed gap (6x0.25 = +1.5 spine-multiplied) is the secret payoff.
- Early game: founding Aldwin matched L1 = 1x(1+0.55) = **1.55** vs pre-830 unmatched 1.15 —
  a felt but modest boost.
- **Crystals slowest (Sec.3b/§7 guard):** perEchoBaseRate doubles as the Dump split weight.
  Bran 0.45 + Maren 0.45 = **0.90 combined** < food 1.0 = gold 1.0 < wood 1.1 < iron 1.15.
  All-matched L1 crystal share = 0.90/5.15 ~= **17.5%** of the pool — the smallest of the five
  faucets (regression-asserted, both in weights and in a real DumpSilos credit).
- **Silo capacity reconciled (Sec.2 caveat 1):** `SiloCapacity` now = SiloCapHours x Base x
  EchoCount x `AggregateHarvestMultiplier()` (same basis as rate; STEWARD talent factor still
  excluded by design) -> fill-time ~= SiloCapHours again.

## Dump routing

5-way largest-remainder split (sums to the exact pool). Wood/Iron/Food + Crystals ->
`EconomyService.GrantSpendable(wood, food, iron, crystals)` (the crystals param — never the old
3-param form, per Sec.7); Gold -> `EconomyService.AddCoins`. Eco-absent fallback: direct
Wood/Iron write + `GameStateService.AddCrystals`; a gold share is loudly Warn-logged (never silent).

## Files changed (braces balanced, every player string ASCII)

- `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs` — 6 Harvest affinities + `HarvestTarget`
  enum + token/label helpers + WO-831 `EmergeLine`/`LoadEmergence`.
- `Assets/_Modules/Village/Harvest/EchoAssignments.cs` — resource-token grammar + `ResourceTokenOf`
  / `TryTargetOf` / `AssignHarvest` / `ResourceLabelFor`; `PickableLanes={Harvest}`;
  `PickableResources`; stale "phase 2" comments fixed.
- `Assets/_Modules/Village/Harvest/EchoBonusCalculator.cs` — assignment-based match law;
  `HarvestTargetWeights()` (5-way, replaces `HarvestResourceWeights`); pair synergies +
  `SynergyFor`; hidden tri in the applied path only.
- `Assets/_Modules/Village/Harvest/EchoBalanceCatalog.cs` — `hiddenTriSynergyBonus` +
  crossBonuses `name`/`bonus` fields + accessors.
- `Assets/_Modules/Village/Harvest/EchoService.cs` — 5-way Dump credit + capacity reconcile.
- `Assets/_Modules/Village/Harvest/EchoCardVM.cs` / `EchoCardView.cs` — the resource picker card
  (ResourceChips, WhatText "Favors: X", StateText "Gathering <res>", SynergyText, taller modal).
- `Assets/_Modules/Village/Harvest/EchoRosterVM.cs` — OwnedStatus names the assigned resource.
- `Assets/Resources/Data/Canonical/echoes-balance.json` + StreamingAssets copy (byte-identical).
- `Assets/_Modules/Core/State/SaveSchema.cs` — EchoLanes grammar doc (comment only).
- `Assets/_Modules/Core/State/EchoLaneBonuses.cs` — stale consumption-status header fixed.
- `Assets/Editor/Regression/EchoSpecializationRegression.cs` — rewritten to the WO-830 table
  (7 groups incl. applied-not-displayed tri + 5-wallet dump credit + crystals-slowest).
- `Assets/Editor/Regression/EchoResourcePickerRegression.cs` — NEW sibling suite (chip
  projection, picker verb, card strings, synergy line, WO-831 emergence data).
  **Committer: register BOTH `Run(out reason)` suites in `DataRegression.RunAll` (fenced file).**
- Canon: `docs/MASTER_CATALOG/village-systems.md` §6 + ledger rows; `CANON_GROUND_TRUTH_2026-08-01.md`
  Echo lines; `WORK_ORDER_738_*.md` stale-copy banner.

## Not done / for the committer

- CompileGate + DataRegression + headless UI capture of the 5-chip card (modal grew to
  0.10-0.90 vertical — verify no Close-band overlap at mobile resolutions).
- DataRegression registration of the two suites (DataRegression.cs is fenced from this agent).
- Resource-chip ICONS: identity is icon+text per the colorblind law — text is in; if a shared
  resource icon atlas exists the committer may add icons to the kit buttons (none referenced here
  to avoid guessing asset names).
