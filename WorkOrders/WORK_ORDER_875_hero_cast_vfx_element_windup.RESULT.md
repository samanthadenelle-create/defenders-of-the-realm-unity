# WORK ORDER 875 - RESULT: element-coded hero cast feedback

**Status:** FIXED 2026-08-25 - source acceptance complete; owner device visual approval remains.
**Code commit:** `1772be8af`

## Landed

- Every committed hero cast now receives one semantic cast flash through the existing Fire, Frost, Arcane, Holy, or Physical family.
- Existing windup timing and interruption remain authoritative; cancelled casts do not receive a committed-cast flash.
- Existing owner-authored motion registry behavior remains additive without duplicating the keyless fallback path.
- Arcane violet is resolved before the broader Frost color heuristic, and violet mage area casts remain Arcane rather than being swallowed by the AoE rule.
- The focused regression is registered in `DataRegression` and pins real ability semantics, single-fire routing, windup ordering, and registry mirror integrity.

## Fresh evidence

- `COMPILE_GATE_OK`
- `HERO_ELEMENT_CAST_VFX_OK`
- Integrated registered regression green.

## Residual - owner felt-test

Capture and inspect the hero kits on device, including Fire/Frost/Arcane/Holy differentiation, an ultimate, and the Thrain/Sylas reads. This is visual approval, not an identified source gap.
