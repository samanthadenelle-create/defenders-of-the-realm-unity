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

        // Scratch, reused every tick (no per-tick allocation).
        private readonly List<IProximityAura> _candidates = new List<IProximityAura>(48);
        private readonly List<float> _sqrDistances = new List<float>(48);
        private readonly HashSet<IProximityAura> _granted = new HashSet<IProximityAura>();

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

        /// <summary>How many drivers are currently in the ring. Exposed for headless verification.</summary>
        public static int RegisteredCount => _registered.Count;

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
            _granted.Clear();
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

            int budget = Mathf.Max(0, VfxLoopBudget.NearestAuraRing);
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
                    " changed state this tick). Ranking origin=" + origin.ToString("F1") +
                    ", scene tier=" + VfxLoopBudget.TierName + " (loop cap " + VfxLoopBudget.CurrentCap +
                    "). This is the budget guard working, NOT a missing effect - the revoked auras " +
                    "return automatically as their hosts close on the view. Towers, the Heart, boss " +
                    "phases and all one-shots are never culled here (they do not register).");
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
