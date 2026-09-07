# WO-1573: QUEUE pill on Manage hub opens nothing

**Status:** READY TO IMPLEMENT
**Silo:** HUD - `ManageScreenPanel` + `ManageQueueDrawerRegression`.
**Source:** Manage pass-three lane handback 2026-09-07. Minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1573 -> 1574 in the same edit).

## 1. EVIDENCE (re-read at source 2026-09-07)

- The Manage hub renders a QUEUE pill (`ManageScreenPanel.ApplyScreenVisibility`, drawer
  initialized as a child of `Zone_Body`).
- Tapping the QUEUE pill does nothing.
- `ApplyScreenVisibility` sets `Zone_Body.SetActive(false)` whenever the Manage hub is active,
  blocking any interactive drawer that lives under it.
- The queue drawer exists; the interactivity path is live; only the Z-order/parenting blocks
  the interaction.

## 2. FIX

Mount the queue drawer overlay ABOVE the Zone_Body hierarchy OR re-parent it on drawer open so
it climbs above the hub's body layer. The drawer must remain interactive while the hub panel is
displayed.

## 3. WHAT NOT TO TOUCH

ManageScreenVM, zone/panel state machines, navigation routing, other drawer/overlay paths.

## 4. ACCEPTANCE

- [x] QUEUE pill on the Manage hub navigates to the queue drawer without error.
- [x] Queue drawer renders and accepts input while the Manage hub is visible.
- [x] `ManageQueueDrawerRegression` case: open drawer from Manage hub state; verify interactivity.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on a fresh log (gate lane).

## FILES TO EDIT

- `Assets/_Modules/HUD/ManageScreenPanel.cs` (`ApplyScreenVisibility`, drawer host/parent)
