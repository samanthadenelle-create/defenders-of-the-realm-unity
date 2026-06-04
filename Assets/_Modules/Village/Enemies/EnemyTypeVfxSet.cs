// =============================================================================
// EnemyTypeVfxSet (DEF-46) — per-enemy-type VFX and audio arrays.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   A ScriptableObject that bundles the hit, death and attack VFX prefabs and
//   AudioClips for one enemy archetype (Walker, Charger, Skirmisher, Boss…).
//   Enemy.cs holds a [SerializeField] reference to one of these; if it's null
//   the enemy falls back to VfxPool built-in particles so everything degrades
//   gracefully before art assets land.
//
// USAGE:
//   1. Create an asset: Assets → Create → Defenders/Enemies/Enemy Type Vfx Set
//   2. Populate the audio and prefab arrays in the Inspector.
//   3. Assign the asset to the _typeVfxSet field on each Enemy prefab variant.
//
// NOTES:
//   * RandomHitClip / RandomDeathClip / RandomAttackClip guard against empty
//     arrays and return null when none is assigned — callers must null-check.
//   * HitVfxPrefab overrides VfxPool.SpawnHitImpact() for this type; leave it
//     null to keep using the procedural built-in particle burst.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// ScriptableObject that holds per-enemy-type VFX and audio arrays for hit,
    /// death and attack events. Assign one per enemy prefab variant (Walker,
    /// Charger, Skirmisher, Boss).
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnemyTypeVfxSet_Walker",
        menuName  = "Defenders/Enemies/Enemy Type Vfx Set")]
    public sealed class EnemyTypeVfxSet : ScriptableObject
    {
        // ── Hit ───────────────────────────────────────────────────────────────

        [Header("Hit VFX (optional — null = use VfxPool built-in burst)")]
        [Tooltip("One of these prefabs is spawned at the hit point when the enemy takes damage. " +
                 "Leave empty to use the VfxPool's built-in particle burst.")]
        [SerializeField] private GameObject[] _hitVfxPrefabs;

        [Header("Hit audio")]
        [Tooltip("One clip is chosen at random on each hit (flesh impacts, grunt, etc.).")]
        [SerializeField] private AudioClip[] _hitSounds;

        // ── Death ─────────────────────────────────────────────────────────────

        [Header("Death VFX (optional — null = use VfxPool built-in burst)")]
        [Tooltip("One of these prefabs is spawned at death. " +
                 "Leave empty to use the VfxPool's built-in death burst.")]
        [SerializeField] private GameObject[] _deathVfxPrefabs;

        [Header("Death audio")]
        [Tooltip("One clip is chosen at random on death (death cry, collapse, etc.).")]
        [SerializeField] private AudioClip[] _deathSounds;

        // ── Attack ────────────────────────────────────────────────────────────

        [Header("Attack audio")]
        [Tooltip("One clip is chosen at random on each melee contact strike.")]
        [SerializeField] private AudioClip[] _attackSounds;

        // ── Telegraph (DEF-48) ────────────────────────────────────────────────

        [Header("Attack telegraph (DEF-48)")]
        [Tooltip("Seconds of wind-up before the attack damage lands. " +
                 "0 = instant, no telegraph. ~0.4 s is readable on mobile.")]
        [SerializeField, Min(0f)] private float _telegraphDuration = 0.4f;

        [Tooltip("VFX prefab spawned at the target's position during the wind-up window " +
                 "(e.g. a red ground ring that warns the player). Destroyed after the delay. " +
                 "Leave blank for no ground warning.")]
        [SerializeField] private GameObject _telegraphVFXPrefab;

        // ── API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a random hit-VFX prefab, or null when the array is empty
        /// (caller should fall back to VfxPool.SpawnHitImpact).
        /// </summary>
        public GameObject RandomHitVfxPrefab() => PickRandom(_hitVfxPrefabs);

        /// <summary>Returns a random hit AudioClip, or null when none assigned.</summary>
        public AudioClip RandomHitClip() => PickRandom(_hitSounds);

        /// <summary>
        /// Returns a random death-VFX prefab, or null when the array is empty
        /// (caller should fall back to VfxPool.SpawnDeathBurst).
        /// </summary>
        public GameObject RandomDeathVfxPrefab() => PickRandom(_deathVfxPrefabs);

        /// <summary>Returns a random death AudioClip, or null when none assigned.</summary>
        public AudioClip RandomDeathClip() => PickRandom(_deathSounds);

        /// <summary>Returns a random attack AudioClip, or null when none assigned.</summary>
        public AudioClip RandomAttackClip() => PickRandom(_attackSounds);

        /// <summary>
        /// DEF-48: Wind-up duration before the attack damage lands.
        /// 0 = instant (no telegraph). Enemy.cs reads this before each attack.
        /// </summary>
        public float TelegraphDuration => _telegraphDuration;

        /// <summary>
        /// DEF-48: Optional VFX prefab spawned at the target's position during
        /// the wind-up window. Null when no ground-ring warning is needed.
        /// </summary>
        public GameObject TelegraphVFXPrefab => _telegraphVFXPrefab;

        // ── Helpers ───────────────────────────────────────────────────────────

        private static T PickRandom<T>(T[] array) where T : class
        {
            if (array == null || array.Length == 0) return null;
            return array[Random.Range(0, array.Length)];
        }
    }
}
