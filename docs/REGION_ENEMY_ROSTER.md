# Region Enemy Roster — who roams where (the open-world threat map)

> **Owner assignment (2026-05-30):** the **living world holds the outer regions, the Wound's
> corruption holds the deep ones.** Wildlands living creatures roam the two safe regions; Wound-tied
> demonic/necromantic enemies infest the two deadly regions.
>
> This is the canonical roster the roaming-mob scaler (WO-143) + zone difficulty
> (`ZONE_STREAMING_ARCHITECTURE.md` — `ThreatLevel = f(DangerTier, Depth)`) read to decide *which*
> enemies spawn *where* and *how hard*. Design/doc — grounded in `docs/enemy-codex.md`; no `.cs`, no
> bake.

---

## The assignment

| Region | Cardinal · Danger | Faction theme | Enemies (from enemy-codex §1.1/§1.2) |
|---|---|---|---|
| **Goldfields** | E · Tier 1 (safest) | **Wildlands — living** | Orc Raider, Wildlands Caveman, Feral Wolf |
| **Stoneback** | W · Tier 2 | **Wildlands — living** | Orc Raider, Wildlands Caveman, Feral Wolf |
| **Mirewood** | S · Tier 3 | **Wound-tied — corrupted** | Tiefling Cultist, Necromancer of the Wound |
| **Ashwood** | N · Tier 4 (deadliest) | **Wound-tied — corrupted** | Tiefling Cultist, Necromancer of the Wound |

**The thematic spine:** you travel *outward* through living, biome-appropriate raiders (farmland +
stony uplands), and *inward toward the Wound* into demonic and necromantic horrors. The danger dial
(`RegionZone.DangerTier`) and the faction flip reinforce each other — crossing from Stoneback into
Mirewood is the moment the world stops being "wild" and starts being "wrong."

### Open question for owner — the Hollow Ones (undead)

The **Hollow Ones** (Walker/Warrior/Rogue/Caster/Reaper/Brute) are canonically the **village wave
faction** — they march Elarion's gates (enemy-codex §1.1). The owner assigned the *outer regions* to
**living** (safe) + **Wound-tied** (deep), which leaves the Hollows as the **village/transitional
threat**, not open-world roamers. **Interpretation (confirm):**
- Hollow Ones remain the **village defend-the-gates** enemy (unchanged).
- They may **bleed into the transition** between Stoneback/Goldfields and the deep regions as the
  living world gives way to the dead — a gradient, not a hard line — if the owner wants the seam to
  feel haunted. **Flagged, not assumed.**

---

## How danger + depth scale within a region (ties to the zone doc)

Per `ZONE_STREAMING_ARCHITECTURE.md`, threat scales on **two axes** — *which region* (danger tier)
**and** *how deep* (`Depth` 0 edge → 1 core). The roster respects both:

- **Goldfields/Stoneback (living):** edges = lone Wolves / a Caveman (fodder); cores = Orc Raider
  packs (heavy). Same faction, scaled up by depth.
- **Mirewood/Ashwood (Wound-tied):** edges = Tiefling Cultist skirmishers; cores = Necromancer-led
  concentrations. The **Ashwood core** is the deadliest ground in the world (tier 4 × depth 1) — and
  the natural home of the **set-piece near the Wound** (Necromancer of the Wound; Alduin lore-anchor
  — flagged for owner).
- **Hordes (zone doc "City/Horde" nodes):** a horde node = a denser, place-anchored concentration of
  that region's roster. **Red-skull tell** fires when the spawn's `ThreatLevel` exceeds player level
  (Fallout-style nameplate skull).

---

## Reconcile (do NOT reinvent)

- Enemy *kits/models/stats* already exist in `docs/enemy-codex.md` + `enemies.json`/`Defs.cs` — this
  doc only assigns **which region** each roams. It does not re-stat them.
- Region identity = `RegionId`/`RegionZone`/`ZoneManager` (built). Roaming spawns = WO-143's layer.
  This roster is the **data table** that layer reads — one source of truth for region→enemy.
- `MineNode`/crystal grades (WO-144) already region-gate by danger tier; the enemy roster uses the
  **same** tier dial so reward-richness and threat rise together (danger ⇄ reward).
