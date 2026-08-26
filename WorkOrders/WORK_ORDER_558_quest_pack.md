# WORK ORDER 558 — Quest Pack (a ton of usable daily quests)

**Status:** CLOSED 2026-08-26 — owner felt-tested PASS on APK `2026.08.26.342478` (source `bcef3be7`).
**Lane:** Combat/AI + Data (quests) — file-disjoint from dialogues/PanelRouter/FeatureFlags
**Author:** quest-pack agent (worktree `agent-af0d02f61a0302dbd`, ff-merged to branch tip `05d2f032` before editing)

---

## 1. RCA — the quest system as it actually is

There are **two** quest data files, but only **one runtime**:

| File | Loader / runtime | Status |
|---|---|---|
| `Data/Canonical/daily-quests.json` | `DailyQuestCatalog` + `DailyQuestService` (`Assets/_Modules/Core/Quests/DailyQuests.cs`) → `DailyQuestHud` (the TOWN ACTIONS "Quests" button) | **WIRED, working** |
| `Data/Canonical/quests.json` | *(none — no `QuestCatalog`/`QuestService` class exists)* | **ORPHAN narrative data**, only `QuestProgress` save-schema stubs in `NestedTypes.cs` |

So the only **usable** quest surface is the **daily-quest system**. Building a runtime for `quests.json` would be greenfielding a whole system (against directive) — out of scope here; flagged below.

### Daily-quest schema (file:line)
`Assets/_Modules/Core/Quests/DailyQuests.cs`
- `DailyQuestTemplate` (L31): `id`, `slot`, `target`, `label`, `weight`, `requiresHero`, `requiresFeature`, `day1Guaranteed`.
- `DailyQuestSlotReward` (L47): per-**slot** reward (`rewardCrystals/Food/Glimmer/Wisdom/RandomItem`) — **rewards are per-slot, not per-quest**.
- `DailyQuestCatalogData` (L58): top-level config + `slots` + `templates`.
- Roller (`RollSet` L305 / `RollOne` L316): picks **1 quest per slot per day** from `combat`, `exploration`, `wildcard`, weighted-random; `day1Guaranteed` short-circuits the combat slot for new players.
- **Progress (the key mechanic) — `Report(eventId, amount)` (L220):** a template ticks when
  `q.TemplateId == eventId || q.TemplateId.StartsWith(eventId + ".")`. So a **child id** `parent.child` ticks whenever gameplay reports the **parent** event. This is the lever the whole pack rides.
- `QuestCompleted` event (L183) fires on completion but **has NO listener** → **no reward is ever dispensed** (pre-existing gap, see flags).

### The usability problem found
Across the whole codebase the ONLY `Report()` caller was `DailyQuestCombatBridge` → `Report("combat.clear-waves")`. **Every other template in the shipped file was dead** — it could roll but never progress (frost-nova, sword-strike, hawks-eye, harvest-*, walk-all-gates, talk-heroes, increase-bond-rank, upgrade-tower, fortify-wall, equip-cosmetic, watch-intro, heart-below-30, no-ultimate). Several were also hero-gated, conflicting with the single-Knight north star.

`DailyQuestHud.ResolveLabel` only substitutes `{target}` — **not** `{element}`, so the legacy `combat.clear-element-wave` label rendered the literal text `{element}`. The new pack avoids `{element}`.

---

## 2. What was implemented

### A. Data — rewrote the template pool (both dual-copies, IDENTICAL except the trailing pointer note)
- `Assets/StreamingAssets/Data/Canonical/daily-quests.json`
- `Assets/Resources/Data/Canonical/daily-quests.json` (the Resources copy wins via `CanonicalJson.Read`, WebGL-safe)

**41 templates**, every one riding a **wired** Report channel so it actually completes:

| Slot | Count | Channel (wired event) | Variety |
|---|---|---|---|
| combat | 18 | `combat.clear-waves` (+ `combat.build-towers` day1) | 17 `clear-waves.*` children, targets 1→10 (difficulty tiers), canon themes: orcs, Hollow tide, trolls, acolytes, necromancer vanguard, Syndrath's shadow, Heart guard, echo cover, night siege, endless vigil |
| exploration | 12 | `explore.visit-gate` | targets 1→6 gate-passes: skirmish/scout/patrol/perimeter/warden route/echo escort/long march |
| wildcard | 11 | `wildcard.earn-glimmer` (7, targets 3→30) + `wildcard.learn-talent` (4, target 1) | glimmer-earning (Sable/jeweler/Glimmer Road flavor) + talent-learning |

Difficulty is expressed via `target` (and weight: easy quests weighted higher so the player mostly sees achievable quests). All canon-correct (Elarion, Heart, orcs/Hollow/trolls/Syndrath, echoes, Sable the jeweler, the four gates). No `requiresHero` (single-Knight north star). No `{element}` placeholder.

### B. Wiring — 3 new `Report()` hooks (minimal, additive, low-contention)
All verified by Read at branch tip; all in assemblies that already reference `DeNelle.Core`:

1. `Assets/_Modules/Village/Gates/GateProximityOpener.cs` — `OnHeroEntered()`, inside the `_heroesInside == 1` transition → `Report("explore.visit-gate", 1)` (one tick per fresh gate approach). +`using DeNelle.Core.Quests;`
2. `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` — `TryAddGlimmer()` on success → `Report("wildcard.earn-glimmer", amount)`. +`using DeNelle.Core.Quests;`
3. `Assets/_Modules/Village/Talents/WisdomCurrencyService.cs` — `Unlock()` on success → `Report("wildcard.learn-talent", 1)`. +`using DeNelle.Core.Quests;`

Combined with the **pre-existing** `combat.clear-waves` hook (`DailyQuestCombatBridge`), all four wired channels cover all three slots, so **every** authored template ticks to completion.

---

## 3. Validation
- Brace check: GateProximityOpener 14/14, GlimmerCurrencyService 30/30, WisdomCurrencyService 21/21. No NUL bytes.
- JSON: both copies parse; 41 templates each; data identical except the intentional trailing dual-copy pointer note.
- All referenced concepts are canon; no template references a non-existent enemy/item/recipe id (the schema references events, not item ids — rewards are slot-level config).

---

## 4. Owner-decision flags (NOT done here — deliberately out of scope)

1. **Reward dispensing is absent for ALL daily quests (pre-existing).** `QuestCompleted` (DailyQuests.cs L183) has no listener; completing a quest fires the "Daily Quest Complete" toast but grants nothing. To close the loop, add a `DailyQuestRewardBridge` (Village) listening to `DailyQuestService.QuestCompleted` and granting via: `GameStateService.Instance.State.Resources.Crystals/Food` (Core), `GlimmerCurrencyService.Instance.TryAddGlimmer`, `WisdomCurrencyService.Instance.Grant`. Cross-assembly (Core+Cosmetics+Village) — needs a deliberate home; flagged rather than smuggled in.
2. **`combat.build-towers` (day1 guaranteed) still has no progress hook** — it can roll but not complete, so `Day1QuestDone` never latches and it re-appears daily for a new player. Kept because it is `DailyQuestService.Day1QuestTemplateId` (a code const). Fix: report `"combat.build-towers"` from the tower-place path (`BuildModeController`) — left for a focused combat-lane WO (that file is large and in the combat serialization lane).
3. **`quests.json` (24 narrative main/side/gear/endgame chains) has no runtime.** Rich content sitting dormant. If desired, a future WO can build a `QuestCatalog`/`QuestService` over the existing `QuestProgress` save schema (Active/Completed/Available/Keystones/stageId) + a board/rumor UI. Greenfield — separate WO.
4. **More objective-type variety needs more hooks.** Easy, identified follow-up channels: enemy kills (`WaveManager.HandleEnemyDied`), boss kills (`HandleApexBossDied`), harvesting (`OfflineHarvestService.Claimed`), crafting (`CraftingPedestal.Crafted`), tower upgrade (`BuildTimerService.JobCompleted`). Each is one additive `Report()` line; not done here to keep contention low.

---

## 5. Files modified / created (for reconcile by explicit path)
- `Assets/StreamingAssets/Data/Canonical/daily-quests.json` (rewritten)
- `Assets/Resources/Data/Canonical/daily-quests.json` (rewritten)
- `Assets/_Modules/Village/Gates/GateProximityOpener.cs` (using + 1 line)
- `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` (using + 1 line)
- `Assets/_Modules/Village/Talents/WisdomCurrencyService.cs` (using + 1 line)
- `WorkOrders/WORK_ORDER_558_quest_pack.md` (this file)

**Did NOT touch:** dialogues.json, PanelRouter.cs, FeatureFlags.cs, WaveManager.cs, any `.unity` scene, VillageSceneBuilder.cs.
