// =============================================================================
// TowerAudioController — DEF-67 (Audio & VFX Layering), deliverable 2.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village. Event-driven tower SFX — NO Update() polling.
//
// RECONCILIATION TO THIS BRANCH (the DEF-67 spec was written against a different
// lineage):
//   • The spec drove this controller off HeartOfTown.OnHealthChanged, escalating
//     creak / rumble / impact layers by HP tier. NEITHER HeartOfTown NOR a
//     health-changed event exists on this branch (HeartController has Hp / SetHp
//     but raises no event), so the HP-tiered creak escalation cannot be wired
//     here without polling — and that low-HP poll is reserved for
//     TowerVoiceController. The "tower in danger" voice line covers the spec's
//     low-HP audio intent.
//   • Towers on this branch (Tower.cs / TowerConstruction.cs) raise NO audio
//     events yet. Per the DEF-67 task narrowing, this controller therefore
//     exposes two PUBLIC, event-style entry points — PlayBuildComplete() and
//     PlayUpgrade() — that Tower.CompleteConstruction / Tower.Upgrade (or their
//     queue) can call when those audio hooks land. Each PlayOneShot's a
//     [SerializeField] AudioClip through a single 2D AudioSource. Calling code
//     pushes the event; nothing here pulls state.
//
// AUDIO ASSETS are placeholder-ready: the AudioClips are [SerializeField] and
// assigned later (never hardcoded / Resources.Load). The AudioSource is built
// once in Awake (2D, non-looping, playOnAwake off) so PlayOneShot just works.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays one-shot tower SFX on demand (build-complete chime, upgrade chime).
    /// Driven by explicit method calls from the tower systems — never polls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerAudioController : MonoBehaviour
    {
        [Header("Clips (assigned later — placeholder-ready)")]
        [Tooltip("Played when a tower finishes construction (call PlayBuildComplete()).")]
        [SerializeField] private AudioClip _buildCompleteClip;

        [Tooltip("Played when a tower is upgraded a level (call PlayUpgrade()).")]
        [SerializeField] private AudioClip _upgradeClip;

        [Header("Mix")]
        [Tooltip("Volume scale for the one-shot SFX (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

        // A single 2D source for the non-positional UI-style tower chimes. Built
        // in Awake — never GetComponent'd in a hot path, never AddComponent'd per
        // play (PlayOneShot reuses this source's mixer voice).
        private AudioSource _source;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;   // 2D chime, non-positional
            _source.volume = 1f;         // PlayOneShot's per-call scale carries the mix
        }

        /// <summary>
        /// Play the build-complete chime. Callable by TowerConstruction /
        /// TowerConstructionQueue when a tower reveals. No-ops if no clip / source.
        /// </summary>
        public void PlayBuildComplete() => PlayOneShot(_buildCompleteClip);

        /// <summary>
        /// Play the upgrade chime. Callable by Tower.Upgrade when a level lands.
        /// No-ops if no clip / source.
        /// </summary>
        public void PlayUpgrade() => PlayOneShot(_upgradeClip);

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip, _volume);
        }
    }
}
