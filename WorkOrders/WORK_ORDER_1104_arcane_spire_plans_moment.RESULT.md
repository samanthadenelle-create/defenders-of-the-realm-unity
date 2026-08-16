# RESULT — WO-1104 the Arcane Spire plans MOMENT

**Date:** 2026-08-16  **Seat:** CLI (commits `449dd9df1`, `ec247d4f1`)
**Status:** DONE — pending PO felt-verify

## What shipped

1. **The seat resolves by COMPONENT, not by tag** (`449dd9df1`). `CastleDefensePlansService.ResolveGateSeat`
   threw on the UNDECLARED `SpawnPoint` tag on every 3 s scan (owner F8 seq 2434–2442), which also made
   its own near-Heart fallback unreachable. Now a `WaveSpawnPoint` component lookup — 4 live in the hub —
   which cannot throw on missing project settings.
2. **Threshold 2 → 3 waves** per the owner's ruling *"it should be given after wave 3"*. The
   `CastlePlansUnlock` oracle now reads `RequiredWavesSurvived` instead of hardcoding 2, so ruling and
   guard move together.
3. **The moment itself** — `SpirePlansCelebration.cs`: ONE dialogue screen, three beats (celebration /
   plans / Aldwin urging haste), in the `StoryIntroController` cold-open idiom per her rulings. It
   subscribes to `CastleDefensePlansPickup.PlansCollected`, a seam with ZERO subscribers since WO-1013.
   Presentational only — skipping can never cost the spire. Speaker resolves from `EchoRosterCatalog`
   at runtime, so no name literal (i.e. "Frost" cannot return).
4. **Three follow-on defects fixed** (`ec247d4f1`, from owner F8 seq 2505 *"Im on wave five and still
   cannot build arcane towers"* — the plans DID drop at wave 3 and were never collected in 647k log lines):
   - **SEAT:** the drop pulled 8 m inward from a spawn point 12 m OUTSIDE the gate, landing ~4 m beyond
     the 40.8 m wall line while the comment claimed "pulled well inside". Fixed with **no new magic
     number** — `WaveSpawnPoint` already carries its gate's world position, so the seat anchors on
     `GatePosition` with the same 3.5 m inset the Gate branch already used (37.33 m).
   - **DETERMINISM:** all four markers are equidistant, so `d < best` could never break the tie and the
     chosen gate rode `FindObjectsByType` iteration order while the docstring claimed determinism. Now
     an ordinal name pick, extracted into a PURE function.
   - **SILENCE:** every early return on the non-spawn path emitted nothing. Now a 5 s throttled heartbeat
     naming the reason, plus an install line from `Bootstrap.Init`.

## Oracles

- `SpirePlansCelebrationRegression` → `SPIRE_CELEBRATION_OK` (registered).
- `CastlePlansSeatRegression` → `[castle-plans-seat]`: asserts the seat is INSIDE the ring (ring read
  from the candidates' authored gate magnitudes, never restated) and that the choice is identical across
  all four rotations. The old code fails both.
- `CastlePlansUnlockRegression` re-pointed to `RequiredWavesSurvived`.
- MVVM allowlist: `SpirePlansCelebration` joins `StoryIntroController` / `HeroSelectController` as a
  one-shot flow controller, not a modal panel View (precedent verified, not an invented exemption).

## Deliberately NOT done

- No banner, modal or announcement chrome — WO-1013 SS3 stands.
- The oracle's own first run threw a FALSE RED on its source-lint (the screen's honest `FlowTrace` names
  `TryCollect` inside a MESSAGE string), so the lint now strips string literals and trailing comments.
  The lint was fixed rather than the trace weakened.

## Owner decision left open

- **DISCOVERABILITY, her call.** The plans prop now wears the existing `PoiBeacon` landmark — the same
  far-field pillar an enemy fortress wears — so it glints visibly from the town centre. Nothing is
  announced and nothing reaches the HUD, so the WO-1013 SS3 ruling ("no banner, no modal, no
  announcement chrome") is not crossed: that ruling forbids announcement CHROME, and this is world-space
  presentation on the prop. Reads by verticality / motion / luminance, never hue. **She has not seen it.**
- **Collision noted at mint:** WO-1031's guide despawn means after wave 3 the Echo body may already be
  gone. WO-1108 Lane B landed the despawn the same night — verify the celebration's speaker still
  resolves with no Echo body present.
