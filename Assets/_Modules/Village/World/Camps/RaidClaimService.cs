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
using DeNelle.Core;
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

        // +configId -> the UTC day key ("yyyy-MM-dd") on which this camp last PAID CRYSTALS.
        // WO-1134. A SEPARATE key from PrefOwnerKey on purpose, and the separation is the
        // whole safety property: PrefOwnerKey is a ONE-TIME, never-expiring flag that also
        // gates the next-companion unlock (RaidVictoryController :~480,
        // OutpostVictoryController :~193). Day-scoping THAT key would re-grant a companion
        // every single day, forever. So the daily axis gets its own key and touches nothing
        // the one-time axis owns.
        private const string PrefCrystalDayKey = "dotr-raid-crystalday-";

        // =====================================================================
        //  THE FIRST-CLEAR GATE  (economy-safe half; the curve is an OWNER call)
        // =====================================================================

        /// <summary>
        /// What fraction of the settled loot a REPEAT clear of an already-claimed base pays.
        ///
        /// <para>Repeat clears pay a small fraction of ordinary resources so replay remains
        /// useful. Crystals are on a SEPARATE axis (the once-per-UTC-day stamp, see
        /// <see cref="CrystalsPaidToday"/>) and are not governed by this multiplier.</para>
        ///
        /// <para>Kept as a named constant rather than a magic 0 at the call site precisely so
        /// the retune is a one-line, one-place edit with this rationale attached.</para>
        /// </summary>
        public const float RepeatClearLootMultiplier = 0.25f;

        /// <summary>
        /// Scales a settled raid payout by the raid loot gates. TWO INDEPENDENT AXES, and they
        /// are deliberately separate parameters rather than one overloaded flag:
        ///
        /// <list type="bullet">
        /// <item><description><paramref name="isRepeatClear"/> — the ORDINARY-RESOURCE axis.
        /// A first clear pays wood/food/iron/coins in full; a re-clear of an already-claimed
        /// base pays <see cref="RepeatClearLootMultiplier"/> of them (rounded DOWN, so the gate
        /// can never round a repeat back up to a full unit).</description></item>
        /// <item><description><paramref name="crystalsAlreadyPaidToday"/> — the CRYSTAL axis
        /// (WO-1134, owner ruling). Crystals are paid on the FIRST clear of each UTC DAY and
        /// zero for every further clear that day. They reset the next day even on a base that
        /// has been claimed for months.</description></item>
        /// </list>
        ///
        /// <para>⛔ DO NOT COLLAPSE THESE INTO ONE FLAG. They answer different questions and
        /// they cross: the second clear of day one is <c>repeat + paid</c> (reduced resources,
        /// no crystals), while the first clear of day two is <c>repeat + NOT paid</c> — reduced
        /// resources but FULL crystals. One boolean cannot express that, and the previous
        /// signature (which hardcoded <c>crystals: 0</c> on any repeat) got the day-two case
        /// silently wrong.</para>
        ///
        /// <para>Pure + static: no PlayerPrefs, no scene, no singleton - so a regression can
        /// assert the gate's arithmetic with nothing loaded. The CALLER decides which case it
        /// is, because both persisted flags are flipped during victory handling: read
        /// <see cref="IsClaimed"/> and <see cref="CrystalsPaidToday"/> BEFORE <c>MarkClaimed</c>
        /// / <c>MarkCrystalsPaid</c> or every clear reads as a repeat that has already paid.</para>
        /// </summary>
        public static ResourceCost ScaleLootForClear(ResourceCost loot, bool isRepeatClear, bool crystalsAlreadyPaidToday)
        {
            // The crystal axis is resolved first and independently of the resource axis.
            int crystals = crystalsAlreadyPaidToday ? 0 : loot.Crystals;

            if (!isRepeatClear)
            {
                if (crystals == loot.Crystals) return loot;
                return new ResourceCost(
                    wood: loot.Wood, food: loot.Food, iron: loot.Iron,
                    crystals: crystals, coins: loot.Coins);
            }

            float m = RepeatClearLootMultiplier;
            if (m >= 1f) m = 1f;        // defensive: a mis-set knob must never PAY MORE than the first clear
            if (m < 0f)  m = 0f;

            // Re-clears remain useful for army practice and food recovery. Crystals are NOT
            // scaled by m — they are all-or-nothing on the day stamp, because a fractional
            // premium payout is exactly the kind of number that quietly becomes a faucet.
            return new ResourceCost(
                wood:     Mathf.FloorToInt(loot.Wood     * m),
                food:     Mathf.FloorToInt(loot.Food     * m),
                iron:     Mathf.FloorToInt(loot.Iron     * m),
                crystals: crystals,
                coins:    Mathf.FloorToInt(loot.Coins    * m));
        }

        // =====================================================================
        //  THE CRYSTAL DAY-STAMP  (WO-1134, owner ruling)
        // =====================================================================

        /// <summary>
        /// True if this camp has ALREADY paid its crystals during the current UTC day, so this
        /// clear must pay zero crystals. False on the first clear of a new day — including on a
        /// base claimed long ago, which is the whole point of the ruling.
        ///
        /// <para>WHY A DAY STAMP AND NOT THE CLAIM FLAG: crystals were the one unbounded faucet
        /// in the game, and the cooldown alone bounded them at ~2 clears/day. Under this stamp
        /// the SECOND clear of a day pays none, so the DAY — not the cooldown — is now the
        /// crystal bound.</para>
        ///
        /// <para>⚠ CORRECTED 2026-09-04 (WO-1374): this used to say
        /// "RaidScoring.ComputeLoot pays FOOD and CRYSTALS only". It no longer does — that
        /// method now also pays WOOD and IRON off the north-star map's performance ladder.
        /// The day-stamp reasoning above is UNCHANGED and still correct, because the stamp
        /// governs the crystal axis alone; only the parenthetical fact had gone stale. The
        /// wood/iron axis is bounded by the repeat-clear multiplier below, not by this
        /// stamp. GOLD is still not paid at all, by WO-1374's fence.</para>
        ///
        /// <para>Read this BEFORE <see cref="MarkCrystalsPaid"/>, exactly as
        /// <see cref="IsClaimed"/> is read before <c>MarkClaimed</c>: the stamp is written
        /// during victory handling, so a read taken afterwards always says "already paid".</para>
        ///
        /// <para>PlayerPrefs, not the save file — same local-first convention as the claim set
        /// above, so this needs NO SaveSchema bump (a schema bump is an owner decision).</para>
        /// </summary>
        public static bool CrystalsPaidToday(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;
            string stamped = PlayerPrefs.GetString(PrefCrystalDayKey + configId, string.Empty);
            return !string.IsNullOrEmpty(stamped) && stamped == UtcDay.Key();
        }

        /// <summary>
        /// Stamp this camp as having PAID CRYSTALS today (UTC). Idempotent within a day, and
        /// self-expiring: tomorrow's <see cref="CrystalsPaidToday"/> compares against a
        /// different key and reports false, so nothing ever has to clean this up.
        ///
        /// <para>Call this AFTER the loot has been granted — a stamp written before a grant
        /// that then throws would burn the player's crystal day for nothing.</para>
        /// </summary>
        public static void MarkCrystalsPaid(string configId)
        {
            if (string.IsNullOrEmpty(configId))
            {
                FlowTrace.Warn("Raid", "RaidClaimService.MarkCrystalsPaid: empty configId - crystal day NOT stamped.");
                return;
            }
            string day = UtcDay.Key();
            PlayerPrefs.SetString(PrefCrystalDayKey + configId, day);
            PlayerPrefs.Save();
            FlowTrace.Step("Raid", $"RaidClaimService: crystal day-stamp SET for '{configId}' = {day} " +
                                   $"(persisted dotr-raid-crystalday-{configId}). Further clears today pay 0 crystals.");
        }

        /// <summary>
        /// Test/dev hook: drop the crystal day-stamp so the camp can pay crystals again today.
        /// Exercised by <c>RaidRepeatClearRegression</c>, which round-trips the stamp on a
        /// scratch id and restores the pref. (The claim set went write-only for months because
        /// its reset hook had zero callers - an unexercised hook proves nothing.)
        /// </summary>
        public static void ClearCrystalDayStamp(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return;
            PlayerPrefs.DeleteKey(PrefCrystalDayKey + configId);
            PlayerPrefs.Save();
            FlowTrace.Step("Raid", $"RaidClaimService: crystal day-stamp on '{configId}' CLEARED.");
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
