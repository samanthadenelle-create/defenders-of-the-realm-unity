# RAID BATTLEFIELD — DEFINITIVE ANATOMY (2026-08-02, verified from tree)

> Known-dictionary law: audit output banked as a registry. Companion to
> `GROK_CONSULT_RAID_BATTLEFIELD_2026-08-02.md`. Every claim file:line-verified; scene YAML parsed.

## Bottom line
**Yes — the battlefield is literally one square.** `RaidBase_mage_enclave` = 91 GameObjects: 86 wall
segments (2 concentric square rings, 21.6m outer), 19 copies of ONE watchtower prefab, 1 flat 140m
brown plane, 1 BossSpawn empty, camera, light, root. Zero buildings, props, terrain, decorations,
spawn points, deploy ring. Authored content covers **2.4%** of the walkable floor. The "keep" contains
exactly one object: the boss marker. AND the 6 guards spawn **outside the fortress** (placement bug
below) — the only enemy inside the square is the boss, which is exactly what the owner saw.

## Scene inventory (all four parsed)
| | raider_camp_small | fortified_garrison | mage_enclave | IronBastion |
|---|---|---|---|---|
| GameObjects | 39 | 48 | 91 | 91 |
| Walls | 34 wood | 43 iron | 86 steel (2 rings) | 86 |
| Outer square | 15.6m | 18.6m | 21.6m | 24.6m |
| Towers (all `Tower_Medieval_Wood`) | 9 | 12 | 19 | 12 |
| Buildings/props/terrain/spawnpts | 0/0/0/1 | 0/0/0/1 | 0/0/0/1 | 0/0/0/1 |
| RaidGarrisonSpawner | yes | yes | yes | **NO** |
| In Build Settings | yes | yes | yes | **NO** |

**IronBastion is a geometry mock-up**: unregistered (GoRaid can't load it), spawner-less (would spawn
zero defenders). Worse than the scenes.md "disk-only template" note.

## Pipeline (bake-time only; the .unity IS the level)
`RaidBaseGenerator.BuildSceneFor` (menu Defenders/Walls/Build All Raid Scenes) → fresh scene →
`BuildFromConfig` reads `scene-configs.json` → `BuildConfigLayout` composes ONLY: outer ring (+ inner
keep rings) + `PlaceTowers` (one hardcoded prefab path) + BossSpawn at origin → separate
`RaidNavBake.BakeAll` drops the 140m plane (URP/Lit tinted 0.16/0.13/0.10 — the brown) + navmesh.
**Config fields parsed and THROWN AWAY (zero consumers): `centralBuilding` ("tower_arcane_spire"),
`towers[]` (catapults/spires), `props{set,count}`, `eliteCount`.** The rich builder ALREADY EXISTS:
`EnemyStrongholdBuilder.cs` (1378 lines — raised keep, stairs + NavMeshLinks, traps, torches, prop
resolution) but targets Village2 only. Retargeting it at RaidBase_* = the highest-leverage depth move.

## Difficulty cards (`scene-configs.json` :64-224, `RaidSelectionVM` :32-94)
| Card | Scene | Walls | Garrison | Lvl base+off | Diff× | Reward× | Shard |
|---|---|---|---|---|---|---|---|
| Regular | raider_camp_small | wood, 2 gates | 5 (orc-berserker×4, shaman) + necromancer boss | 3+0 | 1.00 | 1.0 | 0% |
| Hard | fortified_garrison | iron, 1 gate | 8 (trolls/ogre/orcs) + boss | 5+2 | 1.25 | 1.5 | 0% |
| Extreme | mage_enclave | steel, 2 rings | 7 (hollow/shaman) + necromancer | 6+3 | 1.30 | 2.2 | 20% |

⚠ `rewardMultiplier` and `shardDropChance` are **cosmetic text** — `RaidScoring.ComputeLoot` never
applies them; nothing reads shardDropChance.

## Defender truths
- **GUARD-RING BUG (why "1 enemy"):** `RaidGarrisonSpawner.cs:165` ring = `baseRadius*0.5` = 16m;
  mage_enclave walls at 10.81m → ALL guards spawn OUTSIDE the fortress in all three scenes (camp 14m
  vs 7.81m; garrison 17.5m vs 9.31m). Only the boss is inside. Generator sizes geometry from
  `wallSegmentsPerSide × 1.5m` and never reconciles `baseRadius`. ~1h fix, mint now.
- **Towers DO fire — at the wrong targets:** `GarrisonTurretArmer` arms all watchtowers as EnemyOwned
  `DefenseTower`s (R16/D8/FR0.8), but `DefenseTower.RescanParty` (:371-377) targets ONLY
  HeroHealth.Instance + StoryCompanions — deployed `TroopController`s are never targeted; and
  EnemyOwned towers are indestructible (IsAlive false, ApplyContactDamage no-op). Canon's "tower-fire
  unbuilt (771.10)" is half-wrong: fire exists, targeting is wrong. Rescope 771.10 accordingly.
- **NOTHING the player brings can damage the fortress:** `TroopController.NearestHostile` filters
  Faction==Hostile IDamageable; walls/gates/towers don't qualify. Troops cannot touch structures.

## Objective model (`RaidScoring`)
- **"Razed %" counts defender KILLS, zero structures** (denominator = TotalGarrison). WO-774 already
  rules the copy → "Defenders %".
- Stars (owner 07-30 ladder): ≥50% kills → 1★ retreat credit; full clear 1★, +clock-or-70%-survival
  2★, +both 3★. Loot = 40×dest+15×stars crystals / 60×dest+20×stars food.
- **Clock 180s HARD but configs advertise par 270/330/420s** — every raid force-ends before its own
  recommended clear time. Design tension to resolve.
- Exits: clock → auto-retreat; full clear → victory/claim/GoCastle; manual retreat; hero death (only
  exists because the hero is present at all).

## HERO ROLE — the ruled-canon violation (load-bearing)
Canon (RAID_NORTHSTAR:29, owner-locked 07-26): deploy and WATCH, never control a unit.
**Code does the opposite deliberately:** `HeroControlEnsurer.IsVillageScene` (:42-47) includes
`HubScenes.IsRaid` → every RaidBase load runs the full village pipeline: recover/spawn hero
("Hero (Blaise)" emergency path; the `RaidHeroSpawner` its comment cites DOES NOT EXIST), then bolts
on PlayerAttackController + gear + abilities. `Enemy.cs` hero-primary aggro is UNCONDITIONAL (no
raid/scene posture check; :1752-1758 = the owner's captured "structure sweep SUPPRESSED" line). Camera:
scene ships a bare Main Camera; ensurer attaches SmartMobileCamera at 5m/10m follow — **why 21.6m of
fortress reads as "a square room"** (guards at 16m are off-frame). `RaidDeployVM.PartyClasses` also
shows hero+companions as raid party on the pre-raid screen.
**The in-raid command layer (RaidDeployController/HudController/Scoring) is already hero-clean —
zero hero references.** Only the arrival layer violates canon.

### Minimal path to the ruled model (~1.5-2 days total) — recommend minting WO-774.0, BEFORE WO-774
1. Remove `HubScenes.IsRaid` from `HeroControlEnsurer.IsVillageScene` (1 line + trace) — kills hero
   spawn; both aggro seams and turret targeting go quiet automatically.
2. `RaidCameraRig`: top-down deploy/overview camera (pitch ~55°, height ~35-45m, drag-pan/pinch clamp
   to base bounds) — `LeanTouchBuildDriver` already implements the gesture layer; mirror the
   DungeonCameraRig skip idiom so the ensurer never stomps it (~150 lines).
3. `RaidDeployController._camera` → rig camera (~5 lines).
4. `RaidDeployVM` drops hero/companion rows → troop loadout (folds into WO-774 §2).
5. Regression: after RaidBase load, `HeroLocomotion == null` + `HeroHealth.Instance == null`.
Sequence AFTER: guard-ring fix → WO-774 → interior-content WO (retarget EnemyStrongholdBuilder +
consume centralBuilding/towers/props) → 771.10 rescoped (turrets target troops + siegeable) → 802 →
803 → 772/771.13 art → 804.

## Docs that lie (for the record)
RaidBaseGenerator:9-14 "killing courtyard" (3.0m corridor) · HeroControlEnsurer:39 cites nonexistent
RaidHeroSpawner · RAID_NORTHSTAR:29 "never control a unit" (you control the hero) · RAID_NORTHSTAR:62
"only if towers don't already fire" (they fire, wrong targets) · "Razed %" (counts bodies) ·
scenes.md IronBastion "template" (also spawner-less + unregistered).

## Session evidence appendix (owner felt-test 2026-08-02)
F8 seq 606 mage_enclave "all pink"/white walls + magenta troops → WO-838 (materials RCA proven:
laptop-absolute-path .fbm texture binding = white on EVERY machine; Supercyan mats never URP-fixed +
MagentaGuard never sweeps runtime spawns). F8 seq 610 fortified_garrison (no note): screenshot shows
iron walls TEXTURED (per-asset binding differences — Phase A probe resolves per-tier truth), one
magenta troop, hero locked-on fighting Orc Berserker = the hero-combatant violation live, Razed 89%
with walls untouched = the "Razed" copy lie live.
