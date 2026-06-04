// =============================================================================
// CampaignData — DEF-68 (Spire Chronicles Campaign). ScriptableObject holding an
// ordered list of MissionData for one campaign / season.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Data (inside the existing DeNelle.Core asmdef).
//
// RECONCILIATION TO THIS BRANCH (vs the DEF-68 Linear description):
//   • The Linear spec also sketched campaignId / campaignTitle / seasonEndDate.
//     This branch ships the lean `missions` array the WO-48 work order pins down;
//     season metadata is deferred to the campaign-UI follow-up. The mission count
//     stays fully data-driven (8–12 in the design template, but the array length
//     is authoritative — CampaignManager clamps to it).
//
// PURE DATA: never mutated at runtime. CreateAssetMenu under "Defenders/".
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Data
{
    /// <summary>An ordered campaign of missions (e.g. "The Spire Chronicles — Season 1").</summary>
    [CreateAssetMenu(menuName = "Defenders/CampaignData", fileName = "CampaignData")]
    public class CampaignData : ScriptableObject
    {
        [Tooltip("The missions in play order. Mission count is data-driven (the design " +
                 "template is 8–12). CampaignManager advances through this array.")]
        public MissionData[] missions = new MissionData[0];
    }
}
