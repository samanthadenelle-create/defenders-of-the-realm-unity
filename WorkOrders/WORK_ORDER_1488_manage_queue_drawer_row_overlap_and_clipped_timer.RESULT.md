# WO-1488 RESULT - the row height is derived from the well, the retired-card pin is retired, the rows carry their thumbnails

**Status:** IMPLEMENTED AT SOURCE, UNGATED. Uncommitted in the working tree, 2026-09-07.
*(was: AWAITING OWNER MATCH - device frame vs mockup panel 9 not yet passed; before that: FIXED AT
SOURCE, UNGATED AND UNPROVEN, 2026-09-06)*
**Commit:** none - working tree only.

⛔ **NOTHING HERE IS PROVEN ON A FRAME.** No Unity run was in this lane's scope. Every DEFECT below
is measured off the owner's own device captures
(`Logs/device/screens/owner-screen-20260907-010356.png`, populated drawer;
`-010257.png`, empty drawer); every FIX is a source-level change whose evidence will be the next
`MANAGE_FLOW_MAP_OK` with the PNGs opened.

---

## 1. THE ROOT CAUSE THE TICKET NAMED, AND WHAT WAS ACTUALLY WRONG WITH IT

The ticket says `DrawerModeListKeepPx` still measures the retired 260px card and is pinned verbatim
at `ManageQueueDrawerRegression:273`. Both halves are true, and the diagnosis needs one correction
that changes the fix:

⛔ **`DrawerModeListKeepPx` DOES NOT SEAT THE DRAWER THE OWNER PHOTOGRAPHED.** It is read only on the
BAND path, and `ApplyDrawerPlacement` selects that path with
`_queueDrawerOpen && DrawerInBandMode && !WorkspaceActive`. Since WO-2001 the workspace owns the well
on all four destinations, so `WorkspaceActive` is true and **every Manage screen takes the OVERLAY
path**. Her frames prove it: the title "QUEUE", the corner X and the three tab plates are all
overlay-only chrome that band mode explicitly stands down.

So the pinned constant was holding a number in place **for a shape nothing renders**, while the
overlay's rows clipped top and bottom and the case stayed green. That is the same class as the
ticket describes; it is simply one layer over.

## 2. WHAT LANDED

All in `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` unless named otherwise.

| # | Ask | What changed |
|---|---|---|
| 1 | Derive the drawer well from the full-bleed panel well; fill from under the tab plates to the CLOSE band | `DrawerOverlayY0` `0.02f` -> `0f`. The well's floor already sits a `CanonCtaHeight` + gutter above the panel edge (the close-band reservation in `Build`), so 0 **is** the top of the CLOSE band. It deliberately does NOT go negative: the plate's bottom 96px is transparent margin and the panel's frame art is under it. |
| 2 | Five rows visible at a **measured** row height before scrolling | New `QueueRowsVisibleTarget = 5` and a derived field `_queueRowPx`. `SeatQueueListToWholeRows` divides the **measured** `_drawerList.rect.height` by the target, clamps into `[ElarionUiKit.MinTouchPx, RowHeightPx]`, and the whole-row trim then seats a whole number of THAT height. `AddQueueRow` builds at `_queueRowPx`. |
| 3 | Per row: number, **icon**, name + level words, status line, progress bar, refund line | The five text/graphic channels already existed. The **thumbnail is new** (WO-1488 section 2's last open item): `QueueRowVM.PortraitKey`, composed in `ManageScreenVM.MakeJobRow` from `ManageArt.BuildingPortraitKey` - the ONE key producer - and painted preserveAspect, raycast-off, between the number and the name. |
| 4 | ONE primary SPEED UP with the crystal cost inside its face, two lines if needed | Unchanged and already correct: `BuildTwoLineCta` puts the model's verb over the model's `FinishCostText` in the single `PrimaryX0..PrimaryX1` slot. What made it read as broken on the device was the row clipping (item 2), not the CTA. |
| 5 | CANCEL as a **full-word** secondary | The face is `"CANCEL"` and the slot is wide enough to hold it. `ClusterX0` `0.455` -> `0.415` (with the text column's right edge moved to the new `QueueTextX1 = 0.40f`, replacing four typed `0.44f`s), and the word controls now use `WordSlot` - an even split of what the compact Ad chip leaves. The arithmetic behind "CANC...": three even slots gave a two-letter face the same ~131px as a six-letter one. |
| 6 | The AD chip compact **only if the rewarded-ad skip is ruled on for that channel** - grep the ruling; cite or remove | **RULED, PER CHANNEL, SO IT STAYS - CITED, NOT REMOVED.** `WorkOrders/WORK_ORDER_911_timer_speedup_crystals_all_channels.md:84-85` mints `CanWatchAdToSkip(ChannelId, string)` / `WatchAdToSkip(ChannelId, string)` precisely so ad-skip is offered on ANY channel, and `BuildTimerService.cs:1160` is that signature. The per-row offer is the MODEL's: `ObsidianQueueVM.cs:208` calls `svc.CanWatchAdToSkip(channel, job.StructureId)`, so BUILD / TRAIN / RESEARCH each answer for themselves. The chip is now **compact** - a fixed `AdChipWidthX = 0.075f`, authored AT `MinTouchPx` against the reference row width and not one pixel of the cluster more. |
| 7 | Active-tab state on the three line plates, by fill/weight | `BuildQueueTabs` now sets the active face BOLD gold with an underline and the inactive faces regular and dim. `MedievalUiSkin.ApplyButton`'s `primary` arm tints the plate `(1.08, 1.03, 0.88)` - about 8% of luminance on a dark plate, invisible on the device and meaningless to a red/green colourblind reader either way. Shape + weight + ink; readable in greyscale. |
| 8 | Empty-state copy per channel (BUILD/TRAIN/RESEARCH verbs), fitted inside the well | Two sentences were wrong and one of them pointed at the wrong door. `ManageScreenVM.QueueEmptyText` is new and composed per active channel; `BuildSlotOffer`'s `"tap TRAIN to fill them"` now reads this channel's verb. **ONE table** - `ManageScreenVM.QueueChannelVerb` - read by both, so the two sentences in the same empty well cannot disagree. The View's hardcoded `"Start an upgrade to see it here."` is gone. |

**Evidence for item 8:** her RESEARCH tab (`-010257.png`) reads *"2 slots free - tap TRAIN to fill
them"*, sliced by the frame. A sentence naming the wrong door is worse than no sentence.

## 3. THE ORACLE

`Assets/Editor/Regression/ManageQueueDrawerRegression.cs` - the verbatim pin at **:273** is
**RE-POINTED, WITH THE RETIRED TEXT KEPT IN PLACE** so it is not moved back. It measured a string;
it now measures the derived height and pins that no row is clipped:

- the queue row is built at `_queueRowPx`, not the authored constant;
- the overlay names a visible-row target at all (a layout with no capacity cannot report a shortfall,
  which is how a one-row drawer shipped green);
- inside `SeatQueueListToWholeRows`: the height comes off `_drawerList.rect.height`, it is clamped
  into `[MinTouchPx, RowHeightPx]`, the trim measures `whole * _queueRowPx` (the height rows are
  actually built at - two units drifting is what slices the last row), and the seating compares what
  it got against `QueueRowsVisibleTarget`.

Case 11 `[rows-inside-the-plate]` still passes its own arithmetic with `DrawerOverlayY0 = 0f`
(replayed by hand: drawer `0.79 * 579 = 457px`; list `457 - 96 - 132 - 24 = 205px`, over the
`132 + 20` floor; `y0 < 0` does not fire).

## 4. ⛔ THE HONEST NUMBER: FIVE ROWS DO NOT FIT, AND THE CODE SAYS SO OUT LOUD

Replayed from the constants at the same 579px reference the suite uses: the list band is about
**205px**. Five rows at `MinTouchPx` need `5*112 + 4*8 + 20 = 612px`. **The well is roughly a third
of what mockup panel 9 asks for.**

The derived height therefore clamps to the touch floor, the trim seats as many whole rows as fit,
and **two FlowTrace warnings name the shortfall in px** - the ideal-vs-floor line and the
whole-rows-vs-target line. Nothing is squeezed under the floor to make the count, because five rows
nobody can press or read is not five rows.

**Owed, and it is a WELL problem, not a row problem:** the Manage well is bounded above by the
workspace header row and below by the close-band reservation. WO-1491 now hides the shared CLOSE on
every non-hub screen, so that reservation is dead space on this overlay specifically - reclaiming it
is the next real lever and it needs a capture to size, not an estimate.

## 5. ACCEPTANCE

- [ ] `MANAGE_FLOW_MAP_OK` on a fresh flowmap log. **OPEN** - no Unity run in this lane. Per section 3
      of the WO, the "all nine screens match" claim is NOT repeated.
- [x] Measured drawer case, RED proof stated. `ManageQueueDrawerRegression`, the re-pointed block at
      the old :273 seat, plus case 11 `[rows-inside-the-plate]` which is unchanged and still green by
      replay.
- [ ] Fresh `ManageFlow_BUILD_queue` PNG opened. **OPEN.**
- [ ] `REGRESSION_OK n/n` on a fresh log. **OPEN.**
- [x] Row thumbnails (WO section 2's last open item).
