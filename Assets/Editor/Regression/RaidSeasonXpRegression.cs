// =============================================================================
// RaidSeasonXpRegression [raid-season-xp] -- raids feed the Season Pass, and they
// do it through an OUTCOME door with the table resolved inside the service.
// (PROGRAM_RAID_ECONOMY_2026-09-04 section 6 -- Lane C.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Contract mirrors the siblings:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: RAID_SEASON_XP_OK (Debug.Log) / RAID_SEASON_XP_FAIL (LogError)
//   registered ONCE inside DataRegression.RunAll's fenced registry region.
//
// =============================================================================
//  WHY THIS SUITE EXISTS SEPARATELY FROM [xp-one-door]
// -----------------------------------------------------------------------------
// BattleMonthlyRegression's [xp-one-door] case polices what may NOT enter the pass
// (an amount, a purchased tier, a public AddXp). It is a firewall and it is left
// exactly as it was -- this suite does not weaken it, does not re-point it, and
// does not restate it.
//
// This suite polices the OPPOSITE failure, which is the one the raid programme is
// actually exposed to: the door exists, the arithmetic is wrong or nothing is
// plugged into it, and the whole feature reads as "raids just do not give XP" with
// NO ERROR ANYWHERE. That is the same silent-failure shape WO-1282's correction
// block named for the arena relay, and it cost a session then.
//
// THREE CASES, AND THE FIRST TWO ARE THE POINT:
//
//   * [table]      RaidXpFor is called for real and its answers are compared to the
//                  section 6 table by VALUE: +50 completed, +25 three-star, +25 for
//                  100% destruction, +100 first clear, and 0 for a loss. A source
//                  scan could not have caught an operator typo; this can.
//
//   * [once-ever]  the first-clear bonus is taken ONCE. Proven by driving the
//                  ledger itself through the test hooks, which touch the ledger and
//                  never XP -- so the gate cannot move a developer's season track
//                  as a side effect of running.
//
//   * [wired]      the relay carries a raid door and something under _Modules/Wallet
//                  actually subscribes BattlePassService.OnRaidResult to it. An
//                  unsubscribed relay is the silent failure above, so the
//                  SUBSCRIPTION is asserted, not just the publish surface -- the
//                  same reasoning [xp-one-door] uses, applied to the new seam.
//
// ⚠ WHAT THIS SUITE DELIBERATELY DOES NOT ASSERT: that any gameplay caller publishes
// a raid outcome yet. At the time of writing NOTHING does -- the raid lanes are being
// built in parallel and this is the contract they call. Asserting a caller here would
// fail the gate for work that has not landed, which is a different defect. When a
// raid publisher ships, add the publish assertion in that same change.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    /// <summary>Pins the raid -> Season Pass XP contract: the table, the once-ever bonus, the wiring.</summary>
    public static class RaidSeasonXpRegression
    {
        /// <summary>A ledger key no shipped raid target can collide with.</summary>
        private const string ProbeRaidId = "__raid_season_xp_regression_probe";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== RaidSeasonXpRegression [raid-season-xp] (section 6: raids feed the Season Pass) ===");

            try
            {
                CaseTable(failures, log);
                CaseFirstClearOnceEver(failures, log);
                CaseWired(failures, log);
            }
            catch (Exception ex)
            {
                // NEVER throws (the suite contract): a throw here takes the whole gate down and
                // tells nobody which rule broke.
                failures.Add("[raid-season-xp] RaidSeasonXpRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "RAID SEASON XP OK - one raid outcome pays exactly the section 6 table (+50 completed, " +
                         "+25 three-star, +25 hundred-percent, +100 first clear; a loss pays nothing), the " +
                         "first-clear bonus is takeable exactly once per target, and the raid outcome door is " +
                         "wired end to end: ArenaOutcomeRelay carries a raid publish and something under " +
                         "_Modules/Wallet subscribes BattlePassService.OnRaidResult to it.";
                Debug.Log("RAID_SEASON_XP_OK\n" + log);
                return true;
            }

            reason = "raid-season-xp: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("RAID_SEASON_XP_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  [table] -- the section 6 numbers, asserted by VALUE against the real
        //  function. RaidXpFor is pure, so this reads no state and writes none.
        // =====================================================================
        private static void CaseTable(List<string> failures, StringBuilder log)
        {
            // (win, stars, destruction 0..1, firstClear) -> expected XP, and WHY.
            Expect(failures, true,  3, 1.00f, true,  200, "perfect first clear = 50 + 25 + 25 + 100");
            Expect(failures, true,  3, 1.00f, false, 100, "perfect repeat clear = 50 + 25 + 25");
            Expect(failures, true,  3, 0.80f, false,  75, "three stars, partial destruction = 50 + 25");
            Expect(failures, true,  1, 0.40f, false,  50, "a plain clear is the base line only");
            Expect(failures, true,  0, 0.00f, true,  150, "a first clear pays even at zero stars = 50 + 100");
            Expect(failures, false, 3, 1.00f, true,    0, "a LOST raid earns no season XP, however it went");

            // The 0..100 tolerance is behaviour, not decoration: a caller handing 100 for "one
            // hundred percent" must not silently lose the 25.
            Expect(failures, true, 3, 100f, false, 100, "destruction handed as a 0..100 percentage is normalised");

            if (failures.Count == 0)
                log.AppendLine("  [table] RaidXpFor pays exactly the section 6 table across 7 outcomes");
        }

        private static void Expect(List<string> failures, bool win, int stars, float destruction,
                                   bool firstClear, int expected, string why)
        {
            int actual = BattlePassService.RaidXpFor(win, stars, destruction, firstClear);
            if (actual != expected)
                failures.Add("[table] RaidXpFor(win=" + win + ", stars=" + stars + ", destruction=" + destruction +
                             ", firstClear=" + firstClear + ") returned " + actual + ", expected " + expected +
                             " (" + why + "). PROGRAM_RAID_ECONOMY_2026-09-04 section 6 is the authority; if the " +
                             "owner re-tuned the table, change the constants AND this oracle in the same commit.");
        }

        // =====================================================================
        //  [once-ever] -- +100 is a discovery bonus, not a drip.
        //  Driven through the ledger hooks so the gate credits no XP at all.
        // =====================================================================
        private static void CaseFirstClearOnceEver(List<string> failures, StringBuilder log)
        {
            try
            {
                BattlePassService.ResetRaidFirstClearForTests(ProbeRaidId);

                if (BattlePassService.RaidFirstClearTaken(ProbeRaidId))
                {
                    failures.Add("[once-ever] the first-clear ledger still reports the probe target as taken " +
                                 "immediately after a reset - the reset hook does not clear what the take hook " +
                                 "writes, so this oracle cannot prove anything about the real bonus.");
                    return;
                }

                bool first  = BattlePassService.TakeRaidFirstClearForTests(ProbeRaidId);
                bool second = BattlePassService.TakeRaidFirstClearForTests(ProbeRaidId);

                if (!first)
                    failures.Add("[once-ever] the FIRST take of an untouched target's first-clear bonus was " +
                                 "refused. A player clearing a new camp would never receive the +" +
                                 BattlePassService.RaidXpFirstClear + ".");
                if (second)
                    failures.Add("[once-ever] the SECOND take of the same target's first-clear bonus SUCCEEDED. A " +
                                 "repeat publish of one clear - a retry, a double-fired victory event, a " +
                                 "re-rendered result screen - would pay +" + BattlePassService.RaidXpFirstClear +
                                 " again, which is a farmable season track.");

                if (first && !second)
                    log.AppendLine("  [once-ever] the first-clear bonus is takeable exactly once per target");
            }
            finally
            {
                // Leave no probe row behind, pass or fail. A gate that pollutes the prefs it tests
                // is a gate that passes for the wrong reason on its second run.
                BattlePassService.ResetRaidFirstClearForTests(ProbeRaidId);
            }
        }

        // =====================================================================
        //  [wired] -- the door exists AND something is on the other side of it.
        // =====================================================================
        private static void CaseWired(List<string> failures, StringBuilder log)
        {
            string relay = Application.dataPath + "/_Modules/Commerce/ArenaOutcomeRelay.cs";
            if (!File.Exists(relay))
            {
                failures.Add("[wired] ArenaOutcomeRelay.cs not found at " + relay + " - FAIL, not unknown.");
            }
            else
            {
                string src = File.ReadAllText(relay);
                if (src.IndexOf("public static void RegisterRaidHandler(", StringComparison.Ordinal) < 0)
                    failures.Add("[wired] ArenaOutcomeRelay exposes no RegisterRaidHandler. Raid outcomes then have " +
                                 "nowhere to be published and the Season Pass silently never advances from raiding.");
                if (src.IndexOf("float destructionPct", StringComparison.Ordinal) < 0)
                    failures.Add("[wired] ArenaOutcomeRelay carries no raid Publish overload taking " +
                                 "(win, stars, destructionPct, firstClear). That signature IS the contract the raid " +
                                 "lanes call; renaming it silently breaks every caller at once.");
            }

            string service = Application.dataPath + "/_Modules/Wallet/BattlePassService.cs";
            if (!File.Exists(service))
            {
                failures.Add("[wired] BattlePassService.cs not found at " + service + " - FAIL, not unknown.");
            }
            else
            {
                string src = File.ReadAllText(service);
                if (src.IndexOf("public static void OnRaidResult(", StringComparison.Ordinal) < 0)
                    failures.Add("[wired] BattlePassService.OnRaidResult not found. It is the raid half of the " +
                                 "outcome-typed XP surface; if it was renamed, re-point this oracle in the same " +
                                 "change.");

                // The mirror of [xp-one-door], applied to the raid seam: an AMOUNT parameter here
                // would move the section 6 table OUT of the service and into whatever called it.
                if (src.IndexOf("public static void OnRaidResult(int ", StringComparison.Ordinal) >= 0 ||
                    src.IndexOf("public static void AddRaidXp(", StringComparison.Ordinal) >= 0)
                    failures.Add("[wired] BattlePassService exposes a raid XP entry that takes an AMOUNT. Raid XP " +
                                 "must enter as an OUTCOME and resolve against the section 6 table inside this " +
                                 "service, exactly as arena XP does (owner ruling Q4, 'NEVER SELL TIERS').");
            }

            if (!SubscribesRaidHandler())
                failures.Add("[wired] NOTHING under _Modules/Wallet registers BattlePassService.OnRaidResult as " +
                             "the raid handler. Every raid publish would then go nowhere: the raid still pays its " +
                             "loot, the season track silently never moves, and there is no error on screen. Wire " +
                             "ArenaOutcomeRelay.RegisterRaidHandler(BattlePassService.OnRaidResult) at boot " +
                             "(BattleMonthlyPanelsBootstrap).");
            else
                log.AppendLine("  [wired] raid outcomes publish through ArenaOutcomeRelay and are subscribed at boot");
        }

        /// <summary>
        /// True when SOMETHING under <c>_Modules/Wallet</c> subscribes
        /// <c>BattlePassService.OnRaidResult</c> to the relay. Scans the folder rather than one named
        /// file because WHICH bootstrap owns the wiring is an implementation detail; that the wiring
        /// EXISTS is the invariant. (Same shape as BattleMonthlyRegression.SubscribesToArenaRelay.)
        /// </summary>
        private static bool SubscribesRaidHandler()
        {
            string root = Application.dataPath + "/_Modules/Wallet";
            if (!Directory.Exists(root)) return false;
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string src;
                try
                {
                    src = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    // CLAUDE.md section 12: a catch that swallows without logging is forbidden. An
                    // unreadable file here could hide the very subscription being looked for, so the
                    // skip is announced rather than absorbed.
                    Debug.LogWarning("[raid-season-xp] could not read " + file + " while looking for the raid " +
                                     "handler subscription: " + ex.GetType().Name + ": " + ex.Message +
                                     ". Skipped - if the wiring lives in THIS file, the case will now fail for " +
                                     "the wrong reason.");
                    continue;
                }
                if (src.IndexOf("ArenaOutcomeRelay.RegisterRaidHandler(BattlePassService.OnRaidResult)",
                                StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }
}
