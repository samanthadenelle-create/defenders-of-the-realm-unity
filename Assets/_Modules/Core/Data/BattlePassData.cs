// =============================================================================
// BattlePassData — DEF-69 (Linear: Monetization Framework). ScriptableObject
// holding the authored Battle Pass tier tracks for a season.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Data (existing DeNelle.Core asmdef — no new asmdef).
// Pure authoring data: the runtime BattlePassManager (a follow-up deliverable)
// reads these tracks and never mutates the SO.
//
// Reconciliation note vs the broad DEF-69 description: the Linear spec sketched
// a `BattlePassTier[]` keyed off CampaignManager Wisdom Points (DEF-68). This
// branch's work order narrows the deliverable to two flat reward tracks
// (free + premium) indexed by tier, with no Campaign dependency. Premium-track
// rewards are cosmetics + soft currency only — no gameplay power (DEF-69 AC).
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Data
{
    /// <summary>
    /// Authoring data for one season's Battle Pass: a free track and a premium
    /// track, each intended to hold exactly <see cref="tierCount"/> rewards
    /// (one per tier, indexed by tier level).
    ///
    /// NOTE: both <see cref="freeTrack"/> and <see cref="premiumTrack"/> are
    /// INTENDED to be length == <see cref="tierCount"/>. This is a designer
    /// authoring contract — it is intentionally NOT enforced or resized at
    /// runtime, because ScriptableObjects are pure data and must not be mutated.
    /// Consumers should bounds-check before indexing.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/BattlePassData", fileName = "BattlePassData")]
    public class BattlePassData : ScriptableObject
    {
        public int tierCount = 30;

        public BattlePassReward[] freeTrack;     // intended length == tierCount
        public BattlePassReward[] premiumTrack;  // intended length == tierCount
    }
}
