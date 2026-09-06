# WO-1404: Journey deck subtitles truncate and carry no state - the two cards never say what is waiting

**Status:** FIXED - ON THE SEEKER in build 2026.09.06.357453 (chain 00:31-00:38: APK_OK 463MB, R2_PARITY_OK objects=271; installed 00:41, versionCode 357453 read off dumpsys; Firebase App Distribution release 0kka4h6t9u400); owner felt-test closes 2026-09-05 23:16 - Codex lane + s8.9 rework landed (Core VM, change-only publisher, locked camps excluded), JourneyDeckSubtitleRegression green, COMPILE_GATE_OK + REGRESSION_OK 389/389 + UI_CAPTURE_OK, Journey frame opened (RESULT file); device build after the owner's reboot; felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

## Evidence
- `Builds/ui-capture/JourneyWorkspace_2670x1200.png` (09-05 07:02) - SEEN (`REVIEW_MERGED.md` row 3). Two cards:
  `QUESTS - Read active quests and realm r...` and `RAIDS - Choose a camp and deploy your...`. Both subtitles
  truncate; neither names a number. The WO-1389 `Army 3 / 10` subtitle is NOT on this frame - the fixture has no
  army, or the line has not landed; UNPROVEN which (merged review section 3 lists the check).
- No device capture of the Journey deck exists (`INDEX.md` row 16 is a HUD frame, mislabelled - merged section 0).
- Both reviewers: `REVIEW_A_independent.md` B-3, `REVIEW_B_independent.md` B3.
- CODE: the army numbers already have one producer, `PostureSignals.SetArmyFill(used, cap)`
  (`Assets/_Modules/Core/HudModel/PostureSignals.cs:321`, fed by `BuildTimerService.PublishArmyStatus`
  `Assets/_Modules/Village/Buildings/BuildTimerService.cs:2199`).

## What the player experiences
A deck of two cards whose subtitles are the first half of a verb-phrase. Nothing says two camps are open,
nothing says a quest is ready to claim; the card gives no reason to be the next tap.

## Fix shape (one mechanism)
Subtitles become STATE, from the VMs that already hold it - drop the verb-phrase entirely:
- Raids: `Army 3 / 10 . 2 camps open` (army from `PostureSignals` fill; camps from `RaidSelectionVM` where
  garrison <= deployable, the same predicate WO-1402 uses for its lock word).
- Quests: `2 active . 1 ready to claim` (from the quest/rumor VM's counts).
Single line, fitted by the kit label (no `...`); when a count is 0 the words still render (`Army 0 / 10 . train
to open a camp`), never blank. First step: capture the deck on a fixture WITH an army to settle the WO-1389
question before writing - if the 1389 subtitle IS landing, this ticket only reshapes its text.

```
[ QUESTS ]  2 active . 1 ready to claim
[ RAIDS  ]  Army 3 / 10 . 2 camps open
```
Trace: `FlowTrace.Step("Journey", "deck card=<Quests|Raids> subtitle='<text>'")` per built card.

## Acceptance
- [ ] RED first: `JourneyDeckSubtitleRegression` - fixture army 3/10 with one open camp: Raids subtitle
      contains `3 / 10` and `1 camp`; Quests subtitle contains `active`; no subtitle contains `...` or a verb
      from the old copy (`Choose`, `Read`). Fails on the current tree.
- [ ] Headless: `JourneyWorkspace_2670x1200.png` regenerated on the army fixture, opened and read.
- [ ] Device: HUD > JOURNEY; both subtitles read as numbers; screencap read (closes the INDEX 16 gap too).

## Not in scope
The deck's card set (Season Track WO-1394, Realm Map WO-1396); the raid rows (WO-1402); quest content.

## Owner ruling
None from section 2 - the ticket depends only on the WO-1389 army-status producer already ruled in.
