# Pets — `DeNelle.Pets`

Pet companion runtime (village-side hooks live in `Village/Pets/`).

## Files

- `Pet`, `PetCatalog`, `PetDeployer` — core pet lifecycle
- `PetHeroLeash` — follow-the-hero movement
- `PetHarvester`, `MineNodeBridge` — auto-harvest (WO-119 / WO-106); pet farming
  now feeds the live economy via MineNode/HarvestSite → EconomyService.AddResource / Grant.
  Assigned pets on HarvestSites get yield bonuses. Note a second `PetHarvester.cs` also exists in `Economy/` (superseded — use this one).
- `PetProgression`, `PetSkillTreeCatalog` — leveling + skill tree
- `PetAnimatorController`, `PetClipPlayer`, `PetAttackVfxBridge`, `PetEmoteController`,
  `PetBillboard` — presentation

> Maintenance: update this README when files are added/removed.
