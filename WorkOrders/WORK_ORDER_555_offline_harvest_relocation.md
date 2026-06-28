# WORK ORDER 555 — Relocate offline/Echo harvest UI behind a button next to Settings

**Status:** IMPLEMENTED (edit-only; orchestrator gates + commits)
**Date:** 2026-06-28
**Source:** Owner F8 (2026-06-28): "the offline harvesting needs tucked away in a
button next to settings, not alive on screen — it's a side thought, not the main idea."
**Silo:** HUD / Village (presentation relocation only — no logic change)

---

## RCA — what was always-on (grounded in code + log)

The owner's Player.log line `[Flow:HUD] echoes 1/4 silo 7.8 fill 0.02` is emitted by
`EchoModel.Set(...)` at **`Assets/_Modules/Core/HudModel/HudModels.cs:402`**
(`FlowTrace.Throttle("HUD","echo",...)`). That is only the *trace*; the **on-screen
readout** is built by:

- **`Assets/_Modules/Village/Harvest/EchoWorkforceHud.cs`** — a SELF-CONTAINED uGUI
  widget on its OWN Canvas (`sortingOrder 4500`), anchored TOP-LEFT
  (`PanelPos (170,-150)`), showing **Echoes x/max**, a **silo fill bar**, **Silo %**,
  and a **Dump All** button. It was ALWAYS-ON whenever the HUD context was `Town`
  (the WO-541 context gate at `EchoWorkforceHud.ApplyContextGate()` only hid it
  outside town; in town it sat live on screen every frame). Built/owned by
  `EchoWorkforceBootstrap.Init()` (adds the component to the `EchoService` DDOL host).

Harvest LOGIC (untouched) lives in `Assets/_Modules/Village/Harvest/EchoService.cs`
(accrual, silo, `DumpSilos()`, `EchoUnlocked`) — the view is a dumb consumer of its
`Changed` event.

## What moved (presentation relocation only)

The widget is no longer persistent chrome. It is now a **hidden Obsidian panel**
opened by a **button next to the Settings gear**:

1. **New Core seam — `HarvestPanelGate`** (mirrors `PauseGate`):
   `Assets/_Modules/Core/UI/HarvestPanelGate.cs` (+ `.cs.meta`).
   `RequestToggle()` raises `ToggleRequested`. HUD calls it; the Village panel
   subscribes — so HUD never references Village (§5).

2. **HUD button next to Settings** — `Assets/_Modules/HUD/VillageHudController.cs`:
   - Added `IconHarvest = "hud_harvest"` const (line ~473).
   - In `BuildTopChrome()` the top-right cluster already reserved 3 slots
     (raid · [hidden intel] · settings). Filled the **free middle slot**
     (`0.35..0.65`) — directly LEFT of the Settings gear (`0.69..1.0`) — with a
     `BuildIconButton(... IconHarvest, "Y", () => HarvestPanelGate.RequestToggle())`.
     **Same style** (`BuildIconButton`, round rune-framed icon) as the gear.

3. **Panel rebuilt with shared chrome** — `EchoWorkforceHud.cs` rewritten:
   - Builds via **`ElarionUiKit.BuildObsidianModal("EchoHarvestPanel","ECHO HARVEST", ...)`**
     (near-black fill + gold trim + one Close), compact centred (`0.30,0.34`–`0.70,0.66`),
     `sortingOrder 4600`.
   - **Starts hidden**; `HarvestPanelGate.ToggleRequested` → `Toggle()` show/hide;
     Close button + tap-outside scrim → `Hide()`.
   - Keeps the same summary (Echoes x/max, silo fill bar [life-force green],
     Silo % + raw value, **Dump All** → `EchoService.DumpSilos()`) and the
     `EchoUnlocked` toast. All driven by `EchoService.Changed` (logic intact).
   - Removed the old always-on top-left widget + the WO-541 context-gate auto-show
     (no longer needed — visibility is button-driven).

4. Updated the now-stale inline comment in `EchoWorkforceBootstrap.cs`.

## Files changed
- A `Assets/_Modules/Core/UI/HarvestPanelGate.cs` (+ `.cs.meta`, fresh GUID)
- M `Assets/_Modules/HUD/VillageHudController.cs` (IconHarvest const + middle-slot button)
- M `Assets/_Modules/Village/Harvest/EchoWorkforceHud.cs` (always-on widget → hidden Obsidian panel)
- M `Assets/_Modules/Village/Harvest/EchoWorkforceBootstrap.cs` (comment only)

## Acceptance criteria
- [x] No echo/silo readout persistently on the HUD (widget now hidden by default).
- [x] A harvest button sits adjacent to the Settings gear, same style.
- [x] Tapping it opens a small Obsidian-chrome panel with count/silo/fill/Dump All.
- [x] Harvest logic (accrual, Dump, unlock) unchanged — EchoService untouched.
- [x] §5 respected — HUD↔Village decoupled via the Core `HarvestPanelGate` seam.
- [x] Code-built uGUI (no UXML). Brace check passes on all touched .cs.

## What NOT to touch
- `EchoService.cs` / `OfflineHarvestService.cs` / `WorkerManager.cs` (logic) — left as-is.
- `HudModels.cs` EchoModel trace — left as-is (diagnostic only).

## Notes / owner-decision flags
- **Glyph fallback "Y"** until a `Resources/HudIcons/hud_harvest` sprite is dropped in
  (then the button auto-skins, same as the other HUD icons). Owner art call.
- The middle cluster slot was the one freed when Intel was hidden (owner 2026-06-23) —
  reused here, no layout growth.
