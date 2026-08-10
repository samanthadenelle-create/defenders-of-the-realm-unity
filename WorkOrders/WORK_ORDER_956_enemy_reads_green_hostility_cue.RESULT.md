# WO-956 RESULT — an enemy reads GREEN: hostility never sits on the red/green axis

**Status:** IMPLEMENTED — owner look-pass owed, with ONE named colourblind risk
**Landed:** 2026-08-10 (wave-3 lane; verified, gated and committed by the CLI seat)

## RCA (§12) — including a negative finding

Candidate #1 in the WO (an enemy healer reusing the player's green `Cast_Heal`) is **NOT** the source:
there are no enemy-side `Cast_Heal` / `Impact_Heal` call sites at HEAD — every hit is Hero / companion /
CastleDoorController. Enemy healers heal without a heal VFX. That negative is recorded here rather than
dropped, because it is the finding that redirected the fix.

The green she saw can only come from three enemy-side seams, and all three are now gated:

1. `Aura_Necromancer` → Lana `Fog_poison`, authored saturated green (~0.19/0.58/0.12).
2. The Warband grunt BODY fallback tint, authored orc green (0.30/0.42/0.22) — spared by the 07-10
   ruling as "intended"; this F8 re-flags exactly that.
3. Any data-authored `EnemyTypeVfxSet.RangedVfxTint` that lands green.

## What changed

- NEW `Assets/_Modules/Village/Vfx/HostilePalette.cs` — the faction colour law. `IsGreenDominant`
  (`:73-76`, G strictly dominant by 0.08 with a 0.25 floor), `EnforceOnTint` (`:85-96`, substitute +
  `FlowTrace.Warn`, alpha preserved), and two ROLE-named PLACEHOLDER hues: `PlaceholderEffectTint`
  (violet, `:51`) and `PlaceholderBodyTint` (umber, `:58`).
- `EnemyAuraVFX.cs:248` — an enemy aura whose AUTHORED art reads green gets the hostile placeholder via
  the modulator. Instance-driven, so a future green re-pick self-detects; no type names hardcoded.
- `Enemy.cs:1870` — the ranged-cast tint routes through `EnforceOnTint` before cast/projectile/impact,
  so all three are gated by one call.
- `EnemyFactory.cs:275` — the grunt body arm is now `PlaceholderBodyTint`. Troll / ogre / warlord keep
  their near-neutrals (they fail the green margin).
- `VfxLoopModulator.cs` — the tint override rides the existing modulation contract: authored
  `startColor` is captured in the baseline and `Restore()` (called from BOTH pool-return ends) hands it
  back, so a re-tinted instance can never leak to the next, possibly player-side, user. Alpha keys, key
  times and MinMax structure survive.

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[hostile-green]` green)

## Oracle — what it proves

`HostileGreenCueRegression`: the placeholders are themselves off-axis; a 9-case truth table (known
offenders trip, deliberate near-neutrals and the arcane/fire cast tints do not); `EnforceOnTint`
substitutes and preserves alpha; and END TO END on the real committed `Fog_poison` prefab — green at
baseline, non-green after `SetTintOverride`, green again after `Restore`.

## ⚠ Honest limits — read the first one before closing this

- **It is an RGB channel-dominance test, NOT a colourblind simulation.** The body case is the sharp one:
  under deuteranopia the retired grunt green (0.30, 0.42, 0.22) and the new umber (0.45, 0.30, 0.20) may
  collapse toward the same olive/brown — **the body change may be invisible to you.** If it is, the
  honest fix is a LUMINANCE-separated or blue-shifted body tint, not another hue. Say the word and it is
  one constant.
- The violet effect tint is defensible (blue-dominant, off-axis) but is still a hue swap. No shape or
  motion cue was added in this lane; the colour half leans on the existing WO-889 aura motion
  separation, which is asserted here, not proven.
- Both placeholder names are literal: they are stand-ins for your pick.

## Owner pins

1. Look pass on both placeholder hues.
2. Does the grunt BODY read different to you now? (the collapse risk above)
3. Player-side heal stays green — confirm that is still what you want.
