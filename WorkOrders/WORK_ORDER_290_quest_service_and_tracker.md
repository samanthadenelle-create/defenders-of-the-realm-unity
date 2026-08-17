<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_290 — QuestService + quest tracker UI (questline backbone)

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** none — **FOUNDATIONAL, do early** (291/292/294/296/299/304 depend on it)

## Context
The vendor/forgemaster/pet questlines all need a shared quest backbone. Today there is `DailyQuestHud` and
tutorial flow, but no general quest state machine. Persistence should ride the wallet-keyed save (WO-301).

## Goal
A lightweight, data-driven `QuestService` (state machine) + a tracker UI, usable by Yarn commands.

## Files to edit / create
- New `Assets/_Modules/.../Quests/QuestService.cs` — `StartQuest(id)`, `AdvanceQuest(id)`, `CompleteQuest(id)`,
  `GetStage(id)`, flags (`SetFlag/HasFlag`), Keystone set (`GiveKeystone/HasKeystone/KeystoneCount`),
  `event QuestChanged`. State lives in GameState (wallet-keyed via WO-301).
- New quest data (`Assets/Data/Canonical/quests.json` or ScriptableObjects) — id, stages, objectives, rewards, gates.
- New quest tracker UI (code-built Canvas; reuse/extend `DailyQuestHud`) — current objectives + stages.
- Hook reward grants through `EconomyService` / `VillageInventory`.

## Acceptance criteria
- [ ] Can define a multi-stage quest in data and drive it: start → advance → complete with objectives + rewards.
- [ ] Flags + Keystones tracked and queryable; `KeystoneCount` works (for WO-292 finale gate).
- [ ] Tracker UI shows active objectives and updates on `QuestChanged`.
- [ ] State persists via GameState (wallet-keyed); survives relaunch.
- [ ] Code-built UI (no UXML in builds); brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't fork DailyQuestHud's data source — extend it. HUD→Core only for any HUD piece.
