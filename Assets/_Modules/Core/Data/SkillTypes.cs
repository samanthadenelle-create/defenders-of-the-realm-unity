// =============================================================================
// SkillTypes — DEF-73 (Linear). The single, locked source of truth for the craft
// SkillType enum and the serializable SkillRequirement gate used by tower
// placement.
// -----------------------------------------------------------------------------
// Per DEF-73 "Clarifications Confirmed" the enum is FINAL and LOCKED: no other
// file in the project may declare `SkillType`. `Arcane` gates Mage / magical
// towers; `GatheringSpeed` is reserved for DEF-77. Both TowerData (the data) and
// SkillSystem (the runtime levels, in DeNelle.Core.Progression) reference these
// shared types, so they live in their own dedicated file in DeNelle.Core.Data —
// the namespace inside the existing DeNelle.Core asmdef (no new asmdef; see the
// issue's "Assembly References Required" — Village already references Core).
// =============================================================================

namespace DeNelle.Core.Data
{
    /// <summary>
    /// Craft skill identities. LOCKED single source of truth (DEF-73 clarification)
    /// — declared nowhere else in the project.
    /// </summary>
    public enum SkillType
    {
        None,
        Blacksmith,
        Woodworking,
        Arcane,          // gates Mage / magical towers
        GatheringSpeed   // reserved for DEF-77
    }

    /// <summary>
    /// A skill gate on a buildable: the placement is allowed only when the player's
    /// matching <see cref="SkillType"/> level is at least <see cref="minLevel"/>.
    /// A <see cref="type"/> of <see cref="SkillType.None"/> means "no requirement".
    /// </summary>
    [System.Serializable]
    public class SkillRequirement
    {
        public SkillType type;
        public int minLevel;
    }
}
