<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 155 — Region Enemy Spawning: data-driven region→enemy tables + depth scaling

**Status: READY TO IMPLEMENT (phased)**
**Date:** 2026-05-30
**Priority:** Medium-High — populates the open world with the right enemies; the threat half of the explore loop
**Lane:** Combat/AI — **code + data only. NOT `VillageSceneBuilder`, NOT `OuterWorldBuilder` scene edits beyond spawn anchors; no bake fired by UI.** Combat/AI parallel lane (CLAUDE.md §9).
**Source:** `docs/REGION_ENEMY_ROSTER.md` (the owner-assigned roster) + `ZONE_STREAMING_ARCHITECTURE.md` (depth/threat model)
**Depends on:** `RegionId`/`RegionZone`/`ZoneManager` (built), WO-143 (roaming-raid layer — this provides its region→enemy data), enemy defs in `enemies.json`/`Defs.cs` (built — reused, not re-statted)

---

## Goal

Wire the owner's region→enemy assignment into a **data-driven spawn table** the roaming-mob layer
reads, so each region spawns its assigned faction, scaled by danger tier × depth, with the
Fallout-style red-skull readiness tell.

**The assignment (from REGION_ENEMY_ROSTER.md):**
- **Goldfields (E,t1) + Stoneback (W,t2):** Wildlands living — Orc Raider, Wildlands Caveman, Feral Wolf.
- **Mirewood (S,t3) + Ashwood (N,t4):** Wound-tied — Tiefling Cultist, Necromancer of the Wound.
- **Hollow Ones:** stay the village wave faction (open question — see roster doc; do NOT roam them open-world unless owner confirms).

---

## Phases

**Phase 1 — Region spawn table (data, Core).**
A `RegionSpawnTable` (SO or canonical JSON, mirror `enemies.json` loader pattern) mapping each
`RegionId` → its enemy def ids + per-enemy weight + depth-band (edge/mid/core). Pure data; authorable;
the single source of truth. No spawning yet. *Ships:* the roster as queryable data.

**Phase 2 — Region-aware spawner (the roaming layer / WO-143 input).**
The roaming-mob spawner reads `ZoneManager.GetZone(spawnPos)` → `RegionSpawnTable` → picks an enemy by
weight for the position's depth band, and sets its level from `ThreatLevel(spawnPos)` (danger tier ×
depth). Reuses the existing enemy spawn/`Enemy.Configure` path — does NOT re-stat enemies. *Ships:*
right enemies, right region, right difficulty.

**Phase 3 — Red-skull readiness tell (presentation).**
On a roaming mob's nameplate, show a **red skull** when `ThreatLevel(mobPos) − playerLevel` exceeds a
threshold (graded: skull = risky, double = lethal). Code-built UI (no UXML), reuses the existing
damage-pop/nameplate HUD seam. Pure presentation over Phase 2's math — soft-wall curve unchanged.

---

## Constraints (CLAUDE.md §5/§6/§9)

- `RegionSpawnTable` data → `DeNelle.Core` (pure data, no Village ref). Spawner runtime → `DeNelle.Village`/World. Village→Core only.
- Reuse existing enemy defs (`enemies.json`/`Defs.cs`) + `Enemy.Configure` — **do NOT re-stat or re-model** enemies; only assign region + scale level.
- Reuse `ZoneManager.GetZone`/`ThreatLevel` (the latter added per the zone doc) — one classifier.
- No new currency, no UXML, no `System.Reflection`. Skull tell is code-built HUD.
- Soft-wall: level scaling is punishing-but-survivable, clamped (never a literal one-shot from level delta) — see zone doc.

## Acceptance criteria

1. `RegionSpawnTable` data maps each region to its assigned enemies per the roster doc (Goldfields/Stoneback→living; Mirewood/Ashwood→Wound-tied).
2. Roaming spawner picks the region-appropriate enemy via `ZoneManager.GetZone` + the table; level set from `ThreatLevel` (tier × depth).
3. Deeper-in-region = tougher (depth band scales enemy choice + level); Ashwood core is the hardest.
4. Red-skull nameplate tell fires past the `ThreatLevel − playerLevel` threshold (graded); code-built, no UXML.
5. Existing enemy defs reused (not re-statted); Hollow Ones NOT roamed open-world unless owner confirms.
6. Built on WO-143 layer + ZoneManager — no parallel spawn system; brace balance; Village→Core only.

## What NOT to touch
- No `VillageSceneBuilder`/`OuterWorldBuilder` rewrite (spawn anchors only if needed, coordinated); no bake by UI.
- Don't re-stat/re-model enemies (codex/`enemies.json` own that).
- Don't roam the Hollow Ones open-world without owner confirm (they're the village wave faction).
- No new currency / UXML / Reflection.

## Done checklist (CLAUDE.md §10)
- [ ] Region spawn table authorable, maps roster doc assignment
- [ ] Spawner region-aware via ZoneManager; level from ThreatLevel (tier × depth)
- [ ] Red-skull tell code-built, fires on threshold; soft-wall curve unchanged
- [ ] Existing enemy defs reused; Hollows decision honored
- [ ] Brace balance; Village→Core only; no bake/UXML/new currency
- [ ] `WORK_ORDER_155_region_enemy_spawning.RESULT.md` when complete
