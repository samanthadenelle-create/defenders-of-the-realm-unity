# WORK ORDER 831 — RESULT (IMPLEMENTED 2026-08-02, PENDING GATES + ART)

**Implementer:** edit-only agent (no Unity runs; committer batch-gates + captures).

## What shipped

`EchoUnlockDialogue` is now a TWO-STATE beat (both unlock paths ride it — founding via
`AnnounceFoundingEcho`, #2-6 via the wave bridge; they all land in `Show`):

1. **EMERGENCE (new, 2D only):** sprite from `Resources/Echoes/Emergence/<PortraitName>_emerge`
   (falls back to `Emergence/<PortraitName>`, then the portrait, then a text placeholder —
   each miss `[Flow:Echo]` Warn-logged, NEVER blocking the unlock), the one-line `EmergeLine`
   intro (new ASCII field per roster entry, shared default via `EmergeLineFor`), a "Continue"
   button (bottom-right, >= MinTouch), the shared canon Close (skipping allowed), and a
   ~0.45s CanvasGroup fade + 0.9->1 scale-in coroutine (unscaled time; no tween lib, no
   Timeline, no VideoPlayer, no 3D).
2. **AWAKENING CARD (unchanged):** the existing portrait card layout, buttons, and flavor/lore
   behavior — reached on Continue. If the card build faults after emergence, the beat closes
   cleanly (Guard; unlock/SFX/pip already granted). If the EMERGENCE build faults, Show falls
   straight through to the card. `IsShowing` is true in both states, so the founding one-shot
   flag still only persists on a confirmed render.

## Files

- `Assets/_Modules/Village/Harvest/EchoUnlockDialogue.cs` — the beat + advance + fade.
- `Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs` — `EmergeLine` (6 authored ASCII
  lines) + `LoadEmergence` (Guard-wrapped, cached, null-graceful).
- Regression: emergence-data group in `Assets/Editor/Regression/EchoResourcePickerRegression.cs`
  (EmergeLine present+ASCII for all 6; `LoadEmergence` never throws; `EmergeLineFor(null)`
  default non-empty).

## Owed

- **Art:** the 6 LFS PNGs under `Assets/Resources/Echoes/Emergence/` (owner/art supplies —
  `<PortraitName>_emerge.png`: Frosthowl, VerdantStag, VoidwingRaven, StormcoilSerpent,
  StonewardenBear, EmberPhoenix). Until then the beat shows the portrait with the intro line.
- **Committer:** gates + `RunCaptureHeadless` of the emergence state (landscape + mobile
  resolutions) + both entry paths (founding Aldwin and a wave unlock) per acceptance §5.
