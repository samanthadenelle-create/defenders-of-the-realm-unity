# UniTask (Cysharp) — Async Notes

Zero-allocation `async/await` for Unity. The project's standard async primitive —
used pervasively for delays, frame yields, scene flow, and fire-and-forget jobs.
UPM package `com.cysharp.unitask` **v2.5.10**
(`Library/PackageCache/com.cysharp.unitask@15a4a7657f99`).

## Namespace
`using Cysharp.Threading.Tasks;`

## What this codebase actually uses (by frequency)
- **`async UniTask` methods** (~119 sites) — the standard return type for awaitable
  methods (use `UniTask` for void-returning, `UniTask<T>` for results).
- **`.Forget()`** (~61 sites) — fire-and-forget: call an async method without awaiting,
  WITHOUT an unobserved-exception warning. Use this instead of `async void`.
- **`await UniTask.Delay(ms)`** (~23) — timed waits (TimeSpan or ms; honors PlayerLoop).
- **`await UniTask.Yield()`** (~15) — wait one frame (cheaper than a coroutine yield).
- **`await UniTask.WaitUntil(() => cond)`** (~8) — poll a condition each frame.
- `await UniTask.CompletedTask`, `await UniTask.WhenAll(...)`, and one `UniTaskVoid`.
- Spread across Core (`SceneRouter`, `GameStateService`, `PersistenceBridge`),
  Village (waves, tutorial, dialogue), Wallet/monetization, Dungeons, BattleATB, Audio.

## Gotchas
- **Fire-and-forget = `.Forget()`, never `async void`.** `async void` swallows
  exceptions and can't be tracked; `.Forget()` routes errors to UniTask's handler.
- **Cancellation:** pass a `CancellationToken` into awaits (e.g.
  `this.GetCancellationTokenOnDestroy()`) so awaits stop when the object is destroyed —
  otherwise a continuation can run on a dead object.
- **WebGL:** WebGL is single-threaded — `UniTask.Delay`/`Yield`/`WaitUntil` (PlayerLoop-
  based) are fine, but anything thread-pool-based (`UniTask.RunOnThreadPool`,
  `SwitchToThreadPool`) does NOT work in WebGL. Keep async work on the player loop.
  (Relevant — the game ships a WebGL build via Butler.)
- It's `UniTask`, not `System.Threading.Tasks.Task` — don't mix the two return types in
  a signature; convert with `.AsUniTask()` / `.AsTask()` only at boundaries.

## Doc sources
- Package: `Library/PackageCache/com.cysharp.unitask@15a4a7657f99/` (v2.5.10)
- Official: https://github.com/Cysharp/UniTask
