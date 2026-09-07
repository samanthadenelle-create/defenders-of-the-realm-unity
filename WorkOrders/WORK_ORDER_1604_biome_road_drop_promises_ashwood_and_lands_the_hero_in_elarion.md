# WORK ORDER 1604 - The biome road drop promised Ashwood and landed the hero at (0, 0.08, 50), which ZoneManager calls Elarion

**Status:** READY TO IMPLEMENT (instrument first) - minted 2026-09-07 (CLI) from F8 seq 4703
**Silo / Lane:** Village/World - `Assets/_Modules/Village/World/HollowRoadsDropInjector.cs` (~:495-512, the arrival check), `BiomeRoads` (the region split / ZoneName), `ZoneManager`
**Type:** EXISTING system, DEFECT (the prompt lied)
**Priority:** P2

## Evidence

seq 4703 (13:27 local, Main_Castle_Overworld): `[Flow:BiomeRoads] drop promised Ashwood but the hero
landed at (0.00, 0.08, 50.00), which ZoneManager classifies as Elarion. The derived point and the region
split disagree - the prompt told the player something untrue.` (0, 0, 50) is the derived drop point for
the Ashwood road; ZoneManager's split puts that point inside Elarion, so either the derived point is too
close to the Heart or the split's Ashwood boundary starts further out.

## What to do

- Instrument: log the derived point, the split's boundary for the promised region, and the point's
  classification BEFORE the drop; refuse a drop whose derived point does not classify as its own region
  (fail-closed with a Notify naming the road) instead of teleporting and then complaining.
- Read the two authorities at source; pick the single owner of "where does Ashwood start" and derive the
  drop point from it (boundary + margin). Pin: every road's derived point classifies as its region (RED
  first with the current (0,0,50)).

## Acceptance
- Regression proves each biome road's drop point classifies as its region; device drop lands in Ashwood
  with no Fail line. Owner felt-test closes.
