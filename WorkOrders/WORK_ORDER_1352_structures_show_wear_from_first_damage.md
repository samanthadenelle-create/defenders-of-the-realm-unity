# WORK ORDER 1352 - Structures show wear from the first point of damage

**Status:** FIXED 2026-09-03 - shipped in `2026.09.03.353999` and installed on the Seeker. A SCUFF rung
added below smolder inside the EXISTING owner (`StructureDamageVisuals`): albedo x0.88 / x0.77 / x0.66
with smoothness x1.00 / x0.70 / x0.40 across 100%-83.3%-66.7%-50% HP, handing off to the unchanged
smolder -> fire ladder. Gates `COMPILE_GATE_OK` + `REGRESSION_OK 358/358`.
⚠ AWAITING HER EYE ON ONE NUMBER: at 95% HP the tell is a 12% darkening, which may be too subtle to
read at a glance. The band is authored in `damage-states.json`, so strengthening it is a data change,
not a rebuild.
**Silo / Lane:** VFX / structure damage presentation
**Type:** EXISTING owner, new bottom rung
**Minted:** 2026-09-03 - ⚠ minted by an implementing agent while the numbering banner still read 1349,
so 1349-1352 were consumed without a bump. Reconciled in the banner's fortieth pass.
**Severity:** P2 - the game was billing her for repairs on structures that looked pristine.

## The owner's ruling

She was offered three ways to reconcile the mismatch and chose: **show a visible tell from the FIRST
point of damage**. Explicitly NOT chosen: suppressing the repair affordance above the smolder threshold
(that would make a 60%-HP structure unrepairable), and narrowing Repair-All only.

## The mismatch it closes

| | |
|---|---|
| `RepairTarget.cs:150` | `NeedsRepair => DamageFraction > 0.0001f` |
| `StructureDamageVisuals.cs:108-109` | `smolder = 0.5f` - the FIRST VISIBLE tell |

**So 50%-99.99% HP was pristine to the eye and damaged to the code.** Her device toast proved what that
costs: `"Repaired 1 structures for Wood 35, Iron 7"` - Repair-All ran and charged her for a structure
showing nothing at all. After this change, what she sees always matches what she is billed for.

⚠ **WO-1296 (2026-09-02) could not have caught it.** It changed a MESSAGE, not a predicate, and only
covered `DamageFraction == 0` - a tap on a truly pristine structure. This is the other case, which is
why she reported it as *"still"* showing up.

⚠ **And there was no data because the probe was silent:** `RepairAvailabilityProbe.Poll()` returned
early whenever nothing was on fire - and an invisibly damaged structure is by definition not on fire.
It now runs the inverse pass and names every structure in the silent band with its HP, threshold and
the exact price being charged.

## Why it is not hue-carried

The albedo is multiplied by a **scalar**: R, G and B scale by the identical factor, so hue and the
saturation ratio are mathematically unchanged and only **value** moves. Any greyscale conversion is a
weighted sum of R,G,B, so it scales by that same factor - **the tell survives full desaturation
exactly intact.** The second channel is smoothness (matte), a texture read, not colour at all.

## The cost constraint that shaped it

Unlike smolder, which only ever ran on a damaged few, this sees EVERY structure in a town - and the
device already reports `VfxPerfGate` hitches against a 16.7 ms budget.

- **Undamaged structure: one int compare + two dictionary lookups per 0.3 s eval. Zero allocation,
  zero property-block write, zero work.** A first-line guard exists precisely so a pristine town is
  never resolved or written to - an MPB drops a renderer out of the SRP batcher, so "setting it back to
  its own colour" would have cost draw calls town-wide, permanently.
- Damaged structure: one `GetComponentsInChildren` + N writes ONLY on a step change; at most 4
  transitions across a full damage-and-repair cycle.
- **Zero particles, zero GameObjects, zero pooled loop slots.**

## Baked hub structures - it needed both halves

1. Resolve filters on `r.enabled`, which is exactly what `HubStructureVisualInjector.SkinStorefront`
   uses to hide the baked mesh (`r.enabled = false`, not `SetActive`) - so the tell tints the injected
   `LightSkin_` child, never the invisible baked twin.
2. The renderer list re-resolves when the host's `childCount` moves, so a **late-arriving** skin picks
   it up on the next eval.

Without both, this would have worked everywhere except the town she is looking at.

## Oracle

Appended to the existing `StructureBurnRegression`. It binds to the LIVE `RepairTarget.NeedsRepair`
rather than a copy of the `0.0001` literal, sweeps 201 samples for silence and monotonicity, and pins
that `scuffOnset` may never be authored below the repair predicate.

**Mutation:** setting `scuffOnset` back to `0.5` (the pre-change world) fails three ways - 100/201
silent samples, wrong ordinal above the handoff, onset below threshold.

⭐ The agent found a real bug in its own first draft doing that: the monotonicity test was inverted and
passed everything vacuously. Caught only by running the arithmetic, not by reading it.

## Acceptance

- [x] No HP band is visually silent while repair-eligible.
- [x] Not hue-carried; survives greyscale.
- [x] Reaches baked hub structures, including a late-arriving skin.
- [x] Zero cost on an undamaged town.
- [x] Oracle proven RED; mutation reported.
- [ ] ⛔ **Owner felt-verifies the tell is STRONG ENOUGH to read at a glance**, and closes. If not, the
      lever is `scuffMinDarken` / `scuffMaxDarken` in `damage-states.json` - data, not a rebuild.
