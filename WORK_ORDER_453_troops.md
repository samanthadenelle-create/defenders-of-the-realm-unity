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

## ✅ DECIDED (owner 2026-06-14) — leveling depth = (B) Expendable + light veterancy
**Locked: (B).** Troops are expendable ammunition you invest + risk, NOT XCOM soldiers you mourn. Clean
emotional contract ("lost the squad, train more" = motivating, not punishing), lowest day-one
complexity/balance risk, and it keeps the persistent power fantasy where it belongs (hero + companions +
Echoes). **Light veterancy** (+dmg/survivability per raids-survived or star rating, ~+30% ceiling) gives
progression feel without permadeath sting. NOT a hybrid (hybrid = downsides of both). Deepen toward
persistent (A) ONLY post-grant if data shows players want per-unit attachment. *Ship clean, then expand on
what resonates.*

## ✅ DECIDED — pull the thin slice forward (pre-listing). GREENLIT.
Highest-ROI move: turns the weakest demo moment (solo-hero raiding) into the strongest pillar, gives real
playtest data on the core verbs immediately, very high reuse, and is a tangible "we have raids" milestone
before the grant push. **Slice = Footman + Archer · deploy + rally-flag + Retreat · cap 10 · 1–2 raid
targets (RaidBaseGenerator).** Everything else (Mage/Healer, air/AA, full cap, SKR rewards, defend mode,
deep veterancy) stays post-grant.

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

## AI Fortress & Troop Scaling (relayed design — validated + connected to shipped systems)
Philosophy: raids feel like a *thoughtful enemy base*, difficulty from **smart layout + appropriate counts**,
predictable progression **Regular → Hard → Extreme**. Most of this is ALREADY scaffolded:

**Difficulty tiers = the EXISTING scene-config garrisons** (just label + enrich):
| Tier | Existing config | Base style | Walls | Towers (archer/mage) | Layout (RaidBaseGenerator) |
|---|---|---|---|---|---|
| **Regular** | `raider_camp_small` | Raider camp | Wood | 4 / 0–1 | single ring (PerimeterWallGenerator) |
| **Hard** | `fortified_garrison` | Fortified outpost | Wood+Iron | 6 / 1–2 | concentric (Iron Bastion) |
| **Extreme** | `mage_enclave` | Mage/Dragon enclave | Iron/Steel | 8 / 2–3 + traps | enclave: AA ring + shielded core |

**AI troop scaling (controlled + readable):**
- **Level** = `max(baseEnemyLevel, playerAvg + offset)` — **already shipped** in the scene-config consumer.
- **Count** = base (driven by player power: avg troop lvl + hero lvl + army size) × **difficulty multiplier**:
  Regular 0.7–1.0× · Hard 1.2–1.6× · Extreme 1.8–2.5×.
- **Role balance** = smart, not all-archers — **reuse `WaveCompositionBuilder` family pools** (shipped tonight):
  Regular = basic archers+warriors; Hard = balanced + 1–2 elites; Extreme = role synergy (warriors tank, mages AoE) + more casters.
- **⚠ Perf ceiling (mobile/WebGL/Seeker):** Extreme's 30–45 enemies + the player's cap-30 army + towers =
  75+ combat units. That WILL hurt on Seeker/WebGL. **Cap total live combatants** (LOD/pooling, or scale
  Extreme counts down on mobile) — this is why the player army caps at 30. Tune Extreme to the perf budget, not the fantasy.

**RaidTemplate (the data form):** the relayed `RaidTemplate` SO = our **enriched scene-config** (layout +
troop-composition template + **1★/2★/3★ timer targets** + **reward table**). Decision: keep them as the
existing **CanonicalJson scene-configs** (WebGL-safe, already wired + player-level-scaled) OR mirror to SOs
per the owner's preference — recommend **extend scene-configs** (don't fork a parallel template system; it
already does ownership + scaling). Pool of 8–12 that unlock as the player progresses.

## Echo Bond Shards — the 3★ prestige chase (POST-GRANT; spec now, NOT in the slice)
The 3★ reward that makes mastery mean a *permanent* companion, not just resources. **Extends the shipped
`Pet.cs` bond system (ranks 0–4)** — shards feed bonding; do NOT fork a parallel pet system. Keeps the
identity line crisp: **finite = troops, persistent = Echoes.**

- **Acquisition (skill-only — NO pay-to-win):** shards drop **only on 3★** clears (~15–25% base, tunable),
  scaled by raid difficulty (Extreme = better rate + rarer shards), with a **pity timer** (guaranteed shard
  every ~8–12 same-raid 3★ clears). Premium currency may speed *training time*, **never buy shards.**
- **Shard rarity:** Common / Rare / Epic / **Signature** (legendary-Echo-specific). Shards are **Echo-specific
  or Wild** (apply to any Echo).
- **Bonding curve:** ~40–80 shards to fully bond one Echo. Progressive: **25% → passive bonus · 50% → active
  ability · 100% → full power + cosmetic/personality.** Weeks/months to fully bond the rarest — generous
  per-session progress, chase-worthy total.
- **Troop ↔ Echo synergy:** troops that SURVIVE a raid grant a small bonus shard — a light, lovely thread
  tying the finite force to the persistent one (and it rewards the Retreat verb).
- **The loop:** get good at raids → earn shards → stronger Echoes → better raid performance. Self-reinforcing
  prestige.
- **Example Echoes (incl. the family thread — keep these):** **Train Echo** (from a child's drawings —
  tanky, taunt), **Big Boy 4014** (train-themed legendary — the family win), Storm Mage (AoE support),
  Ancient Dragon Hatchling (rare, breath attack). A collection UI with per-Echo bond progress bars.
- **Scope:** the prestige/retention layer — **post-grant**, after the troop slice + the Echo pillar build.
  NOT in the pre-listing thin slice (which is Footman+Archer+deploy/rally/retreat). May graduate to its own
  WO when the Echo pillar is built; lives here now as the raid 3★-reward contract.

## Troop Training, Barracks & Loss Model (owner troops.txt 2026-06-14)
**Training = Barracks queue + resource + time** (reuse `BuildTimerService` offline clock + `EconomyService`):
select type + quantity (1–10, ≤ army cap) → pay resources **upfront** → enter the queue (Barracks has 1–3
**concurrent slots**, upgradeable) → trains in real time **even offline** ("Your troops are ready" on login).
**Batch bonus:** 5+ of the same type = 10–20% faster (rewards real squads).

**Offline training ("Accelerated Idle" — owner offline-training.txt):** the queue keeps progressing while
away — **reuse `BuildTimerService`'s timestamp/offline-elapsed mechanism** (it already does offline build
clocks), and add the troop-training tuning ON TOP: **8-hour cap** + **~60–70% offline rate** (NOT 100% — so
logging in regularly is rewarded), **diminishing returns** (after ~4h drop toward 40%). On login: advance
`effectiveTime = min(Now − startTime, 8h) × offlineRate`, spawn finished troops, "Your Barracks trained X
troops while you were away!" notification. Later deepeners: Barracks tier raises offline efficiency (→90–100%);
crystals/SKR instant-finish or +offline-cap; veterancy trains slightly faster; 3★ "War Economy" buff boosts
offline speed 4–8h. Client-timestamp for MVP (clock-scum is a minor mobile risk, server-validate if/when
backend lands). **Verify `BuildTimerService` exposes the start-timestamp + an offline-elapsed hook** when the
training queue is built (it's the offline spine).

**Train times (MATCH the committed `troops.json` `buildSeconds`):** Footman 30s · Archer 45s · (post-grant:
Mage 90s · Healer 75s); Barracks upgrades cut these (~half at max).

**Unlock ladder (Step 2+):** Footman = Barracks L1 (immediate) · Archer = Barracks L2 **or** Player L4 ·
Mage = L5 / PL8 · Healer = L7 / PL12. **Alt hook:** unlock via raid stars (e.g. 50 total 3★ → Mage) — ties
roster growth to raid skill. (Slice ships Footman + Archer; the rest is the post-MVP roster.)

**Raid-only crafting (hybrid, post-slice):** normal Barracks training (reliable, slow, common Wood/Food) +
**War Spoils** buff after a 3★ clear (30–60 min: **50% faster training · 30% cheaper · chance to craft a
veteran troop**). Special "Raid-forged" recipes (e.g. Battle-Hardened Archer) cost raid loot + resources.
Loop: raid well → better/faster troop production → stronger raids.

### ✅ Loss model — DECIDED (owner 2026-06-14): WOUNDED-RECOVERY (no permadeath)
A downed troop **returns to `ArmyStorage` wounded** (reduced HP/XP), **full recovery takes time/resources**
— *no permadeath*. Light veterancy: **+5% dmg per survived 3★, capped.** Intentional softer-than-CoC,
mobile-friendly stakes: you never truly lose an army, you pay **downtime** — and the **Retreat** verb lets
you avoid even that by pulling survivors out. (Reviewer's harder "expendable-loss" (B) was considered + set
aside in favor of feel.) Implementation: `ArmyStorage` MARKS the `PlayerTroop` wounded (never deletes) on a
raid loss; the slice's combat `Die()` just despawns the battlefield instance.

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
