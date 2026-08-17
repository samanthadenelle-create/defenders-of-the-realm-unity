// =============================================================================
// EchoRepairService -- the WO-811 REPAIR task consumer (Echoes mend structures).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-1108: repair is PASSIVE. It is no longer an assignable task -- the roster COUNT
// drives it (EchoBonusCalculator.RepairFractionsPerSecond sums EVERY owned Echo), so
// this service needed no change here beyond honest wording: its ONE rate input already
// came from that method.
//
// WHAT IT IS: while one or more Echoes are OWNED, this service advances REAL repair on
// damaged structures through the EXISTING repair authority -- WallRepairController
// (TryPeekWorstDamaged / TryRepairWorst). It NEVER invents a parallel HP or wallet system.
// WO-1108: this is now the SOLE repair loop. PetTaskController's rival RepairAll loop --
// a second, uncoordinated repairer racing this one over the same walls and the same
// construction wallet on its own cadence -- was retired with it.
//
// THE WORK MODEL (mirrors how harvest is modeled -- abstract + rate-based, no
// locomotion; Echoes are portrait-card spirits and NEVER fight or path as units):
//   - The ROSTER accrues a WORK BUDGET in structure-damage FRACTIONS at
//     EchoBonusCalculator.RepairFractionsPerSecond() -- the ONE math source
//     (base knob EchoBalanceCatalog.RepairFractionPerHour, now authored in
//     echoes-balance.json and re-tuned 2.0 -> 0.35 by WO-1108 D3 because the sum
//     spans the whole roster instead of one assigned Echo; level-scaled by the
//     shared LaneContribution terms; NO affinity bonus -- Repairs was removed as
//     an affinity, WO-830 ruling 2026-08-02).
//   - Work accrues ONLY while a damaged, non-destroyed structure exists. ZERO
//     targets = ZERO progress (the WO-811 honesty rule -- no fake work banked
//     against a pristine town).
//   - When the budget covers the WORST target's damage fraction AND the wallet
//     covers its cost, the repair COMPLETES through TryRepairWorst: the same
//     catalog-row pricing (damage fraction x build cost, wood/iron/food, talent
//     discount included, crystals never) and the same EconomyService.TrySpend
//     path every hand-repair takes (F8-42 repair-costs canon: repair SPENDS).
//     Broke = a LOUD throttled refusal + the "waiting for materials" status --
//     never free hitpoints.
//   - PRIORITY: MOST-DAMAGED-FIRST (the WallRepairController worst-first sort;
//     matches WO-701's triage instinct -- documented choice, WO-811 Sec.4).
//   - DESTROYED structures are excluded automatically: TryPeekWorstDamaged /
//     TryRepairWorst reuse CollectRepairAllSet, which already skips
//     IsBroken / DamageFraction >= DestroyedFraction (WO-753: destroyed = LOST,
//     rebuild fresh at full cost -- never auto-repaired back).
//
// OFFLINE (single-clock canon, WO-667; RE-WIRED by WO-1147): this service is a
// CONSUMER of OfflineClaimCoordinator, which performs the ONE read of
// GameState.LastHarvestClaimMs, computes ONE elapsed window, hands the SAME window
// to every consumer, and advances the clock exactly once.
//
// WARNING -- THE BUG THAT LIVED HERE: this service used to read the clock itself from a
// Start + TWO-frame coroutine, while OfflineHarvestService WROTE that clock from a
// Start + ONE-frame coroutine. Our read therefore ALWAYS landed after the clock had
// been zeroed to "now", so (now - lastClaim) was always ~0 and OFFLINE REPAIR NEVER
// ACCRUED A SINGLE FRACTION for its entire life -- silently, because a zero window
// is indistinguishable from "the player was not away". Never re-add a local clock
// read here, and never "fix" ordering with an execution-order attribute or an extra
// frame of delay: that is the duplicate-authority pattern, not a fix.
// Our OfflineCapHours cap stays OURS (the coordinator publishes the raw window and
// each consumer clamps it), as does the MaxBankedFractions ceiling. The banked
// budget itself is NOT persisted -- the offline catch-up regenerates it from the
// clock, so a quit mid-accrual loses at most the sub-cap remainder (same
// coarse-persistence stance as the online silo tick).
//
// BATTLE GATE: no mending mid-assault (BattleLock.IsInBattle -- the same gate
// every RepairAll caller holds); battle time accrues no work.
//
// Installed by EchoWorkforceBootstrap on the persistent EchoService host (DDOL,
// no scene authoring). WO-1108: the Echo CARD no longer renders a per-Echo repair
// status line (repair is not an assignment), so Status / HasRepairTargets are now a
// diagnostic + trace surface ("nothing to repair" / "waiting for materials").
// =============================================================================
using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village
{
    /// <summary>The repair task's honest, player-explainable state (TEXT on the card,
    /// never hue -- colorblind law).</summary>
    public enum EchoRepairStatus
    {
        /// <summary>WO-1108: no Echo is OWNED (or no GameState yet) -- nobody can mend, so the
        /// rate is the honest zero. This replaced <c>NoneAssigned</c>: repair stopped being an
        /// assignment (every owned Echo mends passively), so "none assigned" became a state the
        /// code can no longer produce and was deleted rather than left as a lie.</summary>
        NoEchoes = 0,
        /// <summary>Echo(es) owned, but nothing is damaged -- zero progress, honestly.</summary>
        NothingToRepair = 1,
        /// <summary>Echo(es) owned and accruing work toward the worst damaged structure.</summary>
        Working = 2,
        /// <summary>Work is ready but the wallet cannot cover the repair cost (repair SPENDS).</summary>
        WaitingMaterials = 3,
    }

    /// <summary>
    /// Drives the WO-811 Echo REPAIR task: accrues rate-based repair work while
    /// Echoes are assigned to repair, and completes real repairs (most-damaged-first,
    /// paid at the live cost canon) through the existing <see cref="WallRepairController"/>
    /// backend. Offline-fair via the shared harvest clock. See the file header.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoRepairService : MonoBehaviour, IOfflineClaimConsumer
    {
        public static EchoRepairService Instance { get; private set; }

        /// <summary>Trace name for this consumer's share of the shared offline window.</summary>
        public string OfflineConsumerName => "echo-repair";

        // Cadence for driving the repair backend. (Was described as mirroring
        // PetTaskController.RepairScanInterval; that rival loop is gone as of WO-1108.)
        private const float ScanInterval = 1.5f;
        // Numeric slack for the budget-vs-fraction comparison.
        private const float BudgetEps = 0.0001f;
        // Hard bound on offline catch-up passes (defensive; the budget cap bounds it anyway).
        private const int MaxOfflinePasses = 24;

        [Header("Repair work (WO-811)")]
        [Tooltip("Cap on banked repair work, in structure-damage FRACTIONS (a destroyed-threshold " +
                 "structure is ~1.0). Bounds both online banking and the offline catch-up grant.")]
        [Min(0f)] public float MaxBankedFractions = 2f;

        [Tooltip("Offline catch-up window cap in HOURS (mirrors the Echo silo's 4h default -- " +
                 "the same fairness stance as harvest catch-up on the same clock).")]
        [Min(0f)] public float OfflineCapHours = 4f;

        /// <summary>The task's current honest state (the card status line reads this).</summary>
        public EchoRepairStatus Status { get; private set; } = EchoRepairStatus.NoEchoes;

        /// <summary>True when the last scan found at least one damaged, non-destroyed structure.</summary>
        public bool HasRepairTargets { get; private set; }

        /// <summary>Banked repair work in structure-damage fractions (diagnostic readout).</summary>
        public float BankedWork => _workBudget;

        /// <summary>Raised on status transitions and completed repairs (the card re-binds).</summary>
        public event Action Changed;

        private float _workBudget;
        private float _nextScan;
        private float _lastWorkTime = -1f;
        private WallRepairController _repair;

        /// <summary>Fractions banked by the most recent offline window (diagnostic /
        /// regression readout -- proves the share was non-zero).</summary>
        public float LastOfflineGain { get; private set; }

        /// <summary>Seconds of the last shared window this consumer actually counted
        /// (post-cap). Regression reads it to prove all three consumers saw one delta.</summary>
        public double LastOfflineCountedSeconds { get; private set; }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            // Destroy(this) -- NOT Destroy(gameObject): shares the EchoService host
            // (CLAUDE.md memory: singleton-dedup-destroys-host).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            OfflineClaimCoordinator.Register(this);   // one authority fans the offline window to us
        }

        private void OnDestroy()
        {
            OfflineClaimCoordinator.Unregister(this);
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // WO-1147: no local offline coroutine. The two-frame deferral this used to own
            // (structures must be present for the WallRepairController sweep) now lives on
            // the ONE claim in OfflineHarvestService.ClaimDeferred, so every consumer is
            // served by the same, correctly-deferred claim instead of racing it.
            OfflineClaimCoordinator.Register(this);
        }

        // =====================================================================
        //  Offline catch-up -- the SAME clock seam as harvest (read-only)
        // =====================================================================

        /// <summary>
        /// THIS consumer's share of the ONE shared offline window (WO-1147): integrate the
        /// repair rate over the window capped at <see cref="OfflineCapHours"/>, bank it
        /// (further capped at <see cref="MaxBankedFractions"/>), then complete as many
        /// worst-first repairs as the budget AND the wallet cover. The window arrives from
        /// <see cref="OfflineClaimCoordinator"/>, which did the single clock read and owns
        /// advancing it -- this method never reads or writes the clock. [Flow:Echo].
        /// </summary>
        public void ApplyOfflineWindow(OfflineClaimWindow window)
        {
            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (s == null) return;

            using var _t = FlowTrace.Enter("Echo", "RepairApplyOfflineWindow");

            LastOfflineGain = 0f;
            LastOfflineCountedSeconds = 0.0;

            float rate = EchoBonusCalculator.RepairFractionsPerSecond();
            if (rate <= 0f)
            {
                FlowTrace.Step("Echo",
                    $"claim #{window.Sequence}: 'echo-repair' share = no owned Echo -- nothing accrues " +
                    $"(window was {window.ElapsedSeconds:F0}s).");
                return;
            }

            double elapsedSec = window.ElapsedSeconds;
            double cappedSec = window.CappedSeconds(OfflineCapHours);
            float gained = (float)(rate * cappedSec);
            _workBudget = Mathf.Min(MaxBankedFractions, _workBudget + gained);
            LastOfflineGain = gained;
            LastOfflineCountedSeconds = cappedSec;
            FlowTrace.Step("Echo",
                $"claim #{window.Sequence}: 'echo-repair' share = {cappedSec:F0}s of the {elapsedSec:F0}s window" +
                (window.ExceedsCap(OfflineCapHours) ? $" (capped at {OfflineCapHours:0.##}h)" : "") +
                $" at {rate * 3600f:0.###} fractions/h -> gained {gained:0.###}, banked {_workBudget:0.###}/{MaxBankedFractions:0.###}.");

            int done = ApplyBankedWork("echo offline repair");
            FlowTrace.Step("Echo",
                $"claim #{window.Sequence}: 'echo-repair' applied {done} repair(s); {_workBudget:0.###} work banked for the online loop.");
            Changed?.Invoke();
        }

        /// <summary>Complete worst-first repairs while the banked budget AND the wallet
        /// cover the worst target. Honest stops: no targets, budget short, or broke
        /// (each traced). Returns the number of completed repairs.</summary>
        private int ApplyBankedWork(string reason)
        {
            var repair = EnsureRepair();
            if (repair == null) return 0;

            int done = 0;
            for (int i = 0; i < MaxOfflinePasses; i++)
            {
                if (!repair.TryPeekWorstDamaged(out string name, out float frac, out CoreCost cost))
                {
                    HasRepairTargets = false;
                    if (done == 0)
                        FlowTrace.Step("Echo", $"{reason}: nothing to repair -- banked work held.");
                    break;
                }
                HasRepairTargets = true;

                if (_workBudget + BudgetEps < frac)
                {
                    FlowTrace.Step("Echo",
                        $"{reason}: banked {_workBudget:0.###} < worst '{name}' dmg {frac:0.00} -- keep accruing online.");
                    break;
                }

                if (!repair.CanAffordMaterials(cost))
                {
                    FlowTrace.Warn("Echo",
                        $"{reason}: cannot afford {WallRepairController.DescribeMaterials(cost)} for '{name}' " +
                        "-- waiting for materials (repair SPENDS; never free hitpoints).");
                    break;
                }

                if (!repair.TryRepairWorst(reason, out string repairedName, out float repairedFrac, out CoreCost spent))
                    break;   // backend refused (raced/spend failed) -- already traced by the backend

                _workBudget = Mathf.Max(0f, _workBudget - repairedFrac);
                done++;
                FlowTrace.Step("Echo",
                    $"{reason}: repaired '{repairedName}' (dmg {repairedFrac:0.00}) for " +
                    $"{WallRepairController.DescribeMaterials(spent)}; {_workBudget:0.###} work remains.");
            }
            return done;
        }

        // =====================================================================
        //  Online loop -- accrue + complete on the scan cadence
        // =====================================================================

        private void Update()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + ScanInterval;
            Tick();
        }

        private void Tick()
        {
            // Consume the wall-clock delta up front so paused/gated stretches never
            // retro-accrue when the gate lifts.
            float now = Time.time;
            float dt = _lastWorkTime >= 0f ? Mathf.Max(0f, now - _lastWorkTime) : 0f;
            _lastWorkTime = now;

            var s = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (s == null) { SetStatus(EchoRepairStatus.NoEchoes, HasRepairTargets); return; }

            float rate = EchoBonusCalculator.RepairFractionsPerSecond();
            if (rate <= 0f)
            {
                // WO-1108: no OWNED Echo -> no banked work either (honest: the budget is the
                // roster's labor, not a free-floating pool). With repair passive this is
                // reachable only at zero Echoes (or before GameState lands).
                if (_workBudget > 0f)
                    FlowTrace.Step("Echo", $"Echo repair: no owned Echo -- dropping {_workBudget:0.###} banked work.");
                _workBudget = 0f;
                SetStatus(EchoRepairStatus.NoEchoes, HasRepairTargets);
                return;
            }

            // Never mend mid-assault (the same gate every RepairAll caller holds).
            if (DeNelle.Core.Combat.BattleLock.IsInBattle()) return;

            var repair = EnsureRepair();
            if (repair == null) return;

            if (!repair.TryPeekWorstDamaged(out string worstName, out float worstFrac, out CoreCost worstCost))
            {
                // ZERO targets = ZERO progress: no accrual, no fake work (WO-811 honesty rule).
                SetStatus(EchoRepairStatus.NothingToRepair, false);
                FlowTrace.Throttle("Echo", "repair-clean", 15f,
                    "Echo repair: nothing to repair -- no work accrues (honest empty state).");
                return;
            }

            _workBudget = Mathf.Min(MaxBankedFractions, _workBudget + rate * dt);
            FlowTrace.Throttle("Echo", "repair-tick", 5f,
                $"Echo repair tick: rate {rate * 3600f:0.###}/h, banked {_workBudget:0.###}, " +
                $"worst '{worstName}' (dmg {worstFrac:0.00}).");

            if (_workBudget + BudgetEps < worstFrac)
            {
                SetStatus(EchoRepairStatus.Working, true);
                return;
            }

            if (!repair.CanAffordMaterials(worstCost))
            {
                SetStatus(EchoRepairStatus.WaitingMaterials, true);
                FlowTrace.Throttle("Echo", "repair-broke", 15f,
                    $"Echo repair: cannot afford {WallRepairController.DescribeMaterials(worstCost)} " +
                    $"for '{worstName}' -- waiting for materials (repair SPENDS; never free hitpoints).");
                return;
            }

            if (repair.TryRepairWorst("echo repair", out string name, out float frac, out CoreCost spent))
            {
                _workBudget = Mathf.Max(0f, _workBudget - frac);
                SetStatus(EchoRepairStatus.Working, true);
                FlowTrace.Step("Echo",
                    $"Echo repair: repaired '{name}' (dmg {frac:0.00}) for " +
                    $"{WallRepairController.DescribeMaterials(spent)}; {_workBudget:0.###} work banked.");
                Changed?.Invoke();
            }
        }

        private void SetStatus(EchoRepairStatus status, bool hasTargets)
        {
            if (Status == status && HasRepairTargets == hasTargets) return;
            Status = status;
            HasRepairTargets = hasTargets;
            FlowTrace.Step("Echo", $"Echo repair status -> {status} (targets={hasTargets}).");
            Changed?.Invoke();
        }

        // =====================================================================
        //  Backend resolution -- reuse the ONE repair authority (pet-task pattern)
        // =====================================================================

        /// <summary>
        /// Resolves the shared repair backend: reuses an existing
        /// <see cref="WallRepairController"/> (a wave scene / HubRepairAffordance installs
        /// one) or creates a LOGIC-ONLY, disabled controller purely to price + apply
        /// repairs -- never a second repair system (mirrors HubRepairAffordance.EnsureRepair
        /// verbatim; the PetTaskController.EnsureRepair it also copied is gone, WO-1108).
        /// </summary>
        private WallRepairController EnsureRepair()
        {
            if (_repair != null) return _repair;
            _repair = FindAnyObjectByType<WallRepairController>();
            if (_repair == null)
            {
                var go = new GameObject("WallRepair_EchoRepairEngine");
                _repair = go.AddComponent<WallRepairController>();
                _repair.enabled = false;   // logic-only: we call TryPeekWorstDamaged / TryRepairWorst directly
                FlowTrace.Step("Echo", "Echo repair task self-installed a logic-only WallRepairController.");
            }
            return _repair;
        }
    }
}
