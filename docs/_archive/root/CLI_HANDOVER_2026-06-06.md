# CLI Handover — last 24h (as of 2026-06-06 ~20:00 UTC)

Branch: **feat/tower-core-loop**. This is a digest of every handover/work-order/result
doc touched since 2026-06-05 20:00. Full specs live in the individual WO files.

---

## 1. LANDED overnight + today (DONE, gate-passed, Windows build-verified)

**Hero animation chain — WO-283 → WO-284 → WO-285 (the planned overnight chain).**

- **WO-283 Canonical animation library** — DONE, commit `27e425e`.
  162 FBX imported under `Assets/Action/{Shared,Knight(99),Ranger,Wizard,Enemies(20)}`
  as Humanoid clips (LFS). `ActionClipImporter.cs` now enforces **Optimal** compression
  library-wide. `HeroAnimatorFactory` gained a **Cleric** spec (shares Wizard set) and
  per-type subfolder lookup. 4 hero controllers built clean. CompileGate OK, build SUCCESS.
- **WO-284 Unified animation routines** — DONE (hero slice + driver), commit `bac3fd9`.
  New `AnimParams.cs` + `IActorAnimator.cs` + `ActorAnimator.cs` in `DeNelle.Core.Combat`.
  Death standardized to the **`Dead` bool latch** (killed the `Dead`/`Death` split).
  Heroes routed through the driver; per-class `StringToHash` removed for heroes.
- **WO-285 3D combat uses the library** — DONE (hero combat), commit `bac3fd9`.
  `PlayerAttackController` → `ActorAnimator.PlayAttack`. Knight cycles a **3-swing combo**;
  Mage/Cleric cast; Ranger aims. Hit (`Shared_Hit_Reaction`), death latch, and Knight
  **Block** all wired. Damage still lands on the existing impact-frame window.
- **WO-286 hero FBX import settings** — RESOLVED, commits `148c42f` → `0c86454`.
  Owner re-rigged the 4 hero meshes via **AccuRIG (CC_Base)**; stale `humanDescription`
  cleared in `HeroFbxImporter`; all 4 now import as **valid Humanoid (human=True),
  Read/Write ON**. Ranger green-texture fallback fixed. Build SUCCESS.

**Economy / content (separate lane).**

- **WO-106 Pet Resource Farming + Outpost** — DONE. `EconomyService` extended
  (`SecuredOutpostCount`, `TerritoryMultiplier` = 1 + 0.05·count, `OnOutpostSecured()`).
  MineNode / Outpost / ClaimableCamp income all routed through `EconomyService.Grant` —
  one ledger, no desync.
- **WO-106 Default Gear + Basic Shop** — DONE. New `weapons.json`/`armor.json` canonical
  data, `GearVisualApplier`, code-built `ShopPanel` (BUY/SELL/EQUIP via EconomyService +
  VillageInventory), `NPCCommandBridge` `OpenShop`/`OpenEquip`, Armorer/Forge Yarn nodes.
  > ⚠️ **Number collision:** two unrelated WO-106 files exist (pet-farming + gear-shop).
  > Next free WO number is **287+** (root convention) — pick one and avoid 106 again.

> All five above: CompileGate `COMPILE_GATE_OK`, braces balanced, Windows build SUCCESS.
> **Visual play smoke tests are still PENDING owner/Tricia** (no T-pose / no slide /
> idle-walk-run + attack/cast + hit + death + victory across village-defend + open-world).

---

## 2. HELD — needs a daytime, play-verified session

- **WO-282 Heroes → Addressables** — **HELD, not started** (`...HOLD.md`). Deliberate
  sequencing call by CLI: it converts the hero-spawn critical path from `Resources.Load`
  to async Addressables at 4 call sites (HeroBodySwapper, AtbCombatantSwapper,
  StoryCompanionInjector, PatriciaLight). A blind async bug = "no hero in any scene" —
  too risky to land unattended before a grant build, and the WO itself is medium-priority.
  Resume notes: Localization groups already exist (Heroes won't be the first group);
  UniTask present; **`HeroesGroupConfig` still has stale "Blaise" placeholder slugs to
  fix** to Knight/Ranger/Mage/Cleric. Recommended first step: `WaitForCompletion()` to
  keep control flow synchronous, then migrate to true `await` once play confirms spawns.

---

## 3. READY TO IMPLEMENT — new specs authored today (queued for CLI)

All marked **READY TO IMPLEMENT**; none have RESULT files yet.

- **WO-107 Village Castle overhaul** — Elarion "last bastion": curtain walls, 4 corner
  turrets, walkable ramparts, exactly 4 cardinal gates, Tree of Life at (0,0,0), 4 themed
  districts (Commerce/Housing/Pet/Artisan), stationed-NPC upgrade stations.
  → VillageSceneBuilder lane — **serialization bottleneck, one agent at a time.**
- **WO-108 Castle + World/Region overhaul** — castle as above **plus** elemental regions
  (Verdant/Frost/Stone/Ashen) via additive scene loading, claimable outposts, rampart
  auto-defenses, all income through EconomyService.
- **WO-109 NPC Yarn dialogue + equipment & crafting foundation** — Yarn per stationed NPC,
  `NPCCommandBridge` commands (OpenCraft/OpenUpgrade/OpenEquip/LearnRecipe), equip →
  visual swap + stat effect.
- **WO-110 Yarn blue-button fix + mobile-first HUD** — kill the default
  `RPGDialoguePresenter.lineCompleteImage`, replace with a themed code-built Continue
  button + LineAdvancer tap-advance; redesign HUD as code-built Canvas (large taps).
  > ⚠️ **Duplicate spec:** `WORK_ORDER_110_yarn_blue_button_mobile_hud.md` and
  > `WORK_ORDER_110_yarn_hud_fix.md` are the same WO — **dedupe before working.**
- **WO-111 Audio depth + boss battles + enemy outposts** — extend GameSfx (combat one-shots),
  `BossEncounter` phase framework on DragonBoss, `EnemyOutpost` (clear→boss→claim).
- **WO-282 BuildPreviewModal Premium Rotation** — per-prefab persistent yaw-correction
  registry so a structure type opens pre-rotated to its "natural" orientation; flows
  through existing `PlacedStructureData.yawOffset`.
  > ⚠️ **Number collision again:** this reuses 282 (the Addressables WO). Two different
  > WO-282 files. Renumber one.

---

## 4. ⚠️ Verification gap to resolve first

`QA_CHECKLIST_FILLED.md` (today, 13:37) marks the Castle / NPC / combat items for
"Chunks 1–10" as ✅ **wired in code** — but that pass is **code-inspection only**, and
**WO-107–111 still say READY TO IMPLEMENT with no RESULT files and no CompileGate/build
verification.** Treat 107–111 as *not yet build-verified*. Before trusting the checklist,
CLI should: rebuild Village via `VillageSceneBuilder.BuildVillage`, run `CompileGate.Run`
(expect `COMPILE_GATE_OK`), Windows build-verify, then write the missing RESULT files.

---

## 5. Standing rules reminder (from CLAUDE.md / OVERNIGHT_QUEUE)

- UI never edits `.cs` via bash/mount (sync is unreliable) — **CLI owns all code + builds.**
- Never hand-edit `Village.unity`; rebuild via `VillageSceneBuilder.BuildVillage`.
- Never bake/build with the Unity editor open (project lock).
- Brace-check every `.cs` touched; gate between each unattended step.
- Commit with explicit LFS paths (NOT `git add -A`); UI closes the matching Linear issues.
- Pre-swap hero FBX backups: `Backups/hero_fbx_20260606_005717/`.
