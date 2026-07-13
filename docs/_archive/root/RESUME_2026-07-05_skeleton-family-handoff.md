# RESUME — 2026-07-05 (bedtime handoff)

**Branch:** `wip/village2-and-f8-tickets`  
**Session focus:** AccuRig skeleton family integration + hollow-warrior remap/tune + proportional sword/shield — plus hero-feel fixes earlier the same day (walk gait, animation-clip conflicts, native sword grip)

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

**Rig importer / bone-integrity check (`PeopleCharacterImporter.cs`):** the importer now
*confirms the rig works and the expected bones exist* rather than importing blind —
`SkeletonAvatarVerdict(dst)` returns a per-model verdict (`OK Humanoid` / `WARN Generic` /
`FAIL no avatar`), asserting each silhouette resolves to a **valid Humanoid avatar** (the
`CC_Base_*` bones map onto the biped). `RepairSkeletonAvatars` runs a 3-pass fallback when a
rig imports Generic: (1) `CreateFromThisModel`, (2) copy a proven shared skeleton avatar,
(3) copy the Mage humanoid bone map (same CC_Base naming). Every model logs its verdict into
the DONE report, so a missing/mismapped bone surfaces at import time — not as a T-pose in-game.

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

### 5. Hero feel fixes (earlier commits, same day — now documented)

Hero-animation/gear fixes landed earlier on 2026-07-05, ahead of the skeleton work:

- **Walking animation + turn/walk clips fighting → crouch fixed** (`86847b7f`): the root cause
  was a **clip conflict** — the `turnleft180` turn-in-place clip is low-pivot and *reads as a
  crouch*; in town the hero fed that turn signal **while** walking forward, so the turn-in-place
  clip fought the `Shared_Walk_Forward` clip and blended into a crouch. Fix: turn-in-place clips
  (`DriveTurnSignal`) are now **combat-only** (`if (engaged)`); town slews facing by input
  instead (`Quaternion.Slerp` toward the move heading, `PlayTurn(None)`), and a `TownMoveSpeedMax`
  (3.5 m/s) cap keeps KnightMocap on the upright calm `Shared_Walk_Forward` gait instead of the
  braced sword+shield run at 6 m/s. Calm-gait vs combat-gait blend trees split. Files:
  `HeroLocomotion.cs`, `HeroAnimatorFactory.cs`, rebuilt `KnightMocap.controller`.
- **Holding the sword — native grip** (`d48bfd41` WO-478): weapon grip now seats via
  `SeatNative` (rolled back `ff.weapongripinfer`); `EquipmentController.cs` + `FeatureFlags.cs`.
  Complements the same-session `WeaponBoundsOrient` geometry-derived orientation (Y-long / hilt
  at short-Y end) from `315d60e3`.

_Related combat-anim work (same session, NOT the crouch conflict): `315d60e3` WO-609 posture flip
(calm default, hostile only on pursuit/lock/wave/BattleLock) + `HudPostureReset`, and `38c7fd4b`
WO-586 directional death buckets (`CombatDeathDirection.cs`) + `BattleLock` braced stance._

---

## Key files touched

**Code (skeleton family):** `PeopleCharacterImporter.cs` (rig importer + bone-verdict/repair), `EnemyAnimatorSetup.cs`, `EnemyAnimatorFactory.cs`, `EnemyFactory.cs`, `Enemy.cs`, `AtbCombatantSwapper.cs`, `BattleController.cs`, `Defs.cs`, `GarrisonStatBlocks.cs`, `OutpostEnemyGroupSpawner.cs`, `WaveCompositionBuilder.cs`, `EquipmentController.cs`

**Code (hero feel — §5):** `HeroAnimatorFactory.cs`, `HeroLocomotion.cs`, `KnightMocap.controller`, `PostureEvaluator.cs`, `HudPostureReset` (HUD Kit), `CombatDeathDirection.cs`, `OrcHumanoid.controller`, `EquipmentController.cs`, `FeatureFlags.cs`, `WeaponBoundsOrient.cs`

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
5. **Walk gait** — town hero walks upright; **turning while walking stays upright, no crouch** (the turn-in-place/walk clip conflict — `86847b7f`)
6. **Sword grip** — hero holds the sword by the hilt, correctly oriented (native SeatNative grip)
7. F8 anything still wrong

**Headless (CLI):** `ImportSkeletonFamily` already green; optional `CompileGate.Run` + 1-run autopilot after pull.

---

## Push policy

**Local commit only** — push after owner felt-verifies (project rule).

---

_Good night — the dead march with new bones._