# WORK ORDER 780 — Dungeon Functional Conformance (enemies-leash / treasure / connectivity / win-lose / dark-walls)

- **Status:** READY TO IMPLEMENT (after the §1 decision gate is answered by the owner)
- **Lane:** 11 (Build Mode / Player Base) + Lane 4 (UI feedback) + Lane 1 (scene bake, serial)
- **Minted:** 2026-07-27 (UI seat)
- **Source:** read-only 6-silo RCA of the whole dungeon system (2026-07-27)
- **Branch for this WO's spec:** `claude/ui-spacing-layout-review-bqas0h`
- **Implementer:** CLI (sole committer; writes/build-verifies all `.cs`, owns all bakes)

> This WO is a **conformance spec + acceptance ledger**, like WO-779. It states the
> owner's five felt outcomes as **explicit, checkable acceptance criteria** and gives
> CLI the headless proving-line to read for each one — so CLI can "compare against
> until the dungeon plays correctly." Work is DONE only when every criterion in §4–§9
> passes its stated check AND the §10 gate is green.

---

## 0. Relationship to existing dungeon work (read first — do NOT duplicate)

- **WO-770 (dungeon functional loop)** + its QA docs (`docs/qa/dungeon-raid-validation-2026-07-26.md`,
  `docs/qa/dungeon-regression-2026-07-26.md`, `docs/qa/WORK_ORDER_770_dungeon_functional.md`)
  own the D1–D16 findings. **Most D-findings are already FIXED and regression-locked on
  this branch** (D1 no-exit, D3 wrong return scene, D4 assumed-victory, D6 lore unreadable,
  D13/D14 silent feedback — all closed per the RCA). This WO does **not** re-open them; it
  targets the gaps the RCA found still open + the owner's new asks.
- **WO-770.11** already specs "placed dungeon enemies" (`DungeonEnemy`, `enemyPlacements[]`) —
  UNBUILT. §4 of this WO is the acceptance spec for that build (fold them together; do not mint a third).
- **WO-740–743 (RoomForge into mainline)** owns the composed-dungeon pipeline. §6 (connectivity)
  extends its baker gate; coordinate, don't fork.

---

## 1. ★ DECISION GATE (owner must answer before implementation) ★

The RCA found **two parallel dungeons**, and the owner's five asks are split across them:

| | **Healer's Cottage** (legacy `DungeonSceneBuilder` + `DungeonController`) | **`dg_starter_loop`** (RoomForge `DungeonBaker`) |
|---|---|---|
| Player-reachable from hub? | ❌ **orphaned — dev-tools only** (not in `DungeonWorldPortalSpawner` table) | ✅ East overworld portal |
| In-scene enemies? | ❌ none (combat teleports to isolated arena) | ✅ hero-aggro NavMeshAgent hollows |
| Treasure + recover + persist? | ✅ fully wired (chests/scatter/loot→larder→save) | ❌ not confirmed present |
| Win/lose machine? | ✅ both wired + non-softlocking | ❌ no `DungeonController` win/lose |
| Doors/steps connect? | ✅ horizontal; multi-level via port-link teleports; **no navmesh** | ✅ horizontal only; **stairs dead**; navmesh (ungated) |
| Dark/immersive walls? | fog+lantern dark, but **flat palette-colored walls** | single grey material, atlas absent |
| Hero mover | CharacterController (`DungeonHero`) | NavMeshAgent (`HeroLocomotion`) |

**The owner's five asks (enemies-near-spawn, treasure, doors/steps connect, win/lose, dark
immersive walls) describe ONE cohesive dungeon.** No single existing dungeon delivers all five.

**RECOMMENDATION (UI seat):** make the **Healer's Cottage the canonical player dungeon** — it
already delivers 4 of 5 (treasure, win/lose, multi-level connectivity, dark lighting). Wire it
into the hub portal, add in-scene leashed enemies (§4) + the dark stone wall swap (§9), and fix
its connectivity gaps (§6). Keep RoomForge for future procedural expansion. This concentrates
the work on the dungeon closest to the owner's vision.

**Owner picks ONE (answer before CLI starts):**
- [ ] **A — Canonicalize Healer's Cottage (recommended).** All §4–§9 criteria target the Cottage; §6 fixes its stair/nav for agent-enemies; wire it to the East portal.
- [ ] **B — Build up `dg_starter_loop`.** Port treasure + win/lose + dark walls + leashed enemies onto the composed pipeline; §6 fixes RoomForge stairs/navmesh gating.
- [ ] **C — Both, staged** (A first as the flagship, B later for procedural variety).

The acceptance criteria below are written **pipeline-agnostic where possible**; items marked
**[Cottage]** or **[RoomForge]** apply only under that choice. Default assumption if unanswered = **A**.

---

## 2. RCA summary (what the 6-silo review found — evidence for the criteria)

1. **Enemies near spawn — NOT how it works today.** Cottage has ZERO in-scene enemies; encounters
   teleport to an isolated `BattleArena` at `(5000,0,5000)` whose only leash is a 16m HERO-tether
   (`BattleArena.cs:1651`), not a spawn leash. The composed loop DOES seat hero-aggro agents
   (`DungeonBaker.PopulateForPlay:400`) but they chase the hero, don't hold a post. The reusable
   "hold your spawn anchor" pattern exists but only for raid outposts (`EnemyOutpost.SetBrainTarget(anchor)`).
   → "enemies stay near spawn" is a **NEW build** (WO-770.11 spec, unbuilt).
2. **Treasure — works, in the Cottage only.** Chests + floor-scatter + encounter loot all resolve to
   the persistent larder (`DungeonLootGrant`→`VillageInventory`→`GameState.Save`). Gaps: no open-feedback
   toast, underground treasure-room reachability unproven, id-audit vs `materials.json` outstanding.
3. **Connectivity — horizontal OK, stairs dead.** Flat single-level layouts connect (real 2.2u door
   gaps + coplanar floors). But RoomForge **stairs can never mate** (both sockets point down), build **no
   ramp geometry**, add **no nav-link**, and the baker **never gates on `NavMesh.CalculatePath`** — it logs
   path status and ignores it (`DungeonBaker.cs:229-241`). Cottage does multi-level via port-link teleports
   and bakes **no navmesh** (fine for its CharacterController hero; a blocker the moment agent-enemies are added).
4. **Loop — complete + softlock-free on both player portals, BUT the rich dungeon is orphaned.**
   Healer's Cottage (all WO-770 fixes live + regression-locked) is reachable only via dev tools. Folk's
   Granary returns to the WRONG hub (`Village2`, not `Main_Castle_Overworld`).
5. **Win/lose — both wired + non-softlocking (Cottage).** One felt bug: a real-time dungeon loss is
   **double-handled** (arena revive+fade AND dungeon fade-to-Castle both fire) — terminates at the hub but
   the on-screen result (double fade / stale banner) is unverified.
6. **Walls — flat palette colors, not stone.** Cottage walls get a solid per-room tint at ONE call
   (`DungeonSceneBuilder.cs:630`); lighting/fog is already dark. A single shared dark-stone material at
   that call re-skins every wall. A real stone texture needs an import/author or reuse of a committed
   LFS stone material (visual quality unverified on this Linux mount).

**Methodology caveat (§12):** all findings are static-code-CONFIRMED except the items explicitly marked
**NEEDS-HEADLESS** below. CLI must capture the proving line before claiming those fixed — do not inference-fix.

---

## 3. Definition of Done (the owner's five felt outcomes)

A player, entering the canonical dungeon from the hub, experiences:
1. **Enemies that hold their ground near where they spawned** — visible in-scene, engage when approached,
   return toward their spawn anchor when the player leaves their leash radius, never mob the entrance or
   fall through the floor.
2. **Treasure they can find and carry out** — chests/loot that open with clear feedback and that still
   exist in their inventory/larder back at the hub.
3. **A dungeon they can walk through** — every room reachable via doorways and steps/stairs; nothing
   islanded; the hero (and enemies) can path the whole space.
4. **A clear win and a clear loss** — clearing the dungeon (boss/objective) ends the run with reward and
   a way out; dying/losing ends the run cleanly and returns them to the hub. No softlock either way.
5. **A dungeon that looks like a dungeon** — cohesive dark stone walls, immersive lighting, not flat
   colored planes.

---

## 4. ENEMIES STAY NEAR SPAWN — acceptance criteria

Builds WO-770.11. Reuse the `EnemyOutpost.SetBrainTarget(anchor)` spawn-anchor idiom + `GuardPatrol`
(`Assets/_Sandbox/GuardPatrol.cs`) navmesh-waypoint pattern; do not invent a new AI.

- [ ] **AC-4.1 Placed enemies exist in-scene.** The layout schema carries `enemyPlacements[]` (id, roomId,
  position, optional patrol) and the controller hydrates one live enemy actor per entry at its authored
  position. **Check:** `[Flow:Dungeon] HydrateEnemies: spawned N of N placement(s)` with N>0; N enemies
  visible in a headless screenshot standing in their rooms.
- [ ] **AC-4.2 Leash to spawn anchor.** Each enemy stores its spawn position as a home anchor; when the
  player is beyond `leashRadius` (authored, default ~10m) the enemy returns toward the anchor and idles,
  not toward the hero or the entrance. **Check:** `[Flow:DungeonEnemy] LEASH id=<x> returning home dist=<d>`
  fires when the hero leaves the radius; enemy ends within ~1m of its anchor. NEEDS-HEADLESS to confirm the
  visible return.
- [ ] **AC-4.3 Engage only within range.** Enemy aggros the hero only inside `aggroRadius`
  (≤ leashRadius), and disengages + returns home when the hero exits. No entrance-mobbing: an enemy in a
  back room never migrates to the spawn/entry room while idle.
- [ ] **AC-4.4 Enemies stay on the floor.** Every placed enemy sits on the walkable surface for its whole
  life — no off-mesh fall-snap / sinking. **[Cottage]** requires a baked navmesh (AC-6.5) OR a
  non-agent grounded mover; if `NavMeshAgent`-based, `agent.isOnNavMesh == true` for every enemy every
  frame (`[Flow:DungeonEnemy] WARN off-mesh` must never fire). **Check:** no off-mesh warnings across a
  full headless run; no enemy Y drifts below floor Y.
- [ ] **AC-4.5 Enemies participate in the win/lose loop (§7).** Defeating the placed enemies / boss is what
  drives the clear condition; they route through the single `SettleEncounter` authority, not a second combat path.
- [ ] **AC-4.6 No regression to the arena path.** If the isolated `BattleArena` path is retained for any
  encounter, its hero-tether leash (`BattleArena.cs:1651`) still functions; the new in-scene leash does not
  double-drive the same enemy.

---

## 5. TREASURE TO RECOVER — acceptance criteria

Wiring is largely present (Cottage). These close the RCA's open gaps.

- [ ] **AC-5.1 Treasure is present + reachable.** Every authored chest/loot node is physically reachable by
  walking from spawn (no chest sealed behind an un-openable illusory wall or an unreachable level). **Check:**
  headless walk (AutoPilot) reaches each `Chest_<id>` position; `[Flow:DungeonLoot] HydrateChests: wired N of N`
  with N == authored count. NEEDS-HEADLESS for reachability.
- [ ] **AC-5.2 Open gives feedback.** Opening a chest fires a `DungeonToastView` toast naming the reward
  (mirror the checkpoint/lore/craft subscribers). **Check:** `DungeonChestInteract.Opened` has a
  `DungeonToastView.Show` subscriber wired in `HydrateChests`; toast text asserted in
  `DungeonToastRegression`. (Currently the `Opened` event fires into the void — this is the single biggest
  felt gap in treasure.)
- [ ] **AC-5.3 Loot grants + persists.** Opened loot writes to the persistent larder and survives the exit.
  **Check:** existing path (`DungeonLootGrant.GrantChest`→`VillageInventory.Add`→`GameState.Save`) unchanged;
  a data-regression asserts the granted `materialId`s exist post-`ExitToVillage`.
- [ ] **AC-5.4 Scatter banks once, no leak.** Per-run `DungeonInventory` deposits to the larder on exit
  exactly once (no double-deposit; no leak into the next run). **Check:** `DungeonStateResetRegression`
  covers a two-run sequence; `[Flow:DungeonLoot] DepositDungeonInventory` appears once per run.
- [ ] **AC-5.5 Loot-id audit.** Every id in the 5 dungeon loot tables + chest reward tables
  (`loot-tables.json`) resolves to a real `materials.json` / recipe id (no ghost larder entries). **Check:**
  a `DataRegression` cross-references the tables against `materials.json`; marker fails on any orphan id.

---

## 6. DOORS + STEPS CONNECT — acceptance criteria

- [ ] **AC-6.1 Every room reachable from entry.** The room-connection graph is fully connected from the
  spawn room. **Check:** existing per-layout graph assertion, extended to ALL shipped layouts
  (`dg_starter_loop`, `demo_branching_kit`, plus the Cottage rooms).
- [ ] **AC-6.2 [RoomForge] NavMesh path-connectivity is GATED, not just logged.** `DungeonBaker` hard-fails
  the bake (withholds the OK marker) if `NavMesh.CalculatePath` is not `PathComplete` between every pair of
  rooms — today it logs `path.status` and does nothing (`DungeonBaker.cs:229-241`). **Check:**
  `[Flow:DungeonBake] navmesh path[<a>-><b>]=Complete` for ALL pairs; a deliberately-islanded test layout
  makes the bake ABORT.
- [ ] **AC-6.3 [RoomForge] Regression hard-fails PathPartial.** The oracle's navmesh case tests all sample
  layouts (not just the 3-room spine) and treats `PathPartial` as FAIL, not a note (`RoomForgeRegression.cs:445`).
- [ ] **AC-6.4 Steps/stairs are traversable.** Under the chosen pipeline, a level change is walkable:
  - **[RoomForge]** stair sockets mate (fix the both-point-down bug at `DefaultDungeonRoomsBuilder.cs:465-467`),
    real ramp geometry is emitted, and the vertical seam is spanned by a walkable ramp (project canon: the
    input hero cannot cross a `NavMeshLink` — use a ramp, per `DungeonChainBuilder.cs:17-19`). **OR** if
    multi-level is out of scope, remove/repurpose the `StairUp/StairDown` sockets+rooms so nothing advertises
    a dead capability (they currently ship as flat cosmetic rooms).
  - **[Cottage]** the port-link teleport traversal (`DungeonPortLink` via `DressTraversalLinks`) actually moves
    the hero between levels. **Check:** headless walk crosses every level transition; `[Flow:Dungeon] PortLink
    traversed` per transition.
- [ ] **AC-6.5 [Cottage] NavMesh exists if agent-enemies are added.** If §4 uses `NavMeshAgent` enemies, the
  Cottage bakes a navmesh spanning all rooms/levels (or enemies use a non-agent grounded mover). **Check:**
  `NavMesh.CalculatePath` complete across the Cottage; no off-mesh warnings (ties to AC-4.4).
- [ ] **AC-6.6 Mate implies passable.** A mated door socket is validated to have an actual wall opening
  aligned with it (raycast/gap check at bake), so "mated" can't mean "walk into a solid wall"
  (`DungeonBakerChecks.Compose`). **Check:** a test room with a socket on a solid wall FAILS the bake.
- [ ] **AC-6.7 No overlap / no gap.** Rooms neither interpenetrate nor leave a floor gap at doorways.
  **Check:** existing overlap gate + a new abutment assertion that mated floors are coplanar and touching.

---

## 7. WIN / LOSE — acceptance criteria

- [ ] **AC-7.1 Win condition defined + reachable.** Clearing the dungeon (defeat the boss / all placed
  enemies per §4) sets the clear flag and opens the exit. **Check:** `[Flow:Dungeon] BossDefeated` (or
  `DungeonCleared`) fires; the gated exit reveals; `DungeonExitReachableRegression` green.
- [ ] **AC-7.2 Win payoff.** A win grants boss/clear loot and banks the run before returning to the hub.
  **Check:** `DungeonLootGrant.GrantEncounter(true)` on victory; larder delta asserted.
- [ ] **AC-7.3 Lose condition defined.** Hero HP 0 / timeout / flee / disengage ends the run with NO loot
  and NO clear credit. **Check:** `DungeonDefeatEndsRunRegression` green; `SettleEncounter(false,…)` path.
- [ ] **AC-7.4 Neither outcome softlocks.** Both win and lose route through `ExitToVillage`/return-to-hub;
  hero is revived in place, never arrives dead, never trapped. **Check:** `DungeonReturnSceneRegression` +
  `DungeonRealtimeSettleRegression` green.
- [ ] **AC-7.5 Real-time loss is single-handled (fixes the RCA's one felt bug).** A lost real-time fight does
  NOT run BOTH the arena's in-dungeon revive+fade+banner AND the dungeon's fade-to-Castle. Exactly one
  return sequence plays; the loss banner is correct for the destination. **Check:** NEEDS-HEADLESS — capture a
  lost real-time dungeon boss fight; confirm a single `ScreenFader` sequence and one correct banner (no
  double-fade / stale banner over the hub). This is the one open win/lose defect.
- [ ] **AC-7.6 No orphaned win/lose.** The canonical player dungeon (per §1) HAS this machine. If Folk's
  Granary remains reachable, it either gets a minimal win/lose or is de-listed from the portal table (not
  shipped as a fights-nothing dead end).

---

## 8. LOOP INTEGRITY — acceptance criteria

- [ ] **AC-8.1 The canonical dungeon is reachable from the hub.** **[Cottage, choice A]** add
  `HealersCottage` to the `DungeonWorldPortalSpawner` authored table so a hub portal loads it (today it is
  dev-tools-only). **Check:** `[Flow:...] portal -> HealersCottage` on interact; scene loads.
- [ ] **AC-8.2 Consistent return hub.** Every dungeon returns to the hub the player entered from
  (`Main_Castle_Overworld` / `SceneRouter.Castle`). Fix Folk's Granary's `DungeonStubReturn` → `Village2`
  (`DungeonStubReturn.cs:39`) to the correct hub. **Check:** `DungeonReturnSceneRegression` asserts the
  return scene == entry hub for each dungeon.
- [ ] **AC-8.3 Hero spawns, moves, is framed — no blank/frozen.** On load: layout/scene assembles, hero
  teleports to spawn, input hands back, camera frames. On load FAILURE input is still handed back (no frozen
  hero in a blank scene). **Check:** `[Flow:Dungeon] EnterDungeon: run live (Ready=true)`; NEEDS-HEADLESS to
  confirm the composed hero actually gets camera+control (`HeroControlEnsurer.Ensure`) in a non-village scene.
- [ ] **AC-8.4 Single mover, no leak.** The dual-mover neutralize/restore (`EnsureSingleDungeonMover`/
  `RestoreInjectedHeroMover`) leaves no static `HeroLocomotion` gate (`GroundSnapEnabled`, scripted-move)
  leaked to the village hero after exit, including abnormal teardown. **Check:** NEEDS-HEADLESS — after a
  dungeon round-trip, village hero `GroundSnapEnabled == true` and moves normally.
- [ ] **AC-8.5 Always leavable.** A normal always-open exit exists from load (independent of the boss gate),
  so the run is never a roach-motel. **Check:** `DungeonExitReachableRegression` green.

---

## 9. DARK IMMERSIVE STONE WALLS — acceptance criteria

- [ ] **AC-9.1 One cohesive wall material.** All dungeon walls use a SINGLE shared dark-stone material — not
  per-room solid palette tints. **[Cottage]** replace `ApplyTint(go, PaletteTint(r.Palette))` at
  `DungeonSceneBuilder.cs:630` with assignment of one shared dark-stone `sharedMaterial`; apply the same to
  `BuildFloor` (`:545`) and `BuildCeiling` (`:643`) so floor/ceiling read cohesive too. **[RoomForge]** lower
  `RoomForgeMaterials.Wall` darken (`RoomForgeMaterials.cs:41`) and/or repoint `_BaseMap` to the stone texture.
  **Check:** headless screenshot — every wall the same dark stone; no room-to-room color shift.
- [ ] **AC-9.2 Real stone texture (or an approved dark base).** Use a committed tiling stone material
  (candidates: `Resources/Arena/Floor_Sharp_Stones.mat`, `Resources/Materials/Moat/BridgeStone.mat`, Lana
  Studio `stone03/04`) that reads as dark dungeon stone at wall scale. If none tiles well, a flat dark URP/Lit
  base color is the acceptable fallback for v1. **Check:** owner felt-approval on a screenshot (headless can
  render it; only the owner judges "feels like a dungeon"). NEEDS-HEADLESS-SCREENSHOT + owner sign-off.
- [ ] **AC-9.3 URP/Lit, no pink/blowout.** The stone material is URP/Lit with a set `_BaseColor`, Metallic 0,
  Smoothness 0 (avoids the historical colorless-tile pink-floor bug and spec blowout). **Check:** material
  shader == `Universal Render Pipeline/Lit`; no magenta in the screenshot.
- [ ] **AC-9.4 Still playable-dark, not black.** Keep the existing fog (`#0a0a10`, 14→42m) + lantern +
  per-room warm fixtures; if the dark stone reads too black, nudge `AmbientIntensity` 0.05 → ~0.08–0.12
  (`DungeonSceneBuilder.cs:170/1987`) — do NOT flatten the fog. The player always has a lit bubble (lantern
  range 6u). **[RoomForge]** add matching fog to `DungeonBaker.cs:196` (currently none). **Check:** headless
  screenshot legible around the hero; owner felt-approval.
- [ ] **AC-9.5 Rebuild via batch, never hand-edit the scene.** The re-skin ships by re-running the scene
  builder in batchmode (`DeNelle.Editor.DungeonSceneBuilder.BuildHealersCottage` / RoomForge rebake), NOT by
  editing the `.unity` (CLAUDE.md §3).

---

## 10. Verification gate (run after each concern; WO DONE when green)

CLI is sole gate-runner (§12 — instrument, don't guess; NEEDS-HEADLESS items require the capture, not a claim).

1. **Compile/NUL gate:** `DeNelle.Editor.CompileGate.Run` → `COMPILE_GATE_OK` after every `.cs` edit.
2. **Dungeon regression suite (extend, don't fork):** all of `DungeonExitReachableRegression`,
   `DungeonDefeatEndsRunRegression`, `DungeonRealtimeSettleRegression`, `DungeonReturnSceneRegression`,
   `DungeonStateResetRegression`, `DungeonToastRegression`, plus NEW cases for: enemy-leash return (AC-4.2),
   chest-open toast (AC-5.2), loot-id audit (AC-5.5), navmesh path-complete gate (AC-6.2/6.3), return-hub
   consistency (AC-8.2). Green = required.
3. **Headless AutoPilot run** (`run-defenders` skill) through the FULL loop on the canonical dungeon:
   enter from hub → walk every room + level → reach every chest → engage + leash-test enemies → win path →
   lose path → exit → confirm larder persistence + village hero mover restored. Read the `[Flow:*]` proving
   lines named in each AC above.
4. **Headless screenshots** at the entry room + a mid-dungeon room for the wall/lighting ACs (§9).
5. **PO (owner) felt-verifies + closes** (§13) — the felt items (AC-9.2/9.4 "feels like a dungeon", AC-7.5
   double-return, AC-4.2 visible leash) are owner-judged; headless can capture but not judge feel.

---

## 11. What NOT to touch

- Do **not** hand-edit any `.unity` scene (CLAUDE.md §3) — all geometry/material changes go through the
  batch builders + a bake work order.
- Do **not** run a bake with the Unity editor open (project lock).
- Do **not** re-open the already-fixed WO-770 D-findings (D1/D3/D4/D6/D13/D14) — they're regression-locked.
- Do **not** delete the isolated `BattleArena` path without confirming §4's in-scene combat fully replaces it.
- Do **not** touch `VillageSceneBuilder.cs` (serialization bottleneck, Lane 1 single-writer) as part of this.
- Keep `?.` on cross-module service calls; no new `System.Reflection`.

---

## 12. Acceptance criteria (review line by line before RESULT)

- [ ] §1 decision gate answered by the owner; criteria scoped to the chosen pipeline.
- [ ] §4 enemies exist in-scene, leash to spawn, stay on the floor, feed win/lose.
- [ ] §5 treasure reachable, opens with feedback, grants + persists, ids audited.
- [ ] §6 every room reachable; navmesh path-complete GATED; steps/stairs traversable or dead capability removed.
- [ ] §7 win + lose both wired, non-softlocking, single-handled real-time loss.
- [ ] §8 canonical dungeon reachable from hub, consistent return, no blank/frozen/leak.
- [ ] §9 single dark stone wall material, URP/Lit, playable-dark, rebuilt via batch.
- [ ] §10 gate green (compile + regression + headless loop + screenshots); owner felt-verified.
- [ ] Canon updated (§15): dungeon state in the load-bearing docs + this WO's RESULT + the QA validation doc.

---

_Authored by the UI seat (spec/RCA only — UI never edits `.cs`). CLI implements, build-verifies, bakes,
and is sole committer. Evidence: 6-silo read-only RCA (enemy-leash / treasure / connectivity / loop /
win-lose / walls), 2026-07-27. All findings CONFIRMED-from-code except items marked NEEDS-HEADLESS._
