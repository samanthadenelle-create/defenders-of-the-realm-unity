# RESULT — WO-1030 Echo dialogue options clipped + placeholder portrait

**Date:** 2026-08-16  **Seat:** CLI (commit `323f3c97f`)
**Status:** IMPLEMENTED - pending PO felt-verify

## What changed

1. **DEFECT A — reserve-first sizing fix** in `DialogueView.cs`: the options band's full
   measured height is reserved FIRST, then the remainder goes to the text well — the TEXT is
   the scrollable element, the choice list is fixed. Options never silently clip; a 2-option
   node fits with zero scrolling at every aspect ratio. Option rows respect the mobile touch
   standard (MinTouchPx 112). The `_maxBodyPx` HUD-safe clamp and the `[Flow:Dialogue] resize`
   trace are preserved (WO "Do NOT" list respected).
2. **DEFECT B — Echo portraits:** Echo portrait speaker records added to BOTH `dialogues.json`
   twins for the three Echo speakers (Frost / Ember / Aether), so `ResolveSpeakerPortrait`
   resolves Echo-specific art instead of falling back to the generic silhouette.
3. **Coverage:** new capture case for the Echo engage dialogue + new
   `EchoEngageDialogueRegression`, registered in `DataRegression`.

## Files

- `Assets/_Modules/HUD/DialogueView.cs`
- both `dialogues.json` twins (Echo speaker portrait records)
- `EchoEngageDialogueRegression` (new, registered in DataRegression)
- UI capture harness: new Echo engage dialogue case

## Verification

- Brace + NUL gate green; batch-gated + committed (`323f3c97f`).
- Regression + capture case registered; capture PNGs are the layout proof.

## PO felt-verify

Engage a deployed Echo (Frost) in landscape on the Seeker: both options fully visible and
tappable with no scrolling, panel clear of TargetInfo above and the action bar below, and the
medallion shows the Echo's own portrait.
