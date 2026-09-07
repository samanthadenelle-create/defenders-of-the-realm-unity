# WO-1501 RESULT - the dormant rail now announces its own dormancy; neither of the ticket's two options was taken

**Status:** IMPLEMENTED AS A DEVIATION. The ticket offered exactly two outcomes, Implement or Delete. The
tree took a third: the client path is kept, still pointed at a route that does not exist, and made to say so
out loud. This must be read as a deviation, not a close.
**Commit:** `f957bdbaa` (2026-09-06 20:34).
**Files:**
- `Assets/_Modules/Core/Data/RemoteCatalogService.cs:115-128` - the endpoint constant
  `EndpointPath = "/api/client-catalogs"` now carries a `WO-1501 DORMANT ENDPOINT` banner stating that there
  is no `api/client-catalogs.js` in this repo.
- `:231-242` - at the moment of arming, the service emits a warning naming that the endpoint
  "HAS NO SERVER HALF in this repo (no api/client-catalogs ...)" and cites WO-1501 / WO-1331.
- `Assets/Editor/Regression/RemoteCatalogSeamRegression.cs:69` - case 7 `[endpoint-honesty]`: the endpoint
  constant is either backed by a real route or carries the dormancy marker. The marker string it matches is
  pinned at `:133`; the case is registered at `:176`.
- Verified this session: there is still no `api/client-catalogs` route under `api/`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Case 7 postdates `reg-quiet.log` and has therefore NEVER
EXECUTED - the WO Status line says the same. The wave-two gate is owed.

## The deviation, stated plainly

The ticket's section 3 reads: "Do not leave it dormant with a comment. A dead path that reads as a live seam
is what produced this ticket." What landed is a dormant path with a comment, a runtime warning and an
unexecuted oracle. The warning is a real improvement over silence and the oracle will hold the honesty in
place, but the seam is still not built, and WO-1474 and WO-1331 both remain blocked on it.

## Acceptance

- [ ] Either a live route with an end-to-end retune proven, or the client path deleted and WO-1331 annotated
      - NEITHER. See above.
- [x] `node --test` green across `test/` - `npm test` run 2026-09-06 21:00: tests 424, pass 424, fail 0. No
      new route landed, so no case for one was owed.
- [ ] `REGRESSION_OK n/n` on a fresh log - OPEN, and case 7 has not run even once.

**Still owed:** an owner ruling on which of the two original options to take, then the wave-two gate. No
device capture applies - the rail cannot be armed.
