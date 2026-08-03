// =============================================================================
// DynamicDifficultyRegression [dynamic-difficulty]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core).
//
// This suite is BEHAVIOURAL, not a source lint. That is only possible because the
// difficulty decision was deliberately built as pure static functions over an
// injectable history and an injectable clock (DifficultyMath / DynamicDifficultyState,
// System.Math only, no MonoBehaviour, no singleton). The reference sketch put the
// same arithmetic inside two DontDestroyOnLoad MonoBehaviours, where none of the
// cases below could be written at all -- "deterministic / AutoPilot / regression
// friendly" would have been an unbacked claim.
//
// Cases (each pins a defect that was live in an iteration of this design):
//   1  [sample-gate]      Below minSamples the multiplier is EXACTLY 1.0 (bit-exact,
//                         not "close"). The sketch had NO gate: its empty-history
//                         defaults produced a non-1.0 multiplier on encounter ONE.
//   2  [feel-table]       The owner's four rows, pinned as numbers: at-target -> 1.000
//                         exactly; the struggling threshold -> 0.75-0.85; the
//                         dominating threshold -> 1.25-1.40; early game -> 1.0.
//   3  [rails]            No input -- including absurd and degenerate ones -- ever
//                         drives the base outside [min,max] or the composed value
//                         outside [min, maxWithSpike].
//   4  [rails-reachable]  EVERY authored rail is reachable by some legal input. Two
//                         earlier iterations shipped a ceiling no input could hit
//                         (maxMultiplier under a convex blend; then a 1.8125 cap above
//                         a 1.711 reachable maximum). A cap nothing can reach is a lie
//                         in the config that reads as protection during review.
//   5  [no-nan]           A degenerate profile (collapsed ranges) and NaN/Infinity
//                         inputs never produce NaN or Infinity out. Highest-value case
//                         here: ranges come from JSON, and a NaN would flow silently
//                         into enemy max HP with no log line anywhere.
//   6  [nan-is-neutral]   A NaN/Infinity in either signal at otherwise-target
//                         conditions yields EXACTLY 1.0 -- a corrupt sample must read
//                         neutral, never biased. (An earlier fallback of 1.0 clearRatio
//                         against a 0.65 target quietly dragged an on-target player to
//                         0.921, ~8% easier, with no signal.)
//   7  [weights]          deathWeight + timeWeight == 1.0.
//   8  [determinism]      Same history in -> same multiplier out, twice.
//   9  [pressure]         Build/decay DIRECTIONS, struggling decays faster, the spike
//                         fires at the threshold, and it cannot instantly re-fire.
//  10  [spike-expires]    THE regression for the headline bug: the spike is live before
//                         its duration elapses and DEAD the instant after, because the
//                         value is composed at read time from an absolute timestamp.
//                         In the sketch the timer only controlled a log line and the
//                         spiked value stayed live until the next encounter ENDED.
//  11  [target-is-live]   Changing targetDeathRate MOVES the neutral point -- proof the
//                         target fields are load-bearing, not decorative. Three
//                         iterations of this formula never read them at all.
//  12  [dead-keys]        Every key in difficulty-profile.json binds to a real field
//                         AND that field has a consumer outside the profile file. Also
//                         asserts `scaleAggressiveTactics` is ABSENT (see the ruling in
//                         DifficultyMath's header). Dead authored keys have bitten this
//                         project four times in one week.
//  13  [dual-copy]        The Resources and StreamingAssets copies are BYTE-identical.
//
// Markers: DYNAMIC_DIFFICULTY_OK / DYNAMIC_DIFFICULTY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DynamicDifficultyRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.Adaptive;

namespace DeNelle.Editor.Regression
{
    public static class DynamicDifficultyRegression
    {
        private const string ProfileRes = "Assets/Resources/Data/Canonical/difficulty-profile.json";
        private const string ProfileSA = "Assets/StreamingAssets/Data/Canonical/difficulty-profile.json";
        private const string ProfileSrc = "Assets/_Modules/Core/Difficulty/DifficultyProfile.cs";

        /// <summary>
        /// Folders scanned for a consumer of each authored key. RUNTIME CODE ONLY, on
        /// purpose: this suite's own Clone() helper mentions every field by name, so
        /// including Assets/Editor would make the dead-key check trivially self-satisfying --
        /// a test that can only pass is worth less than no test at all.
        /// </summary>
        private static readonly string[] ConsumerRoots =
        {
            "Assets/_Modules",
        };

        /// <summary>Exact-equality epsilon for "must be 1.0".</summary>
        private const float Eps = 1e-4f;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DYNAMIC_DIFFICULTY_OK - " + reason);
            else Debug.LogError("DYNAMIC_DIFFICULTY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                var p = LoadProfileFromDisk(failures) ?? new DifficultyProfile().Validate();

                Case(failures, "sample-gate", () => Case1_SampleGate(p, failures, notes));
                Case(failures, "feel-table", () => Case2_FeelTable(p, failures, notes));
                Case(failures, "rails", () => Case3_Rails(p, failures));
                Case(failures, "rails-reachable", () => Case4_RailsReachable(p, failures, notes));
                Case(failures, "no-nan", () => Case5_NoNan(failures));
                Case(failures, "nan-is-neutral", () => Case6_NanIsNeutral(p, failures));
                Case(failures, "weights", () => Case7_Weights(p, failures));
                Case(failures, "determinism", () => Case8_Determinism(p, failures));
                Case(failures, "pressure", () => Case9_Pressure(p, failures, notes));
                Case(failures, "spike-expires", () => Case10_SpikeExpires(p, failures, notes));
                Case(failures, "target-is-live", () => Case11_TargetIsLive(p, failures, notes));
                Case(failures, "dead-keys", () => Case12_DeadKeys(failures, notes));
                Case(failures, "dual-copy", () => Case13_DualCopy(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DYNAMIC DIFFICULTY OK - the multiplier is exactly 1.0 below the sample gate and " +
                         "exactly 1.0 at the authored targets, the owner's struggling/dominating feel rows land " +
                         "in band, every authored rail is both enforced and reachable, no degenerate input " +
                         "produces NaN, the pressure spike expires on time at read time, the target keys are " +
                         "provably live, no authored key is dead, and both JSON copies are byte-identical" + noteStr;
                return true;
            }
            reason = "dynamic-difficulty FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the early-game gate holds EXACTLY 1.0
        // =====================================================================
        private static void Case1_SampleGate(DifficultyProfile p, List<string> failures, List<string> notes)
        {
            // A history that would otherwise scale hard in BOTH directions.
            for (int n = 0; n < p.MinSamples; n++)
            {
                float hi = DifficultyMath.BaseMultiplier(0f, 0.2f, n, p);
                float lo = DifficultyMath.BaseMultiplier(1f, 2.0f, n, p);
                if (hi != 1f || lo != 1f)
                    failures.Add("[sample-gate] with " + n + " samples (< minSamples " + p.MinSamples + ") the " +
                                 "multiplier is " + Fmt(hi) + " / " + Fmt(lo) + " instead of EXACTLY 1.0 - a brand " +
                                 "new player would be scaled off one unlucky (or lucky) encounter, which is the " +
                                 "owner's 'early game stays near 1.0' row broken on encounter one");
            }

            // Ramp-in, not a cliff: the first sample past the gate must NOT jump to full authority.
            float atGate = DifficultyMath.BaseMultiplier(0f, 0.2f, p.MinSamples, p);
            float atFull = DifficultyMath.BaseMultiplier(0f, 0.2f, p.SampleWindow, p);
            if (p.SampleWindow > p.MinSamples)
            {
                if (!(Math.Abs(atGate - 1f) < Math.Abs(atFull - 1f)))
                    failures.Add("[sample-gate] the first sample past the gate (" + Fmt(atGate) + ") is already as " +
                                 "far from neutral as a full window (" + Fmt(atFull) + ") - the ramp-in is a CLIFF, " +
                                 "so difficulty lurches the moment the gate opens instead of drifting");
                if (Math.Abs(DifficultyMath.Confidence(p.SampleWindow, p) - 1f) > Eps)
                    failures.Add("[sample-gate] confidence at a full window is " +
                                 Fmt(DifficultyMath.Confidence(p.SampleWindow, p)) + ", not 1.0 - the system never " +
                                 "reaches full authority, so maxMultiplier is unreachable in practice");
            }
            notes.Add("gate " + p.MinSamples + "/" + p.SampleWindow + " ramp " + Fmt(atGate) + "->" + Fmt(atFull));
        }

        // =====================================================================
        //  CASE 2 - the owner's feel table, pinned as numbers
        // =====================================================================
        private static void Case2_FeelTable(DifficultyProfile p, List<string> failures, List<string> notes)
        {
            int full = p.SampleWindow;

            // ROW: performing exactly at the authored targets -> EXACTLY 1.0.
            // This is the single most valuable assertion in the suite: three separate
            // iterations of this formula put an on-target player at 1.17, 1.20 and 1.10.
            float atTarget = DifficultyMath.BaseMultiplier(p.TargetDeathRate, p.TargetClearRatio, full, p);
            if (Math.Abs(atTarget - 1f) > Eps)
                failures.Add("[feel-table] a player performing EXACTLY at the authored targets (deathRate " +
                             Fmt(p.TargetDeathRate) + ", clearRatio " + Fmt(p.TargetClearRatio) + ") gets " +
                             Fmt(atTarget) + " instead of 1.000 - neutral has drifted off the authored target, so " +
                             "the system silently ratchets every average player up (or down) forever");

            float devAtTarget = DifficultyMath.Deviation(p.TargetDeathRate, p.TargetClearRatio, p);
            if (Math.Abs(devAtTarget) > Eps)
                failures.Add("[feel-table] deviation at the authored targets is " + Fmt(devAtTarget) +
                             ", not 0 - the shared PerformanceScore is no longer being used for BOTH the target " +
                             "and the actual, which is the mechanism that makes neutral exact");

            // ROW: struggling -> 0.75-0.85 (the owner's band), at the authored struggling thresholds.
            float struggling = DifficultyMath.BaseMultiplier(p.StrugglingDeathRate, p.StrugglingClearRatio, full, p);
            if (struggling < 0.75f || struggling > 0.85f)
                failures.Add("[feel-table] a player at the authored STRUGGLING thresholds (deathRate " +
                             Fmt(p.StrugglingDeathRate) + ", clearRatio " + Fmt(p.StrugglingClearRatio) + ") gets " +
                             Fmt(struggling) + ", outside the owner's 0.75-0.85 relief band - dying repeatedly must " +
                             "visibly ease the game or the whole system is invisible where it matters most");

            // ROW: dominating -> 1.25-1.40 (the owner's band), BEFORE any spike.
            float dominating = DifficultyMath.BaseMultiplier(p.DominatingDeathRate, p.DominatingClearRatio, full, p);
            if (dominating < 1.25f || dominating > 1.40f)
                failures.Add("[feel-table] a player at the authored DOMINATING thresholds (deathRate " +
                             Fmt(p.DominatingDeathRate) + ", clearRatio " + Fmt(p.DominatingClearRatio) + ") gets " +
                             Fmt(dominating) + ", outside the owner's 1.25-1.40 band - this is the row that was " +
                             "MATHEMATICALLY UNREACHABLE in the original sketch, where a convex blend of two [0,1] " +
                             "terms could never exceed 1.0 and maxMultiplier was dead config");

            // The boss curve must be SOFTER than trash on the way up, and no harsher on the way down.
            float bossUp = DifficultyMath.BossCurve(dominating, p);
            float bossDown = DifficultyMath.BossCurve(struggling, p);
            if (!(bossUp < dominating))
                failures.Add("[feel-table] the boss multiplier (" + Fmt(bossUp) + ") is not softer than the trash " +
                             "multiplier (" + Fmt(dominating) + ") - the owner asked for a slightly softer boss curve");
            if (bossDown > struggling + Eps)
                failures.Add("[feel-table] a struggling player gets LESS relief on bosses (" + Fmt(bossDown) +
                             ") than on trash (" + Fmt(struggling) + ") - bosses are where players actually die, so " +
                             "damping the relief there makes 'softer curve' a lie in the direction that matters most");

            notes.Add("feel: target=" + Fmt(atTarget) + " struggling=" + Fmt(struggling) +
                      " dominating=" + Fmt(dominating) + " boss(dom)=" + Fmt(bossUp));
        }

        // =====================================================================
        //  CASE 3 - the rails hold for EVERY input, including absurd ones
        // =====================================================================
        private static void Case3_Rails(DifficultyProfile p, List<string> failures)
        {
            float[] deathRates = { -5f, -1f, 0f, 0.1f, 0.22f, 0.5f, 0.99f, 1f, 5f, 1e9f,
                                   float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            float[] clearRatios = { -5f, -0.0001f, 0f, 0.01f, 0.4f, 0.65f, 1f, 1.1f, 50f, 1e9f,
                                    float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            int[] counts = { -3, 0, 1, 2, 3, 5, 10, 50, int.MaxValue };

            for (int a = 0; a < deathRates.Length; a++)
            for (int b = 0; b < clearRatios.Length; b++)
            for (int c = 0; c < counts.Length; c++)
            {
                float bas = DifficultyMath.BaseMultiplier(deathRates[a], clearRatios[b], counts[c], p);
                string ctx = "(deathRate=" + Fmt(deathRates[a]) + ", clearRatio=" + Fmt(clearRatios[b]) +
                             ", n=" + counts[c] + ")";

                if (float.IsNaN(bas) || float.IsInfinity(bas))
                { failures.Add("[rails] base multiplier is " + bas + " for " + ctx + " - a non-finite multiplier " +
                               "would flow straight into enemy max HP"); continue; }

                if (bas < p.MinMultiplier - Eps || bas > p.MaxMultiplier + Eps)
                    failures.Add("[rails] base multiplier " + Fmt(bas) + " escapes [" + Fmt(p.MinMultiplier) + ", " +
                                 Fmt(p.MaxMultiplier) + "] for " + ctx);

                float noSpike = DifficultyMath.Compose(bas, false, p);
                float spiked = DifficultyMath.Compose(bas, true, p);

                if (noSpike < p.MinMultiplier - Eps || noSpike > p.MaxMultiplier + Eps)
                    failures.Add("[rails] composed (no spike) " + Fmt(noSpike) + " escapes the base rails for " + ctx);
                if (spiked < p.MinMultiplier - Eps || spiked > p.MaxMultiplierWithSpike + Eps)
                    failures.Add("[rails] composed WITH spike " + Fmt(spiked) + " escapes [" + Fmt(p.MinMultiplier) +
                                 ", " + Fmt(p.MaxMultiplierWithSpike) + "] for " + ctx + " - the spike ceiling must " +
                                 "be an absolute authored number, not a factor multiplied onto another rail");

                // Every lever must also stay finite and railed.
                CheckLever(failures, "EnemyHp", DifficultyMath.EnemyHpMultiplier(spiked, p), p, ctx);
                CheckLever(failures, "EnemyDamage", DifficultyMath.EnemyDamageMultiplier(spiked, p), p, ctx);
                CheckLever(failures, "EnemyCount", DifficultyMath.EnemyCountMultiplier(spiked, p), p, ctx);
                CheckLever(failures, "BossHp", DifficultyMath.BossHpMultiplier(spiked, p), p, ctx);
                CheckLever(failures, "BossDamage", DifficultyMath.BossDamageMultiplier(spiked, p), p, ctx);
            }

            // The down-only damage guards must actually hold at the top of the band.
            if (p.ScaleEnemyDamage && p.EnemyDamageDownOnly &&
                DifficultyMath.EnemyDamageMultiplier(p.MaxMultiplierWithSpike, p) > 1f + Eps)
                failures.Add("[rails] enemyDamageDownOnly is set but the enemy damage lever returned " +
                             Fmt(DifficultyMath.EnemyDamageMultiplier(p.MaxMultiplierWithSpike, p)) +
                             " at the top of the band - the one lever players read as unfair is scaling upward");
            if (p.ScaleBossDamage && p.BossDamageDownOnly &&
                DifficultyMath.BossDamageMultiplier(p.MaxMultiplierWithSpike, p) > 1f + Eps)
                failures.Add("[rails] bossDamageDownOnly is set but the boss damage lever returned " +
                             Fmt(DifficultyMath.BossDamageMultiplier(p.MaxMultiplierWithSpike, p)) +
                             " at the top of the band - a boss one-shot ends a whole run");
        }

        private static void CheckLever(List<string> failures, string name, float v, DifficultyProfile p, string ctx)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                failures.Add("[rails] " + name + " lever is " + v + " for " + ctx);
            else if (v < p.MinMultiplier - Eps || v > p.MaxMultiplierWithSpike + Eps)
                failures.Add("[rails] " + name + " lever " + Fmt(v) + " escapes [" + Fmt(p.MinMultiplier) + ", " +
                             Fmt(p.MaxMultiplierWithSpike) + "] for " + ctx);
        }

        // =====================================================================
        //  CASE 4 - every authored rail is REACHABLE (the general form of a dead cap)
        // =====================================================================
        private static void Case4_RailsReachable(DifficultyProfile p, List<string> failures, List<string> notes)
        {
            float bestBase = float.MinValue, worstBase = float.MaxValue;
            float bestComposed = float.MinValue;
            int full = p.SampleWindow;

            // Sweep the legal input space. Both signals are "lower is better", so the
            // extremes of the reachable band sit at the corners.
            for (int i = 0; i <= 100; i++)
            {
                float dr = i / 100f;
                for (int j = 0; j <= 100; j++)
                {
                    float cr = j * 0.02f;   // 0 .. 2.0
                    float b = DifficultyMath.BaseMultiplier(dr, cr, full, p);
                    if (b > bestBase) bestBase = b;
                    if (b < worstBase) worstBase = b;
                    float c = DifficultyMath.Compose(b, true, p);
                    if (c > bestComposed) bestComposed = c;
                }
            }

            if (Math.Abs(bestBase - p.MaxMultiplier) > 1e-3f)
                failures.Add("[rails-reachable] maxMultiplier is authored at " + Fmt(p.MaxMultiplier) +
                             " but the best reachable base multiplier over the whole legal input space is " +
                             Fmt(bestBase) + " - an unreachable ceiling is a LIE IN THE CONFIG. Two earlier " +
                             "iterations shipped exactly this: a convex blend that could never exceed 1.0, and a " +
                             "per-side gain constant that capped the positive side at 0.722");

            if (Math.Abs(worstBase - p.MinMultiplier) > 1e-3f)
                failures.Add("[rails-reachable] minMultiplier is authored at " + Fmt(p.MinMultiplier) +
                             " but the worst reachable base multiplier is " + Fmt(worstBase) +
                             " - the relief floor can never be felt");

            if (Math.Abs(bestComposed - p.MaxMultiplierWithSpike) > 1e-3f)
                failures.Add("[rails-reachable] maxMultiplierWithSpike is authored at " +
                             Fmt(p.MaxMultiplierWithSpike) + " but the best reachable composed value is " +
                             Fmt(bestComposed) + " - the spike ceiling never BINDS, so it is decoration that reads " +
                             "as a safety rail during review (the exact defect that shipped a 1.8125 cap above a " +
                             "1.711 reachable maximum)");

            notes.Add("reachable base " + Fmt(worstBase) + ".." + Fmt(bestBase) +
                      ", composed max " + Fmt(bestComposed) +
                      " (spike cap binds above base " + Fmt(p.MaxMultiplierWithSpike / Math.Max(1e-6f, p.SpikeMultiplier)) + ")");
        }

        // =====================================================================
        //  CASE 5 - a degenerate profile can never produce NaN or Infinity
        // =====================================================================
        private static void Case5_NoNan(List<string> failures)
        {
            // Every range collapsed onto its target, weights zeroed, rails inverted - the
            // shape a tuner produces by pasting one number into the wrong row. Ranges come
            // from JSON, so this is a real risk and not a theoretical one.
            var bad = new DifficultyProfile
            {
                TargetDeathRate = 0.22f, DeathMasteryBound = 0.22f, DeathStruggleBound = 0.22f,
                TargetClearRatio = 0.65f, FastClearRatio = 0.65f, SlowClearRatio = 0.65f,
                DeathWeight = 0f, TimeWeight = 0f,
                MinMultiplier = 1.45f, MaxMultiplier = 0.75f, MaxMultiplierWithSpike = 0.1f,
                SampleWindow = 0, MinSamples = 0,
                SpikeMultiplier = 0f, SpikeThreshold = 0f, SpikeResetPressure = 99f,
                ScoreSmoothing = 5f, CountScale = -3f,
                BossExcessRetained = -1f, BossReliefRetained = 9f,
            }.Validate();

            float[] inputs = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1e30f, 0f, 1e30f, 0.22f };
            foreach (var dr in inputs)
            foreach (var cr in inputs)
            {
                float b = DifficultyMath.BaseMultiplier(dr, cr, 10, bad);
                float c = DifficultyMath.Compose(b, true, bad);
                if (float.IsNaN(b) || float.IsInfinity(b) || float.IsNaN(c) || float.IsInfinity(c))
                    failures.Add("[no-nan] a degenerate profile with deathRate=" + dr + " clearRatio=" + cr +
                                 " produced base=" + b + " composed=" + c + " - NaN propagates silently through the " +
                                 "whole multiplier chain into enemy HP with no log line anywhere");
            }

            // SafeInverseLerp's own contract: collapsed range degrades to a step, never NaN.
            float step = DifficultyMath.SafeInverseLerp(0.5f, 0.5f, 0.9f);
            if (float.IsNaN(step) || float.IsInfinity(step))
                failures.Add("[no-nan] SafeInverseLerp on a collapsed range returned " + step +
                             " instead of a defined step value");
            if (float.IsNaN(DifficultyMath.SafeInverseLerp(0f, 1f, float.NaN)))
                failures.Add("[no-nan] SafeInverseLerp propagated a NaN value instead of returning its documented " +
                             "0.5 'no information' reading");
        }

        // =====================================================================
        //  CASE 6 - a corrupt sample reads NEUTRAL, never biased
        // =====================================================================
        private static void Case6_NanIsNeutral(DifficultyProfile p, List<string> failures)
        {
            int full = p.SampleWindow;
            var cases = new[]
            {
                new { dr = float.NaN, cr = p.TargetClearRatio, what = "deathRate NaN" },
                new { dr = float.PositiveInfinity, cr = p.TargetClearRatio, what = "deathRate +Inf" },
                new { dr = p.TargetDeathRate, cr = float.NaN, what = "clearRatio NaN" },
                new { dr = p.TargetDeathRate, cr = float.PositiveInfinity, what = "clearRatio +Inf" },
            };

            foreach (var c in cases)
            {
                float m = DifficultyMath.BaseMultiplier(c.dr, c.cr, full, p);
                if (Math.Abs(m - 1f) > Eps)
                    failures.Add("[nan-is-neutral] with " + c.what + " and everything else exactly at target, the " +
                                 "multiplier is " + Fmt(m) + " instead of 1.000 - a corrupt sample must fall back to " +
                                 "its OWN AUTHORED TARGET, not to a convenient literal. Falling back to a hardcoded " +
                                 "clearRatio of 1.0 against a " + Fmt(p.TargetClearRatio) + " target silently drags " +
                                 "an on-target player ~8% easier with no signal anywhere");
            }

            // The same must hold end-to-end through a real recorded sample.
            var state = new DynamicDifficultyState(p);
            for (int i = 0; i < p.SampleWindow; i++)
                state.Record(new EncounterSample(0f, 0f, false, 0f, 0f, false), 0d);   // 0/0 expected duration
            float end = state.BaseMultiplier;
            if (float.IsNaN(end) || float.IsInfinity(end))
                failures.Add("[nan-is-neutral] a window of zero-duration/zero-expected samples produced " + end +
                             " - EncounterSample must map every degenerate duration to a neutral ratio");
        }

        // =====================================================================
        //  CASE 7 - the blend weights sum to 1.0
        // =====================================================================
        private static void Case7_Weights(DifficultyProfile p, List<string> failures)
        {
            float sum = p.DeathWeight + p.TimeWeight;
            if (Math.Abs(sum - 1f) > 1e-3f)
                failures.Add("[weights] deathWeight (" + Fmt(p.DeathWeight) + ") + timeWeight (" + Fmt(p.TimeWeight) +
                             ") = " + Fmt(sum) + ", not 1.0 - the blended score only stays in its intended range when " +
                             "the weights sum to one, and a retune that breaks the sum looks completely harmless in " +
                             "review. (The code normalises defensively; this asserts the AUTHORED intent.)");
        }

        // =====================================================================
        //  CASE 8 - determinism
        // =====================================================================
        private static void Case8_Determinism(DifficultyProfile p, List<string> failures)
        {
            var history = BuildHistory(20, i => new EncounterSample(
                30f + (i % 7) * 4f, 60f, (i % 5) == 0, 100f + i * 3f, 900f - i * 5f, (i % 4) == 0));

            float a = RunHistory(p, history, out float pressureA, out float baseA);
            float b = RunHistory(p, history, out float pressureB, out float baseB);

            if (a != b || pressureA != pressureB || baseA != baseB)
                failures.Add("[determinism] the SAME history produced different results on two runs: current " +
                             Fmt(a) + " vs " + Fmt(b) + ", base " + Fmt(baseA) + " vs " + Fmt(baseB) +
                             ", pressure " + Fmt(pressureA) + " vs " + Fmt(pressureB) +
                             " - the whole design goal is offline determinism, so any hidden RNG or ambient clock " +
                             "read makes every other case in this suite meaningless");
        }

        private static List<EncounterSample> BuildHistory(int n, Func<int, EncounterSample> gen)
        {
            var list = new List<EncounterSample>(n);
            for (int i = 0; i < n; i++) list.Add(gen(i));
            return list;
        }

        private static float RunHistory(DifficultyProfile p, List<EncounterSample> history, out float pressure, out float baseMult)
        {
            var s = new DynamicDifficultyState(p);
            double clock = 1000d;
            for (int i = 0; i < history.Count; i++) { s.Record(history[i], clock); clock += 120d; }
            pressure = s.Pressure;
            baseMult = s.BaseMultiplier;
            return s.CurrentMultiplier(clock);
        }

        // =====================================================================
        //  CASE 9 - pressure builds, decays, fires, and cannot instantly re-fire
        // =====================================================================
        private static void Case9_Pressure(DifficultyProfile p, List<string> failures, List<string> notes)
        {
            if (!p.PressureEnabled) { notes.Add("pressure DISABLED in profile - directional cases skipped"); return; }

            var dominating = new EncounterSample(0.30f * 60f, 60f, false, 80f, 1000f, false);
            var struggling = new EncounterSample(1.40f * 60f, 60f, true, 900f, 300f, false);
            var normal = new EncounterSample(0.80f * 60f, 60f, false, 400f, 700f, false);

            if (DifficultyMath.Classify(dominating, p) != EncounterVerdict.Dominating)
                failures.Add("[pressure] the synthetic dominating encounter classified as " +
                             DifficultyMath.Classify(dominating, p) + " - the thresholds and the sample no longer agree");
            if (DifficultyMath.Classify(struggling, p) != EncounterVerdict.Struggling)
                failures.Add("[pressure] the synthetic struggling encounter classified as " +
                             DifficultyMath.Classify(struggling, p));

            // The damage veto: a FAST, death-free clear where the player was nearly killed
            // must NOT read as dominating.
            var pyrrhic = new EncounterSample(0.30f * 60f, 60f, false, 950f, 1000f, false);
            if (DifficultyMath.Classify(pyrrhic, p) == EncounterVerdict.Dominating)
                failures.Add("[pressure] a fast clear in which the player took " +
                             Fmt(pyrrhic.DamageTakenRatio) + "x as much damage as they dealt still reads as " +
                             "DOMINATING - answering a desperate survival with a difficulty spike is exactly when a " +
                             "spike feels arbitrary, and it is the only consumer of the tracked damage numbers");

            // DIRECTIONS.
            float up = DifficultyMath.NextPressure(0.5f, EncounterVerdict.Dominating, p);
            float down = DifficultyMath.NextPressure(0.5f, EncounterVerdict.Normal, p);
            float fast = DifficultyMath.NextPressure(0.5f, EncounterVerdict.Struggling, p);
            if (!(up > 0.5f)) failures.Add("[pressure] a dominating encounter did not BUILD pressure (0.5 -> " + Fmt(up) + ")");
            if (!(down < 0.5f)) failures.Add("[pressure] a normal encounter did not DECAY pressure (0.5 -> " + Fmt(down) + ")");
            if (!(fast < down))
                failures.Add("[pressure] a struggling encounter (0.5 -> " + Fmt(fast) + ") does not drop pressure " +
                             "FASTER than a normal one (0.5 -> " + Fmt(down) + ") - the owner's 'struggling -> " +
                             "pressure drops faster' row");

            // FIRING + no instant re-fire.
            var s = new DynamicDifficultyState(p);
            double t = 0d;
            int firedAt = -1, fireCount = 0;
            for (int i = 1; i <= 40; i++)
            {
                if (s.Record(dominating, t)) { fireCount++; if (firedAt < 0) firedAt = i; }
                t += 1d;   // one second apart -> still INSIDE the 45 s spike window
            }

            if (firedAt < 0)
                failures.Add("[pressure] 40 consecutive dominating encounters never fired a spike - the owner's " +
                             "'dominating -> pressure builds -> spike' row is unreachable");
            else if (fireCount > 1)
                failures.Add("[pressure] the spike fired " + fireCount + " times inside a single " +
                             Fmt(p.SpikeDurationSeconds) + "s window - the soft reset to " +
                             Fmt(p.SpikeResetPressure) + " and the already-active guard must make an instant " +
                             "re-trigger structurally impossible");

            if (p.SpikeResetPressure >= p.SpikeThreshold)
                failures.Add("[pressure] spikeResetPressure (" + Fmt(p.SpikeResetPressure) + ") is not below " +
                             "spikeThreshold (" + Fmt(p.SpikeThreshold) + ") - the spike would re-arm immediately");

            // Struggling must pull the BASE down as well as bleeding pressure.
            var s2 = new DynamicDifficultyState(p);
            for (int i = 0; i < p.SampleWindow; i++) s2.Record(struggling, 0d);
            if (!(s2.BaseMultiplier < 1f))
                failures.Add("[pressure] a window of struggling encounters left the base multiplier at " +
                             Fmt(s2.BaseMultiplier) + " - the owner's 'struggling -> the base multiplier falls' row");

            notes.Add("spike fired on dominating encounter #" + firedAt + " (x" + fireCount + " in 40)");
        }

        // =====================================================================
        //  CASE 10 - THE headline regression: the spike EXPIRES on time
        // =====================================================================
        private static void Case10_SpikeExpires(DifficultyProfile p, List<string> failures, List<string> notes)
        {
            if (!p.PressureEnabled) { notes.Add("pressure DISABLED - expiry case skipped"); return; }

            var dominating = new EncounterSample(0.30f * 60f, 60f, false, 80f, 1000f, false);
            var s = new DynamicDifficultyState(p);

            double t0 = 10000d;
            double fireTime = -1d;
            for (int i = 0; i < 60 && fireTime < 0d; i++)
            {
                if (s.Record(dominating, t0)) fireTime = t0;
                t0 += 0.5d;
            }
            if (fireTime < 0d)
            {
                failures.Add("[spike-expires] no spike ever fired, so expiry cannot be tested");
                return;
            }

            double dur = p.SpikeDurationSeconds;
            double justBefore = fireTime + dur - 0.01d;
            double justAfter = fireTime + dur + 0.01d;
            double longAfter = fireTime + dur + 600d;

            if (!s.IsSpikeActive(fireTime + 0.001d))
                failures.Add("[spike-expires] the spike is not active immediately after firing");
            if (!s.IsSpikeActive(justBefore))
                failures.Add("[spike-expires] the spike expired EARLY - it was already inactive at " +
                             Fmt((float)(dur - 0.01d)) + "s of an authored " + Fmt(p.SpikeDurationSeconds) + "s window");

            if (s.IsSpikeActive(justAfter))
                failures.Add("[spike-expires] the spike is STILL ACTIVE 0.01s past its authored " +
                             Fmt(p.SpikeDurationSeconds) + "s duration - THIS IS THE HEADLINE BUG: when the spike " +
                             "value is written back into the base field and only a timer in Update() clears a flag, " +
                             "the spiked multiplier stays live until the NEXT ENCOUNTER ENDS, which can be many " +
                             "minutes later. The value must be composed at READ time from an absolute timestamp");

            float during = s.CurrentMultiplier(justBefore);
            float after = s.CurrentMultiplier(justAfter);
            float wayAfter = s.CurrentMultiplier(longAfter);
            float baseM = s.BaseMultiplier;

            if (!(during > after + Eps))
                failures.Add("[spike-expires] the composed multiplier did not DROP when the spike expired (" +
                             Fmt(during) + " -> " + Fmt(after) + ") - expiry is not being felt at read time");
            if (Math.Abs(after - DifficultyMath.Compose(baseM, false, p)) > Eps)
                failures.Add("[spike-expires] after expiry the multiplier is " + Fmt(after) + " but the un-spiked " +
                             "base composes to " + Fmt(DifficultyMath.Compose(baseM, false, p)) +
                             " - a derived value has been stored into the field the base lives in");
            if (after != wayAfter)
                failures.Add("[spike-expires] the multiplier kept changing long after expiry (" + Fmt(after) +
                             " -> " + Fmt(wayAfter) + ") with no new encounters recorded");

            if (during > p.MaxMultiplierWithSpike + Eps)
                failures.Add("[spike-expires] the live spiked value " + Fmt(during) + " exceeds the authored " +
                             "composed ceiling " + Fmt(p.MaxMultiplierWithSpike));

            notes.Add("spike " + Fmt(during) + " -> " + Fmt(after) + " across the " +
                      Fmt(p.SpikeDurationSeconds) + "s boundary");
        }

        // =====================================================================
        //  CASE 11 - the target fields are LIVE, not decorative
        // =====================================================================
        private static void Case11_TargetIsLive(DifficultyProfile p, List<string> failures, List<string> notes)
        {
            int full = p.SampleWindow;

            // Retune ONLY targetDeathRate. The neutral point must move with it: the death
            // rate that used to score 1.0 must no longer score 1.0, and the NEW target must.
            var moved = Clone(p);
            moved.TargetDeathRate = p.TargetDeathRate + 0.10f;
            moved.Validate();

            float oldTargetUnderNewProfile = DifficultyMath.BaseMultiplier(p.TargetDeathRate, p.TargetClearRatio, full, moved);
            float newTargetUnderNewProfile = DifficultyMath.BaseMultiplier(moved.TargetDeathRate, moved.TargetClearRatio, full, moved);

            if (Math.Abs(newTargetUnderNewProfile - 1f) > Eps)
                failures.Add("[target-is-live] after retuning targetDeathRate to " + Fmt(moved.TargetDeathRate) +
                             ", a player performing at the NEW target gets " + Fmt(newTargetUnderNewProfile) +
                             " instead of 1.000 - neutral did not follow the authored target");

            if (Math.Abs(oldTargetUnderNewProfile - 1f) <= Eps)
                failures.Add("[target-is-live] retuning targetDeathRate from " + Fmt(p.TargetDeathRate) + " to " +
                             Fmt(moved.TargetDeathRate) + " did NOT move the neutral point (the old target still " +
                             "scores 1.000) - targetDeathRate is a DEAD AUTHORED KEY, which is exactly the failure " +
                             "class that has bitten this project four times in one week");

            // Same for the clear-ratio target.
            var moved2 = Clone(p);
            moved2.TargetClearRatio = p.TargetClearRatio - 0.10f;
            moved2.Validate();
            float oldClearUnderNew = DifficultyMath.BaseMultiplier(p.TargetDeathRate, p.TargetClearRatio, full, moved2);
            if (Math.Abs(oldClearUnderNew - 1f) <= Eps)
                failures.Add("[target-is-live] retuning targetClearRatio did NOT move the neutral point - " +
                             "targetClearRatio is a dead authored key");

            notes.Add("target+0.10 moves neutral: old target now " + Fmt(oldTargetUnderNewProfile));
        }

        private static DifficultyProfile Clone(DifficultyProfile p)
        {
            return new DifficultyProfile
            {
                Version = p.Version,
                SampleWindow = p.SampleWindow, MinSamples = p.MinSamples,
                TargetDeathRate = p.TargetDeathRate, DeathMasteryBound = p.DeathMasteryBound,
                DeathStruggleBound = p.DeathStruggleBound,
                TargetClearRatio = p.TargetClearRatio, FastClearRatio = p.FastClearRatio,
                SlowClearRatio = p.SlowClearRatio,
                DeathWeight = p.DeathWeight, TimeWeight = p.TimeWeight, ScoreSmoothing = p.ScoreSmoothing,
                MinMultiplier = p.MinMultiplier, MaxMultiplier = p.MaxMultiplier,
                MaxMultiplierWithSpike = p.MaxMultiplierWithSpike,
                BossExcessRetained = p.BossExcessRetained, BossReliefRetained = p.BossReliefRetained,
                ScaleEnemyHp = p.ScaleEnemyHp, ScaleEnemyDamage = p.ScaleEnemyDamage,
                EnemyDamageDownOnly = p.EnemyDamageDownOnly, ScaleEnemyCount = p.ScaleEnemyCount,
                CountScale = p.CountScale, ScaleBossHp = p.ScaleBossHp, ScaleBossDamage = p.ScaleBossDamage,
                BossDamageDownOnly = p.BossDamageDownOnly,
                PressureEnabled = p.PressureEnabled, PressureBuildRate = p.PressureBuildRate,
                PressureDecayRate = p.PressureDecayRate, StrugglingDecayFactor = p.StrugglingDecayFactor,
                SpikeThreshold = p.SpikeThreshold, SpikeMultiplier = p.SpikeMultiplier,
                SpikeDurationSeconds = p.SpikeDurationSeconds, SpikeResetPressure = p.SpikeResetPressure,
                DominatingDeathRate = p.DominatingDeathRate, DominatingClearRatio = p.DominatingClearRatio,
                DominatingMaxDamageTakenRatio = p.DominatingMaxDamageTakenRatio,
                StrugglingDeathRate = p.StrugglingDeathRate, StrugglingClearRatio = p.StrugglingClearRatio,
            };
        }

        // =====================================================================
        //  CASE 12 - no authored key without a field AND a consumer
        // =====================================================================
        private static void Case12_DeadKeys(List<string> failures, List<string> notes)
        {
            if (!File.Exists(ProfileRes)) { failures.Add("[dead-keys] " + ProfileRes + " not found"); return; }
            if (!File.Exists(ProfileSrc)) { failures.Add("[dead-keys] " + ProfileSrc + " not found"); return; }

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(ProfileRes)); }
            catch (Exception ex)
            {
                failures.Add("[dead-keys] difficulty-profile.json failed to parse (" + ex.GetType().Name + ": " + ex.Message + ")");
                return;
            }

            string src = File.ReadAllText(ProfileSrc);

            // jsonKey -> C# field name, straight from the [JsonProperty("...")] attributes.
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(src, @"\[JsonProperty\(""(?<key>[^""]+)""\)\]\s*public\s+\w+\s+(?<field>\w+)"))
                bindings[m.Groups["key"].Value] = m.Groups["field"].Value;

            // The whole consumer corpus, read once.
            var corpus = new List<string>();
            foreach (var rootDir in ConsumerRoots)
            {
                if (!Directory.Exists(rootDir)) continue;
                foreach (var f in Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories))
                {
                    if (Path.GetFullPath(f).Equals(Path.GetFullPath(ProfileSrc), StringComparison.OrdinalIgnoreCase)) continue;
                    try { corpus.Add(File.ReadAllText(f)); } catch { /* unreadable file is not this suite's business */ }
                }
            }

            int checkedKeys = 0;
            foreach (var prop in root.Properties())
            {
                checkedKeys++;
                string key = prop.Name;

                if (!bindings.TryGetValue(key, out string field))
                {
                    failures.Add("[dead-keys] difficulty-profile.json authors '" + key + "' but no field in " +
                                 "DifficultyProfile.cs carries [JsonProperty(\"" + key + "\")] - the value is parsed " +
                                 "into nothing and every tune of it is silently discarded");
                    continue;
                }

                bool consumed = false;
                var probe = new Regex(@"\." + Regex.Escape(field) + @"\b");
                for (int i = 0; i < corpus.Count && !consumed; i++)
                    if (probe.IsMatch(corpus[i])) consumed = true;

                if (!consumed)
                    failures.Add("[dead-keys] '" + key + "' binds to DifficultyProfile." + field + " but NOTHING " +
                                 "outside DifficultyProfile.cs ever reads it - a dead authored key. Either give it a " +
                                 "real consumer or delete it; this exact class of defect has landed four times in " +
                                 "one week (Cathedral mage keys, canHitAir, centralBuilding, elementBias)");
            }

            // The deliberately-absent key. scaleAggressiveTactics can only be implemented by
            // writing to EnemyBrain's STATIC SHARED TacticalData archetypes, which would
            // corrupt every enemy for the whole session - so it is not authored at all.
            if (root["scaleAggressiveTactics"] != null)
                failures.Add("[dead-keys] 'scaleAggressiveTactics' has been re-added to difficulty-profile.json. It " +
                             "was deliberately NOT authored: the only surface it could drive is EnemyBrain's static " +
                             "shared TacticalData archetypes (EnemyBrain.KiterTactics et al.), so a per-difficulty " +
                             "write would permanently corrupt every enemy for the session. Implementing it safely " +
                             "needs per-instance TacticalData copies first (a combat-lane refactor). Until then it " +
                             "is an unimplementable key, and an unimplementable key is a dead key");
            // Match a DECLARATION, not a mention. A raw IndexOf over the whole file also matches the
            // header comment that DOCUMENTS the ruling ("DELIBERATELY ABSENT: scaleAggressiveTactics"),
            // so the suite failed on the very comment recording that the key was correctly left out.
            // The check must therefore look for a real field or [JsonProperty] binding.
            if (Regex.IsMatch(src, @"\[JsonProperty\(""scaleAggressiveTactics""\)\]")
                || Regex.IsMatch(src, @"public\s+bool\s+scaleAggressiveTactics\b"))
                failures.Add("[dead-keys] DifficultyProfile.cs declares a scaleAggressiveTactics field - see above");

            notes.Add(checkedKeys + " authored keys, all bound + consumed");
        }

        // =====================================================================
        //  CASE 13 - dual-copy byte identity
        // =====================================================================
        private static void Case13_DualCopy(List<string> failures)
        {
            if (!File.Exists(ProfileRes)) { failures.Add("[dual-copy] missing " + ProfileRes); return; }
            if (!File.Exists(ProfileSA))
            {
                failures.Add("[dual-copy] missing " + ProfileSA + " - the Resources copy is what a shipped/WebGL " +
                             "player loads, but StreamingAssets is the desktop fallback and the editable source; a " +
                             "missing half is how the two silently diverge");
                return;
            }

            byte[] a = File.ReadAllBytes(ProfileRes);
            byte[] b = File.ReadAllBytes(ProfileSA);

            if (a.Length != b.Length)
            {
                failures.Add("[dual-copy] the two difficulty-profile.json copies differ in length (" + a.Length +
                             " vs " + b.Length + " bytes) - the device and the editor would tune differently");
                return;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i]) continue;
                failures.Add("[dual-copy] the two difficulty-profile.json copies differ at byte " + i +
                             " (0x" + a[i].ToString("X2") + " vs 0x" + b[i].ToString("X2") + ")");
                break;
            }

            // A BOM here has bitten this project before (a PowerShell redirect added one).
            if (a.Length >= 3 && a[0] == 0xEF && a[1] == 0xBB && a[2] == 0xBF)
                failures.Add("[dual-copy] the Resources copy starts with a UTF-8 BOM - write these files with the " +
                             "Edit/Write tool, never a PowerShell redirect");
            foreach (var by in a)
                if (by > 127) { failures.Add("[dual-copy] difficulty-profile.json contains non-ASCII bytes"); break; }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>Reads the profile straight off disk so the suite tests the SHIPPED tuning,
        /// not whatever a previous test left in the catalog cache.</summary>
        private static DifficultyProfile LoadProfileFromDisk(List<string> failures)
        {
            if (!File.Exists(ProfileRes))
            {
                failures.Add("[profile] " + ProfileRes + " not found - the whole suite would silently test the " +
                             "built-in defaults instead of the authored tuning");
                return null;
            }
            try
            {
                var p = Newtonsoft.Json.JsonConvert.DeserializeObject<DifficultyProfile>(File.ReadAllText(ProfileRes));
                if (p == null) { failures.Add("[profile] difficulty-profile.json deserialized to null"); return null; }
                return p.Validate();
            }
            catch (Exception ex)
            {
                failures.Add("[profile] difficulty-profile.json failed to parse (" + ex.GetType().Name + ": " + ex.Message + ")");
                return null;
            }
        }

        private static string Fmt(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
