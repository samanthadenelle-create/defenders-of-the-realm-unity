# WORK ORDER 1301 — 'Combat Item Picker' reports itself a ghost modal on every open (verify-before-assign)

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T17:23:05, build 2026.09.04.354315). PRIOR STATUS: FIXED — `OpenItemPicker` now builds the modal and assigns `_itemPicker` BEFORE calling `PanelManager.NotifyOpened` so the registered probe can answer (a failed build still routes through the same verify and is still reported); the sweep found and fixed one sibling, `TownShowcaseVisitPanel`. See `WORK_ORDER_1301_combat_item_picker_ghost_modal_false_positive.RESULT.md`.
**Source:** F8 captures seq **4360**, **4361**. Ledger: `docs/qa/F8_TRIAGE_2026-09-02.md` §5.
**Silo:** HUD kit / Core UI arbiter
**Severity:** P2 — **not** a player-visible defect. It is a false alarm that fires on *every* combat
item-picker open and buries the owner's real flags in the F8 queue.

## Owner-facing symptom

None on screen — the picker opens and works. The damage is to the triage pipeline: every open writes a
`FlowTrace.Fail` → `Debug.LogError` → a new F8 error capture, tagged as the WO-465 invisible-scrim
class. Two of them (seq 4360, 4361) sat 17 seconds ahead of the owner's own flag in this backlog and
had to be ruled out by hand before the flag could be read.

## Captured proving line (§12 evidence — quoted verbatim)

`logs/f8-inbox/capture-20260902-013504-seq4360.md` (`t=502.4813537597656`) and
`…-seq4361.md` (`t=503.656494140625`), both `scene=Main_Castle_Overworld`:

```
[Flow:UI] PanelManager: 'Combat Item Picker' recorded as OPEN but its IsOpen probe reports NOT open
  — blank/failed panel masquerading as open (WO-465 invisible-scrim class).
```
```
UnityEngine.Debug:LogError (object)
DeNelle.Core.Diagnostics.UnityLogSink:Error (string) (at D:/EoA/Assets/_Modules/Core/Diagnostics/FlowTrace.cs:461)
DeNelle.Core.Diagnostics.FlowTrace:Fail (string,string) (at D:/EoA/Assets/_Modules/Core/Diagnostics/FlowTrace.cs:171)
DeNelle.Core.UI.PanelManager:NotifyOpened (DeNelle.Core.UI.PanelHandle,string,string)
  (at D:/EoA/Assets/_Modules/Core/UI/PanelManager.cs:175)
```

## Root — proven by call ordering in source, not inferred

`Assets/_Modules/HUD/Kit/HudKitController.cs`, `OpenItemPicker()`:

```
1749:    if (_itemPicker != null || _itemUseInFlight) return;
1751:    if (_itemPickerPanelHandle == null)
1752:        _itemPickerPanelHandle = PanelManager.RegisterBattleAllowed(
1753:            "Combat Item Picker", CloseItemPicker, () => _itemPicker != null);   // <- the IsOpen probe
1754:    if (!PanelManager.NotifyOpened(_itemPickerPanelHandle)) return;              // <- verify runs HERE
…
1757:    _itemPicker = ElarionUiKit.BuildObsidianModal("CombatItemPicker", "CHOOSE AN ITEM", …); // <- first assignment
```

`PanelManager.NotifyOpened` runs its visibility verify **synchronously inside the call at line 1754**
(`Assets/_Modules/Core/UI/PanelManager.cs:168-178`), invoking `handle.IsOpen()`. The probe registered at
line 1753 is `() => _itemPicker != null`, and `_itemPicker` is not assigned until line **1757** —
three lines later. The probe therefore returns `false` **every single time, by construction**. The
picker is fine; the check is asking before the answer exists.

Note line 1749 also proves the picker cannot already be non-null on entry — so there is no path where
this reports correctly.

## Acceptance criteria

1. The false `FlowTrace.Fail` no longer fires on a normal picker open. Fix it at the **call site**, not
   by weakening the arbiter — e.g. build the modal and assign `_itemPicker` before announcing the open,
   and close/tear down the modal if `NotifyOpened` then refuses. (Whichever shape is chosen, the
   invariant is: *the probe must be answerable when the verify runs.*)
2. **A genuinely blank picker is still caught.** Prove it: force the modal build to fail (or leave
   `_itemPicker` null) and show the `FlowTrace.Fail` still fires. A fix that only silences the message
   is a regression of WO-465 and is rejected.
3. `WorldHold.Acquire(WorldHold.ReasonCombatItemPicker)` (line 1756) is still acquired exactly once per
   real open and released on every close path — including a refused `NotifyOpened`. No leaked holds.
4. **Sweep for siblings.** Grep every `PanelManager.Register*` call site for an `IsOpen` probe that
   reads a field assigned *after* the matching `NotifyOpened`. This ordering bug is a pattern, not a
   one-off; fix each one found and list them in the RESULT.
5. Headless capture of a combat item-picker open/close cycle shows
   `PanelManager: 'Combat Item Picker' opened and verified visible (IsOpen=true)` and **zero**
   `masquerading as open` lines.
6. `COMPILE_GATE_OK` on a fresh log; brace-balance on every `.cs` touched (CLAUDE.md §1).

## What NOT to touch

- ⛔ **Do not weaken, bypass, or make optional the `IsOpen` verify in
  `Assets/_Modules/Core/UI/PanelManager.cs:168-178`.** That check is the WO-465 detector and is the one
  thing that catches invisible scrims. Deleting the alarm because it went off wrongly is exactly the
  "oracle that cries wolf" failure. Fix the caller.
- ⛔ Do not touch `PanelManager`'s modal-swap arbitration or `RegisterBattleAllowed` semantics.
  `WORK_ORDER_1296_modal_and_world_feedback_ownership.md` is live on the daily-chest modal timing —
  a **different** panel and a **different** defect. Stay off its ground.
- ⛔ Do not restyle, re-lay-out, or re-skin the picker. Lines 1760-1780 (the compact-shell header
  suppression) are deliberate and out of scope.
- ⛔ Do not change `sortingOrder: 31500` or the modal's screen rect.
