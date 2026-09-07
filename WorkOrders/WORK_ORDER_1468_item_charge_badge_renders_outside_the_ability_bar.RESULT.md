# WO-1468 RESULT - the charge badge is seated inside the medallion; the proving capture is still owed

**Status:** FIXED AT SOURCE, UNGATED. Uncommitted in the working tree as of 2026-09-06 21:00, awaiting
the wave-two gate. No fresh capture yet, so acceptance 1 stays open.
**Commit:** none - working tree only.
**Files:**
- `Assets/_Modules/HUD/Kit/HudKitController.cs:2424-2492` - the WO-1468 header plus
  `public static void SeatStackBadgeInMedallion(...)` at `:2457`; called at `:2666` from the adaptive
  combat dock build.
- `Assets/Editor/Regression/HudUiRegression.cs:1910` - `CheckStackBadgeInsideMedallion`, registered at
  `:250`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and
committed in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the
current working tree, and neither log covers this lane's edits. The wave-two gate is owed.

## 1. What landed

The cause recorded in-code is not a bad offset. `ElarionUiKit.StyleAsStackBadge` anchored the plate to
the SLOT ROOT at pivot (1,1); `StyleAsRoundMedallion` inscribes the visible art in a square
`MedallionBounds` child in the top 80 percent of a cell that is wider than tall. The cell corner is
outside the square, so the badge was anchored to the wrong rect at every aspect.

The seat anchors the plate's top-right corner at the 45-degree point of the inscribed circle
(0.5 + 0.5/sqrt(2) on both axes of the medallion square), a FRACTION rather than a pixel offset, which
is what section 3 of the WO forbids hardcoding. `SeatStackBadgeInMedallion` is public and static so the
oracle measures the shipping seat, not a re-typed copy. Missing badge and missing medallion each get a
named `FlowTrace.Warn`, never a silent return.

## 2. Acceptance

- [ ] Badge contained by the frame in a fresh `AdaptiveHudCombat` capture. OPEN. The only capture on
      disk is `Builds/ui-capture/AdaptiveHudCombat_2670x1200.png` at 2026-09-05 23:57, which PREDATES
      the fix and is the ticket's own evidence image.
- [x] Measured containment case with a RED proof. `HudUiRegression.cs:1910` builds two real slots
      through the shipping kit calls - one seated, one left at the kit default - and FAILS if the
      unseated control ever measures as contained. Layout that will not resolve headlessly is a named
      skip, never a pass. The stated one-line RED is deleting the `:2666` call.
- [ ] `REGRESSION_OK n/n` on a fresh log. OPEN - see the gates line.

Still owed: a fresh headless `AdaptiveHudCombat` capture at all three aspects, and a device capture on
the next build to confirm the "7" the owner saw on 358574 now sits inside the round face.
