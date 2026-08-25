# WO-1202 RESULT — quest rewards typed list + placement/difficulty scale

**Status:** IMPLEMENTED locally (2026-08-17) — awaiting `COMPILE_GATE_OK` + `QUEST_REACH_OK` + owner felt-close on reward slab
**Owner creative pack:** LOCKED (*"yes use your guidance"*)

## What landed

### Phase A — schema
- `QuestReward` fixed struct → `List<QuestRewardLine>` (`kind`/`amount`/`id`)
- `QuestRewardMath` sum/describe helpers
- `QuestService.RewardEarned` → `Action<IReadOnlyList<QuestRewardLine>>`
- `QuestRewardBridge` switches kinds: xp (XpEarnerRegistry), wood/iron/food/crystals (GrantSpendable), magic, item; unknown → Fail loud; troop → Warn (out of scope)
- `RumorBoardVM.RewardPartsFor` emits `XP N` first, then resources/items
- `UICaptureLaunch.MakeRumor` builds typed list (+ XP chip)

### Phase B — authoring (both `quests.json` copies, byte-identical)
- Migrator: `tools/migrate_quest_rewards_1202.py`
- **63/63 stages pay XP**; 0 empty
- Parity: no reductions of prior crystals/food/magic/item
- Highlights:
  - `forgemasters_act1` → **900 XP** (was empty)
  - `vendor.armorer` / `hold-the-line` → `armor_knight_common` + iron + XP
  - `forgemasters_act4` / `the-choice` → **2800 XP** + 300c + 100 magic + `ring_heartward`
  - `elarion.welcome` → 315 / 900 XP keeping prior crystals/food

### Phase C — tests
- `QuestCompletabilityRegression` parses typed list; Case 5 checks GrantSpendable + XpEarnerRegistry + XP-on-every-stage
- `QuestCompletionTests` grantItemId scan reads `kind:item` lines

## Files touched
- `Assets/_Modules/Core/Quests/QuestCatalog.cs`
- `Assets/_Modules/Core/Quests/QuestService.cs`
- `Assets/_Modules/Village/Quests/QuestRewardBridge.cs`
- `Assets/_Modules/Village/Hero/RumorBoardVM.cs`
- `Assets/Editor/UICaptureLaunch.cs`
- `Assets/Editor/Regression/QuestCompletabilityRegression.cs`
- `Assets/Tests/EditMode/QuestCompletionTests.cs`
- `Assets/Resources/Data/Canonical/quests.json`
- `Assets/StreamingAssets/Data/Canonical/quests.json`
- `tools/migrate_quest_rewards_1202.py`

## Still ops / PO
- [ ] Batchmode `COMPILE_GATE_OK`
- [ ] `QUEST_REACH_OK` / full `REGRESSION_OK`
- [ ] Felt: reward slab makes quest A vs B a real choice
- [ ] UI capture PNGs of rumor board

## Do not reopen
Creative pack in WO-1202 §OWNER RULING without owner word.
