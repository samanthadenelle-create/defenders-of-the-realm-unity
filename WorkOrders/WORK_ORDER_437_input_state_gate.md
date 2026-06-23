# WORK ORDER 437 — Input / State discipline: battle-lock + hotkey gate (THE streamline)

**Status: READY TO IMPLEMENT** (owner-confirmed rule 2026-06-17). Editor-closed (gate + felt-test).
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
