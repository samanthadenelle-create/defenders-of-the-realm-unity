# WO-1470: two ship chains accept a STALE r2-parity.log, and r2-ship can exit before deleting it

**Status:** FIXED - 2026-09-06: overnight-apk-build.ps1 and morning-ship-chain.ps1 now require LastWriteTime >= start AND the marker; PARSE_OK; r2-ship.ps1:107-110 early-exit and the two stale doc lines still open
**Silo:** `overnight-apk-build.ps1`, `morning-ship-chain.ps1`, `tools/r2-ship.ps1`, plus two stale doc lines.
Pairs with WO-1469 (same gate, different caller).
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1470 -> 1471 in the same edit).

## 1. EVIDENCE

```
overnight-apk-build.ps1:113      Select-String on the marker only - no freshness check
morning-ship-chain.ps1:161-163   Select-String on the marker only - no freshness check
tools/r2-ship.ps1:107-110        exits 16 BEFORE deleting the log if tools/r2_sync.py is missing
```

Combined: if `r2_sync.py` is absent, `r2-ship.ps1` bails with yesterday's `R2_PARITY_OK` still on disk, and
both chains then read that stale marker as today's proof. Marker-on-a-fresh-log is the whole point of sec.16
and neither chain checks the freshness half.

Two doc lines are also stale, asserting the opposite of the code:

```
BATCH_STATE.md:351              "verifies ONE explicit target"
CLI_LANES_WO_NUMBERS.md:20      "verifies ONE explicit target"
```

The log in fact verifies all three targets.

## 2. FIX SHAPE

- Add the same LastWriteTime freshness assertion the AAB script uses (`google-play-aab-build.ps1:314-337`) to
  both chains.
- In `r2-ship.ps1`, delete the stale log BEFORE any early exit path, so a bail can never leave a passing
  marker behind.
- Correct the two doc lines in the same commit (canon-in-the-same-breath, sec.15).

## 3. WHAT NOT TO DO
- Do not re-inline the push or verify commands into either chain.

## 4. ACCEPTANCE
- [ ] Both chains refuse on a stale log; proven by touching the log back and running each.
- [ ] `r2-ship.ps1` leaves no passing marker on any early exit; proven by renaming `r2_sync.py`.
- [ ] Both doc lines corrected in the same commit.
