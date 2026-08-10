using System;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// RED test for <see cref="CompileGate"/> - proves the gate actually goes RED on
    /// a broken tree instead of merely assuming it can never run on one.
    ///
    /// WHY THIS EXISTS: the 2026-08-09 gate-integrity bug shipped because the gate
    /// had only ever been verified in the GREEN direction. Builds/chrome-compile.log
    /// carries COMPILE_GATE_OK (line 4803) alongside 54 "error CS" lines (3681+).
    /// A gate nobody has ever watched go red is not a gate.
    ///
    /// WHY IT REPLAYS A LOG INSTEAD OF BREAKING THE TREE (measured, not assumed -
    /// four batchmode runs on 2026-08-09):
    ///   * Compile errors present at EDITOR LAUNCH are ALREADY safe: Unity prints
    ///     "Scripts have compiler errors." and never runs -executeMethod at all.
    ///     Verified for a broken EDITOR assembly (gatefix-1.log) and for a broken
    ///     leaf RUNTIME assembly outside DeNelle.Editor's closure (gatefix-3-RED.log).
    ///     Neither produced a marker of any kind.
    ///   * AssetDatabase.Refresh()/ImportAsset() from inside an -executeMethod call
    ///     does NOT compile: Unity defers compilation to the main loop, so an
    ///     in-process "break it then check it" test compiles nothing and proves
    ///     nothing (gatefix-5-REDTEST.log: 0 error lines). Same reason
    ///     CompilationPipeline.RequestScriptCompilation() + wait is not viable in a
    ///     single batchmode call.
    ///   * The one genuinely dangerous ordering - compile #1 OK, domain reloads,
    ///     refresh #2 sees a mid-run edit, compile #2 FAILS, -executeMethod runs
    ///     anyway on the stale domain - is a RACE Unity does not schedule
    ///     deterministically. Injecting a break mid-run did reproduce the failing
    ///     compile #2 (gatefix-6-RED3.log) but Unity aborted before the reload on
    ///     that run, so the method still never executed.
    /// Chasing that race is not a test. So this replays the ACTUAL artifact of the
    /// real incident - Builds/chrome-compile.log - through the UNTOUCHED production
    /// code path via CompileGate.LogPathOverrideForSelfTest, and asserts the gate
    /// now refuses to print the OK marker on exactly the input that fooled it.
    ///
    /// Run it exactly like the gate:
    ///   run-unity-method.ps1 -Method DeNelle.Editor.CompileGateSelfTest.RunRedTest
    /// PASS == the log contains SELF_TEST_RED_OK (plus COMPILE_GATE_FAIL, and no
    /// COMPILE_GATE_OK from the replay).
    /// </summary>
    public static class CompileGateSelfTest
    {
        /// <summary>
        /// The real captured log from the 2026-08-09 false green. If it is ever
        /// deleted the test FAILS LOUD rather than passing vacuously - a self-test
        /// that silently skips is the same disease as a gate that silently passes.
        /// </summary>
        private const string ReplayLog = "Builds/chrome-compile.log";

        public static void RunRedTest()
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), ReplayLog);

            if (!File.Exists(full))
            {
                Debug.LogError("SELF_TEST_RED_FAIL :: replay log missing: " + full +
                               " - cannot prove the gate goes red. Restore it, or point " +
                               "ReplayLog at another captured log containing 'error CS' lines.");
                return;
            }

            bool green;
            try
            {
                Debug.Log("[GateSelfTest] replaying " + ReplayLog +
                          " (the real false-green log) through CompileGate.RunInternal()");
                CompileGate.LogPathOverrideForSelfTest = full;
                green = CompileGate.RunInternal();
            }
            catch (Exception e)
            {
                Debug.LogError("SELF_TEST_RED_FAIL :: gate threw during replay: " + e);
                return;
            }
            finally
            {
                CompileGate.LogPathOverrideForSelfTest = null;
            }

            if (green)
            {
                Debug.LogError("SELF_TEST_RED_FAIL :: the gate printed COMPILE_GATE_OK for a log " +
                               "carrying 54 compile errors. The 2026-08-09 bug is BACK.");
            }
            else
            {
                Debug.Log("SELF_TEST_RED_OK :: gate correctly WITHHELD the OK marker and reported " +
                          "COMPILE_GATE_FAIL on the replayed false-green log.");
            }
        }
    }
}
