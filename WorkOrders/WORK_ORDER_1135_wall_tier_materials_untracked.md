**Status:** READY TO IMPLEMENT

# WORK ORDER 1135 — Wall tier materials are not tracked: all three tiers ride embedded FBX materials

**Minted:** 2026-08-21 (CLI, banner bumped 1135 -> 1137 in the SAME edit alongside WO-1136)
**Lane:** World/Environment (art pipeline). **Class:** EXISTING DEBT, newly made visible.
**Silo:** Art / walls.

## HOW THIS SURFACED

A NEW oracle authored 2026-08-21, `Assets/Editor/Regression/RaidWallMaterialRegression.cs`
(untracked at time of minting), failed on its first ever run:

```
raid-wall-material: 3 defect(s) across 3 tier(s) -
 wall tier 'wood':  NO tracked material at Assets/Resources/Walls/Materials/wood_wall.mat
 wall tier 'iron':  NO tracked material at Assets/Resources/Walls/Materials/iron_wall.mat
 wall tier 'steel': NO tracked material at Assets/Resources/Walls/Materials/steel_wall.mat
```

⚠ **THIS IS NOT NEW BREAKAGE.** Verified at source: `Assets/Resources/Walls/Materials/` **does not
exist at all**. It never has. The walls have always rendered from each FBX's EMBEDDED material.
Today is simply the first time anything checked. Do not treat this as a regression from the
2026-08-21 work and do not go looking for what "broke" it.

## WHY IT MATTERS

The three tiers are real gameplay states, not decoration - `WallTier { Wood = 1, Iron = 2,
ReinforcedSteel = 3 }` (`Assets/_Modules/Village/Walls/WallTierData.cs:29`), indexed by
`WallSegment._tier`, and the ladder is both a COSMETIC and a COST progression the player pays for.
An embedded FBX material means:

- **The upgrade the player bought may not read as different.** If all three FBXs embed similar
  materials, a paid tier change is invisible - the exact defect class of "paid for something that
  does not render" (WO-1118 vapor rule).
- **Textures bind through the FBX importer**, so a re-import or an art-pack refresh can silently
  change or lose them, with nothing tracked in git to diff.
- **`WallTierDef.SegmentPrefabPath`** is documented in-code as *"owner art, pending"* - so the art
  side of this ladder was always known to be unfinished.

## SCOPE

1. Determine whether the three tiers currently render **visibly differently** on device. Screenshot
   evidence, not inference (memory `screenshots-are-primary-evidence-for-visual-defects`).
2. If they do not, that is the real defect and it is a PRODUCT issue - a purchased upgrade that does
   not show. Raise it to the owner before authoring art.
3. Author tracked materials at the three paths the oracle names, OR change the oracle to assert
   whatever the real sanctioned source is. ⛔ Do not "fix" this by deleting the assertion.
4. ⚠ The owner is RED/GREEN COLOURBLIND - the three tiers must be distinguishable by VALUE and
   SILHOUETTE/TEXTURE, never by hue alone. A greyscale screenshot is the acceptance gate.

## NOT IN SCOPE

Wall placement, adjacency (WO-972), tier costs/durability, `WallDefense.TargetHeight`.

## ACCEPTANCE

- [ ] `raid-wall-material` passes, or its assertion is corrected to the real sanctioned source with
      a recorded reason
- [ ] The three tiers are distinguishable in a GREYSCALE screenshot
- [ ] Nothing depends on an embedded FBX material for a gameplay-meaningful visual state
