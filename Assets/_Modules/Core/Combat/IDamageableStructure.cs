// =============================================================================
// IDamageableStructure — cross-assembly damageable target contract.
// -----------------------------------------------------------------------------
// Defined in DeNelle.Core so any assembly (Village, BattleATB, HUD, etc.)
// can reference it without depending on DeNelle.Village.
//
// Implementors (18, ALL in DeNelle.Village — verified against the tree 2026-08-03;
// the previous "HeartController, HeroHealth, Building, Tower, Gate" line named 5 of them
// and had been wrong for months. Keep this list in step with the class declarations):
//   Buildings/  Building, Tower, DefenseTower, ArcaneTower
//   Progression/ResourceCollector
//   Walls/      WallSegment          Gates/  Gate          Heart/  HeartController
//   Hero/       HeroHealth           NPCs/   StoryCompanion
//   Troops/     TroopController
//   World/      Settlement, HarvestSite, OutpostHub, OutpostDefender, BreakableContainer
//   World/Camps/Outpost, RaidSpire
// (Assets/Editor/Regression also carries two throwaway test stubs — OracleStructure in
//  DataRegression.cs and StubStructure in StructureBurnRegression.cs — not counted here.)
//
// Four of them — BreakableContainer, RaidSpire, WallSegment and Gate — ALSO implement
// IDamageable, the SEPARATE seam the player/troops sweep for. The two interfaces are deliberately
// disjoint: this one is enemy->structure contact damage and carries no position, HP or
// faction, so making it inherit IDamageable would force those onto all 18 (including
// HeroHealth and HeartController). Dual-implement on the classes that need both instead.
//
// Consumers:    EnemyBrain.TryAttack(), DragonBoss.DealStrike(), Enemy.ProbeForStructure()
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Contract for any game object that can receive contact damage from an enemy.
    /// Implemented by structures (Heart, towers, walls, gates, buildings, collectors,
    /// world camps/settlements, breakables) AND by the friendly bodies enemies swing at
    /// (HeroHealth, TroopController, StoryCompanion). See the file header for the full list.
    /// </summary>
    public interface IDamageableStructure
    {
        /// <summary>True while the structure still stands and can be attacked.</summary>
        bool IsAlive { get; }

        /// <summary>Applies <paramref name="amount"/> contact damage from an enemy hit.</summary>
        void ApplyContactDamage(float amount);
    }
}
