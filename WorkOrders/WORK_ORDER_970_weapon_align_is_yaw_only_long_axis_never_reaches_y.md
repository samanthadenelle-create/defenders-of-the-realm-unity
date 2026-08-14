# WORK ORDER 970 — The bounds align can only YAW, so a weapon whose mesh is not authored Y-long never stands up

**Status:** DONE — shipped `af5e2e7d` ("fix(gear): WO-970"). Still open: the owner's re-dial of the two authored nudges, plus owner felt-verify. RESULT file still owed (not fabricated). *(Status corrected 2026-08-14: the line still said "awaiting batch-gate + commit", and was written `**Status: …**` rather than the canonical `**Status:** …` the sibling files use.)*
**Silo:** Hero / Equipment / Geometry — `WeaponBoundsOrient.cs` (orchestrator holds `EquipmentController.cs`)
**Owner report (2026-08-10, felt-test, playing the Mage):** the Emberglass Staff (`tripo_staff_a`)
**"is not being held correctly."**
**Standing order that night:** everything proven with DATA, no guesses or hunches (CLAUDE.md §12).

---

## VERDICT IN ONE LINE

`WeaponBoundsOrient.AlignAxesYLongXNarrowZWide` built its result as
`Quaternion.LookRotation(Cross(xAxis, yAxis), yAxis)` with **`yAxis = Vector3.up` as a CONSTANT** — so its
output was **a yaw-only rotation by construction**, and a yaw can never lift a Z-long mesh onto +Y. Every
downstream seat (hilt inference, grip seat, hand pose, back pose) is written against the premise
"prop-local +Y is the weapon's long axis", so all four were operating on the staff's **1 mm thickness axis**.

**This is NOT WO-966.** Proven at source below. Fixing WO-966 would have changed nothing here.

---

## 1. WHERE THE GRIP IS DECIDED — derived, no manual value exists · **PROVEN BY CAPTURE + READ AT SOURCE**

`tripo_staff_a` runs the **pure geometry-derivation path**. Her log, verbatim:

```
[Flow:Equip]   branch: RESOURCES map (mesh='staff_A')
[Flow:Equip]   resolved vis: mesh='staff_A' kind=Staff leftHand=False native=False
[Flow:Equip]     seat: GEOMETRY - NormalizeInto (longest->+Y) + SeatHiltLowerHalf (hilt=lower half, blade +Y)
[Flow:Equip]     attached 'tripo_staff_a' on 'Hero (Blaise)': gripPos=(0.00, 0.00, 0.00)
                 baseEuler=(0.00, 90.00, 0.00) kind=Staff native=False trustNative=False infer=False
```

`native=False` / `trustNative=False` = the `SeatNative` authored-pivot branch is NOT taken. **READ AT SOURCE:**
`Assets/Resources/OffsetForge/offsets.json` contains `shield_A`, `shield_A@sheathed`, `sword_A`,
`sword_A@sheathed`, `sword_D/F/G` and structure ids — **there is NO `staff_A` and no `staff_A@sheathed` entry.**
So there is **no `manual=true` value on this weapon to protect and none was overwritten.** The whole
orientation is derived, and the derivation is what is broken.

The `baseEuler=(0,90,0)` is not derived — it is `EquipmentController._staffGripEuler = (0f, 90f, 0f)`
(`EquipmentController.cs:446`), the per-archetype calibration nudge; the derived part
(`ComputeMeleeGripRotation`'s `rigAligned`) evaluates to identity on this rig, so the hand base is a
**pure 90 deg spin and nothing else**. See §5 — that nudge is almost certainly a hand-compensation for
this very bug and is flagged for owner re-dial, **not touched**.

## 2. THE ALIGN FAILS — same signature, twice, a month apart · **PROVEN BY CAPTURE**

```
[Flow:Equip] NormalizeInto 'EquipmentProp_Weapon': raw b0=(0.001, 0.001, 0.021)
             aligned b1=(0.021, 0.001, 0.001) target=1.264 -> propScale=58.801
```
and, from `Player-prev.log`, the shield:
```
[Flow:Equip] NormalizeInto 'EquipmentProp_OffHand': raw b0=(0.008, 0.002, 0.01)
             aligned b1=(0.01, 0.002, 0.008)
```

In both, **"aligned" is the raw box with X and Z swapped** — the exact fingerprint of a 90 deg yaw — and the
longest axis lands on **X**, never on the +Y the method's own name promises. These are the ONLY two
`NormalizeInto` traces in either log, and **both failed**.

**READ AT SOURCE** (`WeaponBoundsOrient.cs`, pre-fix): the final line was
`Quaternion.LookRotation(Vector3.Cross(xAxis, yAxis), yAxis)` where `yAxis` is the literal `Vector3.up`.
`LookRotation`'s second argument IS the resulting up vector, and `Cross(anything, up)` is horizontal — so
the product always has up on +Y and forward horizontal: **yaw only**. `alignLong`
(`FromToRotation(Axis(lng), Vector3.up)`) — the one term that could tilt the long axis up — was consumed
only to choose the narrow-axis sign and then discarded.

The 2026-07-06 shield RCA already stood on this exact line, fixed the SCALE symptom, and recorded verbatim
that **"the align's ROTATION is left as-is"**. It was correctly diagnosed and half-fixed a month ago.

## 3. WHICH POSE IS BROKEN — the BACK catastrophically, the HAND accidentally · **PROVEN BY CAPTURE**

One equip, both sockets, consecutive lines (hero 1.75 m, `heldLength = 1.264 m`):

```
[Flow:Equip] parent-scale compensate: parent='CC_Base_R_Hand'      lossy=(1.72,1.72,1.72) authored=1
             -> worldBounds=(0.218, 1.264, 0.196)
[Flow:Equip] parent-scale compensate: parent='SheatheSocket_Back'  lossy=(1.72,1.72,1.72) authored=1
             -> worldBounds=(0.079, 0.097, 1.265)
```

- **BACK — unambiguously wrong.** The staff's entire 1.265 m runs along **world Z**, with only 0.097 m of
  vertical extent. It is lying **dead horizontal, pointing straight out through her back.**
  `ComputeSheathRotation` (`EquipmentController.cs:2064`) intends prop +Y to land on `worldBlade`
  = `body.up` tilted `_sheatheBladeDiagonalDeg` (28 deg) toward the off shoulder — i.e. near-vertical up the
  spine. The measured long axis is ~90 deg off that. The rotation is doing exactly what it says; **it is
  being handed the wrong prop axis.** She is sheathed far more than drawn in this session (343 back
  compensate lines vs 64 hand), which is why this is what she saw.
- **HAND — right by accident, gripped wrong.** World long extent is on Y (1.264), so it reads near-vertical.
  That is not derivation, it is the hand-bone basis happening to map the wrong axis near-vertical; it will
  not survive a rig change or a different hand pose. The **grip point is wrong regardless**:
  `SeatHiltLowerHalf: gripY=-0.019 ... shiftedY=0.021` and `prop.localPos=(0.00, 0.02, 0.00)` — a **2 cm**
  seat shift on a **1.3 m** haft. It binned the 1 mm thickness axis. A hilt seat on a 1.3 m staff must be
  on the order of 0.4 m. **She is holding it by the wrong part of the shaft.**

**Answer: both poses are broken by the same root; the back is the visible one.**

## 4. NOT STAFF-SPECIFIC — it is mesh-authoring-specific · **READ AT SOURCE**

The permutation, not the archetype, decides. `staff_A` is authored **Z-long** (`raw b0` longest on Z), so it
fails. A greatsword authored **Y-long** passes untouched — the solve is a near-identity for it, which is
exactly why swords have looked fine and this survived. It is **not** "keys on the longest axis and mishandles
poles": the length bookkeeping is right (see §6); the ROTATION never happened. Any prop of any shape whose
source mesh is not already Y-long is affected — staff, shield (already proven), any future Tripo import.

## 5. WO-966 INTERACTION — INDEPENDENT · **READ AT SOURCE**

`HeroBodySwapper.cs:263` applies the yaw as
`VisualFactory.Skin(transform, prefab, new SkinOptions { ... LocalRotation = Quaternion.Euler(0f, forwardYaw, 0f) })`
with `forwardYaw = (cls == HeroClass.Knight) ? 15f : -90f`. That rotation is applied to the **body root**, and
the skeleton — including `CC_Base_R_Hand` and the Chest bone the back socket hangs under — is a **child of that
root**. So the mesh and every attached prop rotate **together**. A body yaw error changes where the hero
faces; it **cannot** change how the weapon sits relative to the body.

**The grip is wrong ON ITS OWN.** Landing WO-966 would have moved hero and staff as one unit and changed
nothing about this report. The two stack and must not be tuned against each other.

## 6. THE 1.72 PARENT-SCALE COMPENSATION IS CORRECT — checked, not assumed · **PROVEN BY CAPTURE**

`CompensateParentScale` sets `gripRoot.localScale = (1/lossy) * authored` (`EquipmentController.cs:2001`).
Composed: `1.264 m` (solved in gripRoot units) x `1/1.72` x bone lossy `1.72` = **1.264 m rendered**. Both
captured lines land on exactly `1.264` / `1.265` against a solved `heldLength` of `1.264`, at **both** sockets.
**The compensation is right and is not a contributor.** Cleared.

**One adjacent inconsistency found while in there (NOT fixed here, no capture):** the back path calls
`CompensateParentScale` unconditionally (`:1819`) while the hand path guards it with `if (_weaponParentCompensate)`
(`:1834`). That flag is deliberately false for owner-dialed `fullOverride` scales, so such a prop is
compensated sheathed but not drawn — it would render a different SIZE in the two poses. `shield_A` is
`fullOverride: true`, so it is the live candidate. Ticket separately; do not fold into an orientation fix.

---

## THE FIX (landed — `Assets/_Modules/Core/Geometry/WeaponBoundsOrient.cs`)

`AlignAxesYLongXNarrowZWide` now solves the basis change directly instead of hand-assembling axes:

```csharp
Quaternion meshToParent = Quaternion.Inverse(Quaternion.LookRotation(Axis(med), Axis(lng)));
prop.transform.localRotation = meshToParent;
```

`LookRotation(med, lng)` is the rotation `S` mapping `(+Z, +Y) -> (med, long)`; its inverse carries the mesh's
long axis onto **+Y** and its medium axis onto **+Z**, leaving narrow on **+X** — the method's contract, now
actually met. **DERIVED from the bounds permutation; no hard-coded compensating Euler, and no pitch standing
in for a yaw (QR-5.2).** `med` and `long` are always distinct unit axes so `LookRotation` is never degenerate;
the result is a proper rotation, so the old `Dot(zAxis, medDir)` sign patch is gone with the hand-built basis
it existed to repair. **Which END points up is deliberately still decided downstream** by
`EnsureHandleAtShortYEnd`'s Z-profile spike — this stays a pure axis solve.

Verified for the captured staff by hand: `lng=Z, med=Y, sht=X` -> R maps `Z->+Y`, `Y->+Z`, `X->-X`. Long axis
reaches +Y. Correct.

**Instrumentation added (permanent, per CLAUDE.md §12 — never stripped):**
```
[Flow:Equip] AlignAxes '<prop>': meshSize=(...) longAxis=Z narrowAxis=X wideAxis=Y
             -> seated long on +Y (localEuler=(...))
```
**This is the line that proves the fix on her next equip.** Read it with the existing `NormalizeInto` line:
`aligned b1` must now come back **Y-longest** — `(0.001, 0.021, 0.001)` for the staff, not `(0.021, 0.001, 0.001)`.
If `aligned b1` is ever non-Y-longest after an `AlignAxes` line, the derivation regressed.

**Expected downstream movement, so it is not misread as a new bug:**
`SeatHiltLowerHalf`'s `shiftedY` should jump from **0.022** to roughly **0.4** (it will finally bin the real
haft), and the sheathed `worldBounds` should move its 1.265 m from world **Z** to near **Y** (28 deg off vertical,
per `_sheatheBladeDiagonalDeg`).

## OWNER PIN — two authored nudges were dialed on top of the broken base (§4: manual is CANON, NOT touched)

Both left exactly as she set them. Neither file was edited.
- **`_staffGripEuler = (0, 90, 0)`** (`EquipmentController.cs:446`) — a pure 90 deg spin, and the derived base
  next to it is identity. It reads as a hand-compensation for a flat staff. **Likely wants a re-dial to
  (0,0,0) in the Seating Editor once the base is correct — owner's call, owner's hands.**
- **`sword_A` rot `(117, -2, 110)`** (`offsets.json`, `fullOverride: false`, so it COMPOSES on the base) —
  will shift **only if** `sword_A`'s mesh is not authored Y-long. Unknown: no `NormalizeInto` capture for a
  sword exists in either log. **The new `AlignAxes` line answers it on her first sword equip** — `longAxis=Y`
  means the base did not move and her nudge is untouched.
  (`shield_A` is `fullOverride: true` = absolute in the socket frame, so it is immune either way.)

## FENCE / RULES OBSERVED
Touched exactly one file: `Assets/_Modules/Core/Geometry/WeaponBoundsOrient.cs`. No fenced file touched
(`HeroLocomotion`, `DungeonHero`, `DungeonCameraRig`, `EquipmentPanel`, `InventoryUIBuilder`,
`HeroPreviewViewer`, `EndState*`, `BattleArena*`, `TownSuspension`, `DataRegression` all untouched).
No Unity run, no gate, no git, no commit — orchestrator batch-gates and is sole committer.
Braces balanced 35/35, NUL-free. ASCII only. No FlowTrace stripped; one permanent trace added.
No `manual=true` / authored value overwritten.
