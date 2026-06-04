// =============================================================================
// BattlePassReward — DEF-69 (Linear: Monetization Framework). A minimal, pure
// data descriptor for a single Battle Pass reward entry.
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core.Data (the namespace inside the existing DeNelle.Core
// asmdef — no new asmdef; DeNelle.Village already references Core, so its
// RewardedAdManager / BattlePass managers can see this type).
//
// Reconciliation note vs the broad DEF-69 description: the Linear spec sketched
// a `MissionReward`-based `BattlePassTier` struct that depends on DEF-68's
// CampaignManager. This branch's work order narrows the deliverable to a
// self-contained reward descriptor (no Mission/Campaign dependency), so we
// define a small, standalone reward shape here. Pure data only — never mutated
// at runtime.
// =============================================================================

using System;

namespace DeNelle.Core.Data
{
    /// <summary>
    /// The category of a Battle Pass reward. Intentionally small: premium-track
    /// rewards are cosmetics + soft Wisdom/resources only — no gameplay power is
    /// gated behind the premium pass (DEF-69 acceptance criterion).
    /// </summary>
    public enum BattlePassRewardKind
    {
        Crystals,   // soft premium currency grant
        Cosmetic,   // cosmetic id (tower skin / VFX / outfit) — unlocked, not equipped
        Resource    // gameplay resource grant (Wood / Stone / etc.)
    }

    /// <summary>
    /// One reward on a Battle Pass track. Pure data — authored on the SO and
    /// read at claim time; never mutated at runtime.
    /// </summary>
    [Serializable]
    public struct BattlePassReward
    {
        public string rewardId;          // cosmetic id, resource key, etc.
        public int amount;               // quantity granted (currency / resource count)
        public BattlePassRewardKind kind;
    }
}
