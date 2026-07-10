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

using DeNelle.Audio;        // MusicDirector (Village references DeNelle.Audio)
using DeNelle.Core.Audio;   // MusicLayer (Core contract)
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Wave-loop music POLICY PROVIDER (2026-07-09, MUSIC_AUTHORITY_DESIGN): combat
    /// music while a wave is active, exploration between waves. It owns ZERO
    /// AudioSources — it Pushes/Releases the single <see cref="MusicDirector"/>'s
    /// Wave layer. Attached to the WaveManager GameObject by the scene builder.
    ///
    /// STILL INERT by intent (WO-571): its two track fields ship UNASSIGNED because
    /// BattleMusicManager (Battle layer) is the live wave scorer; giving this Wave
    /// clips would double-score. With null clips every Push is a harmless no-op
    /// (the director treats a null clip as Release). It now owns no sources either,
    /// so it can never produce a second bed even if clips were wired.
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

        [Tooltip("Tense combat loop pushed when a wave begins.")]
        [SerializeField] private AudioClip _combatTrack;

        [Header("Mix")]
        [Tooltip("Seconds for one crossfade. Uses unscaledDeltaTime — safe under timeScale changes.")]
        [SerializeField, Min(0.01f)] private float _crossfadeSeconds = 1.5f;

        [Tooltip("Target volume of the audible track (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _trackVolume = 1f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void Awake()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            // No AudioSources to build — the single MusicDirector owns playback.
        }

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            if (_wave != null)
            {
                _wave.OnWaveStarted.AddListener(HandleWaveStarted);
                _wave.OnWaveCleared.AddListener(HandleWaveCleared);
            }

            // Begin on the calm exploration bed (if assigned). Null → no-op.
            PushWave(_explorationTrack);
        }

        private void OnDisable()
        {
            if (_wave != null)
            {
                _wave.OnWaveStarted.RemoveListener(HandleWaveStarted);
                _wave.OnWaveCleared.RemoveListener(HandleWaveCleared);
            }

            // Give up our layer so a disabled controller never holds the Wave bed.
            MusicDirector.Instance?.Release(MusicLayer.Wave);
        }

        // ── Wave event handlers ────────────────────────────────────────────────

        private void HandleWaveStarted(int waveId)
        {
            PushWave(_combatTrack);
            // DEF-67: light screen-shake impulse to sell the wave-start transition.
            // SmartMobileCamera.Instance is null between scenes — safe null-conditional.
            SmartMobileCamera.Instance?.Shake(0.12f, 0.35f);
        }

        // Wave cleared → release the Wave layer; the director auto-falls back to the
        // ambient bed (or the exploration clip if this controller re-pushes it).
        private void HandleWaveCleared(int waveId) => MusicDirector.Instance?.Release(MusicLayer.Wave);

        // ── Public API (for any future non-polling caller, e.g. an apex sting) ──

        /// <summary>Push the combat track onto the Wave layer. Safe to call repeatedly.</summary>
        public void PlayCombatTrack() => PushWave(_combatTrack);

        /// <summary>Push the calm exploration track onto the Wave layer. Safe to call repeatedly.</summary>
        public void PlayExplorationTrack() => PushWave(_explorationTrack);

        // ── Request → the single authority ─────────────────────────────────────

        // Push a clip onto the director's Wave layer. A null clip (placeholder not
        // yet assigned) makes the director Release the layer — a harmless no-op that
        // keeps this controller inert until clips are wired.
        private void PushWave(AudioClip clip)
        {
            MusicDirector.Instance?.PushClip(
                MusicLayer.Wave, clip, _trackVolume, true, _crossfadeSeconds, "Wave");
        }
    }
}
