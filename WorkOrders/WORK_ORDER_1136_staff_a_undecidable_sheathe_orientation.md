**Status:** DONE 2026-08-22 - owner ruled the SIGN is the wrong question for a symmetrical staff; M3 now asserts VERTICALITY instead. Three-way outcome (Decided/SignAgnostic/Undecidable), no exemption list, no global-sign flip. Gate-green 255/255.

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

---

# ★★ OWNER RULING 2026-08-22 — THE SIGN IS THE WRONG QUESTION FOR A STAFF

Owner, verbatim: **"the staff should be longest mesh on Y axis with and placed with staff still
verticle not horizontal"**.

## WHAT THIS RESOLVES

M3 fails `staff_A` because it cannot resolve a SIGN - which end is the tip. That question is
**undecidable for this mesh** (taper relGap 0.001, grip-origin relGap 0 - both ends identical to four
decimals) and, per this ruling, **it is also irrelevant**. A symmetrical staff has no upside down.

**The property that actually matters is VERTICALITY**, and unlike tip direction it is measurable. The
shipped instrumentation already reports it:

```
[Flow:Equip] sheathed long axis on 'Hero (Blaise)': tiltFromVertical=0deg
             (must read ~0; ~90 means it is lying across the body)
```

**The rule, stated for implementation:**
1. A staff's LONGEST MESH AXIS is **Y**. That is assertable from `mesh.bounds`.
2. Sheathed, it stays **VERTICAL** - `tiltFromVertical ~= 0`, never ~90 (lying across the body).
3. **Tip direction is NOT asserted for a sign-agnostic prop.** Either way up is correct.

## ⛔ HOW NOT TO IMPLEMENT THIS

**Do NOT add `staff_A` to an exemption list.** That weakens the oracle to make a symptom go away, and
the next symmetrical prop silently inherits the same hole. This repo has spent the week finding
suites that pass while asserting nothing - do not author another.

**Do NOT flip the global `_sheatheLongAxisSign`.** M3's own text says why: it is correct for at most
half the catalogue by construction, so flipping only moves the defect.

## THE CORRECT SHAPE

Narrow M3's question to the one that matters, and keep it FAILABLE:

- **Prop resolves a sign** (ends differ - a sword's hilt vs point): assert the sign, unchanged.
- **Prop is SIGN-AGNOSTIC** (ends measurably identical, long axis Y): **not a failure** - but now
  assert the REAL requirement, that it seats VERTICAL (`tiltFromVertical` within tolerance of 0).
  A sign-agnostic prop that hangs HORIZONTAL must still fail.
- **Prop is ambiguous AND asymmetric, or its long axis is not Y**: still a hard FAIL. That is a
  genuinely broken prop and the case must keep catching it.

The assertion gets NARROWER and STRONGER at once: it stops demanding an answer the geometry cannot
give, and starts demanding the one the player can actually see.

## ACCEPTANCE

- [ ] `staff_A` passes because it is measured VERTICAL and Y-longest - not because it is exempted
- [ ] A sign-agnostic prop rotated to lie horizontal FAILS the case (prove it red-before/green-after)
- [ ] An ambiguous prop whose long axis is NOT Y still fails
- [ ] The global-sign flip is not used anywhere in the fix

---

# IMPLEMENTATION 2026-08-22 (edit-only agent; CLI gates + commits)

**Shape:** a mesh's ends now resolve into THREE outcomes, recorded at the point of measurement:
`WeaponOrientHelper.SheathedSignDecision` = `Decided` / `SignAgnostic` / `Undecidable`.
`SignAgnostic` = **both** discriminators under the new `SignAgnosticSymmetryEpsilon` (0.02) — the ends
are *identical*, not merely *undecided*. The band between 0.02 and the untouched 0.15 decision margins
is where the ends genuinely differ and we failed to read them: that stays `Undecidable` and stays a hard
failure. `TryResolveSheathedTipSign` still returns **false** for both non-`Decided` outcomes (no sign was
measured, so none is handed to a caller — M1d's rule is intact); the *distinction* rides on the struct.

**The new assertion:** `WeaponOrientHelper.TrySheathesVertical` — three failable clauses: a long axis
exists (`LongAxisDominance >= 2`), it is **Y** in the seat frame, and it is within 5° of vertical.

**M3** now demands per-outcome: sign (unchanged) / verticality / hard fail. **M3b** is new and proves the
verticality clause can REFUSE — a symmetrical slab the seat cannot stand up, plus the oracle asked
directly about a long-axis-Z and a long-axis-X prop. **`EquipmentController.ResolveSheathedTipSign`**
stops crying "may hang upside down" at a symmetrical prop and measures verticality instead (`Fail` if it
is lying across the body).

**Two frames, deliberately:** the SIGN is asked in the authored frame (as M3 always did); VERTICALITY is
asked after the shipped `WeaponBoundsOrient.NormalizeInto`, because that is the frame the sheathe pose
consumes. A raw KayKit FBX is commonly Z-long — staff_A's own failure text says "AMBIGUOUS on **Z**" —
so asserting Y against the authored frame would red a prop that plays fine.

⛔ No exemption list, no mesh name in any code path, no global-sign flip, thresholds untouched.
Canon updated in the same change: `docs/WEAPON_MESH_ARCHETYPES.md` §3.

**Files:** `Assets/_Modules/Core/Geometry/WeaponOrientHelper.cs`,
`Assets/Editor/Regression/SheathePoseRegression.cs`,
`Assets/_Modules/Village/Hero/EquipmentController.cs`, `docs/WEAPON_MESH_ARCHETYPES.md`.
Braces balanced, no NUL bytes. **Not gated, not committed** — CLI seat owns both.

## FIX 2 — the M3b fixtures were not the shape they claimed (first gate, 253/254)

First gate: **`staff_A` passed**, 11 props stayed green, and the only red was **my own fixture M3b-A**.
Diagnosed from the captured trace, not from source-reading:

```
[Flow:Equip] AlignAxes 'VerticalityFixture': meshSize=(1, 1, 1) longAxis=X ...
[Flow:Equip] SheatheSign 'VerticalityFixture': ... longest=X(1m) dominance=1 longAxisOffVertical=90deg
```

**Dominance 1 on a bar authored 20:1.** Cause: the fixtures put their shape in `transform.localScale`,
and `NormalizeInto`'s fourth line is `prop.transform.localScale = Vector3.one;` — by design, because a
real prop carries its shape in its MESH and the seat owns the scale. So the seat wiped both fixtures and
handed the oracle a **1x1x1 cube**. Clause 1 (`LongAxisDominance >= 2`) was **RIGHT to refuse it** — an
object with no long axis has no verticality. The fixture was wrong. Bar untouched, nothing special-cased.

**⚠ The half that did NOT show up as red is the worse half.** A and B differ only in size, so once both
were flattened to the same cube their traces came out **byte-identical** (`Builds/reg-staff.log`
17830-17867 vs 17885-17922). **M3b-B was reporting `ok` while asserting nothing** — refused for being a
cube, not for being a slab. A green tick over a shape that no longer existed: precisely the hollow-suite
failure this repo has spent the week hunting, authored by the case written to prevent it.

**Fixed:** fixtures now scale the unit cube's **vertices into a new mesh** (never mutating Unity's shared
built-in cube; the mesh is destroyed with the fixture), and each fixture **asserts its own premise first**
via new `FixtureShapeHolds` — the pre-seat measured size must match the authored size, or the case fails
loudly instead of rendering a verdict about the wrong object. That guard is the durable part: it would
have caught this in the first gate, and it catches the next seat change that invalidates a fixture.
