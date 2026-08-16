// =============================================================================
// PetTaskController -- the pet's TASK state + the repair loop that backs it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-1031 (owner rulings 2026-08-16: "remove this screen then", "it gets managed
// from the echo tab", "the wolf isnt frost or shouldnt be its the first Echo"):
//   THE WORLD ENGAGEMENT PROMPT IS DELETED. Removed with this change:
//     * the Engage / BuildEngageDef pair -- the code-built 2-choice prompt def
//     * TickEngagement -- BOTH trigger paths (the tap AND the proximity auto-greet)
//     * SpeakerName -- the invented species-to-display-name table that bypassed
//       EchoRosterCatalog, the name authority. The guide wolf is Echo #1, ALDWIN,
//       the founding Ice Echo; the name that table invented was never a character.
//     * ApplyEngagementChoice and its dialogue verb (no other producer; verified
//       2026-08-16 -- the deleted def was the only site that emitted it).
//   The prompt was a REDUNDANT SECOND ENTRY POINT: it offered 2 lanes with no
//   resource choice, while the Echo tab (EchoCardView / EchoCardVM ->
//   EchoAssignments) owns the real assignment surface -- the WO-830 per-Echo
//   harvest RESOURCE picker plus the WO-811 "Repair structures" task chip.
//   ONE HOME FOR TASKING, AND IT IS THE ECHO TAB.
//
// WHAT THIS COMPONENT STILL DOES (unchanged backends, no greenfield):
//   * Holds the pet's assigned PetTask (Harvest default, matching the deploy-time
//     PetHarvester the PetDeployer attaches).
//   * SetTask(PetTask) switches the backing loop: Harvest re-enables PetHarvester;
//     Repair disables it and drives the EXISTING WallRepairController.RepairAll
//     backend from TickRepair (the same worst-first, spend-through-the-construction-
//     economy path HubRepairAffordance uses). No second repair system.
//
// DO NOT re-add a world-space prompt here. Tasking is the Echo tab's job. If a
// speaker name is ever needed for an Echo, READ IT FROM EchoRosterCatalog -- never
// hand-author a species -> name table again (WO-1031 sec. 2b/2d).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Pets;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village
{
    /// <summary>The task the pet is performing. Assigned from the Echo tab (WO-1031).</summary>
    public enum PetTask
    {
        /// <summary>Gather resources (the pet's existing PetHarvester loop).</summary>
        Harvest = 0,
        /// <summary>Mend damaged structures (the WallRepairController RepairAll backend).</summary>
        Repair = 1,
    }

    /// <summary>
    /// Holds a deployed <see cref="Pet"/>'s task and runs the repair loop when it is
    /// assigned to Repair. One per deployed Pet (added by <see cref="PetTaskInstaller"/>).
    /// WO-1031: the world engagement prompt this class used to open is REMOVED -- task
    /// assignment lives in the Echo tab (EchoCardView / EchoAssignments).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetTaskController : MonoBehaviour
    {
        // -- Repair tuning ----------------------------------------------------
        private const float RepairScanInterval = 1.5f;

        // -- Runtime ----------------------------------------------------------
        private Pet _pet;
        private PetHarvester _harvester;
        private WallRepairController _repair;

        private PetTask _task = PetTask.Harvest;   // default = gather (matches the deploy-time PetHarvester)
        private float _nextRepairScan;

        /// <summary>The task the pet is currently assigned.</summary>
        public PetTask Task => _task;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            _harvester = GetComponent<PetHarvester>();
        }

        private void Update()
        {
            if (_pet == null || !_pet.IsAlive) return;

            // WO-1031: no engagement tick. Walking near or tapping a pet does NOTHING.
            if (_task == PetTask.Repair) TickRepair();
        }

        /// <summary>Assigns the pet's task and switches the backing loop (Harvest vs Repair).</summary>
        public void SetTask(PetTask task)
        {
            _task = task;
            FlowTrace.Step("Pet", $"task set -> {task} for pet '{(_pet != null ? _pet.PetId : "<null>")}' (source: Echo tab; WO-1031 removed the world prompt).");

            if (_harvester == null) _harvester = GetComponent<PetHarvester>();

            if (task == PetTask.Harvest)
            {
                // Hand back to the existing autonomous gather loop.
                Guard.Try("Pet", "enable harvest task", () =>
                {
                    if (_harvester != null) _harvester.enabled = true;
                });
                FlowTrace.Step("Pet", $"harvest task active -- pet '{PetId()}' will gather via PetHarvester.");
            }
            else
            {
                // Stop gathering so the two loops don't fight; repair runs from TickRepair.
                Guard.Try("Pet", "disable harvest for repair task", () =>
                {
                    if (_harvester != null) _harvester.enabled = false;
                });
                _nextRepairScan = 0f;   // let the first repair pass run immediately
                FlowTrace.Step("Pet", $"repair task active -- pet '{PetId()}' will mend structures via WallRepairController.");
            }
        }

        private string PetId() => _pet != null ? _pet.PetId : "<null>";

        // =====================================================================
        //  Repair task -- drive the EXISTING RepairAll backend (no new repair system)
        // =====================================================================

        private void TickRepair()
        {
            if (Time.time < _nextRepairScan) return;
            _nextRepairScan = Time.time + RepairScanInterval;

            // Don't mend mid-assault (RepairAll's own callers gate on wave phase too).
            if (DeNelle.Core.Combat.BattleLock.IsInBattle()) return;

            var repair = EnsureRepair();
            if (repair == null) return;

            CoreCost cost = repair.RepairAllCost();
            if (WallRepairController.MaterialsZero(cost))
            {
                FlowTrace.Throttle("Pet", "repair-clean-" + PetId(), 5f,
                    $"repair task: nothing damaged -- pet '{PetId()}' idle.");
                return;
            }

            if (!repair.CanAffordMaterials(cost))
            {
                FlowTrace.Throttle("Pet", "repair-short-" + PetId(), 5f,
                    $"repair task: cannot afford {WallRepairController.DescribeMaterials(cost)} -- " +
                    "waiting for materials (go farm).");
                return;
            }

            Guard.Try("Pet", "pet repair pass (RepairAll)", () =>
            {
                var r = repair.RepairAll();
                FlowTrace.Step("Pet",
                    $"repair task pass by '{PetId()}': repaired={r.repairedCount} " +
                    $"spent={WallRepairController.DescribeMaterials(r.spent)} remaining={r.remainingDamaged}.");
            });
        }

        /// <summary>
        /// Resolves the shared repair backend: reuses an existing WallRepairController
        /// (a wave scene / HubRepairAffordance installs one) or creates a LOGIC-ONLY,
        /// disabled controller purely to price + apply RepairAll -- never a second
        /// repair system (mirrors HubRepairAffordance.EnsureRepair).
        /// </summary>
        private WallRepairController EnsureRepair()
        {
            if (_repair != null) return _repair;
            _repair = FindAnyObjectByType<WallRepairController>();
            if (_repair == null)
            {
                var go = new GameObject("WallRepair_PetTaskEngine");
                _repair = go.AddComponent<WallRepairController>();
                _repair.enabled = false;   // logic-only: we call RepairAllCost / RepairAll directly
                FlowTrace.Step("Pet", "pet repair task self-installed a logic-only WallRepairController.");
            }
            return _repair;
        }
    }

    /// <summary>
    /// Self-installing host that attaches a <see cref="PetTaskController"/> to every
    /// deployed <see cref="Pet"/> (pets can spawn after scene load via the Echo Hollow /
    /// tutorial, so it polls on a light interval). Mirrors PetHarvestBootstrap: code-built,
    /// runtime, DDOL -- no scene edit, no Pets-asmdef change.
    /// </summary>
    public sealed class PetTaskInstaller : MonoBehaviour
    {
        private static PetTaskInstaller _instance;
        private float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("PetTaskInstaller");
            _instance = go.AddComponent<PetTaskInstaller>();
            Object.DontDestroyOnLoad(go);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f;

            var pets = FindObjectsByType<Pet>(FindObjectsSortMode.None);
            if (pets == null) return;
            foreach (var pet in pets)
            {
                if (pet == null) continue;
                if (pet.GetComponent<PetTaskController>() == null)
                {
                    pet.gameObject.AddComponent<PetTaskController>();
                    FlowTrace.Step("Pet", $"attached PetTaskController to pet '{pet.PetId}' (task state only -- WO-1031 removed the engage prompt).");
                }
            }
        }
    }
}
