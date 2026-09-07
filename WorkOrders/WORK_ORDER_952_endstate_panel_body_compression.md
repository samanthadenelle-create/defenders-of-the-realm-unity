# WORK ORDER 952 — EndState (wave-clear) panel compresses its body below content size

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:03:11, build 2026.09.07.359076). PRIOR STATUS: FIXED - implemented in f6540db88 (2026-09-04 12:47), on the Seeker in build 2026.09.05.355872; RCA re-verified 2026-09-04 (see the appended block). Awaiting owner felt-test: win an arena round with a GEAR DROP (5-row spoils) on the device and judge the crest/stars/time narrative strip side by side - no band squashed. Gap: only WaveClear PNGs exist under Builds/ui-capture/, no arena EndState capture.
PRIOR STATUS: READY TO IMPLEMENT - REOPENED 2026-09-04. **P1: a GEAR DROP tips the arena victory panel
past its screen-height clamp and squashes every band to 93.3%.** ⛔ **The "renders empty" escalation
is WITHDRAWN - see §0a. The lead over-read the owner's words; the body was never proven empty.** Was
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

### ⛔ §0a. THE "EMPTY PANEL" ESCALATION IS WITHDRAWN. THE LEAD GOT THIS WRONG.

**What happened:** the lead showed the owner `break_01_error.png`, described the body as empty, and
she replied ***"screenshot bug"***. The lead read that as *"confirmed, the panel renders empty"* and
escalated the ticket to P0 on it. **Asked directly which she meant, she answered: the SCREENSHOT is
misleading - it caught the panel mid-fade-in.**

⛔ **Two words are not a confirmation, and "owner statements are ground truth" does not license
inventing which statement she made.** The correct move was to ask before escalating; the escalation
cost a P0 framing and pointed an RCA at a defect that does not exist.

**And the mechanism proves her right** (all READ at source):
- `BreakCaptureHarness.cs:1071` calls `ScreenCapture.CaptureScreenshot` **synchronously inside the
  log handler** - which, for this line, runs inside `BuildBody`.
- `EndStateView.cs:957` sets `rootGroup.alpha = 0f`; `:2214-2216` `Track()` sets every body element
  to `alpha = 0f` with staggered reveals - emblem 0.10s, subtitle 0.14s, time 0.20s, spoils rows
  `0.25 + i*0.05`, **CTA at 0.58s** (`:952`).
- The scrim is a **sibling** of `chrome.root` (`:152`), so it is at full opacity on frame 0.

**So the capture is a ~0.05s frame of a 0.78s staged entrance.** A blank body there is the animation
working. ⭐ **That is a REAL and GENERAL defect - every F8 screenshot of an animated panel is taken
mid-animation and is misleading - but it is a HARNESS defect, and it is now WO-1369, not this ticket.**

Positive evidence the reveal completed: `AutoDismissAfter` is a coroutine on the same `EndStateView`
MonoBehaviour and it fired at t+20.021s, so that object's coroutines were running, so `RevealRoutine`
reached `cg.alpha = 1f` (`:2253`). INFERRED, but from a mechanism read end to end.

### ⭐ §0b. THE REAL DEFECT, PROVEN: A GEAR DROP TIPS THE PANEL PAST THE CLAMP

Verified at source by the lead, not relayed:

| | gear | spoils | panel | body need vs well | dismissed in |
|---|---|---|---|---|---|
| 07:47 victory | `-` | 4 | 638px frac 0.661 | 496 = 496 ✓ | 1.99s |
| 07:48 victory | `-` | 4 | 638px frac 0.661 | 496 = 496 ✓ | 2.04s |
| **09:35 victory** | **Emberglass Staff** | **5** | **907px frac 0.94** | **578 > 540** ✗ | **20.02s** |
| wave-clears | - | 3 | 638px frac 0.661 | 340 = 340 ✓ | 1.4-1.6s |

`MaxPanelHalf = 0.47f` (`EndStateView.cs:236`, applied `:391`) caps the panel at **0.94 of screen
height**. The failing panel sits at **frac 0.94 exactly - pinned at the ceiling** - so the well cannot
grow to meet a 578px need and every band compresses to 0.933.

⛔ **THE TRIGGER IS THE GEAR DROP.** It adds the 5th spoils row. Four rows fit; five do not.
**The wave-clear path is CLEAN** (`need=340 well=340 scale=1`) - the 2026-08-10 fix holds, and this
is arena-with-gear only.

### ⚠ §0c. THE 20 SECONDS - WHAT IT DOES AND DOES NOT MEAN

Measured from the kernel touch-boost oracle (`libPowerHal ... lock_user:touch_boost`, which fires on
physical touch regardless of app handling):

```
09:35:49 -> 09:36:09  (20.0s, the failing panel)   touch events: 0
07:48:09 -> 07:48:12  ( 2.0s, the working panel)   touch events: 20
```

She never touched the glass, and the panel exited on `EndStateView`'s own 20s auto-dismiss
(`BattleArena.cs:2636` passes no timeout -> `BattleArenaHud.cs:80` default 20f -> `EndStateVM.cs:222`
-> `EndStateView.cs:2100-2104`), which fires the SAME `return-home` primary.

⚠ **Do NOT read this as a second defect.** The CTA was built correctly and identically on both panels
(`CTA 'Continue' ... box 360x132px ... band==132px`), and the most economical explanation is that she
saw something wrong and stopped to report it - which is exactly what she did. **NOT PROVEN either
way, and deliberately not ticketed.**

⭐ **Nothing was lost:** `GrantWinReward` ran at 09:35:49 BEFORE the panel - `+60 XP, +33 wood,
+15 iron`, `TryGrantArenaGear: weapon 'tripo_staff_a' ADDED TO INVENTORY`.

### (superseded) WHAT THE LEAD FIRST CLAIMED THE OWNER CONFIRMED

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

### ⭐ DEVICE EVIDENCE PULLED LIVE 2026-09-04 09:41 - the window SURVIVED

⛔ **NOT A SOFTLOCK.** A live `adb screencap` at 09:40:10 shows her back in normal combat (Orc Raider
35/130, Thrain Lv2). Something dismissed the panel. **Establishing WHAT dismissed it is load-bearing**
- see the watchdog line below, because if the watchdog rescued her then Continue never fired and the
reward grant may have been skipped.
Live shot: `logs/f8-inbox/device/live-20260904-094010.png`.

**Artifacts saved before the ring buffer could evict them** (memory `logcat-ring-buffer-destroys-evidence`
- the buffer held **47,853 `Flow:` lines**, `[Flow:EnemyAggro]` alone 13,369, so this window had
minutes to live):
- `logs/device/full-buffer-094110.log` - 1,111,915 lines, the whole buffer
- `logs/device/endstate-window-20260904.log` - 14,424 lines, 09:35:40-09:36:39, stack noise stripped

**The proving lines:**

```
09:35:49.828 [Flow:BattleArena] Resolve: battle ended, victory summary shown - home return is
             DEFERRED until Continue (watchdog armed). NOT yet returned; 'FADE IN: home arrival'
             proves arrival.
09:38:25.074 [Flow:EndState] 'YOU HAVE FALLEN' destroyed WITHOUT firing its primary action -
             EndStateView.Show - REPLACED by a new end-state 'YOU HAVE FALLEN'. That action is now
             abandoned. If it was an arena home-return, the player is stranded until BattleArena's
             watchdog fires.
09:38:25.081 [Flow:EndState] ... - CloseFromArbiter (another modal opened over this end-state).
09:38:25.070 [Flow:EndState] title band left AS BUILT: reserving the frame's 64px top border on a
             451px panel leaves only 0.012 of panel (min 0.05) - a crushed title renders zero glyphs
09:38:25.079 [Flow:EndState] ... on a 370px panel leaves only -0.025 of panel
```

⭐ **TWO LEADS THE COMPRESSION LINE WAS HIDING:**

1. **End-states are being DESTROYED AND REPLACED WITHOUT FIRING THEIR PRIMARY ACTION**, by
   `EndStateView.Show` (a second end-state opening) and by `CloseFromArbiter` (another modal opening
   over it - `PanelManager` is a single-modal arbiter that closes the prior panel). The panel's own
   instrumentation names the consequence: *"If it was an arena home-return, the player is stranded
   until BattleArena's watchdog fires."* **If the victory panel is replaced the same way, an empty
   frame is exactly what a replaced-but-still-drawn panel would look like.**
2. **The panel geometry is already degenerate before the body is considered** - panels built at
   **451px** and **370px**, with the title band computing to **0.012** and **-0.025** of panel
   against a 0.05 minimum. ⚠ A NEGATIVE fraction. If the same arithmetic runs on the body bands,
   *built-but-invisible* becomes the leading hypothesis and the 0.933 compression is incidental.

⛔ **Neither is proven yet** - both are candidates raised by captured lines, and an RCA agent is
working the split. **Do not fix from this section.**

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

---

## 2026-09-04 — THE SECOND REFLOW LEVER: THE NARRATIVE STRIP (edit-only agent, NOT gated)

`Assets/Editor/Regression/EndStateBodyFitRegression.cs` ran RED and proved the defect is **wider
than the capture**. The full failure set (`Builds/wave-regression3.log:110516-110524`) is **8 cases,
not one**: **3, 4, 5 and 6 spoils rows on BOTH `2340x1080` and `1920x1080`**, all pinned at
`MaxPanelHalf`, `need=496/578px` against `well=472px`, `scale=0.952/0.817`, **2 columns**. The
owner's device (2670x1200) was the *lucky* surface — its 1376px body column can carry a 3rd column,
so the August column lever saved it. The two commonest landscape surfaces cannot: a 3rd column needs
`3 x 420 = 1260px` of legibility floor and they have **1206px** and **990px**.

**The lever taken (neither clamp nor band minimum moved):** the emblem (64px), the star rating (72px)
and the Time line (48px) are each ONE SMALL CENTRED ELEMENT on a ~1000-1400px wide band — three
bands and two gaps, **202px of well**, to draw a crest, three stars and five characters. They now
lay out **SIDE BY SIDE in ONE band as tall as the tallest of them (72px)** when, and only when, the
stacked body does not fit the well the clamp allows. **Nothing is scaled down** — every element keeps
its authored fixed size; the three simply stop each paying for a row of their own.

- `EndStateView.NarrativeStripAt(vm, canvasH, cols)` — the decision. **ESCALATION ONLY** (asks the
  STACKED budget, exactly like the column lever, so any panel that already fits keeps its shipped
  layout) and **WIDTH-GATED** (`MinStripCellPx = 216 + 2x24 = 264px`, derived from the star
  cluster's own fixed `2 x StarSpacingPx + StarSizePx`). Compact banners are refused outright, so
  the wave-clear path is untouched.
- `RequiredBodyPxAt(vm, canvasH, cols, strip)` gained the flag; `BuildBody` reads the SAME
  `SpoilColumns` -> `NarrativeStripAt` pair, so the solve and the layout cannot disagree about the
  band count. `ProbeFit` reports it as `FitResult.NarrativeStripMerged`, so the oracle measures the
  shipped decision.
- New instrumentation (§12): `[Flow:EndState] narrative bands REFLOWED to a N-cell STRIP: ... cost
  {stacked}px stacked and {strip}px side by side, against a {wellCeiling}px well ceiling at N spoils
  column(s) (cell {px}px vs a 264px floor)`. The existing `geometry:`, `COMPRESSED to fit` and
  `solved to an EXACT fit` lines are untouched and still the acceptance evidence.

**The arithmetic, all four oracle surfaces x 1-6 rows = 24 cases, every one `scale = 1.000`:**

| surface | rows | cols | strip | need | well | panel frac |
|---|---|---|---|---|---|---|
| Seeker 2670x1200 | 3 / 4 | 2 | no | 496 | 496 | 0.879 |
| Seeker 2670x1200 | 5 / 6 | **3** | no | 496 | 496 | 0.879 |
| 2340x1080 | 3 / 4 | 2 | **yes** | 496 -> **348** | 348 | 0.747 |
| 2340x1080 | 5 / 6 | 2 | **yes** | 578 -> **430** | 430 | 0.874 |
| 1920x1080 | 3 / 4 | 2 | **yes** | 496 -> **348** | 348 | 0.747 |
| 1920x1080 | 5 / 6 | 2 | **yes** | 578 -> **430** | 430 | 0.874 |
| portrait 1080x1920 | 1-6 | 1 | no | 414-824 | = need | 0.38-0.67 |

Nothing is pinned at the clamp any more. **The Seeker cases are bit-for-bit what shipped** (the
oracle's Case 1 "the 4-row victory must still solve at 2 columns" and the 5-row 3-column reflow both
hold), and portrait stays single-column, un-stripped, as WO-894 ruled.

**NOT DONE HERE (edit-only seat):** no gate, no regression run, no capture. To close: re-run
`DataRegression.RunAll` (the suite must go green **without the oracle being touched** — it is
unmodified), then `UICaptureLaunch.RunCaptureHeadless` and OPEN the PNGs, because a geometry solve
that fits is not proof the strip READS right. The one thing arithmetic cannot answer is whether
crest / stars / time side by side is the composition the owner wants — that is a felt call on a
screenshot, and it is the reason the capture half of this WO still matters.

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** SUPERSEDED
**Evidence:**
- The reopen's fix landed in `f6540db88 2026-09-04 12:47` (ancestor of HEAD): stat shows `Assets/_Modules/Village/UI/EndState/EndStateView.cs | 390 +++--`, `Assets/Editor/Regression/EndStateBodyFitRegression.cs | 295 +` (new), `DataRegression.cs | 16 +`, and this WO `| 56 +`. Body line 37: "WO-952 - the arena victory panel. THE ORACLE FOUND MORE THAN THE DEVICE CAPTURE DID".
- `EndStateView.cs:1035` `return RequiredBodyPxAt(vm, canvasH, cols, NarrativeStripAt(vm, canvasH, cols));` - the narrative-strip lever described in the 2026-09-04 section above IS in the committed tree (`git status` clean on the file; last touch `f6540db88`). So "edit-only, NOT gated" in that section is now stale on the "not committed" half.
- `Assets/Editor/Regression/EndStateBodyFitRegression.cs:1-30` header cites this WO, F8 seq 4680, the 578/540 case and the RED recipe (`MaxSpoilColumns = 2`); registered `DataRegression.cs:679` `[endstate-body-fit]`.
- The 2026-08-10 originals: `MaxPanelHalf` and the owned compact solve are still at `EndStateView.cs:340-446`, `:631`, `:701`.
- Regression: `Builds/regression.log` at its 22:31 state read `:113715 REGRESSION_OK 377/377 suites -- 377 green, 0 red, 0 skipped` (a 22:42 rewrite began after that read). Acceptance still OPEN: (b) the EndState capture case - `Builds/ui-capture/` holds only `EndStateWaveClear_plain_{1920x1080,2340x1080,2670x1200}.png`, NO arena-with-gear capture; (c) the owner's felt call on crest/stars/time side by side.
**What changed since the RCA:** the whole reopen fix (3-column lever + narrative strip + the COMPRESSED-absence oracle) is committed; this WO's Status line was never flipped from READY.
**Ready for a lane?** no - implemented; remaining is capture + felt-verify. Files a lane would touch: this WO (Status), the UI-capture case list (add an arena 5-row gear-drop case at 2670x1200).
**Pins/rulings needed:** owner felt call on the side-by-side narrative strip (2340x1080 / 1920x1080 surfaces); an arena EndState capture PNG opened, not just solved.
