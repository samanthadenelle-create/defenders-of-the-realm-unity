# WORK ORDER 886 — VFX: enemy death ladder

**Status:** READY TO IMPLEMENT · **Silo:** Combat/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 (locked contract) · `docs/vfx/VFX_PREFAB_HANDBOOK.md` (Step 1–8 pipeline) · `docs/vfx/VFX_CREATIVE_PICKS_REGISTRY.md` §5 (recipes). Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform (`Vfx`/`VfxElementTables`/builders).

## Scope
Repoint every `Death_*` VFXType to its pack recipe so enemy deaths escalate readably by **scale + motion + layer count** (trash pop → boss set-piece), colourblind-safe. All BURST family (fire-and-reclaim), with an optional lingering loop only on big deaths.

## Recipes (registry §5)
| VFXType | Recipe | Lingering |
|---|---|---|
| Death_Generic | SmallExplosion | — |
| Death_Skeleton | SparksEffect (bone-grey) + SmokeEffect wisp | short wisp |
| Death_Wolf | SparksEffect (crystal) + slow Steam drift | snow drift |
| Death_Tiefling | SmallExplosion (ember) | brief WildFire lick |
| Death_Brute | DustExplosion (500-grain) | SmokeEffect settle |
| Death_EnemyExplosion_Dungeon | EnergyExplosion | — |
| Elite_Death | EnergyExplosion (full) | SmokeEffect column |
| Boss_Death **and** Death_Boss (legacy alias) | **BigExplosion (8-layer, whole)** | WildFire OR SmokeEffect column |

## Files to touch
- Builders: SmallExplosion, BigExplosion, DustExplosion, EnergyExplosion, SparksEffect, SmokeEffect, WildFire → `Assets/Resources/VFX/Death/` (handbook Step 4, `ParticlePackVfxBuilder`).
- `Assets/Editor/VFXCatalogGenerator.cs` — Map rows (IsLoop=false for the burst; lingering loops separate).
- `Assets/_Modules/Village/Vfx/VFXManager.cs` — death `case` block (repoint procedural → pooled).
- `Assets/_Modules/Village/Vfx/VfxPool.cs` `SpawnDeathBurst`; `Assets/_Modules/Village/Enemies/EliteVFXController.cs` `OnEliteDeath`; `Enemy.cs` death path.

## Acceptance criteria
**Engineering:**
- [ ] Each `Death_*` above resolves to its committed Resources prefab (not procedural, not gitignored path).
- [ ] `IsLoop=false` on every death burst; any lingering loop is a SEPARATE capped loop (no leak).
- [ ] BOTH `Death_Boss` and `Boss_Death` point at BigExplosion (alias cannot drift).
- [ ] BigExplosion pooled WHOLE (8 layers intact — verify descendant count in builder).
- [ ] Paired SfxId fires (`EnemyDeath`) via VfxToSfx.
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] A trash mob death does NOT look like a boss death — tiers visibly escalate by size/motion.
- [ ] Boss death is a set-piece (8-layer + lingering smoke column + the existing 0.7 camera shake).
- [ ] Deaths read distinctly in greyscale (colourblind): skeleton = shard scatter, wolf = settling drift, tiefling = rising ember, brute = grounded dust.
- [ ] Headless death-burst screenshots opened + attached for trash / elite / boss.

## RESULT
`WorkOrders/WORK_ORDER_886_vfx_death_ladder.RESULT.md` — builders, catalog rows, markers, screenshots.
