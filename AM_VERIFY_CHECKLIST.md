# AM Verify + Wiring Checklist
**Branch:** `feat/tower-core-loop` (commits e2b140a + 6ead89c)  
**Compile status:** ✅ Both batchmode gates passed rc=0  
**Date:** 2026-05-27

---

## What you can play-test immediately — zero wiring

These three services are **`[RuntimeInitializeOnLoadMethod]` self-bootstrapped** and spawn as `DontDestroyOnLoad` GOs the moment you hit Play. No scene wiring required.

| Service | Spawns as |
|---|---|
| `EconomyService` | `"EconomyService"` GO — Wood=200, Stone=150, Iron=80, Crystals=50 |
| `SkillSystem` | `"SkillSystem"` GO — all skills start at level 1, 0 available points |
| `TowerConstructionQueue` | `"TowerConstructionQueue"` GO — queue processes automatically |
| `RewardedAdManager` | `"RewardedAdManager"` GO — 480 s cooldown stub, no SDK needed |

---

## Step 1 — Seed test TowerData assets (one click)

`Defenders → Seed Tower Data`

Creates 3 `.asset` files in `Assets/Resources/Towers/`:

| Asset | Cost | Required Skill | L2/L3 Ability |
|---|---|---|---|
| `ArcherTower.asset` | 100 wood | None | SlowEnemies |
| `MageTower.asset` | 200 wood | Woodworking L1 | FireAura |
| `CannonTower.asset` | 150 wood | Blacksmith L1 | SlowEnemies |

All three use procedural placeholder visuals (no tower art authored yet). Run once, then re-run any time to reset to defaults.

---

## Step 2 — Add `TowerPlacementSystem` to the village scene

`TowerPlacementSystem` is **not** self-bootstrapped. It needs a GO in the scene.

1. In the Hierarchy, right-click → Create Empty → name it `"TowerPlacementSystem"`
2. Add Component → `TowerPlacementSystem`
3. Inspector fields:
   - **Ground Mask** → set to the `"Ground"` layer (defaults to ~0 / all layers, which also works)
   - **Grid Size** → 1 (default, fine for testing)
   - Leave overlap radius, ray distance at defaults

This GO being present is enough — `TowerPlacementSystem.Instance.StartPlacing(data)` is the only call needed from the HUD.

> **HUD hook (not in scope tonight):** The existing `BuildMenu.cs` has its own ghost system. A future ticket will route `BuildMenu` → `TowerPlacementSystem.StartPlacing()`. For now, you can test placement by calling `TowerPlacementSystem.Instance.StartPlacing(towerData)` from any test script.

---

## Step 3 — Add `LevelUpSkillPopup` to the village scene

`LevelUpSkillPopup` requires a `UIDocument`. Code-builds its own UI at runtime — no UXML file needed.

1. Create Empty → name it `"LevelUpSkillPopup"`
2. Add Component → `UIDocument`
3. **UIDocument → Panel Settings** → assign the same `PanelSettings` asset your other UI Toolkit panels use (look for it at `Assets/UI/...` or wherever your HUD uses it)
4. Add Component → `LevelUpSkillPopup`
5. The popup auto-subscribes to `HeroProgression.OnLevelUp` + `SkillSystem.OnSkillsChanged` in `OnEnable` — no further wiring needed

> **To test without levelling up:** Temporarily call `SkillSystem.Instance.GrantSkillPoint()` from any script, then `HeroProgression.Instance.AddXP(99999)`.

---

## Step 4 — Add GROUP 3 bridges to the WaveManager GO

All three audio components and CampaignManager use `[RequireComponent(typeof(WaveManager))]` — they live **on the WaveManager GameObject** and self-wire to it via `Reset()` / `Awake()`.

### WaveMusicController
1. Select the WaveManager GO → Add Component → `WaveMusicController`  
   (`_wave` field auto-fills via `Reset()`)
2. Assign AudioClips *(or leave empty — null-guarded, just won't play)*:
   - **Exploration Track** → your ambient/exploration music clip
   - **Combat Track** → your wave-combat music clip
3. Tune **Crossfade Seconds** (default 1.5 s) and **Track Volume** (default 1.0)

### TowerVoiceController
1. Select the WaveManager GO → Add Component → `TowerVoiceController`
2. Assign **Voice Lines** AudioClip[] *(leave empty for now — component is fully null-guarded)*  
   Fires once per session when `HeartController.Hp` drops below 30%

### TowerAudioController
1. Select the WaveManager GO → Add Component → `TowerAudioController`
2. Assign *(both optional — null-guarded)*:
   - **Build Complete Clip** → SFX for when a tower finishes construction
   - **Upgrade Clip** → SFX for tower upgrade

### CampaignManager
1. Select the WaveManager GO → Add Component → `CampaignManager`
2. **Campaign** field → assign a `CampaignData` SO *(or leave null — manager is a no-op when unset, free-play works normally)*
3. Create a `CampaignData` SO: right-click in Project → Create → Defenders → Campaign Data → wire in `MissionData` entries

> **Nothing breaks if these are left unassigned.** All four components are fully null-guarded. The village scene runs normally without them.

---

## Step 5 — Play-test the tower loop

With steps 1–3 done, the full place → build → upgrade path is live:

```
Call TowerPlacementSystem.Instance.StartPlacing(towerData)
  ↓
Left-click a valid ground position (green ghost)
  ↓
EconomyService.Spend(cost)  →  TowerConstructionQueue.AddToQueue()
  ↓
Procedural placeholder cube appears, ProgressBar rises over buildTime seconds
  ↓
Tower.Initialize() called  →  Level 1 visual (tinted grey cube)
  ↓
TowerUpgradeButton.OnUpgradeClicked()  →  Tower.Upgrade()  →  Level 2/3 visuals
  ↓
CameraShakeBridge fires Shake() on ThirdPersonCameraFollow  (or no-ops if none found)
  ↓
HeroProgression.OnLevelUp  →  LevelUpSkillPopup shows  →  SpendPoint(type)
```

---

## What is NOT wired and NOT expected to work yet

| Item | Status | Next step |
|---|---|---|
| BuildMenu → TowerPlacementSystem routing | ❌ Not wired | Future ticket to route `BuildMenu` tile tap → `StartPlacing()` |
| TowerData prefab art | ❌ No authored art | Procedural placeholder cubes only |
| AudioClips | ❌ None assigned | Assign when audio assets are ready |
| CampaignData SO | ❌ Not created | Create via right-click → Create → Defenders → Campaign Data |
| `TowerConstruction._finalTowerVisual` | ❌ No prefab | TowerConstruction builds procedural visual via Tower.ApplyVisualForLevel |
| Scaffolding prefab | ❌ None | TowerData.scaffoldingPrefab = null, graceful skip |
| Push to master / PR | ❌ Local branch only | Open PR after verify pass + play-test |

---

## GROUP 1 / GROUP 2 status (action required — read before filing more tickets)

> **These ticket targets do not exist on `feat/tower-core-loop`.** They belong to the `defenders-unity-review` clone.

| Ticket | Target | Status on this branch |
|---|---|---|
| DEF-59/60 | WaveManager spawning corrections | **N/A** — WaveManager uses async UniTask + JSON; completely different architecture |
| DEF-64 | Wildlife (BirdFlock, Butterfly, Rabbit) | **N/A** — no wildlife in Village scene |
| DEF-65 | SmartMobileCamera | **N/A** — camera is `ThirdPersonCameraFollow`; already has `Shake(float, float)` |
| DEF-66 | TowerDamageStateManager rename | **N/A** — class doesn't exist on this branch |
| DEF-72 | EnemyBrain / TacticalData / GroupCoordinator | **N/A** — enemy system is `Enemy.cs` + `Configure()`, not EnemyBrain |

If you want these done, point a session at the review repo clone. On this branch, `ThirdPersonCameraFollow.Shake()` already exists and `CameraShakeBridge` in `Tower.cs` already calls it via reflection.

---

## Quick-reference: which components are on which GO

| GO | Components |
|---|---|
| *(auto-spawned)* | `EconomyService`, `SkillSystem`, `TowerConstructionQueue`, `RewardedAdManager` |
| WaveManager GO | `WaveManager`, `WaveHudBridge`, `DailyQuestCombatBridge`, **`WaveMusicController`**, **`TowerVoiceController`**, **`TowerAudioController`**, **`CampaignManager`** |
| *(new — step 2)* | `TowerPlacementSystem` |
| *(new — step 3)* | `LevelUpSkillPopup` + `UIDocument` |
| *(runtime-spawned)* | `Tower` + `TowerConstruction` (AddComponent'd by TowerConstructionQueue) |

**Bold = added tonight, needs scene placement or AudioClip assignment before full test.**

---

*Generated by Cowork verify pass — 2026-05-27*
