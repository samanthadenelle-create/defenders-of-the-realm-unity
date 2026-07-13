# WORK ORDER 701 — Mending Echoes: perk-gated passive structure repair while offline *(renumbered from a colliding fresh 686 mint, 2026-07-13 — 686 is webtrace ingestion hardening)*

**Status: SPEC — needs owner number pins, then READY** (owner idea 2026-07-12: "an ability or
perk to allow echoes to passively repair structures while you're offline… would need to
determine how much they could repair over x time").
**Lane:** Economy/Harvest + Talents. **Type:** NEW perk on built systems.
**Home:** a WO-676 **Steward branch** node (mid/high tier — it's a meaty strategic passive) +
this WO for the offline mechanics. Canon fit: echoes = autonomous allies you set up and benefit
from (COMBAT_PIVOT unifying principle) — repair-while-away is harvest-while-away's sibling.

## Design

1. **The perk (tree node, data row per WO-676's law):** "Mending Echoes" — while you're away,
   your echoes tend damaged structures. No assignment micro in V1: with the perk owned, ALL
   echoes contribute passively (keeps the one-interaction echo rule; a dedicated "repair lane"
   assignment can join WO-658's picker in V2 if depth is wanted).
2. **Offline mechanics (the OfflineHarvestService clock pattern):** on return, compute elapsed
   real time → repair each damaged structure by `rate × hours × echoCount`, applied cheapest-
   first (or most-damaged-first — pin). Present it in the welcome-back moment: "Your echoes
   mended the Forge (+40%) and the east wall." (the return-hook canon — the tree works while
   you're away, now it heals too).
3. **It SPENDS, never conjures (protects F8-42 repair-costs canon):** repair consumes the
   in-kind materials from the SILO/pending stock the echoes gathered — the perk's value is
   automation (and optionally a discount), not free hitpoints. No materials banked = no repair
   (traced, surfaced in the welcome-back line: "…ran out of iron before finishing").
4. **Hard limits (keep player decisions alive):**
   - DESTROYED (hp==0 broken-shell) structures are NEVER auto-rebuilt — rebuild stays a player
     decision at full cost (WO-672 rule).
   - Offline only (plus optionally idle-in-hub — pin). During play, repair stays the player's
     verb (WO-684 context button / Repair All).
5. **First-pass numbers (owner tunes — the "how much over x time"):**
   `repair-rules.json` (dual-copy): `ratePctPerEchoHour: 5` (3 echoes ≈ 15%/hr), `materialCostMult:
   0.75` (the perk's 25% discount vs hand-repair), `capPct: 100`, `offlineMaxHours: 12` (same
   clamp class as offline harvest). At those numbers an overnight absence fully mends a 60%-
   damaged Forge IF the silo held the materials — feels generous but paid-for.

## Gates
- [ ] EditMode: offline math (elapsed × rate × echoes, material spend, max-hours clamp,
      destroyed-excluded) unit-tested; json parses + dual-copy sync; the WO-676 G3 no-dead-node
      gate covers the new effect type (`offlineRepair` → ONE reader in OfflineHarvestService).
- [ ] Fleet/headless: damage structures, simulate offline elapsed, assert HP restored ==
      formula, silo debited exactly, broken shells untouched; without the perk: zero repair.
- [ ] Welcome-back line names what was mended + what starved; `[Flow:Echo]`/`[Flow:Repair]`
      step-in/out; COMPILE_GATE_OK + REGRESSION_OK + owner felt-pass (PO closes).

## Owner pins
1. The numbers in §5 (rate / discount / max-hours) — first-pass ok?
2. Repair order: most-damaged-first (recommended — triage instinct) vs cheapest-first.
3. Offline only (recommended V1) vs also trickling while idle in the hub?
4. Tree placement: Steward tier 3 node vs capstone-adjacent (it's strong — priced accordingly).

## What NOT to touch
Repair pricing base (WO-672) · echo count/growth (WO-587) · harvest accrual math · no new
clock — reuse the OfflineHarvestService elapsed-time seam (single clock canon, WO-667).

*Cross-refs:* WO-676 (Steward branch home + G3 gate) · WO-672/F8-42 (repair-costs canon) ·
WO-658/WO-667 (echo lanes + single offline clock) · COMBAT_PIVOT_NORTHSTAR (autonomy principle,
return-hook) · ticket REP-1 (repair seam fix lands first).
