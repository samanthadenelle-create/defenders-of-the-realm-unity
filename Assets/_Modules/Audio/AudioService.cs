// =============================================================================
// AudioService — the game's single audio surface (P0-9 in missing-components.md).
// -----------------------------------------------------------------------------
// "No audio system or Audio Mixer wiring — game ships silent." This is the fix.
//
// AudioService is the Unity analog of the React audioManager.ts: a DontDestroy-
// OnLoad MonoBehaviour singleton that owns the music AudioSources, fires SFX,
// applies the AudioMixer, and crossfades the per-scene BGM. SceneRouter's own
// header anticipates it — "an Audio/Core director listens for scene loads" —
// AudioService IS that director (it subscribes to SceneManager.sceneLoaded).
//
// PUBLIC API (the surface a settings menu / scene controllers call):
//   PlayMusic(MusicTrack)        — crossfade to a track (per audio-mix-spec §3).
//   StopMusic()                  — fade the current track out to silence.
//   PlaySfx(AudioClip)           — fire a one-shot on the SFX group.
//   PlayUiSfx / PlayVoice        — one-shots routed to the UI / Voice groups.
//   SetVolume(MixerGroup, value) — drive an exposed mixer parameter, 0..1(.5).
//   SetMuted(bool)               — master mute (audio-mix-spec §5).
//
// MIXER: the service routes through Assets/Audio/Resources/Audio/GameAudioMixer
// .mixer — five groups Master / Music / SFX / UI / Voice, each with an exposed
// volume param (MasterVol / MusicVol / SfxVol / UiVol / VoiceVol). A parallel
// Settings-menu agent drives those params directly via AudioMixer.SetFloat
// (its AudioMixerBridge resolves the SAME mixer by the SAME Resources path);
// this service applies the player's persisted GameState volumes on boot so the
// two stay in sync. Exposed-param names are the documented contract — see
// MixerParams below; they match AudioMixerBridge's MasterParam/MusicParam/SfxParam.
//
// SCENE -> TRACK map (audio-mix-spec §3): Title/ATBBattle/Onboarding -> their
// track; Village -> village; Dungeon_* -> dungeon. A missing clip is GUARDED:
// the path is wired, the gap is logged, the scene plays silent — no invented
// audio (the dungeon MP3 echoes-beneath-elarion is a known-missing asset).
//
// All fades are UniTask coroutines — never `async void` (port-spec Part 3).
// =============================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DeNelle.Audio
{
    /// <summary>
    /// The game-wide audio director — owns music playback + crossfade, fires
    /// SFX, and applies the AudioMixer. A <see cref="DontDestroyOnLoad"/>
    /// singleton; survives scene loads (audio-mix-spec.md §7, P0-9). Created
    /// automatically by <see cref="AudioBootstrap"/> — no scene wiring required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static AudioService _instance;

        /// <summary>The live service, or null before <see cref="AudioBootstrap"/> runs.</summary>
        public static AudioService Instance => _instance;

        // ── Mixer ────────────────────────────────────────────────────────────

        [Header("Mixer")]
        [Tooltip("Assets/Audio/Resources/Audio/GameAudioMixer.mixer. Auto-loaded " +
                 "from Resources by AudioBootstrap when left unassigned; assign " +
                 "explicitly on a prefab to skip the Resources lookup.")]
        [SerializeField] private AudioMixer _mixer;

        [Tooltip("The Music mixer group — the two music AudioSources route here.")]
        [SerializeField] private AudioMixerGroup _musicGroup;

        [Tooltip("The SFX mixer group — PlaySfx one-shots route here.")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Tooltip("The UI mixer group — PlayUiSfx one-shots route here.")]
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Tooltip("The Voice mixer group — PlayVoice one-shots route here.")]
        [SerializeField] private AudioMixerGroup _voiceGroup;

        // ── Music clips (wired in the inspector or by an import pass) ─────────

        [Header("Music clips — assign once imported under Assets/Audio/")]
        [Tooltip("title.mp3 — title screen + cold open. Default mix volume 0.6.")]
        [SerializeField] private AudioClip _titleClip;

        [Tooltip("village.mp3 — village exploration. Default mix volume 0.4.")]
        [SerializeField] private AudioClip _villageClip;

        [Tooltip("dungeons/echoes-beneath-elarion.mp3 — dungeon ambient. KNOWN-MISSING " +
                 "asset (see docs/port-notes/audio-system.md). Leave null until the " +
                 "MP3 lands; PlayMusic(Dungeon) guards it and logs.")]
        [SerializeField] private AudioClip _dungeonClip;

        [Tooltip("battle.mp3 — ATB combat. Default mix volume 0.7.")]
        [SerializeField] private AudioClip _battleClip;

        [Tooltip("victory.mp3 — battle-win sting. Default mix volume 0.7, no loop.")]
        [SerializeField] private AudioClip _victoryClip;

        [Tooltip("defeat.mp3 — battle-loss sting. Default mix volume 0.5, no loop.")]
        [SerializeField] private AudioClip _defeatClip;

        // ── Runtime ──────────────────────────────────────────────────────────

        // Two music sources for crossfading: while one fades out the other
        // fades in. _activeSource always points at the currently-fading-in one.
        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _activeSource;

        // One pooled SFX source set, round-robin, so concurrent one-shots do not
        // cut each other off. Routed per call to the SFX / UI / Voice groups.
        private const int SfxVoices = 8;
        private readonly List<AudioSource> _sfxVoices = new List<AudioSource>(SfxVoices);
        private int _sfxCursor;

        /// <summary>The track currently playing (or fading in). <see cref="MusicTrack.None"/> when silent.</summary>
        public MusicTrack CurrentTrack { get; private set; } = MusicTrack.None;

        /// <summary>True while a crossfade is in flight — used to short-circuit re-entrant requests.</summary>
        private bool _fading;

        // A monotonically-increasing token: each PlayMusic call bumps it, so an
        // older in-flight fade can detect it has been superseded and bail.
        private int _fadeToken;

        // True once master mute is on (audio-mix-spec §5). Mute snaps — no fade.
        private bool _muted;

        // =====================================================================
        //  Exposed-mixer-parameter contract
        // =====================================================================

        /// <summary>
        /// The exposed AudioMixer parameter names — the documented contract the
        /// Settings menu drives via <see cref="AudioMixer.SetFloat"/>. Mirrors
        /// the five mixer groups. These strings MUST match the exposed-parameter
        /// names in <c>Assets/Audio/DeNelleAudioMixer.mixer</c> exactly.
        /// </summary>
        public static class MixerParams
        {
            /// <summary>Master group exposed volume parameter.</summary>
            public const string Master = "MasterVol";
            /// <summary>Music group exposed volume parameter.</summary>
            public const string Music = "MusicVol";
            /// <summary>SFX group exposed volume parameter.</summary>
            public const string Sfx = "SfxVol";
            /// <summary>UI group exposed volume parameter.</summary>
            public const string Ui = "UiVol";
            /// <summary>Voice group exposed volume parameter.</summary>
            public const string Voice = "VoiceVol";
        }

        /// <summary>The five mixer groups a caller can address via <see cref="SetVolume"/>.</summary>
        public enum MixerGroup
        {
            /// <summary>Master — scales every other group.</summary>
            Master,
            /// <summary>Music — the BGM tracks.</summary>
            Music,
            /// <summary>SFX — gameplay one-shots (abilities, enemies, building).</summary>
            Sfx,
            /// <summary>UI — menu / button one-shots.</summary>
            Ui,
            /// <summary>Voice — NPC speech / voice-over.</summary>
            Voice,
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            BuildAudioSources();
            ResolveMixerGroups();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            // One-time mixer check, deferred to Start so AudioBootstrap's
            // post-Awake SetMixer call has had its chance — Awake runs the
            // moment AddComponent returns, before the bootstrap can hand over
            // the Resources-loaded mixer.
            if (_mixer == null)
                Debug.LogWarning(
                    "[AudioService] No AudioMixer assigned — music/SFX play on the " +
                    "default output and SetVolume falls back to per-source volume. " +
                    "Ship Assets/Audio/Resources/Audio/GameAudioMixer.mixer for full " +
                    "mix control.");

            // Apply the player's persisted volume + mute settings on boot. The
            // Settings menu can override these live; this just seeds the mixer
            // from GameState so launch audio matches the saved preference.
            ApplyPersistedSettings();

            // Pick up whatever scene loaded before the service existed (the
            // bootstrap may have created us after the first scene's sceneLoaded
            // already fired).
            HandleSceneMusic(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Builds the two music AudioSources + the SFX voice pool as children of
        /// this GameObject. All are 2D (spatialBlend 0) — music + UI are non-
        /// positional. Idempotent: a second call is a no-op.
        /// </summary>
        private void BuildAudioSources()
        {
            if (_musicA != null) return;

            _musicA = CreateChildSource("Music_A", _musicGroup, loop: true);
            _musicB = CreateChildSource("Music_B", _musicGroup, loop: true);
            _activeSource = _musicA;

            for (int i = 0; i < SfxVoices; i++)
                _sfxVoices.Add(CreateChildSource($"Sfx_{i}", _sfxGroup, loop: false));
        }

        private AudioSource CreateChildSource(string srcName, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(srcName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;          // 2D — music + UI are non-positional.
            src.volume = 0f;
            if (group != null) src.outputAudioMixerGroup = group;
            return src;
        }

        // =====================================================================
        //  Mixer wiring
        // =====================================================================

        /// <summary>
        /// Assigns the mixer + the four routed groups. Public so
        /// <see cref="AudioBootstrap"/> can hand the service a Resources-loaded
        /// mixer before <see cref="Awake"/>'s source build. Re-resolves the
        /// group routing on every existing AudioSource.
        /// </summary>
        public void SetMixer(AudioMixer mixer)
        {
            _mixer = mixer;
            ResolveMixerGroups();
        }

        /// <summary>
        /// Resolves the five mixer groups off <see cref="_mixer"/> by name and
        /// re-routes the live AudioSources. Safe to call repeatedly. When the
        /// mixer is unassigned the service still works — sources route to the
        /// default output and <see cref="SetVolume"/> per-source-clamps instead.
        /// </summary>
        private void ResolveMixerGroups()
        {
            // No mixer yet — AudioBootstrap may still hand one over via SetMixer
            // right after Awake. The one-time "no mixer" warning is deferred to
            // Start() so it only fires when no mixer ever arrives.
            if (_mixer == null)
                return;

            // FindMatchingGroups returns every group whose path ends in the
            // name; the mixer has exactly one of each, so [0] is correct.
            _musicGroup  = FirstGroup("Music")  ?? _musicGroup;
            _sfxGroup    = FirstGroup("SFX")    ?? _sfxGroup;
            _uiGroup     = FirstGroup("UI")     ?? _uiGroup;
            _voiceGroup  = FirstGroup("Voice")  ?? _voiceGroup;

            if (_musicA != null) _musicA.outputAudioMixerGroup = _musicGroup;
            if (_musicB != null) _musicB.outputAudioMixerGroup = _musicGroup;
            foreach (var v in _sfxVoices)
                if (v != null) v.outputAudioMixerGroup = _sfxGroup;
        }

        private AudioMixerGroup FirstGroup(string groupName)
        {
            var matches = _mixer.FindMatchingGroups(groupName);
            return (matches != null && matches.Length > 0) ? matches[0] : null;
        }

        // =====================================================================
        //  PUBLIC API — music
        // =====================================================================

        /// <summary>
        /// Crossfades the BGM to <paramref name="track"/> using that track's
        /// owner-locked fade durations (audio-mix-spec.md §2/§3). Requesting the
        /// track that is already playing is a no-op (the audioManager's
        /// "currentTrack === track" short-circuit). <see cref="MusicTrack.None"/>
        /// fades out to silence.
        /// </summary>
        public void PlayMusic(MusicTrack track)
        {
            if (track == CurrentTrack && !_fading)
                return; // already playing — no thrash.

            if (track == MusicTrack.None)
            {
                StopMusic();
                return;
            }

            MusicTrackDef def = MusicTrackRegistry.Get(track);
            if (def == null)
            {
                Debug.LogWarning($"[AudioService] No mix definition for track '{track}' — ignored.");
                return;
            }

            AudioClip clip = ClipFor(track);
            if (clip == null)
            {
                // GUARDED missing clip — wire the path, log the gap, play silent.
                // No invented audio. The dungeon track's MP3 is known-missing.
                Debug.LogWarning(
                    $"[AudioService] Music track '{track}' has no clip — expected at " +
                    $"'{def.AssetPath}'. Import the MP3 and assign it on the AudioService " +
                    "(or via Resources). Scene plays silent; no audio invented.");
                CurrentTrack = track; // record intent so a later assign + re-request works.
                return;
            }

            CrossfadeTo(track, def, clip).Forget();
        }

        /// <summary>Fades the current music out to silence over its fade-out duration.</summary>
        public void StopMusic()
        {
            if (CurrentTrack == MusicTrack.None && !_fading) return;
            MusicTrackDef def = MusicTrackRegistry.Get(CurrentTrack);
            float fadeOut = def?.FadeOutSeconds ?? 1.0f;
            CurrentTrack = MusicTrack.None;
            FadeOutAll(fadeOut).Forget();
        }

        /// <summary>
        /// The crossfade coroutine — fades the active source out while the idle
        /// source fades in on the new clip. Each call bumps <see cref="_fadeToken"/>
        /// so a superseded fade bails cleanly.
        /// </summary>
        private async UniTaskVoid CrossfadeTo(MusicTrack track, MusicTrackDef def, AudioClip clip)
        {
            int token = ++_fadeToken;
            _fading = true;
            CurrentTrack = track;

            AudioSource fadeIn = (_activeSource == _musicA) ? _musicB : _musicA;
            AudioSource fadeOut = _activeSource;
            _activeSource = fadeIn;

            fadeIn.clip = clip;
            fadeIn.loop = def.Loop;
            fadeIn.volume = 0f;
            fadeIn.Play();

            // The fade target is always the track's owner-locked default volume.
            // Mute is a MIXER concern (the Master param) or, in the no-mixer
            // fallback, the AudioSource.mute flag — never the source .volume —
            // so an un-mute restores audio without re-triggering a crossfade.
            float target = def.DefaultVolume;
            float fadeSeconds = Mathf.Max(0.01f, def.FadeInSeconds);
            float startOut = fadeOut.volume;
            float t = 0f;

            while (t < fadeSeconds)
            {
                if (token != _fadeToken) return; // superseded by a newer request.
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeSeconds);
                fadeIn.volume = Mathf.Lerp(0f, target, k);
                fadeOut.volume = Mathf.Lerp(startOut, 0f, k);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (token != _fadeToken) return;

            fadeIn.volume = target;
            fadeOut.volume = 0f;
            fadeOut.Stop();
            _fading = false;
        }

        /// <summary>Fades both music sources to silence (used by <see cref="StopMusic"/>).</summary>
        private async UniTaskVoid FadeOutAll(float fadeSeconds)
        {
            int token = ++_fadeToken;
            _fading = true;

            float seconds = Mathf.Max(0.01f, fadeSeconds);
            float startA = _musicA != null ? _musicA.volume : 0f;
            float startB = _musicB != null ? _musicB.volume : 0f;
            float t = 0f;

            while (t < seconds)
            {
                if (token != _fadeToken) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                if (_musicA != null) _musicA.volume = Mathf.Lerp(startA, 0f, k);
                if (_musicB != null) _musicB.volume = Mathf.Lerp(startB, 0f, k);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (token != _fadeToken) return;

            if (_musicA != null) { _musicA.volume = 0f; _musicA.Stop(); }
            if (_musicB != null) { _musicB.volume = 0f; _musicB.Stop(); }
            _fading = false;
        }

        /// <summary>The inspector-assigned clip for a track, or null when not yet imported.</summary>
        private AudioClip ClipFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Title:   return _titleClip;
                case MusicTrack.Village: return _villageClip;
                case MusicTrack.Dungeon: return _dungeonClip;
                case MusicTrack.Battle:  return _battleClip;
                case MusicTrack.Victory: return _victoryClip;
                case MusicTrack.Defeat:  return _defeatClip;
                default:                 return null;
            }
        }

        /// <summary>
        /// Assigns a music clip at runtime — the seam an import pass or the
        /// integrator uses once an MP3 lands without touching the inspector. If
        /// the assigned track is the one currently intended but silent (a guarded
        /// missing clip), the music is (re)started.
        /// </summary>
        public void SetMusicClip(MusicTrack track, AudioClip clip)
        {
            switch (track)
            {
                case MusicTrack.Title:   _titleClip = clip;   break;
                case MusicTrack.Village: _villageClip = clip; break;
                case MusicTrack.Dungeon: _dungeonClip = clip; break;
                case MusicTrack.Battle:  _battleClip = clip;  break;
                case MusicTrack.Victory: _victoryClip = clip; break;
                case MusicTrack.Defeat:  _defeatClip = clip;  break;
            }

            // If this track was requested but silent for want of a clip, start it.
            if (clip != null && track == CurrentTrack && !IsAnyMusicPlaying())
            {
                CurrentTrack = MusicTrack.None; // clear the short-circuit guard.
                PlayMusic(track);
            }
        }

        private bool IsAnyMusicPlaying()
        {
            return (_musicA != null && _musicA.isPlaying) ||
                   (_musicB != null && _musicB.isPlaying);
        }

        // =====================================================================
        //  PUBLIC API — SFX
        // =====================================================================

        /// <summary>
        /// Fires a one-shot SFX on the SFX mixer group. Round-robins a small
        /// voice pool so concurrent shots do not cut each other off. A null clip
        /// is a guarded no-op. <paramref name="volume"/> is 0..1, pre-mixer.
        /// </summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            PlayOneShotOn(_sfxGroup, clip, volume);
        }

        /// <summary>Fires a one-shot routed to the UI mixer group (menu / button blips).</summary>
        public void PlayUiSfx(AudioClip clip, float volume = 1f)
        {
            PlayOneShotOn(_uiGroup, clip, volume);
        }

        /// <summary>Fires a one-shot routed to the Voice mixer group (NPC speech / VO).</summary>
        public void PlayVoice(AudioClip clip, float volume = 1f)
        {
            PlayOneShotOn(_voiceGroup, clip, volume);
        }

        private void PlayOneShotOn(AudioMixerGroup group, AudioClip clip, float volume)
        {
            if (clip == null) return; // guarded — a missing SFX is silent, not an error.
            if (_sfxVoices.Count == 0) return;

            AudioSource voice = _sfxVoices[_sfxCursor];
            _sfxCursor = (_sfxCursor + 1) % _sfxVoices.Count;

            // Route this voice to the requested group for this shot.
            voice.outputAudioMixerGroup = group ?? _sfxGroup;
            voice.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // =====================================================================
        //  PUBLIC API — volume + mute
        // =====================================================================

        /// <summary>
        /// Sets a mixer group's volume from a linear 0..1 slider value (Master
        /// accepts up to 1.5 — audio-mix-spec §2 lets the master PUSH past 1.0).
        /// Converts the linear value to the mixer's decibel scale and writes the
        /// exposed parameter. When no mixer is assigned, falls back to scaling
        /// the music AudioSources directly so the game is never stuck loud.
        /// </summary>
        public void SetVolume(MixerGroup group, float linear01)
        {
            float max = (group == MixerGroup.Master) ? 1.5f : 1f;
            float v = Mathf.Clamp(linear01, 0f, max);

            if (_mixer != null)
            {
                _mixer.SetFloat(ParamNameFor(group), LinearToDecibels(v));
                return;
            }

            // No-mixer fallback — only Master / Music can be honoured per-source.
            if (group == MixerGroup.Master || group == MixerGroup.Music)
            {
                if (_activeSource != null && !_fading && !_muted)
                {
                    MusicTrackDef def = MusicTrackRegistry.Get(CurrentTrack);
                    float baseVol = def?.DefaultVolume ?? 1f;
                    _activeSource.volume = Mathf.Clamp01(baseVol * v);
                }
            }
        }

        /// <summary>
        /// Reads back a mixer group's volume as a linear 0..1(.5) value, or
        /// returns <paramref name="fallback"/> when no mixer is assigned / the
        /// parameter is not exposed.
        /// </summary>
        public float GetVolume(MixerGroup group, float fallback = 1f)
        {
            if (_mixer != null && _mixer.GetFloat(ParamNameFor(group), out float db))
                return DecibelsToLinear(db);
            return fallback;
        }

        /// <summary>
        /// Master mute toggle (audio-mix-spec.md §5). Mute SNAPS — ignores fade
        /// durations — and is applied on the Master mixer parameter so every
        /// group is silenced at once. Un-muting restores the Master parameter to
        /// 0 dB (the Settings menu's own slider then re-asserts its value).
        /// </summary>
        public void SetMuted(bool muted)
        {
            _muted = muted;
            if (_mixer != null)
            {
                _mixer.SetFloat(MixerParams.Master, muted ? -80f : 0f);
            }
            else
            {
                // No-mixer fallback — snap every source's mute flag.
                if (_musicA != null) _musicA.mute = muted;
                if (_musicB != null) _musicB.mute = muted;
                foreach (var v in _sfxVoices)
                    if (v != null) v.mute = muted;
            }
        }

        /// <summary>True when master mute is engaged.</summary>
        public bool IsMuted => _muted;

        private static string ParamNameFor(MixerGroup group)
        {
            switch (group)
            {
                case MixerGroup.Master: return MixerParams.Master;
                case MixerGroup.Music:  return MixerParams.Music;
                case MixerGroup.Sfx:    return MixerParams.Sfx;
                case MixerGroup.Ui:     return MixerParams.Ui;
                case MixerGroup.Voice:  return MixerParams.Voice;
                default:                return MixerParams.Master;
            }
        }

        /// <summary>
        /// Linear-slider → mixer decibels. A linear 1.0 maps to 0 dB (unity gain);
        /// 0 maps to -80 dB (the mixer's silence floor). Logarithmic in between
        /// so the slider feels perceptually even.
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Clamp(Mathf.Log10(linear) * 20f, -80f, 10f);
        }

        /// <summary>Mixer decibels → linear-slider value (inverse of <see cref="LinearToDecibels"/>).</summary>
        public static float DecibelsToLinear(float decibels)
        {
            if (decibels <= -80f) return 0f;
            return Mathf.Pow(10f, decibels / 20f);
        }

        /// <summary>
        /// Seeds the mixer from the player's persisted <see cref="GameState"/>
        /// audio settings on boot (MusicVolume / SfxVolume are 0..100; Muted is a
        /// bool). The Settings menu can override these live afterwards. Master is
        /// left at unity (0 dB) — the Settings menu owns the master slider.
        /// </summary>
        public void ApplyPersistedSettings()
        {
            GameState s = GameStateService.Instance != null
                ? GameStateService.Instance.State
                : null;
            if (s == null) return;

            // GameState volumes are 0..100 (React parity); convert to 0..1.
            SetVolume(MixerGroup.Music, Mathf.Clamp01(s.MusicVolume / 100f));
            SetVolume(MixerGroup.Sfx, Mathf.Clamp01(s.SfxVolume / 100f));
            // No persisted UI/Voice volume — leave at the mixer default (0 dB).
            SetMuted(s.Muted);
        }

        // =====================================================================
        //  Scene-music director (SceneRouter's "audio director")
        // =====================================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneMusic(scene.name);
        }

        /// <summary>
        /// Maps a loaded scene name to its BGM track and crossfades to it
        /// (audio-mix-spec.md §3). The dungeon scene names are prefixed
        /// <c>Dungeon_</c> (SceneRouter.Dungeon); everything else matches the
        /// canonical scene constants. An unrecognised scene keeps the current
        /// track rather than cutting to silence.
        /// </summary>
        public void HandleSceneMusic(string sceneName)
        {
            MusicTrack track = TrackForScene(sceneName);
            if (track == MusicTrack.None) return; // unknown scene — leave music alone.
            PlayMusic(track);
        }

        /// <summary>
        /// The scene → music-track map. Public + static so a scene controller
        /// can ask "what track is mine?" without going through the service.
        /// </summary>
        public static MusicTrack TrackForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return MusicTrack.None;

            // Dungeon scenes are "Dungeon_<id>" (SceneRouter.Dungeon).
            if (sceneName.StartsWith("Dungeon_", StringComparison.Ordinal))
                return MusicTrack.Dungeon;

            if (sceneName == SceneRouter.Title)    return MusicTrack.Title;
            if (sceneName == SceneRouter.Village)  return MusicTrack.Village;
            if (sceneName == SceneRouter.ATBBattle) return MusicTrack.Battle;

            // The Onboarding scene (studio bumper / cold open), if it is a
            // separate scene, shares the title track per audio-mix-spec §2.
            if (sceneName == "Onboarding" || sceneName == "Splash")
                return MusicTrack.Title;

            return MusicTrack.None; // unrecognised — caller leaves music as-is.
        }
    }
}
