# DevTools — the DEV-ONLY in-game QA / debug console

A new module, `DeNelle.DevTools`, providing an in-game console for QA. Its
buttons load resources and jump game state so the `docs/qa/qa-test-plan.md`
test cases and the `docs/qa/uat-script.md` UAT steps can be set up without a
full playthrough (e.g. jump straight to a wave, top up crystals, grant a pack,
spawn Syndrath the dragon boss).

**The whole module is compiled OUT of release builds.** See the release-gating
section below — this is the single most important property of the deliverable.

## Files (all under `Assets/_Modules/DevTools/`)

- `DeNelle.DevTools.asmdef` — the assembly definition (authored JSON).
- `DevPanel.uxml` — the UI Toolkit overlay layout.
- `DevPanel.uss` — styling (matches `VillageHud.uss` / `BuildMenu.uss` palette:
  dark Heart-Forest, violet accent, amber CTA; a red "DEV BUILD" tag so a
  tester can never mistake a dev build for a release build).
- `DevPanelController.cs` — the `MonoBehaviour` driver, namespace
  `DeNelle.DevTools`. Builds the action groups, handles the hotkey + corner
  tap, runs every action.
- `DevWalletProbe.cs` — a DEV-only `IWalletProvider` with QA-settable mock
  balances (the shipped `StubWalletProvider` hard-codes fixed balances).
- `DevBootstrap.cs` — a `[RuntimeInitializeOnLoadMethod]` auto-spawner so the
  console appears in every scene with nothing to wire per scene.

No `.meta` files were hand-authored (per the task constraint); Unity generates
them on import.

## Release-gating — how the panel is kept out of release builds

Belt-and-braces, on two independent layers:

1. **asmdef define constraint.** `DeNelle.DevTools.asmdef` carries
   `"defineConstraints": ["UNITY_EDITOR || DEVELOPMENT_BUILD"]`. Unity skips
   compiling the *entire assembly* in a non-development player build — no
   `DeNelle.DevTools.dll` is produced or shipped.

2. **`#if` around every file body.** Every `.cs` file in the module wraps its
   whole body in `#if DEVELOPMENT_BUILD || UNITY_EDITOR ... #endif`. Even if
   the assembly were force-included, each file compiles to nothing in a
   release build.

`UNITY_EDITOR` and `DEVELOPMENT_BUILD` are Unity built-in scripting symbols:
`UNITY_EDITOR` is set in the Editor; `DEVELOPMENT_BUILD` is set only when
"Development Build" is ticked in Build Settings. A normal (release) player
build has neither, so the whole module is absent — its assembly, its types,
its UXML loading code. The "MODULE ISOLATION EXCEPTION" below is therefore
safe: DevTools may reference gameplay modules because none of it ships.

**Call-site gating.** `DevBootstrap`'s `[RuntimeInitializeOnLoadMethod]` is the
only thing that spawns the panel, and it too is inside the `#if`. There is no
release call site to gate — the hook simply does not exist in a release build.

## Module isolation exception (note for `unity-decisions.md` / port spec Part 2)

The project's gameplay modules are normally isolated — e.g. `DeNelle.HUD`
references only `DeNelle.Core`. **DevTools is the deliberate exception.** Its
asmdef references `DeNelle.Core`, `DeNelle.Village`, `DeNelle.Wallet`,
`DeNelle.HUD` and `UniTask`. This is allowed because DevTools is *tooling, not
gameplay*, and is compiled out of release builds — so it cannot create a
shipping coupling between gameplay modules. It reaches systems through their
existing public APIs / Core seams: `GameStateService`, `SceneRouter`,
`HeartController`, `WaveManager`, `DragonBoss`, `WalletService` /
`StubWalletProvider`. Everything is null-guarded — a scene that lacks a given
system reports it in the status line rather than throwing.

## The panel — hotkey, toggle, layout

- **Hotkey: `F1`** toggles the console open/closed (configurable on the
  `DevPanelController` component via the `_toggleKey` field).
- **On-screen corner tap:** a small amber "DEV" chip in the top-left corner
  toggles the console too — the touch-friendly twin of the hotkey (Seeker is a
  touch device).
- A close `✕` button and a red `DEV BUILD` tag sit in the title bar.
- Actions are grouped, each group a captioned card; buttons wrap to multiple
  rows. Built once at runtime into the `dev-group-list` container (the
  `VillageHudController` ability-bar build pattern).

## Action list (grouped, as built by `BuildActionGroups`)

- **Resources** — `+100 Crystals`, `+1000 Crystals`, `+500 Stone/Iron/Wood`.
- **Grant pack / entitlement** — a text field (default `hearth-spark`) +
  `Grant by id` (records the SKU in `OwnedItemIds`; if it is a known pack,
  also applies its economy + cosmetic SKUs — mirrors
  `PackStore.ApplyPackContents`), and `Grant ALL packs`.
- **Heart** — `HP 100% / 50% / 10%`; `State: Serene / Warning / Critical /
  Boss` (drives `HeartController.SetHp` / `SetState`).
- **Waves & enemies** — `Spawn enemy`, `Spawn Syndrath (dragon boss)`, a wave
  number field + `Jump to wave N`, and an `Instant-win wave` toggle.
- **Scene jump** — `Title`, `Village`, `Dungeon`, `ATBBattle` (via
  `SceneRouter.LoadScene`).
- **Mock wallet balance** — a balance field + `Mock SOL / USDC / SKR` and
  `Mock ALL rails` (sets `DevWalletProbe`'s static mock balances).
- **Cheats** — `God-mode` toggle.

Each action echoes its result into the panel's status line.

## What the integrator must wire

DevTools writes source only; the integrator compiles + verifies in Unity.

1. **Spawning the panel.** Two options:
   - *Auto-spawn (recommended, zero per-scene work):* create
     `Assets/_Modules/DevTools/Resources/`, place `DevPanel.uxml` +
     `DevPanel.uss` in it, and create a `PanelSettings` asset there named
     `DevPanelSettings`. `DevBootstrap` then spawns the console in every scene
     in the Editor and in any Development build.
   - *Manual:* drop a `GameObject` with a `UIDocument` (Source Asset =
     `DevPanel.uxml`) + a `DevPanelController` component into the scenes QA
     needs it in.

2. **`Spawn Syndrath`** needs the `Boss_Dragon` prefab assigned to
   `DevPanelController._dragonBossPrefab` in the inspector. Without it the
   action reports cleanly in the status line.

3. **Cheat flags are read by gameplay.** DevTools must not reach into a
   gameplay damage path, so it exposes two static flags —
   `DevPanelController.GodMode` and `DevPanelController.InstantWinWave` — plus
   `GodModeChanged` / `InstantWinWaveChanged` events. In a DEV build the
   integrator gates the relevant gameplay code, e.g. in `HeartController` /
   `Enemy` damage paths:
   ```csharp
   #if DEVELOPMENT_BUILD || UNITY_EDITOR
   if (DeNelle.DevTools.DevPanelController.GodMode) return; // skip the damage
   #endif
   ```
   and clears the active wave when `InstantWinWave` flips.

4. **`Jump to wave N` / `Spawn enemy` — optional `WaveManager` dev seams.**
   `WaveManager` keeps enemy spawning (`SpawnOne` / `SpawnBatch`) and the
   start-wave field private. The panel's safe public fallback is
   `WaveManager.BeginLoop()` (it says so in its status line). For an
   *arbitrary* wave jump and a *single-enemy* spawn, the integrator adds two
   small `#if`-gated public seams to `WaveManager`:
   ```csharp
   #if DEVELOPMENT_BUILD || UNITY_EDITOR
   /// <summary>DEV: jump the loop straight to a wave.</summary>
   public void DevJumpToWave(int wave) => EnterCountdown(Mathf.Max(1, wave));
   /// <summary>DEV: spawn one enemy of the given enemies.json id now.</summary>
   public void DevSpawnOne(string enemyType) { /* SpawnBatch a count-1 batch */ }
   #endif
   ```
   then updates `DevPanelController.JumpToWave` / `SpawnEnemy` to call them.
   This is the only change that touches an existing gameplay file; until it is
   done the panel still works via `BeginLoop()`.

5. **`Mock wallet balance`.** For the mocked numbers to be visible, the
   wallet-using screen (`PackStore` / `WalletConnectDialog`) must build its
   `WalletService` over a `DevWalletProbe` in a DEV build, e.g.:
   ```csharp
   #if DEVELOPMENT_BUILD || UNITY_EDITOR
   _wallet = new WalletService(new DeNelle.DevTools.DevWalletProbe());
   #else
   _wallet = new WalletService();
   #endif
   ```
   Without this the panel still records the mock balances on `DevWalletProbe`
   (and logs them), but a `StubWalletProvider`-backed store will not read them.

## QA coverage rationale

The panel's actions map directly onto setup steps the QA plan / UAT need:

- `Jump to wave N` / `Spawn enemy` / `Spawn Syndrath` — set up TC-VIL-09..17
  and UAT Part A (A9–A13) without surviving earlier waves.
- `Scene jump` — TC-CORE-07 scene transitions; reach the Dungeon / ATBBattle
  scenes directly (UAT Part B).
- `+Crystals` / `Grant pack` — TC-VIL-07 (afford a building), TC-WAL-08..10
  (pack ownership / store state), UAT A8 / E4.
- `Heart HP / state` — TC-VIL-04 Heart threat states without playing a wave.
- `Mock wallet balance` — TC-WAL-08..10, including the store's
  insufficient-funds path (set a balance below a pack price).
- `God-mode` / `Instant-win wave` — let a tester walk a long scenario
  (UAT Part A/B) without dying or grinding every wave.

_Tend the Heart. Hold the dark._
