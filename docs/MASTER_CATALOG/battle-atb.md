# MASTER CATALOG — battle-atb

Turn-based **Active-Time-Battle** combat system. Architecture: a deterministic
pure-C# **engine** (`Engine/`, no `UnityEngine`) wrapped by a runtime-only
**ScriptableObject store** (`ATBRuntimeState`) that raises UnityEvents, driven by
the scene **`BattleController`** which builds a **code-built uGUI HUD**
(`BattleHudUgui`) and swaps placeholder capsules for real 3D models
(`AtbCombatantSwapper`). Direct-C#-port of a TypeScript reference (`src/lib/atb/*`,
`src/store/atbStore.ts`) — RNG is golden-vector tested for bit-parity.

Scope dirs: `Assets/_Modules/BattleATB/` (all code) + `Assets/_Modules/ATB/`
(video only — code-empty/legacy). Scene: `Assets/Scenes/ATBBattle.unity`.
Assembly: **`DeNelle.BattleATB`** (ns `DeNelle.BattleATB.*`), refs
`DeNelle.Core`, `DeNelle.Data`, `Unity.Localization`, `UniTask`, `Unity.TextMeshPro`.

---

## CODE — Unity layer (root of `Assets/_Modules/BattleATB/`)

### BattleController.cs — `DeNelle.BattleATB` / asmdef `DeNelle.BattleATB`
**MonoBehaviour. Scene orchestrator** — bridges engine ↔ `ATBRuntimeState` store ↔ uGUI HUD ↔ 3D capsules. Lives on the "BattleHUD UIDocument" GameObject in ATBBattle.unity. **WIRED/LIVE.**
- Lifecycle: `Start()` — subscribes to runtime-state events, calls `BuildSetup()`, `_runtimeState.StartBattle(setup, source)`, **creates `BattleHudUgui` in code** (`new GameObject` + `AddComponent` + `.Build()`), wires `_hudUgui.OnAction → _runtimeState.ChooseAction`, then `ATBCombatManager.Instance?.StartCombat()`. `Update()` ticks visual ATB only. `OnDisable()` unsubscribes.
- `[SerializeField]`: `_runtimeState` (ATBRuntimeState), `_heroCapsule`/`_enemyCapsule` (Transform), `_fallbackSeed=42`, `_fallbackEnemyDefId="skeleton"`, `_fallbackHeroName="Blaise"`, `_returnDelaySeconds=2.5`.
- Key methods: `BuildSetup()` builds `BattleSetup` from `SceneRouter.PendingBattle` or dev fallback; `BuildParty()` surfaces ≤4 members (hero + pets from `GameStateService`); `BuildEnemyRoster()`/`MapToEngineDef()` map breach ids → `ENEMY_DEFS` keys; `ResolveHeroClass()`/`ResolveHeroName()` read live `GameState.HeroClass`; `BuildInventory()` reads `GameState.Inventory`; `ResolveSource()` (Wave>0 ⇒ Village/Last-Stand, gates Flee) ; `ResolveReturnScene()` (handoff ReturnScene, refuses to reload ATBBattle); `ReturnAfterResult()` `async UniTask` (never async void) → `SceneRouter.LoadSceneWithFade`.
- 3D anim drive: `TryDriveActionAnim` (PlayAttack combo / PlayCast / enemy windup), `TryDriveHitAndDeathAnims` (PlayHit / Die via `ActorAnimator`), `SpawnFloatingDamage` (world-space TMP) — all use a `_lastProcessedLogIndex` cursor so cumulative log isn't replayed each turn.
- Depends on: `Engine.*`, `State.ATBRuntimeState`, `DeNelle.Core` (SceneRouter, GameStateService, HeroClassOpt), `DeNelle.Core.Combat.ActorAnimator`, `AtbControlModeStore`, `BattleHudUgui`, `ATBCombatManager`, Cysharp UniTask, TMPro.

### BattleHudUgui.cs — `DeNelle.BattleATB`
**MonoBehaviour. FF7-style code-built uGUI HUD** (Canvas + Image + TMP + Layout groups; procedural rounded/circle/ring sprites; WebGL-safe, no UXML). **WIRED/LIVE** — instantiated by BattleController at runtime. Replaces the retired VisualElement `BattleHud`.
- Layout: top `BattleInfoPanel` ("The Last Stand" title / WAVE / active turn), bottom-left `CommandPanel` (Attack/Skills/Item/Defend + dynamic skills sub-panel), bottom-right `PartyStatusPanel` (4 fixed slots: portrait+ring, name, HP/MP/ATB bars).
- Public: `Action<BattleAction> OnAction`, `Action<string,ControlMode> OnControlModeToggled` (declared, **never invoked** — no control-mode toggle UI exists); `Build(Canvas=null)`; `Render(ATBRuntimeState)`; `TickVisualAtb(ATBRuntimeState, dt)` (cosmetic ATB charge sim, engine is discrete); `Reset()`; `ActiveUnitId`.
- Real art via `Resources/HudIcons/<Class>/...` (cached `LoadHudIcon`); per-class ability icons mapped Q/W/E/R by slot (engine ability names ≠ icon filenames). Mage portrait falls back `"wizard"`→`"wiard"` (typo in staged art, noted in code). `DressPackBar` skins gauges from `RpgUiCatalog` (`DeNelle.Core.UI`) when the pack is imported, else procedural.
- Depends on: `Engine.*` (Defs.HERO_ABILITIES, BattleState/BattleUnit/AbilitySlot), `State.ATBRuntimeState`, `DeNelle.Core.UI.RpgUiCatalog`, uGUI, TMPro.

### AtbCombatantSwapper.cs — `DeNelle.BattleATB`
**static class. `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` bootstrap** — self-installs (no scene edit): hooks `SceneManager.sceneLoaded` + runs once on the active scene. On any scene whose name contains "ATBBattle". **WIRED/LIVE.**
- `HideStrayWorldVisuals()` (WO-381#1) deactivates DontDestroyOnLoad village objects (`DeNelle.Village.HeroLocomotion/StoryCompanion/Enemy/PetContextualBehaviour/Worker`) **by reflection** (BattleATB must not ref Village).
- `SwapHero()`: finds `HeroCapsule`, loads `Resources/Heroes/<slug>` (slug from `GameStateService.State.HeroClass` — **direct field read**, see FLAG F-SWAP-1), adds `ActorAnimator`, sizes/recenters by bounds (Tripo off-pivot trap), binds per-class basecolor atlas via `TripoMaterialFixer.SetFallbackTexture`, hides the pill renderer.
- `SwapEnemy()`: loads `Resources/Enemies/<ResolveEnemySlug()>` (hard-coded `"Skeleton_Warrior"`, see FLAG F-SWAP-2), faces hero via `LookRotation`, stamps shared enemy animator controller (`EnemyControllerFor` map), `TripoMaterialFixer` by reflection; falls back to violet `TintEnemy` if no model.
- Depends on: `DeNelle.Core.State` (direct), `DeNelle.Core.Combat.ActorAnimator` (typed), `TripoMaterialFixer` (reflection), reflection for Village types. `HideOwnRenderer()` is **dead** (defined, never called).

### ATBCombatManager.cs — `DeNelle.BattleATB`
**MonoBehaviour singleton** (`Instance`, `[DisallowMultipleComponent]`). **Turn idle-timer** (WO-68/93): counts up while combat active + player turn; on timeout (`maxTurnTime=8s`) fires `onEnemyAutoAttack` UnityEvent. **Partially live** — BattleController calls `StartCombat`/`OnPlayerActed`/`StopCombat`; but `onEnemyAutoAttack`/`onPlayerTurnStart` UnityEvents have **no persistent listeners** in the scene asset (see FLAG F-MGR-1). `TurnProgress`/`IsActive` exposed for HUD but `TickVisualAtb` is what actually drives the bars.
- Public: `StartCombat()`, `OnPlayerActed()`, `StopCombat()`, props `TurnProgress`, `IsActive`. Requires a scene instance (singleton); not auto-created.

### AtbControlModeStore.cs — `DeNelle.BattleATB`
**static class. Per-member ControlMode (Player/AI) persistence** via `PlayerPrefs` (key `atb.controlMode.<id>`), WO-169 P0. Seam designed to later move to GameState. **WIRED** — read/seeded in `BattleController.MakeMember`, written by `BattleController.HandleControlModeToggled` (which is itself **never called** — no toggle UI; see FLAG F-CTL-1).
- Public: `Get(id, fallback)`, `Set(id, mode)`, `HasPreference(id)`.

### ATBBackgroundController.cs — `DeNelle.BattleATB`
**MonoBehaviour.** Plays a random looping `VideoClip` on a serialized `VideoPlayer` in `Start()`. **DORMANT/orphan** — not added by BattleSceneBuilder, not present in ATBBattle.unity wiring; intended to play `Assets/_Modules/ATB/Video/ATBBackground{1,2}.mp4` but nothing instantiates/wires it. No-op if fields unset.

---

## CODE — State layer (`State/`)

### ATBRuntimeState.cs — `DeNelle.BattleATB.State`
**runtime-only `ScriptableObject`** (`[CreateAssetMenu]`). Port of `src/store/atbStore.ts` (Zustand mirror). Holds the live immutable `BattleState` snapshot; **all mutation routes through `Engine` statics**; raises UnityEvents. **WIRED/LIVE** (asset at `Generated/ATBRuntimeState.asset`, referenced by the scene's BattleController).
- Events: `OnActionSubmitted`, `OnTurnResolved`, `OnOutcome` (BattleResultEvent), `OnBattleChanged` (typed `UnityEvent<BattleState>` subclasses).
- Props: `Battle`, `Source`, `Result` (survives EndBattle), `Resolving`, `AutoStepAi=true`. Selectors: `ActiveUnit`, `IsAwaitingPlayer`, `IsOver`, `Outcome`, `Party`, `Enemies`.
- Actions: `StartBattle(setup, source)`, `ChooseAction(action)` (submit hero + drain AI to next player turn), `StepAi()` (one AI turn), `AutoResolve()`, `SetAutoStepAi(bool)`, `EndBattle()`. Clones at boundary (`BattleStateOps.CloneBattle`) so the SO owns isolated state. `ApplyFullResetAfterFight` (const `FullResetAfterFight=true`) restores party HP/MP on win.
- `OnEnable()` resets transient state (never carries a stale snapshot — runtime-only).
- Also defines: `enum BattleSource {Village,Dungeon}`, `class AtbBattleResult`, `BattleStateEvent`, `BattleResultEvent`.
- Does **not** touch GameState — the caller reads `Result` and applies Heart/building damage.

---

## CODE — Engine (`Engine/`, pure C#, no UnityEngine except CombatantDefSO)

All ns `DeNelle.BattleATB.Engine`, asmdef `DeNelle.BattleATB`. Static/data classes — deterministic, port of `src/lib/atb/*`.

| File | Class(es) | Responsibility | Key public surface |
|---|---|---|---|
| Types.cs | enums + data classes + `EnumTokens`, `PortMath` | All shared types (Side, ActionKind, AbilitySlot, ElementType, StatusKind, ItemKind, PetAiMode, BattleOutcome, BattlePhase, TargetMode, EnemyArchetype, **HeroClass{Mage,Knight,Ranger}**, PetSpecies, UnitKind, **ControlMode{Player,AI}**, BattleLogEvent) + `StatusEffect`, `AbilityDef`, `ItemDef`, `EnemySpecial`, `EnemyDef`, `BattleUnit`, `RallyReserveUnit`, `BattleLogEntry`, `BattleState`, `BreachEnemySpec`, `PartyPetSpec`, **`PartyMemberSpec`**, `BattleSetup`, `DamageInput`, `DamageResult`, `BattleAction` | `BattleAction.MakeAttack/MakeAbility/MakeItem/MakeDefend/MakeRally`; `PortMath.RoundTs` (JS Math.round half-up — NOT banker's). Type-only, no behaviour. |
| Defs.cs | `Defs` + structs `StatusBlueprint`,`HeroClassStats`,`PetSpeciesStats` | Static tuning tables (byte-identical to TS) | `ATB_FULL=100`, `ATB_RESET=0`, `SLOW/HASTE_FILL_MUL`, `MAX_PARTY=8`, `MAX_ENEMIES=8`, `CRIT_CHANCE=.12`, `CRIT_MULT=1.6`, `BOSS_EVERY`; `STATUS_BLUEPRINTS`, `HERO_ABILITIES` (Knight/Ranger/Mage Q/W/E/R), `HERO_STATS`, `PET_STATS`, `PET_ABILITIES`, `ITEM_DEFS`, `ENEMY_DEFS` (7: goblin, skeleton, bruiser, necromancer, hollow-captain, hollow-king, hollow-apprentice). `ATB_BASE_FILL=12` is **dead** (F-DEFS-1). |
| BattleScaling.cs | `BattleScaling` + struct `WaveScaling` | Endless-wave difficulty curve | `BOSS_EVERY=6`, `IsBossWave`, `BossOrdinal`, `BossHpMul`, `WaveScaling(wave)`. |
| Rng.cs | `Rng` (class), `RngOps` | Deterministic mulberry32 PRNG (bit-parity w/ TS) | `CreateRng(int/uint)`, `RngNext`→[0,1), `RngInt`, `RngChance`, `RngPick<T>`. Seed is `uint`, Rng is a **reference type** (must alias-mutate — golden-vector critical). |
| BattleState.cs | `BattleStateOps` | Bond helpers, unit builders, battle construction, pure read helpers, deep clone | `PetBond*Mul`, `PetUnlockedAbilityCount`, `BuildHeroUnit`/`BuildPetUnit` (legacy + WO-169 id+ControlMode overloads), `BuildEnemyUnit`, `CreateBattle(setup)`, `GetUnit`, `LivingUnits`, `LowestHpEnemy`, `HasStatus`, `StatusFillMul`, `IsBattleOver`, `ComputeOutcome`, `AvailableAbilities`, `UnitAbilityKit`, `FindAbility`, `CooldownOf`, `CloneBattle`. Multi-member party path is authoritative; legacy single-hero path byte-identical. |
| Turn.cs | `Turn` | ATB-fill turn order + begin/resolve/finish pipeline + auto-resolve | `AdvanceToNextTurn`, `ReadyUnit`, `IsPlayerControlled` (reads `ControlMode`, **not** UnitKind — WO-169), `BeginNextTurn`, `ResolveAiTurn`, `SubmitAction`, `FinishTurn`, `EndBattle`, `StartBattle`, `AutoResolveBattle(maxTurns=5000)`, `AutoHeroAction`. |
| Actions.cs | `Actions` + struct `StrikeOpts` | resolve* family + `ApplyAction` dispatcher (mutates state) | `Strike`, `ResolveAttack`, `ResolveAbility` (damage/splash/heal/status/self-status), `ResolveItem`, `ResolveDefend`, `ResolveRally`, `ResolveEnemySpecial`, `ApplyAction`. Unusable ability/item/rally falls back to attack/defend. |
| Combat.cs | `Combat` | Damage calc, HP/resource apply, status apply/consume/cleanse/tick | `ElementMultiplier` (flame>ice>aether>flame RPS), `CalculateDamage` (aether ignores armour, Mark +30%, Defend ×0.5, crit, ±8% spread; Shield negates after both RNG draws), `ApplyDamage`, `ApplyHeal`, `ApplyResource`, `ApplyStatus`, `ConsumeStatus`, `CleanseStatuses`, `TickStatuses` (burn/poison/bleed/regen/freeze/stun). |
| Ai.cs | `Ai` | Pure action-choosing for AI units (imports neither Actions nor Combat) | `ChooseEnemyAction` (per-archetype special chance), `PickEnemyAttackTarget` (tank→lowest-defense, else random), `ChoosePetAction` (aiMode support/damage, draws NO RNG). |
| Targeting.cs | `Targeting` | Pure target resolution | `ResolveTargets(mode,…)`, `AdjacentUnitIds` (Mage R Tempest splash). |
| CombatantDefSO.cs | `AbilityDefSO`,`EnemyDefSO`,`HeroStatsSO`,`PetStatsSO` (ScriptableObjects) | **OPTIONAL designer mirrors** of Defs tables — engine **never reads them**. Only Engine file depending on UnityEngine. | `ToAbilityDef/ToEnemyDef/ToStats`. **No asset instances exist** (no `.asset` of these types in repo) → effectively **dead/unused** infrastructure. |

---

## SCENE / PREFABS / CONTROLLERS

### `Assets/Scenes/ATBBattle.unity` — WIRED, but carries stale wiring
Built by `BattleSceneBuilder` (Editor). Contents: `ATBBattleRoot` (Camera @ (0,4.1,-6.6) 22°/46°FOV, Directional Light, dark Ground plane, `HeroCapsule` violet @ (-2.4,1,0), `EnemyCapsule` crimson @ (+2.4,1,0)), `EventSystem`, and **"BattleHUD UIDocument"** GameObject carrying a `UIDocument` (BattleHUD.uxml + BattlePanelSettings) **and** the `BattleController`.
- BattleController serialized fields in-scene: `_runtimeState` → `Generated/ATBRuntimeState.asset`, `_heroCapsule`/`_enemyCapsule` wired, **`_hudDocument: {fileID 1032066911}` ← STALE** (field no longer exists on BattleController — orphaned serialized value, harmlessly ignored at load). The UIDocument + its UXML HUD are **no longer the live HUD**; `BattleHudUgui` is code-built in `Start()`.

### Generated assets (`Generated/`)
- `ATBRuntimeState.asset` — the live store SO instance (all 4 UnityEvents have empty persistent-call lists; events are wired in code via `AddListener`). **LIVE.**
- `BattlePanelSettings.asset` — PanelSettings for the (now-orphaned) UIDocument HUD. **STALE/orphan** w.r.t. live HUD.

### UI (`UI/`) — ORPHANED
- `BattleHUD.uxml` + `BattleHUD.uss` — the old UXML/UIDocument HUD. Loaded by BattleSceneBuilder into the scene's UIDocument but **NOT the live HUD** (code-built `BattleHudUgui` replaced it; UXML doesn't render reliably in builds — see project memory). Dead for runtime purposes.

### External wiring (live entry path)
- `WaveManager.cs` (`DeNelle.Village`) on tree-breach → `SceneRouter.GoBattle(BattleParams{Wave,BreachedIds,…})` → stashes `SceneRouter.PendingBattle`, fades into `ATBBattle`. BattleController reads `PendingBattle` on the far side; returns via `BattleParams.ReturnScene`.
- `EncounterTrigger.cs` (Dungeons + _Sandbox) is the dungeon-encounter entry to the same flow.
- `SceneRouter` (`DeNelle.Core`): `const ATBBattle="ATBBattle"`, `BattleParams{Wave, BreachedIds, ReturnScene}`, `GoBattle(p)`, `PendingBattle`, `LoadSceneWithFade`.

---

## DATA / JSON
- **No JSON** in this area. All combat data is C# static tables in `Defs.cs`/`BattleScaling.cs` (port strategy "option A" — byte-identical to TS reference, trivially diffable). `ENEMY_DEFS` = 7 entries; `HERO_ABILITIES` = 3 classes × 4; `PET_ABILITIES` = 3 species × 2; `ITEM_DEFS` = 3; `STATUS_BLUEPRINTS` = 10. Hero/enemy/pet ids are mapped from village `enemies.json` ids via `BattleController.MapToEngineDef` (hollow-walker→skeleton, hollow-warrior→bruiser, etc.).

## VIDEO (`Assets/_Modules/ATB/Video/`)
- `ATBBackground1.mp4`, `ATBBackground2.mp4` — intended ATB background loops for `ATBBackgroundController` (which is unwired → currently unused).

## TESTS (`Tests/`, asmdef `DeNelle.BattleATB.Tests`, Editor-only)
Per-engine-file NUnit tests: `ActionsTest`, `AiTest`, `BattleScalingTest`, `BattleStateTest`, `CombatTest`, `RngGoldenVectorTest` (bit-parity guard), `TargetingTest`, `TurnTest`, `TestSupport`. **LIVE.** Refs `DeNelle.BattleATB`, `DeNelle.Core`, `DeNelle.Data`, TestRunner, nunit.

## DOCS
- `Assets/_Modules/BattleATB/README.md` — module README. Title + layout + FF7-HUD + combat-feel notes. **PARTIALLY STALE** (see F-DOC-1): lists `BattleHud`, `BattleVfx` in the Root layout — neither file exists (BattleHud retired for BattleHudUgui; BattleVfx removed, only referenced in a stale code comment). FF7-HUD section also says "Blue/grey FF7 aesthetic" / "blue box" but the live HUD is **light parchment/gilt** (re-skinned). Otherwise accurate.
- `Assets/_Modules/ATB/README.md` — "EMPTY / LEGACY", correctly says code lives in BattleATB. **CURRENT.**
- (Referenced, outside scope: root `ATB_DEBUGGING_GUIDE.md`.)

---

## FLAGS

### Stale comment / doc vs. code
- **F-DOC-1 (README stale):** `BattleATB/README.md` Root layout lists `BattleHud` and `BattleVfx` — **both files do not exist**. `BattleHud` (VisualElement) was replaced by `BattleHudUgui`; `BattleVfx` is gone (only a stale BattleController comment mentions "old BattleVfx log-diff cursor"). README also describes the HUD as "Blue/grey FF7"/"classic FF7 blue box" while the live `BattleHudUgui` is **light parchment + gilt** (ElarionUi palette). Update README layout + aesthetic lines.
- **F-CMT-Tempest:** `BattleHudUgui.AbilityIconBySlot` comment maps Mage E "Frost Nova" → `Wizard_Lightining` (sic) and rationalizes "Lightning reads as a nova" — cosmetic, but the icon filename has a typo (`Lightining`) baked in as the real Resources key.
- **F-PORT note (not a bug):** the `F-*` codes throughout the engine (F-RNG-1, F-STATE-1, F-CMB-1, F-ACT-1..3, F-TARG-1/2, F-AI-1, F-DEFS-1) are intentional **port-fidelity markers** documenting deliberate TS-matching quirks (banker's-rounding avoidance, RNG-draw ordering, splash-0=no-splash, enemy self-targeting SingleAlly). These are accurate to code, not stale.

### Scene wiring stale / orphaned
- **F-SCENE-1:** `ATBBattle.unity` BattleController still serializes **`_hudDocument`** — a field that **no longer exists** on `BattleController.cs`. Orphaned serialized reference (ignored at load, no crash). The scene's `UIDocument` + `BattleHUD.uxml` + `BattlePanelSettings.asset` are likewise **orphaned** — the live HUD is the code-built `BattleHudUgui`. Cleanup: rebuild scene via an updated `BattleSceneBuilder`.
- **F-BUILDER-1:** `Assets/Editor/BattleSceneBuilder.cs` is **STALE** — it wires `_hudDocument` (gone) and builds the UXML/UIDocument HUD path that BattleController no longer uses. Re-running it reproduces the orphaned UIDocument. It does NOT add `ATBBackgroundController`. Needs a refresh to match the code-built-HUD reality.

### Dead / unused code
- **F-DEFS-1:** `Defs.ATB_BASE_FILL=12` — dead constant, no engine reference (event-step sim; exported for parity only). Documented in code.
- **F-SWAP-dead:** `AtbCombatantSwapper.HideOwnRenderer` is defined but never called.
- **F-CTL-1:** `BattleController.HandleControlModeToggled` and `BattleHudUgui.OnControlModeToggled` exist but are **never invoked** — there is no in-battle/Settings control-mode toggle UI. `AtbControlModeStore` is read/seeded but the player-facing flip half of WO-169 is unbuilt. (Engine support is fully present: `ControlMode`, `IsPlayerControlled`.)
- **F-SO-DEAD:** `CombatantDefSO` family (`AbilityDefSO/EnemyDefSO/HeroStatsSO/PetStatsSO`) — optional designer mirrors the engine never reads; **no `.asset` instances exist** in the repo → unused infrastructure.
- **ATBBackgroundController** — dormant orphan (not wired into the scene; `ATB/Video/*.mp4` unused).

### Scene-gated / disabled systems
- **F-MGR-1:** `ATBCombatManager`'s idle-timer is half-wired — BattleController calls Start/Acted/Stop, but its `onEnemyAutoAttack` (the actual "enemy auto-attacks if you idle", WO-93) and `onPlayerTurnStart` UnityEvents have **no listeners** (none in code, none in scene). So the 8s idle timer fires nothing — the punitive auto-attack is effectively disabled. `TurnProgress` is also unused (HUD ATB uses `TickVisualAtb` instead).
- **AutoStepAi / StepAi / AutoResolve** paths in `ATBRuntimeState` are implemented + tested but the live BattleController always runs with default `AutoStepAi=true` (no paced/animated single-AI-step UI hooked up).

### Comment-vs-code mismatches (the HeroLocomotion-class check requested)
- **F-SWAP-1 (resolved correctly — noting the prior trap):** `AtbCombatantSwapper.ResolveHeroSlug` comment explicitly documents that the OLD code used reflection `GetProperty("HeroClass")` which returned null because **`GameState.HeroClass` is a FIELD not a property** → ATB hero was always Mage. Current code does a **direct field read** via `GameStateService.Instance.State.HeroClass`. Code matches comment; the comment is an accurate post-mortem, not stale.
- **F-SWAP-2:** `AtbCombatantSwapper.ResolveEnemySlug()` is **hard-coded to `"Skeleton_Warrior"`** with a `// TODO: read the live encounter def to vary the model`. The enemy model never varies by the actual breach roster (necromancer/orc/dragon defs exist in `ENEMY_DEFS` + `EnemyControllerFor` maps them, but `ResolveEnemySlug` ignores the encounter). Contradiction between the rich enemy-def system and the single fixed visual.
- **F-CTRL-comment:** `BattleController.IsCasterHeroClass()` decides caster-vs-melee anim by **string-matching `_fallbackHeroName`** (`Contains("Mage"|"Thrain"|"Elara")`) — but `_fallbackHeroName` is the *dev fallback* ("Blaise"), not the resolved hero name. Its own comment admits "In a full multi-party this would inspect the ActiveUnit kind." So action anims can mis-pick attack vs cast for the real selected hero. Logic-vs-intent gap.

### Contradictory / risk
- **F-WAVE-1:** `BattleHudUgui.Render` hard-codes `_waveText.text = "WAVE 1"` (comment: "wave info can come from runtime or setup in full integration") even though `BattleState.Wave` is real and scaled. The displayed wave is always "WAVE 1" regardless of the actual breach wave.
- **F-CTL-1 (dup-listed above)** — engine ControlMode plumbing complete but no UI to exercise it.
