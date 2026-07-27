# RAID NORTHSTAR — the ONE canonical raid doc

> **LIVE ANCHOR for all raid work (2026-07-26).** This is the single source of truth for what
> the raid loop *is* and what to build. Where any other raid doc disagrees with this one, this
> wins. Anchored to `CANON_GROUND_TRUTH_2026-07-26.md` §3 (owner ruling: raid loop LOCKED to
> Teleport/Deploy). The build plan is **WO-771 v1** (`docs/qa/WORK_ORDER_771_raid_system.md`) —
> nothing else is the raid loop.
>
> **★ STATUS 2026-07-26 (code-grounded correction): the V1 spine ALREADY EXISTS end-to-end in-tree.**
> Every CoC beat below maps to a shipped class (train → ArmyStorage → RaidSelectionScreen →
> RaidDeployScreen → `SceneRouter.GoRaid` → `RaidBase_*` → RaidDeployController tap-deploy +
> `TroopDeployer.SpawnFromArmy` → TroopController auto-fight → RaidScoring 180s/stars/loot +
> RaidHudController → claim). See **§2A** for the beat→class map. What remains is **polish + clarity
> (WO-774 P0 UX), not a rebuild** — old "nothing built / all SPEC" prose in this doc and the ground-truth
> anchor is STALE; code is ahead of the docs.
>
> **Why this doc exists:** an owner architectural review found the raid canon carried *multiple
> conflicting fantasies* (walk-to overworld outposts; hero/party fortress-infiltration;
> deterministic-sim-first) alongside the real shipping loop. Those are reframed or retired below.

---

## 1. The player loop (memorize this sentence)

**Train at Barracks (cost + timer) → troops enter Army storage (housing cap) → open Raids, pick
a difficulty card (generated base) → deploy troops on the ring → Begin: watch auto-combat →
Stars + loot summary → home.**

That is the whole loop. It is the Clash of Clans PvE model: you never control a unit mid-fight —
you *deploy and watch*. Everything in the raid pillar serves that one sentence.

---

## 2. The V1 spine (what to actually build — the CoC PvE ship)

V1 = **PvE against generated bases**, reusing the existing real-time combat. No deterministic
fixed-point sim, no async PvP, no server re-sim. Concretely, V1 is built from:

- **`RaidBaseGenerator`** — config-driven base baking (rings / gates / towers / boss + garrison
  spawner). A raid target = *a config row + "Build All Raid Scenes."* This **is** the base
  authoring — do NOT rebuild it, and do NOT build a base-snapshot/capture layer for V1.
- **Existing real-time combat: `EnemyFactory → Enemy → TargetManager`** — the auto-battle is
  **free**. The "watch" the player does is this existing real-time combat running on the
  generated base. Do NOT rebuild the auto-battle; ATB is retired for raids.
- **Deploy UI** — tap-to-place troops on the deploy ring outside the walls, then "Begin Assault."
- **Barracks + Train** — an upgradeable building that unlocks/scales troops; training spends
  resources and enqueues a timed job (see WO-773).
- **Stars / loot** — CoC 3-star scoring (destruction %, Heart razed, 100%) + a loot summary.

### V1 spine — ordered (the only build order that matters)

Point all raid work at **WO-771 v1**. The ordered spine:

1. **771.0** — move `IDamageableStructure` into `DeNelle.Core.Combat` (module isolation prereq).
2. **771.1** — troop data schema + canonical `troops.json`.
3. **771.1b** — consolidated save-schema migration (owns all new `GameState` fields).
4. **771.4** — deploy screen + `RaidDeployLog` (deploy on the ring) **+ reuse the existing
   real-time combat as the "watch."**
5. **771.9 + WO-773** — Barracks / troop upgrades / timed training (train channel via the
   common Obsidian queue).
6. **771.6** — scoring / stars / loot payout.
7. **771.10** — defensive towers (only if the generated base's towers don't already fire).
8. **771.11** — live raid HUD.
9. **771.13 + WO-772** — shared troop/enemy art + Animator Controller.

**Do first, unconditionally when raid work starts:** set `ff.overworldencounter=0` (leftover
preview default) and `ff.raidwalk` **OFF** — otherwise neither loop spawns and raids look broken
out of the box.

### V2 — DO NOT BUILD for the CoC PvE ship

These are **V2 (rewarded PvP)** and must NOT be started for the V1 ship:

- **771.2 / 771.7** — base-snapshot capture + async player-base matchmaking / anti-cheat.
- **771.3** — the deterministic fixed-point `RaidSim` (flow-field pathing, fixed-point math).
- **Server byte-exact re-sim** — the anti-cheat re-simulation.

These exist only when a rewarded/SKR PvP ladder needs server-verifiable results. Until then
they are over-engineering. For V1, "replay" = a re-watch from the recorded deploy log + the
stored result — **not** byte-exact determinism.

---

## 2A. STATUS — the V1 spine EXISTS end-to-end (2026-07-26, verified from code)

The ordered spine in §2 reads as a build-plan; in reality it has **largely LANDED**. A code-grounded
review (WO-774 "Why") confirmed the raid loop is reachable and CoC-shaped today. **This is polish +
clarity, NOT a rebuild.** Each CoC beat → the in-tree class that serves it (all paths verified at HEAD):

| CoC beat | In-tree class (verified path) |
|---|---|
| **Train** (cost + timer, multi-channel) | `TroopTrainingVM` + `TroopTrainingPanel` (`Village/Hero/`) → **Train channel** of the WO-773 queue (`Core/Jobs/ObsidianQueueEngine.cs` + `ObsidianQueueState.cs`, `JobKind.cs`, `IJobEffect.cs`) |
| **Army storage** (housing cap + perk + veterancy) | `ArmyStorage` (`Core/State/ArmyStorage.cs`) |
| **Pick target** (difficulty card) | `RaidSelectionScreen` + `RaidSelectionVM` (`Village/Hero/`); `ff.raidwalk` OFF routes the raid icon here (not the walk-to outpost) |
| **Pre-raid** (who comes — the loadout) | `RaidDeployScreen` + `RaidDeployVM` (`Village/Hero/`) |
| **Teleport** | `SceneRouter.GoRaid(sceneName)` (`Core/SceneRouter.cs`) → the `RaidBase_*` scenes: `RaidBase_IronBastion`, `RaidBase_fortified_garrison`, `RaidBase_mage_enclave`, `RaidBase_raider_camp_small` |
| **Tap-deploy** on the map | `RaidDeployController` tray + ground raycast (`Village/Troops/`) → `TroopDeployer.SpawnFromArmy(PlayerTroop, pos, …)` (`Village/Troops/TroopDeployer.cs`) |
| **Auto-fight** (deploy-and-watch) | `TroopController` (`Village/Troops/TroopController.cs`) hunts Hostile; the defending garrison is the existing `EnemyFactory → Enemy → TargetManager` real-time combat |
| **Stars / loot / 180s clock** | `RaidScoring` (`Village/Troops/RaidScoring.cs`, oracle `Editor/Regression/RaidScoringRegression.cs`) + `RaidHudController` (`Village/Troops/`) |
| **Claim** | victory/claim summary |

**Known seam (drives WO-774 P0):** `SceneRouter.GoRaid` takes **only a scene name** — there is no
`RaidParams`/pending loadout bag yet, so the field tray arms the FULL roster (`GetDeployable()`), not a
chosen loadout. That handoff is the WO-774 work, not a missing spine.

### The raid ladder (from the WO-774 review — sequencing, not one WO)

- **P0 — WO-774 (in flight):** loadout handoff (add `RaidParams`/pending bag mirroring `PendingBattle`;
  field tray arms ONLY the loadout) · Army/Deploy naming split (never label both the pre-raid modal AND
  the in-raid tray "Deploy") · deploy ring / spawn apron (first drops outside the outer wall only; ghost
  preview + forbidden-red outside) · victory/star copy = **"Defenders %"** (garrison kills), never "base
  destroyed" · Train-channel queue visible in the Barracks Train tab.
- **P1 (next raid WOs):** scout stub + Auto Recommend recipes; ghost preview + drop VFX/SFX; 2× speed
  toggle; one perfect Footman + one Archer silhouette (art lane, depends on pack tooling + KayKit Phase 2);
  star thresholds tied to boss/gate, not only full clear.
- **P1.5 (= WO-771.6 stakes, PAIN_POINTS F1):** casualties (% of deployed lost on defeat, partial even on
  1★) + stars 1/2/3 + soft loot table.
- **P2 / V1.5:** breach-expand deploy zone (open interior after a gate/wall dies); structure %
  destruction; favorite army presets; post-raid shields; **barracks as a real upgradable catalog building**
  (the F3 follow-on).

### Flags of record (verified in `Core/FeatureFlags.cs`)

- **`ff.raidwalk` = OFF** (`defaultOn: false`) — the raid icon opens `RaidSelectionScreen`'s deploy loop;
  `ff.raidwalk=1` restores the retired walk-to outpost path.
- **`ff.buildtimers` = ON** (`defaultOn: true`) — timed jobs flow through the queue; `=0` restores instant.
- **`ff.barracks` = ON** (`defaultOn: true`, flipped ON 2026-07-26 for V1) — the barracks-gated troop roster
  must be reachable so the deploy loop can pull trained troops; `=0` hides it again if it needs more polish.
- **Do-first when raid work starts:** `ff.overworldencounter=0` (leftover preview default) so encounters
  don't shadow the raid loop.

*V2 (async PvP + deterministic sim) remains parked — see §2 "V2" and §3 below. This §2A records only the
WO-771 V1 list as shipped/in-flight.*

---

## 3. NOT this (retired / reframed raid fantasies)

The raid canon accumulated parallel fantasies. To keep the target single, these are explicitly
**not** the raid loop:

- **Walk-to overworld outposts — RETIRED.** `ff.raidwalk` is OFF; `EnemyOutpost` walk-to markers
  are not the raid loop. (They *may* return later as a light overworld "patrol" side-activity —
  never as the raid control model.) The "~70% raid loop already built" figure in
  `docs/ARENA_SOLUTION.md` describes that walk-to connective tissue, not the locked Teleport/
  Deploy loop.
- **Hero / party fortress-infiltration — NOT the control model.** The concentric / gauntlet /
  enclave "flagship fortress" fantasies in `docs/RAID_PILLAR_VISION.md` are **CONTENT LAYOUTS for
  bases** — i.e. `RaidBaseGenerator` layout presets for deploy targets — **not** a player
  micro-combat / infiltration mode. You deploy troops and watch; you do not walk a hero through
  the fortress.
- **Deterministic-sim-first — DEFERRED to V2.** The `RaidSim` fixed-point authority + snapshots +
  server re-sim are the PvP-era backbone, not the PvE ship (see §2 V2).

---

## 4. Naming note for the owner — "Obsidian" is overloaded (do NOT rename in code)

The name **"Obsidian"** is used for at least three unrelated things:

1. the **Blink UI pack** (UI chrome),
2. a **wall tier** (Stone/Obsidian in raid base configs),
3. the **WO-773 common job queue** (`ObsidianQueueService`).

**Recommendation (owner call):** keep the code name `ObsidianQueueService` **internal**; use a
**player-facing "Builders" / "Training queue"** label in any UI so players never see the
overloaded term. This is a **flag for the owner** — do NOT rename anything in code without the
owner's ruling. (Same note carried in `docs/qa/WORK_ORDER_773_obsidian_queue.md`.)

---

## 5. Doc map — where the supporting raid docs sit under this northstar

| Doc | Role under this northstar |
|---|---|
| `docs/qa/WORK_ORDER_771_raid_system.md` | **The build plan.** V1 spine above = its critical path; 771.2/.3/.7 are V2. |
| `docs/qa/WORK_ORDER_773_obsidian_queue.md` | The common timed-job queue (Train channel + all timers). |
| `docs/RAID_PILLAR_VISION.md` | ⚠ PARALLEL/V2+ fantasy — reframed as `RaidBaseGenerator` LAYOUT PRESETS. |
| `docs/ARENA_SOLUTION.md` | ⚠ RETIRED walk-to loop — the "~70%" figure is walk-to tissue, not this loop. |
| `docs/RAID_TROOP_UI.md` | The Barracks / Raid-select / Deploy UI (code-built uGUI via ElarionUiKit). |
| `docs/raids/coc-raid-system-design.md` | Design reference; V1 presentation = real-time combat, authority sim is V2. |

*Live raid anchor 2026-07-26. Raid loop = Teleport/Deploy; V1 = generated bases + real-time
combat + deploy UI + barracks/train + stars/loot; deterministic sim / async PvP / server re-sim
= V2. Build plan = WO-771 v1.*
