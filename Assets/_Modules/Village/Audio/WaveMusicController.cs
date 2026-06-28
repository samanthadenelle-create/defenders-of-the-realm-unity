// =============================================================================
// WaveMusicController — DEF-67 (Audio & VFX Layering), deliverable 1.
// -----------------------------------------------------------------------------
// ⚠ SUPERSEDED 2026-06-28 (WO-571): BattleMusicManager (WO-372) now owns the
// wave-loop music — it scores four states (Combat/Intense/Victory/Boss), loads
// its clips by Resources path (Resources/Music/Battle/Overworld_*), and routes
// through the shared Music mixer group. This older two-track A/B controller is
// INTENTIONALLY left with NO Resources-by-id wiring: giving it clips would make
// it crossfade combat/exploration music AT THE SAME TIME as BattleMusicManager
// (double-scoring). It ships silent (null clips) and is therefore inert. Retire /
// remove from WaveSystemBridgeBootstrap once BattleMusicManager is felt-verified.
// Do NOT wire AudioClips here — see docs/AUDIO/AUDIO_CLIP_MANIFEST.md.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village. Lives ON the WaveManager GameObject (the canonical
// wave-reactive bridge pattern — see DailyQuestCombatBridge): [DisallowMultiple
// Component] + [RequireComponent(typeof(WaveManager))], a [SerializeField]
// WaveManager set in Reset()=>GetComponent and re-checked in OnEnable, and
// AddListener / RemoveListener strictly in OnEnable / OnDisable. The scene
// builder attaches this — this file does NOT wire scenes.
//
// RECONCILIATION TO THIS BRANCH (the DEF-67 spec was written against a different
// lineage):
//   • The spec's WaveManager.Instance singleton does NOT exist here. WaveManager
//     is a plain component, so we reach it via GetComponent on our own GO and
//     subscribe to its INSTANCE UnityEvents.
//   • Spec event OnWaveStarted(int) exists here (WaveNumberEvent : UnityEvent<int>).
//   • The spec's PlayBossSting() was driven from a WaveManager.SpawnBossWave()
//     that does not exist. This branch instead exposes OnWaveCleared(int), so we
//     also subscribe to it to fade BACK to the exploration track when a wave is
//     done — matching the task's two-track intent (combat during a wave, calm
//     between waves). A public PlayCombatTrack()/PlayExplorationTrack() pair is
//     left for any future caller (e.g. an apex-boss sting) without polling.
//
// AUDIO ASSETS are placeholder-ready: the two AudioClips are [SerializeField] and
// assigned later (never hardcoded / Resources.Load). The crossfade is timeScale-
// safe (Time.unscaledDeltaTime) and runs from a single coroutine restarted on
// each request so overlapping fades never fight.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A/B AudioSource crossfade driven by the wave loop: combat music while a
    /// wave is active, exploration music between waves. Attached to the
    /// WaveManager GameObject by the scene builder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveManager))]
    public sealed class WaveMusicController : MonoBehaviour
    {
        [Header("Wave loop (auto-wired to the WaveManager on this GameObject)")]
        [SerializeField] private WaveManager _wave;

        [Header("Tracks (assigned later — placeholder-ready)")]
        [Tooltip("Calm loop played between waves / before the loop starts.")]
        [SerializeField] private AudioClip _explorationTrack;

        [Tooltip("Tense combat loop crossfaded in when a wave begins.")]
        [SerializeField] private AudioClip _combatTrack;

        [Header("Crossfade")]
        [Tooltip("Seconds for one A/B crossfade. Uses unscaledDeltaTime — safe under timeScale changes.")]
        [SerializeField, Min(0.01f)] private float _crossfadeSeconds = 1.5f;

        [Tooltip("Target volume of the audible track (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _trackVolume = 1f;

        // Two looping music sources we alternate between so a crossfade never cuts.
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _useSourceA = true;
        private Coroutine _fadeRoutine;
        private AudioClip _currentClip;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void Awake()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();

            // Build the two crossfade sources in code (they are music plumbing,
            // not Inspector-authored — the only AddComponent the task allows, and
            // it mirrors the spec's "two AudioSource components" intent). Both
            // loop, start silent, and ignore the scene's spatial blend (2D music).
            _sourceA = CreateMusicSource("MusicSourceA");
            _sourceB = CreateMusicSource("MusicSourceB");
        }

        private AudioSource CreateMusicSource(string label)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;   // 2D — music is non-positional
            src.volume = 0f;
            // A label is handy when inspecting at runtime; AudioSource has no name,
            // so this is purely diagnostic via the component order.
            return src;
        }

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            if (_wave != null)
            {
                _wave.OnWaveStarted.AddListener(HandleWaveStarted);
                _wave.OnWaveCleared.AddListener(HandleWaveCleared);
            }

            // Begin on the calm exploration bed (if assigned) so the village isn't
            // silent before the first wave.
            if (_explorationTrack != null) CrossfadeTo(_explorationTrack);
        }

        private void OnDisable()
        {
            if (_wave != null)
            {
                _wave.OnWaveStarted.RemoveListener(HandleWaveStarted);
                _wave.OnWaveCleared.RemoveListener(HandleWaveCleared);
            }

            // Stop any in-flight fade so a disable mid-crossfade doesn't leave a
            // coroutine dangling against destroyed sources.
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        }

        // ── Wave event handlers ────────────────────────────────────────────────

        private void HandleWaveStarted(int waveId)
        {
            CrossfadeTo(_combatTrack);
            // DEF-67: light screen-shake impulse to sell the wave-start transition.
            // SmartMobileCamera.Instance is null between scenes — safe null-conditional.
            SmartMobileCamera.Instance?.Shake(0.12f, 0.35f);
        }

        private void HandleWaveCleared(int waveId) => CrossfadeTo(_explorationTrack);

        // ── Public API (for any future non-polling caller, e.g. an apex sting) ──

        /// <summary>Crossfade to the combat track. Safe to call repeatedly.</summary>
        public void PlayCombatTrack() => CrossfadeTo(_combatTrack);

        /// <summary>Crossfade to the calm exploration track. Safe to call repeatedly.</summary>
        public void PlayExplorationTrack() => CrossfadeTo(_explorationTrack);

        // ── Crossfade ──────────────────────────────────────────────────────────

        /// <summary>
        /// Crossfade the audible source out and a fresh source (carrying
        /// <paramref name="clip"/>) in. No-ops when already playing that clip, or
        /// when the clip is null (placeholder not yet assigned).
        /// </summary>
        private void CrossfadeTo(AudioClip clip)
        {
            if (clip == null) return;
            if (clip == _currentClip) return;   // already on this track — don't restart
            _currentClip = clip;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(CrossfadeRoutine(clip));
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip)
        {
            AudioSource incoming = _useSourceA ? _sourceA : _sourceB;
            AudioSource outgoing = _useSourceA ? _sourceB : _sourceA;
            _useSourceA = !_useSourceA;

            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();

            float startOutVol = outgoing.volume;
            float t = 0f;
            while (t < _crossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;   // timeScale-safe (spec acceptance criterion)
                float ratio = Mathf.Clamp01(t / _crossfadeSeconds);
                incoming.volume = ratio * _trackVolume;
                outgoing.volume = startOutVol * (1f - ratio);
                yield return null;
            }

            incoming.volume = _trackVolume;
            outgoing.volume = 0f;
            outgoing.Stop();
            _fadeRoutine = null;
        }
    }
}
