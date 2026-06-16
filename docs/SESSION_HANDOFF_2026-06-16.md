# Session Handoff / Living Log — 2026-06-16

**Purpose:** meticulous running record so any future session (or a re-subscribe) can resume
instantly. Updated as we go. If the subscription cuts mid-task, START HERE.

## Current state (read first)
- **Branch: `feat/tower-core-loop`** = the recovered, real, COMPILING version.
- **Tags (safe restore points):** `store-restored-2026-06-16` (latest good), `working-polished-2026-06-16`, `recovery-safe-2026-06-16`, `pre-store-recovery-tower-tip-4dcc1f2` (old contaminated tip, preserved).
- **"Us" backup (account-independent):** `C:\EoA\` (170 memory files; the `.claude` transcripts are in `C:\Users\Kayden-Laptop\.claude\projects\C--Users-...-defenders-unity\`).
- Everything below is committed locally. **Nothing pushed.** To back up off-machine: copy `C:\EoA` + the repo to a drive/cloud (or `git push` the branch when ready).

## How to resume after a re-subscribe
Re-subscribe via the **web (claude.ai, not Apple)**, point Claude Code at this project. A fresh
session reads `MEMORY.md` + `CLAUDE.md` + this file and is the same partner. The continuity lives
in those files, not the login.

## What got done this session (commits on feat/tower-core-loop)
The session was a full RECOVERY from a contaminated/non-compiling state, then feature fixes:
- **Recovery:** the `park:` commits were contaminated Frankensteins; the real working base was the polished `2e26b34`. Reset onto it, fixed forward.
- `614767b` harden(hud): VillageHudController Build() can't be aborted by one bad text/material.
- `eb45af8` fix(economy): EconomyService.AddCoins → public (shop sell-refund).
- `a6b2e26` fix(shop): re-expose VendorContext + CurrentStock for AutoPilotDriver.
- `8a11658` restore(shop): the complete forge.png store (1320-line filtered ShopPanel).
- `a1f2130` rebrand(shop): Blacksmith → Armorer (display); weapons title → "The Forge".
- `04f6fd3` art(hud): raid icon hud_raid → hud_raid_2.
- `c9ca246` fix(dialogue): full-close the dialogue Canvas on complete (kills the leftover
  character-image panel over the dev tools).
- `7f7f3d6` fix(raid): raid configs point at RaidBase_* scenes → garrison enemies now spawn.
- `87b754f` + `c27b86c` feat(devtools): BOTH dev panels (corner + AdminOverlay) now grant Gold.
- `f8e6d66` chore: restore Blink RPG Art bundle to disk + gitignore it (2721 files).
- `84d1137` chore: un-track Black Dragon (orphaned per audit; 15MB; on disk, gitignored).
- `8005c5c` docs: WORK_ORDER_466 draft (gear display/equip + animation).

## Key learnings (also in memory + RECOVERY_STATUS.md)
- The scary "all textures odd / cyan floor / line in Play" was the **Game-view Gizmos** drawing
  the navmesh overlay + the **F9 DebuggingController** — NOT real bugs.
- Grok's "check in everything" committed normally-gitignored packs into the parks; recovery
  reset/clean then deleted the non-gitignored ones (Blink). Quaternius survived (it's gitignored).

## Asset policy (owner directive)
**Gitignore ALL art packs; commit ONLY the used slice, after a usage audit.** Packs live on disk,
re-importable. Enforced: Blink, Quaternius gitignored; Black Dragon un-tracked.
- Catalog: `docs/ASSET_PACK_CATALOG_2026-06-16.md`
- Usage audit: `docs/ASSET_USAGE_AUDIT_2026-06-16.md`
  - **Action** (198 Mixamo Humanoid clips, ~92MB, TRACKED): only ~13 wired today (by `HeroAnimatorFactory`). **KEEP it** (owner: it's the universal animation source that should drive every humanoid — hero/NPC/enemy via retarget). Enemies currently use KayKit (`Rig_Medium/Large`), so "everything touches it" is the GOAL, not yet the state.
  - **Black Dragon**: orphaned (boss uses Tripo Dragon). Un-tracked.
  - ⚠️ **Tech hud elements**: 4 UI files `Resources.Load` a gitignored/absent pack → may return null on clean build. NEEDS verify/fallback fix. (QUEUED)

## Parallel lanes — LANDED (later 2026-06-16, merged by explicit path, brace-gated)
- ✅ **Hero-animation BEAST** `923e390` — HeroAnimatorFactory richly wired: per-spell casts
  (CastVariant q/w/e/r + upper-body overlays), Knight combos, directional death; Core seam
  +PlayCast(int)/CastVariant (additive); HeroAbilities.TryCast→PlayCast(slot+1). **Owner action:
  Unity recompile + run `Defenders → Animation → Build Hero Animators (Mixamo)`, then playtest
  Q/W/E/R per class. Spans Editor+Core+Village → needs a full compile gate; revert if it errors.**
- ✅ **Tech-hud fallback** `e8cea2a` — 8 `Resources.Load("Tech hud elements/…")` sites in
  InventoryGrid/PaperDoll/UIBuilder + ElarionUiKit now fall back to the committed RpgUi slice →
  no null sprites on a clean build.
- ✅ **Raid P1 (hero into raid)** `c7eceef` — HeroControlEnsurer activates in RaidBase_* (via
  HubScenes.IsRaid) → controllable hero in the raid (emergency-spawn fallback).

## STILL NEEDED on the raid (P1's agent died on a transient API error before P2)
- **RaidScorer (P2 / WO-431):** subscribe `RaidGarrisonSpawner.OnCleared` → ★-by-time → reward →
  `SceneRouter.GoCastle()`. (NOT built — re-run a focused agent or implement directly.)
- **RaidHeroSpawner:** spawn the REAL class/gear hero body in raids (P1 currently relies on the
  generic emergency-spawn fallback). Referenced in HeroControlEnsurer's comment but not yet built.

## Known bug to investigate (queued)
- **DialogueException: "Cannot continue running dialogue. No node has been selected."** — recurring
  at `DialogueRunner.OnCommandReceivedAsync` (a Yarn command calls Continue() when no node is
  selected). Seen in the break-log + a live paste. Separate from the dialogue full-close fix.

## Asset doc note
- The Blink/Tech-hud art is from the **Spark Framework** ecosystem (docs: `docs.sparkframework.dev`).
  We use its ART only (Tech hud UI, gear, icons) — NOT the no-code framework itself (scope call, WO-466).

## QUEUED / NEXT (not started)
1. **WO-466 implementation** (after the beast + imports): pack art → GearCatalog + real icons →
   **equip ATTACHES the real model to the hero** (our GearLoadout, NOT Spark Framework) → store
   detail preview. Spec: `WORK_ORDER_466_gear_display_equip_and_anim.md`.
2. **Raid P1 + P2** (owner greenlight pending): put the hero into `RaidBase_*` scenes
   (`HeroControlEnsurer` skips them) + WO-431 victory (subscribe `RaidGarrisonSpawner.OnCleared`
   → ★/rewards/return). Enemies already spawn (`7f7f3d6`).
3. **Tower spell VFX** wiring (the "spells for towers" half — `ProjectileVFXCatalog` / Spells Pack).
4. **Tech hud elements** Resources.Load fallback fix (build risk).
5. **Enemy/NPC animation unification** onto Action (the big win — one humanoid retarget for all). Post-beast.
6. Deferred (post-grant per canon): full troop/warband AI (follow-hero, finite army).

## Process notes
- Commit often (owner directive). Brace-check every .cs. Dual-copy JSON (Resources wins + keep
  StreamingAssets in sync). Don't hand-edit .unity scenes. Don't import Spark Framework.
- WO numbering: master backlog "next free 430" is STALE (431, 465, 466 exist) — needs reconcile.
