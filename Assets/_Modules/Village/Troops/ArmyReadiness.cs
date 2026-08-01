// =============================================================================
// ArmyReadiness — THE one army-readiness formula (owner review 2026-08-01).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-820 computed "is the army full" in THREE places (BuildTimerService.
// PublishArmyStatus, RaidSelectionScreen.Open, BarracksService.EnqueueTraining)
// and PM review flagged the triple-drift risk. This static class is now the
// SINGLE source of that math — every consumer calls Compute() and reads the
// Snapshot; NEVER re-roll the formula locally. The formula:
//
//   deployable = sum of SlotOf over Army.GetDeployable()   (wounded excluded)
//   roster     = Army.SlotsUsed(SlotOf)                    (ALL roster incl. wounded)
//   queued     = BarracksService.CommittedTrainingSlots()  (in-flight Train jobs)
//   cap        = Army.MaxArmySize
//   Ready      = deployable + queued >= cap
//
// Pure aside from two service reads: BuildTimerService.Instance (via
// CommittedTrainingSlots, 0 when absent) and ModifierService (via MaxArmySize).
// All same assembly (DeNelle.Village) — no cross-assembly pull.
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// The single army-readiness formula (owner review 2026-08-01). Consumers:
    /// BuildTimerService.PublishArmyStatus (HUD Raids-button grey state),
    /// RaidSelectionScreen.Open (authoritative entry gate), and
    /// BarracksService.EnqueueTraining (over-queue cap seed).
    /// </summary>
    public static class ArmyReadiness
    {
        /// <summary>One computed readiness snapshot — see class header for the formula.</summary>
        public struct Snapshot
        {
            /// <summary>Healthy roster slots (wounded excluded — GetDeployable skips them).</summary>
            public int DeployableSlots;
            /// <summary>Slots committed to in-flight Train-channel jobs (active + pending).</summary>
            public int QueuedSlots;
            /// <summary>Army cap (Army.MaxArmySize — base + perk bonus).</summary>
            public int CapSlots;
            /// <summary>ALL roster slots incl. wounded (SlotsUsed) — the enqueue-cap seed.</summary>
            public int RosterSlots;
            /// <summary>True when DeployableSlots + QueuedSlots cover CapSlots.</summary>
            public bool Ready;
        }

        /// <summary>
        /// Compute readiness from <paramref name="st"/>. Null st/Army publishes READY with
        /// zeros so headless/AutoPilot never false-blocks (mirrors the WO-813 / WO-820
        /// stateless bypass — a missing GameState must never dim or gate the raid door).
        /// </summary>
        public static Snapshot Compute(GameState st)
        {
            if (st == null || st.Army == null)
                return new Snapshot { Ready = true };   // headless never-false-block rule

            var army = st.Army;
            int deployable = 0;
            foreach (var t in army.GetDeployable())
                deployable += TroopDialogueCommands.SlotOf(t.TroopDefId);

            return Compute(army, deployable, BarracksService.CommittedTrainingSlots());
        }

        /// <summary>
        /// Seam overload for EditMode tests (BuildTimerService is a runtime MonoBehaviour,
        /// so CommittedTrainingSlots is 0 headless): same formula, injected queued count.
        /// Public because the test assembly is separate; runtime callers use Compute(GameState).
        /// </summary>
        public static Snapshot Compute(ArmyStorage army, int deployableSlots, int queuedSlots)
        {
            int cap = army.MaxArmySize;
            return new Snapshot
            {
                DeployableSlots = deployableSlots,
                QueuedSlots = queuedSlots,
                CapSlots = cap,
                RosterSlots = army.SlotsUsed(TroopDialogueCommands.SlotOf),
                Ready = deployableSlots + queuedSlots >= cap
            };
        }
    }
}
