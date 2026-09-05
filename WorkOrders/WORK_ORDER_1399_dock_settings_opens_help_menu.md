# WO-1399: the gear-dock row labelled "Settings" opens the HELP menu; real Settings is reachable only through Pause

**Status:** FIXED - on Firebase App Distribution as build 2026.09.05.356386 (07:05); awaiting owner felt-test (gear dock > Settings opens Settings; Help is a row inside it beside Game Guide; scroll to the bottom - every row reachable). Landed + gated (COMPILE_GATE_OK, REGRESSION_OK 382/382 incl. the new dock-settings-route suite): Core `SettingsGate` (PauseGate's twin) routes the dock "Settings" row to the real Settings; Help is a row inside Settings via the appended `PanelId.Help = 25`; the dock stays 2x3. Fix-alongside reported: Settings' `RequiredLadderPx` was stale at 1370 vs 2066 summed at source (rows past 1370 px were built under the mask on short bodies) - re-summed, overrun is now a Fail. Awaiting the next APK. Minted 2026-09-05 from the UI screen graph (overnight STRETCH).

## Evidence
- Graph: `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:128-129` (dock "Settings" -> HelpMenu), `:137-141` (Pause -> Settings, the only real route), `:250` (dead end 8).
- The row: `Assets/_Modules/HUD/Kit/HudKitController.cs:3954` `AddDockTab(_slideDock.panel, dockRow++, "Settings", OpenSettings)`; `OpenSettings` `:4034-4040` -> `DeNelle.HUD.HelpMenu.Instance.ToggleOverlay()` else `PanelRouter.Open(PanelId.GameGuide)`. Never `SettingsController`.
- What Help is: `Assets/_Modules/HUD/HelpMenuVM.cs:211-220` rows Report a Bug / Controls / Reset Hero & Pet / Credits (+ dev-only Dev Tools, Grant Resources). `HelpMenu.ToggleOverlay` `:514-516` even traces itself as "Settings open requested (gear -> ToggleOverlay)" - the misnomer is baked into the trace.
- Real Settings: `Assets/_Modules/Settings/SettingsController.cs:126` `public void Open()`; rows `:249-359` (wallet, Game Guide, Reset Defaults, Defence Reports, Privacy, Terms, Ad Privacy, Do Not Sell, Play Offline, Dev Panel). Reached ONLY from `PauseController.cs:226-228` "Settings" button -> `OnSettingsClicked` `:336`. `PauseHudBootstrap.cs:91-93` installs both and wires `AttachSettings` at runtime.
- Why the HUD cannot just call it: `Assets/_Modules/HUD/DeNelle.HUD.asmdef` references Core + Data only; `DeNelle.Settings.asmdef` references Core only. Neither may reference the other - the existing seam is `Assets/_Modules/Core/UI/PauseGate.cs:44-58` (`PauseToggleRequested` event, "Kept event-based so Core never references DeNelle.Settings"; `RequestBack` `:86-94`).
- Dock capacity: `HudKitController.cs:4030-4034` `AddDockTab` is a fixed grid `columns = 2; rows = 3` = SIX cells; rows today = Chat (gated `:3950`) + Leaderboard + Music + Settings + Night Market + Pause = 6. A seventh row has no cell.

## What the player experiences
"Settings" opens a bug-report menu. To change quality, difficulty, wallet, privacy or offline play, the player must find Pause first - a door hidden behind another door. Meanwhile Help is labelled as something it is not, so the player never learns where Report a Bug lives either.

## Fix shape (one mechanism)
A Core gate, PauseGate's twin: `SettingsGate.RequestOpen()` in `Assets/_Modules/Core/UI/SettingsGate.cs` raising `SettingsOpenRequested`; `SettingsController` subscribes in `OnEnable` (as `PauseController.cs:93-94` does for PauseGate) and calls its own `Open()`. Dock row "Settings" -> `SettingsGate.RequestOpen()`. Help keeps ONE door without a seventh cell: `HelpMenu` registers a new append-only `PanelId.Help = 25` (`PanelRouter.cs` enum is "append-only: values are load-bearing", `:110`), and `SettingsController` gains a row "Help" -> `PanelRouter.Open(PanelId.Help)` exactly as it already opens Game Guide (`:685`) and Defence Reports (`:695`). `HelpMenu.ToggleOverlay` trace text corrected.

```
gear dock [Settings] --SettingsGate.RequestOpen()--> SETTINGS (SettingsController)
                                                       |-- Help  --PanelRouter.Open(PanelId.Help)--> HELP (Report a Bug / Controls / Credits)
                                                       `-- Game Guide / Defence Reports / ... (unchanged)
Pause [Settings] -> same SETTINGS (unchanged)
```
Trace: `FlowTrace.Step("Settings", "opened via SettingsGate from <dock|pause>")`; `FlowTrace.Fail("Settings", "SettingsGate.RequestOpen had NO subscriber - SettingsController not installed in this scene")` when the event has no listener.

## Acceptance
- [ ] RED first: a `DockSettingsRouteRegression` - source scan: the dock's "Settings" tab calls `SettingsGate.RequestOpen`, `HelpMenu.Instance` is not referenced from HudKitController, and `SettingsController` subscribes `SettingsOpenRequested`. Fails on the current tree. `ModalArbiterRegistrationRegression` still green (HelpMenu, Settings both arbiter-registered).
- [ ] Headless: `Settings_2670x1200.png` (`UICaptureLaunch.cs:2869`) regenerated with the Help row; `HelpMenu_2670x1200.png` (`:3151`) still renders via the new PanelId; `AdaptiveHudGearOpen` shows six cells, no overflow.
- [ ] Device: dock Settings opens Settings (quality/difficulty visible); Settings -> Help -> Report a Bug works; Pause -> Settings unchanged.

## Not in scope
Settings row contents; Pause; the FLAG chip; folding Help's rows into Settings permanently (an option the owner may prefer later - the PanelId door makes it a one-line change).

## Owner question
None - "the label opens what it says" needs no ruling. Default: Help lives as a row inside Settings.
