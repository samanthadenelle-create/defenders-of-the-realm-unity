// =============================================================================
// TowerVoiceController — DEF-67 (Audio & VFX Layering), deliverable 3.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village. Plays a low-HP "the tower / Heart is failing!" voice
// line ONCE per session when the Heart's HP drops below 30% of max. Lives ON the
// WaveManager GameObject (the canonical wave-reactive bridge pattern — see
// DailyQuestCombatBridge): [DisallowMultipleComponent] +
// [RequireComponent(typeof(WaveManager))], a [SerializeField] WaveManager set in
// Reset()=>GetComponent and re-checked in OnEnable. The scene builder attaches
// this — this file does NOT wire scenes.
//
// RECONCILIATION TO THIS BRANCH:
//   • DEF-54 (this pass): HeartController.OnHealthChanged now exists.
//     This controller subscribes on OnEnable and unsubscribes on OnDisable —
//     exactly the spec's "subscribe and unsubscribe after playing" idiom.
//     The Update() polling loop has been removed.
//   • The Heart is reached via WaveManager.Heart (this component is on the
//     WaveManager GameObject), never via Find at runtime.
//
// MAX-HP CHOICE (documented per the task): HeartController.Hp is declared
//   [SerializeField, Range(0f, 100f)] private float _hp = 100f;
// and SetHp() clamps to [0,100]. There is NO separate max-HP field, so the
// canonical maximum is the hard 100 ceiling of that Range. We therefore treat
// MAX HP = 100 and fire when Hp < 30% of 100 (i.e. Hp < 30). This matches
// HeartController's own thresholds (HeartState comments key off 25%/50% of the
// same 0-100 scale). As a defensive fallback for any future variant where the
// first observed Hp exceeds 100, we also cache the first observed Hp as the max
// if it is larger than 100 — so the 30% threshold tracks the real ceiling.
//
// AUDIO ASSETS are placeholder-ready: the voice lines are a [SerializeField]
// AudioClip[] assigned later (never hardcoded / Resources.Load). A 2D AudioSource
// is built once in Awake.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Fires a one-shot low-HP voice line when the Heart drops below 30% of max.
    /// Polls Heart.Hp on a throttle and stops polling permanently after firing.
    /// Attached to the WaveManager GameObject by the scene builder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveManager))]
    public sealed class TowerVoiceController : MonoBehaviour
    {
        [Header("Wave loop (auto-wired to the WaveManager on this GameObject)")]
        [SerializeField] private WaveManager _wave;

        [Header("Voice (assigned later — placeholder-ready)")]
        [Tooltip("Low-HP voice lines (\"The Heart is failing!\"). One is chosen at random when fired.")]
        [SerializeField] private AudioClip[] _voiceLines;

        [Tooltip("Volume for the voice line (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        [Header("Trigger")]
        [Tooltip("Fraction of max HP below which the line fires (0-1). Default 0.30 = 30%.")]
        [SerializeField, Range(0f, 1f)] private float _lowHpFraction = 0.30f;

        // The hard HP ceiling HeartController clamps to (see header MAX-HP CHOICE).
        private const float DefaultMaxHp = 100f;

        private AudioSource _source;
        private float _maxHp = DefaultMaxHp;
        private bool _voiceFired;
        private HeartController _subscribedHeart;   // tracked so OnDisable can unsubscribe

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void Awake()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;   // 2D announcement, non-positional
            _source.volume = 1f;         // PlayOneShot's per-call scale carries the mix
            // WO-571: route through the shared Voice mixer group so the player's
            // Voice/Master volume + mute apply (was bypassing the mixer entirely).
            _source.outputAudioMixerGroup = VillageAudioResources.Group("Voice");

            // WO-571: resolve voice lines by a CONVENTION Resources path when none
            // were authored (canon bans drag-drop). Drop a clip at
            // Resources/Audio/Voice/HeartFailing(_1/_2/_3) and it "just works".
            ResolveVoiceLinesFromResources();
        }

        // WO-571: the Resources-by-id voice path. Only fills in when the serialized
        // array is empty/all-null, so an authored set (if one is ever wired by an
        // import pass via SetVoiceLines/Resources) still wins. Speech can't be
        // synthesised, so a missing set stays a silent no-op — self-reported via
        // FlowTrace so a run shows WHICH cue has no audio (no silent failure, §12).
        private void ResolveVoiceLinesFromResources()
        {
            if (HasAnyVoiceLine()) return;

            var loaded = new System.Collections.Generic.List<AudioClip>();
            foreach (string path in VoiceResourcePaths)
            {
                AudioClip c = VillageAudioResources.Load(path);
                if (c != null) loaded.Add(c);
            }

            if (loaded.Count > 0)
            {
                _voiceLines = loaded.ToArray();
                return;
            }

            FlowTrace.Warn("Audio",
                "TowerVoiceController: no low-HP voice clip found at Resources/Audio/Voice/" +
                "HeartFailing(_1/_2/_3) — the 'Heart is failing!' cue will be SILENT. " +
                "Drop a VO clip there (see docs/AUDIO/AUDIO_CLIP_MANIFEST.md).");
        }

        // Convention Resources paths the low-HP voice cue resolves from (in order;
        // every clip that exists is added so several can rotate).
        private static readonly string[] VoiceResourcePaths =
        {
            "Audio/Voice/HeartFailing",
            "Audio/Voice/HeartFailing_1",
            "Audio/Voice/HeartFailing_2",
            "Audio/Voice/HeartFailing_3",
        };

        private bool HasAnyVoiceLine()
        {
            if (_voiceLines == null) return false;
            foreach (AudioClip c in _voiceLines)
                if (c != null) return true;
            return false;
        }

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();

            HeartController heart = _wave != null ? _wave.Heart : null;
            if (heart == null) return;

            // Seed the max from the current Hp in case it exceeds the default 100
            // ceiling (defensive — see header).
            if (heart.Hp > _maxHp) _maxHp = heart.Hp;

            // DEF-54: subscribe to HeartController.OnHealthChanged instead of
            // polling — the event was added to HeartController.SetHp() in this pass.
            // Unsubscribe in OnDisable so no stale delegate survives a wave restart.
            _subscribedHeart = heart;
            heart.OnHealthChanged += OnHeartHpChanged;
        }

        private void OnDisable()
        {
            if (_subscribedHeart != null)
            {
                _subscribedHeart.OnHealthChanged -= OnHeartHpChanged;
                _subscribedHeart = null;
            }
        }

        private void OnHeartHpChanged(float hp)
        {
            if (_voiceFired) return;
            if (hp > _maxHp) _maxHp = hp;   // keep the ceiling honest
            float threshold = _maxHp * _lowHpFraction;
            if (hp <= threshold)
                FireVoiceLine();
        }

        private void FireVoiceLine()
        {
            _voiceFired = true;   // guard — exactly once per session; Update now early-returns

            if (_source == null || _voiceLines == null || _voiceLines.Length == 0) return;

            // Pick a non-null line (random when several are authored).
            AudioClip clip = _voiceLines[Random.Range(0, _voiceLines.Length)];
            if (clip == null)
            {
                // Fall back to the first non-null entry if the random pick was empty.
                foreach (AudioClip c in _voiceLines)
                    if (c != null) { clip = c; break; }
            }
            if (clip == null) return;

            _source.PlayOneShot(clip, _volume);
        }
    }
}
