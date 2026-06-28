# WORK ORDER 563 — HUD cleanup: remove the OLD battle HUD + wire FOCUS buttons + surface 3 panels

**Status:** IMPLEMENTED (edit-only agent; NOT gated/committed — orchestrator batch-gates + commits)
**Date:** 2026-06-28
**Lane:** HUD architect (DeNelle.HUD + DeNelle.Village.Arena + one DeNelle.Audio public-API add)
**Branch base:** `wip/village2-and-f8-tickets` @ `d8299b6c` (worktree fast-forwarded to tip first)

## Owner decisions (binding)
- KEEP the NEW 9-zone battle HUD (`BattleHud9Zone`). REMOVE the OLD battle HUD.
- Keep OverworldEncounter + BattleHud9Zone flags ON.

---

## TASK 1 — Remove the OLD battle HUD (+ load-bearing coverage fix)

The OLD battle HUD = `VillageHudController._battleHudGroup` (a CanvasGroup @ sortingOrder 150)
+ its four legacy clusters, cross-faded by `BattleHudVisibilityManager`. Removed in full.

### `Assets/_Modules/HUD/VillageHudController.cs`
- **Removed fields:** `_battleCanvas`, `_battleHudGroup`, the `BattleHudGroup` property; `_skillBar`,
  `_vitalsCluster`, `_waveReadout`; wave readout (`_waveText/_waveStateText/_enemyCountText`);
  momentum (`_momentumBadge/_momentumGroup/_comboText/_streakText/_lastCombo/_lastStreak/_momentumPop/_momentumHold`);
  vitals (`_hpFill/_hpText/_manaFill/_manaText`); skill cells (`_slotKey/_slotGlyph/_slotName/_slotAccent/_slotIcon/_slotCooldown/_slotCdFill`);
  the hero XP line (`_xpLineFill/_xpFraction` + the `HeroProgression` reflection poll fields).
  **KEPT** `_hpCurrent/_hpMax` (read by `ApplyCombatGate`), `_lastWaveNumber/_lastWaveState` (town readout).
- **Removed builders + calls:** the battle canvas/group construction in `Build()`; the four cluster
  builder calls + bodies — `BuildWaveReadout`, `BuildMomentumBadge`, `BuildVitalsCluster`, `BuildSkillBar`.
  **KEPT** `BuildPartyFrames` / `_partyStack` (base-canvas companion frames must survive).
- **Removed methods:** `AnimateMomentumBadge`, `UpdateHeroXpLine`, `ResolveHeroProgIfNeeded`, `PopMomentum`
  (+ their `Update()` calls).
- **Updated consumers:**
  - `VerifyHudBuilt` — dropped the `waveText/hpFill/skillBar` checks.
  - `ApplyForgettingDim` — `combatLive` now reads the Core `HudContext.Battle` instead of `_battleHudGroup.alpha`.
  - `ApplyCombatGate` — drops the vitals-cluster toggle; drives only the party-frame stack now.
  - `UpdateTownHud` — removed the now-moot `hud9Owns` double-HUD suppression block (no old group to hide).
  - Responsive layout (portrait + landscape) — removed the `_waveReadout/_skillBar/_vitalsCluster` `SetAnchors`.
  - `SetCombatHudVisible` — latches the flag + re-applies `ApplyCombatGate` (no skill bar/vitals to toggle).
  - **Setters kept as null-safe no-ops** (still pushed by Village bridges — HudModelProducers / ComboHudBridge /
    WaveHudBridge / HeroAbilitiesHudBridge — so the IVillageHud contract is unbroken): `SetComboCount`,
    `SetKillStreak`, `SetEnemyCount`, `SetMana`, `SetAbilityCooldown`, `SetAbilitySlot` (both overloads),
    `RefreshCombatWaveHeadline`. `SetWaveImminent` / `ShowWaveClearBanner` / `HideWaveClearBanner` keep their
    town-lookout logic; `SetHeroHp` keeps `_hpCurrent/_hpMax` + the `SetPartyMember(0,…)` feed.

### `Assets/_Modules/HUD/BattleHudVisibilityManager.cs`  (collapsed)
- Removed `_battleGroup` / `_battleTargetAlpha` (the old group is gone). Now drives **only** the TOWN fade.
- **COVERAGE FIX:** `ApplyEnemySceneHud9` → renamed `ApplyBattleHud9`; spawns the 9-zone for **ANY** Battle
  mode (`mode == Battle && ff.battlehud9zone`), i.e. enemy-owned outposts **and** `RaidBase_*` raids — not just
  enemy-owned scenes. An arena fight still spawns its own 9-zone via `BattleArenaHud`; the existing idempotent
  guard (`FindObjectOfType(_hud9Type) != null → bail`) prevents a double-spawn, and the manager only tears down
  the instance **it** spawned. → **No Battle context is left HUD-less.** (Verified `HudContextEvaluator`
  classifies raid + enemy-owned scenes as `HudContext.Battle`, so the spawned 9-zone's own context gate shows.)

### `Assets/_Modules/Village/Arena/BattleArenaHud.cs`  (trimmed)
- Removed the legacy TOP-CENTRE primary panel (`_primaryPanel` + `_title/_enemyFill/_remain`), `SetPrimary`,
  and `SuppressPrimaryForHud9` (+ its call in `Create`). **KEPT** Flee (tap-to-confirm), `ShowIntro`, and
  `ShowResult` / the WO-556 victory + loss summary panels.

### `Assets/_Modules/Village/Arena/BattleArena.cs`
- Removed both `_hud.SetPrimary(...)` call sites (engage @ ~392 + per-frame @ ~1086) — the 9-zone reads
  enemy HP/target directly.

### Leak / registration check
- `BattleHud9Zone.ApplyContextGate` gates its canvas on `CoreServices.HudModel.Context == HudContext.Battle`.
- `HudModelHost` (DDOL, `RuntimeInitializeOnLoadMethod`) registers the model via `CoreServices.RegisterHudModel`
  and `HudContextEvaluator` flips it to `Town` on return to the hub → the 9-zone canvas hides. Registration is
  correct; **no 2nd gate added**. Manager teardown (`TearDownEnemySceneHud9`) is the belt-and-braces backup.
  → **9-zone cannot linger/leak into town.**

---

## TASK 2 — Wire the dead 9-zone FOCUS buttons  (`BattleHud9Zone.cs`)
Mid-Right FOCUS area (`BuildMidRightFocusArea`) — was 3 `Debug.Log` placeholders:
- **Heal** → casts the class's heal ability via `Cast(slot)` (HeroAbilities.TryCast, gated by cd/mana).
  The heal slot is resolved class-agnostically (`ResolveHealSlot`: scan Q/W/E/R for `EffectEnum == Heal`).
  **The Knight kit has NO heal effect** (q=dash, w=knockback, e=taunt, r=meteor in abilities.json), so for the
  Knight the Heal button is **HIDDEN**; it appears for a class that has a heal in its kit (e.g. Cleric).
- **Attack** → `BasicAttack()` (PlayerAttackController auto-melee combo) — the real basic attack.
- **Mode** → **HIDDEN (not built).** `CameraModeController.Mode` is an auto Town/Battle blend, not a
  player-facing combat/aim toggle — no clean action exists, so per the directive it is hidden, not a no-op.

---

## TASK 3 — Surface 3 finished-but-unreachable panels
New `Assets/_Modules/HUD/SocialAccessCluster.cs` (+ `.cs.meta`) — a touch-friendly uGUI button strip on the
RIGHT edge, built with shared `ElarionUiKit.Button` chrome, self-bootstrapping like the panels' own
bootstraps (skips Title / enemy-owned raid scenes; hidden during battle so it clears the 9-zone FOCUS column):
- **Chat** → `ClanChatPanel.Toggle()` (same assembly, direct).
- **Ranks** → `LeaderboardPanel.Toggle()` (same assembly, direct).
- **Music** → `MusicSelectionPanel.Toggle()` via reflection (HUD → Core only; no Audio asmdef ref — same
  cross-asmdef pattern the panel bootstraps use to find the hero).
- `Assets/_Modules/Audio/MusicSelectionPanel.cs` — added public `Toggle()` + `Open()` (mirrors the other two;
  the J key was keyboard-only + DevHotkeys-gated → unreachable on touch).
- `Assets/_Modules/HUD/LeaderboardPanelBootstrap.cs` — corrected the false "L hotkey" header comment.

PanelRouter NOT used: these three are UI-Toolkit (`UIDocument`) hotkey panels, not the code-built uGUI
`PanelId` modals PanelRouter manages — so a direct `Toggle()` is the correct reachable path.

---

## Validation
- Brace-balance + NUL scan PASS on every touched/new `.cs` (8 files).
- No `.unity` scene hand-edited. No remaining references to any removed symbol (repo-wide grep clean).
- IVillageHud contract intact (no public method removed — bodies gutted to safe no-ops only).
- Owner-decision flags: **Mode FOCUS button hidden** (no clean toggle); **Heal FOCUS button hidden for Knight**
  (no heal in kit); **ATB `BattleHudUgui` left untouched** (separate combat system, owner unruled).

## Files (for reconcile)
**Modified:** `Assets/_Modules/HUD/VillageHudController.cs`,
`Assets/_Modules/HUD/BattleHudVisibilityManager.cs`,
`Assets/_Modules/HUD/LeaderboardPanelBootstrap.cs`,
`Assets/_Modules/Village/Arena/BattleHud9Zone.cs`,
`Assets/_Modules/Village/Arena/BattleArenaHud.cs`,
`Assets/_Modules/Village/Arena/BattleArena.cs`,
`Assets/_Modules/Audio/MusicSelectionPanel.cs`
**New:** `Assets/_Modules/HUD/SocialAccessCluster.cs` (+ `.cs.meta`)
**Deleted:** none (only in-file removals)
