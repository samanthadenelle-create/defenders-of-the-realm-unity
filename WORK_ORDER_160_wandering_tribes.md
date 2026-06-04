# WORK ORDER 160 — Wandering Tribes: radius-triggered, state-saving roaming groups

**Status: READY TO IMPLEMENT (phased)**
**Priority:** Medium-High — living-world threat/encounter layer; the "lived-in realm" payoff
**Date:** 2026-05-30
**Lane:** Combat/AI — code + data (no `VillageSceneBuilder` rewrite; no bake by UI). Combat/AI parallel lane.
**Source:** owner — *"create wandering tribes in the zones. they can trigger spawn within x radius to save state."*
**Ties to:** `ZONE_STREAMING_ARCHITECTURE.md` (radius activation = the streaming/perf principle; state-on-unload = "state until return"), `REGION_ENEMY_ROSTER.md` (a tribe is composed of its region's enemies), WO-143 (roaming-raid layer), WO-155 (region spawn tables).

---

## Tribes are the THREAT to node settlements (owner 2026-05-30) — must be strong enough to destroy them

Tribes are not just ambient encounters — they are the **pressure on the node-settlement economy
(WO-159)**. **Strong tribes wander near nodes/settlements, and an unsupported settlement must be
destroyable by them.** So tribes near a node must be scaled **genuinely dangerous** for that region
(`ThreatLevel` = danger tier × depth) — strong enough that a player who claims a node and walks away
without defending it loses the settlement. Richer node (deadlier region) ⇒ stronger tribe raids ⇒ more
defense required. The two WOs are the two halves of one loop: **WO-159 = the harvest you build, WO-160 =
the threat that razes it if you don't support it.** Build them aware of each other (a tribe targets the
nearest settlement in its roam; a settlement's defense is what it's tested against).

## Raid size is RANDOMIZED within the region's threat band (owner 2026-05-30)

The **size of an attack on a settlement is randomized** — never a fixed, solvable number. This is what
keeps defense from being "build the one wall that beats the known raid and forget it": the player must
defend for the **bad roll**, not the average, so over-investing in defense stays rational and every raid
carries live tension.

**Bounded, not chaotic — randomize WITHIN the region's danger band:**
- Each region/`ThreatLevel` defines a **raid-size range** (min..max members), not a single value. The
  actual raid rolls within that range each time. Goldfields rolls *small* (e.g. 2–4); Ashwood rolls
  *big* (e.g. 8–14) — both vary, but within bands the player can learn and prepare a *range* for.
- Scale the range by `ThreatLevel` = danger tier × **depth**, so a deep-Ashwood node raid rolls from a
  higher band than an edge one — variance rides on top of the danger⇄reward dial, it doesn't replace it.
- Optionally randomize **composition** too (which roster enemies, how many elites) within the region
  roster (WO-155), and **timing** (raids arrive on a jittered interval, not a metronome) — so the player
  can't perfectly pre-position. Keep all ranges in `TribeDef`/raid-config data, never hard-coded.
- **Fairness guardrails:** clamp to the band (no freak unwinnable roll out of nowhere), and the
  "under-attack" warning (WO-159) still fires with the response window — the *size* is a surprise, the
  *incoming* is not.

### The intended FEEL — some raids easy, some brutal (owner 2026-05-30)

The design goal of the randomization is the **emotional swing**, not just a varying integer: on the
same node, **some raids should be easy** (a light roll — you barely notice, a moment of relief) and
**others brutal** (a heavy roll — your full defense is tested, you might lose the settlement). The player
should never know which is coming, so they stay ready. Tune the band + roll distribution so both ends
genuinely occur — not a tight cluster around the average (which would feel same-y), but a real spread:
mostly manageable with **occasional spikes** that make a well-defended settlement *earn* its survival
and an under-defended one fall. The spikes are where the "should I have built more?" tension lives, and
where losing a node (→ the WO-159 3-day lockout) actually happens. A flat, predictable raid size would
kill the whole loop — the variance IS the gameplay.

## Concept

A **tribe** is a persistent roaming group anchored to a zone (a camp / band of that region's enemies —
e.g. an Orc raider band in Stoneback, a Tiefling cult cell in Ashwood). It is a **lightweight state
record** at all times, and only **materializes into live GameObjects when the player enters its
activation radius** — then **de-spawns and writes its state back** when the player leaves. This is the
owner's two halves: *"trigger spawn within X radius"* (activation) + *"to save state"* (persistence).

This is the **per-encounter twin of zone streaming**: a tribe doesn't burn CPU/mem while you're far
(only its small record exists); it inflates near you and remembers what happened when you go.

## The two mechanics

### 1. Radius-triggered spawn (the performance gate)
- Each tribe has an **anchor position + activation radius**. A cheap distance check (player vs anchor,
  throttled — not every frame) flips the tribe **active** when the player is within radius, **dormant**
  when beyond a slightly larger de-activation radius (hysteresis, so it doesn't thrash at the edge).
- **Active:** spawn the tribe's members (composed from the region roster, WO-155, scaled by
  `ThreatLevel` = danger tier × depth, with the red-skull tell). They roam/patrol around the anchor.
- **Dormant:** members de-spawned (GameObjects freed — the GC/render win); only the record remains.

### 2. State-saving (so a tribe is remembered)
- Each tribe carries a **`TribeState` record** (in GameState, alongside the zone `RegionProgress`):
  tribe id, anchor, region, **members remaining**, **cleared/defeated flag**, last-seen timestamp.
- **On de-activation:** write live state back to the record — how many members the player killed, whether
  the tribe was wiped. So a half-cleared tribe stays half-cleared.
- **On re-activation:** re-spawn **from the record** — a damaged tribe returns at its reduced member
  count; a **wiped tribe returns smaller/weaker** (owner: reduced respawn — `clearCount` scales it down
  each wipe, floor/fully-gone after N clears). All tribes hostile.
- Persists through the existing `GameState`/`SaveSchema`/`SaveMigrator` round-trip (extend, bump schema
  per convention — coordinate with SaveMigrator owner).

## Phases

- **Phase 1 — TribeState record + data (Core).** `TribeDef` (id, region, anchor, radius, roster ref,
  size) + `TribeState` (members remaining, cleared, timestamp) in GameState. Author a few tribes per
  region. No spawning yet. *Ships:* tribes exist as data + persist.
- **Phase 2 — Radius activation/de-activation (the trigger).** A `TribeManager` ticks the distance
  check (throttled) and spawns/de-spawns members. Uses WO-155 region rosters + `ThreatLevel` scaling.
  *Ships:* tribes appear near the player, vanish when far.
- **Phase 3 — State write-back + rehydrate.** On de-activation persist remaining/cleared; on
  re-activation spawn from the record. *Ships:* tribes remembered across visits + save/load.

## Constraints (CLAUDE.md §5/§6/§9)
- `TribeDef`/`TribeState` → `DeNelle.Core` (pure data; no Village ref). `TribeManager` + spawn runtime →
  `DeNelle.Village`/World. Village→Core only; state writes GameState directly.
- Reuse WO-155 region rosters + existing enemy defs + `Enemy.Configure` — **do NOT re-stat enemies** or
  build a parallel spawn system; a tribe spawn is a grouped, anchored use of the roaming spawner.
- Reuse `ZoneManager.GetZone`/`ThreatLevel`. Throttle the distance check (e.g. every 0.25–0.5 s, or use
  trigger volumes) — never a per-frame O(tribes) scan at scale.
- No new currency, no UXML, no `System.Reflection`. Persist via existing save pipeline.

## Acceptance criteria
1. Tribes exist as persistent `TribeState` records anchored to zones; authorable `TribeDef`s, a few per region.
2. A tribe **spawns its members when the player enters its radius** and **de-spawns when the player leaves** (hysteresis, no thrash); dormant tribes cost only their record (perf gate verified).
3. Members are composed from the region roster (WO-155) and scaled by `ThreatLevel` (tier × depth); red-skull tell applies.
4. **State saves:** killing some/all members persists (remaining count / cleared flag); re-entering the radius re-spawns from the record (wiped stays wiped or long-respawn; damaged returns reduced).
5. Survives save/load via the existing GameState round-trip.
6. Built on WO-155/143 + ZoneManager — no parallel spawn/enemy system; brace balance; Village→Core only.

## Owner decisions (2026-05-30 — locked)
- **Respawn = REDUCED.** A wiped tribe **returns smaller/weaker each clear** — diminishing presence as
  the player dominates a region. `TribeState` tracks a `clearCount`; each respawn scales member count
  (and/or level) down by a curve (e.g. −1 member or −X% per clear, floor at 1 or fully gone after N
  clears — tune in `TribeDef`). NOT permanent-gone, NOT full-strength repeat.
- **All tribes HOSTILE.** Every tribe is a threat — no neutral/trade camps in this WO. (Neutral camps,
  if ever wanted, are a separate future WO.)
- **Still open — size/count:** how many tribes per region, members each? Default: 2–3 tribes/region,
  4–8 members each, tuned in `TribeDef`. (Non-blocking — designer tunes.)

## Done checklist (CLAUDE.md §10)
- [ ] TribeDef/TribeState data + persistence (Core); a few tribes/region authored
- [ ] Radius activation/de-activation with hysteresis; dormant = record-only (perf verified)
- [ ] Members from region roster, ThreatLevel-scaled, red-skull tell
- [ ] State write-back + rehydrate; survives save/load
- [ ] Built on WO-155/143/ZoneManager; no parallel system; brace balance; Village→Core only
- [ ] `WORK_ORDER_160_wandering_tribes.RESULT.md` when complete
