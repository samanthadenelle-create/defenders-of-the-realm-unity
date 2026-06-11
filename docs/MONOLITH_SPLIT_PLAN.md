# Monolith Split Plan — >800-line offenders

**Status:** PLAN / marching orders. Read-only analysis; no `.cs` touched producing this doc.
**Authored:** 2026-06-10. **Binding law:** `docs/ARCHITECTURE_PRINCIPLES.md`.
**Audit source:** owner's >800-line offender list (the same failure mode as the retired Village monolith).

---

## 0. The law this serves (one paragraph)

Every component is a **bounded context** with deliberately-limited scope (§1). **Presentation is its own
layer that never touches the objects** — objects expose state, the view observes/renders (§2). Cross-cutting
concerns — presentation, input, economy, persistence, i18n — are **their own composed layers via thin seams**
(§1). Pooling is the default for anything spawned repeatedly (§2b.2). And the hard gate: **unit tests are the
permission gate** — a refactor of a working subsystem is NOT "done" until tests prove behavior was preserved
(§2c). Nothing here is called done on faith.

### Two kinds of split (be explicit which one each entry is)
- **CLASS EXTRACTION (the real fix).** Pull a concern into a *separate class/file* with a thin seam
  (interface, event, or method call). This is the bounded-context cure. Higher risk, needs the test gate.
- **PARTIAL SPLIT (interim, file-size only).** Same class, carved across `ClassName.Concern.cs` `partial`
  files. Changes ZERO behavior (same type, same members) → gate-verifiable by compile + brace check alone.
  Buys compliance + readability now; flagged as INTERIM where the true cure is class extraction later.

> A partial split is honest only when labeled interim. It is NOT the bounded-context fix — it just stops the
> file from being a 1500-line wall while the real extraction is scheduled (§3: right-sized-now + log the tier).

---

## 1. Summary table (ranked by value × safety — do top-down)

| # | File | Lines | Verdict | Split kind | Risk | Existing test cover | Gate before "done" |
|---|------|-------|---------|-----------|------|--------------------|--------------------|
| — | **BattleHud** | 1210 | **DELETE — dead code** | n/a | low | n/a | confirm 0 refs after BattleVfx removal, compile |
| 1 | GameStateService | 1276 | SPLIT | class extraction (backend-sync layer) | **low** | `GameStateRoundtripTests`, `SaveLoadRoundTripTest`, `SaveMigratorTest` | existing suite green + new `GameStateMutatorTests` |
| 2 | HeroInventoryController | 1559 | SPLIT | class extraction (dedupe → `ElarionUiKit`) | **low** | none (UI) | new `HeroInventoryModelTests` on the non-UI slice |
| 3 | AudioService | 1111 | SPLIT | class extraction (music vs sfx vs routing) | low-med | none | new `AudioRoutingTests` (TrackForScene/volume math) |
| 4 | DevPanelController | 1186 | SPLIT | partial (INTERIM) — editor-only | **low** | none (dev-only) | compile under `DEVELOPMENT_BUILD` + brace check |
| 5 | TowerPlacementRotateMenu | 1111 | SPLIT | class extraction (view vs orient-apply) | low-med | none | new `OrientRecipeApplyTests` |
| 6 | BuildModeController | 1682 | SPLIT | class extraction (placement-rules + economy + camera) | **med** | none | new `PlacementValidationTests` + `BuildLedgerTests` |
| 7 | Enemy | 1331 | SPLIT | class extraction (combat/death vs nav vs presentation) | **med** | `VillageSmokeTests` (smoke only) | new `EnemyCombatTests` (damage/hit-dir/death) |
| 8 | WaveManager | 1556 | SPLIT | class extraction (spawning vs reward-economy vs flow) | **med** | `VillageSmokeTests` (smoke only) | new `WaveRewardTests` + `WaveSequenceTests` |
| 9 | ShopPanel | 991 | SPLIT | class extraction (dedupe → `ElarionUiKit` + txn) | low-med | `EconomyServiceTests` (txn side only) | new `ShopTransactionTests` |
| 10 | BuildMenu | 952 | SPLIT | class extraction (view vs build/upgrade actions) | med | none | new `TowerBuildAffordTests` |
| 11 | EnemyBrain | 991 | SPLIT | class extraction (targeting vs tactical-movement) | med | none | new `EnemyTargetingTests` (ScoreAndPick) |
| 12 | HeroBodySwapper | 1174 | SPLIT | partial (INTERIM) → later class extraction | med | none | compile + brace; later `UrpMaterialRetargetTests` |
| 13 | Tower | 895 | SPLIT | class extraction (data/upgrade vs presentation/VFX) | med | none | new `TowerUpgradeTests` |
| 14 | StoryCompanion | 876 | SPLIT | class extraction (class-abilities vs follow/locomotion) | med | none | new `CompanionAbilityTests` |
| 15 | TitleController | 1305 | SPLIT — **share with #16** | class extraction (roster-card kit) | med | none | new `HeroCardModelTests` |
| 16 | HeroSelectController | 867 | SPLIT — **share with #15** | class extraction (roster-card kit) | med | none | (shared w/ #15) |
| 17 | SmartMobileCamera | 955 | **DO NOT SPLIT — cohesive** | (optional partial only) | n/a | `OrientationValidatorTests` (tangential) | leave; or interim partial if compliance forced |
| — | VillageHudController | 1292 | **SKIP** — mid-rewrite (WO-403) | — | — | — | — |
| — | GameState (data SO) | 302 | **NOT AN OFFENDER** — audit miscount | — | — | — | (the 1276 = GameStateService) |

> **Audit correction:** the "GameState 1276" row is `GameStateService.cs` (1276). The `GameState.cs` *data*
> ScriptableObject is 302 lines and is already a clean pure-data container — leave it.

---

## 2. Per-file detail

### DELETE — `BattleHud.cs` (1210) + `BattleVfx.cs`  ·  RISK low
**Finding (load-bearing):** `BattleController.cs` line 56 — *"Old UIDocument + complex VisualElement BattleHud
removed"*; line 657 — *"Old BindUi / UIDocument / old BattleHud completely removed."* The live HUD is
`BattleHudUgui` (instantiated at `BattleController.cs:146`). The ONLY remaining references to `BattleHud` are
`BattleVfx.cs` (which holds a `BattleHud _hud` and reads `TryGetCardElement` / `VfxLayer`) and the README.
`BattleVfx` is itself the old VisualElement-VFX path that the uGUI HUD replaced.
- **Action:** verify `BattleVfx` is not wired into the live `BattleController` path, then **delete both
  `BattleHud.cs` and `BattleVfx.cs`** (+ `.meta`). This removes 1210+ lines of monolith with ZERO behavior
  change — the highest-leverage, lowest-risk move on the board. Do NOT "split" a dead file.
- **Gate:** grep proves 0 live references; project compiles; ATB battle still renders via `BattleHudUgui`.
- **If** `BattleVfx` turns out still-wired (owner confirms retro numbers/flashes are live), downgrade to:
  keep `BattleVfx`, delete only `BattleHud`'s unreachable command/overlay code — but the evidence says dead.

---

### 1. `GameStateService.cs` (1276) → extract the **backend-sync layer**  ·  RISK low
**Clusters:** (a) load/save/snapshot/apply + ~25 typed mutators (the Zustand-slice analog, lines 94–760);
(b) a self-contained **backend sync** block (lines 771–1269): `SyncAfterWave`, `LoadFromBackend`,
`SyncToBackend`, `SendDelta`, `BuildDeltaPayload`, `FetchNonce`, `TryAttachAuthHeaders`, offline queue,
diff helpers, `UnityWebRequest` plumbing.
- **Seam:** the sync block already only reads `Snapshot()` + applies deltas — a clean persistence/transport
  concern (§1: persistence is its own layer). Extract to `GameStateSyncService` (own MonoBehaviour or plain
  service), invoked by the service via a thin call. The mutators + load/save stay as the state owner.
- **Why low risk:** the sync layer is **isolated** (network I/O, no scene coupling) and the state core is
  **already the most-tested code in the repo** (round-trip, migrator, schema-validate). Pulling out the
  network half does not touch the tested mutator paths.
- **Test gate:** existing `GameStateRoundtripTests` + `SaveLoadRoundTripTest` + `SaveMigratorTest` stay green;
  ADD `GameStateMutatorTests` characterizing a handful of mutators (AddCrystals/AddToParty/ChooseHero/
  ResetToNewGame) so the split can't silently alter them. (Backend is stubbed/never-deployed per memory →
  sync layer testable by asserting `BuildDeltaPayload` shape, not by hitting a server.)

### 2. `HeroInventoryController.cs` (1559) → **dedupe into `ElarionUiKit` + extract the model**  ·  RISK low
**Finding (load-bearing):** ~280 lines (1212–1505) are private re-implementations of helpers that **already
exist** in `Assets/_Modules/Core/UI/ElarionUiKit.cs`: `AddImage`/`ApplyRounded`/`AddRimUnderline`/
`AddInnerRim`/`StyleButtonColors`/`RarityColor`/`RarityGlyph`/`RarityFrameStrength`/`BuildRoundedSprite`.
The header even says it's "mirrored from ArenaPanel" — i.e. copy-paste drift, the exact thing `ElarionUiKit`
was created to end.
- **Split:** (a) **delete the duplicated helpers**, call `ElarionUiKit.*` (mechanical, big line win, §1 one
  owner per concern). (b) Extract the small non-UI slice — `ResolveLoadout`, `HeroLevel`, `JobEligible`,
  rarity/glyph mapping (if not already in the kit) — into a `HeroInventoryModel`/presenter the view reads
  (§2: the view renders state it's given). The remaining file is a pure code-built view.
- **Risk low:** the dedupe is helper-for-helper identical (the kit's signatures match); the model slice is
  trivial pure functions.
- **Test gate:** ADD `HeroInventoryModelTests` (JobEligible / HeroLevel / rarity mapping) — these are the only
  logic in the file and are unit-testable without a Canvas. The UI build path is covered by compile + an
  owner smoke-open.

### 3. `AudioService.cs` (1111) → extract **music vs sfx vs scene-routing**  ·  RISK low-med
**Clusters:** music sources/crossfade/pools (266–611), sfx/voice/ui-click (626–725), volume/mixer/mute math
(726–826, includes static `LinearToDecibels`), scene→track routing + ambient choices (846–1068).
- **Split:** keep `AudioService` as the public `IAudioService` surface; extract `MusicMixer` (crossfade +
  pools), and `SceneMusicMap` (the static `TrackForScene`/`DefaultTrackFor`/`AmbientChoicesFor` lookup — pure
  data, already static). Sfx + volume math can stay or move to `SfxPlayer`.
- **Risk:** low-med — it's a singleton service with real audio state; the *routing/math* is pure and safe to
  pull, the source/crossfade plumbing is the touchy part.
- **Test gate:** ADD `AudioRoutingTests` over the pure statics (`TrackForScene`, `LinearToDecibels`/
  `DecibelsToLinear` round-trip, `DefaultTrackFor`). Extract the data map FIRST (zero-risk), then the rest.

### 4. `DevPanelController.cs` (1186) → **partial split, INTERIM**  ·  RISK low
Editor/dev-only (entire body under `#if DEVELOPMENT_BUILD || UNITY_EDITOR`). Three concerns: metrics panel
(332–512), action-group UI builders (577–751), and the cheat actions (752–1116). It's not in any shipped path.
- **Split:** carve into `DevPanelController.Metrics.cs` / `.Actions.cs` / `.Ui.cs` **partials** (same class).
  Class extraction has no holistic payoff here (dev tool, never ships, no reuse) → not worth the risk.
- **Risk low:** mechanical, compile-gated. **No new test required** (dev-only, §2c gate is for shipped
  behavior) — gate = compiles under the dev define + brace balance. Flag as INTERIM only for the line count.

### 5. `TowerPlacementRotateMenu.cs` (1111) → extract **orient-apply from the view**  ·  RISK low-med
The bulk is UI Toolkit panel building (BuildHeader/Viewport/AxisRow/ControlsRow/RuneStrip, ~330–740) — one
cohesive view. The non-view concern is `ApplyOrientToCatalog` + `CollectCatalogIds` + the orient-recipe
persistence (the "bake orientation into the catalog" path, §1 persistence concern).
- **Split:** extract `OrientRecipeWriter` (apply euler+scale to a catalog id, collect ids) as a plain class;
  the panel calls it on confirm. The RenderTexture/slider view stays.
- **Test gate:** ADD `OrientRecipeApplyTests` (apply produces the expected recipe entry). The view stays
  compile-gated.

### 6. `BuildModeController.cs` (1682) → extract **placement-rules + economy-ledger + build-camera**  ·  RISK med
**Clusters:** enter/exit + place/move/select loops (179–512); **placement validation geometry** (513–706:
`IsValidPlacement`, `FootprintWorldBounds`, `OverlapsExistingStructure`, `IsTooCloseToGate`, `OverlapsXZ`);
**economy/ledger** (1077–1210: cost/refund/afford/charge/refund-ledger/shortfall — all `static`);
**layout persistence** (1211–1314); **build camera** (1337–1537).
- **Split (class extraction, the real fix):**
  - `BuildPlacementValidator` — pure geometry/overlap/gate-distance (no Unity scene state beyond bounds in).
  - `BuildLedger` — the static cost/afford/charge/refund block (economy is its own layer, §1).
  - `BuildCameraRig` — pull-back/pan/restore (presentation/input concern, §2).
  - Controller orchestrates the loop + holds the seams.
- **Risk med:** it's a live player-facing flow (place/move/sell/upgrade). The validator + ledger are the
  safest pulls (mostly static, pure-ish); the camera rig touches live transforms.
- **Test gate (REQUIRED before touching):** ADD `PlacementValidationTests` (overlap, gate-proximity,
  footprint bounds) and `BuildLedgerTests` (afford/charge/refund math, atomic spend). Owner build-verify the
  place→charge→persist loop after. Do the validator + ledger first (testable, low risk); defer camera.

### 7. `Enemy.cs` (1331) → extract **combat/death vs nav vs presentation**  ·  RISK med
**Clusters:** nav driving + hero-aggro (606–797), contact/ranged attack (798–931), damage/hit-direction/death
(977–1234, incl. `ComputeHitDirection`, `Die`, death VFX, glimmer award), and **presentation** wiring
(`EnsureHealthBar`, `EnsureHitReaction`, `DriveAnimator`, anim-hash table 216–226, `EnsureAudio`/`PlayTypeSound`).
- **Split:** `EnemyCombat` (damage in / hit-dir / death sequence — exposes "died" event), `EnemyNav`
  (destination resolution + hero-aggro), and move the health-bar/hit-flash/animator/audio wiring behind a
  presentation seam (§2: the Enemy exposes HP/state; the view shows the bar — it must not own bar colors).
  Per §2b, `Enemy` is a collection entry composing Destructible/Targetable capabilities — keep that read-model.
- **Risk med:** core wave-loop object; death/VFX is where the two-combat-feel-stacks scar lives — be careful
  not to double-spawn VFX (§2b.1). `VillageSmokeTests` only proves it spawns, not combat correctness.
- **Test gate (REQUIRED):** ADD `EnemyCombatTests` — `TakeDamageFrom` reduces HP, `ComputeHitDirection`
  maps source→`HitDirection`, death fires once / awards once (guards double-death). EditMode-testable by
  configuring from an `EnemyDef` without a NavMesh if `EnemyCombat` is pulled out clean.

### 8. `WaveManager.cs` (1556) → extract **spawning vs reward-economy vs flow**  ·  RISK med
**Clusters:** countdown/flow state machine (394–618, `EnterCountdown`/`TickCountdown`/`StartWave`/
`CompleteWave`), **spawning** (729–1064: composed-family/smart-wave/apex-boss/batch/placeholder), **reward
economy** (1189–1350: `AwardWaveResources`/`AwardWaveCrystals`/`ScaledRoll`/`BuildRewardParts`/toast — pure-ish
math + an econ call), and breach/ATB handoff (1351–1464).
- **Split:** `WaveSpawner` (turn a `WaveDef` into spawned `Enemy`s at spawn points — and **route spawns
  through a pool**, §2b.2: this is one of the hot `Instantiate(` sites the law calls out), `WaveRewards`
  (the reward math + econ award, isolated), leaving `WaveManager` as the flow/state orchestrator.
- **Risk med:** the central loop. Reward math is the safest pull (mostly static helpers).
- **Test gate (REQUIRED):** ADD `WaveRewardTests` (`ScaledRoll`/`DueThisWave`/reward-parts deterministic for
  a seed) + `WaveSequenceTests` (countdown→spawn→complete advances wave id). Reward layer first (pure), then
  spawner (verify pooling didn't change spawn counts), owner build-verify the felt loop.

### 9. `ShopPanel.cs` (991) → **dedupe → `ElarionUiKit` + extract transactions**  ·  RISK low-med
Same drift as #2 — private gilt-frame/rim/button helpers that overlap `ElarionUiKit`; plus the txn logic
(`TryBuyWeapon`/`TryBuyArmor`/`TryBuyPotion`/`TrySell`/`TryEquip`/`ScaleCost`) interleaved with row-building.
- **Split:** route helpers to `ElarionUiKit`; extract `ShopTransactions` (buy/sell/equip against
  `EconomyService` + `VillageInventory` + `GearLoadout`) — the view calls it and re-renders (§2).
- **Test gate:** ADD `ShopTransactionTests` (buy deducts + adds to inventory atomically, sell refunds, equip
  updates loadout). `EconomyServiceTests` already covers the spend primitive underneath.

### 10. `BuildMenu.cs` (952) → extract **view vs build/upgrade actions**  ·  RISK med
UI Toolkit menu with a code fallback (337–447) + tower build/upgrade/repair actions (519–828:
`OnConfirmBuild`/`RenderUpgradeTower`/`CanAfford`/`InvokeRepairNearestWall`).
- **Split:** `TowerBuildActions` (confirm-build, upgrade, affordability, material counts) behind a thin call;
  the menu renders + delegates. (Note: overlaps BuildModeController's build path — confirm which is canonical
  before deep work; they may be two eras. Flag for owner.)
- **Test gate:** ADD `TowerBuildAffordTests` (`CanAfford`/`VariantFor`/`GetMaterialCount`).

### 11. `EnemyBrain.cs` (991) → extract **targeting vs tactical-movement**  ·  RISK med
Two clear halves: **target selection** (673–960: `ChooseTarget`/`ScoreAndPickTarget`/`ConsiderCandidate`/
`Find*` — pure scoring) and **tactical movement** (432–672: kite/reposition/destination/navmesh-sample).
- **Split:** `EnemyTargetSelector` (scoring → a chosen `Transform`, the most testable code in the file) and
  `EnemyTacticalMovement` (state→destination). Brain ticks both.
- **Test gate:** ADD `EnemyTargetingTests` — given candidate set + threat/HP inputs, `ScoreAndPickTarget`
  returns the expected target (deterministic). Pull the selector first.

### 12. `HeroBodySwapper.cs` (1174) → **partial INTERIM now, class extraction later**  ·  RISK med
Mostly a big procedural pipeline: load FBX → retarget materials to URP (487–613, large) → apply extracted
texture / tint / flat-steel stopgap (614–963) → normalize/strip/align/plant (964–1144). It's run-once at
scene start.
- **Now (interim):** carve the URP-material + texture-apply blocks into `HeroBodySwapper.Materials.cs`
  partial — they're the biggest, most self-contained chunk. Compile-gated, zero behavior change.
- **Later (real fix):** extract `UrpMaterialRetargeter` (reusable — `Tower`, `Enemy`, and the Tripo fixer all
  do their own URP retarget; this is a cross-cutting concern that should be ONE owner, §1). Defer because it
  touches shader/material guesswork that only an owner playtest validates (memory: tripo-material-fixer).
- **Test gate:** interim = compile + brace. Real extraction later = `UrpMaterialRetargetTests` (a known
  Phong material maps to the expected URP shader/keywords) + owner visual check.

### 13. `Tower.cs` (895) → extract **data/upgrade vs presentation/VFX**  ·  RISK med
Data/level/upgrade (94–643: `Initialize`/`SwapToType`/`Upgrade`/`TryEmpower`/`CurrentRange`/`CurrentDamage`)
vs presentation (249–595, 676–847: `ApplyVisualForLevel`, empowerment/upgrade VFX, code-aura/burst,
placeholder visual, `RetargetMaterialsToUrp`, a reflection-based `Shake`).
- **Split:** keep `Tower` as the gameplay entry (level/upgrade/range/damage state); move all VFX/aura/burst/
  visual-swap behind a `TowerView`/presentation seam (§2 — the tower must not own its aura colors). Note the
  reflection `Shake` (859–884) — candidate to drop in favor of the camera seam (CLAUDE.md: no new reflection
  bridges). The placeholder-visual + URP-retarget overlap #12's retargeter.
- **Test gate:** ADD `TowerUpgradeTests` (`Upgrade` raises level + range/damage per `TowerData`, `TryEmpower`
  gating). Pull the data/upgrade core out first (testable), defer the VFX presentation move.

### 14. `StoryCompanion.cs` (876) → extract **class-abilities vs follow/locomotion**  ·  RISK med
Class-ability casting (387–579: cleric mend / knight taunt / ranger multishot / mage burst + ally-scan) vs
follow/locomotion/speech (593–868: animator drive, combat move, leash, warp, speech bubble).
- **Split:** `CompanionAbilities` (per-class cast logic — data-ish, testable) vs the locomotion/follow
  controller. Speech bubble already routes through a `TownsfolkBubble` seam (good — §2).
- **Test gate:** ADD `CompanionAbilityTests` (`FindMostWoundedAlly` picks the lowest-HP ally; cooldown
  gating). Locomotion stays owner-felt.

### 15 + 16. `TitleController.cs` (1305) & `HeroSelectController.cs` (867) → **extract a SHARED roster-card kit**  ·  RISK med
**Finding (load-bearing — these two are near-duplicates):** both implement `BuildDragonStage`,
`BuildRosterPanel`, `BuildCards`/`BuildCard`, `BuildDetailCard`, `MakeStatRow`/`Pips`, `OnCardClicked`,
`PreselectFromSave`, `RefreshSelectionVisuals`, `RefreshDetailCard`, `ReflowForSize`, `VerifyFour*Even`,
`FindInfo`, `SetBorderWidth/Color` — the SAME hero-roster card UI, copy-pasted across two screens (the
TitleController-vs-HeroSelect divergence is a maintenance trap: a fix to one regresses via the other).
- **Split:** extract a shared `HeroRosterView` (or `HeroCardKit`) consuming `HeroCatalog`/`HeroCardInfo`
  (already shared types) — ONE owner of the 4-card roster presentation (§1, §2). Both controllers host it and
  keep only their screen-specific orchestration (Title = arrival sequence + wallet + start; HeroSelect =
  confirm + persist + dive). This collapses ~600 duplicated lines AND is the bounded-context fix.
- **Risk med:** UI Toolkit, owner-acceptance-sensitive (memory: "stop the hero-select regressing" + the
  4-cards-even invariant). Do them as ONE work order (file-disjoint from everything else; both files = one
  agent per §9).
- **Test gate:** ADD `HeroCardModelTests` (`FindInfo` maps class→info, `Pips` formats, preselect-from-save
  resolves the right class). The visual reflow + 4-even invariant is an owner build-verify on BOTH screens.

### 17. `SmartMobileCamera.cs` (955) → **DO NOT SPLIT (big-but-cohesive)**  ·  verdict: leave
This is **one bounded responsibility that is merely long**: a third-person follow camera (follow/aim,
collision push-in, occluder fade, enemy-scan auto-frame, shake, teleport-sync, sole-camera enforcement). Every
method serves the single "where does the camera go this LateUpdate" job; they share tightly-coupled per-frame
state (`_orbitYaw`, faded-occluder set, target). Splitting would create a chatty cross-class state seam for
**no holistic gain** and real risk to a hard-won, owner-validated feel (memory: camera-3d-thirdperson-validated,
camera-relative-follow-validated). **§3 says don't risk a working system for no felt benefit.**
- If line-count compliance is *mandated*, the ONLY safe move is an INTERIM partial (`.Collision.cs` /
  `.Targeting.cs`) — same class, zero behavior change. Prefer to leave it.

---

## 3. Recommended execution order (value × safety)

**Wave A — free wins (do immediately, mechanical, gate = compile/brace, no new tests needed beyond noted):**
1. **DELETE `BattleHud.cs` + `BattleVfx.cs`** — removes 1210+ lines of *dead* monolith. Highest leverage,
   lowest risk. (Verify 0 live refs first.)
2. **HeroInventoryController + ShopPanel — dedupe helpers into `ElarionUiKit`** (#2a, #9a). Mechanical,
   helper-for-helper; big line win; enforces §1 "one owner per concern." (Add the small model tests after.)
3. **DevPanelController — interim partial split** (#4). Dev-only, compile-gated.

**Wave B — low-risk class extractions WITH cheap pure-function tests (safe, high holistic value):**
4. **GameStateService → `GameStateSyncService`** (#1) — isolated network layer, core already well-tested.
5. **AudioService → `SceneMusicMap` + `MusicMixer`** (#3) — pull the pure data map first.
6. **TowerPlacementRotateMenu → `OrientRecipeWriter`** (#5).
7. The model/transaction extractions: `HeroInventoryModel` (#2b), `ShopTransactions` (#9b).

**Wave C — med-risk gameplay extractions — each NEEDS new characterization tests THEN owner build-verify:**
(do the testable sub-pull first in each; defer the presentation/camera/VFX sub-pull)
8. **BuildModeController** → `BuildPlacementValidator` + `BuildLedger` (#6) — tests gate it.
9. **WaveManager** → `WaveRewards` then `WaveSpawner` (pool the spawns) (#8).
10. **Enemy** → `EnemyCombat` then nav/presentation seam (#7).
11. **EnemyBrain** → `EnemyTargetSelector` first (#11).
12. **Tower** → data/upgrade core first, defer VFX (#13).
13. **BuildMenu** → `TowerBuildActions` (#10) — confirm canonical build path vs #6 first.
14. **StoryCompanion** → `CompanionAbilities` (#14).

**Wave D — med-risk UI dedupe (one WO, owner-acceptance-sensitive):**
15. **TitleController + HeroSelectController → shared `HeroRosterView`** (#15/#16). One agent, both files.
    Build-verify BOTH screens (4-cards-even invariant).

**Later / cross-cutting (its own WO, §2b.2 + §1):**
16. **`UrpMaterialRetargeter`** — ONE owner for the URP-retarget logic duplicated across HeroBodySwapper (#12),
    Tower (#13), Enemy, and the Tripo fixer. Interim-partial HeroBodySwapper now; unify later with tests.
17. **Pool consolidation** — WaveManager/Enemy spawns are hot `Instantiate(` sites (§2b.2); route through a
    pool when extracting `WaveSpawner`. Reconcile with `VfxPool`/`ProjectilePool`, don't greenfield.

**Do NOT touch:** SmartMobileCamera (#17, cohesive), VillageHudController (mid-rewrite WO-403),
GameState.cs (already-clean 302-line data SO).

---

## 4. The test-gate rule (per the law, §2c) — non-negotiable

> A refactor of a working subsystem does NOT get permission to be called *done* until tests prove behavior was
> preserved (`ARCHITECTURE_PRINCIPLES.md` §2c; memory: don't-patch-and-claim-fixed).

Applied here:
- **Mechanical / dead-code / interim-partial** (Wave A, DevPanel, interim partials): gate = **compile +
  brace-balance** (CLAUDE.md §1). No behavior changes ⇒ no new behavioral test required, but the compile gate
  is still mandatory.
- **Every class extraction that moves logic** (Waves B/C/D): gate = **the named new test(s) GREEN +
  pre-existing suite GREEN + (for player-felt gameplay) owner build-verify**. The new test must exist and pass
  BEFORE the extraction is merged — characterize first, then move the code, then prove the characterization
  still holds.
- **Build on the existing harness, don't greenfield** (§2c): mirror the proven patterns —
  `EconomyServiceTests`, `GameStateRoundtripTests`, the BattleATB `Tests/` suite, `Data/Tests/BuildingCatalogTest`.
  New tests live in `Assets/Tests/EditMode` or the module's own `Tests/` asmdef.
- **No "it's fixed" without the gate.** The structural WOs ship *with* their tests or they are not "ready."

---

## 5. Findings worth the owner's eye (architectural reads, not just line counts)

1. **`BattleHud.cs` is dead** (1210 lines) — superseded by `BattleHudUgui`; delete it + `BattleVfx`. Biggest,
   safest reduction on the list. (Confirm `BattleVfx` isn't still wired to live retro-VFX before deleting it.)
2. **Copy-paste UI drift is the dominant smell, not "long methods":**
   - HeroInventoryController & ShopPanel **re-implement `ElarionUiKit`** helpers (~280 + ~80 lines). The kit
     exists precisely to stop this; the fix is dedupe, not new structure.
   - TitleController & HeroSelectController are **near-duplicate roster screens** (~600 shared lines). This is
     an active regression trap (fix one, the other rots). Extract one `HeroRosterView`.
   These three dedupes alone retire ~1000 lines with low risk and directly serve §1 "one owner per concern."
3. **Cross-cutting URP-material retarget is duplicated 4×** (HeroBodySwapper, Tower, Enemy, TripoMaterialFixer).
   One `UrpMaterialRetargeter` owner (§1) — schedule as its own WO; touches shader guesswork → owner-felt gate.
4. **Hot `Instantiate(` spawn sites** (WaveManager, Enemy) line up with §2b.2's pooling directive — fold
   pooling into the `WaveSpawner` extraction rather than as a separate pass.
5. **Two cohesive long files are NOT monoliths** — `SmartMobileCamera` (one camera job) and `GameState.cs`
   (clean 302-line data SO, mis-listed as 1276). Splitting the camera would *hurt* a validated feel for no
   gain (§3). Leave both.
6. **`BuildMenu` vs `BuildModeController`** appear to be two eras of the build flow — confirm the canonical
   path before deep work on #10 to avoid refactoring a vestigial screen.
