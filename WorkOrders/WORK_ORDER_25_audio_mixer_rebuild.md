<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 25 — Rebuild GameAudioMixer (volume sliders dead)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

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

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `GameAudioMixer.mixer:18 m_ExposedParameters []` — sliders dead. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
