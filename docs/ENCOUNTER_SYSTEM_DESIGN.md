# Encounter System Design — the texture between objectives (the exploration gap)

> Owner gap (2026-05-30): *"in the exploration, also in the dungeon as well as in the world there are
> random encounters, right?"* — and the honest answer was **no, not yet.** We designed the big anchored
> things (tribes, node settlements, raids, dungeon mini-bosses) but **not the texture between them.**
> Today travel = walk → objective → fight → walk. The dead air between objectives has nothing in it.
> This doc designs the **random-encounter layer** that fills it — **woven into the systems we built**
> (owner's choice), not pure dice. Design only; reconciled to real systems. Build when prioritized.

---

## The gap, precisely

| Space | Big anchored content (designed) | The dead air (the gap) |
|---|---|---|
| **Open world** | node settlements, tribes, region rosters, mines | **the travel between them — nothing happens as you cross a zone** |
| **Dungeons (D2–D11)** | rooms, mini-bosses, lore beats | **the corridors between rooms — pure transit** |
| **Both** | the destinations | **the journey itself has no tension/reward** |

FF/Elden-Ring/Witcher make *travel itself* worth doing — you go somewhere and the *getting there*
surprises you. That's the missing layer. **All four encounter types, woven into systems (owner).**

---

## The spine — an Encounter Director (context-weighted, not pure random)

One lightweight system: as the player travels (open world or dungeon), the **Encounter Director** rolls
an encounter from a **table weighted by context** — *where you are + what's happening + how long since the
last one*. Context-aware dice = travel feels authored, not slot-machine.

**Weighting inputs (all already exist or are spec'd):**
- **Region + `ThreatLevel`** (zone × depth, WO-164) — which table, how hard.
- **Nearby tribe/raid state** (WO-160) — a tribe near you biases toward *its* scouts.
- **Dungeon theme** (D2–D11) — each dungeon has its *own* table (the Watcher dungeon rolls watching-eye
  events; the Drowned Archive rolls flooded-cache finds).
- **Cooldown / pacing** — a min-distance / min-time since the last encounter so you're never spammed;
  ramp frequency with depth (deep zones = denser).
- **Player state** — low HP might bias toward a rest/merchant; flush with rare stones might bias toward a
  jeweler-material guardian. (Optional, advanced.)

Each roll resolves to **one of four encounter types** ↓.

---

## The four encounter types — woven into existing systems

### 1. Combat ambushes — woven into TRIBES + ZONES (WO-160/155)
- **Tribe scouts (the woven beat):** a tribe near you sends a **scout party before its raid** — the
  ambush becomes *early warning* ("they know you're here; the raid is coming"), not random noise. Cause +
  tension. Survive scouts → expect the settlement raid.
- **Region-themed packs:** Goldfields = Wildlands skirmish; Ashwood-deep = Wound-corrupted pack w/ the
  **red-skull** read. Roster from `REGION_ENEMY_ROSTER.md`; level from `ThreatLevel`.
- **Chase-or-fight:** sometimes the roll is *too strong* (red skull) → run. The soft-wall lets a brave
  player try anyway. Makes the *world* dangerous, not just the wave timer (the NS roaming-threat clause).

### 2. NPC / story encounters — woven into the MOURNING LORE + COZY/COLLECTOR loop
- **A dying Keeper / Hollow-touched villager** on the road — a letter, a line, a fragment of the
  mourning-story (the dungeon lore, now out in the world). Advances the narrative *between* dungeons.
- **A wandering merchant** — buy/sell out in the world (ties the economy); risk = you're exposed, no walls.
- **A lost creature / stray pet** — recruit it → **feeds the Pet Home collection loop** (the cozy player's
  reason to explore: find pets in the wild).
- **A hint-giver** — points you toward a hidden dungeon portal (WO-165) or a rich node. Region-flavored:
  *who* you meet tells you *where* you are.

### 3. Choice / risk events — woven into CRAFTING + the DANGER DIAL
- **A guarded rare-stone vein** — harvest it (**jeweler material**, the crafting economy) but the guardian
  wakes. Risk/reward tied directly to the refinery/jeweler system.
- **A shrine** — offer resources for a temporary buff/blessing; OR a **Withering-tainted shrine** that
  *gambles* (echoes the jeweler failure-mechanic feel — risk a loss for a big roll).
- **A trapped creature** — **free it** (companion/karma, the mournful tone) or **harvest it** (resources).
  A small moral beat that fits "mourn even while ending them."
- **A collapsed path / locked cache** — spend a resource/tool (a torch? a key?) to open it → loot.

### 4. Discovery / loot finds — woven into DUNGEONS + CRAFTING + the parked REWARDS question
- **Hidden caches** behind the **illusory walls** already specced for dungeons (Elden-Ring depth) — now in
  the world too. The connoisseur reward for thorough exploration.
- **Rare-stone veins / blueprint drops** — **this is a strong answer to the parked "what makes a
  dungeon/journey worth it?" question:** encounters are *where unique crafting mats + structure blueprints
  come from.* Found, not bought. Feeds the defensive tech tree + jeweler.
- **An abandoned/razed settlement** — a **razed node-site** (ties the WO-159 3-day-lockout!) you can loot,
  or re-claim early. The world remembers its own events.
- **A lore-stone / map fragment** — reveals a nearby hidden dungeon portal or node.

---

## Why this closes the gap (and feeds everything)

- **Travel becomes content** — every crossing has a chance of a fight, a face, a choice, or a find. The
  dead air is gone; exploration has its own loop (the thing FF/Elden-Ring nail).
- **It's a hub that feeds every other system** — encounters *deliver* crafting mats (jeweler), pets (cozy
  loop), lore (mourning story), tribe warnings (raid tension), blueprints (tech tree), and the danger read
  (red skull). It's not a side-feature; it's connective tissue that makes the *other* systems richer.
- **It answers two parked questions** — "what's worth the journey?" (encounter loot = unique mats/
  blueprints/pets) and gives the cozy player a reason to explore (NPCs, pets, finds), not just the conqueror.
- **Cheap evergreen content** (NS live-ops lever) — new encounters = new table entries. A 1% table change
  refreshes how the whole world feels. Drip content for years from a data table.

## Build shape (for the eventual WO — reconcile, don't reinvent)
- **`EncounterDirector`** (Village/World, `DeNelle.Village`) — ticks on travel, rolls a weighted
  `EncounterTable` per context, instantiates the chosen encounter. Throttled/cooldowned.
- **`EncounterTable` / `EncounterDef`** (Core data) — per-region + per-dungeon tables; each entry = type +
  weight + payload (which enemies / which NPC / which choice / which loot). Data-authored, creative-expandable.
- Reuses: `ZoneManager`/`ThreatLevel` (weighting), `REGION_ENEMY_ROSTER` (ambush rosters), WO-160 tribes
  (scout beats), `DungeonController` (dungeon tables), the economy/crafting (loot), Pet system (stray pets),
  the lore/quest data (story beats). **No new combat/loot/dialogue engine — a director + tables on top.**
- Dungeons get their **own themed tables** (per-dungeon flavor) layered on the world tables.

## Phases
- **P1 — world combat ambushes** (the cheapest, highest-impact: travel has fights). Region table +
  ThreatLevel scaling + cooldown.
- **P2 — discovery finds** (caches, rare-stone veins, blueprints — feeds crafting; answers the rewards Q).
- **P3 — NPC/story + choice events** (the texture + lore + cozy/collector hooks).
- **P4 — dungeon-themed tables + tribe-scout woven beats** (the deepest weave).

## Open questions for owner / creative
- **Frequency/pacing** — how often should an encounter fire (every ~X meters/seconds of travel)? Tune to taste.
- **Interrupt vs ambient** — do encounters *stop* you (a forced fight/dialogue) or can some be *walked past*
  (an ambient camp you choose to engage)? Recommend a mix: ambushes interrupt, finds/NPCs are opt-in.
- **Dungeon density** — denser encounters in dungeons (tighter space) vs the open world? Recommend denser.

🤖 Design doc (UI lane). Reconciled to ZoneManager/ThreatLevel (WO-164), REGION_ENEMY_ROSTER, tribes
(WO-160), dungeons (D2–D11/WO-165), crafting (DEFENSE_DEPTH_ANALYSIS), pets, lore. No code/scene/bake.
