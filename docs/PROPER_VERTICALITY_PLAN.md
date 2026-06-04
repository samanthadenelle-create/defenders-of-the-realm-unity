# Proper Hero Verticality Plan — Defenders of the Realm / Echoes of Elarion

**Status:** PROPOSAL (owner review). Read-only investigation; no code/scene/asset changed.
**Author:** Senior Unity architect (agent)
**Branch context:** `feat/tower-core-loop` · Unity 6 · URP · mobile/WebGL
**Scope of the thesis:** DEF-147 (hover exploit), "stairs don't climb", and the rampart LIFT band-aid.

---

## 1. Verdict on the thesis

**Mostly TRUE, with one important correction.**

The hypothesis is that all three problems share ONE root cause — "the hero has no proper verticality/ground system." That is **directionally correct**: the hero locomotion has **no real ground/gravity model**, and that single gap produced all three symptoms. But the precise mechanism is **not** the one both existing work orders (WO-248, WO-261) assume.

Both prior WOs assume the hero is a `CharacterController` (or Rigidbody) that needs gravity added. **That is wrong.** The hero is a **NavMeshAgent-driven kinematic transform with NO CharacterController, NO Rigidbody, and NO gravity at all.** Movement is `NavMeshAgent.Move()` when on-mesh, and a raw `transform.position +=` fallback when off-mesh. This matters enormously because it changes the fix:

- The hover is **not** "gravity isn't applied to a controller." It is "**when the hero leaves the NavMesh, the agent's ground-clamp stops applying and the fallback path has no downward force at all** — so the hero keeps its last Y forever."
- Stairs don't climb because **the climb ramps were deleted** (the NavMesh has no walkable surface connecting ground → deck) — see §2.3. It is a missing-geometry / bake problem, not an agent step-height problem.
- The lift is a genuine band-aid that exists *only* because the ramp was removed, and it works by suspending the agent and hand-carrying the hero (§2.4).

So: **one root cause (no proper ground+traversal system), three surface symptoms** — thesis CONFIRMED. But the fix is **NavMesh/bake-side + an off-mesh ground-snap**, *not* "add CharacterController gravity." Implementing WO-248/WO-261 as written would add a CharacterController the rest of the system doesn't use and would fight the NavMeshAgent.

---

## 2. Current architecture (evidence-backed)

### 2.1 How the hero moves today

`Assets/_Modules/Village/Hero/HeroLocomotion.cs`

The hero is a **NavMeshAgent that the player drives manually** — input does not set a destination; it calls `Move()` directly. Key lines:

- `HeroLocomotion.cs:91-103` — `Awake()` adds/configures a `NavMeshAgent` (radius 0.4, height 1.8, `updateRotation=false`, `autoBraking=false`, no obstacle avoidance, speed 30 "so it never caps us"). The agent exists purely to **clamp the hero to the walkable surface and follow its height**, not to pathfind.
- `HeroLocomotion.cs:219-230` — reads WASD/stick into an XZ `move` vector, smooths a `Velocity` (accel/decel), **Y is always 0 in the move vector**.
- `HeroLocomotion.cs:245-249` — the actual move:
  ```csharp
  Vector3 step = Velocity * Time.deltaTime;
  if (_agent != null && _agent.isOnNavMesh)
      _agent.Move(step);              // on-mesh: agent clamps to surface + follows height
  else
      transform.position += step;     // off-mesh fallback: RAW transform, no ground logic
  ```
- `HeroLocomotion.cs:266-274` — an XZ playable clamp + `if (p.y < 0f) p.y = 0f;` that runs **only when off-mesh**. Note it floors Y at 0 but **never pulls the hero down to the ground** if they're above it.

There is **no CharacterController, no Rigidbody, no `Physics.gravity`, no ground raycast** anywhere in this file. "Stays grounded" is entirely delegated to the NavMeshAgent's surface-follow — which only works while `_agent.isOnNavMesh` is true.

### 2.2 Why hover happens (DEF-147)

When the hero walks off an elevated edge (rampart deck, ramp lip, terrain ledge), the agent goes **off-mesh** (`_agent.isOnNavMesh` becomes false, or the agent is suspended — see the lift, §2.4). Execution drops into the `else` branch at `HeroLocomotion.cs:249` (`transform.position += step`) where:

1. `step` only has XZ components (Y of `Velocity` is always 0).
2. The off-mesh clamp at `:266-274` only **raises** Y to 0 if below; it never lowers Y toward ground.

**Result: the hero retains its elevated Y indefinitely and "floats."** There is no downward force in any code path. This is the hover exploit — and it is a *direct consequence* of having no gravity/ground-snap for the off-mesh state. (Both existing WOs correctly identify "no gravity off the surface" but prescribe a CharacterController fix that doesn't fit the actual NavMeshAgent architecture.)

The deck itself is baked NavigationStatic, so *standing* on the deck the agent holds you; the exploit is specifically the **transition off the baked surface**, where nothing catches the fall.

### 2.3 Why stairs don't climb

`Assets/Editor/VillageSceneBuilder.Fortify.cs` — `BuildRamparts()` (`:173` onward).

The honest root cause: **there are no climb ramps in the current build.** They were deliberately removed:

- `Fortify.cs:386-393`:
  ```
  // ── Rampart access: the runtime LIFT, not a climb ramp ──────────────
  // Owner 2026-06-02: REMOVED the N/S stone climb ramps. The pressure-plate
  // RampartLiftInstaller lift is the access path to the deck now...
  _ = stairPrefab;   // (stair prefab loaded but no longer placed)
  ```
- The deck and inner walk-lane (`:227-295`) ARE built and ARE NavigationStatic, so the *top* surface bakes walkable — but **nothing connects ground (Y≈0) to deck (Y≈5.4)** in the navmesh anymore.

History (why the ramp approach "didn't stick"): the original design used a `Stairs_Medieval_Stone` prefab as the *visual* with a hidden ~31° nav ramp beneath (`Fortify.cs:184-206`, `rampRun = 9f` for a 5.4 m rise ≈ 31°, under the 45° `agentSlope` limit). On paper this is bakeable. In practice it never produced a reliable walkable connection — likely causes (to verify in-editor): the ramp slab's NavigationStatic flag / voxelization didn't connect to both the ground tile and the deck tile across the `agentClimb` (0.4 m) threshold at the seams; or the stepped stair visual's colliders interfered; or the ramp was too narrow after the agent-radius (0.5 m bake radius) erosion. Rather than solve the bake, the team deleted the ramp and shipped the lift.

**NavMesh bake settings** (the legacy `NavMeshBuilder.BuildNavMesh()` at `OuterWorldBuilder.cs:378-379` and `VillageSceneBuilder.NavMesh.cs:69-70` reads these from the scene's `NavMeshSettings`):

- `Assets/Scenes/Village.unity:102-113` — `agentRadius: 0.5`, `agentHeight: 2`, `agentSlope: 45`, `agentClimb: 0.4`, `cellSize: 0.1667`.
- `ProjectSettings/NavMeshAreas.asset:74-88` — Humanoid agent type 0, same values (`agentClimb: 0.75` there, but the *scene* settings win for the bake → 0.4).
- `Assets/Scenes/OuterWorld.unity` — identical (`agentSlope: 45`, `agentClimb: 0.4`).

So a **single shared agent type (radius 0.5, slope 45°, climb 0.4 m)** is baked for both hero and enemies. A 31° ramp is well within the 45° slope limit — meaning **a properly-connected ramp WOULD bake walkable**; the problem was connection/voxelization at the ends, not slope. Note also a **radius mismatch**: the bake uses radius 0.5 but the hero agent is set to 0.4 (`HeroLocomotion.cs:93`). An agent smaller than the bake radius is generally safe (the navmesh is eroded *more* than the agent needs), so this is not the climb blocker, but it is an inconsistency worth normalizing.

### 2.4 How the lift band-aids it

`Assets/_Modules/Village/World/LiftPlatform.cs` + `Assets/_Modules/Village/World/RampartLiftInstaller.cs`

- `RampartLiftInstaller` is a self-bootstrapping DDOL singleton (`:42-57`) that, on Village scene load, raycasts the floor and spawns two stone-slab lifts at the N/S rampart access points (`:70-138`), configured `bottomSurfaceY → DeckTopY (5.4)`.
- `LiftPlatform` is a pressure-plate toggle. The carry mechanism (`LiftPlatform.cs:86-117`):
  ```csharp
  bool travelling = _state == State.Rising || _state == State.Lowering;
  if (heroOn && travelling) {
      SuspendAgent();                       // _heroAgent.enabled = false  (:119-127)
      _hero.position = transform.position;  // hard-lock hero XZ+Y to the slab
  } else {
      if (_agentSuspended) RestoreAgent();  // re-enable + Warp agent back on (:129-136)
      ...keep an idle rider at surface Y...
  }
  ```
  So the ride **disables the NavMeshAgent and hand-carries the hero by setting `transform.position` every frame**, then re-enables and `Warp()`s the agent when it lands. This is a bespoke workaround for "the agent would drag the hero back to the ground navmesh mid-rise because there's no navmesh between ground and deck."

- Crucially, the lift's own comments (`LiftPlatform.cs:96-104`) record that **the hover bug was partly caused by the lift**: leaving the agent suspended on the deck let the transform-fallback move the hero with no ground clamp → "where i can fly." Their mitigation was to re-enable the agent at rest. This confirms the hover and the lift are the *same* underlying defect (off-mesh transform movement with no ground logic).

**What retiring the lift requires:** a baked walkable ground→deck connection (a ramp the agent climbs) so access no longer needs agent suspension. Once that exists, `RampartLiftInstaller` simply stops spawning and `LiftPlatform` is unreferenced.

### 2.5 Interactions / blast radius adjacencies

- **Gates:** the gate openings are deliberately kept WALKABLE in the bake (`VillageSceneBuilder.NavMesh.cs:34-61`) — the perimeter gate arches are *excluded* from NavigationStatic so they don't voxelize into a wall. A prior fix (gate NavMesh exit) hinges on this. Any change to bake inputs must preserve gate-opening walkability.
- **Combat positioning:** enemies share the *same* navmesh and agent type ("so enemies can climb to attack a hero defending up top" — `HeroLocomotion.cs:86-89`). A connected ramp means enemies could also path to the deck — *intended*, but a gameplay change to validate.
- **Camera:** `SmartMobileCamera` follows the hero transform; vertical snapping of the hero Y (a ground-snap) will move the camera. Smooth the snap to avoid camera jitter.
- **Bake chain:** the canonical chain is BuildVillage → BuildOuterWorld → BuildExterior → BakeWorldNavMesh (memory: "world void = skipped BuildExterior"). Any navmesh-side fix must run the full chain, editor-closed.

---

## 3. The proper fix

Because the architecture is **NavMeshAgent-driven (not CharacterController)**, the fix is in TWO parts — both small, both fitting the existing model. **Do not add a CharacterController/Rigidbody gravity loop (WO-248/261 as written) — it would duplicate and fight the agent.**

### Part A — Restore a real ground→deck connection so stairs climb naturally (NavMesh-side)

The agent CAN climb a 31° ramp (45° slope limit). The job is to make the bake produce a **continuous walkable surface from ground to deck**. Recommended approach, in order of preference:

1. **Re-add a hidden nav ramp under the existing stair visual**, but fix the connection that failed before:
   - Make the ramp a single **continuous angled box** (not stepped) spanning ground (Y≈0) to the deck inner edge (Y≈5.4) at ≤45° (the old `rampRun = 9f` ≈ 31° is fine), width ≥ 2× bake radius + margin (≥ ~2.5 m so it survives radius-0.5 erosion).
   - Ensure the ramp's **bottom overlaps the ground tile** and its **top overlaps/abuts the deck slab** within `agentClimb` (0.4 m) so voxels connect at both ends. Overlap by ~0.3 m vertically at each seam.
   - Mark it NavigationStatic (the `BuildRamparts` Box helper + the NavMesh sweep already include the Walls root; confirm the ramp lands under a swept root or add it explicitly).
   - Keep the `Stairs_Medieval_Stone` prefab as the **visual only** (no collider that fragments the ramp, or a collider that matches the ramp slope).
2. If a baked ramp still won't connect reliably at the seams, add an explicit **`NavMeshLink`** (OffMeshLink) from a ground point to the matching deck point at each access — a deterministic, voxelization-independent bridge the agent traverses. This is the robust fallback and is cheap.

Either way the deck/lane loop already bakes walkable (`Fortify.cs:227-295`), so only the **vertical connector** is missing.

### Part B — An off-mesh ground-snap so the hero can never float (Locomotion-side)

This closes DEF-147 *systemically* and as a safety net for any future ledge. In `HeroLocomotion.Update()`, in the **off-mesh fallback path only** (where `transform.position += step` runs, `:248-249`, and within the off-mesh clamp block `:266-274`):

- Replace the "floor Y at 0" logic with a **downward ground-snap**: raycast down from slightly above the hero; if a walkable surface is below, `MoveTowards` the hero's Y down to it at a gravity-like rate (e.g. accumulate a downward velocity capped at ~-20 m/s), instead of leaving Y frozen.
- Prefer snapping back onto the navmesh: after the move, call `NavMesh.SamplePosition` near the hero; if a navmesh point is within a small vertical band, `Warp` the agent back on so the agent resumes its own ground-follow (this also auto-heals the lift's "suspended on deck" float).
- This is ~15–25 lines, no new components, and uses systems already imported (`UnityEngine.AI`).

**Why not just CharacterController gravity (the WO-248/261 prescription):** it adds a second movement authority on the same transform as the NavMeshAgent. The two will fight (agent clamps Y up to the surface; controller pushes Y down through it), causing jitter, and it duplicates ground-follow the agent already does on-mesh. The off-mesh ground-snap achieves the same anti-float guarantee within the existing single-authority model.

### Part C — Retire the lift (after A is verified)

Once Part A bakes a climbable ramp and is owner-verified in-editor:
- Stop `RampartLiftInstaller` from spawning (it's a runtime DDOL singleton — gating its `BuildLifts()` behind a flag retires it with zero scene/bake churn).
- `LiftPlatform` becomes unreferenced; leave the file in place initially (see §6 — keep as fallback for one build).

---

## 4. What it fixes & retires

| Symptom | Fixed by | Mechanism |
|---|---|---|
| DEF-147 hover exploit | Part B (+ B's navmesh re-snap heals the lift-suspend float) | off-mesh ground-snap / re-bind to navmesh; hero can never retain elevated Y |
| "Stairs don't climb" | Part A | baked continuous ground→deck ramp (or NavMeshLink) the shared agent traverses |
| Lift band-aid | Part C (enabled by A) | with a climbable ramp, access no longer needs agent-suspend hand-carry |

All three trace to "no proper ground+traversal." Part A gives traversal; Part B gives ground. Together they retire the lift.

---

## 5. Risk & blast radius

**Touches / could affect:**
- **All hero movement** (HeroLocomotion is the only locomotion script) — Part B changes the off-mesh path. On-mesh movement is unchanged. Risk: a bad ground-snap could yank the hero or jitter the camera. Mitigation: cap fall speed, smooth the snap, keep on-mesh path untouched.
- **NavMesh bake** (Part A) — re-adds geometry to the bake. Risk: a mis-seated ramp could (a) not connect (no fix) or (b) connect somewhere unintended, or (c) the new NavigationStatic box could alter gate-opening walkability if placed wrong. Mitigation: place ramps only at the two N/S access points (away from gates), verify the bake log's marked-count and walkability.
- **Enemy pathing** — enemies share the navmesh; a climbable ramp lets enemies reach the deck (intended per the design comment, but a *gameplay* change — validate it's not trivially exploitable or unfair).
- **Lift** — Part C disables it; if Part A regresses, access to the deck is lost (mitigated by keeping the lift as fallback for one build, §6).
- **Camera spacing / combat spacing** — hero Y snaps could nudge the follow camera; combat auto-target uses positions, minor.
- **Gates (prior fix)** — must NOT re-flag gate arches NavigationStatic (`NavMesh.cs:34-61`). Part A adds geometry only at rampart access, so low risk, but verify.

**What genuinely could break:** the ramp bake not connecting (same failure that caused the original removal) — this is the real uncertainty. The NavMeshLink fallback (§3 A.2) de-risks it. Worst case, keep the lift and ship Part B alone (still closes DEF-147).

---

## 6. Phasing & safety

1. **Branch:** do this on a dedicated branch (e.g. `feat/hero-verticality`), not directly on `feat/tower-core-loop`.
2. **Part B first (low risk, high value):** the off-mesh ground-snap closes DEF-147 immediately and is independent of the bake. Ship/verify it alone first.
3. **Part A behind the bake chain:** add the ramp/NavMeshLink, then run the FULL chain editor-closed: BuildVillage → BuildOuterWorld → BuildExterior → **BakeWorldNavMesh**. Confirm the bake log marks ground + ramp + deck and reports a connected surface. **This is the gate.**
4. **Keep the lift as a fallback for ONE build** (flag it off only after the ramp is owner-verified climbable in a real playtest). Do not delete `LiftPlatform.cs` / `RampartLiftInstaller.cs` yet — gate `BuildLifts()` behind a bool so it's a one-line revert.
5. **Playtest gate:** none of the navmesh connection can be confirmed headlessly — voxelization/connection is only observable in Play mode. Owner (or Tricia) must walk it.

**Must be verified in-editor (cannot confirm headlessly):**
- Hero actually walks UP the ramp ground→deck (the bake connected).
- Hero cannot float off any edge (Part B).
- Enemies climbing the ramp behaves acceptably.
- Camera doesn't jitter on the ground-snap.
- Gate exits still work (no bake regression).

---

## 7. Playtest checklist (owner walks through)

1. Spawn in the village, walk to a N/S rampart access point.
2. **Walk straight up the ramp onto the deck** — no lift, no stall. (Part A)
3. Walk the full deck/lane perimeter loop — no falling through, no invisible walls.
4. **Walk off the outer deck edge** — hero should FALL to the ground within ~0.5 s, not float. (Part B / DEF-147)
5. Walk off the ramp side mid-climb — same: falls, doesn't hover.
6. Once landed, confirm an enemy can reach and hit the hero (not stuck "untouchable").
7. Walk OUT a gate onto the exterior terrain — still seamless (no bake regression).
8. Let an enemy wave run while you stand on the deck — confirm enemies can path up to engage (intended).
9. Camera: during the ground-snap/fall, the camera follows smoothly (no jitter/pop).
10. (If lift still present as fallback) confirm it no longer hard-locks/float-bugs.

---

## 8. Recommendation

**Do it — it's the right systemic fix, and the thesis holds.** Approach:

- **Part B (off-mesh ground-snap in `HeroLocomotion`) — do this immediately.** ~15–25 lines, no new components, closes DEF-147 the *correct* way (not the CharacterController route in WO-248/261, which would fight the agent). **Supersede WO-248 and WO-261 with this proposal** — their prescribed fix targets an architecture that doesn't exist here.
- **Part A (re-add a connected nav ramp, NavMeshLink as fallback) — do this next, on the same branch, with a full bake.** This is what actually makes "stairs climb" and is the precondition for retiring the lift.
- **Part C (retire the lift) — gate behind a flag; flip OFF only after Part A is owner-verified climbable in a real playtest.** Keep `LiftPlatform`/`RampartLiftInstaller` in the tree as a one-line-revert fallback for the first build.

**Effort estimate:**
- Part B: ~0.5 day (code + self-test).
- Part A: ~1–1.5 days (geometry + iterate the bake until it connects; NavMeshLink fallback if needed). The bake-connection iteration is the only real unknown.
- Part C: ~0.5 day (flag + playtest cleanup).
- **Total: ~2–2.5 days**, gated on one in-editor playtest after the bake.

**Lift retention call:** **keep the lift as a fallback for the first build** after Part A lands — don't retire it immediately. The exact ramp-connection failure that caused its creation is the one risk Part A faces; having a working fallback in the same build means a deck-access regression doesn't strand the player. Retire it in the *following* build once the ramp is proven in playtest.
