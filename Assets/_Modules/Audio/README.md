# Audio — `DeNelle.Audio`

Audio playback service. Implements `IAudioService` (from Core), resolved via
`CoreServices.Audio`.

## Files

- `AudioService` — implements `IAudioService`; music + SFX playback
- `AudioBootstrap` — scene wiring
- `SfxClipLibrary`, `SfxId`, `MusicTrack` — clip registry + IDs
- `MusicSelectionPanel` + `MusicSelectionPanelBootstrap` — player music picker (WO-162)
- `WebGLAudioUnlock` — unmutes audio on first user gesture in WebGL builds

Mix spec: `docs/audio-mix-spec.md`. Full audio pass: WO-243.

> Maintenance: update this README when files are added/removed.
