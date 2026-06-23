# WORK_ORDER_297 — Pet acquisition (tame / egg-hatch / rescue) + active slots

**Status: READY TO IMPLEMENT**
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
