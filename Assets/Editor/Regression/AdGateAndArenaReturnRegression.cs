// =============================================================================
// AdGateAndArenaReturnRegression — headless oracle for two 2026-08-07 fixes that
// shipped WITHOUT a suite. Marker: AD_GATE_ARENA_OK / AD_GATE_ARENA_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Wired into DataRegression.RunAll.
// Style/contract mirrors the other Run(out reason) oracles.
//
// WHY THESE TWO TOGETHER: both are "a thing that must NEVER come back" rather than a
// feature. Neither had a pin, and both are the kind of regression that is invisible
// until it costs money or strands a player.
//
// 1. THE REWARDED-AD GATE (release blocker, found twice by the monetization review
//    and again by the WO-911 work that widened it to all three channels).
//    RewardedAdManager.ShowAdInternal used to be:
//        protected virtual void ShowAdInternal(Action onReward) { onReward?.Invoke(); }
//    i.e. "watch an ad to skip 10 minutes" GRANTED THE REWARD INSTANTLY, with no ad
//    and no revenue. There is still NO ad SDK in the project. This oracle proves the
//    stub cannot grant and the flag defaults OFF, so the free-skip path cannot
//    silently return.
//
// 2. THE ARENA HOME-RETURN (owner stranded twice, on Seeker AND desktop EXE).
//    doMaskedReturn is the ONLY route home from a won arena, and it was handed to the
//    victory panel as its Continue action — a UI object three code paths destroy
//    without firing it. The village wave banner did exactly that. This oracle proves
//    the arena still owns a watchdog and the wave banner still checks for a live
//    battle before it shows.
//
// Proves, with REAL types and no play mode (reflection only — these are runtime
// behaviours, so the oracle asserts the SEAMS exist and are shaped correctly):
//   * FeatureFlags.RewardedAdSkip exists and defaults OFF;
//   * RewardedAdManager.ShowAdInternal returns bool (a void signature is the old,
//     always-granting shape) and the base body does not invoke the reward;
//   * BattleArena carries a stranding watchdog with a bounded timeout;
//   * WaveCelebrationManager consults BattleArena before showing its banner;
//   * ASCII ONLY in the player-visible strings these paths emit.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace DeNelle.Editor
{
    public static class AdGateAndArenaReturnRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== AdGateAndArenaReturnRegression: ad gate + arena home-return ===");

            try
            {
                CheckAdGate(failures, log);
                CheckArenaReturn(failures, log);
                CheckAsciiOnly(failures, log);
            }
            catch (Exception e)
            {
                failures.Add("AdGateAndArenaReturn threw: " + e.GetType().Name + ": " + e.Message);
            }

            if (failures.Count > 0)
            {
                reason = "AD_GATE_ARENA_FAIL — " + string.Join(" | ", failures);
                return false;
            }
            reason = "AD_GATE_ARENA_OK — " + log.ToString().Replace(Environment.NewLine, " ");
            return true;
        }

        // ── 1. the rewarded-ad gate ──────────────────────────────────────────
        private static void CheckAdGate(List<string> failures, StringBuilder log)
        {
            var flags = FindType("DeNelle.Core.FeatureFlags");
            if (flags == null) { failures.Add("FeatureFlags type not found"); return; }

            var prop = flags.GetProperty("RewardedAdSkip", BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                failures.Add("FeatureFlags.RewardedAdSkip is GONE — the rewarded-ad path is ungated again. " +
                             "Without it the ad button grants a free timer skip with no SDK behind it.");
                return;
            }

            // Default OFF. Reading the property honours any PlayerPrefs override, so assert on the
            // DECLARED default in source rather than the live value (a dev machine may have it on).
            var src = ReadRepoFile("Assets/_Modules/Core/FeatureFlags.cs");
            if (src != null && !src.Contains("Get(\"rewardedadskip\", defaultOn: false)"))
                failures.Add("FeatureFlags.RewardedAdSkip no longer declares defaultOn: false — " +
                             "the ad path must stay OFF until a real SDK lands AND WO-912 server-side " +
                             "window validation ships (the window is stamped from the DEVICE clock, so " +
                             "a clock roll mints a fresh allowance = fabricated impressions = account ban).");
            else
                log.AppendLine("[ad-gate] RewardedAdSkip present, declares defaultOn: false");

            // The stub must not be able to grant. A void ShowAdInternal is the OLD shape whose whole
            // body was onReward?.Invoke().
            var mgr = FindType("DeNelle.Village.RewardedAdManager")
                   ?? FindType("RewardedAdManager");
            if (mgr == null) { failures.Add("RewardedAdManager type not found"); return; }

            var show = mgr.GetMethod("ShowAdInternal",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (show == null)
                failures.Add("RewardedAdManager.ShowAdInternal not found — the SDK seam is gone.");
            else if (show.ReturnType != typeof(bool))
                failures.Add("RewardedAdManager.ShowAdInternal returns " + show.ReturnType.Name +
                             ", expected bool. The void form is the old always-granting stub — it " +
                             "called onReward?.Invoke() unconditionally, i.e. a free reward with no ad.");
            else
                log.AppendLine("[ad-gate] ShowAdInternal returns bool (cannot silently grant)");

            var stub = ReadRepoFile("Assets/_Modules/Village/Monetization/RewardedAdManager.cs");
            if (stub != null && stub.Contains("protected virtual void ShowAdInternal"))
                failures.Add("RewardedAdManager still declares a VOID ShowAdInternal — the always-grant " +
                             "stub is back.");
        }

        // ── 2. the arena home-return ─────────────────────────────────────────
        private static void CheckArenaReturn(List<string> failures, StringBuilder log)
        {
            var arena = ReadRepoFile("Assets/_Modules/Village/Arena/BattleArena.cs");
            if (arena == null) { failures.Add("BattleArena.cs not readable"); return; }

            if (!arena.Contains("StrandingWatchdog"))
                failures.Add("BattleArena.StrandingWatchdog is GONE. doMaskedReturn is the ONLY route " +
                             "home from a won arena and it is handed to a UI object that three paths " +
                             "destroy without firing it. Without the watchdog the player is stranded " +
                             "7km out at ArenaCentre with the HUD locked in Battle (owner hit this on " +
                             "both Seeker and desktop).");
            else
                log.AppendLine("[arena] StrandingWatchdog present");

            if (!arena.Contains("StrandWatchdogSeconds"))
                failures.Add("BattleArena.StrandWatchdogSeconds is gone — the watchdog must be BOUNDED, " +
                             "an unbounded wait is the bug it exists to fix.");

            // The banner that ate the victory panel must still check for a live battle.
            var wave = ReadRepoFile("Assets/_Modules/Village/Waves/WaveCelebrationManager.cs");
            if (wave == null) { failures.Add("WaveCelebrationManager.cs not readable"); return; }

            if (!wave.Contains("AnyBattleInProgress"))
                failures.Add("WaveCelebrationManager no longer checks BattleArena.AnyBattleInProgress. " +
                             "EndStateView.Show DESTROYS whatever end-state is open, so an unguarded " +
                             "wave banner replaces a live arena victory summary and takes its " +
                             "home-return action with it. Proven on device: victory at 16:51:59, " +
                             "wave banner at 16:52:02, player stranded.");
            else
                log.AppendLine("[arena] wave banner guarded by AnyBattleInProgress");

            // The abandon paths must stay loud.
            var endState = ReadRepoFile("Assets/_Modules/Village/UI/EndState/EndStateView.cs");
            if (endState != null && !endState.Contains("AbandonedPrimaryWarn"))
                failures.Add("EndStateView.AbandonedPrimaryWarn is gone — the three paths that destroy " +
                             "an end-state without firing its primary action are silent again. That " +
                             "silence is why the original strand had no log line.");
            else if (endState != null)
                log.AppendLine("[arena] AbandonedPrimaryWarn present on the teardown paths");
        }

        // ── 3. ASCII in player-visible strings ───────────────────────────────
        private static void CheckAsciiOnly(List<string> failures, StringBuilder log)
        {
            // TMP renders non-ASCII as tofu. Comments may carry anything; string LITERALS may not.
            string[] files =
            {
                "Assets/_Modules/Village/Monetization/RewardedAdManager.cs",
                "Assets/_Modules/Village/Waves/WaveCelebrationManager.cs",
            };
            foreach (var rel in files)
            {
                var text = ReadRepoFile(rel);
                if (text == null) continue;
                int bad = 0;
                foreach (var line in text.Split('\n'))
                {
                    var t = line.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                    if (line.IndexOf('"') < 0) continue;
                    // EDITOR-ONLY attribute labels are not runtime TMP strings and may carry
                    // anything. Caught on this suite's first run: WaveCelebrationManager.cs:49
                    // is [Header("Bloom (optional - skipped if Volume/Bloom absent)")] with an
                    // em dash, which the Inspector renders perfectly and TMP never sees. Flagging
                    // it would have taught the next reader to ignore this check.
                    if (t.StartsWith("[Header(") || t.StartsWith("[Tooltip(") ||
                        t.StartsWith("[CreateAssetMenu") || t.StartsWith("[MenuItem")) continue;
                    foreach (var ch in line) if (ch > 127) { bad++; break; }
                }
                if (bad > 0)
                    failures.Add(rel + " has " + bad + " string-bearing line(s) with non-ASCII — " +
                                 "TMP renders those as tofu.");
            }
            log.AppendLine("[ascii] player-visible strings checked on " + files.Length + " file(s)");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static string ReadRepoFile(string relativePath)
        {
            try
            {
                var full = Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch { return null; }
        }
    }
}
