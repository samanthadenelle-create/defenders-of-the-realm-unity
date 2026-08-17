<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — SUPERSEDED
> **Superseded by:** WO-608 (world merge) + the shipped RuntimeRegionGate. **Git first-add:** 2026-06-22.
> **Evidence:** the WO's stated premise is "scenes baked at the same origin cannot share one navmesh, so a crossing is ALWAYS a masked transition" — CLAUDE.md §7 records the opposite as current: `Main_Castle_Overworld`, MergedWorld ON, ONE navmesh. Its first recipe row is `castle_to_outerworld` (from `MainCastle_Hall` to `OuterWorld`), and `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`. The primitive itself already exists as `RuntimeRegionGate` + `Assets/Resources/Data/region-gates.json` (cited as working in WO-509).
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 467 — RegionGate: one recipe-driven crossing primitive for all region boundaries

**Status:** SUPERSEDED by WO-608 (world merge) + the shipped RuntimeRegionGate (era sweep 2026-08-17)
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

---

# Runtime auto-seam (2026-06-23) — QUEUED BEHIND THE KNIGHT

**Owner ask (2026-06-23, felt-test flag):** "world seam issue — can we apply the same scripting
logic from Grok to auto-seam from script at RUNTIME?" **Yes.** This section supersedes the
editor-bake delivery above for the world seam: instead of an editor `BuildFromRecipe` that bakes a
crossing into `MainCastle_Hall.unity` at author time, the SAME RegionGate primitive is constructed
**from a recipe at RUNTIME, on scene/seam load** — no editor bake, no scene hand-edit. The recipe
schema, the design law (tunnel default, occluder, no cross-scene link), and the SEAM-REACHABLE
oracle from above all carry over unchanged. The only new piece is the **runtime recipe → build
path** — it mirrors Grok's runtime-dungeon idea, but built on OUR proven RegionGate machinery, not
a greenfield generator.

**Status: QUEUED BEHIND THE KNIGHT** (single-Knight north-star is the active V1 slice; fold this in
after the Knight is felt-verified — memory `combat-pivot-single-hero-northstar`).
**Lane:** World/Environment (architect lane). Touches a NEW runtime component + the recipe asset;
does NOT touch combat/AI silos — parallel-safe per §9.

## Why runtime, not bake
The editor-bake `RegionGateBuilder` (above) writes the deck + trigger into the `.unity` scene and
bakes the navmesh offline. That works, but: (1) it re-touches a hand-dialed scene file every time the
geometry moves (the −572 → origin re-center, WO-483, just invalidated every baked coord); (2) it
can't react to runtime state (progression-scaled dungeons, GUID-keyed pairings, additive-load
timing); (3) a bake is a serial editor-closed bottleneck. **A runtime builder reads the recipe and
assembles the crossing in `Awake`/`AfterSceneLoad` from primitives + a `NavMeshSurface` runtime
re-bake (or `NavMeshLink` weld) — so a coord change is a data edit, never a re-bake.** This is the
direct analogue of Grok's runtime dungeon construction, kept on the RegionGate spine.

## The runtime builder — `RuntimeRegionGate.cs` (NEW, `DeNelle.Village`, runtime asmdef)
A runtime component (NOT editor) that, on load, constructs ONE crossing from a `region-gates.json`
row. Self-bootstrapping like `WorldSceneLoader` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` +
`sceneLoaded` subscription, with the same domain-reload-off guard reset) so no scene wiring is
needed. For each recipe row whose `from` == the active hub scene, it builds the five parts below.
Instrumented per §12 (`[Flow:RuntimeSeam] Step/Warn/Fail`; `Guard.Try` around every risky op).
Idempotent — destroy a prior `__RuntimeSeam_<id>` subtree first; safe no-op off a hub scene.

### The five parts the runtime recipe → build path constructs
1. **Walkable approach deck welded to the source navmesh.** Lift the body of
   `CastleHubBuilder.CreateInvisibleFloor` (Plane + MeshCollider, renderer disabled,
   `NavMeshModifier overrideArea=Walkable` so the gate arch can't carve it) into a runtime helper.
   The deck spans `from-gate-Z → threshold-Z` with the proven **+6 m overlap into the courtyard** so
   it FUSES with the source navmesh — then a runtime `NavMeshSurface.BuildNavMesh()` (or `UpdateNavMesh`)
   re-bakes JUST the additive source surface so the deck is on-mesh without an editor bake. (The
   2026-06-19 lesson: a deck that floats only at the far Z leaves a void to the gate and reads
   off-mesh — keep the continuous gate→threshold strip + overlap.)
2. **`SceneTransitionTrigger` seated at the threshold** (deck level, NOT floating). REUSE the
   component verbatim — set ONLY `transform.position`, `targetSceneName`, `targetPosition`,
   `loadAdditive`, `ProximityRadius` (per §"do not change its field wiring"). Confirm-to-cross is
   already unconditional; the wide `ConfirmMinRadius` already reaches the hero at the navmesh edge.
3. **Narrow `NavMeshLink`(s) across the two additive navmeshes** so AI (reps, troops) can PATH the
   crossing once BOTH scenes are loaded additively — this is the runtime-only capability the
   editor bake could never have (the far endpoint exists at runtime). REUSE `BuildBridgeNavLink`'s
   body (direct `Unity.AI.Navigation.NavMeshLink`, `bidirectional`, width = arch width). Build the
   link ONLY after `WorldSceneLoader` confirms OuterWorld is loaded (subscribe to its `sceneLoaded`)
   so the end endpoint lands on a live navmesh — NOT the dead-end dangling link the editor bake hit.
   *(This is the ONE place the §"NEVER a cross-scene NavMeshLink" law relaxes: it was impossible at
   BAKE time because the far scene wasn't loaded; at RUNTIME both are additive, so the link is valid.
   The masked-warp `SceneTransitionTrigger` remains the HERO path; the link is the AI path.)*
4. **`HeroLinkCrossing` paired-warp for the input-driven hero**, GUID-keyed per WO-479. Place TWO
   `HeroLinkCrossing` markers sharing one `crossingId` (= the gate GUID): entry on the source deck,
   destination at the OuterWorld landing. REUSE the component verbatim (it already does id-paired
   distance-independent warp via `Partner()`). This is the deliberate spawn-pair the owner asked for
   (memory `region-gate-crossing-primitive`), keyed by the stable GUID backbone (WO-479 §GUID).
5. **Gate-funnel blocker panels (WO-479 §gate-refinement).** Two thin vertical panels
   (BoxCollider + `NavMeshObstacle` carve, invisible) at the inner edges of the arch, auto-fit from
   the arch width, so navmesh + physics route ONLY through the opening — kills "slip around the
   gate side" and makes the gate a real threshold/choke. Auto-placed from the recipe arch bounds,
   never hand-dialed.

## New geometry coords — origin-centered OuterWorld (replaces the stale −572 coords)
**The old seam coords are DEAD.** The donor `AddCastleBridgeSeam` warped to `(gateX, 0.5, -66)` and
the legacy gate wiring to `(0, 0.5, -12)` — both authored against the OLD `ExteriorTerrainBuilder`
terrain centered at **Z = −572** (1000×1000). WO-483 re-centers that terrain to **origin (0,0,0),
shrunk to ~460u**, with a single `DeNelle.Core.World.WorldGeometry` constant as the shared truth
(`ZoneManager`/`OuterWorldBuilder`/`ExteriorTerrainBuilder`/`CastleHubBuilder` all read it). The
runtime seam MUST read its coords from `WorldGeometry`, never hardcode them. Derive from where the
hero EXITS the castle now:

| Coord | Source of truth (read at runtime) | Notes |
|---|---|---|
| `gateX` (lane centre) | `CastleHubBuilder.ReadSouthGatePos().x` (≈ −4.37, the recipe Gate_South) | unchanged by re-center (castle is local) |
| `gateZ` (deck weld start) | `ReadSouthGatePos().z` (≈ −40.6) | castle-local, unchanged |
| `thresholdZ` (trigger + entry marker) | `gateZ − ~22` (the deck far end, ≈ −63 deck-local) | on the castle deck, on-mesh |
| **`targetPosition` (OuterWorld landing)** | **`WorldGeometry.SouthGateSeamLanding`** (origin-centered) | **REPLACES `(gateX,0.5,−66)` / `(0,0.5,−12)`** — must land inside the new ~460u origin terrain, just inside its south edge (≈ `(0, 0.5, −40…−60)` band, final value owned by WorldGeometry, NOT this WO) |
| arch width (funnel + link width) | recipe `approach.width` (≈ 7) | drives panels 5 + link 3 |

> **Hard rule:** do NOT mint a literal landing Z here. The single source is `WorldGeometry`
> (WO-483). If WO-483 hasn't landed when this is built, BLOCK on it — a hardcoded landing re-creates
> the exact −572 desync this is fixing.

## Reachability validation (SEAM-REACHABLE oracle)
Same repeatable test as the editor variant — no bespoke verification:
- **On build (runtime):** after the deck weld + runtime re-bake, assert `PATH-COMPLETE` from a
  source-courtyard point to the threshold trigger ON the source navmesh (tight ≤1.0 m end
  tolerance so a snap onto a stacked far-scene navmesh can't false-green — the 2026-06-19 lesson).
  Emit `RUNTIME_SEAM_NAV_OK` / `..._FAIL` via FlowTrace.
- **On AI link (runtime):** once OuterWorld is additive, assert a cross-link `PATH-COMPLETE` from
  the source deck to the OuterWorld landing through the `NavMeshLink` (this is the runtime-only
  check the bake couldn't run).
- **Fleet:** `AutoPilotProbes` PROBE 5 (**SEAM-REACHABLE**) regression-guards it on a headless run —
  the same oracle that guards every other gate. This is also the fix for the WO-453
  `AttemptExitCastle` fleet timeout (WO-483 notes it as "THIS unfixed seam").

## Reuse vs. new
| Piece | Reuse (verbatim / lift body) | NEW |
|---|---|---|
| Walkable approach deck | `CastleHubBuilder.CreateInvisibleFloor` + `AddWalkableNavMeshModifier` (lift to runtime helper) | runtime `NavMeshSurface.BuildNavMesh()` re-bake (no editor bake) |
| Threshold crossing | `SceneTransitionTrigger` (verbatim — set transform + 4 fields only) | — |
| AI cross-path | `BuildBridgeNavLink` body (direct `NavMeshLink`) | runtime build gated on OuterWorld-additive-loaded |
| Hero paired warp | `HeroLinkCrossing` (verbatim, GUID `crossingId`) | place the pair from the recipe |
| Gate funnel panels | WO-479 §gate-refinement design | auto-fit panel builder from arch bounds |
| Recipe schema + tunnel/occluder law | `region-gates.json` + design law (this WO, above) | runtime loader of the same JSON |
| Additive load timing | `WorldSceneLoader` (subscribe to its `sceneLoaded`) | — |
| Coords | `WorldGeometry` (WO-483) + `ReadSouthGatePos` | — (NEVER hardcode) |
| Reachability | `AutoPilotProbes` PROBE 5 SEAM-REACHABLE | runtime `PATH-COMPLETE` asserts (build-time + cross-link) |
| Instrumentation | `FlowTrace` / `Guard` (§12) | `[Flow:RuntimeSeam]` tag |

## Files (runtime variant)
- NEW `Assets/_Modules/Village/World/RuntimeRegionGate.cs` (`DeNelle.Village`, runtime asmdef —
  references `Unity.AI.Navigation`, like `EnemyStrongholdBuilder` proves compiles).
- REUSE `region-gates.json` (the same recipe asset as the editor variant).
- REUSE (no edits): `SceneTransitionTrigger`, `HeroLinkCrossing`, `WorldSceneLoader`,
  `AutoPilotProbes` PROBE 5.
- DEPENDS ON: `DeNelle.Core.World.WorldGeometry` (WO-483) for the origin-centered landing coord.
- Donor bodies to lift: `CastleHubBuilder.CreateInvisibleFloor`, `AddWalkableNavMeshModifier`,
  `BuildBridgeNavLink`.

## What NOT to touch (runtime variant)
- Do NOT hardcode the OuterWorld landing Z — read `WorldGeometry`; block on WO-483 if absent.
- Do NOT change `SceneTransitionTrigger`'s field wiring (transform + the 4 public fields only).
- Do NOT build the cross-scene `NavMeshLink` before OuterWorld is additive-loaded (dangling endpoint).
- Do NOT hand-edit any `.unity` scene — this is a pure runtime build, NO bake.
- Do NOT start until the single-Knight V1 slice is felt-verified (QUEUED BEHIND THE KNIGHT).
