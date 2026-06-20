// =============================================================================
//  HudBuildingFocus — cross-assembly proximity signal (owner 2026-06-20)
// -----------------------------------------------------------------------------
//  The HUD's bottom context button (VillageHudController) swaps between the Quest
//  action and the Upgrade action depending on whether the hero is standing next to
//  an upgradable, not-yet-maxed building. The HUD assembly may only reference Core,
//  never Village — so BuildingInteractable (Village) WRITES this focus while the
//  hero is in range, and VillageHudController (HUD) READS it each town tick. One
//  shared null-or-id signal, no Village<->HUD coupling.
//
//  There is NO global "nearest building" registry — each BuildingInteractable polls
//  its own range. Set() is last-writer-wins (fine when two upgrade targets overlap),
//  and Clear() only releases focus if the caller still holds it, so a building
//  leaving range can't clobber another that just claimed it.
// =============================================================================
namespace DeNelle.Core.UI
{
    /// <summary>
    /// Which upgradable, not-maxed building the hero is currently next to
    /// (<c>null</c> when none). Set/cleared by the Village proximity poll; read by
    /// the HUD to toggle its context action button.
    /// </summary>
    public static class HudBuildingFocus
    {
        /// <summary>The in-range upgradable building's hook id, or null.</summary>
        public static string CurrentBuildingId { get; private set; }

        /// <summary>Claim focus for <paramref name="buildingId"/> (last writer wins).</summary>
        public static void Set(string buildingId)
        {
            CurrentBuildingId = string.IsNullOrEmpty(buildingId) ? null : buildingId;
        }

        /// <summary>
        /// Release focus, but ONLY if <paramref name="buildingId"/> is the one that
        /// currently holds it — so a non-focused building (or a null/empty caller)
        /// leaving range never clobbers the building the hero is actually next to.
        /// </summary>
        public static void Clear(string buildingId)
        {
            if (CurrentBuildingId == buildingId) CurrentBuildingId = null;
        }
    }
}
