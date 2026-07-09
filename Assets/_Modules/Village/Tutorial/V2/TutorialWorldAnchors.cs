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
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

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
        private static Transform _townAnchor;

        // Runtime holder for the SAFE TOWN Sylas anchor (see ResolveTownAnchor).
        private const string TownAnchorName = "SylasTownAnchor (runtime)";

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
            // 3. No Sylas body (ff.singlehero default ON no-ops both companion-intro
            //    spawners) -> a SAFE TOWN anchor, NOT the nearest wave gate. The old
            //    ResolveNearestGate() fallback put "Sylas" ON the enemy WAVE-SPAWN
            //    cluster CastleSpawnPointInjector injects just outside the south gate
            //    (z ~ -60..-64), so step 1 "walk to Sylas" marched the player straight
            //    onto the spawn ring — owner F8 2026-07-08 "when you start at the gate
            //    the enemies spawn on you". Town anchor = a short, safe in-town walk.
            _sylasCache = go != null ? go.transform
                                     : ResolveTownAnchor();
            return _sylasCache;
        }

        // ── Safe TOWN anchor (single-hero: no Sylas body to walk up to) ────────

        /// <summary>
        /// A safe town spot for the tutorial's "walk to Sylas" step when no Sylas body
        /// exists — a short walk from the baked hero start (world ~origin / the Heart in
        /// Main_Castle_Overworld, town-central, NORTH of the south gate at z=-50) and
        /// ~65m from the south wave-spawn cluster (z ~ -60..-64, CastleSpawnPointInjector).
        /// Replaces the old nearest-wave-gate fallback that sat the player on the enemy
        /// spawn ring. Snapped onto the baked walkable courtyard NavMesh; (6,0,4) is the
        /// same known-walkable courtyard point HeroControlEnsurer's hero-recovery uses.
        /// Created once per scene (a runtime holder, cleared on scene change).
        /// </summary>
        private static Transform ResolveTownAnchor()
        {
            if (_townAnchor != null) return _townAnchor;

            var existing = GameObject.Find(TownAnchorName);
            if (existing != null) { _townAnchor = existing.transform; return _townAnchor; }

            Vector3 townPos = new Vector3(6f, 0f, 4f);
            if (NavMesh.SamplePosition(townPos, out var hit, 12f, NavMesh.AllAreas))
                townPos = hit.position;

            var anchor = new GameObject(TownAnchorName);
            anchor.transform.position = townPos;
            _townAnchor = anchor.transform;

            FlowTrace.Step("Tutorial",
                $"Sylas TOWN anchor placed at {townPos} (safe town spot, " +
                $"~{Vector3.Distance(townPos, new Vector3(0f, 0f, -62f)):0}m from the south gate spawn cluster) " +
                "— replaces the enemy-spawn nearest-gate fallback (owner F8 'enemies spawn on you').");
            return _townAnchor;
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
