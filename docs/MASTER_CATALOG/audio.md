# MASTER CATALOG — Audio (`DeNelle.Audio` + the Village audio players + assets)

**Rewritten 2026-08-02 — verified from the actual code AND assets (not comments), file:line cites.**
Scope: `Assets/_Modules/Audio/` (service, director, bootstrap, SFX, jukebox), the Core audio seams,
the mixer/clip assets they load, the Village-side SFX/music policy providers, the Hovl vendor audio
note, and the audio regression oracles. Supersedes the prior revision (which pre-dated
MusicDirector and the WO-243/682 SFX work).

Legend: **[LIVE]** wired & functional · **[STUB]** scaffolded/inert · **[DEAD]** unused ·
**[FLAG n]** see Risk Ledger.

Asmdef: `DeNelle.Audio` → refs `DeNelle.Core` + UniTask only; autoReferenced.

---

## 1. THE TWO LOAD-BEARING TRUTHS (read these before touching audio)

### ★ [FLAG 1] GameAudioMixer is a STUB — every mixer SetFloat silently fails
`Assets/Audio/Resources/Audio/GameAudioMixer.mixer` (verified by reading the asset 2026-08-02):
name `GameAudioMixer`, **ONLY a `Master` group** (`m_Children: []`), **`m_ExposedParameters: []`**,
one default Attenuation effect, one snapshot. The code contract (`AudioService.MixerParams`
`AudioService.cs:172-184`: `MasterVol/MusicVol/SfxVol/UiVol/VoiceVol`) matches NOTHING in the
asset. Consequences, all verified in code:
- `SetVolume`'s `_mixer.SetFloat(...)` (`AudioService.cs:817-818`), `SetMuted`'s
  `SetFloat(Master, -80)` (`:850-853`), and `ApplyMobilePlatformRules`' SfxVol/ReverbSend writes
  (`:1160-1167`) all target non-exposed params → **silent no-ops**.
- `ResolveMixerGroups`' `FindMatchingGroups("Music"/"SFX"/"UI"/"Voice")` (`:313-331`) returns
  empty → group refs stay null → all sources play on the default output.
- **The REAL volume/mute path is AudioSource-direct:** `SetVolume` also calls
  `_director.ApplyVolumeScale(v)` for Master/Music (`:825-826` — deliberately "not mixer XOR
  source"), and `SetMuted` always snaps `.mute` on the director pair + every SFX voice
  (`:854-863`, `MusicDirector.SetMuted` `MusicDirector.cs:462-467`). SFX/UI/Voice sliders have NO
  working path beyond the per-call volume argument.
The Settings module's `MusicToggleBootstrap.cs` already documents this reality. Building the real
5-group mixer with exposed params remains the unshipped fix.

### ★ Music ownership = MusicDirector (2026-07-09, MUSIC_AUTHORITY_DESIGN)
Exactly ONE class owns music AudioSources: **`MusicDirector`** (one A/B pair). `AudioService`,
`BattleMusicManager`, `WaveMusicController`, `WorldMusicDirector` are POLICY PROVIDERS that
Push/Release a `MusicLayer`; two beds are impossible by construction, and
`AssertSingleBed` fires a `FlowTrace.Fail` if the invariant ever trips (`MusicDirector.cs:417-434`).

---

## 2. CODE — `Assets/_Modules/Audio/`

### AudioService.cs  [LIVE — facade + clip lookup + SFX pool]
`sealed MonoBehaviour : IAudioService`, singleton, DDOL; created by `AudioBootstrap` (not
self-spawning). Registers with `CoreServices.RegisterAudio` (`:218`).
- **No longer owns music sources** (`:137-148` — moved to MusicDirector; `_facadeLayer` tracks the
  single layer the facade occupies). `BuildAudioSources` creates the director + the **8-voice
  round-robin SFX pool** (`SfxVoices=8` `:152-154`, `:268-289`).
- **`PlayMusic(track)`** (`:350-406`): None→StopMusic; Battle/Arena triggers `PrewarmCombatSfx`
  (`:363-364`); same-track-and-sounding idempotency guard (F8 2026-07-10 music-thrash, `:370-371`);
  registry-null → warn+ignore (`:373-378`) **[FLAG 2 — Raid has no registry row]**; clip lookup
  stays here (`ClipFor` `:444-458`, rotation pools `NextFromPool` `:465-477`); then
  `_director.PushClip(LayerFor(track), ...)` with a Release of the previous facade layer
  (`:400-405`). `StopMusic` releases the facade layer (`:428-436`).
- **Rotation pools (WO-171):** `_battlePool` / `_overworldPool` (`:119-125`), seeded/extended via
  `SetMusicClip`/`AddMusicClip` (`:485-534`).
- **SFX:** `PlaySfx/PlayUiSfx/PlayVoice` one-shots (`:558-616`); `PlayUiClick` (DEF-183, the
  IAudioService seam HUD calls) loads `Resources/Sfx/UiClick` else synthesizes a tick
  (`:581-611`).
- **`PlaySfxAtPosition(SfxId,...)` is NO LONGER a silent no-op (WO-243):** resolves
  `Resources.Load<SfxClipLibrary>("Audio/SfxClipLibrary")` once (`:636-643`, path const `:1142`) —
  the asset does not exist (§4) — then **falls back to `ProceduralSfx.For(id)`** (`:661-662`), so
  every SfxId is audible with zero asset wiring. An authored library would win if built.
- **WO-682 hardening:** `PlayOneShotOn` quarantines undecodable clips (WebGL "Loading FSB failed"
  class) — one Warn then silent skips (`:668-690`, `_deadSfxClips` `:699`, `MarkSfxClipDead`
  `:795-799`). `PrewarmCombatSfx` (`:733-787`) decodes the library entries + the 20-name
  `CombatSfxResourceNames` set (`:711-719`) at battle/arena load so the first swing never pays the
  FSB decode stall (db-proven 167ms/4000ms frames).
- **Volume/mute/persist:** `SetVolume` (dual-path, §1), `GetVolume` (`:834-839`), `SetMuted`
  (`:847-864`), `LinearToDecibels/DecibelsToLinear` (`:887-898`), `ApplyPersistedSettings` seeds
  Music/SFX/Muted from GameState 0..100 values at Start (`:906-918`).
- **Scene→track map:** `HandleSceneMusic`/`TrackForScene` (`:936-985`) — `Dungeon_*`→Dungeon;
  MainCastle_Hall / CastleHub / CastleHub_MainKeep / Main_Castle_Overworld → Village ambient
  (`:974-976`); Village routes through `PlayAmbientContext` so the jukebox pick wins (`:946-951`);
  unknown scene leaves music alone.
- **Ambient/jukebox (WO-162/171):** `AmbientContext{Village,Overworld}` (`:1006-1012`),
  `PlayAmbientContext`/`ReturnToAmbient` (`:1038-1054`), choice persisted per-context in
  PlayerPrefs `dotr-ambient-music-choice-<int>` (`:1021`, `:1063-1086`) **[FLAG 5 — Audio-enum
  ordinal persisted]**; state cues (Battle/Victory/Defeat) can never be jukeboxed (`:1093-1098`);
  curated list `AmbientChoicesFor` (`:1107-1128`).
- **WebGL unlock:** `ResumeAfterUnlock` → `AudioListener.pause=false` + `_director.Reassert()`
  (`:417-420`).
- **explicit `IAudioService.PlayMusic(Core.MusicTrack)`** maps Core→Audio by name incl. Arena,
  Raid, Title (`:1181-1198`).

### MusicDirector.cs  [LIVE — THE single music owner]
Plain C# `sealed class : IMusicAuthority` (Core seam), created by AudioService
(`GetOrCreate(host, group, clipResolver)` `:127-134`; sources `Music_A`/`Music_B` parented under
the AudioService GO `:136-147`).
- **Priority-layer stack:** dense 7-slot `LayerEntry[]` (`:85-97`, `Cutscene=6` top); on every
  Push/Release, `Resolve` sounds the highest active layer or fades to silence (`:270-314`);
  idempotent when the top clip is already sounding (`:297-301`). Auto-fallback on Release deletes
  the whole "forgot to restore ambient" bug class (`:26`, `:239-249`).
- **`LayerFor(track)`** (`:158-173`): Title→Cutscene, Victory/Defeat→Outcome, Battle/Arena→Battle,
  Raid→Wave, Overworld/Dungeon→Overworld, Village→Ambient.
- **Crossfade** (`CrossfadeTo` `:320-378`): the ONE implementation; an in-flight fade being
  superseded HARD-STOPS both sources first (the F8 2026-07-10 "two songs" supersede-storm fix,
  `:322-334`); failed-load clip guard (`:347-352`); token-superseded fades bail cleanly.
- **`AssertSingleBed`** (`:417-434`): post-fade, both-sources-audible = `FlowTrace.Fail` into
  break-log.jsonl — the runtime proof of the invariant.
- `ApplyVolumeScale` (music slider 0..1.5, guarded vs in-flight fades `:453-458`), `SetMuted`
  (`:462-467`), `Reassert` (WebGL unlock `:470-474`), `IsAnyPlaying` (`:477-478`).
- Core↔Audio enum maps by name incl. Raid (`:486-518`).

### AudioBootstrap.cs  [LIVE — the sole spawner]
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] EnsureAudioService()` (`:62-143`). Prefab path
(`Resources/DeNelleAudioService`) is still a dead branch — no prefab exists. Code path loads the
stub mixer (`Audio/GameAudioMixer` `:88-90`) and wires clips by Resources short name, **warn-on-
miss since TGVRU V** (`TryAssignClip/TryAddClip` `:175-208` — a missing clip self-reports instead
of silent silence).
- **Current clip map (`:102-136`):** Title=`title`; Village=`whispering_pines` (primary, owner
  Suno 2026-06-29) + pool add `village`; Victory=`victory`; Dungeon=`whispering_depths` (the
  "known-missing dungeon BGM" is CLOSED); Defeat=`defeat` then `Audio/Music/GameOver` (override
  when present); Battle=`siege_iron_bastion` (primary) + pool adds `battle_theme_NEW/2/3`;
  Overworld=`mainworld1_NEW` + pool add `world_theme_NEW`; Arena=`Music/echo_theme`;
  Raid=`Music/Raid/brass-rampart` (WO-453).
- `UnmuteOnce()` one-time PlayerPrefs migration `dotr-unmute-migration-v1`, reflection into
  GameStateService (`:145-173`).

### MusicTrack.cs  [LIVE]
- Audio-side `enum MusicTrack` now `None..Raid` (10 values, `:28-50`).
- `MusicTrackRegistry` (`:118-172`): owner-locked per-track mix (Title .6, Village .4, Dungeon .25,
  Battle .7, Victory .7 no-loop, Defeat .5 no-loop, Overworld .4, Arena .4). **NO Raid row**
  (`Defs` `:139-159`) **[FLAG 2]**. `AssetPath` strings are doc/log-only and stale-by-design vs
  the actual filenames.

### SfxId.cs / SfxClipLibrary.cs / ProceduralSfx.cs  [LIVE — synth-backed]
- `SfxId`: 16 ids + None (unchanged; `SfxId.cs:28-66`).
- `SfxClipLibrary` (ScriptableObject, CreateAssetMenu) — class live, **no `.asset` instance exists
  anywhere** (re-verified 2026-08-02: `find` hits only the .cs) **[FLAG 3]**.
- `ProceduralSfx.For(id)` (`ProceduralSfx.cs:54-65`): cached per-id clip — **an authored
  `Resources/Sfx/Sfx_<Id>` drop-in WINS over the synth** (`:60-62`). 12 of 16 ids HAVE authored
  WAVs (§4), so most SfxId sounds are real clips today; WaveClear/ComboSmall/ComboBig/PetFireAura
  fall to synth.

### WebGLAudioUnlock.cs  [LIVE]
Self-spawning DDOL poller; first tap/click/key → `AudioService.ResumeAfterUnlock()` → destroys
itself. Harmless off-WebGL.

### MusicSelectionPanel.cs + MusicSelectionPanelBootstrap.cs + JukeboxVM.cs  [LIVE]
- WO-162 jukebox, code-built UITK panel (`J` toggle / Esc close), auto-attached per scene with a
  PanelSettings-bearing UIDocument.
- **`JukeboxVM.cs` (new since the prior catalog revision):** pure ViewModel (strict-MVVM Silo E),
  implements `IPanelViewModel`, no UnityEngine-UI types; projects `AmbientChoicesFor` into rows
  with the "chosen==None → context default" selection logic in the VM, over an `ISource` seam
  fakeable in tests (`JukeboxVM.cs:31-44`).

---

## 3. CORE-SIDE seams (referenced, out-of-dir)

- `Core/Audio/IAudioService.cs` — `PlaySfx(clip,vol)`, `PlayMusic(Core.MusicTrack)`,
  `PlayUiClick()`; resolved via `CoreServices.Audio`. This is the ONLY audio surface
  Village/HUD/Pets call (cross-assembly rule).
- `Core/Audio/MusicTrack.cs` — Core enum with explicit indices, `Raid = 8` appended (`:10`).
- `Core/Audio/IMusicAuthority.cs` + `MusicRequest.cs` — the typed Push/Release/Current seam
  MusicDirector implements; how Village-side policy providers reach the director without
  referencing DeNelle.Audio.

---

## 4. ASSETS (verified on disk 2026-08-02)

### Mixer
- `Assets/Audio/Resources/Audio/GameAudioMixer.mixer` — **STUB** (§1 / Flag 1).

### Music — `Assets/Audio/Resources/`
Present: `title, battle, defeat, victory, siege_iron_bastion, whispering_pines,
whispering_depths, mainworld1_NEW` (.mp3) + `Music/echo_theme.mp3`, `Music/Raid/brass-rampart.mp3`,
`Music/Battle/Overworld_Battle_1/2, Overworld_Boss_Fight, Overworld_Victory` (BattleMusicManager's
own wave-state clips).
**Absent but referenced by AudioBootstrap (warn-miss, thinner pools than the comments imply)
[FLAG 4]:** `village.mp3`, `battle_theme_NEW/2/3.mp3`, `world_theme_NEW.mp3`,
`Audio/Music/GameOver.mp3`. Net: Village + Battle + Overworld each run on ONE clip (no rotation);
Defeat uses `defeat.mp3`. `battle.mp3` is on disk but no longer loaded by any bootstrap name.

### SFX — two Resources/Sfx roots (the runtime lazy-load surface)
- `Assets/_Modules/Audio/Resources/Sfx/` — 30 authored WAVs: the combat set (SwordSwing,
  SwordClash+2/3/4, SpellCast, WeaponDraw, DragonRoar, EnemyHit, EnemyDeath+2, EnemyCastCharge,
  HeroHit, Heal, TowerArrowHit, BuildingUpgrade, FootstepsWalk, UiClick) + **12 `Sfx_<Id>` WAVs**
  (ArcaneExplosion, EnemyDeath, FireExplosion, FlameArrowLaunch, Heal, LevelUp, PetAttack,
  Shockwave, TowerShot, WardDim, WardLit, WizardCast) that ProceduralSfx prefers over synth.
- `Assets/Resources/Sfx/` — Heal.mp3, LookoutHorn.wav, Spell_Impact.mp3, Swords_Clash.mp3.
- `SfxClipLibrary.asset` — **DOES NOT EXIST** (the `SfxClipLibraryBuilder` editor tool
  `Assets/Editor/Audio/SfxClipLibraryBuilder.cs` would author it at
  `_Modules/Audio/Resources/Audio/SfxClipLibrary.asset` (`:51-53`); never run to completion —
  only the Sfx_<Id> clip drops exist). Harmless today: ProceduralSfx covers the path (§2).
- `DeNelleAudioService.prefab` — does not exist; bootstrap prefab branch dead.
- Mirror tool: `Assets/Editor/Audio/SfxResourceMirror.cs` (Defenders/Audio menu) mirrors authored
  combat WAVs from `Assets/Audio/SFX/Combat` into Resources/Sfx.

---

## 5. VILLAGE-SIDE audio (DeNelle.Village — players over the Core seams)

- **`Village/Audio/GameSfx.cs`** [LIVE] — static; the DEF-183/WO-111/#51 one-shot set (tower
  fire/place, wave-start horn, LookoutHorn, sword swing/clash pool, spell cast, arrow hit, pet
  harvest, building upgrade, level up, enemy death, hero hit, weapon draw, dragon roar, build-
  denied buzz). **Pattern: `Resources.Load("Sfx/<Name>") ?? Generate<Name>()` then
  `CoreServices.Audio?.PlaySfx(clip, vol)`** (e.g. `:66-71`, `:122-145` clash variant pool behind
  `FeatureFlags.CombatFeel`; LookoutHorn is authored-only, no synth `:104-110`). Every synth is a
  seeded local `Synth(...)` (`:286-309`) — fresh-clone-safe, no binary assets needed.
- **`Village/Audio/EnemyCombatAudio.cs` / `AbilityAudioBridge.cs`** — same convention (authored
  Resources/Sfx override + synth fallback), fire through `CoreServices.Audio`.
- **`Village/Audio/BattleMusicManager.cs` / `WaveMusicController.cs` /
  `Village/World/WorldMusicDirector.cs`** [LIVE] — music POLICY PROVIDERS: they Push/Release
  layers on `IMusicAuthority`/`MusicDirector` (grep-verified callers) instead of owning sources.
  BattleMusicManager's four wave-state clips are the `Music/Battle/Overworld_*` MP3s (no MusicTrack
  enum value — pushed as raw clips via `PushClip`).

---

## 6. VENDOR — Hovl AOE prefab audio  [FLAG 6]

`Assets/Hovl Studio/HSFiles/Scripts/HS_EffectSound.cs` (verbatim vendor script, 39 lines): plays
its AudioSource clip in `Start()` and — **default `Repeating = true`, `RepeatTime = 2.0`** —
starts an `InvokeRepeating("RepeatSound", ...)` that `PlayOneShot`s forever (`:9-37`). It is NOT
pool-aware: under our VFX pooling (a) a pooled re-enable never replays a non-repeating sound
(Start fires once), and (b) a Repeating instance **keeps invoking/playing every 2s while parked
in the pool** unless disabled — repeated-one-shot spam from invisible pooled instances. AOE
prefabs (e.g. `Energy explosion`) ship with AudioSource + this script inside; everything else in
the project routes audio through `IAudioService`. Ownership decision pending — strip/disable
`HS_EffectSound` on pooled instances and route through `CoreServices.Audio`, or make the pool
drive it (documented in `docs/HOVL_STUDIO_SME.md` §2.4 + §4.g + recommendation 7).

---

## 7. REGRESSION ORACLES

- **`Assets/Editor/Regression/SfxWebglAudioRegression.cs`** [sfx-webgl, wired in
  `DataRegression.RunAll`] — the WO-682 defect-class lock: (1) every AudioClip under the two
  Resources/Sfx roots (`:39-43`) loads as an AudioClip; (2) **no Sfx clip carries a WebGL
  `platformSettingOverrides` block in its .meta** (`HasWebglOverride` `:109-116`) — the
  SwordSwing FSB-decode root ("Loading FSB failed", db-proven 2026-07-12) had the only override.
  Fails if 0 clips scanned (surface gone, `:99-100`). Marker `SFX_WEBGL_OK`. Deliberately does
  NOT assert SfxId library resolution (no asset exists; null rows are by-design no-ops — header
  `:18-23`).
- MusicDirector's `AssertSingleBed` is the RUNTIME oracle for the single-bed invariant (F8/
  break-log captured).

---

## RISK LEDGER (prioritized)

1. **The mixer is a stub** — Master-only, zero exposed params; every `SetFloat` in
   AudioService/AudioMixerBridge silently fails; only the AudioSource-direct paths (director
   scale + `.mute` snaps) actually control what you hear. SFX/UI/Voice volume sliders have no
   effect. Building the documented 5-group mixer is still owed (port-notes/audio-system.md is
   aspirational, not shipped).
2. **Raid has no `MusicTrackRegistry` row** — `AudioService.PlayMusic(Raid)` warns "No mix
   definition" and IGNORES the request (`AudioService.cs:373-378`), so the facade/IAudioService
   path can never start the raid BGM even though `brass-rampart.mp3` loads
   (`AudioBootstrap.cs:136`). The `IMusicAuthority.Push` path plays it — at def-null defaults
   **volume 1.0** (`MusicDirector.Push` `:187-190`), louder than every tuned track (max .7).
   Add a Raid row (~0.5, loop) to fix both.
3. **`SfxClipLibrary.asset` still does not exist** — the SfxId path is audible only via
   ProceduralSfx + the 12 `Sfx_<Id>` drop-ins; 4 ids (WaveClear, ComboSmall/Big, PetFireAura) are
   placeholder synth. Running `SfxClipLibraryBuilder.Build` (or authoring the asset) upgrades the
   path; per-id volume tuning (`GetVolume`) is unreachable until then.
4. **Music rotation pools are thinner than the code comments claim** — `village`,
   `battle_theme_NEW/2/3`, `world_theme_NEW`, `GameOver` are ABSENT from Resources; each of
   Village/Battle/Overworld runs a single clip and the bootstrap now warn-logs each miss (TGVRU V).
   Either restore the files or prune the load list.
5. **Jukebox PlayerPrefs persists the AUDIO-side enum ordinal** (`GetAmbientChoice` casts the
   stored int, `AudioService.cs:1078-1083`) while the Core enum froze explicit indices for
   exactly this reason — reordering/inserting into the Audio-side `MusicTrack` silently remaps
   every player's saved jukebox pick. Append-only, forever.
6. **Hovl `HS_EffectSound` on pooled AOE prefabs** — silent on pooled replays AND repeating-spam
   while parked in the pool; second audio owner outside `IAudioService`. Decide one owner
   (HOVL_STUDIO_SME rec 7) before shipping AOE-heavy content.
7. **`battle.mp3` + `defeat.mp3` legacy clips** — `battle.mp3` is unreferenced by the bootstrap
   (dead weight in the build); Defeat's intended `GameOver.mp3` upgrade never landed.
