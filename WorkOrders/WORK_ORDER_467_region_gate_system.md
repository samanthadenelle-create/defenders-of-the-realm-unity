# WORK ORDER 467 — RegionGate: one recipe-driven crossing primitive for all region boundaries

**Status: READY TO IMPLEMENT**
**Lane:** World/Environment (architect lane — touches a builder, not gameplay)
**Owner-endorsed:** 2026-06-19 ("wow thats smart … makes the testing a simple repeatable process")
**Numbering:** 467 (filesystem max was 466; reconcile against `MASTER_PIPELINES_BACKLOG` on next groom).

---

## Why
The 2026-06-19 castle→OuterWorld bridge fix proved a single reusable crossing primitive for any
**stacked-region boundary** (scenes baked at the same origin cannot share one navmesh, so a crossing
is ALWAYS a masked transition, never a continuous cross-scene walk):

> **walkable approach (welds to the source navmesh) → threshold `SceneTransitionTrigger` → masked
> transition across the scene cut.**

Bridge, tunnel/cave/pass, and "small dungeon that pops in" are the SAME primitive — only two params
differ: the **visual gate** and the **load mode**. Today that primitive lives hand-coded inside
`CastleHubBuilder.AddCastleBridgeSeam`. Generalize it so **every future connector is ONE recipe row**,
not bespoke editor code — and so the SEAM-REACHABLE fleet oracle (already shipped, AutoPilotProbes
PROBE 5) regression-guards each one identically. That is the "expansions = create a scene + add a row,
and testing is the same repeatable process every time" payoff.

See memory `region-gate-crossing-primitive` + `world-architecture-gated-regions-playable-connectors`.

## The recipe schema (data-driven, live-content-ready)
New `region-gates.json` (StreamingAssets + Resources dual copy, like `garrison-recipes.json`):
```jsonc
{
  "id": "castle_to_outerworld",
  "type": "tunnel",            // tunnel | pass | bridge  (TUNNEL = default; see below)
  "loadMode": "warp",          // warp (hard fade) | stream (additive, no black-fade)
  "from": "MainCastle_Hall",
  "to": "OuterWorld",
  "approach": { "width": 7, "fromZ": -40.6, "toZ": -63.0, "overlap": 6 },  // continuous walkable deck/path
  "trigger": { "atZ": -63.0, "proximityRadius": 6, "warpTo": [ -4.37, 0.5, -66 ] },
  "gatePrefab": "<tunnel-mouth | bridge | pass prefab path>",
  "occluder": true             // tunnel/pass: place a sightline occluder over the far side
}
```

## Design law baked into the builder
- **TUNNEL is the default gate, not the bridge.** A bridge leaves a sightline across the gap into the
  stacked far-scene (exposes the same-origin illusion). A tunnel/cave/pass OCCLUDES the far side — the
  fade fires unseen, the player emerges in the next region. The occlusion IS the mask. `bridge` is the
  special case for a deliberately-visible chasm/water. When `type != bridge` and `occluder: true`, the
  builder MUST place a far-side sightline occluder.
- **NEVER a cross-scene NavMeshLink.** Proven impossible (same-origin stacked bake; the far endpoint
  dangles). The approach is a continuous walkable strip on the SOURCE side that welds to the source
  navmesh; the boundary is the masked transition. Do not reintroduce the dangling-link approach.
- **`stream` load mode** = the dungeon-pop variant: instead of `SceneTransitionTrigger.WarpTo`, additive-
  load the destination via the existing `DungeonEntrance` / `DungeonPortal` path (no black-fade, snappier).

## Builder — `RegionGateBuilder.cs` (Editor, DeNelle.Editor)
- Editor-only static builder: menu `Defenders/World/Build Region Gate (recipe)` + batchmode method
  `DeNelle.Editor.RegionGateBuilder.BuildFromRecipe` (`-executeMethod`, editor CLOSED).
- **Lift the proven body of `CastleHubBuilder.AddCastleBridgeSeam`** into the parameterized builder:
  continuous walkable approach deck (`CreateInvisibleFloor`, welds to source navmesh) → seat a
  `SceneTransitionTrigger` ON the approach at the threshold (deck level, NOT floating) → bake + persist →
  the loud `CastleGateNavVerify`-style `GATE_NAV_OK` + `PATH-COMPLETE` assertion (target the trigger on
  the deck, NOT a point that snaps onto the stacked far-scene navmesh — that false-greened on 06-19).
- ADD-ONLY into the source scene (never regen a hand-dialed scene). Idempotent (destroy a prior gate
  subtree of the same id first).
- TGVRU-instrumented per WO-430 ([Flow:RegionGate] Step/Warn/Fail; Guard.Try around every risky op).
- `gatePrefab` skip-safe on a missing pack (`Debug.LogWarning`, primitive fallback) — mirror the bridge.

## Migrate the existing crossing
- Re-express the castle→OuterWorld bridge as the FIRST `region-gates.json` row (`type: bridge` to keep
  parity, or `tunnel` if the owner wants to upgrade the mask). Building from the recipe must reproduce
  the committed walkable deck + deck-seated trigger (regression parity).

## Acceptance criteria
1. A connector is authored as ONE `region-gates.json` row; `BuildFromRecipe` produces it with no code edit.
2. Built gate: the approach welds to the source navmesh (`GATE_NAV_OK`), the trigger is deck-seated and
   on-mesh, and the honest `PATH-COMPLETE` (targeting the trigger, tight tolerance) passes — NOT a
   stacked-scene false-green.
3. SEAM-REACHABLE (AutoPilotProbes PROBE 5) reports the new seam REACHABLE on a fleet run — same
   repeatable test for every gate, no bespoke verification.
4. `type: tunnel|pass` with `occluder: true` places a far-side sightline occluder.
5. `loadMode: stream` routes through `DungeonEntrance`/`DungeonPortal` (additive), not `WarpTo`.
6. Compile gate `COMPILE_GATE_OK`; braces balanced; no NUL; no new reflection in the builder.

## What NOT to touch
- Do NOT regen `MainCastle_Hall` (add-only; hand-dialed offsets preserved).
- Do NOT reintroduce a cross-scene NavMeshLink (proven impossible on the same-origin stack).
- Do NOT hand-edit any `.unity` scene — builder + recipe only (CLAUDE.md §3).
- Do NOT change `SceneTransitionTrigger`'s proven field wiring — only its transform + targetPosition.

## Files
- NEW `Assets/Editor/RegionGateBuilder.cs` (DeNelle.Editor) — lifted/parameterized from
  `CastleHubBuilder.AddCastleBridgeSeam`.
- NEW `region-gates.json` (StreamingAssets + Resources dual copy).
- REUSE `SceneTransitionTrigger`, `DungeonEntrance`, `DungeonPortal`, `CastleGateNavVerify`,
  `AutoPilotProbes` PROBE 5 (no edits required to ship v1).
- Reference donor: `Assets/Editor/CastleHubBuilder.cs` `AddCastleBridgeSeam` / `RelocateExitSeamToBridge`
  / `BuildBridgeNavLink` (the last is DROPPED — no link in the generalized builder).
