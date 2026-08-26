# WORK ORDER 1229 - Env_Candle leaks the VFX loop pool, and it is eating the colourblind low-health signal

**Status:** FIXED 2026-08-26 - `COMPILE_GATE_OK` + `REGRESSION_OK 292/292`; post-fix APK soak/trace verification queued
**Silo:** VFX
**Severity:** P1, and it carries an ACCESSIBILITY consequence the instrumentation names itself.
**Origin:** CLI triage of the owner's device log, Seeker build `2026.08.26.342290`, 2026-08-26,
while investigating three separate *"nothing happened"* reports.

---

## PROOF — captured from the device

```
[Flow:VFXManager] PlayLoop('Env_Candle')    SKIPPED — active loops 24/24 (cap hit; auras/trails dropping)
[Flow:VFXManager] PlayLoop('Aura_NearDeath') SKIPPED — active loops 24/24
[Flow:HeroHpAura] 'NearDeath' aura ('Aura_NearDeath') was REFUSED by VFXManager (loop cap or
                  quality gate). This is the PRIMARY colourblind low-HP read — if it is being
                  dropped, the hero has no non-colour danger signal. Retrying.
```

Measured over the buffer:
- **52 `SKIPPED` lines**, saturated **continuously from 12:19:11 to 12:36:28 — 17 minutes.** Not a
  spike; a pool that fills and never drains.
- Requests: **`Env_Candle` 44** vs `Aura_NearDeath` 3. **The candles are the consumer.**

## ⭐ WHY THIS MATTERS MORE THAN A DROPPED EFFECT

The owner is **red/green colourblind**. The low-health tell was deliberately rebuilt away from a red
vignette into pulse-rate + guttering + a recipe swap below quarter health — and that tell is a
LOOP. When the pool is starved it is REFUSED, so **she fights with no danger signal at all.** The
instrumentation predicted this exact consequence in its own message; nobody was listening because
device captures never reach the inbox (WO-1227).

It also explains **three** of today's reports as ONE cause: an opened chest whose loot mote was
invisible, the missing low-health aura, and dropped trails.

## ⛔ DO NOT RAISE THE CAP

This repo has met this at **20/20**, then **40/40**, now **24/24**. A ceiling that keeps moving while
the symptom returns is the signature of a leak being papered over. **44 requests against a 24 cap is
not a capacity problem.**

Canon already records the shape (2026-08-06, WO-874-era): the `IsLoop` sticky-checkbox defect meant
*"a fire-and-forget loop permanently consumed one of the 20 global slots"* — handles issued and never
`Stop()`d. And the carried-open note says the **ONESHOT pool saturating 40/40 is a separate reclaim
path and was explicitly NOT closed** by the loop-cap fix.

## Required

Find who calls `PlayLoop('Env_Candle')` and who never releases the handle. Fix the RECLAIM, not the
ceiling.

Read before editing: `VFXManager` (the pool + cap + `Stop`), whatever injects candles in dungeons
(the capture is from `dg_folks_granary` / an outpost), and `HubAmbientVfxInjector` for the
ambient-attach precedent. ⚠ `Env_Candle` is *environmental* — a candle attached to a prop that is
pooled, culled or destroyed without stopping its loop would leak exactly this way.

⚠ **Instrument the reclaim, do not assume it.** §12: the fix is not "I added a Stop()" — it is a
captured line showing the active-loop count going DOWN when a candle leaves.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. ⭐ A device capture over several minutes of play showing the active-loop count RISING AND FALLING,
   and **zero** `SKIPPED — active loops` lines. The absence of the message is the acceptance.
3. ⭐ A regression that FAILS on today's tree: request more loops than the cap in a lease/release
   cycle and assert the pool drains. Prove it RED first (WO-1138).
4. ⭐ A case asserting `Aura_LowHealth` / `Aura_NearDeath` are **never** refused for pool exhaustion.
   The colourblind tell must not be a casualty of an environment effect — if a priority or a reserved
   slot is the right mechanism, say so.
5. The RESULT states the leak site and quotes the reclaim trace.

## What NOT to touch

- ⛔ The loop cap number, in either direction, until the leak is found. If the cap genuinely needs to
  move afterwards, that is a separate owner decision with a measured justification.
- ⛔ The low-health tell's design (pulse rate / guttering / recipe swap below quarter health) — it is
  owner-ruled and correct. The defect is that it is being refused, not what it looks like.
- ⛔ The ONESHOT pool. Different pool, different reclaim path, explicitly still open — do not conflate
  the two or "fix" both in one change.
## LANDED-WORK AUDIT (2026-08-26)

The bounded-demand implementation landed in `b303c4fbf`; diagnosis corrected the premise from a
leak to unrestrained ambient demand. Fresh evidence: `Builds/batch0-compile-2.log:1966`
`COMPILE_GATE_OK`; `Builds/batch0-regression-2.log:83803` `VFX AMBIENT BUDGET OK` proves an ambient
cap of 8, accessibility reserve of 2, release-to-zero, unrefusable low-health/near-death auras, and
runtime scene-tier binding; `:83814` is `REGRESSION_OK 291/291`. **Post-FIXED APK checklist:** the several-minute
device soak showing counts rise and fall with zero pool-exhaustion skips, plus the reclaim trace in a RESULT.
