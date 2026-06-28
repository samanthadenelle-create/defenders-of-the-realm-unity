# WORK ORDER 564 — Building Passive Income + Daily/Story Quest Reward Payout

**Status:** IMPLEMENTED (edit-only; not gated/committed — CLI reconciles)
**Lane:** Economy / Quest-rewards (Combat-adjacent, code-only)
**Date:** 2026-06-28
**Assembly:** DeNelle.Village → DeNelle.Core / DeNelle.Cosmetics (legal per §5)

---

## Problem (two dead-by-default loops from the gap audit)

### TASK 1 — Building passive income never ticked
`ResourceBuildingHarvester` (the per-level auto-harvest tick that makes the
upgrade ladder's speed/size fields actually pay out) was `AddComponent`'d ONLY by
the LEGACY `BuildingUpgradePanelBootstrap` (line 83). That bootstrap short-circuits
when `ff.buildingupgradepanel` is ON (the default) — `BuildingUpgradePanelBootstrap.cs:47`
`if (DeNelle.Core.FeatureFlags.BuildingUpgradePanel) return;`. The MVVM bootstrap
that actually runs (`BuildingUpgradePanelMvvmBootstrap.cs:64-67`) never added the
harvester. Net: on the live default path, upgrading a Farm/Lumbermill/Forge changed
a label but produced **zero** passive income.

### TASK 2 — Daily-quest rewards only partially paid; story-quest items not granted
- The daily reward schema is `DailyQuestSlotReward` (`DailyQuests.cs:47-56`):
  `rewardCrystals, rewardFood, rewardGlimmer, rewardWisdom, rewardRandomItem`.
- The completion event is `DailyQuestService.QuestCompleted` (`DailyQuests.cs:183`),
  fired in `Report(...)` at `DailyQuests.cs:251`.
- **Audit nuance (verified against code):** the claim "QuestCompleted has NO
  listener / grants nothing" is **partly inaccurate** — `DailyQuestTowerBridge`
  (`DailyQuestTowerBridge.cs:105,117`) DID listen and grant crystals/wisdom/glimmer.
  But it **silently dropped** two schema fields:
  - `rewardFood` — the **exploration** slot grants **20 food** (`daily-quests.json`)
    → never paid (no food branch).
  - `rewardRandomItem` — the **wildcard** slot has `rewardRandomItem: true` → never
    paid (no item branch).
  So daily quests genuinely under-paid, and reward logic was mixed into a class
  named for tower placement.
- Story-quest item rewards (`QuestRewardBridge.cs:87-88`) were **log-only** — the
  `GrantItemId` never entered any inventory.

---

## Grant-API RCA (file:line)

| Reward | API | Location |
|---|---|---|
| Crystals | `GameStateService.AddCrystals(int)` | `Core/State/GameStateService.cs:300` |
| Food | `GameStateService.AddFood(int)` | `Core/State/GameStateService.cs:317` |
| Glimmer | `GlimmerCurrencyService.TryAddGlimmer(int)` | `Cosmetics/GlimmerCurrencyService.cs:181` (ns `DeNelle.Cosmetics`) |
| Wisdom | `WisdomCurrencyService.Grant(int)` | `Village/Talents/WisdomCurrencyService.cs:74` |
| Item (persisted) | `VillageInventory.Add(id, amount)` → `GameState.GearInventory` | `Village/Crafting/VillageInventory.cs:69` |
| Random-item pool | `ConsumableCatalog.All` (data-driven) | `Village/Items/ConsumableCatalog.cs:115` |

Daily slot rewards (`Assets/{Resources,StreamingAssets}/Data/Canonical/daily-quests.json`,
both copies identical, 41 templates): combat = 80💎/0🍖/1✨/1📘; exploration =
60💎/**20🍖**/1✨/0📘; wildcard = 0💎/0🍖/1✨/1📘/**randomItem**.

---

## Changes

### TASK 1 — `BuildingUpgradePanelMvvmBootstrap.cs` (line ~64-67)
Added `go.AddComponent<ResourceBuildingHarvester>();` to the MVVM panel GameObject.
Lifecycle-safe: the harvester self-guards a singleton (`ResourceBuildingHarvester.cs:48`
destroys a duplicate component) and the bootstrap's global dedupe ensures only one
panel GO exists — so it can never double-add even if both bootstrap paths ran.
(Level-1 buildings still produce nothing — the harvester's "earned, not free" gate
at `ResourceBuildingHarvester.cs:76` is untouched.)

### TASK 2a — NEW `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs`
The single, dedicated daily-reward dispenser. Subscribes to
`DailyQuestService.QuestCompleted`, reads the slot reward from
`DailyQuestCatalog.RewardFor(q.Slot)` (data-driven), latches `ClaimedAtUnix` first
(no double-grant), then grants **all five** schema fields via the canonical APIs
above. Random item rolls a `ConsumableCatalog.All` entry into `VillageInventory`.
One `[Flow:Economy]` line per grant (§12). Self-bootstraps (DontDestroyOnLoad),
same pattern as the other quest bridges.

### TASK 2b — `DailyQuestTowerBridge.cs` (reward logic removed)
Stripped the reward-dispense responsibility (HookReward/UnhookReward/
HandleQuestCompleted + Glimmer/Wisdom usings). It now does ONLY its tower-placement
tick. Rationale: exactly ONE listener on `QuestCompleted` avoids racing the
`ClaimedAtUnix` latch, and reward logic lives in one place covering the whole schema.

### TASK 2c — `QuestRewardBridge.cs` (story-quest item grant)
Replaced the log-only stub (`:87-88`) with a real grant:
`VillageInventory.Instance?.Add(reward.GrantItemId, 1)` (persists via GearInventory)
+ `[Flow:Economy]` trace; warns if the inventory isn't ready.

---

## Verification

- **Brace check (all OK, no NUL):**
  - `BuildingUpgradePanelMvvmBootstrap.cs` 8/8
  - `DailyQuestRewardBridge.cs` 35/35
  - `DailyQuestTowerBridge.cs` 11/11
  - `QuestRewardBridge.cs` 16/16
- **Namespaces confirmed** against usings: `DeNelle.Cosmetics`,
  `DeNelle.Village.Talents`, `DeNelle.Village.Crafting`, `DeNelle.Village.Items`.
- **Cross-assembly legal:** all new refs are Village→Core / Village→Cosmetics
  (the latter already referenced by the prior TowerBridge code, so the asmdef
  reference exists). All cross-module calls null-conditional.
- **JSON untouched** (both daily-quests.json copies already carry the slot rewards;
  verified identical).

---

## Files

**Modified**
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvmBootstrap.cs`
- `Assets/_Modules/Village/Buildings/DailyQuestTowerBridge.cs`
- `Assets/_Modules/Village/Quests/QuestRewardBridge.cs`

**New**
- `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs` (Unity will generate
  the `.meta` on import — CLI to include it when committing)

---

## Owner-decision flags

1. **Audit overstated the daily gap.** Daily rewards were NOT entirely undispensed —
   crystals/wisdom/glimmer paid via TowerBridge; only **food + random-item** were
   missing. I consolidated into a dedicated bridge (correct, avoids double-listener)
   rather than a parallel one that would race the latch.
2. **Random-item pool = `ConsumableCatalog`** (potions/food/tents). If you'd rather
   daily wildcard reward gear (weapons/armor) instead of consumables, point the pool
   at a different catalog — trivial swap in `GrantRandomItem`.
3. **Random item grants quantity 1.** Schema has no amount field for the random item;
   add one to `DailyQuestSlotReward` if a stack is wanted.
