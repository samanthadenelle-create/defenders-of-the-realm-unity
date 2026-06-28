// =============================================================================
// HeartwoodAmbientController — WO-243 §2 ("Heartwood — living ambient sounds").
// -----------------------------------------------------------------------------
// namespace DeNelle.Village. Event-driven Heartwood audio — NO Update() polling.
// Lives ON the HeartController GameObject (the canonical reactive-bridge pattern,
// like WaveMusicController / TowerAudioController): [RequireComponent(Heart
// Controller)], the ref auto-wired in Reset()/Awake/OnEnable, and AddListener /
// RemoveListener strictly in OnEnable / OnDisable so there's no stale-delegate
// leak. The scene builder attaches this — this file does NOT wire scenes.
//
// WO-243 §2 maps Heart HP to a living ambient bed plus two stingers:
//   100–75%  Healthy   — warm hum, leaves, occasional chime           (loop)
//   74–40%   Strained  — deeper hum, occasional groan                 (loop)
//   39–15%   Critical  — dissonant undertone, bark cracking, wind     (loop)
//   <15%     (Critical bed continues — the danger read is the visuals/voice)
//   on hit   Hit       — deep resonant bell-struck impact          (one-shot)
//   destroyed Fall     — long descending tone, crack, then silence  (one-shot)
//
// RECONCILIATION TO THIS BRANCH: HeartController already exposes the exact event
// surface WO-243 names — OnHealthChanged(float 0..100) and OnHeartDestroyed() —
// so this subscribes to them directly (no polling, unlike the older TowerAudio
// Controller which predated those events). The ambient BED crossfades between HP
// tiers on a private A/B AudioSource pair (timeScale-safe, unscaledDeltaTime),
// mirroring WaveMusicController so two looping beds never hard-cut.
//
// AUDIO ASSETS are placeholder-ready: every clip is [SerializeField], assigned
// later by the integrator (never hardcoded / Resources.Load). With no clips the
// controller is a silent no-op — it never throws and never blocks the Heart.
// The "Hit" one-shot needs a damage signal; OnHealthChanged fires on every HP
// change, so a DECREASE is treated as "took damage" and plays the impact.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays the Heartwood's living ambient bed (crossfading by HP tier) plus a
    /// resonant hit stinger when it takes damage and a falling stinger when it is
    /// destroyed. Event-driven off <see cref="HeartController"/>; never polls.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeartController))]
    public sealed class HeartwoodAmbientController : MonoBehaviour
    {
        // HP tier the ambient bed is keyed to.
        private enum AmbientTier { None, Healthy, Strained, Critical }

        [Header("Heart (auto-wired to the HeartController on this GameObject)")]
        [SerializeField] private HeartController _heart;

        [Header("Ambient beds (assigned later — placeholder-ready, looped)")]
        [Tooltip("100–75% HP: warm hum, leaves rustling, occasional crystal chime.")]
        [SerializeField] private AudioClip _healthyBed;

        [Tooltip("74–40% HP: deeper hum, occasional groan, chimes less frequent.")]
        [SerializeField] private AudioClip _strainedBed;

        [Tooltip("39–0% HP: dissonant undertone, bark cracking, wind picks up.")]
        [SerializeField] private AudioClip _criticalBed;

        [Header("Stingers (assigned later — placeholder-ready, one-shot)")]
        [Tooltip("Played when the Heart takes damage — a deep resonant bell-struck impact.")]
        [SerializeField] private AudioClip _hitClip;

        [Tooltip("Played once when the Heart is destroyed — a long descending tone, crack, then silence.")]
        [SerializeField] private AudioClip _fallClip;

        [Header("Mix")]
        [Tooltip("Volume of the looping ambient bed (0-1). Soft — it's a background presence.")]
        [SerializeField, Range(0f, 1f)] private float _bedVolume = 0.5f;

        [Tooltip("Volume of the hit / fall one-shot stingers (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _stingerVolume = 0.85f;

        [Tooltip("Seconds for one ambient-bed crossfade between HP tiers. unscaledDeltaTime — timeScale-safe.")]
        [SerializeField, Min(0.01f)] private float _crossfadeSeconds = 1.5f;

        // ── HP-tier thresholds (WO-243 §2) ───────────────────────────────────
        private const float HealthyMin  = 75f; // ≥ 75% -> Healthy
        private const float StrainedMin = 40f; // ≥ 40% -> Strained, else Critical

        // Two looping beds we alternate so a tier change never cuts. A separate
        // one-shot source carries the hit / fall stingers (so a stinger never
        // disturbs the bed crossfade).
        private AudioSource _bedA;
        private AudioSource _bedB;
        private AudioSource _stinger;
        private bool _useBedA = true;
        private Coroutine _fadeRoutine;

        private AmbientTier _tier = AmbientTier.None;
        private float _lastHp = -1f;
        private bool _fell;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        // WO-571: self-bootstrap — this controller was DEAD CODE (defined but never
        // attached to any GameObject, so the Heartwood ambient bed never played).
        // Like BattleMusicManager, attach it onto the HeartController GO at runtime
        // (no Village.unity re-save). [RequireComponent(HeartController)] is honoured
        // because we only AddComponent onto a GO that already has a HeartController.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedStatic;
            AttachToHeart();
        }

        private static void OnSceneLoadedStatic(
            UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
            => AttachToHeart();

        private static void AttachToHeart()
        {
            var hearts = Object.FindObjectsByType<HeartController>(FindObjectsSortMode.None);
            foreach (var heart in hearts)
            {
                if (heart == null) continue;
                if (heart.GetComponent<HeartwoodAmbientController>() == null)
                    heart.gameObject.AddComponent<HeartwoodAmbientController>();
            }
        }

        private void Reset() => _heart = GetComponent<HeartController>();

        private void Awake()
        {
            if (_heart == null) _heart = GetComponent<HeartController>();

            _bedA    = CreateSource("HeartwoodBedA", loop: true);
            _bedB    = CreateSource("HeartwoodBedB", loop: true);
            _stinger = CreateSource("HeartwoodStinger", loop: false);

            // WO-571: route through the shared mixer so the player's volume + mute
            // apply (the beds are music-bed presence; the stingers are SFX). Was
            // bypassing the mixer entirely (default output).
            AudioMixerGroupRoute();

            // WO-571: resolve every clip by a CONVENTION Resources path when the
            // serialized field is unassigned (canon bans drag-drop). Drop a clip at
            // the documented Resources/Audio/... path and it "just works".
            ResolveClipsFromResources();
        }

        // Route bed sources -> Music group, stinger -> SFX group (null-safe: a
        // missing mixer leaves them on the default output, still audible).
        private void AudioMixerGroupRoute()
        {
            var music = VillageAudioResources.Group("Music");
            var sfx   = VillageAudioResources.Group("SFX");
            if (_bedA != null)    _bedA.outputAudioMixerGroup = music;
            if (_bedB != null)    _bedB.outputAudioMixerGroup = music;
            if (_stinger != null) _stinger.outputAudioMixerGroup = sfx;
        }

        // WO-571: the Resources-by-id clip path. Each field is filled ONLY when it
        // was left unassigned, so an authored clip (if ever wired) still wins. A
        // tier with no bed simply stays a silent no-op (the controller already
        // handles a null bed gracefully); FlowTrace self-reports the gaps (§12).
        private void ResolveClipsFromResources()
        {
            if (_healthyBed  == null) _healthyBed  = VillageAudioResources.Load("Audio/Ambient/Heartwood_Healthy");
            if (_strainedBed == null) _strainedBed = VillageAudioResources.Load("Audio/Ambient/Heartwood_Strained");
            if (_criticalBed == null) _criticalBed = VillageAudioResources.Load("Audio/Ambient/Heartwood_Critical");
            if (_hitClip     == null) _hitClip     = VillageAudioResources.Load("Audio/Sfx/Heart_Hit");
            if (_fallClip    == null) _fallClip    = VillageAudioResources.Load("Audio/Sfx/Heart_Fall");

            if (_healthyBed == null && _strainedBed == null && _criticalBed == null)
                FlowTrace.Warn("Audio",
                    "HeartwoodAmbientController: no ambient bed clips found at " +
                    "Resources/Audio/Ambient/Heartwood_{Healthy,Strained,Critical} — the " +
                    "Heartwood plays silent. Drop ambient loops there (see " +
                    "docs/AUDIO/AUDIO_CLIP_MANIFEST.md).");
        }

        private AudioSource CreateSource(string label, bool loop)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;   // 2D — the Heart bed is a constant presence, not positional
            src.volume = 0f;
            return src;
        }

        private void OnEnable()
        {
            if (_heart == null) _heart = GetComponent<HeartController>();
            if (_heart != null)
            {
                _heart.OnHealthChanged += HandleHealthChanged;
                _heart.OnHeartDestroyed += HandleDestroyed;

                // Seed the starting tier from current HP so the bed plays from the
                // first frame without waiting for an HP change.
                _lastHp = _heart.Hp;
                ApplyTierForHp(_heart.Hp, crossfade: false);
            }
        }

        private void OnDisable()
        {
            if (_heart != null)
            {
                _heart.OnHealthChanged -= HandleHealthChanged;
                _heart.OnHeartDestroyed -= HandleDestroyed;
            }
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        }

        // ── Heart event handlers ────────────────────────────────────────────────

        private void HandleHealthChanged(float hp)
        {
            // A DROP in HP means the Heart just took damage — fire the resonant
            // hit stinger. (OnHealthChanged also fires on heals; only damage rings.)
            if (_lastHp >= 0f && hp < _lastHp - 0.01f)
                PlayStinger(_hitClip);
            _lastHp = hp;

            // Move the ambient bed to the matching HP tier (crossfaded).
            ApplyTierForHp(hp, crossfade: true);
        }

        private void HandleDestroyed()
        {
            if (_fell) return; // fire-once guard (mirrors HeartController's own guard)
            _fell = true;

            // Fade the living bed out — the Heartwood goes quiet — and ring the
            // long descending fall stinger over the top.
            StopBed();
            PlayStinger(_fallClip);
        }

        // ── Tier selection + bed crossfade ─────────────────────────────────────

        private static AmbientTier TierForHp(float hp)
        {
            if (hp >= HealthyMin)  return AmbientTier.Healthy;
            if (hp >= StrainedMin) return AmbientTier.Strained;
            return AmbientTier.Critical;
        }

        private AudioClip BedFor(AmbientTier tier)
        {
            switch (tier)
            {
                case AmbientTier.Healthy:  return _healthyBed;
                case AmbientTier.Strained: return _strainedBed;
                case AmbientTier.Critical: return _criticalBed;
                default:                   return null;
            }
        }

        private void ApplyTierForHp(float hp, bool crossfade)
        {
            if (_fell) return; // destroyed — no bed.

            AmbientTier next = TierForHp(hp);
            if (next == _tier) return; // already on this tier's bed.
            _tier = next;

            AudioClip clip = BedFor(next);
            if (crossfade) CrossfadeBed(clip);
            else SnapBed(clip);
        }

        // Immediate bed start (OnEnable seed) — no crossfade.
        private void SnapBed(AudioClip clip)
        {
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            AudioSource incoming = _useBedA ? _bedA : _bedB;
            AudioSource other    = _useBedA ? _bedB : _bedA;
            if (other != null) { other.Stop(); other.volume = 0f; }
            if (incoming == null) return;
            if (clip == null) { incoming.Stop(); incoming.volume = 0f; return; }
            incoming.clip = clip;
            incoming.volume = _bedVolume;
            incoming.Play();
        }

        private void CrossfadeBed(AudioClip clip)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(CrossfadeRoutine(clip));
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip)
        {
            AudioSource incoming = _useBedA ? _bedA : _bedB;
            AudioSource outgoing = _useBedA ? _bedB : _bedA;
            _useBedA = !_useBedA;

            // A null clip (placeholder not yet assigned for this tier) just fades
            // the current bed out — the Heartwood goes quiet for that tier.
            if (incoming != null && clip != null)
            {
                incoming.clip = clip;
                incoming.volume = 0f;
                incoming.Play();
            }

            float startOut = outgoing != null ? outgoing.volume : 0f;
            float target = clip != null ? _bedVolume : 0f;
            float t = 0f;
            while (t < _crossfadeSeconds)
            {
                t += Time.unscaledDeltaTime; // timeScale-safe
                float k = Mathf.Clamp01(t / _crossfadeSeconds);
                if (incoming != null && clip != null) incoming.volume = k * target;
                if (outgoing != null) outgoing.volume = startOut * (1f - k);
                yield return null;
            }

            if (incoming != null && clip != null) incoming.volume = target;
            if (outgoing != null) { outgoing.volume = 0f; outgoing.Stop(); }
            _fadeRoutine = null;
        }

        private void StopBed()
        {
            if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
            if (_bedA != null) { _bedA.Stop(); _bedA.volume = 0f; }
            if (_bedB != null) { _bedB.Stop(); _bedB.volume = 0f; }
            _tier = AmbientTier.None;
        }

        // ── Stingers ───────────────────────────────────────────────────────────

        private void PlayStinger(AudioClip clip)
        {
            if (clip == null || _stinger == null) return;
            _stinger.PlayOneShot(clip, _stingerVolume);
        }
    }
}
