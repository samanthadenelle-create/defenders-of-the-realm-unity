# Weapon / Armor / Item Auto-Orient Logic — BINDING CANON

> **Owner mandate (2026-06-13):** this logic was worked out in detail and **prior sessions never
> applied it** — weapons got slapped onto the hand at identity and sat wrong (blade flat / pointing
> forward, gripped by the blade). **Do NOT re-derive this worse. Apply it. Manual corrections are
> canon and must never be overwritten by the auto heuristic.** Referenced from `docs/ARCHITECTURE.md`
> and `docs/ARCHITECTURE_PRINCIPLES.md`; read before touching any weapon/armor attach or item placement.

## The principle
**Orient any weapon / armor / placeable from its NAME + its MESH BOUNDS — deterministically — never at
identity.** The name tells you the *archetype*; the bounding box tells you the *axes*. Together they
yield a best-estimate transform with no guessed Euler. A rare human nudge then perfects it and *teaches*
the heuristic.

## The bounds rule (geometry → orientation)
Measure combined renderer bounds, then:
- **Longest axis → the item's primary direction.** For a blade/staff/bow that's the LENGTH → stand it
  **vertical (longest axis → world +Y)**.
- **Narrowest axis → the flat / thickness** (a blade is thin flat-to-flat; a shield is thin front-to-back).
- **The grip/seat is at ONE END of the longest axis** — identified by where the cross-section *changes*
  (a sword's cross-guard *widens*; opposite the tapering point).

This is exactly what `CatalogOrientationBaker` already does for **structures** (longest→+Y, base-centre to
origin) and what `HeroBowAttachment.NormalizeInto` does for the **bow**. The system below GENERALIZES the
bow's logic to every weapon + armor.

## Worked example — a SWORD (the canonical case)
1. **Title = "sword"** → bladed weapon → grip rules apply.
2. **Bounds:** narrowest axis = blade thickness; longest of the other two = blade LENGTH; middle = width.
3. **Rotate longest axis → +Y** → sword stands **vertical, blade up**, narrow axis = the flat face.
4. **Hilt end** = the end where the cross-section widens (cross-guard), opposite the point.
5. **Grip point** = the handle **just below the cross-guard** → align to the `RightHand` bone.
Result: vertical, blade up, hand below the hilt — never blade-in-hand or laid flat.

(Archetype rules extend per type: **shield** → flat face forward, centre→hand; **staff** → longest
vertical, grip lower third; **axe** → longest vertical, grip the haft below the head; **helm/chest** →
fit to the head/torso socket, no hand grip.)

## The system — `WeaponOrientHelper` (generalize `HeroBowAttachment.NormalizeInto`)
1. **Estimate:** `name → archetype` + `bounds → axes` ⇒ best-estimate `{rotation, gripOffset, scale}`.
2. **Apply at EQUIP:** `EquipmentController` calls the helper when a weapon attaches → it self-orients
   onto the hand socket the Humanoid rigs expose (any enemy/hero × any weapon).
3. **Dev-build adjust:** the Orientation Inspector / DevOrient (RAID-launched, our tooling) lets the
   owner nudge the offset live; it saves the corrected transform with **`manual=true`**.
4. **`manual=true` is CANON** — the auto heuristic preserves it untouched, forever (same rule
   `CatalogOrientationBaker` already enforces for structures).
5. **Statistical enhancement:** compare the auto-estimate vs the manual correction across items; the
   accumulated deltas refine the archetype defaults (e.g. "swords consistently need +5° → fold it into
   the rule"). Corrections *teach* the next estimate.

## Existing groundwork (extend, do NOT rebuild)
- `Assets/Editor/CatalogOrientationBaker.cs` — bounds-orient for the structures catalog (longest→+Y,
  base-to-origin, `{euler,offset,scale,note}`, `manual=true` preserved). The template.
- `HeroBowAttachment.NormalizeInto` — the bow's bounds-based auto-orient (the weapon seed to generalize).
- `Assets/_Modules/Village/Hero/EquipmentController.cs` — where weapons equip/attach (the apply point).
- The **Orientation Inspector** (manual correction tool) + DevOrient/RAID (the dev-adjust UI).

## The mandate (why this doc exists)
"The words and dimensions alone tell you one thing." Deriving the transform from name + geometry makes
weapon/armor placement **deterministic and reusable** — do it right once → every item self-orients →
unlimited weapons × unlimited wearers with no per-asset hand-tuning. **Future sessions: apply this; build
`WeaponOrientHelper` on the existing baker/normalizer; never overwrite a `manual=true` correction.**
