> ⚠ **UNRESOLVED NUMBER COLLISION — WO-437 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_437_combat_hud_tech_skin.md` (06-13, first-on-disk), `WORK_ORDER_437_input_state_gate.md` (06-17, marked DONE), `WORK_ORDER_437_bar_overflow_rectmask.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WORK ORDER 437 — Input / State discipline: battle-lock + hotkey gate (THE streamline)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at BattleLock.cs:40.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT** (owner-confirmed rule 2026-06-17). Editor-closed (gate + felt-test)._

**Why:** the base loop has NO input/state discipline → ~11 panels on global single-key hotkeys spam
open ("13 windows load, none good"), panels open mid-battle (shop while companion fights), nothing
gates by game state. This is the single highest-leverage base-loop stability fix.

## Grounding (from the panel audit, 2026-06-17)
- **Arbiter EXISTS:** `Core/UI/PanelManager.cs` (single-modal — opening one closes the prior) +
  `Core/UI/PanelRouter.cs` (enum `PanelId` registry). ~26 modals route through it.
- **The "13 windows" root = global hotkeys.** Panels polled on keys: **C** Cosmetics, **T** HeroTalents,
  **P** PetSkillTree, **K** Crafting, **U** BuildingUpgrade, **J** Music, **Y** ClanChat, **L** Leaderboard,
  **M** TowerManager (+ dev **F1** DevPanel, **Ctrl+Shift+A** Admin). Each bootstrap polls `Input.GetKeyDown`
  in `Update` and opens its panel — independently, so any key pops a panel regardless of context.
- **2 panels BYPASS the arbiter:** `RumorBoardPanel`, `ArenaPanel` (open via backdrop only, no
  `PanelManager` registration) — they can stack.
- **No battle-lock gate exists.** **ESC** is polled by multiple panels + `PauseController` (collision).

## The rule (owner-confirmed)
**During ACTIVE battle:** allowed = **Battle HUD** + **hero Movement** + **Pause/Flee**. LOCKED = every
gameplay panel (Shop, Inventory, Equipment, Crafting, BuildingUpgrade, HeroTalents, PetSkillTree,
Cosmetics, TroopTraining, RumorBoard/Quests, TowerManager, Music, ClanChat, Leaderboard, Help) **and
Build mode**. (Build is a between-battles activity.)
**Outside battle:** the stray global panel hotkeys (C/T/P/K/U/J/Y/L/M) are **disabled** — those panels
open via their WORLD interaction (NPC/building → `PanelRouter`) only. Keep **Build** + **Battle HUD**
hotkeys. (Owner: "disable all hotkeys except battle hud and build.")

## Implementation (one choke point + cleanup)

### 1. Battle-state predicate (single source of truth)
- Add/confirm a reliable `BattleState.IsInBattle()` (or reuse an existing combat-active flag). It must be
  TRUE for the duration of an active ATB/Arena battle, FALSE in hub/explore. Reference: `BattleATB/
  BattleController.cs` battle lifecycle + `ArenaMode`. Expose via a thin static both `Core` and the
  panel bootstraps can read (mirror `CoreServices`).

### 2. Gate panel-opens centrally — `PanelManager`
- In `PanelManager` (the choke point every modal already calls): when an open is requested, if
  `IsInBattle()` and the panel is not on the battle-allow-list (Battle HUD / Pause), **reject + log**
  (`FlowTrace.Warn`), do not open. One gate covers all ~26 arbiter panels.
- **Fix the 2 bypassers:** register `RumorBoardPanel` + `ArenaPanel` with `PanelManager`
  (`NotifyOpened`/`NotifyClosed` + a Close handle) so they route through the gate too.

### 3. Disable the stray global hotkeys
- In each panel bootstrap that polls a key (Cosmetics/Talents/Pet/Crafting/BuildingUpgrade/Music/
  TowerManager/ClanChat/Leaderboard), **remove the global `Input.GetKeyDown` open** (or guard it behind
  a dev flag). These panels stay reachable via their world interactable → `PanelRouter.Open(id)`.
  Files: `HUD/HeroTalentPanelBootstrap`, `HUD/PetSkillTreePanelBootstrap`, `Village/Crafting/
  VillageCraftingPanelBootstrap`, `Village/Buildings/Progression/BuildingUpgradePanelBootstrap`,
  `HUD/CosmeticShopPanel`, `Audio/MusicSelectionPanel`, `Village/Buildings/UI/TowerManagerPanel`,
  `HUD/ClanChatPanel`, `HUD/LeaderboardPanel`. Keep dev hotkeys (F1/Ctrl+Shift+A) but guard with
  `IsInBattle()`.
- Keep **Build mode** hotkey + the **Battle HUD**.

### 4. Gate the Yarn panel verbs
- In `Village/Tutorial/DialogueCommandBridge.cs` + `NPCCommandBridge`: the panel-open verbs (OpenShop,
  OpenRumorBoard, ShowTrainingUI, OpenEquip, etc.) check `IsInBattle()` first and no-op (with a brief
  in-dialogue "not during battle" line or just skip) so an NPC can't open a panel mid-fight.

### 5. Centralize ESC
- Route ESC through ONE owner (`PauseController` or an InputManager): ESC closes the top modal if one is
  open (via `PanelManager`), else toggles Pause. Remove the per-panel ESC polling that races.

## Acceptance criteria
- [ ] Compile gate green; owner felt-test.
- [ ] During battle: pressing any panel hotkey / walking into an NPC does NOT open a panel; Battle HUD +
      movement + Pause/Flee work.
- [ ] Outside battle: C/T/P/K/U/J/Y/L/M no longer pop panels; those panels still open from their
      building/NPC. Build + Battle HUD hotkeys still work.
- [ ] `RumorBoardPanel` + `ArenaPanel` route through `PanelManager` (no stacking).
- [ ] ESC: closes top modal if open, else pauses — no double-fire.
- [ ] No regression to opening panels via world interaction.

## What NOT to touch
- Do NOT remove the panels themselves or their PanelRouter entries — only the global hotkey opens.
- Do NOT change panel internals/MVVM. Do NOT alter Battle HUD behavior. §0: CLI edits on Windows path.

*Cross-ref:* the panel audit (this session), `ARCHITECTURE_PRINCIPLES.md §2`, `GRANT_DEMO_VALIDATION.md`.
Pairs with WO-438 (base-loop RCA fixes).
