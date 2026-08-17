// =============================================================================
// RaidClaimService — the persisted "which raid bases has the player CLAIMED?" set.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE GAP THIS CLOSES (core-loop payoff, WO-441 Phase A/C spine): clearing a raid
// garrison (RaidGarrisonSpawner.OnCleared) had NO subscriber and NO record of the
// win — so victory -> CLAIM -> "this base is now mine" was missing and a cleared
// raid soft-locked. This static persists the claimed-raid set so the win READS as
// the player's: the OuterWorld outpost / re-entry can reflect claimed state, and a
// claimed scene flips ownership ENEMY -> PLAYER (the inverse of
// RaidGarrisonSpawner's SceneOwnership.SetEnemyOwned(true)).
//
// PERSISTENCE: PlayerPrefs, exactly mirroring the established ClaimableCamp
// convention (dotr-camp-claimed-<id>) and the WO-441 spec's named key
// (dotr-raid-owner-<id>). This is the SAME additive pattern the camp loop already
// ships — NO SaveSchema migration (the versioned save layer is risk-gated; camp/
// raid ownership has always lived in PlayerPrefs). A later WO can fold the set into
// SaveSchema v24 (OwnedOutposts) for cloud sync; until then this is local-first and
// correct.
//
// configId is the scene-config id the raid base was generated from (the baked scene
// is RaidBase_<configId>; RaidGarrisonSpawner carries the STORED id). ASCII-only.
// Canon: the village is Elarion (never Avalon).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// The persisted set of CLAIMED raid bases (PlayerPrefs <c>dotr-raid-owner-&lt;configId&gt;</c>).
    /// Written on raid victory by <see cref="RaidVictoryController"/> and
    /// <see cref="Village2RaidController"/>; READ by those same victory paths BEFORE they
    /// claim, to decide whether this clear is the FIRST one (full payoff) or a REPEAT
    /// (diminished payoff, see <see cref="ScaleLootForClear"/>).
    ///
    /// <para>WHAT THIS SUMMARY USED TO CLAIM, AND WHY THE LINE IS WRITTEN THIS WAY NOW
    /// (defect sweep 2026-08-15): it said the set was "read by re-entry / world-state so a
    /// cleared-and-claimed base reads as the player's". Nothing read it. <c>IsClaimed</c> had
    /// no caller outside <c>MarkClaimed</c>'s own re-claim guard and <c>ClearClaim</c> had
    /// ZERO callers repo-wide, so the whole service was write-only: every raid payout was
    /// computed as though the base had never been taken, which made an Extreme base
    /// (rewardMultiplier 2.2) an infinitely repeatable full payout. A comment asserting a
    /// read that does not exist is worse than no comment - it is why the hole survived
    /// review. Only claim a reader here that you can point at by name.</para>
    /// </summary>
    public static class RaidClaimService
    {
        // +configId -> "1" once the player has cleared + claimed that raid base.
        private const string PrefOwnerKey = "dotr-raid-owner-";

        // =====================================================================
        //  THE FIRST-CLEAR GATE  (economy-safe half; the curve is an OWNER call)
        // =====================================================================

        /// <summary>
        /// What fraction of the settled loot a REPEAT clear of an already-claimed base pays.
        ///
        /// <para>0 = the strict first-claim gate: the base pays its loot ONCE. This is the
        /// deliberately SAFE default, and it is the value that makes the surrounding
        /// documentation true ("a re-cleared base never double-grants") rather than
        /// aspirational. It cannot inflate the economy, which is the property a stop-gap
        /// needs; whether farming a cleared base SHOULD pay a trickle (and on what decay
        /// curve) is an economy-balance decision reserved for the owner - flagged, not
        /// chosen here. When that ruling lands, this ONE constant is the knob: set it to the
        /// repeat fraction and nothing else in the raid stack has to move.</para>
        ///
        /// <para>Kept as a named constant rather than a magic 0 at the call site precisely so
        /// the retune is a one-line, one-place edit with this rationale attached.</para>
        /// </summary>
        public const float RepeatClearLootMultiplier = 0f;

        /// <summary>
        /// Scales a settled raid payout by the first-clear gate: a FIRST clear pays in full,
        /// a REPEAT clear pays <see cref="RepeatClearLootMultiplier"/> of it (rounded down, so
        /// the gate can never round a repeat back up to a full unit).
        ///
        /// <para>Pure + static: no PlayerPrefs, no scene, no singleton - so a regression can
        /// assert the gate's arithmetic with nothing loaded. The CALLER decides which case it
        /// is, because the claim flag is flipped during victory handling: read
        /// <see cref="IsClaimed"/> BEFORE <c>MarkClaimed</c> or every clear reads as a repeat.</para>
        /// </summary>
        public static ResourceCost ScaleLootForClear(ResourceCost loot, bool isRepeatClear)
        {
            if (!isRepeatClear) return loot;

            float m = RepeatClearLootMultiplier;
            if (m <= 0f) return default(ResourceCost);
            if (m >= 1f) return loot;   // defensive: a mis-set knob must never PAY MORE than the first clear

            return new ResourceCost(
                wood:     Mathf.FloorToInt(loot.Wood     * m),
                food:     Mathf.FloorToInt(loot.Food     * m),
                iron:     Mathf.FloorToInt(loot.Iron     * m),
                crystals: Mathf.FloorToInt(loot.Crystals * m),
                coins:    Mathf.FloorToInt(loot.Coins    * m));
        }

        /// <summary>True once the player has claimed the raid base with this config id.</summary>
        public static bool IsClaimed(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;
            return PlayerPrefs.GetString(PrefOwnerKey + configId, null) == "1";
        }

        /// <summary>
        /// Mark the raid base <paramref name="configId"/> CLAIMED (player-owned), persist,
        /// and report whether this was a NEW claim (true) or a no-op re-claim (false). The
        /// caller uses the "new" signal to decide whether to grant the one-time payoff
        /// (the next-companion unlock) so a re-cleared base never double-grants. Idempotent.
        ///
        /// <para>The RESOURCE payout is gated separately, and deliberately so: it is decided
        /// from an <see cref="IsClaimed"/> read taken BEFORE this call (see
        /// RaidVictoryController.HandleVictory), because by the time this returns the flag has
        /// already flipped. Do not re-derive "was this a repeat" from IsClaimed afterwards.</para>
        /// </summary>
        public static bool MarkClaimed(string configId)
        {
            if (string.IsNullOrEmpty(configId))
            {
                FlowTrace.Warn("Raid", "RaidClaimService.MarkClaimed: empty configId — not claimed.");
                return false;
            }
            if (IsClaimed(configId))
            {
                FlowTrace.Step("Raid", $"RaidClaimService: '{configId}' already claimed — no-op (no re-grant).");
                return false;
            }
            PlayerPrefs.SetString(PrefOwnerKey + configId, "1");
            PlayerPrefs.Save();
            FlowTrace.Step("Raid", $"RaidClaimService: '{configId}' CLAIMED -> player-owned (persisted dotr-raid-owner-{configId}).");
            return true;
        }

        /// <summary>
        /// Test/dev hook: drop the claim on a raid base (so it can be re-raided).
        /// Called by <c>RaidRepeatClearRegression</c>, which round-trips
        /// MarkClaimed -> IsClaimed -> ClearClaim on a scratch id and restores the pref.
        /// (It had ZERO callers before that suite, which is how the write-only claim set
        /// went unnoticed - an unexercised hook proves nothing.)
        /// </summary>
        public static void ClearClaim(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return;
            PlayerPrefs.DeleteKey(PrefOwnerKey + configId);
            PlayerPrefs.Save();
            FlowTrace.Step("Raid", $"RaidClaimService: claim on '{configId}' CLEARED - the base is raidable again.");
        }
    }
}
