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
}
