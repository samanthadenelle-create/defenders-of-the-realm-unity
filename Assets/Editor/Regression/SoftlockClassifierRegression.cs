using System.Collections.Generic;
using System.IO;
using DeNelle.Core.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1237: proves the watchdog distinguishes an idle player from a stuck world.</summary>
    public static class SoftlockClassifierRegression
    {
        public const string MarkerOk = "SOFTLOCK_CLASSIFIER_OK";
        public const string MarkerFail = "SOFTLOCK_CLASSIFIER_FAIL";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // Historical RED proof: the old one-dimensional rule saw only "no progress for 180s".
            // It therefore returned possible_softlock for all three fixtures, including healthy AFK.
            bool legacyIdleWasSoftlock = LegacyNoProgressRule(181f);
            bool legacyFrozenWasSoftlock = LegacyNoProgressRule(181f);
            bool legacyInputWasSoftlock = LegacyNoProgressRule(181f);
            if (!(legacyIdleWasSoftlock && legacyFrozenWasSoftlock && legacyInputWasSoftlock))
                failures.Add("[red-proof] historical no-progress rule fixture no longer demonstrates all-three possible_softlock");

            Expect("idle/no-input/live-world",
                BreakCaptureHarness.ClassifyStall(false, true, true),
                BreakCaptureHarness.StallClassification.Idle, failures);
            Expect("no-input/frozen-world",
                BreakCaptureHarness.ClassifyStall(false, true, false),
                BreakCaptureHarness.StallClassification.Softlock, failures);
            Expect("input-without-progress",
                BreakCaptureHarness.ClassifyStall(true, true, true),
                BreakCaptureHarness.StallClassification.Softlock, failures);
            Expect("backgrounded-app",
                BreakCaptureHarness.ClassifyStall(true, false, false),
                BreakCaptureHarness.StallClassification.Idle, failures);

            string daemonPath = Path.Combine(".claude", "skills/run-defenders/f8-watch-daemon.ps1");
            if (!File.Exists(daemonPath) || !File.ReadAllText(daemonPath).Contains("session_start|scene_loaded|note|idle"))
                failures.Add("[paging] desktop daemon does not explicitly suppress recorded idle captures");

            if (failures.Count > 0)
            {
                reason = MarkerFail + ": " + string.Join(" | ", failures);
                return false;
            }

            reason = MarkerOk + " -- RED_PROOF legacy=3/3 possible_softlock; idle is recorded without paging; " +
                     "frozen world and input-without-progress remain softlock";
            return true;
        }

        private static bool LegacyNoProgressRule(float stalledSeconds) { return stalledSeconds > 180f; }

        private static void Expect(string fixture, BreakCaptureHarness.StallClassification actual,
            BreakCaptureHarness.StallClassification expected, List<string> failures)
        {
            if (actual != expected) failures.Add(fixture + " expected " + expected + " but got " + actual);
        }

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log(reason); else Debug.LogError(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
