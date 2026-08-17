# ENEMY & WAVE CATALOG — the consolidated, source-cited enemy registry

**Built:** 2026-08-16 · **Branch:** `wip/village2-and-f8-tickets` · **HEAD:** `30fda4ffd` (working tree
carried uncommitted edits from other seats while this was built) · **Method:** read-only enumeration of
the runtime data + every resolver and spawner that touches it. Every fact carries its `file:line` so any
single row is re-verifiable at a glance (project memory `audit-outputs-as-known-dictionaries`).

> **⚠ LINE NUMBERS ARE AGAINST THE LIVE WORKING TREE, NOT A FROZEN COMMIT.** `EnemyBrain.cs` moved by
> ~50 lines *during* this pass (another seat was editing it), so a cite that is off by a few dozen lines
> is drift, not an error — the quoted symbol name is the durable half. Re-grep the symbol, not the line.

> **⚠ THIS IS A DERIVED REGISTRY, NOT A DESIGN DOC.** Where code and data disagree, the code wins and
> the row says so. Where a source is silent the tag is **Unassigned** or **none**, never a guess.

Companion registries built the same night: `docs/reference/ICON_CATALOG.md`,
`docs/reference/WEAPON_CATALOG.md`, `docs/reference/VFX_CATALOG.md`. This is the third of the set.

---

## 0. The authorities (read these before trusting any other doc)

| Authority | What it decides | Path |
|---|---|---|
| Enemy stat catalog | which enemies exist, and their base stats | `Assets/Resources/Data/Canonical/enemies.json` (19 rows, `version: 5`) |
| Wave schedule | countdown, combat budget, authored boss / apexBoss / bossHp | `Assets/Resources/Data/Canonical/waves.json` (20 waves + an `endless` block) |
| Wave **roster** | who is actually in each wave (the schedule does NOT decide this) | `Assets/_Modules/Village/Waves/WaveCompositionBuilder.cs:169-289` |
| Model resolution | which mesh an id wears | `EnemyResolver.TryResolveHollowModel` / `TryResolveDataModel` (`Assets/_Modules/Core/Enemies/EnemyResolver.cs:347-406`), then `EnemyFactory.ModelForEnemy` (`Assets/_Modules/Village/Enemies/EnemyFactory.cs:499-658`) |
| Animator resolution | which controller drives the mesh | `EnemyAnimatorFactory.RigFor` + `ControllerForModel` (`Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs:29-135`) |
| Body construction | the ONE place a hittable enemy body is built | `EnemyFactory.Build` (`Assets/_Modules/Village/Enemies/EnemyFactory.cs:32-390`) |
| VFX/audio set | which cue set an enemy uses | `EnemyTypeVfxLibrary.Resolve` (`Assets/_Modules/Village/Enemies/EnemyTypeVfxLibrary.cs:90-135`) |

### 0.1 The two `enemies.json` / `waves.json` copies are IDENTICAL

`Assets/Resources/Data/Canonical/{enemies,waves}.json` and
`Assets/StreamingAssets/Data/Canonical/{enemies,waves}.json` are **byte-identical** (verified by `diff`
2026-08-16). Unlike `weapons.json` — where the StreamingAssets copy is a 435-row library and the
Resources copy a 96-row curated subset (`docs/reference/WEAPON_CATALOG.md` §0.1) — there is **no
curation step** for enemies, so there is no "library-only, unreachable" tier here. Resources still wins
at load (`Assets/_Modules/Core/Data/CanonicalJson.cs:9-17`).

### 0.2 The single wave truth, stated once

`waves.json`'s per-wave `enemies[]` batch lists were **STRIPPED** on 2026-07-30 (WO-783 D1) because they
were inert: `_smartComposition` is serialized **`1`** in both live hubs
(`Assets/Scenes/Main_Castle_Overworld.unity:3153`, `Assets/Scenes/MainCastle_Hall.unity:1619`), so
`WaveManager.StartWave` runs `SpawnSmartComposedWave` first and **generates** every roster
(`Assets/_Modules/Village/Waves/WaveManager.cs:1546-1550`). From `waves.json`, only
**`countdownSeconds`, `expectedCombatSeconds`, `boss`, `bossHp` and `apexBoss`** still take effect
(`waves.json:14`; `WaveManager.cs:1704-1709`). **Never re-add an `enemies[]` array** — the
`[wave-authoring]` regression fails the gate on it (`Assets/Editor/Regression/WaveAuthoringLiveRegression.cs`,
registered at `Assets/Editor/Regression/DataRegression.cs:439`).

---

## 1A. THE CATALOG — identity, stats, rewards (19 rows)

Every value is the **authored base**, before `WaveScalingCurve` and before any spawner's own level scale.
`role` is the `enemies.json` string; **EnemyRole** is what it becomes at runtime via
`EnemyDef.RoleKind` (`Assets/_Modules/Village/Waves/WaveData.cs:212-225`: brute→Tank, caster→Healer,
skirmisher→Ranged, elite→MiniBoss, everything else→DPS).

| # | id | display name | family | role | EnemyRole | ai | HP | dmg | atk int. | speed | height | boss | xp | coin | var | citation |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `hollow-walker` | Hollow Walker | hollow | grunt | **DPS** | walker | 52 | 8 | 1.3 | 2.5 | 1.7 | – | 10 | 4 | 0.15 | `enemies.json:28-45` |
| 2 | `hollow-warrior` | Hollow Warrior | hollow | grunt | **DPS** | walker | 156 | 10 | 1.3 | 2.2 | 1.88 | – | 24 | 10 | 0.15 | `enemies.json:48-65` |
| 3 | `hollow-rogue` | Hollow **Skirmisher** | hollow | skirmisher | **Ranged** | skirmisher | 70 | 5 | 1.0 | 3.8 | 1.78 | – | 14 | 6 | 0.15 | `enemies.json:68-85` |
| 4 | `hollow-acolyte` | Hollow Acolyte | hollow | caster | **Healer** | walker | 90 | 4 | 1.4 | 2.2 | 1.8 | – | 18 | 8 | 0.15 | `enemies.json:88-105` |
| 5 | `hollow-mage` | Hollow **Caster** | hollow | caster | **Healer** | walker | 85 | 6 | 1.4 | 2.3 | 1.82 | – | 18 | 8 | 0.15 | `enemies.json:108-125` |
| 6 | `hollow-reaper` | Hollow Reaper | hollow | elite | **MiniBoss** | walker | 240 | 14 | 1.5 | 2.3 | 2.0 | – | 55 | 28 | 0.15 | `enemies.json:128-145` |
| 7 | `hollow-brute` | **Bone-Golem** | hollow | brute | **Tank** | charger | 900 | 24 | 1.8 | 1.6 | 3.0 | – | 120 | 60 | 0.15 | `enemies.json:148-165` |
| 8 | `cellar-hollow` | Cellar Hollow | hollow | grunt | **DPS** | walker | 40 | 4 | 1.6 | 1.4 | 1.7 | – | 8 | 3 | 0.15 | `enemies.json:168-185` |
| 9 | `hollow-apprentice` | The Apprentice of the Apothecary | hollow | elite | **MiniBoss** | walker | 440 | 20 | 1.3 | 2.0 | 2.0 | – | 150 | 80 | 0.15 | `enemies.json:188-205` |
| 10 | `necromancer` | **Necromancer of the Wound** | hollow | elite | **MiniBoss** | walker | **1700** | 17 | 1.3 | 1.5 | 2.7 | **✔** | 220 | 120 | 0.10 | `enemies.json:208-225` |
| 11 | `orc-berserker` | Orc Berserker | orc | brute | **Tank** | charger | 117 | 10 | 1.2 | 2.8 | 2.0 | – | 22 | 10 | 0.15 | `enemies.json:228-245` |
| 12 | `orc-shaman` | Orc Shaman | orc | caster | **Healer** | skirmisher | 78 | 3 | 1.5 | 2.4 | 1.9 | – | 16 | 7 | 0.15 | `enemies.json:248-265` |
| 13 | `orc-necromancer` | **Warband Deathspeaker** | orc | elite | **MiniBoss** | walker | 600 | 18 | 1.3 | 1.8 | 2.2 | – | 90 | 50 | 0.15 | `enemies.json:268-285` |
| 14 | `orc-raider` | Orc Raider | orc | skirmisher | **Ranged** | charger | 130 | 12 | 1.3 | 3.1 | 2.0 | – | 24 | 11 | 0.15 | `enemies.json:288-305` |
| 15 | `troll` | Cave Troll | troll | brute | **Tank** | charger | 320 | 14 | 1.8 | 1.8 | 2.6 | – | 46 | 24 | 0.15 | `enemies.json:308-325` |
| 16 | `troll-mage` | Troll Stonecaller | troll | caster | **Healer** | skirmisher | 210 | 6 | 1.6 | 2.0 | 2.4 | – | 38 | 18 | 0.15 | `enemies.json:328-345` |
| 17 | `troll-shaman` | Troll Bonesinger | troll | caster | **Healer** | skirmisher | 250 | 4 | 1.1 | 2.0 | 2.4 | – | 46 | 22 | 0.15 | `enemies.json:348-365` |
| 18 | `troll-overlord` | Troll Overlord | troll | elite | **MiniBoss** | charger | **1100** | 24 | 1.7 | 1.6 | 3.0 | **✔** | 180 | 100 | 0.10 | `enemies.json:368-385` |
| 19 | `ogre` | Ogre | troll | brute | **Tank** | charger | 280 | 12 | 1.6 | 2.0 | 2.4 | – | 42 | 22 | 0.15 | `enemies.json:388-405` |

**Not in this table, deliberately — the apex dragon.** `boss-dragon-syndrath` is **not an `enemies.json`
row**: it is the kinematic `Boss_Dragon` prefab driven by `DragonBoss`, spawned from the wave's
`apexBoss` block, and it does not path a NavMesh (`waves.json:13`;
`Assets/_Modules/Village/Waves/WaveManager.cs:2136-2198`). Default max HP **4200**
(`Assets/_Modules/Village/Enemies/DragonBoss.cs:170,173`), matching the `apexBoss.hp` the wave authors
(`waves.json:156-160`).

### 1A.1 Stats are modified after Configure — in this exact order

1. `Enemy.Configure` copies the def (`Enemy.cs:590-621`) — note the **global −5 % speed dial** applied to
   every enemy in the game at `Enemy.cs:604` (`def.MoveSpeed * 0.95f`), so no enemy ever moves at its
   authored speed.
2. `Enemy.ApplyWaveScaling(hp, speed, dmg)` (`Enemy.cs:758-777`) multiplies by the curve. HP and damage
   multipliers **below 1.0 are discarded** (`:760`, `:773`).
3. `bossHp` pin, if any, **replaces** the scaled HP (`WaveManager.cs:2411-2417`).

The curve is `WaveScalingCurve` — HP 1.0×@w1 → **2.5×@w20**, speed 1.0 → **1.4×**, damage 1.0 → **2.0×**,
all clamped past 20 (`Assets/_Modules/Village/Waves/WaveScalingCurve.cs:68-96`). **Neither live hub wires
a curve asset** (`_scalingCurve: {fileID: 0}`, `Main_Castle_Overworld.unity:3148`,
`MainCastle_Hall.unity:1614`), so `EnsureScalingCurve` builds the runtime default from those field
initializers (`WaveManager.cs:1836-1845`). The defaults above ARE the live curve.

---

## 1B. THE CATALOG — body, animator, VFX, audio

`avatar` is the FBX import `animationType` (2 = Generic, 3 = Humanoid). `VFX rung` is which rung of
`EnemyTypeVfxLibrary`'s three-rung fallback answered — see §7.

| id | model (Resources/Enemies/) | controller | avatar | death VFX | spawn VFX | aura VFX | VFX rung | citation |
|---|---|---|---|---|---|---|---|---|
| `hollow-walker` | `Skeleton_Minion` | `HumanoidEnemy` | **Generic** (`Skeleton_Minion.fbx.meta:111`) | `Death_Skeleton` | `Enemy_Spawn` | none | **2** | `EnemyResolver.cs:110-116`; `EnemyAnimatorFactory.cs:93` |
| `hollow-warrior` | `Skeleton_Warrior` | `SkeletonHumanoid` | Humanoid (`Skeleton_Warrior.fbx.meta:1059`) | `Death_Skeleton` | `Enemy_Spawn` | none | **2** | `EnemyResolver.cs:117-125`; `EnemyAnimatorFactory.cs:79-82` |
| `hollow-rogue` | `Skeleton_Rogue` | `SkeletonHumanoid` | Humanoid (`Skeleton_Rogue.fbx.meta:1069`) | `Death_Skeleton` | `Enemy_Spawn` | none | **2** | `EnemyResolver.cs:126-132` |
| `hollow-acolyte` | `Skeleton_Healer` | `SkeletonHumanoid` | Humanoid (`Skeleton_Healer.fbx.meta:1048`) | `Death_Skeleton` | `Enemy_Spawn` | **`Aura_EnemyCaster`** | **2** | `EnemyResolver.cs:133-139`; `Enemy.cs:3124` |
| `hollow-mage` | `Skeleton_Mage` | `SkeletonHumanoid` | Humanoid (`Skeleton_Mage.fbx.meta:1058`) | `Death_Skeleton` | `Enemy_Spawn` | **`Aura_EnemyCaster`** | **2** | `EnemyResolver.cs:140-146` |
| `hollow-reaper` | `Skeleton_Warrior` (variant `reaper`) | `SkeletonHumanoid` | Humanoid | `Elite_Death` | `Elite_Spawn` | **`Aura_SmokeReaper`** | **2** | `EnemyResolver.cs:147-153`; `Enemy.cs:3121` |
| `hollow-brute` | `Skeleton_Golem` | `LargeEnemy` | **Generic** (`Skeleton_Golem.fbx.meta:111`) | `Death_Brute` | `Enemy_Spawn` | none | **2** | `EnemyResolver.cs:154-160`; `EnemyAnimatorFactory.cs:33` |
| `cellar-hollow` | `Skeleton_Minion` (variant `cellar`) | `HumanoidEnemy` | **Generic** | `Death_Skeleton` | `Enemy_Spawn` | none | **2** | `EnemyResolver.cs:161-167` |
| `hollow-apprentice` | `Skeleton_Mage` (variant `apprentice`) | `SkeletonHumanoid` | Humanoid | `Elite_Death` | `Elite_Spawn` | none | **2** | `EnemyResolver.cs:176-182` |
| `necromancer` | `Necromancer` | **`Boss`** | **Generic** (`Necromancer.fbx.meta:111`) | `Death_Boss` | `Boss_Spawn` | **`Aura_Necromancer`** | **2** | `EnemyResolver.cs:169-175`; `EnemyAnimatorFactory.cs:59` |
| `orc-berserker` | `Orc_Berserker` | `OrcWarband` | Humanoid (`Orc_Berserker.fbx.meta:1037`) | `Death_Brute` | `Enemy_Spawn` | none | **2** | `EnemyFactory.cs:570`; `EnemyAnimatorFactory.cs:62-65` |
| `orc-shaman` | `Orc_Shaman` | `OrcWarband` | Humanoid (`Orc_Shaman.fbx.meta:572`) | **none → `VfxPool` burst** | `Enemy_Spawn` | **`Aura_EnemyCaster`** | **2** | `EnemyFactory.cs:571`; `Enemy.cs:3081-3084` |
| `orc-necromancer` | `Orc_Necromancer` | `OrcWarband` | Humanoid (`Orc_Necromancer.fbx.meta:597`) | `Elite_Death` | `Elite_Spawn` | **`Aura_Necromancer`** | **2** | `EnemyFactory.cs:572` |
| `orc-raider` | *(redirected — see §7.4)* | – | – | – | – | – | – | `EnemyFactory.cs:76-87` |
| `troll` | `Troll` | `LargeHumanoid` | Humanoid (`Troll.fbx.meta:101`) | `Death_Brute` | `Enemy_Spawn` | none | **2** | `EnemyFactory.cs:614`; `EnemyAnimatorFactory.cs:43-45` |
| `troll-mage` | `Troll_Mage` | `LargeHumanoid` | Humanoid (`Troll_Mage.fbx.meta:101`) | none → `VfxPool` | `Enemy_Spawn` | **`Aura_EnemyCaster`** | **2** | `EnemyFactory.cs:619`; `EnemyAnimatorFactory.cs:52-54` |
| `troll-shaman` | `Troll_Mage` *(shared, by design)* | `LargeHumanoid` | Humanoid | none → `VfxPool` | `Enemy_Spawn` | **`Aura_EnemyCaster`** | **2** | `EnemyFactory.cs:615-620` |
| `troll-overlord` | `Troll_Overlord` | `LargeHumanoid` | Humanoid (`Troll_Overlord.fbx.meta:101`) | `Death_Boss` | `Boss_Spawn` | none | **2** | `EnemyFactory.cs:621` |
| `ogre` | **`Orc_Shaman` (STAND-IN)** | `OrcWarband` | Humanoid | `Death_Brute` | `Enemy_Spawn` | none | **2** | `EnemyFactory.cs:622`; §7.3 |

**Death/spawn/aura rules, stated once (they are per-ROLE, not per-id):**
`SpeciesDeathVfx` — `boss:true`→`Death_Boss`; `role:"elite"`→`Elite_Death`; `role:"brute"`→`Death_Brute`;
`family:"hollow"`→`Death_Skeleton`; else `None` → `VfxPool.SpawnDeathBurst` (`Enemy.cs:3068-3085`,
consumed `:2762-2789`).
`SpawnVfxFor` — boss→`Boss_Spawn`, elite→`Elite_Spawn`, everything else→`Enemy_Spawn`
(`Assets/_Modules/Village/Enemies/EliteVFXController.cs:148-153`, driven from `Enemy.cs:734`).
`SpeciesAuraVfx` — id contains `necromancer`→`Aura_Necromancer`; id contains `reaper`→`Aura_SmokeReaper`;
`role=="caster"`→`Aura_EnemyCaster`; else `None` (`Enemy.cs:3113-3127`, attached every `Configure` at
`Enemy.cs:582`).

### 1B.1 Sounds — what a player ACTUALLY hears (identical for all 19 rows)

There is exactly one authored `EnemyTypeVfxSet` asset in the tree and **every one of its clip arrays is
empty** (`Assets/Resources/Enemies/EnemyVfxSet_Default.asset` — `_hitSounds: []`, `_deathSounds: []`,
`_attackSounds: []`, `_hitVfxPrefabs: []`, `_deathVfxPrefabs: []`, `_telegraphVFXPrefab: {fileID: 0}`).
So per-type audio resolves to `null` for every enemy, and what remains is:

| Event | What plays | Fallback? | Citation |
|---|---|---|---|
| **Melee attack swing** | **SILENT** | ✗ none — `PlayTypeSound(clip)` is called with no `fallback` argument | `Enemy.cs:1693`, `:1809`, `:1986`; `PlayTypeSound` `:989-1003` |
| **Ranged attack land** | **SILENT** | ✗ none, same call shape | `Enemy.cs:1986` |
| Taking a hit | `EnemyCombatAudio.PlayHit()` | ✔ guaranteed | `Enemy.cs:2359-2360` → `EnemyCombatAudio.cs:48-85` |
| Death | `EnemyCombatAudio.PlayDeath()` (A/B `EnemyDeath`/`EnemyDeath2` under `FeatureFlags.CombatFeel`) | ✔ guaranteed | `Enemy.cs:2800-2801` |
| Ranged cast wind-up | `EnemyCombatAudio.PlayCastCharge()` — **unconditional**, set-independent | n/a | `Enemy.cs:1927` |

**The attack swing being silent for every enemy in the game is a real gap, not a catalog artifact.**
Hit and death both name a `CombatSfxFallback`; attack does not.

### 1B.2 Ranged cast VFX — one set of keys for every caster

No enemy authors an override, so every ranged cast uses the `EnemyTypeVfxSet` defaults:
`Fire_Cast` → `PP_FireBall` (travelling loop) → `FireballImpact_Impact`, tinted fire-orange
`(1, 0.55, 0.15)` and then forced through `HostilePalette.EnforceOnTint`
(`EnemyTypeVfxSet.cs:95-115`; consumed `Enemy.cs:1959-1966`). Those three keys are owner-tagged in
`VfxManualPicks.json` and catalogued at `docs/reference/VFX_CATALOG.md:116,166`.

> **The 2026-08-16 fix that made this true:** the four `EnemyTypeVfxSet` field initializers used to read
> `Arcane_*` / violet while `Enemy.cs`'s own no-set fallback read the owner-tagged `Fire_*` / orange.
> Harmless only while NO enemy ever had a set — the moment `EnemyTypeVfxLibrary` began resolving one for
> every enemy, an un-authored set would have silently re-skinned every caster from fire to arcane, a
> creative substitution no owner tagged. They are now one value (`EnemyTypeVfxSet.cs:86-102`).

---

## 2. REACHABILITY — which enemies a player can actually meet

**6 of the 19 rows are DEAD CONTENT today, and a 7th never spawns as itself.** An id is "reachable" only
if some live (non-dev, non-flag-off) path names it. Tally: **12 reachable · 1 redirected · 6 dead.**

| id | Reachable? | By what path |
|---|---|---|
| `hollow-walker` | ✔ **wave** | weak/grunt slot every wave (`WaveCompositionBuilder.cs:131,217`); also FTUE (`TutorialWaveSpawner.cs:38`), dungeon `hollow-group` (`OutpostEnemyGroupSpawner.cs:297-299`), garrison `ruined_keep` (`garrison-recipes.json:25`) |
| `hollow-rogue` | ✔ **wave** | weak/skirmisher slot every wave (`WaveCompositionBuilder.cs:132,220`); dungeon groups; overworld hollow scatter (`OverworldEncounterSpawner.cs:118-119`) |
| `hollow-warrior` | ✔ **wave** | brute slot from wave 1 (`WaveCompositionBuilder.cs:133,334-336`); overworld scatter; dungeon + garrison groups |
| `hollow-acolyte` | ✔ **wave** | caster slot from wave 1 (`WaveCompositionBuilder.cs:134,341-342`); overworld scatter; `mage_enclave` raid (`scene-configs.json:200-209`) |
| `necromancer` | ✔ **wave boss + elite** | authored boss waves 6/12/18 (`waves.json:66,104,142`); elite cadence pool (`WaveCompositionBuilder.cs:345-346`); region roamers (`RegionSpawnTable.cs:97-125`) |
| `orc-berserker` | ✔ **wave (3+)** | brute pool from wave 3 (`WaveCompositionBuilder.cs:335-336`); garrisons + raid bases |
| `orc-shaman` | ✔ **wave (3+)** | caster pool from wave 3 (`WaveCompositionBuilder.cs:342`); garrisons + raid bases |
| `orc-necromancer` | ✔ **wave (6+) + raid boss** | elite cadence pool from wave 6 (`WaveCompositionBuilder.cs:346`); raid-base boss (`scene-configs.json:98,160`); dungeon `orc-group` |
| `troll` | ✔ **wave (5 boss, 6+ brute)** | authored wave-5 boss (`waves.json:57`); brute pool from wave 6 (`WaveCompositionBuilder.cs:336`); garrisons |
| `ogre` | ✔ **wave (6+)** | brute pool from wave 6 (`WaveCompositionBuilder.cs:336`); dungeon `troll-group` (`OutpostEnemyGroupSpawner.cs:311`); `fortified_garrison` (`scene-configs.json:146-147`) |
| `troll-mage` | ✔ dungeon only | `troll-group` table (`OutpostEnemyGroupSpawner.cs:311`), authored in `dg_ember_deep.json:306,334` |
| `troll-shaman` | ✔ dungeon only | same table + rooms |
| `orc-raider` | ⚠ **named but NEVER spawns as itself** | Every camp/outpost/tribe/ward spawner defaults to it, but it is on the **deferred Wildlands list** (`EnemyResolver.cs:291-295`) and `EnemyFactory.Build` redirects it to `hollow-warrior`/`hollow-walker` (`EnemyFactory.cs:76-87`, `EnemyResolver.cs:314-320`) |
| **`hollow-mage`** | ✘ **DEAD** | No spawner names it. Only `EnemyResolver.cs:140` + `EnemyResolverRegression`. The composition's caster slot uses `hollow-acolyte`, never the mage. |
| **`hollow-reaper`** | ✘ **DEAD** | Same — resolver + regression only. Its `Aura_SmokeReaper` therefore never plays in a live session. |
| **`hollow-brute`** | ✘ **DEAD** | Same. The 900 HP Bone-Golem is authored, modelled (`Skeleton_Golem`) and unreachable. |
| **`cellar-hollow`** | ✘ **DEAD** | `spawn:["dungeon"]` but no dungeon layout names it; `healers-cottage.json:301` has an *encounter-trigger* id `cellar-hollow-one` whose `enemyTypes` list `hollow_apprentice_minor` instead. |
| **`hollow-apprentice`** | ✘ **DEAD in the 3D stack** | `OverworldEncounterSpawner.cs:115` states it plainly: *"hollow-apprentice exists only in the ATB stack, NOT in EnemyFactory."* It has an ATB def (`BattleATB/Engine/Defs.cs:470`) and an ATB model map (`AtbCombatantSwapper.cs:585`). |
| **`troll-overlord`** | ✘ **DEAD** | Deliberately excluded from every group table — *"The Overlord is a camp BOSS and is deliberately NOT here"* (`OutpostEnemyGroupSpawner.cs:308-310`) — and no `boss:` field in `scene-configs.json` / `garrison-recipes.json` names it. **A 1100 HP `boss:true` enemy with a dedicated mesh that nothing fields.** |

### 2.1 Ids that exist in CODE but not in `enemies.json`

These are synthesised defs; they have no catalog row and no `modelKey`, so they resolve through the
`EnemyFactory` switch or the family/size fallback.

| id | Where it comes from | Model | Live? |
|---|---|---|---|
| `orc-warlord` | outpost raid boss (`EnemyOutpost.cs:865`), arena boss (`BattleArena.cs:171`), raid-base boss default (`RaidGarrisonSpawner.cs:282`) | `Orc_Warlord` (`EnemyFactory.cs:591`) | ✔ |
| `orc-warrior` / `orc-tank` / `orc-mage` | overworld encounter family packs (`OverworldEncounterSpawner.cs:42,116-120`) | `Orc_Warrior` / `Orc_Tank` / `Orc_Mage`, per-role override controllers (`EnemyAnimatorFactory.cs:126-131`) | ✔ (flag `FeatureFlags.OverworldEncounter`, default ON) |
| `caveman` / `feral-wolf` / `tiefling-cultist` | region roam / tribe / ward / camp tables (`RegionSpawnTable.cs:97-125`) | **redirected** — all three are deferred Wildlands ids (`EnemyResolver.cs:291-295`) | ⚠ never as themselves |
| `demon` | `EnemyFactory.cs:624` case only | `Demon` | ✘ no spawner |
| `blink-orc-warrior` | dev hotkey `B` compare (`EnemyFamilyTestSpawner.cs:129`), gated by `FeatureFlags.DevHotkeys` (default OFF, `FeatureFlags.cs:302`) | `Blink/Blink_Orc_Warrior` | dev only |
| **`blink-orc-hunter`**, `blink-orc-warlock`, `blink-orc-boss` | **nothing** — one `EnemyFactory` switch line each (`:601`, `:602`, `:603`) | committed Blink meshes | ✘ **not even the dev spawner emits these** |
| `grunt-0/1/2`, `tank`, `healer`, `scatter-elite-0..4` | dev hotkeys `J` / `K` (`EnemyFamilyTestSpawner.cs:227-297`, `:169-225`) | synthesised | dev only |

---

## 3. THE WAVE LADDER — what the player actually meets, wave by wave

### 3.1 The formula (this, not `waves.json`, is the roster authority)

```
total   = clamp(round(4 + 0.9*(wave-1)), 4, 22)                  WaveCompositionBuilder.cs:149-152,181-183
waves 1-2 : 100% weak
waves 3-5 : 60% weak / 40% medium
waves 6+  : strong = min(0.40, 0.12 + 0.03*(wave-6)); medium = 0.40; weak = 1 - medium - strong
                                                                 WaveCompositionBuilder.cs:186-201
weak    -> grunts  = round(weak * U[0.50,0.75))  -> hollow-walker   (SpawnRole.Weak,   EnemyRole.DPS)
           skirms  = weak - grunts               -> hollow-rogue    (SpawnRole.Melee,  EnemyRole.Ranged)
medium  -> brutes  = round(med  * U[0.50,0.70))  -> BrutePool  (SpawnRole.FrontTank, EnemyRole.Tank)
           casters = med - brutes                -> CasterPool (SpawnRole.Archer,    EnemyRole.Healer)
strong  -> BrutePool again                       (SpawnRole.FrontTank, EnemyRole.Tank)
elite   -> 1 from ElitePool on every 5th wave, UNLESS waves.json authors a heavy
                                                                 WaveCompositionBuilder.cs:207-268
BrutePool  : w<=2 {hollow-warrior} | w<=5 +{orc-berserker} | w>=6 +{troll, ogre}   :332-337
CasterPool : w<=2 {hollow-acolyte} | w>=3 +{orc-shaman}                            :339-343
ElitePool  : w<=5 {necromancer}    | w>=6 +{orc-necromancer}                       :345-346
```

The RNG is **seeded per wave** — `InitState(waveId*7919 + seedSalt*104729 + (waveId&1)*31)` — so a retry
of the same wave reads the same, and consecutive waves differ (`WaveCompositionBuilder.cs:178-179`).
`AddVaried` then spreads a tier's count round-robin across its pool, remainder to the earlier families
(`:360-373`), which is why a single wave mixes skeletons/orcs/trolls/ogres instead of being all-skeleton.

### 3.2 The ladder

`weak/med/strong` are **exact** (integer arithmetic on the formula, computed in IEEE-754 float to match
Unity's `Mathf.RoundToInt` half-to-even). The grunt/skirm and brute/caster splits depend on the seeded
`Random.Range`; where the jitter range collapses to one integer the value is exact, where it can land on
two the row shows both.

| wave | name | countdown | combat budget | **total** | weak (grunt / skirm) | medium (brute / caster) | strong | generated elite | **authored heavy** |
|---|---|---|---|---|---|---|---|---|---|
| 1 | First Light | 45 s | 40 s | **4** | 4 (2–3 / 1–2) | 0 | 0 | – | – |
| 2 | The Warband Comes | 300 s | 45 s | **5** | 5 (2–4 / 1–3) | 0 | 0 | – | – |
| 3 | The Deep Ones | 300 s | 50 s | **6** | 4 (2–3 / 1–2) | 2 (1 / 1) | 0 | – | – |
| 4 | The Green Tide | 300 s | 60 s | **7** | 4 (2–3 / 1–2) | 3 (2 / 1) | 0 | – | – |
| **5** | Stonebreakers | 300 s | 105 s | **8** | 5 (2–4 / 1–3) | 3 (2 / 1) | 0 | **DEFERRED** | **`troll` @ 1050 HP** (`waves.json:57-58`) |
| **6** | The Wound Speaks | 300 s | 125 s | **8** | 4 | 3 (2 / 1) | 1 | – | **`necromancer`** (`waves.json:66`) |
| 7 | The Deathspeaker's Levy | 300 s | 75 s | **9** | 4 | 4 (2–3 / 1–2) | 1 | – | – |
| 8 | The Warrens Empty | 300 s | 80 s | **10** | 4 | 4 (2–3 / 1–2) | 2 | – | – |
| 9 | March of the Forgotten | 300 s | 90 s | **11** | 4 | 4 (2–3 / 1–2) | 3 | – | – |
| **10** | Warband Ascendant | 300 s | 95 s | **12** | 4 | 5 (2–3 / 2–3) | 3 | **✔ 1 elite** | – |
| 11 | Oak and Stone | 300 s | 100 s | **13** | 4 | 5 (2–3 / 2–3) | 4 | – | – |
| **12** | The Second Dirge | 300 s | 160 s | **14** | 4 | 6 (3–4 / 2–3) | 4 | – | **`necromancer`** (`waves.json:104`) |
| 13 | The Red Banner | 300 s | 110 s | **15** | 4 | 6 (3–4 / 2–3) | 5 | – | – |
| 14 | Giants at the Gate | 300 s | 120 s | **16** | 4 | 6 (3–4 / 2–3) | 6 | – | – |
| **15** | The Hollowed Hundred | 300 s | 125 s | **17** | 4 | 7 (4–5 / 2–3) | 6 | **✔ 1 elite** | – |
| 16 | The Last Levy | 300 s | 130 s | **18** | 4 | 7 (4–5 / 2–3) | 7 | – | – |
| 17 | The Mountain Moves | 300 s | 135 s | **18** | 4 | 7 (4–5 / 2–3) | 7 | – | – |
| **18** | The Third Dirge | 300 s | 195 s | **19** | 4 | 8 (4–6 / 2–4) | 7 | – | **`necromancer`** (`waves.json:142`) |
| 19 | Eve of the Wing | 300 s | 150 s | **20** | 4 | 8 (4–6 / 2–4) | 8 | – | – |
| **20** | The Last Wing | 300 s | 285 s | **21** | 4 | 8 (4–6 / 2–4) | 9 | **DEFERRED** | **apexBoss `boss-dragon-syndrath` @ 4200 HP** (`waves.json:156-160`) |

**Wave 5 worked example** (the case established tonight): total 8 → weak 5 → **`hollow-walker` ×2–4 +
`hollow-rogue` ×1–3**; medium 3 → brutes 2 → **`hollow-warrior` ×1 + `orc-berserker` ×1**; casters 1 →
**`hollow-acolyte` ×1**; strong 0; elite cadence **deferred**; plus the authored **Cave Troll pinned to
exactly 1050 HP**. That is 8 composed bodies + 1 boss.

**Wave 20 worked example:** 21 composed ground bodies (4 weak + 8 medium + 9 strong, brutes drawn across
`hollow-warrior`/`orc-berserker`/`troll`/`ogre`) **plus** the dragon — and, since 2026-08-16, **no** extra
generated elite on top.

### 3.3 What modifies the ladder at runtime

- **Concurrency cap.** Both live hubs serialize `_maxSimultaneousEnemies: 8`
  (`Main_Castle_Overworld.unity:3177`, `MainCastle_Hall.unity:1643`). From wave 9 on, the composition
  exceeds it, so the surplus is **HELD as reinforcements and released as slots free** — the total roster
  and the clear condition are unchanged, only the arrival pacing (`WaveManager.cs:1909-1927`;
  `SmartEnemySpawner.cs:145-157`). Set it to 0 to restore the all-at-once release.
- **Gate rotation.** One gate per wave, `gateIndex = (waveId-1) % gateCount`, N→E→S→W
  (`SmartEnemySpawner.cs:329-355`). Markers are injected at runtime as `spawn-castle-<dir>-<i>` with
  GateIndex 0 N / 1 E / 2 S / 3 W (`CastleSpawnPointInjector.cs:80-86,156`) — both hubs serialize
  `_spawnPoints: []`.
- **Tactical positioning.** FrontTank +2.5 m toward the Heart, Melee 0, Archer −3.5 m, Weak −5 m, Elite
  dead-centre at +1.25 m; siblings fan ±2 m (`SmartEnemySpawner.cs:46-51,161-183`).
- **Endless (wave 21+).** The loop never completes: waves cycle authored defs **4..20 in order** and each
  waits for the player's DEFEND press. Counts multiply by `1 + 0.05*(trueWave-20)` capped at **3×**
  (`waves.json:17-21`; `WaveManager.cs:1286-1360`, `:1892-1902`), applied **on top of** the smart
  composition, which itself keeps ramping on the true wave number (so `total` sits at the 22 cap from
  wave 21 on before the endless multiplier). `WaveScalingCurve` is clamped past 20, so stats stop
  growing there — **only counts grow in endless.**

---

## 4. BOSSES & ELITES — and the 2026-08-16 single-authority fix

Two independent producers can put a "heavy" in a wave, and until 2026-08-16 **neither knew about the
other**:

1. **Authored** — `waves.json`'s `boss` (a ground `enemies.json` id, optionally pinned by `bossHp`) or
   `apexBoss` (the `DragonBoss` prefab).
2. **Generated** — `WaveCompositionBuilder`'s `%5` elite cadence.

They collided: wave 5 authored a 1050 HP Cave Troll **and** hit the cadence, so the owner fought two
heavies; wave 20 authored the dragon and got an elite on top of it
(`WaveCompositionBuilder.cs:247-268`).

**The fix:** `Build` now takes a **required, no-default** `waveHasAuthoredHeavy` parameter — a caller that
forgets to say who owns the wave must not compile (`WaveCompositionBuilder.cs:163-170`). `WaveManager`
supplies it from `WaveHasAuthoredHeavy(wave)` = `!string.IsNullOrEmpty(wave.Boss) || wave.IsApexBossWave`
(`WaveManager.cs:3272-3273`, called at `:1884`). When both fire, **the authored wave wins** and the
cadence logs a `FlowTrace.Step` naming the deferral (`WaveCompositionBuilder.cs:263-268`).

| wave | heavy | authority | HP the player meets |
|---|---|---|---|
| 5 | Cave Troll | **authored** (cadence deferred) | **1050 flat** — the pin is applied *after* `ApplyWaveScaling`, so `WaveScalingCurve` is bypassed for it (`WaveManager.cs:2408-2417`) |
| 6 | Necromancer of the Wound | authored (no cadence collision) | 1700 × HP curve @ w6 (no `bossHp`) |
| 10 | 1 elite, `necromancer` **or** `orc-necromancer` | **generated** | 1700 / 600 × HP curve @ w10 |
| 12 | Necromancer | authored | 1700 × curve @ w12 |
| 15 | 1 elite, `necromancer` **or** `orc-necromancer` | **generated** | × curve @ w15 |
| 18 | Necromancer | authored | 1700 × curve @ w18 |
| 20 | Syndrath the Devourer (dragon) | **authored apex** (cadence deferred) | **4200 flat** (`waves.json:158`; prefab default `DragonBoss.cs:170,173`) |

Ground bosses use the normal floating enemy bar, not the apex `BossHealthBar` (`waves.json:52`), and
enter from the **north** marker when one exists — resolved, with a loud `FlowTrace.Fail`/`Warn` when it
does not (`WaveSpawnResolver.cs:47,60-85`; `WaveManager.cs:1628-1653`). *This too was fixed on
2026-08-16: the boss used to pass a hardcoded `"spawn-0"` that **no producer emits**, so it fell through
to the first element of an unordered `FindObjectsByType` list and walked in from a random side every
session, announced only by a `Debug.LogWarning` the F8 harness never sees* (`WaveManager.cs:1621-1627`).

The apex dragon's prefab reference is null in both hubs (`_apexBossPrefab: {fileID: 0}`), so it loads via
the `Resources/Enemies/Boss_Dragon` fallback with a `FlowTrace.Warn` (`WaveManager.cs:2140-2160`).

---

## 5. ⚠ `CarriesBow` HAS NO SUBJECT — no bow-carrying enemy exists

**Confirmed at source.** The owner ruled (2026-08-06) that `EnemyRole.Ranged` no longer implies "carries a
bow": role drives combat, and a separate predicate alone decides the bow attach, so casters carry
nothing until staff art is chosen (`EnemyBrain.cs` doc block above `CarriesBow`).

```csharp
public static bool CarriesBow(string id)                       // EnemyBrain.cs:452-461
{
    string s = (id ?? "").ToLowerInvariant();
    if (s.Length == 0) return false;                            // fails CLOSED
    if (s.Contains("mage") || s.Contains("caster") || s.Contains("shaman")) return false;
    if (s.Contains("heal") || s.Contains("acolyte") || s.Contains("necro"))  return false;
    return s.Contains("archer") || s.Contains("ranger") || s.Contains("bow")
        || s.Contains("hunter") || s.Contains("scout");
}
```

**Matched against the full id universe:**

| Source of ids | Any match? |
|---|---|
| The 19 `enemies.json` rows | **none** — no id contains archer/ranger/bow/hunter/scout |
| Code-synthesised live ids (`orc-warlord`, `orc-warrior`, `orc-tank`, `orc-mage`, `caveman`, `feral-wolf`, `tiefling-cultist`, `demon`, `raider-*`, `ward-echo`) | **none** |
| ATB engine ids (`Defs.cs`) | **none** |
| Dev-only ids (`grunt-*`, `tank`, `healer`, `scatter-elite-*`) | **none** |
| Blink family | **`blink-orc-hunter` is the ONLY string in the entire tree that satisfies the predicate** |

And `blink-orc-hunter` appears in exactly **one line of the whole codebase** —
`EnemyFactory.cs:601`, a switch case mapping it to `Blink/Blink_Orc_Hunter`. **No spawner, no data file,
no dungeon layout and not even the dev-hotkey compare emits it** (the `B` hotkey stages
`blink-orc-warrior` vs `orc-warrior`, `EnemyFamilyTestSpawner.cs:129-130`).

> ### THE PLAIN STATEMENT THE OWNER ASKED FOR
> **No bow-carrying enemy currently exists in the game.** The bow-attach seam is live, correct and
> reachable (`EnemyBrain.cs:831-845` → `HeroBowAttachment`), and it has never fired, because the only id
> that would satisfy its gate is spawned by nothing. The owner's ruling — that a bow-carrying enemy must
> use the bow animation — is therefore **unviolated and untested**: the first enemy id containing
> `archer`/`ranger`/`bow`/`hunter`/`scout` that any spawner emits will be the first to exercise it.
>
> Two further consequences worth recording:
> * `RosterId` is cleared to `string.Empty` on pooled reset **specifically so `CarriesBow` fails closed**
>   — an archer body reused as a shaman would otherwise re-read "archer" and arm a bow on a caster,
>   undoing the ruling silently (`EnemyBrain.cs:1071-1078`).
> * The live wave path (`SmartEnemySpawner`) **never stamps `RosterId` at all** — it sets `brain.Role` and
>   applies role tactics (`SmartEnemySpawner.cs:269-283`) and nothing else — so every wave enemy reaches
>   the gate with an empty id and takes the fail-closed branch by construction.

---

## 6. ANIMATOR / AVATAR COMPATIBILITY

A Humanoid clip can only pose a rig through a valid Humanoid avatar; a Generic clip cannot pose a
Humanoid rig. Mixing them is the "sliding statue / T-pose" class of bug, and both directions are now
`FlowTrace.Fail`-guarded at spawn (`EnemyAnimatorFactory.cs:189-209`) plus a deferred 8-frame pose verify
(`EnemyPoseVerifier`, `:265-397`, skipped headless/off-screen at `:283-299`).

### 6.1 The controllers

| Controller | Clip source | Clips are | Cite |
|---|---|---|---|
| `HumanoidEnemy` | KayKit `Rig_Medium_*` | **Generic** | `Rig_Medium_General.fbx.meta:101` = `animationType: 2` |
| `Boss` | KayKit `Rig_Medium_*` | **Generic** | same source metas |
| `LargeEnemy` | KayKit `Rig_Large_*` | **Generic** | `Rig_Large_General.fbx.meta:101` = 2 |
| `LargeHumanoid` | Mixamo `Assets/Action/**` | **Humanoid** | `Assets/Action/Orc Idle.fbx.meta:876` = 3 |
| `SkeletonHumanoid` | Mixamo `Assets/Action/**` | **Humanoid** | `Shared_Hit_Reaction.fbx.meta:876` = 3 |
| `OrcWarband` | Mixamo `Assets/Action/**` | **Humanoid** | same |
| `OrcHumanoid` (+ `_Mage` / `_Tank` / `_Warrior` override controllers) | Mixamo `Assets/Action/**` | **Humanoid** | `OrcHumanoid.controller.meta` guid `1c04fe23…` wrapped by all three overrides |
| `Blink/BlinkOrc`, `Blink/BlinkOrcBoss` | Blink pack `Anim/` + `AnimBoss/` (22 clips each) | **Humanoid** | `Blink/Anim/Orc_Idle.fbx.meta:1124` = 3 |
| `Dragon` | `DragonAnimatorSetup` (apex only) | n/a — the dragon is not a NavMesh enemy | `DragonBoss.cs:397-409` |

### 6.2 The meshes

**Generic (`animationType: 2`) — 3 meshes, all legacy KayKit:**
`Skeleton_Minion` (`:111`), `Skeleton_Golem` (`:111`), `Necromancer` (`:111`).

**Humanoid (`animationType: 3`) — everything else:** `Skeleton_Warrior` (`:1059`), `Skeleton_Rogue`
(`:1069`), `Skeleton_Healer` (`:1048`), `Skeleton_Mage` (`:1058`), `Skeleton_Golem_NEW` (`:101`),
`Necromancer_NEW` (`:101`), `Orc_Berserker` (`:1037`), `Orc_Shaman` (`:572`), `Orc_Necromancer` (`:597`),
`Orc_Warrior` (`:1040`), `Orc_Tank` (`:1040`), `Orc_Mage` (`:101`), `Orc_Warlord` (`:101`), `Troll`
(`:101`), `Troll_Mage` (`:101`), `Troll_Overlord` (`:101`), `Demon` (`:976`), and all four
`Blink/*_Mesh` (`:933`–`:953`). All carry `avatarSetup: 1`.

### 6.3 The pairing, and what it means for the bow ruling

| Rig class | Meshes on it | Clips | Coherent? |
|---|---|---|---|
| `HumanoidEnemy` (Generic) | `Skeleton_Minion` (Generic) | Generic | ✔ |
| `Boss` (Generic) | `Necromancer` (Generic) | Generic | ✔ |
| `LargeEnemy` (Generic) | `Skeleton_Golem` (Generic) | Generic | ✔ |
| `SkeletonHumanoid` | Skeleton Warrior/Rogue/Healer/Mage, `Necromancer_NEW` | Humanoid | ✔ |
| `LargeHumanoid` | `Troll`, `Troll_Mage`, `Troll_Overlord`, `Demon`, `OgreMage`, `Skeleton_Golem_NEW` | Humanoid | ✔ |
| `OrcWarband` | `Orc_Berserker`, `Orc_Shaman`, `Orc_Necromancer` | Humanoid | ✔ |
| `OrcHumanoid` (+3 overrides) | `Orc_Warrior`, `Orc_Tank`, `Orc_Mage`, `Orc_Warlord` | Humanoid | ✔ |
| `Blink/BlinkOrc(Boss)` | the four Blink meshes | Humanoid | ✔ |

`EnemyRigControllerCoherenceRegression` fails the gate on a mismatch and asks the runtime authority
(`EnemyAnimatorFactory.ResolveControllerName`) rather than re-deriving the rule
(`EnemyAnimatorFactory.cs:231-240`).

**For the bow ruling specifically:** the three **Generic** controllers (`Boss`, `LargeEnemy`,
`HumanoidEnemy`) carry no archer candidate — none of `Skeleton_Minion`, `Skeleton_Golem`, `Necromancer`
maps from an id that satisfies `CarriesBow`. The only mesh that would (`Blink/Blink_Orc_Hunter`) is
**Humanoid** on the **Humanoid-clip** `Blink/BlinkOrc` controller, so a future bow-carrying enemy built on
it starts rig-coherent — but its 22-clip set is the Blink pack's own, and whether it contains a bow-draw
clip is **not determinable outside Unity** (see §10).

---

## 7. SILENT FALLBACKS — where the game uses a default rather than an authored value

### 7.1 The VFX/audio set: EVERY enemy lands on rung 2

`EnemyTypeVfxLibrary.Resolve` is a 3-rung, never-null chain (`EnemyTypeVfxLibrary.cs:90-135`):

| rung | Path | Live today? |
|---|---|---|
| 1 | `Resources/Enemies/VfxSets/EnemyVfxSet_<family>` | ✘ **The directory `Assets/Resources/Enemies/VfxSets/` does not exist.** No family can ever hit rung 1. |
| 2 | `Resources/Enemies/EnemyVfxSet_Default` | ✔ **This is where all four families (`hollow`, `orc`, `troll`, and the `hollow` default for a null def) land.** |
| 3 | a synthesized in-memory instance | only if rung 2's asset is deleted; logs a `FlowTrace.Warn` |

**So `VFX rung = 2` for all 19 rows in §1B, which means: nothing was authored for ANY family.** The
rung-2 asset's clip and prefab arrays are all empty (§1B.1), so every "authored" branch in `Enemy.cs`
takes its hardcoded fallback: no per-type hit VFX, no per-type death VFX, no attack sound, no telegraph
prefab. What survives is the timing (`_telegraphDuration: 0.5` on the asset, floored to 1.0 s for contact
and 1.2 s for ranged, `Enemy.cs:1596-1610`, `:1880-1886`) and the `Fire_*` ranged keys.

> **Why the library exists at all:** the per-prefab `_typeVfxSet` assignment **never landed**. The field
> appears in exactly one prefab in the tree (`Assets/Prefabs/Village/Generated/Enemy_HollowWalker.prefab:123`)
> and its value there is `{fileID: 0}` — null; and the only `EnemyTypeVfxSet` asset on disk is referenced
> by nothing but its own `.meta` (`EnemyTypeVfxLibrary.cs:6-20`). It could not have worked anyway: every
> live enemy is `AddComponent<Enemy>()` at runtime (`EnemyFactory.cs:362`), so there is no serialized
> field to carry a reference. An authored prefab reference still WINS if one is ever set — `Enemy` latches
> authorship in `Awake` (`Enemy.cs:845`) and `EnsureTypeVfxSet` returns early for it (`:952`).

### 7.2 `EliteVFXController` is attached to NOTHING

`Enemy.Die()`'s `GetComponent<EliteVFXController>()` has always returned null, so `OnEliteDeath`, its
aura light pulse and `DramaticSpawnRoutine` have **never run in the shipped game** — proven by grepping
every `.prefab`/`.unity`/`.asset` in the tree (`EliteVFXController.cs:104-110`, `:131-137`;
`Enemy.cs:706-708`). The tier RULES were therefore lifted to statics (`SpawnVfxFor`, `DeathVfxFor`,
`PlayDeathShake`) and `Enemy` drives them off its `enemies.json` stat block instead — which is why the
spawn/death VFX columns in §1B are real today.

### 7.3 Model stand-ins and rejected data keys

| id | Data asks for | Actually gets | Why |
|---|---|---|---|
| `ogre` | `modelKey: "OgreMage"` (`enemies.json:394`) | **`Orc_Shaman`** | No `OgreMage.fbx` exists. `EnemyResolver.CommittedModels` rejects the key **by name** in the trace, and `EnemyFactory.cs:622`'s documented stand-in is used (`EnemyResolver.cs:60-76,393-401`). The single declared art-pending key (`EnemyResolverRegression.cs:202`). |
| `troll-shaman` | `Troll_Mage` | `Troll_Mage` | Shared with `troll-mage` **on purpose** — one caster silhouette, differentiated by stats and role, not by model (`EnemyFactory.cs:615-620`) |
| `hollow-reaper` | `Skeleton_Warrior` | `Skeleton_Warrior` + variant `reaper` | shared base, distinct `ResolvedKey` (`EnemyResolver.cs:147-153`) |
| `cellar-hollow` / `hollow-apprentice` | `Skeleton_Minion` / `Skeleton_Mage` | same + variants `cellar` / `apprentice` | ditto |
| any id with no case and no known family | – | `Skeleton_Golem` if height ≥ 2.3 else `Skeleton_Minion` | the size default, always with a `FlowTrace.Warn` naming the family (`EnemyFactory.cs:648-657`) |

### 7.4 The Wildlands deferral — four ids that never spawn as themselves

`orc-raider`, `caveman`, `feral-wolf`, `tiefling-cultist` are on the deferred list
(`EnemyResolver.cs:291-295`) because the living Wildlands roster has no shippable art. Every spawner
funnels through `EnemyFactory.Build`, which asks `IsCombatApproved` and redirects to a ratified Hollow
substitute — `hollow-warrior` for a heavy/tall request, `hollow-walker` otherwise — with a
`FlowTrace.Warn` (`EnemyFactory.cs:76-87`; `EnemyResolver.cs:303-320`). This is a **one-edit-covers-all**
gate at the single chokepoint. It also means every camp guard, tribe raider, ward kindle mob and outpost
guard in the game is visibly a skeleton, whatever id the spawner asked for.

### 7.5 Material fallbacks — tint instead of texture

The Warband/Troll family's authored Tripo basecolors **never travelled from the authoring machine**
(`Orc_Berserker.json` records a machine-local export dir that resolves nowhere; the `.fbx.meta` texture
remaps dangle to guids that exist nowhere; a binary scan of the FBXs finds zero embedded images —
`EnemyFactory.cs:225-238`). So `ResolveBasecolor` returns null and a **solid tint** is bound instead:
troll `(0.38, 0.40, 0.34)`, ogre `(0.48, 0.47, 0.52)`, warlord/necromancer `(0.22, 0.20, 0.26)`, and
Warband grunts `HostilePalette.PlaceholderBodyTint` — an umber placeholder that replaced the old orc
green after F8 seq 2269, because a whole enemy body painted the colourblind-safe hue read as a defect
(`EnemyFactory.cs:264-280`). **Texture and tint are mutually exclusive**: the fixer multiplies tint over
texture, so binding both renders authored art green-multiplied (`EnemyFactory.cs:444-450`).

### 7.6 Other silent-default seams worth knowing

- **`WaveScalingCurve` asset**: unwired in both hubs → runtime default curve, with a trace (§1A.1).
- **`_apexBossPrefab`**: unwired → `Resources/Enemies/Boss_Dragon` fallback + `Warn` (§4).
- **`_enemyPrefab`**: both hubs point it at `Enemy_HollowWalker.prefab`
  (`Main_Castle_Overworld.unity:3146`) but it is only used when `ModelForEnemy` returns nothing —
  a valid def always routes through the factory (`WaveManager.cs:2362-2365`).
- **Tinted capsule**: if a model fails to load *or* loads but carries no enabled renderer with a mesh, the
  body is dropped and rebuilt as a tinted capsule rather than shipping an invisible-but-hittable enemy
  (`EnemyFactory.cs:166-170`, `:816-847`).

---

## 8. SECOND STAT AUTHORITIES — where `enemies.json` is NOT the source

The catalog is the authority for the **wave/roamer/camp 3D stack**. Three other stacks carry their own
numbers for the same ids, and a balance change to `enemies.json` does **not** reach them:

| Stack | Ids it re-states | Where | Note |
|---|---|---|---|
| **Overworld arena** | `hollow-rogue` 58/13, `hollow-acolyte` 50/8, `hollow-warrior` 84/15, generic `hollow` 55/9, plus the orc family and `orc-warlord` 520/34 | `Assets/_Modules/Village/Arena/BattleArena.cs:1596-1625` | The file says so itself: *"these are HARDCODED here (not read from enemies.json) — see follow-up ticket to make the arena read the canonical enemy catalog"* (`:1620-1622`) |
| **ATB engine** | `hollow-warrior`, `hollow-captain`, `hollow-king`, `hollow-apprentice`, `necromancer`, `orc-warrior`, `orc-tank`, `orc-mage` | `Assets/_Modules/BattleATB/Engine/Defs.cs:400-520` | Turn-based combatant defs; models mapped separately by `AtbCombatantSwapper.cs:566-593` |
| **Garrison / camp** | `troll`, `orc-*`, `hollow-*`, `necromancer`, the three deferred Wildlands ids | `Assets/_Modules/Village/World/Camps/GarrisonStatBlocks.cs:112-144` | Unknown ids fall back to a generic brute (`:140-142`) — never crashes, never invents a different real id |
| **Region roamers** | `feral-wolf`, `caveman`, `orc-raider`, `tiefling-cultist`, `necromancer` | `RegionMobSpawner.BuildRoamerDef` (`:558-606`) | Synthesised defs carry no `modelKey`, so they take the code-table path |

---

## 9. THE FULL SPAWNER LEDGER — every path that can put an enemy in the world

All of these route through `EnemyFactory.Build`, the single enemy-creation path.

| Spawner | Context | Ids it can emit | Gate |
|---|---|---|---|
| `WaveManager` + `SmartEnemySpawner` | `Main_Castle_Overworld` (+ legacy `MainCastle_Hall`) | the composition pools (§3.1) + authored `boss` + `apexBoss` | `_smartComposition: 1`; FTUE stand-down (`WaveManager.cs:777`) |
| `TutorialWaveSpawner` | FTUE scene-3 horn wave | **`hollow-walker` ×2**, weakened to 12 HP / 2 dmg (`:38,49-51,206-236`) | tutorial director |
| `OverworldEncounterSpawner` | `Main_Castle_Overworld` open world | `orc-warrior`/`orc-tank`/`orc-mage` (ring reps + near scatter), `hollow-warrior`/`hollow-rogue`/`hollow-acolyte` (mid/far scatter) (`:42,116-120`) | `FeatureFlags.OverworldEncounter` (**default ON**) |
| `RegionMobSpawner` | open-world roamers | `feral-wolf`, `caveman`, `orc-raider`, `tiefling-cultist`, `necromancer` (`RegionSpawnTable.cs:97-125`) | `FeatureFlags.RegionRoam` — **but self-suppresses whenever `OverworldEncounter` is ON** (`:150`), so it is a no-op by default |
| `TribeManager` | open-world tribes | same region roster + `raider-<tribeId>` | same self-suppression (`:123`) |
| `WardTetherService` | ward-stone relight trickle | same region roster + `ward-echo` | none found |
| `EnemyOutpost` | walk-to raid outpost / arena | boss **`orc-warlord`**; guards from the region roster | prior-clear PlayerPrefs |
| `CampGuards` / `CampDefenseWave` | claimable camps | region roster | camp state |
| `GarrisonController` | `Garrison_*.unity` | `garrison-recipes.json` rows: troll_outpost `troll,troll,orc-berserker`; ruined_keep `troll,hollow-warrior,hollow-walker`; hill_fort `orc-berserker,orc-shaman,troll`; frost_keep `hollow-warrior,hollow-rogue,hollow-acolyte`; village2_stronghold `orc-berserker,orc-shaman,orc-raider,troll,orc-necromancer` | `Activate()` / `activateOnStart` |
| `RaidGarrisonSpawner` | `RaidBase_*.unity` | `scene-configs.json`: raider_camp_small `orc-berserker×7, orc-shaman×2` + boss `orc-necromancer`; fortified_garrison `troll×4, ogre×2, orc-berserker×6, orc-shaman×3` + boss `orc-necromancer`; mage_enclave `hollow-acolyte×7, orc-shaman×5, hollow-warrior×7` + boss `necromancer` | scene config `IsEnemy` + a `garrison` block |
| `OutpostEnemyGroupSpawner` | composed dungeon rooms | `hollow-group`: walker/rogue/warrior/acolyte · `orc-group`: raider/berserker/shaman/necromancer · `troll-group`: troll/ogre/troll-mage/troll-shaman · `mixed` (unused by any layout) | `autoSpawnOnStart` |
| `ComposedAmbushDirector` | dungeon darkness ambush | delegates to the above (`hollow-group`, 1–2 bodies); with no spawner in scene it **logs "ambush roll wasted" and spawns nothing** (`:111-117`) | `Lantern.IsInDarkness` + encounter roll |
| `EnemyFamilyTestSpawner` | `Village2` only | `grunt-*`, `tank`, `healer`, `scatter-elite-*`, `blink-orc-warrior` vs `orc-warrior` | **`FeatureFlags.DevHotkeys` (default OFF)** + key `J`/`K`/`B` |

---

## 10. LIMITS — boundaries this pass could not cross

Stated as limits, not guessed around.

1. **Exact per-wave RNG splits.** `Random.Range` is seeded deterministically per wave
   (`WaveCompositionBuilder.cs:178-179`), so the splits ARE fixed values — but Unity's `InitState`
   seeding of its xorshift PRNG is not reproducible outside the engine. §3.2 therefore gives the tier
   totals **exactly** and the grunt/skirm + brute/caster splits as the **complete range the jitter can
   produce**. A headless run that logs the `WaveComposition wave=N slots:` trace line
   (`WaveCompositionBuilder.cs:285`) would collapse each range to its single value.
2. **Which clip a controller actually plays.** Controller→clip→`animationType` was fully resolved from
   serialized guids and `.fbx.meta` (§6), but **whether a given Animator instance ends up with a valid
   Avatar** is a runtime property (`anim.avatar.isValid`) that only Unity can answer. The code already
   answers it loudly at spawn (`EnemyAnimatorFactory.cs:189-209`) — read a capture, do not infer.
   Likewise, whether the Blink 22-clip set contains a **bow-draw** clip is not readable from metas.
3. **Whether an enemy's VFX prefab actually renders.** The catalog records which `VFXType` each row
   resolves to; whether that type has a live prefab behind it is `docs/reference/VFX_CATALOG.md`'s
   jurisdiction, and the magenta/empty cases are guarded at runtime, not at rest.
4. **Felt balance.** Every HP/damage number here is authored data times a documented multiplier chain. How
   a wave *plays* is a felt question and belongs to the owner's PO close (`CLAUDE.md` §13), not to this
   registry.
5. **`spawn` field is inert.** `EnemyDef.Spawn` (`wave`/`roam`/`camp`/`dungeon`/`world`) has **no
   consumer** — `MembersOf`'s `spawnContext` parameter has no caller that passes one
   (`WaveData.cs:80-91`). The `spawn` column in `enemies.json` is documentation, not behaviour; §2 is
   derived from the spawners, not from that field, and the two disagree (e.g. `cellar-hollow` declares
   `["dungeon"]` and no dungeon fields it).

---

## 11. HOW TO ADD AN ENEMY CORRECTLY

Each step cites the authority that will reject you if you skip it.

1. **Import the mesh** into `Assets/Resources/Enemies/<Name>.fbx`. Import it **Humanoid**
   (`animationType: 3`, `avatarSetup: 1`) unless you deliberately want a legacy Generic controller — every
   mesh added since 2026-07 is Humanoid (§6.2). Textures go to `Enemies/TripoTex/<Name>_basecolor`
   (preferred) or `Enemies/OrcTex/<Name>_basecolor` (`EnemyFactory.cs:484-492`).
2. **Register the mesh in the committed registry**: add `"<Name>"` to `EnemyResolver.CommittedModels`
   (`Assets/_Modules/Core/Enemies/EnemyResolver.cs:77-93`). Until you do, a data `modelKey` naming it is
   **rejected by name** and the code stand-in is used (`:393-401`), and
   `EnemyResolverRegression` check 11 `Resources.Load`s every name in that set, so a typo fails the gate.
3. **Route the rig**: add the model name to `EnemyAnimatorFactory.RigFor`
   (`Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs:29-95`) so it lands on a controller whose
   clips match its avatar type. A Humanoid mesh on `Boss`/`LargeEnemy`/`HumanoidEnemy` T-poses, and
   `EnemyRigControllerCoherenceRegression` fails the gate on exactly that mismatch (`:46-51`).
4. **If it is a `+X`-forward AccuRig/Tripo export**, add it to `EnemyFactory.AccuRigIntake` so it gets the
   −90° yaw and the runtime Tripo→URP material fixer (`EnemyFactory.cs:433-439`, applied `:144-156`).
   Skip this for KayKit `Skeleton_*`/`Boss` rigs — they already face `+Z` and must NOT be rotated.
5. **Author the stat row** in `Assets/Resources/Data/Canonical/enemies.json` — `id`, `name`,
   `displayName`, `family`, `role` (grunt/brute/skirmisher/caster/elite — this becomes the runtime
   `EnemyRole`, `WaveData.cs:212-225`), `modelKey`, `ai`, `hp`, `moveSpeed`, `contactDamage`,
   `attackInterval`, `height`, `boss`, `xpReward`, `coinReward`, `rewardVariance`. **`modelKey` is the
   first authority for every family** (`EnemyFactory.cs:518-538`). Height drives the agent capsule and the
   `VisualFactory.Fit` normalisation — put the intended size here, never in a code multiplier
   (`EnemyFactory.cs:96-102`).
   **Mirror the row into `Assets/StreamingAssets/Data/Canonical/enemies.json`** — the two copies are
   byte-identical today (§0.1) and should stay so.
6. **If it is a Hollow**, add it to `EnemyResolver.HollowTable` with a distinct `ModelKey`+`Variant` and
   to `_approvedHollowCombatIds`, or it will collapse to the size-default skeleton
   (`EnemyResolver.cs:107-240`). The distinctness oracle iterates that list.
7. **Make it REACHABLE — this is the step §2 exists to shame.** A row alone spawns nothing. Pick a path:
   * a wave → add the id to a pool in `WaveCompositionBuilder` (`:131-146`, `:332-346`), or author it as a
     wave `boss` in `waves.json` (optionally with `bossHp` to pin it flat);
   * a dungeon → add it to a family table in `OutpostEnemyGroupSpawner` (`:287-318`) and name that
     `kind` in a dungeon layout JSON;
   * a garrison / raid base → add it to `garrison-recipes.json` or `scene-configs.json` **and** teach
     `GarrisonStatBlocks.BuildTypedDef` (`:112-144`) about the id;
   * an overworld pack → add it to a pool in `OverworldEncounterSpawner` (`:42,116-120`) or to
     `spawn-areas.json`.
8. **Do NOT author a `boss` in `waves.json` on a `%5` wave without understanding §4** — the authored heavy
   wins and the cadence elite is deferred, by design.
9. **Presentation is derived, not authored per-enemy.** Death/spawn/aura VFX come from `boss`/`role`/
   `family` (§1B). If you want bespoke cues, author
   `Assets/Resources/Enemies/VfxSets/EnemyVfxSet_<family>.asset` — that is rung 1, which **no family uses
   today** (§7.1) — rather than trying to set `_typeVfxSet` on a prefab, which cannot work for a
   runtime-built body.
10. **If it carries a bow**, the id must contain `archer`/`ranger`/`bow`/`hunter`/`scout` and must not
    contain `mage`/`caster`/`shaman`/`heal`/`acolyte`/`necro` (`EnemyBrain.CarriesBow`, `:452-461`),
    **and** the spawner must stamp `brain.RosterId` — the live wave path does not, so it fails closed
    (§5). It would be the first such enemy in the project.
11. **Add loot** in `Assets/Resources/Data/Canonical/loot-tables.json` if it should drop anything — rows
    exist today for `hollow-walker`, `hollow-rogue`, `hollow-warrior`, `hollow-acolyte`, `necromancer`,
    `orc-berserker`, `orc-shaman`, `orc-necromancer`, `orc-warrior`, `orc-warlord` plus the generic
    `common-grunt` / `boss-hoard` / `dungeon-*` tables.
12. **Run the gates**: `COMPILE_GATE_OK`, then `DataRegression.RunAll` for `REGRESSION_OK <n>/<n> suites`
    — the enemy-relevant suites are `[enemy-resolver]` (`ENEMY_RESOLVER_OK`), `[wave-authoring]`,
    `EnemyRigControllerCoherenceRegression`, `EnemyRigColorRegression`, `EnemyRewardRegression`,
    `EnemyPoolResetRegression`, `WaveScalingRegression`, `WavesSchemaRegression`. Then `UI_CAPTURE_OK` and
    **open the PNGs** — a new enemy is a visual change, and compile-green never proves a body renders
    (project memory `headless-screenshot-verify-ui-before-build`).
13. **Update this catalog in the same commit** (`CLAUDE.md` §15). A new enemy that is not in the table
    above is exactly the drift this registry exists to prevent.
