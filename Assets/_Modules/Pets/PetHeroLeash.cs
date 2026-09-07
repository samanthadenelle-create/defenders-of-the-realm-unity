// =============================================================================
// PetHeroLeash — gives each deployed pet a natural "exploring companion" feel:
// it meanders around the hero on smooth, curving paths instead of sprinting in
// straight lines between unrelated points (the old behaviour, which read as a
// triangular ping-pong / yo-yo on a string).
// -----------------------------------------------------------------------------
// How it works (owner 2026-05-25 "should feel like natural exploration"):
//   • A continuously-drifting wander HEADING (a slow random walk) — the pet
//     never reverses sharply, so its path curves like an animal nosing around.
//   • The pet's HomePost (which Pet.cs steers toward) is a "carrot" projected a
//     few metres AHEAD of the pet along that heading, refreshed every frame.
//     Because it stays > Pet.ArrivalDamp (1.6 m) ahead, Pet.cs never hits its
//     arrival brake, so the pet cruises smoothly and never stop-starts.
//   • When the pet drifts past the explore radius the heading is gently steered
//     back toward the hero (a curve home, not a snap), scaling with how far out
//     it is; beyond the hard leash it beelines home. A clamp keeps the carrot
//     inside the leash at all times.
//   • Occasional "stop and sniff" beats shorten the carrot so the pet eases to a
//     near-stop and looks around, then resumes — adds life. Each pet has its own
//     RNG seed, so the three explore independently instead of moving as a clump.
//
// Cross-module note: DeNelle.Pets cannot reference DeNelle.Village (asmdef
// isolation), so HeroLocomotion is resolved by reflection — name-matched once,
// cached, refreshed on scene reload.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Pets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Pet))]
    public sealed class PetHeroLeash : MonoBehaviour
    {
        private const float ResolveRetrySeconds = 1.0f;

        // Carrot distance ahead of the pet along its heading. Kept > Pet.cs's
        // ArrivalDamp (1.6 m) so the pet cruises without ever braking → smooth,
        // continuous motion instead of stop-start.
        private const float LeadDistance = 3.5f;
        // Pets keep AT LEAST this far from the hero — they ring him, never cruise
        // through his centre-of-frame spot. Without this a smoothly-moving pet
        // gliding through the (stationary, centred) hero reads as "the camera is
        // following the pet" (owner 2026-05-25, persisted across camera fixes).
        private const float InnerRadius = 4.5f;
        // Pet roams freely out to this radius of the hero; past it the heading is
        // steered back toward the hero so it curves home.
        private const float ExploreRadius = 9f;
        // Hard leash — the carrot is never placed beyond this; past it the pet
        // beelines home.
        private const float ReturnRadius = 13f;
        // Max gentle bend of the wander heading while meandering (deg/sec).
        private const float WanderTurnDegPerSec = 70f;
        // How sharply it may curve home at the very edge of the leash (deg/sec).
        private const float HomeSteerMaxDegPerSec = 200f;
        // Perlin-noise drift rate for the wander heading — how fast the coherent
        // turn-intent evolves. Lower = lazier meander, higher = more restless. TUNABLE.
        private const float NoiseDriftRate = 0.35f;
        // Idle-behavior FSM tuning (TUNABLE — set at playtest). Behaviors: 0 wander,
        // 1 sniff, 2 sit, 3 look, 4 circle, 5 dash. The controller maps each to a clip.
        private const float DashLead       = 7f;     // longer carrot → a brief dart ahead
        private const float OrbitRadius    = 5.5f;   // ring radius when circling the hero
        private const float OrbitDegPerSec = 45f;
        // Context-weighted behavior selection (research: weighted-random idle states +
        // cooldown). Index = behavior id. Hero MOVING → keep up; hero STILL → potter.
        private static readonly int[] WeightsHeroMoving = { 45, 5, 3, 5, 17, 25 };
        private static readonly int[] WeightsHeroStill  = { 30, 22, 14, 16, 12, 6 };

        private Pet _pet;
        private Transform _heroT;
        private float _resolveTimer;
        private float _noHeroTime;       // seconds the hero has been unresolved (for the not-following warn)
        private bool  _warnedNoHero;     // one-shot guard so the warn fires once per outage
        private System.Random _rng;

        private float _headingDeg;       // current wander heading (0 = +Z)
        private float _turnIntentDeg;    // signed bend currently being applied (Perlin-driven)
        private float _pauseTimer;       // >0 = the pet is stopped (sniff/sit/look idle beat)
        private float _noiseSeed;        // per-pet offset into the Perlin field (own personality)
        private int   _behavior;         // current idle-FSM behavior (0 wander … 5 dash)
        private float _behaviorTimer;    // dwell remaining in the current behavior
        private float _orbitDeg;         // angle for the "circle the hero" behavior
        private Vector3 _lastHeroPos;    // to detect whether the hero is moving (context weighting)

        private static Type s_heroType;

        // ── WO-1012 P2: the GUIDE-LEAD seam ──────────────────────────────────
        // The tutorial's pet-Echo GUIDE leads movement beats ("Come with me."):
        // while a lead target is set, every leashed pet suspends its wander/orbit
        // FSM and paces AHEAD of the hero toward the target — the carrot projects
        // toward the anchor but stays hard-clamped inside the hero leash, so the
        // guide WAITS for a lagging hero instead of deserting it. WO-1336: that carrot
        // is NAVMESH-ROUTED (see CarrotAlongCorners) rather than a straight-line
        // projection, so a structure on the route is walked AROUND, not walked into.
        // At the anchor it
        // holds and looks around. STATIC by design: the narrowest data/config seam
        // (TutorialFlow hands over ONE world position; no pet-instance plumbing,
        // no cross-module object handoff), and during the FTUE exactly ONE pet
        // exists (the starter Echo) so "all leashed pets" == the guide. Cleared by
        // the flow on beat completion / teardown; verified at source that Pet.cs
        // steers to SetHomePost, so no new movement code beyond this carrot mode.
        private static bool s_leadActive;
        private static Vector3 s_leadTarget;
        private const float LeadArriveRadius = 2.2f;   // hold distance at the anchor
        private int _leadBehaviorSent = -1;            // dedupe SetBehavior calls in lead mode

        // ── WO-1014 HALF B: the guide-lead FORENSICS (instrumentation ONLY) ───
        // Owner 2026-08-10: "wolf is supposed to lead, but doesn't move". F8 seq
        // 2307's harvest contains NO [Flow:Pets] lead lines at all, so the seam is
        // currently UNOBSERVABLE — that is the blocker, not a known defect. These
        // fields exist purely so ONE capture can separate the four ways this beat
        // can silently do nothing:
        //   (A) TutorialFlow never called SetLeadTarget       -> no "guide-lead SET" line
        //   (B) it called, but NO enabled leash exists to hear -> SET line says leashes=0
        //       (PetHarvester.SuspendLeash disables this component while it harvests)
        //   (C) a leash heard it and wrote the carrot, but the BODY never moved
        //       -> "guide-lead TICK" says carrot written, moved=0.00 m/s
        //   (D) the body moved and simply never reached the anchor -> moved>0, dist flat
        // NOTHING here changes movement. Per CLAUDE.md section 12 the fix waits on
        // the capture. (See also the two Pet.Update early-return traces, which are
        // what turn case (C) from a mystery into a named gate.)
        private static int s_enabledLeashes;            // live count of ENABLED PetHeroLeash
        private Vector3 _leadLastPos;                   // body position at the last forensic sample
        private float   _leadLastSampleTime;            // Time.time of that sample
        private bool    _leadEngagedTraced;             // one "ENGAGED" line per lead episode
        private bool    _leadArrivedTraced;             // one "ARRIVED" line per lead episode
        private NavMeshAgent _agent;                    // cached mover handle (diagnostics only)
        private bool    _agentResolved;                 // so a genuinely absent agent is not re-probed

        // ── WO-1336: the guide-lead carrot is NAVMESH-ROUTED ─────────────────
        // PROVEN CAUSE (owner F8 seq 4225 + the identical seq 3604/3606/4162 sticks,
        // Main_Castle_Overworld):
        //
        //   [Flow:Pets] guide-lead TICK 'pet-ice-wolf': moved=0.00 m/s over 1.00s ->
        //     BODY DID NOT MOVE (carrot written, zero displacement). dist=21.09m
        //     heroDist=13.00m mode=Defend agent(enabled=True, onNavMesh=True,
        //     isStopped=False, velocity=0.00) carrot=(-1.31, 0.08, -17.65)
        //
        // Every downstream gate PASSED in that same capture ("guide-lead LANE ACTIVE ...
        // MoveToward(_homePost) IS being integrated this frame"), the agent was enabled,
        // ON the mesh and NOT stopped -- and the body still covered zero metres, at a
        // dist that never changed. That excludes the stopped-agent shape outright.
        //
        // The mechanism: the pet has NO PATHFINDING. Pet.MoveToward integrates the carrot
        // with NavMeshAgent.Move(displacement), which CLAMPS a raw slide to the walkable
        // surface -- it never computes a route (that is also why velocity reads 0.00 on a
        // healthy agent in every one of these traces). The lead carrot was a dead-straight
        // projection, petPos + normalize(anchor - petPos) * LeadDistance, so with a
        // build-mode structure on the line the carrot sat INSIDE the carve
        // (BaseLayoutLoader gives every placed structure a NavMeshObstacle with
        // carving = true), Move() clamped the step to nothing, and the guide pressed into
        // the tower face forever.
        //
        // THE FIX GENERALISES: the carrot is now placed LeadDistance along the corner
        // polyline of a real NavMesh.CalculatePath, so it routes around WHATEVER carves
        // the route -- this tower, a wall the player moves there tomorrow, anything. On a
        // clear straight route the first leg IS the straight line, so the old feel is
        // preserved exactly. This stays inside the one existing mover seam (the leash
        // writes a carrot, Pet.MoveToward integrates it): no second mover, no second
        // spawner, no unstick coroutine, and EchoWorldPresence remains the sole
        // appearance owner (WO-1108 Lane B).
        private const float LeadRepathSeconds       = 0.35f;  // route refresh cadence
        private const float LeadAnchorMoveTolerance = 1.0f;   // re-route early if the anchor jumps
        private const float LeadAnchorSampleRadius  = 6.0f;   // snap an anchor inside a carve back out
        private const float LeadBlockedGraceSeconds = 3.0f;   // no-progress window before we say so
        private const int   LeadMaxCorners          = 32;

        private NavMeshPath _leadPath;
        private readonly Vector3[] _leadCorners = new Vector3[LeadMaxCorners];
        private int   _leadCornerCount;
        private float _leadRepathTimer;
        private Vector3 _leadPathForAnchor;
        private Vector3 _leadResolvedAnchor;
        private bool  _leadAnchorOnMesh;
        private NavMeshPathStatus _leadPathStatus = NavMeshPathStatus.PathInvalid;
        private float _leadBlockedSeconds;
        private bool  _leadBlockedTraced;
        private float _leadProgressBest = float.MaxValue;

        /// <summary>True while a guide lead is in force. Read by <see cref="Pet"/> so its
        /// early-return gates can say, once, that the lead is landing on a deaf pet.</summary>
        public static bool IsLeading => s_leadActive;

        /// <summary>The active lead anchor (meaningless unless <see cref="IsLeading"/>).</summary>
        public static Vector3 LeadTarget => s_leadTarget;

        /// <summary>How many PetHeroLeash components are currently ENABLED — i.e. how many
        /// listeners a <see cref="SetLeadTarget"/> call can possibly reach. Zero is the
        /// silent-no-op case (B) above.</summary>
        public static int EnabledLeashCount => s_enabledLeashes;

        /// <summary>Point every leashed pet (the FTUE guide) at a world-space lead
        /// anchor. Idempotent — safe to re-assert every frame; traces on change only.</summary>
        public static void SetLeadTarget(Vector3 worldPos)
        {
            if (!s_leadActive || (worldPos - s_leadTarget).sqrMagnitude > 1f)
            {
                FlowTrace.Step("Pets",
                    $"guide-lead SET -> {worldPos} (WO-1012 P2: the pet-Echo paces ahead of the hero toward " +
                    $"the anchor). listeners: {s_enabledLeashes} enabled PetHeroLeash.");
                // WO-1014 case (B): a lead nobody can hear. This used to be perfectly
                // silent — the static took the value and no Update ever consumed it.
                if (s_enabledLeashes == 0)
                    FlowTrace.Warn("Pets",
                        "guide-lead SET but ZERO enabled PetHeroLeash exists — NOTHING will consume this " +
                        "anchor, so the guide cannot move no matter what the tutorial asks. Either no pet " +
                        "body was spawned, or the component is disabled (PetHarvester.SuspendLeash turns it " +
                        "off while the pet harvests and only RestoreLeash turns it back on).");
            }
            s_leadActive = true;
            s_leadTarget = worldPos;
        }

        /// <summary>End the lead (beat complete / flow teardown) — the leash resumes
        /// natural exploration. Safe when no lead is active.</summary>
        public static void ClearLeadTarget()
        {
            if (!s_leadActive) return;
            s_leadActive = false;
            FlowTrace.Step("Pets", "guide-lead CLEARED — leash resumes natural exploration.");
        }

        // WO-1014 Half B: the enabled-listener census. OnEnable/OnDisable are the only
        // honest place to count — PetHarvester flips `enabled` directly.
        private void OnEnable()
        {
            s_enabledLeashes++;
            if (s_leadActive)
                FlowTrace.Step("Pets", $"leash ENABLED on '{PetIdSafe()}' while a guide lead is active " +
                                       $"(listeners now {s_enabledLeashes}).");
        }

        private void OnDisable()
        {
            s_enabledLeashes = Mathf.Max(0, s_enabledLeashes - 1);
            _leadEngagedTraced = false;
            _leadArrivedTraced = false;
            ResetLeadEpisodeState();
            if (s_leadActive)
                FlowTrace.Warn("Pets", $"leash DISABLED on '{PetIdSafe()}' WHILE A GUIDE LEAD IS ACTIVE — this " +
                                       $"pet stops consuming the lead anchor from now on (listeners now " +
                                       $"{s_enabledLeashes}). PetHarvester.SuspendLeash is the known caller.");
        }

        private string PetIdSafe() => _pet != null ? _pet.PetId : gameObject.name;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            // Stable per-pet rng so each pet has its own personality but a
            // restart of the scene replays the same trail.
            _rng = new System.Random(gameObject.GetInstanceID());
            _headingDeg = (float)(_rng.NextDouble() * 360.0);
            _noiseSeed = (float)(_rng.NextDouble() * 1000.0);   // unique slice of the noise field per pet
        }

        private void Update()
        {
            // WO-1483 frame budget — the EchoWorldPresence tick (this leash is the Echo's
            // one per-frame owner). FIRST line so every early-return path is still timed.
            // Accumulating 4-arg overload — no per-frame log; PerfReporter rolls it up 1/s.
            using var _perf = FlowTrace.Measure("Perf", "PetHeroLeash.Update", 4f, 1f);

            if (_heroT == null)
            {
                _noHeroTime += Time.deltaTime;
                _resolveTimer -= Time.deltaTime;
                if (_resolveTimer <= 0f)
                {
                    _resolveTimer = ResolveRetrySeconds;
                    _heroT = ResolveHeroTransform();
                }
                if (_heroT == null)
                {
                    // NO SILENT FAILURE (§12): a pet that can't resolve the hero just sits there
                    // ("doesnt follow", owner F8 2026-06-17) with no clue why. Surface it ONCE after
                    // a grace period so the next capture says whether the pet even found the hero.
                    if (!_warnedNoHero && _noHeroTime > 5f)
                    {
                        _warnedNoHero = true;
                        Debug.LogWarning("[PetHeroLeash] hero (HeroLocomotion) not resolved after 5s — " +
                                         "pet cannot follow until it appears. (No HeroLocomotion in scene / wrong scene?)");
                    }
                    return;
                }
            }
            // Hero is present this frame — clear the not-following watch.
            _noHeroTime = 0f;
            _warnedNoHero = false;

            float dt = Time.deltaTime;
            Vector3 petPos = transform.position;
            Vector3 toHero = _heroT.position - petPos; toHero.y = 0f;
            float distHero = toHero.magnitude;

            // ── WO-1012 P2 guide-lead: an active lead target overrides the wander
            //    FSM entirely — the pet paces ahead of the hero toward the anchor. ──
            if (s_leadActive)
            {
                Vector3 toAnchor = s_leadTarget - petPos; toAnchor.y = 0f;
                float distToAnchor = toAnchor.magnitude;
                // WO-1336: refresh the routed path BEFORE the carrot is chosen, so the
                // carrot follows the walkable route rather than the blocked straight line.
                EnsureLeadPath(petPos);
                bool blocked = UpdateLeadBlockedWatch(distToAnchor, distHero);

                Vector3 leadCarrot;
                int wantBehavior;
                if (distToAnchor <= LeadArriveRadius)
                {
                    leadCarrot = s_leadTarget;   // arrived — hold at the anchor
                    wantBehavior = 3;            // "look around" idle while waiting
                }
                else if (blocked)
                {
                    // The route is closed and no progress is being made. Hold at the
                    // furthest point the route DOES reach and stand there looking around,
                    // rather than running on the spot into a wall forever. The escort beat
                    // itself completes on the HERO reaching the gate (TutorialFlow's
                    // hero.reached probe), so the lifecycle continues from here either way.
                    leadCarrot = _leadCornerCount > 0 ? _leadCorners[_leadCornerCount - 1] : petPos;
                    wantBehavior = 3;
                }
                else
                {
                    leadCarrot = CarrotAlongCorners(_leadCorners, _leadCornerCount, petPos,
                                                    toAnchor, LeadDistance);
                    wantBehavior = 0;            // travel gait
                }
                if (wantBehavior != _leadBehaviorSent)
                {
                    _leadBehaviorSent = wantBehavior;
                    _pet.SetBehavior(wantBehavior);
                }
                // Same hard clamp as the wander path: the carrot never leaves the
                // hero's ReturnRadius — the guide LEADS, it never deserts.
                Vector3 fh = leadCarrot - _heroT.position; fh.y = 0f;
                if (fh.magnitude > ReturnRadius)
                {
                    leadCarrot = _heroT.position + fh.normalized * ReturnRadius;
                    // WO-1336: the clamp is a straight-line projection like the old carrot
                    // was, so it can itself land inside a carve. Snap it back onto walkable
                    // ground; without this the "never desert" rule could re-create the
                    // exact wedge this ticket fixed, just at the leash limit instead.
                    if (NavMesh.SamplePosition(leadCarrot, out NavMeshHit clampHit, 3f, NavMesh.AllAreas))
                        leadCarrot = clampHit.position;
                }
                leadCarrot.y = Mathf.Max(0f, leadCarrot.y);
                _pet.SetHomePost(leadCarrot);
                TraceLeadForensics(petPos, leadCarrot, distToAnchor, distHero);
                _lastHeroPos = _heroT.position;   // keep the moving-context sample warm
                return;
            }
            if (_leadEngagedTraced) { _leadEngagedTraced = false; _leadArrivedTraced = false; }
            ResetLeadEpisodeState();
            _leadBehaviorSent = -1;   // out of lead mode — the FSM owns behavior again

            // ── heading drift: a CONTINUOUS Perlin-noise turn intent (coherent noise →
            //    smooth, animal-like curving), signed so there's no directional bias. ──
            _turnIntentDeg = (Mathf.PerlinNoise(_noiseSeed, Time.time * NoiseDriftRate) - 0.5f) * 2f * WanderTurnDegPerSec;

            // ── idle-behavior FSM: on a cooldown, weighted-randomly pick what the pet
            //    DOES (wander/sniff/sit/look/circle/dash), shifted by whether the hero is
            //    moving; the controller maps the int to a clip. Sniff/sit/look stop the
            //    pet (short carrot) so the idle anim reads; circle/dash drive movement. ──
            float heroSpeed = dt > 0f ? (_heroT.position - _lastHeroPos).magnitude / dt : 0f;
            _lastHeroPos = _heroT.position;
            _behaviorTimer -= dt;
            if (_behaviorTimer <= 0f)
            {
                _behavior = PickBehavior(heroSpeed > 0.6f);
                _behaviorTimer = 1.2f + (float)_rng.NextDouble() * 2.8f;   // 1.2–4 s dwell
                _pet.SetBehavior(_behavior);
                _pauseTimer = (_behavior >= 1 && _behavior <= 3) ? _behaviorTimer : 0f; // sniff/sit/look = stop
            }
            if (_pauseTimer > 0f) _pauseTimer -= dt;

            // Gently bend the heading (smooth random walk → curving paths).
            _headingDeg += _turnIntentDeg * dt;

            // Steer back toward the hero past the explore radius — a curve home,
            // its strength rising the further out we are (never a hard snap).
            if (distHero > ExploreRadius && distHero > 0.01f)
            {
                float homeDeg = Mathf.Atan2(toHero.x, toHero.z) * Mathf.Rad2Deg;
                if (distHero > ReturnRadius)
                {
                    _headingDeg = homeDeg; // beyond the leash → head straight home
                }
                else
                {
                    float urgency = Mathf.Clamp01(
                        (distHero - ExploreRadius) / Mathf.Max(0.01f, ReturnRadius - ExploreRadius));
                    float steer = Mathf.Lerp(WanderTurnDegPerSec, HomeSteerMaxDegPerSec, urgency) * dt;
                    _headingDeg = Mathf.MoveTowardsAngle(_headingDeg, homeDeg, steer);
                }
            }

            // Project the carrot ahead along the heading. Shorten it during a pause
            // (sniff/sit/look) so the pet eases down and the idle anim reads; lengthen
            // it for a dash so the pet briefly darts ahead.
            float lead = _pauseTimer > 0f ? 0.25f : (_behavior == 5 ? DashLead : LeadDistance);
            float rad = _headingDeg * Mathf.Deg2Rad;
            Vector3 carrot = petPos + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * lead;

            // "Circle the hero": override the carrot to an orbit point so the pet rings
            // the hero (curious/loyal) — still clamped inside the leash just below.
            if (_behavior == 4)
            {
                _orbitDeg += OrbitDegPerSec * dt;
                float o = _orbitDeg * Mathf.Deg2Rad;
                carrot = _heroT.position + new Vector3(Mathf.Sin(o), 0f, Mathf.Cos(o)) * OrbitRadius;
            }

            // Hard clamp inside the leash so the pet is never sent past the limit.
            Vector3 fromHero = carrot - _heroT.position; fromHero.y = 0f;
            if (fromHero.magnitude > ReturnRadius)
                carrot = _heroT.position + fromHero.normalized * ReturnRadius;
            carrot.y = Mathf.Max(0f, carrot.y);

            _pet.SetHomePost(carrot);
        }

        // =====================================================================
        //  WO-1336 — the routed guide-lead carrot.
        //  Pet.MoveToward integrates the carrot with NavMeshAgent.Move(), which does
        //  NO pathfinding: it slides the body and clamps that slide to the walkable
        //  surface. A straight-line carrot therefore wedges against the first carved
        //  structure on the line and the guide never moves again (the proving capture
        //  is quoted on the fields above). Routing the CARROT is the narrowest possible
        //  fix: the mover, the owner and the lifecycle are all unchanged, and it works
        //  for any blocking structure because CalculatePath queries the live navmesh,
        //  holes and all.
        // =====================================================================

        /// <summary>
        /// Places the carrot <paramref name="leadDistance"/> metres along the walkable
        /// corner polyline. PURE + static so the oracle can execute the real rule with
        /// synthetic geometry (no navmesh bake needed in a batchmode suite).
        /// <para/>
        /// With fewer than two corners there is no route to follow and it degrades to the
        /// historical straight-line projection, so open ground behaves exactly as before.
        /// </summary>
        public static Vector3 CarrotAlongCorners(Vector3[] corners, int count,
                                                 Vector3 from, Vector3 straightDir, float leadDistance)
        {
            if (corners == null || count < 2)
            {
                Vector3 flat = straightDir; flat.y = 0f;
                if (flat.sqrMagnitude < 0.000001f) return from;
                return from + flat.normalized * leadDistance;
            }

            float budget = Mathf.Max(0.01f, leadDistance);
            Vector3 cursor = corners[0];
            for (int i = 1; i < count && i < corners.Length; i++)
            {
                Vector3 leg = corners[i] - cursor;
                leg.y = 0f;
                float len = leg.magnitude;
                if (len < 0.0001f) { cursor = corners[i]; continue; }
                if (len >= budget) return cursor + (leg / len) * budget;
                budget -= len;
                cursor = corners[i];
            }
            // The whole remaining route is shorter than the carrot: aim at its end so the
            // pet eases down onto the anchor instead of overshooting past it.
            return cursor;
        }

        /// <summary>Refresh the routed path to the lead anchor on a cadence (and immediately
        /// when the anchor moves). Never throws; a failed query leaves zero corners, which
        /// degrades to the straight-line carrot.</summary>
        private void EnsureLeadPath(Vector3 petPos)
        {
            _leadRepathTimer -= Time.deltaTime;
            bool anchorMoved = (s_leadTarget - _leadPathForAnchor).sqrMagnitude >
                               LeadAnchorMoveTolerance * LeadAnchorMoveTolerance;
            if (_leadPath != null && !anchorMoved && _leadRepathTimer > 0f) return;

            _leadRepathTimer = LeadRepathSeconds;
            _leadPathForAnchor = s_leadTarget;
            if (_leadPath == null) _leadPath = new NavMeshPath();

            // The anchor itself can sit inside a carve (a structure placed over the gate
            // mouth) - a point no agent can ever stand on. Snap it to the nearest walkable
            // spot first so an unreachable-by-definition destination still resolves to a
            // sane nearby one instead of latching the guide forever.
            _leadResolvedAnchor = s_leadTarget;
            _leadAnchorOnMesh = false;
            if (NavMesh.SamplePosition(s_leadTarget, out NavMeshHit anchorHit,
                                       LeadAnchorSampleRadius, NavMesh.AllAreas))
            {
                _leadResolvedAnchor = anchorHit.position;
                _leadAnchorOnMesh = true;
            }

            Vector3 from = petPos;
            if (NavMesh.SamplePosition(petPos, out NavMeshHit fromHit, 2f, NavMesh.AllAreas))
                from = fromHit.position;

            bool ok = false;
            Vector3 fromLocal = from;
            Vector3 toLocal = _leadResolvedAnchor;
            Guard.Try("Pets", "calculate the guide-lead route",
                () => { ok = NavMesh.CalculatePath(fromLocal, toLocal, NavMesh.AllAreas, _leadPath); });

            _leadPathStatus = ok ? _leadPath.status : NavMeshPathStatus.PathInvalid;
            _leadCornerCount = ok ? _leadPath.GetCornersNonAlloc(_leadCorners) : 0;
        }

        /// <summary>
        /// Watches whether the guide is actually closing on the anchor. Returns true once
        /// it has made no headway for <see cref="LeadBlockedGraceSeconds"/> - at which
        /// point the caller holds the guide at the furthest REACHABLE point instead of
        /// pressing it into the blockage. Warns ONCE per episode, naming the shape, so the
        /// next stick reports itself without another investigation (CLAUDE.md sec.12).
        /// </summary>
        private bool UpdateLeadBlockedWatch(float distToAnchor, float distHero)
        {
            if (distToAnchor <= LeadArriveRadius || _leadArrivedTraced)
            {
                // Arrived (or has arrived once this episode). Holding, then drifting back
                // to a lagging hero, is the beat working - never a block.
                _leadBlockedSeconds = 0f;
                return false;
            }
            if (distHero >= ReturnRadius - 0.5f)
            {
                // The guide is at its leash limit: the hero-clamp is deliberately holding
                // it back so it WAITS instead of deserting. Standing still here is the
                // designed behaviour, not a blockage - do not accuse the navmesh of it.
                _leadBlockedSeconds = 0f;
                return false;
            }
            if (distToAnchor < _leadProgressBest - 0.25f)
            {
                _leadProgressBest = distToAnchor;      // real headway - not blocked
                _leadBlockedSeconds = 0f;
                return false;
            }

            _leadBlockedSeconds += Time.deltaTime;
            if (_leadBlockedSeconds < LeadBlockedGraceSeconds) return false;

            if (!_leadBlockedTraced)
            {
                _leadBlockedTraced = true;
                string shape =
                    _leadCornerCount < 2 ? "NO ROUTE AT ALL (CalculatePath returned nothing usable - the pet " +
                                           "or the anchor is off the navmesh)"
                    : _leadPathStatus == NavMeshPathStatus.PathPartial ? "PARTIAL PATH - a structure carves the " +
                                           "route and the anchor cannot be reached from here"
                    : _leadPathStatus == NavMeshPathStatus.PathInvalid ? "INVALID PATH - no walkable connection " +
                                           "between the guide and the anchor exists"
                    : "COMPLETE path but zero headway - the body is wedged on something the navmesh does not know " +
                      "about (a solid collider with no carving NavMeshObstacle)";
                FlowTrace.Warn("Pets",
                    $"guide-lead BLOCKED on '{PetIdSafe()}': no progress for {_leadBlockedSeconds:0.0}s. {shape}. " +
                    $"anchor={s_leadTarget} resolvedAnchor={_leadResolvedAnchor} anchorOnNavMesh={_leadAnchorOnMesh} " +
                    $"pathStatus={_leadPathStatus} corners={_leadCornerCount} dist={distToAnchor:0.00}m " +
                    $"bestDist={_leadProgressBest:0.00}m. Holding at the furthest REACHABLE point and standing " +
                    "there rather than pressing into the blockage forever - the escort beat still completes on " +
                    "the HERO reaching the gate, so the lifecycle is not stranded.");
            }
            return true;
        }

        /// <summary>Clear the per-episode routing/progress state so a re-issued lead starts
        /// from a clean slate instead of inheriting a stale block verdict.</summary>
        private void ResetLeadEpisodeState()
        {
            _leadCornerCount = 0;
            _leadRepathTimer = 0f;
            _leadPathForAnchor = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _leadPathStatus = NavMeshPathStatus.PathInvalid;
            _leadAnchorOnMesh = false;
            _leadBlockedSeconds = 0f;
            _leadBlockedTraced = false;
            _leadProgressBest = float.MaxValue;
        }

        // =====================================================================
        //  WO-1014 Half B — guide-lead forensics. READ-ONLY: it inspects and
        //  reports, it never steers. One ENGAGED line per episode (the full
        //  census), a 1 Hz TICK line (did the BODY actually move?), one ARRIVED.
        //  Between them, one capture answers "the wolf doesn't move" without a
        //  single guess: whether a mover exists, whether it is on the navmesh,
        //  what mode the pet is in, what carrot was written, and the measured
        //  per-second displacement of the body that was supposed to walk.
        // =====================================================================
        private void TraceLeadForensics(Vector3 petPos, Vector3 carrot, float distToAnchor, float distHero)
        {
            if (!_agentResolved) { _agentResolved = true; _agent = GetComponent<NavMeshAgent>(); }
            // isStopped may only be read on an agent that is enabled AND on the mesh -
            // reading it otherwise logs a Unity error, which would make the diagnostic
            // itself the noise. Report the two cheap facts and only then the third.
            string mover;
            if (_agent == null)
                mover = "NO NavMeshAgent component (Pet falls back to a raw transform move)";
            else if (!_agent.enabled || !_agent.isOnNavMesh)
                mover = $"agent(enabled={_agent.enabled}, onNavMesh={_agent.isOnNavMesh}) - OFF THE NAVMESH, " +
                        "so Pet.MoveToward takes its raw-transform fallback branch";
            else
                mover = $"agent(enabled=True, onNavMesh=True, isStopped={_agent.isStopped}, " +
                        $"velocity={_agent.velocity.magnitude:0.00})";
            string mode = _pet != null ? _pet.Mode.ToString() : "<no Pet>";
            string alive = _pet != null ? _pet.IsAlive.ToString() : "?";
            // WO-1336: the ROUTE facts. Without these a stick is only ever "moved=0.00"
            // and the four causes (closed route / stopped agent / partial path /
            // destination inside the carve) look identical from outside. Never strip.
            string route = $"route(status={_leadPathStatus}, corners={_leadCornerCount}, " +
                           $"anchorOnNavMesh={_leadAnchorOnMesh}, resolvedAnchor={_leadResolvedAnchor}, " +
                           $"noProgressFor={_leadBlockedSeconds:0.0}s, bestDist=" +
                           $"{(_leadProgressBest >= float.MaxValue * 0.5f ? -1f : _leadProgressBest):0.00}m)";

            if (!_leadEngagedTraced)
            {
                _leadEngagedTraced = true;
                _leadLastPos = petPos;
                _leadLastSampleTime = Time.time;
                FlowTrace.Step("Pets",
                    $"guide-lead ENGAGED on '{PetIdSafe()}': anchor={s_leadTarget} dist={distToAnchor:0.00}m " +
                    $"heroDist={distHero:0.00}m mode={mode} alive={alive} {mover} {route} bodyPos={petPos} " +
                    $"carrot={carrot} listeners={s_enabledLeashes}. NOTE: writing the carrot is SetHomePost " +
                    $"only — Pet.Update decides whether it is ever integrated (see its early-return traces).");
            }

            float dt = Time.time - _leadLastSampleTime;
            if (dt >= 1f)
            {
                float moved = (petPos - _leadLastPos).magnitude;
                _leadLastPos = petPos;
                _leadLastSampleTime = Time.time;
                string verdict = moved < 0.05f
                    ? "BODY DID NOT MOVE (carrot written, zero displacement — the write is being ignored downstream)"
                    : "body moving";
                FlowTrace.Throttle("Pets", "guide-lead-tick-" + PetIdSafe(), 1f,
                    $"guide-lead TICK '{PetIdSafe()}': moved={moved / Mathf.Max(0.0001f, dt):0.00} m/s over " +
                    $"{dt:0.00}s -> {verdict}. dist={distToAnchor:0.00}m heroDist={distHero:0.00}m mode={mode} " +
                    $"{mover} {route} carrot={carrot} " +
                    $"homePost={(_pet != null ? _pet.HomePost.ToString() : "?")}.");
            }

            if (!_leadArrivedTraced && distToAnchor <= LeadArriveRadius)
            {
                _leadArrivedTraced = true;
                FlowTrace.Step("Pets",
                    $"guide-lead ARRIVED: '{PetIdSafe()}' is within {LeadArriveRadius:0.0}m of {s_leadTarget} " +
                    $"(dist={distToAnchor:0.00}m) and holds. The LEAD half of the walk beat completed.");
            }
        }

        // Weighted-random idle behavior (0 wander,1 sniff,2 sit,3 look,4 circle,5 dash).
        // Hero MOVING → keep up (wander/dash/circle); hero STILL → potter (sniff/sit/look).
        // No per-frame alloc — called only on the behavior cooldown, using static weights.
        private int PickBehavior(bool heroMoving)
        {
            int[] w = heroMoving ? WeightsHeroMoving : WeightsHeroStill;
            int total = 0;
            for (int i = 0; i < w.Length; i++) total += w[i];
            int r = _rng.Next(total);
            for (int i = 0; i < w.Length; i++) { if (r < w[i]) return i; r -= w[i]; }
            return 0;
        }

        private static Transform ResolveHeroTransform()
        {
            try
            {
                if (s_heroType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Village.HeroLocomotion", false);
                        if (t != null) { s_heroType = t; break; }
                    }
                }
                if (s_heroType == null) return null;
                var found = UnityEngine.Object.FindAnyObjectByType(s_heroType) as Component;
                return found != null ? found.transform : null;
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn("Pets", $"ResolveHeroTransform failed: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>Keeps a TextMesh facing the main camera (used for pet name tags).</summary>
    [DisallowMultipleComponent]
    internal sealed class PetNameTagBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
