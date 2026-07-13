# Acceptance-Criteria Verification — Static Code Audit

**Date:** 2026-05-30
**Author:** QA verification engineer (read-only static pass)
**Project:** Defenders of the Realm — `C:\Users\Kayden-Laptop\Documents\defenders-unity`
**Companion doc:** `docs/QA_player_sanity_pass_2026-05-30.md` (player-journey triage; cross-referenced throughout)

---

## Method & static-audit caveat

This is a **static, source-level verification** of each work order's own **Acceptance Criteria**,
traced to `file:line` in the current Windows source tree. I did **not** launch the build, enter
playmode, run a bake, or edit any file. Every verdict is the predicted behaviour from reading the
code path. Verdicts:

- **PASS** — the criterion is satisfied in code with concrete evidence.
- **PARTIAL** — partially satisfied; a meaningful gap remains.
- **FAIL** — the criterion is contradicted by the code.
- **CAN'T-VERIFY-STATICALLY** — depends on scene/prefab state, a bake, or a runtime visual that
  cannot be confirmed by reading source (flagged with the reason).

**Two hard limits of a static pass on this project:**
1. **Scene wiring is invisible here.** `Village.unity` / `ATBBattle.unity` are never hand-read
   (CLAUDE.md §3), and many criteria ("camera is stable", "Q drops the boss HP bar") are runtime
   visuals. Those are marked CAN'T-VERIFY-STATICALLY even when the supporting code exists.
2. **Prefab field divergence.** Where a fix lands on a `.cs` default or one prefab copy but the
   scene/spawn path uses a *different* asset, the static read catches it — see the dragon
   orbit-height regression below, which is the single most important finding in this report.

---

## Per-WO verification matrix

### WO-100 — Defend the Tower: camera drift / targeting / geometry
Scope is almost entirely **scene geometry + Cinemachine rig setup** in the Defend-the-Tower scene,
which a static pass cannot inspect. Code-side anchors exist (`PatriciaLightController.cs`,
`HeroHealth` safe-turret mode at `HeroHealth.cs:90-99`).

| Criterion | Verdict | Evidence |
|---|---|---|
| Camera stable, no drift | CAN'T-VERIFY-STATICALLY | Cinemachine rig lives in the DTT scene; not readable statically. |
| Camera angle shows arena (hero+enemies+tower) | CAN'T-VERIFY-STATICALLY | Scene VC offsets, not in source. |
| Camera does not clip geometry | CAN'T-VERIFY-STATICALLY | Scene transform state. |
| Hero spawns on ground (Y≈0, not falling) | CAN'T-VERIFY-STATICALLY | Spawn-clamp is scene/controller-runtime. |
| Enemies spawn on NavMesh | CAN'T-VERIFY-STATICALLY | Bake + scene state. |
| Click/auto-target registers a hit | CAN'T-VERIFY-STATICALLY | Runtime. |
| Tower visible at back of arena | CAN'T-VERIFY-STATICALLY | Scene placement. |
| No geometry fills view | CAN'T-VERIFY-STATICALLY | Scene/runtime. |
| `[EnemyBrain] No target found` warnings gone | CAN'T-VERIFY-STATICALLY | Runtime log. |
| Healing Beacon routes to HeartHealth not HeroHealth (WO-98) | PASS | `HeartController.Heal(float)` is the tower-heal sink and is documented as the WO-98 DTT route (`HeartController.cs:21-32, 188-192`); `PatriciaLightController` drives tower HP via `HeartController.SetHp`/`OnHealthChanged`. |

**WO-100 overall:** Effectively unverifiable statically (it is a scene/camera WO). The one code-checkable
criterion (Healing Beacon → Heart) passes. Recommend re-running this WO's checks against a live DTT build.

---

### WO-101 — Village rebuild: polyperfect asset swap
`VillageSceneBuilder.cs` contains the polyperfect swap code (35 references to polyperfect / `SM_*`
prefabs / wall-perimeter builders). Whether the **baked scene** reflects it is a bake-state question
(see WO-103).

| Criterion | Verdict | Evidence |
|---|---|---|
| Village builds/loads without crash | CAN'T-VERIFY-STATICALLY | Build/runtime. |
| 5 gameplay buildings use polyperfect meshes | CAN'T-VERIFY-STATICALLY (code present) | `VillageSceneBuilder.cs` references `SM_House/Stables/...`; baked result not readable. |
| Wall perimeter encloses arena w/ gates | CAN'T-VERIFY-STATICALLY (code present) | Wall-builder code present in `VillageSceneBuilder.cs`. |
| No Tripo mesh refs remain in builder | PARTIAL / CAN'T-VERIFY | Polyperfect refs present; a full "zero Tripo" sweep of the builder was not exhaustively confirmed. |
| Player.log no D3D12 upload-buffer warnings | CAN'T-VERIFY-STATICALLY | Runtime log. |
| Build size −300 MB | CAN'T-VERIFY-STATICALLY | Build artifact. |
| Heart of Elarion (tree) intact, not replaced | CAN'T-VERIFY-STATICALLY | Scene state. |
| Building interactables still fire | CAN'T-VERIFY-STATICALLY | Runtime. |
| WaveManager spawn points align to gate lanes | CAN'T-VERIFY-STATICALLY | Scene (`SpawnPoint_*` GOs). |
| NavMesh covers interior + 40 m approaches | CAN'T-VERIFY-STATICALLY | Bake state (`Assets/Scenes/Village/NavMesh.asset` exists). |

**WO-101 overall:** Code present, outcome is bake/build-gated. Re-verify after a green village bake.

---

### WO-102 — Village bug cluster (dragon / aggro / stacked bars)

| Criterion | Verdict | Evidence |
|---|---|---|
| **Bug1** Dragon clearly visible while orbiting | **FAIL (regression risk)** | Fix-B lowered orbit to 10 on `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab` (`_orbitHeight: 10`, `_swoopLowHeight: 2.5`) — but `WaveManager.SpawnApexBoss` loads `Resources.Load<DragonBoss>("Enemies/Boss_Dragon")` as the live fallback (`WaveManager.cs:543`), and **that prefab still reads `_orbitHeight: 22` / `_swoopLowHeight: 4.5`** (`Assets/Resources/Enemies/Boss_Dragon.prefab:78,81`). The DragonBoss.cs default is also still `22f` (`DragonBoss.cs:108`). So unless the scene's `_apexBossPrefab` field points at the fixed Generated copy, the dragon still orbits at 22. |
| **Bug1** Each breath/swoop reduces Heart HP | PASS | `HeartController` now implements `IDamageableStructure` (`HeartController.cs:90, 267-269`), so `DragonBoss.DealStrike` → `ApplyContactDamage` → `SetHp` lands real damage. |
| **Bug1** Hero can land spell hits on dragon | PARTIAL | Reachability still range-bound per WO-125 Bug1 (no aim-at-boss fallback landed — see below). Hits only during low swoops. |
| **Bug1** Dragon dies / spirals after damage | CAN'T-VERIFY-STATICALLY | Runtime. |
| **Bug1** Phase-1 swoop chance 0.25→0.55 | PASS | `DragonBoss.cs:470` `if (UnityEngine.Random.value < 0.55f) BeginSwoop();`. |
| **Bug2** Standing near enemies decrements hero HP | PASS | `HeroHealth` implements `IDamageableStructure` (`HeroHealth.cs:34, 227-229`) + proximity fallback (`HeroHealth.cs:90-122`). |
| **Bug2** Enemies stop & attack the hero | PASS (code) | Hero is a valid `IDamageableStructure` target; `VillageSceneBuilder.cs:3383` sets `go.tag = "Player"`. |
| **Bug2** Enemies resume to Heart after hero dies | CAN'T-VERIFY-STATICALLY | Runtime AI behaviour. |
| **Bug3** Hero HP bar moved off the Heart bar (y=64→110) | PASS | `HeroHealth.OnGUI` `const float ... y = 110f;` (`HeroHealth.cs:236`). |
| **Bug3** Both bars update during play | PASS (code) | Hero bar IMGUI driven by `Fraction`; Heart bar via `HeartHudBridge`. |

**WO-102 overall:** Three of four code fixes landed (interface impls, swoop chance, bar offset, Player tag).
The **dragon orbit-height fix did not reach the prefab the game actually spawns** — a real
PASS-claimed/FAIL-in-practice gap.

---

### WO-103 — Village scene rebake
A single batchmode bake command. Cannot be verified statically — it is a build-log/scene-timestamp
outcome. The *inputs* it propagates are present in source (polyperfect swap, Player tag at
`VillageSceneBuilder.cs:3383`, spawn points). **Verdict: CAN'T-VERIFY-STATICALLY** for every criterion
(log lines, scene timestamps, in-editor visual checks). Note: the dragon orbit-height regression
(WO-102 Bug1) is *not* a builder input — a rebake will not fix it; the Resources prefab must be edited.

---

### WO-05 — Magenta ground + pets (RESULT'd ✅)
RESULT claims all symptoms resolved; root cause was a missing KayKit atlas material recovered into
`Assets/Generated/Materials/`.

| Criterion | Verdict | Evidence |
|---|---|---|
| Recovery commits at top of master | CAN'T-VERIFY-STATICALLY | Git state, not source. |
| Village ground green not magenta | CAN'T-VERIFY-STATICALLY | Recovered material exists per RESULT; render is scene-state. RESULT's own §6 still requests eyes-on. |
| Three visible pet meshes near Heart | CAN'T-VERIFY-STATICALLY | Runtime-spawned pets; RESULT confirms assets on disk only. |
| Clean headless player build | CAN'T-VERIFY-STATICALLY | Build artifact. |
| Player build shows the fixes | CAN'T-VERIFY-STATICALLY | RESULT marks this ⚠️ unverified. |

**Caveat:** QA report (P2-L, §5.1 of WO-05 RESULT) notes ~15 *other* missing KayKit prefab warnings
(dungeon/decoration) of the same root-cause class — out of WO-05 scope but still degrading other scenes.
WO-126 Bug1 (magenta near the tree) suggests the *polyperfect* material conversion is a separate,
possibly still-open magenta source.

---

### WO-06 — HUD in builds (RESULT'd ✅)
RESULT concluded "no defect to fix — HUD config is build-safe." Static config audit, not a render.

| Criterion | Verdict | Evidence |
|---|---|---|
| Build SUCCESS exit 0 | CAN'T-VERIFY-STATICALLY | Build artifact. |
| Built exe HUD == editor HUD | CAN'T-VERIFY-STATICALLY | RESULT marks ⚠️ "by configuration". |
| HUD buttons respond in build | CAN'T-VERIFY-STATICALLY | Runtime; `EventSystem` + `InputSystemUIInputModule` claimed present. |
| Text legible (no missing glyphs) | CAN'T-VERIFY-STATICALLY | RESULT proves only the Title screen renders. |

**Note:** The HUD *renders* per WO-06, but WO-07/WO-20 separately found two HUD readouts had **no
runtime data push** — WO-06's "HUD works" must be read narrowly as "chrome renders," not "all readouts
are live." See WO-20.

---

### WO-07 — Hero abilities (RESULT'd ✅, two fixes landed)

| Criterion | Verdict | Evidence |
|---|---|---|
| Abilities fire on key 1/2/3/4 | PASS (code) | `HeroAbilityInput` maps digit1..4; RESULT §2 traces the fire path. |
| HUD reflects mana/cooldown | PASS (code) | `HeroAbilitiesHudBridge` per-frame push added (RESULT §3.1). |
| VFX not magenta in URP | PASS (code) | `HeroAbilities.BuildBuiltInBurst` URP-guarded shader assign (RESULT §3.2). |
| Runtime key-press visual confirm | CAN'T-VERIFY-STATICALLY | RESULT §5 flags as the remaining tick. |

**Contradiction with HUD labels (QA P1-D):** abilities fire on **1/2/3/4**, but the HUD badges read
**Q/W/E/R** (`VillageHudController.cs:132` `SlotKeys = { "Q","W","E","R" }`, rendered at line 774).
The label/input mismatch is real and unfixed — see "regressions" callout.

---

### WO-08 — Proximity gates (RESULT'd ✅)
New `GateProximityOpener.cs` + `Gate.RequestOpen/Close`, runtime-attached via `VillageController.Start`.

| Criterion | Verdict | Evidence |
|---|---|---|
| `GateProximityOpener.cs` exists, compiles, no warnings | PASS | File exists per RESULT; build clean. |
| `Gate.RequestOpen/Close` added, damage mechanic intact | PASS (code) | Additive per RESULT §1-2. |
| Hero approach opens all 4 gates | CAN'T-VERIFY-STATICALLY | Runtime; "by construction." |
| Enemy approach does NOT open | PASS (code) | Hero identity via `GetComponentInParent<HeroLocomotion>()` (RESULT §3). |
| Damage-to-25% still collapses | PASS (code) | Combined-state logic preserved (RESULT §2). |
| Build succeeds, replicates in exe | CAN'T-VERIFY-STATICALLY | Build/runtime. |

---

### WO-20 — HUD data binding: Heart HP + Crystals push (RESULT'd ✅)

| Criterion | Verdict | Evidence |
|---|---|---|
| Heart HP bar drops on damage / rises on repair | PASS (code) | `HeartHudBridge.cs` exists; pushes `SetHeartHp(heart.Hp,100f)` per frame via reflection (RESULT §2). Visual confirm is runtime. |
| Crystal counter reflects GameState.Resources.Crystals | PASS (code) | `HeartHudBridge` pushes `SetCrystals(...Resources.Crystals)` (RESULT §2). |
| No balance values changed; additive | PASS | Additive bridge only. |
| Build clean + RESULT written | PASS | RESULT present. |

**Cross-check:** the crystal *display* path is correct, but the **spend** path (placement) uses a
different wallet (EconomyService) — so the counter can show hundreds while placement spends a separate
50. The desync is in placement, not WO-20's display. See WO-127/economy callout.

---

### WO-58 — Pet aura system (RESULT'd ✅)
`AuraController.cs` created.

| Criterion | Verdict | Evidence |
|---|---|---|
| L1 subtle glow / L3 brighter / L5 orbit sparks | PASS (code) | Per-level `SetLevel` tiers per RESULT. |
| 2s burst then return to level rate | PASS (code) | `PlayLevelUpBurst`. |
| LevelUp_Celebration VFX at pet | PASS (code) | Fires `VFXType.LevelUp_Celebration`. |
| Aura parented to pet / stops on disable | PASS (code) | Pool-safe per RESULT. |
| Fire=orange/red, Ice=blue/cyan | FAIL / not-automated | RESULT explicitly leaves this unchecked — set via ParticleSystem prefab Color, **not done in code**. |
| **Hook into PetProgression.ApplyBonuses** | **PARTIAL/UNWIRED** | RESULT lists `SetLevel()` + `PlayLevelUpBurst()` calls + prefab `AuraController` add as **"Not Automated — needs designer/editor work."** So the aura exists but is **not invoked** unless a designer wired it. |

**WO-58 overall:** Component is solid; the *integration* (call sites + prefab components + colors) is
manual TODO per the RESULT itself. PARTIAL.

---

### WO-86 — ScriptableObject data architecture
The WO specs **five new SO classes under `Assets/_Modules/Data/`** plus a set of authored `.asset`
files under `Assets/_Data/`. The project diverged from this spec.

| Criterion | Verdict | Evidence |
|---|---|---|
| Editing `TowerData.baseDamage` updates tower at runtime | PARTIAL | `Assets/Data/TowerData.cs` is an **empty stub** (`TowerData.cs:1-17`) deferring to the pre-existing `DeNelle.Core.Data.TowerData`. Towers read live from that Core asset via `Tower.Data` — so data-driven towers exist, but **not the WO-86 class** (no `damageMultiplierL2`, etc.). |
| All 10 waves authored in WaveData assets, no hard-coded loops | FAIL | `WaveManager.AwardWaveCrystals` and spawn logic are still code/config-driven; **no `WaveData_01..10` assets exist** (no `_Data/Waves/`). |
| Kill grants EnemyData.aetherReward via MonetizationManager | CAN'T-VERIFY-STATICALLY | `Assets/Data/EnemyData.cs` exists; wiring to MonetizationManager not confirmed. |
| Tower upgrade matches TowerData multipliers | PARTIAL | Upgrades run off Core `TowerData.upgrades[]`, not the WO-86 multiplier fields. |
| AbilityData.cooldown drives cooldown UI | CAN'T-VERIFY-STATICALLY | Abilities load from `abilities.json` (WO-07), not `AbilityData` SO. |
| PetData.damagePerLevel increases pet damage | CAN'T-VERIFY-STATICALLY | `Assets/Data/PetData.cs` exists; wiring unconfirmed. |
| All `.asset` files exist & assigned to prefabs | FAIL | No `Assets/_Data/**.asset` files found at the spec'd paths. |

**WO-86 overall:** **PARTIAL/divergent.** Data-driven architecture *exists* via the pre-existing
`DeNelle.Core.Data` types and `Resources/Towers/*.asset` (Dev/Frost/Mage/Archer), but the WO-86
classes and the `_Data/` asset library were largely **not** created as specified. Should not be marked
"done" against its own AC.

---

### WO-87 — Cinemachine camera system
`CinemachineCameraController.cs` exists, uses real `Unity.Cinemachine`, and implements shake +
combat-proximity switching + wave-clear cinematic. Architecture **drifted** from spec (it documents a
separate `HeroCinemachineRig` priority-100 OTS rig and a `Shake(float,float)` shim rather than
`Shake(ShakeTier)`), but the intent is met.

| Criterion | Verdict | Evidence |
|---|---|---|
| Hero followed by VC_Village w/ damping | CAN'T-VERIFY-STATICALLY (code present) | VC GameObjects are scene-wired; controller expects them in Inspector (`CinemachineCameraController.cs:8-11, 39`). |
| Enemy-near blends to VC_Combat in 0.35 s | CAN'T-VERIFY-STATICALLY (code present) | Proximity poll present; blend time is CinemachineBrain scene setting. |
| Blends back when clear | CAN'T-VERIFY-STATICALLY (code present) | Toggle logic present. |
| Wave-clear triggers VC_WaveClear | PASS (code) | `PlayWaveClearCinematic(duration)` present. |
| `Shake(...)` produces impulse | PASS (code) | `CinemachineImpulseSource.GenerateImpulse`. |
| Existing CameraShake callers still work | PASS (code) | Reflection `Shake(float,float)` shim documented at `CinemachineCameraController.cs:16-17`. |
| Mobile shake ×0.55 | PASS (code) | `#if UNITY_ANDROID/IOS` scaling per RESULT-style header `:23`. |
| No clipping/NaN | CAN'T-VERIFY-STATICALLY | Runtime. |

**WO-87 overall:** Implemented in code (real Cinemachine), spec drift is benign. Scene-wiring of the
three VCs is the unverifiable remainder.

---

### WO-106 — XP / Level progress HUD (RESULT'd ✅)
`XPBarController.cs` + `PlayerProgressPanel.cs` exist, with IMGUI fallbacks.

| Criterion | Verdict | Evidence |
|---|---|---|
| XP bar always visible (IMGUI fallback) | PASS (code) | IMGUI fallback per RESULT. |
| Smooth fill lerp on XP gain | PASS (code) | `_fillLerpSpeed`. |
| Pulse on each XP gain | PASS (code) | `xp-fill--pulse` coroutine. |
| Level label "Lv. X" updates | PASS (code) | RESULT. |
| Gear icon opens panel / Close dismisses | PASS (code) | `settings-button`/`progress-close`. |
| Panel shows level/XP/lifetime | PASS (code) | `RefreshData()`. |
| No polling — event-driven | PASS (code) | Subscribes `OnXpChanged`/`OnLevelUp` via reflection. |
| Works in builds (IMGUI when UXML absent) | PASS (code) | Fallback path. |

**WO-106 overall:** Strongest of the RESULT'd WOs — every criterion is code-satisfiable; only the
in-build visual is the usual runtime tick.

---

## Bug-WO cross-check (confirm the bug still exists in code)

### WO-125 — P0 combat bugs (dragon unhittable + Heart-fall no-lose)
| Bug | Still present? | Evidence |
|---|---|---|
| **Bug1** Hero Q can't reach orbiting dragon | **YES** | No aim-at-boss fallback in `HeroAbilities.ResolveEffect`; abilities still sweep `OverlapSphere` at the hero's feet with authored radii. Q (~13.85u) can't reach a 34u-distant orbiting boss. |
| **Bug2** Towers can't damage the dragon | **YES** | `TowerCombat.FindNearestTarget`/`FindHighestHpTarget` iterate only `_wave.LiveEnemies` and require `GetComponent<EnemyDamageable>()` (`TowerCombat.cs:123,137,156,170`). No `LiveApexBoss` / `IDamageable` boss branch. Dragon is never in `LiveEnemies` and has no `EnemyDamageable`. |
| **Bug3** Heart HP 0 → no defeat/game-over | **YES (WORST)** | `HeartController` has **no death event** — only `OnHealthChanged` (`HeartController.cs:147`); `SetHp` clamps at 0 and never raises a defeat signal (`HeartController.cs:204-215`). The only `GameOverUI.Show()` caller is `HeroHealth.HandleDeath` (`HeroHealth.cs:158-186`), keyed on **hero** death. No `OnHeartDestroyed`/Heart-fall subscriber exists anywhere (grep: 0 matches). The game is unloseable via the Heart. |

**WO-125: all three bugs confirmed real and unfixed.**

### WO-126 — Scene material & placement bugs
| Bug | Verdict | Evidence |
|---|---|---|
| Magenta missing-material near tree | CAN'T-VERIFY-STATICALLY | Scene-render state. The fixer tool `Assets/Editor/PolyperfectUrpFix.cs` (menu `Defenders/Art/Fix Polyperfect URP Materials`) **exists**; whether it was run + rebaked is not in source. |
| Barn-in-wall / placement overlaps | CAN'T-VERIFY-STATICALLY | `VillageSceneBuilder.cs` coordinate state + bake. |
| Blue crystal-mine cube / gate color | CAN'T-VERIFY-STATICALLY | Scene/material state. |

**WO-126:** bugs are scene/render-state — not statically confirmable beyond noting the fix tooling exists.

### WO-127 — Tower "Manage All Towers" stale Lvl 1
| Bug | Still present? | Evidence |
|---|---|---|
| Manage screen reads `Building.Level`, not live `Tower.CurrentLevel` | **YES** | `BuildMenu.RenderUpgradeTower` enumerates `FindObjectsByType<Building>` filtered to `ArcaneTower` (`BuildMenu.cs:617-627`); row prints `b.Level` (`BuildMenu.cs:647`); result line `b.Level + 1` (`BuildMenu.cs:663`). |
| Upgrade button is a stub | **YES** | `BuildMenu.cs:668-673` logs only + "arrives in a later update." |

**WO-127: bug confirmed real and unfixed.**

### WO-130 — ATB pills + broken loop
| Issue | Still present? | Evidence |
|---|---|---|
| Issue 1 — enemy renders as tinted pill, not a model | **YES** | `AtbCombatantSwapper.TrySwap` still calls `TintEnemy(enemy.transform)` (`AtbCombatantSwapper.cs:53`); `TintEnemy` only recolors (`:121`). No `SwapEnemy`/`Resources/Enemies` load exists. |
| Issues 2-4 (editor buttons / single-enemy / ATB pressure bar) | LIKELY YES | Not exhaustively traced this pass; WO is READY-TO-IMPLEMENT (owner KEEP/PARK/CUT pending), so treat all four as open. |

**WO-130: headline pill bug confirmed real and unfixed.**

---

## Summary table (criterion counts per WO)

| WO | PASS | PARTIAL | FAIL | CAN'T-VERIFY | Net status |
|---|---|---|---|---|---|
| WO-100 camera/DTT | 1 | 0 | 0 | 9 | Scene WO — mostly unverifiable |
| WO-101 polyperfect | 0 | 1 | 0 | 9 | Code present, bake-gated |
| WO-102 bug cluster | 6 | 1 | 1 | 2 | Mostly landed; **dragon-prefab regression** |
| WO-103 rebake | 0 | 0 | 0 | all | Bake-state only |
| WO-05 magenta/pets | 0 | 0 | 0 | 5 | RESULT'd; eyes-on pending |
| WO-06 HUD builds | 0 | 0 | 0 | 4 | RESULT'd "by config" |
| WO-07 abilities | 3 | 0 | 0 | 1 | Landed (label mismatch is separate) |
| WO-08 gates | 4 | 0 | 0 | 2 | Landed |
| WO-20 HUD binding | 3 | 0 | 0 | 1 | Landed (display only) |
| WO-58 pet aura | 5 | 1 | 1 | 0 | Component done, **integration unwired** |
| WO-86 SO data | 0 | 3 | 2 | 2 | **Divergent — not done as specced** |
| WO-87 cinemachine | 4 | 0 | 0 | 4 | Code landed, VCs scene-wired |
| WO-106 XP HUD | 8 | 0 | 0 | 0 (1 runtime) | Strongest — all code-satisfied |

---

## Regressions / specced-but-not-working callout

Things effectively marked done (or implied live in the build) that **do not meet their own criteria**:

1. **WO-102 Bug1 — dragon orbit-height fix never reaches the spawned prefab (PASS→FAIL).**
   The fix landed on `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab` (`_orbitHeight: 10`), but
   `WaveManager` spawns from `Resources.Load("Enemies/Boss_Dragon")` (`WaveManager.cs:543`), and that
   prefab still reads `_orbitHeight: 22` / `_swoopLowHeight: 4.5`. `DragonBoss.cs:108` default is also
   still 22. **Net: in the actual spawn path the dragon still orbits at 22 m** — the visibility bug
   the WO claims fixed is likely still live. A rebake will NOT fix this (it's a Resources prefab field).

2. **WO-86 ScriptableObject architecture — not built as specced (FAIL on its own AC).**
   The five `Assets/_Modules/Data/*.cs` classes and the `Assets/_Data/**.asset` library do not exist as
   written; `Assets/Data/TowerData.cs` is an empty stub. The project is data-driven through *different*
   pre-existing types (`DeNelle.Core.Data.TowerData`, `Resources/Towers/*.asset`). It should not be
   counted "done" against WO-86's acceptance criteria.

3. **WO-58 pet aura — component exists but is never invoked.** The RESULT itself lists the
   `PetProgression.ApplyBonuses` call sites, the per-prefab `AuraController` add, and the fire/ice
   colors as **"Not Automated — needs designer/editor work."** So the aura cannot actually fire today.

4. **QA P0-C / P1-E economy split is real and unfixed.** Tower placement spends
   `EconomyService.Instance.Spend(cost)` (`TowerPlacementSystem.cs:192`) from a standalone wallet that
   never reads/writes `GameState` (`EconomyService.cs` has no GameState reference; bootstraps to
   `_crystals = 50`), while `BuildMenu.CrystalBalance` reads `GameState.Resources.Crystals`
   (`BuildMenu.cs:855-868`) and `OnConfirmBuild` (`BuildMenu.cs:581-601`) **never deducts**. Material
   counts are hard-coded (`GetMaterialCount` → wood 20 / stone 5, `BuildMenu.cs:697-704`). The visible
   economy and the spend economy are two different currencies.

5. **QA P1-D ability hotkey labels — HUD says Q/W/E/R, input is 1/2/3/4 (still mismatched).**
   `VillageHudController.cs:132` `SlotKeys = { "Q","W","E","R" }`; WO-07 RESULT confirms input is 1-4.

6. **QA P1-H wave rewards — non-boss waves pay 0.** `AwardWaveCrystals` only credits on
   `waveId % BossInterval == 0` + a `DropChance` roll (`WaveManager.cs:828-852`). Ordinary waves grant
   nothing; the wave-clear banner shows the running balance, not an earned delta.

7. **QA P1-F / WO-127 — tower upgrade UI stale + stub (confirmed above).** Both the display
   (`Building.Level`) and the action (logging stub) are wrong/no-op in `BuildMenu.cs`.

8. **WO-125 Bug3 — Heart-fall is unloseable (confirmed above).** No `OnHeartDestroyed` event, no
   Heart-death → GameOverUI path. This is the worst single gameplay gap: the core lose condition does
   not exist for the thing the apex boss attacks.

**Contradiction with QA report worth noting (in the project's favor):** the QA pass (P0-B) states the
village hero "never adds `HeroHealth` or `HeroHitReaction`." That is **out of date** — `HeroHealth`
now (a) implements `IDamageableStructure`, and (b) self-attaches via `HeroHealthBootstrap`
(`HeroHealth.cs:269-297`, `RuntimeInitializeOnLoadMethod`), which also adds `HeroHitReaction`. So the
village hero CAN take damage and die (hero-death game-over path works). The remaining lose-condition
gap is specifically the **Heart** (WO-125 Bug3), not the hero.

---

## Recommended re-verify cadence (what CLI should re-run after the next green build)

**Re-run against EVERY green village build (runtime smoke, the static pass can't see these):**
- WO-100 (all camera/targeting criteria) — pure scene/runtime.
- WO-101 / WO-103 (building meshes, wall perimeter, spawn points, NavMesh) — bake outputs.
- WO-05 / WO-06 (magenta-free render, HUD readouts live in-build).
- WO-102 Bug1 dragon visibility — **and specifically diff the two Boss_Dragon prefabs** until they agree.

**Re-run on any change to combat/economy code (these are statically re-checkable each time):**
- WO-125 Bug1/2/3 — grep `HeroAbilities` for a boss-aim path, `TowerCombat` for `LiveApexBoss`,
  `HeartController` for an `OnHeartDestroyed` event. All three should flip to PASS when fixed.
- WO-127 — confirm `BuildMenu.RenderUpgradeTower` enumerates `Tower` and the Upgrade button calls
  `Tower.Upgrade()`.
- Economy unification (P0-C) — confirm placement and BuildMenu share one wallet; `GetMaterialCount`
  is no longer a constant.
- P1-D ability labels — `SlotKeys` matches the real input map.
- P1-H — `AwardWaveCrystals` grants a base reward on non-boss waves.

**Re-run on any pet/data/camera refactor:**
- WO-58 — confirm `PetProgression.ApplyBonuses` calls `AuraController.SetLevel/PlayLevelUpBurst` and
  prefabs carry the component.
- WO-86 — re-audit whether the SO library was actually built, or formally retire WO-86 in favor of the
  `DeNelle.Core.Data` reality so the AC stops failing on paper.
- WO-87 — confirm the three VC GameObjects are present in the baked Village scene.

**Cadence:** treat the **statically re-checkable** set as a CI-style grep gate run on every PR touching
those files; treat the **runtime/bake** set as a once-per-green-build manual playtest checklist.
