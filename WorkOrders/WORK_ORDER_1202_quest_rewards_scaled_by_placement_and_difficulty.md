# WO-1202 — Quest rewards: typed list + placement/difficulty scale (lore-aligned)

**Status:** READY TO IMPLEMENT — **creative rulings LOCKED** (owner 2026-08-17: *"yes use your guidance"*)  
**Minted:** 2026-08-17 (CLI seat) — banner bumped 1202 → 1203 in the same edit  
**Silo:** Progression / Quests / Rumor Board  
**Folds / sequences:** **WO-1201** (schema Option B + XP seam) — implement 1201’s migration **as Phase A of this ticket**, then author the scale table in Phase B. Do not leave 1201 as a second competing pickup.  
**Owner intent:** *"read the lore and quest story lines and assign rewards that both scale with placement and difficulty and make sure it all works. they can give exp resources rare equipment whatever you scope"* + Option B typed reward list.  
**Context:** `docs/QUEST_REWARD_DIRECTIONS.md`, `WORK_ORDER_1201_quests_pay_experience.md`, live `quests.json` v (24 quests / 63 stages).

---

## ⛔ OWNER RULING 2026-08-17 — creative pack LOCKED (do not re-open)

Owner: **"yes use your guidance."** The following is binding for Phase B authoring. CLI does not ask for alternatives.

1. **XP is the MAIN reward** — players pick quests for XP first; resources/gear are placement flavor.
2. **XP bands (terminal):** side ~400 · gear ~650 · main ~900 · endgame ~1600–2800 (× chain depth). Earlier stages still pay weighted XP (empty slabs are the defect).
3. **Resources follow story place:** lumber→wood · forge→iron · granary→food · jeweler→crystals+magic · market→crystals · Echo bonds→food/crystals (+magic for aether) · steward→wood+iron+crystals · saga→crystals+magic.
4. **Rare gear — only three moments:**
   - `forgemaster.first-commission` / `claim-weapon` → keep `knight_iron` (Iron Longsword)
   - `vendor.armorer` / `hold-the-line` → `armor_knight_common` (Ironward Plate)
   - `forgemasters_act4` / `the-choice` → `ring_heartward` (Heartward Seal)
5. **`forgemasters_act1` ("Honest Steel")** must pay ~900 XP (pays nothing today).
6. **Do NOT:** loot boxes · combat potions from quests · troop unlocks this pass · daily-quest changes · Aegis legendary grants from quests.

Tone: Folk thank you early; forge/saga = steel and memory; Echo = attunement; Act 4 = a vow kept.

---

## 0. One-line truth

**Quests must pay in a currency the player can feel choosing between.**  
Today ~half the stages pay nothing; the rest are a fixed struct (`crystals`/`food`/`magic`/`grantItemId`) with no XP, no wood/iron, and no placement sense. Migrate to a **typed reward list**, derive **XP from type × stage weight × chain depth**, author **resources/items from lore placement**, and prove the single grant seam works end-to-end.

---

## 1. Phase A — Schema (from WO-1201; do not re-litigate)

### 1.1 Shape

```json
"reward": [
  { "kind": "xp",       "amount": 500 },
  { "kind": "crystals", "amount": 75 },
  { "kind": "wood",     "amount": 400 },
  { "kind": "iron",     "amount": 120 },
  { "kind": "food",     "amount": 40 },
  { "kind": "magic",    "amount": 15 },
  { "kind": "item",     "id": "knight_iron" }
]
```

- Empty / missing reward → `[]` (not a zeroed struct).  
- `{kind, amount}` and `{kind, id}` both bind.  
- **`kind: "troop"` is OUT OF SCOPE** — shape must accommodate it in comments only.  
- **Unknown kind → `FlowTrace.Fail`** naming quest id, stage id, kind; skip that entry only (Guard.TryEach). Never silent drop (WO-1163 lesson).

### 1.2 Code surfaces (all same PR)

| Surface | Duty |
|---|---|
| `QuestCatalog.cs` `QuestReward` | Become list of `QuestRewardLine { Kind, Amount, Id }` + helpers to sum legacy axes for parity |
| `QuestStage.Reward` | `List<QuestRewardLine>` (or wrapper that Newtonsoft binds) |
| `QuestService` `RewardEarned` | Payload = list (or aggregate object); FlowTrace enumerates kinds |
| `QuestRewardBridge.OnRewardEarned` | Switch on kind: crystals/food via Economy; magic → GameState; wood/iron via `GrantSpendable`; item → VillageInventory; **xp → `XpEarnerRegistry.TryGet(HeroProgression.Id)` + Guard** |
| `RumorBoardVM.RewardPartsFor` | Emit `XP N` chip (ASCII); also wood/iron chips; never silently drop unknown kinds (show `?kind` or Fail) |
| `Editor/UICaptureLaunch.cs` (~2374) | Synthetic reward migrated to list form |
| `QuestCompletabilityRegression` | Round-trip parity case + unknown-kind Fail case + XP curve oracle |

### 1.3 Mirror law

Both copies byte-identical, same edit:
- `Assets/Resources/Data/Canonical/quests.json`
- `Assets/StreamingAssets/Data/Canonical/quests.json`

### 1.4 Save schema

**Do NOT bump `SaveSchema.CurrentVersion`.** Rewards are catalog; hero XP already persists (v29 fields).

### 1.5 Daily quests

**OUT OF SCOPE** (`daily-quests.json` is a different DTO). Flag for PO separately.

---

## 2. Phase B — Scale rules (DERIVE, then override sparingly)

### 2.1 Difficulty axes (already in data)

| Axis | Source | Weight |
|---|---|---|
| **Type tier** | `QuestDef.type` | `side=1.0`, `gear=1.25`, `main=1.6`, `endgame=2.2` |
| **Chain depth** | walk `requiresQuestId` | `1 + 0.25 * depth` (act1=1.0, act2=1.25, act3=1.5, act4=1.75) |
| **Stage weight** | index / count | early `0.35`, mid `0.55`, **terminal `1.0`** (last stage carries the chapter beat) |
| **Placement** | quest id / vendor theme | chooses **resource mix + item**, not the XP formula |

### 2.2 XP curve (primary reward — not garnish)

```
xp = round( BaseType[type] * ChainDepth * StageWeight )
```

| type | BaseType (terminal stage) | Notes |
|---|---:|---|
| side | 400 | petbond / vendor sides |
| gear | 650 | forge / armorer / first commission |
| main | 900 | welcome / steward / forgemasters act1–2 |
| endgame | 1600 | forgemasters act3–4 |

Non-terminal stages use StageWeight so mid beats still pay (today many are EMPTY).  
**Anchor:** `XpToNextFor` L1→2=150, L2→3=1000, L3→4=2850 — a main terminal must read as **hundreds–low thousands**, not 25.

Override key (optional on stage): `"xpOverride": N` — oracle lists every override; silent drift fails.

### 2.3 Resource placement map (lore-aligned)

Match the **story placement**, not a flat crystal dump:

| Placement (quest family) | Primary resources on terminal | Why |
|---|---|---|
| `elarion.welcome` | crystals + food | Heart + first defense — keep existing feel, add XP |
| `vendor.lumbermill` / Old Pell | **wood** heavy + food | Grove / sapling lore |
| `vendor.forge` / forgemaster / Borin | **iron** + wood + magic | Forge / steel |
| `vendor.armorer` / Halvard | **iron** + crystals | Plate / salvage |
| `vendor.granary` / Mother Wren | **food** heavy | Full bellies |
| `vendor.jeweler` / Sable | crystals + magic | Aether facet |
| `vendor.market` / Coppin | crystals + food | Trade road |
| `vendor.inn` | food + crystals | Hall defense |
| `vendor.stable` / Fenn / petbond.* | food or crystals + magic (aether lines) | Echo bonds |
| `vendor.steward` | crystals + magic + wood/iron mix | Rebuild Elarion |
| `forgemasters_act*` | magic + crystals; act4 terminal = rare item | Saga climax |

**Amounts (terminal, before type mult on resources):** use a soft band so side < gear < main < endgame:

| Band | wood | iron | food | crystals | magic |
|---|---:|---:|---:|---:|---:|
| side terminal | 600–1200 | 200–400 | 40–80 | 40–75 | 0–15 |
| gear terminal | 400–800 | 300–600 | 0–40 | 75–120 | 10–25 |
| main terminal | 800–1500 | 400–700 | 40–80 | 100–250 | 25–50 |
| endgame terminal | 0–1000 | 0–500 | 0–60 | 150–300 | 50–100 |

Non-terminals: ~35–55% of that band’s resource line **or** XP-only if the beat is pure talk (still pay XP — empty slabs are the defect).

Preserve **round-trip parity** for crystals/food/magic/item that already exist; **add** XP + wood/iron on top. Never reduce an existing crystal/food/magic/item payout without an explicit override note in the RESULT.

### 2.4 Rare equipment (scoped, real ids only)

Grant **only** ids that resolve in gear catalogs. Prefer teaching the system over dumping legendaries.

| Quest / stage | Item id | Display | Rationale |
|---|---|---|---|
| `forgemaster.first-commission` / `claim-weapon` | `knight_iron` | Iron Longsword | **KEEP** — already authored |
| `vendor.armorer` / `hold-the-line` (terminal) | `armor_knight_common` | Ironward Plate | Shields of the Fallen payoff |
| `vendor.forge` / `field-test` (terminal) | — | (no second sword) | Keep crystals; XP is the pull |
| `forgemasters_act4` / `the-choice` | `ring_heartward` **or** `amulet_oathward` | Heartward Seal / Oathward Pendant | Saga climax — rare accessory, not a full legendary set (legendaries stay craft/Aegis) |
| `petbond.aetherfox` terminal | — | no gear | Magic + crystals + XP only (Echo is the prize) |

⛔ Do not grant `aegis_*_legendary` from quests — those are craft/endgame sinks.  
⛔ Do not invent item ids. If unsure, XP + resources only.

---

## 3. Phase B — Per-quest authoring checklist (CLI fills both JSON copies)

Use Phase A list form. Values below are **targets**; derive XP from §2.2 and adjust resources to §2.3. Mark overrides in `_authoringNotes` at file top.

| Quest id | type | Placement | Terminal XP (derive) | Terminal extras (keep parity + add) |
|---|---|---|---:|---|
| `elarion.welcome` | main | Heart / gate | ~900 on `first-defense`; ~315 on `meet-elder` | Keep 50/100 crystals + 20 food; add XP both stages |
| `forgemaster.first-commission` | gear | Forge | ~650 on claim; mid on gather | Keep `knight_iron` + magic 10; add iron/wood on gather; XP both |
| `vendor.supply-run` | side | Market | ~400 | Keep 25 crystals; add XP |
| `vendor.forge` | gear | Forge | ~650 terminal | Keep 75 crystals; add iron/wood early; XP all stages |
| `vendor.armorer` | gear | Blacksmith | ~650 | Keep 75 crystals; add `armor_knight_common` on terminal; iron mid; XP all |
| `vendor.lumbermill` | side | Thornwood / Pell | ~400 | Keep 50c+30f; add **wood** heavy; XP all |
| `vendor.granary` | side | Mill / Wren | ~400 | Keep food lines; XP all |
| `vendor.jeweler` | side | Sable | ~400 | Keep magic/crystals; XP all |
| `vendor.market` | side | Coppin | ~400 | Keep crystal ladder; XP all |
| `vendor.inn` | side | Inn | ~400 | Keep food/crystals; XP both |
| `vendor.stable` | side | Fenn / Echo | ~400 | Keep 50c; XP all |
| `vendor.steward` | main | Rebuild | ~900 terminal; weighted earlier | Keep 250c+50 magic; add wood/iron on wall/silo beats; XP all **4** stages (3 are EMPTY today) |
| `forgemasters_act1` | main | Four crafts | **~900** | **EMPTY today — must pay XP** (WO-1201 first target) |
| `forgemasters_act2` | main | Saga | ~900×1.25 chain | Keep existing; XP all 4 |
| `forgemasters_act3` | endgame | Saga | ~1600×1.5 | Keep 150c+50m; XP both |
| `forgemasters_act4` | endgame | Reforge | ~1600×1.75 | Keep 300c+100m; add rare accessory; XP |
| `petbond.*` (8) | side | Echo Hollow / biomes | ~400 terminal | Keep existing; XP all stages; wood/food/magic per §2.3 row |

---

## 4. Phase C — Make sure it works (verification)

1. **Mechanical parity:** for every stage, crystals/food/magic/grantItemId resolved from the list **≥** pre-migration baseline (additions allowed; reductions need RESULT note).  
2. **Unknown kind test:** synthetic kind → Fail line; catalog still loads.  
3. **XP curve oracle:** recompute §2.2; fail on silent divergence (overrides listed).  
4. **Grant smoke:** advance one stage in Editor/headless → FlowTrace grant lines for xp + resources + item; hero XP persists across save.  
5. **Board UI:** `RewardPartsFor` shows `XP N | …` on `forgemasters_act1` and a petbond.  
6. Dual-copy byte-identical; `QUEST_REACH_OK`; `COMPILE_GATE_OK`; brace/NUL gates on touched `.cs`.

---

## 5. Acceptance (CLI can close)

- [ ] Typed list live; both `quests.json` copies identical  
- [ ] All 63 stages migrated; no zeroed struct leftovers  
- [ ] XP on every stage (including former EMPTYs), derived not hand-scattered  
- [ ] Placement-themed wood/iron/food where §2.3 applies  
- [ ] Rare gear only from §2.4 table (or XP-only if id missing)  
- [ ] `forgemasters_act1` pays meaningful XP  
- [ ] Single grant seam; no second XP path; no save bump  
- [ ] Rumor board shows XP chip  
- [ ] UICaptureLaunch compiles  
- [ ] RESULT.md with parity proof + override list + one grant trace excerpt  

## 6. PO-owned (not CLI-closeable)

- Felt: “I pick quest A over B because of the reward slab”  
- UI capture PNGs of the redesigned reward panel  
- Whether daily slots should also pay XP (separate question)

## 7. Do NOT

- Implement `troop` grants  
- Touch packs / monetization / daily-quests  
- Hand-author 63 unrelated XP numbers  
- Grant legendary Aegis gear from quests  
- Special-case hide unfinished doors / Avalon copy  
- Strip FlowTrace  
- Edit only one `quests.json` copy  

---

## 8. Paste for CLI

```text
Implement WORK_ORDER_1202_quest_rewards_scaled_by_placement_and_difficulty.md.
Phase A = WO-1201 typed reward list + QuestRewardBridge xp/wood/iron + RumorBoardVM XP chip + UICaptureLaunch.
Phase B = migrate both quests.json copies; derive XP from type×chain×stageWeight; author placement resources; rare items only from §2.4.
Phase C = parity oracle + unknown-kind Fail + grant smoke. No save bump. No troop kind. RESULT with proof.
```
