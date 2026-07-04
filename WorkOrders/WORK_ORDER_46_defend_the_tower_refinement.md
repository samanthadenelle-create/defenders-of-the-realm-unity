# ⚠ WORK ORDER 46 — "Defend the Tower" Scene Refinement + Playtest Bug Sweep — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09 (see `PIPELINE_STATE.md` §2 and `CANON_GROUND_TRUTH_2026-07-03.md`). This WO is historical; the tower combat arc is no longer a pillar.

**Status:** CLOSED — **SUPERSEDED** (system removed 2026-06-09)
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

## C2. Playtest round 2 (post-fix dev build, 2026-05-28)

| ID | Type | Item | Status / root cause |
|----|------|------|---------------------|
| REG | Bug | Console flood "Tag: Tower/Building is not defined" | **FIXED** — my P10/placement code used `CompareTag`/`tag = "Tower"` but the project had `tags: []`. Added Tower/Building tags + layers to TagManager.asset. Needs rebuild. |
| P7b | Bug | Enemies STILL walk through hero | Targeting fix only redirects pathing. Enemy `TickContactAttack` damages only `IDamageableStructure`; the hero has **no health component** and there's no enemy→hero attack. True engagement = a new system (hero HP + enemy melee-vs-hero + death). Overlaps P11. **Design decision needed.** |
| EMP | Feat | Couldn't see empowerment VFX | Root: **no tower asset has empowerment data** (`empowerment` block empty in ArcherTower/FrostTower/MageTower/DevTower) so `ApplyEmpowermentVFX` bails. Added dev-only **F8 force-empower** (EmpowermentDebugTrigger + Tower.DebugForceEmpower) to showcase the auras. Real fix = author empowerment data on the 4 tower assets. |
| P11 | Bug | Defeat in Defend-the-Tower just exits to map | No proper loss/defeat flow (no defeat screen / retry). |
| P12 | Bug | Heart/Tree: ATB breach-choice fires on FIRST hit | Should trigger at ~30% Heart integrity, not on first enemy contact. Trigger lives in HeartController/WaveManager breach path → gate on HpFraction <= 0.30. |
| P13 | Bug | Crystal cluster prompt reads "[F] Healer's Cottage" | Wrong interact label/target on the crystal mine (WO28 building-interact-tag-alignment). Scene-wiring / interactor target. |
| P14 | Bug | Crystal shards don't rotate | `CrystalVfx` rotation not running on the shards — component not attached in scene, or rotation not implemented. Verify CrystalVfx + scene wiring. |
| F4 | Feat | Marketplace building should open Cosmetic & pack store | `MarketplaceInteractor` exists (proximity F → PackStore) but appears unwired in the scene (not attached to "Marketplace" GO / `_storeUiRoot` unset). |

> Note: P11/P12/P13/P14/F4 are largely **scene-wiring / data** issues — several need a careful
> scene re-bake (beware [[village-scene-resave-corruption]]), not just code.

## C3. Playtest round 3 (2026-05-28) — hero health decided + more

OWNER DECISION: the hero **should take damage and have a visible health bar** ("like we
used to have"). Implemented `HeroHealth` (proximity contact-damage from enemies + IMGUI
bar, self-attaching) — see Hero/HeroHealth.cs. Resolves P7b damage intake; loss flow (P11)
still open (OnDied event is the hook).

| ID | Type | Item | Notes |
|----|------|------|-------|
| HH | Feat | Hero health + bar | **Implemented** (HeroHealth.cs). Enemies within 1.5 m deal contact damage; IMGUI bar top-left. Tuning first-pass. |
| P15 | Bug | "Folk's Old Granary" portal indicator (giant arrow + label) floats over empty field, detached from the actual archway portal | WO30 dungeon-portal reposition. |
| P17 | Bug | Walk through walls at gates; gates never open/close/animate | Gate.cs — no collider / no open-close logic wired. |
| F5 | Feat | Pets: start with **one** pet (the 3 are placeholder shapes); add a way to **unlock** more | Pet roster + unlock mechanism. |
| F6 | Feat | Pets don't earn XP | Want shared damage→XP for pets too (PetProgression as an XpEarner in ProgressionManager.Distribute). |

## C4. ATB "Last Stand" battle cluster (2026-05-28 annotated playtest)

| ID | Item | Root cause / note |
|----|------|-------------------|
| ATB-1 | ATB gauge doesn't move (hero + enemy); no fill-over-time, no timeout/auto-attack | **By design**: `Turn.AdvanceToNextTurn` fills bars *instantly* in code to find the next actor — it's a classic turn-based engine, not a real-time ATB. Owner expects real-time fill + forfeit-on-timeout → loop rework. |
| ATB-2 | Enemy renders as a purple "pill" | `BattleSceneBuilder.CreateCombatantCapsule` placeholder (= P5). Needs real enemy model. |
| ATB-3 | Hero pose wrong ("Look at Pose") | Battle scene doesn't drive the hero Animator (idle/attack states). |
| ATB-4 | Attack should play an animation | No attack anim trigger in the battle visual layer. |
| ATB-5 | Skills does damage but unclear (no target select / spell type / DoT vs heal) | Needs target selection UI + action feedback. |
| ATB-6 | Item button does nothing | Not implemented. |
| ATB-7 | On battle end (Victory) it just re-enters the loop | No victory/reward/return-to-map resolution flow. |

> The ATB battle is a separate system (DeNelle.BattleATB). ATB-1/-5/-7 are loop/UX rework;
> ATB-2/-3/-4 are visual/animation; ATB-6 is unimplemented. This is its own refinement effort.

## D. Recommended sequencing
1. ✅ Land the 4 code fixes (this session).
2. Get Scene-view screenshot → confirm "5 shapes" = placeholder tower vs stacked towers.
3. V1 distinct tower placeholders + F2 Level-4 (code+data, no heavy scene work) — quick visible win.
4. V2/V3 terrain scale + lighting + hero scale (the big visual lift; careful re-bake).
5. P3b/F3 wire level-up popup + XP bar.
6. P8 monster families, F1 NPC, P5 ATB models.
7. R1/WO34 larger world (later).
