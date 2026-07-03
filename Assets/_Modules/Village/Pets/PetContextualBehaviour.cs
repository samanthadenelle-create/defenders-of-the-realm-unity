// =============================================================================
// PetContextualBehaviour (DEF-71) — contextual pet reactions to world events.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS LIVES IN DeNelle.Village (not DeNelle.Pets):
//   The reactions are driven by world events that live in Village —
//   WaveManager (Assets/_Modules/Village/Waves/WaveManager.cs) and
//   HeartController (Assets/_Modules/Village/Heart/HeartController.cs).
//   DeNelle.Village references BOTH DeNelle.Pets AND DeNelle.Core, so from here
//   we can see Pet, WaveManager AND HeartController. DeNelle.Pets does NOT
//   reference DeNelle.Village (verified in DeNelle.Pets.asmdef), so this
//   wave-reactive behaviour cannot live in the Pets assembly.
//
// RECONCILIATION WITH THE DEF-71 SPEC (review-repo lineage, not this branch):
//   * The spec's `PetAnimatorController` and `PetType` enum DO NOT EXIST on this
//     branch. The real pet is DeNelle.Pets.Pet, which keeps its Animator PRIVATE
//     (GetComponentInChildren<Animator>() in Pet.Awake) and exposes a `Species`
//     STRING ("aether-sprite" / "flame-pup" / "ice-wolf"). We therefore (a) gate
//     reactions on Pet.Species rather than a PetType enum, and (b) drive the SAME
//     child Animator by caching our own GetComponentInChildren<Animator>() — we
//     do not touch Pet.cs or its asmdef.
//   * The spec's `WaveManager.OnBossSpawned` / `OnWaveCompleted` DO NOT EXIST.
//     The real events are OnApexBossSpawned(DragonBoss) and OnWaveCleared(int).
//     We map the boss reaction to OnApexBossSpawned and the celebrate reaction to
//     OnWaveCleared.
//   * DEF-54 (this pass): HeartController.OnHealthChanged now exists. The low-HP
//     whimper subscribes to the event instead of polling; Update() removed.
//
// ARCHITECTURE (non-negotiable):
//   * GetComponent caches happen in Awake.
//   * Event sub/unsub happens ONLY in OnEnable / OnDisable.
//   * No FindAnyObjectByType — we use a serialized WaveManager ref with a guarded
//     FindObjectsByType fallback resolved once in Awake.
//   * HP detection is throttled (never a per-frame Find / string lookup).
//   * All animator parameter names are cached `static readonly int` hashes — no
//     per-call Animator.StringToHash.
// =============================================================================

using System.Collections;
using DeNelle.Pets;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives contextual one-shot animations on a deployed <see cref="Pet"/> in
    /// reaction to world events: an apex boss spawning (Ice Wolf howls), a wave
    /// being cleared (Flame Pup does an excited circle), and the defended Heart
    /// dropping to low HP (any pet whimpers once). Lives on the pet's root and
    /// reaches the rig's Animator the same way <see cref="Pet"/> does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetContextualBehaviour : MonoBehaviour
    {
        [Header("Refs (auto-resolved in Awake when left blank)")]
        [Tooltip("The pet this behaviour reacts for. Left blank: found on this " +
                 "GameObject or its children in Awake.")]
        [SerializeField] private Pet _pet;

        [Tooltip("The pet rig's Animator. Left blank: GetComponentInChildren in " +
                 "Awake — the SAME child Animator Pet itself drives.")]
        [SerializeField] private Animator _animator;

        [Tooltip("The wave loop whose boss-spawn / wave-clear events we react to. " +
                 "Left blank: resolved via FindObjectsByType in Awake (guarded).")]
        [SerializeField] private WaveManager _waveManager;

        [Tooltip("The defended Heart whose HP we poll for the low-HP whimper. " +
                 "Left blank: taken from the WaveManager's Heart, else found.")]
        [SerializeField] private HeartController _heart;

        [Header("Low-HP whimper")]
        [Tooltip("Fraction (0-1) of Heart HP at or below which the pet whimpers once.")]
        [SerializeField, Range(0f, 1f)] private float _whimperHpFraction = 0.25f;

        [Header("Excited-circle (wave clear)")]
        [Tooltip("How many times the Flame Pup re-triggers the excited circle on a wave clear.")]
        [SerializeField, Min(1)] private int _excitedCircleLoops = 3;

        [Tooltip("Seconds one excited-circle loop lasts before the next re-trigger.")]
        [SerializeField, Min(0.1f)] private float _excitedCircleLoopSeconds = 1.5f;

        // Species ids — match pets.json (PetCatalog) verbatim.
        private const string SpeciesIceWolf = "ice-wolf";
        private const string SpeciesFlamePup = "flame-pup";

        // One-shot guard: the whimper fires once per wave (reset on OnWaveStarted).
        private bool _whimperPlayed;

        // Tracked so OnDisable can unsubscribe from the heart event.
        private HeartController _subscribedHeart;

        private Coroutine _excitedCircleRoutine;

        // Animator parameter hashes — cached once, never re-hashed per call.
        private static readonly int HowlHash = Animator.StringToHash("Howl");
        private static readonly int ExcitedCircleHash = Animator.StringToHash("ExcitedCircle");
        private static readonly int WhimperHash = Animator.StringToHash("Whimper");

        // WO-163: param hashes the controller actually declares, cached once.
        // SetTrigger checks this before driving so an absent param never spams
        // "Parameter does not exist".
        private readonly System.Collections.Generic.HashSet<int> _declaredParams
            = new System.Collections.Generic.HashSet<int>();

        private void Awake()
        {
            // Cache component refs in Awake (never in OnEnable / event handlers).
            if (_pet == null) _pet = GetComponentInChildren<Pet>();
            // Drive the SAME child Animator Pet uses (Pet caches it the same way).
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                    _declaredParams.Add(p.nameHash);
            }

            if (_waveManager == null)
                _waveManager = ResolveWaveManager();

            if (_heart == null)
                _heart = _waveManager != null ? _waveManager.Heart : ResolveHeart();
        }

        private void OnEnable()
        {
            if (_waveManager != null)
            {
                _waveManager.OnApexBossSpawned.AddListener(HandleApexBossSpawned);
                _waveManager.OnWaveCleared.AddListener(HandleWaveCleared);
                _waveManager.OnWaveStarted.AddListener(HandleWaveStarted);
            }

            // DEF-54: subscribe to HeartController.OnHealthChanged now that it exists.
            if (_heart != null)
            {
                _subscribedHeart = _heart;
                _heart.OnHealthChanged += OnHeartHpChanged;
            }
        }

        private void OnDisable()
        {
            if (_waveManager != null)
            {
                _waveManager.OnApexBossSpawned.RemoveListener(HandleApexBossSpawned);
                _waveManager.OnWaveCleared.RemoveListener(HandleWaveCleared);
                _waveManager.OnWaveStarted.RemoveListener(HandleWaveStarted);
            }

            if (_subscribedHeart != null)
            {
                _subscribedHeart.OnHealthChanged -= OnHeartHpChanged;
                _subscribedHeart = null;
            }

            if (_excitedCircleRoutine != null)
            {
                StopCoroutine(_excitedCircleRoutine);
                _excitedCircleRoutine = null;
            }
        }

        private void OnHeartHpChanged(float hp)
        {
            if (_whimperPlayed) return;
            // HeartController.Hp is on the 0-100 scale; compare as a fraction.
            if (hp / 100f <= _whimperHpFraction)
            {
                _whimperPlayed = true;
                SetTrigger(WhimperHash);
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────

        /// <summary>
        /// An apex flying boss spawned — the Ice Wolf howls. Other species ignore
        /// it. (Spec mapped OnBossSpawned -> OnApexBossSpawned for this branch.)
        /// </summary>
        private void HandleApexBossSpawned(DragonBoss boss)
        {
            if (IsSpecies(SpeciesIceWolf))
                SetTrigger(HowlHash);
        }

        /// <summary>
        /// A wave was cleared — the Flame Pup does an excited circle, looping the
        /// trigger a fixed number of times then settling back to idle. Other
        /// species ignore it. (Spec OnWaveCompleted -> OnWaveCleared.)
        /// </summary>
        private void HandleWaveCleared(int waveId)
        {
            if (!IsSpecies(SpeciesFlamePup)) return;
            if (_animator == null) return;

            if (_excitedCircleRoutine != null) StopCoroutine(_excitedCircleRoutine);
            _excitedCircleRoutine = StartCoroutine(ExcitedCircleRoutine());
        }

        /// <summary>
        /// A new wave started — re-arm the one-shot whimper so it can fire again
        /// for the new wave's threat window.
        /// </summary>
        private void HandleWaveStarted(int waveId)
        {
            // Re-arm the one-shot whimper so it can fire again for the new wave.
            _whimperPlayed = false;
        }

        private IEnumerator ExcitedCircleRoutine()
        {
            var wait = new WaitForSeconds(_excitedCircleLoopSeconds);
            for (int i = 0; i < _excitedCircleLoops; i++)
            {
                SetTrigger(ExcitedCircleHash);
                yield return wait;
            }
            _excitedCircleRoutine = null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>True when the bound pet is of the given species id (pets.json).</summary>
        private bool IsSpecies(string speciesId)
        {
            return _pet != null && _pet.Species == speciesId;
        }

        /// <summary>Null-guarded animator trigger — a pet with no rig is a no-op.</summary>
        private void SetTrigger(int hash)
        {
            // WO-163: only drive a param the controller declares (no error spam).
            if (_animator != null && _declaredParams.Contains(hash))
                _animator.SetTrigger(hash);
        }

        /// <summary>
        /// Resolves the scene's WaveManager without FindAnyObjectByType. There is one
        /// wave loop per Village scene; FindObjectsByType + a guard is the
        /// approved discovery path (see WaveManager.ResolveSceneRefs).
        /// </summary>
        private static WaveManager ResolveWaveManager()
        {
            var all = Object.FindObjectsByType<WaveManager>();
            return (all != null && all.Length > 0) ? all[0] : null;
        }

        /// <summary>Resolves the Heart without FindAnyObjectByType (guarded fallback).</summary>
        private static HeartController ResolveHeart()
        {
            var all = Object.FindObjectsByType<HeartController>();
            return (all != null && all.Length > 0) ? all[0] : null;
        }
    }
}
