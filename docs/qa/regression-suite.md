# Regression Test Suite — Defenders of the Realm (v2 Unity Port)

**Project:** Defenders of the Realm — Unity 6 LTS / URP port (Unity 6000.4.7f1)
**Owner:** Samantha Denelle / DeNelle Studios
**Scope:** automated **EditMode** regression coverage over the **stable** modules.
**Maintained by:** QA / test engineering. Last updated 2026-05-19.

This is the automated companion to `docs/qa/qa-test-plan.md`. The test plan tracks
the full 90-case functional matrix (much of it `Build`-gated). This suite covers
the **deterministic logic of the stable areas** — the parts that can be verified
today, headless, on every integration commit, with no playable build. It satisfies
`qa-test-plan.md` test case **TC-XC-14** ("EditMode suite green") and feeds the
`Editor`-checkable rows of sections 1, 2, 5 and 7.

Where a stable module depends on something not yet finished, the suite **backtests
with stubs / test doubles** — each stable area is exercised in isolation.

---

## 1. What the suite covers

The suite is split across **four EditMode test assemblies**, one per stable area.

| Test assembly | Location | Covers | Approx. tests |
|---------------|----------|--------|--------------|
| `DeNelle.Core.Tests` | `Assets/_Modules/Core/Tests/` | Save/load round-trip (all 41 persisted fields), `SaveMigrator` v1→v10 chain, `SaveSchema.Validate` clamps + NaN/Infinity rejection, `Reset()` carve-out | 59 |
| `DeNelle.BattleATB.Tests` | `Assets/_Modules/BattleATB/Tests/` | mulberry32 RNG golden vector + determinism, combat math (damage/defense/element/crit/status ticks/heal clamp), targeting, actions, AI choice, turn/ATB order, battle-state lifecycle, wave/boss scaling, `ENEMY_DEFS` | 64 |
| `DeNelle.Data.Tests` | `Assets/Data/Tests/` | Every canonical-JSON loader: `BuildingCatalog`, `WaveDataLoader`/`EnemyCatalog`, `AbilityCatalog`, `PetCatalog`, `PackCatalog`; canonical-JSON well-formedness + stray-markup scan (BUG-013) | ~50 |
| `DeNelle.Wallet.Tests` | `Assets/_Modules/Wallet/Tests/` | `StubWalletProvider` connect/balance/pay flow, `WalletService` app surface (devnet default, mainnet guard, pay guards), `WalletRegistry`/`wallets.json` (public-addresses-only) | ~35 |

**Total: ~208 EditMode tests across 4 assemblies.**

### 1.1 Coverage by stable area

- **Core — save/load + migration + schema + RNG.**
  - `SaveLoadRoundTripTest` — author a fully-populated state, `Save()`, simulate a
    quit+relaunch, `Load()`, assert all 41 persisted fields round-trip byte-for-byte
    (the spec Week-1 acceptance path). Includes the string-enum / `tutorialStep`
    wire-format edge cases and the empty-envelope guard.
  - `SaveMigratorTest` — one test per `v1→v2 … v9→v10` step + the cumulative v1→v10
    chain + the `MigrateForImport` version gate (rejects newer/NaN versions).
  - `SaveSchemaValidateTest` — `NonNegInt` / `FiniteInt` / `RequireFinite` clamps,
    NaN/Infinity rejection with the offending field path.
  - `ResetCarveOutTest` — `Reset()` wipes progression but preserves `boundWallet`,
    `breachStyle` and every social field; verified through a relaunch.
  - **RNG:** the ATB engine owns the project's deterministic RNG (`RngOps`
    mulberry32). `RngGoldenVectorTest` pins it bit-for-bit against an independent
    reference + a hand-traced integer anchor.

- **BattleATB — the ATB combat engine.** Combat math, turn/ATB order, targeting,
  AI, actions, battle-state lifecycle, the wave/boss difficulty curve and the
  `ENEMY_DEFS` enemy table (`goblin … hollow-king`). Pure C# — runs as plain
  `[Test]` fixtures with no scene.

- **Village/Pets/Wallet data loaders.** Every canonical-JSON loader parses its file
  and exposes the expected entries: 5 buildings, the wave schedule + village-enemy
  catalog (with a cross-reference check that every wave batch keys into
  `enemies.json`), the Mage Q/W/E/R ability loadout, 3 starter pets with 5 bond
  ranks each + the deploy-ring geometry, and the 5-tier pack ladder. Plus a
  standing canonical-JSON integrity scan (well-formed JSON, no stray agent markup).

- **Wallet stub.** `StubWalletProvider` is itself the test double for the
  not-yet-resolved Solana SDK (BUG-010). The suite backtests its full
  connect → balance → pay → disconnect flow, the insufficient-funds path, and the
  per-rail debit. `WalletServiceTest` adds a synchronous `FakeWalletProvider` so the
  service's own branch logic (no-wallet guard, null/zero-price pack, status events)
  is tested deterministically.

### 1.2 "Backtest with stubs" — the test doubles used

| Stable area | Incomplete dependency | Test double / isolation strategy |
|-------------|----------------------|----------------------------------|
| Core service tests | A real scene-serialized `GameStateService` | `TestSupport.SpawnService` builds the service in code and injects `_state`/`_loadOnAwake` by reflection (see BUG-003 below) |
| Wallet | Solana Unity SDK not resolved (`SOLANA_SDK` undefined) | `StubWalletProvider` (the shipped devnet mock) IS the stand-in; `WalletService.Create(useStub:true)` forces it |
| WalletService branch logic | Real network timing | `FakeWalletProvider` — a synchronous `IWalletProvider` double, no `UniTask.Delay` |
| ATB engine | Live scene / `BattleController` wiring | `BattleATB/Tests/TestSupport` builds plain `BattleSetup` / `BattleUnit` fixtures — engine is pure C# |
| Data loaders | Async Android `UnityWebRequest` path | In the Editor `StreamingAssets` is a real directory, so the `UniTask` loaders complete synchronously and are drained with `GetAwaiter().GetResult()` |

---

## 2. Running the suite headless (Unity Test Runner CLI)

The whole suite runs in batchmode with no GUI. From the project root:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" ^
  -runTests ^
  -batchmode ^
  -projectPath "C:\Users\Kayden-Laptop\Documents\defenders-unity" ^
  -testPlatform EditMode ^
  -testResults "C:\Users\Kayden-Laptop\Documents\defenders-unity\Artifacts\editmode-results.xml" ^
  -logFile "C:\Users\Kayden-Laptop\Documents\defenders-unity\Artifacts\editmode-run.log"
```

Notes:
- `-runTests` implies `-batchmode`; Unity exits non-zero if any test fails — wire
  that exit code into CI as the pass/fail gate.
- `-testResults` writes an NUnit3 XML report; `-testPlatform EditMode` selects all
  four assemblies above (they are all `"includePlatforms": ["Editor"]`).
- Do **not** add `-quit` — `-runTests` manages its own shutdown.
- To run **one assembly** add `-assemblyNames "DeNelle.Data.Tests"` (comma-separate
  for several). To run one area by name use
  `-testFilter "DeNelle.Wallet.Tests.*"`.
- macOS/Linux: swap the executable path; everything else is identical.

PowerShell equivalent (Windows):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -runTests -batchmode `
  -projectPath "C:\Users\Kayden-Laptop\Documents\defenders-unity" `
  -testPlatform EditMode `
  -testResults "$PWD\Artifacts\editmode-results.xml" `
  -logFile "$PWD\Artifacts\editmode-run.log"
```

---

## 3. Current pass/fail expectation

| Assembly | Expected | Notes |
|----------|----------|-------|
| `DeNelle.Core.Tests` | **59 / 59 pass** | Was 43/59 — see BUG-003 below. |
| `DeNelle.BattleATB.Tests` | **64 / 64 pass** | Pure-C# engine; no environmental dependency. |
| `DeNelle.Data.Tests` | **all pass** | Depends on the canonical JSON under `StreamingAssets/Data/Canonical/` being present and intact. |
| `DeNelle.Wallet.Tests` | **all pass** | Runs over the devnet stub; no SDK required. |

**Whole-suite expectation: green (~208 / ~208).** A red result is a regression —
mark the corresponding `qa-test-plan.md` case `FAIL` and open a `bug-log.md` row.

### 3.1 BUG-003 — Core save/load EditMode tests (RESOLVED)

`bug-log.md` BUG-003: 16 Core EditMode tests (`SaveLoadRoundTripTest` +
`ResetCarveOutTest`) failed with a `NullReferenceException` — `GameStateService._state`
was null inside `Reset()`/`Load()`.

**Investigation outcome:** the fix described in `docs/port-notes/core-test-fix.md`
**is already applied** in `Assets/_Modules/Core/Tests/TestSupport.cs`. Root cause was
the test harness, not production code: `SpawnService` originally injected the
private `[SerializeField]` fields *while the GameObject was inactive*, and the
inactive→active serialization sync then clobbered those managed-only writes back to
their native defaults (`_state` → null, `_loadOnAwake` → true). The current
`SpawnService` builds the GameObject **active** and injects `_state`/`_loadOnAwake`
by reflection **after** `AddComponent` — i.e. after any serialization sync — so the
managed writes survive. EditMode never calls `Awake`, so injecting last is safe.

**Status:** no further action needed. Production `GameStateService`/`GameState` are
unchanged. The Core run is **59/59** once the integrator executes the suite. QA
should move BUG-003 from `Fixed` to `Verified` after the first green headless run
(re-test cases TC-CORE-01 / TC-CORE-02).

No remaining blocker. The only standing prerequisite for `DeNelle.Data.Tests` is
that the canonical JSON files are present in `StreamingAssets/Data/Canonical/` — the
suite's `CanonicalJsonIntegrityTest` itself asserts this and will fail loudly if a
file is missing or carries stray markup (the BUG-013 standing scan).

---

## 4. Convention — adding tests as new areas stabilise

The next modules to stabilise are **Dungeons** and **HUD**. When an area's logic is
stable, add an EditMode regression assembly following the pattern below.

### 4.1 Create the test assembly

1. Make a `Tests/` folder inside (or alongside) the module —
   e.g. `Assets/_Modules/Dungeons/Tests/`.
2. Author a `DeNelle.<Area>.Tests.asmdef` **JSON file** (do **not** hand-create
   `.meta` files — Unity generates those). Copy `DeNelle.Core.Tests.asmdef` exactly
   and change only the `name`, `rootNamespace` and the module `references`:

   ```json
   {
       "name": "DeNelle.Dungeons.Tests",
       "rootNamespace": "DeNelle.Dungeons.Tests",
       "references": [
           "DeNelle.Dungeons",
           "DeNelle.Core",
           "DeNelle.Data",
           "UnityEngine.TestRunner",
           "UnityEditor.TestRunner"
       ],
       "includePlatforms": ["Editor"],
       "excludePlatforms": [],
       "allowUnsafeCode": false,
       "overrideReferences": true,
       "precompiledReferences": ["nunit.framework.dll"],
       "autoReferenced": false,
       "defineConstraints": ["UNITY_INCLUDE_TESTS"],
       "versionDefines": [],
       "noEngineReferences": false
   }
   ```

   - Keep `"includePlatforms": ["Editor"]`, `overrideReferences: true`, the
     `nunit.framework.dll` precompiled reference and the `UNITY_INCLUDE_TESTS`
     define constraint — these are what make it an EditMode test assembly.
   - Add `"UniTask"` to `references` if the module's logic is `async UniTask`.
   - Add any extra module references the tests need (HUD tests will reference
     `DeNelle.HUD`; cross-module data tests can reference several, as
     `DeNelle.Data.Tests` does).

### 4.2 Write the tests

- One `[TestFixture]` class per production type / concern; descriptive
  `snake_case` test method names that read as a sentence (match the existing files).
- **Backtest with stubs:** if the area depends on something unfinished, write a
  minimal test double implementing the production interface (see
  `FakeWalletProvider`) rather than waiting on the dependency.
- Deterministic logic only — automate the parts that need no playable build.
  Frame-dependent / scene-dependent behaviour stays in the `Build`-gated
  `qa-test-plan.md` rows.
- For `async UniTask` code that uses real delays, use `[UnityTest]` returning
  `IEnumerator` via `UniTask.ToCoroutine(...)` (see `StubWalletProviderTest`); for
  synchronous test doubles a plain `[Test]` with `.GetAwaiter().GetResult()` is fine.
- Re-use a module `TestSupport` static helper for shared fixtures (the Core and
  BattleATB assemblies each have one).

### 4.3 Wire it in

- The headless command in §2 picks up any new `["Editor"]` test assembly
  automatically — no edit to the run command is needed.
- Add a row to the table in §1, update the total, and add the new area to the
  pass/fail table in §3.
- Trace new tests to `qa-test-plan.md` case ids in a comment header so coverage
  stays auditable (e.g. dungeon `RandomEncounterTable` determinism → TC-DUN-16).

_Tend the Heart. Hold the dark._
