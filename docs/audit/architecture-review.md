# Architecture & Code-Hardening Review — Defenders of the Realm (v2 Unity Port)

**Reviewer:** Software-architecture audit pass
**Date:** 2026-05-19
**Scope:** Module isolation, dependency direction, code hardening (Weeks 1–7 C#),
consistency, and pre-handoff risk — per the review brief and `docs/v2-unity-port-spec.md`
Parts 2–3.
**Method:** Read-only static review of every `.asmdef` and a representative
sample of C# across all nine modules plus `Assets/Editor/`. No code was modified.

---

## Executive summary

**Overall verdict: SOLID — with concerns.**

The architecture is genuinely well-built and disciplined. The asmdef topology is
clean and acyclic, module isolation holds with no violations, the editor/runtime
split is correctly enforced, and the cross-module combat seam (`IDamageable` in
Core) is a textbook resolution of the Pets-vs-Village isolation problem. The
ScriptableObject + UnityEvent state pattern, the data-loader pattern, and the
async/UniTask discipline are applied consistently across modules. Error handling
in the save/load and wallet paths is careful and defensive. This is a codebase a
contractor can pick up at Week 9 without owner ramp-up — which is the Week-8
acceptance bar.

The "concerns" qualifier is about **integration state and a small set of
correctness/lifecycle issues**, not about the architecture itself. Weeks 5–7 code
is written but not scene-integrated (expected per the brief and the decisions
log); judged purely as code it is sound. The findings below are mostly hardening
items — event-subscription symmetry, swallowed exceptions in the migrator, a few
null-safety gaps, and a `WaveManager` resume-correctness bug — none of which
threaten the overall design.

### Findings by severity

| Severity | Architecture | Code hardening | Total |
|----------|-------------|----------------|-------|
| Critical | 0 | 0 | 0 |
| High     | 0 | 2 | 2 |
| Medium   | 3 | 5 | 8 |
| Low      | 3 | 4 | 7 |
| **Total**| **6** | **11** | **17** |

### Top 3 risks heading into contractor handoff

1. **Scene-integration debt is the real Week-8 risk, not the code.** Weeks 4–7
   gameplay systems compile cleanly but are not wired into scenes (NavMesh bake,
   `VillageController` hookup, prefabs, HUD `UIDocument`s, layer masks, the
   breach→ATB→breach round-trip). The acceptance gate is an *end-to-end
   playthrough*; that depends entirely on an integration pass that has not
   happened. A contractor inherits compiling-but-unproven systems. (ARC-001)
2. **The breach→ATB→breach loop is designed but never exercised, and has a known
   resume defect.** `WaveManager.BeginLoop()` always restarts at `_startWave`
   after an ATB return, so a breach on Wave 2 resumes at Wave 1; no system reads
   `ATBRuntimeState.Result` to apply Heart/building damage on return. The full
   round-trip is the headline acceptance scenario and is currently untestable.
   (CODE-001, ARC-002)
3. **The Solana SDK seam is unverified against a real SDK.** `SolanaWalletProvider`
   is well-isolated behind `#if SOLANA_SDK` and `IWalletProvider`, but every SDK
   call is a best-guess marked `// SDK-VERIFY:`. Week 7's acceptance ("a devnet
   transaction goes through") cannot be met until the SDK is installed and those
   calls are reconciled — a task with unknown surface-area drift. (CODE-002)

---

## Part A — Architecture findings

### ARC-001 — Weeks 4–7 systems are unintegrated; acceptance depends on a pass that has not run
**Severity:** Medium (process/state risk, not a design defect)
**Location:** `Assets/_Modules/Village/*`, `Assets/_Modules/Dungeons/*`,
`Assets/_Modules/Wallet/*`; `docs/unity-decisions.md` Week-4 flags.
**Description:** Per the brief and the decisions log, Weeks 5–7 code is written
but not scene-integrated, and even Week 4 landed as "compiling C# modules first;
scene wiring is a separate integration pass." This is a *deliberate, logged*
sequencing choice and not an architecture defect. But it means none of the
gameplay loops (wave loop, breach hand-off, dungeon run, pack purchase) have run
end-to-end. The code is reviewable and looks correct; it is not *proven*.
**Recommendation:** Treat the integration pass as the critical-path Week-8 work
item, not a polish task. Sequence it as: NavMesh bake → `VillageController`
wires WaveManager/HeroAbilities/PetDeployer/BuildMenu → enemy/building prefabs +
layer masks → run Wave 1 → wire the ATB return path → run a full breach
round-trip. Track it explicitly against the Part-9 acceptance gates.

### ARC-002 — No owner of the ATB-return contract; the battle result is produced but never consumed
**Severity:** Medium
**Location:** `BattleATB/BattleController.cs` (`ReturnAfterResult`),
`Village/Waves/WaveManager.cs` (`TriggerBreach`), `Dungeons/EncounterTrigger.cs`
(`ResumePendingEncounter`).
**Description:** The breach hand-off is one-directional and asymmetric.
`WaveManager.TriggerBreach()` hands off cleanly via `SceneRouter.GoBattle`, and
`BattleController` settles the outcome onto `ATBRuntimeState.Result` and fades
back to the Village. But nothing on the Village side reads `ATBRuntimeState.Result`
on return — Heart damage, building damage and wave progress from the battle are
never applied. `BattleController.ReturnAfterResult` is also hard-coded to return
to `SceneRouter.Village` and explicitly defers the dungeon return path, so a
dungeon ATB encounter will route the player to the *village*, not back to the
dungeon. `EncounterTrigger.ResumePendingEncounter` exists to consume that result
but has no caller. The return contract is half-specified.
**Recommendation:** Define the return contract explicitly: a Village-side and a
Dungeon-side resumer that read `ATBRuntimeState.Result` / `SceneRouter.PendingBattle`
on scene load and apply consequences. Make `BattleController` route back by
`BattleSource` (Village vs Dungeon) rather than a hard-coded scene name. This is
the same gap as CODE-001 viewed from the architecture side.

### ARC-003 — HUD module has an asmdef but no implementation; HUD is a stated Week 3–4 deliverable
**Severity:** Medium
**Location:** `Assets/_Modules/HUD/DeNelle.HUD.asmdef` (no `.cs` files in the module).
**Description:** Spec Part 3 maps `src/modules/village/hud/` to
`_Modules/HUD/HUDDocument.uxml` + `HUDController.cs`, and Weeks 3–4 list a HUD
shell (resource bar, hero portrait, ability hotbar, wave countdown, build menu)
as a deliverable. The module folder and asmdef exist but contain no code. The
gameplay systems already expose the right hooks for it — `WaveManager` raises
`OnCountdownTick`/`OnWaveStarted`, `HeroAbilities` exposes `CooldownFraction`,
`GameStateService` raises per-domain events — so the HUD is genuinely "wireable,"
but it is currently absent. The acceptance playthrough needs a wave countdown and
resource bar on screen.
**Recommendation:** Confirm with the owner whether the HUD slipped or lives
elsewhere; if slipped, schedule `HUDController.cs` + `HUDDocument.uxml` into the
Week-8 integration pass. It is low-risk work given the existing event surface.

### ARC-004 — Two parallel damage interfaces (`IDamageable` vs `IDamageableStructure`) with overlapping intent
**Severity:** Low
**Location:** `Core/Combat/IDamageable.cs`, `Village/Enemies/Enemy.cs`
(`IDamageableStructure`).
**Description:** Core defines `IDamageable` (faction, HP, `TakeDamage`,
`ApplyStatus`) as the cross-module combat-target seam. `Enemy.cs` separately
defines `IDamageableStructure` (in `DeNelle.Village`) for buildings/walls/gates.
Both model "a thing an attacker damages." The split is *defensible* — structures
have no faction/status and the enemy probe is village-internal — but it is two
contracts for one concept, and a future contractor will reasonably ask why a wall
is not just an `IDamageable` with `Friendly` faction. The `EnemyDamageable`
adapter (a separate component bolted onto the Enemy GameObject rather than
`Enemy : IDamageable` directly) adds a third moving part for the same idea.
**Recommendation:** Not urgent. Consider, at the next refactor, folding
`IDamageableStructure` into `IDamageable` (structures return `Friendly` and
no-op `ApplyStatus`) and folding `EnemyDamageable` into `Enemy` directly — the
adapter's own header comment already anticipates this. Document the rationale in
the decisions log if the split is kept.

### ARC-005 — `GameStateService` singleton is `DontDestroyOnLoad`-bootstrapped but has no defined bootstrap owner
**Severity:** Low
**Location:** `Core/State/GameStateService.cs` (`Awake`, `Instance`).
**Description:** `GameStateService` is a `MonoBehaviour` singleton that
`DontDestroyOnLoad`s itself and auto-loads on `Awake`. Multiple consumers
(`PackStore.ApplyPackContents`, `IsOwned`) call `GameStateService.Instance` and
correctly null-guard it — but nothing in the reviewed code defines *where* the
service is first instantiated (which scene, which bootstrap object). If it lives
only in the Village scene, then opening ATBBattle or a dungeon scene directly (a
common dev/test path, and exactly what `BattleController`'s dev-fallback supports)
runs with `Instance == null`. The save system silently no-ops in that case.
**Recommendation:** Establish a single Core bootstrap scene or a
`[RuntimeInitializeOnLoadMethod]` that guarantees `GameStateService` exists before
any consumer runs, and document it. This also matters for the save-persistence
acceptance gate.

### ARC-006 — `SceneRouter` static `PendingBattle` is the only scene-handoff channel; no lifetime/clearing discipline
**Severity:** Low
**Location:** `Core/SceneRouter.cs` (`PendingBattle`, `GoBattle`).
**Description:** Battle hand-off state is a static field on `SceneRouter` that is
set on `GoBattle` and never cleared. `BattleController.BuildSetup` reads it. If a
later battle is started without going through `GoBattle` (e.g. the dev fallback
opening ATBBattle directly), it reads a *stale* `PendingBattle` from a previous
breach. The same static-handoff pattern is reused by the dungeon encounter path,
so two subsystems share one un-scoped channel.
**Recommendation:** Clear `PendingBattle` after `BattleController` consumes it,
or pass a generation token. Low impact today (one battle scene), but a latent
bug once the dungeon and village both feed the ATB scene in one session.

---

## Part B — Code-hardening findings

### CODE-001 — `WaveManager` resume after ATB return restarts at the wrong wave; no result applied
**Severity:** High
**Location:** `Village/Waves/WaveManager.cs` — `BeginLoop()`, `TriggerBreach()`,
`Start()`.
**Description:** The file's own header says "when the ATB scene returns to the
Village a fresh `WaveManager.Start()` resumes the loop." But `Start()` →
`BeginLoop()` → `EnterCountdown(_startWave)` always re-enters at the serialized
`_startWave` (default 1). A breach on Wave 3 returns the player to Wave 1.
Worse, `TriggerBreach()` calls `e.Kill()` on the breaching roster and
`_liveEnemies.Clear()` — "the rest of the wave is abandoned" — so even the
correct wave restarts from a clean slate with no memory of progress. And nothing
reads the battle outcome: a *loss* in the ATB scene should damage the Heart or
end the run, but the WaveManager just re-runs the wave regardless of win/lose.
**Recommendation:** Persist the current wave id (and ideally a
within-wave checkpoint) before `GoBattle`, and on Village re-entry read
`ATBRuntimeState.Result` to (a) resume at the correct wave and (b) apply
victory/defeat consequences to the Heart. This is the concrete code-level half of
ARC-002 and is on the critical acceptance path.

### CODE-002 — `SolanaWalletProvider` SDK calls are unverified guesses; Week-7 acceptance blocked until reconciled
**Severity:** High
**Location:** `Wallet/SolanaWalletProvider.cs` (entire `#if SOLANA_SDK` block).
**Description:** The provider is *architecturally* excellent — every SDK
touch-point is behind `#if SOLANA_SDK`, `IsSdkAvailable` gates provider
selection, and `WalletService` falls back to the stub with no caller change. But
every SDK API call is the agent's best guess, self-marked `// SDK-VERIFY:`
(`Web3.Instance.LoginWalletAdapter()`, `TransactionBuilder`, `TokenProgram.Transfer`,
`GetSignatureStatusesAsync` result shape, `Transaction.Deserialize`, etc.). The
Solana Unity SDK's surface has drifted across versions, and the file is honest
about this. None of it has been compiled against a real SDK. Week-7 acceptance
("a devnet SKR transaction goes through, pack contents land in GameState") cannot
be met until the SDK is installed and these are reconciled.
**Recommendation:** Treat SDK installation + `// SDK-VERIFY:` reconciliation as a
discrete, scheduled task with its own buffer (the decisions log already defers
the install to Week 7). Keep the isolation exactly as-is — it is the right
design; the risk is purely the unknown reconciliation effort. Compile-test the
`#if SOLANA_SDK` block as soon as the package resolves.

### CODE-003 — Swallowed exceptions in `SaveMigrator` can mask save corruption silently
**Severity:** Medium
**Location:** `Core/State/SaveMigrator.cs` — `MigrateToV8` (`catch (Exception) { bd.Remove("gate-0"); }`),
`MigrateToV9` (two `catch (Exception) { ... }` blocks, one fully empty).
**Description:** The v8 step catches *all* exceptions and silently drops a save
key; the v9 step catches all exceptions twice, once setting `legacy = null` and
once with an empty body. The intent is graceful degradation (a best-effort
migration should not hard-fail the load), which is reasonable — but a bare
`catch (Exception)` with no log means genuine corruption or a logic bug in a
migration step is invisible. The save path elsewhere in `GameStateService` logs
every failure; the migrator is the one place that does not.
**Recommendation:** Keep the graceful-degradation behavior, but add a
`Debug.LogWarning` inside each catch naming the step and the exception. A
contractor debugging a "save came back wrong" report needs that breadcrumb.

### CODE-004 — `WaveManager.SpawnOne` subscribes enemy events but only partially unsubscribes; placeholder enemies leak
**Severity:** Medium
**Location:** `Village/Waves/WaveManager.cs` — `SpawnOne`, `HandleEnemyDied`,
`HandleEnemyReachedHeart`, `TriggerBreach`, `BuildPlaceholderEnemy`.
**Description:** Two issues. (1) `SpawnOne` subscribes `enemy.Died` and
`enemy.ReachedHeart`. `HandleEnemyDied` unsubscribes both — good. But the breach
path (`TriggerBreach`) calls `e.Kill()` on the roster, which raises `Died`, which
*does* unsubscribe — so that path is actually fine — yet `_liveEnemies.Clear()`
is also called, and if `Kill()`'s `Died` handler runs `_liveEnemies.Remove(enemy)`
mid-iteration over the same list there is an ordering dependency that works today
only because the loop captured the roster separately. It is fragile. (2)
`HandleEnemyReachedHeart` adds to `_breachRoster` and calls `TriggerBreach` while
`Update`→`TickActiveWave` may also be iterating; re-entrancy is not guarded.
**Recommendation:** Make breach consumption explicit and re-entrancy-safe: snapshot
the roster, set `_phase = WavePhase.Breached` *first* (it already does), then
clear and kill. Add a guard so `HandleEnemyReachedHeart` is a no-op once
`_phase != Active` (it checks this — good — but `TickActiveWave`'s own breach
detection does not re-check phase after `TriggerBreach` returns; it does `return`,
so this is OK, but worth a comment). Net: tighten and comment the ordering
contract.

### CODE-005 — Placeholder GameObjects created at runtime are never pooled or bounded; per-cast VFX allocation
**Severity:** Medium
**Location:** `Village/Waves/WaveManager.BuildPlaceholderEnemy`,
`Pets/PetDeployer.SpawnPet`, `Village/Hero/HeroAbilities.SpawnVfx` /
`BuildBuiltInBurst`.
**Description:** `HeroAbilities.SpawnVfx` creates a fresh `GameObject` +
`ParticleSystem` on every cast and `Destroy`s it after its lifetime — one alloc
per Q/W/E/R press. `WaveManager` and `PetDeployer` `CreatePrimitive` placeholders
per spawn. None is pooled. For the Week-8 60-FPS / ≤400 MB acceptance gate during
a village wave, per-cast `GameObject`/`ParticleSystem` instantiation plus
per-spawn primitive creation will produce GC spikes. The hot-path overlap buffers
(`HeroAbilities._overlap`, `Pet._overlap`) *are* correctly pre-allocated and use
`OverlapSphereNonAlloc` — good — so the team clearly knows the pattern; it just
is not applied to spawned objects.
**Recommendation:** Acceptable for a placeholder milestone, but flag for the
integration pass: pool the ability VFX (a small ring buffer of reusable
`ParticleSystem`s) and pool enemies once the KayKit prefab lands. Verify against
the Profiler during the acceptance run as the spec's Week-8 step requires.

### CODE-006 — `Lantern` and `Pet` cache no `Transform`/component refs defensively; null hero mid-run is unguarded in places
**Severity:** Medium
**Location:** `Dungeons/Lantern.cs` (`Update`→`FollowHero`/`CheckOilStones`),
`Pets/Pet.cs` (`Update`→`MoveToward`).
**Description:** `Lantern.Update` runs `FollowHero`, `DrainOil`, `CheckOilStones`,
`ApplyRange`, `ApplyIntensity` every frame. `FollowHero` and `CheckOilStones`
null-check `_hero` — good. But `ApplyRange`/`ApplyIntensity` dereference `_light`
unconditionally; `_light` is set in `Awake` via `GetComponent<Light>()` under
`[RequireComponent(typeof(Light))]`, so it is safe in practice, but if the
`Light` is destroyed at runtime (scene teardown order) the component reference
becomes a Unity "fake null" and the next `Update` throws. `Pet` has the same
shape — `Update` runs combat math but the GameObject could be mid-teardown.
**Recommendation:** Low-likelihood but cheap to harden: early-out of `Update`
when the owning subsystem is torn down (`Lantern` could check
`_controller == null` or run-active; `Pet` already checks `IsAlive`). Mostly a
defensive note — these are not active bugs today.

### CODE-007 — `EnemyDamageable.ApplyStatus` records status timers the consumer never reads; silent dead code path
**Severity:** Low
**Location:** `Village/Enemies/EnemyDamageable.cs` (`ApplyStatus`, `IsFrozen`/`IsSlowed`/`IsBurning`);
`Village/Enemies/Enemy.cs`.
**Description:** Hero abilities and pets call `ApplyStatus(Slow/Freeze/Burn, …)`.
`EnemyDamageable` records the expiry and exposes `IsFrozen`/`IsSlowed`/`IsBurning`,
but `Enemy.cs` has no status-timer fields and nothing reads those properties — so
`Mage` Frost Nova and `Ice Wolf` Frostbite *appear* to apply crowd control but
have zero gameplay effect. The header comments are honest that this is a
deferred hook, so it is intentional, not a hidden bug — but it is a behavior the
acceptance playthrough may visibly miss (a Frost Nova that does not slow).
**Recommendation:** Either wire `Enemy`'s `NavMeshAgent.speed` to read
`EnemyDamageable.IsSlowed`/`IsFrozen` during the integration pass, or explicitly
descope status effects for the v2 foundation and note it. Avoid shipping an
ability that looks like it does nothing.

### CODE-008 — `WaveManager._enemyMask`/`HeroAbilities._enemyMask`/`Pet._enemyMask` default to `~0` (everything)
**Severity:** Low
**Location:** `Village/Hero/HeroAbilities.cs` (`_enemyMask = ~0`),
`Pets/Pet.cs` (`_enemyMask = ~0`), `Pets/PetDeployer.cs` (`_enemyMask = ~0`).
**Description:** The ability and pet target sweeps default their `LayerMask` to
`~0` — every layer. The code does filter the results by
`IDamageable.Faction == Hostile`, so it is *correct*, but an everything-mask
`OverlapSphereNonAlloc` is wasteful (it returns ground, walls, props, the hero)
and the 48/64-element non-alloc buffers can fill with non-combatants and silently
truncate real targets in a busy scene. The decisions log already flags "enemy
layer mask must be added to `Village.unity`" as an integration item.
**Recommendation:** Make the integration pass set a real Enemy layer on the
enemy prefab and assign the mask on `HeroAbilities`/`Pet`/`PetDeployer`. Until
then the `~0` default is a known temporary; just confirm the non-alloc buffers
are large enough or the mask is set before the acceptance run.

### CODE-009 — `GameStateService.Snapshot`/`ApplyPersisted` is a 41-field hand-written mapping — high drift risk
**Severity:** Low
**Location:** `Core/State/GameStateService.cs` (`Snapshot`, `ApplyPersisted`).
**Description:** `Snapshot()` and `ApplyPersisted()` are two parallel
hand-written field-by-field copies of 41 persisted fields, plus `Reset()` is a
third hand-written enumeration of the same field set. Any new persisted field
must be added in four places (the SO, `Snapshot`, `ApplyPersisted`, `Reset`) plus
a migration step. The spec mandates a `SchemaTests.cs` per data file, and the
`SaveLoadRoundTripTest` exists — so drift would be *caught* — but the maintenance
surface is large and a missed field silently fails to persist.
**Recommendation:** Acceptable given the test coverage; note for the contractor
that adding a save field is a four-touch-point change and point them at the
round-trip test. A future refactor could drive `Snapshot`/`Apply` from a single
field registry, but that is not Week-8 work.

### CODE-010 — `DungeonController.MakeRunSeed` mixes `TickCount` with `realtimeSinceStartup` — weak, and non-reproducible by design
**Severity:** Low
**Location:** `Dungeons/DungeonController.cs` (`MakeRunSeed`).
**Description:** `MakeRunSeed` XORs `Environment.TickCount` with
`realtimeSinceStartup * 1000`. The header on `EncounterTrigger` and the spec's
RNG-determinism property (`docs/anti-cheat-spec.md`, seed=42 reproducibility)
imply encounter sequences should be reproducible. A wall-clock seed makes every
dungeon run non-reproducible, which conflicts with the deterministic-engine
posture the ATB port is careful about. The method even comments "v1.1 can swap
this for a save-derived value" — so it is a known stopgap.
**Recommendation:** Fine for v2 foundation since random encounters are disabled
(`disableRandomEncounters`), so the seed is currently unused for gameplay. Note
it for v1.1: derive the run seed from the save (`SaveSchema` + dungeon id + run
count) so a run is reproducible and anti-cheat-auditable.

### CODE-011 — No `CancellationToken` on long-lived UniTasks; fire-and-forget `.Forget()` tasks outlive their scene
**Severity:** Medium
**Location:** `Village/Waves/WaveManager.SpawnBatch` (`.Forget()` with
`UniTask.Delay`), `BattleATB/BattleController.ReturnAfterResult`,
`Dungeons/DungeonController.EnterDungeon`, `Wallet/SolanaWalletProvider.ConfirmTransaction`
(30 × 1 s poll loop).
**Description:** Several `async UniTask` flows are launched fire-and-forget with
`.Forget()` and contain `await UniTask.Delay(...)` loops, but none takes a
`CancellationToken`. If the scene unloads (a breach mid-spawn-batch, a dungeon
exit mid-load, a wallet confirm still polling when the player backs out), the
continuation resumes on a destroyed MonoBehaviour and either throws a
`MissingReferenceException` or mutates dead state. `WaveManager.SpawnBatch`
partly mitigates this by checking `_phase != WavePhase.Active` after each delay —
good defensive instinct — but `ConfirmTransaction`'s 30-second poll has no such
guard, and `EnterDungeon` does not re-check liveness after `await DungeonLayoutLoader.LoadAsync`.
**Recommendation:** Adopt `this.GetCancellationTokenOnDestroy()` (UniTask
provides it) and thread it through the delay/poll loops, or consistently
re-check a liveness flag after every `await` as `SpawnBatch` already does.
This matters most for the wallet confirm loop and any scene-transition path.

---

## Appendix — What was verified clean

These were checked and found correct; recorded so the contractor knows they were
audited, not skipped.

- **Module isolation (spec Part 2): no violations.** All nine module asmdefs
  (`Core`, `Data`, `BattleATB`, `Village`, `Dungeons`, `Onboarding`, `HUD`,
  `Pets`, `Wallet`) reference only `DeNelle.Core`, `DeNelle.Data`, `Unity.*`
  packages and `UniTask`. No gameplay module references another gameplay
  module's asmdef. Cross-module combat correctly goes through
  `DeNelle.Core.Combat.IDamageable`.
- **Dependency direction: acyclic and sane.** `Core` depends on nothing but
  `UniTask`; `Data` depends only on `Core`; every gameplay module depends on
  `Core`/`Data` + Unity packages. No cycles.
- **Editor/runtime separation: correctly enforced.** `DeNelle.Editor` is
  `includePlatforms: ["Editor"]`, references only `Core`/`Data` + render-pipeline
  runtime assemblies, and is never referenced by a runtime module. The scene
  builders add gameplay components to scenes by full-name reflection
  (`assembly.GetType(fullName)` → `AddComponent(type)`), so the one-way
  Editor→runtime relationship is real — runtime code has no compile dependency on
  the editor.
- **`async void`: none in the codebase.** Every async flow returns `UniTask`;
  fire-and-forget call sites use `.Forget()`. Spec Part 3 mandate met.
- **Deprecation warnings — not actually deprecated.** `FindObjectsByType` /
  `FindObjectsSortMode.None` is the *current* Unity 6 API (the modern replacement
  for the deprecated `FindObjectsOfType`); its use in `WaveManager.ResolveSceneRefs`
  and the editor builders is correct. `StaticEditorFlags.NavigationStatic` appears
  only in `Assets/Editor/VillageSceneBuilder.cs` (editor-only NavMesh-static
  marking) — not a runtime concern. No genuine deprecation debt was found in the
  reviewed runtime C#.
- **Data-loader pattern: consistent.** `WaveDataLoader`, `DungeonLayoutLoader`
  and `PackCatalog` all follow the same shape — async `UniTask` read from
  `Application.streamingAssetsPath`, `UnityWebRequest` fallback for the Android
  in-APK case, `Newtonsoft.Json` deserialization, null+empty guards, and
  `JsonException` caught and logged. Strongly-typed `[JsonProperty]` records with
  sane defaults. Matches spec Part 4.
- **Save/load hardening: careful.** `GameStateService.Load` is defensively layered
  — missing key, empty string, parse exception, null envelope, migration
  rejection and schema validation each fail closed to "keep fresh state" with a
  distinct logged reason. `Save` wraps serialization in try/catch.
- **Wallet service hardening: solid.** `WalletService` wraps every provider call
  in try/catch with logged failures and typed `PaymentResult.Failure`; the
  `Mainnet` guardrail is defended in two places (`WalletService.SetNetwork`
  warning + `SolanaWalletProvider.SendPayment` hard block). No private keys
  anywhere; the game builds only unsigned transactions.
- **ScriptableObject + UnityEvent state pattern: applied consistently.**
  `GameStateService`'s per-domain `UnityEvent`s (an explicit, logged improvement
  over one fat event), `ATBRuntimeState`'s `OnBattleChanged`/`OnActionSubmitted`/
  `OnOutcome`, and `WaveManager`'s `OnCountdownTick`/`OnWaveStarted` all follow
  the spec Part 3 pattern; `BattleController` correctly subscribes in `OnEnable`
  and unsubscribes in `OnDisable`.
