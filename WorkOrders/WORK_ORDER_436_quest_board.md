<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 436 — Quest Board: a proper data-driven home for quests (repurpose the HUD "quest" click)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** 12 — Narrative / Onboarding / Quests (UI surface touches Lane 4)
**WO#:** 436 — confirmed free vs `CLI_LANES_WO_NUMBERS.md` (next-free=430; 430–435 minted by later sessions, this is the next slot after them). Slots into Lane 12 after WO-290/291/304.
**Editor must be CLOSED for any bake; this WO is code + one HUD-wire only, no scene hand-edit.**
**Owner trigger:** the HUD quest button is being repurposed into an **Upgrade** button, so quests need a real home — clicking "quests" should open a **Quest Board** that shows active/available quests *properly, from a model* (not a placeholder). Owner: quests "aren't part of any model and should be."

---

## Current state (verified from code, NOT comments — CLAUDE.md §0/MASTER_CATALOG rule)

There are **TWO parallel quest systems** in the project. The owner's "not part of any model" is **half true** — it is exactly right for what the HUD button currently opens, and wrong for the system she hasn't seen because it's buried behind Brom's dialogue.

### A. STORY quests — fully modeled, with a data-driven board that already exists
- **Model / runtime:** `Assets/_Modules/Core/Quests/QuestService.cs:25` — singleton, `RuntimeInitializeOnLoadMethod` bootstrap. Full lifecycle API: `StartQuest`/`AdvanceQuest`/`CompleteQuest`/`GetStage`/`ActiveQuestIds`/`IsActive`/`IsCompleted`/`SetFlag`/`GiveKeystone`. Raises `QuestChanged` (HUD repaint) + `RewardEarned` (Village bridge grants).
- **Content catalog:** `Assets/_Modules/Core/Quests/QuestCatalog.cs:68` — loads `StreamingAssets/Data/Canonical/quests.json` (WebGL-safe via `CanonicalJson`). DTOs `QuestDef`/`QuestStage`/`QuestReward` (`QuestCatalog.cs:29-63`).
- **Persistence — ALREADY in the model + save:** `Assets/_Modules/Core/State/NestedTypes.cs:197-230` — `QuestState` (`beatIndex`, `flags`, `stageId`) + `QuestProgress` (`active`/`completed`/`available`/`keystones`), persisted on `GameState.Quests`. `QuestService.Progress => GameStateService.Instance?.State?.Quests` (`QuestService.cs:63`) and every mutation calls `GameStateService.Save()`. **So story quests ARE part of the model and DO persist.** (WO-339 already bumped SaveSchema for this; additive, no further bump needed.)
- **The board already exists and IS data-driven:** `Assets/_Modules/Village/Hero/RumorBoardPanel.cs:41` — "Brom's Rumor Board". Code-built uGUI overlay (no UXML — player/WebGL safe), reads `QuestCatalog.Quests` + buckets Available / Active off the live `QuestService` ledger (`RumorBoardPanel.cs:164-200`), shows title + stage objective, has a working **ACCEPT → `QuestService.StartQuest(id)`** flow (`:344`), repaints on `QuestChanged`, and **already routes through `PanelManager`** (one-panel-at-a-time + WO-437 battle-lock, `:59/:136`).
- **The problem:** this board is **only reachable from Brom's Yarn dialogue** — the `OpenRumorBoard` command verb in `Assets/_Modules/Village/Tutorial/DialogueCommandBridge.cs:183` / `:1044-1066`. There is **no HUD entry point and no world board object**. Most players never see it.

### B. DAILY quests — what the HUD button actually opens (the "placeholder" the owner means)
- The HUD quest button wires to **DailyQuests**, not the story board: `Assets/_Modules/HUD/VillageHudController.cs:1413-1414` —
  `_questButton = BuildIconButton(... IconQuest, "!", () => DailyQuestHud.Instance?.Toggle());`
- `Assets/_Modules/HUD/DailyQuestHud.cs:45` `Toggle()` flips a **top-right chip stack** of 3 daily-quest chips (`DailyQuestService.Today.Quests`). Owner's WO-439 calls these the "thin lines top-right" that "read as unfinished."
- Dailies are a **separate ledger** (PlayerPrefs via `DailyQuestService`, NOT `GameState.Quests` — see `QuestService.cs:1-7` header). Functional but it's a transient HUD widget, not a board.

### Summary of state
| Thing | Exists? | Data-driven? | In the persisted model? | Reachable today |
|---|---|---|---|---|
| Story `QuestService` + `QuestProgress` (GameState) | ✅ | ✅ | ✅ `GameState.Quests` | API only |
| `QuestCatalog` (quests.json) | ✅ | ✅ | n/a (content) | — |
| `RumorBoardPanel` (story quest board) | ✅ | ✅ | reads model | **Brom Yarn only** |
| HUD "!" quest button | ✅ | — | — | → DailyQuestHud (top-right chips) |
| Daily quests | ✅ | ✅ | PlayerPrefs (separate) | top-right chip stack |

---

## The GAP (what "isn't part of any model")
The owner clicks the HUD quest button and gets the **daily-quest chip stack** (transient, top-right, reads as placeholder) — she never sees the **real, data-driven story quest board (`RumorBoardPanel`) that already binds the `QuestService`/`QuestCatalog` model**, because its only door is a line of Brom's dialogue. The model exists; **the entry point is wrong/missing.** This is an entry-point + presentation gap, **not** a missing data model.

Secondary gap: with the HUD button being repurposed to **Upgrade**, quests lose even the daily-chip door — so quests need their OWN reliable entry point.

---

## Proposed design (minimal, reuses everything; do NOT greenfield a model)

**Decision lens (what is right, not easy):** the model + a working data-driven board already exist. The right move is to give the existing board a HUD/world entry point and (optionally) fold dailies into it as a section — NOT to rebuild a quest model or a second board.

### 1. Route the board through `PanelRouter` (Core, reflection-free cross-assembly open)
`Assets/_Modules/Core/UI/PanelRouter.cs:37` `enum PanelId` — add `QuestBoard`. Then in `RumorBoardPanel.Open()` path, register its open action:
`PanelRouter.Register(PanelId.QuestBoard, () => /* find-or-create host */ .Open());`
(Mirror how other panels self-register; the existing `DialogueCommandBridge.OpenRumorBoard` find-or-create host logic at `DialogueCommandBridge.cs:1053-1056` is the template for the host lifecycle.) This gives ANY assembly a clean `PanelRouter.Open(PanelId.QuestBoard)` with no reflection and no Village↔HUD reference (CLAUDE.md §5).

### 2. Entry points (pick per "Open decision" — both are cheap)
- **HUD element (if a quest affordance remains after the Upgrade repurpose):** wherever the quest button lived (`VillageHudController.cs:1413`), if a quest entry is kept, change its callback from `DailyQuestHud.Instance?.Toggle()` to `PanelRouter.Open(PanelId.QuestBoard)`. **Per memory `never-dragdrop-or-manual-playtest` + WO-437: prefer opening via interaction, not a free hotkey.**
- **World board object (recommended primary):** a "Quest Board" interactable in the hub (a `BuildingInteractable`/board prop) whose interaction calls `PanelRouter.Open(PanelId.QuestBoard)` — the diegetic home. Keeps the existing Brom `OpenRumorBoard` Yarn verb working too (both doors hit the same panel).
- Keep the Brom Yarn `<<command: OpenRumorBoard>>` path (`DialogueCommandBridge.cs:183`) — it should call the same router/open so all doors are one panel.

### 3. Presentation — reuse `RumorBoardPanel`, optionally re-dress (do NOT rebuild)
- Minimum: ship `RumorBoardPanel` as-is behind the new entry point — it already groups Active / Available and is data-driven.
- Stretch (only if owner wants the Blink look): this is **exactly WO-439's scope** (Blink-styled grouped board via the MVVM `QuestVM`/`IQuestStore` seam). **See "Relationship to other WOs."** Don't duplicate 439 — if 439 is being done, 436 is just the entry-point + dailies-folding half.

### 4. (Optional, owner call) Fold DAILY quests into the board as a third section
Add a "Daily" section to the board reading `DailyQuestService.Today.Quests`, and **retire the top-right chip stack** (`DailyQuestHud` Toggle) so the board is the single home. This directly answers WO-439's "shouldn't show as thin lines top-right." If kept separate, leave dailies as-is and just re-point the button.

---

## Data model (already present — additive only, NO new model needed)
- `QuestDef { id, title, stages[] }`, `QuestStage { stageId, objectiveText, reward, requiresFlag, grantsKeystone }`, `QuestReward { crystals, food, magic, grantItemId }` — `QuestCatalog.cs:29-63`. Content authored in `Data/Canonical/quests.json`.
- `QuestState { beatIndex, flags, stageId }` + `QuestProgress { active, completed, available, keystones }` on `GameState.Quests` — `NestedTypes.cs:197-230`. **Persisted; no SaveSchema bump (WO-339 already covered it; all fields additive).**
- If the board needs a per-quest "giver" / "reward summary" line not in the DTO today, add `[JsonProperty]` fields to `QuestDef`/`QuestStage` **additively** (Newtonsoft ignores absent keys on old saves) — do not restructure.

---

## Acceptance criteria
- [ ] Compile gate green (`COMPILE_GATE_OK`); brace balance on every edited `.cs`.
- [ ] `PanelId.QuestBoard` exists; `RumorBoardPanel` registers + opens through `PanelRouter.Open(PanelId.QuestBoard)` (no reflection, no Village↔HUD direct ref).
- [ ] A reliable entry point opens the board: a world Quest Board interactable (and/or the re-pointed HUD quest affordance) — NOT only Brom's dialogue. Brom's existing `OpenRumorBoard` still opens the same panel.
- [ ] Board shows quests **from the model** (`QuestService` Active + `QuestCatalog` Available) — already true in `RumorBoardPanel`; verify ACCEPT still calls `StartQuest` and the row moves Active→In-Progress on `QuestChanged`.
- [ ] Routes through `PanelManager` — one-panel-at-a-time + WO-437 battle-lock (already wired at `RumorBoardPanel.cs:59/:136`; verify after re-route).
- [ ] (If §4 chosen) dailies appear as a board section and the top-right `DailyQuestHud` chip stack is removed/reduced to ≤1 hint line.
- [ ] Headless/fleet self-check: open the board via the new entry point, confirm ≥1 available quest renders from `quests.json` and accept persists to `GameState.Quests` across a reload (instrument with `FlowTrace.Step` per CLAUDE.md §12 — owner is never the detector).

## What NOT to touch
- Do **not** create a new quest data model or a second board — the model (`QuestService`/`QuestCatalog`/`QuestProgress`) and a data-driven board (`RumorBoardPanel`) already exist. Reuse them.
- Do **not** change `QuestService`/`QuestCatalog`/`QuestProgress` **logic** — additive JSON fields only if needed.
- Do **not** bump SaveSchema (WO-339 already did; all quest fields additive).
- Do **not** hand-edit any `.unity` scene; a world board object goes through the scene builder / a `BuildingInteractable` recipe, not manual scene surgery (CLAUDE.md §3, memory `never-dragdrop`).
- Do **not** restyle other panels. §0: CLI edits `.cs` on the Windows path only.

## Relationship to WO-290 / WO-304 / WO-339 / WO-439
- **WO-290 (QuestService + tracker UI):** BUILT — `QuestService.cs` is the model this WO surfaces. 436 does not re-spec it; it gives it a player door.
- **WO-304 (Brom's rumor board):** BUILT — `RumorBoardPanel.cs` IS the board. 436 = give it a HUD/world entry point (currently Brom-Yarn-only) + route via `PanelRouter`/`PanelManager`.
- **WO-339 (SaveSchema quest state):** DONE — `QuestProgress`/`QuestState` persist on `GameState.Quests`. No further schema work.
- **WO-439 (Blink-styled grouped quest board via `QuestVM`/`IQuestStore`):** **OVERLAPS — reconcile, don't duplicate.** 439 is the *re-skin + MVVM-seam* of this same board; 436 is the *entry-point + reachability + (optional) dailies-folding*. **Recommendation: merge — make 436 the umbrella ("quests get a real home"), with 439's Blink/MVVM work as its presentation phase.** Do the 436 entry-point/route first (cheap, unblocks reachability), then 439's dressing. Owner to confirm whether to fold 439 into 436 or keep 439 as the phase-2 ticket.

## Open decision (groom before READY)
1. **Entry point:** world Quest Board object, re-pointed HUD affordance, or both? (Recommend a world board as primary + keep Brom.)
2. **Dailies:** fold into the board as a section (retire top-right chips, satisfies WO-439's complaint) or leave separate?
3. **Skin:** ship `RumorBoardPanel` as-is now, or gate on WO-439's Blink/MVVM dressing? (Recommend: ship reachability now, dress in 439.)

*Cross-ref:* `RumorBoardPanel.cs`, `QuestService.cs`, `QuestCatalog.cs`, `NestedTypes.cs:197`, `PanelRouter.cs:37`, `VillageHudController.cs:1413`, `DialogueCommandBridge.cs:183/1044`, WO-290/291/304/339/439, memories `ui-mvvm-binding-seam`, `never-dragdrop-or-manual-playtest`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
