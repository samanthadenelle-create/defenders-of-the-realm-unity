// =============================================================================
// CampaignProgressRecord — DEF-68 (Spire Chronicles Campaign). Per-playthrough,
// IN-MEMORY-ONLY progress for an active CampaignData. NOT a ScriptableObject.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Data. This is the one mutable piece of the campaign
// model — ScriptableObjects (MissionData / CampaignData) stay pure data, and
// all per-run state (which mission is current, which are completed) lives here.
//
// PERSISTENCE IS OUT OF SCOPE (per DEF-68 Acceptance Criteria): this record
// lives only in memory for now. It is [System.Serializable] so a future
// Save/Load ticket can serialize it without a reshape.
// =============================================================================

namespace DeNelle.Core.Data
{
    /// <summary>Mutable, in-memory progress through a CampaignData (no persistence yet).</summary>
    [System.Serializable]
    public class CampaignProgressRecord
    {
        /// <summary>0-based index of the mission currently in play / next to play.</summary>
        public int currentMissionIndex;

        /// <summary>Parallel to CampaignData.missions: true once that mission is cleared.</summary>
        public bool[] missionCompleted;

        public CampaignProgressRecord() { }

        /// <summary>Sizes <see cref="missionCompleted"/> to <paramref name="missionCount"/> and starts at mission 0.</summary>
        public CampaignProgressRecord(int missionCount)
        {
            currentMissionIndex = 0;
            missionCompleted = new bool[missionCount < 0 ? 0 : missionCount];
        }
    }
}
