# WO-1471 RESULT - three player-paced modals move to the player-owned hold; the other holders are still unnamed

**Status:** PARTIALLY FIXED. The named holder and the sweep are done in source, uncommitted in the
working tree as of 2026-09-06 21:00, awaiting the wave-two gate. Acceptance 2 is open on a capture.
**Commit:** none. All four files are working-tree modifications.
**Files:**
- `Assets/_Modules/Core/UI/HarvestOverflowModal.cs:105-113` - `WorldHold.Acquire` becomes
  `AcquirePlayerOwned("harvest-overflow-result", ...)` with a liveness probe; `:264` the per-frame
  renew `Update` is DELETED (it was the workaround for the bounded ceiling); `:269` `Close` is now
  idempotent because `OnDisable` steps the hold out; `:287` the host's own lifecycle is the net.
- `Assets/_Modules/Core/UI/FocusedModalHost.cs:38-46,62` - same change for the card modal host, via a
  single `AcquireHold()` helper.
- `Assets/_Modules/Core/UI/ObsidianNavigationWorkspace.cs:53,208` - same for the card-led workspace.
- `Assets/Editor/Regression/WorldHoldLivenessRegression.cs` (+101) - case 6 in two halves: (a) a
  behavioural check that a player-owned hold survives its beat and dies with its owner, and (b) a
  source lint failing any listed player-paced modal still calling `WorldHold.Acquire(<token>)` or
  missing `AcquirePlayerOwned`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [x] `HarvestOverflowModal` uses `AcquirePlayerOwned`, and the sweep is listed above: the WO-1360
      section 3 rows 14-17 player-paced modals, three of which took the bounded default.
- [ ] Every remaining holder NAMED from a captured trace line - OPEN. The device log carried 152
      `WORLD CLOCK FROZEN timeScale=0.00` lines between 12:51:25.175 and 13:27:32.451; the harvest
      modal accounts for one 101-second window (ACQUIRE 12:51:25.157, RELEASE 12:53:06.089). The rest
      are unattributed and no post-fix capture exists to attribute them.
- [x] A regression fails when a player-paced modal takes the bounded handle - case 6(b) above.
- [ ] `REGRESSION_OK n/n` on a fresh log - not obtained, see the gates line.

Owed: a full overworld device session on the post-fix build, judged by the count of
`WORLD CLOCK FROZEN` lines. Any survivor names its own remaining holder.
