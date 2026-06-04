// =============================================================================
// FormationType — dynamic pack formation shapes (WO-146 / Monster Family arch).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// The 5 dynamic shapes a Monster Family holds as a CONTINUOUSLY-TRACKED slot
// field in leader-local space. DISTINCT from Village's SpawnFormation
// (Line/Wedge/Scattered), which is a ONE-SHOT spawn-time spread — different
// lifetime, different math. Named per docs/MONSTER_FAMILY_ARCHITECTURE.md §3.
//
// Lives in Core so a future HUD / save / SO can reference it. Append-only —
// do not renumber (serialized).
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>
    /// The dynamic formation a monster family holds, tracked relative to the
    /// moving leader (not the goal). Maps from a high-level posture (roam /
    /// engage / flee) chosen by the leader.
    /// </summary>
    public enum FormationType
    {
        /// <summary>Roam — members spread on a loose ring around the leader.</summary>
        LooseRing = 0,

        /// <summary>Engage — arrowhead opening behind the leader (leader is the tip).</summary>
        Wedge = 1,

        /// <summary>Wide engage — skirmish row abreast of the leader (broad front).</summary>
        Line = 2,

        /// <summary>Flee / protect — dense cluster bunched close behind the leader.</summary>
        TightPack = 3,

        /// <summary>Corridor — single-file column behind the leader (chokes / bridges).</summary>
        Column = 4,
    }

    /// <summary>
    /// The high-level posture that selects a <see cref="FormationType"/>. Separate
    /// from EnemyTacticalState (per-enemy posture) and AwarenessState (perception).
    /// </summary>
    public enum FormationContext
    {
        /// <summary>No engage target — wandering as a group.</summary>
        Roam = 0,

        /// <summary>A target is committed — charge / spread to attack.</summary>
        Engage = 1,

        /// <summary>Retreating — bunch up to protect the weak member.</summary>
        Flee = 2,
    }
}
