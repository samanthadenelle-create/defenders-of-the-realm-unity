# WO-1408: Welcome-back reports resources, then COLLECT lands on a HUD with no next door

**Status:** READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review (sprint: the reason to tap the next screen)

## Evidence
- `Builds/ui-capture/WelcomeBack_2670x1200.png` (09-05 00:26) - SEEN (`REVIEW_MERGED.md` row 7): per-resource rows,
  the Echo mending lines (fresh), a single `COLLECT`. Nothing says a build or troop finished, the town was
  attacked, Heartfire is full, or the army is ready. Device: `docs/qa/UI_REVIEW_2026-09-05/00-title-or-hub.png`.
- Both reviewers: `REVIEW_A_independent.md` E-2, `REVIEW_B_independent.md` "The through-line" + E1.
- CODE: `Assets/_Modules/Village/Harvest/UI/WelcomeBackPopup.cs:30` (`WelcomeBackPopup`); COLLECT calls `Dismiss`
  (`:20`, `:107-108`). The away-time data already exists per line: build/train completion in the Obsidian queue
  (`BuildTimerService`), attacks in the Defense Report, army fill in `PostureSignals.SetArmyFill`
  (`Core/HudModel/PostureSignals.cs:321`), Heartfire in the posture model.

## What the player experiences
The return moment is the one screen the player reads for sure. It reports what they earned and then drops them
on the HUD with nothing ready-looking to tap; the loudest thing left is the store card. A returning player's
first reason to tap is to spend.

## Fix shape (one mechanism)
`WelcomeBackVM` gains an optional-rows list, each row = (label, door PanelId, tab). Rows are data-driven and only
present when true:
- `FINISHED WHILE AWAY  Footman x1, Arcane Spire L2` -> door `PanelRouter.Open(PanelId.Manage, "<tab>")`;
- `ATTACKED 1x - north gate breached` -> door Defense Report;
- one line above COLLECT when true: `Heartfire is full - a wave is ready` / `Army 3/10 ready - The Forsaken
  Camp awaits`, with a second SMALL door (`START WAVE` / `RAID`) beside COLLECT. COLLECT remains the primary and
  still dismisses; the doors collect first, then route (one path).

```
WELCOME BACK - away 6h 12m
Wood +1200   Iron +340   Crystals +12
FINISHED WHILE AWAY  Footman x1, Arcane Spire L2        [ MANAGE > ]
Army 3 / 10 ready - The Forsaken Camp awaits
                      [ COLLECT ]        [ RAID ]
```
Trace: `FlowTrace.Step("WelcomeBack", "rows finished=<n> attacked=<n> ready='<none|wave|raid>'")` once per open.

## Acceptance
- [ ] RED first: `WelcomeBackDoorsRegression` - fixture with one completed job and one recorded attack: two rows
      present with the named words and doors; fixture with nothing: zero rows, COLLECT alone (no empty rows);
      army-ready fixture: the RAID door exists and routes to Journey/Raids (trace). Fails on the current tree.
- [ ] Headless: `WelcomeBack_2670x1200.png` regenerated on both fixtures (`UI_CAPTURE_OK`), opened; fits, no `...`.
- [ ] Device: relaunch after a queued job completes offline; the row reads and its door lands on Manage; screencap read.

## Not in scope
Resource-row content (WO-1392 fixed); the Daily Chest (capture gap, opener unproven); Echo mending copy.

## Owner ruling
None from section 2 with a default - Reviewer B's "Return-rows? default yes" is folded in; ruling #3
(Heartfire-does?) is NOT needed here because the line only states "full", not what it buys.
