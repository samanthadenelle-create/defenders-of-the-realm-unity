> ⚠ **UNRESOLVED NUMBER COLLISION — WO-439 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_439_quest_board_collection.md` (06-17, first-on-disk), `WORK_ORDER_439_left_slide_panel.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WORK ORDER 439 — Quest board from the Blink pack (quests as a collection)

**Status: READY TO IMPLEMENT** (feature). Editor-closed. Owner: "quests grouped on a quest board from
the UI pack… that should be a collection so easy… shouldn't show as thin lines top-right."

## Problem
Quests currently surface as **thin lines in the top-right** (a `QuestTracker`/`DailyQuestHud` HUD widget)
— reads as unfinished. There IS a quest board (`RumorBoardPanel`, Brom's board — Available/Active from
`QuestService`), but it's not the Blink-styled grouped board the owner wants, and the top-right tracker
clutters the HUD.

## Approach — it's a collection, so it's the proven MVVM/collection pattern (cheap)
Quests are a **collection** (the One Model, §2b): `QuestService` + `QuestCatalog` are the model. Apply
the same seam the shop/inventory used:
1. **`QuestVM`** (`IPanelViewModel`, pure) — Data: `IReadOnlyList<QuestGroupVM>` grouped by state
   (Available / Active / Completed), each group a list of quest entries (title, giver, objective,
   progress, reward) as a value type (mirror `ItemVM`). Commands: `Accept(id)`, `Track(id)`, `Abandon(id)`,
   `Select(id)`, `Close`. Reads `QuestService`/`QuestCatalog` via a thin `IQuestStore` seam (mockable,
   like `IInventoryStore`); subscribes `QuestService.QuestChanged` → `Changed`.
2. **Quest board View** — reuse/replace `RumorBoardPanel` as an `IPanelView` binding `QuestVM`, dressed
   with the **Blink `QuestLog` / `QuestPanel`** art (`Assets/Blink/Art/UI/Obsidian_UI/Prefabs_Obsidian/
   QuestLog.prefab` / `QuestPanel.prefab`). Grouped sections (Available/Active/Completed) with the Blink
   panel + slot plates (reuse `slot_item` or import a quest-row sprite, flag-gated like WO-432). Route
   through `PanelManager` (and per WO-437, open via the board interactable / a single quest hotkey, not
   the thin-line widget).
3. **Retire the thin-line tracker** — remove or restyle the top-right `QuestTracker`/quest HUD lines.
   Keep a minimal "active quest" HUD hint if desired (one line, not a stack), but the BOARD is the home.

## Acceptance criteria
- [ ] Compile gate green; owner felt-test.
- [ ] A quest BOARD opens (via the board NPC/building or a single quest key) showing quests **grouped**
      Available/Active/Completed, dressed in Blink QuestLog art.
- [ ] Quests come from the model (`QuestService`/`QuestCatalog`) through `QuestVM` — no hardcoded list;
      board updates on quest state change.
- [ ] Accept/Track/Abandon work via the VM; routes through `PanelManager` (no stacking).
- [ ] The top-right thin-line tracker is gone (or reduced to a single unobtrusive hint).
- [ ] Ships with `QuestVMTests` (the §2c gate) — grouping, accept/track/abandon raise Changed.

## What NOT to touch
- Don't change `QuestService`/`QuestCatalog` logic — wrap behind `IQuestStore` (additive). Don't restyle
  other panels. §0: CLI edits on Windows path.

*Cross-ref:* `docs/UI_MVVM_BINDING_MAP.md §2/§5` (Quest row), WO-431/434 (the proven VM pattern),
WO-437 (open via interaction, not hotkey), panel audit (`RumorBoardPanel`, `DailyQuestHud`).
