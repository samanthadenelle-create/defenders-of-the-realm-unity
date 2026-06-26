> ⚠ **STALE — pre-pivot process/state doc** (stale branch `feat/tower-core-loop`, Linear board, or Solana/tower-defense framing). Board = Notion; branch = `wip/village2-and-f8-tickets`. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Backlog Silos — Defenders of the Realm
**Last updated:** 2026-06-03T13:05Z (manual priority sequencing pass)
**Active backlog:** 71 issues across 5 lanes + 9 Legacy/Misc

---

## ACTIVE SPRINT — Run These Now (all parallel-safe)

| WO | Linear | Lane | Priority | Title |
|---|---|---|---|---|
| 253 | DEF-154 | World/Environment | **URGENT — BOTTLENECK** | Split VillageSceneBuilder into partial classes |
| 254 | DEF-147 | Combat/AI | Urgent | Hero hover exploit fix |
| 255 | DEF-155 | Combat/AI | Urgent | Hero backwards + walk anim not playing |
| 256 | DEF-205 | VFX/Audio | High | Blue ring/circle removal |
| 257 | DEF-204 | UI/HUD | High | Hero Select screen layout fix |

**CLI: run all 5 in parallel. Zero file overlap between lanes.**

---

## World/Environment (21 issues — SERIAL on VillageSceneBuilder)

**BLOCKED until WO-253 (DEF-154) lands.**

### Urgent
- DEF-154 — Split VillageSceneBuilder → **WO-253** ← BOTTLENECK

### High (queue after DEF-154)
- DEF-156 — Village Roster Reconcile (magenta ghosts)
- DEF-157 — Strip Crystal Veins (magenta)
- DEF-163 — Exterior terrain missing (black void)
- DEF-18 — Dungeon KayKat NPC grey capsule
- DEF-61 — World Terrain Foundation + Fog
- DEF-62 — Nature & Environment Population (blocked by DEF-61)
- DEF-91 — Replace KayKit NPCs with character pack
- DEF-96 — Upside-down tree reappeared

### Medium
- DEF-106, DEF-114, DEF-126, DEF-188, DEF-191, DEF-193, DEF-195, DEF-198, DEF-63

---

## Combat/AI (27 issues — parallel safe, code only)

### Urgent
- DEF-147 — Hero hover exploit → **WO-254**
- DEF-155 — Hero backwards + walk anim → **WO-255**
- DEF-38 — Spire Defense Mode (LARGE — needs sub-tasks before scheduling)

### High
- DEF-37, DEF-95, DEF-161, DEF-166, DEF-167, DEF-168, DEF-172

### Medium
- DEF-119, DEF-139, DEF-140, DEF-164, DEF-165, DEF-169, DEF-170, DEF-173, DEF-174, DEF-175, DEF-176, DEF-177, DEF-180, DEF-187, DEF-189, DEF-200

### Low
- DEF-142, DEF-199

---

## UI/HUD (12 issues — parallel safe)

### High
- DEF-204 — Hero Select layout → **WO-257**
- DEF-112 — Camera over walls + store polish
- DEF-152 — Gate crossing blind exit
- DEF-179 — Tutorial Redesign
- DEF-84 — Cherry-pick tower core PR
- DEF-99 — Castle doors purple

### Medium
- DEF-115, DEF-149, DEF-181, DEF-182, DEF-194, DEF-197

---

## Monetization/Backend (7 issues — fully isolated)

### High
- DEF-121 — Resource economy correction
- DEF-29 — Glimmer earn path

### Medium
- DEF-12, DEF-68, DEF-69, DEF-185, DEF-190

### Low
- DEF-89

---

## VFX/Audio (4 issues — no gameplay deps)

### High
- DEF-205 — Blue ring removal → **WO-256**
- DEF-94 — Portal color incorrect

### Medium
- DEF-178, DEF-183

### Low
- DEF-184

---

## Cross-Lane Dependencies

- **DEF-154 blocks DEF-156, DEF-157, DEF-163, DEF-96, DEF-106, DEF-114** (all touch VillageSceneBuilder)
- **DEF-61 blocks DEF-62 blocks DEF-63** (terrain → population → POIs)
- **DEF-38 depends on DEF-37** (Spire Awakens cinematic)
- **DEF-68 blocks DEF-69** (Campaign before Monetization)
- **DEF-94 blocks DEF-100** (portal color before glow VFX)

---

## Legacy/Misc (9 issues — closed out)
DEF-158, DEF-159, DEF-160, DEF-162, DEF-171, DEF-186, DEF-192, DEF-196, DEF-201
