# COVERAGE VALUE SCORECARD — the battery vs ALL OPEN bugs

**Date:** 2026-07-08. **Question (owner):** run the coverage system against all open bugs and really see its
value — does it actually detect what's currently broken? **Method:** enumerate every open bug (ledger open
COVs + CLI_PREP open F8 tickets + pre-existers + the LAST_100 NONE gap), cross-map each to the LIVE battery
result this session (`RunAll` 5 oracle FAILs + 8-seed fleet 4 tickets), classify DETECTED / COVERED-BUT-GREEN
/ BLIND. Sibling docs: `LAST_100_BUGS_AUDIT_2026-07-08.md` (fixed bugs hold), `COVERAGE_FINDINGS_LEDGER_2026-07-08.md`.

## THE NUMBER
| Class | Count |
|---|---|
| **DETECTED** (a check currently FAILS on it) | 8 |
| **COVERED-BUT-GREEN** (a check maps to it but PASSES — false comfort) | 6 |
| **BLIND** (no check) | 9 |
| **Total open items** | 23 |

**Detection rate:** 8/23 = **35%** raw · 8/20 = **40%** of actionable defects (excl. 2 unbuilt features + 1 design pin a battery can't judge). The battery catches ~1 in 3 open items, ~2 in 5 real open defects.

## DETECTED (8) — the system's strength: structural/data/logic invariants
F8-39 towers-vanish (`TowerRespawn`) · F8-41 untargetable towers (`DefenseTargetable`) · COV-003 arena untextured ground (`ArenaPrefabAudit`) · COV-013 pet-slot (`GlimmerEconomy`) · COV-021 Wood/Iron dual-wallet (`VillageEconomy`) · WO-602 home-return (fleet) · WO-453 rep-spawn (fleet) · CavePortal seam (fleet). Every one is a deterministic structural/data invariant — exactly where oracles + real-path probes are strong.

## COVERED-BUT-GREEN (6) — FALSE COMFORT, the audit's most valuable finding
A green check reads as "covered" on the dashboard while the open bug sails underneath — worse than no check.

| Open bug | Check that's GREEN | Why it doesn't fire |
|---|---|---|
| F8-34 gray T-pose enemy | `EnemyRigColor` 10/10 | static rig/color sheet read; T-pose is a runtime animator-bind state — needs a live-instance probe |
| F8-37 arena pole | `ArenaPrefabAudit` | pole is instantiated at RUNTIME, not in the prefab the audit reads; the `BattleArena AUDIT` fleet probe was never driven |
| F8-38 walk-while-cast | (instrumented only) | `drivenav-casting` trace logs it, but NO probe asserts "isStopped stays true during cast" — instrumentation ≠ detection |
| F8-15 death popups | `DeathTrace` (forensics only) | captures the PanelManager bypass + timeScale freeze, but nothing FAILS on them |
| COV-008 Glimmer debit-without-grant | `GlimmerEconomy` | the oracle fails on pet-slot, NOT the pay-without-grant invariant — that money path is unproven |
| COV-009 CanonicalJson silent null | `CoreDataHub` | passes by loading 48 files that all resolve; never drives the dual-copy-MISS branch where the silent null is |

## BLIND (9) — no check
- **Save round-trip gap:** COV-012 Tribes/Settlements/Wards dropped on reload (`CoreSaveContract` asserts version-triple only, not collection round-trip).
- **UI-layout cluster:** F8-31 nameplate GUID/castBar, F8-32 portrait mask, F8-33 BR icons, F8-35 victory rows, 11 card-framed portraits — no glyph/layout oracle exists.
- **Not battery-detectable (route to spec/owner):** F8-40 max-tier tower identity (feature), F8-42 repair costs (feature), F8-23/26 wave-countdown posture (design pin).

## HIGHEST-ROI NEXT MOVE — assertions, not more instrumentation
Converting the 6 COVERED-BUT-GREEN into FAILING assertions raises real-defect detection **40% → ~70% with ZERO new instrumentation** (the traces already exist):
1. Runtime enemy-animator-bound probe (F8-34) — a live-spawn rig check, not the static sheet.
2. Drive the `BattleArena AUDIT` fleet probe (F8-37) — a runtime renderer scan in the arena.
3. Assert `isStopped` holds through a cast (F8-38) — read the `drivenav-casting` trace and fail on movement.
4. Assert end-states register with PanelManager + timeScale restores (F8-15) — fail on the DeathTrace bypass line.
5. Assert Glimmer debit⇒grant round-trip (COV-008) — the pay-without-grant invariant, not just pet-slot.
6. Drive `CanonicalJson.Read` on a MISSING dual-copy (COV-009) — assert it warns, not silently null.

Then the named BLIND next-oracles (save round-trip, UI-layout/glyph, orientation, structure-albedo) convert the remaining gap.

## HONEST VERDICT
Against OPEN bugs (not the fixed ones it proves are holding), the coverage system is a **partial net: ~40% of actionable open defects, all of them structural/data/logic.** Its blind spots are systematic and honest — runtime-only visual state, UI-layout/presentation, and behaviors that are traced but never asserted. The single most important takeaway is the **6 false-green checks**: they overstate coverage. The cheapest, highest-value work is turning those 6 into failing assertions (no new instrumentation) — that is where "really seeing its value" points next.
</content>
