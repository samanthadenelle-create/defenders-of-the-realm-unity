# Pets — `DeNelle.Pets`

Pet companion runtime (village-side hooks live in `Village/Pets/`).

## Files

- `Pet`, `PetCatalog`, `PetDeployer` — core pet lifecycle
- `PetAcquisitionService` — WO-297: tame / hatch / rescue → roster unlock
  (GameState.Pets + OwnedPets) + active deploy-slot model; asks PetDeployer to
  re-sync via `SyncDeployedToSlots`. Clean seam for quest gating (WO-299) — no Yarn.
- `PetHeroLeash` — follow-the-hero movement
- `PetHarvester`, `MineNodeBridge` — auto-harvest (WO-119 / WO-106); pet farming
  now feeds the live economy via MineNode/HarvestSite → EconomyService.AddResource / Grant.
  Assigned pets on HarvestSites get yield bonuses. Note a second `PetHarvester.cs` also exists in `Economy/` (superseded — use this one).
- `PetProgression` **DELETED 2026-08-16 (WO-993)** — owner ruling "same with pet progression": Echoes are a faucet, not a levelling companion. `Pet.SetProgressionMultipliers` went with it; `HeroProgression` is now the only `IXpEarner`. `PetSkillTreeCatalog` DELETED 2026-07-08 (pet skill-tree retire — dead content, pets are harvest/companion only per docs/COMBAT_PIVOT_NORTHSTAR.md).
- `PetAnimatorController`, `PetClipPlayer`, `PetAttackVfxBridge`, `PetEmoteController`,
  `PetBillboard` — presentation

> Maintenance: update this README when files are added/removed.
