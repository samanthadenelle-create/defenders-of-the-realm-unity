# Pets — `DeNelle.Pets`

Pet companion runtime (village-side hooks live in `Village/Pets/`).

## Files

- `Pet`, `PetCatalog`, `PetDeployer` — core pet lifecycle
- `PetHeroLeash` — follow-the-hero movement
- `PetHarvester`, `MineNodeBridge` — auto-harvest (WO-119); note a second
  `PetHarvester.cs` also exists in `Economy/` — check which is live before editing
- `PetProgression`, `PetSkillTreeCatalog` — leveling + skill tree
- `PetAnimatorController`, `PetClipPlayer`, `PetAttackVfxBridge`, `PetEmoteController`,
  `PetBillboard` — presentation

> Maintenance: update this README when files are added/removed.
