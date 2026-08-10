// =============================================================================
// EchoRepairTaskTests (EditMode) -- WO-811 pure locks for the Echo REPAIR task.
// -----------------------------------------------------------------------------
// GameState-LESS checks only (EditMode has no GameStateService -- the stateful
// round-trip proof lives in EchoSpecializationRegression group 3f and the picker
// suite's group 6, which install a headless state by reflection): the repair
// label, the safely-rejected assign, the chip projection (text-cued, never an
// affinity cue -- Repairs was removed as an affinity, WO-830 2026-08-02), the
// TaskChips shape, and the honest-zero repair rate.
// =============================================================================
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EchoRepairTaskTests
    {
        [Test]
        public void repair_lane_labels_as_full_text()
        {
            Assert.That(EchoAssignments.LabelFor(EchoAssignments.LaneRepair), Is.EqualTo("Repair"));
        }

        [Test]
        public void assign_repair_is_safely_rejected_without_gamestate()
        {
            // No GameStateService in EditMode -> logs + returns false (never throws) --
            // the same contract EchoRosterVMTests locks for the generic Assign.
            Assert.That(EchoAssignments.AssignRepair(0), Is.False);
        }

        [Test]
        public void repair_chip_is_text_cued_and_never_preferred()
        {
            var vm = new EchoCardVM(1);
            try
            {
                var chip = vm.RepairTaskChip();
                Assert.That(chip.Id, Is.EqualTo(EchoAssignments.LaneRepair));
                Assert.That(chip.Label, Does.Contain("Repair structures"), "full label, no clip/icon-only");
                Assert.That(chip.Preferred, Is.False,
                    "Repairs was removed as an affinity (WO-830) -- the repair chip may never claim the match cue");
                Assert.That(chip.Label, Does.Not.Contain("best"));
                Assert.That(chip.Selected, Is.False, "no GameState -> index 1 defaults Idle, not repair");
            }
            finally { vm.Dispose(); }
        }

        [Test]
        public void task_chips_append_repair_last_after_the_five_resources()
        {
            var vm = new EchoCardVM(1);
            try
            {
                var chips = vm.TaskChips();
                Assert.That(chips.Length, Is.EqualTo(EchoAssignments.PickableResources.Length + 1),
                    "five WO-830 resource rows + the WO-811 repair row");
                Assert.That(chips[chips.Length - 1].Id, Is.EqualTo(EchoAssignments.LaneRepair),
                    "the repair task row is appended LAST -- resource order untouched");
                for (int i = 0; i < EchoAssignments.PickableResources.Length; i++)
                    Assert.That(chips[i].Id, Is.EqualTo(EchoAssignments.PickableResources[i]));
            }
            finally { vm.Dispose(); }
        }

        [Test]
        public void repair_rate_is_the_honest_zero_without_gamestate()
        {
            // No GameState/service -> owned count 0 -> exactly 0 work/sec (never fake work).
            Assert.That(EchoBonusCalculator.RepairFractionsPerSecond(), Is.EqualTo(0f));
        }
    }
}
