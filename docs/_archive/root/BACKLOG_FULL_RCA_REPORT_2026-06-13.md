# Defenders of the Realm — Full Backlog RCA + Done/Stale Report
**Generated:** 2026-06-13 (full-tilt pass) · For: Samantha
**Scope:** All 217 Work Orders. Read-only on repo (no code changes); analysis attached to every open Notion ticket. All agents worked from `docs/MASTER_CATALOG.md` + `docs/ARCHITECTURE_PRINCIPLES.md` (code = truth, comments ignored).

---

## Executive summary
- **Every open ticket on the board now has a root-cause (bugs) or readiness analysis (features) attached**, lane by lane — with file:line evidence and a CLI fix/build spec gated on owner retest.
- **The board overstates remaining work.** A large set of tickets marked Ready/Blocked are **already built in code** — they need verify-and-close, not implementation.
- **Biggest single insight:** the genuine new-build queue is much smaller than 217 rows. Most P2s are partially or fully shipped; the real CLI work is a handful of clean bugs + a few true features + one backend deploy.
- **Elemental zones (your upload):** foundation exists, elemental layer doesn't — created as **WO-450** (renumbered off the colliding "429"), architecture-validated.

---

## 1. Done vs Stale (repo ground truth)
- **36 WOs have RESULT files = definitively complete:** 05–12, 18–20, 27, 36, 52, 55, 58, 59, 66, 73, 83, 84, 86, 87, 106(×3), 108, 153, 166, 172, 175, 178, 283–286, 358, 368, 380, 382.
- **~46 fixed-in-commit but no RESULT file** (CLI-claims-done-unverified): includes 333, 374, 383–391, 393, 394, 397, 398, 405, 408–419, 424, 428. Verify against playtest before trusting "done".
- **STALE / DROP — Defend-the-Tower block (confirmed dead, commit abc7aa8):** 46, 47, 48, 96, 99, 100, 209, 221, 317, 318, 319, 320 (317–320 already Dropped this session). Watch: the DTT-named files at 330/331/333 are stale, but those numbers ALSO have live non-DTT tickets — do not drop the live ones.
- **STALE scene target — Village.unity (abandoned):** WO-137 (rebake), WO-104 + WO-181 (build into dead scene) → drop or retarget to MainCastle_Hall/CastleHubBuilder.

---

## 2. Already built — VERIFY-AND-CLOSE candidates (board says open, code says done)
These are shipped or nearly shipped; recommend owner verify then mark Done rather than re-implement:

- **Combat/AI:** WO-145 (advanced tactics ≈ EnemyBrain roles+BT), WO-147 (situational awareness = AwarenessSensor, literally this WO), WO-155 (region spawning = RegionMobSpawner, literally this WO; its "blocked on 164" is stale — 164 is done).
- **World/Explore:** WO-154 (RareCrystalSpawner full impl), WO-165 (DungeonWorldPortalSpawner full impl), WO-160 + WO-159 (TribeManager/Settlement live; only persistence wiring left; both were stale-blocked on the now-done WO-164).
- **UI/HUD:** WO-448 (CompassHud already renders 8-point heading), WO-124 (Resource HUD ships via SetResources), WO-378 (duplicate of WO-403).
- **Economy:** WO-115 (OfflineHarvestService live), WO-117 (WorkerManager live), WO-119 (PetHarvester live — dup of WO-228).
- **Narrative:** WO-235, 296, 294, 230 (fully shipped); 401, 422, 300, 299, 227 (largely shipped). The 7 blank backlog pages (238, 277, 116, 300, 133, 230, 294) describe features that already exist.
- **Combat Feel:** WO-219 (hit-stop+shake+damage numbers shipped, ~70%), WO-295 (set/ward/perks shipped ~85%), WO-218 (hero-side layering built).
- **Build Mode:** WO-215 (BuildMode wired end-to-end; "broken" symptom is stale), WO-282-rotation (RotationCorrectionRegistry built).
- **Monetization:** WO-236 (CosmeticShopPanel ships), WO-171 (battle-theme rotation coded), WO-74/75 (~70% PackStore purchase flow).
- **Build/Perf:** WO-57 (3-tier mobile quality settings ship end-to-end), WO-191/211 (texture tooling committed, just not run).
- **Verify:** WO-329-regression (~75% — RegressionSuite + CompileGate live).

---

## 3. Duplicates & number collisions (renumber/merge)
**True number collisions (two distinct tickets share a number — renumber one each):** 106, 282, 327, 328, 329, 438.
**Functional duplicates (merge):** WO-119→228 (pet harvest), WO-378→403 (town HUD), WO-411→fold into 403, WO-246→268 (NPC replace), WO-239→CampSystem/426 (claim loop), WO-191+211→408 (WebGL texture), WO-151→293+392 (superseded).
**Already handled this session:** 166/178/374 backfill dups dropped; WO-391 tech-debt collision renumbered to 439.

---

## 4. Genuine net-new work (the real CLI queue)
Clean, high-value, ready:
- **WO-449** — hero targets through walls; root cause = no LoS raycast in `HeroTargetIndicator.RebuildCandidates` (Hero/HeroTargetIndicator.cs:223-267). Single-file `Physics.Linecast` gate.
- **WO-420** — fire/spell VFX render as dark quads; root cause = `AbilityVfxKit.cs:519` `Shader.Find` returns null in player builds (stripped shaders) → null-shader material. Add both shaders to Always-Included + null-guard. One fix repairs all elements.
- **WO-396** — resource-node Yarn cutscene + save flag (only true greenfield in Narrative).
- **WO-313** — Windmill production crafter (place in Village2 via VillageSceneBuilder, wire like sibling crafters).
- **WO-305** — relic-recovery quests (QuestService done; author ≥3 quests).
- **WO-392 / 407** — tiered building upgrades (extend live BuildingUpgradePanel; 407 correctly blocked on 392).
- **WO-53** — animator culling not built (and 3 sites force AlwaysAnimate); low-risk perf win.
- **WO-450** — Elemental Zone Layer (see §6).

Keystone dependency:
- **WO-80 (backend deploy: Vercel + Neon)** gates WO-78, 120, 121, 129, 74, 76, 77, 429. The Unity client side is built and resilient; the work is the deploy. **Highest unblock leverage.**

---

## 5. Bounce-backs — need an owner decision before CLI can work
- **WO-386** (Battle Visualization) — decide ATB-overlay vs Arena as the target.
- **WO-222** (Tutorial redesign) — needs a design brief; working FTUE already exists.
- **WO-299** (pet bond capstones) — perk-unlock vs actual pet acquisition? (capstones set a flag but grant no pet).
- **WO-77 / staking** — on-chain vs backend-tracked; recommend folding 76+77+78 into one staking epic.
- **WO-328-NRE** — needs the actual stack trace (F8 BreakCaptureHarness).
- **WO-445** (brute T-pose) — needs a specific repro; mitigation already in place.
- **WO-215** — re-confirm the "no input" symptom; code is wired.

---

## 6. Elemental zones (your Biome Expansion upload)
**Verdict: the zone plumbing is built; the elemental layer is not.** `RegionZone`/`ZoneManager` (ThreatLevel/depth/ZoneState), `RegionSpawnTable`, tribes, settlements, portals all exist — but keyed on **danger-tier + biome** (Goldfields/Stoneback/Mirewood/Ashwood), not Fire/Ice/Wind/Earth. Elements appear only as a discarded brainstorm in WO-108.

Your uploaded spec is **architecturally correct** — it matches ARCHITECTURE_PRINCIPLES §2b (add an `Element` *property* to `RegionZone`, don't fork the collection), which my independent audit reached too. Created as **WO-450** (P2, Lane 5; renumbered off the colliding "429"). Two corrections folded into the ticket:
1. **Elemental combat is bigger than the draft implies** — ATB/`IDamageable` have **no damage-type dimension today**, so "Fire +50% vs Ice" is a real new subsystem, not a field add.
2. **No new `System.Reflection`** in HUD bridges (CLAUDE.md §10) — the draft's "via reflection" note is corrected to read element through a Core contract/`CoreServices`.
Also: pool all elemental VFX (one stack, §2b.1); ship with EditMode tests (§2c).

---

## 7. Recommended sequencing (highest leverage first)
1. **Deploy WO-80 backend** — unblocks 8 monetization/persistence tickets in one move.
2. **Knock out the clean bugs:** WO-449 (LoS), WO-420 (VFX shader), WO-53 (animator cull) — each single-file, high felt-impact.
3. **Verify-and-close the §2 "already built" set** — removes ~25 phantom-open tickets from the board cheaply.
4. **Renumber the §3 collisions** and merge the functional dups.
5. **Drop the §1 stale set** (DTT block + Village.unity-targeted).
6. **Then the true features:** 392/407 tiers, 396 cutscene, 305 quests, 313 windmill, and WO-450 elemental layer (start with the enum + RegionZone property; scope elemental combat as its own sub-task).

---

*All analysis is attached in each ticket's body under "Root-Cause / Readiness Analysis (2026-06-13)". No repo files were modified. Companion files: BOARD_STATUS_2026-06-13.md, BACKLOG_READINESS_CHECK_2026-06-13.md.*
