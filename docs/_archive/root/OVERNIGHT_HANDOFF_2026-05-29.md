# Overnight Handoff & Routing Order — 2026-05-29 → 05-30

**Author:** UI (Claude), with creative authority delegated by the owner for the night.
**For:** CLI (build-verify lane) + owner (Samantha) in the morning.
**Supersedes:** the stale "END OF DAY — RED tree" block in `PIPELINE_STATE.md` §⏸ (tree is now GREEN — see below).

---

## 0. Build state — GREEN ✓

The RED-tree blocker is cleared. `Enemy.cs` is clean (69/69 braces, one namespace, one
class), and the green build is committed:
- `00b1662` build: lock in the GREEN build — full working code snapshot
- `c4f02e5` feat: dev portal (+10k XP) + CrystalVisual + WO-105 + URP asmdef
- `6149cf2` feat(devtools): set-hero-to-level-N dev action

The previously-staged CLI work (dev portal, CrystalVisual, WO-105 builder restore) is
committed. **No `.cs` was edited via the Linux mount tonight** (CLAUDE.md §0 respected) —
all overnight work is markdown specs/work orders only.

---

## 1. Owner's priority for this push

**Rung 3 — Defend + Explore.** Build the castle, then the realm beyond the walls
(four biomes), with exploration driven by the ward-tether. Plus: finish the tower
Level-4 imbuements and add the Arcane Tower. Everything siloed into non-conflicting lanes.

---

## 2. Creative-director decisions locked tonight (treat as ratified)

1. **Ward-tether = the Rung 3 exploration mechanic.** Relight ward-stones along each
   march to extend the Heart's reach, gate the regions, and claim resource nodes. → WO-112.
2. **Alduin / previous-Keeper threads stay implicit** in v1 (Sister Wren, Old Bram, the
   One Who Remembers). No names the player can't infer.
3. **One anchor questline per region**; the resource node is the repeatable layer beneath.
4. **Ashwood "forgetting" effect** ships as a gentle, fully reversible mechanic (HUD dims /
   song fades past a dark ward) — never punishing, no damage/death.
5. **Tower Level-4 = "Imbued" tier (UI label only).** Code stays `MaxLevel = 3` +
   Empowerment prestige. **Declined** a true `MaxLevel = 4` (non-additive, save-migration risk).
6. **Wardlight** is the new healing/ward imbuement (Aether) — regen + 20% damage-soak for
   friendly structures and the Heart. Plus **Consecrate** (enemy vulnerability aura) and
   **Rally** (tower haste) round out the Arcane Tower's support trio.
7. **Arcane Tower joins the buildable roster** as the magic/support tower, using
   `Tower_Castle_Square` (NOT `Tower_Medieval_Big`, which the Ground Tower already uses). → WO-113.

---

## 3. The three silos (no two lanes touch the same file)

**Bottleneck rule (CLAUDE.md §9):** only ONE lane touches `VillageSceneBuilder.cs` /
`Village.unity` at a time. All scene-builder WOs are serialized into Lane A. The other two
lanes never touch the builder, so they run fully in parallel.

### 🟫 Lane A — World / Architect (serialized; the Rung 3 priority)
Owns `VillageSceneBuilder.cs` + `Village.unity` exclusively. Run in this order, **one rebake at the end**:

1. **WO-105** — builder restore + re-land. Done (435/435 braces); CLI confirm full build + write RESULT.
2. **WO-104** — castle: curtain walls, round towers, moat, 4 drawbridges.
3. **WO-109a** — rampart second tier: walkable wall-tops + stairs + NavMesh upper layer (depends on 104).
4. **WO-107 climate** — the four biomes beyond the walls (depends on 104; anchors off the moat ring).
5. **WO-112** — ward-stone placement rides this rebake (relight/reach system; new code in `DeNelle.Environment`).
6. **WO-110 crystal mine** — wire the on-map crystal as the first node.
7. **REBAKE ONCE** (WO-103 batchmode) — propagates all of the above into `Village.unity`. **CLI fires this; UI does not.**

### 🟦 Lane B — Combat / AI (parallel, code-only, never touches the builder)
- **WO-110 siege/trebuchet** — `SiegeUnit.cs` (new), `WallSegment.cs` HP/Breach/Repair, `waves.json`, `enemies.json`.
- **WO-109b** — tower elevation range bonus in `Tower.cs`/`TowerCombat.cs`.
- **WO-113 Arcane Tower** — additive TowerData asset + palette/seeder + prefab wiring (no builder seeding until Lane A rebake; placement note in the WO).

### 🟩 Lane C — Backend / Docs (parallel, fully isolated)
- **WO-107 backend reconciliation** — `docs/` + Unity URL constants + backend repo. Contains a server-side save-auth security item.

### ⏸ Deferred
- **WO-109c** — player-placeable Wall Tower (palette item + `BuildZone.WallTop`). **Blocked on WO-108** (player build mode); the palette/grid it edits doesn't exist yet. Rampart + elevated towers ship fine without it.

---

## 4. New artifacts created tonight (all markdown)

| File | What |
|---|---|
| `docs/regions-narrative-and-npcs.md` | Four-biome narrative, plotlines, NPCs, the ward-tether concept, canon registry additions |
| `docs/tower-empowerment-spec.md` (§9–§11 appended) | Wardlight + Consecrate + Rally imbuements; Arcane Tower as buildable type; "Imbued/Level 4" terminology |
| `WORK_ORDER_112_ward_tether_exploration.md` | READY — relight-the-marches exploration system (DeNelle.Environment) |
| `WORK_ORDER_113_arcane_tower_buildable.md` | READY — Arcane Tower as a buildable tower type (additive) |
| `WORK_ORDER_109_*` (edited) | Added the owner-ratified a/b/c implementation split |
| `WORK_ORDER_114_wall_upgrade_tiers.md` | READY — wood→stone→reinforced wall tiers (CoC sink). Reuses inert `GameState.WallLevel`. ⚠ both 114 & 110 edit `WallSegment.cs` → **110 lands first** |
| `WORK_ORDER_115_offline_harvest_accrual.md` | READY — mines/pets accrue while away to a cap. Adds `LastHarvestClaimMs`. Depends on 117/111 nodes + 112 ward gate |
| `WORK_ORDER_116_npc_dialogue_bark_system.md` | READY — **extends** existing NPC stack (AmbientNPC/TownsfolkDialogue/VillageNpcInjector). 9 canon NPCs + per-region quest threads |
| `WORK_ORDER_117_worker_dispatch_autocollect.md` | READY — **SUNDAY PRIORITY.** Send workers to a node → auto-collect to cap → bank; random encounters invade (Phase 2). Greenfield, runtime-spawned (no rebake) |
| `WORK_ORDER_118_rewarded_ads_route.md` | READY — rewarded ads so non-spenders get spender benefits via attention. Provider abstraction, store-build only (asmdef strip). First surfaces ride WO-117 |
| `WORK_ORDER_119_pet_auto_harvest.md` | READY — pets tend a node (boost rate or stand-in harvest); enforces "tend OR defend"; feeds offline (115). Additive on WO-117; does NOT touch WO-58 combat aura. Land after WO-117 P1 |
| `WORK_ORDER_124_resource_hud.md` | READY — code-built 4-resource HUD (Wood/Food/Crystal/Ore) + node-fill readout; additive `SetResource(ResourceType,int)` on IVillageHud. Land after/with WO-117 (owns ResourceType enum) |

---

## 🎯 7. SUNDAY critical path — the worker / auto-collect demo (owner's #1)

Owner's goal: by Sunday, **send a worker to a node → auto-collect until the store is full → defend it from random encounters.** WO-117 Phase 1 is scoped to be demoable in 2 days because it's greenfield, runtime-spawned, and needs no new currency or rebake.

**Build order for the demo (all Lane B / code-only — runs parallel to the architect castle lane):**
1. **WO-117 Phase 1 (MVP):** `ResourceType` enum (Core) + `ResourceNodeData` SO + `ResourceNode` + `Worker` (NavMesh travel) + `HarvestService`. Dispatch → travel → auto-collect a **Wood** node to cap → bank to `GameState.Wood`. Code-built fill UI. Invasions stubbed off. **This is the demoable slice.**
2. **WO-117 Phase 2:** telegraphed random-encounter invasions (reuse `EnemyGroupSpawner`/`EnemyBrain`) + recoverable raid consequences (25–40% partial loss, worker not permanently lost by default). This delivers the "needs to be safe" half.
3. **WO-118 (if time):** wire the first rewarded-ad surfaces onto WO-117 — watch-to-instant-collect, watch-to-shield a node from invasion, watch-to-double-rate. Opt-in, store-build only.

**Owner decisions waiting (none block Phase 1):**
- Crystal node banks to `AetherCrystals` (recommended) vs `Resources.Crystals`.
- Phase-2 risk tuning (encounter cadence, raid %) — playtest knobs.
- `ADS_LEVELPLAY` define name — confirm at SDK install.

**Dependency notes:** WO-115 (offline) and WO-118 (ads) both build ON TOP of WO-117 — sequence 117 first. WO-112 (ward-tether) provides the node-claim gate but Phase 1 can spawn a pre-claimed node to stay unblocked.

---

## 5. Open flags for the owner (non-blocking)

1. **Arcane Tower / Ground Tower prefab split** — resolved in spec (Arcane = `Tower_Castle_Square`), but confirm you like the distinct silhouette.
2. **Duplicate WO numbers** (two 107s, two 110s) — known and accepted by owner; renumber when convenient.
3. **Ward-tether questline scope** — v1 ships one questline per region; expand later if desired.
4. Imbuement tuning numbers (Wardlight soak %, Consecrate vulnerability %, Rally haste %) — ratification checklist in `tower-empowerment-spec.md` §11.

---

## 6. Suggested CLI start order in the morning

1. Confirm green build still holds; write `WORK_ORDER_105_*.RESULT.md`.
2. Start **Lane A** at WO-104 (castle). Lanes B and C can start immediately in parallel.
3. Hold the single Lane-A rebake (WO-103) until 104 → 109a → 107-climate → 112-placement → 110-crystal have all landed compile-clean.

---

## 🐞 8. Playtest bugs, QA sweep & verification (added late 05-30, from owner playtest)

Owner ran the build and reported bugs; I triaged them into fix WOs and ran a full QA sweep
+ acceptance-criteria verification. **Key meta-finding: several things marked "done" do not
meet their own acceptance criteria.** Reports:
- `docs/QA_player_sanity_pass_2026-05-30.md` — player-journey audit (triaged P0/P1/P2).
- `docs/acceptance_verification_2026-05-30.md` — per-WO acceptance-criteria matrix (PASS/PARTIAL/FAIL).

### Bug-fix WOs filed (all READY)
| WO | Pri | What | Root cause (verified) |
|---|---|---|---|
| **125** | P0 | Dragon unhittable (hero+towers) + Heart-fall = no defeat | Towers scan `LiveEnemies`, dragon is `LiveApexBoss`; `HeartController` raises no death event. **Also: the spawned dragon is `Resources/Enemies/Boss_Dragon` orbiting at height 22 — WO-102's height fix edited the wrong prefab, so it never took.** |
| **126** | P1 | Magenta materials, gates wrong color, barn-in-wall, blue cube under spire | Polyperfect URP materials (run `Defenders/Art/Fix Polyperfect URP Materials`); Farm z=20 overlaps wall z=21 → z=14; blue cube = Crystal Mine placeholder (mesh failed to load) |
| **127** | High | Tower-manage panel shows Lv1 after upgrade | BuildMenu UXML screen reads `Building.Level` (never mutated) + upgrade button is a stub; should read live `Tower.CurrentLevel` |
| **130** | Owner: keep/park? | ATB enemies are pills + "feels broken" | `AtbCombatantSwapper` only tints the capsule (stale "no model" comment — models exist in `Resources/Enemies/`); engine is healthy, it's presentation/binding. **NORTH_STAR parks ATB — recommend KEEP-but-defer** |
| **131** | P0 | Economy: cost shown ≠ cost paid; rewards don't refill | 3 unsynced wallets; placement drains **Wood** via deprecated `EconomyService` overloads; rewards go to `CrystalEconomy`→GameState. Unify on one source |
| **132** | P0 | No defeat when hero/Heart down | Hero CAN die (HeroHealthBootstrap) but there's **no `GameOverUI` in the Village scene**. Pair with WO-125 Heart path |
| **133** | P0 | No first-run tutorial; cold-open replays | `OnboardingFlow` built but referenced nowhere; `Onboarded` never flips. Wire it (likely needs code-built overlay — UXML doesn't render in builds) |
| **134** | P1 | Ability hotkey labels Q/W/E/R vs actual 1/2/3/4 vs json "F"; non-boss waves award 0 but banner implies reward; faked Wood/Stone costs | Unify input scheme; fix reward/banner; real material deduction |

### Verification regressions (marked done, fail their AC)
- **WO-86** ScriptableObject architecture — the specced `_Modules/Data` classes/assets don't exist; `Assets/Data/TowerData.cs` is an empty stub. Don't count as done.
- **WO-58** pet aura — component exists but is never invoked/wired.
- **WO-102** dragon-height fix — edited a Generated prefab, not the spawned Resources prefab → ineffective.

### Cross-file serialization (one branch at a time)
- **`BuildMenu.cs`** ← WO-127, WO-131, WO-134(b/c). Order: **131 first** (sets crystal source of truth) → 127 → 134.
- **`EconomyService.cs`** ← WO-131 (crystals) + WO-134c (Wood/Stone) — split ownership.
- **`VillageSceneBuilder.cs`** ← WO-125/132 (GameOverUI) + WO-126 (Farm coord) + the architect lane (104/107/109). Batch the GameOverUI + Farm builder edits into ONE bake.

## 🚩 9. REVISED morning priority — "make it playable" before "more features"

The build currently has broken fundamentals (effectively **unloseable**, **economy desync**, **no FTUE**, **dragon unhittable**). For a playable Sunday demo these P0s should lead, in parallel with the architect castle lane:

**P0 critical path (do first):** WO-131 economy → WO-125 + WO-132 (defeat condition: GameOverUI + Heart death event + dragon Resources-prefab height) → WO-133 onboarding. Then WO-126 materials (URP fix + rebake) so it stops looking broken.
**Sunday feature (parallel, Lane B):** WO-117 Phase 1 worker auto-collect MVP.
**Architect lane (parallel):** WO-104 castle → 109a ramparts → 107 biomes → rebake.
**Defer:** WO-130 ATB (owner keep/park/cut), WO-127/134 polish after 131.

*Living handoff. UI marked the matching Linear issues per CLAUDE.md §2 where applicable.*
