# WORK ORDER 430 — Comprehensive Instrumentation Pass (TGVRU)

**Status: READY TO IMPLEMENT (active mandate)**
**Owner directive (2026-06-19, BINDING):** *"this isn't just this tree. This needs applied to
every structure through all of our code. WE will not continue till all that is 100% complete and
audit confirms."* + *"each bug we find once and close correctly"* + *"the fact that you can now
target the real error in 3 seconds is exactly why."*

This is a **gate on all other feature work.** Nothing else proceeds until every render/build/spawn
path meets the standard below and a re-audit confirms 100% coverage.

---

## The Standard — "TGVRU"

Every site that **renders to screen, builds presentation, spawns a structure/body/visual, or does a
risky op** (parse · Addressables/Resources load · service lookup · equip · `Instantiate` · scene-cross
· animator bind · pool get/return) MUST have all five:

- **T — Trace:** `FlowTrace.Step` at entry + every branch taken + every fallback (`Warn`) + the
  render/commit seam. (No bare `Debug.Log` — it does NOT reach the F8 break-log.)
- **G — Guard:** `Guard.Try` / `Guard.TryEach` / real try-catch on the risky op. **No bare risky op,
  no silent catch** (catch must `FlowTrace.Fail`).
- **V — Verify:** assert the **actual rendered/built result** — e.g. ≥1 enabled `SkinnedMeshRenderer`
  with a `sharedMesh`; `Animator` controller bound + valid avatar + actually animating (not T-pose);
  list/grid rows > 0; sprite/material non-null; agent `isOnNavMesh`. `FlowTrace.Fail` on wrong state.
- **R — Rollback:** restore a safe visible state on failure (base body / last-good / placeholder /
  empty-state row). **Never** leave broken / blank / T-pose / invisible / magenta.
- **U — Up:** the failure is a `FlowTrace.Fail` (error-level → break-log) so it **self-reports** — the
  owner is never the detector. All behind the `FlowTrace.Enabled` master toggle (zero cost when off).

**Gold-standard reference implementation:** `Assets/_Modules/Village/Hero/HeroArmorVisual.cs`
(`VerifyArmorRendersNow` + deferred `VerifyPoseThenMaybeRollback` + `RollbackArmor` + deep trace).
Mirror this everywhere. In-lane templates also at: `SceneTransitionTrigger`, `RaidClaimService`,
`RaidVictoryController`, `OutpostVictoryController`, `BuildPaletteUI`, `EnemyFactory`, `HeroBowAttachment`,
`WaveManager.BeginLoop`, `DamageNumberSpawner`, `DecalSpawner`.

---

## Audit headline (2026-06-19, 6 read-only audit silos)

- **~180 P0 sites** (render/spawn/panel build with NO verify+rollback) across the codebase.
- **Only `HeroArmorVisual` meets the full bar today.** ~80% of files never even import
  `DeNelle.Core.Diagnostics` — they log via `Debug.*`, invisible to the break-log (**systemic U-gap**).
- **V is essentially absent everywhere** — `if (x == null) return/continue` is used as a "verify";
  it's a null-check, not a result-assertion, and the skip is silent.
- **Inverted priority:** the armor OVERLAY self-protects; the BASE bodies it sits on do not.

### The 6 shared CHOKE POINTS (fix FIRST — each retro-covers dozens of callers)
1. **`VisualFactory.Skin`** — every enemy/troop/structure/prop/animal/companion body skins through it;
   no render-verify, no FlowTrace. Add `VerifyRenders()` here → covers EnemyFactory, TroopFactory,
   StructureFactory, MineNodeVisual, StoryCompanionInjector, HubStructureVisualInjector at once.
2. **`TripoMaterialFixer.Run`** — the grey/magenta fixer; shader-null → silent `Debug.LogWarning`,
   leaves every mesh magenta. Add Fail + post-rebuild magenta/white verify.
3. **`EnvironmentTreeMaterialFixer` + `TreeOfLifeMaterialFixer`** — white-tree / centrepiece class;
   `Debug.Log`-only, no roll-up.
4. **`EnemyFactory.Build` + `EnemyPool.GetInternal`** — null-on-failure with no `FlowTrace.Fail`;
   silent no-spawn, break-log blind.
5. **`StructureFactory.Create`** — the instantiation primitive all BuildMode + Camps funnel through;
   null-guard + LogWarning only.
6. **`HeroBodySwapper.WireHeroBody` + `EquipmentController` attach + `AtbCombatantSwapper.SwapHero/SwapEnemy`**
   — the other body/mesh-swap T-pose twins of the armor bug.

### P0 distribution by area (consolidated)
| Area | ~P0 | Worst offenders |
|---|---|---|
| Enemies/World/BuildMode/Buildings/Camps/Waves | ~79 | Tower (6), ClaimableCamp (5), WaveManager (5), RegionMobSpawner (4), EnemyOutpost (4) |
| Economy/Pets/Store/Wallet/Onboarding/Dungeons | ~48 | DungeonController (5), TitleController (4), JupiterSwap (3), ShopPanel (3), PartyShopMvvm (3), PetDeployer (4), CosmeticApplier (4) |
| Core/HUD | ~30 | VillageHudController (6), CosmeticShopPanel (3), PetSkillTreePanel (3), ClanChatPanel (3) |
| Factories/VFX/Materials | ~25 | VisualFactory (2), TripoMaterialFixer (2), tree fixers (4), EnvironmentVFX (2) |
| Battle/Dialogue/Audio | ~18 | DialogueCommandBridge async verbs (7), AtbCombatantSwapper (2), DialogueService (2), CompanionDialoguePresenter (2) |
| Hero/NPCs | ~15 | StoryCompanionInjector (4), EquipmentController (3), HeroEquipment (2 — confirm live), 3 NPC injectors (1 each) |

### Forbidden silent catches (§12 violations — fix regardless of priority)
CanonicalJson L54 · SaveMigrator L203/235/253 · SceneRouter L325/337 · GameStateService L1242 ·
VillageHudController L689/776 · CosmeticShopPanel L218/259/577 · PetSkillTreePanel L653/666/682 ·
HeroTalentPanel L516 · PlayerProgressPanel L158/168 · AtbCombatantSwapper L224/347/107 ·
AudioBootstrap L152 · CompanionDialoguePresenter L428 · DungeonLayout.ReadTextAsync ·
PetDeployer/PetHeroLeash/PetAttackVfxBridge · InventoryUIBuilder/InventoryGrid · Enemy L1645 ·
EnemyBehaviorTree L66 · WaveData L449 · ElarionUiKit L362 · ReferralService L131 · WaveFeedbackDirector (~7).

---

## Execution plan (orchestrated, file-disjoint waves)

1. **Wave 0 — the 6 choke points** (highest leverage; serialize the shared files, parallel the rest).
2. **Wave 1 — body/mesh-swap T-pose class:** HeroBodySwapper, StoryCompanionInjector, AtbCombatantSwapper,
   EquipmentController, the 3 NPC injectors. (Add a shared `VerifyBodyRendersNow` helper.)
3. **Wave 2 — UI/panel "renders empty/invisible scrim" class:** VillageHudController + bridges,
   PanelManager/PanelRouter/AddressableUIManager post-open verify, all `AdoptPanelSettings` + bootstrap
   `panel==null` bails, ShopPanel/PartyShopMvvm/store rows, Cosmetic/Talent/PetSkill/Clan/Leaderboard panels.
3. **Wave 3 — spawners/pools/world:** Tower + projectile pools, Camps, Waves, RegionMob/Tribe/Ward,
   Dungeons, VFX pools/VFXManager.
4. **Wave 4 — save/persistence + silent-catch sweep:** GameStateService.Load, PersistenceBridge,
   SaveMigrator, CanonicalJson, all forbidden silent catches → Fail/Warn.

**Per file-disjoint silo:** edit-only agents instrument to TGVRU (told NOT to gate/commit) → orchestrator
batch-gates once (`COMPILE_GATE_OK`) → commits each lane by explicit path.

## Audit-confirm criteria ("100%")
- A re-run of the 6 audit silos reports **0 P0** (every render/spawn/panel build has V+R+U).
- 0 forbidden silent catches (grep: `catch` blocks with no `FlowTrace`/`Debug` inside).
- Every failure path is `FlowTrace.Fail/Warn` (not bare `Debug.*`) → reaches the break-log.
- A headless capture run shows the new verify/rollback lines executing (the fix is WATCHED, not assumed).

---

*Source: 6 read-only TGVRU audit silos, 2026-06-19. Standard codified from CLAUDE.md §12 +
docs/INSTRUMENTATION_STANDARD.md + memory `tracing-must-be-deep-enough-to-root-cause-first-pass`.*
