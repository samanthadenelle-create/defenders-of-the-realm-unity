# WORK ORDER 952 — EndState (wave-clear) panel compresses its body below content size

**Status:** READY TO IMPLEMENT
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
