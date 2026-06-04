# Parallel Lanes — division of work for CLI's silos (2026-05-30)

> For CLI running multiple silos at once. The rule (CLAUDE.md §9): lanes that **never touch the same
> files** run simultaneously; the **`VillageSceneBuilder.cs` lane is single-writer** (serialization
> bottleneck) — only ONE silo in it at a time. This maps the open WOs onto conflict-free lanes.

## The hard rule
- **`VillageSceneBuilder.cs` = ONE writer.** Every castle/gate/village-scene WO funnels through this one
  file → they **cannot** run in parallel with each other. Serialize them in one lane.
- **Everything in its own file = parallel-safe.** Combat code, world code (own builders), UI, data — run together.

---

## LANE A — Village Scene / Architect (SINGLE-WRITER — serialize, do NOT parallelize within)
*All touch `VillageSceneBuilder.cs` + bakes. One silo only. Run these in order.*
- **WO-166** — gate render/passable + 4 gates + walk-anim + pet (in progress)
- **WO-167** — gatehouse pillar clips ceiling (fold into 166's gate pass)
- **WO-168** — navmesh gate openings
- **WO-157** — strip crystal veins (magenta)
- **WO-137** — castle/rampart rebake (after the above)
> This lane is the **playable-village blocker** — highest priority, but inherently sequential.

## LANE B — Combat / AI (code only, no scene files — PARALLEL-SAFE)
*`EnemyBrain`, ATB, targeting, enemy data — never touches the builder.*
- **WO-135** — P1 bug-triage fixes (CrystalMine/VFXManager/WaveManager)
- **WO-155** — region enemy spawning + red-skull (reads WO-164)
- **WO-145/146/147** — advanced enemy tactics / formation / perception (CLI's own batch)
> Runs fully alongside Lane A — different files.

## LANE C — Core Data / Systems (code + data, no scene — PARALLEL-SAFE)
*GameState, catalog, progression, economy — Core/Village code, no builder.*
- **WO-164** — zone foundation (ThreatLevel/depth/ZoneState) — **keystone-adjacent, do early** (B+D read it)
- **WO-163** — console error triage (AmbientNPC param + AudioMixer) — partly animation, mostly code
- **WO-151** — village progression + crafting (BuildingUpgrade/VillageLevel — code+SO)
- **WO-108** — ⭐ **Player Build Mode** (the keystone — its own focused effort once village is playable)
> Lane C is where the **keystone (WO-108)** lives — the highest-value build, mostly its own files.

## LANE D — World / Exploration (own builders + code — MOSTLY PARALLEL-SAFE)
*`OuterWorldBuilder` (its own file, not VillageSceneBuilder) + world runtime.*
- **WO-153** — world crystal mine
- **WO-159** — node settlements (claim/defend/deplete) — big, phased
- **WO-160** — wandering tribes (randomized raids)
- **WO-165** — dungeon world portals (extends WO-154 spawner)
> Touches `OuterWorld.unity` via `OuterWorldBuilder` — its own scene/file, so safe vs Lane A's village.
> (Coordinate only if two world WOs touch `OuterWorldBuilder` at once — serialize those two.)

## LANE E — UI / Polish / Content (no code-system conflict — PARALLEL-SAFE)
- **WO-156** — camera over walls + pivot + wall-fade (`SmartMobileCamera.cs`, own file)
- **WO-162** — music selection (Audio + UI)
- **WO-161** — player home / pet home / store interiors (own scenes)
- **Dungeon content** (D2–D11 from `DUNGEON_DESIGNS.md`) — content authoring
- **WO-152** — city redesign (DESIGNER-led — parked for the designer)

---

## Conflict matrix (who can run together)
| | Lane A (builder) | Lane B (combat) | Lane C (core/data) | Lane D (world) | Lane E (UI/polish) |
|---|---|---|---|---|---|
| **A** | — (1 writer) | ✅ | ✅ | ✅ | ✅ |
| **B** | ✅ | — | ✅ | ✅ | ✅ |
| **C** | ✅ | ✅ | — | ✅ | ✅ |
| **D** | ✅ | ✅ | ✅ | —* | ✅ |
| **E** | ✅ | ✅ | ✅ | ✅ | — |

\* Lane D: serialize only if two WOs both edit `OuterWorldBuilder.cs` at once.
**Watch-outs:** WO-164 (Lane C) is read by B (WO-155) + D (WO-159/160) — **do WO-164 first** so they
build on a real `ThreatLevel`. WO-163's AmbientNPC fix overlaps WO-166's pet/anim — **reconcile** (one
animation fix). Wallet-merge (RESOURCE_ECONOMY_DESIGN Step 0) underlies WO-151/108/159 — do it early in C.

## Suggested 4-silo assignment
1. **Silo 1 → Lane A** (playable-village blocker: WO-166/167/168 → 157 → 137). Sequential, top priority.
2. **Silo 2 → Lane C** (WO-164 first, then wallet-merge, then toward WO-108 keystone).
3. **Silo 3 → Lane B** (WO-135 then WO-155).
4. **Silo 4 → Lane D** (WO-153/159/160 — or Lane E polish if world should wait on the keystone).

---

# 12-AGENT FAN-OUT (owner can run up to 12 silos)

**The bottleneck at 12 is NOT ideas (~41 open WOs) — it's the ONE single-writer file + dependencies.**
So: **exactly ONE agent in the builder lane** (everything else would corrupt it), and **11 agents fan out
across file-disjoint code/data/content** that never touches the builder. Dependency-respecting order baked in.

### The disjoint-file map (so 12 agents never collide)
Each agent owns a **distinct file/module**. Conflicts only arise if two agents edit the same `.cs` —
this assignment guarantees they don't.

| # | Agent task | Primary file(s) — disjoint | Depends on | Notes |
|---|---|---|---|---|
| **1** | **Builder lane (SOLE writer)** | `VillageSceneBuilder.cs` + bakes | — | WO-166→167→168→157→137, **sequential within this one agent** |
| **2** | Zone foundation | `Core/World/ZoneManager.cs`, `RegionZone.cs`, `ZoneState` | — | **WO-164 — do FIRST, 5/9/10/11 read it** |
| **3** | Wallet merge + economy | `EconomyService.cs`, `GameState.cs` (econ fields), `SaveSchema` | — | RESOURCE_ECONOMY Step 0 — underlies 108/151/159; coordinate GameState w/ #8 |
| **4** | P1 bug fixes | `CrystalMine.cs`, `VFXManager.cs`, `WaveManager.cs` | — | WO-135 — own files |
| **5** | Region enemy spawn | `RegionSpawnTable`, spawner, nameplate | #2 (ThreatLevel) | WO-155 — combat lane |
| **6** | Enemy tactics | `EnemyBrain.cs` + AI files | — | WO-145/146/147 — combat AI, own files |
| **7** | Camera | `SmartMobileCamera.cs` | — | WO-156 — own file (over-wall + pivot + wall-fade) |
| **8** | Village progression/crafting | `BuildingUpgrade.cs`, `VillageLevel.cs`, `BuildingEffects.cs` (new) + `GameState` progression fields | #3 (wallet) | WO-151 — coordinate GameState field-adds w/ #3 |
| **9** | World crystal mine + dungeon portals | `MineNode.cs`, dungeon-portal spawner | #2 | WO-153/165 — world runtime, own files |
| **10** | Node settlements | `Settlement*.cs` (new), node-reserve reframe | #2, #3 | WO-159 — big, phased; own new files |
| **11** | Wandering tribes | `TribeManager.cs`, `TribeDef/State` (new) | #2, #5 | WO-160 — own new files |
| **12** | Music + UI polish | `AudioService`/music UI; `BuildMenu` palette | — | WO-162 + UI; own files |

### Dependency waves (so nobody builds on air)
- **Wave 1 (start immediately, no deps):** #1 (builder), #2 (zone), #3 (wallet), #4 (bugs), #6 (enemy AI),
  #7 (camera), #12 (UI/music). **7 agents go at once, zero conflicts.**
- **Wave 2 (after #2 zone lands):** #5 (region spawn), #9 (mine/portals), #10 (settlements), #11 (tribes).
- **Wave 2 (after #3 wallet lands):** #8 (progression).
- **Keystone:** **WO-108 build mode** — give it a **dedicated agent** once #3 (wallet) is in; it's the
  highest-value build and mostly its own new files (`BuildMode/*`). Slot it as the freed-up agent after Wave 1.

### Hard coordination notes (the only ways 12 agents collide)
1. **`VillageSceneBuilder.cs` — agent #1 ONLY.** No other agent edits it. Period.
2. **`GameState.cs` is touched by #2 (ZoneState), #3 (econ/wallet), #8 (progression), +WO-108 (BaseLayout).**
   These add **different fields** — coordinate as **additive-only, one at a time** (or one agent owns all
   GameState field-adds and the others request them). This is the #2 collision risk after the builder.
3. **`OuterWorldBuilder.cs`** — if #9/#10/#11 all need to place world objects via it, **serialize those
   edits** (or one agent owns OuterWorldBuilder placements, others provide data).
4. **`SaveSchema`/`SaveMigrator`** — every persisted field bumps the schema; **one agent owns the schema
   version bump**, others coordinate their field through it (avoid version-clash).
5. **Reconcile overlaps:** #4's WaveManager vs CLI's existing WaveManager work; #5 region spawn vs #11
   tribes (both spawn enemies — share the roster, don't fork).

### Realistic throughput
- **7 agents can start instantly** (Wave 1, zero deps, disjoint files).
- **+4–5 more** unlock the moment #2 (zone) and #3 (wallet) land — usually within the first cycle.
- So **12 concurrent is achievable** after a short ramp, with #1 (builder) as the permanent solo lane and
  GameState/SaveSchema as the shared resources to coordinate (additive, one-at-a-time).
