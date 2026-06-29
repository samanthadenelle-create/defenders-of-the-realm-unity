# Technical-Debt Ledger — Authoritative Consolidation

> **Status:** PLANNING ONLY — no code changed in producing this. Consolidates five
> independent read-only debt scans (Error-Handling, Architecture, Dead/Orphaned,
> Scaffolding/TODO, Risky-Patterns) into one deduped, lane-grouped, sequenced paydown plan.
> **Date:** 2026-06-28. **Scope scanned:** `Assets/_Modules` (772 .cs) + `Assets/Editor` (172 .cs).
> **Method:** every entry below is reference-verified by its source scan (grep over `.cs` +
> GUID grep over `.prefab/.unity/.asset`). Where two scanners flagged the same `file:line`,
> the entry is **merged** with both notes and the cross-scan corroboration is called out.

Severity scale: **CRITICAL / HIGH / MED / LOW / INFO**. Effort: **S** (single-pass edit),
**M** (multi-file, hours), **L** (own WO, design surface). **Auto?** = a single edit-agent pass
could clear it under the compile gate with no behavior change.

---

## TOP 10 HIGHEST-LEVERAGE PAYDOWNS

| # | Item | Lane | Sev | Effort | Auto? | Why it's leverage |
|---|------|------|-----|--------|-------|-------------------|
| 1 | **Widen the `CoreServices` / `IVillageHud` interface seam** so the ~26–30 string-reflection bridges (HUD→Village *and* Village→HUD) become typed calls | UI / Core | CRITICAL | L | N | Single fix kills the §5 runtime violation in BOTH directions + the §0/§10 reflection ban + ~30 silent-breakage-on-rename surfaces. Highest structural ROI in the tree. |
| 2 | **Typed seam for the two PLAYER-FELT reflection bridges first**: `Enemy.cs:2035` (kill→Glimmer reward) + `PersistenceBridge.cs:193` (save-on-wave-clear) | Combat/AI + Core | MED | M | N | These two are where silent reflection breakage hits gold payout and save integrity — carve them out of #1 as a fast slice. |
| 3 | **Log-only silent-catch bundle** (`HeroProgression`, `WaveFeedbackDirector`, `AutoPilotInstaller`, haptics) | Combat/AI + Dev | MED | S | **Y** | 4 files, file-disjoint, log-only, no behavior change — clears the §12 "no silent failure" debt in one safe parallel lane. |
| 4 | **Memoize party-portrait `Resources.Load`** in `VillageHudController.cs:2967` | UI | MED | S | **Y** | Per-update string-concat + Resources lookup in a HUD setter; one `Dictionary<string,Sprite>` cache. Pure win. |
| 5 | **Collapse the 3 concurrent combat paths** (ATB vs BattleArena vs Wave-defense) behind one labeled V2 flag set | Combat/AI | HIGH | M | N | Three battle systems + two battle HUDs on interacting flags = the active "RCA: Battle Arena↔HUD" pain (task #43). Owner scope call, but biggest cognitive-debt reducer. |
| 6 | **Delete confirmed dead code**: `ATBBackgroundController.cs` (+meta, zero GUID refs) + 4 `#pragma CS0162` unreachable bodies | Combat/AI + UI + Editor | MED | S | **Y** | True orphans behind early-returns; safe-delete verified. Removes foot-guns from navigation. |
| 7 | **Resolve the duplicate `Village2Generator` class** (DeNelle.Editor + Assembly-CSharp, reflection-resolved → genuinely ambiguous) | Editor | HIGH | M | N | Name collision resolved by reflection is a live foot-gun; route through Task #47 / WO-584 gated archive. |
| 8 | **`ArenaDefenseCatalog.cs` → `arena-defense.json`** (~30 hardcoded stat rows, `// TODO data-driven`) | Combat/AI | MED | L | N | Largest single data-first violation; owner "thinks in data structures" — high design-alignment value. |
| 9 | **`VillageInventory.cs` crafting stub** returns fake success (no recipe check / no ingredient consume) behind a LIVE UI | Combat/AI (systems) | MED | M | N | Functional gap, not cosmetic — UI runs on a lie; real player-facing correctness. |
| 10 | **Reconcile `DeNelle.Village.asmdef` vs CLAUDE.md §5** ("Village → Core only" — reality refs 6 modules) | Core / Canon | MED | M | N | Doc-vs-code disagreement (§15 staleness flag); either update canon or route Audio/Wallet through `CoreServices`. |

---

## CROSS-SCANNER CORROBORATION (where multiple scans agreed)

- **Reflection bridges** flagged by **Architecture (A1/A2)**, **Risky-Patterns (A)**, and touched by **Error-Handling** — merged into UI §U1 + Combat §C1 below. Strongest signal in the tree.
- **`Village2Generator`** flagged by **Architecture (A4)**, **Dead/Orphaned (C)**, **Scaffolding (E)** — merged into Editor §E1.
- **`FlowTrace.Enabled = true`** flagged by **Scaffolding (A)** — explicitly **NOT** auto-fix (load-bearing for the §14 F8 watcher).
- **`EquipmentController` weapon mesh/grip table** flagged by **Scaffolding (B)** + tied to **Offset Forge WO-490** — merged into Combat §C7.

---

## GROUPED LEDGER (by §9 lane + subsystem)

### LANE: UI / HUD (presentation)

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| U1 | `HUD/AdminOverlay.cs:337-807`, `HUD/BattleHudVisibilityManager.cs:202-452`, `HUD/ClanChatPanelBootstrap.cs:66`, `HUD/CompassHudBootstrap.cs:70,92`, `HUD/CosmeticShopPanel.cs:46-158`, `HUD/CosmeticShopPanelBootstrap.cs:68` | **HUD→Village reflection** (`Type.GetType("DeNelle.Village.*")` + `GetMethod` by name) reaches WaveManager/EconomyService/HeroProgression/Wisdom/BattleHud9Zone/HeroLocomotion/Enemy. Bypasses §5 invisibly to asmdef; silent break on any rename. Corroborated by Architecture A1 + Risky A. | CRITICAL | L | N |
| U2 | `HUD/VillageHudController.cs:2967` (`SetPartyMember`) | Uncached `Resources.Load<Sprite>("HeroPortraits/"+name)` inside a per-update HUD setter pushed on every party-HP change. Memoize in `Dictionary<string,Sprite>`. | MED | S | **Y** |
| U3 | `HUD/VillageHudController.cs` (3046 lines) | §10 god-HUD; aggregates every HUD subsystem + is the target/source of U1. Partial-friendly already. Split by bounded context. | MED | L | N |
| U4 | `HUD/HeroTalentPanelBootstrap.cs:44` | `#pragma CS0162` dead body — legacy UIDocument spawn superseded by `HeroSkillTreePanelMvvm` (owns `PanelId.HeroTalents`). "RETIRED" comment. | MED | S (body) | **Y** |
| U5 | `HUD/XPBarController.cs:293` | `#pragma CS0162` dead `OnGUI()` IMGUI XP strip — "retired, see VillageHudController vitals". Whole body unreachable. | MED | S (body) | **Y** |
| U6 | `Core/UI/ElarionUiKit.cs` (1662 lines) | God-class presentation kit; split presentation vs domain. | MED | L | N |

### LANE: Combat / AI (code only — Enemy, ATB, Arena, Waves, Hero)

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| C1 | 16 `Village/**/*HudBridge.cs` (`Arena/ArenaHudBridge`, `Heart/HeartHudBridge:126`, `Hero/HeroAbilitiesHudBridge:132`, `HUD/TownHudBridge:214`, `NPCs/PartyHudBridge:143`, `NPCs/TalkHudBridge:93`, `Vfx/ComboHudBridge:91`, `Walls/WallRepairHudBridge:187`, `Waves/StartWaveHudBridge:112`, `Waves/WaveHudBridge`, `BuildMode/BuildModeHudBridge`, `Buildings/BuildMenuHudBridge`, +`Hero/HeroEquipHud:158`, `Hero/RaidEntryBridge:108`, `BuildMode/BuildButtonBridge:90`, `OnboardingIntegrator:116`) | **Village→HUD reflection** — `IVillageHud` too thin, so every HUD extra (SetMana/SetWaveProgress/SetComboCount/SetAbilityCooldown…) is `hudType.GetMethod("Name")`. Violates §0/§10 reflection ban; silent no-op on rename. Fix = promote methods onto `IVillageHud` (or segregated IWaveHud/IHeroHud/ICombatHud). Corroborated Architecture A2 + Risky A. | HIGH | M-L | N |
| C2 | `Village/Enemies/Enemy.cs:2035-2055` | Reflection resolves `GlimmerCurrencyService.TryAddGlimmer` by string → **enemy-kill currency reward silently stops** on rename. Both types in DeNelle.* — no assembly barrier reason. PLAYER-FELT. | MED | M | N |
| C3 | `Core/State/PersistenceBridge.cs:193-224` | Reflection subscribes to `WaveManager.OnWaveCleared` + `AddListener` by string → **save-on-wave-clear silently dies** on rename/retype. PLAYER-FELT (persistence). | MED | M | N |
| C4 | `Village/Hero/HeroProgression.cs:177,181,192,195` | `catch {}` swallows on `WisdomCurrencyService.Grant`, `SkillSystem.GrantSkillPoint`, `OnLevelUp/OnAnyLevelUp` invokes → level rewards / LevelUpSkillPopup can fail with zero trace. §12 no-silent-failure. Add `FlowTrace.Warn("Progression",…)` per catch; keep resilient fan-out. | MED | S | **Y** |
| C5 | `Village/Waves/WaveFeedbackDirector.cs:111,112` | `catch {}` on per-wave `WisdomCurrencyService.Grant` + `GlimmerCurrencyService.TryAddGlimmer` → silent loss of wave-completion currency. Add `FlowTrace.Warn("Waves",…)`. | MED | S | **Y** |
| C6 | `Village/Waves/WaveFeedbackDirector.cs:219,240,246,252,257,262` + `Village/Hero/HeroImpactFeedback.cs:104,116` | Empty `catch {}` around `Handheld.Vibrate()` / `Gamepad.SetMotorSpeeds()`. Platform haptics legitimately throw; defensible but off-standard. Convert to `Guard.Try(...)` for uniform self-report. | LOW | S | **Y** |
| C7 | `Village/Arena/ArenaDefenseCatalog.cs:18-187` | ~30 hardcoded HP/Damage/Range/Interval/PointCost rows, `// TODO data-driven: arena-defense.json`. Largest single data-first violation. Author JSON + DataInjector loader + regression. | MED | L | N |
| C8 | `Village/Hero/EquipmentController.cs:67-72,181` (2068 lines) | Weapon-id→KayKit mesh + grip (pos/euler/scale) hardcoded; `// TODO delete once weapons.json carries visualMesh/grip`. Converges with Offset Forge / AttachmentOffsetRegistry (WO-490). Also a god-class. | MED | L | N |
| C9 | `Village/Arena/DefensePatternLibrary.cs:1` | Placeholder MVP defense patterns; `TODO arena-defense-patterns.json`. Move to JSON when pool populates. | MED | M | N |
| C10 | `Village/Crafting/VillageInventory.cs:106-120` | Crafting stub: `CanCraft` ignores recipes, `Craft` returns `true` (fake success), no ingredient consume — UI runs on fake. Real recipe lookup + check/consume + output add. | MED | M | N |
| C11 | `Village/Items/ConsumableUseService.cs:14,18` | Buff / Mana / DoT effects only "recognised + logged TODO", not applied. Implement effect handlers. | MED | M | N |
| C12 | `Village/Buildings/Tower.cs:853-865` | `TODO: DEF-?? wire SlowAura/HealAura/FireAura/FrostNova/MagicalAffinity` — upgrade abilities unwired. | MED | M | N |
| C13 | `Village/Hero/HeroAbilities.cs:753,972,1009` | Temp shield = cheap self-heal stand-in; placeholder built-in particle VFX (ties Task #44 blocky-cube VFX). Real temp-shield + real ability VFX. | MED | M | N |
| C14 | `Village/Enemies/Enemy.cs` (2119), `Waves/WaveManager.cs` (2078), `Arena/BattleHud9Zone.cs` (1688), `Arena/BattleArena.cs` (1687) | God-classes; separate data/state from MonoBehaviour orchestration. | MED | L each | N |
| C15 | `Village/Buildings/UI/BuildMenu.cs:132,778,831-833` | `STUB hardcoded variant table until tower-variants.json`; material inventory "report fixed" values; per-call reflection (`GetMethod` WarpTo/AddXp). | MED | M | N |
| C16 | `Village/EconomyService.cs:24` | Starter resources hardcoded (Wood 200, Iron 80) → config/profile JSON. | LOW | S | N |
| C17 | `Village/Buildings/NPCUpgradeStation.cs:214-234` | "simple scale bump as growth / just log + scale as proof" — placeholder upgrade. Real anim/particle + state change. | LOW | M | N |
| C18 | `Village/Arena/BattleArena.cs:1074,1554`, `Core/SceneRouter.cs:291`, `Village/Hero/HeroLocomotion.cs:1058`, `Village/Buildings/Tower.cs:1099-1104` | Uncached per-call reflection (`GetMethod("WarpTo")`/`AddXp`/`MatchShake`). Fires rarely (perf fine) but fragile. Typed calls. | LOW | S-M | N |
| C19 | `Village/Hero/InventoryPaperDoll.cs:190-205` | Empty `catch {}` on `Resources.Load<Sprite>` fallback chain. Cosmetic (null handled downstream); optional `FlowTrace.Once` if all fallbacks miss. | LOW | S | **Y** |
| C20 | `Village/Hero/HeroChargeVFX.cs:71,91`; `HeroImpactFeedback.cs:70`; `Pets/PetAnimatorController.cs:96,106` | VFX/footstep entry points stubbed "wire from HeroCombat/PetCombat once it lands". | LOW | S | N |
| C21 | `Village/Arena/ArenaMode.cs:84,156` | `TODO data-driven: persist if needed`; wallet stub. Persist via save schema if design needs. | LOW | M | N |
| C22 | `Village/NPCs/StoryCompanion.cs:452` | `PlayHeroHit()` as placeholder heal cue — "no dedicated heal SFX yet (FLAGGED)". Add heal SFX id (asset add). | LOW | S | **Y** (asset add) |

> **Combat path consolidation (A3 — owner scope call):** `BattleATB/` (ATBCombatManager — flagged DEPRECATED side-path, `WaveBreachToAtb` OFF but still wired via `SceneRouter.GoBattle`) vs `Village/Arena/BattleArena.cs` (V1 DESCOPED/CUT, `Arena` OFF but preview-flipped ON) vs `Waves/WaveManager.cs` town defense, plus `World/Camps/RaidOutpostSystem`. **Three battle systems + two battle HUDs on interacting flags.** Recommend: pick V1 canonical (town wave-defense + walk-to raid), move ATB + BattleArena + BattleHud9Zone behind one labeled V2 flag set or `_V2Deferred`, document one combat owner. **Do NOT delete (gated per memory).** Sev HIGH, Effort M, Auto N. → Wave 3 (owner call).

### LANE: Monetization / Backend (isolated)

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| M1 | `Village/Buildings/TowerSwapService.cs:349` | `catch { /* non-critical — EventTracker already captured */ }` on analytics web request. Justified + documented. **Leave as-is.** | LOW | — | N |
| M2 | `Village/Monetization/RewardedAdManager.cs:92-96` | Stub ad: grants reward immediately, no SDK; `TODO Unity Ads/AdMob`. Intentional pre-backend. | LOW-MED | L | N |
| M3 | `Web3/JupiterSwapService.cs:13,281,291`, `Web3/WalletBridgeStub.cs`, `Wallet/StubWalletProvider.cs`, `Village/Arena/ArenaWalletService.cs:11,62,81` | Wallet/swap signing+submission stubbed; `STUB — replace with WalletService(Skr)+backend`. Intentional staged stub (data-architecture memory: Solana last). | LOW | L | N |
| M4 | `Wallet/CryptoPaymentManager.cs:257-351`, `Wallet/PackStore.cs:275`, `Cosmetics/BattlePassManager.cs:258-263` | String-keyed cross-module `Type.GetType` + `FindObjectOfType` reach (cached, guarded, optional features). | LOW | M | N |
| M5 | `HUD/CosmeticShopPanel.cs:46-158` | Resolves whole cosmetic catalog/service by string reflection (store path). | MED | M | N |

### LANE: VFX / Audio (no gameplay deps)

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| V1 | `Core/VFX/AttentionGlow.cs:54` | `new Material(Shader.Find("Sprites/Default"))` no null-guard → null-material magenta if absent. Add `?? Shader.Find("Universal Render Pipeline/Unlit")`. | LOW | S | **Y** |
| V2 | Magenta fallback-to-`Standard` sites: `Village/Buildings/Tower.cs:457,997`, `Buildings/CrystalMine.cs:462`, `Buildings/DefenseTower.cs`, `Buildings/BuildingInteractable.cs:529`, `Pets/PetDeployer.cs:955`, `BattleATB/AtbCombatantSwapper.cs:647`, `Core/EnvironmentTreeMaterialFixer.cs:403` | Built-in `Standard`/`Sprites/Default` fallback renders magenta under URP; mitigated by `MagentaGuard.cs` for objects it reaches. Verify coverage, no edit needed. | LOW | S (verify) | N |
| V3 | `Village` Audio direct-coupling (15 files: `Audio/BattleMusicManager`, `Vfx/VFXManager`, `Harvest/WorkerManager`, `World/WorldMusicDirector`…) | Direct `AudioService` type coupling instead of `CoreServices.Audio` seam (which exists, lines 64-75). Route through seam. (Sub-finding of E1-asmdef.) | MED | M | N |

### LANE: Core / State / Persistence

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| K1 | `Core/Progression/SkillSystem.cs:32` | `public static SkillSystem Instance;` is a public **mutable field** (not `{ get; private set; }`) — any code can null/reassign → stale state. Make it a private-set property. | LOW | S | **Y** |
| K2 | `DeNelle.Village.asmdef` vs CLAUDE.md §5 | §5 says "Village → Core only"; asmdef refs Core, AI, Cosmetics, Data, Pets, Wallet, Audio. Doc-vs-code disagreement (§15 staleness). Either update canon or route Audio→`CoreServices.Audio` + Wallet→Core `IStoreService` and drop refs. | MED | M | N |
| K3 | `Core/Diagnostics/FlowTrace.cs:24` | `Enabled = true` ships ON. **Deliberate** — load-bearing for the §14 F8 live-triage watcher. Flip via remote/config only, owner decision. **Do NOT auto-fix.** | MED | Trivial | **N** |
| K4 | `Core/State/GameStateService.cs` (1390 lines) | God-class; split by bounded context. | MED | L | N |
| K5 | `Core/Diagnostics/BreakCaptureHarness.cs:177,189,212,313,380,410,427` | Multiple empty `catch {}` — **by design** (the instrumenter must never throw; logs once at :448, re-routes at :235). **Do NOT instrument the instrumenter.** | LOW | — | N |
| K6 | `Core/State/SaveMigrator.cs` (v1→v27) | **REVIEWED CLEAN** — registry-based, additive-only, per-step unit-tested (`SaveMigratorTest.cs`); rejects newer-than-build saves; two try/catch steps log via FlowTrace. Healthy, not fragile. No action. Keep "additive-default-on-read skips a Steps entry" discipline. | INFO | — | N |
| K7 | DontDestroyOnLoad singleton fleet (GameStateService, AudioService, GlimmerCurrencyService, QuestService, ClanService, EventTracker, PersistenceBridge, ArenaMode…) | All guard duplicates via RuntimeInitializeOnLoad + scene-load dedupe. No stale-state bug found. `ArenaMode.Instance` lazy-creates a host on access even during teardown — one pattern to watch. | LOW | — | N |

### LANE: Dev Tools / Diagnostics

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| D1 | `DevTools/AutoPilotInstaller.cs:92,99,118,136` | Fully empty `catch {}` in arg/env parsing — dev-only "never break startup" is legit but a no-breadcrumb swallow hides a misconfigured fleet run. One-line `Debug.LogWarning` (lines 67/76 model it). | LOW | S | **Y** |
| D2 | `Village/Hero/VillageCamera.cs:11,38-41,…` | "TEMP DIAGNOSTIC (2026-05-25)" logs every cam script + hero-vs-cam velocity twice/sec from a per-frame path; "Strip once resolved." Drift bug fixed (commit `dd11da34`). Strip diag timers/logging; keep follow rig. | MED | S | **Y** |
| D3 | `Pets/PetDeployer.cs:111-130` | `DIAG_SKIP_ALL_PETS` TEMP DIAG const (currently `false`/reverted) + dead `if` branch still shipping. Delete const + branch. | LOW | S | **Y** |
| D4 | `Onboarding/TitleController.cs:365-376` | "Temporary panel-render diagnostic" in `Update()` — **entangled** with the live DEF-253 arrival watchdog in the same `Update()`. Separate the diag log from the watchdog first; do NOT auto-strip. | LOW | S | **N** |
| D5 | `DevTools/AutoPilotDriver.cs` (2215 lines) | God-class dev driver. Lower priority (dev-only). | LOW | L | N |
| D6 | `HUD/AdminOverlay.cs:32` | `OwnerWalletAddress = ""; // TODO(owner)` — empty admin-gate constant. Owner fills (or move to secure config). | LOW | Trivial | N (owner value) |

### LANE: World / Environment (architect lane — builders, scenes)

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| W1 | `Editor/ExteriorTerrainBuilder.cs:1144` | `#pragma CS0162` dead `PlacePonds()` body — disabled by owner 2026-05-20; comment refers to **retired** "cathedral spire" (canon = living world-tree) → double-stale. | LOW | S (body) | **Y** |
| W2 | `Editor/VillageSceneBuilder.Dressing.cs:22` | `#pragma CS0162` dead `BuildPlotFence()` body — disabled by owner ("wooden things disappear", 2026-05-20). | LOW | S (body) | **Y** |
| W3 | `Editor/CastleHubBuilder.cs` (2677), `Editor/DungeonSceneBuilder.cs:842` (2182), `BuildMode/BuildModeController.cs` (1845) | God-classes / placeholder hero capsule swapped at runtime. | MED/LOW | L | N |
| W4 | `Village/Cinematics/DragonCinematicFlyby.cs:264`, `Village/World/RuntimeRegionGate.cs:686-694` | String-keyed cross-module `Type.GetType` reach (cached, guarded). | LOW | S | N |
| W5 | `Village/Tutorial/TutorialDirector.cs:526,572`; `Village/NPCs/CastleVendorNpcInjector.cs:288` | `TODO(WO-277)` DailyQuestPanel.Pulse/SetObjective commented out; placeholder vendor "TODO real NPC art". | LOW | S | N |

### LANE: Editor / Tooling (Village2 cluster, orphans, codegen)

| ID | file:line | Issue | Sev | Effort | Auto? |
|----|-----------|-------|-----|--------|-------|
| E1 | `Editor/Village2Generator.cs` **AND** `Assets/_Village2/Village2Generator.cs` | **DUPLICATE** `public class Village2Generator : MonoBehaviour` (no namespace) in two assemblies (DeNelle.Editor + Assembly-CSharp); `Village2Build.cs` resolves it by reflection `FindTypeByName` → genuinely **ambiguous**. Both are the SUPERSEDED house-village generator (live Village2 = EnemyStrongholdBuilder output). Also has `TODO: place torches / scatter crates`. Corroborated Architecture A4 + Dead/Orphaned C + Scaffolding E. | HIGH (collision smell) | M | N — owner confirm |
| E2 | `Editor/Village2FinalizeApproach.cs`, `Village2Build.cs`, `Village2Playable.cs`, `Village2MakePlayable.cs`, `Village2GroundFill.cs`, `Village2IslandMap.cs`, `Village2OutpostFinalize.cs`, `Village2PlaceCrossing.cs`, `Village2PlaceGateCrossings.cs` | Menu-driven build/finalize tools for the superseded house-village approach; not invoked from code. Archive as ONE owner-gated batch (overlaps Task #47 / WO-584). | MED | M | N — confirm |
| E3 | `Editor/Village2BakeDiag.cs`, `Village2LayoutDump.cs`, `Village2NavMeshMeasure.cs`, `Village2NavRCA.cs`, `Village2TraversalDiag.cs` | One-shot nav-tunnel-era diagnostic menu tools. Keep (cheap) or archive with the E2 cluster. | LOW | S | N |
| E4 | `_Sandbox/` (asmdef `DeNelle.Sandbox`): `EncounterTrigger.cs` (diverged DUP of `Dungeons/EncounterTrigger.cs`), `GuardPatrol.cs` (0 ext refs), `CastleBuilder/DeepDungeonBuilder/EnemyOutpostBuilder/ProceduralCastleBuilder/UpgradableOutpostBuilder` (1-2 refs each) | Grok experiment pack. Fold into WO-584 / Task #47 consolidation. Safe-delete N until consolidation confirms winner. | MED | M | N |
| E5 | `Editor/Village3Builder.cs` (1009 lines, live `Defenders/Village3/*`) vs `Editor/EnemyStrongholdBuilder.cs` (1375) vs `VillageSceneBuilder.*` (13 partials) | Multiple competing scene-construction entry points. Document which is canonical per scene; retire losers. | MED | M | N |
| E6 | `_Modules/ATB/`, `_Modules/Economy/`, `_Modules/Characters/` | Empty/codeless module folders (READMEs only) that mislead navigation. Remove dirs+READMEs after verifying no .meta/scene GUID refs. | LOW | S | N (verify first) |
| E7 | `Editor/HudComposer/HudStubGenerator.cs` (15 TODOs), `HudComposerWindow.cs:448,508,512`; `DungeonStubBuilder.cs:205`; `CastleHomeBuilder.cs:194`; `VillageSceneBuilder.Characters.cs:273`; `Catalog/GearCatalogGenerator.cs:328,492` | By-design codegen templates / editor build placeholders / generator seed defaults. Editor-only, low priority. | LOW | — | N |

### CONFIRMED DEAD — safe-delete (orphaned, zero references)

| ID | file | Evidence | Sev | Auto? |
|----|------|----------|-----|-------|
| X1 | `BattleATB/ATBBackgroundController.cs` (+.meta) | String `ATBBackgroundController` appears ONLY in its own file; meta GUID `41b39cdfb34a89e499c011d006b8d858` in **zero** `.prefab/.unity/.asset`. VideoPlayer ATB background attached to nothing. **Rest of `BattleATB/` is LIVE — do NOT bulk-delete.** | MED | **Y** |

> **NOT dead (verified, do NOT remove):** `VillageSceneBuilder.*` (12 partials — Village.unity "abandoned" but builder + `"Village"` scene name still referenced by `SceneRouter`/`PersistenceBridge`/`BuildingInteractable`; §3 mandates rebuild-via-builder); `GroundZFightFixer`, `TreeOfLifeMaterialFixer` (referenced from live builders/SceneRouter); the live `BattleATB` core (`BattleController`/`ATBCombatManager`/`ATBRuntimeState`/`AtbCombatantSwapper` — still wired via SceneRouter/WaveManager/EncounterTrigger despite the overworld pivot); the three `#pragma 0618` slicers (`HudIconSlicer`/`ItemIconSlicer`/`ProjectileArtSlicer` — intentional obsolete-API suppression on a live batch tool); `JewelerStationInjector.cs:111` / `CraftingStationInjector.cs:120` placeholder-cube-on-pack-missing (graceful §4 degrade — leave).

---

## SEQUENCED PAYDOWN ROADMAP

### WAVE 1 — QUICK WINS (safe + S effort, high leverage, no behavior change)
All clearable under the compile gate by edit-only agents on file-disjoint silos, then one batch-gate. **No owner decision required.**

1. **Log-only silent-catch bundle** — C4 `HeroProgression`, C5 `WaveFeedbackDirector` (currency), D1 `AutoPilotInstaller`, C6 haptics→`Guard.Try`. (Error-Handling scan's recommended single S-effort WO.)
2. **U2** — memoize party-portrait `Resources.Load` in `VillageHudController.cs:2967`.
3. **Dead-code strips** — X1 delete `ATBBackgroundController.cs`+meta; U4 `HeroTalentPanelBootstrap` body; U5 `XPBarController.OnGUI` body; W1 `ExteriorTerrainBuilder.PlacePonds` body; W2 `VillageSceneBuilder.Dressing.BuildPlotFence` body.
4. **Diag strips** — D2 `VillageCamera` TEMP DIAGNOSTIC; D3 `PetDeployer` `DIAG_SKIP_ALL_PETS`.
5. **Hardening** — K1 `SkillSystem.Instance` mutable field → private-set property; V1 `AttentionGlow` shader null-guard.
6. **C19** `InventoryPaperDoll` fallback `FlowTrace.Once`; **C22** `StoryCompanion` heal SFX (asset add).

### WAVE 2 — STRUCTURAL (M/L, needs care, likely its own WO)
Mechanical-but-wide or needs a small design/loader. Each is a candidate WO.

1. **★ Interface-seam widening (U1 + C1)** — the headline fix. Promote the ~26–30 reflected HUD methods onto `IVillageHud` (+ segregated `IWaveHud`/`IHeroHud`/`ICombatHud`/Core grant interfaces); HUD↔Village both become typed `CoreServices.Hud?.X()` calls. Kills §5-runtime + §0/§10-reflection debt at once. **Slice C2/C3 first** (Enemy→Glimmer reward + PersistenceBridge save-on-wave-clear — the player-felt pair).
2. **Data-first migrations** — C7 `ArenaDefenseCatalog`→`arena-defense.json` (largest); C8 `EquipmentController` mesh/grip→`weapons.json` (converges Offset Forge WO-490); C9 `DefensePatternLibrary`; C15 `BuildMenu`→`tower-variants.json`.
3. **Functional gaps behind live UI** — C10 `VillageInventory` crafting stub; C11 `ConsumableUseService` effects; C12 `Tower` aura abilities.
4. **asmdef/canon reconcile (K2)** — route Village Audio (V3, 15 files) through `CoreServices.Audio` + Wallet through Core `IStoreService`, OR update CLAUDE.md §5 to the real dependency set (§15 same-breath canon fix).
5. **Editor consolidation** — E1 resolve duplicate `Village2Generator`; E2/E3 archive Village2* cluster; E4 `_Sandbox` fold; E5 Village3 vs Stronghold reconcile; E6 empty module dirs. Route through **Task #47 / WO-584** gated sweep (one owner-gated decision, not piecemeal).
6. **God-class splits** (as touched, not a campaign) — U3 `VillageHudController`, C14 `Enemy`/`WaveManager`/`BattleArena`/`BattleHud9Zone`, K4 `GameStateService`, U6 `ElarionUiKit`.

### WAVE 3 — RISKY / DEFERRED (needs design or owner call)
1. **A3 combat-path collapse** — pick V1 canonical, gate ATB+BattleArena+BattleHud9Zone behind one V2 flag set. Owner scope decision; ties to active task #43 RCA. Do NOT delete (gated per memory).
2. **Monetization stubs (M2/M3/M4/M5)** — intentional pre-backend; unstub when Solana/ad-SDK lands (staged per data-architecture memory). Leave until then.
3. **K3 `FlowTrace.Enabled`** — owner/config call only (load-bearing for §14 watcher). NOT a cleanup.
4. **D4 `TitleController` diag** — entangled with DEF-253 watchdog; separate before any strip.
5. **Lower-value content holes** — C13 temp-shield/VFX (Task #44), C16/C17/C20/C21, W5. Wire when their owning ticket lands.

---

## SAFE AUTO-FIX BATCH (single edit-agent pass, clears under the compile gate)

These are **log-only / dead-body-removal / null-guard / cache** edits with **no behavior change**,
on **file-disjoint silos** (§9 lanes) — a single edit-only pass then one orchestrator batch-gate
(`COMPILE_GATE_OK`), commit by explicit path. **No owner decision, no design surface.**

| ID | file:line | One-line action |
|----|-----------|-----------------|
| C4 | `Village/Hero/HeroProgression.cs:177,181,192,195` | Add `FlowTrace.Warn("Progression",…)` in each `catch {}` (keep fan-out). |
| C5 | `Village/Waves/WaveFeedbackDirector.cs:111,112` | Add `FlowTrace.Warn("Waves",…)` in each catch. |
| C6 | `Village/Waves/WaveFeedbackDirector.cs:219,240,246,252,257,262` + `Village/Hero/HeroImpactFeedback.cs:104,116` | Wrap haptics in `Guard.Try(...)`. |
| C19 | `Village/Hero/InventoryPaperDoll.cs:190-205` | Add `FlowTrace.Once` if all sprite fallbacks miss. |
| D1 | `DevTools/AutoPilotInstaller.cs:92,99,118,136` | One-line `Debug.LogWarning` in each empty catch. |
| D2 | `Village/Hero/VillageCamera.cs:11,38-41` | Remove the resolved 2026-05-25 TEMP DIAGNOSTIC velocity logging (keep follow rig). |
| D3 | `Pets/PetDeployer.cs:111-130` | Delete `DIAG_SKIP_ALL_PETS` const + dead `if` branch. |
| U2 | `HUD/VillageHudController.cs:2967` | Memoize portrait in `Dictionary<string,Sprite>`; load on miss only. |
| U4 | `HUD/HeroTalentPanelBootstrap.cs:44` | Remove the `#pragma CS0162` unreachable spawn body. |
| U5 | `HUD/XPBarController.cs:293` | Remove the dead `OnGUI()` IMGUI body. |
| W1 | `Editor/ExteriorTerrainBuilder.cs:1144` | Remove dead `PlacePonds()` body. |
| W2 | `Editor/VillageSceneBuilder.Dressing.cs:22` | Remove dead `BuildPlotFence()` body. |
| K1 | `Core/Progression/SkillSystem.cs:32` | `public static SkillSystem Instance` → `{ get; private set; }`. |
| V1 | `Core/VFX/AttentionGlow.cs:54` | Add `?? Shader.Find("Universal Render Pipeline/Unlit")` null-guard. |
| X1 | `BattleATB/ATBBackgroundController.cs` (+.meta) | Delete file + meta (zero GUID refs verified). |

**Excluded from auto-batch on purpose** (flagged Auto=N): K3 `FlowTrace.Enabled` (load-bearing §14),
D4 `TitleController` (entangled watchdog), M1/K5/K6 (justified/by-design catches — do NOT instrument
the instrumenter), all reflection-seam items (need an interface, not an edit), all data-first JSON
migrations (need loader + regression), the Village2*/`_Sandbox` cluster (owner-gated archive),
god-class splits, and the A3 combat-path collapse.

---

## NOTES & NON-FINDINGS (verified clean — recorded so we don't re-scan)

- **No `NotImplementedException`** anywhere in `Assets/`.
- **No empty comment-only catches, no "not a blocker" swallows**; cross-module `CoreServices.Hud/Audio` calls are null-guarded (§12 instrumentation pass already done on save/money/swap/network paths).
- **No per-frame `FindObjectsByType`/`GetComponent` in gameplay `Update()` loops** — all such hits are in bootstraps/dev-tools/diagnostics/one-shot resolves; `PetHarvester:58` + `MineNodeBridge:185` are throttled.
- **`SaveMigrator` (v1→v27) is HEALTHY** — registry-based, additive-only, per-step unit-tested. Not debt.
- **Singleton fleet is healthy** — all dedupe-guarded; only `ArenaMode.Instance` lazy-create-on-access-during-teardown to watch.
- **The `\ §12` / `\ Assembly` fragments** in raw grep output (`GameStateService.cs:290`, `SaveMigrator.cs:266`, `WorkerManager.cs:4`) are a grep rendering artifact — files contain correct `//` comments. **No §0 mount-garble found.**
- The three `#pragma 0618` slicers and `Standard`-shader fallbacks (mitigated by `MagentaGuard`) are intentional — not debt.
