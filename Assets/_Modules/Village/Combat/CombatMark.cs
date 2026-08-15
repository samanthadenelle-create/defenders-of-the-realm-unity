// =============================================================================
// CombatMark — temporary damage-taken debuff (Hunter's Mark, WO-910).
// -----------------------------------------------------------------------------
// Pure runtime table keyed by IDamageable instance id. No scene wiring.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Applies / queries temporary "takes more damage" marks on foes.</summary>
    public static class CombatMark
    {
        private struct Entry
        {
            public float Until;
            public float Mult;
        }

        private static readonly Dictionary<int, Entry> s_marks = new Dictionary<int, Entry>(32);

        /// <summary>Mark a Unity object (Enemy / IDamageable host) to take more damage.</summary>
        public static void Apply(Object target, float durationSeconds, float damageMult)
        {
            if (target == null) return;
            int id = target.GetInstanceID();
            float until = Time.time + Mathf.Max(0.1f, durationSeconds);
            float mult = Mathf.Clamp(damageMult, 1f, 2.5f);
            s_marks[id] = new Entry { Until = until, Mult = mult };
            FlowTrace.Step("CombatMark",
                $"APPLY id={id} mult={mult:F2} for {durationSeconds:0.#}s until={until:F1}");
        }

        /// <summary>Mark via IDamageable when it is a UnityEngine.Object host.</summary>
        public static void Apply(IDamageable target, float durationSeconds, float damageMult)
        {
            if (target is Object uo) Apply(uo, durationSeconds, damageMult);
        }

        /// <summary>Damage multiplier for a marked target (1 if none / expired).</summary>
        public static float DamageTakenMultiplier(Object target)
        {
            if (target == null) return 1f;
            int id = target.GetInstanceID();
            if (!s_marks.TryGetValue(id, out var e)) return 1f;
            if (Time.time > e.Until)
            {
                s_marks.Remove(id);
                return 1f;
            }
            return e.Mult;
        }

        public static float DamageTakenMultiplier(IDamageable target)
            => target is Object uo ? DamageTakenMultiplier(uo) : 1f;

        /// <summary>Scale an outgoing amount by the target's active mark.</summary>
        public static float ScaleDamage(Object target, float amount)
        {
            if (amount <= 0f) return amount;
            float m = DamageTakenMultiplier(target);
            return m > 1.001f ? amount * m : amount;
        }

        public static float ScaleDamage(IDamageable target, float amount)
            => target is Object uo ? ScaleDamage(uo, amount) : amount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() => s_marks.Clear();
    }
}
