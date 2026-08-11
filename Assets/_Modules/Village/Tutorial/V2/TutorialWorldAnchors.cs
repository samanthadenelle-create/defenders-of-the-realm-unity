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
//
// WO-962 — THE STEP-ENTER LATCH (owner F8 seq 2301, 2026-08-10).
// "Nearest" is measured FROM THE HERO, so a live per-frame resolve made the
// walk-to target MOVE as the player obeyed it. The captured proving lines from
// ONE founding_walk step:
//     guide-lead SET -> (-3.43, 0.08, -38.63)   (south gate)
//     guide-lead SET -> (37.29, 0.08, -0.21)    (east gate)
//     guide-lead SET -> ( 3.07, 0.08,  38.68)   (north gate)
//     STEP-STUCK :: founding_walk - no 'hero.reached:guide_gate' after 123s
// The anchor is therefore RESOLVED ONCE on step ENTER and LATCHED for the life
// of the step (LatchAnchor / ClearLatch, driven by TutorialFlow.EnterStep /
// CompleteCurrentStep / FinishFlow). TryResolveAnchor reads the latch, so the
// proximity probe, the guide lead (PetHeroLeash.SetLeadTarget) and the
// "world.gate_direction" highlight all agree on ONE target. The resolver stays
// LIVE for everything else. If the live resolver would now answer differently
// we FlowTrace.Step that divergence ONCE and DO NOT act on it — that line is
// the regression's evidence, not a fallback.
// NOT the fix (explicitly forbidden by WO-962 §3): widening ReachedRadius or
// lengthening the watchdog. Both hide the defect.
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

        // ── WO-962: the step-ENTER latch (one target per step) ────────────────
        private static string _latchAnchorId;
        private static Vector3 _latchPos;
        private static Transform _latchSource;      // the transform the latch resolved THROUGH (gate)
        private static string _latchSourceName;
        private static bool _latchActive;
        private static bool _latchDivergenceTraced;
        private static float _nextDivergenceCheckAt;

        /// <summary>The transform the LAST live resolve went through (set by TryResolveLive).
        /// Captured by LatchAnchor so the gate HIGHLIGHT points at the same gate the latch
        /// took its position from - resolving it a second time could pick a different one.</summary>
        private static Transform _lastLiveSource;

        /// <summary>How often the latch may ask the LIVE resolver whether it would now
        /// answer differently. Diagnostic only - the answer is never acted on.</summary>
        private const float DivergenceCheckSeconds = 1f;

        /// <summary>Planar distance at which a live re-resolve counts as a MOVED goalpost
        /// (the F8 seq 2301 gates were tens of metres apart; 1.5m ignores navmesh jitter).</summary>
        private const float DivergenceMeters = 1.5f;

        /// <summary>Live-resolve signature. Exposed so the WO-962 regression can drive a
        /// MOVING resolver (the F8 seq 2301 south -> east -> north walk) without a baked
        /// scene, hero rig or navmesh. Runtime NEVER sets this.</summary>
        public delegate bool AnchorLiveResolver(string anchorId, out Vector3 pos, out string sourceName);

        /// <summary>Regression-only seam (see <see cref="AnchorLiveResolver"/>). Null in play.</summary>
        public static AnchorLiveResolver LiveResolverOverride;

        private void OnEnable()
        {
            // Registration points (WO-T2 world targets, guide-identity per WO-1012 P2):
            TutorialHighlightRegistry.RegisterResolver("world.guide",
                () => new HighlightTarget(ResolveGuide()));
            // WO-962: while a step's anchor is LATCHED the highlight points at the LATCHED
            // gate, not at whatever gate is nearest this frame - the arrow and the guide-lead
            // must never disagree (that disagreement IS the bug).
            TutorialHighlightRegistry.RegisterResolver("world.gate_direction",
                () => new HighlightTarget(_latchActive && _latchSource != null ? _latchSource : ResolveNearestGate()));
        }

        private void OnDisable()
        {
            TutorialHighlightRegistry.Unregister("world.guide");
            TutorialHighlightRegistry.Unregister("world.gate_direction");
            ClearLatch("anchors component disabled (scene teardown)");
        }

        // =====================================================================
        //  WO-962 — the LATCH
        // =====================================================================

        /// <summary>True when <paramref name="anchorId"/> is the currently latched anchor.</summary>
        public static bool IsLatched(string anchorId) =>
            _latchActive && string.Equals(_latchAnchorId, anchorId, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Resolve <paramref name="anchorId"/> ONCE and hold that world position for the life
        /// of the step (WO-962). Idempotent: a second call for the SAME anchor is a no-op and
        /// returns true, so TutorialFlow may re-call it every frame to cover an anchor that is
        /// not resolvable yet at STEP-ENTER (late-spawning gates) WITHOUT ever re-targeting a
        /// latch that already took. Latching a DIFFERENT anchor replaces the latch.
        /// Returns false when nothing resolved - the caller simply has no anchor this frame
        /// (no silent fallback, no substitute target).
        /// </summary>
        public static bool LatchAnchor(string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId)) return false;
            if (IsLatched(anchorId)) return true;

            _lastLiveSource = null;
            if (!TryResolveLive(anchorId, out Vector3 pos, out string sourceName))
                return false;

            _latchAnchorId = anchorId;
            _latchPos = pos;
            _latchSourceName = sourceName;
            _latchSource = _lastLiveSource;   // the exact gate this position came from
            _latchActive = true;
            _latchDivergenceTraced = false;
            _nextDivergenceCheckAt = Time.unscaledTime + DivergenceCheckSeconds;

            FlowTrace.Step("Tutorial",
                $"anchor '{anchorId}' LATCHED at {pos} (gate '{sourceName}') - WO-962: resolved ONCE on step " +
                "enter and held for the life of the step; the probe, the guide lead and the gate highlight " +
                "all read THIS position. A live re-resolve is diagnostic only.");
            return true;
        }

        /// <summary>Drop the latch (step exit / completion / flow reset), so a re-entered step
        /// resolves once again. Safe to call when nothing is latched.</summary>
        public static void ClearLatch(string reason = null)
        {
            if (!_latchActive)
            {
                _latchAnchorId = null; _latchSource = null; _latchSourceName = null;
                return;
            }
            string was = _latchAnchorId;
            Vector3 wasPos = _latchPos;
            _latchActive = false;
            _latchAnchorId = null;
            _latchSource = null;
            _latchSourceName = null;
            _latchDivergenceTraced = false;
            FlowTrace.Step("Tutorial",
                $"anchor '{was}' latch CLEARED (was {wasPos})" +
                (string.IsNullOrEmpty(reason) ? "." : $" - {reason}.") +
                " A re-entered step re-resolves once (WO-962).");
        }

        /// <summary>
        /// Diagnostic ONLY (WO-962 §3): ask the live resolver what it would answer now and,
        /// the FIRST time it disagrees with the latch, record that divergence. The latch is
        /// NOT updated - this line is the evidence that the goalpost would have moved.
        /// </summary>
        private static void TraceDivergenceOnce(string anchorId)
        {
            if (_latchDivergenceTraced) return;
            if (Time.unscaledTime < _nextDivergenceCheckAt) return;
            _nextDivergenceCheckAt = Time.unscaledTime + DivergenceCheckSeconds;

            if (!TryResolveLive(anchorId, out Vector3 livePos, out string liveName)) return;

            Vector3 d = livePos - _latchPos;
            d.y = 0f;
            if (d.sqrMagnitude < DivergenceMeters * DivergenceMeters) return;

            _latchDivergenceTraced = true;
            FlowTrace.Step("Tutorial",
                $"anchor '{anchorId}' LATCH DIVERGENCE: the live resolver would now answer {livePos} " +
                $"(gate '{liveName}'), {d.magnitude:0.0}m from the LATCHED {_latchPos} (gate " +
                $"'{_latchSourceName}'). The latch HOLDS - WO-962: following the live answer is the " +
                "moving-goalpost defect (F8 seq 2301). Diagnostic line, no action taken.");
        }

        /// <summary>Position of a named step anchor. False when unresolvable this frame.
        /// WO-962: a LATCHED anchor answers with the latched position, never a re-resolve.</summary>
        public static bool TryResolveAnchor(string anchorId, out Vector3 pos)
        {
            if (!string.IsNullOrEmpty(anchorId) && IsLatched(anchorId))
            {
                pos = _latchPos;
                TraceDivergenceOnce(anchorId);
                return true;
            }
            return TryResolveLive(anchorId, out pos, out _);
        }

        /// <summary>The LIVE (unlatched) resolve. Every anchor still resolves live for
        /// anything that is not an active latched step.</summary>
        private static bool TryResolveLive(string anchorId, out Vector3 pos, out string sourceName)
        {
            pos = default;
            sourceName = null;
            if (string.IsNullOrEmpty(anchorId)) return false;

            // Regression seam (WO-962): a scripted moving resolver stands in for the scene.
            var over = LiveResolverOverride;
            if (over != null) return over(anchorId, out pos, out sourceName);

            switch (anchorId.ToLowerInvariant())
            {
                case "guide_anchor":
                {
                    var t = ResolveGuide();
                    if (t == null) return false;
                    pos = t.position;
                    sourceName = t.name;
                    _lastLiveSource = t;
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
                    sourceName = gate.name;
                    _lastLiveSource = gate;
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
                    sourceName = heart.name;
                    _lastLiveSource = heart.transform;
                    return true;
                }
                default:
                    return false;
            }
        }

        // ── The GUIDE (the pet-Echo; steward body = the parked-rotation fallback) ──

        /// <summary>
        /// THE SINGLE AUTHORITY ON "DOES THE GUIDE HAVE A BODY" (WO-1014, owner felt-test
        /// 2026-08-10: "but still wolf and npc"). Returns the live pet-Echo body, or null.
        /// <para>
        /// WHY THIS IS PUBLIC AND WHY NOTHING MAY RE-IMPLEMENT IT: the guide's identity
        /// resolves down a CHAIN — pet body, then the steward stand-in, then the Heart —
        /// and a chain only works if exactly one place decides which link answers. Before
        /// this existed, <see cref="ResolveGuide"/> knew the pet won while
        /// SylasStewardInjector had no idea a pet existed at all, so once WO-961 gave the
        /// guide a real body BOTH stood in the courtyard: the spotlight pointed at the wolf
        /// and the stand-in it was supposed to REPLACE was still there. That is not a chain,
        /// it is two things side by side. Every consumer asks HERE.
        /// </para>
        /// Deliberately uncached: callers gate their own polling, and a stale "no body yet"
        /// is precisely the answer that leaves a second body standing.
        /// </summary>
        public static Transform LiveGuideBody() => FindAnyObjectByType<DeNelle.Pets.Pet>()?.transform;

        /// <summary>True when the founding guide has a real world body (see
        /// <see cref="LiveGuideBody"/>) — i.e. when the stand-in must NOT be seated.</summary>
        public static bool HasLiveGuideBody => LiveGuideBody() != null;

        private static Transform ResolveGuide()
        {
            if (Time.unscaledTime - _guideCachedAt < CacheSeconds && _guideCache != null)
                return _guideCache;
            _guideCachedAt = Time.unscaledTime;

            // 1. The live pet-Echo body — THE guide (WO-1012 P2). PetDeployer names the
            //    root "Pet_<species>" and deploys it once the ARRIVE-beat grant lands.
            //    Asked through the shared authority so the stand-in's spawn gate and this
            //    chain can never disagree about who the guide is (WO-1014).
            var petT = LiveGuideBody();
            if (petT != null)
            {
                _guideCache = petT;
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
