# WO-1521 RESULT - one quest authority, one list, one claim door - and a double-grant P0 found while building it

**Status:** SOURCE COMPLETE except the backdrop - uncommitted in the working tree as of 2026-09-06 21:45, awaiting
the wave-two gate. **Tree contradicts the ticket:** its Status line still reads `READY TO IMPLEMENT` while the work
sits in the tree. (Status line not edited here - RESULT-only lane.)
**Commit:** none. Edit-only lane.
**Files (all `M`):** `Core/Quests/DailyQuests.cs`, `Village/Quests/DailyQuestRewardBridge.cs`,
`Core/HudModel/JourneyDeckSubtitleVM.cs`, `Village/Hero/RumorBoardVM.cs`, `Village/Hero/RumorBoardPanel.cs`,
`Assets/Editor/Regression/RumorBoardLayoutRegression.cs`, `Assets/Editor/UICaptureLaunch.cs`,
`Assets/Tests/EditMode/RumorBoardVMTests.cs`.
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and the owner's report arrived 20:18, so the gate
predates the lane. `Builds/cg-aab.log` (20:54) is RED (42x `CS0103`, the Manage lane's half-written suites).

## 1. P0 FOUND WHILE BUILDING THE DOOR - surface this first

`DailyQuestService.Report` called its private `Save()` **before** `QuestCompleted?.Invoke(q)`, and the bridge writes
`ClaimedAtUnix` from inside that handler. Nothing saved afterwards, so a daily paid as the last act of a session
**reloaded as `Completed && ClaimedAtUnix == 0`** - i.e. claimable. Until today that only made the counter lie; the
moment this ticket gives that state a CLAIM face it becomes a **DOUBLE GRANT**. `Report` now calls `Save()` after
the completion handlers run, the rule `RequestClaim` follows. Also fixed: `RumorBoardLiveBackend.Changed` wired only
`QuestService.QuestChanged`, so a claimed daily would have sat on the board until reopened; it now also wires
`DailyQuestService.SetChanged`.

## 2. The disagreement, closed at its source

`DailyQuestService` owns the claimable fact - `IsClaimable(q)`, `ClaimableCount`, `TodayQuests`, `Find(id)`.
`JourneyDeckSubtitleVM.FromCurrentState` reads `ClaimableCount` instead of its own inline
`Completed && ClaimedAtUnix == 0` loop; that copy WAS the disagreement. `RumorBoardVM.Rebuild` composes one `Rows`
list - claimable dailies, then active story quests, then available offers - claimable leading so the counter's tap
lands on page 0; paging moved from `_available` to `_rows`. One verb per poster: `KindOf` / `ActionLabelFor`
("Claim"/"Go To"/"Accept") / `Invoke`; the View never branches on kind. `RequestClaim` returns the payer's VERDICT,
never "an event was raised", so a claim that credits nothing keeps its row and says why (WO-978's full-bank case).
There is still exactly ONE payer - `DailyQuestRewardBridge.HandleQuestCompleted`, reused for the new
`ClaimRequested` event, so the `_payingOut` re-entrancy set and the `ClaimedAtUnix` latch are the existing ones.
`RumorBoardPanel.Repaint` gated the quiet copy on `shown == 0` (THIS PAGE); it now gates on `RumorBoardVM.IsQuiet`
(the whole LIST).

## 3. Acceptance

- [x] A claimable quest produces a board ROW with objective and CLAIM door - `ObjectiveFor` + `Invoke`, five new
      measured cases in `RumorBoardVMTests.cs`.
- [x] Counter and board agree, from the one authority - sec.2.  [x] The quiet copy never paints while non-empty.
- [x] Door assertions follow `WelcomeBackDoorsRegression`'s pattern - `[source-laws]` re-pointed (the face is now
      `ActionLabelFor`; `GoTo`/`ClaimDaily`/a Track COMMAND must exist).
- [ ] Headless `RumorBoard_*.png` captured and opened - **OPEN**. `UICaptureLaunch`'s `WorstCaseRumorBackend` now
      carries one claimable daily so the shot proves all three row kinds, but no capture was run.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 4. NOT DONE, deliberately - the backdrop

UNPROVEN, so untouched. WO-1462's shape does not apply: `RumorBoardPanel` calls `ElarionUiKit.BuildObsidianModal`,
which passes `withBackdrop` at its default `true` (`ElarionUiKit.cs:568,573-579`), so a 0.94-alpha full-screen
Backdrop IS built, and `MedievalUiSkin.ApplyShell` never touches it. The device frame plainly shows the town, so
something removes or hollows it - naming that from a source read would be the inference-fix CLAUDE.md sec.12 bans.
**Owed: one runtime hierarchy dump of `RumorBoardPanelUI` with the Backdrop's `Image.color.a` and
`activeInHierarchy`**, split to its own ticket. Also owed: the correction to WO-1477 (PREVIOUS already exists on
this screen) is recorded in that ticket and needs its own verify.

## 5. Second pass - 2026-09-06: the P0 is PINNED, and the backdrop is measured not guessed

**The P0 was already fixed by the first pass** - `DailyQuests.cs:324`, the trailing `Save()` inside
`if (justCompleted != null)`. It had no test. It has one now:
`DailyQuestEmptyStateRegression` case 6 `[claim-latch-persists]` - a FIXTURE, not a lint. It injects
today's set into a real `DailyQuestService` (no catalog, no RNG, no scene), subscribes a stand-in for
`DailyQuestRewardBridge` that stamps `ClaimedAtUnix` from inside `QuestCompleted`, calls `Report`, and
asserts the PERSISTED PlayerPrefs blob carries a non-zero latch (prefs snapshotted/restored). The
pre-fix ordering fails it by construction; it cannot be re-run from here, so that is stated, not run.

**Backdrop.** sec.4 said UNPROVEN. It still is - and a blind "add a backdrop" would have stacked a
second one on the kit's own. `RumorBoardPanel.EnsureBackdrop` instead TAKES the owed hierarchy dump on
every open (`present / drawn / alpha / image / canvasChildren` into `FlowTrace.Step("RumorBoard")`) and
repairs only when `BackdropNeedsRepair(present, drawn, alpha)` says the invariant is broken - through
`ElarionUiKit.AddImage`, never a hand-rolled `Image` (the hand-rolled-uGUI law is armed on this file).
`RumorBoardLayoutRegression` case 6 `[backdrop]` fixtures that pure predicate on four states and pins
that the dump is still taken.

**Files:** `Village/Hero/RumorBoardPanel.cs` (+~85), `Assets/Editor/Regression/RumorBoardLayoutRegression.cs`
(+~85, suite now 6 cases), `Assets/Editor/Regression/DailyQuestEmptyStateRegression.cs` (+~120, now 6 cases).
**No new registration** - both suites are already registered; `DataRegression.cs` untouched.
