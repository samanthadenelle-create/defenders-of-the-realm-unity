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
