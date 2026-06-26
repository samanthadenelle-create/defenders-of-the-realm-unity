> ⚠ **STALE — pre-pivot process/state doc** (stale branch `feat/tower-core-loop`, Linear board, or Solana/tower-defense framing). Board = Notion; branch = `wip/village2-and-f8-tickets`. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Defenders of the Realm — Implementation Phases

**Last updated:** 2026-05-28
**Rule: Fun first. Monetisation last.**

---

## PHASE 1 — Core Combat Feel (Highest Priority)

Implement these first. Nothing else matters until combat feels great.

| WO | Title | Status |
|---|---|---|
| WO-49 | Enemy AI Fix (EnemyBrain — superseded by WO-69) | Superseded |
| WO-53 | Animator Culling (includes EnemyBrain v2 — superseded by WO-69) | Superseded |
| WO-68 | Fix ATB System + Enemy Engagement | READY |
| WO-69 | Complete Enemy + Pet Combat Overhaul (canonical EnemyBrain) | READY — **implement first** |
| WO-70 | Final Combat System (HeroHealth + EnemyHealth) | READY |

**Goals:** Enemies detect and chase hero. ATB bar fills and triggers enemy turn.
Hero and enemy take damage. Pets attack. Kill combo fires. Everything wired.

---

## PHASE 2 — Tower & Wave Satisfaction

Towers need to feel powerful. Wave clears need celebration.

| WO | Title | Status |
|---|---|---|
| WO-50 | VFXManager + Modern VFX Integration | READY |
| WO-55 | Torch Fire Polish (`TorchFireController`) | READY |
| WO-56 | Full VFXManager Integration (abilities, towers, pets) | READY |
| WO-60 | Wave Clear Celebration + Kill Combo System | READY |
| WO-61 | Ground Decals, Hit Reactions & Camera Shake | READY |
| WO-63 | Hero/Pet Level-Up Celebration (`LevelUpVFXController`) | READY |
| WO-65 | Scene Transition + Portal VFX | READY |
| WO-66 | Boss/Special Enemy VFX (`EliteVFXController`) | READY |

**Goals:** Towers fire with satisfying VFX + sound. Wave clear triggers big
celebration. Kill combos escalate VFX and camera shake. Deaths are dramatic.

---

## PHASE 3 — World & Immersion

The world needs to feel alive.

| WO | Title | Status |
|---|---|---|
| WO-52 | WeatherManager (shooting stars, rain, wind) | READY |
| WO-54 | LOD Setup for Characters | READY |
| WO-59 | Dungeon Mode VFX Differentiation | READY |
| WO-64 | Master Quality Controller (`GameQualityController`) | READY |
| WO-71 | Complete World Implementation & Polish Pass | READY |

**Goals:** Terrain replaces flat plane. Foliage, lighting, fog, occlusion culling
baked. NavMesh verified. Weather active. 60 FPS on mid-range mobile.

---

## PHASE 4 — Polish & Retention Loops

Once combat is fun, make players want to come back.

| WO | Title | Status |
|---|---|---|
| WO-51 | Mobile Performance & Animation Optimisation | READY |
| WO-57 | Mobile Quality Settings UI | READY |
| WO-58 | Pet Aura System (`AuraController`) | READY |
| WO-62 | Audio Integration | READY |
| WO-67 | Master Integration Checklist + Final Code Cleanup | READY |

**Additions to implement in this phase (WOs to be written):**
- Daily quest system (clear 3 waves, upgrade 1 tower, use 5 abilities)
- Visible village growth (decoration unlock every 5 waves)
- Simple streak/daily login UI (beyond DailyLoginBonus.cs from WO-77)
- Hero/pet "carry moment" feedback (big highlight when a pet lands the killing blow)

---

## PHASE 5 — Monetisation, Backend & Advanced Systems

Only after the above is solid and fun.

| WO | Title | Status |
|---|---|---|
| WO-72 | Monetisation Strategy (CosmeticData, MonetizationManager) | READY |
| WO-73 | Shop UI + Battle Pass System | READY |
| WO-74 | Solana Crypto Payments (SOL + SKR + USDC) | READY |
| WO-75 | Full Shop UI with Crypto Tabs + SKR Bonus | READY |
| WO-76 | Staked SKR Bonus System (`StakingBonusManager`) | READY |
| WO-77 | Staked SKR Full Integration (Shop + Lumbermill + Daily Login) | READY |
| WO-78 | Backend Transaction Verification + Staking Dashboard UI | READY |
| WO-79 | Management War Room / Control Room | READY |
| WO-80 | Vercel + Neon Backend (full production backend) | READY |

---

## Dependency Order (within phases)

```
Phase 1 implementation order:
  WO-69 → WO-70 → WO-68

Phase 2 implementation order:
  WO-50 → WO-56 → WO-60 → WO-61 → WO-63 → WO-65 → WO-66 → WO-55

Phase 3 implementation order:
  WO-52 → WO-54 → WO-71 → WO-59 → WO-64

Phase 4 implementation order:
  WO-51 → WO-57 → WO-58 → WO-62 → WO-67

Phase 5 implementation order:
  WO-72 → WO-73 → WO-74 → WO-75 → WO-76 → WO-77 → WO-78 → WO-79 → WO-80
```

---

## Notes for Claude Code CLI

When implementing, always:
1. Read the WO file in full before writing any code.
2. Check the dependency list — earlier WOs must be implemented first.
3. Never stage Unity auto-generated files beyond `.meta` files for new scripts.
4. Do NOT modify `CLAUDE.md`.
5. Commit after each WO is complete with the WO number in the commit message.

WOs marked **Superseded** should have their code replaced by the canonical version
listed alongside them.
