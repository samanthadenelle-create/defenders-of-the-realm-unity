<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_297 — Pet acquisition (tame / egg-hatch / rescue) + active slots

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
**Branch:** feat/tower-core-loop · **Lane:** 6 · **Depends on:** 290 (QuestService) for gating
**Design source:** `DESIGN_PET_SYSTEM.md` §2–3, §6

## Context
Reconcile with existing: `PetUnlockTracker` (per-species level/xp/skills, PlayerPrefs), `PetDeployer`
(spawn/deploy, SetHeartPosition, DeployStarterPets), `PetHarvester`, `PetSelectController`. Do NOT greenfield.

## Goal
Let the player obtain more pets via taming, egg-hatching, and camp rescue, with role-based active slots.

## Files to edit / create
- New `Assets/_Modules/Pets/PetSpeciesCatalog.cs` — species defs (region, role, signature, model id).
- New `Assets/_Modules/Pets/PetAcquisitionService.cs` — tame flow (weaken→bond approach), egg item +
  hatch timer (offline-aware), camp rescue hook (`ClaimableCamp`), slot management.
- Extend `PetDeployer` for role assignment (harvest node / guard outpost / follow party) + slot count.
- Gate species/slot unlocks via `QuestService` + region-clear flags.

## Scope
- Active slots: start 1 → 2 (Fenn questline) → 3 (village tier). Resting pets gain trickle XP.
- Eggs drop from camps/raids; hatch by feeding Food over time (works offline via accrual).
- Rescue: freeing a caged beast in a cleared camp bonds it.

## Acceptance criteria
- [ ] Player can tame a wild pet in a region and it joins the roster.
- [ ] An egg can be obtained and hatched (offline-aware) into a region-appropriate species.
- [ ] Rescuing a caged beast in a camp bonds it.
- [ ] Slot cap enforced; deploying assigns a role; resting pets persist + gain trickle XP.
- [ ] Reconciles with PetUnlockTracker/PetDeployer (no duplicate pet state); brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't fork PetUnlockTracker; extend the existing pet stack.
