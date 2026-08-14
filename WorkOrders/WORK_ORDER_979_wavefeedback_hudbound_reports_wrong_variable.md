# WORK ORDER 979 — `WaveFeedbackDirector` reports a HUD bind that can never succeed

**Status:** DONE — took **fork B (delete the seam)**: `Bind`'s `hud` parameter was never dereferenced and `FindHud()` had exactly one caller, so the seam was vestigial; both are removed loudly (WO-979 comments in place) and the install trace now reports `waveBound` (the reference `Bind` actually received) with a `FlowTrace.Warn` naming the unrendered visuals when `CoreServices.Hud` is absent.
**Lane:** Village / Waves / instrumentation
**Minted:** 2026-08-10 (CLI), from the hollow-assertion audit (`docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`)
**Closed:** 2026-08-14

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

- [x] Seam removed, absence documented in place (fork B — see §6).
- [x] No trace remains that names one variable and reports another.
- [x] A null-HUD run produces a `FlowTrace.Warn` naming the consequence, not a cheerful boolean.
- [x] Brace balance 66/66 + 0 NUL bytes (§1, §0).

## 6. Resolution (2026-08-14) — fork B, and the evidence for it

**Evidence.** `Bind(WaveManager wave, object hud)`'s entire body was `_wave = wave;` — the `hud`
parameter was **never dereferenced**, at any point in the file's life. `FindHud()` had exactly **one**
caller (the `TrySpawn` line feeding that dead parameter), and every real HUD call in the class already
went through `CoreServices.Hud?.…` at its own use site (`SetAttackDirections`, `ShowWaveClearBanner`,
`SetWaveImminent` ×2, and the `WallRepairHudBridge` install). So there was no wave feedback being
starved by the null bind — nothing downstream ever read it. Fork A (finishing the resolver) would have
added a captured reference the class does not want: point-of-use `CoreServices.Hud` always reaches the
HUD registered *now*, which is what lets the HUD register a frame after scene load and still work.

**Tradeoff named:** deleting the seam gives up the ability to inject a test-double HUD through `Bind`.
That is not a live capability — nothing injects one today, and the null-conditional use sites mean a
test-double would need `CoreServices.RegisterHud` anyway, which is the sanctioned path (CLAUDE.md §5/§6).

**Changes** (`Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs`, sole file):
1. `Bind(WaveManager wave, object hud)` → `Bind(WaveManager wave)`; `FindHud()` deleted. Both removals
   carry WO-979 comments explaining *why* the director holds no HUD reference, so the next reader does
   not "restore" the seam.
2. The install trace is now three falsifiable branches instead of one unconditional line
   (registry shape **H5** eliminated): `FlowTrace.Fail` if `_wave` is null post-`Bind`,
   `FlowTrace.Warn` if the wave bound but `CoreServices.Hud` is unregistered (naming banner / vignette /
   compass as the visuals that will not render), `FlowTrace.Step` only when both hold.
3. The **second** instance of the same `hudBound=` line, in `FireImminentAlert`, is fixed too. There the
   value did match what the alert consumes, but the name was the same misleading one and it had no
   failure branch — it now reads `hudRegistered` and `Warn`s that sting + haptic will play while the
   vignette and compass flash will not.

**No assembly boundary touched:** the fix removes a reference rather than adding one; Village still
reaches the HUD only through `CoreServices` / `IVillageHud`.

## 5. Related

Registry: `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`. Same class: WO-976 (`hasSurface`),
WO-977 (starter skill points), WO-978 (economy credited vs requested), WO-973 (`bubble=ok`).
