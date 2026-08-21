<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-270: ClaimableNode.DestroyNode() — cache Building list and EconomyService
**Linear:** [DEF-139](https://linear.app/defenders-of-the-realm/issue/DEF-139/claimablenodedestroynode-findobjectsoftypebuilding-on-every-raze)
**Lane:** Combat/AI
**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
**Priority:** Medium

## Acceptance Criteria
- [ ] `FindObjectsOfType<Building>()` removed from `DestroyNode()`
- [ ] `List<Building> _spawnedBuildings` maintained on `ClaimableNode`, populated in `OutpostBuildPanel.OnBuild()`
- [ ] `FindObjectOfType<EconomyService>()` removed from `FinishPlayerRaze()`
- [ ] `EconomyService` reference cached in `Awake()`
- [ ] No functional regression in raze behavior
- [ ] Brace balance check passed

## Files to Edit
- `Assets/_Modules/*/ClaimableNode.cs`
- `Assets/_Modules/*/OutpostBuildPanel.cs` (if needed to populate building list)

## Do NOT Touch
- Village.unity (never hand-edit)
- Files outside Combat/AI lane

## Dependencies
- None — standalone perf fix. Can be batched with WO-250.

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `no ClaimableNode.cs/OutpostBuildPanel.cs` — targets never existed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
