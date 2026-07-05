// =============================================================================
// CombatDeathDirection — resolve which death clip bucket a killing hit came from.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// Owner design (2026-07-03): hero deaths are directional relative to the hero's
// facing — front / back / left / right (+ optional assassinate later). The
// AnimatorController's DeathDir int selects the clip; this helper maps a world-
// space attacker position to the enum value ActorAnimator.Die() consumes.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Combat
{
    /// <summary>Maps a killing hit's source position to a <see cref="DeathDirection"/>.</summary>
    public static class CombatDeathDirection
    {
        /// <summary>
        /// Classify the attacker's position relative to the victim's facing.
        /// Uses a dot/cross test on the XZ plane; falls back to <see cref="DeathDirection.Fall"/>
        /// when the source is unknown or coincident with the hero.
        /// </summary>
        public static DeathDirection Resolve(Vector3 victimPosition, Vector3 victimForward,
                                             Vector3? attackerWorldPosition)
        {
            if (!attackerWorldPosition.HasValue) return DeathDirection.Fall;

            Vector3 toAttacker = attackerWorldPosition.Value - victimPosition;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.04f) return DeathDirection.Fall; // ~20 cm — no direction

            Vector3 fwd = victimForward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return DeathDirection.Fall;
            fwd.Normalize();
            toAttacker.Normalize();

            float dot = Vector3.Dot(fwd, toAttacker);
            // Cross Y > 0 => attacker on the hero's right side.
            float crossY = Vector3.Cross(fwd, toAttacker).y;

            const float frontBackBand = 0.45f; // ~63° cone for front/back vs side
            if (dot >= frontBackBand)  return DeathDirection.Front;
            if (dot <= -frontBackBand) return DeathDirection.Back;
            return crossY >= 0f ? DeathDirection.Right : DeathDirection.Left;
        }
    }
}