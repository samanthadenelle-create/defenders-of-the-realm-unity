# WORK ORDER 1262 — Dungeon scenes have no repair surface while structures burn

**Status:** SPEC — needs owner ruling (design gap, not a code defect)
**Minted:** 2026-08-28 (CLI, F8 device triage seq 3628)
**Silo:** Dungeon/Village systems
**Evidence (captured):** device `SM02G4061955851`, scene `dg_sunken_vault`, 2026-08-27T20:32Z:
`[Flow:RepairProbe] SURFACES scene='dg_sunken_vault' WallRepairController=ABSENT
HubRepairAffordance=ABSENT WaveManager=none(pure hub) -> NO repair surface exists in this scene at
all while a structure burns. The player has no way to repair anything here.`
(stack: `RepairAvailabilityProbe.ReportSurfaces`)
Capture: `logs/f8-inbox/capture-device-20260828-131839-seq3628.md`.

## What the data says
The probe (built to detect exactly this) confirms: in the sunken-vault dungeon a structure can be
burning with zero repair affordance present — no wall controller, no hub affordance, no wave
manager. This is a DESIGN gap in dungeon scenes, not a regression: the repair surfaces were built
for town/raid contexts and dungeons never got one.

## Owner ruling needed (pick one before implementation)
A. Dungeons intentionally have no repair — then structures in dungeons should not enter a
   burning/damaged state that implies repairability (suppress the state or the probe's error level
   in pure-dungeon scenes).
B. Dungeons get a repair affordance — spec which surface (HubRepairAffordance seems the fit for a
   "pure hub" scene) and its cost rules.

## Acceptance (after ruling)
Probe reports either a present surface or a scene legitimately excluded by design; no error-level
line fires in dungeon play.
