# Backlog Snapshot — 2026-06-23 (operator one-pager)

Groomed PREP pass. Frame: **Knight-first** ("get the single Knight + overworld real-time
battle perfect, fold the rest in when there's time"). Source-of-truth authorities cross-checked:
`MASTER_PIPELINES_BACKLOG_2026-06-06.md`, `CLI_LANES_WO_NUMBERS.md`, `PIPELINE_STATE.md`,
`NOTION_SOURCE_OF_TRUTH.md`, `WorkOrders/WORK_ORDER_483_v1_roadmap`, recent git log.
Status legend: ✓ done/verified · ◐ in progress · ▶ queued/ready · ⏸ held · ⚠ stale.

---

## 1. DONE + verified recently (this session's V1 vertical — local commits, NOT pushed)

| ✓ | Commit | What landed |
|---|---|---|
| ✓ | `22081724` | **WO-482** overworld encounter → isolated **MonsterFamily BattleArena** (orc family, OrcHumanoid rig); single-hero cleanup (companions gated `ff.singlehero`); **armored Tripo Knight** promoted to `Resources/Heroes`; shop All/Armor/Weapons categories; F8 ⚑ + `f8-watch.sh` (§14) |
| ✓ | `fe58e4ce` | **Real-path encounter PROVEN** (reps now spawn → drop to battle; fleet oracle `AssertEncounterRealPath` green); **light world** (RegionMobSpawner/Raid/Camp/Tribe gated OFF under `ff.overworldencounter`); **PetSelect bypass** default ON; **Knight heal+ranged skill tree + 4-skill loadout** (code-built uGUI MVVM, Skills inventory tab, Q=basic attack) |
| ✓ | `f3ef39f9` | **OuterWorld terrain re-centered to origin** + navmesh re-baked — floor now under the play area (S5 geometry-desync fix) |
| ✓ | `5f7c780c` | Castle footprint kept clear of trees/rocks after re-center |
| ✓ | `740e5c66` | **Pink-floor ROOT FOUND + fixed** — was camera bg showing through a missing floor (deep RCA, not material) |
| ✓ | `368fc222` | **Default shield** (off-hand) on Knight build — sword + shield |
| ✓ | `807e382f` | **Knight class LOCK** forced at the hero-BUILD chokepoint (was rendering Mage) |
| ✓ | `3224e942` | **WO-482/1a** — `ArenaRequest`/`ArenaResult` **JSON contracts in Core** (the bounded-arena seam) |

Gates green at hand-off: CompileGate, PROMOTE_KNIGHT_OK, build SUCCESS, fleet real-path PASS.
EditMode = 349 pass / **8 pre-existing failures only** (BuildingCatalog ×3, ModalPanel,
VillageStrayCleanup lint — unrelated; do not chase).

---

## 2. IN-PROGRESS NOW — "Knight-perfect" silos (the only active critical path)

S1 encounter/arena · **S2 hero kit+skill tree (serial)** · S3 economy (Core, additive) ·
S4 art (DONE) · **S5 world layout (serial builder)** · S6 reward/loop-close.

- ◐ **Body/art (S4)** — armored Tripo Knight + sword/shield landed; ongoing polish (proportion/material parity).
- ◐ **Anim** — locomotion + basic attack on Knight; combo/directional blends still thin.
- ◐ **Gear (WO-466)** — gear display/equip + equip-anim; stats wiring still open.
- ◐ **Skills (S2, spine committed)** — heal+ranged tree + 4-slot loadout live; **Slice 2** = wood/iron cost
  funding the tree (`HeroTalentNodeDef` cost + `EconomyService.TrySpend`); **Slice 3** = real per-slot
  signature mechanics (Q-pierce, E-regen, low-HP clutch, W ranged-AoE).
- ◐ **Loop-close (C2, S6)** — extend `BattleArena.GrantWinReward` (XP-only) → skill points + light gear/resources
  via the outpost loot path; retire the dead "unlock next companion" reward.

---

## 3. QUEUED behind the Knight ("fold in when time")

| ▶ | WO | Title / note |
|---|----|----|
| ▶ | **WO-482 (refine)** | BattleArena as a **bounded JSON module + SceneDirector lifecycle** — dedicated arena scene, ArenaRequest→ArenaResult, disposed after use, headless-testable. DESIGN LOCKED; supersedes the far-offset (5000,5000) hack. Slice 1a (Core contracts) DONE. |
| ▶ | **WO-485** | **Winding dungeon generator** — seeded recipe emitter for `DungeonComposer` (winding crit-path → boss, branches, budget scaling, reachability validator). DESIGN-COMPLETE; **explicitly queued behind the Knight.** |
| ▶ | **WO-467** | **RegionGate system** — generalized crossing primitive (seam/threshold/masked transition); also the castle→OuterWorld + RETURN seam repair (S5). |
| ▶ | **WO-466** | Gear display / equip + anim (stats wiring) — folds into the Knight gear silo. |
| ▶ | (no WO#) | **Gear stats** + **weapon VFX** — owner-named, not yet ticketed; mint when claimed (see §4). |
| ▶ | **C7 / C8 (S3)** | `LifeForceService` (Core writes GameState) + ONE autonomous Echo **harvester** (wood) reusing `PetHarvester`/`OfflineHarvestService`. Save-additive. |
| ▶ | **Store** | Owner-named "store" follow-up (categories already in `22081724`); vendor BUY/catalog chain = existing **WO-412/413/415/429/406** (Lane 4/6/7). No new "store" WO file exists yet — reuse the chain, do NOT greenfield (PackStore ~70% built). |

World-layout serial chunk (S5, owner-led, editor-closed bakes): finish the single south
castle↔OuterWorld seam via `WorldGeometry` constant; do NOT attempt the WO-453 seamless cross-zone walk.

---

## 4. Next-free WO number + numbering conflicts

- **Filesystem max = WO-485.** `WORK_ORDER_484` and `WORK_ORDER_486` do **NOT exist on disk.**
  (Task referenced 485/486; only 485 is real. The "store"/"gear stats"/"weapon VFX" items are
  **un-ticketed** owner asks, not minted WOs.)
- **NEXT FREE WO = 486.** (480/481/482/483/485 used; **484 is a free gap** — usable but leaves a hole.)
- **Authority-doc lag:** `CLI_LANES_WO_NUMBERS.md` + `MASTER_PIPELINES_BACKLOG` still say "Next free WO = 430."
  This is **STALE** — 430–485 were minted on-board (Notion) + on-disk since 2026-06-12 without updating the
  authority line. WO-485 itself flags this. **Action:** reconcile the authority line to **486** and slot
  480–485 into lanes (S1/S2/S5/dungeon) when the Knight closes.
- Pre-existing collision ledger (still open, low priority): duplicate repo files for 329/330/331/333/334;
  344–351 skipped (do NOT mint); 420/427/391/396/397/402 used on-board (titles not mirrored — do NOT mint).

---

## 5. STALE / orphaned — candidates to retire or freeze

- **ATB combat** — owner froze it; real-time BattleArena is V1 combat. Keep dormant, **do not invest, do not delete**
  (WO-068/171/335/336/381/389 ATB items → park).
- **Defend-the-Tower / PatriciaLight** — **REMOVED 2026-06-09.** Retire WO-317/318/319/320/330/331/332 (all DTT).
- **Village.unity** — ABANDONED (corruption-cursed). Lane 1 items keyed to it (DEF-156/157, WO-311/312/313/321/323)
  → re-target to Village2 or freeze.
- **Blink armor** — JUNKED (memory `blink-canonical-art-foundation` reversed). Retire any Blink-mesh-swap WO.
- **YarnSpinner** — being removed (WO-455 custom dialogue). Vendor-Yarn WOs (291/294/401) → re-scope to JSON dialogue.
- **Pets/companions in battle** — gated OFF under single-hero. Pet WOs (128/297/298/299/422) = **V2-gated**, not V1.
- **Base-building / barracks / troops** (WO-453-troops, 108/215/239/292 build-mode) — **V2 behind `ff.basebuilding`**, OFF.
- **Old HUD chain** (403/404/405/411 unified-HUD) — superseded by the code-built uGUI MVVM panels this session;
  re-validate before reviving.

---

### One-line resume
V1 Knight vertical is committed-local-not-pushed and fleet-proven; the only live work is the Knight-perfect
silos (skills Slice 2/3, gear stats, reward loop-close, S5 seam). Everything else folds in behind it.
Next free WO = **486**; reconcile the "430" authority line.
