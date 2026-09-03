# WORK ORDER 1299 — RESULT

**Status:** FIXED (edit-only; gating + commit belong to the lead)
**Silo:** Audio (`DeNelle.Audio`)
**File changed:** `Assets/_Modules/Audio/MusicDirector.cs` (only file touched in code)

## Root cause

On scene teardown the **native** `AudioSource` behind `fadeIn` is destroyed while its managed
wrapper survives. `CrossfadeTo` wrote `fadeIn.clip = clip` with **no null check at all** — the only
unguarded AudioSource access in the method — so the write threw inside
`UnityEngine.AudioSource.set_generatorObject`. Because `CrossfadeTo` is an `async UniTaskVoid` that
nothing awaits, the NRE surfaced only through `UniTaskScheduler.PublishUnobservedTaskException` and
the method never reached `_fading = false`, leaving `_fading` latched `true`; the next
`CrossfadeTo` therefore took the supersede branch and hard-stopped **both** sources before starting,
which is the "music cuts out on a transition" the owner hears.

## Proving line (§12 — captured data, not theory)

`logs/f8-inbox/capture-20260902-013507-seq4364.md`, `scene=Main_Castle_Overworld`, `t=607.533`:

```
UnityEngine.AudioSource.set_generatorObject (UnityEngine.Object value)
UnityEngine.AudioSource.set_clip (UnityEngine.AudioClip value)
DeNelle.Audio.MusicDirector.CrossfadeTo (...) (at .../Assets/_Modules/Audio/MusicDirector.cs:341)
Cysharp.Threading.Tasks.UniTaskScheduler:PublishUnobservedTaskException(Exception)
DeNelle.Audio.<CrossfadeTo>d__33:MoveNext() (at .../MusicDirector.cs:377)
```

`set_generatorObject` is the frame that names it: the throw is on **assigning into a destroyed
native object**, not on a managed null clip. That is exactly the fake-null case only Unity's
overloaded `==` operator can see.

## The fix

All inside `MusicDirector.CrossfadeTo`:

1. **Entry guard (AC 1).** After `fadeIn`/`fadeOut` are chosen, bail if `fadeIn == null || clip == null`
   using **Unity's overloaded `== null`** — stated explicitly in the code comment, because `is null` /
   `ReferenceEquals` see a live managed wrapper and would let the throw straight through. The bail
   clears `_fading` and emits `FlowTrace.Warn("Audio", …)` naming which of the two was dead and the
   track (AC 2). `fadeOut` needed no new guard: every existing touch of it is already
   `if (fadeOut != null)`, which is the same Unity operator, and it is re-evaluated each iteration.
2. **Race window (AC 1).** The four native writes that follow (`clip`, `loop`, `volume`, `Play()`)
   are wrapped in `Guard.Try("Audio", "crossfade prime fade-in source", …)` — teardown does not run
   on our frame, so the source can die between the check and the writes. `Guard.Try` reports via
   `FlowTrace.Fail`, so this is never a silent catch; on `false` it clears `_fading` and returns.
3. **Mid-fade survival (AC 3).** The fade loop now re-checks `fadeIn == null` **every iteration**,
   not once at entry — the UniTask is parked on `UniTask.Yield` across the frames in which the scene
   unloads. A destroyed source unwinds with a `Warn` carrying `t/secs`, and clears `_fading`. The
   post-loop settle path carries the same guard before the final volume snap.
4. `_fading` is now cleared on **every** exit path except the `token != _fadeToken` supersede
   returns, which are deliberately left alone: a newer fade already owns the flag and set it true.

## What proves it

- The three new bail paths each clear `_fading` and log, so the latched-`_fading` secondary symptom
  (music cutting out on the next transition) cannot recur even if a new lifetime hole appears.
- Next occurrence self-reports as `[Flow:Audio] Crossfade bailed/unwound … scene teardown?` instead
  of an unobserved NRE — the failure now names itself (INSTRUMENTATION_STANDARD §1.4).
- Brace/NUL gate: `Assets/_Modules/Audio/MusicDirector.cs` → **BALANCED clean**.
- **Still owed by the lead (AC 4, 5):** `COMPILE_GATE_OK` on a fresh log, and the headless run that
  loads and unloads `Main_Castle_Overworld` under an active crossfade, judged by the `Player.log`
  carrying the Warn line and zero `MusicDirector` NREs — never by the exit code
  (memory `gates-report-success-without-proving-it`). This was an edit-only ticket; no gate was run.

## Deliberately NOT touched

- Crossfade timing, curve, durations, `MusicTrack` selection, the layer stack, `Resolve`,
  `AssertSingleBed`, `FadeOutAll` — this is a lifetime bug, not a mix bug.
- `AudioService.cs`, `SfxClipLibrary`, `IAudioService`, `AudioBootstrap` — out of scope per the WO.
- The `clip.loadState == AudioDataLoadState.Failed` guard is preserved verbatim and still runs
  before `Play()`.
- `CrossfadeTo` remains `async UniTaskVoid`; no call site was converted to await it.
- No existing `FlowTrace` call was removed or weakened (CLAUDE.md §12).
- Nothing under `Packages/com.solana.unity_sdk/`; nothing in HUD, inventory, or hero files.
- Board not regenerated and nothing staged/committed — the lead gates and commits.
