# WO-784 — Echo lanes: wire the consumers (all four lanes are write-only stubs)

**Status:** READY TO IMPLEMENT (Phase 1); Phase 2 gated on owner copy
**Minted:** 2026-07-30 (CLI, from the 12-agent SME fan-out — Echo/FTUE/Dialogue dossier)
**Lane:** `Village/Harvest` + one consumer per lane. File-disjoint from the raid + dungeon lanes.
**Supersedes the canon line** "Echo lanes 3-of-4 stub" (`CANON_GROUND_TRUTH_2026-07-22` §8) — it is worse than that: see Why.

---

## Why (evidence, code-verified 2026-07-30)

Canon says three of four Echo lanes are stubs. **The truth is all four.**

The Core contract is `Assets/_Modules/Core/State/EchoLaneBonuses.cs` — four multiplier fields at
`:39` (`HarvestBonusMult`), `:42` (`CraftingMult`), `:45` (`DefenseMult`), `:48` (`ExplorationMult`).
Its **sole writer** is `EchoBonusCalculator.Recompute()` (`Assets/_Modules/Village/Harvest/EchoBonusCalculator.cs:217-220`).

A grep for production readers of each field returns **ZERO for all four**. The only non-writer hits
are the editor oracle `Assets/Editor/Regression/EchoSpecializationRegression.cs:329-336` (which
asserts the *write* side only) and the fields' own declarations/`Reset()`.

Harvest looks live but is not routed through the contract: `EchoService.RatePerSecond`
(`Assets/_Modules/Village/Harvest/EchoService.cs:126`) calls
`EchoBonusCalculator.AggregateHarvestMultiplier()` **directly**, bypassing `EchoLaneBonuses`
entirely. So the shared Core seam that every host is supposed to read has no consumer at all.

Two compounding facts:
- `EchoAssignments.PickableLanes = { harvest, crafting }` (`Assets/_Modules/Village/Harvest/EchoAssignments.cs:60`)
  — **Defense and Exploration cannot even be assigned from the picker**, so their multipliers are
  permanently 1.0 on every save. The lane chips render from this list (`EchoCardVM.cs:153-171`).
- **The header lies.** `EchoLaneBonuses.cs:14-18` says *"only HarvestBonusMult currently has a real
  reader ... CONSUMED: EchoService.RatePerSecond"*. False per `EchoService.cs:126`. Anyone trusting
  that comment will wire a second harvest multiplier and double-apply. (`EchoBonusCalculator.cs:203-205`
  states the truth correctly — two comments, one contradiction.)

**Player impact:** the picker advertises a +x% for lanes nothing consumes. Assigning an Echo changes
a number no system reads. Per the owner's own framing, the picker's agency is ~75% placebo.

## Owner rulings already in force (do NOT re-decide these)

From memory `echo-lane-design-rulings` (owner, 2026-07-17) and `echo-is-essence-of-guarded-person`:

1. **Defense lane = a flat +x% to CITY DEFENCE — the easy one.** Do **NOT** build the offline
   async-raid resolver the original WO-738 specced. It is a broad passive buff to the whole
   defensive package: defensive **structures' damage AND HP** (towers, walls/gates, the Heart) —
   CoC-style breadth, not one knob. Echoes still NEVER fight; canon holds.
2. **Wiring Defense is the prerequisite that makes onboarding honest.** Teaching lane-assignment is
   dishonest while only one lane is real. Once Defense is live there is a genuine two-lane choice,
   and the teach moves to Echo #2's unlock.
3. **Exploration = dungeons-only** (dungeon reward/loot scaling), per the WO-738 model.

## Scope — Phase 1 (implement now)

**P1-A — Make the Core contract the single seam.**
Repoint `EchoService.RatePerSecond` (`EchoService.cs:126`) to read `EchoLaneBonuses.HarvestBonusMult`
rather than calling `AggregateHarvestMultiplier()` directly, so there is exactly ONE path from
calculator to consumer. Behaviour must be numerically identical — this is a seam fix, not a balance
change. Correct the lying header at `EchoLaneBonuses.cs:14-18` in the same commit (§15).

**P1-B — Wire Defense (the owner's "easy one").**
`EchoLaneBonuses.DefenseMult` → a broad defensive buff. Reconcile onto the EXISTING modifier seam —
`ModifierService` / `GameModifiers` (the `TowerDamageMult` + structure-HP surfaces named in the
ruling). Do **not** greenfield a parallel buff system. Structures affected: towers, walls/gates, the
Heart. Read the value at the point the modifier is recomputed; never cache it across a Recompute.

**P1-C — Open the picker to Defense.**
Add `defense` to `EchoAssignments.PickableLanes` (`EchoAssignments.cs:60`) so the lane can actually be
chosen. Exploration stays OUT of the picker until P2 (do not advertise what nothing reads).

**P1-D — Fix the founding-identity contradiction.**
Aldwin (`EchoRosterCatalog.cs:117`) is `PreferredLane = Exploration, HarvestResource = null`, yet he is
taught and auto-assigned as the HARVESTER and his own flavor copy says *"Name my task -- wood, iron,
or grain"*. He can therefore never earn the 0.75 preferred-lane match bonus. Same trap on Corvin
(Exploration, `:129`) and Bran (Defense, `:139`) — **3 of 6 souls have an unreachable calling.**
Owner ruling needed on WHICH way to reconcile (change Aldwin's `PreferredLane` to Harvest, or change
what the founding beat teaches). Default if unruled: set the founding echo's `PreferredLane = Harvest`
with a non-null `HarvestResource`, because the taught copy is player-facing canon and the lane table
is not. Note `EchoSpecializationRegression` Group4's fixture currently **enshrines** the mismatch and
must be updated with the fix.

## Scope — Phase 2 (gated)

- **Exploration lane** → dungeon reward scaling (`DungeonLootGrant`). Gated on the dungeon
  reachability fix (WO-783) landing, else there is nothing to scale.
- **Crafting lane** → forge cost/time delta. Verify a real consumer exists before advertising it.
- **The teaching conversation at Echo #2** (assign-lanes + element match + the Harvest-vs-Defense
  fork). Copy is owner/creative sign-off — see WO-752 Part B.
- `echoes-balance.json` `crossBonuses: []` is parsed (`EchoBalanceCatalog.cs:55`) but **no code reads
  `Data.CrossBonuses`** — a dead knob. Either wire or delete; do not leave it advertising.

## Acceptance (data-verified — NOT source-lint)

The existing `[echo-spec]` suite asserts the WRITE side only; that is exactly how this shipped
unnoticed. New assertions must prove a READ.

1. **New oracle `[echo-lane-consumers]`**, registered in `DataRegression.RunAll`, marker
   `ECHO_LANE_CONSUMERS_OK`. Per wired lane, drive the real production system and assert the delta
   tracks the multiplier:
   - Defense: force `DefenseMult` to a sentinel via `Recompute`, then assert the resolved
     tower-damage AND structure-HP surfaces move by exactly that factor.
   - Harvest: force `HarvestBonusMult` to a sentinel, assert `EchoService.RatePerSecond` tracks it
     (this goes RED today — that is the point, it proves P1-A).
2. **Anti-advertising assert:** every lane offered by `EchoAssignments.PickableLanes` MUST have a
   production reader. A lane in the picker with no consumer fails the gate. This is the ratchet that
   stops this class from recurring.
3. **Founding identity:** assert the founding echo (`Order == 1`) has a `PreferredLane` reachable from
   `PickableLanes` and a non-null `HarvestResource` if the taught copy names a resource.
4. **Screenshot proof** (owner standing rule): the Echo card + roster picker render with the correct
   live lane set and no truncation/overlap. `UICaptureLaunch.RunCaptureHeadless` already shoots the
   Echo card; extend it to the picker if the panel is edit-mode safe (the roster modal currently is
   NOT — see `UICaptureLaunch.cs:288-295` — so a device capture may be required).

## Do NOT touch

- **Echoes never fight.** Defense is a passive stat buff, not a combatant or an offline battle sim.
- The 6-soul identity table's NAMES / element assignments (`EchoRosterCatalog`) — owner creative canon.
- `EchoBonusCalculator`'s curve maths — it is the single math source; only change WHO reads it.
- `echoes-balance.json` numbers (balance is a later, separate pass).
- The founding-card copy — WO-752 owns it.

## References

`docs/PAIN_POINTS_2026-07-26.md` · memory `echo-lane-design-rulings`, `echo-is-essence-of-guarded-person` ·
`WORK_ORDER_738` (the model this refines) · `WORK_ORDER_752` (card copy) ·
SME dossier: Echo/FTUE/Dialogue, 2026-07-30 fan-out.
