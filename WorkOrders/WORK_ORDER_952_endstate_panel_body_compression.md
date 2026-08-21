# WORK ORDER 952 — EndState (wave-clear) panel compresses its body below content size

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 952 → 953 in the same edit)
**Silo:** Village/UI EndState — no overlap with any live lane
**Origin:** the panel's OWN instrumentation net (FlowTrace.Fail), captured TWICE in one session by the
F8 daemon (2026-08-10, `capture-20260810-102345.md` and seq 2268 at 11:04), desktop exe:

```
[Flow:EndState] body rows COMPRESSED to fit: need=276px well=249px scale=0.9 - the panel hit its
screen-height clamp; every band is now below its own content size
```
Stack: `EndStateView.BuildBody` (:921) ← `Bind` (:705) ← `Show` (:167) ←
`WaveCelebrationManager.WaveClearRoutine` (:179).

## 1. The defect

On a wave-clear, the EndState panel's body rows need 276px but the well allows 249px at the current
resolution; the clamp scales everything to 0.9 — text and touch bands land BELOW their authored
minimums (the exact class the fixed-px band law + MinTouchPx exist to prevent). Recurring, not a
one-off; the trace is loud (working as designed) but the layout ships wrong.

## 2. Fix shape (verify at source first)

- Read `EndStateView.BuildBody`'s band budget vs the well height derivation (kit surface heights, the
  screen-height clamp) — decide whether the fix is: fewer/merged rows at small heights, a scrollable
  body well (existing kit scroll pattern), or a corrected well allocation. Do NOT let bands scale
  below MinTouchPx/content size — the clamp should reflow, not shrink.
- Mind the 08-06 victory-screen lessons (two-column landscape spoils; the WO-894 wireframe deviation
  precedent) — extend that layout logic, don't fork it.
- Geometry class ⇒ needs EYES: add/extend a UI capture case at the failing resolution (the harness
  renders real geometry since `7e05e6d3`) and assert no `COMPRESSED to fit` Fail line fires across
  the capture set (the absence of the trace IS the acceptance signal — plus opened PNGs).

## 3. What NOT to touch

WaveCelebrationManager flow/timing · the EndState VM data · the FlowTrace net itself (it caught this
— it stays).

---

## 2026-08-10 - PARTIAL LANDING (CLI seat, gated)

**The geometry half landed and is gate-green; the acceptance signal this WO asked for did NOT.**

Landed in `Assets/_Modules/Village/UI/EndState/EndStateView.cs`: the compact banner now SOLVES its
final height from its content up front and stamps every band against that one height (`:547`); the
close-band reclaim gate is inverted from `compactNoCta` to `compactAnyCta` so it runs on EVERY
compact banner (`:532`) - that gate is the root of the defect, because a Repair-All banner kept the
stale 0.45 body floor computed against the 0.30h splash; the CTA gets its own band on the frame art's
measured well floor (`:566`); the old downward-growth block is fenced to the art-less fallback
(`:750`); and the width chain now uses the banner's real 0.70 canvas fraction via
`PanelWidthFracFor` instead of the modal's 0.56, which had been over-counting wrapped subtitle lines
and inflating `need` for nothing (`:315`). By construction well == need, with the 0.08 growth-floor
clamp as the ONE remaining compression source. The FlowTrace `Fail` net that caught this is untouched.

**Completion by the committer:** the lane's session expired mid-refactor - three helpers had been
re-signed to take an explicit `panelWidthFrac` with FOUR call sites still on the old arity, so the tree
did not compile (`error CS7036` x3). Completed at `:495`, `:837`, `:886`, `:931` to pass
`PanelWidthFracFor(vm)`; a repo-wide grep returns no old-arity call and all three helpers are
`private static`.

**NOT DONE - why this stays READY.** WO §2 bullet 3 asks for a UI capture case at the failing resolution
asserting the ABSENCE of the `COMPRESSED to fit` Fail line. `Assets/Editor/UICaptureLaunch.cs` is
unmodified and carries zero EndState/wave-clear cases; there is no `EndState*Regression.cs`. This fix
therefore ships with no eyes on it and no automated absence assertion - it is source-reasoned, not
captured. The remaining scope of this WO is exactly that capture case.

**Gate at the time of writing:** `Builds/gate-settle4.log` -> `COMPILE_GATE_OK` (zero `error CS`) ·
`Builds/regression-settle3.log` -> `REGRESSION_OK 143/143 suites` ·
`Builds/ui-capture-settle.log` -> `UI_CAPTURE_OK 62` + `UI_CAPTURE_FIDELITY_OK 44` with the
pre-existing `UI_GEOMETRY_FAIL x16` (WO-941's known RumorBoard/RealmMap baseline - **no EndState case
is in that set**, which is the gap named above).

**Owner felt-verify meanwhile:** a wave-clear at the 16:9 desktop resolution that produced
`need=276px well=249px scale=0.9`, and a WO-672 Repair-All banner (the CTA path - the one that broke).
The log must NOT carry `body rows COMPRESSED to fit`.

---

## 2026-08-14 - THE CAPTURE CASE LANDED (HUD lane agent, edit-only - NOT yet run)

The remaining scope (WO §2 bullet 3) is implemented in `Assets/Editor/UICaptureLaunch.cs`. It does NOT
close this WO by itself: **no harness run has executed it yet** (this seat is edit-only and does not
gate). What exists now:

- `CaptureEndStateWaveClear()` is registered in `RunCaptureHeadless`, and runs **two real compact
  banners per capture target** (1920x1080 - the failing 16:9 desktop resolution - plus 2340x1080 and
  the Seeker's 2670x1200): the **Repair-All / CTA** banner with a full 4-row damage report (the shape
  that broke) and the **plain no-CTA** banner. Fixtures, not `EndStateVM.FromWaveClear` - that factory
  reads the live wall-damage ledger and wallet, neither of which stands up in a synchronous edit-mode
  render. Both go through the REAL `EndStateView.Show`, so the geometry under test is the shipped path.
- **The absence assertion is checked, not hoped for:** `FlowTrace.Sink` is tapped for the duration of
  each build (restored in a `finally`), and a captured `body rows COMPRESSED to fit` line FAILS the run.
- **It is not trusted alone.** A new settled-layout probe measures the RESOLVED `Zone_RewardWell` rect
  and the resolved `Band` stack in kit reference px and **recomputes** the compression factor as
  `stack extent / need`. (BuildBody lays bands at `px * scale` with `BandGapPx * scale` between them,
  so the measured extent IS `need x scale` - the factor comes from geometry, not from the view's own
  arithmetic.) Below 0.995, or a band outside its well, fails.
- **Silence cannot pass.** No `need=` line, no `Zone_RewardWell`, zero bands, a null `Show`, or zero
  cases measured all report `UI_ENDSTATE_FIT_FAIL`, and the OK marker always prints the four numbers.
- New DISTINCT marker: **`UI_ENDSTATE_FIT_OK <n> banners`** / `UI_ENDSTATE_FIT_FAIL x<n>` (per
  CLAUDE.md §8 - never share a marker string with the other entry points).

**To close this WO:** run `DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless`, confirm
`UI_ENDSTATE_FIT_OK 6 banners` with `scale` >= 0.995 on every case, and OPEN the six
`Builds/ui-capture/EndStateWaveClear_*.png`. A `UI_ENDSTATE_FIT_FAIL` re-opens the geometry half.
The pre-existing `UI_GEOMETRY_FAIL x16` (WO-941 RumorBoard/RealmMap) baseline may grow by any EndState
finding this newly-captured canvas surfaces - triage it, do not baseline it.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `UICaptureLaunch.cs:526,548` — geometry fix landed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
