// =============================================================================
// IDamageable — the shared combat-target contract (cross-module).
// -----------------------------------------------------------------------------
// Module-isolation seam (port spec Part 2): a gameplay module's asmdef must NOT
// reference another gameplay module's asmdef. Pets need to attack Village
// enemies, but DeNelle.Pets cannot reference DeNelle.Village (and vice versa).
//
// The resolution: a tiny, behaviourless contract in DeNelle.Core — which BOTH
// modules already reference. The Village Enemy implements IDamageable; a pet's
// attack code talks only to IDamageable, never to the concrete Enemy type.
// Same pattern the React project uses with the village combat registry: the
// pet AI reads abstract "enemy runtime" rows, never a concrete component.
//
// This interface deliberately carries NO Unity-scene coupling beyond a world
// position — it is pure data + two verbs. Anything that can be attacked in the
// village (enemies now; destructible props later) implements it.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Faction of a combat target — lets a pet / ability tell friend from foe
    /// without referencing a concrete component from another module. Pets and
    /// the hero attack <see cref="CombatFaction.Hostile"/> targets only.
    /// </summary>
    public enum CombatFaction
    {
        /// <summary>Village-side: the hero, pets, buildings, walls, the Heart.</summary>
        Friendly = 0,
        /// <summary>Hollow Ones — anything a defender should attack.</summary>
        Hostile = 1,
    }

    /// <summary>
    /// The cross-module contract for "a thing that can be attacked". Implemented
    /// by <c>DeNelle.Village.Enemy</c>; consumed by <c>DeNelle.Pets.Pet</c> and
    /// the hero's ability code. Lives in DeNelle.Core so neither gameplay module
    /// has to reference the other (port spec Part 2 — asmdef isolation).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Faction — defenders attack <see cref="CombatFaction.Hostile"/> targets.</summary>
        CombatFaction Faction { get; }

        /// <summary>World position of the target — used for range / nearest-target queries.</summary>
        Vector3 WorldPosition { get; }

        /// <summary>Current hit points. Non-positive means the target is dead.</summary>
        float Hp { get; }

        /// <summary>True while the target is alive and a valid attack candidate.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Applies <paramref name="amount"/> damage to this target. The target
        /// itself owns the consequences (death, hit-flash, on-hit FX).
        /// </summary>
        /// <param name="amount">Damage on the 0–100 HP scale.</param>
        /// <param name="element">Element of the damage source — used for resist / bonus math.</param>
        void TakeDamage(float amount, DamageElement element);

        /// <summary>
        /// Applies a status effect (slow / freeze / burn) for <paramref name="seconds"/>.
        /// A no-op on targets that do not model the effect.
        /// </summary>
        /// <param name="effect">Which status to apply.</param>
        /// <param name="seconds">How long the effect lasts.</param>
        void ApplyStatus(StatusEffect effect, float seconds);
    }

    /// <summary>Elemental flavour of a damage source — Mage / pet element wheel.</summary>
    public enum DamageElement
    {
        /// <summary>No element — plain physical damage.</summary>
        None = 0,
        /// <summary>Aether — the Heart's own light.</summary>
        Aether = 1,
        /// <summary>Flame.</summary>
        Flame = 2,
        /// <summary>Ice / frost.</summary>
        Ice = 3,
    }

    /// <summary>A timed status a defender can inflict — mirrors the React enemy-runtime flags.</summary>
    public enum StatusEffect
    {
        /// <summary>Movement slow — Ice Wolf's Frostbite, Ranger snare.</summary>
        Slow = 0,
        /// <summary>Frozen solid — Mage Frost Nova, Ice Wolf Glacial Bond.</summary>
        Freeze = 1,
        /// <summary>Damage-over-time burn — Flame Pup's Emberbite.</summary>
        Burn = 2,
    }
}
