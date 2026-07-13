# RESULT — WO-678: Pi SDK 120s promise timeout wrapped cleanly

**Status: IMPLEMENTED + GATED. Owner felt-pass on a non-Pi mobile browser pending (PO closes).**
Commit `66b3272f`.

## What shipped

- `Pi/index.html` owns the global JS error surface: `unhandledrejection` + `window.onerror`
  suppress ONLY the known-benign Pi host-channel class (`promise with id N timed out` /
  not-in-Pi-Browser / `window.Pi undefined`) with one quiet `console.info` + a forward into
  Unity telemetry (`PiBridge.OnPiCallback`). Unknown errors always pass through — no blanket
  swallow (§5 no-silent-failure).
- Loader `showBanner` is owned: genuine loader errors → `console.error` + the loading-bar text;
  warnings/benign → console only. Never a raw alert/default banner.
- C# (`WebGLPiPlatform` / `PiSignInController`): a late/unmatched SDK 'error' arriving after our
  own 20s/30s timeouts already settled is absorbed as a traced Warn, not error-level noise.

## Verification

- `COMPILE_GATE_OK`; template/jslib are exercised only in a web player — verify on the new
  preview: sit past sign-in in a normal mobile browser ≥2 min; expect NO raw
  "promise with id 0 timed out after 120000ms" surface (one `[Pi] … suppressed` console.info is
  the correct behavior).
