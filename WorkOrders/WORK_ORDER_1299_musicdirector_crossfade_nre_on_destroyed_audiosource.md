# WORK ORDER 1299 — `MusicDirector.CrossfadeTo` throws NRE on an AudioSource destroyed under an in-flight UniTask

**Status:** READY TO IMPLEMENT
**Source:** F8 capture seq **4364** (`kind=exception`). Ledger: `docs/qa/F8_TRIAGE_2026-09-02.md` §3.
**Silo:** Audio (`DeNelle.Audio`) — no gameplay dependencies, safe parallel lane (CLAUDE.md §9)
**Severity:** P3 — an unobserved exception at teardown. Not a blocker, but it is a one-line class of bug
that fires on a scene transition, which is the most-travelled path in the game.

## Owner-facing symptom

Leaving `Main_Castle_Overworld` (scene teardown / transition) raises an unhandled
`NullReferenceException` from the music crossfade. The fade never completes, so `_fading` is left
latched `true` — the next `CrossfadeTo` on a fresh scene takes the `if (_fading)` branch and hard-stops
both music sources before starting, which is heard as music cutting out on a transition.

## Captured proving line (§12 evidence — quoted verbatim)

`logs/f8-inbox/capture-20260902-013507-seq4364.md`, `scene=Main_Castle_Overworld`, `t=607.5332641601563`
(the final event of that session):

```
NullReferenceException: Object reference not set to an instance of an object.
UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj)
UnityEngine.AudioSource.set_generatorObject (UnityEngine.Object value)
UnityEngine.AudioSource.set_clip (UnityEngine.AudioClip value)
DeNelle.Audio.MusicDirector.CrossfadeTo (UnityEngine.AudioClip clip, System.Single targetVol,
  System.Boolean loop, System.Single fadeSeconds) (at D:/EoA/Assets/_Modules/Audio/MusicDirector.cs:341)
UnityEngine.Debug:LogException(Exception)
Cysharp.Threading.Tasks.UniTaskScheduler:PublishUnobservedTaskException(Exception)
  (at ./Packages/com.solana.unity_sdk/Runtime/Plugins/UniTask/Runtime/UniTaskScheduler.cs:90)
DeNelle.Audio.<CrossfadeTo>d__33:MoveNext() (at D:/EoA/Assets/_Modules/Audio/MusicDirector.cs:377)
Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder:Start(<CrossfadeTo>d__33&)
```

## Suspected seam — pinned to the line

`Assets/_Modules/Audio/MusicDirector.cs:337-341`:

```csharp
AudioSource fadeIn  = (_activeSource == _musicA) ? _musicB : _musicA;
AudioSource fadeOut = _activeSource;
_activeSource = fadeIn;

fadeIn.clip = clip;          // <-- line 341, no null check on fadeIn
```

- `fadeIn` is **never null-checked** before line 341. Every other AudioSource access in the method
  (lines 330-333) is guarded with `!= null`; this one is not.
- The NRE is raised inside `AudioSource.set_generatorObject`, i.e. the **native** AudioSource was
  destroyed while the managed wrapper survived — the classic Unity fake-null case that a bare
  `fadeIn.clip = …` cannot detect but `fadeIn == null` (Unity's overloaded operator) can.
- It surfaces via `UniTaskScheduler.PublishUnobservedTaskException` because `CrossfadeTo` is an
  `async UniTaskVoid` (`<CrossfadeTo>d__33`, resumed at line 377). Nothing awaits it, so the throw is
  invisible to the caller and `_fading` is never reset.

## Acceptance criteria

1. `CrossfadeTo` null-guards **both** `fadeIn` and `fadeOut` with Unity's `== null` (not `is null` /
   `ReferenceEquals`, which miss a destroyed native object) before touching `.clip`, `.loop`,
   `.volume`, `.Play()` or `.Stop()`.
2. On a bail-out, `_fading` is reset to `false` and a `FlowTrace.Warn("Audio", …)` names the reason —
   no silent catch (CLAUDE.md §12 step 2).
3. The async body (from line 377 onward) survives the sources being destroyed **mid-fade**: it
   re-checks the source each iteration rather than once at entry, and unwinds cleanly.
4. Headless proof: a run that loads `Main_Castle_Overworld` and unloads it during an active crossfade
   produces **zero** `NullReferenceException` from `MusicDirector`, and the `Player.log` carries the new
   Warn line instead. Judge by the log, not the exit code (memory `gates-report-success-without-proving-it`).
5. `COMPILE_GATE_OK` on a fresh log; brace-balance check on `MusicDirector.cs` (CLAUDE.md §1).

## What NOT to touch

- ⛔ Do not change the crossfade *timing*, curve, durations, or `MusicTrack` selection — this is a
  lifetime bug, not a mix bug.
- ⛔ Do not touch `AudioService`, `SfxClipLibrary`, or `IAudioService`. The fix lives entirely inside
  `Assets/_Modules/Audio/MusicDirector.cs`.
- ⛔ Do not convert `CrossfadeTo` away from `UniTaskVoid` or introduce a new awaiting call site — that
  is a larger change than the evidence supports.
- ⛔ Do not remove the existing `clip.loadState == AudioDataLoadState.Failed` guard (lines 346-349);
  it covers a different failure and is load-bearing.
- ⛔ Do not touch anything under `Packages/com.solana.unity_sdk/`.
