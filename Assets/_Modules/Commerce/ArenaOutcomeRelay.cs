// =============================================================================
// ArenaOutcomeRelay - the ONE door battle XP enters through, made rail-neutral
// (WO-1282).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Commerce   Namespace: DeNelle.Commerce   (STATIC)
//
// WHAT MOVED AND WHAT DID NOT. DeNelle.Village.Arena.ArenaProgressStore used to call
// DeNelle.Wallet.BattlePassService.OnArenaResult directly. BattlePassService cannot
// leave Wallet - it reads BattleMonthlyCatalog.ActiveSeason, and that catalogue is
// itself entangled with the rail (WO-1282's own CORRECTION block records the proof).
// So the CALL inverts instead: Village publishes an outcome here, and Wallet
// subscribes at boot. Nothing about the battle pass moved.
//
// =============================================================================
//  ⛔ THE ONE-DOOR RULE IS UNCHANGED AND IS STILL ENFORCED.
// -----------------------------------------------------------------------------
// BattlePassService's own header: "There is exactly ONE public way XP enters this
// service - OnArenaResult - and it takes a battle OUTCOME, not an amount." This relay
// carries the same shape deliberately: (win, streak, perfect). There is no amount
// parameter, so nothing that can reach this seam can credit XP directly. Adding an
// int here would re-open the exact door Q4's "NEVER SELL TIERS" ruling closed, and
// BattleMonthlyRegression's [xp-one-door] case asserts against it in source.
//
// =============================================================================
//  ⛔ AND IT IS INSTRUMENTED, BECAUSE AN UNSUBSCRIBED RELAY IS A SILENT FAILURE.
// -----------------------------------------------------------------------------
// WO-1282's correction block names this precisely: a lazily-registered hook whose
// registration never happens makes the whole battle pass read as "nothing happened",
// with no error. So Publish() distinguishes the two cases and says which is which:
//   * NO HANDLER + this build has no battle pass compiled in (Google Play excludes
//     DeNelle.Wallet) -> the arena result is still recorded; only XP is absent. That
//     is correct, and it is traced as a Step, not a failure.
//   * NO HANDLER + a build that DOES carry the pass -> the wiring broke. There is no
//     way to tell those two apart from inside Commerce, so the trace names BOTH
//     readings rather than asserting the comfortable one. Every publish is traced,
//     so the read is one grep either way.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Commerce
{
    /// <summary>
    /// Relays a finished arena bout to whatever progression service this build carries.
    /// Outcome-shaped, never amount-shaped.
    /// </summary>
    public static class ArenaOutcomeRelay
    {
        /// <summary>FlowTrace system tag for every line this seam emits. Matches BattlePassService's.</summary>
        private const string TraceSystem = "BattlePass";

        /// <summary>
        /// The subscribed arena progression handler, installed at boot by the assembly that owns the
        /// battle pass. Deliberately a single handler and not a multicast event: two things
        /// crediting season XP from one bout is a duplicated-state bug, not a feature.
        /// </summary>
        private static Action<bool, int, bool> _handler;

        /// <summary>
        /// The subscribed raid progression handler, installed at boot by the assembly that owns the
        /// battle pass. Deliberately a single handler: raid outcomes and arena outcomes are both
        /// season progression sources, and duplicating either is a bug.
        /// </summary>
        private static Action<int, float, bool, string> _raidHandler;

        /// <summary>
        /// Installs the arena progression handler. Called once, at BeforeSceneLoad, by the assembly that
        /// owns the battle pass. The last registration wins.
        /// </summary>
        public static void RegisterHandler(Action<bool, int, bool> handler)
        {
            _handler = handler;
            FlowTrace.Step(TraceSystem, "ArenaOutcomeRelay: progression handler registered.");
        }

        /// <summary>
        /// Installs the raid progression handler. Called once, at BeforeSceneLoad, by the assembly that
        /// owns the battle pass. The last registration wins.
        /// </summary>
        public static void RegisterRaidHandler(Action<int, float, bool, string> onRaidOutcome)
        {
            _raidHandler = onRaidOutcome;
            FlowTrace.Step(TraceSystem, "ArenaOutcomeRelay: raid handler registered.");
        }

        /// <summary>True when a progression service is listening in this build.</summary>
        public static bool HasHandler => _handler != null;

        /// <summary>True when a raid progression service is listening in this build.</summary>
        public static bool HasRaidHandler => _raidHandler != null;

        /// <summary>
        /// Publish one finished arena bout. The battle result itself is ALREADY recorded by the
        /// caller before this runs - a throw or an absent handler here can never lose a win.
        /// </summary>
        /// <param name="win">True on a victory.</param>
        /// <param name="streak">The caller's own live streak; there is no second streak counter.</param>
        /// <param name="perfect">Optional flawless-bout flag.</param>
        public static void Publish(bool win, int streak, bool perfect = false)
        {
            var handler = _handler;
            if (handler == null)
            {
                FlowTrace.Warn(TraceSystem, "ArenaOutcomeRelay.Publish(win=" + win + ", streak=" + streak +
                    "): NO handler is registered, so no season XP was credited. Two readings, and this " +
                    "seam cannot tell them apart: (a) EXPECTED - this build excludes DeNelle.Wallet " +
                    "(Google Play) and carries no battle pass; (b) DEFECT - a Seeker/dApp-Store build " +
                    "whose BattleMonthlyPanelsBootstrap did not run. The bout itself is unaffected.");
                return;
            }

            Guard.Try(TraceSystem, "publish arena outcome to the battle pass",
                      () => handler(win, streak, perfect));
        }

        /// <summary>
        /// Publish one finished raid. The raid result itself is ALREADY recorded by the caller before
        /// this runs - a throw or an absent handler here can never lose a raid reward.
        /// </summary>
        /// <param name="win">True on a victory.</param>
        /// <param name="stars">The raid's star rating (0-3).</param>
        /// <param name="destructionPct">Percentage destruction (0-100).</param>
        /// <param name="firstClear">True if this is the first clear of this raid config.</param>
        /// <param name="configId">The raid configuration ID (e.g. 'raider_camp_small').</param>
        public static void Publish(bool win, int stars, float destructionPct, bool firstClear, string configId)
        {
            var handler = _raidHandler;
            if (handler == null)
            {
                FlowTrace.Warn(TraceSystem, "ArenaOutcomeRelay.Publish(raid): NO raid handler is registered, " +
                    "so no raid XP was credited. Two readings: (a) EXPECTED - this build excludes the " +
                    "battle pass or raid progression; (b) DEFECT - registration did not run. The raid " +
                    "itself is unaffected.");
                return;
            }

            Guard.Try(TraceSystem, "publish raid outcome to the battle pass",
                      () => handler(stars, destructionPct, firstClear, configId));
        }
    }
}
