# MASTER CATALOG — Audio (`DeNelle.Audio`)

Scope: `Assets/_Modules/Audio/` + the audio assets/mixer it loads + the audio
docs. Verified by reading source + assets (not comments). Asmdef
`DeNelle.Audio` → references `DeNelle.Core` + `UniTask` only; namespace
`DeNelle.Audio`; `autoReferenced: true`.

---

## CODE — classes

### AudioService — `Assets/_Modules/Audio/AudioService.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `sealed MonoBehaviour : IAudioService`, `[DisallowMultipleComponent]`.
- **Responsibility:** game-wide audio director — owns 2 music AudioSources (A/B crossfade) + 8-voice SFX pool, fires SFX, applies the AudioMixer, crossfades per-scene BGM, persists/serves the jukebox ambient choice.
- **Bootstrap:** NOT self-spawning; created by `AudioBootstrap` (RuntimeInit AfterSceneLoad). `Awake` claims singleton (`_instance`) + `DontDestroyOnLoad`, builds sources, resolves mixer groups, `CoreServices.RegisterAudio(this)`. `OnEnable/OnDisable` (un)subscribe `SceneManager.sceneLoaded`. `Start` warns if no mixer, `ApplyPersistedSettings`, `ApplyMobilePlatformRules`, plays the active scene's track.
- **Singleton dedup:** `if (_instance != null && _instance != this) Destroy(gameObject)` — note this destroys the whole GameObject (safe here: dedicated host, not shared; contrast the `singleton-dedup-destroys-host` memory).
- **Key PUBLIC methods:**
  - `static AudioService Instance` — live service or null pre-bootstrap.
  - `MusicTrack CurrentTrack {get;}` — current/fading-in track.
  - `void PlayMusic(MusicTrack)` — crossfade; same-track no-op; `None`→StopMusic; missing-clip guarded (logs, records intent, plays silent).
  - `void StopMusic()` — fade current out to silence.
  - `void ResumeAfterUnlock()` — WebGL gesture unlock: `AudioListener.pause=false`, re-asserts CurrentTrack.
  - `void PlaySfx(AudioClip, float vol=1)` / `PlayUiSfx(...)` / `PlayVoice(...)` — one-shot on SFX/UI/Voice group (round-robin pool).
  - `void PlayUiClick()` — DEF-183 shared button blip; loads `Resources/Sfx/UiClick` or synthesizes a tick. This is the `IAudioService` seam HUD calls.
  - `void PlaySfxAtPosition(SfxId, Vector3, float vol=1)` — resolves clip via `_sfxLibrary`; **silent no-op when library unassigned** (it is — see FLAGS).
  - `void SetVolume(MixerGroup, float linear01)` — Master allows 0..1.5; writes exposed mixer param AND scales music source directly (dual-path fallback).
  - `float GetVolume(MixerGroup, float fallback=1)`.
  - `void SetMuted(bool)` / `bool IsMuted` — snaps; writes Master param AND every source `.mute` flag.
  - `void SetMixer(AudioMixer)` — bootstrap hands the Resources mixer in post-Awake.
  - `void SetMusicClip(MusicTrack, AudioClip)` / `void AddMusicClip(MusicTrack, AudioClip)` — runtime clip-assign seams; Battle/Overworld are pooled (WO-171 rotation).
  - `void ApplyPersistedSettings()` — seeds Music/SFX vol + Muted from `GameStateService.State` (0..100→0..1).
  - `void HandleSceneMusic(string)` + `static MusicTrack TrackForScene(string)` — scene→track map.
  - `void PlayAmbientContext(AmbientContext)` / `void ReturnToAmbient()` / `MusicTrack GetAmbientChoice(ctx)` / `void SetAmbientChoice(ctx, track)` / `void ClearAmbientChoice(ctx)` — WO-162 jukebox; choices persisted in `PlayerPrefs` key `dotr-ambient-music-choice-<int>`.
  - `static IReadOnlyList<MusicChoice> AmbientChoicesFor(AmbientContext)` — curated jukebox list.
  - `static MixerGroup`/`static class MixerParams` — 5 groups + exposed-param name contract (`MasterVol/MusicVol/SfxVol/UiVol/VoiceVol`).
  - `static float LinearToDecibels/DecibelsToLinear`.
  - explicit `IAudioService.PlayMusic(Core.MusicTrack)` — maps Core enum → Audio enum (Overworld→`PlayAmbientContext`).
- **Inspector fields:** `_mixer`, 4 group refs, `_titleClip/_villageClip/_dungeonClip/_battleClip/_victoryClip/_defeatClip/_arenaClip`, `_battlePool`/`_overworldPool` (List), `_sfxLibrary`.
- **Deps:** `DeNelle.Core` (CoreServices, SceneRouter, GameState/GameStateService), UniTask, UnityEngine.Audio. Mixer group resolve is by-name via `FindMatchingGroups`.
- **Wired/live:** LIVE for music (auto-spawned, scene-driven, dual-path source fallback works without a real mixer). SFX-by-SfxId path DEAD (no library asset). Mixer-param path DEAD (stub mixer — FLAGS).

### AudioBootstrap — `Assets/_Modules/Audio/AudioBootstrap.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `static class`.
- **Responsibility:** guarantees one AudioService exists, no scene wiring.
- **Bootstrap:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] EnsureAudioService()`. Idempotent (no-op if `AudioService.Instance != null`).
- **Two paths:** (1) PREFAB `Resources.Load<GameObject>("DeNelleAudioService")` — **prefab does NOT exist** (verified), so always falls through. (2) CODE: `new GameObject` + `AddComponent<AudioService>`, mixer from `Resources.Load<AudioMixer>("Audio/GameAudioMixer")`, then loads clips by Resources short-name.
- **Clip load list:** `title, village, victory, dungeon, defeat, Audio/Music/GameOver` (Defeat reassigned), Battle pool `battle_theme_NEW + battle_theme2_NEW + battle_theme3_NEW`, Overworld pool `mainworld1_NEW + world_theme_NEW`, Arena `Music/echo_theme`.
- `UnmuteOnce()` — one-time `PlayerPrefs` migration `dotr-unmute-migration-v1`; flips `GameState.Muted=false` via reflection (`DeNelle.Core` GameStateService).
- **Wired/live:** LIVE — the sole spawner. Always uses the code path.

### MusicTrack / MusicChoice / MusicTrackDef / MusicTrackRegistry — `Assets/_Modules/Audio/MusicTrack.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. Pure C# (no MonoBehaviour).
- `enum MusicTrack` (Audio-side): None,Title,Village,Dungeon,Battle,Victory,Defeat,Overworld,Arena. **NOTE: different ordering from the Core-side enum** (see FLAGS).
- `MusicChoice` — immutable `{MusicTrack Track; string DisplayName}` jukebox row.
- `MusicTrackDef` — immutable per-track mix: `AssetPath, DefaultVolume, Loop, FadeInSeconds, FadeOutSeconds`.
- `static MusicTrackRegistry` — owner-locked table (port of React `audioManager.ts`): `Get(track)`, `Has(track)`, named vol consts. Volumes: Title .6, Village .4, Dungeon .25, Battle .7, Victory .7, Defeat .5, Overworld .4, Arena .4. Stings (Victory/Defeat) don't loop.
- **Wired/live:** LIVE — referenced by AudioService for fade/volume/loop. `AssetPath` strings are documentation/log-only (clips loaded by Resources short-name in bootstrap, NOT from these paths).

### SfxId — `Assets/_Modules/Audio/SfxId.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `enum`.
- Values: None,FireExplosion,ArcaneExplosion,Shockwave,Heal,WizardCast,FlameArrowLaunch,TowerShot,EnemyDeath,WaveClear,LevelUp,ComboSmall,ComboBig,PetFireAura,PetAttack,WardLit,WardDim.
- Key into `SfxClipLibrary` + `VFXManager.VfxToSfx()`.
- **Wired/live:** enum referenced by callers, but the resolution path is dead (no library asset).

### SfxClipLibrary — `Assets/_Modules/Audio/SfxClipLibrary.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `sealed ScriptableObject`, `[CreateAssetMenu] Defenders/Audio/SFX Clip Library`.
- **Responsibility:** maps `SfxId`→`AudioClip`(+volume). `Entry[] Entries`. Lazy dict (`BuildLookup` on `OnEnable/OnValidate`; last dup wins).
- **Key methods:** `AudioClip GetClip(SfxId)` (null→silent), `float GetVolume(SfxId)` (default 1).
- **Wired/live:** CLASS live, but **no `.asset` instance exists** anywhere in the project (verified). `AudioService._sfxLibrary` is therefore null → `PlaySfxAtPosition` no-ops. Effectively DEAD until an asset is authored + assigned.

### WebGLAudioUnlock — `Assets/_Modules/Audio/WebGLAudioUnlock.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `sealed MonoBehaviour`.
- **Responsibility:** un-suspends mobile-browser AudioContext on first user gesture.
- **Bootstrap:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()` — self-spawns a DDOL GameObject (guarded by `s_spawned`). `Update` polls touch/mouse/anyKey; on first gesture calls `AudioService.Instance?.ResumeAfterUnlock()` then `Destroy(gameObject)`.
- **Wired/live:** LIVE. Harmless off-WebGL (re-affirms current track once).

### MusicSelectionPanel — `Assets/_Modules/Audio/MusicSelectionPanel.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `sealed MonoBehaviour`, `[RequireComponent(UIDocument)]`.
- **Responsibility:** WO-162 jukebox UI — code-built (no UXML) UI Toolkit panel; `J` toggles, `Esc` closes. Lists `AmbientChoicesFor(CurrentAmbientContext)`, checkmark on selected, tap → `SetAmbientChoice` (persist + live preview).
- **Bootstrap:** spawned by `MusicSelectionPanelBootstrap`. `Awake` borrows a live PanelSettings (disables itself + warns if none), `sortingOrder=96`.
- **Wired/live:** LIVE in scenes that have a HUD canvas / PanelSettings; quietly absent otherwise. All audio state delegated to AudioService.

### MusicSelectionPanelBootstrap — `Assets/_Modules/Audio/MusicSelectionPanelBootstrap.cs`
- **ns/asmdef:** `DeNelle.Audio` / `DeNelle.Audio`. `static class`.
- **Responsibility:** auto-attaches one `MusicSelectionPanel` per scene that has a UIDocument+PanelSettings (mirrors CosmeticShopPanelBootstrap). `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + `sceneLoaded` hook. Idempotent per scene.
- **Wired/live:** LIVE.

---

## CORE-SIDE (out of scope dir, referenced)
- `Assets/_Modules/Core/Audio/IAudioService.cs` — `interface IAudioService { PlaySfx(clip,vol); PlayMusic(Core.MusicTrack); PlayUiClick(); }`. Resolved via `CoreServices.Audio`.
- `Assets/_Modules/Core/Audio/MusicTrack.cs` — Core-side `enum MusicTrack { Village=0, Battle=1, Victory=2, Dungeon=3, Overworld=4, Defeat=5, Title=6, Arena=7 }` (explicit indices, append-at-end for save/PlayerPrefs stability).

---

## DATA / ASSETS

### AudioMixer — `Assets/Audio/Resources/Audio/GameAudioMixer.mixer`
- Loaded at `Resources.Load<AudioMixer>("Audio/GameAudioMixer")`.
- **ACTUAL CONTENTS (read):** name `GameAudioMixer`; **ONLY a `Master` group**; `m_ExposedParameters: []`; one default Attenuation effect; one snapshot. **No Music/SFX/UI/Voice child groups. No `MasterVol`/`MusicVol`/`SfxVol`/`UiVol`/`VoiceVol`/`ReverbSend` exposed params.** This is a near-default stub, NOT the 5-group/5-param mixer the code + docs assume. → See FLAGS.

### Music MP3s — `Assets/Audio/Resources/` (and subfolders)
Present (verified): `title.mp3, village.mp3, battle.mp3, victory.mp3, defeat.mp3, battle_theme_NEW.mp3, battle_theme2_NEW.mp3, battle_theme3_NEW.mp3, mainworld1_NEW.mp3, world_theme_NEW.mp3, Music/echo_theme.mp3`.
Also present but NOT loaded by bootstrap short-names: `Music/Battle/Overworld_Battle_1.mp3, Overworld_Battle_2.mp3, Overworld_Boss_Fight.mp3, Overworld_Victory.mp3`, and `Assets/Audio/Victory/Victory.mp3` (outside Resources).
**Absent** (bootstrap loads by name, missing→silent): `dungeon` (echoes-beneath-elarion — known-missing), `world` (Overworld registry path `world.mp3` — only `world_theme_NEW`/`mainworld1_NEW` exist under different names), `Audio/Music/GameOver` (GameOver.mp3 referenced for Defeat, not present).

### SfxClipLibrary.asset — **DOES NOT EXIST** anywhere (verified). SfxId→clip path unwired.
### DeNelleAudioService.prefab — **DOES NOT EXIST** (verified). Prefab bootstrap path never taken.
### audio-mix.json — not present in StreamingAssets (registry constants used instead; per port-note).

---

## DOCS
- `Assets/_Modules/Audio/README.md` — module file map. **Partly STALE:** describes AudioBootstrap as "scene wiring" (it's RuntimeInit auto-spawn, no scene wiring); calls the asset set "clip registry" (fine). Mentions "Mix spec: docs/audio-mix-spec.md. Full audio pass: WO-243."
- `docs/audio-mix-spec.md` — owner-locked mix spec (§2 volumes/fades, §3 transitions, §4 nudges, §5 mute, §7 Unity port note). Current as the design source; some §4/§5 features unbuilt (nudges, reduced-motion snap).
- `docs/port-notes/audio-system.md` — P0-9 port note (2026-05-19). **Title says the mixer has 5 groups + 5 exposed params — CONTRADICTED by the actual asset (stub). STALE/aspirational vs shipped asset.** Also lists integrator TODOs (delete stray `DeNelleAudioMixer.mixer`, import MP3s, author prefab, wire battle stings) — verify which are done.

---

## FLAGS

### Stale-comment / doc-vs-code mismatches
1. **GameAudioMixer is a stub, not the documented 5-group mixer.** `AudioService` header, `MixerParams`, `AudioBootstrap` comments, README cross-module note, and `port-notes/audio-system.md` all assert groups Master/Music/SFX/UI/Voice with exposed params `MasterVol/MusicVol/SfxVol/UiVol/VoiceVol`. The actual `.mixer` has only `Master`, no children, `m_ExposedParameters: []`. → `_mixer.SetFloat(...)` in `SetVolume`/`SetMuted`/`ApplyMobilePlatformRules` (incl. `"ReverbSend"`) silently fail; `FirstGroup("Music"/"SFX"/...)` returns null so sources keep their inspector group (none). Only the AudioSource-direct fallback in `SetVolume`/`SetMuted` actually controls volume/mute. `MusicToggleBootstrap.cs` (Settings) comment already documents this reality ("bridge NO-OPS when no AudioMixer assigned"). **This is the load-bearing gap: the documented mixer was never built into the asset.**
2. **AudioBootstrap comment "carries the mixer + the six music clips wired in the inspector" / prefab path** — no `DeNelleAudioService.prefab` exists; the prefab branch is dead. Comment implies a fidelity path that doesn't ship.
3. **README "AudioBootstrap — scene wiring"** — it is the opposite: RuntimeInit auto-spawn, explicitly *no* scene wiring.
4. **MusicTrackRegistry `AssetPath` strings are stale-by-design** vs actual files: Overworld path says `world.mp3` (file is `world_theme_NEW.mp3`/`mainworld1_NEW.mp3`); Dungeon path is the known-missing asset; Battle path `battle.mp3` vs the loaded `battle_theme*_NEW.mp3`. These strings are doc/log-only and not used for loading, but they no longer describe what loads.
5. **Two MusicTrack enums with different member order** (Audio-side declaration order vs Core-side explicit indices). Audio-side is cast to/from `int` for PlayerPrefs (`GetAmbientChoice` casts the stored int). Core-side has explicit indices "for save stability." The explicit `IAudioService.PlayMusic` switch maps between them by name so playback is correct, BUT the **persisted jukebox PlayerPrefs int is the Audio-side ordinal** — if the Audio-side enum is ever reordered, saved jukebox picks shift. Worth noting (not currently broken).

### Dead / unwired
6. **SfxClipLibrary / SfxId / PlaySfxAtPosition** — no `SfxClipLibrary.asset` exists and `_sfxLibrary` is unassigned → every `PlaySfxAtPosition(SfxId,...)` is a silent no-op. Whole SfxId→clip subsystem is built-but-dead pending an authored asset. (SFX callers live in Village/Pets modules — out of this scope.)
7. **DeNelleAudioService.prefab path** in AudioBootstrap — dead branch (asset absent).

### Missing assets (guarded, intentional)
8. Dungeon BGM, GameOver.mp3 (Defeat upgrade), and an Overworld `world.mp3` matching the registry name are absent — guarded missing-clip path logs + plays silent. Defeat falls back to `defeat.mp3` (present).

### Unbuilt spec features
9. §4 volume nudges, §5 reduced-motion fade-snap, and `audio-mix.json` hydration are not implemented (per port-note "Known limitations").

### Integrator TODO possibly stale
10. `port-notes/audio-system.md` step "DELETE stray `Assets/Audio/DeNelleAudioMixer.mixer`" — that file was not found in the current tree (likely already deleted); the note still lists it as a pending task.
