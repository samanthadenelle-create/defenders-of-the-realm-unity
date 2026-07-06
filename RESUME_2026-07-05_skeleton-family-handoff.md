# RESUME — 2026-07-05 (bedtime handoff)

**Branch:** `wip/village2-and-f8-tickets`  
**Session focus:** AccuRig skeleton family integration + hollow-warrior remap/tune + proportional sword/shield (prior in tree)

---

## Landed this session (commit on this doc's date)

### 1. AccuRig skeleton family (LIVE)

Four Character-Creator / AccuRig humanoid silhouettes in `Assets/Resources/Enemies/`:

| Silhouette | File | Wired to |
|------------|------|----------|
| Mage | `Skeleton_Mage.fbx` | `hollow-apprentice` (ATB), caster body |
| Warrior | `Skeleton_Warrior.fbx` | `hollow-warrior`, ATB default grunt visual |
| Ranger | `Skeleton_Rogue.fbx` | `hollow-rogue`, `feral-wolf` stand-in (slug = Rogue) |
| Healer | `Skeleton_Healer.fbx` | `hollow-acolyte` |

- **Rig:** Humanoid `CC_Base_*` → `SkeletonHumanoid.controller` (Mixamo `Assets/Action`)
- **Import menu:** `Defenders → Animation → Import Skeleton Family (AccuRig)`
- **Batchmode:** `-executeMethod DeNelle.Editor.PeopleCharacterImporter.ImportSkeletonFamily`
- **Verified:** all four Humanoid avatars OK (Healer needed re-export with skeleton embedded)

KayKit **legacy** unchanged: `Skeleton_Minion`, `Skeleton_Golem`, `Necromancer` (Generic rigs).

### 2. hollow-warrior remap + stat tune

- Model: `Skeleton_Golem` → **`Skeleton_Warrior`** (AccuRig)
- Village stats (bruiser-tier → standard melee):

| Stat | Old | New |
|------|-----|-----|
| HP | 320 | 156 |
| Move | 1.8 | 2.2 |
| Damage | 14 | 10 |
| Interval | 1.6s | 1.3s |

- ATB: new `ENEMY_DEFS["hollow-warrior"]` (HP 110, Atk 22, Def 0.18, Shield Bash) — no longer `bruiser`
- Outpost role: Tank → DPS

### 3. Proportional sword/shield (`EquipmentController.cs`)

- Hero standing height measured from renderers; held lengths scale vs 1.8m ref
- Sword archetype 0.65m, shield 0.45m at ref height
- Fleet + compile gate green before skeleton work continued

### 4. Canon docs updated

- `docs/enemy-codex.md` — AccuRig family replaces KayKit for Mage/Warrior/Rogue/Healer
- `docs/MODEL_CATALOG.md`, `docs/kaykit-asset-catalog.md`, `docs/asset-inventory/05_resources_project_built.md`
- `enemies.json` (Resources + StreamingAssets) — mesh sources + hollow-warrior block

---

## Key files touched

**Code:** `PeopleCharacterImporter.cs`, `EnemyAnimatorSetup.cs`, `EnemyAnimatorFactory.cs`, `EnemyFactory.cs`, `Enemy.cs`, `AtbCombatantSwapper.cs`, `BattleController.cs`, `Defs.cs`, `GarrisonStatBlocks.cs`, `OutpostEnemyGroupSpawner.cs`, `WaveCompositionBuilder.cs`, `EquipmentController.cs`

**Assets:** `Skeleton_{Mage,Warrior,Rogue,Healer}.fbx`, `SkeletonHumanoid.controller`, `.fbm` texture folders, `Material_Pbr_Diffuse.mat`

---

## NOT committed (intentionally)

- `QA_F8_ARCHIVE/fleet-preserved_2026-07-05_184555/` — local F8 archive
- `terminals/` — temp CLI scripts

---

## Open F8 queue (felt-test — not fixed this session)

From build session ~Jul 5 evening:

| Flag | Issue | Silo |
|------|-------|------|
| HUD left panel | Huge empty yellow-bordered panel | HUD Kit |
| Forge panel | Text overlap; armor catalog not swords | Forge UI |
| Battle posture | Peaceful HUD stays on during combat Lv10 | PostureEvaluator |

Earlier: sheathed weapon placement, weapon re-seat on battle, enemy weapon scale, Continue button, mobile HUD.

---

## Verify tomorrow (owner felt-pass)

1. Launch `Builds/Windows/DefendersOfTheRealm.exe` (rebuild if stale — wipe `Builds/Windows` first)
2. Trigger a mixed hollow wave — confirm **four distinct AccuRig silhouettes** animate (not T-pose)
3. **Hollow Warrior** should read as armored melee (~156 HP feel), not golem tank
4. Knight sword/shield scale on hub hero (proportional sizing)
5. F8 anything still wrong

**Headless (CLI):** `ImportSkeletonFamily` already green; optional `CompileGate.Run` + 1-run autopilot after pull.

---

## Push policy

**Local commit only** — push after owner felt-verifies (project rule).

---

_Good night — the dead march with new bones._