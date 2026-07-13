# WO-672 RESULT — Unified structure damage lifecycle (DONE; one follow-up ticket spawned)

**Committed:** `80a2f944` + `1b3224f6` (2026-07-08 wave 2/3 arc, with F8-50), gated + fleet-clean
(3 known pre-existers only). Owner felt-closed in the 07-08→11 batches. RESULT written
retroactively 2026-07-13 during the sync handoff.

- hp==0 = broken shell everywhere; damage bars + Ember smolder/fire tells off `HpFraction`
  (`StructureDamageVisuals`, `damage-states.json` dual-copy); Raid_Explosion on break.
- Repair All on the wave report via the ONE crystal/in-kind spend path (`WallRepairController`);
  collector damage scales accrual ("damage to collectors reduces economy" — owner).
- Proven live in Player.log: `[Flow:DamageVis] bar attached: 'forge' (collector) hp=0.85`.
- **Known defect found later (REP-1, 2026-07-13):** the charged repair passed a fixed
  `Repair(100f)` — full restore for walls/gates (MaxHp≤100) but PARTIAL for Buildings
  (MaxHp 120–240). Root-fixed in the 07-13 wave (`RepairTarget.RepairFull()` + REPAIR_PROBE);
  see REP-1 disposition in `CANON_GROUND_TRUTH_2026-07-13.md`. Slice B's desaturated-material
  broken look was never implemented (bar+ember+explosion shipped instead) — noted, not a defect.
