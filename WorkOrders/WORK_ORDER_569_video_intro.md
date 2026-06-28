# WORK ORDER 569 — Video Intro (replace image-slate boot intro with Defenders.mp4)

**Status: IMPLEMENTED (pending CLI gate + commit)**
**Date:** 2026-06-28
**Lane:** VFX/Presentation (isolated — one file)
**Owner decision:** the image-slate intro is replaced by a real ~30s cinematic VIDEO
(`Assets/StreamingAssets/Video/Defenders.mp4`, ends on the gold title "ECHOES OF
ELARION"). Plays full-screen at boot, SKIPPABLE.

---

## What changed

Single file rewritten:
- `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs`

The public seam is **unchanged**:
- `IntroSequencePlayer.Register()` still binds `DeNelle.Core.IntroLauncher.Play`.
- `IntroSequencePlayer.Play()` still spawns the `DontDestroyOnLoad` driver.
- `TitleController.OnPlayIntro()` → `IntroLauncher.Play.Invoke()` works unchanged.
- Advance is still `SceneRouter.GoHeroSelect()`.

The .mp4 itself is committed-pending by CLI (already in the working tree at
`Assets/StreamingAssets/Video/Defenders.mp4`, ~5.6 MB; not yet committed, so it is
not present in this agent worktree — referenced by runtime path only).

---

## Proven pattern mirrored

Mirrored the working VideoPlayer flow from
`Assets/_Modules/Onboarding/SplashLoading.cs`:
- VideoPlayer + `Prepare()` then a bounded wait before `Play()` (SplashLoading
  `TryPlayVideo` lines 161-176).
- `skipOnDrop = false` so a slow decoder doesn't render-then-snap frames
  (SplashLoading lines 150-160, the explicit lesson).
- `errorReceived` handler → fallback path (SplashLoading lines 141-146, 224-228).
- Prepare/start timeout → fallback card (SplashLoading lines 56-57, 172-176).

Difference from SplashLoading (deliberate, both are valid in builds):
- **Source = URL** (`Path.Combine(Application.streamingAssetsPath, "Video/Defenders.mp4")`),
  NOT a `VideoClip`. A StreamingAssets URL plays in Windows + WebGL with no importer
  step. SplashLoading uses an imported `VideoClip`; this WO uses URL per owner spec.
- **Surface = uGUI RawImage** fed by a `RenderTexture`, on
  `ElarionUiKit.BuildModalCanvas("IntroCanvas", 6000)` — matches this file's existing
  uGUI kit. SplashLoading renders its RenderTexture onto a UI-Toolkit
  `VisualElement.style.backgroundImage` (lines 264-268). Both are RenderTexture-based;
  the only difference is the presentation layer (uGUI here vs UI Toolkit there).

(`TitleController.cs` references the bumper via the `_splash` SerializeField but the
bumper stage is CUT — `RunArrival` lines 451-455 — so the live VideoPlayer reference
is SplashLoading; that is the one mirrored.)

---

## How it works

1. **Play full-screen.** `BuildCanvas()` builds a top-most overlay canvas with a black
   backdrop and a full-screen `RawImage` ("VideoSurface"). `BootVideo()` creates a
   `VideoPlayer` (URL source), a `RenderTexture(1920,1080)`, wires it to the surface,
   `Prepare()`s, waits up to 6 s for `isPrepared`, then `Play()`s and enables the
   surface. An opening black dip fades out so the first frame reveals cleanly.
2. **Skip.** Three skip affordances, all → `EndIntro()`:
   - visible gold "Skip ›" button (top-right),
   - full-screen invisible tap target (raycasts, alpha 0),
   - any keyboard key (`Update` → `Keyboard.current.anyKey`).
3. **Natural end.** `loopPointReached` → `OnVideoEnded` → `EndIntro()`.
4. **Advance.** `EndIntro()` blacks the dip, releases the video, calls
   `SceneRouter.GoHeroSelect()`, and `Destroy(gameObject)` (same next-step as before).
5. **Fallback (never hard-blocks boot).** If the file is missing (`File.Exists` false),
   `errorReceived` fires, or `Prepare()` times out → `LogWarning` + `StartFallback()`,
   which tears down the video and runs the original WO-561 five-slate caption sequence
   (kept intact: `RunSlateSequence`/`JumpTo`/`ShowSlate`). Never throws.

---

## Audio handling

`audioOutputMode = VideoAudioOutputMode.AudioSource`; a dedicated `AudioSource` on the
driver receives track 0 (`EnableAudioTrack(0,true)` + `SetTargetAudioSource(0, src)`).
The video's own audio plays directly.

**OWNER FLAG — audio bus:** this plays at the AudioSource's own output, NOT routed
through the SFX/music mixer bus (mixer routing here is non-trivial). Flag if the intro
should sit on the music bus / respect its volume slider. (The old slate intro instead
called `CoreServices.Audio.PlayMusic(MusicTrack.Title)`; that title-music call is
removed since the video carries its own soundtrack.)

---

## Cleanup

`ReleaseVideo()` (idempotent) stops the VideoPlayer, unhooks events, nulls
`targetTexture`/surface texture, and `Release()`+`Destroy()`s the RenderTexture. Called
on fallback, on `EndIntro`, and on `OnDestroy` (belt-and-braces). `Destroy(gameObject)`
tears down the child canvas.

---

## Validation

- Brace balance: **52/52 — OK**
- NUL bytes: **0**
- No UXML (the 2 "UXML" hits are in comments/log strings only)
- No functional Yarn refs (the 3 "Yarn" hits are comment/log strings: "NO Yarn",
  "Yarn-free")
- `using UnityEngine.Video;` added; `DeNelle.DialogueUI` asmdef references
  `DeNelle.Core`, `Unity.TextMeshPro`, `Unity.InputSystem` and has
  `noEngineReferences:false` → the UnityEngine.Video module resolves automatically.

---

## Files for reconcile (CLI, by explicit path)

- `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` (rewritten)
- `Assets/StreamingAssets/Video/Defenders.mp4` (already committed-pending by CLI)
- `WorkOrders/WORK_ORDER_569_video_intro.md` (this file)

NOTE: this agent ff-merged the branch tip `wip/village2-and-f8-tickets`
(ea087782) into its worktree before editing. The .mp4 is uncommitted in the shared
tree so it is absent from the worktree — the code references it by runtime path, so
this is expected and harmless to compilation.

---

## What was NOT touched

- `TitleController.cs` / `SplashLoading.cs` — unchanged (call site + pattern source).
- `SceneRouter`, `IntroLauncher` — unchanged seams.
- No scene files, no asmdef edits, no new packages.
