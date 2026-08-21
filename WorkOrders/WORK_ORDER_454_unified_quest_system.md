**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **NUMBER COLLISION — this document does not own WO-454; `WORK_ORDER_454_faction_base_scene_generator.md` does.**
> Referred to hereafter as **WO-454-B (unified quest system)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK_ORDER_454 — Unified Quest System (board tabs + select-to-track + HUD pin)

**Status: READY TO IMPLEMENT**
*(WO number provisional — slot into `MASTER_PIPELINES_BACKLOG` per the numbering rule.)*

## Owner design (2026-06-20)
All quests — **daily / storyline / gear / endgame** — flow through **one quest UI**. Click
**Quests** in the HUD → board pops up → **you SELECT the quest you want to track** → close →
it **pins as the active quest on the far-right HUD slot**, *replacing* the standalone daily
widget. Tabs in the board (Daily / Story / Gear / Endgame) are **styling**, not separate
systems. Player picks the tracked quest (not an auto-default) so you control the slot.

## Architecture (the One Model, §2b — EXTEND, do not reinvent)
A quest is an **entry**; its **source/Type** is a *property*, not a separate class. The board,
the HUD pin, and persistence are all **readers**. The runtime already exists — we extend it:
add a Type, a Track action, a persisted tracked-id, and make the board a tabbed reader.
**Holistic work (§3): ships with tests (the permission gate, §2c).**

## Current system — verified file:line (extend THESE, greenfield nothing)
- `Assets/_Modules/Core/Quests/QuestService.cs` — story-quest runtime; persists to
  `GameState.Quests`; `QuestChanged` event; `StartQuest/AdvanceQuest/CompleteQuest/GetStage/
  ActiveQuestIds/IsActive/IsCompleted`.
- `Assets/_Modules/Core/Quests/QuestCatalog.cs` — `QuestDef{Id,Title,Stages[]}` (**no Type
  field yet**); loads `quests.json` via CanonicalJson (**Resources + StreamingAssets dual-copy**).
- `Assets/_Modules/Core/Quests/DailyQuests.cs` — `DailyQuestService`: 3 daily slots, PlayerPrefs,
  per-day roll, `Report(eventId, amount)`. Different shape from story chains — keep its runtime.
- `Assets/_Modules/Core/State/NestedTypes.cs:209` — `QuestProgress{Active,Available,Completed,
  Keystones}`. **Add `TrackedId` here** (the persistence seam).
- `Assets/_Modules/Village/Hero/RumorBoardPanel.cs` — the board reader (In Progress / Rumors,
  Accept→StartQuest). Gains tabs + a **Track** action.
- `Assets/_Modules/HUD/QuestTrackerHud.cs` — far-right HUD pin (interim: shows first-active).
  Becomes: pin the **tracked** quest.
- `Assets/_Modules/HUD/DailyQuestHud.cs` — standalone daily widget; its right-side role is
  superseded by the unified pin (daily DATA still flows via DailyQuestService).

## Phases — each independently COMPLETE + verified (no broken slices)

### Phase 1 — Select-to-track + HUD pin (story quests) — SMALL, visible
1. **Persist tracked id:** add `[JsonProperty("trackedId")] public string TrackedId;` to
   `QuestProgress` (`NestedTypes.cs`). Bump `SaveSchema.CurrentVersion` + add an append-only
   `SaveMigrator` step (mirror the last field added the same way). Append-only — never reorder.
2. **QuestService:** add `SetTracked(string id)` + `string TrackedId` getter (reads
   `GameState.Quests.TrackedId`); `Persist()` so it saves + raises `QuestChanged`. On
   `CompleteQuest`, if the completed id was tracked, clear it (fall back to none).
3. **RumorBoardPanel:** add a **Track** button per *active* quest row → `QuestService.SetTracked(id)`
   → close board. (On Accept, auto-track if nothing is tracked yet.)
4. **QuestTrackerHud:** pin the **tracked** quest (resolve `QuestService.TrackedId` →
   `QuestCatalog`/`GetStage`), not first-active. None tracked → show nothing. (The far-right
   anchor + single-card render from this session stay.)
5. **Acceptance:** open board → Track a quest → close → it shows far-right; survives reload;
   completing it clears the pin. **Tests:** `TrackedId` save round-trip (mirror existing save
   round-trip test); `SetTracked`/`CompleteQuest` clears tracked.

### Phase 2 — Board tabs + daily in the board — MEDIUM
1. **QuestDef:** add `[JsonProperty("type")] public string Type;` (default `"story"`; values
   `story|gear|endgame`). Keep `quests.json` Resources + StreamingAssets copies **in sync**.
2. **RumorBoardPanel:** tab strip (Story / Daily / …). Story/Gear/Endgame tabs read
   `QuestCatalog` by `Type`; **Daily tab reads `DailyQuestService`** (its slots rendered as
   quest rows). Track works across tabs — tracked ref carries **(Type, id)**.
3. **HUD pin** resolves the tracked quest by Type (story → QuestService/QuestCatalog; daily →
   DailyQuestService). Retire `DailyQuestHud`'s standalone right-side role (data stays).
4. **Tests:** board filter-by-Type; tracked-daily round-trip.

### Phase 3 — Gear / Endgame as quests — LATER
Add gear/crafting goals + endgame objectives as `quests.json` entries with `Type=gear|endgame`
(or a thin adapter). They surface under their tabs automatically (every system is a reader).

## Do NOT
- Do **not** merge the two runtimes — dailies stay in `DailyQuestService`; the **board is the
  unified reader**. - Do **not** edit only one `quests.json` copy (sync Resources +
  StreamingAssets). - Do **not** greenfield a new quest system. - Do **not** reorder save fields
  (append-only + migrator step).

## Landmines
- `quests.json` dual-copy sync. - `SaveSchema` version bump + migrator for `TrackedId`. -
  DailyQuestService is PlayerPrefs/per-day vs QuestService GameState — the tracked ref's **Type**
  selects the right source. - HUD is code-built UIElements (renders in player builds; only `.uxml`
  ASSETS fail — the tracker is code-built, fine).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
