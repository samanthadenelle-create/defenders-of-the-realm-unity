# WO-1413: copy hygiene across several screens - retired words, dev tools on a player Help, unreadable and redundant controls

**Status:** FIXED 2026-09-05 23:5x - part 1 (458baf57f: Help reset confirm, Echo workforce additive copy, Settings slider, Daily Chest CTA, prose; Pause reverted per s8.9 ruling) + part 2 (fixture verbs, live combat skill faces, CopyHygieneRegression with the lead's retired-pet scan correction) gated COMPILE_GATE_OK + REGRESSION_OK 390/390 (RESULT file); device build after the owner's reboot; felt-test closes. *(was: READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review)*

## Evidence
AGREED by both reviewers, CLI SEEN `Builds/ui-capture/Settings_2670x1200.png` (`REVIEW_MERGED.md` row 12;
`REVIEW_A_independent.md` E-6..E-10 and B-5/B-7, `REVIEW_B_independent.md` E8 / E9 / E11 / E13). One line each:
1. Help (`HelpMenu_2670x1200.png` 07:02): `RESET HERO & PET` - retired word, destructive, no confirm; `DEV TOOLS`
   on a player-facing menu. Source: grep this session found NO `"RESET HERO"` literal in any `.cs` under `Assets/`
   (only `HUD/OwnerDevToolsOverlay.cs:190` `OWNER DEV TOOLS`) - the label is composed; locate it first.
2. Echo roster (`EchoRoster_2670x1200.png` 09-01): `x1.5 to every node's yield` - canon is ADDITIVE (CLAUDE.md s7).
   Source: `Assets/_Modules/Village/Harvest/EchoWorkforceVM.cs:189`.
3. Dialogue (`DialogueOptions_2opt_2670x1200.png` 07:02): option `Repair structures` - the retired assignment.
   Grep this session: the string is NOT in `Assets/Resources/Data/Canonical/dialogue/dialogues.json`, so it is
   capture FIXTURE text, not data - fix the fixture, no data change.
4. Defense Report (`DefenseReport_2670x1200.png`): empty-state text light grey on beige - unreadable.
5. Combat HUD (`AdaptiveHudCombat_2670x1200.png`): `SKILL I / II / III` identical icons.
6. Pause (`PauseMenu_2670x1200.png`): RESUME and CLOSE both present.
7. Settings (SEEN): a Music slider AND a Music toggle.
8. Daily chest (`DailyChest_2670x1200.png` 09-01): `AD NOT READY` with no time.
9. Rumor Board (`RumorBoard_2670x1200.png`): two cards `Standing Watch Over the Western Fields 1 / 2`, identical bodies.

## What the player experiences
Nine small lies and dead ends: a word the game retired, a developer door on Help, a multiplier the game does not
have, a reset with no warning, an ad button with no wait time, two cards that look like a duplicate.

## Fix shape (one ticket, one line each; kit primitives, MVVM, words never hue)
1. `RESET HERO & ECHOES` behind a confirm (`ElarionUiKit` confirm modal); `DEV TOOLS` compiled out of release Help.
2. Roster subtitle from `EchoBonusCalculator` (`Echoes 3/6 - harvest +7% together`), never a multiplier word.
3. Fixture option -> `Gather resources` / a live verb from `dialogues.json`.
4. Empty-state text on the same dark card as the left panel.
5. Combat face label = equipped skill's short name; `EMPTY` when unassigned (a nudge to Loadout).
6. Pause: RESUME only (the shell CLOSE is the same verb).
7. Settings: slider only; the toggle goes.
8. `AD READY IN 2m` from the ad cooldown, or the row hidden until ready.
9. Distinct titles or a `Part 1 of 2` chip from the quest chain index.

## Acceptance
- [ ] RED first: `CopyHygieneRegression` - source scan: no `.cs` under `Assets/_Modules` contains `& PET`,
      `to every node's yield`, or an unguarded `DEV TOOLS` in a release build; Pause exposes exactly one of
      RESUME/CLOSE; Settings exposes no Music toggle alongside the slider; combat faces never read `SKILL I`.
      Fails on the current tree (`EchoWorkforceVM.cs:189` at minimum).
- [ ] Headless: `HelpMenu`, `EchoRoster`, `DialogueOptions_2opt`, `DefenseReport`, `AdaptiveHudCombat`, `PauseMenu`,
      `Settings`, `DailyChest`, `RumorBoard` `_2670x1200.png` regenerated (`UI_CAPTURE_OK`), each opened and read.
- [ ] Device: Help, Settings, Pause, Echo roster screencapped; words match.

## Not in scope
Any behaviour change behind these labels (reset logic, ad cooldown length, quest content); the `Master 0%` /
`Mute all audio` default (inferred, fixture - queued check); Talk verbs with no data (graph dead-end 5).

## Owner ruling
- Section 2 #14 Dev-tools-in-Help? - written to the default NO.
- Section 2 #15 Pet? - written to the default YES (`RESET HERO & ECHOES`).
