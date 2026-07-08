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
using DeNelle.Core.Diagnostics;
using DeNelle.Data;       // PetData SO — WO-86
using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>Tracks a pet's XP/level and applies its level-up stat bonuses.</summary>
    [RequireComponent(typeof(Pet))]
    [DisallowMultipleComponent]
    public sealed class PetProgression : MonoBehaviour, IXpEarner
    {
        // WO-86: hardcoded fallback values — overridden by PetData SO if assigned.
        private const float DamagePerLevelDefault = 0.07f;   // +7% per level
        private const float HpPerLevelDefault = 0.08f;       // +8% per level
        private const float MaxMultiplier = 3f;

        [Header("Data (WO-86)")]
        [Tooltip("Optional PetData SO. When assigned, damagePerLevel and hpMultiplierPerLevel are read from it instead of the hardcoded defaults.")]
        [SerializeField] private PetData _petData;

        private float DamagePerLevel => _petData != null ? _petData.damagePerLevel / 100f : DamagePerLevelDefault;
        private float HpPerLevel     => _petData != null ? _petData.hpMultiplierPerLevel   : HpPerLevelDefault;

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
            if (gained > 0)
            {
                FlowTrace.Step("PetXp", $"pet '{EarnerId ?? "<null>"}' leveled +{gained} -> Lv{_level} (xp {_xp:0}/{XpToNextFor(_level):0}, +{amount:0} this grant)");
                ApplyBonuses();
            }
            return gained;
        }

        private void ApplyBonuses()
        {
            if (_pet == null) { FlowTrace.Warn("PetXp", "ApplyBonuses skipped: _pet is null (no RequireComponent(Pet)?) — level-up stats NOT applied"); return; }
            float dmgMult = Mathf.Min(MaxMultiplier, 1f + (_level - 1) * DamagePerLevel);
            float hpMult  = Mathf.Min(MaxMultiplier, 1f + (_level - 1) * HpPerLevel);
            _pet.SetProgressionMultipliers(dmgMult, hpMult);
        }
    }
}
