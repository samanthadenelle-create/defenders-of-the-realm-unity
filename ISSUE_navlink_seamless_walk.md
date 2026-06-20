# ISSUE: Player cannot walk seamlessly across additively-loaded scenes (NavMeshLink seam fails)

**Status:** FIX IMPLEMENTED (2026-06-20) — pending bake + owner playtest verification. Root cause in §0; implemented fix in §0.5.
**Date:** 2026-06-20

---

## 0.5 IMPLEMENTED FIX (2026-06-20) — keeps direct WASD control, no warp

Chosen approach: **(A) manual off-mesh-link traversal for the input-driven player** + **fix the OuterWorld navmesh coverage to the cave.** We did NOT switch to `SetDestination` (that would convert direct WASD/stick control into point-and-click, changing the whole control feel).

**Fix #1 — manual NavMeshLink traversal in `HeroLocomotion`** (`Assets/_Modules/Village/Hero/HeroLocomotion.cs`):
- New `TryTraverseSeamLink()` called each frame from the movement loop, BEFORE the normal `_agent.Move(step)`. While a crossing is active it OWNS movement (the normal Move is suppressed via a `seamConsumed` flag).
- Detection: when the hero is within 2.5m of one seam endpoint AND the input `Velocity` points toward the OTHER endpoint, begin a crossing. Endpoints are hard-coded to match the builder's link: `SeamCastleEnd = (-4.37,0,-63)`, `SeamOuterWorldEnd = (-4.37,0,-76)`.
- Traversal: slide `transform.position` toward the far endpoint at the hero's `_moveSpeed` (a continuous in-world WALK — no fade, no scene cut). On arrival, `_agent.Warp(target)` re-binds the agent to the far navmesh, then normal control resumes (with a 1s cooldown so it doesn't bounce back).
- Critical detail: `_isTeleporting = true` is set during the slide so the existing off-mesh "±50 playable clamp" (which would yank the hero back, since z=-76 exceeds ±50) is skipped while crossing.
- Bidirectional (works castle→OuterWorld and OuterWorld→castle).

**Fix #2 — OuterWorld navmesh coverage to the cave** (`Assets/Editor/ExteriorTerrainBuilder.cs`):
- Widened the flat "cave path corridor": `CavePathHalfWidth 6→10`, `CavePathFlattenHalf 10→20`, `CavePathFlattenFalloff 8→14` — a wider, gentler-sloped flat strip so the bake produces a solid agent-width walkable navmesh the whole way to the cave (addresses the `SEAM-OFF-MESH` at (0,1,-684)). Requires re-bake (`ExteriorTerrainBuilder.BuildExterior` → `OuterWorldNavBake.Bake`).

**Verification plan:**
- Fix #2 (cave on-mesh) is fleet-verifiable: after re-bake, the `SEAM-REACHABLE`/`SEAM-OFF-MESH` oracle should stop flagging the cave trigger.
- Fix #1 (the WALK across the seam) is NOT fleet-verifiable — the autopilot bot "exits" by warping to the south-most trigger, it does not drive WASD across the link. **Only an owner playtest can confirm the manual traversal feels right** (walk south, slide across the seam into OuterWorld, continue to the cave).

**If fix #2's corridor-widen does not clear the SEAM-OFF-MESH:** deeper options — dump the actual baked navmesh Y at z=-684 (the trigger at y=1 may be >2m above a low navmesh), check `OuterWorldNavBake` agent slope/step limits vs the corridor falloff slope, verify the bake bounds cover z=-700, or add a `NavMeshModifierVolume` (Walkable) around the cave.

---

---

## 0. CONFIRMED ROOT CAUSE (code-verified + cross-checked against Unity docs)

**The player character cannot traverse the NavMeshLink because it is moved by DIRECT INPUT, not by pathfinding.**

`HeroLocomotion` drives the agent like this (`Assets/_Modules/Village/Hero/HeroLocomotion.cs:618-627`):
```csharp
Vector3 step = Velocity * Time.deltaTime;          // Velocity comes from WASD/stick input
if (_agent != null && _agent.isOnNavMesh)
    _agent.Move(step);                              // direct kinematic move
else
    transform.position += step;
```
It **never** calls `SetDestination` / `CalculatePath`, and **never** sets `autoTraverseOffMeshLink`. A `NavMeshAgent.Move()`-driven (kinematic, input-steered) agent is clamped to the navmesh surface it currently stands on. **`NavMeshLink`s / off-mesh links are only auto-crossed by an agent that is FOLLOWING A COMPUTED PATH.** Therefore the player walks to the south edge of the castle navmesh (~z=-63), the link is sitting right there, and the hero simply **stops at the edge** — it never steps onto the OuterWorld surface. The cave at z=-700 is unreachable because step one (leaving the castle navmesh) is impossible for this movement model.

**Everything else checked out (ruled out with evidence):**
- **Agent Type IDs all MATCH** — the project has exactly ONE agent type (`ProjectSettings/NavMeshAreas.asset`: `agentTypeID: 0`, Humanoid). Castle surface (`MainCastle_Hall.unity:2985`), OuterWorld surface (`OuterWorld.unity:2172`), the seam link (`MainCastle_Hall.unity:914`), and the hero agent are all `agentTypeID: 0`. (Agent-type mismatch — the usual #1 culprit — is NOT the bug here.)
- **The NavMeshLink is correctly configured**: start `(-4.37,0,-63)`, end `(-4.37,0,-76)` (world space, host at origin), width 10, bidirectional, area 0 (Walkable), and `BuildBridgeNavLink` DOES call `link.UpdateLink()`. The link binds fine; nothing drives an agent across it.
- **Both `NavMeshSurface`s are enabled, have their NavMeshData assigned, and contribute at runtime.**

**Two compounding problems found alongside the root cause:**
1. `BuildSeamlessOuterWorldSeam` (`CastleHubBuilder.cs:~2254-2264`) **deleted the masked-warp `SceneTransitionTrigger`** to "make it a walk." So the previously-WORKING warp crossing is gone — combined with the un-traversable link, there is now **no working mechanism to leave the castle at all.**
2. The cave trigger at (0,1,-684) is **off-mesh** (`SEAM-OFF-MESH` evidence in §5a) — a SEPARATE OuterWorld navmesh-coverage problem (descending south biome / the corridor "flatten" not producing walkable navmesh that far south). Even a perfect seam wouldn't let the player reach the cave until this is fixed too.

### The decision this forces
A `NavMeshLink` **cannot** provide a seamless walk for an INPUT-DRIVEN player. Options:
- **(A) Manual off-mesh-link traversal (keeps the seamless walk, NO warp):** in `HeroLocomotion`, detect when the input-driven agent reaches a NavMeshLink edge and smoothly carry the agent ACROSS the gap onto the far surface *in-world* (continuous movement / `agent.Warp` across the ~13m span, no scene fade). This is the known pattern for player-controlled characters crossing off-mesh links. Also requires fixing the OuterWorld navmesh so the cave is on-mesh.
- **(B) One continuous `NavMeshSurface` spanning both scenes (no gap, no link):** then `Move()` rides straight across — but a single surface across two additively-streamed scenes is heavier and partly defeats streaming.
- **(C) Restore the masked-warp `SceneTransitionTrigger`:** the mechanism that worked — but the owner has explicitly REJECTED a warp.

Owner has rejected (C). The path forward is **(A)** (manual link traversal for the player) or **(B)** (continuous surface), plus fixing the OuterWorld navmesh coverage to the cave.

### Cited sources (Unity docs + maintainer threads)
- NavMeshLink requires agent/surface/link to share one agent type; endpoints must land on a surface: <https://github.com/Unity-Technologies/NavMeshComponents/blob/master/Documentation/ConnectingSurfaces.md>
- NavMeshLink source (binds at `AddLink`/`OnEnable`; `UpdateLink` re-adds): <https://github.com/Unity-Technologies/NavMeshComponents/blob/master/Assets/NavMeshComponents/Scripts/NavMeshLink.cs>
- Additive loading — "NavMeshes in different scenes are not connected by default … use an Off-Mesh link": <https://docs.unity3d.com/560/Documentation/Manual/nav-AdditiveLoading.html>
- `agentTypeID` (Humanoid=0, custom types are hashes): <https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent-agentTypeID.html>
- `autoTraverseOffMeshLink` / `CompleteOffMeshLink` (agent pauses on link if false): <https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.autoTraverseOffMeshLink.html>
- Runtime link won't connect (UpdateLink / tile size): <https://discussions.unity.com/threads/navmesh-link-does-not-connect-properly-in-runtime/666121>

---
**Severity:** CRITICAL (blocks the core castle → world → outpost loop)
**Owner constraint:** the crossing MUST be a seamless WALK. A masked WARP is explicitly rejected.

> This doc is written to be **self-contained** so it can be handed to external AI tools / engineers
> with no other context. It states the goal, the architecture, the exact geometry, every piece of
> evidence collected, the ranked hypotheses, and the open questions.

---

## 1. Environment
- **Unity 6000.4.8f1** (Unity 6).
- Navigation via the **AI Navigation package** (`Unity.AI.Navigation`) — `NavMeshSurface` + `NavMeshLink` components (NOT the legacy built-in `Navigation` window bake).
- Render pipeline: URP.
- Build target tested: headless Windows player (`-nographics`) for an autopilot fleet, AND the Unity editor Play mode.
- The player character (`HeroLocomotion`) drives a `NavMeshAgent` (confirmed: despite a "pure transform" comment in the header, it is a NavMeshAgent).

## 2. Goal / intended design
A seamless traversal:
```
MainCastle_Hall (home hub, active scene)
   --- player WALKS south, across a seam, no warp ---> OuterWorld (additive, large outdoor area)
   --- player WALKS ~600m south along a path ---> a CAVE/portal at the far end
   --- player CLICKS the cave portal (confirm-to-cross) ---> Village2 (enemy outpost, single scene load)
```
The castle must remain visible to the north as the player walks south (so the castle scene stays loaded).

## 3. Architecture (and the history that led here)
**History:** originally the town/castle and OuterWorld were **STACKED at the same world origin** (both centered on 0,0,0), loaded additively. This produced two overlapping `NavMeshSurface`s over the same XZ region (a "DUAL-NAVMESH" smell — agents could path onto the wrong surface). The crossing was a **warp** (SceneTransitionTrigger fades + repositions the hero) because you cannot bake one continuous navmesh across two same-origin stacked scenes.

**Current approach (the change under investigation):** **UN-STACK** the scenes so they sit **side by side** in world space, then bridge them with a **NavMeshLink** so the player can WALK across (no warp), and so the two navmeshes no longer overlap.

### Scene layout after un-stack
- **MainCastle_Hall**: at world origin. Its own baked `NavMeshSurface` (file: `Assets/Scenes/MainCastle_Hall/NavMesh-NavMeshSurface.asset`). The castle floor/courtyard navmesh extends to roughly **z = -65 to -68** (south gate area). Castle floor is at **Y = 0** (has its own MeshCollider ground, `CourtyardFloor_Nav`).
- **OuterWorld**: loaded ADDITIVELY over MainCastle_Hall (it is NEVER the active scene). A Unity **Terrain**, 1000×1000m, **shifted south** so its NORTH edge is at **z = -72** and it spans to **z = -1072** (terrain center z = **-572**). Its own baked `NavMeshSurface` (`Assets/Scenes/OuterWorld/NavMesh-OuterWorld.asset`). Terrain base Y is dropped by `TerrainBaseDepth = 0.5`; a flat "path corridor" was supposed to hold a walkable strip at **Y ≈ 0** from z=-76 down to z=-700.
- **The seam gap**: castle navmesh ends ~z=-68; OuterWorld navmesh starts ~z=-72. A ~4–8m gap between them by design (so the navmeshes don't overlap).
- **The NavMeshLink** (built in the castle scene): `Unity.AI.Navigation.NavMeshLink`, start `(-4.37, 0, -63)` (on castle navmesh), end `(-4.37, 0, -76)` (on OuterWorld navmesh), width 10, intended bidirectional. Meant to let the agent path across the gap.
- **The cave portal**: a `CavePortal` GameObject at `(0, 0, -700)` with a child `CavePortal_Trigger` at `(0, 1, -684)` carrying a `SceneTransitionTrigger` (targetScene = "Village2", confirm-to-cross click).

```
   NORTH (+Z)
   ┌─────────────────────────┐
   │   MainCastle_Hall       │  Y=0 floor, own navmesh to ~z=-68
   │   (origin)              │
   │        [south gate]     │  z≈-40 gate, navmesh edge ~z=-65/-68
   └─────────[ NavMeshLink ]─┘  link start (-4.37,0,-63)
              ↓  gap ~8m         link end   (-4.37,0,-76)
   ┌─────────────────────────┐
   │   OuterWorld TERRAIN     │  north edge z=-72, own navmesh
   │   (shifted south,        │  flat path corridor z=-76 .. -700 (Y≈0 intended)
   │    center z=-572)        │
   │         │ path south     │
   │         ▼                │
   │   CavePortal z=-700      │  trigger at (0,1,-684)
   │   (→ Village2)           │
   └─────────────────────────┘
   SOUTH (-Z), terrain south edge z=-1072
```

## 4. SYMPTOM (what the owner sees in Play mode)
- **The player cannot reach the cave/portal.** The walk south from the castle fails before reaching the cave.
- Earlier related owner flags during playtest (F8): "everything seems like it's below me and not aligned, I can see the path but it's seamed and its y isn't 0"; "ground is pink" (magenta = missing-shader material on some ground); "couldn't get to cave."
- So the failure is some combination of: (a) cannot cross the castle→OuterWorld seam, (b) the OuterWorld ground/path is mis-aligned in Y / not walkable where expected, (c) the cave area has no navmesh.

## 5. EVIDENCE collected (hard data)
### 5a. The cave trigger is OFF the navmesh (runtime, from the autopilot fleet's SEAM-REACHABLE oracle)
```
SEAM-OFF-MESH: 'CavePortal_Trigger' in 'OuterWorld' at (0.00, 1.00, -684.00)
  is not within 2m of any baked navmesh — the hero can never walk up to it; the seam can't fire.
```
→ **There is no baked OuterWorld navmesh within 2m of (0,1,-684).** Despite the cave sitting inside the intended flat path corridor (z=-76..-700), the navmesh does not reach it (or is at a very different Y there). This alone makes the cave unreachable even if the seam crossing worked.

### 5b. The terrain DID shift south (confirmed)
`Assets/Scenes/OuterWorld.unity`: the `ExteriorTerrain` GameObject `m_LocalPosition` is `{x: -500, y: -30, z: -1072}` (SW corner), consistent with a 1000-unit terrain centered at z=-572. So the un-stack geometry is applied.

### 5c. The south biome of the terrain DESCENDS
The terrain's `SouthHeight` biome function makes the south region drop (down to ~-14m in older notes). The "flat path corridor" was added to hold a walkable Y≈0 strip from z=-76 to z=-700, but the cave-off-mesh evidence (5a) suggests the corridor flatten and/or the navmesh bake is NOT actually producing walkable navmesh at the far-south cave location.

### 5d. NavMeshLink — built, but traversal unproven
The fleet's autopilot does NOT walk the seam (its "exit" phase warps directly to the south-most SceneTransitionTrigger), so it has never actually exercised the NavMeshLink as a WALK. The link's endpoints were reported "not dangling" in an earlier census, but whether a NavMeshAgent will TRAVERSE it has not been verified.

## 6. RANKED HYPOTHESES (what we are testing)
1. **HeroLocomotion moves the agent by INPUT (direct `NavMeshAgent.Move`/velocity), not by pathfinding (`SetDestination`).** NavMeshLinks are only auto-traversed by an agent that is FOLLOWING A COMPUTED PATH. A player steered by WASD/stick via `agent.Move()` is NOT pathfinding, so the agent will reach the edge of the castle navmesh and STOP — it will never cross the NavMeshLink, because direct-input movement doesn't consult links. **If true, a NavMeshLink fundamentally cannot deliver a player-driven seamless walk; we'd need either a single continuous navmesh across the seam, or manual off-mesh-link traversal handling for the player agent.** (Strongest hypothesis — needs confirmation of how HeroLocomotion drives the agent.)
2. **Agent Type ID mismatch.** The castle `NavMeshSurface`, the OuterWorld `NavMeshSurface`, the `NavMeshLink`, and the hero `NavMeshAgent` may not all share the same `agentTypeID`. A link/agent of agent type X cannot use a surface baked for agent type Y. (Being audited.)
3. **OuterWorld navmesh does not cover the far-south path / cave (5a).** The bake bounds, the descending south biome, or the corridor-flatten not actually flattening means no walkable navmesh at z≈-684..-700. Even with a perfect seam, the player can't walk the last stretch to the cave.
4. **Y-misalignment at the seam and along the path.** The corridor is not actually at Y=0 (owner: "below me, y isn't 0"), so the path drops away / the agent falls off / the trigger floats above the mesh.
5. **NavMeshLink runtime binding.** The link is authored in the castle scene with an endpoint in OuterWorld's space; OuterWorld loads LATER (additively). If the link does not `UpdateLink()`/auto-update after OuterWorld's surface arrives, it may not bind.
6. **OuterWorld NavMeshSurface not contributing at runtime** (must be enabled + AddData on load).

## 7. What we are doing RIGHT NOW (the "team")
- **Research agent** (online): researching the canonical Unity pattern for letting a PLAYER walk seamlessly between two additively-loaded scenes with separate `NavMeshSurface`s — specifically whether a `NavMeshLink` can be traversed by an input-driven (non-pathfinding) player agent, agentTypeID matching rules, additive-surface contribution, and `UpdateLink()` timing.
- **Code-RCA agent** (our codebase): auditing the actual `agentTypeID` of every surface/link/agent, the full NavMeshLink config, the OuterWorld NavMeshSurface runtime contribution, and CRUCIALLY **how `HeroLocomotion` moves the agent (SetDestination pathfinding vs direct Move)** — the deciding factor for hypothesis #1.
- **Orchestrator** (me): pulled the runtime SEAM-OFF-MESH evidence (5a); next, a headless/Play-mode path-trace from the castle hero spawn to the cave to find exactly where `NavMesh.CalculatePath` breaks.

## 8. KEY OPEN QUESTIONS for any AI tool reviewing this
1. **Can a player-controlled `NavMeshAgent` that is moved by direct input (`Move`/velocity, NOT `SetDestination`) traverse a `NavMeshLink`/OffMeshLink at all?** If not, what is the correct pattern for a seamless player WALK across a gap between two separate additively-loaded `NavMeshSurface`s? (Single spanning surface? Manual off-mesh-link traversal for the player? A baked connection?)
2. Is there a supported way to make ONE `NavMeshSurface` span TWO additively-loaded scenes (e.g., a surface in a persistent scene with "collect objects = all scenes" / runtime `BuildNavMesh`), so there's NO gap and NO link needed?
3. For two side-by-side (non-overlapping) baked surfaces, what makes a `NavMeshLink` reliably connect them at runtime (agentTypeID, UpdateLink, autoUpdate, area mask), and how do you verify the connection in code?
4. Why would a baked OuterWorld `NavMeshSurface` not produce walkable navmesh at the far end of a Terrain (z≈-700) where a "flatten the corridor" heightmap modification was applied — bake bounds, Terrain height, agent slope/step limits, or the flatten not taking effect?

## 9. Relevant files
- Terrain + un-stack + path corridor: `Assets/Editor/ExteriorTerrainBuilder.cs` (`TerrainCenterZ = -572`, `CavePathStartZ = -76`, `CavePathEndZ = -700`, corridor flatten).
- OuterWorld navmesh bake: `Assets/Editor/OuterWorldNavBake.cs`.
- Castle seam + NavMeshLink: `Assets/Editor/CastleHubBuilder.cs` (`BuildSeamlessOuterWorldSeam`, `BuildBridgeNavLink`).
- Cave portal: `Assets/Editor/OuterWorldCavePortalBuilder.cs` (cave (0,0,-700), trigger (0,1,-684)).
- Boundary: `Assets/_Modules/Village/World/OuterWorldBoundaryInjector.cs`.
- Additive loader: `Assets/_Modules/Village/World/WorldSceneLoader.cs` (loads OuterWorld additively when in a hub scene).
- Hero movement: `HeroLocomotion` (search `Assets/_Modules/.../HeroLocomotion*.cs`) — the NavMeshAgent driver (movement model is the crux of hypothesis #1).
- Runtime reachability oracle (produced 5a): `Assets/_Modules/DevTools/AutoPilotProbes.cs` (SEAM-REACHABLE / SEAM-OFF-MESH).
- Scenes: `Assets/Scenes/MainCastle_Hall.unity`, `Assets/Scenes/OuterWorld.unity`.

## 10. Constraints / non-negotiables
- **No warp.** The crossing must be a seamless WALK.
- Builders only — no hand-editing `.unity` scenes (regenerate via the editor builders + batchmode, editor closed).
- Castle stays loaded/visible to the north as the player walks south.
