# WORK ORDER 854 — Quest Completability Program

**Status: READY TO IMPLEMENT (Phases 0–2); PHASES 3–7 GATED ON OWNER RULINGS §6**
**Minted:** 2026-08-03 · **Type:** Program, 7 phases, file-disjoint silos
**Roles:** Architect specs (this doc) · CLI implements + batch-gates + sole-commits · Owner rules §6 · PO felt-closes
**Owner directive, verbatim:** *"Have an architect and team resolve quests and create all the missing
components as well as regression tests and repeat and test till working 100%."*

---

## 0. What this program is

The owner ruled (2026-08-03) that **a quest which can be accepted and tracked but not completed is a BUG,
not an unbuilt feature** — the game promises a completability it cannot keep. Commit `42b73d07` paid the
first half (the cast has bodies). This pays the rest.

Built around one idea: **"100% working" must be a number a machine prints, not a sentence an agent writes.**

## 1. GROUND TRUTH (given — do NOT re-derive)

| Fact | Proof |
|---|---|
| 0 of 63 stages / 24 quests completable | `quests.json` |
| `AdvanceQuest` has exactly ONE runtime caller | `QuestService.cs:108` <- `DialogueCommandSink.cs:121` |
| No shipped dialogue references any of the 24 quest ids | only quest verbs target `companion.sylas` (`dialogues.json:423,506`) |
| `companion.sylas` is not in `quests.json` -> unknown-id no-op | `QuestService.cs:92` |
| No reward has ever paid; no keystone ever minted | `QuestRewardBridge.cs:67-103` never invoked |
| `*QuestBridge` files feed `DailyQuestService` (separate ledger) | `DailyQuestCombatBridge.cs:37-45` |
| `QuestStage` has no speaker and no completion condition | `QuestCatalog.cs:40-47` |
| Every quest reachable + acceptable, no gate | `RumorBoardVM.cs:223-252` |
| Travel does not exist | `RealmMapVM.cs:203 TravelEnabled => false` |
| `forgemasters_act4` gates 5 legendary recipes | `gear-recipes.json:52,69,86,103,120` |

**Two findings the architect ADDED:**

1. **`QuestDef` has no prerequisite field.** Nothing enforces act1->act2->act3->act4. `forgemasters_act4`
   is startable on a fresh save before Act I. **Act ordering is currently fiction.** `gear-recipes.json`
   already ships the vocabulary (`requiresQuestId`); `QuestDef` does not.
2. **`QuestService.SetFlag` silently activates a non-active quest** at beat 0 (`:225-230`), bypassing the
   Available bookkeeping. A `SetQuestFlag` verb authored before `StartQuest` starts the quest by side effect.

## 2. THE ORACLE — the definition of "100%" (the spine)

**New (Silo R):** `Assets/Editor/Regression/QuestCompletabilityRegression.cs`
**Markers:** `QUEST_REACH_OK <n>/63 stages completable` / `QUEST_REACH_FAIL: ...`
Modelled on the two proven reachability oracles already in-tree — copy their shape, do not invent one:
`AegisSetReachabilityRegression.cs` (Run-out-reason covenant `:42`, honest-verdict discipline `:20-27`) and
`TutorialStepReachabilityRegression.cs` (emitter source-scan `:272-328`, comment-stripping lint `:676-681`).

Per the lane-fence convention (`ItemIdentityRegression.cs:53-55`) **the silo does NOT edit
`DataRegression.cs`** — the committer adds the wiring line at batch-gate.

### Cases

| # | Case | Assertion |
|---|---|---|
| 0 | `catalog-shape` | dual copies byte-identical; ids unique; every quest has >=1 stage |
| 1 | `entry-live` | every quest is enterable (board render or a `StartQuest` author) |
| 2 | **`advance-live`** | **THE SPINE** — every stage has a **distinct** advance path: a `completeOn` whose signal has a LIVE emitter, or a dialogue authoring `AdvanceQuest` in an openable dialogue |
| 3 | `speaker-embodied` | any dialogue named by Case 2 is playable; every speaker is a `speakers[]` record |
| 4 | `referent-resolves` | every proper noun resolves in shipped data (hard-fail on `grantItemId`, note-ledger for prose) |
| 5 | `reward-payable` | every non-zero reward has a live dispenser; `grantItemId` resolves |
| 6 | `terminal-consumer` | every `gear-recipes.json requiresQuestId` names a **completable** quest |
| 7 | `no-orphan-verbs` | every quest verb arg resolves to a real quest id (**`companion.sylas` fails today**) |
| 8 | `flag-satisfiable` | any `requiresFlag` has a matching emitter |

### Two traps the oracle MUST get right

**(a) Distinctness.** `AdvanceQuest` is ORDINAL (`QuestService.cs:119-146`) — it advances whatever stage is
current. One dialogue node re-opened four times technically "completes" a four-stage quest. A naive oracle
scores that 4/4. **Case 2 must prove a distinct source PER STAGE INDEX.** (Independently the strongest
argument for §4's schema: with `completeOn`, distinctness is structural.)

**(b) Latch poisoning.** `TutorialSignals` LATCHES (`:55-56,77-78`). A stage whose `completeOn` names an id
that already fired would complete the instant the quest is accepted. The bridge must
`TutorialSignals.Clear(awaitedId)` when a stage becomes current; **the oracle lints for that Clear call.**

### THE RATCHET — how "repeat till 100%" is enforced

```csharp
/// Stages proven completable as of the last SHIPPED phase. This only ever goes UP.
private const int MinCompletableStages = 0;   // P0 baseline
```
Always prints `<n>/63`. FAILS on backsliding or any hard case. **Every phase's acceptance criterion is:
raise the floor and still pass.** Without the ratchet either nothing ships until 63, or nothing is enforced.

### What EditMode CANNOT decide — state this in the file header

**The oracle proves a path EXISTS. It does not prove the runtime walks it.**

| Layer | Artifact | Proves |
|---|---|---|
| EditMode data/logic | `QuestCompletabilityRegression` | a reachable path exists for `<n>`/63 |
| EditMode unit | `Assets/Tests/EditMode/QuestCompletionTests.cs` | the matcher is correct (right stage, wrong-signal rejection, latch clear, idempotence) |
| PlayMode headless | `AssertStoryQuestAdvance` in `AutoPilotDriver.cs` | the real loop fires: Accept -> signal -> BeatIndex -> reward paid -> survives save round-trip |
| Felt (PO) | manual | the tracker points at the right person; the beat is satisfying |

Two hard constraints, honoured not fought: **`break-log` captures ERROR-LEVEL ONLY** so the assert must
`FlowTrace.Fail`; and **fleet coverage is HUB-capped**, so region-gated stages report `DEFERRED-NO-TRAVEL`,
**never** pass.

## 3. PHASES — ordered by leverage

**Two revisions to the audit's ordering, with reasons:**

**R1 — the oracle goes FIRST.** The directive is "repeat and test till 100%." The test must exist before the
repeats or every phase's "done" is an assertion. Read-only, file-disjoint, zero risk.

**R2 — the schema moves UP, ahead of bulk dialogue hand-wiring.** Doing the cheap thing first authors those
stages TWICE and leaves the oracle carrying two grammars forever. `completeOn {kind:"talk"}` rides the
existing `dialogue.ended:<id>` signal and needs no command in the dialogue at all, so the schema **subsumes**
the hand-wiring. Cheap-first is the easy path; contract-first is the right one (ARCHITECTURE_PRINCIPLES §0).
**But keep a thin slice of the audit's instinct:** the reward -> keystone -> wallet -> save path has never
executed, so Phase 1 hand-wires exactly ONE quest end-to-end as a proving line.

**REJECTED: a `StoryQuestCombatBridge` cloned from `DailyQuestCombatBridge`.** Right instinct, wrong seam.
`wave.cleared` is already on the signal bus (`TutorialSignalAdapters.cs:185-186`); a second component
subscribing `WaveManager.OnWaveCleared` would be a **second owner of one concern** — the exact double-stack
failure ARCHITECTURE_PRINCIPLES §2b.1 names as the VFX scar. Covered by `completeOn {kind:"wave"}`.

| P | Name | Ratchet floor | Gated on |
|---|---|---|---|
| **0** | The oracle + the ratchet | 0 -> 0 (honest baseline) | nothing |
| **1** | One quest end-to-end (`vendor.supply-run`) + `AssertStoryQuestAdvance` | 0 -> 1 | nothing |
| **2** | The completion contract (`completeOn`, `StoryQuestSignalBridge`, emitter hoist, Counters) | 1 -> 1 | nothing |
| **3** | Data-only truth pass (renames, `iron-sword`, orphan verb, `elarion.welcome`) | +2-4 | D6, D7, D4 |
| **4** | Hub-completable authoring | -> **~24** | D0 for felt-close |
| **5** | `forgemasters_act4` escalation | +1-2, unlocks 5 recipes | D8 |
| **6** | WO-827 travel | +4-6 alone; +~14 with rulings | — |
| **7** | Content builds (Stonebelly, Stable/Inn, 8 species) | remainder -> 63 | D1, D2, D3, D5 |

**P0/P1/P2 have ZERO owner dependencies and can start immediately.**

## 4. THE SCHEMA DECISION — decided

**Add `completeOn` to `QuestStage`.**

```jsonc
"completeOn": { "kind": "talk", "targetId": "village_elder", "count": 1 }
```

**Save schema: NO BUMP. v36 stands.** `QuestStage` is CATALOG content (`QuestCatalog.cs:40-47`), not the
persisted contract — the save side is `QuestState`/`QuestProgress` (`NestedTypes.cs:218-243`), where
`completeOn` never appears. Precedent is explicit in that file (`:224-226`, `:240-243`): *"Additive —
defaults to null on read of an older save, no version bump."* Bump `quests.json` `version` 2 -> 3 as content
hygiene; both dual copies stay byte-identical (Case 0 guards it).

**The one thing that could force a bump, pre-empted:** counting conditions need per-stage progress, and
`QuestState.Flags` is `Dictionary<string,bool>`. Add `Counters` (`Dictionary<string,int>`) — additive, also
no bump by the same precedent, but it IS a save-shape change and must gate with `SaveLoadRoundTripTest` +
`CoreSaveContractRegression`. **Land it in Phase 2** — retrofitting counters after content is authored is
the expensive order.

**Runtime:** one new Village bridge, ~120 lines. `StoryQuestSignalBridge` subscribes `TutorialSignals.Raised`
and compares against the active stage's composed signal. Core purity preserved — Core raises, Village
bridges — the `QuestRewardBridge` pattern.

**Bounded-context cost, named honestly:** the bus is called `TutorialSignals` but is already a GENERAL event
bus (dialogue-ended, wave-cleared, structure-placed, arena-resolved, echo-born). Quests consuming it does
not violate §1 — one owner of "something happened." A second bridge re-subscribing the same events WOULD.

**A real defect this exposes, fixed in P2:** the emitters only exist when `ff.tutorialv2` is ON in a
non-enemy-owned hub (`TutorialFlow.cs:361-366`). A quest bridge riding that bus silently inherits a tutorial
feature flag. **Hoist `TutorialSignalAdapters` into its own `RuntimeInitializeOnLoadMethod` bootstrap.**
⚠ `TutorialStepReachabilityRegression` source-lints `TutorialFlow.cs` (`:550-589`) — re-run it after.

**NOT in scope (own WO):** renaming `TutorialSignals` -> `GameSignals`. Right name, but smuggling a rename
into player-facing work violates ARCHITECTURE_PRINCIPLES §3.

**Dialogue hand-wiring is retained as a legal secondary route** (`kind:"dialogueCommand"`) for authored
one-off beats — not the primary mechanism. It cannot express "clear a wave", "place a mill", "bond a pet",
"clear a region" (the majority of the 63) without inventing a dialogue the player must find AFTER doing the
thing, which makes the objective text a lie.

### `kind` vocabulary (v1) — every kind maps to an EXISTING signal or ONE new Raise in the system that already owns the event

| kind | signal | emitter |
|---|---|---|
| `talk` | `dialogue.ended:<id>` | LIVE `TutorialSignals.cs:108-109` |
| `wave` | `wave.cleared` | LIVE `TutorialSignalAdapters.cs:185-186` |
| `build` | `build.structure_placed:<id>` | LIVE `:107-116` |
| `panel` | `panel.opened:<id>` | LIVE `TutorialSignals.cs:111-112` |
| `arena` | `arena.resolved:win` | LIVE `:188-189` |
| `reach` | `hero.reached:<id>` | LIVE TutorialFlow proximity probe |
| `pet` | `pet.bonded:<species>` | **NEW** one Raise in `PetAcquisitionService.Acquire` |
| `upgrade` | `structure.upgraded:<id>` | **NEW** one Raise in `BuildingUpgradeService` |
| `population` | `population.threshold:<n>` | **NEW** one Raise in the population service |
| `region` | `region.cleared:<id>` | **NEW** WO-827 |
| `flag` | quest flag via dialogue | LIVE `QuestService.SetFlag` |
| `dialogueCommand` | explicit `AdvanceQuest` | LIVE `DialogueCommandSink.cs:121` |

**Companion proposal (owner/committer call):** add `requiresQuestId` to `QuestDef`, mirroring the vocabulary
`gear-recipes.json` already ships. Without it act ordering is fiction (§1 finding 1).

## 5. FILE-DISJOINT SILOS

| Silo | Owns (exact paths) | Phases |
|---|---|---|
| **R Oracle** | `Assets/Editor/Regression/QuestCompletabilityRegression.cs` (new), `Assets/Tests/EditMode/QuestCompletionTests.cs` (new) | P0 |
| **S Schema/Runtime** | `Core/Quests/QuestCatalog.cs`, `Core/Quests/QuestService.cs`, `Core/State/NestedTypes.cs` (Counters only), `Village/Quests/StoryQuestSignalBridge.cs` (new) | P2 |
| **E Emitters** | `Village/Tutorial/V2/TutorialSignalAdapters.cs`, `Village/Tutorial/V2/TutorialFlow.cs` (hoist ONLY), `Pets/PetAcquisitionService.cs`, `Village/Buildings/Progression/BuildingUpgradeService.cs`, the population service | P2 |
| **C Quest content** | BOTH copies of `Data/Canonical/quests.json` | P1, P3, P4 |
| **D Dialogue content** | BOTH copies of `Data/Canonical/dialogue/dialogues.json` | P1, P3, P4 |
| **N New entities** | `enemies.json`, `enemy-roles.json`, `structures-catalog.json`, `pets.json` (both copies each) + `QuestCastNpcInjector.cs` (anchor field only) | P7 |
| **T Travel** | WO-827's file set | P6 |
| **A PlayMode** | `DevTools/AutoPilotDriver.cs` | P1 |

### Files two silos would both need — FLAGGED

1. **`DataRegression.cs`** — everyone wants a wiring line. **NOBODY edits it**; the committer adds it at
   batch-gate (convention already in-tree, `ItemIdentityRegression.cs:53-55`). Same serialization-bottleneck
   rule as `VillageSceneBuilder.cs` (CLAUDE.md §9).
2. **`quests.json`** — Silo S wants `completeOn`, Silo C wants prose fixes. **Silo C is sole writer**; S
   ships the DTO + a written field spec. Two agents rewriting a 617-line JSON in parallel is a guaranteed clobber.
3. **`NestedTypes.cs`** — Counters collides with any in-flight save work. **Serialize it**, gate with
   `SaveLoadRoundTripTest` + `CoreSaveContractRegression` green.
4. **`TutorialFlow.cs`** — the hoist touches the FTUE spine. **Isolated commit** + re-run
   `TutorialStepReachabilityRegression`.
5. **Every canonical JSON is DUAL COPY.** Both edited, always; oracle Case 0 guards it.
6. **`CLI_LANES_WO_NUMBERS.md`** — owner/committer only. No agent, ever.

## 6. THE MINIMAL OWNER DECISION SET — ranked

> # ✅ OWNER RULING 2026-08-04: **RETARGET ONTO WHAT SHIPS.**
> Verbatim: *"retarget onto what ships"* — in answer to the §7 fork (retarget vs build).
>
> **This resolves D1, D2, D3, D5, D6, D7 and D8 in one direction, and makes 63/63 reachable inside this
> program.** No new species, no new structures, no new enemy family, no sixth region, no new item.
> Quest content points at shipped content; where prose names something that does not exist, the PROSE
> moves, not the game.
>
> | Ruling | Resolved as |
> |---|---|
> | **D2** 8 pet species | RETARGET the 8 `petbond.*` lines onto the 3 shipped species (`ice-wolf`, `flame-pup`, `aether-sprite`). **26 stages unblocked.** Also answers voice-draft Q1: no second companion track is built, so Fenn trains what already exists. |
> | **D3** Stable / Inn / Granary | RETARGET onto shipped structures. `vendor.granary` already says "Mother Wren's mill" in its own text. Fenn's anchor stays at `pet-house` and is now CANON, not inferred. |
> | **D7** region names | RENAME prose to the shipped titles: Verdant Forest→The Thornwood, Frost Peaks→Hollowfrost Vale, Ashen Wastes→The Emberwastes, the Mire→The Mirewood. |
> | **D1** Stone Mountains | RETARGET onto a shipped region (no sixth region is authored). |
> | **D5** Stonebelly | RETARGET onto a shipped enemy family. |
> | **D6** `iron-sword` | RETARGET onto a shipped item id (`knight_iron` is the near neighbour). |
> | **D8** `forgemasters_act4` | RE-GATE the 5 legendary recipes onto a quest that IS completable — do not build the region chain. `forgemasters_act2` is all hub-completable talk beats. |
>
> **STILL OPEN — retarget does not answer these two:**
> - **D0** — approve or amend the voice draft (`docs/NARRATIVE/QUEST_CAST_VOICES_2026-08-03.md`). Blocks
>   PO felt-close, not mechanical completability. The oracle can score placeholder copy as completable.
> - **D4** — `elarion.welcome`: delete, or keep as the non-tutorial onboarding thread? The retarget spirit
>   argues against duplicating Sylas's shipped beat, but deleting a quest is its own content call.
>   **Silo C PROPOSES, the owner disposes — no agent deletes a quest.**
>
> **Ceiling under this ruling: 63/63 reachable**, gated only on WO-827 travel for the region stages and on
> D0 for felt-close. Where a stage's objective cannot be honestly retargeted, Silo C reports it rather than
> inventing a mechanic.

**Creative and scope calls. The architect deliberately chose NONE of them.** *(Superseded above for D1/D2/D3/D5/D6/D7/D8.)*

### The minimum FOUR

- **D2 — the 8 pet species: BUILD or RETARGET onto the 3 that ship? — 26 STAGES, the largest single block.**
  `pets.json` ships ice-wolf, flame-pup, aether-sprite. The 8 `petbond.*` lines + `vendor.stable` name
  Sproutling, Craghound, Frostkit, Emberpup, Mirewing, Glimmermoth, Stoneback, Aether Fox.
  **Entangled with the voice draft's Q1** — are Fenn's beasts Echoes (DESIGN-DECISIONS #21: verb is
  *attune*, keeper is the Echo Warden) or a second companion track? **Same decision; answering it answers both.**
- **D0 — approve or amend the quest-cast voice draft** — blocks the FELT-CLOSE of nearly every stage. Both
  new NPCs ship placeholder copy. The oracle can score a placeholder stage completable; **the PO cannot close it.**
- **D7 — region-name canon** — ~6 stages' truth plus every future region `completeOn`. Two different maps
  are shipping. One ruling, pure data.
- **D3 — Stable / Inn / Granary: build or retarget?** — 5 stages + Fenn's address. `vendor.granary` already
  says "Mother Wren's mill" in its own text, so it likely needs retarget only.

### Second wave

- **D1 — Stone Mountains: retarget or author a sixth region?** 2 stages directly, but it ripples act3 -> act4
  -> the 5 legendary recipes. *Reporting only:* a live `RegionId` named **Stoneback** already exists at
  danger tier 2 (`CrystalGrade.cs:49-57`) — a zero-cost retarget candidate.
- **D8 — `forgemasters_act4` re-gate (ESCALATED).** `aegis_plate` + four `aegis_*` weapons are unobtainable
  **by construction**. ⚠ *A gap between two oracles:* `AegisSetReachabilityRegression` PASSES today — it
  proves the set is co-EQUIPPABLE, not ACQUIRABLE. Case 6 closes that gap. Options: build the chain (needs
  827 + D1); re-gate the recipes on `forgemasters_act2` (4 stages, **all hub-completable today**); or source
  the components from crafting instead of travel.
- **D5 — Stonebelly: author, rename, or drop?** 2 stages. Shipped candidates: `hollow-brute`, `troll`,
  `ogre`, `orc-raider`.
- **D6 — `iron-sword`** (`quests.json:40`): retarget to `knight_iron` or author it. **Hard FAIL in Case 5.**
- **D4 — `elarion.welcome`: delete or hook?** The audit says delete (Sylas's shipped tutorial duplicates the
  beat AND pays out). **Cost the audit did not price:** the Village Elder body shipped in `42b73d07` exists
  SOLELY for `meet-elder`; deleting orphans him. Two coherent resolutions: (a) delete and repurpose the Elder
  as the `vendor.steward` speaker — which also answers voice-draft Q3 (is the Elder Warden Alric?); or
  (b) keep it as the non-tutorial onboarding thread. **Either way two mouths on one beat is a content bug.**
- **D9 — the Spire framing** — 2 stages point at an object the 2026-06-26 reversal superseded.

## 7. THE HONEST CEILING

**Nobody should promise 100% out of this program.**

- **Without any ruling and without WO-827: ~24 of 63 (38%)**, band 22-26.
  Clean wins today: **`forgemasters_act2` — all 4 stages** (hearth/talk beats among NPCs that now have
  bodies; the highest-value single quest in the program) · `forgemasters_act1` (all four Forgemasters stand
  in the hub after `42b73d07`) · `vendor.granary`, `vendor.jeweler` (3 each, pure hub) · `vendor.supply-run`.
- **With WO-827 but no rulings: ~28-30 (46%).** Travel alone adds less than its 14-stage headline, because
  most region stages ALSO name a missing species or enemy.

### The lever, stated plainly — the two directions are NOT symmetric

- **RETARGET** (8 pet lines onto the 3 shipped species; Stable/Inn quests onto existing buildings; region
  prose onto shipped titles; Stonebelly onto a shipped family) -> **63/63 becomes reachable inside this
  program**, mostly data-only, no new art, only partly dependent on 827.
- **BUILD** -> 63/63 requires 8 species + 2-3 structures + an enemy family + travel, and **this program
  cannot deliver 100%** — it delivers ~24/63 plus a machine-checked ledger of exactly what remains and why.

Both are legitimate. The first is faster; the second is richer. **This is the single decision that
determines whether "100%" is a promise this program can keep.**

## 8. ACCEPTANCE (per phase)

- [ ] `COMPILE_GATE_OK`
- [ ] `REGRESSION_OK <n>/<n> suites` — read off the marker, never restated (CLAUDE.md §8)
- [ ] `QUEST_REACH_OK <n>/63`, with `MinCompletableStages` raised in the same commit
- [ ] EditMode NUnit green
- [ ] P1+: `AssertStoryQuestAdvance` green headless
- [ ] RESULT file carrying the **verbatim proving line** for each claim

**Program-complete:** `QUEST_REACH_OK 63/63` **and** PO felt-close on every stage. The first is a machine's
job; the second is the owner's, and no marker substitutes for it.

## 9. WHAT NOT TO TOUCH

- `CLI_LANES_WO_NUMBERS.md` — committer only, same edit as a mint.
- `DataRegression.cs` — lane-fenced; committer wires at batch-gate.
- Any `.unity` scene — the cast is seated by runtime injectors for exactly this reason (CLAUDE.md §3).
- `DailyQuestService` and every `Daily*Bridge` — separate ledger, out of scope, do not "unify".
- Renaming `TutorialSignals` -> `GameSignals` — right idea, wrong WO.
- The six characters' placeholder copy — blocked on D0. Inventing voice is inventing canon.
- `waves.json` `enemies[]` batches — inert by ruling; a re-add fails a test.

## APPENDIX A — per-stage ledger

Legend: ✔ completable with no ruling and no travel · 827 needs travel · D# needs that ruling · ? judgement

| Quest | Stages | Verdict |
|---|---|---|
| `elarion.welcome` :5 | meet-elder, first-defense | ✔? ✔? both at risk of **D4** |
| `forgemaster.first-commission` :26 | gather-iron, claim-weapon | ✔ · **D6** |
| `vendor.supply-run` :47 | talk-vendor | ✔ **P1 proving line** |
| `vendor.forge` :62 | relight, quench, field-test | ✔ · ✔ · **D5** |
| `vendor.armorer` :90 | salvage, reforge, hold-line | ? (WO-453) · ✔ · ✔ |
| `vendor.lumbermill` :118 | grove, sapling, defend | **827+D7** · ? · ✔ |
| `vendor.granary` :146 | mill, population, workers | ✔ ✔ ✔ |
| `vendor.jeweler` :174 | spawn, heartgem, broker | ✔ ✔ ✔ |
| `vendor.market` :202 | road, route, network | **827** · ? · ✔? |
| `vendor.inn` :230 | defend, rally | **D3** **D3** |
| `vendor.stable` :251 | tame, train, work | **D1/D2/827** · ✔ · ✔ |
| `vendor.steward` :279 | tier2, walls, wards, spire | **D3** · **D5** · **D2** · **D9** |
| `forgemasters_act1` :315 | gather-the-four | ✔ |
| `forgemasters_act2` :329 | ×4 | ✔✔✔✔ **cleanest quest in the game** |
| `forgemasters_act3` :364 | components, all-gathered | **827+D1** · blocked ordinally |
| `forgemasters_act4` :385 | the-choice | ✔ mechanically — see **D8** |
| `petbond.*` ×8 | 23 stages | **D2** (+D1/D5/D9/827 variously) |

**Totals: ✔ ~24 · 827-gated ~6 · D2-gated 26 · other-ruling ~7.**

## APPENDIX B — stage-authoring template

```jsonc
{
  "stageId": "restore-mill",
  "objectiveText": "Restore food flow: build or upgrade Mother Wren's mill.",
  "reward": { "crystals": 0, "food": 40, "magic": 0, "grantItemId": "" },
  "requiresFlag": "",
  "grantsKeystone": false,
  "completeOn": { "kind": "build", "targetId": "mill", "count": 1 }
}
```

Oracle-enforced authoring rules: `targetId` resolves in the catalog its `kind` implies (Case 4) · no two
stages of one quest share a `completeOn` (Case 2 distinctness) · `objectiveText` names no non-existent
referent (Case 4) · `grantItemId` resolves (Case 5, hard fail).
