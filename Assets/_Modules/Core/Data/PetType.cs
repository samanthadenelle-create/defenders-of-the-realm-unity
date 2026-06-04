// =============================================================================
// PetType — DEF-57: ScriptableObject stub for pet species data.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Data
//
// Placeholder SO authored in the Inspector. Full pet system (abilities, skill
// tree, synergies) is a separate ticket. This stub exists so PetAnimatorController
// and PetDeployer can reference a typed asset without a compiler error.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Data
{
    /// <summary>
    /// Data container for a pet species. Create via
    /// Assets → Create → Defenders/Pet Type.
    /// Expand when the full pet system ticket is filed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPetType", menuName = "Defenders/Pet Type")]
    public sealed class PetType : ScriptableObject
    {
        [Tooltip("Display name shown in the UI.")]
        public string petName;

        [Tooltip("World units per second at full locomotion.")]
        [Min(0.1f)] public float moveSpeed = 4f;

        [Tooltip("Distance within which the pet can attack.")]
        [Min(0.1f)] public float attackRange = 1.5f;

        [Tooltip("Flat damage per attack (before talent modifiers).")]
        [Min(0f)] public float damage = 5f;

        [Tooltip("Minimum seconds between attacks.")]
        [Min(0.1f)] public float attackCooldown = 1.2f;

        // Expand when pet system ticket is filed:
        // public Sprite portrait;
        // public PetAbility[] abilities;
        // public PetSkillTree skillTree;
    }
}
