// =============================================================================
// AbilityData — WO-86. ScriptableObject for hero ability balance stats.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Data   Namespace: DeNelle.Data
//
// Create assets via Assets > Create > Defenders/Data/Ability.
// Hero ability scripts read from this SO at Start(). Falls back to hardcoded
// values when null.
//
// VFXType lives in DeNelle.Village (which references DeNelle.Data), so VFX
// fields are stored as int indices to avoid a circular assembly reference.
// =============================================================================

using UnityEngine;

namespace DeNelle.Data
{
    [CreateAssetMenu(fileName = "NewAbilityData", menuName = "Defenders/Data/Ability")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        public string  abilityName          = "Fireball";
        public Sprite  icon;
        public string  description;

        [Header("Stats")]
        public int     damage               = 35;
        public float   cooldown             = 5f;
        public float   range                = 10f;
        public float   aoeRadius            = 0f;       // 0 = single target

        [Header("Timing")]
        public float   windupDuration       = 0.18f;
        public float   castDuration         = 0.35f;

        [Header("Cost")]
        public int     manaCost             = 0;        // Reserved for future mana system

        [Header("Feedback")]
        public float   hitStopDuration      = 0.06f;

        // VFX enum indices — cast to VFXType in DeNelle.Village consumers.
        [Header("VFX (int indices matching VFXType enum)")]
        [Tooltip("VFXType enum value for the projectile travel effect.")]
        public int     projectileVFXIndex   = 0;
        [Tooltip("VFXType enum value for the impact effect.")]
        public int     impactVFXIndex       = 0;
        [Tooltip("VFXType enum value for the windup/cast effect.")]
        public int     windupVFXIndex       = 0;
    }
}
