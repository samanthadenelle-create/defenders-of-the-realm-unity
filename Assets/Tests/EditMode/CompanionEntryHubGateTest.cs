using NUnit.Framework;
using DeNelle.Core;

namespace DeNelle.Tests.EditMode
{
    /// <summary>
    /// Regression guard for the COMPANION-NEVER-JOINS bug: every companion entry path
    /// (StoryCompanionInjector spawn, CompanionMeetingTrigger FTUE host, SylasFirstMeeting,
    /// ElaraWaveThreeJoin, GromOuterWorldReturnJoin) was hard-gated to the abandoned
    /// "Village2" scene, so the live home hub (MainCastle_Hall) never triggered a companion.
    /// The fix re-points all of those gates through <see cref="HubScenes.IsHub"/>, so the
    /// hub list IS the companion gate now.
    ///
    /// This asserts the two scene names the gate MUST accept. If anyone shrinks
    /// HubScenes.Names and drops the castle hub (or Village2), these go RED before the
    /// companion silently stops joining again. Pure data assertion — no scene load.
    /// </summary>
    public class CompanionEntryHubGateTest
    {
        [Test]
        public void CompanionGate_RecognizesTheCastleHub()
        {
            Assert.IsTrue(HubScenes.IsHub("MainCastle_Hall"),
                "MainCastle_Hall (the live home hub) MUST be a hub scene — else NO companion " +
                "ever joins (the companion-never-joins gate; every entry path routes through HubScenes.IsHub).");
        }

        [Test]
        public void CompanionGate_StillRecognizesVillage2()
        {
            Assert.IsTrue(HubScenes.IsHub("Village2"),
                "Village2 must remain a hub scene so widening the companion gate to the castle " +
                "does not regress the original village companion flow.");
        }

        [Test]
        public void CompanionGate_DoesNotFireOutsideAHub()
        {
            // The streamed field and the boot/title scenes must NOT count as a hub, or the
            // companion beats would try to bootstrap in the wrong place.
            Assert.IsFalse(HubScenes.IsHub("OuterWorld"), "OuterWorld is the streamed field, not a hub.");
            Assert.IsFalse(HubScenes.IsHub("Title"), "Title is not a hub.");
        }
    }
}
