**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK_ORDER_474 — Audio on/off actually mutes everything (master toggle)

**Status: READY TO IMPLEMENT** (held until editor closed) · F8 ticket (owner): "audio on/off doesn't work."
**Type:** EXISTING (mis-wired) · **Silo:** Audio/Settings (code + 1 mixer-asset edit)

## Root cause (RCA agent, code-proven) — two compounding defects
1. **Mixer exposes ZERO parameters.** `Assets/Audio/Resources/Audio/GameAudioMixer.mixer` line 18: `m_ExposedParameters: []`.
   So `AudioService.SetMuted`/`SetVolume` `_mixer.SetFloat(...)` and `AudioMixerBridge.SetGroup` all **return false / no-op** —
   the whole `SettingsModel.ApplyAudio → AudioMixerBridge` path cannot affect audio. (The mixer asset DOES load; the
   in-code "no mixer asset" comments are stale — the real fault is the empty exposed-params list.)
2. **The reachable ♪ toggle is MUSIC-ONLY.** `MusicToggleHud.Toggle()` (MusicToggleBootstrap.cs:115-139) OFF branch sets
   MusicVolume=0 + drives the music source, but **never calls `SetMuted(true)`** → SFX voices keep playing → "audio off
   doesn't work." (`AudioService.SetMuted` sets `voice.mute` per source — the path that actually silences SFX — is only
   called on the ON branch.) Secondary: `SetVolume`'s `&& !_fading` guard (AudioService.cs:769) skips the write if toggled
   during a scene-load crossfade → can also drop the music-off.

## DESIGN CONFIRM (owner): the ticket says "audio on/off" → treat the ♪ control as a MASTER toggle (mutes music AND SFX).
If you actually want music-only ♪ + a separate SFX control, say so — this WO assumes master on/off.

## Fix
- **Primary (code):** `MusicToggleBootstrap.cs` `MusicToggleHud.Toggle()` — OFF branch ALSO `AudioServiceBridge.SetMuted(true)` + `SettingsModel.Muted = true`; ON branch keep `SetMuted(false)` + `Muted = false`. `MusicOn` already reflects master mute.
- **Secondary (mixer asset, editor edit — NOT a scene):** expose `MasterVol/MusicVol/SfxVol/UiVol/VoiceVol` on `GameAudioMixer.mixer` so the volume sliders + Settings panel work too.
- **Optional hardening:** drop/defer the `!_fading` skip in `AudioService.SetVolume` so a toggle during a crossfade still applies.

## NOT touch
SettingsController UXML (§8 — non-functional in builds; not the reachable control); the Echo Hollow / unrelated audio.

## Acceptance
Tapping audio-off in MainCastle_Hall silences **music AND SFX**; on restores both; state persists; works even if toggled
during the load crossfade.

## INSTRUMENT-FIRST (§12)
Add `FlowTrace.Step("Audio", ...)` at Toggle entry/branch, `SetMuted` (SetFloat return), `SetVolume` (_fading, final source vol).
Headless tap in MainCastle_Hall: prove SFX voices go muted (not just music) and the SetFloat-returns-false defect. Cite the line.

Key files: `Assets/_Modules/Settings/MusicToggleBootstrap.cs`, `Assets/_Modules/Audio/AudioService.cs`, `Assets/_Modules/Settings/SettingsModel.cs`, `Assets/_Modules/Settings/AudioMixerBridge.cs`, `Assets/Audio/Resources/Audio/GameAudioMixer.mixer`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
