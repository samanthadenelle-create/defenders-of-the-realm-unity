**Status:** READY TO IMPLEMENT

# WORK ORDER 1136 — staff_A has no decidable sheathe orientation (the last 1 of 12)

**Minted:** 2026-08-21 (CLI, banner bumped 1135 -> 1137 in the SAME edit alongside WO-1135)
**Lane:** Hero / equipment visuals. **Class:** EXISTING DEBT, newly measured.
**Provenance:** owner felt-test 2026-08-21: *"shield looks good sword upside down (sheathed)"*.

## WHERE THE WEAPON-SEATING WORK LANDED

The 2026-08-21 pass replaced the single global `_sheatheLongAxisSign` with a PER-MESH derivation
(`WeaponOrientHelper.TryResolveSheathedTipSign`), reading `mesh.bounds` because the shipped props
have **Read/Write OFF**, which makes every vertex-based approach inert on device.

**11 of 12 shipped weapon meshes now resolve.** This ticket is the twelfth.

## THE MEASUREMENT (from `SheathePoseRegression`, not inferred)

```
M3: 1 of 12 shipped weapon meshes cannot resolve a sheathed orientation - staff_A
  (taper AMBIGUOUS on Z (relGap=0.001 < 0.15) - neither end reads as the pointy one;
   and the grip origin sits mid-prop on Z (|-end|=1.0747 |+end|=1.0747 relGap=0 < 0.15),
   so neither end is the hilt by proximity either. NOTHING DECIDABLE - the caller's
   serialized sign stands and this prop may hang upside down.)
```

**Read that carefully: the prop is geometrically SYMMETRICAL.** Both ends measure identical to
four decimal places on both tests. This is not a derivation bug and not a tuning miss - **the mesh
genuinely does not encode which end is the top.** No amount of cleverness in
`WeaponOrientHelper` can extract a fact the geometry does not contain.

Asset: `Assets/Models/KayKit/KayKit Fantasy Weapons Bits 1.0/Assets/fbx/staff_A.fbx`
(a KayKit staff - and a staff being symmetrical is entirely reasonable for a staff).

⛔ **DO NOT "fix" this by flipping the global sign.** The regression text says exactly why: the one
global sign is correct for at most half the catalogue BY CONSTRUCTION, so flipping it only moves
the defect to the other half. That is the bug this whole lane already fixed once.

## THE THREE HONEST OPTIONS (owner picks; do not assume)

1. **Author a per-asset override.** A tiny explicit table/field for the handful of props whose
   geometry is undecidable, consulted only when derivation returns NOTHING-DECIDABLE. Truthful and
   cheap. ⚠ It IS duplicated state, so it must be a LAST-RESORT fallback keyed off the derivation
   failing - never a parallel source of truth that can drift (the failure class behind the stale WO
   block, the stale dependency table, and the fallback cost table found the same day).
2. **Fix the asset.** Re-export `staff_A` with an asymmetric pivot or a distinguishable head so the
   geometry says which end is up. Cleanest long-term; needs art time.
3. **Accept it.** A symmetrical staff hanging either way up is arguably not wrong - a staff has no
   obvious "upside down". Costs nothing. Verify on device before choosing this.

**Recommendation: (3) pending a screenshot, else (1).** A symmetrical staff is the one prop where
"upside down" may be meaningless, so LOOK before building machinery for it.

## ACCEPTANCE

- [ ] Device screenshot of `staff_A` sheathed (memory `screenshots-are-primary-evidence-for-visual-defects`)
- [ ] Owner rules between the three options
- [ ] `SheathePoseRegression` M3 passes, or records staff_A as an ACCEPTED exception naming the
      reason - ⛔ never by weakening the 12-mesh sweep or the ambiguity thresholds
- [ ] The global-sign flip is NOT used as the fix
