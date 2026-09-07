# WO-1537 RESULT - the repair math was never broken; the PROBE FIXTURE was

**Status:** IMPLEMENTED 2026-09-07 - uncommitted, awaiting the gate. Edit-only lane, no Unity, no git.

## What WO-1352 actually fixed (the question the WO asked first)
WO-1352 changed **structure damage VISUALS only** - albedo/smoothness rungs inside the existing
`StructureDamageVisuals` owner, so wear shows from the first point of damage. It never touched
`Building.Repair`, `WallSegment.Repair` or `RepairTarget.RepairFull`, so it could not have covered this
symptom and did not regress it. The two tickets share the word "repair" and nothing else.

## Root cause - the section-2 fix shape is DISPROVEN
No MaxHp-vs-tier mismatch; the fraction is a flat `0.00`, not a partial, because repair never ran:
- `Assets/_Modules/Village/Buildings/Building.cs:260` - `Repair()` returns early on `IsDestroyed`
  (hp <= 0), tracing `[Flow:Destroy] Repair ignored on DESTROYED building`.
- `Assets/_Modules/Village/Walls/WallSegment.cs:504` - the mirrored guard on a collapsed section.
- Both are the **WO-753 owner ruling**: destroyed = LOST, it returns only via a full-cost placement.

The probe drove every fixture to hp=0 / damage=100 - the **DESTROYED** state the ruling excludes -
then asserted a full restore. Gate passed only because `Gate.Repair` has no such guard. The eleven
"failures" were the ruling working. The probe header still cited `Building.cs:221-225` for a bare
additive clamp: authored pre-2026-07-19, opted out of RULE 2, registered by WO-1496 on 09-06 without
re-reading the fixture. **No production HP math was changed** - a DAMAGED building has always reached
full (`_hp = min(_maxHp, _hp + MaxHp)`, `Building.cs:267`). DEVIATION, declared: the WO asked for a fix
at the repair path; that would reverse the WO-753 ruling and break `DestroyedStructureRegression`,
which pins `WallSegment.Repair()` no-opping on rubble - and `RepairFull` routes walls through
`Repair(100f)`. The fixture was corrected instead.

## The change
1. `Assets/Editor/Regression/RepairProbeRegression.cs` - fixture drives **DAMAGED, not destroyed**:
   buildings to hp=1 (PATH 1 and the PATH 2 reset), wall to damage ~99 via a bounded 0.5f loop
   (`ApplyContactDamage` divides by tier toughness >= 1 and BULWARK >= 0, so a step cannot overshoot).
   Assertions (`HpFraction >= 0.999`, `!NeedsRepair`) unchanged on every row - not weakened. Legacy
   threshold `> 100.001f` -> `> 101.001f` (from hp=1, `Repair(100f)` tops at 101; all ten rows still
   qualify at 120..240). **PROBE D added**: Building and WallSegment driven to DESTROYED,
   `RepairFull()`, assert they STAY destroyed - `DestroyedStructureRegression` pins that ruling on the
   raw `Repair(amount)` primitives, nothing pinned the `RepairFull` seam the CHARGED paths call.
2. `Assets/_Modules/Village/Walls/RepairTarget.cs` - `RepairFull` emits `FlowTrace.Warn` when the
   fraction does not move, so a charged no-op is a logged anomaly, not a normal-looking `Step`.
   Behaviour unchanged; warnings are non-fatal to the batch (`DataRegression.cs:4350` logs one itself).

## Findings for the lead (no ruling exists; not acted on)
- **Gate has no destroyed guard.** A hp-0 Gate IS restored by `RepairFull`, which is what
  `ConfirmRepair`'s rebuild branch (`>= DestroyedFraction` -> FULL build cost) pays for. Coherent for
  gates, a WO-753 consistency gap for the kind. NOT pinned - inventing a ruling is worse.
- **That branch is unreachable for Building/Wall today**: `AddDamagedOfType` skips
  `>= DestroyedFraction` (`WallRepairController.cs:361`), `Collapse()` drops every solid collider, a
  destroyed Building is removed by `Destructible`, and `RegisterStructures` has zero callers. Nobody is
  billed today - the new `FlowTrace.Warn` is the detector if a door ever opens.

## Acceptance
- [x] Fixed at the authority; the eleven named (10 buildings + wall); probe not weakened, case ADDED;
      WO-1352 statement above; brace/NUL clean (73/73, 52/52).
- [ ] `REGRESSION_OK n/n` on a fresh log - CLI gate; this lane cannot run Unity.
