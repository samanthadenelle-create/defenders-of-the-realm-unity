# Backlog by Core Flow (2026-06-16)

Organizing principle: bugs/WOs are bucketed by the **core gameplay flow** they live in, and we
clear **flow-by-flow down the demo critical path**. Flows are disjoint silos → parallel-safe (one
diagnosis agent per flow, no collisions). This is the flow-first + parallelize-silos doctrine, and
it doubles as the demo punch-list (alongside `FeatureFlags`).

**Mirror to Notion:** this is the source-of-truth structure for the Notion "Work Orders" DB — group
the board by these flows. (CLI session has no Notion connector; the UI session or owner applies it.)

**Discipline:** VERIFY-STILL-LIVE first — this is seeded from the 2026-06-14 break-log triage, which
predates tonight's relocation + art restore, so some entries are already fixed (stale wins). Confirm
a bug reproduces before fixing it; never fix a ghost.

## Early progression hook (post-FTUE — candidate, owner "maybe" 2026-06-16)
Two flavors (pick by demo budget):
- **(a) Quest line → unlocks a crafting NPC** at the crafting station (progression gate / depth). Reuses
  QuestService (WO-290), `OpenCraft`, NPC-spawn, catalog/crafting — wiring, not net-new. More build/surface.
- **(b) Easy quests → reward RESOURCES** (do a small task → wood/iron/gold) — plugs straight into the
  harvest→spend economy loop the FTUE teaches; almost nothing new (QuestService + a resource grant).
  **Leaner / lower-risk — the demo-friendly choice.**
- SCOPE NOTE: post-FTUE depth (reason-to-continue), **nice-to-have, not critical-path.** CLI rec = start
  with **(b)** for the demo; (a) post-grant. Decide once FTUE + stability are solid.

## Demo critical path (clear in this order)

### 1. Onboarding  (title → hero select → intro → land in town)
- **Owner verdict (2026-06-16): DONE & CLEAN except ONE point** (TBD — capture below).
- [ ] **THE ONE POINT — RESOLVED: canonical FTUE onboarding arc (owner 2026-06-16):**
      1. **Select hero** (existing HeroSelectController).
      2. **Drop in town** (MainCastle_Hall).
      3. **Meet the pet via YARN** — narrative intro + grant (NOT a select screen; the pet is met/given
         through dialogue). Needs: a Yarn node + a grant-pet command bridge.
         - **Pet must FEEL organic:** reuse the existing **leash-to-hero** follow AI (companions/troops
           already have it) so the pet trails naturally, + a **walk animation cycle** (even a small Tripo
           / simple model — but it MUST have a walk loop; no T-pose/slide). Model can be simple; the
           leash-follow + walk cycle are what sell "alive."
      4. **Tutorial beats:** learn **harvesting**, then **building defenses** (TutorialDirector beats).
      5. **Survive wave 1** (existing wave system).
      6. **Companion joins** on the **"wave 1 complete" alert** — natural trigger; hook the wave-complete
         event → AddToParty / StoryCompanionInjector. (NOTE: companion-follow S4 must work for this to land.)
      7. **Companion mentors the hand-off into the real game** (via Yarn): drives the player to
         **explore town** → **get proper gear** (shop/Forge → EQUIP → visible on hero — showcases the
         store + Blink equip work) → **grants starting GOLD** ("here's some gold") + **teaches how to
         EARN it** (the harvest→gold economy loop). This FTUE deliberately demos the whole stack in
         narrative order: pet → harvest → build → fight → companion → gear → economy.
      7b. **Secure a node** — apply the new gear + learn the node-capture/economy loop (claim/defend a
          resource node). ⚠️ **OWNER FLAG: "this part feels VERY BROKEN"** — live bug, needs diagnosis
          (see World/OuterWorld flow). The FTUE can't ship this beat until the node-securing works.
      8. **FTUE ENDS at the Armorer + Forge** — the guided arc delivers the player to the two gear
         vendors (Armorer = armor, Forge = weapons), geared-up and economy-literate, then HANDS OFF to
         free play. Everything before = hand-held teaching; after = open game. (Requires Armorer/Forge
         solid — already true: store populated + EQUIP tab working.) This is the demo's guided scope.
      → Mostly WIRING existing systems (hero-select, pet, tutorial, waves, companion injector) into this
        sequence — reconcile-not-replace; verify what exists before building.
- [x] Cold-start path clean end-to-end; Intro PanelSettings not regressed post-relocation.

### 2. Town / Hub  (MainCastle_Hall: movement, camera, HUD)
- [x] ~~Tree of Life missing~~ — **FIXED long ago (owner 2026-06-16); triage entry was STALE.**
- [ ] HUD-partial: `VillageHudController.BuildTownPassiveXp` TMP null-material (×3, SafeStep-guarded). (live log) — verify still live

### 3. Dialogue  (Yarn NPC conversations)  ← START HERE (biggest cluster, P0)
- [x] ~~**S1/S2: Yarn dialogue-END wedges UI/input**~~ — **FULLY FIXED (verified 2026-06-16).** 4 root
      causes resolved: CanvasGroup blocker release + Yarn orphaned pointer-action disable + dialogue
      Canvas close + EventSystem watchdog & OnDisable safety net (commits 2861c9b→4605902d). STALE triage.
- [x] Yarn "no node" on `OpenShop` — FIXED (async IEnumerator command, gated 2026-06-16).
- [x] Yarn "no node" SIBLINGS: OpenUpgrade/OpenCraft/OpenEquip/OpenArena/OpenRumorBoard — FIXED
      2026-06-16 (commit 8ed0bce9). All 5 converted to async IEnumerator (open → yield → Stop),
      registrations recast to Func<...,IEnumerator>. COMPILE_GATE_OK.

### 4. Store / Economy  (shop buy/equip/sell, gear)
- [x] EQUIP tab re-enabled in store. FIXED 2026-06-16.
- [~] **Per-member equip + class/weight restrictions (owner 2026-06-16)** — IN PROGRESS. "Select which
      character gets gear" + "play as Grom, assign Ranger's bow to him" + "armor lightweight/heavy
      distinction." Owner decisions: armor LIGHT=Ranger+Mage / HEAVY=Knight+Cleric; FULL scope (selector
      + restriction + persisted per-member stats + weapon shown on companion body, re-applied each respawn).
      - DONE (backend, gated): `ArmorDef.weight` + `GearCatalog.ClassWeight/ArmorFitsClass/WeaponFitsClass`;
        armor.json weight tags (cloth/aegis=any, leather=light, chain/plate=heavy, ×3 copies);
        `GearLoadout` per-class PlayerPrefs persistence (`dotr-equip-weapon/armor-<class>`) + `BindOwnerClass`
        + push WeaponMult onto a sibling `StoryCompanion`; `StoryCompanion._gearWeaponMult` at 3 damage sites;
        companion bodies get a `GearLoadout`+`EquipmentController` bound to their class on spawn.
      - TODO: `EquipmentPanel` target selector (hero + companions) + filter list by the target's class/weight
        + route equip to the target's loadout.
      - NOTE (data balance, owner to tune): light classes currently have cloth→leather→aegis only (thin mid);
        add light mid-tiers in armor.json (content, no recompile) if the gap matters.
- [ ] Detail-pane blank preview (icon not resolving) — minor.
- [ ] Aegis `setId` missing on aegis WEAPONS in weapons.json → Oathweld full-set bonus unreachable. (data fix)

### 5. Combat / Waves  (enemies, ATB, abilities, win/lose)
- [ ] Win/lose condition (ties to Tree of Life as the enemy target). Verify wave loop end-to-end.

### 6. Companions / Party
- [x] ~~**S4: companion won't follow / stays at Tree**~~ — **FIXED (audit 2026-06-16, WO-301).**
      `StoryCompanion.cs:757-838` has a per-frame mesh-aware leash that switches NavMeshAgent↔lerp and
      warp-catches-up across the village→OuterWorld seam. STALE triage.
- [x] "Second companion ends up being ME" (owner 2026-06-16) — FIXED (commit b1fd1730). Root cause:
      the canon companion roster is FIXED (Sylas=Ranger, Elara=Cleric, Grom=Knight), so a player who
      picks Ranger/Cleric/Knight has one roster entry that COLLIDES with their own class → the injector
      spawned a body that cloned the player. Fix: Spawn() drops the player's own hero class from the
      desired spawn set (roster/party-frame untouched; only the duplicate BODY is suppressed).
      NOTE: distinct from the older "duplicate Grom" theory in WORK_ORDER_second_grom_companion.md.

### 7. World / OuterWorld
- **"Secure a node" CONFIRMED BROKEN (audit 2026-06-16):** it's the **ClaimableCamp** flow
  (Clear→Claim→Build→**Defend**→Secured). The Defend stage **silently auto-resolves** when no attackers
  spawn (NavMesh missing at camp anchors) + **zero player feedback** → feels broken. Owner: "we do not
  have anything working for securing nodes." `CampDefenseWave.cs:128-135` (silent auto-secure),
  `CampPromptUI.cs:172-177` (no feedback).
- **🔁 DESIGN PIVOT (owner 2026-06-16) — replace "secure a node" with "INVADE → OWN → BUILD":**
  1. **Invade** an enemy outpost (combat — reuses the existing camp CLEAR stage: kill the guards).
  2. On clear, the enemy camp **becomes the player's OWN starter camp** (reuses CLAIM).
  3. It's a **buildable site** with **CoC-style simple SQUARE WALLS** → this is where we **teach the
     player to build their own base** + **the value of upgrading walls / stronger defensive structures.**
     - **REDESIGN (owner 2026-06-16):** make the camps a **simple SQUARE base** — e.g. **two rows of
       WOODEN walls in a square** (a clean CoC footprint), not the current complex/broken camp geometry.
       REUSE the existing CoC wall system: Tripo wall art in `Resources/Walls`, `CastleWallKit` /
       `CastleWallKitSpawner`, the Wood→Iron→ReinforcedSteel upgrade ladder ([[grid-coc-base-wall-tiers]]).
       The player upgrades wood→iron→steel = the "stronger walls" lesson. Applies to BOTH the enemy
       camp (the raid target) and the claimed player camp (same square-base generator, flipped owner).
  4. **DROP the broken Defend stage** (the silent-fail part) for now — that's what's broken; clear+claim+
     build is the easier, working hook.
  → REUSES the existing ClaimableCamp Clear+Claim+Build, minus the broken Defend → **simplification, not
    new-build.** This becomes the base-building teaching beat (CoC×Warcraft north star), replacing FTUE 7b.
- [x] **IMPLEMENTED 2026-06-16 (commit 50b7b743).** `OutpostFoundationGenerator.GenerateSquareWalledBaseRecipe`
      — CoC square base with N concentric wood-wall rings (default 2 = "two rows"), outer ring = corner
      towers + one front gate, inner rings = plain walls with the gate column left open → straight entry
      corridor into an interior courtyard. `ClaimableCamp` now uses a 7×7 footprint + double ring (3×3
      buildable courtyard) and, on BUILD, flips to a PLAYER-OWNED base immediately via `MarkOwned()` (sets
      Secured, grants the territory/economy benefit, raises OnDefended for existing UI) — the broken
      counterattack is retired (StartDefense/HandleDefended kept dead for a later re-enable). Single-ring
      `GenerateFootprintRecipe` preserved for EnemyOutpost/Arena. COMPILE_GATE_OK.
      - ASSET NOTE (owner 2026-06-16): `Resources/Structures/arcane tower.fbx` is ~52KB (very lightweight,
        WebGL-friendly) — candidate to swap in as the camp corner-tower vs the current `tower_ground_archer`.
- **SEQUENCING (owner):** land the **core loop + this invade→own→build hook FIRST**; the bigger world +
  "areas to explore for better nodes" comes **AFTER the loop lands** (today the OuterWorld is too small
  to travel — world expansion is a later pillar, not a demo blocker).
- [ ] **P1 — S3: enemy outposts run PLAYER behaviors** (turrets help you, build hotkeys work in enemy
      bases) → `scene-configs ownership: Enemy` not enforced at runtime. NOTE: the pivot intentionally
      FLIPS a cleared camp to player-owned, so the ownership gate (S3) is the SAME mechanism — fixing S3
      and building the pivot are the same work: enforce ownership, and flip it on claim.

### 8. Raid  — GATED OFF (FeatureFlags.Raid = false)
- [ ] Build victory/return to UN-FLAG: subscribe `RaidGarrisonSpawner.OnCleared` → ★-by-time → reward →
      `SceneRouter.GoCastle()`; RaidHeroSpawner (real class body, not capsule). (WO-431)

### 9. Arena  — VERIFIED WORKING (FeatureFlags.Arena = true)
- Full loop confirmed (enter→fight→win/lose→reward→return); SKR wallet is intentional client-stub.

### 10. Web / Platform  (cross-cutting demo gate)
- [ ] WebGL stability pass: build → serve → run the loop in-browser → confirm no crash/OOM/empty-UI.
      Known WebGL landmines: UXML-empty-in-builds, Resources.Load vs File.ReadAllText (CanonicalJson),
      OOM/perf. This is the resubmission gate.

## Likely-already-fixed (verify, then close)
- Missing-prefab errors: CourtyardFloor / StorefrontVine / StorefrontCrate / Anvil — almost certainly
  resolved by tonight's Quaternius/art restore. Confirm in a fresh play, then close the cluster.
- ShopPanel CS compile errors in the live log — from a transient mid-session broken state, now green.

## Session progress 2026-06-16 (pets / echos)
- ✅ Two AccuRIG echos wired into the pet load path: `Resources/Pets/ice-wolf.fbx` (1.3MB) +
  `aether-sprite.fbx` (4.6MB). `UseLitePetVisuals` flipped **false** (3D pets on). Animation imported
  (Generic, clips present). **WebGL win: old ~208MB pet-FBX bloat → ~9.3MB.** Stale raw `sprite.fbx`
  archived to `Models/Pet/_archive_raw/`. COMPILE_GATE_OK.
- [ ] **Pet COLOR** — both echos import as Tripo Phong / external-material → likely grey on first spawn.
  Fix = `DeNelle.Core.TripoMaterialFixer` (or re-link the extracted Coyote/PBR diffuse). **Owner playtest
  to confirm grey-vs-colored, then one targeted fix.**
- [ ] **Pet controllers** (polish) — `Resources/Pets/<species>.controller` (idle↔walk on `Speed`); until
  then the embedded-clip fallback plays an animation.
- [ ] **Mentor handoff is double-blocked** (not a 5-min wire): `PostTutorial_WhatsNext` has NO C# trigger
  AND guards on `<<if $tutorialComplete>>` which is never set in C#. Needs a `PostTutorialGuidanceProvider`
  (onboarded + no-wave + idle → set the Yarn var → `DialogueService.Play`) + a playtest.
- Cleanup left: leftover `sprite.fbm/` nested texture folders in Resources/Pets (minor bloat; clean once
  pet color is confirmed so we don't pull a referenced texture); 1 CS0162 warning at PetDeployer:127.
