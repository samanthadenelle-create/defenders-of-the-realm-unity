// =============================================================================
// PetData — WO-86. ScriptableObject for pet balance stats and aura settings.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Data   Namespace: DeNelle.Data
//
// Create assets via Assets > Create > Defenders/Data/Pet.
//
// NOTE: PetType (in DeNelle.Core.Data) remains the canonical species reference
// used by PetDeployer. PetData is the BALANCE LAYER used by Pet.cs to override
// hardcoded constants at runtime. When PetData is null, Pet.cs falls back to its
// existing hardcoded values.
//
// ORPHANED FIELDS (WO-993, 2026-08-16): damagePerLevel + hpMultiplierPerLevel are
// NO LONGER READ BY ANYTHING. Their only reader was PetProgression.cs, deleted with
// the pet levelling surface (owner ruling "same with pet progression"). They are kept
// rather than removed because they are SERIALIZED fields on shipped .asset files -
// deleting them is a data migration, not a code retirement. Do not wire them back up
// without an owner ruling that re-opens pet levelling.
// =============================================================================

using UnityEngine;

namespace DeNelle.Data
{
    [CreateAssetMenu(fileName = "NewPetData", menuName = "Defenders/Data/Pet")]
    public class PetData : ScriptableObject
    {
        [Header("Identity")]
        public string petName               = "Flamepup";
        public Sprite icon;

        [Header("Combat")]
        public int   damage                 = 9;
        public float attackRange            = 9f;
        public float attackCooldown         = 2.2f;

        [Header("Leveling")]
        public int   xpPerLevel             = 300;
        public int   maxLevel               = 10;
        [Tooltip("Flat damage added per level (stacks on top of base damage).")]
        public float damagePerLevel         = 1.5f;
        [Tooltip("HP multiplier gained per level (e.g. 0.08 = +8% per level).")]
        public float hpMultiplierPerLevel   = 0.08f;

        [Header("Aura (WO-58)")]
        public float level1EmissionRate     = 4f;
        public float level3EmissionRate     = 14f;
        public float level5EmissionRate     = 28f;
        public bool  enableOrbitSparksAtL5  = true;
    }
}
