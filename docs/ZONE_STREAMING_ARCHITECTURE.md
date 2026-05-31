# Zone Streaming & Persistence Architecture — the seamless open world

> **Owner vision (2026-05-30):** *"Elden Ring where the world is open"* — one continuous map the
> player walks across with **no load screens**; zones stream in/out for **performance, rendering,
> and GC**; a zone's live state is **pushed to GameState while it's unloaded and rehydrated on
> return**; and **each zone attaches to neighboring zones and cities** as a connected graph.
>
> **Status: DESIGN / NORTH-STAR.** Lock the architecture now so nothing paints into a corner.
> **Build the streaming machinery later — when zones carry real weight** (terrain, props, dungeons,
> raids). Today zones are anchors + mine nodes; premature streaming would be cost with no payoff.
> Design-only doc — no `.cs`, no builder edit, no bake.

---

## Reconcile — what already exists (build ON this, do NOT replace)

| Piece | Symbol / file | Role it already plays |
|---|---|---|
| Zone **identity** | `RegionId { Village, Goldfields, Stoneback, Mirewood, Ashwood }` — `Assets/_Modules/Core/World/RegionZone.cs` | the stable zone enum (append-only) |
| Zone **facts** | `RegionZone { Id, DisplayName, DangerTier 1–4, Cardinal }` — same file | per-zone static data (danger dial) |
| Zone **lookup** | `ZoneManager.GetZone(Vector3)` / `ZoneAt` / `DangerTierAt` — `Core/World/ZoneManager.cs` | classify any world position → its zone (no Village ref) |
| Zone **persisted ledger** | `GameState.Regions` = `RegionProgress { Discovered, Cleared }` — `NestedTypes.cs:226`, `SaveSchema.cs:137` | already saves per-region discovered/cleared (v10 migration seeded) |
| Additive **scene loading** | `WorldSceneLoader.cs` — `Assets/_Modules/Village/World/` | loads `OuterWorld.unity` additively over Village (the just-built two-scene split) |
| Outer-world **content builder** | `OuterWorldBuilder.cs` | bakes region anchors + mine nodes into its own scene |

**The streaming layer described below sits on top of these.** It does not redesign `ZoneManager`
(zone identity), `RegionProgress` (the save ledger), or the additive loader (the load mechanism) —
it coordinates them into a streaming graph.

---

## Core principle — separate a zone's STATE (light, always resident) from its SCENE (heavy, streamed)

The one decision everything else hangs on:

- **Zone State Record** — a *small, always-in-memory* data object per zone: its id, discovered/cleared
  flags, harvested-node states, active raids, building progress, last-visit timestamp. Lives in
  `GameState` (extends the existing `RegionProgress`). **Never unloaded** — it's kilobytes.
- **Zone Scene** — the *heavy* GameObjects: terrain, props, meshes, spawned enemies, VFX. Lives in a
  per-zone `.unity` scene (or a streamed chunk). **Loaded only when near, unloaded when far.**

When a zone unloads, its scene is freed (the GC win) but its **state record persists**; when the
player returns, the scene **re-inflates from the record** instead of resetting. A dormant zone is
"remembered" by its record, not by keeping its GameObjects alive. This is the spine of
performance + "state until return."

---

## The zone graph — zones attach to zones and cities

Each zone declares its **neighbors** (and which cities it borders), forming a connected graph. The
streamer uses the graph to know what to **preload as the player approaches a seam**, so crossing a
border is seamless (the neighbor is already loading before you reach it).

```
                 Ashwood (N, danger 4)
                      |
Stoneback (W) — [ VILLAGE / city ] — Goldfields (E, danger 1)
                      |
                 Mirewood (S, danger 3)
```

- Cities (the Village, future cities) are **nodes in the same graph** — a zone "attaches to" a city
  the same way it attaches to a neighbor zone. The Village is `RegionId.Village` already.
- A zone's record carries `Neighbors : RegionId[]` (+ optional seam transform / portal point) so the
  streamer and the navmesh seam logic both read one source of truth.

### Typed destinations — zones run to a CITY or an ENEMY HORDE (owner 2026-05-30)

Each graph connection leads to a **typed node**, which is what gives the open world its rhythm
(*"the zones run to city or enemy hordes"*): you travel through a zone and arrive either at
sanctuary or at a fight.

| Node type | Meaning | Danger dial fit |
|---|---|---|
| **City** | A safe hub (Village, future cities) — shop/forge/rest/quest-give | low-danger zones (Goldfields) run toward cities |
| **Horde** | A contested area / enemy concentration — the DEFEND beat out in the world | high-danger zones (Mirewood/Ashwood) run toward hordes |
| **Neutral** | A through-zone / crossroads — leads onward | any |

- Add a `NodeType { City, Horde, Neutral }` to the graph; each zone (or each seam/connection) is
  tagged with what it runs toward. `RegionZone.DangerTier` already drives this — high tier ⇒ horde,
  low ⇒ city — so the typing is largely derivable, with manual overrides for authored set-pieces.
- **Hordes reuse the roaming-raid layer (WO-143)** — a horde node is a denser, place-anchored raid
  spawn, not a new combat system. **Cities reuse the village/city build** (a horde-free safe zone).
  Reconcile, don't reinvent.

### Hidden dungeons — random portals on the map (owner 2026-05-30; the dungeon relocation)

Dungeons are **not fixed buildings** — they're **hidden portals that spawn at random points within
zones**, found by exploring and entered through the portal (*"random hidden dungeons on map with the
portal"*). This is the **dungeon relocation to world nodes** noted earlier (dungeons + crystals →
world nodes).

- **Reuse the rare-spawn pattern (WO-154).** A hidden dungeon portal is the same shape as a rare
  timed crystal: a scheduler rolls a **random valid location within eligible zones**, marks it with a
  portal (code-built cue), and the player discovers it by roaming. **Do NOT build a new spawner** —
  extend WO-154's region-gated random spawner with a "dungeon portal" payload type.
- **Differences from a rare crystal:** a portal may **persist once discovered** (or stay until
  entered) rather than time-out; entering it **loads the dungeon** (its own scene — `DungeonController`
  already exists) and returns the player to the same world spot on exit. Discovery flips the zone
  record's dungeon flag (persisted in `ZoneState`, so a found dungeon is remembered).
- **Region-gated rarity (WO-144 danger⇄reward):** deeper/more-dangerous zones host rarer, richer
  dungeons — the same danger-tier dial that grades crystals grades dungeons.
- **Reconcile:** the old village dungeon **portal generator was removed (WO-150)** precisely because
  dungeons move out here. `DungeonController` / dungeon scenes are reused as the *destination*; only
  the *entrance* changes from a fixed village building to a random world portal.

---

## Streaming model (Elden-Ring seamless)

- **Active set:** the zone the player is in + its immediate neighbors (from the graph) are loaded.
  Everything else is unloaded.
- **Hysteresis:** load a neighbor when the player crosses a *preload* radius toward its seam; unload a
  far zone only after the player is well past a *keep* radius (avoid load/unload thrash at borders).
- **Seamless seam:** because neighbors preload before the border, the player never sees a load screen
  — they walk Goldfields→Stoneback continuously. This is the cost of "open world" vs discrete zones:
  the seam must be stitched (next section), not hidden behind a loading screen.
- **Frame-budgeted:** load/bake/instantiate work is spread across frames (additive `LoadSceneAsync`,
  `allowSceneActivation` gating) so a zone streaming in never spikes a frame — the rendering/perf win.

---

## Roaming mobs — scaled by zone AND depth, gated by a level check (owner 2026-05-30)

Difficulty scales on **two axes**, not one (*"roaming mobs based on zone and deeper in zone classify
level checks"*):

1. **Which zone** — `RegionZone.DangerTier` (1 Goldfields → 4 Ashwood). The coarse dial.
2. **How deep into the zone** — a `Depth` read (0 at the safe edge → 1 at the zone core). The fringe
   of a zone is survivable; its heart is the hardest part. This is the Elden-Ring "push in at your own
   risk" feel.

**Effective threat = `f(DangerTier, Depth)`** — roaming mob level / density / composition scale on the
product, so a shallow Ashwood edge ≈ a deep Goldfields core, and the Ashwood core is the deadliest
ground in the world.

- **Extend `ZoneManager`, don't replace it.** Today `GetZone(pos)` returns the region. Add
  `Depth(Vector3) → 0..1` (normalized distance from the zone's safe edge toward its core/center) and
  a `ThreatLevel(Vector3)` convenience = `combine(DangerTierAt(pos), Depth(pos))`. Pure Core, no
  Village ref — same module as the existing `GetZone`.
- **Roaming mobs read `ThreatLevel(spawnPos)`** to set their level/stats — this is the WO-143
  roaming-raid layer's scaling input. Reconcile: WO-143 already scales raids by region; this adds the
  depth factor to the *same* scaler, not a parallel one.
- **Level check (the "classify" gate):** compare the player's level to the position's `ThreatLevel`.
  Uses: (a) a **readiness signal** — UI/compass cue or mob-nameplate tint showing "you're
  under-leveled for this depth" (Elden-Ring's red-skull tell); (b) optional **soft gating** of the
  richest rewards (deep-zone crystal grades WO-144 / rarer dungeons) behind being level-appropriate.
  It does **not** hard-wall the player out — they can push in and risk it (open-world ethos).
- **Hordes + hidden dungeons inherit depth too:** a horde node or dungeon portal deeper in a zone is
  tougher/richer than one near the edge — the same `ThreatLevel` read grades all world content.

> Player level lives in the existing progression (`HeroProgression` / `SkillSystem`); the level check
> reads it. No new level system — `ThreatLevel` vs current player level is the whole gate.

### The danger gate is a SOFT WALL (owner decision 2026-05-30) — Elden-Ring, not a hard level wall

When an under-leveled player pushes into a too-dangerous zone, it is **brutal but survivable** — never
an instant hard wall. This decision defines how the two power axes (level vs resources) interact, and
it is the spine of the whole build→push→reward loop:

- **Level is the soft entry signal, not a barrier.** The `ThreatLevel` vs player-level check drives a
  *warning* and **scales how punishing** the fight is — it does **NOT** lock the player out or
  one-shot them on a level-delta. The player can always choose to push in and risk it (open-world ethos).
- **The tell = a Fallout-style red SKULL (owner decision 2026-05-30).** When a mob's `ThreatLevel`
  exceeds the player's level by a threshold, its **nameplate/health bar shows a red skull icon** — the
  Fallout "this enemy is above your level, it will wreck you" read. Clearer and more explicit than
  Elden-Ring's subtle cues. It is a **pure presentation layer** over the existing math: read
  `ThreatLevel(mobPos) − playerLevel`, show the skull past the threshold (optionally graded — single
  skull = risky, double/△ = lethal). No new system; code-built UI (no UXML); the soft-wall damage
  curve is unchanged — the skull just *warns* before the player commits. Reuses the existing damage-pop
  / nameplate HUD seam.
- **Resources + upgrades partly offset level.** A player who spent their haul on **Forge (+damage)
  and Armory (−damage taken)** (WO-151) — and who plays well — can push **deeper than their raw level
  suggests**, just punishingly. This is what keeps the economy meaningful: resources don't *buy past*
  the danger, but they **widen how far under-level you can survive**. A maxed-upgrade level-10 beats a
  no-upgrade level-10 in the same deep zone.
- **The honest model:** *level sets the comfortable band; resources + skill stretch how far past it
  you can reach; raw resources with no level and no skill still get you killed.* So grinding the
  village economy is **necessary fuel but not a free pass** — exactly the owner's point: *"regardless
  of how many resources you build, until you level up enough these zones will slaughter you"* — with
  the Elden-Ring nuance that **upgrades + skill let a brave player punch above their level**, they
  just bleed for it.
- **Implementation shape (defer to WO):** scale incoming/outgoing damage on the `ThreatLevel −
  playerLevel` delta as a **smooth curve** (steep but continuous), **clamped so it's never a literal
  one-shot** from a level gap alone — the player's Armory mitigation and dodge/skill always leave a
  survivable margin. NO binary "you may not enter" gate. The richest rewards (deep crystal grades
  WO-144, rare dungeons) sit deep on purpose, so the soft wall *is* the risk/reward dial.

## NavMesh across the seam (the main wrinkle — defer until something walks across)

A navmesh baked in one zone scene does **not** auto-connect to a neighbor's. Two options when raids
(WO-143) actually need to path between zones:

1. **Off-mesh links at seams** — pre-place link points at each zone border; agents hop the link to
   cross. Cheaper, works with per-zone bakes.
2. **One navmesh volume spanning loaded zones** — a runtime `NavMeshSurface` that re-stitches as zones
   load. Heavier, more seamless for AI.

**Defer this entirely for now** — mine nodes don't path, and raids aren't built. Recommend off-mesh
links when the first cross-zone raid lands. Flag, don't build.

---

## Phased build path (each phase shippable; build Phase 1 only when a zone gets heavy)

**Phase 0 — TODAY (done / in progress):** `ZoneManager` identity + `RegionProgress` ledger +
`OuterWorld.unity` + `WorldSceneLoader`. Zones are *logical* (one scene, classified by position).
No streaming. **This is enough until zones have weight.**

**Phase 1 — Zone records + graph (data, no streaming).** Extend `RegionProgress` into a per-zone
`ZoneState` (discovered/cleared + node states + neighbors). Author the neighbor/city graph. Pure
Core data; nothing unloads yet. *Ships:* zones remember their state; graph queryable.

**Phase 2 — Streaming (the load/unload machinery).** Promote each heavy zone to its own scene;
`WorldSceneLoader` becomes a graph-driven streamer (active-set load/unload with hysteresis,
frame-budgeted async). *Ships:* only nearby zones loaded — the perf/GC win. **Trigger:** a zone has
real terrain/props/dungeon worth unloading.

**Phase 3 — State serialize / rehydrate.** On unload, write the zone's live state to its record
(harvested nodes, raid progress); on load, re-inflate from it. *Ships:* "state until return" — a
zone you left stays as you left it. Reuses the existing `SaveSchema`/`SaveMigrator` round-trip
(bump version per convention).

**Phase 4 — Cross-zone navmesh (off-mesh links at seams).** Only when raids/roaming walk between
zones. *Ships:* seamless AI traversal.

---

## Constraints (CLAUDE.md §5/§6/§9 — for whoever builds the phases)

- Zone **identity + state** are `DeNelle.Core` (pure data; `RegionId`/`RegionZone`/`ZoneState` —
  Core never refs Village). Streaming **runtime** (loader/streamer) is `DeNelle.Village` (or a World
  module) — Village→Core only.
- State persists via the existing `GameState`/`SaveSchema`/`SaveMigrator` (extend `RegionProgress`,
  bump schema per convention — coordinate with the SaveMigrator owner).
- Per-zone scenes are baked by their builder (single-writer per builder, CLAUDE.md §9); never
  hand-edit `.unity`. Editor closed for bakes.
- No new currency, no UXML for any zone HUD (code-built), no `System.Reflection`.

## What this doc deliberately does NOT do

- Does **not** trigger building streaming now — Phase 0 stands until zones are heavy.
- Does **not** redesign `ZoneManager`, `RegionProgress`, or the additive loader — extends them.
- Does **not** split the four light zones into four scenes yet (cost with no payoff at current weight).

---

🤖 Design/architecture doc (UI lane). Grounded in `Core/World/RegionZone.cs` (`RegionId`/`RegionZone`),
`Core/World/ZoneManager.cs` (`GetZone`), `Core/State/NestedTypes.cs` (`RegionProgress`),
`Core/State/SaveSchema.cs` (regions persisted), and `Village/World/WorldSceneLoader.cs` (additive
loader). No `.cs` touched, no builder edit, no bake.
