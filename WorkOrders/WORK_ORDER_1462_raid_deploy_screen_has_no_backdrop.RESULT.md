# WO-1462 RESULT - the deploy screen takes the kit backdrop; the capture is still owed

**Status:** IMPLEMENTED IN THE TREE, NOT YET GATED. Acceptance 3 and 4 are open.
**Commit:** none - uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate.
**Files:**
- `Assets/_Modules/Village/Hero/RaidDeployScreen.cs:158` - the `BuildObsidianPanel` call no longer passes
  `withBackdrop: false`; it takes the kit default. Rationale block at `:144-157`.
- `Assets/Editor/Regression/RaidSelectionLayoutRegression.cs:273` (case dispatch) and `:694-715` (case body)
  - new sibling case `S7:deploy-opaque-backdrop`. Sibling file constant at `:164`; family addendum at
  `:105-129`; missing-file path is a NAMED failure at `:236-245`.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. The two reds (UI-MVVM violation on `BuildPreviewModal.cs:252-253`; hollow-pass at
`NightMarketNoWalletRegression.cs:761`) were fixed at source in `eb161dc98` (20:10), AFTER both logs. Neither
log postdates `eb161dc98` or the working tree, so the wave-two gate is owed. Measured:
`grep -c deploy-opaque-backdrop Builds/reg-quiet.log` returns **0** - case S7 has never executed.

## What landed

Three transparent layers were stacking, which is why the ticket's premise held: `BuildObsidianPanel` builds
`chrome.content` at alpha 0, `MedievalUiSkin.ApplyShell` re-asserts alpha 0 on it, and the swapped-in shell
sprite `UI/ElarionMedieval/frames/modal-frame-16x9` is hollow. Removing the argument hands the panel the
kit's named 0.94-alpha Backdrop plate rather than a bespoke quad, which section 3 forbids. S7 fails on all
three of: the argument returning, a bespoke plate appearing, and the screen leaving `BuildObsidianPanel`.

## Acceptance

- [x] `withBackdrop: false` gone - `RaidDeployScreen.cs:158`, read at source.
- [x] Sibling case in `RaidSelectionLayoutRegression` covering the raid modal family -
      `S7:deploy-opaque-backdrop` at `:273`/`:694`, alongside the existing selection-door case.
- [ ] A fresh capture of the deploy screen with no world text visible through it - **not captured**. No
      post-fix deploy-screen PNG exists under `Logs/device/screens/`; the only evidence on file remains the
      pre-fix `seeker-357453-raid-deploy.png` named in the ticket.
- [ ] `REGRESSION_OK n/n` on a fresh log - **not run** (see the gates line; S7 count in the log is 0).

**Still needs a device capture:** the deploy screen opened on a post-fix build, proving no world geometry or
world text reads through the panel. Source removal of the flag cannot prove opacity on the device.
