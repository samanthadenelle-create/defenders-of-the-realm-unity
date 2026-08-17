<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — SUPERSEDED
> **Superseded by:** WO-608 (world merge to one scene). **Git first-add:** 2026-07-03.
> **Evidence:** the WO is a Castle→OuterWorld crossing-wayfinding MVP whose whole problem statement is "new players can't tell they must cross at a seam". `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files` and CLAUDE.md §7 puts the castle and the overworld in ONE scene on ONE navmesh — there is no seam to sign-post.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 593 — Wide Downward Bridge Exit (Castle → OuterWorld wayfinding MVP)

**Status:** SUPERSEDED by WO-608 (world merge to one scene) (era sweep 2026-08-17)
**Date:** 2026-07-01
**Priority:** P1 (new players can't tell they must cross at a seam)
**Owner:** Samantha (PO) · **Author:** CLI (architect synthesis) · **Implements:** CLI
**Lane:** World/Environment (RegionGate + castle builders) — single-agent (serialization bottleneck)
**Slot into:** `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md` (WO-593, world lane)

**⛑ SAFE RESTORE POINT (before any work):** tag `restore/pre-castle-bridge-2026-07-01` +
branch `backup/pre-castle-bridge-2026-07-01`, both at `ca2c7ce5` (clean tree, terrain fixes in).
Restore: `git checkout restore/pre-castle-bridge-2026-07-01`.

---

## 0. What this is (scoped DOWN from the full concept — owner 2026-07-01)
The owner's concept render = raised castle on a plateau, moat, drawbridges, terrain slanting down to
OuterWorld. **The MVP is just the SIMPLE part: a WIDE, DOWNWARD BRIDGE out of the gate**, framed by the
(already-built) moat. The **raised-plateau + full terrain down-slope re-bake is DEFERRED** to ride with the
parked WO-453 un-stack (same terrain/bake surface — doing them together avoids a double bake).

Guidance folded in (all treated AS guidance — CLI is architect): the seam diagnosis doc, Grok's
"wide gates + cliffs" WO, and the owner's confirmations: *"simple part is just a downward wide bridge,"
"for nav we use a plane," "two super-close navlinks," "longer on the connection sides," "higher castle +
water-filled edges hides the shading/sink seam."*

## 1. Goal
A new player looks at the castle and sees an obvious **wide bridge sloping down and out** over the moat —
and walks down it into OuterWorld. Diegetic wayfinding (no HUD clutter, no archway beams). Non-breaking:
the crossing mechanic is unchanged; this improves the **approach geometry + nav** only.

## 2. Architecture (confirmed with the owner — the load-bearing decisions)
- **Bridge = a wide plane sloping downward** out of the gate, over the moat. Nav = an **invisible walkable
  plane** (the RegionGate approach-deck pattern: MeshCollider + `NavMeshModifier overrideArea=Walkable`,
  welded to the courtyard navmesh + runtime `NavMeshSurface.BuildNavMesh()` rebake). Just wider + sloped.
- **Long on the connection sides, short in the span.** The bridge deck **overlaps deep onto BOTH sides**
  (castle courtyard nav + OuterWorld landing) — the "abutment" weld. Reuse/extend the existing ~18 m overlap
  tongue (`RuntimeRegionGate.cs:282,296-308`). Long overlap = solid weld + gentle grade; short middle span.
- **Downward slope ≤ ~45°** (default agent max slope; runtime bake uses `agentTypeID=0`, no override —
  `RuntimeRegionGate.cs:562-575`). Gentle exit ramp bakes **walkable**; anything steeper elsewhere bakes
  **non-walkable = free barrier**. Do NOT add a slope override.
- **Crossing = two super-close navlinks + warp fallback.** A short `NavMeshLink` whose endpoints nearly
  touch lets the NavMeshAgent **walk across** the seam (no visible warp). The downward slope gives the
  OuterWorld side a **lower Y**, separating the two nav surfaces so the link isn't ambiguous at the stacked
  origin. **Keep the existing `HeroLinkCrossing` warp as the safety net** (`HeroLocomotion.cs:~900-922`) —
  if the agent link-traverse ever fails, the warp still crosses. Hero must let the agent auto-traverse the link.
- **Moat = the frame + the seam-hider (owner-confirmed principle).** Raising the castle edge + flooding it
  with water **occludes** the terrain depression/shading seam (you can't see a dip that's underwater). The
  moat also reads as the impassable barrier that makes the bridge the obvious exit. *(Final visual proof =
  a bake; the occlusion principle is sound.)*
- **Data-driven + capturable.** Bridge width/slope/overlap live in the gate recipe; hand-tweaks captured to JSON.

## 3. Workflow (owner-chosen: Grok-reshape → owner hand-tweak → CLI capture)
1. Grok reshapes the builder toward the concept (defaults). 2. Owner hand-tweaks positions/scale/slope in the
editor on clearly-named children. 3. **CLI builds `RuntimeSeamOffsetCaptor`** → writes final offsets to
`seam-offsets.json` → builder replays the tuned layout on next load/build. (Offset Forge capture→data→replay.)

## 4. Scope: 3 builder UPDATES + 1 NEW editor tool (no new castle editor)
1. **UPDATE `Assets/_Modules/Village/World/CastleMoatBuilder.cs`** — already builds moat + 4 drawbridge decks
   at the 4 gates (`:314-348`), gated by `ff.castlemoat` (**default OFF today — flip ON**, header "default ON"
   is STALE, `FeatureFlags.cs:273`). Widen the bridge deck + slope it downward; name children clearly
   (`RuntimeSeam_Bridge_<id>`, etc.). Reuse the 9 m deck as the base.
2. **UPDATE `Assets/_Modules/Village/World/RuntimeRegionGate.cs`** — seat the walkable plane on the bridge;
   deepen the two-sided overlap ("longer on connection sides"); add the short near-touching `NavMeshLink`;
   keep `HeroLinkCrossing` pair + `SceneTransitionTrigger` (suppressPrompt) exactly as-is; parse bridge
   width/slope/overlap from the recipe.
3. **UPDATE `Assets/Editor/ExteriorTerrainBuilder.cs`** — (DEFERRED slice, rides with un-stack) raise the
   castle edge + flood the low ring with water to hide the seam. **Do NOT re-introduce a raw depression**
   (CastleDepressionDepth just went −3→0 to fix the sink bug, `:204-208`); raise + flood, then re-bake.
4. **NEW `Assets/Editor/RuntimeSeamOffsetCaptor.cs`** — editor capture tool → `seam-offsets.json` (+ mirror).
   The only genuinely new editor piece; small.

Data: `Assets/Resources/Data/region-gates.json` (+ StreamingAssets mirror) — add bridge fields
(`bridgeWidth`, `bridgeSlope`, `bridgeOverlap`, `isCanonicalExit`); **NEW** `seam-offsets.json`.

## 5. What NOT to touch (safety — BINDING)
- **Crossing mechanic unchanged:** `HeroLinkCrossing` warp stays as the fallback; `SceneTransitionTrigger`
  `suppressPrompt=true` stays. This WO is bridge geometry + nav weld + navlink only.
- **Do NOT land the WO-453 un-stack here** (no `WorldGeometry.cs`, no OuterWorld origin shift, no stash
  re-apply). The raise+slope terrain re-bake (item 4.3) is DEFERRED to the un-stack; the bridge MVP ships on
  the current warp seam.
- **Do NOT hand-edit `.unity` scenes.** Runtime-built by RegionGate; any navmesh bake goes through the bake
  orchestrator in a separate bake WO (editor CLOSED).
- No `System.Reflection`. Null-conditional (`?.`) on cross-module calls. `.cs` via Write/Edit (Windows path).
  Brace + NUL gate on every touched file.

## 6. Acceptance criteria
1. `ff.castlemoat` ON: moat + drawbridges render at the gates (zero-bake recognizable slice).
2. The canonical gate shows a **wide bridge sloping downward** over the moat; hero walks down it and crosses
   into OuterWorld via the **navlink** (no visible warp), with the `HeroLinkCrossing` warp still succeeding as
   fallback if the link fails.
3. Nav: bridge plane welds deep to BOTH sides; runtime rebake succeeds; `AssertApproachWelded` passes; no
   `RUNTIME_SEAM_NAV_FAIL`; no dual-navmesh regression.
4. Downward ramp ≤45° bakes walkable; moat water is non-walkable (hero can't "walk on water" — carve/verify).
5. Editor: hand-tweak a `RuntimeSeam_*` child → run `RuntimeSeamOffsetCaptor` → `seam-offsets.json` updates →
   next build reproduces the tuned bridge.
6. (Deferred slice) raised edge + flooded moat hides the depression/shading seam — owner felt-verifies.
7. Data-driven: changing `bridgeWidth`/`bridgeSlope` in JSON changes the bridge with no code edit.

## 7. Verify (per §12 — no claim-fixed on faith)
`CompileGate.Run` green (editor CLOSED) → bake if nav needs it via the orchestrator → `run-autopilot-fleet.ps1`
crosses the gate over the bridge (SPAWN_TO_GATE / crossing OK, weld asserts, no dual-navmesh) → **owner
felt-verifies** the walk-down feel + seam-hidden-by-water before push.

## 8. Open questions for the owner
1. One canonical bridged exit (south) or bridges on all four gates? (Default: emphasize south; moat already builds all four.)
2. Bridge look — plain timber vs stone-arch? (Owner hand-tweaks; captor persists.)
3. Ship the bridge MVP now on the warp seam, then do the raise+flood+slope re-bake with the un-stack? (Recommended.)
