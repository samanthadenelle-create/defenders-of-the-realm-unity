# WORK ORDER 1301 — RESULT

**Status:** FIXED (edit-only; NOT gated, NOT committed — the lead gates and commits)
**Date:** 2026-09-02

## What was actually wrong

`HudKitController.OpenItemPicker` announced the open to the arbiter BEFORE it built the thing the
arbiter was about to ask about. `PanelManager.NotifyOpened` runs its WO-465 visibility verify
**synchronously inside the call**, invoking the probe registered three lines earlier —
`() => _itemPicker != null` — and `_itemPicker` was not assigned until three lines *later*. The probe
was therefore false **by construction on every open** (the method's own first-line guard proves
`_itemPicker` is null on entry, so no path reported correctly), producing a `FlowTrace.Fail` →
`Debug.LogError` → a fresh F8 error capture every single time the picker opened.

The picker was fine. The arbiter was right to ask. The caller asked it too early.

## The fix — at the call site, not by weakening the arbiter

`Assets/_Modules/HUD/Kit/HudKitController.cs`, `OpenItemPicker`:

**BUILD FIRST, ANNOUNCE LAST.** The world hold is acquired and the modal built and fully populated
(`RefreshItemPicker()` included) before `PanelManager.NotifyOpened(_itemPickerPanelHandle)` is called.
The probe can then answer truthfully and the verify emits
`'Combat Item Picker' opened and verified visible (IsOpen=true)`.

`PanelManager.cs` is untouched. The verify, its `FlowTrace.Fail` severity, `RegisterBattleAllowed`
semantics, the modal-swap arbitration, `sortingOrder: 31500`, the screen rect and the compact-shell
header suppression are all exactly as they were.

### Acceptance 2 — a genuinely blank picker is still caught

A new branch immediately after the build:

```csharp
if (_itemPicker == null || _itemPicker.chrome == null)
{
    var stillborn = _itemPicker;
    _itemPicker = null;                                  // the probe must tell the TRUTH
    PanelManager.NotifyOpened(_itemPickerPanelHandle);   // verify runs -> ghost REPORTED
    if (stillborn != null && stillborn.canvas != null) Destroy(stillborn.canvas);
    CloseItemPicker();
    return;
}
```

A failed build is routed **through the same verify**, so the `masquerading as open` line still fires —
that is the real WO-465 case. Note this branch also closes a pre-existing hole: a handle with a null
`chrome` (a shell with nothing in it) would have satisfied `_itemPicker != null` and reported a ghost as
healthy; clearing the field first makes the probe honest.

### Acceptance 3 — the world hold

`WorldHold.Acquire(WorldHold.ReasonCombatItemPicker)` is taken exactly once per open, and every exit
path releases it:

- **normal close / item use** → `CloseItemPicker` (unchanged).
- **stillborn build** → `CloseItemPicker` in the branch above.
- **refused `NotifyOpened` (battle-lock)** → the arbiter invokes `handle.Close` on its way out, and the
  new code calls `CloseItemPicker()` again on the refusal path. `CloseItemPicker` is idempotent (every
  field null-checked then nulled) and `WorldHold.Handle.Dispose` is documented idempotent, so the
  double call is a no-op, never a double release.

The hold is now acquired before a possible battle-lock refusal and released in the same frame, which is
the trade for having it released on that path at all.

## Both directions proved

**Healthy case stops failing.** With the build moved ahead of the announce, the probe reads a non-null
`_itemPicker` when the verify runs, so the open takes the `IsOpen=true` Step branch.

**Mutation — a genuine ghost is still caught.** Register a panel whose IsOpen probe reports `false`
(exactly what a failed modal build produces) and call `NotifyOpened`: the verify must still emit the
`masquerading as open` error. Pinned permanently as
`ModalPanelDisciplineTests.NotifyOpened_StillReportsAGenuineGhostModal`
(`LogAssert.Expect(LogType.Error, /masquerading as open/)`), with
`NotifyOpened_IsCleanWhenTheProbeCanAnswer` as the paired positive control
(`LogAssert.NoUnexpectedReceived()`). Both live in the suite so nobody has to re-mutate by hand.

The ordering itself is pinned by `CombatItemPickerBuildsBeforeItAnnouncesTheOpen`, which asserts by
**call order** — the index of `_itemPicker = ElarionUiKit.BuildObsidianModal` must be less than the index
of `PanelManager.NotifyOpened(_itemPickerPanelHandle)`. That test FAILS against the pre-fix source, so it
is a real guard and not a restatement of the fix.

Files: `Assets/Tests/EditMode/ModalPanelDisciplineTests.cs` (+ `using UnityEngine;` /
`using UnityEngine.TestTools;`; the asmdef already references `UnityEngine.TestRunner`).

## Acceptance 4 — sibling sweep: ONE MORE OFFENDER FOUND AND FIXED

**The ordering bug is a pattern, and the sweep proved it.** All 80 live `PanelManager.Register` /
`RegisterBattleAllowed` call sites (88 grep hits minus 9 source-text assertions inside regression
suites/comments, plus the two `PanelMgr.Register` alias sites in `DevPanelController.cs:222` and
`AdminOverlay.cs:88`) were read at the METHOD level, tracing `() => IsOpen`-style probes back to what
actually backs them.

### Second offender: `Assets/_Modules/HUD/TownShowcaseVisitPanel.cs` — "Town Showcase"

Verified at source, not taken on report:

- Probe (line 35/39): `() => IsOpen`, where `IsOpen => _modal != null && _modal.canvas != null &&
  _modal.canvas.activeSelf`.
- `EnsureBuilt` **ends with `_modal.canvas.SetActive(false)`** (line 99), and `Close` also leaves the
  canvas inactive (line 188).
- `Open` called `EnsureBuilt()` → `NotifyOpened` → *then* `_modal.canvas.SetActive(true)`.

So `activeSelf` was false at the verify **by construction on every open** — first open (just
deactivated by `EnsureBuilt`) and every later one (still deactivated by `Close`). Identical class to
the picker: a permanent `FlowTrace.Fail` / F8 error capture on a working panel.

**Fixed the same way:** `SetActive(true)` moved above the announce; a null-build branch still routes a
genuine ghost through the verify and then clears the arbiter slot; a battle-lock refusal invokes the
panel's own `Close`, which deactivates the canvas again, so nothing is left visible. Pinned by
`ModalPanelDisciplineTests.TownShowcaseVisit_ShowsBeforeItAnnouncesTheOpen`.

### The other 78 are correct

A mechanical first-occurrence pass flagged 12 candidates; each was read at the method level and every
one establishes the probed state before the announce. The main shapes:

| Call site | Probe | Why it is correct |
|---|---|---|
| `Dungeons/DungeonExitInteractable.cs:924` | `() => _confirmOpen` | `_confirmOpen = true` at 904, before the announce |
| `Dungeons/UI/LoreReadingModal.cs:231` | `() => !_closed && _canvas != null` | canvas built earlier in the same method |
| `HUD/BugReportView.cs:78` | `() => _canvasRoot != null && !_closing` | announce is in `OpenRoutine` after `BuildUi` + a null-check |
| `Onboarding/FoundingChoiceController.cs:296` | `() => !_routed && _canvas != null` | canvas built before |
| `Onboarding/LoginPanelController.cs:311` | `() => !_routed && _canvas != null` | canvas built before |
| `Village/Arena/ArenaPanel.cs:90, 350` | `() => _ui != null` | `BuildRoot()` / `ShowEntry()` run before `NotifyOpened` |
| `Village/Hero/ShopPanel.cs:116` | `() => _ui != null` | `BuildChrome` runs before |
| `Village/Buildings/DungeonSealedDoorPanel.cs:184` | `() => IsOpen` | modal built above; refusal tears down |
| `Village/Buildings/UI/LevelUpSkillPopup.cs:63` | overlay display state | registers in `OnEnable`; announce happens in `Show` |
| `Village/UI/EndState/EndStateView.cs:200` | `() => view != null` | `view` is the local just constructed |

Two notes recorded rather than silently passed (neither is a defect today):

- `ElarionUiKit.BuildModalCanvas` / `BuildObsidianModal` return **active** GameObjects — that is the
  assumption behind every "canvas built before notify" pass. Only five panels deliberately build hidden
  (PauseController, SettingsController, HelpMenu, GooglePlayStorefront, TownShowcaseVisitPanel), and of
  those only TownShowcaseVisitPanel announced before re-activating. If another panel ever adopts the
  build-hidden shape, it inherits this bug.
- `EndStateView` probes `() => view != null` and `VillageLoadOverlay` probes `() => this != null`.
  These can never fail, so they are trivially correct but give the WO-465 detector **zero coverage** on
  those two panels. Not changed here (out of scope, and a probe change is a behaviour change), but
  worth a ticket.

**Offenders: 2 — `HudKitController.OpenItemPicker` (the ticket's own) and `TownShowcaseVisitPanel.Open`
(found by the sweep). Both fixed.**

## Not run here (edit-only lane)

- `COMPILE_GATE_OK` and the headless picker open/close capture (acceptance 5, 6) — the lead gates.
  Judge by marker on a fresh log, never the exit code.

## Deliberately NOT touched

- `Assets/_Modules/Core/UI/PanelManager.cs` — the verify is the WO-465 detector and was not weakened,
  bypassed, made optional, or demoted from `Fail`.
- Modal-swap arbitration, `RegisterBattleAllowed` semantics, and anything on WO-1296's daily-chest
  ground.
- The picker's styling, layout, compact-shell header suppression, `sortingOrder: 31500` and screen rect.
- Wave/battle-lock, hero, tutorial, audio, inventory, talents and enemy-asset files (other lanes).

## Brace / NUL check (CLAUDE.md §1)

```
Assets/_Modules/HUD/Kit/HudKitController.cs         BALANCED clean
Assets/_Modules/HUD/TownShowcaseVisitPanel.cs       BALANCED clean
Assets/Tests/EditMode/ModalPanelDisciplineTests.cs  BALANCED clean
```
