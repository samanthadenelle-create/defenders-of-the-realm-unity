# WO-1538 RESULT: cause (a) - the fire-point TRACE was gone from the runtime path

**Status:** IMPLEMENTED - 2026-09-07 uncommitted, awaiting gate. P2. Edit-only lane: no Unity, no git.

## 1. WHICH CAUSE - (a), narrowly. NOT (b).

The ticket title says "projectile"; there is none. This is the MELEE SWING TRAIL
(`WeaponTrailController`), so (b)'s "the fixture never fires a shot" has no object. The fixture DOES
reach the assertion - `Builds/reg-wave3h.log:13735-13736`, captured this run:
`[trail] high-rarity weapon: id='knight_flameblade' rarity='uncommon'` /
`[exec] TRAIL -> controller applied (0.52,0.79,0.36,0.88) (== resolver, NON-steel)`.

The equip took, the real apply ran, the colour matched `WeaponVfxMap.Resolve`. The VFX works and always
did. Missing is the instrumentation: `"TRAIL color="` appears NOWHERE in runtime code - an Assets-wide
grep hits only the oracle's own copies (`ArenaCombatOracle.cs:27,367,368,370`), and
`WeaponTrailController.ApplyWeaponTrailVfx` (`:230-256` pre-edit) held zero FlowTrace calls. It was
dropped when the trail moved off the retired `PlayerAttackController.EnsureSwingTrail` onto this
component (header, `:7-16`) - an instrumentation regression holding the oracle red on a working feature.

## 2. THE CHANGE

- `Assets/_Modules/Village/Vfx/WeaponTrailController.cs` - `using DeNelle.Core.Diagnostics;` plus a
  permanent `FlowTrace.Step("WeaponTrail", "TRAIL color=(...) width=... weapon='...' actor='...'
  rarity=<raw> applied")` at the END of `ApplyWeaponTrailVfx`, after `_trail.colorGradient = grad`, so
  the line proves the renderer was TINTED, not merely that a colour resolved. Rarity is the weapon's
  raw catalog string and nothing sits between it and `" applied"` (the oracle's needle). Dedupe is
  PER-INSTANCE, not `FlowTrace.Once`/`Throttle`: those keys are process-wide and a WeaponTrail already
  exists earlier in the same batch (`reg-wave3h.log:9862`), so a static key would be consumed before
  the oracle installs its sink and the live seam would read dead forever. First apply per actor and
  weapon swaps speak; per-swing repeats stay silent (no frame-path flood). Systems default ON
  (`FlowTrace.Allowed`, `:115-125`), so it speaks in felt-tests too.
- `Assets/Editor/Regression/ArenaCombatOracle.cs` - NEW reachability guard (d.2b) BEFORE the trace
  assertion: if `heroGo.GetComponentInChildren<TrailRenderer>(true)` is null the case FAILS BY NAME
  ("the apply seam built NO TrailRenderer ... UNREACHABLE (a false green, not a pass)") and (d.3) is
  skipped. A seam that no-ops and one that runs but stays silent used to surface as the identical
  line - the ambiguity that cost this ticket its diagnosis. The original assertion is untouched.

Braces balanced (27/27, 81/81), zero NUL bytes, added text ASCII-only. Nothing under Manage/, Raid*,
Dungeons/, EnemyContent/, Harvest*, repair or FeatureFlags touched. `REGRESSION_OK n/n` on a fresh log is still owed by the gating seat.
