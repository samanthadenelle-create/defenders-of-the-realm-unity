# WO-1498 RESULT - the retired tagline is out of the lore array, and an oracle can now see .cs literals

**Status:** FIXED AT SOURCE, UNGATED. Uncommitted in the working tree as of 2026-09-06 21:00, awaiting
the wave-two gate.
**Commit:** none - working tree only.
**Files:**
- `Assets/_Modules/Core/UI/VillageLoadOverlay.cs:59-74` - the literal is gone from the `Lore` array.
  What remains at `:60-65` is a do-not-re-add comment naming the 2026-07-24 retirement and the live
  tagline. Five lore lines remain, none of them the tagline.
- `Assets/_Modules/Onboarding/CanonStrings.cs:45` - the doc-comment now names the string as RETIRED
  instead of quoting it as canon.
- `Assets/Editor/Regression/GlossaryRegression.cs:540` - new `Case7_BannedInUiSource`, registered at
  `:113` as `[ui-source-copy]`. `:89` keeps the string in `BannedInPlayerCopy`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and
committed in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the
current working tree. The wave-two gate is owed.

## 1. What landed

The durable half is case 7. The old case 6 scanned only `glossary.json` and `guide-content.json`, so
no oracle could see a hardcoded UI literal - which is how a three-month-old retirement kept shipping on
the first screen every player sees. Case 7 scans UI source under `Assets/_Modules` (any `/UI/` folder,
plus all of the HUD module) through three filters, each documented as a principle: comments stripped,
diagnostic and frozen-wire statements skipped, bare slug literals skipped. A known gap is stated rather
than hidden - verbatim `@"..."` strings may be missed, and no UI file carries that shape today.

One dated exemption row exists, `ElarionUiKitDemo.cs` count 1, with a proven dev-only reason and a
remove-by of 2026-12-06. It is an exact count, so a second hit still fails, a lower count fails as
drift, and a zero match fails as stale.

## 2. Acceptance

- [x] Zero hits for the retired string outside comments and frozen ledgers. Grep over `Assets/`
      returns four hits, ALL of them comments or the banned-word list itself:
      `GlossaryRegression.cs:89` (the ban entry), `GlossaryRegression.cs:477` (the RED-proof note),
      `VillageLoadOverlay.cs:63` (the do-not-re-add comment), `CanonStrings.cs:45` (the retirement
      note). No live string literal remains.
- [x] Case 7 scans .cs literals. Registered at `GlossaryRegression.cs:113`. The RED proof at `:477-481`
      is a replicated Python measurement over the same globs against HEAD `815c628e9`, 156 files, the
      filter ladder falling 13 -> 3 -> 2 -> 1, the one survivor being `VillageLoadOverlay.cs:65`. It is
      declared honestly as a replication, NOT a Unity run.
- [ ] A fresh loading-screen capture. OPEN - none taken after the edit.
- [ ] `REGRESSION_OK n/n` on a fresh log. OPEN - see the gates line.

Section 3 was respected: the new tagline was NOT substituted into the lore rotation.
Still owed: the real suite run via `DataRegression.RunAll`, and a loading-screen capture.
