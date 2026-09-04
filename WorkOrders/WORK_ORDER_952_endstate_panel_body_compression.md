# WORK ORDER 952 — EndState (wave-clear) panel compresses its body below content size

**Status:** READY TO IMPLEMENT - ⛔ **P0. THE VICTORY PANEL RENDERS EMPTY ON HER DEVICE** (owner
confirmed 2026-09-04, *"screenshot bug"*). REOPENED, and the severity is NOT the 7% compression the
log line describes. Was
"DONE - audit-verified as shipped (2026-08-21 backlog audit)"; that close rested on an AUDIT, not on
device evidence, which is the same shape as PROD-019's reopen ("closed on EDITOR-ONLY evidence;
still broken on device").

## 0. REOPEN - F8 seq 4680, her device, 2026-09-04T14:35:49Z

```
[Flow:EndState] body rows COMPRESSED to fit: need=578px well=540px scale=0.933
  - the panel hit its screen-height clamp; every band is now below its own content size
```
`EndStateView.BuildBody` <- `Bind` <- `Show` <- **`BattleArenaHud.ShowResult`** <-
`BattleArena.Resolve` <- `WatchToResolution`. Scene `Main_Castle_Overworld`, t=299.5s.
Device `SM02G4061955851`, build on device = the 2026.09.04.354315 production candidate.
Capture: `logs/f8-inbox/capture-device-20260904-093607-seq4680.md`.

### Why this is NOT simply "the old bug is back"

**It is a different call path with roughly twice the content.**

| | Original (2026-08-10) | Now (2026-09-04) |
|---|---|---|
| Entry | `WaveCelebrationManager.WaveClearRoutine` | **`BattleArena.Resolve` -> `BattleArenaHud.ShowResult`** |
| need | 276px | **578px** |
| well | 249px | **540px** |
| scale | 0.9 | **0.933** |

So the wave-clear path may well be fixed; **the ARENA victory path is not**, and it carries a much
bigger body. Do NOT assume one fix covers both - establish which paths still clamp before touching
the solver.

⚠ 0.933 is a REAL clamp, not float residue: the emitter's own threshold is
`CompressFailBelow = 0.995f` (`EndStateView.cs:~1379`), added precisely to stop a 0.9997 hairline
tripping a FAIL. 0.933 is well below it.

### ⛔ AND THIS IS WHY NOBODY CAUGHT IT BEFORE HER

**The `COMPRESSED`-absence oracle STILL DOES NOT EXIST.** `grep -rn "COMPRESSED" Assets/Editor/Regression/`
returns **zero hits** (verified 2026-09-04). `KEY_FACTS.md` flagged this as an honest partial on
2026-08-10 - *"WO-952 the geometry fix landed but its capture case + `COMPRESSED`-absence oracle do
NOT exist"* - and the ticket was nevertheless marked DONE by the 08-21 audit. **The missing
deliverable is the reason the recurrence reached her eyes instead of a gate's.**

⛔ **The oracle is not optional this time.** A fix without it puts us right back here.

### ⛔ OWNER CONFIRMED 2026-09-04: THE EMPTY BODY IS THE BUG, NOT A CAPTURE ARTIFACT

Owner, on being shown `break_01_error.png`: ***"screenshot bug"***.

**She was looking at the screen. That is primary evidence and it outranks the lead's caution below**
(memory: `owner-statements-are-ground-truth`; `screenshots-are-primary-evidence-for-visual-defects`).
The hedge that follows was written BEFORE she confirmed, and is kept only so the reasoning is
auditable - ⛔ **do not act on it, act on her statement.**

**SO THE DEFECT IS RESTATED:**

> On an ARENA VICTORY the EndState panel draws its title and frame and **renders NO BODY** - no
> rewards, no stats, and **no visible Continue/Claim control**. The world is visible through the
> empty frame.

⛔ **THE `COMPRESSED` LINE IS A SYMPTOM, NOT THE DEFECT - AND CHASING IT IS THE TRAP.** A 0.933 scale
squashes bands to 93%; it does **not** make them disappear. Something else empties the body, and the
compression warning is merely the loudest thing in the log. **Do not spend the session tuning the
fit solver.** Split it first (CLAUDE.md §12):
- **data-empty** - `EndStateVM` handed `BuildBody` no rows for the arena path (the VM is built by
  `BattleArenaHud.ShowResult`, a DIFFERENT producer from the wave-clear path that works).
- **built-but-invisible** - bands built at zero/clipped size, or off-screen, or behind. ⭐
  `UiSurfaceProbe` (WO-976) reports `SURFACE_ZERO_SIZE` / `SURFACE_TRANSPARENT` / `SURFACE_OFFSCREEN`
  / `SURFACE_BEHIND` as four separate classes and measures AFTER layout settles. Use it.
- **threw-and-skipped** - a `build(host)` delegate throwing per band. ⚠ Note the stack shows
  `Guard.Try` wrapping `ShowResult`, so a throw would be caught and logged - **check the device log
  around 14:35:49Z for a Guard/Fail line before assuming this one.**

⛔ **AND CHECK FOR A SOFTLOCK FIRST, BEFORE ANYTHING ELSE.** If there is no reachable dismiss control
on that panel, an arena victory strands the player - and this is the **2026.09.04.354315 PRODUCTION
CANDIDATE**. Establish whether she could get out of it. If she could not, this outranks every other
item on the board including the Play submission.

### ⚠ (SUPERSEDED BY HER CONFIRMATION ABOVE) WHAT THE LEAD THOUGHT THE SCREENSHOT SHOWED

`logs/f8-inbox/device/SM02G4061955851/break_01_error.png` (09:35:50) shows the VICTORY! title and the
panel frame drawn, with **the body area empty** and the world visible through it.

⛔ **Do NOT record that as "the panel renders empty."** The harness screenshots ON the error, and the
error fires from inside `BuildBody` - i.e. BEFORE the bands are added. The empty body is consistent
with "not populated yet at capture time". **Whether the settled panel is empty, merely squashed, or
fine is UNPROVEN from this capture**, and it is the first thing the next session must establish:
split it into *data-empty* vs *built-but-invisible* vs *threw-and-skipped* (CLAUDE.md §12) with a
capture taken AFTER layout settles. `UiSurfaceProbe` (WO-976) exists for exactly this and measures
after settle.

### Acceptance additions for the reopen

- [ ] Which entry points still clamp - answered with captures, not inference (wave-clear vs arena).
- [ ] A settled-state capture showing what the player actually sees. Quote it.
- [ ] ⛔ The `COMPRESSED`-absence oracle EXISTS, is REGISTERED in `DataRegression`, and is **proven RED**
      against today's 578/540 case before it goes green.
- [ ] An EndState capture case in the UI-capture set at the device's real 2670x1200 - canon records
      that the harness was geometry-blind until `7e05e6d3` and that **no EndState case is in the
      capture set**, which is WO-952's other never-delivered half.

---

*(Original 2026-08-10 body follows, unrewritten per CLAUDE.md §15.)*

**Status (original):** DONE - audit-verified as shipped (2026-08-21 backlog audit).
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
