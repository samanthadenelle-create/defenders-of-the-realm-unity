// =============================================================================
// TutorialWorldAnchors — world-anchor resolution for Tutorial V2 (WO-T1/T2).
// -----------------------------------------------------------------------------
// Two jobs:
//   1. hero.reached:<anchor> positions — TryResolveAnchor("sylas_anchor" /
//      "hub_anchor") for TutorialFlow's proximity probe.
//   2. Registry resolvers — registers "world.sylas" and "world.gate_direction"
//      as LAZY resolvers in TutorialHighlightRegistry (targets that spawn late
//      or move; registration points cited below).
//
// Resolution order for Sylas: the walk-up companion-introducer NPC ("Companion-
// Introducer", spawned by CastleCompanionIntroducerInjector.cs:223 — the live
// Sylas body in the hub), else any GameObject named *Sylas*, else the nearest
// wave gate (WaveSpawnPoint — "the scout BY THE GATE"), else invalid (the flow
// degrades: no spotlight, proximity waits, watchdog self-reports).
// "world.gate_direction" = the WaveSpawnPoint nearest the hero at resolve time
// (the same nearest-gate rule the legacy director used).
// Results are cached with a short TTL — resolvers run per-frame under the
// spotlight, but a scene scan should not.
// =============================================================================

using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Resolves the tutorial's world anchors + registers the world
    /// highlight resolvers. Added alongside <see cref="TutorialFlow"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialWorldAnchors : MonoBehaviour
    {
        private const float CacheSeconds = 2f;

        private static Transform _sylasCache;
        private static float _sylasCachedAt = float.NegativeInfinity;
        private static Transform _gateCache;
        private static float _gateCachedAt = float.NegativeInfinity;

        private void OnEnable()
        {
            // Registration points (WO-T2 world targets):
            TutorialHighlightRegistry.RegisterResolver("world.sylas",
                () => new HighlightTarget(ResolveSylas()));
            TutorialHighlightRegistry.RegisterResolver("world.gate_direction",
                () => new HighlightTarget(ResolveNearestGate()));
        }

        private void OnDisable()
        {
            TutorialHighlightRegistry.Unregister("world.sylas");
            TutorialHighlightRegistry.Unregister("world.gate_direction");
        }

        /// <summary>Position of a named step anchor. False when unresolvable this frame.</summary>
        public static bool TryResolveAnchor(string anchorId, out Vector3 pos)
        {
            pos = default;
            if (string.IsNullOrEmpty(anchorId)) return false;

            switch (anchorId.ToLowerInvariant())
            {
                case "sylas_anchor":
                {
                    var t = ResolveSylas();
                    if (t == null) return false;
                    pos = t.position;
                    return true;
                }
                case "hub_anchor":
                {
                    // Home = the Heart of Elarion (scene centre 0,0,0 by canon §7);
                    // prefer the live HeartController, fall back to the canon origin.
                    var heart = FindAnyObjectByType<HeartController>();
                    pos = heart != null ? heart.transform.position : Vector3.zero;
                    return true;
                }
                default:
                    return false;
            }
        }

        // ── Sylas ─────────────────────────────────────────────────────────────

        private static Transform ResolveSylas()
        {
            if (Time.unscaledTime - _sylasCachedAt < CacheSeconds && _sylasCache != null)
                return _sylasCache;
            _sylasCachedAt = Time.unscaledTime;

            // 1. The walk-up introducer NPC (the live Sylas body in the castle hub).
            var go = GameObject.Find("CompanionIntroducer");
            if (go == null)
            {
                // 2. Any explicitly-named Sylas object (future dedicated NPC spawn).
                go = GameObject.Find("Sylas");
            }
            _sylasCache = go != null ? go.transform
                                     : ResolveNearestGate();   // 3. "the scout by the gate"
            return _sylasCache;
        }

        // ── Nearest gate to the hero ──────────────────────────────────────────

        private static Transform ResolveNearestGate()
        {
            if (Time.unscaledTime - _gateCachedAt < CacheSeconds && _gateCache != null)
                return _gateCache;
            _gateCachedAt = Time.unscaledTime;

            var hero = FindAnyObjectByType<HeroLocomotion>();
            Vector3 from = hero != null ? hero.transform.position : Vector3.zero;

            var points = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
            Transform best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in points)
            {
                if (p == null) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p.transform; }
            }
            _gateCache = best;
            return best;
        }
    }
}
