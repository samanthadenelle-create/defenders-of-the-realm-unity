// =============================================================================
// WorkerManager — harvest orchestrator + offline catch-up (WO-117 Phase 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// One scene MonoBehaviour. Owns the worker roster, runs the per-frame auto-collect
// tick for every collecting worker, and exposes the read-only registry seam the
// offline-accrual layer (WO-115) consumes. It is the "HarvestService" of the WO,
// named WorkerManager to match the roster it owns.
//
// RECONCILIATION (do NOT blind-build a parallel system):
//  • Resource banking stays in MineNode (WO-142) — workers only drive its existing
//    TryAutoExtract(), so there is ONE banking path into GameState, not two.
//  • No new currency / no new save round-trip for payouts (WO-117 §1, "Do NOT").
//  • Offline progression is self-contained: a single PlayerPrefs timestamp records
//    when the harvest was last live. On Start we integrate each then-assigned node's
//    RatePerSecond over the elapsed wall-clock gap and bank the catch-up haul through
//    MineNode's own path. This does NOT touch the 41-field SaveSchema round-trip, so
//    it adds zero schema-migration risk. When WO-115's richer offline service lands,
//    it can read ActiveAssignments() / node.RatePerSecond and supersede this stub.
//
// Phase 1: a single worker auto-spawns near the village drop-off and can be
// dispatched (DispatchNearestFreeWorkerTo) to a node. No invasions (Phase 2).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class WorkerManager : MonoBehaviour
    {
        public static WorkerManager Instance { get; private set; }

        [Header("Roster (Phase 1)")]
        [Tooltip("How many workers to auto-spawn at Start near the drop-off. Phase 1 = 1.")]
        [Min(0)] public int StartingWorkers = 1;

        [Tooltip("Where freshly-spawned workers appear (the village drop-off). " +
                 "Defaults to this GameObject's position if unset.")]
        public Transform DropOff;

        [Header("Offline catch-up")]
        [Tooltip("Cap on offline seconds credited in one catch-up, so a long absence " +
                 "doesn't overflow a node. 0 = uncapped (still clamped by node depletion).")]
        [Min(0f)] public float MaxOfflineSeconds = 8f * 3600f;   // 8h default cap

        [Tooltip("PlayerPrefs key storing the last-live wall-clock (UTC ticks) of the " +
                 "harvest. Self-contained — outside the 41-field SaveSchema round-trip.")]
        public string LastActiveKey = "dotr-harvest-last-active";

        private readonly List<Worker> _workers = new List<Worker>();

        // Resolved assignments cache, refreshed each frame for the read-only seam.
        private readonly List<MineNode> _activeNodes = new List<MineNode>();

        private void Awake()
        {
            // Destroy(this) — NOT Destroy(gameObject): this component may share a host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (DropOff == null) DropOff = transform;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                StampLastActive();   // record when the harvest went dark, for next launch's catch-up
                Instance = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StampLastActive();
            else ApplyOfflineCatchUp();   // mobile resume — credit the backgrounded gap
        }

        private void OnApplicationQuit() => StampLastActive();

        [Header("Demo dispatch (Phase 1)")]
        [Tooltip("If true, a left-click / tap on a MineNode dispatches the nearest free " +
                 "worker to it (the Phase-1 player verb). Disable when a proper UI drives dispatch.")]
        public bool ClickToDispatch = true;

        [Tooltip("Auto-attach a code-built fill bar to every MineNode found at Start.")]
        public bool AttachFillIndicators = true;

        private void Start()
        {
            for (int i = 0; i < StartingWorkers; i++) SpawnWorker(i);

            if (AttachFillIndicators)
            {
#if UNITY_2023_1_OR_NEWER
                var nodes = Object.FindObjectsByType<MineNode>(FindObjectsSortMode.None);
#else
                var nodes = Object.FindObjectsOfType<MineNode>();
#endif
                for (int i = 0; i < nodes.Length; i++) NodeFillIndicator.Attach(nodes[i]);
            }

            ApplyOfflineCatchUp();
        }

        private void Update()
        {
            if (ClickToDispatch) HandleClickDispatch();

            // Per-frame auto-collect: every collecting worker drives its node's extract.
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (w == null) continue;
                if (w.State == WorkerState.Collecting && w.TargetNode != null)
                {
                    w.TargetNode.TryAutoExtract();   // banks one extract when the node's cooldown elapses
                    // (A collect chime / HUD ping could fire here when the return is > 0; left out
                    //  until an AudioClip-backed path exists — IAudioService.PlaySfx takes a clip.)
                    if (w.TargetNode.IsDepleted)
                        w.Release();   // node spent → free the worker (Phase 1: just idle)
                }
            }
        }

        // Phase-1 demo verb: click/tap a MineNode → send the nearest free worker.
        // Uses legacy Input (MineNode's [F] path uses the same), so no new-Input-System
        // hard dependency is introduced here.
        private void HandleClickDispatch()
        {
            bool pressed = UnityEngine.Input.GetMouseButtonDown(0);
            if (!pressed) return;

            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var node = hit.collider.GetComponentInParent<MineNode>();
                if (node != null) DispatchNearestFreeWorkerTo(node);
            }
        }

        // =====================================================================
        //  Dispatch
        // =====================================================================

        /// <summary>Send the nearest free worker to a node. Refuses if the node is
        /// already worker-claimed or depleted, or no worker is free. Returns the
        /// dispatched worker, or null.</summary>
        public Worker DispatchNearestFreeWorkerTo(MineNode node)
        {
            if (node == null || node.IsDepleted || node.IsClaimedByWorker) return null;

            Worker best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (w == null || !w.IsAvailable) continue;
                float d = (w.transform.position - node.transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = w; }
            }

            if (best == null) return null;
            return best.DispatchTo(node) ? best : null;
        }

        /// <summary>Register an externally-created worker into the roster (e.g. one
        /// authored in the scene rather than auto-spawned).</summary>
        public void RegisterWorker(Worker w)
        {
            if (w != null && !_workers.Contains(w)) _workers.Add(w);
        }

        // =====================================================================
        //  WO-115 offline-accrual seam — READ-ONLY
        // =====================================================================

        /// <summary>The nodes currently being collected (one per on-station worker),
        /// for the offline-accrual layer to integrate RatePerSecond over an absence.
        /// Read-only and null-safe to consume — WO-115 no-ops if this is empty/absent.</summary>
        public IReadOnlyList<MineNode> ActiveAssignments()
        {
            _activeNodes.Clear();
            for (int i = 0; i < _workers.Count; i++)
            {
                var w = _workers[i];
                if (w != null && w.State == WorkerState.Collecting && w.TargetNode != null)
                    _activeNodes.Add(w.TargetNode);
            }
            return _activeNodes;
        }

        // =====================================================================
        //  Offline catch-up (self-contained — outside SaveSchema)
        // =====================================================================

        private void StampLastActive()
        {
            PlayerPrefs.SetString(LastActiveKey, System.DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Credit resource that would have accrued at each then-collecting node
        /// while the player was away. Banks through MineNode's own extract path so the
        /// catch-up haul respects yield, region bonus and depletion. Stamps the new
        /// last-active time at the end.</summary>
        private void ApplyOfflineCatchUp()
        {
            float awaySeconds = ReadAwaySeconds();
            if (awaySeconds <= 0f) { StampLastActive(); return; }

            var nodes = ActiveAssignments();
            if (nodes.Count == 0) { StampLastActive(); return; }

            int total = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsDepleted) continue;
                // Integrate the node's banking rate over the gap, then bank it as whole
                // extracts through the node so depletion/cooldown still gate the haul.
                float owed = node.RatePerSecond * awaySeconds;
                if (owed <= 0f) continue;
                total += BankOfflineToNode(node, owed);
            }

            if (total > 0)
                Debug.Log($"[WorkerManager] Offline catch-up: banked ~{total} across " +
                          $"{nodes.Count} node(s) over {Mathf.RoundToInt(awaySeconds)}s away.");

            StampLastActive();
        }

        /// <summary>Bank an offline-owed amount to a node as repeated extracts (each
        /// extract advances depletion exactly like live collection), stopping when the
        /// owed amount is paid or the node depletes. Returns the total banked.</summary>
        private int BankOfflineToNode(MineNode node, float owed)
        {
            int banked = 0;
            // Force-collect ignoring the live cooldown: offline time already "paid" it.
            // Each ForceExtract advances depletion, so an offline node still runs dry.
            int safety = 100000;   // hard loop guard
            while (owed > 0f && !node.IsDepleted && safety-- > 0)
            {
                int got = node.ForceAutoExtract();
                if (got <= 0) break;
                banked += got;
                owed -= got;
            }
            return banked;
        }

        private float ReadAwaySeconds()
        {
            string raw = PlayerPrefs.GetString(LastActiveKey, string.Empty);
            if (string.IsNullOrEmpty(raw) || !long.TryParse(raw, out long ticks)) return 0f;

            var last = new System.DateTime(ticks, System.DateTimeKind.Utc);
            double seconds = (System.DateTime.UtcNow - last).TotalSeconds;
            if (seconds <= 0) return 0f;
            if (MaxOfflineSeconds > 0f) seconds = System.Math.Min(seconds, MaxOfflineSeconds);
            return (float)seconds;
        }

        // =====================================================================
        //  Runtime worker spawn (no VillageSceneBuilder / no bake — WO-117 §7)
        // =====================================================================

        private void SpawnWorker(int index)
        {
            Vector3 pos = DropOff != null ? DropOff.position : transform.position;
            pos += new Vector3(index * 1.5f, 0f, 0f);   // simple stagger if >1

            // Seat onto the baked NavMesh if one is present.
            if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas))
                pos = hit.position;

            var go = new GameObject($"Worker-{index}");
            go.transform.SetParent(transform, true);
            go.transform.position = pos;

            // A small capsule body so the worker is visible without an art prefab
            // (LogWarning-free stub — pack-agnostic, CLAUDE.md §4).
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);   // body is cosmetic; no physics needed

            // Add a NavMeshAgent only if the spawn point is actually on a mesh, else the
            // worker uses Worker.cs's straight-line fallback so the verb still demos.
            if (NavMesh.SamplePosition(pos, out _, 2f, NavMesh.AllAreas))
            {
                var agent = go.AddComponent<NavMeshAgent>();
                agent.speed = 5f;
                agent.angularSpeed = 720f;
                agent.acceleration = 16f;
                agent.radius = 0.4f;
                agent.height = 1.8f;
                agent.stoppingDistance = 1.5f;
            }

            var worker = go.AddComponent<Worker>();
            _workers.Add(worker);
        }
    }
}
