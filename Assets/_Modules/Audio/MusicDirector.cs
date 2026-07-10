// =============================================================================
// MusicDirector — THE single owner of the music AudioSources (2026-07-09).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Audio   Namespace: DeNelle.Audio.
//
// THE INVARIANT (MUSIC_AUTHORITY_DESIGN 2026-07-09, owner-confirmed):
//   Exactly ONE class owns music AudioSources. Every other system REQUESTS a
//   track through a typed contract; none can play one. Two beds are impossible by
//   construction — there is one A/B playback pair, and it always sounds exactly
//   one resolved track (the highest active layer).
//
// Before this class the music could double: AudioService, BattleMusicManager and
// WaveMusicController each owned their own crossfading source pair and ran their
// own fades, so nothing structurally forbade two beds (F8 2026-07-09 "two songs
// at once" in Main_Castle_Overworld). The fix is structural: those players become
// policy providers that Push/Release a LAYER here; this class owns the only pair.
//
// RESOLUTION MODEL — priority-layer stack (owner-selected):
//   • A dense per-layer table holds the active request per MusicLayer.
//   • top = the highest layer whose entry is active. On every Push/Release we
//     recompute top; if top's clip differs from what is sounding, we crossfade
//     the ONE pair to it. No layer active → fade to silence.
//   • Auto-fallback: Release(Battle) re-resolves to Wave/Overworld/Ambient with
//     no caller having to "restore ambient" — the whole "forgot to restore" bug
//     class is deleted.
//   • Idempotent: Push of the already-sounding top track is a no-op.
//
// FEEL: preserves the existing per-track fade durations (from MusicTrackRegistry)
// for enum pushes and the 1.5s battle crossfade for BattleMusicManager's clips,
// and keeps the Music mixer-group routing (AudioService hands us the group).
//
// INSTRUMENTATION (§12): every Push/Release/crossfade emits a [Flow:Audio] Step
// naming layer/track/top/outgoing; the moment the pair ever settles with BOTH
// sources audible, FlowTrace.Fail fires — the runtime proof of the invariant that
// must never trip.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Audio;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Audio;

namespace DeNelle.Audio
{
    /// <summary>
    /// The single owner of the music playback pair — a priority-layer stack that
    /// always sounds the highest active layer. Requesters Push/Release a
    /// <see cref="MusicLayer"/>; they never touch an AudioSource. Implements the
    /// Core <see cref="IMusicAuthority"/> seam. Created + owned by
    /// <see cref="AudioService"/>; its sources live under the AudioService GO.
    /// </summary>
    public sealed class MusicDirector : IMusicAuthority
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static MusicDirector _instance;

        /// <summary>The live director, or null before <see cref="AudioService"/> builds it.</summary>
        public static MusicDirector Instance => _instance;

        // ── The ONLY music AudioSource pair (A/B crossfade) ──────────────────
        private readonly AudioSource _musicA;
        private readonly AudioSource _musicB;
        private AudioSource _activeSource;          // the one currently faded-in
        private AudioMixerGroup _musicGroup;

        // Clip resolver — AudioService owns the serialized clips + rotation pools,
        // so it resolves a MusicTrack → AudioClip (pools rotate per call). The
        // director owns PLAYBACK, AudioService owns CLIP LOOKUP (spec §3).
        private readonly Func<MusicTrack, AudioClip> _clipResolver;

        // ── Crossfade state ──────────────────────────────────────────────────
        private bool _fading;
        private int _fadeToken;                     // supersede an in-flight fade
        private bool _muted;
        private float _volumeScale = 1f;            // music-volume slider (0..1.5)

        // ── Per-layer request table (dense, indexed by MusicLayer) ───────────
        // A layer's cell is a small playback record. The spec's "MusicTrack[] by
        // layer" is generalised to (clip + volume + loop + fade + track) because
        // BattleMusicManager's four wave-state clips are Resources-loaded and have
        // NO MusicTrack enum value — a bare enum array can't represent them. The
        // invariant is unchanged: one owner, one pair, highest-active-layer wins.
        private struct LayerEntry
        {
            public bool Active;
            public AudioClip Clip;
            public float Volume;
            public bool Loop;
            public float FadeIn;
            public string Name;
            public MusicTrack Track;   // for Current reporting; None for arbitrary clips
        }

        private const int LayerCount = 7;           // MusicLayer 0..6 (Cutscene = 6)
        private readonly LayerEntry[] _layers = new LayerEntry[LayerCount];

        // ── Currently-sounding bed ───────────────────────────────────────────
        private MusicLayer _currentLayer = MusicLayer.None;
        private AudioClip _currentClip;
        private float _currentVolume;
        private string _currentName;
        private MusicTrack _currentTrack = MusicTrack.None;

        private const float DefaultFadeSeconds = 1.5f;

        // =====================================================================
        //  Construction (AudioService owns us)
        // =====================================================================

        private MusicDirector(Transform host, AudioMixerGroup musicGroup,
                              Func<MusicTrack, AudioClip> clipResolver)
        {
            _musicGroup = musicGroup;
            _clipResolver = clipResolver;
            _musicA = CreateSource(host, "Music_A");
            _musicB = CreateSource(host, "Music_B");
            _activeSource = _musicA;
        }

        /// <summary>
        /// Returns the singleton, creating it (with its source pair parented under
        /// <paramref name="host"/>) on first call. AudioService is a DDOL singleton
        /// that dedups itself in Awake before building us, so this creates once.
        /// </summary>
        public static MusicDirector GetOrCreate(Transform host, AudioMixerGroup musicGroup,
                                                Func<MusicTrack, AudioClip> clipResolver)
        {
            if (_instance != null) return _instance;
            _instance = new MusicDirector(host, musicGroup, clipResolver);
            FlowTrace.Step("Audio", "MusicDirector created — single music owner online (one A/B pair).");
            return _instance;
        }

        private AudioSource CreateSource(Transform host, string srcName)
        {
            var go = new GameObject(srcName);
            if (host != null) go.transform.SetParent(host, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;      // 2D — music is non-positional
            src.volume = 0f;
            if (_musicGroup != null) src.outputAudioMixerGroup = _musicGroup;
            return src;
        }

        // =====================================================================
        //  Layer mapping (Audio-side MusicTrack → MusicLayer)
        // =====================================================================

        /// <summary>
        /// The layer a track belongs to (MUSIC_AUTHORITY_DESIGN "LayerFor" table).
        /// Used by AudioService's facade (PlayMusic) so a Battle track lands on the
        /// Battle layer, Village on Ambient, etc.
        /// </summary>
        public static MusicLayer LayerFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Title:     return MusicLayer.Cutscene;
                case MusicTrack.Victory:   return MusicLayer.Outcome;
                case MusicTrack.Defeat:    return MusicLayer.Outcome;
                case MusicTrack.Battle:    return MusicLayer.Battle;
                case MusicTrack.Arena:     return MusicLayer.Battle;
                case MusicTrack.Raid:      return MusicLayer.Wave;
                case MusicTrack.Overworld: return MusicLayer.Overworld;
                case MusicTrack.Dungeon:   return MusicLayer.Overworld;
                case MusicTrack.Village:   return MusicLayer.Ambient;
                default:                   return MusicLayer.None;
            }
        }

        // =====================================================================
        //  PUBLIC API — Push / Release (the one seam)
        // =====================================================================

        /// <summary>
        /// Convenience Push for an enum track: resolves the clip (via AudioService's
        /// rotation-aware resolver) + the owner-locked mix (MusicTrackRegistry) and
        /// sets that layer. A missing clip is a guarded no-op (leaves the layer as
        /// it was — no invented audio, no silent cut).
        /// </summary>
        public void Push(MusicLayer layer, MusicTrack track)
        {
            MusicTrackDef def = MusicTrackRegistry.Get(track);
            float vol = def != null ? def.DefaultVolume : 1f;
            bool loop = def == null || def.Loop;
            float fade = def != null ? Mathf.Max(0.01f, def.FadeInSeconds) : DefaultFadeSeconds;
            AudioClip clip = _clipResolver != null ? _clipResolver(track) : null;
            if (clip == null)
            {
                FlowTrace.Warn("Audio", $"Push layer={layer} track={track} — no clip resolved; layer left unchanged (silent).");
                return;
            }
            PushClip(layer, clip, vol, loop, fade, track.ToString(), track);
        }

        /// <summary>
        /// The low-level Push: sets/replaces the request on <paramref name="layer"/>
        /// with an explicit clip + mix, then re-resolves the top. A null clip clears
        /// the layer (== Release) — the seam a policy provider with a not-yet-imported
        /// clip uses harmlessly. Idempotent when the top does not change.
        /// </summary>
        public void PushClip(MusicLayer layer, AudioClip clip, float volume, bool loop,
                             float fadeIn, string name, MusicTrack track = MusicTrack.None)
        {
            int i = (int)layer;
            if (i <= 0 || i >= LayerCount)
            {
                FlowTrace.Warn("Audio", $"PushClip ignored — invalid layer {layer}.");
                return;
            }
            if (clip == null)
            {
                Release(layer);
                return;
            }

            _layers[i] = new LayerEntry
            {
                Active = true,
                Clip = clip,
                Volume = volume,
                Loop = loop,
                FadeIn = Mathf.Max(0.01f, fadeIn),
                Name = name,
                Track = track,
            };
            FlowTrace.Step("Audio", $"Push layer={layer} track='{name}' clip='{clip.name}' vol={volume:F2}");
            Resolve(fadeIn);
        }

        /// <summary>
        /// Clears <paramref name="layer"/> and re-resolves the top (auto-fallback to
        /// the next-highest active layer, or silence). Idempotent on an inactive layer.
        /// </summary>
        public void Release(MusicLayer layer)
        {
            int i = (int)layer;
            if (i <= 0 || i >= LayerCount) return;
            bool was = _layers[i].Active;
            _layers[i].Active = false;
            _layers[i].Clip = null;
            _layers[i].Track = MusicTrack.None;
            FlowTrace.Step("Audio", $"Release layer={layer}{(was ? "" : " (was already inactive)")}");
            if (was) Resolve(-1f);
        }

        // ── IMusicAuthority (Core seam) ──────────────────────────────────────

        void IMusicAuthority.Push(MusicRequest req)
        {
            MusicTrack audio = ToAudioTrack(req.Track);
            MusicLayer layer = req.Layer != MusicLayer.None ? req.Layer : LayerFor(audio);
            Push(layer, audio);
        }

        void IMusicAuthority.Release(MusicLayer layer) => Release(layer);

        /// <summary>The Core-side key of the bed currently sounding (best-effort; the
        /// default value when silent or when an arbitrary clip has no enum key).</summary>
        DeNelle.Core.Audio.MusicTrack IMusicAuthority.Current => ToCoreTrack(_currentTrack);

        // =====================================================================
        //  Resolution — sound the highest active layer
        // =====================================================================

        private void Resolve(float fadeOverride)
        {
            int top = -1;
            for (int i = LayerCount - 1; i >= 1; i--)
            {
                if (_layers[i].Active) { top = i; break; }
            }

            if (top < 0)
            {
                // No layer active — fade the one pair to silence.
                if (_currentLayer != MusicLayer.None || IsAnyPlaying)
                {
                    FlowTrace.Step("Audio", $"Resolve top=None → fade to silence (was '{_currentName}').");
                    FadeOutAll(DefaultFadeSeconds).Forget();
                }
                _currentLayer = MusicLayer.None;
                _currentClip = null;
                _currentName = null;
                _currentTrack = MusicTrack.None;
                _currentVolume = 0f;
                return;
            }

            LayerEntry e = _layers[top];
            var topLayer = (MusicLayer)top;

            // Idempotent: the top clip is already the one sounding on the pair.
            if (ReferenceEquals(_currentClip, e.Clip) && _currentLayer == topLayer && IsActiveAudible())
            {
                FlowTrace.Step("Audio", $"Resolve top={topLayer} track='{e.Name}' — already sounding, no-op.");
                return;
            }

            float fade = fadeOverride > 0f ? fadeOverride : e.FadeIn;
            FlowTrace.Step("Audio",
                $"Resolve top={topLayer} track='{e.Name}' outgoing='{_currentName ?? "(silence)"}' → crossfade {fade:F2}s.");

            _currentLayer = topLayer;
            _currentClip = e.Clip;
            _currentVolume = e.Volume;
            _currentName = e.Name;
            _currentTrack = e.Track;
            CrossfadeTo(e.Clip, e.Volume, e.Loop, fade).Forget();
        }

        // =====================================================================
        //  Crossfade (the ONE implementation — moved off AudioService)
        // =====================================================================

        private async UniTaskVoid CrossfadeTo(AudioClip clip, float targetVol, bool loop, float fadeSeconds)
        {
            int token = ++_fadeToken;
            // Owner model "terminate current before starting next" (F8 2026-07-10 "music duplicating —
            // impossible for a singleton"): if a fade is STILL IN FLIGHT, a rapid re-resolve (e.g.
            // BattleMusicManager's 0.5s Combat<->Intense flap under this 1.5s crossfade) supersedes it —
            // and the old fade's coroutine bails on the token check WITHOUT Stop()'ing its source, so both
            // A and B keep looping = two beds. Hard-terminate BOTH sources first so the next track fades in
            // from silence: exactly one bed at any instant. A SETTLED transition (_fading already false) is
            // untouched and keeps its smooth crossfade.
            if (_fading)
            {
                if (_musicA != null) { _musicA.volume = 0f; _musicA.Stop(); }
                if (_musicB != null) { _musicB.volume = 0f; _musicB.Stop(); }
            }
            _fading = true;

            AudioSource fadeIn = (_activeSource == _musicA) ? _musicB : _musicA;
            AudioSource fadeOut = _activeSource;
            _activeSource = fadeIn;

            fadeIn.clip = clip;
            fadeIn.loop = loop;
            fadeIn.volume = 0f;

            // Guard a clip object whose underlying file failed to load (FMOD
            // "file not found" otherwise crashes on Play).
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                FlowTrace.Warn("Audio", $"Crossfade skipped — clip '{clip.name}' failed to load (file missing from build?).");
                _fading = false;
                return;
            }

            fadeIn.Play();

            float target = _muted ? 0f : Mathf.Clamp01(targetVol * _volumeScale);
            float secs = Mathf.Max(0.01f, fadeSeconds);
            float startOut = fadeOut != null ? fadeOut.volume : 0f;
            float t = 0f;

            while (t < secs)
            {
                if (token != _fadeToken) return;   // superseded by a newer resolve
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / secs);
                fadeIn.volume = Mathf.Lerp(0f, target, k);
                if (fadeOut != null) fadeOut.volume = Mathf.Lerp(startOut, 0f, k);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (token != _fadeToken) return;

            fadeIn.volume = target;
            if (fadeOut != null) { fadeOut.volume = 0f; fadeOut.Stop(); }
            _fading = false;

            AssertSingleBed("post-crossfade");
        }

        private async UniTaskVoid FadeOutAll(float fadeSeconds)
        {
            int token = ++_fadeToken;
            _fading = true;

            float secs = Mathf.Max(0.01f, fadeSeconds);
            float startA = _musicA != null ? _musicA.volume : 0f;
            float startB = _musicB != null ? _musicB.volume : 0f;
            float t = 0f;

            while (t < secs)
            {
                if (token != _fadeToken) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / secs);
                if (_musicA != null) _musicA.volume = Mathf.Lerp(startA, 0f, k);
                if (_musicB != null) _musicB.volume = Mathf.Lerp(startB, 0f, k);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (token != _fadeToken) return;

            if (_musicA != null) { _musicA.volume = 0f; _musicA.Stop(); }
            if (_musicB != null) { _musicB.volume = 0f; _musicB.Stop(); }
            _fading = false;
        }

        // =====================================================================
        //  The invariant proof — no two beds, ever
        // =====================================================================

        /// <summary>
        /// After a fade settles exactly one source may be audible. If BOTH are, the
        /// single-bed invariant has been violated — FlowTrace.Fail (LogError → lands
        /// in break-log.jsonl) so a headless/felt run PROVES the single bed. This
        /// must never fire; it is the runtime assertion of the whole design.
        /// </summary>
        private void AssertSingleBed(string where)
        {
            bool aOn = _musicA != null && _musicA.isPlaying && _musicA.volume > 0.02f;
            bool bOn = _musicB != null && _musicB.isPlaying && _musicB.volume > 0.02f;
            if (aOn && bOn)
            {
                FlowTrace.Fail("Audio",
                    $"OVERLAP INVARIANT VIOLATED at {where}: BOTH music sources audible " +
                    $"(A='{(_musicA.clip != null ? _musicA.clip.name : "null")}' v={_musicA.volume:F2}, " +
                    $"B='{(_musicB.clip != null ? _musicB.clip.name : "null")}' v={_musicB.volume:F2}). " +
                    "Two beds must be impossible by construction — investigate the resolve/token path.");
            }
            else
            {
                FlowTrace.Step("Audio",
                    $"single-bed OK at {where}: sounding='{_currentName ?? "(silence)"}' layer={_currentLayer}.");
            }
        }

        // =====================================================================
        //  Volume / mute / mixer wiring (called by AudioService)
        // =====================================================================

        /// <summary>Re-routes the pair to the (re)resolved Music mixer group.</summary>
        public void SetMusicGroup(AudioMixerGroup group)
        {
            _musicGroup = group;
            if (_musicA != null) _musicA.outputAudioMixerGroup = group;
            if (_musicB != null) _musicB.outputAudioMixerGroup = group;
        }

        /// <summary>
        /// Applies the music-volume slider (0..1.5). Scales the active source live
        /// (guarded so it never fights an in-flight crossfade) and persists so the
        /// next crossfade honours it. Mirrors AudioService's prior music-scaling.
        /// </summary>
        public void ApplyVolumeScale(float scale)
        {
            _volumeScale = Mathf.Max(0f, scale);
            if (_activeSource != null && !_fading)
                _activeSource.volume = _muted ? 0f : Mathf.Clamp01(_currentVolume * _volumeScale);
        }

        /// <summary>Snaps mute on both music sources (mute is a MIXER concern too, but
        /// the .mute flag guarantees silence even when Music-group routing failed).</summary>
        public void SetMuted(bool muted)
        {
            _muted = muted;
            if (_musicA != null) _musicA.mute = muted;
            if (_musicB != null) _musicB.mute = muted;
        }

        /// <summary>Re-plays the current top from scratch (WebGL gesture-unlock resume).</summary>
        public void Reassert()
        {
            _currentClip = null;   // break idempotency so the top re-crossfades
            Resolve(-1f);
        }

        /// <summary>True while either music source is playing.</summary>
        public bool IsAnyPlaying =>
            (_musicA != null && _musicA.isPlaying) || (_musicB != null && _musicB.isPlaying);

        private bool IsActiveAudible() => _activeSource != null && _activeSource.isPlaying;

        // =====================================================================
        //  Core ↔ Audio MusicTrack mapping (the two enums differ in order)
        // =====================================================================

        private static MusicTrack ToAudioTrack(DeNelle.Core.Audio.MusicTrack core)
        {
            switch (core)
            {
                case DeNelle.Core.Audio.MusicTrack.Village:   return MusicTrack.Village;
                case DeNelle.Core.Audio.MusicTrack.Battle:    return MusicTrack.Battle;
                case DeNelle.Core.Audio.MusicTrack.Victory:   return MusicTrack.Victory;
                case DeNelle.Core.Audio.MusicTrack.Dungeon:   return MusicTrack.Dungeon;
                case DeNelle.Core.Audio.MusicTrack.Overworld: return MusicTrack.Overworld;
                case DeNelle.Core.Audio.MusicTrack.Defeat:    return MusicTrack.Defeat;
                case DeNelle.Core.Audio.MusicTrack.Title:     return MusicTrack.Title;
                case DeNelle.Core.Audio.MusicTrack.Arena:     return MusicTrack.Arena;
                case DeNelle.Core.Audio.MusicTrack.Raid:      return MusicTrack.Raid;
                default:                                      return MusicTrack.Village;
            }
        }

        private static DeNelle.Core.Audio.MusicTrack ToCoreTrack(MusicTrack audio)
        {
            switch (audio)
            {
                case MusicTrack.Village:   return DeNelle.Core.Audio.MusicTrack.Village;
                case MusicTrack.Battle:    return DeNelle.Core.Audio.MusicTrack.Battle;
                case MusicTrack.Victory:   return DeNelle.Core.Audio.MusicTrack.Victory;
                case MusicTrack.Dungeon:   return DeNelle.Core.Audio.MusicTrack.Dungeon;
                case MusicTrack.Overworld: return DeNelle.Core.Audio.MusicTrack.Overworld;
                case MusicTrack.Defeat:    return DeNelle.Core.Audio.MusicTrack.Defeat;
                case MusicTrack.Title:     return DeNelle.Core.Audio.MusicTrack.Title;
                case MusicTrack.Arena:     return DeNelle.Core.Audio.MusicTrack.Arena;
                case MusicTrack.Raid:      return DeNelle.Core.Audio.MusicTrack.Raid;
                default:                   return DeNelle.Core.Audio.MusicTrack.Village;
            }
        }
    }
}
