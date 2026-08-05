// =============================================================================
// ConstructionWorker + ConstructionWorkerPool -- WO-871: a builder NPC stands at a
// structure and works for exactly as long as its build/upgrade job is in flight.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ONE AUTHORITY, ASKED NOT DUPLICATED (CLAUDE.md sec.7 / WO-856 lesson):
// this file has NO notion of "is this building". It never reads BuildTimerService,
// never keeps a level/state of its own, and never polls. UnderConstructionVisual --
// THE per-structure "has a live build job" hook -- spawns a worker in Bind() and
// releases it in Reveal() + OnDestroy(), exactly beside the _upgradeLoop VFX handle
// it already manages. The worker's lifetime IS the scaffold's lifetime.
//
// WO-753 TEARDOWN (one owner, impossible to orphan) -- four independent guarantees:
//   1. EXPLICIT   -- UnderConstructionVisual.StopWorker() runs from Reveal (job
//                    completed) AND OnDestroy (cancel / move / structure destroyed /
//                    scene teardown). Same handle discipline as StopUpgradeLoop.
//   2. SELF-HEAL  -- Update() releases the worker the moment its owning scaffold
//                    component is gone (Warn-traced). Covers any path that skips (1).
//   3. POOL REAP  -- ConstructionWorkerPool.Prune reclaims any LIVE worker whose owner
//                    is gone, on every Spawn and every count query. (1) and (2) are
//                    faster but both need Unity to deliver a message; this one does
//                    not, so the property holds BY CONSTRUCTION. Added 2026-08-04 after
//                    the headless run proved a destroyed structure could leave a worker
//                    standing (Builds/wo871-verify.log).
//   4. STRUCTURAL -- a worker is never parented to the structure, so it can never be
//                    half-destroyed with it; and a released body is deactivated
//                    before it re-enters the pool, so a stale body cannot render.
//   The worker is deliberately NOT a child of the host: re-parenting during a host's
//   OnDestroy is an error in Unity, which is precisely how a "worker with no building"
//   would have been born.
//
// PERFORMANCE (bounded, pooled, silent when idle):
//   * A pooled body sits INACTIVE on the pool root -- Unity does not tick Update on an
//     inactive GameObject, so with nothing under construction the cost is exactly zero.
//   * MaxLive caps concurrent workers; past the cap Spawn returns null (throttled trace)
//     and the build proceeds with the scaffold's dim + countdown + aura only.
//   * Bodies are leased, never Instantiate/Destroy'd per build (WO-871 sec.3).
//   * Update does no allocation: one null check + one float countdown.
//
// ANIMATION OVER MINUTES, NOT SECONDS (WO-855 Phase 4 stretched build tiers to
// 30s / 1.5m / 4.5m / 13.5m / 40m / 2h): a single 3-second loop is maddening at 2 hours.
// So the shared controller carries TWO states -- Work and Rest -- and each worker
// drives its own randomized work/rest cycle with a random start phase and a small
// per-worker animator speed jitter, so several workers never chop in lockstep and no
// single loop plays uninterrupted for more than ~16 seconds. See BuilderWorkerAnimatorSetup.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// WO-871 -- the leased builder body standing at a structure with a live build job.
    /// Created and recycled ONLY by <see cref="ConstructionWorkerPool"/>; owned for its
    /// whole life by the <see cref="UnderConstructionVisual"/> that spawned it.
    /// </summary>
    internal sealed class ConstructionWorker : MonoBehaviour
    {
        /// <summary>Animator bool the shared controller switches Work &lt;-&gt; Rest on.</summary>
        internal const string RestParam = "Rest";

        // Randomized cycle bounds. A work bout is long enough to read as real labour and
        // short enough that the un-looped chop take never repeats more than ~4 times before
        // the rest beat resets the eye. Rest is the calm standby idle (a genuinely looping clip).
        private const float WorkMinSeconds = 9f;
        private const float WorkMaxSeconds = 16f;
        private const float RestMinSeconds = 2.5f;
        private const float RestMaxSeconds = 5f;

        // Re-anchor only if the structure actually moved (F8-51 move-mid-build re-keys the
        // scaffold); checked once per phase flip, never per frame.
        private const float ReanchorDistance = 1f;

        private UnderConstructionVisual _owner;   // THE authority -- gone => this worker goes
        private Transform _host;
        private string _key;
        private Animator _animator;
        private bool _hasRestParam;
        private Vector3 _hostPosAtAnchor;
        private float _phaseTimer;
        private bool _resting;

        /// <summary>The job key this worker was leased for (trace/verify only).</summary>
        internal string Key => _key;

        /// <summary>True while this worker is leased to a live build job.</summary>
        internal bool IsLive => _owner != null && _host != null && gameObject.activeSelf;

        /// <summary>
        /// True when this worker is still holding a lease but the thing it was leased FOR is gone --
        /// its UnderConstructionVisual was destroyed (structure removed / cancelled) without an
        /// explicit release. The scaffold is the only authority consulted; nothing here re-derives
        /// "is it still building". <see cref="ConstructionWorkerPool"/> reaps these.
        /// </summary>
        internal bool IsOrphaned => _owner == null || _host == null;

        /// <summary>
        /// Take up station at <paramref name="host"/> for job <paramref name="key"/>, owned by
        /// <paramref name="owner"/>. Positions beside the structure facing it, activates the
        /// body, and starts a randomized work/rest cycle. Called only by the pool.
        /// </summary>
        internal void Bind(UnderConstructionVisual owner, Transform host, string key, Animator animator)
        {
            _owner = owner;
            _host = host;
            _key = key;
            _animator = animator;
            _hasRestParam = HasRestParam(animator);

            // Activate FIRST: NpcGroundSeat measures live renderer bounds, and an inactive
            // GameObject's Renderer bounds are stale -- seating before activation mis-plants the feet.
            gameObject.SetActive(true);
            Anchor();

            _resting = false;
            // Random START phase (not just random duration) so two workers spawned in the same
            // frame -- the common case when a player queues several buildings -- are never in step.
            _phaseTimer = Random.Range(WorkMinSeconds * 0.35f, WorkMaxSeconds);
            if (_animator != null)
            {
                _animator.speed = Random.Range(0.92f, 1.08f);
                if (_hasRestParam) _animator.SetBool(RestParam, false);
            }
        }

        /// <summary>
        /// Does this animator's controller actually declare the Rest bool? Setting a parameter a
        /// controller does not have logs a Unity error every call, and a work-only controller
        /// (rest clip missing at bake time) is a legal degradation -- so we probe first.
        ///
        /// -- 2026-08-04: THIS PROBE USED TWO SOURCES OF TRUTH AND THREW ----------------------
        /// The first cut walked `animator.parameterCount` and indexed `animator.GetParameter(i)`.
        /// Those are DIFFERENT sources: GetParameter indexes Animator's cached `parameters` array
        /// while parameterCount comes live off the native controller, and on an Animator that has
        /// never been initialized (edit mode, or a pooled body that has not run a frame yet) they
        /// disagree. PROVEN by the headless DataRegression run (Builds/wo871-stack.log), not
        /// inferred:
        ///     IndexOutOfRangeException: Index must be between 0 and 1
        ///       at UnityEngine.Animator.GetParameter (System.Int32 index)
        ///       at ConstructionWorker.HasRestParam ... ConstructionWorker.cs:124
        ///       at ConstructionWorker.Bind ... :98
        ///       at ConstructionWorkerPool.Spawn ... :319
        ///       at UnderConstructionVisual.Bind ... UnderConstructionVisual.cs:183
        ///       at UnderConstructionVisual.Attach ... :91
        /// -- i.e. parameterCount reported >= 2 while parameters.Length was 1.
        ///
        /// Read the trace one step further and this was never a test problem: Attach is called
        /// straight from BuildModeController placement and from BaseLayoutLoader on load, and
        /// nothing on that path caught it. A cosmetic worker animation was one Unity quirk away
        /// from throwing out of every structure placement and every mid-build reload. So: ONE
        /// source of truth (the array, read once), and the probe is Guard-wrapped so a future
        /// quirk degrades to "no Rest parameter" instead of aborting a build.
        /// </summary>
        private static bool HasRestParam(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            bool has = false;
            Guard.Try("BuildWorker", "probe builder animator for the Rest parameter", () =>
            {
                var ps = animator.parameters;   // the ONE authority -- never paired with parameterCount
                if (ps == null) return;
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    if (p == null || p.type != AnimatorControllerParameterType.Bool || p.name != RestParam)
                        continue;
                    has = true;
                    return;
                }
            });
            return has;
        }

        /// <summary>
        /// Hand this worker back to the pool: stop animating, deactivate, forget the host.
        /// Idempotent -- a second call on an already-released worker is a no-op, which is what
        /// makes the Reveal + OnDestroy double-call safe (same contract as StopUpgradeLoop).
        /// </summary>
        internal void Release()
        {
            if (_owner == null && _host == null && !gameObject.activeSelf) return;   // already parked
            ConstructionWorkerPool.Return(this);
        }

        /// <summary>Pool-internal park step: clear the lease and deactivate. Never destroys.</summary>
        internal void Park(Transform poolRoot)
        {
            _owner = null;
            _host = null;
            _key = null;
            if (_animator != null) _animator.speed = 1f;
            gameObject.SetActive(false);
            // Already-parented in the normal case; guarded so we never re-parent needlessly
            // (and never at all while a host is tearing down -- we are not its child).
            if (poolRoot != null && transform.parent != poolRoot) transform.SetParent(poolRoot, false);
        }

        private void Update()
        {
            // SELF-HEAL (guarantee 2): the scaffold is the single authority for "still building".
            // The instant it is gone -- revealed, cancelled, or destroyed with the structure --
            // this worker has no reason to exist. No second "is it building" notion is consulted.
            if (_owner == null || _host == null)
            {
                FlowTrace.Warn("BuildWorker",
                    $"worker for '{_key ?? "<none>"}' outlived its UnderConstructionVisual " +
                    "(scaffold or host gone without an explicit release) -- self-releasing to the pool. " +
                    "No orphaned worker is left standing at a finished/removed structure.");
                Release();
                return;
            }

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            _resting = !_resting;
            _phaseTimer = _resting
                ? Random.Range(RestMinSeconds, RestMaxSeconds)
                : Random.Range(WorkMinSeconds, WorkMaxSeconds);
            if (_animator != null && _hasRestParam) _animator.SetBool(RestParam, _resting);

            // Cheap (once per phase, ~1 per 10s) catch for a structure dragged mid-build.
            if ((_host.position - _hostPosAtAnchor).sqrMagnitude > ReanchorDistance * ReanchorDistance)
                Anchor();
        }

        /// <summary>
        /// Stand beside the structure, facing it. The side is DERIVED FROM THE JOB KEY, so it is
        /// stable across a save/reload of the same in-flight job and different per structure
        /// (two adjacent builds do not put their workers in the same spot).
        /// </summary>
        private void Anchor()
        {
            if (_host == null) return;
            _hostPosAtAnchor = _host.position;

            Vector3 centre = _host.position;
            float radius = 1.6f;

            var rends = _host.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++)
                    if (rends[i] != null) b.Encapsulate(rends[i].bounds);
                centre = new Vector3(b.center.x, b.min.y, b.center.z);
                radius = Mathf.Max(b.extents.x, b.extents.z) + 0.9f;
            }
            radius = Mathf.Clamp(radius, 1.2f, 6f);

            float angle = (StableAngleDegrees(_key)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 pos = centre + dir * radius;

            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas)) pos = hit.position;

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-dir, Vector3.up));
            NpcGroundSeat.Seat(gameObject, pos.y);
        }

        /// <summary>
        /// FNV-1a over the job key -> a stable 0..359 bearing. Deliberately NOT string.GetHashCode
        /// (not guaranteed stable across runtimes), so a reloaded job re-anchors its worker on the
        /// same side of the same building.
        /// </summary>
        private static float StableAngleDegrees(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0f;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < key.Length; i++)
                {
                    h ^= key[i];
                    h *= 16777619u;
                }
                return h % 360u;
            }
        }
    }

    /// <summary>
    /// WO-871 -- the bounded lease pool for <see cref="ConstructionWorker"/> bodies. Static (not a
    /// MonoBehaviour singleton) so it works identically in play mode and in the edit-mode
    /// regression that drives Attach/Reveal without a play session.
    /// </summary>
    public static class ConstructionWorkerPool
    {
        /// <summary>Hard ceiling on concurrent workers. A town can queue more builds than this;
        /// the extras simply get the scaffold's dim + countdown + aura with no worker, which is
        /// the correct degradation -- a hundred chopping bodies is a frame-rate bug, not a feature.
        /// PUBLIC because UnderConstructionGateRegression (a separate assembly, and reflection is
        /// banned by CLAUDE.md sec.10) asserts the bound.</summary>
        public const int MaxLive = 8;

        /// <summary>
        /// The builder body. OWNER-RETAGGABLE IN ONE WORD (memory `vfx-map-owner-tags-no-creative-pick`
        /// -- the CLI must not creative-pick art). Default 'Engineer': the KayKit Adventurers 2.0
        /// engineer, the only builder archetype in the STAGED, git-TRACKED body set
        /// (Assets/Resources/NPCs/KayKit/, WO-818) and already the workshop's catalog npcModel.
        /// Other staged options: Farmer_A, Farmer_B, Hoarder, Barbarian, Druid, Cleric, Mage,
        /// Ranger, Tiefling, BlackKnight, Paladin_with_Helmet.
        /// NOT an Echo -- Echoes stay 2D portrait spirits (WO-871 sec.5).
        /// </summary>
        public const string BodySlug = "Engineer";

        /// <summary>Resources path of the WO-871 work/rest controller
        /// (built by DeNelle.Editor.BuilderWorkerAnimatorSetup.Build).</summary>
        internal const string WorkControllerRes = "NPCs/KayKit/BuilderWorkerWork";

        private const string PoolRootName = "~ConstructionWorkers";

        private static readonly List<ConstructionWorker> Live = new List<ConstructionWorker>();
        private static readonly List<ConstructionWorker> Idle = new List<ConstructionWorker>();
        private static Transform _root;
        private static bool _pruning;   // reentrancy guard for the orphan reap in Prune

        /// <summary>Live (leased) worker count -- regression/verify surface.</summary>
        public static int LiveCount { get { Prune(); return Live.Count; } }

        /// <summary>Parked (pooled, inactive) worker count -- regression/verify surface.</summary>
        public static int IdleCount { get { Prune(); return Idle.Count; } }

        /// <summary>
        /// Lease a worker for the structure under <paramref name="host"/>. Returns null -- and the
        /// build proceeds normally -- when the body/controller assets are absent, when the cap is
        /// reached, or when the instantiate fails. A null return is never an error for the build.
        /// </summary>
        internal static ConstructionWorker Spawn(UnderConstructionVisual owner, Transform host, string key)
        {
            if (owner == null || host == null) return null;
            Prune();

            if (Live.Count >= MaxLive)
            {
                FlowTrace.Throttle("BuildWorker", "cap", 5f,
                    $"worker cap reached ({Live.Count}/{MaxLive}) -- '{key}' builds with no worker " +
                    "(scaffold dim + countdown + aura unchanged). This is the bound, not a failure.");
                return null;
            }

            var controller = Resources.Load<RuntimeAnimatorController>(WorkControllerRes);
            if (controller == null)
            {
                // A body with no controller renders its BIND POSE -- the owner's 2026-08-02
                // "NPC Stuck in T Pose". A T-posing builder is worse than no builder, so skip.
                FlowTrace.Once("BuildWorker", "no-controller",
                    $"work controller MISSING at Resources/{WorkControllerRes} -- no build worker will spawn " +
                    "(a controller-less humanoid renders its T-pose bind pose). Rebuild it: " +
                    "Defenders/Art/Build Builder Worker Controller (DeNelle.Editor.BuilderWorkerAnimatorSetup.Build).");
                return null;
            }

            ConstructionWorker worker = null;
            for (int i = Idle.Count - 1; i >= 0 && worker == null; i--)
            {
                worker = Idle[i];
                Idle.RemoveAt(i);
            }
            if (worker == null) worker = CreateNew();
            if (worker == null) return null;

            worker.Bind(owner, host, key, worker.GetComponentInChildren<Animator>(true));
            Live.Add(worker);

            FlowTrace.Step("BuildWorker",
                $"worker SPAWNED for '{key}' on '{host.name}' at {worker.transform.position} " +
                $"(body={BodySlug}, live={Live.Count}/{MaxLive}, parked={Idle.Count}).");
            return worker;
        }

        /// <summary>
        /// Park <paramref name="worker"/> back in the pool. Called from
        /// <see cref="ConstructionWorker.Release"/> only.
        /// </summary>
        internal static void Return(ConstructionWorker worker)
        {
            if (worker == null) return;
            string key = worker.Key;

            Live.Remove(worker);
            worker.Park(EnsureRoot());
            if (!Idle.Contains(worker)) Idle.Add(worker);
            Prune();

            FlowTrace.Step("BuildWorker",
                $"worker DESPAWNED for '{key ?? "<none>"}' -- parked inactive in the pool " +
                $"(live={Live.Count}/{MaxLive}, parked={Idle.Count}). No worker survives its build job.");
        }

        /// <summary>
        /// Tear the whole pool down (bodies included). Used by the edit-mode regression so a
        /// verification run leaves no builder standing in the open scene; never called at runtime.
        /// </summary>
        public static void DisposeAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--) DestroyBody(Live[i]);
            for (int i = Idle.Count - 1; i >= 0; i--) DestroyBody(Idle[i]);
            Live.Clear();
            Idle.Clear();
            if (_root != null) DestroyHost(_root.gameObject);
            _root = null;
        }

        private static ConstructionWorker CreateNew()
        {
            var prefab = Resources.Load<GameObject>(KayKitNpcBody.ResourcesRoot + BodySlug);
            if (prefab == null)
            {
                FlowTrace.Once("BuildWorker", "no-body",
                    $"builder body MISSING at Resources/{KayKitNpcBody.ResourcesRoot}{BodySlug} -- no build " +
                    "worker will spawn (the scaffold's dim + countdown + aura still show the build). " +
                    "Check the staged KayKit bodies under Assets/Resources/NPCs/KayKit/.");
                return null;
            }

            GameObject body = null;
            Guard.Try("BuildWorker", $"instantiate builder body '{BodySlug}'", () =>
            {
                body = Object.Instantiate(prefab, EnsureRoot());
            });
            if (body == null)
            {
                FlowTrace.Fail("BuildWorker",
                    $"Instantiate returned null for builder body '{BodySlug}' -- no worker this build " +
                    "(see the preceding Guard line).");
                return null;
            }

            body.name = "ConstructionWorker";
            body.hideFlags = HideFlags.DontSave;   // never serialized into a curated scene
            NormalizeToHeroHeight(body);
            // WO-833: a staged KayKit body ships an Animator + Humanoid avatar but NO controller.
            // Same arming seam the structure NPCs use -- this one gets the work/rest controller.
            KayKitNpcBody.ArmController(body, KayKitNpcBody.ResourcesRoot + BodySlug,
                                        WorkControllerRes, "BuildWorker");
            body.SetActive(false);

            // ArmController already set applyRootMotion = false (the worker stands its ground).
            return body.GetComponent<ConstructionWorker>() ?? body.AddComponent<ConstructionWorker>();
        }

        /// <summary>The lazily created, never-serialized parent every parked body lives under.
        /// Dies with its scene; <see cref="Prune"/> then drops the stale references.</summary>
        private static Transform EnsureRoot()
        {
            if (_root != null) return _root;
            var go = new GameObject(PoolRootName) { hideFlags = HideFlags.DontSave };
            _root = go.transform;
            return _root;
        }

        /// <summary>
        /// Two jobs, both about never holding a lease that has stopped meaning anything:
        ///
        ///  1. DROP DEAD REFS -- scene unload takes the pool root and every body with it; the static
        ///     lists must not keep corpses or the cap silently jams.
        ///  2. REAP ORPHANS -- release any LIVE worker whose owning UnderConstructionVisual is gone.
        ///
        /// (2) is the guarantee that does not depend on a MonoBehaviour callback firing. The
        /// explicit release (UnderConstructionVisual.OnDestroy -> StopWorker) and the worker's own
        /// Update self-heal are both faster, but both need Unity to run a message: OnDestroy and
        /// Update do NOT fire on an edit-mode object, and a leased worker whose structure was
        /// destroyed would otherwise hold a cap slot until its next tick. PROVEN 2026-08-04 by the
        /// headless run (Builds/wo871-verify.log): with only the callback nets, a structure
        /// destroyed mid-build left "1 worker(s) SURVIVED". Now the property is true BY
        /// CONSTRUCTION -- the pool cannot report or hand out a lease without first reclaiming the
        /// ones whose owner died -- rather than true only if a callback happened to run.
        ///
        /// Reentrancy-guarded: the reap parks bodies directly (list surgery + Park) instead of
        /// calling Release, which would re-enter Return -> Prune.
        /// </summary>
        private static void Prune()
        {
            for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] == null) Live.RemoveAt(i);
            for (int i = Idle.Count - 1; i >= 0; i--) if (Idle[i] == null) Idle.RemoveAt(i);

            if (_pruning) return;
            _pruning = true;
            try
            {
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    var w = Live[i];
                    if (w == null || !w.IsOrphaned) continue;

                    string key = w.Key;
                    Live.RemoveAt(i);
                    w.Park(EnsureRoot());
                    if (!Idle.Contains(w)) Idle.Add(w);

                    FlowTrace.Step("BuildWorker",
                        $"worker REAPED for '{key ?? "<none>"}' -- its UnderConstructionVisual is gone " +
                        "(structure destroyed / cancelled without an explicit release). Parked inactive; " +
                        "no builder is left standing at a structure that no longer exists.");
                }
            }
            finally { _pruning = false; }
        }

        private static void DestroyBody(ConstructionWorker worker)
        {
            if (worker == null) return;
            DestroyHost(worker.gameObject);
        }

        /// <summary>Destroy that also works OUTSIDE play mode (edit-mode regression), mirroring
        /// UnderConstructionVisual.DestroyHost -- Object.Destroy throws in edit mode.</summary>
        private static void DestroyHost(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        /// <summary>Same 1.95m normalization the NPC injectors apply -- the staged packs import at
        /// varying native scales, so an un-normalized builder towers over the hero.</summary>
        private static void NormalizeToHeroHeight(GameObject go)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                if (rends[i] != null) b.Encapsulate(rends[i].bounds);
            if (b.size.y <= 0.01f) return;

            float scale = 1.95f / b.size.y;
            if (scale > 0.01f && !Mathf.Approximately(scale, 1f)) go.transform.localScale *= scale;
        }
    }
}
