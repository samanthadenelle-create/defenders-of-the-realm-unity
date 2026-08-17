> ⚠ **NUMBER COLLISION — this document does not own WO-433; `WORK_ORDER_433_shop_blink_cohesion.md` does.**
> Referred to hereafter as **WO-433-B (Village2 raid destination)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-433 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK ORDER 433 — Village2 Raid Destination (populate + objective + headless test)

**Status: SPEC — owner design sign-off needed on 3 decisions (defaults proposed so the loop can build v1).**
WO# provisional (confirm vs CLI_LANES_WO_NUMBERS). **Lane:** 2 Combat/AI + 5 World/Explore.
**Unblocked by:** the castle→OuterWorld→cave-portal→Village2 flow now WORKS (owner-confirmed 2026-06-20,
"made it to the port and worked"). Now Village2 = the place "where they go" — design + populate + make it
**headless-testable** (the fleet can finally drive bots in).

## Current state (RCA done — be the SME, do NOT rebuild)
- **Built + baked + reachable.** `EnemyStrongholdBuilder` (`Defenders > World > Build Village2 Enemy
  Stronghold` / batch `DeNelle.Editor.EnemyStrongholdBuilder.Build`) builds a layered fortress: outer
  courtyard + wall ring + main gate + 4 corner towers → chokepoint → raised inner keep → raised boss
  chamber w/ altar. Navmesh baked (`Assets/Scenes/Village2/NavMesh-Village2.asset`), stairs bridged by
  NavMeshLinks. Recipe = `garrison-recipes.json` → `"village2_stronghold"`: enemies
  `[orc-berserker, orc-shaman, troll, hollow-warrior]`, levels 8–14, threat 3, **boss: null**.
- **Entry:** `HeroStartPoint_PlayerSpawn` ≈ `(0, 0.1, -courtyardHalf-6)`, facing the courtyard. Return seam
  `ReturnToOuterWorld_Seam` → OuterWorld (correct).
- **8 spawn points** authored + a `GarrisonController` auto-wired to `StrongholdRoot`… **but `Activate()`
  is NEVER called → ZERO enemies spawn today.**
- **No victory:** `RaidVictoryController` self-installs ONLY in `RaidBase*` scenes, so Village2 has no
  win/lose. Traps + corner turrets are built but inert.
- **Autopilot:** `AutoPilotDriver` has a `WalkToEachGate` phase that finds `SceneTransitionTrigger`s (so a
  bot CAN cross the cave portal into Village2), but **no Village2 combat phase** to verify the raid.

## Scope (the delta — build v1)

### 1. Spawn the garrison on scene load — `Village2RaidController` (new, self-installing)
Mirror `RaidVictoryController.InstallHook()` (a `[RuntimeInitializeOnLoadMethod]` that gates on scene name).
On `Village2` loaded: find the `GarrisonController`, call `Activate()` (spawns the 8-point garrison via the
single `EnemyFactory.Build` path) + `ArmGarrisonTurrets()`. Guard with FlowTrace (§12) so a capture shows
"garrison activated, N enemies". Idempotent (don't double-spawn on additive reload).

### 2. Victory condition + flow — reuse the proven raid backbone
Subscribe to `GarrisonController.OnCleared` (fires when the last defender dies). On clear, run the
RaidVictoryController-style flow adapted for Village2: **victory banner → claim (RaidClaimService) →
unlock next companion (GameStateService.AddToParty) → "Return to Castle" button** (routes via the return
seam). Reuse the existing services; do NOT re-implement claim/companion.

### 3. Boss (DESIGN DECISION #2) — the boss chamber is built but `boss:null`
Default proposal: author a **stronghold warlord** at the altar (a buffed `orc-berserker`/`troll` variant,
level 14) as the capstone — defeating it (or clearing the garrison incl. it) = win. Creative owns the
boss's identity/feel. (Alt: leave boss-less for v1 and win on garrison-clear — simpler.)

### 4. Objective UI
On arrival, a brief prompt: "Raid the enemy stronghold — defeat the garrison." + a HUD objective/alive-count
tracker (mirror the castle "Defend the tower" callout). Code-built UI (§8 — no UXML).

### 5. Headless test — new autopilot phase + oracles
Add a Village2 phase after `WalkToEachGate`: once in Village2, (a) assert `GarrisonController.AliveCount > 0`
within N seconds (enemies spawned), (b) drive the bot to engage + assert the hero can deal damage (alive
count drops), (c) assert `OnCleared` eventually fires (raid winnable) OR time-box + report progress. New
`AutoPilotProbes` checks: ENEMIES-PRESENT (fail if 0 after spawn window), RAID-WINNABLE. So the fleet
self-verifies the raid end-to-end with no human.

## Design decisions for owner (defaults let the loop build v1)
1. **Win condition:** (a) clear the whole garrison [default], or (b) just defeat the boss, or (c) destroy
   the altar/keep objective?
2. **Boss:** author a warlord capstone [default], or boss-less v1?
3. **Reward on victory:** claim the stronghold + next companion [default, reuses raid flow], or different
   loot/progression (crystals, gear, a Keystone toward the Spire finale WO-292)?

## Acceptance criteria
- [ ] Entering Village2 spawns the garrison (8 points, recipe enemies, turrets armed) exactly once.
- [ ] An objective prompt + alive-count tracker shows on arrival.
- [ ] Defeating the garrison fires victory → claim + companion + return-to-castle.
- [ ] Fleet bot: reaches Village2, sees enemies (>0), engages, and the raid resolves (win or measured
      progress) — ENEMIES-PRESENT + RAID-WINNABLE oracles green.
- [ ] No regression to the working seam/portal flow.

## What NOT to touch
- The seam walk (`HeroLocomotion`), the cave portal entry, the return seam wiring.
- `EnemyStrongholdBuilder`'s geometry/bake (only ADD the activation + victory + test, don't rebuild layout).
- The single `EnemyFactory.Build` spawn path (§9) — reuse it.
- No hand-edited scenes (Village2 rebuilds via the builder; bake in a work order, editor closed).

## Files
- NEW `Assets/_Modules/Village/World/Camps/Village2RaidController.cs` (self-install + activate + victory).
- `Assets/_Modules/Village/World/Camps/GarrisonController.cs` (use `Activate()`/`ArmGarrisonTurrets()`/
  `OnCleared`/`AliveCount` — already exist).
- `Assets/Resources/Data/Canonical/garrison-recipes.json` (`village2_stronghold` — add boss if decision #2=yes).
- `Assets/_Modules/DevTools/AutoPilotDriver.cs` + `AutoPilotProbes.cs` (Village2 phase + oracles).
- Objective UI: a small code-built HUD callout (reuse the castle pattern).
