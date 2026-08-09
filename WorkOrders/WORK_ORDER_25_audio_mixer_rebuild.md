# WORK ORDER 25 — Rebuild GameAudioMixer (volume sliders dead)

**Status:** READY TO IMPLEMENT (reconciled 2026-08-09 from the tree - still unfixed: `Assets/Audio/Resources/Audio/GameAudioMixer.mixer` still reads `m_ExposedParameters: []`, and no commit references WO-25)

**Date:** 2026-05-24 (filed from owner playtest triage). **Authority:** #35 + WO-025.
**Priority:** Medium-High. **Depends on:** WO-05. **Class:** TRACKED-ASSET rebuild (needs the Audio Mixer GUI — NOT a code change).

## Bug (#A) — volume sliders do nothing; log spams "Mixer has no exposed parameter"
Player.log: `[AudioMixerBridge] Mixer has no exposed parameter 'MasterVol' / 'MusicVol' / 'SfxVol'`.

**Root cause (NOT a name mismatch):** the code is correct + consistent — `AudioMixerBridge.cs` and `AudioService.MixerParams` both expect exactly `MasterVol`/`MusicVol`/`SfxVol` (+`UiVol`/`VoiceVol`), and `docs/port-notes/audio-system.md` documents the mixer as shipping 5 groups + 5 exposed params. But the **asset on disk is an empty stub**: `Assets/Audio/Resources/Audio/GameAudioMixer.mixer` has `m_ExposedParameters: []` and the Master group's `m_Children: []` (no Music/SFX/UI/Voice groups). So `mixer.SetFloat("MasterVol", db)` returns false → the warning, and sliders never reach audio. (The documented "5 groups/5 params" version was never persisted into the asset — a Unity-serialization-didn't-persist case.) `SetFloat` cannot expose params at runtime, so this MUST be fixed in the asset.

## Fix (Audio Mixer window — do NOT hand-edit the .mixer YAML)
1. Open `Assets/Audio/Resources/Audio/GameAudioMixer.mixer` in the Audio Mixer window.
2. Add child groups under Master: `Music`, `SFX` (+ `UI`, `Voice` to match AudioService).
3. For each group's Attenuation **Volume**, right-click → **Expose to script**, then rename in the Exposed Parameters list to EXACTLY (case-sensitive): `MasterVol`, `MusicVol`, `SfxVol` (+`UiVol`, `VoiceVol`).
4. Save; keep the asset path + GUID (`3d421b8c8923e2148b27f9a8cb7f160d`) so references resolve. Do NOT change the code/param names — they're correct.

## Acceptance criteria
1. `GameAudioMixer.mixer` exposes `MasterVol/MusicVol/SfxVol` (+`UiVol/VoiceVol`) on the matching groups.
2. The `[AudioMixerBridge]` warnings stop; Master/Music/SFX sliders attenuate audio.
3. `WORK_ORDER_25_*.RESULT.md` written.

Key files: `Assets/_Modules/Settings/AudioMixerBridge.cs`, `Assets/_Modules/Audio/AudioService.cs`, `Assets/Audio/Resources/Audio/GameAudioMixer.mixer`, `docs/port-notes/audio-system.md`.
