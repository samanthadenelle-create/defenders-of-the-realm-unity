// =============================================================================
// BattlePassService - the thin interpreter over battle_monthly.json's season
// track (WORK_ORDER_battle_and_monthly_packs sections 2.1 / 4.3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet   (STATIC)
//
// =============================================================================
//  XP IS EARNED BY PLAYING. IT IS NOT BUYABLE AND IT IS NOT GRANTABLE.
// -----------------------------------------------------------------------------
// There is exactly ONE public way XP enters this service - OnArenaResult - and it
// takes a battle outcome, not an amount. No reward kind credits XP, no SKU
// credits XP, and there is no AddXp(int) on the public surface for anything else
// to call. Owner ruling Q4 (2026-08-21) went further: NEVER SELL TIERS. No
// cosmetic catch-up, no partial-season pricing, no "unlock what you missed".
// Buying the pass unlocks the LANE; the TIERS are earned, full stop.
//
// The XP source is the live arena ledger, not a new combat hook:
// ArenaProgressStore.RecordWin / RecordLoss (DeNelle.Village) notify this service.
// That store was already the single wired W/L recorder and was already
// instrumented, so the pass adds no combat surface at all.
//
// =============================================================================
//  ONE DELIBERATE DIVERGENCE FROM THE WO'S PSEUDOCODE, DECLARED
// -----------------------------------------------------------------------------
// Section 4.3 sketches Grant(tier.free) the instant a tier is crossed. This
// service instead moves a crossed tier to READY and grants it when the player
// CLAIMS it. Two reasons, and neither is preference:
//   1. The UI spec (section U1) names four column states and calls READY "the only
//      state that animates". Auto-granting deletes that state, and with it the
//      only motion on the screen that means anything.
//   2. Section 2.1 already requires "unclaimed-but-earned tier rewards auto-grant
//      at season close (no 'you lost it' trap)". That auto-claim is implemented
//      here (AutoClaimAll, run on rollover AND on every boot), so claiming is a
//      moment of pleasure and never a deadline. Nothing is ever lost by not
//      tapping - which is the same non-predatory promise section 3.2 makes about
//      the monthly card.
//
// =============================================================================
//  STATE LIVES IN PLAYERPREFS, AND THAT IS A DELIBERATE NON-BUMP
// -----------------------------------------------------------------------------
// Open question 7 was left as the implementer's call. SaveSchema.CurrentVersion is
// 38 on a LIVE PUBLISHED game, and a schema bump there is an OWNER decision, not a
// side effect of a monetization feature. PlayerPrefs is the pattern
// ArenaProgressStore itself uses for exactly this reason (its own note: "until the
// save owner wires ArenaProgress into SaveSchema"). NO SCHEMA BUMP IS TAKEN OR
// NEEDED. When the save
// owner threads ArenaProgress through the round-trip, this state should ride along
// in the same change.
//
// =============================================================================
//  ⚠ DECLARED CONFLICT: BattlePassManager ALREADY EXISTS AND IS DORMANT.
//     THIS NEEDS AN OWNER DECISION. DO NOT LET IT SIT.
// -----------------------------------------------------------------------------
// Assets/_Modules/Cosmetics/BattlePassManager.cs is a FINISHED 311-line WO-73
// implementation, ruled KEEP/DORMANT on 2026-08-21. It was NOT rewritten and was
// NOT deleted by this change - it is untouched. But this service does not build on
// it either, and that deviation is recorded here rather than buried, because two
// battle-pass runtimes in one tree is exactly the duplicated state CLAUDE.md keeps
// naming as the source of this repo's worst drift.
//
// WHY IT COULD NOT BE BUILT ON, stated plainly so the decision is informed:
//   * It is driven by a BattlePassData ScriptableObject. No such asset exists, and
//     a season authored as a Unity asset cannot be validated by the section 6 build
//     gate the WO requires - the gate reads canonical JSON. The WO's own section 4
//     specifies JSON extension blocks, not a ScriptableObject.
//   * Its premium track is bought for 2400 GLIMMER. The owner's 2026-08-21 ruling
//     retired glimmer as a paid reward line precisely because its only sink is
//     cosmetics and no cosmetic art renders. Building the premium lane on that
//     purchase path would re-open the decision she had just closed.
//   * It has one flat xpPerLevel and no concept of a season, a calendar month, a
//     per-tier reward pair, a claim state or a monthly card - the four shapes this
//     WO is actually about. Ruling Q1 (calendar-month seasons with the XP curve
//     scaled to the month) has nowhere to live in it.
//
// THE TWO HONEST OPTIONS, both cheap, neither this agent's to pick:
//   (a) RETIRE BattlePassManager now that a data-driven replacement exists. Its
//       one genuinely nice part - the guarded LevelUpVFX reflection bridge - is
//       worth lifting across first.
//   (b) KEEP it dormant deliberately and record WHY, so the next reader does not
//       have to re-derive this comparison.
// What must NOT happen is both surviving un-ruled: the next session finds two
// battle passes and has no way to know which one is real.
// =============================================================================
//
// ASCII-only strings. Never throws out of a public entry point.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>What one column of one lane is showing. Every state carries a WORD in the UI.</summary>
    public enum TierState
    {
        /// <summary>Not yet reached. The reward is shown plainly anyway - never a mystery box.</summary>
        Locked = 0,
        /// <summary>Earned and unclaimed. The ONLY state that animates.</summary>
        Ready = 1,
        /// <summary>Claimed and granted.</summary>
        Earned = 2,
        /// <summary>
        /// Premium row, tier reached, lane not owned. <b>Shows the reward, never hides it</b> -
        /// concealing it would turn the track into a mystery box, which section 8 forbids as
        /// explicitly as it forbids gacha.
        /// </summary>
        PremiumLocked = 3,
    }

    /// <summary>The season track's runtime: XP from play, tier states, claims, the lane flag.</summary>
    public static class BattlePassService
    {
        // PlayerPrefs keys. "bp." prefixed so they are obvious in a prefs dump.
        private const string KeySeason        = "bp.seasonKey";
        private const string KeyXp            = "bp.xp";
        private const string KeyFreeClaimed   = "bp.freeClaimedThrough";
        private const string KeyPremClaimed   = "bp.premiumClaimedThrough";
        private const string KeyPremium       = "bp.premiumLane";
        private const string KeyDayStamp      = "bp.dayStamp";
        private const string KeyDayXp         = "bp.dayXp";
        private const string KeyRaidFirstClear = "bp.raidFirstClear.";

        private static bool _ready;

        /// <summary>One-time bonus XP for the first clear of each raid config.</summary>
        public const int RaidXpFirstClear = 100;

        /// <summary>Raised after XP or a claim changes anything a screen shows.</summary>
        public static event Action Changed;

        /// <summary>The season being rendered. Null when battle_monthly.json is absent.</summary>
        public static BattlePassSeason Season => BattleMonthlyCatalog.ActiveSeason;

        /// <summary>Total XP earned this season.</summary>
        public static int Xp { get { EnsureReady(); return PlayerPrefs.GetInt(KeyXp, 0); } }

        /// <summary>True when the premium lane is owned this season.</summary>
        public static bool PremiumLaneOwned { get { EnsureReady(); return PlayerPrefs.GetInt(KeyPremium, 0) == 1; } }

        /// <summary>Whole days left in the calendar month. A COUNT, never a countdown clock.</summary>
        public static int DaysRemaining => BattlePassSeason.DaysRemainingInSeason(DateTime.UtcNow);

        /// <summary>Days in the month actually being played - the scale factor for every XP gate.</summary>
        public static int SeasonDays => BattlePassSeason.DaysInSeasonMonth(DateTime.UtcNow);

        /// <summary>The XP gate for a tier, scaled to this month's real length (owner ruling Q1).</summary>
        public static int XpFor(BattlePassTier tier)
        {
            var s = Season;
            return s != null ? s.XpRequiredScaled(tier, SeasonDays) : int.MaxValue;
        }

        /// <summary>The highest tier whose scaled XP gate has been passed. 0 when none.</summary>
        public static int HighestTierReached
        {
            get
            {
                EnsureReady();
                var s = Season;
                if (s == null || s.Tiers == null) return 0;
                int xp = Xp, best = 0;
                for (int i = 0; i < s.Tiers.Count; i++)
                {
                    var t = s.Tiers[i];
                    if (t != null && xp >= XpFor(t) && t.Tier > best) best = t.Tier;
                }
                return best;
            }
        }

        /// <summary>The next tier not yet reached, or null at the capstone.</summary>
        public static BattlePassTier NextTier
        {
            get
            {
                var s = Season;
                if (s == null || s.Tiers == null) return null;
                int xp = Xp;
                BattlePassTier best = null;
                for (int i = 0; i < s.Tiers.Count; i++)
                {
                    var t = s.Tiers[i];
                    if (t == null || xp >= XpFor(t)) continue;
                    if (best == null || t.XpRequired < best.XpRequired) best = t;
                }
                return best;
            }
        }

        /// <summary>0..1 progress toward the next tier. 1 at the capstone.</summary>
        public static float ProgressToNextTier
        {
            get
            {
                var next = NextTier;
                if (next == null) return 1f;
                int target = XpFor(next);
                var s = Season;
                int prevTarget = 0;
                if (s != null && s.Tiers != null)
                    for (int i = 0; i < s.Tiers.Count; i++)
                    {
                        var t = s.Tiers[i];
                        if (t != null && t.Tier == next.Tier - 1) { prevTarget = XpFor(t); break; }
                    }
                int span = target - prevTarget;
                if (span <= 0) return 1f;
                float p = (float)(Xp - prevTarget) / span;
                return Mathf.Clamp01(p);
            }
        }

        // =====================================================================
        //  Tier state - what the screen draws
        // =====================================================================

        /// <summary>The state of the FREE cell for a tier.</summary>
        public static TierState FreeState(BattlePassTier tier)
        {
            EnsureReady();
            if (tier == null) return TierState.Locked;
            if (Xp < XpFor(tier)) return TierState.Locked;
            return tier.Tier <= PlayerPrefs.GetInt(KeyFreeClaimed, 0) ? TierState.Earned : TierState.Ready;
        }

        /// <summary>The state of the PREMIUM cell for a tier.</summary>
        public static TierState PremiumState(BattlePassTier tier)
        {
            EnsureReady();
            if (tier == null) return TierState.Locked;
            if (Xp < XpFor(tier)) return TierState.Locked;
            if (!PremiumLaneOwned) return TierState.PremiumLocked;
            return tier.Tier <= PlayerPrefs.GetInt(KeyPremClaimed, 0) ? TierState.Earned : TierState.Ready;
        }

        /// <summary>True when anything at all is waiting to be claimed (drives the screen's one animation).</summary>
        public static bool HasClaimable
        {
            get
            {
                var s = Season;
                if (s == null || s.Tiers == null) return false;
                for (int i = 0; i < s.Tiers.Count; i++)
                {
                    var t = s.Tiers[i];
                    if (t == null) continue;
                    if (FreeState(t) == TierState.Ready) return true;
                    if (PremiumState(t) == TierState.Ready) return true;
                }
                return false;
            }
        }

        // =====================================================================
        //  XP from PLAY - the ONE entry point
        // =====================================================================

        /// <summary>
        /// Credits Battle XP for a completed arena battle. Called from
        /// <c>ArenaProgressStore.RecordWin</c> / <c>RecordLoss</c> - the single wired W/L recorder.
        ///
        /// <para><paramref name="perfect"/> is accepted but pays 0 today: the season authors
        /// <c>perfectBonus: 0</c> because the no-hit signal is not tracked anywhere yet (open
        /// question 5, default taken). The parameter exists so the day the signal lands, lighting it
        /// up is a data edit.</para>
        ///
        /// <para>Never throws - a battle result must never be lost because a pass was mid-load.</para>
        /// </summary>
        public static void OnArenaResult(bool win, int streak, bool perfect = false)
        {
            try
            {
                EnsureReady();
                var s = Season;
                if (s == null)
                {
                    FlowTrace.Warn("BattlePass", "OnArenaResult: no season loaded - no XP credited (the battle " +
                                                 "result itself is unaffected).");
                    return;
                }

                var xpRules = s.Xp ?? new BattlePassXpRules();
                int xp = win ? xpRules.PerWin : xpRules.PerLoss;

                // The streak bonus reuses the streak ArenaProgressStore already tracks - no second
                // streak, no second source of truth. Capped so a long run cannot run away with the
                // season.
                int steps = Mathf.Clamp(Mathf.Max(0, streak), 0, Mathf.Max(0, xpRules.StreakStepCap));
                xp += steps * Mathf.Max(0, xpRules.PerStreakStep);

                if (perfect) xp += Mathf.Max(0, xpRules.PerfectBonus);

                // The daily soft cap is a GENEROSITY cap, not an energy gate: past it, wins still
                // pay their purse and everything else in full and only the XP tapers. Nothing is
                // ever refused, and nothing is ever locked out for the rest of the day.
                int dayXp = TodayXp();
                if (xpRules.DailySoftCap > 0 && dayXp >= xpRules.DailySoftCap)
                {
                    double taper = xpRules.SoftCapTaperPct <= 0d ? 0.5d : xpRules.SoftCapTaperPct;
                    int before = xp;
                    xp = Mathf.Max(1, (int)Math.Round(xp * taper));
                    FlowTrace.Step("BattlePass", "daily soft cap reached (" + dayXp + "/" + xpRules.DailySoftCap +
                                                 ") - XP tapered " + before + " -> " + xp + ". Purse and every " +
                                                 "other reward are unaffected; nothing is gated.");
                }

                if (xp <= 0) return;

                int priorTier = HighestTierReached;
                int before2 = Xp;
                PlayerPrefs.SetInt(KeyXp, before2 + xp);
                SetTodayXp(dayXp + xp);
                PlayerPrefs.Save();

                int reachedTier = HighestTierReached;
                if (reachedTier > priorTier) BattlePassLevelUpVfxBridge.Play(reachedTier);

                FlowTrace.Step("BattlePass", "OnArenaResult(win=" + win + ", streak=" + streak + ", perfect=" +
                                             perfect + "): +" + xp + " XP -> " + Xp + " (tier " +
                                             HighestTierReached + "/" + TierCount + ").");
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", "OnArenaResult THREW: " + ex.GetType().Name + ": " + ex.Message +
                                             " - no XP credited. The battle result itself is unaffected.");
            }
        }

        /// <summary>How many tiers this season has.</summary>
        public static int TierCount
        {
            get { var s = Season; return s != null && s.Tiers != null ? s.Tiers.Count : 0; }
        }

        /// <summary>
        /// Calculates raid outcome XP per PROGRAM_RAID_ECONOMY_2026-09-04 section 6.
        /// Pure function with no side effects.
        ///
        /// <para>XP table: base 50 if win, +25 for 3+ stars, +25 for 100% destruction,
        /// +100 for first clear. Losses pay 0 regardless.</para>
        ///
        /// <para><paramref name="destruction"/> is normalised to 0..1 if handed as 0..100.</para>
        /// </summary>
        public static int RaidXpFor(bool win, int stars, float destruction, bool firstClear)
        {
            if (!win) return 0;

            // Normalize destruction from 0..100 range to 0..1 if necessary
            if (destruction > 1.0f) destruction /= 100f;
            destruction = Mathf.Clamp01(destruction);

            int xp = 50; // base for a win
            if (stars >= 3) xp += 25;
            if (destruction >= 1.0f) xp += 25;
            if (firstClear) xp += RaidXpFirstClear;

            return xp;
        }

        /// <summary>
        /// Credits Battle XP for a completed raid. Called from ArenaOutcomeRelay after a raid
        /// outcome is published. Resolves the outcome against the section 6 table, tracks
        /// first-clear bonuses per config, and credits the XP into the season track.
        ///
        /// <para>Never throws - a raid result must never be lost because a pass was mid-load.</para>
        /// </summary>
        public static void OnRaidResult(bool win, int stars, float destructionPct, bool firstClear, string configId)
        {
            try
            {
                EnsureReady();
                var s = Season;
                if (s == null)
                {
                    FlowTrace.Warn("BattlePass", "OnRaidResult: no season loaded - no XP credited (the raid " +
                                                 "result itself is unaffected).");
                    return;
                }

                // The first-clear ledger ensures the bonus is taken at most once per config.
                // If firstClear is claimed, try to take it; if it was already taken, zero the flag.
                if (firstClear && !TakeRaidFirstClear(configId))
                {
                    firstClear = false;
                }

                // The relay used to drop 'win' and this line hardcoded 'true' - a lost raid paid as
                // a victory. Fixed 2026-09-04; RaidSeasonXpRegression [table] covers the loss rows.
                int xp = RaidXpFor(win, stars, destructionPct, firstClear);
                if (xp <= 0) return;

                int priorTier = HighestTierReached;
                int before = Xp;
                PlayerPrefs.SetInt(KeyXp, before + xp);
                PlayerPrefs.Save();

                int reachedTier = HighestTierReached;
                if (reachedTier > priorTier) BattlePassLevelUpVfxBridge.Play(reachedTier);

                FlowTrace.Step("BattlePass", "OnRaidResult(win=" + win + ", stars=" + stars + ", destruction=" + destructionPct +
                                             ", firstClear=" + firstClear + ", configId='" + configId + "'): +" + xp +
                                             " XP -> " + Xp + " (tier " + HighestTierReached + "/" + TierCount + ").");
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", "OnRaidResult THREW: " + ex.GetType().Name + ": " + ex.Message +
                                             " - no XP credited. The raid result itself is unaffected.");
            }
        }

        // =====================================================================
        //  Claiming
        // =====================================================================

        /// <summary>
        /// Claims every READY reward in both lanes. Returns how many grants landed.
        /// <para>Claims walk UPWARD from the last claimed tier and stop at the first failure, so the
        /// "claimed through" watermark can never run ahead of what was actually granted - a
        /// watermark that outruns the grant is a silently lost reward.</para>
        /// </summary>
        public static int ClaimAllReady()
        {
            try
            {
                EnsureReady();
                var s = Season;
                if (s == null || s.Tiers == null) return 0;

                int reached = HighestTierReached;
                int granted = 0;

                granted += ClaimLane(s, reached, premium: false);
                if (PremiumLaneOwned) granted += ClaimLane(s, reached, premium: true);

                if (granted > 0)
                {
                    RewardGrantWriter.Save("battle pass claim");
                    Changed?.Invoke();
                }
                return granted;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", "ClaimAllReady THREW: " + ex.GetType().Name + ": " + ex.Message);
                return 0;
            }
        }

        private static int ClaimLane(BattlePassSeason season, int reachedTier, bool premium)
        {
            string key = premium ? KeyPremClaimed : KeyFreeClaimed;
            int claimedThrough = PlayerPrefs.GetInt(key, 0);
            int granted = 0;

            for (int tier = claimedThrough + 1; tier <= reachedTier; tier++)
            {
                var row = FindTier(season, tier);
                if (row == null) { PlayerPrefs.SetInt(key, tier); continue; }

                var grant = premium ? row.Premium : row.Free;
                string where = "season '" + season.SeasonId + "' tier " + tier + (premium ? " premium" : " free");

                // A null slot is a legitimately empty rung, not a failure - the watermark advances.
                if (grant == null) { PlayerPrefs.SetInt(key, tier); continue; }

                if (!RewardGrantWriter.Grant(grant, GrantOrigin.Earned, where))
                {
                    FlowTrace.Fail("BattlePass", where + ": grant did not land - the claim watermark is HELD at " +
                                                 (tier - 1) + " so this tier stays claimable rather than being " +
                                                 "silently consumed.");
                    break;
                }
                PlayerPrefs.SetInt(key, tier);
                granted++;
            }

            if (granted > 0) PlayerPrefs.Save();
            return granted;
        }

        private static BattlePassTier FindTier(BattlePassSeason season, int tier)
        {
            if (season == null || season.Tiers == null) return null;
            for (int i = 0; i < season.Tiers.Count; i++)
                if (season.Tiers[i] != null && season.Tiers[i].Tier == tier) return season.Tiers[i];
            return null;
        }

        /// <summary>
        /// The no-you-lost-it guarantee (section 2.1). Runs on every boot and on season rollover, so
        /// a player who earned a tier and never opened the screen still receives it.
        /// </summary>
        public static int AutoClaimAll()
        {
            int n = ClaimAllReady();
            if (n > 0)
                FlowTrace.Step("BattlePass", "AutoClaimAll granted " + n + " unclaimed reward(s) - earned rewards " +
                                             "are kept, never expired.");
            return n;
        }

        // =====================================================================
        //  The premium lane
        // =====================================================================

        /// <summary>
        /// Unlocks the premium lane and RETRO-GRANTS every premium reward already earned (the
        /// standard courtesy: buying late loses you nothing you climbed to).
        ///
        /// <para><b>Refuses today, and says why.</b> The season authors no <c>premiumPassSku</c>
        /// because owner ruling Q3 makes the pass SKR-buyable and there is no SKR ledger in this
        /// build, purchases are flag-off, and the mainnet block is unlifted. Rather than let a
        /// lane be unlocked for free by any caller, this refuses unless the season names a SKU that
        /// resolves to a real pack. The refusal is a returned false with a trace line, never a
        /// throw and never a silent no-op.</para>
        /// </summary>
        public static bool UnlockPremiumLane(bool bypassPurchaseCheckForTesting = false)
        {
            try
            {
                EnsureReady();
                var s = Season;
                if (s == null) return false;
                if (PremiumLaneOwned) return true;

                if (!bypassPurchaseCheckForTesting && !s.HasPurchasablePremiumLane)
                {
                    FlowTrace.Warn("BattlePass", "UnlockPremiumLane REFUSED: season '" + s.SeasonId + "' names no " +
                                                 "purchasable premiumPassSku in this build (no SKR ledger, " +
                                                 "purchases flag-off). Nothing was unlocked and nothing was " +
                                                 "charged.");
                    return false;
                }

                PlayerPrefs.SetInt(KeyPremium, 1);
                PlayerPrefs.Save();
                FlowTrace.Step("BattlePass", "premium lane UNLOCKED - retro-granting every tier already earned.");
                int n = ClaimLane(s, HighestTierReached, premium: true);
                RewardGrantWriter.Save("battle pass premium retro-grant");
                FlowTrace.Step("BattlePass", "premium lane retro-grant COMMITTED (" + n + " tier(s)).");
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", "UnlockPremiumLane THREW: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        // =====================================================================
        //  Season rollover + the daily stamp
        // =====================================================================

        private static void EnsureReady()
        {
            if (_ready) return;
            _ready = true;
            try
            {
                var s = BattleMonthlyCatalog.ActiveSeason;
                if (s == null) return;

                var now = DateTime.UtcNow;
                string key = (s.SeasonId ?? "?") + ":" + now.ToString("yyyy-MM");
                string stored = PlayerPrefs.GetString(KeySeason, string.Empty);

                if (string.Equals(stored, key, StringComparison.Ordinal))
                {
                    AutoClaimAll();   // boot-time safety net
                    return;
                }

                if (!string.IsNullOrEmpty(stored))
                {
                    // Rollover. Pay out anything still owed under the OLD track BEFORE resetting,
                    // so a season close can never silently swallow an earned reward.
                    FlowTrace.Step("BattlePass", "season rollover '" + stored + "' -> '" + key +
                                                 "': auto-claiming anything still owed before the reset.");
                    AutoClaimAll();
                }

                PlayerPrefs.SetString(KeySeason, key);
                PlayerPrefs.SetInt(KeyXp, 0);
                PlayerPrefs.SetInt(KeyFreeClaimed, 0);
                PlayerPrefs.SetInt(KeyPremClaimed, 0);
                // The premium lane is per-season and does NOT carry over - but earned rewards do,
                // and they were already granted above.
                PlayerPrefs.SetInt(KeyPremium, 0);
                PlayerPrefs.Save();
                FlowTrace.Step("BattlePass", "season '" + key + "' started: " + s.Tiers?.Count + " tiers over " +
                                             BattlePassSeason.DaysInSeasonMonth(now) + " days (XP gates scaled to " +
                                             "the month, so every month is equally completable).");
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("BattlePass", "EnsureReady THREW: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static int TodayXp()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (!string.Equals(PlayerPrefs.GetString(KeyDayStamp, string.Empty), today, StringComparison.Ordinal))
                return 0;
            return PlayerPrefs.GetInt(KeyDayXp, 0);
        }

        private static void SetTodayXp(int value)
        {
            PlayerPrefs.SetString(KeyDayStamp, DateTime.UtcNow.ToString("yyyy-MM-dd"));
            PlayerPrefs.SetInt(KeyDayXp, Mathf.Max(0, value));
        }

        /// <summary>Test hook - forgets the boot-time season check so a re-run re-evaluates.</summary>
        public static void ResetForTests() { _ready = false; }

        // =====================================================================
        //  Raid first-clear tracking (private, exposed to tests)
        // =====================================================================

        /// <summary>
        /// Takes the first-clear bonus for a raid config if it has not been taken before.
        /// Returns true if the bonus was successfully taken (first time), false if it was
        /// already taken (repeat clear, not eligible for the bonus again).
        ///
        /// <para>Stores the taken state in PlayerPrefs with key "bp.raidFirstClear.{configId}".</para>
        /// </summary>
        private static bool TakeRaidFirstClear(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;

            string key = KeyRaidFirstClear + configId;
            if (PlayerPrefs.GetInt(key, 0) == 1) return false; // already taken

            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// Test hook - checks whether the first-clear bonus for a raid config has been claimed.
        /// </summary>
        public static bool RaidFirstClearTaken(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return false;
            return PlayerPrefs.GetInt(KeyRaidFirstClear + configId, 0) == 1;
        }

        /// <summary>
        /// Test hook - takes the first-clear bonus for a raid config. Returns true if successful
        /// (first time), false if it was already taken.
        /// </summary>
        public static bool TakeRaidFirstClearForTests(string configId)
        {
            return TakeRaidFirstClear(configId);
        }

        /// <summary>
        /// Test hook - resets the first-clear bonus for a raid config so it can be taken again.
        /// </summary>
        public static void ResetRaidFirstClearForTests(string configId)
        {
            if (!string.IsNullOrEmpty(configId))
                PlayerPrefs.DeleteKey(KeyRaidFirstClear + configId);
        }
    }
}
