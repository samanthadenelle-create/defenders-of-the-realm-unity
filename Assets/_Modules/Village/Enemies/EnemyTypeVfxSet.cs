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

        // ── Ranged cast Hovl VFX (WO-VFX-RANGED) ──────────────────────────────
        // String keys into HovlVfxCatalog for this archetype's rooted ranged cast
        // (muzzle flash, travelling projectile, impact). Default to the Arcane set so a
        // caster with no per-type override still reads as an arcane orb. A fire/ice enemy
        // type overrides these to Fireball_*/Frost_* (+ a matching tint).

        // DEFAULTS ARE THE OWNER-TAGGED ONES (2026-08-16). These four initializers used
        // to read Arcane_* / violet while Enemy.cs's own no-set fallback read the
        // owner-tagged Fire_Cast / PP_FireBall / FireballImpact_Impact / fire orange.
        // That divergence was harmless only while NO enemy ever had a set; the moment
        // EnemyTypeVfxLibrary started resolving one for every enemy, an un-authored set
        // would have silently RE-SKINNED every caster from fire to arcane - a creative
        // substitution no owner tagged. The two are now one value, declared here and
        // consumed by Enemy.cs's fallback constants, so they cannot drift again.

        /// <summary>Owner-tagged default HovlVfxCatalog key for the ranged cast flash.</summary>
        public const string DefaultCastVfxKey = "Fire_Cast";
        /// <summary>Owner-tagged default HovlVfxCatalog LOOP key for the travelling projectile.</summary>
        public const string DefaultProjectileVfxKey = "PP_FireBall";
        /// <summary>Owner-tagged default HovlVfxCatalog key for the ranged impact burst.</summary>
        public const string DefaultImpactVfxKey = "FireballImpact_Impact";

        /// <summary>Owner-tagged default HDR recolour for the ranged-cast FX (fire orange).</summary>
        public static readonly Color DefaultRangedVfxTint = new Color(1f, 0.55f, 0.15f, 1f);

        [Header("Ranged cast Hovl VFX (WO-VFX-RANGED)")]
        [Tooltip("HovlVfxCatalog key for the muzzle/cast flash at the caster's hands.")]
        [SerializeField] private string _castVfxKey = DefaultCastVfxKey;

        [Tooltip("HovlVfxCatalog LOOP key for the travelling projectile.")]
        [SerializeField] private string _projectileVfxKey = DefaultProjectileVfxKey;

        [Tooltip("HovlVfxCatalog key for the impact burst where the orb lands.")]
        [SerializeField] private string _impactVfxKey = DefaultImpactVfxKey;

        [Tooltip("HDR recolour applied to the ranged-cast Hovl FX (colourblind: reads by motion/shape).")]
        [SerializeField] private Color _rangedVfxTint = new Color(1f, 0.55f, 0.15f, 1f); // fire orange

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

        /// <summary>WO-VFX-RANGED: HovlVfxCatalog key for the ranged-cast muzzle/cast flash.</summary>
        public string CastVfxKey => _castVfxKey;

        /// <summary>WO-VFX-RANGED: HovlVfxCatalog LOOP key for the travelling ranged-cast projectile.</summary>
        public string ProjectileVfxKey => _projectileVfxKey;

        /// <summary>WO-VFX-RANGED: HovlVfxCatalog key for the ranged-cast impact burst.</summary>
        public string ImpactVfxKey => _impactVfxKey;

        /// <summary>WO-VFX-RANGED: HDR recolour applied to the ranged-cast Hovl FX.</summary>
        public Color RangedVfxTint => _rangedVfxTint;

        // ── Helpers ───────────────────────────────────────────────────────────

        private static T PickRandom<T>(T[] array) where T : class
        {
            if (array == null || array.Length == 0) return null;
            return array[Random.Range(0, array.Length)];
        }
    }
}
