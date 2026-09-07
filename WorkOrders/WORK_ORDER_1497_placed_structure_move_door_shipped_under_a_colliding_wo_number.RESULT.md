# WO-1497 RESULT: the placed-structure MOVE door -- SHIPPED

**Status:** FIXED - ON THE SEEKER 2026.09.07.358574
**Shipped in:** `32659c0f6` -- *"feat(manage,build): a door to move a placed structure, and the Manage
screens rebuilt against the owner's mockup"* (Samantha DeNelle; 348 files changed, 8084 insertions,
931 deletions).
**Recorded by:** tooling/docs lane 2026-09-06 (read-only; no Unity, no code edit). This file is the
missing record for work that was already on the device -- there was nothing left to implement.

## 1. WHY THIS RESULT EXISTS AT ALL

The feature shipped under a WO number that belongs to a different ticket, so the board never showed it.
**Three numbers point at this one ticket, and all three mean WO-1497:**

- the commit body opens `WO-1445 - the tester's blocker.` -- but `WO-1445` on disk is
  `WORK_ORDER_1445_offline_grant_discards_clamped_remainder.md`, an unrelated economy ticket minted the
  same day;
- `Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs:76` cites the same card as
  `WO-2006 (ruling section 25)`;
- this ticket, WO-1497, minted from the `CLI_LANES_WO_NUMBERS.md` banner, is the one that owns it.

**Read `BuildCollectionBrowser.cs:76`'s "WO-2006 (ruling section 25)" and the commit message's "WO-1445"
as references to THIS ticket.** Verified at source this session:

```
BuildCollectionBrowser.cs:76   /// WO-2006 (ruling section 25) - open the category root, with the MANAGE PLACED door.
git show 32659c0f6            "WO-1445 - the tester's blocker. \"He accidentally put a palisade down...\""
```

## 2. WHAT SHIPPED

Quoting the commit body:

> The Move, Sell and Upgrade controls were LIVE THE WHOLE TIME. BuildSelectionUI has built all four verbs
> since it shipped, wired by BuildModeController. The only route to them was an unnamed gesture - tap a
> placed structure while already in build mode - and nothing on screen ever mentioned it. Not a missing
> feature; a missing signpost, the same species as WO-1430's three doorless panels.

> Fix: one more category card, "Manage Placed", beside the existing ones in the build browser. It clears
> any armed placement, says "Tap a building or wall to move, upgrade or sell it", and gets out of the way -
> the existing select loop does the rest. No new panel, no second selection owner. Not built when zero
> structures are placed, so it cannot open onto an empty map.

## 3. THE THREE RULINGS, QUOTED FROM THE COMMIT BODY

> Ruling 25's three open questions answered AT SOURCE, not assumed: a palisade IS selectable
> (BaseLayoutLoader attaches PlacedStructure to every row, walls included); SELL refunds ~50% of invested
> cost, deliberately different from WO-911's flat-100% cancel rule; MOVE is free and preserves level.

1. **A placed palisade IS SELECTABLE** -- `BaseLayoutLoader` attaches `PlacedStructure` to every row,
   walls included. This was the tester's blocker ("he accidentally put a palisade down and now he has no
   way to move the Palisade").
2. **SELL refunds ~50% of invested cost; CANCEL refunds a flat 100%.** Two different doors at two
   different prices -- the ~50% sell is *deliberately* different from WO-911's flat-100% cancel rule, so
   the two must read differently on the face.
3. **MOVE is FREE and PRESERVES LEVEL.** Relocating a placed structure costs nothing and does not reset
   its level.

## 4. FILES (from `git show 32659c0f6 --stat`)

The move door itself:

```
Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs          119 +-
Assets/_Modules/Village/BuildMode/BuildModeController.cs              66 +
Assets/_Modules/Village/BuildMode/BuildInventoryModel.cs              36 +
Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs                   15 +-
Assets/Editor/Regression/PlacedStructureDoorRegression.cs            282 +   (new, + .meta)
Assets/Editor/Regression/BuildCollectionPlayerRegression.cs           20 +-
```

Landing alongside it in the same commit (the Manage-screen rebuild, WO-1443/WO-1444 in the body):

```
Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs               772 +-
Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs                  178 +-
Assets/_Modules/Core/Manage/ManageWorkspacePanel.cs                  884 +-
Assets/_Modules/Core/Manage/ManageArt.cs / ManageViewContract.cs / ManageVmProjection.cs
Assets/Editor/Regression/ManageOneHeadingRegression.cs               359 +   (new)
Assets/Editor/Regression/ManagePortraitCoverageRegression.cs         585 +   (new)
Assets/Editor/Regression/ManageNavigationRegression.cs                62 +-
Assets/Editor/Regression/ManageQueueDrawerRegression.cs               84 +-
Assets/Editor/Regression/HeartSurfaceRegression.cs                    18 +-
Assets/Editor/UICaptureLaunch.cs                                    1024 +-
Assets/{Resources,StreamingAssets}/Data/Canonical/structures-catalog.json  12 +- each
Assets/_Modules/Village/Catalog/Generated/CatalogFallbackData.g.cs    86 +-
docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png                      (the spec, now in the repo)
```

## 5. GATES ON THE SHIPPING COMMIT (quoted, not re-run here)

```
COMPILE_GATE_OK (Builds/cg-ship3.log)
REGRESSION_OK 413/413 suites -- 413 green, 0 red, 0 skipped (Builds/reg-ship3.log, 16:50:07)
```

This lane did NOT re-run either gate (no Unity in this lane) -- the markers above are quoted from the
commit body, and are the shipping seat's evidence, not this seat's.

## 6. WHAT REMAINS OPEN ON WO-1497

Two of the ticket's four acceptance boxes are documentation follow-ups this lane did not close, and they
are named here rather than ticked:

- `BuildCollectionBrowser.cs:76` still cites only `WO-2006 (ruling section 25)`. Adding "= WO-1497" is a
  one-line `.cs` comment edit and is OUT OF SCOPE for this lane (no `.cs` edits). Not done.
- The three rulings in section 3 are NOT yet copied into `DESIGN-DECISIONS.md`. Not done.

Closed by this file: the board now has a row for the work (`python tools/board_build.py`), and the three
rulings live outside a commit message.
