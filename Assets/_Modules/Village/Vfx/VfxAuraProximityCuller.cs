// =============================================================================
// VfxAuraProximityCuller - the nearest-N ring that bounds how many ENEMY and PET
// auras may hold a loop slot at once.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHY THIS EXISTS (WO-889 part 1, and it lands BEFORE the auras it bounds)
//
// VfxLoopBudget raises the CEILING per scene tier. That alone does not bound the
// POPULATION: a wave can spawn thirty enemies, and thirty persistent auras would
// eat any ceiling anyone is willing to set. Raising a cap to fit the worst case is
// how a cap stops meaning anything.
//
// So the population is bounded instead: only the N enemies/pets NEAREST the view
// hold an aura. The rest are told to release, and are re-granted the moment they
// come back into the ring. An aura twenty metres behind the camera is paying a
// pool slot to be invisible.
//
// ## IT APPLIES TO ENEMY AND PET AURAS ONLY - STRUCTURALLY, NOT BY POLICY
//
// WO-889 is explicit: nearest-N must NOT touch towers, the Heart, boss phases, or
// any one-shot impact. That is not enforced here by a type check that a later
// caller could get wrong - it is enforced by the fact that this culler only knows
// about things that REGISTERED with it. A tower aura, a Heart aura, a boss phase
// aura and every oneshot simply never call Register, so there is no code path by
// which this class could cull one. To make a new aura class cullable you have to
// opt in, in its own file, deliberately.
//
// The asymmetry is a design judgement, not an oversight:
//   * Enemy/pet auras are MANY, TRANSIENT and INTERCHANGEABLE. Losing the aura on
//     the twelfth-nearest wolf costs nothing - you cannot read a detail you cannot
//     resolve at that distance anyway.
//   * A tower / Heart / boss aura is ONE, FIXED and LOAD-BEARING. The Heart aura is
//     a health readout; a boss phase aura tells you which phase you are fighting.
//     Culling those by distance would delete information, and a far-away landmark
//     is exactly when you most want to see it (the same reasoning PoiCalloutSystem
//     applies to its Landmark tier, which it likewise never budget-culls).
//
// ## PATTERN SOURCE: PoiCalloutSystem (verbatim, not re-invented)
//
// That class already solves this exact problem for POI node auras - "CAPPED to the
// nearest ~6 auras so we respect VFXManager's shared loop budget". Its shape is
// reused deliberately: RuntimeInitializeOnLoadMethod self-bootstrap, DDOL
// singleton, a throttled tick rather than per-frame work, scratch lists reused
// across ticks to avoid alloc churn, and a sort that only runs when the candidate
// count actually exceeds the budget. Two independent nearest-N implementations
// would be two things to tune and one to forget.
//
// ## NO SILENT DROPS (CLAUDE.md section 12)
//
// Every cull is FlowTrace-throttled. A missing aura must be DIAGNOSABLE - "the
// culler revoked 4 of 19 candidates this tick" is an answer; an aura that simply
// is not there is the invisible-failure class this project keeps paying for.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// A persistent aura that consents to being distance-culled. Implemented by the
    /// ENEMY and PET aura drivers only - see the class header for why towers, the
    /// Heart and boss phases deliberately do not implement it.
    /// </summary>
    public interface IProximityAura
    {
        /// <summary>Where this aura sits, for the distance sort. Null once the host is gone.</summary>
        Transform AuraTransform { get; }

        /// <summary>
        /// True when the driver's OWN logic says an aura should be showing (alive, has a
        /// recipe, not suppressed). The culler only ever decides WHETHER A SLOT IS
        /// GRANTED - it never decides whether an aura is wanted. Keeping those two
        /// questions apart is what stops a cull from being confused with a state change.
        /// </summary>
        bool WantsAura { get; }

        /// <summary>
        /// Grant (true) or revoke (false) permission to hold a loop. Called only on a
        /// CHANGE, so an implementation may treat it as an edge, not a poll. A revoked
        /// driver must stop its loop; a granted one may start it on its own schedule.
        /// </summary>
        void SetAuraAllowed(bool allowed);
    }

    /// <summary>
    /// Keeps only the <see cref="VfxLoopBudget.NearestAuraRing"/> nearest enemy/pet
    /// auras holding loops; revokes the rest and re-grants them when they close.
    /// Self-bootstrapping singleton; owns no VFX handles of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxAuraProximityCuller : MonoBehaviour
    {
        public static VfxAuraProximityCuller Instance { get; private set; }

        // Ambient bookkeeping, not combat logic - a per-frame sort of every enemy in
        // the scene would cost more than the loops it saves. PoiCalloutSystem uses
        // 0.35 s for the same reason; matched so the two systems feel alike.
        private const float TickInterval = 0.35f;

        // How far the ranking origin must move before an early re-tick is worth it.
        // Standing still cannot change the ordering, so most ticks are pure waste;
        // this lets the interval stay slow without the ring lagging a sprint.
        private const float OriginMoveRetickSqr = 4f;   // 2 m

        private static readonly List<IProximityAura> _registered = new List<IProximityAura>(48);

        // WO-1229: the AMBIENT ENVIRONMENT ring. A SECOND registry inside THIS class, not a
        // second culler - the header's own rule ("two independent nearest-N implementations
        // would be two things to tune and one to forget") applies to this ticket more than
        // any other, because the two rings differ ONLY in their budget. Same registry shape,
        // same distance sort, same edge/poll grant call; different budget and different
        // trace tag. See VfxLoopBudget.AmbientEnvBudget for why ambient's budget is dynamic
        // where the combat ring's is fixed.
        private static readonly List<IProximityAura> _registeredAmbient = new List<IProximityAura>(64);

        // Scratch, reused every tick (no per-tick allocation). Both passes share it: the
        // combat pass has finished with it before the ambient pass clears and refills it.
        private readonly List<IProximityAura> _candidates = new List<IProximityAura>(64);
        private readonly List<float> _sqrDistances = new List<float>(64);
        private readonly HashSet<IProximityAura> _granted = new HashSet<IProximityAura>();
        private readonly HashSet<IProximityAura> _grantedAmbient = new HashSet<IProximityAura>();

        private Transform _hero;
        private float _heroFindTimer;
        private const float HeroFindInterval = 0.5f;

        private float _tickTimer;
        private Vector3 _lastOrigin;
        private int _lastCulledCount;

        // =====================================================================
        //  Registration - the ONLY way into the ring
        // =====================================================================

        /// <summary>
        /// Opt <paramref name="aura"/> into distance culling. Idempotent. Safe before the
        /// culler exists (the static list is the registry; the component is only the
        /// ticker), which matters because enemies spawn during scene load.
        /// </summary>
        public static void Register(IProximityAura aura)
        {
            if (aura == null) return;
            if (_registered.Contains(aura)) return;
            _registered.Add(aura);
        }

        /// <summary>
        /// Remove <paramref name="aura"/> from the ring. MUST be called from the driver's
        /// OnDisable/OnDestroy: a registry entry whose host is gone would otherwise occupy
        /// a slot in the nearest-N ranking forever and starve a live enemy of an aura -
        /// the same leak shape as an unstopped loop, one level up. The tick also prunes
        /// dead entries defensively, because "must be called" is not a guarantee.
        /// </summary>
        public static void Unregister(IProximityAura aura)
        {
            if (aura == null) return;
            _registered.Remove(aura);
        }

        /// <summary>
        /// WO-1229: opt an AMBIENT ENVIRONMENT loop (dungeon candle, brazier, steam vent)
        /// into the ambient ring. Separate from <see cref="Register"/> on purpose: room
        /// dress must not compete with enemy/pet auras for the SAME N, or 44 candles would
        /// take all eight slots of a ring that exists to keep enemy role-reads on screen.
        /// Idempotent; safe before the culler component exists.
        /// </summary>
        public static void RegisterAmbient(IProximityAura ambient)
        {
            if (ambient == null) return;
            if (_registeredAmbient.Contains(ambient)) return;
            _registeredAmbient.Add(ambient);
        }

        /// <summary>Remove an ambient loop from the ambient ring. See <see cref="Unregister"/>.</summary>
        public static void UnregisterAmbient(IProximityAura ambient)
        {
            if (ambient == null) return;
            _registeredAmbient.Remove(ambient);
        }

        /// <summary>How many drivers are currently in the ring. Exposed for headless verification.</summary>
        public static int RegisteredCount => _registered.Count;

        /// <summary>How many ambient loops are registered. Exposed for headless verification.</summary>
        public static int AmbientRegisteredCount => _registeredAmbient.Count;

        /// <summary>How many ambient loops currently hold a grant. Exposed for headless verification.</summary>
        public int AmbientGrantedCount => _grantedAmbient.Count;

        /// <summary>How many candidates the last tick revoked. Exposed for headless verification.</summary>
        public int LastCulledCount => _lastCulledCount;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            try
            {
                if (Instance != null) return;
                var go = new GameObject("[VfxAuraProximityCuller]");
                go.AddComponent<VfxAuraProximityCuller>();
                Object.DontDestroyOnLoad(go);
            }
            catch (System.Exception ex)
            {
                // Degrade to "no culling" - every driver keeps its own cap-refusal
                // handling - rather than throwing into the scene loader.
                Debug.LogWarning("[VfxAuraCuller] bootstrap skipped: " + ex.Message);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            // Leave the world in the PERMISSIVE state. If this ticker dies, every driver
            // must be able to run its own aura unaided; a revoked driver with nothing left
            // to re-grant it would be permanently auraless with no error anywhere.
            for (int i = 0; i < _registered.Count; i++)
            {
                var a = _registered[i];
                if (a != null) Guard.Try("VfxAuraCuller", "restore grant on teardown",
                                         () => a.SetAuraAllowed(true));
            }
            // WO-1229: the ambient ring gets the SAME permissive teardown, for the same
            // reason - a revoked candle with nothing left to re-grant it would be dark
            // forever with no error anywhere. Ambient drivers additionally self-permit
            // while Instance is null, so this is belt AND braces.
            for (int i = 0; i < _registeredAmbient.Count; i++)
            {
                var a = _registeredAmbient[i];
                if (a != null) Guard.Try("VfxAuraCuller", "restore ambient grant on teardown",
                                         () => a.SetAuraAllowed(true));
            }
            _granted.Clear();
            _grantedAmbient.Clear();
            Instance = null;
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;

            Vector3 origin = RankingOrigin();
            bool moved = (origin - _lastOrigin).sqrMagnitude >= OriginMoveRetickSqr;
            if (_tickTimer > 0f && !moved) return;

            _tickTimer = TickInterval;
            _lastOrigin = origin;
            Tick(origin);
        }

        // =====================================================================
        //  The ring
        // =====================================================================

        private void Tick(Vector3 origin)
        {
            _candidates.Clear();
            _sqrDistances.Clear();

            // One pass: prune dead registrations, revoke anything that no longer wants an
            // aura, and collect the live candidates with their distances.
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                var a = _registered[i];
                if (a == null) { _registered.RemoveAt(i); continue; }

                Transform t = a.AuraTransform;
                if (t == null)
                {
                    // Host destroyed without an Unregister. Drop it so it cannot hold a
                    // ranking slot against a live enemy.
                    _registered.RemoveAt(i);
                    _granted.Remove(a);
                    continue;
                }

                if (!a.WantsAura) { _granted.Remove(a); continue; }

                _candidates.Add(a);
                _sqrDistances.Add((t.position - origin).sqrMagnitude);
            }

            // WO-1242: the AUTHORED ring is VfxLoopBudget.NearestAuraRing and it is not
            // changed here. VfxPerformanceGate returns exactly that value unless the
            // measured frame-time gate has already shed ALL ambient dress and is still
            // over the device's budget - combat auras are the LAST thing shed, and only
            // ever halved with a floor of 2, never switched off.
            int authoredAuraRing = Mathf.Max(0, VfxLoopBudget.NearestAuraRing);
            int budget = Mathf.Clamp(VfxPerformanceGate.AuraRingNow, 0, authoredAuraRing);
            int total  = _candidates.Count;

            // Only sort when the budget actually bites. Under the ring everyone is
            // granted and the ordering is irrelevant - which is the common case in a
            // village and is not worth an O(n log n) every tick.
            if (total > budget) SortCandidatesByDistance();

            int culled = 0;
            for (int i = 0; i < total; i++)
            {
                var a = _candidates[i];
                bool allow = i < budget;
                bool had   = _granted.Contains(a);

                if (allow == had) continue;   // edge-triggered: only report real changes

                if (allow) _granted.Add(a);
                else     { _granted.Remove(a); culled++; }

                Guard.Try("VfxAuraCuller", "SetAuraAllowed(" + allow + ")",
                          () => a.SetAuraAllowed(allow));
            }

            _lastCulledCount = Mathf.Max(0, total - budget);

            // NO SILENT DROPS. A missing aura has to be answerable from the log, or the
            // next person debugging one re-derives this whole system from scratch.
            if (total > budget)
            {
                FlowTrace.Throttle("VfxAuraCuller", "cull", 1f,
                    "nearest-N ring: " + budget + " of " + total + " enemy/pet aura candidate(s) " +
                    "hold a loop; " + (total - budget) + " beyond the ring are REVOKED (" + culled +
                    " changed state this tick)." +
                    (budget < authoredAuraRing
                        ? " NOTE: the ring is TRIMMED from its authored " + authoredAuraRing +
                          " by the frame-time gate at shed level " + VfxPerformanceGate.Level +
                          " (WO-1242) - a measured, traced quality drop, not a bug."
                        : "") +
                    " Ranking origin=" + origin.ToString("F1") +
                    ", scene tier=" + VfxLoopBudget.TierName + " (loop cap " + VfxLoopBudget.CurrentCap +
                    "). This is the budget guard working, NOT a missing effect - the revoked auras " +
                    "return automatically as their hosts close on the view. Towers, the Heart, boss " +
                    "phases and all one-shots are never culled here (they do not register).");
            }

            TickAmbient(origin);
        }

        // =====================================================================
        //  WO-1229 - the ambient environment ring
        // =====================================================================
        //
        // THE LINE THIS EXISTS TO DELETE, captured on the owner's device in
        // dg_starter_loop (08-25 19:30:29 -> 19:31:26, saturated the whole time):
        //
        //   [Flow:DungeonVFX] bound 44 CandleAnchor marker(s) ... in 'dg_starter_loop'.
        //   [Flow:VFXManager] PlayLoop('Env_Candle')     SKIPPED - active loops 24/24
        //   [Flow:VFXManager] PlayLoop('Aura_NearDeath') SKIPPED - active loops 24/24
        //   [Flow:HeroHpAura] 'NearDeath' aura was REFUSED ... the hero has no
        //                     non-colour danger signal. Retrying.
        //
        // TWO DELIBERATE DIFFERENCES FROM THE COMBAT PASS ABOVE:
        //
        //  1. THE GRANT CALL IS A POLL, NOT AN EDGE. The combat pass may be edge-
        //     triggered because an enemy driver's own WantsAura is what starts and stops
        //     its aura; the grant only gates it. An ambient candle has no such driver -
        //     the grant IS its whole policy - so a candle that entered the ring between
        //     ticks, or that was constructed after the last edge, must still be told.
        //     44 virtual calls every 0.35 s is not a cost worth a correctness hole.
        //  2. THE BUDGET IS DYNAMIC (VfxLoopBudget.AmbientEnvBudget). The combat ring is
        //     a fixed 8 because enemy auras are the only thing in it. Ambient dress is
        //     the class that must YIELD to everything else in the pool, so its ring
        //     shrinks as the rest of the pool fills, and it stops entirely before it can
        //     touch the accessibility reserve.
        private void TickAmbient(Vector3 origin)
        {
            _candidates.Clear();
            _sqrDistances.Clear();

            for (int i = _registeredAmbient.Count - 1; i >= 0; i--)
            {
                var a = _registeredAmbient[i];
                if (a == null) { _registeredAmbient.RemoveAt(i); continue; }

                Transform t = a.AuraTransform;
                if (t == null)
                {
                    _registeredAmbient.RemoveAt(i);
                    _grantedAmbient.Remove(a);
                    continue;
                }

                if (!a.WantsAura)
                {
                    // Out of its own range: it is not a candidate and it is not holding a
                    // grant. The driver stops its own flame on the same edge (range is the
                    // driver's question, budget is ours).
                    _grantedAmbient.Remove(a);
                    continue;
                }

                _candidates.Add(a);
                _sqrDistances.Add((t.position - origin).sqrMagnitude);
            }

            int live = 0, cap = VfxLoopBudget.CurrentCap;
            var mgr = VFXManager.Instance;
            if (mgr != null) { live = mgr.ActiveLoopCount; cap = mgr.MaxActiveLoops; }

            // WO-1229 budget, then the WO-1242 frame-time shed on top of it. The two are
            // deliberately layered rather than merged: AmbientEnvBudget answers "how much
            // room is there in the pool", which is a CORRECTNESS question and is authored;
            // AmbientRingNow answers "how much can this device afford right now", which is
            // a MEASURED one. Min() of the two means the shed can only ever take dress
            // away, never hand out a slot the reserve was holding.
            int poolBudget  = VfxLoopBudget.AmbientEnvBudget(live, _grantedAmbient.Count, cap);
            int shedRing    = Mathf.Max(0, VfxPerformanceGate.AmbientRingNow);
            int budget      = Mathf.Min(poolBudget, shedRing);
            int total  = _candidates.Count;

            if (total > budget) SortCandidatesByDistance();

            int revoked = 0, granted = 0;
            for (int i = 0; i < total; i++)
            {
                var a = _candidates[i];
                bool allow = i < budget;
                bool had   = _grantedAmbient.Contains(a);

                if (allow && !had)  { _grantedAmbient.Add(a);    granted++; }
                if (!allow && had)  { _grantedAmbient.Remove(a); revoked++; }

                // Poll, not edge - see difference (1) in the header above.
                Guard.Try("VfxAmbientRing", "SetAuraAllowed(" + allow + ")",
                          () => a.SetAuraAllowed(allow));
            }

            // THE RECLAIM LINE. Section 12: the fix for WO-1229 is not "a Stop() was
            // added", it is a captured line showing the ambient hold going UP AND DOWN
            // while the pool stays off its ceiling. This prints the ambient hold, the
            // budget that produced it, the pool occupancy and the reserve - every number
            // needed to judge the guard from a log with no code in front of you.
            if (total > 0)
            {
                FlowTrace.Throttle("VfxAmbientRing", "ambient-ring", 1f,
                    "ambient env ring: " + _grantedAmbient.Count + " of " + total +
                    " candidate(s) hold a loop (budget " + budget + ", ring max " +
                    VfxLoopBudget.AmbientEnvRing + "; +" + granted + " granted / -" + revoked +
                    " RELEASED this tick). Pool " + live + "/" + cap + ", reserve " +
                    VfxLoopBudget.AccessibilityReserve + " slot(s) held open for the low-HP tell. " +
                    (shedRing < poolBudget
                        ? "THE FRAME-TIME GATE IS SHEDDING: ambient ring trimmed to " + shedRing +
                          " (the pool would have allowed " + poolBudget + ") at shed level " +
                          VfxPerformanceGate.Level + " - WO-1242, a measured and traced quality drop, " +
                          "NOT a bug. See [Flow:VfxPerfGate] for the frame times that caused it. The " +
                          "low-HP tell is EXEMPT and is unaffected. "
                        : "") +
                    "Beyond-ring dress is dark BY BUDGET, not missing - it relights as the hero closes.");
            }
        }

        /// <summary>
        /// Insertion sort over the parallel candidate/distance lists. n is bounded by the
        /// live enemy population and the list is nearly-sorted between ticks (bodies move
        /// a little, the ordering rarely churns), which is the case insertion sort wins.
        /// Sorting a List of pairs instead would allocate a comparer closure every tick.
        /// </summary>
        private void SortCandidatesByDistance()
        {
            for (int i = 1; i < _candidates.Count; i++)
            {
                var a = _candidates[i];
                float d = _sqrDistances[i];
                int j = i - 1;
                while (j >= 0 && _sqrDistances[j] > d)
                {
                    _candidates[j + 1] = _candidates[j];
                    _sqrDistances[j + 1] = _sqrDistances[j];
                    j--;
                }
                _candidates[j + 1] = a;
                _sqrDistances[j + 1] = d;
            }
        }

        /// <summary>
        /// What "nearest" is measured from: the CAMERA when there is one, else the hero.
        /// The camera is the right origin because this culls by what can be RESOLVED on
        /// screen, and in a pulled-back strategy view the camera can sit far from the
        /// hero. The hero is the fallback (and matches PoiCalloutSystem) for headless
        /// runs and any frame before a camera exists.
        /// </summary>
        private Vector3 RankingOrigin()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform.position;

            EnsureHero();
            return _hero != null ? _hero.position : Vector3.zero;
        }

        private void EnsureHero()
        {
            if (_hero != null) return;
            _heroFindTimer -= Time.deltaTime;
            if (_heroFindTimer > 0f) return;
            _heroFindTimer = HeroFindInterval;
            var p = SafeFindWithTag("Player");
            _hero = p != null ? p.transform : null;
        }

        /// <summary>Undefined-tag-safe FindWithTag (Unity throws on an undefined tag).</summary>
        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }
    }
}
