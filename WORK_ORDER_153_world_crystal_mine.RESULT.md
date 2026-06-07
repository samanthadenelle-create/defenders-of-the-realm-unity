# WORK ORDER 153 — World Crystal Mine — RESULT

**Status: IMPLEMENTED (code-only; needs an OuterWorld rebuild to go live — see Bake)**
**Date:** 2026-05-31
**Branch:** feat/tower-core-loop

---

## Summary

The world Crystal Mine is delivered as a **thin companion on the existing `MineNode`**,
not a parallel node system. A crystal `MineNode` (Resource=AetherCrystal) gains a new
`CrystalMineNode` component that adds exactly the three "mine specifics" WO-153 asks for —
**renewable framing, region-graded yield, upgrade tiers** — while all banking, cooldown,
worker/offline/settlement seams stay on `MineNode` (one banking path into GameState).

The WO-144 `CrystalGrade` enum + danger→grade mapping that WO-153 acceptance #3 hard-depends
on did **not** exist yet (WO-144 is spec'd, unbuilt). I created the **lean Core slice** of it
(enum + pure `CrystalRegion.TopGradeFor(tier)` helper) — NOT WO-144's heavier ledger/SaveSchema
work. Crystals still bank through the single `AetherCrystals` total today; the grade is a
forward-compatible classification WO-144's ledger will consume with no fork.

---

## Files changed

| File | Action | What / why |
|---|---|---|
| `Assets/_Modules/Core/World/CrystalGrade.cs` | **Create** | `enum CrystalGrade { Aether, Verdant, Mire, Wraith }` (order = rarity = danger) + pure `CrystalRegion.TopGradeFor(int dangerTier)` mapping (WO-144 §1a/§2, minimal slice). DeNelle.Core.World, no UnityEngine, headless-safe. |
| `Assets/_Modules/Village/World/CrystalMineNode.cs` | **Create** | The WO-153 mine behaviour. `[RequireComponent(MineNode)]` companion: (1) forces renewable mode, (2) stamps the region `CrystalGrade`, (3) tier upgrades paid via `CrystalEconomy.TrySpend` that bump the sibling node's yield/capacity. Code-built `[G]` upgrade prompt (no UXML), reuses Village `PromptBillboard`. |
| `Assets/Editor/OuterWorldBuilder.cs` | **Edit (additive)** | Crystal (Aether) MineNodes now placed as **renewable** (UseFiniteReserve=off, cooldown-respawn vein) and get the `CrystalMineNode` companion attached (by reflection, same asmdef-free pattern). Non-crystal nodes keep their WO-159 finite-reserve behaviour unchanged. |

No other files touched. No `VillageSceneBuilder` / `WallLayout` / `CityManifest.json` / `Village.unity` edits. No commit/push/build.

---

## How it reconciles (no duplication)

- **`MineNode` (WO-142/WO-159):** the Crystal Mine IS a `MineNode`. Banking (`BankYield` →
  `GameState.AetherCrystals`), the per-extract region-danger bonus (`EffectiveYield`), the
  renewable cooldown-respawn loop, depletion, and the worker/offline/settlement seams
  (`TryAutoExtract`/`ForceAutoExtract`/`DrainReserve`/`RatePerSecond`) are all reused unchanged.
  `CrystalMineNode` only mutates `MineNode`'s public tuning fields (`YieldPerExtract`,
  `TotalExtracts`, `ReserveTotal`) on upgrade — it never writes the wallet for harvest.
- **Old village `CrystalMine.cs` (WO-150, removed from village):** its tier-upgrade + proximity-
  prompt UX is the **pattern salvaged** (TrySpend-paid tiers, code-built bubble) — relocated to
  the world, not rebuilt. `CrystalMine.cs` itself is untouched (it pays in Coins per-wave; the
  world mine pays in crystals per-tier — distinct, no double-pay).
- **WO-144 grades:** consumed via the new lean Core enum/helper. A mine's grade is derived from
  its region's danger tier (`ZoneManager.DangerTierAt`): Aether (Village/safe) → Verdant
  (Goldfields/Stoneback) → Mire (Mirewood) → Wraith (Ashwood). WO-144's later ledger plugs into
  the SAME enum.
- **WorkerManager / Settlement / SettlementPlacer:** untouched. Crystal mines are renewable
  (not finite reserves), so `SettlementPlacer.ClaimAt` (which requires `UseFiniteReserve`) simply
  won't claim them — they remain the manual/worker-driven faucet, exactly the WO-153 intent (the
  reliable repeatable mine vs. the settlement-drained reserve). Workers still auto-collect them
  via `MineNode.TryAutoExtract` with zero new wiring.

---

## Bake / build needed (DESCRIBED for CLI — not fired)

To make the renewable+graded crystal mines live in the world, the CLI gatekeeper must
**rebuild the OuterWorld scene** (the builder change only affects newly-built scenes):

- `Defenders/World/Build Outer World (Regions + Mine Nodes)`
  (batchmode: `-executeMethod DeNelle.Editor.OuterWorldBuilder.BuildOuterWorld`)

This re-emits `Assets/Scenes/OuterWorld.unity` with the 2 crystal nodes (Mirewood +
Ashwood) now carrying `CrystalMineNode` + renewable tuning. **No NavMesh re-bake is
required for this change** (no new walkable geometry; nodes are markers). If the world
build pipeline normally follows with `Bake World NavMesh`, that's unchanged and optional here.

Editor must be **closed** for batchmode (CLAUDE.md §3). UI did not fire it.

---

## Risks

- **Execution-order on capacity:** `MineNode.Awake` caches `_extractsLeft = TotalExtracts`
  before `CrystalMineNode.Start`/upgrades raise `TotalExtracts`. Net effect: a tier-up's
  **yield** bump is immediate (Extract reads `YieldPerExtract` live), but a **capacity** bump
  applies on the next vein refill, not retroactively mid-vein. This is intended/sensible feel,
  not a bug.
- **WO-144 coupling:** I shipped only the lean grade slice. When WO-144 builds its full ledger,
  it must consume this same `CrystalGrade`/`CrystalRegion` (in DeNelle.Core.World) — flagged so
  it doesn't fork a second enum. The grade currently affects UI label only; it does NOT yet split
  the wallet (that's WO-144).
- **Reflection field names:** the builder sets `MineNode` fields by string name
  (`UseFiniteReserve`, `TotalExtracts`, `RespawnSeconds`). If those fields are ever renamed,
  update OuterWorldBuilder's setters — same pre-existing fragility as the rest of that builder.

---

## Test steps (CLI, after OuterWorld rebuild)

1. Build OuterWorld (above), open Village + load OuterWorld additively (WorldSceneLoader).
2. Walk the hero to the Mirewood (S) or Ashwood (N) crystal mine. Press **[F]** to extract —
   `GameState.AetherCrystals` rises by the region-scaled yield; the node goes on cooldown and
   **refills** (it does not despawn). Confirm it's repeatable after the respawn window.
3. Confirm a **[G] Upgrade Mine** prompt appears in range; with enough crystals, press [G] —
   tier rises, yield-per-extract increases, crystals are debited via `CrystalEconomy`.
4. Confirm the prompt label shows the region grade (Mirewood → Mire, Ashwood → Wraith).
5. Confirm `SettlementPlacer` click-to-claim does NOT claim a crystal mine (renewable, not a
   reserve) but DOES still claim the iron/stone/wood finite-reserve nodes — unchanged.

---

## Acceptance criteria check

1. Crystal Mine is a Crystal-type harvest node on the existing node model — **yes** (MineNode companion, no parallel system).
2. Renewable + rate-limited (refills over cooldown to a capacity cap; repeatable, not one-shot, not infinite) — **yes** (UseFiniteReserve=off, TotalExtracts=8 vein + 90s respawn, capacity scales with tier).
3. Region-graded per WO-144 — **yes** (CrystalGrade via CrystalRegion.TopGradeFor(dangerTier); lean WO-144 slice created since WO-144 was unbuilt).
4. Upgradeable via EconomyService/TrySpend; salvages old CrystalMine tier/UX, code-built prompt — **yes** (CrystalEconomy.TrySpend, [G] bubble).
5. Banks to GameState.AetherCrystals; worker/pet/offline seams exposed not implemented — **yes** (banking stays MineNode's single path; seams untouched).
6. No VillageSceneBuilder edit, no bake fired by UI, no UXML, no new currency, no parallel node system — **yes**.
7. Brace balance on every .cs; Village→Core only; ?. on cross-module calls — **yes** (balance verified; Village refs Core only).
