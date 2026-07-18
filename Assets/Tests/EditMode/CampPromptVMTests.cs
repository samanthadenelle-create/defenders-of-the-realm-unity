// =============================================================================
// CampPromptVMTests (EditMode) — §2c lock for the camp claim-prompt VM.
// -----------------------------------------------------------------------------
// Over a fake ICampProximity / ICampTarget (no scene): asserts the prompt-shown
// projection, the Claim -> open-menu -> Build -> close state machine, the command
// mutations (Claim() / BuildOutpost(type) called on the right camp), Changed on
// each transition, and that an open menu owns input (Tick does not re-target).
// =============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Village.World.Camps;

namespace DeNelle.Tests.EditMode
{
    internal sealed class FakeCampTarget : ICampTarget
    {
        public bool Cleared { get; set; } = true;
        public bool Claimed { get; set; }
        public Vector3 WorldAnchor { get; set; }
        public object Key { get; set; }
        public int ClaimCalls;
        public readonly List<OutpostType> Built = new List<OutpostType>();
        public void Claim() { ClaimCalls++; Claimed = true; }
        public void BuildOutpost(OutpostType type) { Built.Add(type); }
    }

    internal sealed class FakeCampProximity : ICampProximity
    {
        public ICampTarget Nearest;
        public int EnsureRefsCalls;
        public void EnsureRefs() => EnsureRefsCalls++;
        public ICampTarget FindClaimable() => Nearest;
        public bool TryProject(Vector3 world, out Vector2 screen) { screen = new Vector2(world.x, world.y); return true; }
    }

    [TestFixture]
    public class CampPromptVMTests
    {
        [Test]
        public void prompt_hidden_when_no_camp_near()
        {
            var p = new FakeCampProximity { Nearest = null };
            var vm = new CampPromptVM(p);
            vm.Tick();
            Assert.That(vm.ShowPrompt, Is.False);
            Assert.That(p.EnsureRefsCalls, Is.EqualTo(1), "Tick refreshes refs through the service");
        }

        [Test]
        public void prompt_shown_and_anchored_when_camp_near()
        {
            var camp = new FakeCampTarget { Key = "a", WorldAnchor = new Vector3(3f, 4f, 5f) };
            var vm = new CampPromptVM(new FakeCampProximity { Nearest = camp });
            vm.Tick();
            Assert.That(vm.ShowPrompt, Is.True);
            Assert.That(vm.PromptText, Is.EqualTo("[ Tap ]  Claim Camp"));
            Assert.That(vm.PromptWorldAnchor, Is.EqualTo(new Vector3(3f, 4f, 5f)));
        }

        [Test]
        public void retarget_only_raises_changed_on_a_different_camp()
        {
            var camp = new FakeCampTarget { Key = "a" };
            var vm = new CampPromptVM(new FakeCampProximity { Nearest = camp });
            int fires = 0; vm.Changed += () => fires++;

            vm.Tick();               // null -> camp : a change
            vm.Tick();               // camp -> same camp : no change
            Assert.That(fires, Is.EqualTo(1), "same target does not re-raise");
        }

        [Test]
        public void claim_calls_camp_opens_menu_and_hides_prompt()
        {
            var camp = new FakeCampTarget { Key = "a" };
            var p = new FakeCampProximity { Nearest = camp };
            var vm = new CampPromptVM(p);
            vm.Tick();
            int fires = 0; vm.Changed += () => fires++;

            vm.ClaimCurrent();

            Assert.That(camp.ClaimCalls, Is.EqualTo(1), "the prompted camp is claimed");
            Assert.That(vm.MenuOpen, Is.True, "claim opens the build menu");
            Assert.That(vm.ShowPrompt, Is.False, "prompt hides while the menu is open");
            Assert.That(fires, Is.EqualTo(1), "claim raises Changed");
        }

        [Test]
        public void open_menu_owns_input_tick_does_not_retarget()
        {
            var camp = new FakeCampTarget { Key = "a" };
            var p = new FakeCampProximity { Nearest = camp };
            var vm = new CampPromptVM(p);
            vm.Tick();
            vm.ClaimCurrent();
            p.EnsureRefsCalls = 0;

            // A second camp wanders into range — but the menu owns input.
            p.Nearest = new FakeCampTarget { Key = "b" };
            vm.Tick();

            Assert.That(vm.MenuOpen, Is.True);
            Assert.That(vm.ShowPrompt, Is.False);
            Assert.That(p.EnsureRefsCalls, Is.EqualTo(0), "Tick short-circuits while the menu is open");
        }

        [Test]
        public void build_builds_on_the_claimed_camp_and_closes_menu()
        {
            var camp = new FakeCampTarget { Key = "a" };
            var vm = new CampPromptVM(new FakeCampProximity { Nearest = camp });
            vm.Tick();
            vm.ClaimCurrent();
            int fires = 0; vm.Changed += () => fires++;

            vm.Build(OutpostType.LumberOutpost);

            Assert.That(camp.Built, Is.EqualTo(new[] { OutpostType.LumberOutpost }));
            Assert.That(vm.MenuOpen, Is.False, "building closes the menu");
            Assert.That(fires, Is.EqualTo(1), "build raises Changed (close transition)");
        }

        [Test]
        public void close_menu_dismisses_without_building()
        {
            var camp = new FakeCampTarget { Key = "a" };
            var vm = new CampPromptVM(new FakeCampProximity { Nearest = camp });
            vm.Tick();
            vm.ClaimCurrent();

            vm.CloseMenu();

            Assert.That(vm.MenuOpen, Is.False);
            Assert.That(camp.Built, Is.Empty, "close builds nothing");
        }

        [Test]
        public void claim_with_no_target_is_a_noop()
        {
            var vm = new CampPromptVM(new FakeCampProximity { Nearest = null });
            vm.Tick();
            vm.ClaimCurrent();   // nothing prompted
            Assert.That(vm.MenuOpen, Is.False);
        }
    }
}
