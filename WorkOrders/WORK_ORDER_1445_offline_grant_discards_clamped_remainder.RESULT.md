# WO-1445 RESULT - the discard is no longer silent, and it finally reaches the screen

**Status:** IMPLEMENTED - 2026-09-07, UNCOMMITTED, awaiting gate. Edit-only lane: no Unity, no gate,
no git. Nothing below was RUN; every claim is read at source and cited.

## CONTRADICTION - the ticket's fix shape does not exist
"Retain the remainder on the same pending store the producers use" - **there is no such store here.**
The retaining pool is `ResourceCollector.PendingAmount`, PER COLLECTOR. `Grant`'s income is MINE
NODES / SETTLEMENTS / PETS (`OfflineHarvestService.cs:985-1071`); none has a collector. Pushing it
into an unrelated collector re-mints the WO-1392 two-producer defect and hits that collector's cap;
a new persisted store is a schema bump, out of lane. So the units are NOT ADDED and the screen says
so - which the owner's own law allows ("if a remainder must be discarded because no cache exists,
the screen says so in words"). **Lead ruling wanted** on whether away yield should ever gain a pool.

## What changed - `OfflineHarvestService.Grant`
1. **No more `out _`.** The three clamps capture their loss; each non-zero one raises a permanent,
   unthrottled `FlowTrace.Warn` via the new `WarnDiscarded` - `away haul NOT ADDED [Wood]: accrued
   N, banked M, K could not be stored`. This path previously left NO line at all, which is
   indistinguishable from income that never accrued.
2. **`Grant` now opens a WARN SCOPE** (`BankOverflowToastPresenter.BeginWarnScope("OfflineHarvest")`)
   - a SECOND, LARGER defect found at source, and the reason the away haul's overflow has never once
   been on the player's screen. `BankOverflowToastPresenter.OnOverflow` opens with
   `if (_scopeDepth <= 0) return;` (`:162-171`, *"Ruling 3 (WO-1207): NO SCOPE, NO SCOLD"*). `Grant`
   had no scope, so the presenter dropped its three events for the whole life of the code. The scope
   stamps `Source` and calls `HarvestOverflowModal.Present` (`:250-256`) and already merges repeats
   per resource - so NO `BeginBatch` here; a second aggregation is the shape WO-1392 was about.
   *(I first wrote `BeginBatch` and it would have collected nothing. Corrected before hand-off.)*
   **Limit:** the presenter's per-resource cooldown can return before `Present`, so a second
   overflowing claim in quick succession is deliberately silent on screen. The warn is not.
3. **One claim-summary line**: `away claim banked: away=Ns capped=B | WOOD asked/banked/notAdded/cap
   | IRON ... | STONE ... | CRYSTALS ...`. One grep reconciles the window to the banked deltas.
4. The `Debug.Log` no longer says "the surplus was LOST" without naming why.

The row copy stays **"N lost"**: WO-1434 forbids calling a RETAINED amount lost; it does not license
softening a genuine discard. The FOOTER explains - *"Storage was full, so the amounts marked lost
never reached it - away gathering has no store to wait in. Make room first."*

## !! WATCH FIRST ON THE DEVICE - a NEW ordering interaction I introduced and did not run
`Grant` runs inside `ApplyOfflineWindow`, BEFORE `OnClaimCompleted` raises the welcome-back popup.
So on a resume whose haul overflows, a HARVEST RESULT modal can now open FIRST and WELCOME BACK
second - both on the same exclusive `PanelManager` arbiter. Before this change the question could
not arise (the events were dropped). Grep tomorrow for `harvest-result modal for [OfflineHarvest]`
and check `welcome-back REVEAL` still lands. If they collide, defer the scope's emission to
`OnClaimCompleted`; do NOT drop the scope again. Flagged, not guessed at (CLAUDE.md 11B).

## The fixture
`HarvestResultShapeRegression` case 8 `[burn-never-lies]` already drives an `OfflineHarvest`-sourced
row and asserts it says "lost" with NO reassurance footer - unchanged, still the proof.
**The ticket's "assert pending carries 500" case is NOT written: there is no pending store to assert
against, and writing it would pin a mechanism the tree does not have.**

## Gate evidence in-lane
Braces 200/200, zero NULs, zero non-ASCII added, LF preserved, FlowTrace kept and extended.
Stale reference for the lead: `HarvestBoostService.cs:222` quotes the old log text in a comment.
