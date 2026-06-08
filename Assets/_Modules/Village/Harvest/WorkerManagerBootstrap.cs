// =============================================================================
// WorkerManagerBootstrap — self-installs the WO-117 WorkerManager at runtime so the
// dispatch → travel → auto-collect → bank loop actually RUNS in the world scene.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE GAP THIS CLOSES (found 2026-06-08): WorkerManager, Worker and MineNode's
// TryAutoExtract auto-collect seam were all fully built (WO-117 Phase 1), but
// NOTHING ever instantiated WorkerManager — so the whole dispatch + autocollect
// system was dead code at runtime (no roster spawned, no per-frame harvest tick,
// no click-to-dispatch). Every sibling harvest service self-installs via a
// [RuntimeInitializeOnLoadMethod] bootstrap (OfflineHarvestBootstrap,
// PetHarvestBootstrap, CampSystem) — WorkerManager was simply missing its.
//
// WHERE: the dispatchable MineNodes live in the OuterWorld scene (baked per-region
// by OuterWorldBuilder), loaded ADDITIVELY by WorldSceneLoader. So we install the
// manager when OuterWorld is the active/loaded scene — never in the village interior
// (no nodes there) — mirroring CampSystem / RaidOutpostSystem's OuterWorld scoping.
//
// RECONCILIATION (no parallel system, no greenfield):
//  • Reuses the existing WorkerManager — this only HOSTS it; all dispatch + collect
//    logic stays in WorkerManager/Worker, and banking stays in MineNode.BankYield →
//    EconomyService (the single unified-wallet faucet). No second economy path.
//  • Offline accrual is WO-115's OfflineHarvestService (separate, already installed);
//    WorkerManager.UseOfflineCatchUp stays OFF so there is no double-grant. This
//    bootstrap touches only the ACTIVE-SESSION autocollect, leaving that seam clean.
//  • No VillageSceneBuilder / OuterWorldBuilder edit, no bake, no scene hand-edit —
//    pure runtime install (CLAUDE.md §3/§9).
//
// PERSISTENCE: worker ASSIGNMENTS are in-session only (not saved). Re-dispatch on
// reload is acceptable for Phase 1; persisting which worker is on which node would
// be a SaveSchema field — FLAGGED for the save owner, NOT edited here (WO-117 §"Do
// NOT add a new save round-trip").
// =============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Installs a single <see cref="WorkerManager"/> into the OuterWorld scene
    /// at runtime so the harvest dispatch + auto-collect loop is live in play.</summary>
    public sealed class WorkerManagerBootstrap : MonoBehaviour
    {
        public static WorkerManagerBootstrap Instance { get; private set; }

        // The scene the dispatchable MineNodes live in (OuterWorldBuilder bakes them
        // per region). WorldSceneLoader loads it additively. Matched by exact name.
        private const string OuterWorldSceneName = "OuterWorld";

        // Phase-1 roster size. Tunable here (and on the spawned WorkerManager in the
        // inspector). Kept small so the demo is legible; WO-117 Phase 3 grows it.
        private const int StartingWorkers = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("WorkerManagerBootstrap");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<WorkerManagerBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // OuterWorld may already be the active (or additively-loaded) scene when
            // this fires (AfterSceneLoad on a direct OuterWorld boot) — install now.
            if (IsOuterWorldLoaded()) InstallManager();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Install when OuterWorld finishes loading (the common path: it loads
        // additively after the village via WorldSceneLoader).
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == OuterWorldSceneName) InstallManager();
        }

        private static bool IsOuterWorldLoaded()
        {
            if (SceneManager.GetActiveScene().name == OuterWorldSceneName) return true;
            var s = SceneManager.GetSceneByName(OuterWorldSceneName);
            return s.IsValid() && s.isLoaded;
        }

        // Drop the WorkerManager into the world the first time OuterWorld is present.
        // Idempotent: the manager is a singleton, so a second call no-ops.
        private void InstallManager()
        {
            if (WorkerManager.Instance != null) return;

            var go = new GameObject("WorkerManager");
            // Live in the OuterWorld scene so the manager (and the workers it spawns)
            // unload cleanly when the player returns to the village interior. If the
            // OuterWorld scene isn't the active one, default placement (this DDOL
            // bootstrap's scene) is fine — the manager only needs the baked NavMesh,
            // which is shared across the additive world.
            var outer = SceneManager.GetSceneByName(OuterWorldSceneName);
            if (outer.IsValid() && outer.isLoaded) SceneManager.MoveGameObjectToScene(go, outer);

            var mgr = go.AddComponent<WorkerManager>();
            mgr.StartingWorkers = StartingWorkers;
            // DropOff defaults to the manager's own transform (village/world origin) in
            // WorkerManager.Awake — no explicit drop-off node needed for Phase 1.
            // UseOfflineCatchUp stays at its default (OFF) so WO-115 owns offline.

            Debug.Log("[WorkerManagerBootstrap] WorkerManager installed in OuterWorld — " +
                      "harvest dispatch + auto-collect is live (tap a MineNode to send a worker).");
        }
    }
}
