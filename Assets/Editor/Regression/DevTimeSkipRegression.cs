// =============================================================================
// DevTimeSkipRegression -- headless oracle for the DEV queue time-skip (owner ask
// 2026-08-04: "a speed timer for testing building queues ... but NOT impact the
// battle timer").
// -----------------------------------------------------------------------------
// Drives the REAL seam (DeNelle.Village.TimeSource + DeNelle.Core.Diagnostics
// .DevClock) and asserts from data:
//
//   1. EXACT ADVANCE     -- Add(N) moves NowUnixMs by exactly N (measured as the
//                          delta of the OFFSET from the raw device clock, so the
//                          device clock ticking mid-test cannot make this flaky).
//   2. ADDITIVE STACKING -- repeated adds sum; SkipMs tracks the running total.
//   3. RESET             -- clears to exactly zero and NowUnixMs returns to the
//                          pure (device + ServerOffsetMs) value.
//   4. LANE ISOLATION    -- a skip leaves ServerOffsetMs untouched, a server offset
//                          leaves SkipMs untouched, and the two compose additively.
//                          (This is WHY DevSkipMs is not folded into ServerOffsetMs:
//                          the WO-120 backend lane owns that field and a dev skip
//                          must be clearable independently.)
//   5. FORWARD-ONLY      -- a negative / non-finite delta is REFUSED, so the dev tool
//                          can never write a PAST-skewed timestamp into a save.
//   6. COMBAT FIREWALL   -- *** THE IMPORTANT ONE *** a SOURCE assertion that no
//                          combat / wave / battle-timer / raid-timer file reads
//                          TimeSource. This is the entire premise of the owner's
//                          constraint; without this pin, a future refactor that
//                          routes (say) the raid countdown through TimeSource would
//                          silently turn a QA convenience into a battle-warping
//                          cheat with nothing failing. Verified at source
//                          2026-08-04: those systems use Time.deltaTime/Time.time.
//   7. RELEASE STRIP     -- a SOURCE assertion that DevClock's mutable backing field
//                          is #if-gated and the release branch is a constant zero,
//                          so a shipped build CANNOT carry a skip.
//
// SAFETY: restores TimeSource.ServerOffsetMs and clears the dev skip in a finally,
// so the oracle leaves the process clock exactly as it found it.
// Mirrors OfflineHarvestRegression: public static bool Run(out string reason).
// =============================================================================
using System.Collections.Generic;
using System.IO;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class DevTimeSkipRegression
    {
        // Tolerance for the "exact advance" cases. The measurement subtracts the raw
        // device clock, so the only error source is the two DateTimeOffset reads
        // straddling a ms boundary -- 2ms is generous and still catches any real bug
        // (the smallest control is 60000ms).
        private const double ToleranceMs = 2.0;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            double priorServerOffset = TimeSource.ServerOffsetMs;
            try
            {
                // Clean slate: no dev skip, no server offset.
                TimeSource.ResetDevSkip();
                TimeSource.ServerOffsetMs = 0d;

                if (!DevClock.Available)
                {
                    // The oracle only runs where the dev skip is compiled in. In the
                    // editor UNITY_EDITOR is always defined, so this is a guard against
                    // a future define change, NOT an expected path. Named skip, never a
                    // false FAIL (harness-integrity rule).
                    reason = "DEV TIME-SKIP skipped: DevClock.Available is false (dev skip not compiled in this configuration)";
                    return true;
                }

                // --- Case 1: a skip advances NowUnixMs by EXACTLY the requested amount ---
                // Measure the OFFSET from the device clock, not NowUnixMs itself, so the
                // real clock advancing between the two reads cannot make this flaky.
                double offsetBefore = OffsetFromDeviceClock();
                if (System.Math.Abs(offsetBefore) > ToleranceMs)
                    failures.Add($"case1 baseline offset should be ~0 with no skip/server offset, was {offsetBefore:0.###}ms");

                const double OneMinuteMs = 60d * 1000d;
                TimeSource.AddDevSkipMs(OneMinuteMs);

                if (System.Math.Abs(TimeSource.DevSkipMs - OneMinuteMs) > 0.0001)
                    failures.Add($"case1 DevSkipMs should be exactly {OneMinuteMs} after a +1min skip, was {TimeSource.DevSkipMs}");

                double offsetAfter = OffsetFromDeviceClock();
                double advanced = offsetAfter - offsetBefore;
                if (System.Math.Abs(advanced - OneMinuteMs) > ToleranceMs)
                    failures.Add($"case1 NowUnixMs advanced {advanced:0.###}ms, expected {OneMinuteMs} (+/-{ToleranceMs})");

                // TimeSource.DevSkipMs must be the SAME value DevClock holds (one store).
                if (System.Math.Abs(TimeSource.DevSkipMs - DevClock.SkipMs) > 0.0001)
                    failures.Add($"case1 TimeSource.DevSkipMs ({TimeSource.DevSkipMs}) diverged from DevClock.SkipMs ({DevClock.SkipMs}) -- two stores, not one");

                // --- Case 2: additive stacking (repeated taps accumulate) ---
                const double TenMinutesMs = 600d * 1000d;
                const double OneHourMs = 3600d * 1000d;
                TimeSource.AddDevSkipMs(TenMinutesMs);
                TimeSource.AddDevSkipMs(OneHourMs);
                double expectedTotal = OneMinuteMs + TenMinutesMs + OneHourMs;
                if (System.Math.Abs(TimeSource.DevSkipMs - expectedTotal) > 0.0001)
                    failures.Add($"case2 stacked skip should be {expectedTotal}ms (1m+10m+1h), was {TimeSource.DevSkipMs}");

                double stackedOffset = OffsetFromDeviceClock() - offsetBefore;
                if (System.Math.Abs(stackedOffset - expectedTotal) > ToleranceMs)
                    failures.Add($"case2 NowUnixMs offset {stackedOffset:0.###}ms does not match the stacked skip {expectedTotal}ms");

                // --- Case 3: Reset returns the clock to zero skip ---
                double cleared = TimeSource.ResetDevSkip();
                if (System.Math.Abs(cleared - expectedTotal) > 0.0001)
                    failures.Add($"case3 Reset should report {expectedTotal}ms cleared, reported {cleared}");
                if (TimeSource.DevSkipMs != 0d)
                    failures.Add($"case3 DevSkipMs should be exactly 0 after Reset, was {TimeSource.DevSkipMs}");
                double offsetAfterReset = OffsetFromDeviceClock();
                if (System.Math.Abs(offsetAfterReset) > ToleranceMs)
                    failures.Add($"case3 NowUnixMs should be the raw device clock after Reset, offset was {offsetAfterReset:0.###}ms");
                // A second Reset is a harmless no-op reporting 0 cleared.
                if (TimeSource.ResetDevSkip() != 0d)
                    failures.Add("case3 a second Reset should report 0 cleared (idempotent)");

                // --- Case 4: lane isolation -- dev skip vs WO-120 ServerOffsetMs ---
                // 4a: a skip must NOT touch ServerOffsetMs.
                const double ServerOffset = 12345d;
                TimeSource.ServerOffsetMs = ServerOffset;
                TimeSource.AddDevSkipMs(OneMinuteMs);
                if (TimeSource.ServerOffsetMs != ServerOffset)
                    failures.Add($"case4a a dev skip mutated ServerOffsetMs ({TimeSource.ServerOffsetMs} != {ServerOffset}) -- the lanes are conflated");

                // 4b: they compose additively -- NowUnixMs = device + server + skip.
                double combined = OffsetFromDeviceClock();
                if (System.Math.Abs(combined - (ServerOffset + OneMinuteMs)) > ToleranceMs)
                    failures.Add($"case4b NowUnixMs offset {combined:0.###}ms should equal ServerOffsetMs+DevSkipMs ({ServerOffset + OneMinuteMs})");

                // 4c: clearing the DEV skip must leave the SERVER offset intact -- the whole
                // reason the two are separate fields (a dev skip is independently clearable).
                TimeSource.ResetDevSkip();
                if (TimeSource.ServerOffsetMs != ServerOffset)
                    failures.Add($"case4c Reset of the dev skip cleared ServerOffsetMs too ({TimeSource.ServerOffsetMs} != {ServerOffset})");
                double serverOnly = OffsetFromDeviceClock();
                if (System.Math.Abs(serverOnly - ServerOffset) > ToleranceMs)
                    failures.Add($"case4c after clearing the dev skip the offset should be the server offset alone ({ServerOffset}), was {serverOnly:0.###}ms");

                // 4d: moving the SERVER offset must not create a dev skip.
                TimeSource.ServerOffsetMs = ServerOffset * 2d;
                if (TimeSource.DevSkipMs != 0d)
                    failures.Add($"case4d setting ServerOffsetMs created a dev skip ({TimeSource.DevSkipMs})");
                TimeSource.ServerOffsetMs = 0d;

                // --- Case 5: forward-only -- negative / non-finite deltas are refused ---
                // A rewind would write PAST-skewed stamps into the save for no benefit;
                // Reset is the single, logged, self-healing way back.
                TimeSource.AddDevSkipMs(OneMinuteMs);
                double beforeBadDelta = TimeSource.DevSkipMs;
                TimeSource.AddDevSkipMs(-OneMinuteMs);
                if (TimeSource.DevSkipMs != beforeBadDelta)
                    failures.Add($"case5 a NEGATIVE delta changed the skip ({beforeBadDelta} -> {TimeSource.DevSkipMs}); the dev skip must be forward-only");
                TimeSource.AddDevSkipMs(double.NaN);
                TimeSource.AddDevSkipMs(double.PositiveInfinity);
                if (TimeSource.DevSkipMs != beforeBadDelta)
                    failures.Add($"case5 a non-finite delta changed the skip ({beforeBadDelta} -> {TimeSource.DevSkipMs})");
                if (double.IsNaN(TimeSource.NowUnixMs()) || double.IsInfinity(TimeSource.NowUnixMs()))
                    failures.Add("case5 NowUnixMs went non-finite after a NaN/Infinity delta was submitted");
                TimeSource.ResetDevSkip();

                // --- Case 6: COMBAT FIREWALL (source assertion) -------------------
                // The owner's constraint is "must not impact the battle timer". That holds
                // ONLY because combat runs on engine time and never reads this seam. Pin it.
                AssertNoTimeSourceInCombat(failures);

                // --- Case 7: RELEASE STRIP (source assertion) ---------------------
                AssertDevClockIsReleaseStripped(failures);
            }
            finally
            {
                // Leave the process clock exactly as we found it.
                TimeSource.ResetDevSkip();
                TimeSource.ServerOffsetMs = priorServerOffset;
            }

            if (failures.Count > 0)
            {
                reason = "DEV TIME-SKIP FAILED: " + string.Join(" | ", failures);
                return false;
            }
            reason = "dev time-skip OK (exact advance, additive, reset, lane-isolated from ServerOffsetMs, " +
                     "forward-only, combat reads no TimeSource, release-stripped)";
            return true;
        }

        /// <summary>
        /// How far <see cref="TimeSource.NowUnixMs"/> sits ahead of the RAW device clock.
        /// Measuring the offset (rather than NowUnixMs itself) makes the assertions immune
        /// to the real clock advancing during the test.
        /// </summary>
        private static double OffsetFromDeviceClock()
        {
            double now = TimeSource.NowUnixMs();
            double device = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now - device;
        }

        // =====================================================================
        //  Case 6 -- combat must never read the wall-clock seam
        // =====================================================================

        /// <summary>
        /// Fails if ANY combat / wave / battle-timer / raid-timer source file references
        /// <c>TimeSource</c>. These systems run on <c>Time.deltaTime</c> / <c>Time.time</c>,
        /// which is precisely why the dev queue skip cannot warp a battle. If this ever
        /// fires, the dev time-skip has become a combat cheat -- fix the refactor, do NOT
        /// relax the assertion.
        /// <para>
        /// NOTE the deliberately narrow scope. Village/Hero and Village/Troops are NOT
        /// swept wholesale: TroopTrainingPanel (barracks queue UI) and TroopRecoveryService
        /// (out-of-battle army healing between raids) legitimately read TimeSource. Only
        /// the in-battle files from those folders are named individually.
        /// </para>
        /// </summary>
        private static void AssertNoTimeSourceInCombat(List<string> failures)
        {
            // Whole trees that must be clock-free.
            string[] dirs =
            {
                "Assets/_Modules/BattleATB",          // ATBCombatManager, BattleController
                "Assets/_Modules/Village/Waves",      // WaveManager countdown + spawn pacing
                "Assets/_Modules/Village/Enemies",    // EnemyBrain attack cooldowns
                "Assets/_Modules/Dungeons",           // dungeon run timing
            };

            // Individual in-battle files from folders that DO have legitimate clock readers.
            string[] files =
            {
                "Assets/_Modules/Village/Hero/HeroHealth.cs",           // invuln / contact ticks / Last Stand
                "Assets/_Modules/Village/Troops/RaidScoring.cs",        // the raid battle timer itself
                "Assets/_Modules/Village/Troops/RaidHudController.cs",  // its on-screen countdown
                "Assets/_Modules/Village/Troops/RaidDeployController.cs",
            };

            int scanned = 0;

            for (int d = 0; d < dirs.Length; d++)
            {
                if (!Directory.Exists(dirs[d]))
                {
                    failures.Add($"case6 combat directory missing: {dirs[d]} (the firewall assertion cannot run -- did a module move?)");
                    continue;
                }
                var found = Directory.GetFiles(dirs[d], "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < found.Length; i++)
                {
                    scanned++;
                    ScanOne(found[i], failures);
                }
            }

            for (int f = 0; f < files.Length; f++)
            {
                if (!File.Exists(files[f]))
                {
                    failures.Add($"case6 combat file missing: {files[f]} (the firewall assertion cannot run -- did it move?)");
                    continue;
                }
                scanned++;
                ScanOne(files[f], failures);
            }

            // Sanity: if the scan found nothing at all, the assertion is vacuous and would
            // pass forever. Treat that as a failure of the oracle, not a pass.
            if (scanned < 20)
                failures.Add($"case6 only {scanned} combat files scanned -- the firewall assertion looks vacuous (expected the BattleATB/Waves/Enemies/Dungeons trees)");
        }

        private static void ScanOne(string path, List<string> failures)
        {
            string raw;
            try { raw = File.ReadAllText(path); }
            catch (System.Exception ex)
            {
                failures.Add($"case6 could not read {path}: {ex.Message}");
                return;
            }
            // Comments are stripped first: a combat file is allowed to MENTION TimeSource
            // in prose (e.g. "unlike TimeSource, this runs on Time.deltaTime"). Only a real
            // code reference is a breach.
            string text = StripLineComments(raw);
            if (text.IndexOf("TimeSource", System.StringComparison.Ordinal) >= 0)
            {
                failures.Add(
                    $"case6 COMBAT FIREWALL BREACHED -- '{path.Replace('\\', '/')}' now references TimeSource. " +
                    "Combat/wave/battle timing must stay on Time.deltaTime/Time.time, or the DEV queue " +
                    "time-skip (DevClock) would warp battles -- exactly what the owner ruled out. " +
                    "Move that timing back to engine time; do not relax this assertion.");
            }
        }

        // =====================================================================
        //  Case 7 -- the skip cannot ship enabled
        // =====================================================================

        /// <summary>
        /// Fails if DevClock's mutable backing field is not compile-gated, or if the
        /// release branch does not hard-zero the skip. A shipped build must behave
        /// exactly as if DevSkipMs is 0, with no storage to write and no flag to flip.
        /// </summary>
        private static void AssertDevClockIsReleaseStripped(List<string> failures)
        {
            const string Path = "Assets/_Modules/Core/Diagnostics/DevClock.cs";
            if (!File.Exists(Path))
            {
                failures.Add($"case7 {Path} not found -- the dev-skip store moved; re-point this assertion");
                return;
            }

            string raw;
            try { raw = File.ReadAllText(Path); }
            catch (System.Exception ex) { failures.Add($"case7 could not read {Path}: {ex.Message}"); return; }

            // Scan CODE, not prose. DevClock.cs's own header legitimately DISCUSSES
            // PlayerPrefs and Time.timeScale (explaining why it uses neither), so a raw
            // text scan for those tokens would false-FAIL on the very documentation that
            // proves the property holds. Strip line comments first.
            string src = StripLineComments(raw);

            if (src.IndexOf("#if UNITY_EDITOR || DEVELOPMENT_BUILD", System.StringComparison.Ordinal) < 0)
                failures.Add("case7 DevClock.cs no longer carries the '#if UNITY_EDITOR || DEVELOPMENT_BUILD' gate -- the dev skip could ship enabled");

            if (src.IndexOf("private static double _skipMs;", System.StringComparison.Ordinal) < 0)
                failures.Add("case7 DevClock.cs no longer declares the single '_skipMs' backing field -- re-verify the release strip by hand");

            if (src.IndexOf("public static double SkipMs => 0d;", System.StringComparison.Ordinal) < 0)
                failures.Add("case7 DevClock.cs release branch no longer hard-zeroes SkipMs -- a shipped build could carry a skip");

            // The dev skip must NEVER be reachable through a runtime flag (PlayerPrefs /
            // FeatureFlags), because that WOULD be flippable in a shipped build.
            if (src.IndexOf("PlayerPrefs", System.StringComparison.Ordinal) >= 0)
                failures.Add("case7 DevClock.cs references PlayerPrefs -- the dev skip must be COMPILE-gated, never runtime-flag gated (a flag ships)");

            // Time.timeScale is the thing the owner explicitly ruled out (it speeds combat).
            if (src.IndexOf("timeScale", System.StringComparison.Ordinal) >= 0)
                failures.Add("case7 DevClock.cs touches Time.timeScale -- the owner ruled that out; it would speed up combat");
        }

        /// <summary>
        /// Drops <c>//</c> line comments so a source assertion tests CODE rather than the
        /// prose that documents it. Deliberately simple (DevClock.cs has no block comments
        /// and no string literal containing "//"); it is a lint aid, not a C# parser.
        /// </summary>
        private static string StripLineComments(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            var sb = new System.Text.StringBuilder(source.Length);
            var lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int slash = line.IndexOf("//", System.StringComparison.Ordinal);
                sb.Append(slash >= 0 ? line.Substring(0, slash) : line).Append('\n');
            }
            return sb.ToString();
        }
    }
}
