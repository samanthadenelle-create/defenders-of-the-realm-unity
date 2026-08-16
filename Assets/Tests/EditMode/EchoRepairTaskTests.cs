// =============================================================================
// EchoRepairTaskTests (EditMode) -- WO-811 locks, REWRITTEN for WO-1108.
// -----------------------------------------------------------------------------
// WO-1108 made repair PASSIVE: it is no longer an assignable task, so the three
// WO-811 cases that pinned the "Repair structures" chip (its label, its projection,
// its position LAST in TaskChips) pinned behaviour that no longer exists. They are
// INVERTED here, never deleted -- each now asserts the RETIREMENT it used to assert
// the presence of, so re-adding the chip fails this fixture.
//
// The WO-811 HONEST-ZERO case survives in a NEW form: with no GameState there is no
// owned Echo, so the passive rate is still exactly 0 (never fake work). It is joined
// by the retired-verb refusal and the read-migration of the legacy "repair" token.
//
// GameState-LESS checks only (EditMode has no GameStateService) -- the stateful
// round-trip + count-scaling proofs live in EchoSpecializationRegression group 3f/4b
// and the picker suite's group 6, which install a headless state by reflection.
// =============================================================================
using System.Reflection;
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class EchoRepairTaskTests
    {
        // WO-811 case 1, INVERTED: "repair" no longer labels as its own task. The token
        // normalizes to the Harvest lane (the WO-1108 read-migration), so the label a
        // stored repair token renders under is "Harvest" -- never "Repair".
        [Test]
        public void retired_repair_token_labels_as_harvest_not_repair()
        {
            Assert.That(EchoAssignments.LabelFor(EchoAssignments.LaneRepair), Is.EqualTo("Harvest"),
                "WO-1108: repair is passive, not a task -- a stored 'repair' token reads as the Harvest lane");
        }

        // WO-811 case 2, KEPT and STRENGTHENED: the assign verb was "safely rejected
        // without GameState"; it is now rejected ALWAYS, because the task is retired.
        [Test]
        public void assign_repair_is_always_refused()
        {
            Assert.That(EchoAssignments.AssignRepair(0), Is.False,
                "WO-1108: the repair task is RETIRED -- the verb must always refuse, never throw");
            Assert.That(EchoAssignments.AssignRepair(3), Is.False);
        }

        // WO-811 case 3, INVERTED: the chip must NOT exist. Reflection, so a resurrected
        // member fails here even though this fixture no longer names it in code.
        [Test]
        public void repair_chip_and_verb_are_gone_from_the_card_vm()
        {
            const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            var t = typeof(EchoCardVM);
            Assert.That(t.GetMethod("RepairTaskChip", Pub), Is.Null,
                "WO-1108: the 'Repair structures' chip is RETIRED -- repair is passive, not a pick");
            Assert.That(t.GetMethod("AssignRepair", Pub), Is.Null,
                "WO-1108: there is no repair task left to assign from the card");
        }

        // WO-811 case 4, INVERTED: TaskChips is EXACTLY the five resources -- no sixth row.
        [Test]
        public void task_chips_are_the_five_resources_with_no_repair_row()
        {
            var vm = new EchoCardVM(1);
            try
            {
                var chips = vm.TaskChips();
                Assert.That(chips.Length, Is.EqualTo(EchoAssignments.PickableResources.Length),
                    "five WO-830 resource rows; the WO-811 repair row is retired");
                for (int i = 0; i < EchoAssignments.PickableResources.Length; i++)
                {
                    Assert.That(chips[i].Id, Is.EqualTo(EchoAssignments.PickableResources[i]));
                    Assert.That(chips[i].Id, Is.Not.EqualTo(EchoAssignments.LaneRepair));
                    Assert.That(chips[i].Label, Does.Not.Contain("Repair"),
                        "no chip may advertise repair as a pick");
                }
            }
            finally { vm.Dispose(); }
        }

        // WO-811 case 5, SURVIVES IN A NEW FORM: the honest zero. It used to mean "no Echo
        // ASSIGNED to repair"; it now means "no Echo OWNED" (no GameState/service -> owned
        // count 0). Either way the rule is the same: zero labor accrues zero work, never fake.
        [Test]
        public void passive_repair_rate_is_the_honest_zero_without_gamestate()
        {
            Assert.That(EchoBonusCalculator.RepairFractionsPerSecond(), Is.EqualTo(0f),
                "no owned Echo -> exactly 0 work/sec (WO-811 honesty rule, WO-1108 wording)");
        }

        // WO-1108: the repair PACE knob must be authored data with a sane positive value.
        // The count-scaling proof itself needs a live GameState and lives in
        // EchoSpecializationRegression group 4b.
        [Test]
        public void repair_pace_knob_is_positive_and_retuned_for_the_whole_roster()
        {
            float perHour = EchoBalanceCatalog.RepairFractionPerHour;
            Assert.That(perHour, Is.GreaterThan(0f), "the repair pace knob must be positive");
            Assert.That(perHour, Is.LessThan(2f),
                "WO-1108 D3: passive repair sums the WHOLE roster, so the per-Echo knob was tuned DOWN "
                + "from the WO-811 single-assigned-Echo value of 2.0 -- leaving it there is a silent 6x");
        }
    }
}
