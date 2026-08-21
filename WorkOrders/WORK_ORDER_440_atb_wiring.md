**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-440 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_440_atb_wiring.md` (06-17, first-on-disk), `WORK_ORDER_440_resources_collapse_right.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WORK ORDER 440 — ATB combat: finish the 2 wiring gaps (polish the fight)

**Status: READY TO IMPLEMENT.** Editor-closed (gate). Owner: "refine ATB — nothing really touches it,
should be simple wiring" + "HUD looks polished, I want combat to feel polished too." RCA (read-only,
2026-06-17) confirmed: ATB is mostly wired — only **2** real gaps. Everything else verified WIRED
(AtbCombatantSwapper auto-hooks 3D models on load; per-class ability icons mapped; hit/cast/death anims
+ floating damage; battle entry has a fallback roster + idle timeout — no empty-state).

## Gap 1 — Item button is a dead handler
`BattleATB/BattleHudUgui.cs:430` — `OnItemClicked()` is an empty `/* populate item submenu */` stub.
The Skills button works; Item was never finished, so clicking it does nothing.
**Fix:** mirror `OnSkillsClicked()`/the Skills submenu — create an `_itemsSubPanel` in
`CreateCommandPanel()`, populate it in `OnItemClicked()` from `state.Inventory` (Potions / ManaCrystals /
Cleanses), route clicks to `SubmitAction(BattleAction.MakeItem(itemKind))`. The `_pendingItem` field
(`:157`) already exists for this. Files: `BattleHudUgui.cs:157,335,430`.

## Gap 2 — Battle log is never displayed ("what happened" silence)
The engine generates user-facing log events (BattleStart, TurnStart, Attack, Ability, Death, Victory,
Defeat) — `Engine/BattleState.cs:299` — and `BattleController` even tracks a cursor
(`_lastProcessedLogIndex`), but **no HUD element shows the log**. So turns pass silently → the "what
happened" confusion.
**Fix:** add a small scrollable log panel to `BattleHudUgui` (3-4 lines, bottom-centre, classic ATB
spot). On each `Render()`, append new entries (index ≥ `_lastProcessedLogIndex`), filtered to
user-facing events (skip StatusTick/Skip). Files: `BattleHudUgui.cs` (new panel + Render append).

## Acceptance
- [ ] Compile gate green; owner felt-test in an ATB battle.
- [ ] Item button opens a working items submenu; using an item applies + logs.
- [ ] A battle log shows "Battle start", turns, hits, and the outcome — combat reads clearly.
- [ ] No regression to Skills/Attack/Defend, the 3D swap-in, icons, or damage floats.

## What NOT to touch
- Don't change battle ENTRY/engine/swapper (verified wired). Don't restyle the HUD beyond the log panel.
  §0: CLI edits on Windows path. ATB is a side-path (Arena is the demo loop) — this is polish, not a blocker.

*Cross-ref:* ATB RCA (this session), `FeatureFlags.cs` (Arena=ON demo loop), panel audit.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
