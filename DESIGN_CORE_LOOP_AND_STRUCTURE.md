> ⚠ **SUPERSEDED — Defend-the-Tower / PatriciaLight was REMOVED 2026-06-09; not a live system.** Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` (single-Knight overworld BattleArena; ATB flat/separate).

# DESIGN — Core Loop & Progression Structure

**Status:** CANONICAL (owner-driven, 2026-05-31). This is the spine every world/economy/progression WO serves.
**Read alongside:** `ORCHESTRATION_LIVE.md` (execution) · `PIPELINE.md` (dispatch).
**Origin:** owner design session 2026-05-31 — "fun first." Defend-the-Tower as tutorial → open world → outposts feed a single upgradeable home → boss escalation.

---

## 1. The spine (one loop)

> **Tutorial → expand → harvest → pipe home → upgrade ONE seat → beat the boss → unlock the next, richer, deadlier region. Repeat. Offline, it keeps ticking.**

1. **Defend the Tower** teaches combat in a sandbox (move, hero attack, pet engage/repair, tower, survive a wave). Win/lose, contained.
2. **Venture out.** Ward-tether (WO-112) expands how far you can safely reach — soft guardrails + pacing.
3. **Plant self-sustaining outposts** on resource nodes. Cheap, repeatable, disposable.
4. **Outposts auto-harvest and pipe resources home** (workers/pet/offline).
5. **Spend at ONE place** — the central seat of Elarion, which upgrades through visible tiers.
6. **Each tier hardens defenses and gates the next region + boss.**
7. **Beat the boss at the seat** → unlock the next tier of world (richer nodes, deadlier raids).

---

## 2. The structural anchor — the central tiered seat ("the Town Hall pattern")

Borrow Warcraft/CoC: **one central building whose TIER is the master gate** for everything. Raising it unlocks new buildings, higher upgrade tiers, new defenses, new regions, the next boss.

**In Elarion this is the Heart of Elarion itself** — already at center (0,0,0), already the defend-target and lose-condition. We **unify three roles into one object**:
- the thing you **upgrade** (the sink),
- the thing you **defend** (the stake),
- the thing the **boss attacks** (the test).

One spot. One silhouette that visibly grows as you progress — that's the "natural lineage." Every resource on the map has one destination; progress is legible at a glance.

**Why one sink, not many:** concentrating investment makes growth readable and keeps outposts cheap/disposable, so losing one to a raid stings without erasing progress. The seat is your savings account; outposts are income.

**Canon flag:** no "Keep" (retired, DESIGN-DECISIONS #3). Tier names need Elarion flavor — *owner's creative call*. Placeholder idea: tie tiers to **Stone Choir restoration** (each tier restores more of the chord / lights another Chorister).

**Existing machinery (not greenfield):** WO-151 (level village → unlock buildings, Warcraft-style) is the tier system's home. WO-137/148/149 (catalog data model, structure factory, base persistence) are the catalog the tier gates. WO-114 (wall tiers) + WO-181 (rampart siege defenses) are tier-gated upgrades.

### 2a. Walls = the CoC sink, gated by the seat

The seat's primary CoC tie-in is **walls**. Seat tier pulls two levers (both straight from Clash):
- **Tier cap** — max wall material/HP you can upgrade to (WO-114: wood → stone → reinforced), unlocked by seat level.
- **Segment budget** — how much wall you're permitted to place at all. Higher seat = stronger walls *and* permission to enclose more.

Walls are deliberately the **deepest, slowest resource sink** — what soaks surplus once everything else is maxed, giving the late game something to spend on. They radiate from the one spot. They're not cosmetic: walls shape the lane the boss/raiders must break through, which is what makes WO-110 (siege / wall breach) and WO-181 (rampart siege defenses, firing from the wall tops) matter — the wall is what the boss chews through while your garrison + towers fire down.

**DECIDED (owner 2026-05-31) — wall building is PER-PART / per-brick.** In open-world build mode, per-piece placement = player creativity (every fortress is unique, expression is half the genre's fun) and the **"I only need a few more bricks" micro-goal** that pulls players back to harvest. "Bricks" = a concrete material the stone economy feeds; each segment is individually placed + upgraded → the deep long-tail sink. Cleanest spot for an *optional* top-up later (WO-118 rewarded ad / WO-172 speedup: "watch an ad for the last few bricks" reads as help, not a shakedown — fits fun-first). Feeds WO-108 (build-mode creativity) + WO-114 (per-part wall tiers).

---

## 3. What a tier gates (tech-tree sketch — placeholder values, tune later)

| Seat tier | Wall | Towers unlocked | Outpost types | Region opened | Boss |
|---|---|---|---|---|---|
| T1 (post-tutorial) | Wood | Basic arrow tower | 1 (wood node) | Home marches | First wave-boss |
| T2 | Stone | + 2nd tower type, rampart access | + crystal node | Mid region | Tougher boss |
| T3 | Reinforced | + arcane tower, **siege defenses (WO-181)** | + rare/risk node | Deep region | Apex (dragon) |
| T4+ | … | … | … | … | … |

Each tier: costs resources (banked from outposts) and may require **the prior boss defeated** to unlock. Tier raises the build catalog's available items (WO-151 ⇄ catalog).

---

## 4. The outpost model

**claim → build (cheap, via build mode WO-108) → auto-harvest (WO-117 workers, WO-119 pet) → pipe to EconomyService ledger (WO-131) → accrue offline (WO-115).**

Each outpost is a small claimable site with two functions: a **harvest** output (wood / crystal / food, by node type) and a **garrison** of defenders.

### 4a. Food-gated garrisons (the supply/upkeep mechanic) — the Warcraft hook

Defense is built from **small garrisoned squads** (the unit of allocation), not loose soldiers. A squad = a few AI actors that move and fight as one — literally WO-146's leader/follower formation, running WO-145 tactics + WO-147 perception. "Garrison a squad" = park a formation at a site to engage raiders.

**The number of squads you can field is capped by FOOD.** Total food income → how many squads stand across the *whole map* at once. Per-squad food cost; garrison capacity measured in **squad slots** (seat tier raises home slots; outpost level raises its own). Small sites hold 1–2 squads; the Heart holds several (for boss fights).

This converts the "self-sustaining curve" into a **player allocation choice**: squads are food-limited, so you can't fill every slot. Spread thin across all outposts, or stack the rich/exposed frontier and leave the safe ones bare. **That allocation is the strategy.**

- Food gets a real job: food nodes / granaries (WO-180) directly **buy reach** (more squads → defend deeper, richer regions).
- **Barracks** (production roster, WO-180): train squads **at the seat**, then march/assign them out — reinforces the hub (army is *made* at the one spot, deployed outward).
- **Squad type/role (optional depth):** spear / archer / shield-line, so stationing becomes a counter-pick vs what raids that region (makes WO-145 raids strategic, not a numbers check). Uniform = simpler; typed = where the strategy lives.
- **Optional squeeze:** the seat needs squads for boss fights too. If home + outposts share one food cap, **pre-boss you may pull squads home to hold the Heart, exposing the frontier** right when raids peak.

### 4b. Self-sustaining curve — value gradient (DECIDED)

**Risk = reward.** Safe/poor nodes are calm passive income — garrison light, mostly idle. **The richest veins are heavily patrolled by large enemy swarms:** claiming one means fighting through the swarm, and *holding* it demands a serious garrison (multiple squads, near your food cap). When raids out-scale the garrison the player chooses: **reinforce, send the pet, or let it fall and lose the income.** The high end is where the "come back now" tension lives; the low end keeps the baseline calm. (WO-155 region spawns, WO-160 raids, WO-144 risk tiers, WO-164 ThreatLevel, WO-145 swarm tactics.)

---

## 5. Risk/reward gradient

Distance from the seat = **richer resources + deadlier raids + tougher region.** The map is a difficulty gradient radiating out from home. The **richest veins are heavily patrolled by large swarms** — visible, scary, guarded prizes you grow into, not stumble onto. You push outward only as your squad cap + defenses can support it. (WO-144 regional crystal subtypes, WO-164 ThreatLevel, WO-107 climate regions, WO-145 swarm tactics.)

**DECIDED (owner 2026-05-31) — enemies by region + gentle-start curve.** The **monster family trees already exist** (`docs/MONSTER_FAMILY_ARCHITECTURE.md`, `docs/REGION_ENEMY_ROSTER.md`, `docs/enemy-codex.md`) — reuse them. **Region determines which family you face** (data-driven roster, WO-155). Difficulty is **challenging-but-not-brutal at the start, then tiers up** as you push outward / raise ThreatLevel (WO-164) — early regions are a fair fight, deep regions are the swarm-guarded grind. *Implication:* since each region throws a different family, **typed friendly squads earn their keep** (counter-pick the local family) — leans the squad-composition knob toward typed.

---

## 5d. Expedition, Casual Zones & Provisioning (owner 2026-05-31)

The world serves **two moods** — a calm check-in near home, and a committed run into danger. This fits a
mobile audience with mixed session lengths.

**MODEL — DECIDED: GATE the danger (don't rely on geography).** Rather than a seamless gradient where a
casual player could *wander* into roaming tribes, the dangerous content lives **behind a threshold into a
separate expedition area/map**. So "casual players are never exposed" is a **hard guarantee**, not a hope —
you're only in danger if you deliberately cross in.

- **Safe world (home + casual harvest band):** contiguous, walkable, relaxed harvest, nobody hunting you.
  The idle/check-in vibe. A casual player can live here entirely and never get burned.
- **Expedition map(s) (gated):** entered through a deliberate gate/portal after you **commit + provision**.
  This is where the **roaming tribes (WO-160 / WO-143)** patrol and the **rich, swarm-guarded veins** live.
- **Why gating wins here:** (1) hard casual-safety guarantee; (2) a clear "you're entering danger" threshold
  = readable + opt-in; (3) the gate is the natural place to **stock up before you go**; (4) **easier to build
  + more mobile-friendly** — a separate map is a simple scene load (Addressables-aligned), no hard seamless-
  streaming engineering needed yet.

**Reconcile with `ZONE_STREAMING_ARCHITECTURE.md` (the "Elden Ring seamless" north-star):** not a conflict —
the **safe world can still be seamless/contiguous**; only the **danger** is gated behind a threshold into its
own map. Hybrid: seamless safe hub-world + discrete, opt-in expedition maps. Streaming stays a later north-star
for when the safe world itself grows large.

**The provisioning loop (the point):** no resupply out in the expedition map, so you **stock at camp before
crossing** — food (squad upkeep on the road), healing, repair kits, ammo/mana. Going in unprepared gets you
caught. The threshold makes the journey a *decision*, not a dart-and-back.

**Forward camps / outposts** planted inside an expedition map double as **safe islands + resupply + reach-
extenders** (WO-159), and the **ward-tether (WO-112, "earn the range")** gates how deep you can safely operate.

**HAUL-LOSS DIAL — DECIDED (clean with gating):**
- **Safe world: zero loss, ever.** Casual players never lose anything.
- **Inside the expedition map: partial drop-on-death** — go down deep and you drop *part* of the carried haul
  where you fell, **recoverable on a corpse-run**. A real sting + a comeback, **never a total loss**. The
  risk-taker opts into this by crossing the gate; the stay-home player never touches it.

Maps to: WO-112 ward-tether (the gate/range), WO-160 wandering tribes, WO-143 roaming raids, WO-165 world
portals (the threshold tech), WO-159 forward camps, WO-164 ThreatLevel, WO-144 risk tiers, + food/squad-cap (§4a).

---

## 6. Boss escalation

Staged **at the seat** (upgrades + threats share one stage — you watch what you built get tested). Defeating a boss gates the next region/tier. Apex enemies + dragon boss already built.
**DECIDED (owner 2026-05-31): boss AUTO-FIRES when sufficiently prepped** — no manual "ring the bell." Better for a mobile passive game: the milestone comes to you when you've earned it, no required ritual. (Telegraph it clearly so the player can garrison/pull squads home first — ties to the shared-cap squeeze.)

---

## 7. Player journey (FTUE spine — maps WO-133)

1. **Defend the Tower (PROLOGUE)** — combat tutorial in a framed setpiece. *Not* your home — teaches the verbs, then ends.
2. **Arrive with nothing** — step into the open world; ward-tether grants initial safe range (WO-112). You found/claim a bare home plot and raise the first Heart-seat from scratch (ownership lands).
3. **First outpost** — plant on a nearby safe node + watch it harvest.
4. **First resources home → first seat upgrade → first walls** — the core dopamine + "few more bricks."
5. **First boss** at the seat.
6. **Loop opens** — more outposts, push toward the swarm-guarded rich veins, next region, escalate.

---

## 8. Open knobs (owner decisions)

1. **Seat tier naming / lineage** — creative (Stone Choir restoration?). — STILL OPEN.
2. ~~Self-sustaining curve~~ — **DECIDED: value-gradient.** Safe/poor nodes are calm idle income; **the richest veins are heavily patrolled by large enemy swarms** — claiming one means fighting through, and holding it demands a real garrison. Risk = reward. See §4b / §5.
3. ~~Boss trigger~~ — **DECIDED: auto-fires when prepped** (telegraphed), no manual ring. Mobile-passive friendly. See §6.
4. ~~Tutorial tower vs home~~ — **DECIDED: PROLOGUE.** The Defend-the-Tower tutorial is a framed prologue; the Heart-seat / home is built from scratch in the open world so ownership lands hard.
5. ~~Food model~~ — **DECIDED: hard supply CAP** (CoC/StarCraft-style), no upkeep drain. Reason: mobile — far easier to balance and read on a phone. Food income raises the squad cap; no running economy tax.
6. **Soldier loss** — do garrison soldiers die on a lost raid (re-recruit costs food+resource → real stakes) or just scatter & return? — NEEDS OWNER CONFIRM.
7. **Shared army cap** — do home + outposts draw from one food cap (enables the pre-boss "pull troops home" squeeze) or separate pools? — NEEDS OWNER CONFIRM.
8. **Squad composition** — uniform vs typed roles. *Now leaning TYPED* — region-determined enemy families reward counter-pick garrisons (see §5). — SOFT-RESOLVED, confirm at build.
9. ~~Wall upgrade granularity~~ — **DECIDED: per-part / per-brick** (build-mode creativity + "few more bricks" hook). See §2a.

---

## 9. Core-loop build order (after Batch A playable-village lands)

1. **WO-131** economy ledger (EconomyService) — the pipe's destination.
2. **WO-108** build mode — place outposts + seat upgrades.
3. **WO-117** outpost harvest MVP — income.
4. **WO-164 + WO-144** zone/threat + risk tiers — the gradient.
5. **WO-137/148/149/151** catalog + tier gate — the Town Hall tech tree.
6. **WO-115 + WO-160/159** offline accrual + raids — retention + tension.
7. **WO-114 + WO-181** wall tiers + siege — the sink's upgrades.
8. **Boss escalation wiring** — the goal.

> This re-sequences scattered world/economy WOs into one intentional arc. Batch A (playable village) still runs first — none of this matters while the world is a black void and gates don't open.
