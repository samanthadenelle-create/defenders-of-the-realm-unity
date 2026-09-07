# WO-1393: a tap in the frame a panel closes lands on the screen beneath; the queue drawer overlays the Troops card and QUEUE does not close it

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:53:48, build 2026.09.07.358574). PRIOR STATUS: FIXED - in da90ddc0f, on Firebase App Distribution as build 2026.09.05.356329 (05:55). Gated: REGRESSION_OK 378/378 incl. [queue-toggle-closes], [drawer-clear-of-card], [close-frame-grace]. Awaiting owner felt-test: close Manage with a tap over the Night Market card's area - the store must NOT open; on Troops, OPEN QUEUE pushes the card down and QUEUE/HIDE QUEUE closes it. Found on the headed walk 2026-09-04 23:45-23:47 (build 355952).

## Evidence
- `docs/qa/UI_REVIEW_2026-09-05/10-troops-after-upgrade.png`: after OPEN QUEUE the drawer sits OVER the
  selected-troop card; the UPGRADE TO L4 tap hit the drawer; the top-right QUEUE tap did not close the drawer
  (it rendered `IN QUEUE - TRAINING` clipped under its own rail). Trace: `queue drawer expanded (rows 1)`, no
  `collapsed` line after the QUEUE tap.
- `docs/qa/UI_REVIEW_2026-09-05/11-research-upgrade-door.png`: a tap at the Research door's coordinates,
  issued as Manage was closing, opened THE NIGHT MARKET - the HUD card beneath (now 320x156, WO-1384) caught
  it. No `research locked door` line; the store opened instead.

## Two defects
1. **Drawer over card**: `ToggleQueueDrawer` expands the drawer into the list band without collapsing the
   Troops workspace, so the card's buttons are covered but still the visual target. Either the drawer is a
   distinct band that pushes the workspace (owner ruling #6: queue verbs live in the drawer, the card stays
   readable) or opening it collapses the card to its name band. And QUEUE (top-right) must toggle it closed
   (`ManageQueueDrawerRegression` pins the drawer; add the close pin).
2. **Close-frame tap leak**: `PanelManager.NotifyClosed` clears the record the same frame; a tap already in
   flight reaches the canvas beneath. Standard fix: the HUD ignores pointer-down for ONE frame after any
   modal close (a `PanelManager.CloseGraceUntilFrame` the HUD's tap handlers consult), traced once.

## Acceptance
- [ ] Headless: Troops + drawer open -> the card's TRAIN/UPGRADE buttons are not under the drawer's rect
      (`AuditGeometry` no-overlap); QUEUE closes the drawer (trace `collapsed`).
- [ ] Device: close Manage with a queued tap on the Night Market card's area -> the store does NOT open; the
      trace shows the grace frame swallowing it.
- [ ] `ManageQueueDrawerRegression` + a new `[close-frame-grace]` pin, RED first.
