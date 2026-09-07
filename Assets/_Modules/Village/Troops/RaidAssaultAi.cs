// =============================================================================
// RaidAssaultAi — WO-1595 pure assault rules (phase + job + formation).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner north star (2026-09-07): breach walls → push / capture the spire; if
// aggro or being attacked prioritize staying alive; deploy and move as a
// formation (Front ahead, ranged/DPS behind, healers safe).
//
// Pure helpers live here so EditMode regressions can assert without a scene.
// TroopController / TroopDeployer call these; they do not replace NavMesh.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Assault phase — Survive/Peel beats Breach beats Push/Finish.</summary>
    public enum RaidAssaultPhase
    {
        Peel = 0,
        Breach = 1,
        Push = 2,
        Finish = 3,
    }

    /// <summary>Formation job mapped from <see cref="TroopDef.Role"/>.</summary>
    public enum RaidAssaultJob
    {
        Front = 0,
        Ranged = 1,
        Breaker = 2,
        Support = 3,
    }

    /// <summary>
    /// WO-1595 — pure selector + formation math for the raid assault loop.
    /// </summary>
    public static class RaidAssaultAi
    {
        /// <summary>Seconds after taking damage that count as "under attack" for Peel.</summary>
        public const float PeelHurtWindowSeconds = 2.5f;

        /// <summary>Leash (m) inside which a hostile unit forces Peel even without recent hurt.</summary>
        public const float PeelUnitLeashMeters = 6f;

        /// <summary>Front line sits this far ahead of the deploy point along the march axis.</summary>
        public const float FrontForwardMeters = 2.0f;

        /// <summary>Ranged / DPS hold this far behind the deploy point along the march axis.</summary>
        public const float RangedBackMeters = 3.5f;

        /// <summary>Support / healers hold farther back than ranged.</summary>
        public const float SupportBackMeters = 5.0f;

        /// <summary>Breaker sits slightly ahead of Front while cracking the approach.</summary>
        public const float BreakerForwardMeters = 1.25f;

        /// <summary>Lateral spacing between same-role slots (m).</summary>
        public const float LateralSpreadMeters = 1.4f;

        /// <summary>Map authored <c>TroopDef.Role</c> → assault job (no new JSON field).</summary>
        public static RaidAssaultJob JobFromRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return RaidAssaultJob.Front;
            switch (role.Trim().ToLowerInvariant())
            {
                case "ranged":
                case "caster":
                case "mage":
                    return RaidAssaultJob.Ranged;
                case "siege":
                    return RaidAssaultJob.Breaker;
                case "support":
                case "healer":
                    return RaidAssaultJob.Support;
                case "tank":
                case "melee":
                default:
                    return RaidAssaultJob.Front;
            }
        }

        /// <summary>
        /// Signed meters along the march axis (toward objective). Positive = ahead of deploy.
        /// </summary>
        public static float ForwardOffsetMeters(RaidAssaultJob job)
        {
            switch (job)
            {
                case RaidAssaultJob.Ranged: return -RangedBackMeters;
                case RaidAssaultJob.Support: return -SupportBackMeters;
                case RaidAssaultJob.Breaker: return BreakerForwardMeters;
                case RaidAssaultJob.Front:
                default: return FrontForwardMeters;
            }
        }

        /// <summary>
        /// World-space formation offset from a tap/deploy origin.
        /// <paramref name="marchForward"/> should be flat (y=0) and normalized toward the goal.
        /// </summary>
        public static Vector3 FormationWorldOffset(
            RaidAssaultJob job, int stackIndex, Vector3 marchForward, float lateralSpread = LateralSpreadMeters)
        {
            Vector3 forward = marchForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            float along = ForwardOffsetMeters(job);
            // Alternate left/right: 0 → 0, 1 → +1, 2 → -1, 3 → +2, …
            float lane = 0f;
            if (stackIndex > 0)
            {
                int n = (stackIndex + 1) / 2;
                lane = ((stackIndex % 2) == 1 ? 1f : -1f) * n;
            }

            return forward * along + right * (lane * lateralSpread);
        }

        /// <summary>
        /// Bias a push/breach destination so back-line jobs hold standoff behind the objective
        /// approach (Front closes; Ranged/Support stop short).
        /// </summary>
        public static Vector3 BiasMoveDestination(
            RaidAssaultJob job, Vector3 selfPos, Vector3 objectiveOrFoePos, float attackRange)
        {
            if (job == RaidAssaultJob.Front || job == RaidAssaultJob.Breaker)
                return objectiveOrFoePos;

            Vector3 flatSelf = new Vector3(selfPos.x, 0f, selfPos.z);
            Vector3 flatGoal = new Vector3(objectiveOrFoePos.x, 0f, objectiveOrFoePos.z);
            Vector3 toGoal = flatGoal - flatSelf;
            float dist = toGoal.magnitude;
            if (dist < 0.01f) return objectiveOrFoePos;

            float standoff = job == RaidAssaultJob.Support
                ? Mathf.Max(attackRange + SupportBackMeters - RangedBackMeters, attackRange)
                : Mathf.Max(attackRange * 0.85f, attackRange - 0.5f);

            if (dist <= standoff) return selfPos; // already far enough — hold

            float stopAt = dist - standoff;
            Vector3 hold = flatSelf + (toGoal / dist) * stopAt;
            hold.y = objectiveOrFoePos.y;
            return hold;
        }

        /// <summary>
        /// Priority stack: Peel → Breach → Finish → Push.
        /// </summary>
        public static RaidAssaultPhase ResolvePhase(
            bool peelThreat,
            bool routeToObjectiveOpen,
            bool objectiveInAttackRange)
        {
            if (peelThreat) return RaidAssaultPhase.Peel;
            if (!routeToObjectiveOpen) return RaidAssaultPhase.Breach;
            if (objectiveInAttackRange) return RaidAssaultPhase.Finish;
            return RaidAssaultPhase.Push;
        }

        /// <summary>
        /// Idle (no foe) destination rule — CLI review 2026-09-07.
        /// Rally flag beats spire push. Spire push only on Push/Finish (never Breach —
        /// marching into an intact wall with NoObstacleAvoidance freezes the troop).
        /// </summary>
        public static bool IdleShouldPushSpire(bool rallyPointSet, RaidAssaultPhase phase)
        {
            if (rallyPointSet) return false;
            return phase == RaidAssaultPhase.Push || phase == RaidAssaultPhase.Finish;
        }

        /// <summary>
        /// Whether a hostile UNIT should beat structures for this phase.
        /// Peel always takes the unit when one exists. Push/Finish only peel units already
        /// in attack range (stay alive locally) — otherwise the objective wins over walls.
        /// Breach keeps the WO-1438 reachability gate for non-siege.
        /// </summary>
        public static bool PreferUnit(
            RaidAssaultPhase phase,
            bool preferStructures,
            bool hasUnit,
            bool hasStruct,
            bool unitInAttackRange,
            bool routeToUnitOpen)
        {
            if (!hasUnit) return false;
            if (phase == RaidAssaultPhase.Peel) return true;

            if (phase == RaidAssaultPhase.Push || phase == RaidAssaultPhase.Finish)
            {
                // Local survival only — do not chase distant units instead of the spire.
                return unitInAttackRange;
            }

            // Breach: siege stays on masonry; others use the existing reachability rule.
            if (preferStructures) return false;
            if (!hasStruct) return true;
            return unitInAttackRange || routeToUnitOpen;
        }

        /// <summary>
        /// After a hole exists (Push/Finish), non-siege must not farm the wall ring.
        /// Breach (and siege Breaker in Breach) may still pick approach structures.
        /// The objective (spire) is never "wall ring" — callers pass hasObjective separately.
        /// </summary>
        public static bool AllowNonObjectiveStructure(
            RaidAssaultPhase phase, bool preferStructures)
        {
            if (phase == RaidAssaultPhase.Breach) return true;
            // Siege may keep structure bias in Peel only if no unit (PreferUnit already handled);
            // in Push/Finish even siege joins the push — no ring farming.
            if (preferStructures && phase == RaidAssaultPhase.Peel) return true;
            return false;
        }

        /// <summary>
        /// Pick among unit / objective / other-structure / none after buckets are measured.
        /// Returns which bucket wins: 0=unit, 1=objective, 2=otherStruct, -1=none.
        /// </summary>
        public static int PickBucket(
            RaidAssaultPhase phase,
            bool preferStructures,
            bool hasUnit,
            bool hasObjective,
            bool hasOtherStruct,
            bool unitInAttackRange,
            bool routeToUnitOpen)
        {
            bool preferUnit = PreferUnit(
                phase, preferStructures, hasUnit, hasOtherStruct || hasObjective,
                unitInAttackRange, routeToUnitOpen);

            if (preferUnit && hasUnit) return 0;

            // Live gate for wall-ring farm: Push/Finish refuse non-objective masonry unless
            // AllowNonObjectiveStructure says otherwise (Breach / peel-siege only).
            bool mayWall = hasOtherStruct
                && AllowNonObjectiveStructure(phase, preferStructures);

            if (phase == RaidAssaultPhase.Push || phase == RaidAssaultPhase.Finish)
            {
                if (hasObjective) return 1;
                if (hasUnit) return 0; // nothing else — engage any unit rather than idle
                if (mayWall) return 2;
                return -1; // do not pick ring walls
            }

            if (phase == RaidAssaultPhase.Peel)
            {
                if (hasUnit) return 0;
                if (hasObjective) return 1;
                // Hurt by a tower with no unit in leash — do NOT resume wall-ring farming.
                return -1;
            }

            // Breach
            if (preferStructures)
            {
                if (mayWall) return 2;
                if (hasObjective) return 1;
                return hasUnit ? 0 : -1;
            }

            if (hasUnit && unitInAttackRange) return 0;
            if (mayWall) return 2;
            if (hasObjective) return 1;
            return hasUnit ? 0 : -1;
        }
    }
}
