# WORK ORDER 1242 - A Seeker performance gate, so the 48-loop dungeon tier cannot cost frame time

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 304/304 suites` (Builds/w5-c, Builds/w5-r). AWAITING OWNER FELT-VERIFY to close.
**Silo:** VFX / performance
**Severity:** P2 - preventative. Nothing is broken today; this protects a ruling that raised a ceiling.
**Origin:** Owner ruling 2026-08-26, attached to the WO-1229 dungeon-tier decision.

---

## CONTEXT

The owner ruled the dungeon VFX tier **ON**: dungeon scenes now permit **48** simultaneous loops
instead of the village ceiling of 24. That tier had **never engaged in a shipped build** - its only
activator lived in zero scenes - so it now self-binds from the loaded scene set.

Owner ruling: *"Keep 48-tier ON, but add a Seeker performance gate and automatic VFX degradation if
frame time crosses target. Preserve the visual ruling while protecting the device from fill-rate
carnage."*

## What to watch, in priority order (from the WO-1229 lane's own analysis)

1. **Particle overdraw.** Dungeon candles are additive transparent quads and the Seeker is
   **fill-rate bound**, so the tell is frame time collapsing when the player **LOOKS TOWARD** a lit
   room - not when they enter it. A gate that samples only on scene entry will miss this entirely.
2. **`demand-warm` bursts.** 48 loops can pull pool instantiations mid-play, which reads as a HITCH
   on entry rather than a steady cost. Different symptom, different fix.
3. **The `[Flow:VfxAmbientRing]` line.** Ambient is still capped at 8, so if frame time moves,
   **ambient dress is NOT the cause** and the extra slots went to enemy auras or portals. This is the
   discriminator - use it before degrading anything.

## Required

1. **Measure before you degrade** (CLAUDE.md section 12). Instrument frame time against loop
   occupancy on device and report the real relationship. Do NOT pick a threshold from intuition.
2. **Automatic degradation when frame time crosses target**, and it must degrade the RIGHT thing -
   per point 3, ambient dress first, never the accessibility loops.
3. ⛔ **The accessibility allowlist (`Aura_LowHealth`, `Aura_NearDeath`) is EXEMPT from degradation,
   absolutely.** The owner is red/green colourblind and that aura is her only non-colour danger
   signal; it was made unrefusable by explicit ruling on 2026-08-26. A perf gate that can silence it
   re-opens the exact hole WO-1229 closed.
4. Degradation must be **VISIBLE in the trace** - a silent quality drop is indistinguishable from a
   bug, and this repo has been burned by exactly that.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs.
2. A regression proving degradation triggers above the threshold, that it sheds ambient before
   anything else, and that the two accessibility loops are NEVER shed. **Prove RED first** (WO-1138).
3. ⭐ **A device capture** showing frame time and loop occupancy in a lit dungeon room, looked at -
   not inferred.
4. The RESULT states the measured frame-time/occupancy relationship and the chosen target, in numbers.

## What NOT to touch

- ⛔ The 48 dungeon tier or the 24 village tier. The ceiling is owner-ruled; this ticket protects it,
  it does not re-litigate it.
- ⛔ The ambient nearest-8 ring or the 2-slot accessibility reserve.
- ⛔ The accessibility allowlist. See point 3.
