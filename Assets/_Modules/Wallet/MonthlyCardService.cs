// =============================================================================
// MonthlyCardService - the pool-model claim runtime for the Monthly Ledger cards
// (WORK_ORDER_battle_and_monthly_packs section 3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet   (STATIC)
//
// =============================================================================
//  THE POOL MODEL IS THE WHOLE PRODUCT DECISION. READ THIS BEFORE CHANGING IT.
// -----------------------------------------------------------------------------
// durationDays is a count of CLAIMS, not a calendar expiry (open question 2,
// resolved to POOL - the WO's own recommendation and the only model under which
// its section 3.2 promise is literally true).
//
//   * A missed day is NEVER LOST. The card lives until all of its claims are
//     spent, however long that takes.
//   * NOTHING EXPIRES, so nothing counts down. There is no timer in this service
//     and there must be no timer on the screen - a ticking clock over a pool that
//     cannot lapse is a lie that manufactures urgency, which is exactly the
//     pressure section 3.2 promises not to apply. The header says "N claims left".
//   * There is NO STREAK and no streak penalty. Missing a day costs the player
//     nothing at all.
//   * Buying again while a card is active EXTENDS the pool (stackable), never
//     overwrites it. A player can never lose claims by re-buying.
//   * The drip is a BONUS ON TOP OF the free daily system. It is not a timer you
//     pay to avoid, and a non-buyer's daily rewards are untouched by this file -
//     nothing here reads or writes DailyQuestRewardBridge state. The two claims
//     are independent; claiming one never consumes the other.
//
// ONE CLAIM PER UTC DAY, LATCHED. The latch mirrors DailyQuestRewardBridge's
// ClaimedAtUnix discipline: the stamp is written BEFORE the grant is attempted is
// exactly what we do NOT do - see Claim() for why the order is grant-then-stamp
// here and what protects against a double grant.
//
// STATE IN PLAYERPREFS - NO SCHEMA BUMP. SaveSchema.CurrentVersion is 38 on a live
// published game; bumping it is an OWNER decision, not a side effect of a
// monetization feature. Additive PlayerPrefs keys need no migration and no bump.
//
// ASCII-only strings. Never throws out of a public entry point.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>What one cell of the 30-day grid is showing.</summary>
    public enum MonthlyDayState
    {
        /// <summary>Not owned yet, or beyond the remaining pool. The reward is still shown.</summary>
        Upcoming = 0,
        /// <summary>Owned, still in the pool, not claimable until the next UTC day.</summary>
        Available = 1,
        /// <summary>Claimable right now. The ONLY cell that animates.</summary>
        Today = 2,
        /// <summary>Already claimed.</summary>
        Claimed = 3,
    }

    /// <summary>Per-card claim pool, the UTC-day latch, and the grant dispatch.</summary>
    public static class MonthlyCardService
    {
        /// <summary>Raised after a claim or an activation changes anything a screen shows.</summary>
        public static event Action Changed;

        private static string KeyOwned(string sku)      => "mc." + sku + ".owned";
        private static string KeyClaimsLeft(string sku) => "mc." + sku + ".claimsLeft";
        private static string KeyNextDay(string sku)    => "mc." + sku + ".nextDay";
        private static string KeyLastDay(string sku)    => "mc." + sku + ".lastClaimUtc";

        /// <summary>Every authored card, cheapest tier first as authored.</summary>
        public static System.Collections.Generic.IReadOnlyList<MonthlyCard> Cards => BattleMonthlyCatalog.Cards;

        /// <summary>True when the player holds this card with claims left.</summary>
        public static bool IsActive(string sku) =>
            !string.IsNullOrEmpty(sku) &&
            PlayerPrefs.GetInt(KeyOwned(sku), 0) == 1 &&
            ClaimsRemaining(sku) > 0;

        /// <summary>How many claims are left in the pool. This is the ONLY number the header shows.</summary>
        public static int ClaimsRemaining(string sku) =>
            string.IsNullOrEmpty(sku) ? 0 : Mathf.Max(0, PlayerPrefs.GetInt(KeyClaimsLeft(sku), 0));

        /// <summary>The 1-based day of the table that the next claim will pay.</summary>
        public static int NextDay(string sku) =>
            string.IsNullOrEmpty(sku) ? 1 : Mathf.Max(1, PlayerPrefs.GetInt(KeyNextDay(sku), 1));

        /// <summary>True when a claim is available right now (owned, claims left, not yet claimed this UTC day).</summary>
        public static bool CanClaimToday(string sku)
        {
            if (!IsActive(sku)) return false;
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            return !string.Equals(PlayerPrefs.GetString(KeyLastDay(sku), string.Empty), today, StringComparison.Ordinal);
        }

        /// <summary>
        /// The state of one grid cell.
        /// <para>Note that <see cref="MonthlyDayState.Upcoming"/> is what a NON-OWNER sees for all
        /// thirty days - which is the point of drawing all thirty pre-purchase. Once the card is
        /// owned, every unclaimed day in the pool reads <see cref="MonthlyDayState.Available"/>,
        /// because under the pool model every one of them is still the player's. That is the state
        /// that proves the promise.</para>
        /// </summary>
        public static MonthlyDayState DayState(string sku, int day)
        {
            if (string.IsNullOrEmpty(sku) || day < 1) return MonthlyDayState.Upcoming;
            if (PlayerPrefs.GetInt(KeyOwned(sku), 0) != 1) return MonthlyDayState.Upcoming;

            int next = NextDay(sku);
            if (day < next) return MonthlyDayState.Claimed;
            if (day == next) return CanClaimToday(sku) ? MonthlyDayState.Today : MonthlyDayState.Available;

            int left = ClaimsRemaining(sku);
            return day <= next - 1 + left ? MonthlyDayState.Available : MonthlyDayState.Upcoming;
        }

        // =====================================================================
        //  Activation (purchase entitlement)
        // =====================================================================

        /// <summary>
        /// Applies a purchased card: adds <c>durationDays</c> claims to the pool and grants the
        /// month-exclusive cosmetic up front.
        ///
        /// <para><b>Nothing calls this today and that is correct.</b> No monthly card SKU is on the
        /// shelf: FeatureFlags.RealmStorePurchase is defaultOn:false with the mainnet block
        /// unlifted, so nothing can be bought. This is the seam a purchase will land on, written
        /// now so the screens can be exercised, and it is idempotent per purchase because the
        /// caller is the confirmed-payment path.</para>
        ///
        /// <para><b>Stacking EXTENDS, never overwrites</b> (section 3.3). A player who buys a
        /// second month while one is running keeps every claim they had.</para>
        ///
        /// <para>The exclusive cosmetic is granted ONCE per SKU. Today it is unauthored in the data
        /// for the honest reason recorded there - no cosmetic art exists, so it would land on a
        /// preview tint - so this branch is a no-op until G1.</para>
        /// </summary>
        public static bool ActivateCard(string sku)
        {
            try
            {
                var card = BattleMonthlyCatalog.FindCard(sku);
                if (card == null)
                {
                    FlowTrace.Fail("MonthlyCard", "ActivateCard('" + (sku ?? "<null>") + "'): no such card in " +
                                                  "battle_monthly.json - NOTHING was activated. If a payment " +
                                                  "settled, the player is charged with no entitlement.");
                    return false;
                }

                bool firstPurchase = PlayerPrefs.GetInt(KeyOwned(sku), 0) != 1;
                int before = ClaimsRemaining(sku);
                int add = Mathf.Max(0, card.DurationDays);

                PlayerPrefs.SetInt(KeyOwned(sku), 1);
                PlayerPrefs.SetInt(KeyClaimsLeft(sku), before + add);
                if (firstPurchase) PlayerPrefs.SetInt(KeyNextDay(sku), 1);
                PlayerPrefs.Save();

                FlowTrace.Step("MonthlyCard", "ActivateCard '" + sku + "': claim pool " + before + " -> " +
                                              (before + add) + (firstPurchase ? " (first purchase)" : " (EXTENDED " +
                                              "by a re-buy - never overwritten)") + ".");

                if (firstPurchase && !string.IsNullOrEmpty(card.ExclusiveCosmetic))
                {
                    var grant = new RewardGrant { KindRaw = "cosmetic_sku", CosmeticSku = card.ExclusiveCosmetic };
                    RewardGrantWriter.Grant(grant, GrantOrigin.Purchased, "card '" + sku + "' exclusive cosmetic");
                }

                RewardGrantWriter.Save("monthly card activation");
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("MonthlyCard", "ActivateCard THREW: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        // =====================================================================
        //  Claiming
        // =====================================================================

        /// <summary>
        /// Spends one claim and pays that day's drip. Returns true when a reward landed.
        ///
        /// <para><b>The latch order is GRANT FIRST, THEN STAMP, and it is deliberate.</b> Stamping
        /// first would protect against a double grant at the cost of BURNING a claim whenever the
        /// grant fails - a paid claim silently consumed for nothing, which is the worse failure on
        /// a card the player bought. Granting first means a failed grant leaves the claim intact and
        /// the day still claimable. The double-grant window it opens is a single synchronous call
        /// with no await inside it, and <c>_claiming</c> closes re-entrancy from a double-tap.</para>
        /// </summary>
        public static bool Claim(string sku)
        {
            if (_claiming) return false;
            _claiming = true;
            try
            {
                if (!CanClaimToday(sku))
                {
                    FlowTrace.Step("MonthlyCard", "Claim '" + (sku ?? "<null>") + "' refused: nothing claimable " +
                                                  "right now (owned=" + PlayerPrefs.GetInt(KeyOwned(sku ?? ""), 0) +
                                                  ", claimsLeft=" + ClaimsRemaining(sku) + "). Nothing expires - " +
                                                  "the claim is still there tomorrow.");
                    return false;
                }

                var card = BattleMonthlyCatalog.FindCard(sku);
                if (card == null) return false;

                int day = NextDay(sku);
                var drip = card.Day(day);
                string where = "card '" + sku + "' day " + day;

                if (drip == null || drip.Grant == null)
                {
                    // A hole in the table must not strand the pool forever: advance past it, keep
                    // the claim (the player is not charged a claim for a row that pays nothing) and
                    // say so loudly.
                    FlowTrace.Fail("MonthlyCard", where + ": no grant authored for this day - advancing past it " +
                                                  "WITHOUT spending a claim. The 30-day table has a hole and the " +
                                                  "regression should have caught it.");
                    PlayerPrefs.SetInt(KeyNextDay(sku), day + 1);
                    PlayerPrefs.Save();
                    return false;
                }

                if (!RewardGrantWriter.Grant(drip.Grant, GrantOrigin.Purchased, where))
                {
                    FlowTrace.Fail("MonthlyCard", where + ": grant did not land - the claim was NOT spent and the " +
                                                  "day stays claimable. A paid claim is never burned for nothing.");
                    return false;
                }

                PlayerPrefs.SetInt(KeyNextDay(sku), day + 1);
                PlayerPrefs.SetInt(KeyClaimsLeft(sku), Mathf.Max(0, ClaimsRemaining(sku) - 1));
                PlayerPrefs.SetString(KeyLastDay(sku), DateTime.UtcNow.ToString("yyyy-MM-dd"));
                PlayerPrefs.Save();
                RewardGrantWriter.Save(where);

                FlowTrace.Step("MonthlyCard", where + ": CLAIMED (" + drip.Grant.Describe() + "). " +
                                              ClaimsRemaining(sku) + " claim(s) left in the pool.");
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("MonthlyCard", "Claim THREW: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                _claiming = false;
            }
        }

        private static bool _claiming;

        /// <summary>Test hook - clears every stored pool for one card.</summary>
        public static void ResetCardForTests(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return;
            PlayerPrefs.DeleteKey(KeyOwned(sku));
            PlayerPrefs.DeleteKey(KeyClaimsLeft(sku));
            PlayerPrefs.DeleteKey(KeyNextDay(sku));
            PlayerPrefs.DeleteKey(KeyLastDay(sku));
            PlayerPrefs.Save();
        }
    }
}
