# WORK ORDER 682 — Web errors caught QUIETLY: no player-visible failure screen, ever

**Status: READY TO IMPLEMENT** (owner directive 2026-07-12 evening: "those need caught quietly —
not a giant json failure screen").
**Lane:** Platform / WebGL + Audio (lane 9/10 overlap). **Type:** EXISTING (the capture pipe works;
the player-facing surface is the defect).
**Numbering note:** minted as 682 = filesystem max 681 + 1. The authority docs
(`CLI_LANES_WO_NUMBERS.md`, next-free "412") are ~270 numbers stale, and 677/678 each carry TWO
specs on disk (677 mobile-buildmode vs 677 Asset Caster in HANDOVER; 678 Pi-timeout vs 678 Hovl
RESULT). Authority-doc refresh + collision cleanup is flagged as its own hygiene task.

## Symptom (owner, mobile/desktop web, 2026-07-12)

Playing the web build, an error surfaced as a **full-screen giant JSON/stack failure overlay** —
the demo-killing class of surface. The underlying error was cosmetic-grade (one audio clip).

## RCA — proven from the db (WebTrace → analytics_events → runtime-log echo), per pipeline rule 0

Verbatim proving lines, pulled 2026-07-12 ~23:10 UTC:

```
[Main_Castle_Overworld] error: Loading FSB failed for audio clip "SwordSwing".
```
- 22:48:27 UTC — session `wt-b085deef5b6`, same batch: `[Flow:Perf] LOW fps=6 ms=167.9`
- 23:05:04 UTC — session `wt-370cb605d41`, same batch: `[Flow:Perf] LOW fps=0 ms=4000.0` (4s stall)

The error repeats across sessions and pairs with severe frame stalls (likely also the owner's
observed browser violation `canvas#unity-canvas event handlers blocked ui updates for 229ms`).
The rest of the 24h window is clean — build-mode placement chains all healthy.

## Root candidates (CODE-READ + PROVE before editing — §12; static candidates only)

1. **The surface (the directive's target): the WebGL build ships as a Development build.**
   `docs/MASTER_CATALOG.md` risk ledger P3 #25: "Both build tools ship `BuildOptions.Development`
   for the 'ship' path." A Development WebGL build paints every uncaught error as Unity's
   full-screen debug overlay — the "giant JSON failure screen." Verify what `build-webgl.ps1` /
   `WebGLBuild.BuildWebGL` actually set on the deployed build, then kill the player-visible
   overlay on ship builds (non-dev build, or explicitly suppress the error overlay), keeping the
   WO-678 `showBanner` ownership in `Pi/index.html` as the single quiet surface.
2. **The trigger: `SwordSwing` FSB decode fails on WebGL.** Audio import settings are the usual
   suspect class (load type / compression / streaming unsupported on WebGL for that clip).
   Neighborhood: owner sound-drop commit `1ee7b6af` (Heal/Spell_Impact/Swords_Clash) + whatever
   imported SwordSwing. Fix the clip's import (or Guard the load path) so a failed clip logs ONE
   `FlowTrace.Warn` and plays silent — never error-level spam, never a stall.

## The fix (bounded)

- **Lane A — quiet the surface (the directive):** ship WebGL must NEVER show an engine error
  overlay. Errors route: Guard/FlowTrace → trace-tail ring + WebTrace (db) → nothing on screen.
  The WO-678 wrapper stays the only owner of the loader-error surface.
- **Lane B — fix the trigger:** SwordSwing import settings corrected for WebGL; audio clip loads
  are guarded (a bad clip = one Warn + silence, not error-level + stall).
- **Lane B2 — PRE-WARM combat audio on battle load (owner ask 2026-07-12: "can we pre warm those
  files on battle load?"):** at the arena warp-in / battle-load moment (the WO-584 ownership
  flip), `AudioService` walks the combat-relevant SfxId set and `AudioClip.LoadAudioData()` each,
  wrapped in `Guard.Try` — decode cost moves into the load transition instead of the first swing
  (the db's 167ms/4s stalls ride first-use decode). A clip that fails pre-warm is marked dead
  with ONE `FlowTrace.Warn` and skipped at runtime — the quiet-catch mechanism and the stall fix
  in one move. Check `preloadAudioData` import flags on combat clips in the same pass.
- **Lane C — regression:** a probe/assert that a deliberately-broken clip in a dev scene produces
  the quiet path (one Warn line, no error-level, no overlay). Headless where possible; the
  overlay half needs a browser check on the preview.

## Acceptance

- [ ] Web build: audio failure produces NO on-screen surface; game continues; one Warn line lands
      in WebTrace/db.
- [ ] `Loading FSB failed ... "SwordSwing"` no longer appears at error level in a fresh session.
- [ ] No Development-build error overlay on the shipped/preview WebGL build.
- [ ] Proving lines quoted in the RESULT (pre-fix root above + post-fix clean session capture).
- [ ] `COMPILE_GATE_OK` + owner felt-pass on a device (PO closes).

## What NOT to touch

- The WebTrace capture pipe (it worked exactly as designed — the error WAS in the db).
- The WO-678 Pi-timeout wrapper (proven; extend, don't rewrite).
- `FlowTrace`/`Guard` internals.

*Proof source: Vercel runtime logs (`[web_trace]`/`[sig]` echo from `api/trace.js`), sessions
`wt-b085deef5b6` + `wt-370cb605d41`, 2026-07-12.*
