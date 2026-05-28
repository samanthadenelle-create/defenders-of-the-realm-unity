# WORK ORDER 46 — "Defend the Tower" Scene Refinement + Playtest Bug Sweep

**Status:** ACTIVE — owner playtest 2026-05-28 (CLI session)
**Trigger:** Owner playtested the *Defend the Tower* scene; it reads as broken/placeholder
("5 random shapes piled together", oversized dark hex terrain, tiny hero, generic towers).
**Goal:** Take the scene from placeholder scaffolding to a presentable, correct vertical slice.

---

## A. Confirmed code bugs — FIXED this session (compile-gated, code-only, no scene re-bake)

| ID | Bug | Root cause | Fix |
|----|-----|-----------|-----|
| P1/P2 | Tower builds in the air; placement marker valid on bad spots | Placement ray hits ANY collider on the Default layer (rooftops/props/slopes) and keeps that Y; ground mask doesn't help because everything is on Default | `TowerPlacementSystem.IsValidSurface()` — require near-flat top (`normal.y ≥ 0.85`) and reject Tower/Building hits |
| P10 | Built tower has no collider — hero walks through it; can't click it; towers stack | `TowerConstructionQueue` spawns a bare GameObject; visual build never added a collider | `Tower.EnsureBodyCollider()` — capsule sized from visual bounds, set Tower/Building layer + tag, rebuilt each level |
| P7 | Enemies walk through the hero / never engage | `EnemyBrain.ChooseTarget()` — only Tank role ever targets the hero; DPS/Ranged/MiniBoss return towers/Heart | `FindNearbyHero()` — non-Tank roles engage the hero within `_heroEngageRadius` (4 m) before towers/Heart |
| P3/P9 | Wave gives ~1800 XP vs 200-XP level curve → hero jumps ~5 levels/wave, popup misbehaves | Curve `level*120+80` far too shallow vs `MaxHp*0.5 *(1+0.2/wave)` kill XP | Steepened `HeroProgression.XpToNextFor` to `1000 + (lvl-1)*700 + (lvl-1)^2*100`. **First-pass — tune** |

> P7 (collision) note: targeting is fixed, but NavMesh agents may still clip *through* the
> hero body. If physical blocking is wanted, add a `NavMeshObstacle` (carve) to the hero.

---

## B. The visual disappointment — needs real content/scene work (NOT a code patch)

| ID | Issue | What it actually needs |
|----|-------|------------------------|
| V1 | Towers all look identical / "5 shapes" | Assign real `visualPrefab` per `TowerUpgrade` (L1/L2/L3) per tower type in the 4 TowerData assets. Until art exists, replace `BuildPlaceholderVisual` with 4 visually-distinct, better-shaped placeholders |
| V2 | Oversized dark hex terrain, black seams | Re-scale the hex floor / close seams (P6 unwalkable spots live here); fix lighting/exposure so it isn't near-black |
| V3 | Hero tiny vs terrain | Reconcile hero scale ↔ terrain tile size |
| V4 | Scene reads empty | Dress with props once scale is right (see WO34 world-expansion spec — that's the larger-world plan) |

⚠️ V1–V4 touch the scene-build editor scripts (`BattleSceneBuilder` / TD scene builder) and
require a **re-bake**, which has historically corrupted scenes — do this carefully, verify in a
fresh build, and keep the scene revertible.

---

## C. Other captured playtest items (triaged, not yet started)

| ID | Type | Item | Notes |
|----|------|------|-------|
| P5 | Bug | ATB shows only hero + capsule "pills" | `BattleSceneBuilder.CreateCombatantCapsule` is intentional Week-2 placeholder; needs real combatant models |
| P6 | Bug | Can't walk on parts of village | Same root as V2 (terrain seams) + check standing-stone KayKit colliders; use `HeroLocomotion._loggedColliders` diagnostic |
| P8 | Feat | Monster "families" (tank/2 DPS/healer) not spawning | Tactical scaffolding exists (`EnemyBrain` roles, `TacticalData`, `EnemyGroupCoordinator` in CC_implementations/DEF-72) but wave *composition* not wired into WaveManager |
| F1 | Feat | Add village NPC | Reuse dungeon "Bryn" pattern (`DungeonSceneBuilder` 1229–1278) or the `Assets/Resources/NPCs/` prefabs |
| F2 | Feat | Add Tower **Level 4** (to see VFX) | `TowerData.upgrades` is hardcoded `[3]` + `Tower.MaxLevel=3`; extend to 4, author L4 per tower, move empowerment gate to L4 |
| F3 | Feat | Hero XP bar (visual to-next-level) | `HeroProgression.OnXpChanged(cur, toNext)` already fires — needs a HUD bar bound to it |
| P3b | Bug | Level-up popup never appears / can't spend point | `LevelUpSkillPopup` logic is complete but its comment says scene-attachment is "the integration step" — likely not attached in the scene |
| R1 | Review | "Larger world / floor generator" | = WORK_ORDER 34 (zone-streamed 1000×1000 world). Full spec exists; gated Week 8+ |

---

## D. Recommended sequencing
1. ✅ Land the 4 code fixes (this session).
2. Get Scene-view screenshot → confirm "5 shapes" = placeholder tower vs stacked towers.
3. V1 distinct tower placeholders + F2 Level-4 (code+data, no heavy scene work) — quick visible win.
4. V2/V3 terrain scale + lighting + hero scale (the big visual lift; careful re-bake).
5. P3b/F3 wire level-up popup + XP bar.
6. P8 monster families, F1 NPC, P5 ATB models.
7. R1/WO34 larger world (later).
