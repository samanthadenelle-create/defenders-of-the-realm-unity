# WO-1382: Manage - Troops screen - the troop detail card floats over the roster; redesign the screen

**Status:** READY TO IMPLEMENT - owner picked the independent review's wireframe 2026-09-04 22:45 (pasted back verbatim, section below); View-only lane on ManageScreenPanel.cs once the RCA section lands

**Owner (2026-09-04 22:34, felt-test on the Seeker, build 2026.09.05.355872), verbatim:**
> "something is wrong with manage troops screen i think its the box around train, can you have UI
> redesign this screen more intuitively?"

**Evidence (captured, not inferred):** `docs/qa/seeker-manage-troops-2026-09-04.png` (2670x1200,
adb screencap, 22:34). Surface: `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs` (the only
file under `_Modules` that authors "OPEN ARMIES" / "UPGRADE OPTIONS" / "Saved army compositions").

## What the screenshot shows (each item is visible in the PNG)

1. A **troop detail card** ("Available / Front-line melee bruiser. Day-one workhorse. / TRAIN /
   UPGRADE OPTIONS") is drawn as a framed box **on top of** the roster list: its frame cuts through
   the troop portrait column on the left, and it covers the rows above and below the selected unit.
2. **Two TRAIN buttons for the same unit** - one inside the floating card, one on the "Train Footman
   / 550 gold / Ready" row beneath it. Which one is the door is not readable.
3. The **row's own label and price ("Train Footman", "550 gold") sit half under the card's bottom
   edge** - the card's y-extent is not reserved in the list layout; it is an overlay.
4. **Scroll arrows top-right are clipped** by the card's frame.
5. The lane chips (Builders 0/2 / Training 0/2 / Research 0/2) and the "Saved army compositions /
   OPEN ARMIES" row are fine and stay.

Read: the detail card was added as an overlay onto a list that never reserved room for it, so
every state where a unit is selected collides. This is a layout-ownership defect, not a styling nit.

## Design direction (tie-breaker: what would Clash of Clans do - owner's standing default)

CoC's Train Troops screen: a **grid of troop cards** (portrait, name, cost) with the **training
queue bar across the top**; tapping a card **trains one** immediately (the card IS the button);
tapping the **info "i"** opens a separate **modal** with the description / level / upgrade. There
is never an inline expandable box inside the grid. Proposed for us, for the UI seat to mock:

- Roster = one card per unit: portrait, name, gold cost, Ready/Locked state, ONE action face
  ("TRAIN"), consistent card height; the list reserves its own rows, nothing floats.
- Description + "Upgrade options" move to an **info modal** (tap the portrait or an "i" glyph) -
  the existing `ElarionUiKit.BuildObsidianModal` family, one modal on screen at a time.
- The Training 0/2 chip stays as the queue glance; queue rows remain in the Queue drawer.
- Locked units keep the lock glyph and say WHY (barracks level), in words, greyscale-safe.
- Touch targets >= MinTouchPx; ASCII-only strings; no meaning by colour alone.

## Acceptance (for the implementation lane, after the mock is approved)

- [ ] No element of the Troops tab overlaps another at any scroll position, any selected unit
      (headless UI capture at 2670x1200 + the owner's device).
- [ ] Exactly ONE "TRAIN" face per unit; tapping it enqueues through `BuildTimerService` (the one
      mechanism) and the Training chip increments.
- [ ] Description / upgrade live in a modal; `ModalArbiter` registered; dismiss returns to the same
      scroll position.
- [ ] `ManageTroopsTrainDoorRegression` + `UiObsidianConformanceRegression` + the UI capture pass
      stay green; add a capture case for "unit selected" if none exists.
- [ ] Owner felt-test on the Seeker.

## Not in scope
Troop balance, costs, the hire-mercenaries verb (WO-1372, shipped tonight), the Armies drawer.

## Hand-off
- UI seat: mock the grid + modal against the screenshot; mint nothing (this WO is the number).
- CLI: attach file:line RCA of the current layout (in flight, read-only agent), then implement on
  the owner's pick.

## OWNER RULING 2026-09-04 22:45 - the layout (pasted back by the owner, verbatim)

Source: `docs/qa/INDEPENDENT_REVIEW_manage_troops_2026-09-04.md` section C (the independent review,
formed without reading this WO). The owner pasted this wireframe back as the pick. It supersedes the
"Design direction" section above where they differ (notably: NO separate info modal - the description
lives on the selected-troop card; UPGRADE is a second button on the same card; a TRAINING NOW band
mirrors the Train line).

```
BACK               MANAGE - TROOPS                 QUEUE
Builders 0/2       Training 1/2 . 1/5       Research 0/2
[ TROOPS rail ]   [ SELECTED TROOP card ]
 Footman L1  >     [portrait] FOOTMAN                LEVEL 1
 Archer  L1        Front-line melee bruiser. Day-one workhorse.
 Shield  lock T2   Train one: 550 gold . 40 sec . Ready
 (scroll)          [ TRAIN 1 FOOTMAN ] [ UPGRADE TO L2 ]
                   Upgrade: 300 wood . 120 iron . Ready
[ TRAINING NOW band ]  Footman ########.. 32s   Archer . queued 2nd   [ OPEN QUEUE ]
Saved army compositions                          [ OPEN ARMIES ]
                         [ CLOSE ]
```

Binding details from the review the lane must honour:
- ONE verb per button, different words: `TRAIN 1 FOOTMAN` (primary) / `UPGRADE TO L2` (secondary);
  the boxed TRAIN/UPGRADE mode TOGGLE is deleted (it was a no-op that looked like the door).
- Name band always on screen (the current one is clipped above the strip); pager arrows deleted; the
  rail is a scroll list, selected = gold outline AND a `>` chevron; locked = dim + padlock + tier word.
- Fact line is a sentence: `Train one: <cost> . <time> . <state>`; the tap's consequence shows in the
  TRAINING NOW band without opening the drawer. Finish Now / Ad / Cancel STAY in the drawer;
  `OPEN QUEUE` calls the existing `ToggleQueueDrawer`.
- The band is a NEW method called from `RenderTroopsDestination`, never `AddQueueRow` inside
  `RenderList` (ManageQueueDrawerRegression pin). VM unchanged where possible; MVVM strict.
- Touch targets >= MinTouchPx; ASCII-only; state by words/shape never hue.
- Three owner questions from the review remain OPEN until she answers (count picker yes/no; BACK
  keep/merge; rail names/icons) - implement the review's defaults (one unit per tap; BACK kept;
  names beside medallions) and flag them in the RESULT.
