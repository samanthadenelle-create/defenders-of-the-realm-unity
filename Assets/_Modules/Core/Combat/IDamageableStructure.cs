// =============================================================================
// IDamageableStructure — cross-assembly damageable target contract.
// -----------------------------------------------------------------------------
// Defined in DeNelle.Core so any assembly (Village, BattleATB, HUD, etc.)
// can reference it without depending on DeNelle.Village.
//
// Implementors: HeartController, HeroHealth, Building, Tower, Gate (all Village).
// Consumers:    EnemyBrain.TryAttack(), DragonBoss.DealStrike(), Enemy.ProbeForStructure()
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Contract for any game object that can receive contact damage from an enemy.
    /// Implemented by structures (Heart, towers, gates, buildings) and the hero.
    /// </summary>
    public interface IDamageableStructure
    {
        /// <summary>True while the structure still stands and can be attacked.</summary>
        bool IsAlive { get; }

        /// <summary>Applies <paramref name="amount"/> contact damage from an enemy hit.</summary>
        void ApplyContactDamage(float amount);
    }
}
