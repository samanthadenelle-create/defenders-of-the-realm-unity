# WORK ORDER 979 — `WaveFeedbackDirector` reports a HUD bind that can never succeed

**Status:** READY TO IMPLEMENT
**Lane:** Village / Waves / instrumentation
**Minted:** 2026-08-10 (CLI), from the hollow-assertion audit (`docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`)

---

## 1. The defect

`Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs`

- **`:321`** logs `hudBound={CoreServices.Hud != null}`
- **`:325`** — `FindHud()` is a **stub whose entire body is `return null;`**

So the bind **always** fails, and the line that appears to report on it is actually reporting on a
**different variable**.

## 2. Why this is worse than an ordinary hollow trace

The other entries in the registry are *unfalsifiable*. This one is **actively misdirecting**: a
reader asking *"did the wave HUD bind?"* gets a confident answer about `CoreServices.Hud`, a
different object that is usually non-null. So the trace does not merely fail to prove the thing — it
supplies **evidence for the opposite conclusion**.

That is the most expensive kind of instrumentation defect, because it survives review. A missing
line prompts someone to add one; a wrong line ends the investigation.

## 3. Fix — pick one, and say which in the commit

**A. Finish `FindHud()`** if the wave feedback path is supposed to bind a HUD. Then the trace should
report the result of *that* call, post-resolution.

**B. Delete the seam** if it is dead. Remove `FindHud`, remove the field, and leave a one-line
comment saying the wave feedback path deliberately does not bind a HUD and why.

**What must not stand is the current state:** a stub with a trace next to it that implies it works.
If you cannot determine which of A or B is correct from the code, that is a question for the owner —
put it on the WO rather than guessing, per §12 (do not inference-fix).

## 4. Acceptance criteria

- [ ] Either `FindHud()` resolves a HUD and the trace reports **its** result, or the seam is removed
      and the absence is documented in place.
- [ ] No trace remains that names one variable and reports another.
- [ ] If (A): a null-HUD run produces a `FlowTrace.Warn`/`Fail`, not a cheerful `false`.
- [ ] Brace balance + 0 NUL bytes (§1, §0).

## 5. Related

Registry: `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`. Same class: WO-976 (`hasSurface`),
WO-977 (starter skill points), WO-978 (economy credited vs requested), WO-973 (`bubble=ok`).
