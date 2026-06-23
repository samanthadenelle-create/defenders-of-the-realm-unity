# WORK ORDER 448 — Hub→OuterWorld seam: natural transition + kill the castle-floor z-fight

**Status: READY TO IMPLEMENT.** Owner-reported + agent-RCA'd 2026-06-17. Lane: World/seam (CastleHubBuilder,
SceneTransitionTrigger, GroundZFightFixer). Requires a **hub rebuild** for CastleHubBuilder changes (bake → CLI,
editor closed).

## The report (player, 2026-06-17)
"When I walk out I'm auto-teleported to the outpost" — which has a "broken floor that was never fixed" where
"the texture is fighting for the ground rendering."

## Root cause (agent RCA, cited — NOT an outpost; TWO independent bugs)
1. **The "teleport" = an auto-cross seam, not an outpost.** There is no outpost at the landing (OuterWorld has
   only terrain/mine-nodes/anchors; `RaidOutpostSystem` is feature-flagged OFF and anchors at ±70 anyway).
   "The outpost" = the **OuterWorld landing zone near origin**. The hub south gate (`SceneTransitionTrigger`,
   wired in `CastleHubBuilder.cs:1043-1072`) has **`requireConfirm = false`** (set 2026-06-17, `:1069-1071`) +
   an **18m radius** (`:1067`) → it auto-crosses on mere APPROACH, warping the hero to a hardcoded
   `targetPosition = (0, 0.5, -12)` (`CastleHubBuilder.cs:1059`).
2. **The z-fight = coplanar floors.** Castle plaza `qFloorWood` at **Y=0.01** (`CastleHubBuilder.cs:213`,
   footprint XZ [-16,+16]) vs OuterWorld `ExteriorTerrain` at **Y=0** (`OuterWorld.unity`). The castle is
   **never unloaded on cross** ("FIX B FLAGGED, NOT APPLIED", `SceneTransitionTrigger.cs:395-404`), so its floor
   keeps rendering under the terrain. The existing fix `GroundZFightFixer.FixGroundPlane()` lowers coplanar
   ground to Y=-0.5 but `InVillageScene()` (`GroundZFightFixer.cs:132-136`) gates it to scenes starting
   "Village" — the active scene here is `MainCastle_Hall`, so **it never runs**. The landing `z=-12` sits INSIDE
   the overlap footprint → the seam dumps the player directly onto the flicker.
- **They do NOT share one root** — two bugs the player hits back-to-back; the warp just lands them ON the z-fight.

## The fix (owner design: "seam to the other side, far enough to naturally transition")
**Keep the auto-cross** (owner intent, 2026-06-17) but land it RIGHT and clear the flicker:

1. **Land FAR, on the other side — natural transition.** Move `CastleHubBuilder.cs:1059`
   `targetPosition` from `(0, 0.5, -12)` to a **clear OuterWorld spot well OUTSIDE the castle footprint**
   (XZ beyond ±16 with margin — e.g. a vetted open area further out / "the other side"), so the player arrives
   in open world, OFF the coplanar overlap, having *travelled out*. Pick a spot on valid NavMesh, clear of mine
   nodes/rocks/camps (camps are at ±95).
2. **Fire at the gate MOUTH, not on approach.** Tighten the seam radius `CastleHubBuilder.cs:1067` from **18 → ~5m**
   so the cross triggers when the hero actually walks THROUGH the gate, not when approaching. (Keeps auto-cross,
   removes the "yanked early" feel.)
3. **Kill the z-fight so it doesn't lurk behind you** — pick the smaller-blast option:
   - **(preferred, no rebuild)** extend `GroundZFightFixer`: broaden `InVillageScene()` (`:132-136`) to also
     fire for the hub (`MainCastle_Hall`/`Castle*`), and broaden the ground finder to catch the castle floor
     (`qFloorWood`/`CourtyardFloor_Nav`), lowering it to Y=-0.5. OR
   - apply the flagged **castle-unload/deactivate on cross** (`SceneTransitionTrigger.cs:395-404`, "FIX B") so the
     castle floor stops rendering once in OuterWorld (also a perf win), OR
   - (rebuild) seat the castle floor `CastleHubBuilder.cs:213` Y `0.01 → -0.5` (terrain wins depth test).

## Acceptance
- [ ] Walking THROUGH the south gate (not merely approaching) crosses to OuterWorld.
- [ ] The player lands in OPEN OuterWorld, far from the hub footprint — reads as a natural transition, not a warp-onto-a-seam.
- [ ] No flickering/z-fighting ground at the landing point (or anywhere the player can see the castle/terrain overlap).
- [ ] Hero on valid NavMesh at the landing; no fall-through / no stuck.
- [ ] §12: the seam emits a `[Flow:Seam]` step on cross (request → load → reposition) so this is a trace-read next time, not an RCA.
- [ ] Compile gate green; brace + NUL guards; hub rebuilt via the builder (no hand-edited `.unity`).

## What NOT to touch / notes
- Keep `requireConfirm=false` (auto-cross is the owner's 2026-06-17 intent) — fix the LANDING + RADIUS, not the confirm.
- §3: never hand-edit `Village.unity`/scenes — rebuild the hub via `Defenders > … Build`. Bake only with the editor closed.
- §0: CLI edits `.cs` on the Windows path; UI does not touch code.

*Cross-ref:* agent RCA 2026-06-17, `CastleHubBuilder.cs` (seam wiring), `SceneTransitionTrigger.cs` (cross/FIX B),
`GroundZFightFixer.cs` (the gated fix), `OuterWorld.unity`, `RaidOutpostSystem`/`CampSystem` (the real outpost/camp anchors).
