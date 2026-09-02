# WORK ORDER 1303 — `EnemyAnimatorLateBinder.Arm` prewarms by CONTROLLER name, so `enemyfam-skeletonhumanoid` never resolves

**Status:** READY TO IMPLEMENT
**Source:** F8 captures seq **4359, 4369, 4377** and — critically — seq **4639**, which fired *after* the
PROD-021 R2 push landed. Ledger: `docs/qa/F8_TRIAGE_2026-09-02.md` §7.
**Silo:** Enemy content warming (`DeNelle.Core` Addressables + `DeNelle.Village` enemies)
**Severity:** P2 — the family pre-fetch is skipped for every skeleton-humanoid enemy, so their bodies
trickle in per-address instead of arriving as one fetch. This is the "enemy spawns and slides for now"
case the code itself describes. Plus one logged error per occurrence, polluting the F8 queue.

## Owner-facing symptom

Skeleton-family enemies pop in late: they spawn on a placeholder and their real model/controller binds
afterwards, rather than the whole family arriving in one bundle fetch before the wave. The 2026-08-20
per-family warming ruling is silently not applied to this family.

## Captured proving line (§12 evidence — quoted verbatim)

`logs/f8-inbox/capture-20260902-015015-seq4639.md`, `scene=Main_Castle_Overworld`, `t=17.124544143676759`,
**wall clock 2026-09-02 01:50:15** — i.e. **two minutes after `R2_PUSH_OK` / `R2_PARITY_OK`, on a fresh
session**. This is the proof that it is a missing LABEL, not the PROD-021 404:

```
UnityEngine.AddressableAssets.InvalidKeyException: Exception of type
  'UnityEngine.AddressableAssets.InvalidKeyException' was thrown. No Location found for Key=enemyfam-skeletonhumanoid
UnityEngine.AddressableAssets.Addressables:DownloadDependenciesAsync (object,bool)
DeNelle.Core.EnemyContentWarmer/<>c__DisplayClass43_0:<WarmFamily>b__0 ()
  (at D:/EoA/Assets/_Modules/Core/Addressables/EnemyContentWarmer.cs:386)
DeNelle.Core.Diagnostics.Guard:Try (string,string,System.Action) (at D:/EoA/Assets/_Modules/Core/Diagnostics/Guard.cs:34)
DeNelle.Core.EnemyContentWarmer:WarmFamily (string) (at D:/EoA/Assets/_Modules/Core/Addressables/EnemyContentWarmer.cs:384)
DeNelle.Core.EnemyAssetLoader:PrewarmFamily (string) (at D:/EoA/Assets/_Modules/Core/Addressables/EnemyAssetLoader.cs:125)
DeNelle.Village.EnemyAnimatorLateBinder:Arm (UnityEngine.Animator,string,string)
  (at D:/EoA/Assets/_Modules/Village/Enemies/EnemyAnimatorLateBinder.cs:...)
```

Seq 4359 (`t=445.9`), 4369 (`t=212.2`) and 4377 (`t=252.6`) are the same exception in the three
pre-fix sessions, with byte-identical stacks and the same key.

## Root — proven from source and from the Addressables settings

**The declared label set has five members, and `enemyfam-skeletonhumanoid` is not one of them.**
`Assets/AddressableAssetsData/AddressableAssetSettings.asset:116-120`:
```
    - enemyfam-orc
    - enemyfam-hollow
    - enemyfam-shared
    - enemyfam-troll
    - enemyfam-bosses
```
A sweep of the settings asset for `enemyfam-*` returns exactly those five — **no other family label is
missing; this is the only bad key.**

**Where the bad key is manufactured.** `Assets/_Modules/Village/Enemies/EnemyAnimatorLateBinder.cs`:
```
49:   internal static void Arm(Animator animator, string modelName, string ctrlName)
…
60:       binder._model    = modelName;
61:       binder._ctrlName = ctrlName;
…
65:       EnemyAssetLoader.PrewarmFamily(ctrlName);     // <-- the CONTROLLER name
```
`PrewarmFamily` (`Assets/_Modules/Core/Addressables/EnemyAssetLoader.cs:124-125`) passes straight into
`EnemyContentWarmer.FamilyOf`, which (`EnemyContentWarmer.cs:215-225`) strips the address prefix and any
path, then takes the text before the first `_`. Given the controller `SkeletonHumanoid`, that yields the
family `"skeletonhumanoid"` and the label `enemyfam-skeletonhumanoid`, which has no location, so
`Addressables.DownloadDependenciesAsync` throws.

**Every other call site passes the MODEL name**, which is what `FamilyOf` is built for:
- `Assets/_Modules/Village/Enemies/EnemyFactory.cs:1004` → `PrewarmFamily(m)`
- `Assets/_Modules/Village/Enemies/EnemyLateSkinner.cs:92` → `PrewarmFamily(model)`

and `modelName` is already in scope in `Arm`, bound to `binder._model` one line earlier at line 60.

The throw is contained by `Guard.Try` (`Guard.cs:34`) so nothing crashes — but it logs an error, which
is why it reaches the F8 inbox.

## Acceptance criteria

1. `EnemyAnimatorLateBinder.Arm` prewarms by the value `FamilyOf` is designed to consume — the **model**
   — so the family resolves to a declared label. Do not special-case the string `SkeletonHumanoid`.
2. **Prove the positive path, not just the absence of the error** (memory
   `prove-the-success-path-not-just-the-refusal`). A captured run must show the family actually
   downloading:
   `[Flow:...] family '<family>' bundles Succeeded via 'enemyfam-<family>'. Only this family was fetched…`
   (`EnemyContentWarmer.cs:391-393`). An absence of `InvalidKeyException` alone does not close this — a
   prewarm that is silently skipped also produces no exception.
3. Zero `No Location found for Key=enemyfam-*` lines in a full town→wave→town headless run.
4. **Guard the general case.** `WarmFamily` should refuse a family whose label is not in the declared
   set with a `FlowTrace.Warn` naming the bad key and its caller, instead of letting
   `DownloadDependenciesAsync` throw into `Guard.Try` — so the next bad key reads as a named defect, not
   as an engine exception. Keep the existing `fam-empty-<family>` throttle at
   `EnemyContentWarmer.cs:381-388`; add to it, do not replace it.
5. A regression under `Assets/Editor/Regression/` asserts that every family name reachable from a live
   `PrewarmFamily` call site maps to a label declared in `AddressableAssetSettings.asset`.
6. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs; brace-balance on every `.cs`
   touched (CLAUDE.md §1).

## What NOT to touch

- ⛔ **Do not add an `enemyfam-skeletonhumanoid` label to `AddressableAssetSettings.asset`, and do not
  re-label, re-group, or re-pack any enemy asset.** That is the wrong fix — the family is
  `hollow`/`orc`/`troll`, keyed by model; `SkeletonHumanoid` is a shared *controller*, not a family.
  Worse, **any change under `Assets/AddressableAssetsData/` re-hashes every bundle and mandates a fresh
  `tools\r2-ship.ps1` push (CLAUDE.md §16)** — that is exactly the trap that produced PROD-021 and three
  prior capsule-enemy incidents. This ticket must be content-free.
- ⛔ Do not make `Arm` **wait** on the prewarm. `EnemyAnimatorLateBinder.cs:67-70` records that waiting on
  this seam is what deadlocked the game on 2026-08-20. It stays fire-and-forget.
- ⛔ Do not change `EnemyContentWarmer.FamilyOf`'s parsing rule (`EnemyContentWarmer.cs:215-225`) — three
  correct call sites depend on it. Fix the one caller that feeds it the wrong string.
- ⛔ Do not remove the `Guard.Try` wrapper or any `FlowTrace` in this path (CLAUDE.md §12).
- ⛔ Do not touch `EnemyFactory.cs` or `EnemyLateSkinner.cs` — both already pass the right value.
