# WORK ORDER 1209 - The weapon renders OVERSIZED in a dungeon, and the seat solve re-fires forever

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (dungeon review).
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1209 -> 1210 in the same edit)
**Silo:** Village / Hero equipment
**Reported:** the owner, 2026-08-25, felt-testing build `2026.08.25.341262`:
*"staff oversized in starter loop dugeon"* / *"same thing we saw with the knight sword and shield"*.

---

## She is right that it is a returning class - the code says so itself

`EquipmentController.cs:2828` carries the precedent in its own comment:

> *"Owner F8 2026-07-06 'Shield larger than hero': props are bounds-normalized to their proportional
> heldLength at the WORLD ORIGIN (unit scale), then `SetParent(bone, false)` preserves LOCAL scale -
> so the rendered size gets multiplied by the bone's lossyScale, which carries the
> `VisualFactory.Fit` body-normalization factor."*

`ParentScaleCompensation(Transform parent)` (`:2847`) divides that factor back out. So the mechanism
to prevent exactly this defect EXISTS and is the one to investigate - **do not write a second one.**

## Proving evidence - the owner's device, dungeon `dg_starter_loop`

Screen: `tmp/wo970/staff-dungeon-193002.png`. The staff head alone is roughly the size of the hero's
torso and the shaft runs past the top of the frame. The same staff in the town fight 19 minutes
earlier (`tmp/felt2/combat-191119.png`) is correctly sized - **so the prop is right in one scene and
wrong in another**, which is where the investigation starts.

Device log, `tmp/felt2/logcat-dungeon.txt`:

```
19:29:47.785 [Flow:Equip] sheathed long axis on 'Hero (Blaise)': tiltFromVertical=0deg ...
             src=PER-MESH derived why=taper/taper on Y ... socket='SheatheSocket_HipMain'
19:29:52.792 [Flow:Equip] sheathed long axis on 'Hero (Blaise)': ... (identical)
19:29:57.815 [Flow:Equip] sheathed long axis on 'Hero (Blaise)': ... (identical)
19:30:02.819 [Flow:Equip] sheathed long axis on 'Hero (Blaise)': ... (identical)
```

**Two facts fall straight out of those four lines, and neither is a theory.**

### 1. The hero in the dungeon is `Hero (Blaise)` - the HUD says Thrain, a Mage

`Blaise` is retired canon (`docs/COMBAT_PIVOT_NORTHSTAR.md` supersedes all Blaise / party-of-4
material). The dungeon is posing equipment onto a body whose name does not match the hero the player
is controlling.

⭐ **THIS IS THE PRIME SUSPECT AND IT MUST BE SETTLED FIRST.** A different body means a different rig
means a different bone `lossyScale` - and `heldLength` normalization is computed against the body the
prop was sized for. If the dungeon instantiates a different hero body, a prop normalized for the town
rig renders at the ratio between the two rigs' Fit factors. ⛔ Settle it with a captured line naming
the instantiated body, not by reading spawn code (§12: static reading LOCATES, it never CONCLUDES).

### 2. The seat solve is re-firing about every 5 seconds, in a scene where nothing is moving

`[Flow:HeroOwner]` for the same window reads `velSelf=0.00 velRoot=0.00 pos=(1.23, 0.07, 4.11)`
unchanged - the hero is standing still - yet `ApplyHoldPose` / `ComputeSheathRotation` run again and
again with byte-identical output.

⛔ **That directly contradicts the 2026-08-18 seat-trace fix** (`EquipmentController.cs:2855-2863`),
which made the solve EVENT-DRIVEN precisely so it fires only on attach, on a hand<->back re-parent, on
a body/height swap that moves the bone's lossyScale, and on an authored-scale change. Something is
invalidating `ParentCompensationState` on a cadence. **Whatever is changing is the same thing that can
leave the compensation applied against the wrong parent** - so this is not a separate performance
nit, it is likely the same defect wearing a second face. Find what differs between the recorded state
and the live one; the struct already stores all four inputs.

## What to do, in order

1. **Instrument first.** The values needed are already traced elsewhere in this file -
   `boneLossy=` at `:733` and `:2311` - but did not appear in this window. Make the dungeon path emit
   grip root, parent bone, `parent.lossyScale`, authored scale and the resulting `localScale` on
   attach and on every re-solve. Capture in `dg_starter_loop`, then read.
2. **Answer the body question** with that capture: is the dungeon hero the same body object as the
   town hero, and are the two Fit factors equal?
3. **Fix the cause the capture names.** If it is the body, the fix belongs at the dungeon hero
   spawn/carry seam, not in the compensation maths. If it is the re-solve, fix what invalidates the
   state.
4. **Pin it.** An oracle that asserts a held prop's WORLD size is within tolerance of its authored
   `heldLength` after a scene change - a headless gate can measure bounds even though it can never
   judge orientation (the 08-18 lesson).

## What NOT to touch

- ⛔ Do not add a second compensation path, and do not hand-dial a per-scene scale constant. The 08-09
  precedent: a global rotation "fix" laid the whole town on its side with every marker green.
- ⛔ Do not strip or quiet the repeated `[Flow:Equip]` line to make the log tidy (§12 forbids removing
  instrumentation). Its repetition IS the second finding.
- ⛔ WO-970's grip euler. That ticket owns the sheathed SEAT and the neutral staff grip; this one owns
  SCALE. The 2026-08-25 felt-test proved they are different failures, and tuning one to hide the other
  manufactures a third.
