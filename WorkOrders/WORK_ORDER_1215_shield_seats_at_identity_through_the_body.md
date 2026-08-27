# WORK ORDER 1215 - A dropped shield attaches at IDENTITY and sits through the hero's body

**Status:** FIXED 2026-08-26 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 294/294 suites` (Builds/g3-c, Builds/g3-r). AWAITING OWNER FELT-VERIFY to close.
**Silo:** Gear seating / attachment orientation
**Origin:** Owner felt-test, Seeker build `2026.08.26.341419`, 2026-08-26.
Owner verbatim: *"and shield sits through the body not seated correctly"*.

## PROOF (captured, not inferred)

- Device screenshot `tmp/shield-seat-101829.png` - the shield renders as a flat, face-on slab
  intersecting the hero's chest and upper arm. Not gripped, not angled, not offset outward.
- `Assets/OffsetForge/offsets.json`, read at source this session: **26 rows total, only TWO mention
  a shield** - `shield_A` (hand-dialled: rot -160/-180/-84, pos 0.12/-0.01/0, scale 1.04) and
  `ShieldWithItemLogic` (**rot 0,0,0** - identity, scale 1.733).
- `Assets/Resources/Data/Canonical/weapons.json`: **19 shield entries exist** - `tripo_shield_a`
  plus `blink_shield1h_01..25`. **Eighteen have no offset row at all**, so they attach at identity -
  exactly the pose in the screenshot.

## Why this is a principle violation, not merely a bug

`docs/ARCHITECTURE_PRINCIPLES.md` §4, verbatim:

> *"Orientation, grip, seat, and scale of any asset are DERIVED - from the mesh BOUNDS + the asset
> NAME - not guessed as a hand-typed Euler and **not left at identity**. Prior sessions ignored this
> and attached weapons at identity (blades laid flat / gripped by the blade) - that is a principle
> violation even though it compiles."*

⛔ **Read `docs/WEAPON_ARMOR_ORIENT_LOGIC.md` before touching any attach / placement / orient code.**
It is binding canon and carries the algorithm.

## The fix is the DERIVER, not eighteen hand-typed Eulers

Hand-authoring 18 rows is the exact failure this repo keeps paying for: **a value authored BY HAND
instead of DERIVED from the thing it describes.** The 2026-08-06 sweep found four of these in one
day (`IsLoop` 53 of 122 picks wrong, the self-contained VFX flag, `HeroTalentNodeDef.Hidden`, the
capture resolution that was a label rather than a layout).

**Required:** extend the existing derivation so a shield with no authored row gets a correct seat
from its own geometry - bounds give the axes (a shield's broadest face is its plane; the grip sits
on the inward normal, offset outward from the forearm), the name gives the archetype.

⛔ **`manual = true` rows are CANON and are NEVER overwritten by the auto pass** (§4). `shield_A`'s
hand-dialled values stay exactly as they are. The deriver fills the GAP - it does not re-derive what
a human already perfected.

⚠ **A shipped prop's mesh may have Read/Write OFF.** The 2026-08-21 sheathe work records that
vertex-based approaches are **silently inert ON DEVICE** while looking correct in the editor. Derive
from `mesh.bounds`, which is available regardless - never from vertex data.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. ⭐ **A DEVICE SCREENSHOT of the hero holding a shield, opened and looked at.**
   ⛔ Headless gates CANNOT see orientation. This is stated canon and `bb6dc010` proved it by laying
   the entire town on its side **with every marker green**. A green gate is NOT acceptance here.
3. Spot-check **at least four different `blink_shield1h_*` ids**, not one. One correct shield proves
   one shield.
4. `shield_A` is byte-identical after the change - prove it, do not assert it.
5. Owner felt-verifies and CLOSES.

## What NOT to touch

- ⛔ The eight structure `-90` pitch rows. Different lane, and they are CORRECT (their FBX metas
  read `bakeAxisConversion: 0`). A "tidy up the remaining -90s" pass breaks all eight including
  `collector_lumbermill`, the FTUE's first building.
- ⛔ The global `_sheatheLongAxisSign` - WO-1136 records that flipping it only moves the defect onto
  the other heroes.
- The equip LOGIC. That a Mage can hold a shield at all is **WO-1214**, a separate ticket.
