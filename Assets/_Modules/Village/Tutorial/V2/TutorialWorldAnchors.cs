// =============================================================================
// TutorialWorldAnchors — world-anchor resolution for Tutorial V2 (WO-T1/T2,
// re-pointed at the GUIDE identity by WO-1012 P2).
// -----------------------------------------------------------------------------
// Two jobs:
//   1. hero.reached:<anchor> positions — TryResolveAnchor("guide_anchor" /
//      "hub_anchor") for TutorialFlow's proximity probe.
//   2. Registry resolvers — registers "world.guide" and "world.gate_direction"
//      as LAZY resolvers in TutorialHighlightRegistry (targets that spawn late
//      or move; registration points cited below).
//
// THE GUIDE (WO-1012 §2a as RE-RULED 2026-08-09) is the player's first PET —
// an Echo of Elarion. Resolution order for the guide anchor:
//   1. the live pet-Echo BODY (DeNelle.Pets.Pet — deployed by PetDeployer once
//      the ARRIVE-beat starter grant lands; the guide itself),
//   2. the steward stand-in body ("CompanionIntroducer" / a GameObject named
//      after the steward NPC — the PARKED hero-rotation path's walk-up body,
//      kept as the fallback so the beat still has a physical presence when no
//      pet body is deployed, e.g. before the grant applies),
//   3. the Heart of Elarion (the guide wakes AT the Heart — canon),
//   4. a SAFE TOWN anchor (never the wave-spawn ring — owner F8 2026-07-08
//      "enemies spawn on you"), else invalid (the flow degrades: no spotlight,
//      proximity waits, watchdog self-reports).
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

        /// <summary>WO-1012 P3 (WALK beat): how far the "guide_gate" anchor is pulled
        /// BACK from the nearest wave gate toward the Heart. WaveSpawnPoints sit ~12m
        /// OUTSIDE each gate (CastleSpawnPointInjector), and anchoring a walk-to beat
        /// ON that ring is the owner F8 2026-07-08 bug ("enemies spawn on you") — the
        /// pull-back lands the anchor INSIDE the walls, gate-adjacent but safe.</summary>
        private const float GateAnchorPullbackMeters = 14f;

        private static Transform _guideCache;
        private static float _guideCachedAt = float.NegativeInfinity;
        private static Transform _gateCache;
        private static float _gateCachedAt = float.NegativeInfinity;
        private static Transform _townAnchor;

        // Runtime holder for the SAFE TOWN guide anchor (see ResolveTownAnchor).
        private const string TownAnchorName = "GuideTownAnchor (runtime)";

        private void OnEnable()
        {
            // Registration points (WO-T2 world targets, guide-identity per WO-1012 P2):
            TutorialHighlightRegistry.RegisterResolver("world.guide",
                () => new HighlightTarget(ResolveGuide()));
            TutorialHighlightRegistry.RegisterResolver("world.gate_direction",
                () => new HighlightTarget(ResolveNearestGate()));
        }

        private void OnDisable()
        {
            TutorialHighlightRegistry.Unregister("world.guide");
            TutorialHighlightRegistry.Unregister("world.gate_direction");
        }

        /// <summary>Position of a named step anchor. False when unresolvable this frame.</summary>
        public static bool TryResolveAnchor(string anchorId, out Vector3 pos)
        {
            pos = default;
            if (string.IsNullOrEmpty(anchorId)) return false;

            switch (anchorId.ToLowerInvariant())
            {
                case "guide_anchor":
                {
                    var t = ResolveGuide();
                    if (t == null) return false;
                    pos = t.position;
                    return true;
                }
                case "guide_gate":
                {
                    // WO-1012 P3 (the arc, beat 2 WALK): the follow-the-guide target.
                    // The nearest wave gate, pulled GateAnchorPullbackMeters back toward
                    // the Heart so the anchor sits INSIDE the walls — never on the
                    // wave-spawn ring (the F8 2026-07-08 lesson) — then navmesh-snapped.
                    // TutorialFlow.TickProximityProbe feeds this to PetHeroLeash
                    // .SetLeadTarget, so the pet-Echo guide leads the hero here.
                    var gate = ResolveNearestGate();
                    if (gate == null) return false;
                    pos = gate.position;
                    var heartT = ResolveHeart();
                    if (heartT != null)
                    {
                        Vector3 dir = heartT.position - gate.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f)
                            pos = gate.position + dir.normalized * GateAnchorPullbackMeters;
                    }
                    if (NavMesh.SamplePosition(pos, out var gateHit, 10f, NavMesh.AllAreas))
                        pos = gateHit.position;
                    FlowTrace.Once("Tutorial", "guide-gate-anchor",
                        $"WALK anchor 'guide_gate' resolved at {pos} — nearest gate '{gate.name}' pulled " +
                        $"{GateAnchorPullbackMeters:0}m toward the Heart (inside the walls, never the spawn ring).");
                    return true;
                }
                case "hub_anchor":
                {
                    // Home = the Heart of Elarion. STALE-FIX: the old "scene centre 0,0,0
                    // by canon" comment is WRONG for the merged Main_Castle_Overworld, whose
                    // castle content is offset (~5000,0,5000) -- the Heart is NOT at world
                    // origin. Resolve the LIVE HeartController's real transform (it is baked
                    // active+enabled into Main_Castle_Overworld, so FindAnyObjectByType finds
                    // it). If NO Heart resolves, return "no anchor" (false) so the proximity
                    // probe reports false -- NEVER strand the return_home step at (0,0,0),
                    // which in the offset world is thousands of metres from the hero (the old
                    // Vector3.zero fallback made return_home un-completable there).
                    var heart = FindAnyObjectByType<HeartController>();
                    if (heart == null) return false;
                    pos = heart.transform.position;
                    return true;
                }
                default:
                    return false;
            }
        }

        // ── The GUIDE (the pet-Echo; steward body = the parked-rotation fallback) ──

        private static Transform ResolveGuide()
        {
            if (Time.unscaledTime - _guideCachedAt < CacheSeconds && _guideCache != null)
                return _guideCache;
            _guideCachedAt = Time.unscaledTime;

            // 1. The live pet-Echo body — THE guide (WO-1012 P2). PetDeployer names the
            //    root "Pet_<species>" and deploys it once the ARRIVE-beat grant lands.
            var pet = FindAnyObjectByType<DeNelle.Pets.Pet>();
            if (pet != null)
            {
                _guideCache = pet.transform;
                return _guideCache;
            }

            // 2. The steward walk-up body (the PARKED hero-rotation stand-in — spawned
            //    by CastleCompanionIntroducerInjector / SylasStewardInjector when their
            //    flags allow). A canon NPC, kept as the physical-presence fallback.
            var go = GameObject.Find("CompanionIntroducer");
            if (go == null)
                go = GameObject.Find("Sylas");   // the steward NPC's canon name (see SylasStewardInjector)
            if (go != null)
            {
                _guideCache = go.transform;
                return _guideCache;
            }

            // 3. The Heart of Elarion — the guide wakes AT the Heart (canon: its essence
            //    returns from the tree that guarded it), so the Heart is the honest
            //    "beneath the tree" target when no body has spawned yet.
            var heart = FindAnyObjectByType<HeartController>();
            if (heart != null)
            {
                _guideCache = heart.transform;
                return _guideCache;
            }

            // 4. No body, no Heart (degenerate scene) -> a SAFE TOWN anchor, NOT the
            //    nearest wave gate. The old nearest-gate fallback put the anchor ON the
            //    enemy WAVE-SPAWN cluster CastleSpawnPointInjector injects just outside
            //    the south gate (z ~ -60..-64), so the walk-to step marched the player
            //    straight onto the spawn ring — owner F8 2026-07-08 "when you start at
            //    the gate the enemies spawn on you". Town anchor = a short, safe walk.
            _guideCache = ResolveTownAnchor();
            return _guideCache;
        }

        // ── Safe TOWN anchor (no guide body resolvable) ────────────────────────

        /// <summary>
        /// A safe town spot for the tutorial's walk-to-the-guide step when no guide body
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
                $"guide TOWN anchor placed at {townPos} (safe town spot, " +
                $"~{Vector3.Distance(townPos, new Vector3(0f, 0f, -62f)):0}m from the south gate spawn cluster) " +
                "— replaces the enemy-spawn nearest-gate fallback (owner F8 'enemies spawn on you').");
            return _townAnchor;
        }

        // ── The Heart (cached — anchor resolves run per-frame under the probe) ─

        private static Transform _heartCache;
        private static float _heartCachedAt = float.NegativeInfinity;

        private static Transform ResolveHeart()
        {
            if (Time.unscaledTime - _heartCachedAt < CacheSeconds && _heartCache != null)
                return _heartCache;
            _heartCachedAt = Time.unscaledTime;
            var heart = FindAnyObjectByType<HeartController>();
            _heartCache = heart != null ? heart.transform : null;
            return _heartCache;
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
