// =============================================================================
// SpecialAbility — DEF-73 (Linear). The per-upgrade special ability a tower gains
// at a given level (referenced by TowerData.TowerUpgrade.ability).
// -----------------------------------------------------------------------------
// Extracted to its own dedicated file in DeNelle.Core.Data (DEF-73 Correction
// Pass 1, Issue 3) so the shared enum is not buried inside TowerData.cs. Abilities
// are currently data-only flags; DEF-74 logs them and a future ticket wires each
// to a runtime component.
// =============================================================================

namespace DeNelle.Core.Data
{
    /// <summary>A tower upgrade's special ability slot (data flag; wiring is future work).</summary>
    public enum SpecialAbility
    {
        None,
        SlowEnemies,
        HealAllies,
        FireAura,
        FrostNova,
        MagicalAffinity
    }

    /// <summary>
    /// The unique ability a tower unlocks at max level (Level 3) via Empowerment.
    /// Separate from <see cref="SpecialAbility"/>, which is the per-upgrade passive
    /// granted at each upgrade step. Empowerment is a one-time, irreversible prestige
    /// state chosen by the player after reaching max level.
    /// </summary>
    public enum EmpowermentAbility
    {
        /// <summary>No empowerment ability assigned (tower not empowered).</summary>
        None,
        /// <summary>Arcane Tower — every 5th shot fires a 3-bolt spread burst, each at 60 % damage.</summary>
        ManaSurge,
        /// <summary>Frost Tower — pulses an AoE slow field; all enemies in range move at 70 % speed.</summary>
        GlacialCore,
        /// <summary>Flame Tower — every hit applies a Burn DoT (4 dmg/s for 4 s, non-stacking).</summary>
        EternalEmber,
        /// <summary>Arrow Tower — acquires a second target (highest HP in range) and fires both simultaneously.</summary>
        TrueAim,
    }
}
