# WORK ORDER 575 — Building-Upgrade panel: maxed button fully inert + title tidy

**Status:** IMPLEMENTED (edit-only; not gated/committed — orchestrator reconciles)
**Date:** 2026-06-28
**Silo:** UI / presentation (Village building progression)
**Branch base:** `wip/village2-and-f8-tickets` (ff-merged into worktree before edits)

## Owner felt-test (screenshot — "Upgrade Farm Building")
Every level OWNED, Village Tier 3 (Max). The main action button correctly reads
**"Maxed"** + status **"Fully upgraded."**, BUT it still shows an interactive
hover/selection highlight ("circle") as if it were a live upgrade CTA. Owner:
*"if everything is maxed the button should not circle and show upgrade."* Also the
header rendered the building name overlapping a stale "Upgrade … Building" label.

## RCA (captured from code path)

**1. Hover/selection "circle" on a maxed button.**
- `BuildingUpgradeVM` already sets `MainButtonEnabled = false` when maxed
  (`BuildingUpgradeVM.cs:363` city, `:423` resource), and the View applies it
  (`BuildingUpgradePanelMvvm.cs` Render → `_mainBtn.interactable = _vm.MainButtonEnabled`).
- The button is built by `ElarionUiKit.ButtonPack` → `Button` → `StyleButtonColors`
  (`ElarionUiKit.cs:561`) which sets `transition = Selectable.Transition.ColorTint`
  with `disabledColor = (0.5,0.5,0.5,0.5)`. So a "maxed" button stayed a **half-alpha
  gold CTA** with a live ColorTint transition — reading as a still-actionable upgrade
  button rather than a settled, disabled chip.

**2. Title overlap ("Upgrade [Farm] Building").**
- `ElarionUiKit.Header` (`ElarionUiKit.cs:474`) builds **two** labels from the passed
  title: a black **drop-shadow** label (offset) AND the returned gilt title.
- `BuildChrome` passed the static title `"Upgrade Building"` (both shadow + title get it),
  then `Render` re-texted **only** the returned gilt label to `_vm.Title` ("Farm").
  The shadow kept the stale "Upgrade Building" → the gilt "Farm" overlapping the
  shadowed "Upgrade Building" = the owner's overlap.

## Fix

**ViewModel — `BuildingUpgradeVM.cs`**
- Added `public bool IsMaxed { get; private set; }` (distinguishes *fully maxed / no
  upgrade* from *merely unaffordable*, so only the former goes fully inert).
- Set it: `BuildCity` (`IsMaxed = maxed;`), `BuildResource` (`IsMaxed = maxed;`),
  `BuildUnknown` (`IsMaxed = true;` — no upgrade path).

**View — `BuildingUpgradePanelMvvm.cs`**
- `Open`: construct the VM **before** `BuildChrome` so the title can be composed once
  from the live building name.
- `BuildChrome`: title composed once as `"Upgrade: " + _vm.Title` and passed to
  `BuildObsidianPanel` → shadow + gilt title now carry the **same** text (no overlap,
  single clean string, e.g. "Upgrade: Farm").
- `Render`: removed the per-render `_headerLabel.text = _vm.Title` (that was the stale-
  shadow cause); now calls `ApplyMainButtonState(_vm.IsMaxed)` after setting interactable.
- New helper `ApplyMainButtonState(bool inert)`:
  - **inert (maxed):** `transition = Selectable.Transition.None` (kills hover/selection
    "circle"), dims plate to `(0.30,0.27,0.22,0.85)` + label to `ParchmentDim` — reads
    as a settled disabled chip, keeps the "Maxed" label + "Fully upgraded." status.
  - **active:** restores `StyleButtonColors` (ColorTint) + white plate + Parchment label
    — the normal gold CTA. The merely-unaffordable case stays a live CTA that greys via
    the disabled colour, so the **upgrade path is unchanged**.
- `CreateRow`: owned/locked rows (`!interactable`) also get `transition = None` for
  consistency (no selection highlight on non-actionable rows). Active "NEXT" rows
  keep the normal ColorTint feedback.

## Active-upgrade path preserved
- When an upgrade is available, `IsMaxed == false` → `ApplyMainButtonState(false)`
  restores the full gold CTA; `MainButtonEnabled` drives interactable as before;
  `UpgradeNext()` / `Select()` logic untouched. WO-564 passive-income harvester wiring
  untouched. Black+gold Obsidian chrome untouched (only the maxed plate is dimmed).

## Files modified (for reconcile, explicit paths)
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs`
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs`

## Brace check
- BuildingUpgradePanelMvvm.cs — OK (36/36)
- BuildingUpgradeVM.cs — OK (53/53)
