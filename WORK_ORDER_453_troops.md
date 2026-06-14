# WORK ORDER 453 — Offensive Troop System (training, leveling, deploy)

**Status: SPEC — owner template + relayed XP design + creative review, synthesized.** P1, Lane
Combat/Economy/Progression. Owner: Samantha. Supersedes the looser `docs/TROOPS_PILLAR_SPEC.md`.
⚠ **WO-number note:** the desktop template called the base builder "WO-452," but the board's WO-452 =
the build-palette bug. Troops = **WO-453**; the base builder is `RaidBaseGenerator` (shipped tonight).

## BLUF — build the cheap 20% before listing, spec the rest
A solo hero walking into the Iron Bastion does NOT sell the Clash-meets-Warcraft North Star. The
**pull-forward slice** (mostly reuse) that makes raids demo-worthy:
1. **Two units** — Footman + Archer, as `type:"troop"` catalog entries (faction-flipped Enemies),
   trained via the shipped `BuildTimerService` + `StructureFactory`. Cap 10.
2. **Two verbs in RAID mode only** (2-faction, arena-proven path) — **Deploy point** (tap a wall/gate
   to drop the army) + **Rally flag** (one banner the whole army re-targets — the ONLY Warcraft verb).
3. **Retreat-with-recovery** — end a raid as a loss but keep surviving troops. This single verb *is* the
   finite-army feel, and it's demoable in one sentence.
**Defer post-grant (spec'd below, not built yet):** air/AA, siege, Banner-Captain, Defend-mode garrison
(the 4-faction AI scope risk), the full 30-cap curve, SKR-on-raids.

## What already ships (reuse, do NOT greenfield — review-verified)
`BuildTimerService` (offline build clock) · `DefenseTower.TowerAllegiance`/`GarrisonController.ArmGarrisonTurrets`
(faction-flip combat seam) · `EnemyBrain` role targeting (not hero-hardcoded) · arena PvE + `ArenaWalletService`
SKR loop (clean async swap) · `Pet.cs` (bond ranks 0–4, on-demand combat) · `RaidBaseGenerator` (raid bases)
· `GameState.PartyMemberIds`/`ResourceBalance`. **Needs adding:** a `troop` `CatalogType`.

## Core loop
Build/upgrade **Barracks** → **Train** (resources + time, or crystals/SKR instant) → **Store** (finite army
cap) → **Level** (raid/arena XP) → **Deploy** in raids + arena (offensive-only at launch — no Defend yet).

## Decided design (from the creative review — positions, not options)
- **Echoes ≠ troops (keep separate).** Troops = finite, expendable, faction-flipped Enemies. Echoes =
  persistent, leveling hero-pets (1–2 deployed, don't permadie) — reuse `Pet.cs` as-is. **Finite = troops;
  persistent = hero + companions + Echoes.** The **3-star raid drop grants an Echo bond-shard, NOT a troop**
  (a permanent, growing chase beats expendable loot).
- **Control = CoC deploy + 2 global verbs.** No per-unit micro (mobile-first). Deploy-point + Rally-flag.
  Troops auto-fight via `EnemyBrain`; hero+companions stay directly driven. Rally + Retreat ARE the mid-raid
  interaction loop — without them raids are a slot-machine pull (the **#1 fun risk**).
- **Finite-loss feel = Veterancy + Retreat, NOT insurance.** Survivors gain ranks (+10%/rank, max +30%) so
  your *standing army* is worth more than its rebuild cost — that's the earned sting. **Retreat** saves
  living troops at the cost of stars (knowing when to cut losses = the Warcraft read). Rebuild = cheap
  resources + meaningful time (~15–20 min via `BuildTimerService`, or crystals). Base/Echoes/hero levels
  never lost.
- **Scout report + soft RPS (the missing mechanic that makes comp matter).** Pre-raid Scout screen reveals
  the target's defense profile (wall tier, AA density, choke vs open, boss); soft counters: walls→siege,
  air-light→Sky Raider, open→ranged, choke→melee+banner. Each flagship raid already implies its counter.
  The interesting decision happens at army-select, before deploy. Ties defend↔raid↔arena via one comp literacy.

## ⚠ THE ONE OPEN FORK FOR THE OWNER — troop leveling depth
Your WO-453 template + the relayed XP design give **troops individual XP bars + deep leveling** (a
persistent RPG army). The review argues troops should be **expendable fodder with only light veterancy**,
with the *deep* persistent leveling living in Echoes (so "finite vs persistent" stays a crisp one-sentence
line). These genuinely conflict — it's the make-or-break identity call:
- **(A) Persistent leveling army (your template):** troops level individually over many raids; losing a
  level-10 troop is a heavy, XCOM-like permadeath. Deep investment, high stakes, more UI/save complexity.
- **(B) Expendable + veterancy (review):** troops are ammunition with a light +30% veteran ceiling; deep
  growth is the hero/companions/Echoes. Cleaner finite line, less sprawl, leans CoC.
- **Recommended hybrid:** **(B) for the launch slice** (keeps finite crisp + cheap to build), with a
  *shallow* 3-rank veterancy that reads like "leveling" — then, if the persistent-army fantasy proves the
  fun, deepen toward (A) post-grant. This honors your template's leveling intent without the permadeath
  rage-risk on day one. **Your call.**

## Star scoring (reuse the existing countdown timer)
**Stars = clear-time thresholds, tracked by REUSING the existing wave/raid countdown timer**
(`WaveManager` countdown / the same timer the HUD already shows) — no new timer system. Raid spawns the
target, the countdown runs up (or a budget runs down); clear under the 3★ time = 3 stars, under 2★ = 2,
any clear = 1. One timer, three loops (waves, raid stars, ATB turn pressure already share the pattern).

## XP (relayed design, reconciled to the fork above)
Primary = **Raid completion, scaled by stars** (1★ base · 2★ +50–75% · 3★ +150–200% + Echo-shard chance).
Secondary = arena wins. Bonuses: time-survived, kills/%-damage, squad-synergy. **Survivors full XP; downed
troops partial (50–70%)** so harder content isn't over-punishing. Light idle/passive (5–10% of active).
Under fork (B) this XP feeds *veterancy ranks*; under (A) it feeds individual troop levels.

## Air ↔ AA (deferred build, numbers locked for when it lands)
Air = a **key, not a wrecking ball**: HP 120 / DPS 15 (low) / 4 slots; flies over Wood/Iron walls but AA
towers + keep see it. AA tower 40 DPS @ 18m (kills lone air ~3s). Ranged troop = soft AA (12 DPS vs air).
Air's job = kill AA + back-line so the *ground* army walks in (combined-arms). Applied symmetrically, the
enemy **Dragon** becomes answerable with ~2 AA towers OR ~4 ranged — closes the original "dragon ignored
towers" bug.

## Army economy (v1 to react to)
Cap **10** at Barracks T1 → **16 / 22 / 30** at T2/T3/T4 (rising Wood/Iron + 10m/45m/3h timers). 30 caps it
for WebGL/Seeker perf + raid readability.

| Unit | Role | Slots | HP | DPS | Cost W/I/F | Build | Notes |
|---|---|---|---|---|---|---|---|
| **Footman** | Melee fodder | 1 | 100 | 12 | 40/10/0 | 30s | What you spend |
| **Archer** | Ranged + soft-AA | 1 | 60 | 24 (12 vs air) | 30/20/0 | 45s | Comp staple |
| **Battering Ram** | Siege | 3 | 250 | 60 (walls only) | 60/120/0 | 4m | Wall-cracker; the "bring siege?" call |
| **Sky Raider** | Air | 4 | 120 | 15 | 80/80/40 | 6m | Premium bypass (kills AA/back-line) |
| **Banner-Captain** | Support | 2 | 140 | 8 | 50/30/30 | 2m | Aura +15% HP; the Rally flag drops here |

(Launch slice ships only Footman + Archer; the rest are the post-grant roster.)

## SKR-on-raids (deferred; framing locked = skill, not gambling)
Fun only if staked on the read: (1) wager raids MUST show the Scout report before staking; (2) Retreat
saves troops but **forfeits the bet on any non-clear**; (3) **you can't wager a raid you haven't already
beaten un-wagered** — converts it from a gamble into "I've proven this, now I race it for stakes."

**Staking BONUSES (owner 2026-06-14 — make staking attractive, drive SKR utility):** staking SKR on a
raid isn't just risk — a cleared staked raid pays **bonus rewards on top of the pot**: a resource/XP
multiplier (e.g. +25–50%) and **improved Echo-shard odds**, scaled by stake size + stars (3★ staked =
the top payout). So the wager is a *high-stakes mode* you opt into for richer loot, not a side bet —
rewards the skill read (scout + comp + clean clear) with the best rewards in the game. Stake-gated behind
a dry clear (rule 3) keeps it skill-not-gamble. Reuses the `ArenaWalletService` Debit/Credit seam.

## Data structures (owner template — refined)
`TroopDef` (ScriptableObject: id, prefab, role, base stats, train cost/time, level/veterancy data) —
SOs are WebGL-safe (Resources-loaded); match the existing catalog convention, not a parallel path.
`PlayerTroop` (runtime/saved: def ref, rank/level, XP). `ArmyStorage` (saveable: ownedTroops,
maxArmySize, Train/LevelUp/GetDeployable). Persist via the existing SaveSystem.

## Implementation order (slice-first)
1. `troop` CatalogType + Footman/Archer `TroopDef`s (faction-flipped Enemy combat reuse).
2. `ArmyStorage` + persistence (cap 10).
3. Barracks build/train (reuse `BuildTimerService`).
4. **Raid deploy: Deploy-point + Rally-flag + Retreat-with-recovery** (RAID mode, 2-faction).
5. Star-scaled raid XP + shallow veterancy.
6. Scout report + comp tags.
*— pre-listing slice ends here —*
7+ (post-grant): air/AA, siege, Banner-Captain, Defend garrison, 30-cap, SKR-on-raids, deepen leveling if (A).

## Acceptance (slice)
Train 2 troop types at a barracks (resource+timer); deploy a cap-10 squad into the Iron Bastion; deploy-point
+ rally + retreat all work; survivors persist with veterancy + partial XP on the downed; clear gives
star-scaled XP + resources; AutoPilot fleet gets troop-deploy assertions.
