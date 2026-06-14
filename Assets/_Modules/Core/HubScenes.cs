// =============================================================================
// HubScenes — the ONE canonical list of home/hub scenes.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// WHY: "is this a town/hub scene?" was answered in TWO drifted places — the HUD
// (`VillageHudController.EvaluateInVillage` only knew "Village2") and the world
// streamer (`WorldSceneLoader.HubSceneNames` knew the full set). That drift hid the
// whole town HUD on MainCastle_Hall (WO-411 root cause A). This is the single source
// both read, so adding a hub scene = one edit + the test below stays green.
//
// Lives in Core so BOTH DeNelle.Village (WorldSceneLoader) and DeNelle.HUD
// (VillageHudController) can reference it (Village→Core, HUD→Core; never Village↔HUD).
// =============================================================================
namespace DeNelle.Core
{
    public static class HubScenes
    {
        /// <summary>Canonical home/hub scene names. Add a new hub here (and only here).</summary>
        public static readonly string[] Names = { "Village2", "MainCastle_Hall", "CastleHub", "CastleHub_MainKeep" };

        /// <summary>True if <paramref name="sceneName"/> is a home/hub scene (exact or prefix-contains,
        /// matching WorldSceneLoader's prior behaviour so CastleHub* variants still count).</summary>
        public static bool IsHub(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < Names.Length; i++)
                if (sceneName == Names[i] || sceneName.Contains(Names[i])) return true;
            return false;
        }

        /// <summary>True if <paramref name="sceneName"/> is an enemy RAID scene
        /// (<c>RaidBase_*</c>) — the single source both the HUD (combat-cluster gate,
        /// WO-457) and the Village (RaidDeployController self-install) read so the raid
        /// naming convention lives in ONE place.</summary>
        public static bool IsRaid(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && sceneName.StartsWith("RaidBase", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
