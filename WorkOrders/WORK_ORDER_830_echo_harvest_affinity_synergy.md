# WORK ORDER 830 — Echo Harvest Affinity + Synergy System

**Status:** READY TO IMPLEMENT (design owner-approved 2026-08-01; three soft spots flagged `OWNER CONFIRM` inline)
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** Combat/Economy data + Harvest (single silo; §9). Does NOT touch VillageSceneBuilder or scene files.
**Supersedes/extends:** WO-738 (per-echo agency + specialization). This is the "make every Echo actually work" pass.
**Sibling:** WO-831 (Echo emergence cutscene — separate VFX lane, do not couple).

---

## 1. Why (owner intent, verbatim)
> "Dive deep into Echos and make sure they work and each has a natural affinity to a type of harvesting so they
> naturally get an added buff when in synergy." … "all should have a affinity with harvest / each a separate one
> that adds that affinity or synergy bonus." … "maybe one can be gold? / other can be crystals / one for repairs?"
> … "maybe if three synergies running as expected add a hidden bonus (not disclosed)."

**Plain reading:** every Echo (all 6) gets its OWN distinct harvest affinity; assigning it earns a synergy bonus
routed to that affinity; when three synergies run together, a **secret, undisclosed** bonus kicks in.

## 2. Read-first — the current state (RCA, sourced from live code 2026-08-01)
The affinity engine already EXISTS (WO-738) and the harvest income path WORKS end-to-end. The problem is that
**4 of 6 Echoes can never earn their affinity** because their `PreferredLane` points at dead/undesigned lanes:

- **Harvest is the ONLY functional lane.** Income `EchoService.RatePerSecond` (`EchoService.cs:126`) folds
  `EchoBonusCalculator.AggregateHarvestMultiplier()`; the per-resource split `HarvestResourceWeights()` is consumed
  by `DumpSilos` (`EchoService.cs:348`) → `EconomyService.GrantSpendable` (`EchoService.cs:387`). Verified: income
  reaches the wallet (Wood/Iron/Food) on Dump, online + offline.
- **Crafting is cosmetic** — pickable, shows a "+%", changes nothing (no gameplay reader of `EchoLaneBonuses.CraftingMult`).
- **Defense + Exploration are fully dead** — no reader anywhere AND not assignable
  (`EchoAssignments.PickableLanes = { Harvest, Crafting }`, `EchoAssignments.cs:60`).
- **Current roster affinities (`EchoRosterCatalog.cs`):** only 2 of 6 have a `HarvestResource` —
  Elowen→Wood, Doran→Iron. Aldwin/Corvin→Exploration, Bran→Defense, Maren→Crafting, all `HarvestResource = null`.
  So **Aldwin (the founding Echo the owner screenshotted) can never surface his affinity** through either offered lane.
- **The 2 empty rows in the owner's screenshot are a STALE BUILD** — live source renders exactly 2 rows
  (Harvest, Crafting). A rebuild clears them. (Confirm with a fresh build; not a source bug.)
- **Two known caveats to fix here (from the income trace):**
  1. Silo **capacity** scales by `EchoCount²` (`SiloCapacity`, `EchoService.cs:148`) but **rate** scales by
     `AggregateHarvestMultiplier` (spine + specialization). With bonuses active, rate outruns capacity and the silo
     fills faster than the intended `SiloCapHours`. Reconcile capacity to the same multiplier.
  2. Online silo ticks aren't persisted per-frame (`EchoService.cs:311-313`); rely on Dump/quit-save. Acceptable,
     but note it — the new currencies (Gold/Crystals) must ride the SAME silo/Dump path, no per-frame writes.

## 3. The design to build

### 3a. Six unique affinities (identity — `EchoRosterCatalog.cs`)
Generalize the affinity from `ResourceType? HarvestResource` to an **affinity target** that is a resource OR a
utility. Map all 6, lore-grounded:

| Echo | Element | Affinity target | Lore hook |
|---|---|---|---|
| Elowen | Nature | **Wood** | grove-warden of the forest edge |
| Doran | Earth | **Iron** | mason; "hauls the heaviest loads" = ore |
| Aldwin | Frost | **Food** | founding card: "tend the fields… wood, iron, or grain"; winter stores |
| Corvin | Shadow | **Gold (Coins)** | scout "carrying spoils across the void" = treasure |
| Bran | Storm | **Crystals (Aether)** | storm-charged aether |
| Maren | Fire | **Repairs** | forge that kept "a mended blade" — mends walls/structures |

All six: set `PreferredLane = Harvest` so every Echo's affinity is reachable and flags `(best)` on assignment.

**Data-model change:** introduce `EchoAffinityTarget { Wood, Iron, Food, Gold, Crystals, Repairs }` (or reuse
`ResourceType` + a `Repairs` sentinel — implementer's call, but Repairs is NOT a `ResourceType`). Keep the old
`HarvestResource` populated for the 3 that map to a real `ResourceType` (Wood/Iron/Food) so the existing
`HarvestResourceWeights()` split keeps working unchanged for them.

### 3b. Where each affinity's yield goes (`EchoService.DumpSilos` + `EchoBonusCalculator.HarvestResourceWeights`)
Five affinities credit the silo→wallet on Dump; one is a utility:
- **Wood / Iron / Food** — unchanged (existing `GrantSpendable(wood, food, iron)` path).
- **Gold (Corvin)** — extend the Dump to credit Coins via `EconomyService.AddCoins` (`EconomyService.cs:463`).
- **Crystals (Bran)** — credit `EconomyService.AddCrystals` / `Grant(..., crystals)`.
  `OWNER CONFIRM`: crystals is an EARNABLE soft currency (upgrades/respec "300c"), real money = SKR/Solana, so a
  free Echo trickle is safe — **spec it SLOW** (default `perEchoBaseRate` for Bran the lowest of the six). Flagged.
- **Repairs (Maren)** — NOT a wallet credit. Maren's "harvest" accrues a **repair charge** that auto-mends the
  lowest-HP `WallSegment` / `IDamageableStructure` over time (hook the existing `WallRepairController` /
  `WallSegment.cs` / `Destructible.cs`). `OWNER CONFIRM`: auto-repair-over-time vs. "reduce repair cost" — default =
  auto-repair-over-time (feels alive; matches "comes to life"). If no structure is damaged, the charge banks/caps
  (no waste-feel) or converts to a token Food credit — implementer default: bank to a small cap, do nothing if full.

### 3c. Synergies — the three pairs (`echoes-balance.json` `crossBonuses`, currently empty)
Group the 6 into 3 thematic synergy pairs. A pair "runs" when **both** its Echoes are owned AND assigned to Harvest:

| Synergy | Pair | Members |
|---|---|---|
| **Provisions** | Wood + Food | Elowen + Aldwin |
| **Forge** | Iron + Repairs | Doran + Maren |
| **Fortune** | Gold + Crystals | Corvin + Bran |

Each running pair grants a small **DISCLOSED** bonus (populate the 3 `crossBonuses` entries; surfaced in the UI per
§3e — this is the "synergy bonus" the owner asked to be visible).

### 3d. Hidden Tri-Synergy Bonus (UNDISCLOSED — the secret)
When **all three** pair-synergies run at once, apply a flat hidden multiplier to global harvest income.
- New tunable `hiddenTriSynergyBonus` in `echoes-balance.json` (default `0.25`).
- Applied inside the **applied** path only (`AggregateHarvestMultiplier` → `RatePerSecond`).
- **NOT DISCLOSED ANYWHERE player-facing:** it must NOT appear in `EchoBonusReadout.BonusPct`, the card "+%",
  `OwnedStatus`, any tooltip, toast, or visible log. The displayed aggregate deliberately UNDER-reports vs. applied.
- Internal only: emit one `FlowTrace.Step("Echo", "hidden tri-synergy ACTIVE …")` so we can headless-verify it fires.
- Stacks with (and is separate from) the existing DISCLOSED `sixSetBonusGlobalHarvest` (+0.20 for OWNING all 6;
  the tri-synergy is about all three pairs actively HARVESTING, a stronger condition).

### 3e. UI — surface affinity + disclosed synergy; hide the dead lanes and the secret
Files: `EchoCardVM.cs` / `EchoCardView.cs` / `EchoRosterVM.cs`.
- The card (`EchoCardView`) currently offers a 2-lane picker (Harvest/Crafting). Since every Echo now prefers Harvest
  and Crafting is dead, **collapse to Harvest as the single meaningful assignment** and repurpose the card to show:
  the Echo's **affinity target** (e.g. "Favors: Gold"), its `(best — this Echo's calling)` flag, its "+%", and its
  **synergy status** ("Provisions synergy: ACTIVE" / "pair with Aldwin to activate"). `OWNER CONFIRM`: remove the
  Crafting chip entirely vs. hide it like Defense/Exploration — default = remove (dead pick = confusing).
- Reuse the existing colorblind-safe text patterns (`StateText` `EchoCardVM.cs:125`, `NoteFor` `:176`,
  `OwnedStatus` `EchoRosterVM.cs:117`). Never hue-only.
- **Hard rule:** the readout `+%` = disclosed bonuses ONLY (base + level + match + disclosed pair-synergy). The
  hidden tri-synergy must be excluded from every displayed number (§3d).
- Note the stale-build empty rows are cleared by a rebuild; no code change needed for that specifically.

### 3f. Balance re-tune (`echoes-balance.json` — BOTH copies, byte-identical)
Making all 6 Harvest-matched inflates `AggregateHarvestMultiplier` (every Echo now earns the +0.75 match instead of
2 of 6). Re-tune so early game isn't absurd and late game still rewards the full roster:
- Re-balance `preferredLaneMatchBonus` and/or `baseContributionPerEcho` for the all-matched reality (proposal:
  lower `preferredLaneMatchBonus` from 0.75 → ~0.35–0.45, keep floor 0.15; CLI to verify the curve headless).
- Add `hiddenTriSynergyBonus` (0.25) and the 3 `crossBonuses` entries (§3c).
- Reconcile silo **capacity** (`EchoService.cs:148`) to the SAME multiplier basis as rate (fix §2 caveat 1).
- Files: `Assets/Resources/Data/Canonical/echoes-balance.json` + `Assets/StreamingAssets/Data/Canonical/echoes-balance.json`
  (MUST stay byte-identical — the regression asserts it).

## 4. Files to edit
- `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs` — 6 affinities + all `PreferredLane = Harvest` + affinity-target model.
- `Assets/_Modules/Village/Harvest/EchoBonusCalculator.cs` — hidden tri-synergy in applied path (excluded from readout); pair-synergy math.
- `Assets/_Modules/Village/Harvest/EchoBalanceCatalog.cs` — parse `hiddenTriSynergyBonus`; `crossBonuses` already modeled.
- `Assets/_Modules/Village/Harvest/EchoService.cs` — Dump credits Gold + Crystals; Repairs charge → WallRepair; capacity fix.
- `Assets/_Modules/Village/Harvest/EchoAssignments.cs` — `PickableLanes` → Harvest-only (per §3e OWNER CONFIRM).
- `Assets/_Modules/Village/Harvest/EchoCardVM.cs` / `EchoCardView.cs` / `EchoRosterVM.cs` — affinity + synergy display.
- `Assets/Resources/Data/Canonical/echoes-balance.json` + `Assets/StreamingAssets/Data/Canonical/echoes-balance.json` — tunables (byte-identical).
- `Assets/_Modules/Village/Walls/WallRepairController.cs` (+ `WallSegment.cs`) — expose an auto-repair entry for the Repairs affinity (read the file first; reuse, don't reinvent).

## 5. MUST update in the SAME commit (canon + tests — else the gate/regression fails)
- `Assets/Editor/Regression/EchoSpecializationRegression.cs` — it ASSERTS the old identity table (stag→Wood,
  bear→Iron, phoenix→Crafting/null) and "every spirit non-Idle preferred lane." Update to the new 6-affinity table;
  add assertions for: each Echo matched on Harvest, Gold/Crystals dump credit, Repairs charge, the 3 pair-synergies,
  and the hidden tri-synergy (assert it IS applied to income but NOT present in `ReadoutFor().BonusPct`).
- `WorkOrders/WORK_ORDER_738_echo_per_echo_agency_specialization.md` — mark superseded-by-830 for the affinity table;
  fix the STALE dialogue copy "Frosthowl (Ice | prefers Harvest)" (already contradicts shipped data).
- `Assets/_Modules/Core/State/EchoLaneBonuses.cs` — header comment "HarvestBonusMult → CONSUMED: EchoService.RatePerSecond"
  is STALE (income reads `AggregateHarvestMultiplier` live, not this field). Fix the comment.
- `EchoAssignments.cs:19-22, 123` — "phase 2 (a later agent)" comment is STALE (consumers shipped). Fix.
- Update the current `CANON_GROUND_TRUTH_*.md` Echo line + `docs/MASTER_CATALOG/` Harvest section (§15).

## 6. Acceptance criteria (headless-verifiable — CLI proves with DATA, §12)
- [ ] All 6 Echoes have a distinct affinity target; all `PreferredLane == Harvest`; each flags `(best)` when assigned.
- [ ] Dump credits the right wallet per affinity: Wood/Iron/Food (unchanged) + **Gold** (Corvin) + **Crystals** (Bran),
      verified by a `DataRegression`/`EchoSpecializationRegression` run asserting each wallet field moved.
- [ ] Repairs affinity (Maren) mends a damaged `WallSegment`/structure over time (assert HP rises with Maren assigned).
- [ ] Each of the 3 pair-synergies grants its disclosed bonus when both members harvest; SHOWN in the card text.
- [ ] Hidden tri-synergy: with all 3 pairs harvesting, realized income rises by `hiddenTriSynergyBonus`, AND the
      displayed `+%`/readout does NOT include it (assert applied ≠ displayed; `FlowTrace` fires).
- [ ] Silo capacity vs. rate reconciled (fill time ≈ `SiloCapHours` with specialization active).
- [ ] `echoes-balance.json` two copies byte-identical; `CompileGate` + `DataRegression` green.
- [ ] The card no longer shows a dead Crafting pick or empty rows on a FRESH build; affinity + synergy are visible.

## 7. Do NOT
- Do NOT wire Defense/Exploration (unlock undesigned — owner ruling 2026-07-24; keep them hidden).
- Do NOT surface the hidden tri-synergy anywhere player-facing (that is the whole point).
- Do NOT make Crystals a fast faucet (monetization; keep the trickle slow — §3b OWNER CONFIRM).
- Do NOT touch `VillageSceneBuilder`/`.unity` scenes (§3, §9). Do NOT couple with WO-831 (cutscene).
- Do NOT credit AetherCrystal via the old `GrantSpendable(wood,food,iron)` overload without the crystals param.

## 8. OWNER CONFIRM (defaults chosen; veto any — non-blocking)
1. **Repairs behavior:** auto-repair-over-time (default) vs. reduce repair cost.
2. **Crafting chip:** remove entirely (default) vs. hide like Defense/Exploration.
3. **Crystals trickle rate:** slowest of the six (default) — confirm crystals may be Echo-farmed at all.
