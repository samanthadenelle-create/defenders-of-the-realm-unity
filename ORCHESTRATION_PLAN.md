# Defenders of the Realm — Implementation Orchestration Plan

> ⚠️ **HISTORICAL (superseded).** This is the 2026-05-28 VFX-sprint map, kept for history.
> For current state + plan + handoff rules, read **`SESSION_START_HERE.md`** first.

**Last updated:** 2026-05-28
**Philosophy:** Additive architecture, not rewrites. Every sprint delivers
shippable, playable progress. Mobile-first performance at each step.

---

## Core Principle: No Forced Rewrites

Each WO is categorised by its implementation type:

| Type | Meaning | Example |
|---|---|---|
| **CREATE** | New file, no existing code touched | `TowerVFXController.cs` |
| **ADDITIVE** | New component added alongside existing one | `EnemyHitReaction.cs` on enemy prefab |
| **TARGETED EDIT** | Change 5–15 lines in an existing file | Add `_hitReaction?.React()` to `EnemyHealth.TakeDamage` |
| **REPLACE** | Existing file replaced with canonical version | `EnemyBrain.cs` (WO-69 supersedes WO-49) |
| **PREFAB WIRE** | No code — Inspector-only assignment/addition | Assign `TowerData` asset to tower prefab |
| **SCENE EDIT** | No code — Unity Editor scene changes | Terrain, lights, colliders, VCs |

The CLI should read the type before opening any file. **REPLACE** is the only
type that requires deleting old code. Everything else builds on what exists.

---

## Sprint Map

### SPRINT 1 — Combat Foundation (Phase 1, ~2–3 hours CLI time)

**Goal:** Combat compiles and runs correctly with full damage pipeline.
No gameplay feel work yet — just correctness.

| Order | WO | Type | Scope |
|---|---|---|---|
| 1 | WO-69 | REPLACE | `EnemyBrain.cs` — canonical final version |
| 2 | WO-70 | CREATE + TARGETED EDIT | `HeroHealth.cs`, `EnemyHealth.cs`, edit `PetCombatController` |
| 3 | WO-68 | CREATE | `ATBCombatManager.cs` |
| 4 | WO-50 | CREATE | `VFXManager.cs`, `VFXAutoReturn.cs`, `VFXCatalog.cs` |

**Exit criteria:** Enemies chase and attack hero. Hero takes damage and can die.
ATB bar fills. EnemyHealth is the single damage entry point.

---

### SPRINT 2 — Combat Feel (Phase 1, ~3–4 hours CLI time)

**Goal:** Combat feels satisfying. Weight, hit reactions, ability feedback.

| Order | WO | Type | Scope |
|---|---|---|---|
| 5 | WO-81 | TARGETED EDIT + CREATE | Update `HeroLocomotion`, create `HeroCombatController`, `HeroHitReaction`, `AbilityCooldownUI` |
| 6 | WO-61 | CREATE | `CameraShakeManager`, `HitStopManager`, `DecalSpawner` |
| 7 | WO-84 | ADDITIVE + TARGETED EDIT | `EnemyHitReaction.cs`, edit `EnemyHealth.Die()` |
| 8 | WO-56 | TARGETED EDIT | Wire VFXManager into all hero ability scripts |

**Exit criteria:** Hero attacks feel weighty. Enemies flash red on hit, explode
on death with scorch marks. Camera shakes on impactful hits.

---

### SPRINT 3 — Towers & Waves (Phase 2, ~2–3 hours CLI time)

**Goal:** Towers feel powerful. Wave clears feel rewarding.

| Order | WO | Type | Scope |
|---|---|---|---|
| 9  | WO-82 | CREATE + TARGETED EDIT | `TowerVFXController.cs`, wire into `TowerCombat` + `TowerProjectile` |
| 10 | WO-60 | CREATE + TARGETED EDIT | `KillComboTracker.cs`, wire into `WaveManager` |
| 11 | WO-83 | CREATE + TARGETED EDIT | `WaveCelebrationManager.cs`, extend `KillComboTracker` with Tier3 |
| 12 | WO-55 | CREATE | `TorchFireController.cs` |
| 13 | WO-65 | CREATE | `PortalVFXController.cs` |
| 14 | WO-66 | CREATE | `EliteVFXController.cs` |

**Exit criteria:** Tower shots spark and boom. Wave clear triggers celebration.
Kill combos escalate. Elite enemies have dramatic deaths.

---

### SPRINT 4 — World & Immersion (Phase 3, ~3–4 hours CLI time)

**Goal:** World looks and feels alive. No more flat teal test map.

| Order | WO | Type | Scope |
|---|---|---|---|
| 15 | WO-71 | SCENE EDIT | Terrain, lighting, fog, NavMesh, boundaries, foliage |
| 16 | WO-85 | SCENE EDIT + TARGETED EDIT | Extended terrain polish, wave-reactive weather, auditor tool |
| 17 | WO-52 | CREATE + TARGETED EDIT | `WeatherManager.cs`, wire to `WaveManager` |
| 18 | WO-63 | CREATE | `LevelUpVFXController.cs`, `LevelUpEvents.cs` |
| 19 | WO-58 | CREATE | `AuraController.cs` (pet aura) |
| 20 | WO-59 | TARGETED EDIT | `VFXManager.ApplyDungeonMode()`, dungeon VFX swap |

**Exit criteria:** Village looks like a real place. Hills at edges. Trees in clusters.
Torches flickering. Weather reacts to big waves. Hero/pet level-ups feel great.

---

### SPRINT 5 — Performance & Quality (Phase 3 + 4, ~2 hours CLI time)

**Goal:** Locked 60 FPS on mobile. Architecture cleaned up.

| Order | WO | Type | Scope |
|---|---|---|---|
| 21 | WO-53 | CREATE + PREFAB WIRE | `AnimatorCullingController.cs`, apply to all animated prefabs |
| 22 | WO-54 | PREFAB WIRE | LOD Groups on all tree/rock/building prefabs |
| 23 | WO-51 | CREATE | `MobilePerformanceSettings.cs`, `PerformanceManager.cs` |
| 24 | WO-57 | CREATE + SCENE EDIT | `MobileQualitySettings.cs`, `QualityToggleUI.cs` |
| 25 | WO-64 | CREATE | `GameQualityController.cs`, `QualityDebugMenu.cs` |

**Exit criteria:** AnimatorCullingAuditor (WO-85) reports 0 missing.
LOD Groups reduce overdraw at distance. `PerformanceManager` auto-selects tier.
60 FPS on a mid-range Android device.

---

### SPRINT 6 — Data Architecture (Phase 4 early, ~3 hours CLI time)

**Goal:** All game stats live in ScriptableObjects. No magic numbers in code.

| Order | WO | Type | Scope |
|---|---|---|---|
| 26 | WO-86 | CREATE + TARGETED EDIT | `TowerData`, `EnemyData`, `AbilityData`, `WaveData`, `PetData` SOs + wire into existing scripts |
| 27 | WO-87 | CREATE + TARGETED EDIT | Cinemachine install, three VCs, `CinemachineCameraController`, update `CameraShakeManager` wrapper |

**Exit criteria:** All 10 waves authored as `WaveData` assets. Tower balancing
in Inspector only. Cinemachine handles all camera follow and shake.

---

### SPRINT 7 — Retention & Polish (Phase 4, ~3 hours CLI time)

**Goal:** Players want to come back tomorrow.

| Order | WO | Type | Scope |
|---|---|---|---|
| 28 | WO-62 | TARGETED EDIT | Audio integration (VFXManager → `AudioService`) |
| 29 | WO-67 | TARGETED EDIT | Master integration checklist, delete legacy files |
| 30 | — | CREATE | Daily quest system (WO TBD) |
| 31 | — | CREATE | Streak / daily login UI polish (WO TBD) |
| 32 | — | CREATE | Village visible growth (decoration unlock at wave 5/10/15) |

---

### SPRINT 8 — Monetisation (Phase 5, only after Sprints 1–7 complete)

| Order | WO | Type | Scope |
|---|---|---|---|
| 33 | WO-72 | CREATE | `CosmeticData.cs`, `MonetizationManager.cs` |
| 34 | WO-73 | CREATE | `CosmeticApplier.cs`, `ShopUI.cs`, `BattlePassSystem.cs` |
| 35 | WO-74 | CREATE | `CryptoPaymentManager.cs` (Solana SDK) |
| 36 | WO-75 | REPLACE | Full tabbed `ShopUI.cs` with crypto options |
| 37 | WO-76 | CREATE | `StakingBonusManager.cs` |
| 38 | WO-77 | CREATE + TARGETED EDIT | `DailyLoginBonus.cs`, wire Lumbermill multiplier |
| 39 | WO-78 | CREATE | `TransactionVerifier.cs`, `StakingDashboardUI.cs` |
| 40 | WO-79 | CREATE | `WarRoomWindow.cs` (Editor) |
| 41 | WO-80 | CREATE | Vercel/Neon API routes + `BackendAPI.cs` |

---

## Dependency Graph (simplified)

```
VFXManager (WO-50)
  └── All combat VFX calls (WO-56, 82, 83, 84)

EnemyBrain (WO-69) ── canonical
  └── EnemyHealth (WO-70)
        └── EnemyHitReaction (WO-84) — ADDITIVE
        └── KillComboTracker (WO-60) ← called in Die()

ATBCombatManager (WO-68)
  └── calls EnemyBrain.TryAttack()

HeroHealth (WO-70)
  └── HeroHitReaction (WO-81) — ADDITIVE

CameraShakeManager (WO-61)
  └── delegates to CinemachineCameraController (WO-87) when present

TowerCombat (existing)
  └── TowerVFXController (WO-82) — ADDITIVE
  └── TowerData ScriptableObject (WO-86) — TARGETED EDIT

WaveManager (existing)
  └── WaveCelebrationManager (WO-83) — ADDITIVE call
  └── WeatherManager (WO-52) — ADDITIVE call
  └── WaveData ScriptableObject (WO-86) — TARGETED EDIT

MonetizationManager (WO-72)
  └── CryptoPaymentManager (WO-74) — calls AddShards
  └── StakingBonusManager (WO-76) — calls AddShards
  └── DailyLoginBonus (WO-77) — calls AddShards
```

---

## Mobile Architecture Notes (from HP design review)

These align with the team's recommendations for mobile-first Unity development:

1. **Additive component pattern** — New features are added as separate
   `MonoBehaviour` components alongside existing ones, not merged into them.
   This means hot-swapping, easy disabling for debugging, and no risky merges.

2. **Null-safe wiring** — All inter-system calls use `?.` null-safety so any
   component can be absent without crashing (useful for staged rollouts).

3. **ScriptableObject stats** — No stat literals in code. Data-driven design
   lets you ship balance updates without recompilation (critical for mobile
   where resubmission is slow).

4. **Single damage entry point** — `EnemyHealth.TakeDamage()` is the only place
   damage can enter an enemy. Towers, pets, heroes, spells all call this one
   method. Easy to add hit-pause, armour, resistances later.

5. **Tiered performance** — `PerformanceManager` auto-selects Low/Medium/High
   on first run. All VFX, shaders, and animators read from the active tier.
   No device-specific `#if` guards outside the manager.

6. **Pool everything** — VFXManager uses `ObjectPool<GameObject>`. Extend this
   pattern to projectiles and enemy spawning in Sprint 6.

---

## Claude Code CLI Instructions

When implementing a sprint:

1. Read the WO file completely before writing any code.
2. Check the `Type` column — only **REPLACE** warrants deleting existing code.
3. After each WO, run the game in the Editor and confirm the exit criteria.
4. Commit with message `feat: implement WO-XX — <title>`.
5. Do NOT stage Unity auto-generated files beyond `.meta` files for new scripts.
6. Do NOT touch `CLAUDE.md`.
7. Do NOT skip to Sprint 8 (monetisation) before Sprint 1–4 are green.
