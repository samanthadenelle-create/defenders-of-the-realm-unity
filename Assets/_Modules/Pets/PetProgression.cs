// =============================================================================
// PetProgression — a deployed pet's XP / level and its level-up stat bonuses.
// -----------------------------------------------------------------------------
// An IXpEarner on each pet: ProgressionManager (DeNelle.Village) feeds it its
// share of kill-XP through the Core XpEarnerRegistry seam — neither gameplay
// module references the other. It levels on the same growing curve as the hero
// (level*85 + 55) and on each level boosts the Pet's attack damage and max HP
// via Pet.SetProgressionMultipliers. Pets earn STATS only (Wisdom is the hero's
// talent currency and lives across the asmdef boundary).
//
// Registers under the pet's id (PetId) — the same id Pet attributes its hits to.
// Attached by PetDeployer right after Configure() so PetId is already set when
// this enables and registers. In-memory for a run.
// =============================================================================

using DeNelle.Core.Progression;
using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>Tracks a pet's XP/level and applies its level-up stat bonuses.</summary>
    [RequireComponent(typeof(Pet))]
    [DisallowMultipleComponent]
    public sealed class PetProgression : MonoBehaviour, IXpEarner
    {
        private const float DamagePerLevel = 0.07f;   // +7% pet damage per level
        private const float HpPerLevel = 0.08f;       // +8% pet max HP per level
        private const float MaxMultiplier = 3f;

        [SerializeField] private int _level = 1;
        [SerializeField] private float _xp;
        [SerializeField] private float _lifetimeXp;

        private Pet _pet;

        public string EarnerId => _pet != null ? _pet.PetId : null;
        public int Level => _level;
        public Vector3 WorldPosition => transform.position + Vector3.up * 1.8f;

        private static float XpToNextFor(int level) => level * 85f + 55f;

        private void Awake() => _pet = GetComponent<Pet>();

        private void OnEnable()
        {
            if (_pet == null) _pet = GetComponent<Pet>();
            XpEarnerRegistry.Register(this);
            ApplyBonuses();   // re-assert the level-1 (or restored) stats
        }

        private void OnDisable() => XpEarnerRegistry.Unregister(this);

        public int AddXp(float amount)
        {
            if (amount <= 0f) return 0;
            _xp += amount;
            _lifetimeXp += amount;

            int gained = 0;
            while (_xp >= XpToNextFor(_level))
            {
                _xp -= XpToNextFor(_level);
                _level++;
                gained++;
            }
            if (gained > 0) ApplyBonuses();
            return gained;
        }

        private void ApplyBonuses()
        {
            if (_pet == null) return;
            float dmgMult = Mathf.Min(MaxMultiplier, 1f + (_level - 1) * DamagePerLevel);
            float hpMult  = Mathf.Min(MaxMultiplier, 1f + (_level - 1) * HpPerLevel);
            _pet.SetProgressionMultipliers(dmgMult, hpMult);
        }
    }
}
