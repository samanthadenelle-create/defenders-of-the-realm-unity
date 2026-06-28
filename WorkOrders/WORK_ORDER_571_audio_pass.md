# WORK ORDER 571 — Audio content pass: Resources-by-id wiring + clip manifest

**Status:** IMPLEMENTED (edit-only; NOT gated/committed — orchestrator reconciles)
**Date:** 2026-06-28  **Lane:** VFX/Audio (§9, no gameplay deps)  **Assembly:** DeNelle.Village
**Branch base:** ff-merged to `wip/village2-and-f8-tickets` tip (3aec8f27)

## Problem (gap audit)

The game "sounds like beeps": SFX are procedural synth tones and several music/voice/
ambient cues ship NULL because they were authored as `[SerializeField] AudioClip` fields
that are never assigned (canon BANS inspector drag-drop, and no prefab carries them).

## RCA — the audio system already exists and is good

- **AudioService** (`Assets/_Modules/Audio/AudioService.cs`) — DDOL singleton music
  director + SFX voice pool + AudioMixer routing + volume/mute. Music clip resolved via
  `ClipFor()` (`:511`); missing clip is guarded + logged (`:369-380`).
- **AudioBootstrap** (`Assets/_Modules/Audio/AudioBootstrap.cs:99-128`) — already loads
  every music clip **by Resources short-name** (`title`, `village`, `Music/echo_theme`,
  …) with `FlowTrace.Warn` on a miss. **Music is already data-driven by id.**
- **SFX**: `AudioService.PlaySfxAtPosition` (`:694`) → `SfxClipLibrary` → else
  `ProceduralSfx.For(id)` (`ProceduralSfx.cs:54`), which itself tries
  `Resources/Sfx/Sfx_<id>` then synths. `GameSfx.cs` / `EnemyCombatAudio.cs` follow the
  same `Resources/Sfx/<name>` ?? synth pattern. **SFX are already data-driven by id with
  a synth fallback** — they are never silent.
- **BattleMusicManager** (`Assets/_Modules/Village/Audio/BattleMusicManager.cs`) — the
  real wave-music scorer (Combat/Intense/Victory/Boss), loads clips by Resources path,
  routes through the shared Music mixer group, self-bootstraps.

So the system is sound; the gap is **specific cues with no clip AND no Resources path**.

## Clips present vs missing (full list in `docs/AUDIO/AUDIO_CLIP_MANIFEST.md`)

- **Present:** title, village, victory, defeat, GameOver, battle pool (×3), overworld
  pool (×2), Arena (echo_theme), Raid (brass-rampart), all 4 wave-battle states, and one
  authored SFX (`Sfx/LookoutHorn.wav`).
- **Missing:** `dungeon` music (only missing music track); ALL Heartwood ambient beds +
  stingers; ALL Heart voice lines; all other SFX play synth placeholders.
- **Orphan:** `Resources/Audio/bellssteel-panic.mp3` (no code reference).

## True code gaps fixed (the three controllers the audit cited)

1. **TowerVoiceController** — voice lines never wired, no Resources path → silent.
2. **HeartwoodAmbientController** — beds/stingers never wired, no Resources path, AND
   **never attached to any GameObject** (dead code — never played).
3. **WaveMusicController** — superseded by BattleMusicManager; wiring it would
   double-score wave music. Left silent + banner-flagged for retirement.

## Changes

### New file
- `Assets/_Modules/Village/Audio/VillageAudioResources.cs` — internal static helper:
  `Load(path)` / `LoadFirst(paths)` (Resources-by-id, WebGL-safe, try/catch) and
  `Group(name)` (resolves the SHARED `AudioMixer` group via `AudioBootstrap.MixerResourcePath`,
  same mixer AudioService/BattleMusicManager use). No drag-drop fields.

### Edited
- `Assets/_Modules/Village/Audio/TowerVoiceController.cs`
  - Routes its AudioSource to the **Voice** mixer group (was bypassing the mixer).
  - `ResolveVoiceLinesFromResources()` (called in Awake): when no line is authored,
    loads `Audio/Voice/HeartFailing(_1/_2/_3)`; `FlowTrace.Warn` self-report if none.
- `Assets/_Modules/Village/Audio/HeartwoodAmbientController.cs`
  - **Self-bootstrap** (`[RuntimeInitializeOnLoadMethod]` + sceneLoaded) attaches the
    controller onto the HeartController GO at runtime — fixes the dead-code gap.
  - Routes beds → **Music** group, stinger → **SFX** group (was default output).
  - `ResolveClipsFromResources()` (Awake): fills unassigned fields from
    `Audio/Ambient/Heartwood_{Healthy,Strained,Critical}` + `Audio/Sfx/Heart_{Hit,Fall}`;
    `FlowTrace.Warn` when all beds missing.
- `Assets/_Modules/Village/Audio/WaveMusicController.cs` — header `⚠ SUPERSEDED` banner
  (per canon §15); no behavior change (stays silent/inert; do NOT wire clips).

### Deliverables
- `docs/AUDIO/AUDIO_CLIP_MANIFEST.md` — every needed clip by exact Resources path + 1-line
  description + present/missing + owner-decision flags.
- `WorkOrders/WORK_ORDER_571_audio_pass.md` (this file).

## Fallback behavior (unchanged philosophy)

- SFX: authored clip wins → else `Resources/Sfx/...` drop-in → else procedural synth
  (never silent).
- Music / voice / ambient: authored/Resources clip → else **silent + FlowTrace.Warn**
  (no synth — can't synth a song/speech). No errors, no throws.

## Volume / settings

All newly-routed sources go through the shared `GameAudioMixer` groups (Voice / Music /
SFX), so the existing settings volume + mute (`AudioService.SetVolume/SetMuted`,
`GameState.MusicVolume/SfxVolume`, `AudioMixerBridge`) now apply to them.

## Validation

- Brace check PASS: VillageAudioResources 14/14, TowerVoiceController 15/15,
  HeartwoodAmbientController 36/36, WaveMusicController 13/13.
- No drag-drop fields added. No `.unity`/`.asset` hand-edits. No new `System.Reflection`.
  Null-safe cross-module calls. `AudioService`/`CoreServices.Audio` + existing callers
  untouched.

## NOT done (owner decisions — see manifest)

- Sourcing the actual clips (dungeon music, Heartwood beds/stingers, Heart VO, authored
  SFX) — content task; the wiring is ready for drop-in.
- Removing `WaveMusicController` from `WaveSystemBridgeBootstrap` (retirement) — deferred
  until BattleMusicManager felt-verified.

## Headless verify suggestion (CLI)

`DataRegression` already guards the silent-track class (`Resources.Load("dungeon")==null`,
`DataRegression.cs:149`). After a clip drop, run the compile gate + data regression.
