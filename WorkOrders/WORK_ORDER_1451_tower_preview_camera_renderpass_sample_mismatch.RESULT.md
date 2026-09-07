# WO-1451 RESULT - the preview RT drops to 1 sample to match its camera; the suite has never run

**Status:** FIXED IN SOURCE, GATE OWED. The camera fix is uncommitted in the working tree as of
2026-09-06 21:00, awaiting the wave-two gate. No session has been captured since.
**Commit:** partly landed - `eb161dc98` (2026-09-06 20:10) committed the DataRegression REGISTRATION
line for this suite; the suite FILE and the camera fix are both still uncommitted.
**Files:**
- `Assets/_Modules/Village/UI/TowerPreviewCamera.cs:99` - `antiAliasing` 2 -> 1 (WO section 2's
  preferred branch); `:146` `_cam.allowMSAA = false` unchanged; `:151-153` a permanent
  `FlowTrace.Step("Orient", ...)` printing BOTH halves of the contract on every `Begin`.
- `Assets/Editor/Regression/PreviewRenderTextureSamplesRegression.cs` (280 lines, new, UNTRACKED) -
  markers `PREVIEW_RT_SAMPLES_OK` / `_FAIL`; pins `allowMSAA == false` implies RT `antiAliasing == 1`,
  measured off the live rig. RED proof stated at its `:42-53`.
- `Assets/Editor/Regression/DataRegression.cs:1666` - the `preview-rt-samples` suite registration.

## What landed

WO section 5's correction is implemented as written: the pin is RT == CAMERA, not RT == pipeline. The
chosen branch is `antiAliasing = 1`, so RT and camera agree at one sample and the pass can close.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The reds were a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` and a hollow pass at `NightMarketNoWalletRegression.cs:761`, both fixed
at source in `eb161dc98` (20:10), AFTER both logs. Neither log postdates that commit or the current
working tree, so the wave-two gate is owed.

## Acceptance

- [ ] Zero `RenderPass` / `EndRenderPass` errors in a 3-minute build-mode session - NOT captured. No
      device or headless run exists after the edit; only a capture can close this line.
- [x] Regression pins the corrected invariant (RT == camera), RED proof stated in the suite header.
- [ ] `REGRESSION_OK n/n` - not obtained. `grep "[preview-rt-samples]" Builds/reg-quiet.log` returns
      NOTHING: the 20:07 run predates the registration, so this suite has never executed once.

## Discrepancy to surface at the gate

`eb161dc98` committed the call site (`DataRegression.cs:1651` in that commit) while
`PreviewRenderTextureSamplesRegression.cs` and its `.meta` remain untracked (`git ls-files` does not
know the file). A fresh clone of `eb161dc98` does not compile.

Owed: one build-mode device or headless capture, plus the wave-two regression run.
