using NUnit.Framework;
using DeNelle.Core;

namespace DeNelle.Tests.EditMode
{
    /// <summary>
    /// §2c permission-gate test for WO-411 root cause A: the canonical home/hub scenes
    /// MUST be recognized as town context. The bug was the HUD only knowing "Village2"
    /// while the home hub is "MainCastle_Hall" — so its town chrome silently hid. Adding a
    /// new hub scene without registering it in HubScenes.Names turns these RED before a
    /// blank-chrome HUD can ship. Pure data assertion — no scene load.
    /// </summary>
    public class HubScenesTest
    {
        [Test]
        public void RecognizesCanonicalHubScenes()
        {
            Assert.IsTrue(HubScenes.IsHub("MainCastle_Hall"),
                "MainCastle_Hall (the home hub) MUST be a hub scene — else the town HUD hides (WO-411).");
            Assert.IsTrue(HubScenes.IsHub("Village2"), "Village2 must be a hub scene.");
            Assert.IsTrue(HubScenes.IsHub("CastleHub_MainKeep"), "CastleHub* variants must count (prefix-contains).");
        }

        [Test]
        public void RejectsNonHubScenes()
        {
            Assert.IsFalse(HubScenes.IsHub("OuterWorld"), "OuterWorld is the streamed field, not a hub.");
            Assert.IsFalse(HubScenes.IsHub("Title"), "Title is not a hub.");
            Assert.IsFalse(HubScenes.IsHub(""), "Empty is not a hub.");
            Assert.IsFalse(HubScenes.IsHub(null), "Null is not a hub.");
        }
    }
}
