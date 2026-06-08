// =============================================================================
// OrientationValidator — WO-363 character orientation hard gate (pure logic).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Validation
//
// RULE (WO-363): a character's FACING must match its movement INPUT.
//   UP    (input +Z) -> face up    (+Z)
//   DOWN  (input -Z) -> face down  (-Z)
//   LEFT  (input -X) -> face left  (-X)
//   RIGHT (input +X) -> face right (+X)
//   IDLE  (no input) -> preserve last facing (no assertion — never a failure)
// A ~30° tolerance is allowed so smooth (slerp) rotation in-flight isn't flagged.
//
// This is the PURE, engine-light core of the gate: it takes a facing Vector3 and
// an input Vector3 and answers "does facing match input within tolerance?". It is
// deliberately generic so it applies identically to the hero, companions, pets,
// and enemies — anything that moves and faces a direction. The MonoBehaviour
// runtime guard (OrientationGuard) and the EditMode unit tests both call into
// THIS so there is one source of truth for the rule.
//
// Both facing and input are flattened to the XZ plane (Y ignored) because the
// game's locomotion + facing all live in XZ (see HeroLocomotion / Pet). Only the
// horizontal heading is meaningful for "are you facing where you're walking".
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Validation
{
    /// <summary>
    /// Pure, engine-light validator for the WO-363 rule: a character's facing must
    /// match its movement input (within a tolerance for smooth rotation). Generic —
    /// takes a facing <see cref="Vector3"/> + an input <see cref="Vector3"/>, so it
    /// applies to the hero, companions, pets and enemies alike. The runtime guard
    /// (<c>OrientationGuard</c>) and the EditMode tests both call into this so the
    /// rule has a single source of truth.
    /// </summary>
    public static class OrientationValidator
    {
        /// <summary>
        /// Default angular tolerance (degrees) between facing and input before the
        /// gate fails. ~30° covers an in-flight slerp toward the target heading
        /// (smooth rotation) while still catching a real mismatch (e.g. facing the
        /// camera-relative direction instead of the input direction).
        /// </summary>
        public const float DefaultToleranceDegrees = 30f;

        /// <summary>Magnitude below which an input/facing vector is treated as "none".</summary>
        public const float Epsilon = 1e-4f;

        /// <summary>
        /// True when <paramref name="input"/> represents an actual movement command
        /// (non-zero on the XZ plane). When false, the character is idle and facing
        /// is NOT asserted (idle preserves the last facing — see <see cref="IsValid"/>).
        /// </summary>
        public static bool HasInput(Vector3 input)
        {
            input.y = 0f;
            return input.sqrMagnitude > Epsilon * Epsilon;
        }

        /// <summary>
        /// The XZ-plane angle (degrees) between <paramref name="facing"/> and
        /// <paramref name="input"/>. Returns 0 when either flattened vector is ~zero
        /// (no meaningful heading to compare — treated as "aligned" / not a failure).
        /// </summary>
        public static float AngleBetween(Vector3 facing, Vector3 input)
        {
            facing.y = 0f;
            input.y = 0f;
            if (facing.sqrMagnitude <= Epsilon * Epsilon) return 0f;
            if (input.sqrMagnitude <= Epsilon * Epsilon) return 0f;
            return Vector3.Angle(facing, input);
        }

        /// <summary>
        /// Validates the WO-363 rule for one sample. Returns true (PASS) when either:
        ///   (a) there is no movement input — idle preserves last facing, never a fail; or
        ///   (b) the XZ angle between facing and input is within <paramref name="toleranceDegrees"/>.
        /// Returns false (GATE-FAILED) only when the character is actively moving and
        /// its facing diverges from the input direction beyond tolerance.
        /// </summary>
        /// <param name="facing">The character's current facing/forward (e.g. transform.forward).</param>
        /// <param name="input">The movement input direction (e.g. new Vector3(inputX, 0, inputY)).</param>
        /// <param name="toleranceDegrees">Allowed angular slack; defaults to <see cref="DefaultToleranceDegrees"/>.</param>
        public static bool IsValid(Vector3 facing, Vector3 input,
                                   float toleranceDegrees = DefaultToleranceDegrees)
        {
            if (!HasInput(input)) return true;            // idle — preserve last facing
            return AngleBetween(facing, input) <= toleranceDegrees;
        }

        /// <summary>
        /// As <see cref="IsValid"/> but also yields a human-readable reason when the
        /// gate fails (for a clear GATE-FAILED log). On pass, <paramref name="reason"/>
        /// is empty.
        /// </summary>
        public static bool TryValidate(Vector3 facing, Vector3 input,
                                       out string reason,
                                       float toleranceDegrees = DefaultToleranceDegrees)
        {
            if (!HasInput(input)) { reason = string.Empty; return true; }

            float angle = AngleBetween(facing, input);
            if (angle <= toleranceDegrees) { reason = string.Empty; return true; }

            Vector3 f = facing; f.y = 0f;
            Vector3 i = input;  i.y = 0f;
            reason = $"facing {Flat(f)} vs input {Flat(i)} differ by {angle:F1}° " +
                     $"(> {toleranceDegrees:F0}° tolerance) — character is not facing its movement direction";
            return false;
        }

        private static string Flat(Vector3 v)
        {
            v = v.sqrMagnitude > Epsilon * Epsilon ? v.normalized : v;
            return $"({v.x:F2},{v.z:F2})";
        }
    }
}
