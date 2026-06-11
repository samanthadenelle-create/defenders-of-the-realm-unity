using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Reusable "draw attention" API. <c>Hud.Focus(target)</c> wraps the target in a chasing-glow
    /// frame (<see cref="AttentionGlow"/>); <c>Hud.Unfocus(target)</c> removes it. Source-agnostic —
    /// NPC/merchant proximity, a parry window, a low-HP heal hint, a tutorial step. Call it from
    /// anywhere (Core is referenced by every module).
    /// </summary>
    public static class Hud
    {
        private static readonly Dictionary<GameObject, AttentionGlow> _active =
            new Dictionary<GameObject, AttentionGlow>();

        /// <summary>Draw the chasing-glow frame around <paramref name="target"/> (idempotent).</summary>
        public static void Focus(GameObject target, float size = 0f, Color? tint = null, float speed = 1.4f)
        {
            if (target == null) return;
            if (_active.TryGetValue(target, out var existing) && existing != null) return;   // already focused
            if (size <= 0f) size = EstimateSize(target);
            _active[target] = AttentionGlow.Attach(target, size, tint ?? new Color(1f, 0.85f, 0.35f, 1f), speed);
        }

        /// <summary>Remove the glow frame from <paramref name="target"/> (safe if not focused).</summary>
        public static void Unfocus(GameObject target)
        {
            if (target == null) return;
            if (_active.TryGetValue(target, out var glow))
            {
                if (glow != null) Object.Destroy(glow.gameObject);
                _active.Remove(target);
            }
        }

        public static bool IsFocused(GameObject target)
            => target != null && _active.TryGetValue(target, out var g) && g != null;

        // Frame size from the target's footprint (XZ), with a little breathing room.
        private static float EstimateSize(GameObject target)
        {
            var r = target.GetComponentInChildren<Renderer>();
            if (r != null) { var b = r.bounds.size; return Mathf.Max(0.4f, Mathf.Max(b.x, b.z) * 1.25f); }
            return 1.5f;
        }
    }
}
