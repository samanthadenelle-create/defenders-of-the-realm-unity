# WO-759 RESULT — Wire VfxManualPicks into gameplay

**Status:** IMPLEMENTED (code + data). **Catalog regenerate required in Editor** (Unity was open; batchmode locked).

## SME model (how it works)

1. Owner tags prefabs in **VFX Caster** → `Assets/Editor/VfxManualPicks.json`
2. **Defenders → VFX → Generate Hovl VFX Catalog** merges Map + manual rows into
   `Assets/Resources/VFX/HovlVfxCatalog.asset` (manual wins on key collision)
3. Runtime: `VFXManager.PlayKey("SomeKey", pos, …)` looks up the catalog row and pools the prefab

## What was wired

| Area | Keys (owner roster) | Call site |
|------|---------------------|-----------|
| Archer tower bolt | `ArcherTower_Projectile` (+ Fire/Ice element variants) | `DefenseTower.SpawnProjectileVisual` follow + impact-on-arrive |
| Sky Ballista (airOnly) | `RangerTowerBaseProjectile_Projectile` | same, AirOnly branch |
| Arcane Spire | `SimpleCast_Cast`, `ARcaneTower_Projectile` / L2+ `ArcaneTower-Baselevel_Projectile`, `PP_PlasmaExplosionEffect` | `ArcaneTower.FireBlast` / `ApplyBlast` |
| Enemy rooted cast | `EnemyCast_Cast`, `SimpleCast_Projectile`, `FireImpact_Impact` | `Enemy` defaults (type sets still override) |
| Sword on-hit | `Weaponskillsword_Impact` (+ perfect → `KnightWeaponskill_Impact`) | `PlayerAttackController` melee connect |
| Mage Q/W/E/R | SimpleCast / Freezing / NoneMageHealingCast / MageMeoteorAOE | `abilities.json` mage |
| Heals / buffs | `NoneMageHealingCast_Cast`, `softhealingaura_Aura` | knight E, mend, fountain, knight-skills |
| Healing fountain | `HealingFountain_Aura` | `HealingFountain.AuraKey` |

## Code-key aliases (manual overlay)

`tools/merge_vfx_aliases.py` added aliases so legacy keys (`Heal_Cast`, `Fireball_*`, `Melee_Impact`, …)
point at the same prefabs as the owner names. Re-run after pick edits if needed.

## MUST DO in Unity Editor (owner / next session)

1. Menu: **Defenders → VFX → Generate Hovl VFX Catalog**
2. Confirm console: `HOVL_VFX_CATALOG_OK` and that ArcherTower_*/EnemyCast_Cast/NoneMageHealingCast_Cast appear
3. Felt-test: archer fire, ballista, arcane spire, enemy cast, melee sword hit, mage heal / meteor

## Files touched

- `Assets/_Modules/Village/Buildings/DefenseTower.cs`
- `Assets/_Modules/Village/Buildings/ArcaneTower.cs`
- `Assets/_Modules/Village/Buildings/TowerCombat.cs`
- `Assets/_Modules/Village/Buildings/HealingFountain.cs`
- `Assets/_Modules/Village/Enemies/Enemy.cs`
- `Assets/_Modules/Village/Enemies/PlayerAttackController.cs`
- `Assets/StreamingAssets/Data/Canonical/abilities.json` (+ Resources mirror)
- `Assets/Editor/VfxManualPicks.json` (+ code-key aliases)
- `tools/merge_vfx_aliases.py`
