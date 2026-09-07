# WO-1434 RESULT - the welcome-back popup never presents an uncollectable amount as a gain

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify (return after a silo-capped absence: the row must read `STONE <n> WAITING`, never `+n` that does
not bank) and a headless PNG of the popup at zero headroom.
**Commit:** `5bc5025f5` (`git log -S"BuildReturnRows"` returns exactly that commit). The WO's own §8 is that
lane's handback; the Status was never flipped. This RESULT closes the gap after a read-only re-verification at
source on 2026-09-06. The §15 same-commit canon update the commit missed is in
`docs/MASTER_CATALOG/village-systems.md` (§6.1 EchoService split helpers and the "DumpSilos does not burn" law;
§6.3 `OfflineHarvestResult`'s fifth axis, `BuildReturnRows`, the retired API/string, the oracles) and is
committed with this RESULT.
**Gates:** `REGRESSION_OK 409/409` on `Builds/regression-raid4.log` (13:43, postdates the commit);
`REGRESSION_OK 414/414` on `Builds/reg-final2.log` (18:50).

## Acceptance, verified at source
| Item | Verdict |
|---|---|
| No row presents an uncollectable amount as a gain | MET. `OfflineHarvestRegression.cs:171-225` `[no-gain-without-headroom]`; `ReturnRowLabel` (`OfflineHarvestService.cs:814-817`) emits `<WORD> <Pending> WAITING` when nothing banks, `+{Banks}` otherwise - the `+` number is what banks on every branch. RED proof recorded in-file (`:141-148`). |
| Rendered row count equals aggregated row count | MET. `[every-producer-rendered]` (`OfflineHarvestRegression.cs:238-283`) asserts the units and that the Wood row carries both producers. |
| `[warn-before-collect]` re-pointed with the copy | MET. `"Storage nearly full"` survives only in comments; `PredictCollectWaits` / `AddCollectWaitRows` are gone. |
| D3 answered (recoverable, not burned) | MET. `HarvestResultCopyRegression` `[silo-never-burns]` inverts the old assertion. |
| §5 settled | MET. Hypothesis killed; the cap is correct. |
| `REGRESSION_OK` + headless PNG | HALF. Regression green; the zero-headroom popup PNG does not exist yet. |

Row budget cannot truncate a resource: `WelcomeBackPopup.MaxCollectorRows = 4` (`:56`) and
`ResourceCollectorService.RailOrder` has length 4 (`:52-53`).

## Finding carried forward - minted as WO-1445
`OfflineHarvestService.Grant` (`:1043-1050`) banks the `ClampGrant` result and discards the pre-clamp
remainder; nothing retains `result.Wood - wood`. Dead on the owner's save (`total=0`), but divergent from both
retaining producers.
