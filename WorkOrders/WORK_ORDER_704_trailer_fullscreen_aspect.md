# WORK ORDER 704 — Intro trailer: true full-screen at correct aspect on every surface (VID-1)

**Status: READY TO IMPLEMENT** (owner ask 2026-07-13: "can the trailer play full screen?").
**Lane:** UI/Onboarding (View-only). **Type:** EXISTING (display defect).

## Verified from code (IntroSequencePlayer.cs)

- The video surface IS full-screen-stretched (RawImage anchored 0,0->1,1, `:266`).
- **Defect:** the RenderTexture is hardcoded **1080x1920 portrait** (`:165`) with NO aspect
  fitter — correct on a portrait phone, **stretched/distorted on desktop or any landscape
  window**; a 16:9 source trailer is squashed into the portrait RT everywhere.

## The fix

1. **RT sized from the source:** once `isPrepared`, allocate the RenderTexture from
   `VideoPlayer.width/height` (release + reallocate; keep the portrait default only as the
   pre-prepare placeholder).
2. **AspectRatioFitter, ENVELOPE mode** on the VideoSurface (fill the screen, crop overflow —
   cinematic full-bleed, never letterbox bars, never distortion), ratio set from the prepared
   video dimensions. The fallback slate keeps its current behavior.
3. **WebGL browser-fullscreen (best-effort):** on the intro's first tap (the existing
   skip/interact gesture — fullscreen APIs require a user gesture), request
   `Screen.fullScreen = true` on WebGL; exit on intro end. Quiet-fail per web-errors law
   (never a visible error if the browser refuses).

## Acceptance
- [ ] Desktop landscape window: trailer fills the screen, no distortion (crop, not stretch).
- [ ] Portrait phone: unchanged full-bleed.
- [ ] WebGL: tapping during the intro enters browser fullscreen where permitted; refusal is
      silent; intro end restores.
- [ ] Fallback slate path unchanged; skip flow unchanged.
- [ ] COMPILE_GATE_OK + owner felt-pass on desktop + phone (PO closes).

## What NOT to touch
The URL-source/Prepare/timeout flow (SplashLoading-proven) · audio Direct routing (flagged
separately for the owner) · the fallback slate sequence.

*Cross-refs:* ticket VID-1 · `IntroSequencePlayer.cs:165,:266` · web-errors-caught-quietly law.
