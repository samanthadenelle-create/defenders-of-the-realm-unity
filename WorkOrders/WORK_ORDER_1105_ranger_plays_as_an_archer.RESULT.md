# RESULT — WO-1105 Sylas the Ranger plays like an archer

**Date:** 2026-08-16  **Seat:** CLI (commits `562f3d3e5`, `14a2c66ed`, `682c6f595`, `998ca0751`)
**Status:** DONE — pending PO felt-verify

⚠ **Read the arc, not just the last commit.** The lane ran in four passes and the OWNER REVISED IT
mid-flight (R5), so `562f3d3e5`'s ranged-primary is deliberately *reverted* by `682c6f595`. The shipped
arrangement is the R5 one.

## What shipped (final state)

1. **The bow is an ACTION-BAR ability on slot Q; the dagger/melee sweep is the primary attack** —
   owner, verbatim: *"change the bow and arrow attack to the action bar and leave the attack as the
   dagger attack"* (`682c6f595`).
   ⚠ **The bow was already on the bar.** `ranger.q` (Quick Shot) has always been slot Q, and Q is the
   class's LOCKED basic (only W/E/R are loadout-swappable). `562f3d3e5` additionally wired the shot to
   the PRIMARY input, making it reachable two ways; R5 removed that path. What was genuinely missing was
   the **FACE** — nothing had ever rendered a verb on an ability slot.
2. **`FirePrimary` / `FireRangedPrimary` / `ResolveRangedTarget` / `ResolvePrimaryFace` deleted**
   (~145 lines), so the phone's one attack button never spends an arrow. A new regression case FAILS if
   `DrivePrimaryFace` or the interactable override reappears.
3. **Verb + icon are DATA-DRIVEN, never a per-class table:** new `AbilityDef.Verb` /
   `AbilitySlotRecord.Verb` carried through to `SetCaption`; only `ranger.q` ("Shoot") and
   `mage.fireball` ("Cast") author one, plus a `concept-icons.json` row `ranger.q -> spellicons/Hunter12`.
   Both JSON twins updated.
4. **Bow grip, DERIVED** (`562f3d3e5` then corrected in `14a2c66ed`). `WeaponBoundsOrient`'s
   bounds-centre seat put the grip in the HOLLOW between string and belly. Owner ruling, verbatim:
   *"You wanna follow that perpendicular from the y axis over to the rounded hilt. The round part of the
   bow is where the grip is."* The rule now walks the mid-Y band and takes the **MAX** depth over every
   vertex (`apexDist = max((v.z - zStraight) * dir)`), so the string (depth ~0) can never win — the apex
   of the rounded side is the riser. Gate measured it: **`bow-grip-apex` err = 0 m**.
5. **Bow HELD ROTATION was 90° off** (`998ca0751`) — found from an owner screenshot with annotation
   (canon: for visual/spatial defects the screenshot IS the data). **Not** the grip position, and not
   fixed by moving the grip. Root cause: `HeroBowAttachment.cs:232` was an identity hand-local rotation,
   which maps the bow's long axis onto the LeftHand bone's own +Y — correct for a SWORD (blade continues
   the fist), wrong for a BOW (the hand closes AROUND the riser, perpendicular to the limb span). Fix is
   derived, not a nudge euler: new **`WeaponBoundsOrient.ComputeBowHeldRotation`** builds the target in
   world from the body's axes (limbs → `body.up`, belly → `body.forward`) then expresses it in the bone's
   local frame — the same construction `ComputeSheathRotation` already uses.
   ⚠ **This bug was found once before and the fix was REVERTED.** A prior "+91 Z" tweak was almost
   certainly someone spotting the same 90° and guessing at the axis; reverting it restored the defect,
   and a comment then asserted "the bow arrives in the hand ALREADY oriented to spec — so GripLocalEuler
   stays ZERO", locking the wrong conclusion in for every later reader. The block is rewritten with the
   history preserved and the correction marked, not deleted.
6. **The action-bar cast path was COMPLETELY SILENT** and now prints slot / id / `FIRED|GATED` with the
   cooldown and mana that gated it.

## Deliberately NOT done

- **The cooldown special case is GONE, and its justification went with it.** `14a2c66ed` kept the slot
  interactable during the sweep so the ranger would not be inputless while the bow cooled; with the
  dagger as the basic attack that is no longer true, so the bow slot greys out through the ordinary
  `!cooling` gate like every other ability. The attack PILL keeps its build-time face deliberately —
  driving `SetCooldown` on it would disable it for the whole 0.6 s swing and make the touch perfect-hit
  second tap unreachable.
- **CROSSBOWS EXCLUDED** per her ruling (*not until one is verified*): no inverted mapping is authored.
  The guard is in the oracle, not a comment — `[ranged-primary]` asserts the runtime `Resources`
  `weapons.json` carries no crossbow in id / mesh / name / **category** (category is the field the
  431-row StreamingAssets side keys its 125 crossbows on), so `Generate Gear Catalog` re-inflating
  96 → 431 cannot ship one silently.

## Known / carried caveats

- **MEASUREMENT LIMIT, stated plainly (`14a2c66ed`):** no real bow mesh was measured. Bow FBX/meshes are
  binary and only readable inside Unity, and that lane was fenced from batchmode. The trace prints
  `apexZ` / `apexOverNearest` / grip-vs-bounds-centre on the first real draw — if it still sits wrong,
  that line names the step to argue with. (The later `998ca0751` gate did measure err = 0 m against a
  synthetic bow.)
- **Quick Shot's cooldown is 0.45 s and the shared readout prints whole seconds**, so it flashes "1" for
  the whole sweep. That is the existing shared visual. If it reads as broken on device the fix is a
  longer cooldown (balance) or a fractional readout (new visual) — **not** a per-ability special case.

## Owner decisions left open (each its own ticket, not fixed here)

- **The mage's Q medallion now reads "Cast" but its icon is still a SWORD** — there is no
  `mage.fireball` concept-icons row. Visible consequence she has not seen yet.
- **`HeroCatalog` carries a separately drifted copy of ability names** — it still says "Frost Nova" and
  "Mending Salve" where the live kit disagrees.

## Oracle

`RangedPrimaryRegression` → `[ranged-primary]`, rewritten to the R5 arrangement rather than weakened:
crossbow exclusion untouched, plus new cases pinning bow-on-slot-Q, cooldown-greys-out, the grip apex,
and the derived held rotation.
