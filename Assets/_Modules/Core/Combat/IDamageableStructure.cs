// =============================================================================
// IDamageableStructure — cross-assembly damageable target contract.
// -----------------------------------------------------------------------------
// Defined in DeNelle.Core so any assembly (Village, BattleATB, HUD, etc.)
// can reference it without depending on DeNelle.Village.
//
// Implementors (18, ALL in DeNelle.Village — verified against the tree 2026-08-03,
// recounted 2026-08-21 after WO-1132, RE-COUNTED 2026-09-06 (WO-1439) which found
// HealingCaravanMobility had been missing from this list since it shipped; the original
// "HeartController, HeroHealth, Building, Tower, Gate" line named 5 of them and had been
// wrong for months. Keep this list in step with the class declarations):
//   Buildings/  Building, Tower, DefenseTower, ArcaneTower, HealingCaravanMobility
//   Progression/ResourceCollector
//   Walls/      WallSegment          Gates/  Gate          Heart/  HeartController
//   Hero/       HeroHealth           NPCs/   StoryCompanion
//   Troops/     TroopController
//   World/      Settlement, HarvestSite, OutpostHub, OutpostDefender
//   World/Camps/Outpost, RaidSpire
// (Assets/Editor/Regression also carries two throwaway test stubs — OracleStructure in
//  DataRegression.cs and StubStructure in StructureBurnRegression.cs — not counted here.)
//
// ⛔ BreakableContainer WAS on this list and was REMOVED by WO-1132 (owner ruling
// 2026-08-21) — the loot container became an OPENABLE chest, not an attackable prop. It
// implements NEITHER this interface nor IDamageable now, declares no CombatFaction, and is
// no longer relayered to "Enemy". That relayer is what made every crate a valid target for
// the hostile reticle (WO-1047), so the removal retires that defect class rather than
// filtering it. Do not re-add it here.
//
// Four of them — RaidSpire, WallSegment, Gate and DefenseTower — ALSO implement IDamageable,
// the SEPARATE seam the player/troops sweep for. The two interfaces stay disjoint: this one
// is enemy->structure contact damage and carries no position or HP, so making it inherit
// IDamageable would force those onto all 18 (including HeroHealth and HeartController).
// Dual-implement on the classes that need both instead.
//
// ⚠ FACTION IS THE ONE MEMBER THE TWO CONTRACTS DELIBERATELY SHARE (WO-1439, 2026-09-06).
// The header used to say this interface "carries no position, HP or faction", and that
// missing faction is exactly what let a raid garrison spend a whole raid destroying the
// RaidSpire it exists to guard: Enemy.ProbeForStructure / SweepForNearestStructure /
// EnemyBrain's scans filtered on null + IsAlive + "is it the hero" and NOTHING ELSE, so a
// Hostile attacker happily selected a Hostile objective. Measured, not inferred — 11,620
// `[Flow:EnemyAggro] raid*: ProbeForStructure hit 'RaidSpire'` lines in
// logs/debug/raid-ai-and-pets-2026-09-06.log, 8,359 of them AFTER
// `[Flow:World] SceneOwnership resolved 'RaidBase_raider_camp_small' -> Enemy-owned`, which
// rules the ownership machinery IN and the target test OUT.
//
// `Faction` is declared here with the IDENTICAL name and type as IDamageable.Faction, so a
// dual-implementer (RaidSpire, WallSegment, Gate, DefenseTower) satisfies BOTH contracts with
// the ONE property it already had. That COLLAPSES a source of truth rather than adding one —
// which is why this is the seam and not a raid-scene-local "whose is this?" lookup (that
// would have been a THIRD answer alongside IDamageable.Faction and DefenseTower.Allegiance,
// and this repo's dominant failure mode is duplicated state — CLAUDE.md §2, §5, §8, §16).
//
// AUTHORING RULE for a new implementor: never invent a faction. Either it IS the player's
// (hero, troops, Heart, companion, caravan, claimed outposts) => a constant Friendly, or it
// is a scene-placed structure whose side is the SCENE's => `SceneOwnership.IsEnemyOwned ?
// Hostile : Friendly`, the same expression WallSegment and Gate already use.
//
// Consumers:    EnemyBrain.TryAttack(), DragonBoss.DealStrike(), Enemy.ProbeForStructure(),
//               and CombatFactionRules.MayAttack — the ONE predicate every selection site calls.
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Contract for any game object that can receive contact damage from an enemy.
    /// Implemented by structures (Heart, towers, walls, gates, buildings, collectors,
    /// world camps/settlements) AND by the friendly bodies enemies swing at
    /// (HeroHealth, TroopController, StoryCompanion). See the file header for the full list.
    /// </summary>
    public interface IDamageableStructure
    {
        /// <summary>True while the structure still stands and can be attacked.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Which side owns this thing. An attacker must never damage an asset of its OWN
        /// faction — call <see cref="CombatFactionRules.MayAttack"/>, do not re-implement
        /// the comparison at the call site. Deliberately the same member as
        /// <see cref="IDamageable.Faction"/> so a class implementing both declares it ONCE.
        /// </summary>
        CombatFaction Faction { get; }

        /// <summary>Applies <paramref name="amount"/> contact damage from an enemy hit.</summary>
        void ApplyContactDamage(float amount);
    }
}
