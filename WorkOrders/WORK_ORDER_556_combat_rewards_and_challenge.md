# WORK ORDER 556 — Combat Rewards & Challenge

**Status:** IMPLEMENTED (CLI to batch-gate + commit)
**Lane:** Combat/AI + UI (file-disjoint silo, see §9). Owns loot-tables.json.
**Author:** architect agent (worktree agent-a4083bcef307bf498)
**Date:** 2026-06-28

Builds on WO-505 (star/timer/reward-scaling logic — already gate-verified). Does NOT
reinvent it. Four items: a real victory summary screen, a rare boss challenge, boss-only
gem loot, and star-scaled gear-drop odds.

---

## ITEM 1 — REAL VICTORY SUMMARY SCREEN (promote the 2.5s banner)

The old `BattleArenaHud.ShowResult` flashed a centre banner with ASCII stars (`* * -`)
and auto-closed after 2.5s; `BattleArena.Resolve` scheduled the masked home-return
immediately and independently. Promoted to a real summary:

- `GrantWinReward` now **returns** the granted totals as a `BattleRewardSummary`
  (xp / wisdom / wood / iron / gearName) instead of `void`.
- `BattleArenaHud.ShowResult` rebuilt to use the shared Obsidian chrome
  (`ElarionUiKit.BuildObsidianPanel`): title, a star row (filled/empty star glyphs,
  replacing ASCII), battle TIME taken (M:SS), an itemized reward list, and a **Continue
  button** that fires the deferred home-return.
- The WIN return is **deferred** until Continue (or a ~20s softlock-guard auto-timeout).
  The hero stands in the arena over the dead family while reading the summary. The LOSS
  path is unchanged (banner + immediate recoverable return — its post-loss grace timing
  is preserved).
- Softlock guard: if a WIN has no HUD (build failure), `Resolve` runs the return
  immediately so the hero can never be stranded at the far arena.

Files: `BattleArena.cs` (Resolve rework, GrantWinReward return, `BattleRewardSummary`
struct, SUMMARY FlowTrace), `BattleArenaHud.cs` (rich ShowResult + Continue/timeout).

**Verify:** `ArenaCombatOracle` drives `ResolveForTest` and now also asserts the
permanent `SUMMARY xp=.. wisdom=.. wood=.. iron=.. gear=..` FlowTrace line (proves the
totals were captured and handed to the view). Headless can't render the uGUI panel; the
panel build is felt-verified by the PO.

---

## ITEM 2 — RARE BOSS (~5%) joins an arena fight

`BattleArena.BeginEncounter` now rolls `BossSpawnChance` (named const = **0.05**) and, on
a hit, appends `BossEnemyId` (named const = **"orc-warlord"**) to the staged family. The
roll is FlowTrace-instrumented (`BOSS ROLL chance=0.05 rolled=<x> -> <added/none>`) so the
rate is provable from the break-log / Editor.log.

- Boss model: id **"orc-warlord"** resolves through `EnemyFactory.ModelForEnemy` →
  **"Orc_Necromancer"**, a VERIFIED file `Assets/Resources/Enemies/Orc_Necromancer.fbx`
  (the existing outpost raid-boss model — heaviest orc silhouette). No capsule fallback.
- `BattleArena.BuildEncounterDef` gains a boss branch (id contains "warlord"/"boss"):
  high HP (520·t), big contact damage (34·t), height 2.6 (reads big), bumped XP/glimmer.
- `SpawnFamily` family clamp widened 1..6 → **1..7** so the boss can join a full family.
  Boss role = DPS (RoleForId → Flanker tactics); it is an extra member, not the leader.

Chose a GROUND boss (orc-warlord) over the flying `DragonBoss`: the Dragon flies its own
kinematic orbit and does not path the kite-arena navmesh, so it would not engage the hero.
The orc-warlord is a navmesh-walking `Enemy` like the rest of the family.

---

## ITEM 3 — BOSS-ONLY GEM LOOT, low rate (owner decision)

**Schema decision:** `loot-tables.json` had NO per-drop boss/rarity gate (`source` was
documentation-only). Added a **minimal flag** `bossOnly` (bool, default false) to each
drop line (`LootDropLine.BossOnly`). A `bossOnly` line only rolls when the kill is a BOSS
kill.

- `LootTableCatalog.Roll(tableId)` → new overload `Roll(tableId, bool includeBossOnly)`.
  Default `Roll(tableId)` keeps `includeBossOnly = false` (back-compat for ordinary kills).
  A `bossOnly` line is skipped unless `includeBossOnly` is true. FlowTrace fires when a
  bossOnly gem line is awarded (`LOOT bossOnly gem '<id>' x<n> dropped`).
- `ItemDropWatcher` now decides boss-ness **data-drivenly**: a kill rolls with
  `includeBossOnly = (resolvedTable.Source == "boss")`. This covers BOTH the `DragonBoss`
  path (always boss) AND a boss-tier `Enemy` (orc-warlord) whose table is `source:"boss"`.
- Gems = the EXISTING crystal ingredients `ing_ember_crystal` / `ing_aether_shard` /
  `ing_heartstone_crystal` (materials.json — no new gems authored).
- `loot-tables.json` (Resources + StreamingAssets, kept identical):
  - Added an **"orc-warlord"** table (`source:"boss"`) — currency-ish mats always, plus
    the 3 crystal gems as `bossOnly` low-rate drops (0.30 / 0.22 / 0.12).
  - Marked the existing crystal-gem lines on the V1 **orc enemy** tables (orc-berserker,
    orc-shaman, orc-necromancer, orc-warrior) as `bossOnly:true` so ordinary orc mobs no
    longer trickle gems (matches the owner "boss-only gems" decision). Their non-gem
    ingredients (ironroot, oil_flask, etc.) are unchanged.
  - The `necromancer` boss table (`source:"boss"`) keeps its gems (now rolled as proper
    boss drops).

GEAR is NOT a loot-table concept (tables drop materials into the larder). Gear drops are
the arena reward path (`GrantWinReward` → `TryGrantArenaGear`) — see ITEM 4.

**Caveat (noted, not fixed):** arena enemy deaths roll loot via the scene `ItemDropWatcher`
and (with `UseWorldPickups`) spawn a pickup mote at the death point — which is the far
arena (~5000,5000) the hero leaves on win. This is pre-existing for ALL arena drops, not
introduced here. A follow-up could route arena boss gems through `GrantWinReward` (larder
deposit) so they are always collected.

---

## ITEM 4 — STAR-SCALED GEAR-DROP CHANCE

`TryGrantArenaGear` now takes the star count; the drop chance gets a per-star bonus
(`GearDropPerStar = 0.10`): 1★ +0.00, 2★ +0.10, 3★ +0.20 on top of the threat curve
(base 0.30 + 0.05·threat), clamped to 0.85. So a faster, cleaner win has better gear odds.
All knobs are named consts (owner-tunable).

---

## CAVEAT — two combat paths

- **Arena** (overworld BattleArena) — the star/summary path. **Wired here.**
- **Raid/outpost** clear reward — actually `EnemyOutpost.GrantClearReward()` (the prompt
  said `RaidOutpostSystem.GrantClearReward`; the method lives in `EnemyOutpost.cs`). It
  already has its own threat-scaled loot (resources / crystals / gear / rare-gem) and a
  StringBuilder summary that is only LOGGED. **Follow-up:** give the raid clear the same
  Obsidian summary screen (not done here — out of the arena scope, and it has no star
  rating to show).

---

## Owner-decision flags (tune freely — all named consts / JSON)

| Knob | Where | Value |
|---|---|---|
| Boss spawn rate | `BattleArena.BossSpawnChance` | 0.05 |
| Boss id/model | `BattleArena.BossEnemyId` → Orc_Necromancer | "orc-warlord" |
| Boss stats | `BattleArena.BuildEncounterDef` boss branch | hp 520·t, dmg 34·t |
| Summary auto-timeout | `BattleArenaHud.ShowResult` autoTimeoutSeconds | 20s |
| Gem drop rates (boss) | loot-tables.json orc-warlord | 0.30 / 0.22 / 0.12 |
| Ordinary mobs drop gems? | loot-tables.json `bossOnly` | NO (gated) |
| Star→gear bonus | `BattleArena.GearDropPerStar` | +0.10 / star above 1 |

**Economy note for the orchestrator:** gating gems behind bosses (5% of fights) sharply
reduces the ordinary gem supply the Jeweler agent consumes. This is the owner's stated
"boss-only loot" decision and is one-flag reversible (`bossOnly`), but flagging it so the
PO can decide whether the Jeweler needs a second gem source (e.g. a shop/quest).

---

## Acceptance criteria

- [x] Victory summary: Obsidian panel, sprite/glyph stars, time, itemized rewards, Continue.
- [x] WIN return deferred to Continue / 20s timeout; LOSS unchanged; no-HUD softlock guard.
- [x] ~5% boss roll, FlowTrace-instrumented, named const; resolves to a real model.
- [x] `bossOnly` loot gate; boss kills roll gems; ordinary orc mobs do not.
- [x] Star-scaled gear chance.
- [x] Resources + StreamingAssets loot-tables identical.
- [x] Brace-balanced .cs; valid JSON.
