# WO-1474 RESULT - BaseRateFor is wired in and the three rates are authored; the live split DOES move

**Status:** FIXED IN SOURCE, GATE AND AN OWNER RULING OWED. Uncommitted in the working tree as of
2026-09-06 21:00, awaiting the wave-two gate.
**Commit:** none. All five files are working-tree modifications.
**Files:**
- `Assets/_Modules/Village/Harvest/EchoBonusCalculator.cs:190` `HarvestTargetWeights()`; `:211-214` the
  authored weight finally applies, `rate * Mathf.Max(0f, EchoBalanceCatalog.BaseRateFor(entry.Id))` -
  the roster entry was resolved and then discarded before today. `:172-184` the header is corrected in
  the same change to describe what the code does; `:89` records the removed consts.
- `Assets/_Modules/Village/Harvest/EchoBalanceCatalog.cs:43-55` the `harvestRatePerHour` block
  (common 3600 / gold 900 / crystals 4); `:144-157` the three accessors, clamped at zero so a bad
  authored row cannot go negative; `:171` `BaseRateFor` now has a production consumer.
- `Assets/Resources/Data/Canonical/echoes-balance.json:25` and its StreamingAssets twin -
  `"harvestRatePerHour": { "common": 3600.0, "gold": 900.0, "crystals": 4.0 }`.

**Authority chosen:** `BaseRateFor` is KEPT as the authority for the per-echo weight; the rate class is
the authority for the per-hour number. Nothing was left dead - `BaseRateFor` had zero callers repo-wide
before this change and now has one.

**Canonical JSON proof:** both twins are byte-identical (md5 `33205fb0c37ce57dfb10eff3c038599d`). LF
count HEAD 25 -> working tree 26 in each, i.e. exactly the one added row and no flattening.

## Deviation from WO section 3, stated plainly

Section 3 says the live split must be identical before and after. The RATE-CLASS move is split-neutral
(same three numbers), but wiring `BaseRateFor` in DOES move the split, because the authored rows were
dead: Wood x1.1 (`echo-verdant-stag`), Iron x1.15 (`echo-stonewarden-bear`), Crystals x0.45
(`echo-ember-phoenix`); Food and both Gold Echoes are authored at 1.0 and do not move. That is the
point of the ticket, and it is what makes the WO-830 section 3b crystals guard actually run. It is
recorded in the file's `_authoringNotes` and needs the owner's eye.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [x] One authority, the other alive rather than deleted - `EchoBonusCalculator.cs:214`.
- [x] The three rates authored in `echoes-balance.json`, LF count proven above.
- [x] Regression asserts the arithmetic - `Assets/Editor/Regression/EchoSpecializationRegression.cs:610-641`
      pins the three authored rates to the literals they replaced, pins each weight to
      `rate * BaseRateFor(id)`, and re-asserts Crystals stays the smallest weight.
- [ ] `REGRESSION_OK n/n` on a fresh log - not obtained, see the gates line.

Owed: the wave-two regression run, plus the owner's call on the split movement. No device capture is
required; a `DumpSilos split` line on the next build would confirm it.
