# WORK ORDER 1201 - quests pay experience

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1201 -> 1202 in the same edit)
**Silo:** Progression / Quests
**Ruling:** owner, 2026-08-25.

---

> *"add xp to quest rewards"*
> *"don't forget EXP amounts can drive those too"*
> *"we could add quests to give new offensive and defensive troops"*
> *"half the games I've played, that's the main reward."*

## The ruling

**Quests pay experience.** And because the owner named the second and third reward kinds in the
same breath as the first, she also ruled the SHAPE: **a quest reward becomes a TYPED LIST, not
another struct field.**

    "reward": [
      { "kind": "xp",       "amount": 500 },
      { "kind": "troop",    "id": "pikeman" },
      { "kind": "crystals", "amount": 220 }
    ]

⚠ **Say it plainly: the migration is the larger half of this ticket.** This is not "add an `xp`
field." It is a reward-schema migration across all 63 authored stages, with `xp` as its first new
kind. Context doc: `docs/QUEST_REWARD_DIRECTIONS.md`.

## Why it matters right now

The owner is redesigning the quest board so the largest panel element is the **REWARD**, on the
grounds that the reward is what decides which quest you take. The renderer for that slab already
exists - `RumorBoardVM.RewardPartsFor` (`Assets/_Modules/Village/Hero/RumorBoardVM.cs:152`),
consumed at `RumorBoardPanel.cs:1253`. Today it can emit only `Crystals N | Food N | Magic N | <item>`.

⛔ **XP would render as nothing, for every quest in the game**, on the one screen being rebuilt
around it. And *"half the games I've played, that's the main reward"* means XP is a **PRIMARY**
reward, not a garnish: the values must be authored at a scale where a player picks quest A over
quest B because of the XP. **A token value satisfies the schema and fails the design.**

---

## ANSWER TO THE LOAD-BEARING QUESTION: YES, HERO XP IS SAVE-PERSISTED

Verified at source this session, and it is the answer that keeps this ticket READY rather than
blocked.

- The sole `IXpEarner` implementer is **`HeroProgression`**
  (`Assets/_Modules/Village/Hero/HeroProgression.cs`). `XpEarnerRegistry`'s own header and
  `FeatureFlags.cs:1059` both state it: PetProgression was retired with the physical pet stack, so
  **nothing else registers as an earner**.
- It **restores** from the save on enable - `RestoreFromSave()` reads `GameState.HeroLevel /
  HeroXp / HeroLifetimeXp` and never downgrades a live higher level.
- It **writes back on every XP change** - `WriteBackToState()` is called from inside `AddXp`, and
  the spine's existing save moments flush it.
- Those three fields have been persisted since **schema v29** (`GameState.cs:415-421`;
  `SaveSchema.cs:479/485/491`; `SaveMigrator.cs:442-444`).

### ⛔ Therefore: DO NOT BUMP THE SAVE SCHEMA FOR THE XP ITSELF.

`SaveSchema.CurrentVersion` is **38** (`Assets/_Modules/Core/State/SaveSchema.cs:41`) and stays 38
for this work. Nothing new is persisted: quest reward definitions are **catalog data**, not save
state, and hero XP already round-trips. The persisted quest ledger (`GameState.Quests`) records
which stage you are on, never what it paid you.

⚠ But say the other half out loud: **this IS a progression change on a LIVE build with an
activated pay path.** XP feeds level -> Wisdom -> the talent tree
(`HeroProgression.ApplyLevelRewards`), and Wisdom is deliberately scarce (WO-763 removed the
per-wave, arena and daily-quest Wisdom faucets so skills feel earned). Quest XP is a **new Wisdom
faucet by transitivity**. Author against the curve, not against vibes - see the authoring rule.

---

## THE SINGLE PAYOUT SEAM

There is exactly one, and it is already clean. ⛔ Do not add a second.

`Assets/_Modules/Core/Quests/QuestService.cs:128-131` - Core raises, never grants:

    if (leaving.Reward != null)
    {
        DeNelle.Core.Diagnostics.FlowTrace.Step("Quest", $"reward earned on '{id}' beat {st.BeatIndex} ...");
        RewardEarned?.Invoke(leaving.Reward);
    }

`Assets/_Modules/Village/Quests/QuestRewardBridge.cs:67` - Village dispenses:

    private void OnRewardEarned(QuestReward reward)

with crystals+food at `:73`, magic at `:77`, item grant at `:90`. **XP is granted HERE and nowhere
else** - one more branch in `OnRewardEarned`, not a new listener, not a new bridge, not a call from
`QuestService`.

⚠ Note the seam fires on the stage being **LEFT** (`AdvanceQuest`), and final-stage completion
routes through `CompleteQuest` (`QuestService.cs:140`) *after* the reward has already been raised.
That is the existing contract; XP inherits it unchanged.

### The asmdef question is answered: there is no architecture problem

- `QuestRewardBridge` is `DeNelle.Village`, whose `.asmdef` references `DeNelle.Core` (checked at
  source, not assumed).
- `HeroProgression` is **in `DeNelle.Village` itself**, and `XpEarnerRegistry` is in `DeNelle.Core`.
- So the bridge can reach the earner **directly**, with no reflection and no new reference.

⭐ **Resolve via `XpEarnerRegistry.TryGet(HeroProgression.Id)`, not via
`HeroProgression.Instance`.** The registry is the sanctioned seam, it is what survives the
bootstrap-instance takeover documented in `HeroProgression.Awake`, and it is the join point for any
future earner. `AddXp` returns the levels gained - use it for feedback, and ⛔ Guard the call: an
earner that is not up yet must log via `FlowTrace.Fail` and never silently swallow the player's XP
(CLAUDE.md sec.12; the same defect class WO-977/978 already paid for twice in this exact area).

⛔ **Core purity holds.** `QuestReward` gains reward DATA only. Core still raises numbers; Village
still grants them.

---

## ⭐⭐ THE AUTHORING RULE - DERIVE, DO NOT HAND-AUTHOR 63 NUMBERS

⛔ **Do not hand-author 63 independent XP values.** This repo's single dominant failure is a
hand-maintained table that drifts, and it has produced, in order: a stale WO-number block in
CLAUDE.md sec.2, a retired dependency table in sec.5, a `UtcDay` ledger wrong about its own call
sites, and a cost formatter written thirteen times. Sixty-three hand-typed numbers is that failure
pre-committed.

⭐ **Derive the XP from what the quest already declares.** The inputs exist in `quests.json` today:

| input | present as | measured |
|---|---|---|
| tier / weight | `type` | 4 `main`, 15 `side`, 3 `gear`, 2 `endgame` |
| chain depth | `requiresQuestId` | walkable prerequisite chain, already honoured by `RumorBoardVM` |
| length | `stages[]` count | 1 to 4 stages per quest, **63 stages over 24 quests** |
| terminal beat | last stage index | the stage that currently carries the payout |

A curve of the shape `xp = base(type) * stageWeight(index, count)` covers every stage from data the
author already writes. **A per-stage override is allowed only where the curve is wrong**, and each
override should be visibly an override.

### Anchor the scale to the real level curve, or the number is decoration

`HeroProgression.XpToNextFor` is `150 + (L-1)*350 + (L-1)^2*500`:

| level step | XP required |
|---|---|
| 1 -> 2 | 150 |
| 2 -> 3 | 1,000 |
| 3 -> 4 | 2,850 |
| 4 -> 5 | 5,700 |

⭐ So a main-quest chapter must be worth **hundreds to low thousands** to read as a primary reward.
A 25-XP quest is a rounding error against a wave and would prove the owner's "token value" warning
right on the first screen she looks at.

### ⛔ An oracle must assert the curve, or it drifts

Whatever derivation is chosen, `QuestCompletabilityRegression`
(`Assets/Editor/Regression/QuestCompletabilityRegression.cs`, registered once at
`DataRegression.cs:510`, marker `QUEST_REACH_OK`) gains a case that **recomputes the curve and
compares it to the authored values**, listing every override explicitly. A value that silently
diverges from the curve fails the suite. ⛔ Add it to the EXISTING suite - there is exactly one
registration line and a second one double-counts failures (the file's own header says so).

---

## THE MIGRATION - fixed struct to typed list

### What exists today

`QuestCatalog.cs:30-36`:

    public sealed class QuestReward
    {
        [JsonProperty("crystals")] public int Crystals;
        [JsonProperty("food")] public int Food;
        [JsonProperty("magic")] public int Magic;
        [JsonProperty("grantItemId")] public string GrantItemId;
    }

⚠ **Correction to the brief:** `grantsKeystone` is **not** on the reward - it is a sibling field on
the STAGE (`QuestService.cs:127`). It is not part of this migration.

### ⛔ THE DECIDING ARGUMENT - a widening struct fails SILENTLY

This is not a hypothetical. It happened **in this repo, today**:

- `packs.json` authored a `stone` key.
- `PackCatalog.cs:64` bound only the fields it knew (`crystals`, `food`, `coins`, `wood`, `iron`).
- Newtonsoft dropped the unknown key at the deserialize call, `PackCatalog.cs:654`.
- **Three LIVE impulse SKUs would have granted literally nothing.** No exception. No log. No red
  test. Indistinguishable from a correct parse. (WO-1163, bounced 2026-08-25.)

⭐ **A typed list makes an unknown `kind` FAIL LOUD instead of vanishing. That property is the whole
point of the change** - it is the only reason the migration is worth its cost.

### ⛔ Unknown-kind behaviour is a REQUIREMENT, not a nicety

An unrecognised `kind` must **FAIL LOUD** per CLAUDE.md sec.12:

- `FlowTrace.Fail("Quest", ...)` naming the quest id, stage id and the unknown kind verbatim.
- The parse of the surrounding catalog is `Guard.TryEach`-shaped: one bad entry logs and is
  skipped; it never blanks the catalog and it is never silent.
- ⛔ **Silently ignoring an unrecognised kind reintroduces the exact bug this shape exists to
  prevent, and makes the entire migration pointless.**

⛔ The oracle asserts this too: feed it a synthetic unknown kind and require the failure. A
loud-failure requirement with no test is a comment.

### ⭐ Round-trip parity is the single most important acceptance item

**No quest may pay differently after the migration than before.** Assert it **mechanically over all
63 stages**, ⛔ not by spot-check: for every stage, the crystals / food / magic / item the list form
resolves to must equal what the struct form resolved to.

The measured baseline to preserve (read from `quests.json` this session):

- **24 quests, 63 stages.**
- **33 of 63 stages currently pay NOTHING at all** (an all-zero reward object). ⭐ Those 33 are the
  best argument for this ticket: a quest beat that pays nothing today can pay progression tomorrow
  without inventing a new currency.
- Reward keys in use across the whole file: `crystals`, `food`, `magic`, `grantItemId`. No others.

### `kind: "troop"` is OUT OF SCOPE

⛔ Do not implement it. But the shape must obviously accommodate it - `{kind, id}` alongside
`{kind, amount}` - because that accommodation is the justification for the migration's cost. Say so
in the code comment so the next seat does not re-litigate the schema.

---

## ⚠ THE MIRROR LAW - and every surface that reads a quest reward

**Both canonical copies of `quests.json` move together, atomically, in the same edit**, or the
build is red. They are byte-identical today at **35,402 bytes**:

- `Assets/StreamingAssets/Data/Canonical/quests.json`
- `Assets/Resources/Data/Canonical/quests.json`

⚠ `CanonicalJson` reads **Resources FIRST**, so an edit made only in StreamingAssets is invisible to
the shipped player. `QuestCompletabilityRegression` Case 0 `[catalog-shape]` already asserts the two
are byte-identical - that guard is live and must stay green through the migration.

⭐ **WO-1163 established that a mirror law naming only what your tests can reach is not a law.** So
here is every surface that reads a quest reward, named:

| surface | file:line | what it does | migration duty |
|---|---|---|---|
| the DTO | `Core/Quests/QuestCatalog.cs:30-36` | `QuestReward` struct bound by Newtonsoft | ⛔ **THE silent-drop risk.** Becomes the typed list. |
| stage binding | `Core/Quests/QuestCatalog.cs:181` | `[JsonProperty("reward")] public QuestReward Reward` | type changes with the DTO |
| the raise | `Core/Quests/QuestService.cs:128-131` | raises `RewardEarned` | payload type changes; the FlowTrace line at `:130` names the four fixed fields and must be rewritten to enumerate the list |
| the event | `Core/Quests/QuestService.cs:37` | `event Action<QuestReward> RewardEarned` | signature follows the DTO |
| the dispenser | `Village/Quests/QuestRewardBridge.cs:67-101` | the ONE payout path | switches on `kind`; gains the `xp` branch |
| the board VM | `Village/Hero/RumorBoardVM.cs:152-179` | `RewardPartsFor` / `RewardFor` build the display chips | must emit an XP chip, and must not silently drop kinds it cannot render |
| the board panel | `Village/Hero/RumorBoardPanel.cs:1253` | consumes `RewardPartsFor` | the slab the owner is redesigning |
| the capture fixture | `Editor/UICaptureLaunch.cs:2374` | builds a synthetic `QuestReward` for the rumor-board worst-case screenshot | ⚠ **compiles against the struct today** - it breaks on the type change and must be migrated in the same pass, or the UI capture gate goes red |
| the oracle | `Editor/Regression/QuestCompletabilityRegression.cs` | Case 0 shape, Case 5 `[reward-payable]` | Case 5 greps the bridge source for `RewardEarned` - keep it satisfied; add the new cases here |

⛔ **`Editor/UICaptureLaunch.cs:2374` is the sleeper.** It is the only non-obvious compile-time
consumer of `QuestReward` and it lives in the Editor assembly, so a seat migrating "the runtime"
will miss it and find out at the capture gate.

---

## Q4 ANSWERED: DAILY QUESTS DO **NOT** SHARE THE REWARD SHAPE, AND ARE OUT OF SCOPE

Verified at source. `daily-quests.json` is a different design and the brief's framing does not hold:

- The **38 templates carry NO reward at all.** Template keys are exactly `id`, `slot`, `target`,
  `label`, `weight`, `requiresFeature`, `day1Guaranteed`.
- The reward lives on **3 SLOT rows** (`combat`, `exploration`, `wildcard`) in a completely separate
  flat DTO - `DailyQuestSlotReward` (`Core/Quests/DailyQuests.cs:48-55`):
  `rewardCrystals`, `rewardFood`, `rewardWisdom`, `rewardRandomItem`.
- It has its own dispenser, `Village/Quests/DailyQuestRewardBridge.cs:115`, and its own ledger
  (dailies persist to PlayerPrefs; story quests persist to `GameState.Quests`).

⛔ **This migration does not touch `daily-quests.json`.** Whether dailies should also pay XP is a
separate, much smaller owner question - **3 slot rows, not 38 templates** - and it is deliberately
NOT decided here. ⚠ Flag for the PO while she is looking: those slot rows still author
`rewardWisdom: 1` on `combat` and `wildcard`, which reads as a surviving faucet against WO-763's
ruling that daily-quest Wisdom was removed. Worth one look; ⛔ not this ticket's job to change.

---

## ⭐ FIRST AUTHORING TARGET: quest 3, `forgemasters_act1` ("Honest Steel")

    "id": "forgemasters_act1", "type": "main", "title": "Honest Steel"
    stages: 1  ->  reward { crystals: 0, food: 0, magic: 0, grantItemId: "" }

A **main-line** quest, one stage, whose objective is to meet the four crafts of Elarion and hear
Borin Emberhand tell the legend of the broken Aegis - and it pays **absolutely nothing**. The board
renders an empty reward slab for it.

⭐ It is the natural first target, and the cleanest demonstration of why XP belongs in the game: **a
quest whose payoff is progression can finally pay in the currency of progression.** Author it as
main-line tier at the anchored scale, not as a token.

---

## Acceptance - what a dev lane can close on its own

1. `QuestReward` is a typed list; `{kind, amount}` and `{kind, id}` both bind.
2. All **63 stages** in **both** canonical `quests.json` copies are in list form, and the two files
   are still **byte-identical**.
3. ⭐ **Round-trip parity proven mechanically over all 63 stages** - every stage resolves to the
   same crystals / food / magic / item it resolved to before. ⛔ Not a spot-check.
4. An unknown `kind` produces a `FlowTrace.Fail` naming quest, stage and kind, skips only that
   entry, and is covered by a test that feeds it one.
5. XP is granted in exactly ONE place - the `xp` branch of `QuestRewardBridge.OnRewardEarned` -
   resolved through `XpEarnerRegistry.TryGet(HeroProgression.Id)` and Guarded.
6. XP values are DERIVED from `type` / stage index / stage count, with overrides visibly marked,
   and an oracle case recomputes the curve and fails on silent divergence.
7. `forgemasters_act1` pays XP.
8. `RumorBoardVM.RewardPartsFor` emits an XP chip, ASCII-only.
9. `Editor/UICaptureLaunch.cs:2374` compiles against the new shape.
10. `COMPILE_GATE_OK` on a fresh log; `QUEST_REACH_OK` green; `REGRESSION_OK <n>/<n> suites`.

## ⛔ OPS-OWNED - not closeable by the dev lane

- Any **Unity batchmode run**: the compile gate, `DataRegression.RunAll`, `QUEST_REACH_OK`.
- **`UI_CAPTURE_OK` and opening the PNGs.** The reward slab is a visual change on the panel the
  owner is redesigning; compile-green never proves a panel looks right.
- **Felt-verification and CLOSE are the PO's.** Only she can judge whether the XP number reads as a
  reason to pick quest A over quest B - which is the entire design intent. Headless cannot judge it.
- Any judgement that a derived value is *wrong for that quest* and needs an override.

## ⛔ DO NOT

- ⛔ Bump `SaveSchema.CurrentVersion` (38). Nothing new is persisted. If a later decision makes a
  bump necessary, **say so explicitly and stop** - never bump silently.
- ⛔ Add a second XP grant path. One branch, one bridge, one seam.
- ⛔ Hand-author 63 independent XP numbers.
- ⛔ Author values that outpace the level curve, or token values that undercut it.
- ⛔ Implement `kind: "troop"`.
- ⛔ Touch settlement, pricing, `packs.json`, or any monetization surface.
- ⛔ Touch `daily-quests.json` or the daily reward bridge.
- ⛔ Silently ignore an unrecognised `kind`.
- ⛔ Edit only one copy of `quests.json`.
- ⛔ Strip or disable any FlowTrace line in this path (CLAUDE.md sec.12).
