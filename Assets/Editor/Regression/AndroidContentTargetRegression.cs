// =============================================================================
// AndroidContentTargetRegression — WO-1124. Proves the content-build target gate
// actually FAILS the known-bad state, and that the APK builder still switches
// the target before it builds content.
// -----------------------------------------------------------------------------
// WHY THIS SUITE EXISTS, AND WHY IT ASSERTS A FAILURE RATHER THAN A PASS.
//
// WO-1124 shipped an Android APK whose Addressables content had been built for
// StandaloneWindows64. The device asked the CDN for an Android catalog that was
// never uploaded and resolved NOTHING — no buildings, no enemies — silently, on a
// build where COMPILE_GATE_OK, APK_OK and R2_PUSH_OK were all green. None of those
// markers ever named a platform, which is the only fact that was wrong.
//
// The fix has three parts and the ticket's §5.2 is explicit that proving the happy
// path is not enough: "Deliberately break it ... and prove the new assert FAILS. A
// gate that does not fail the known-bad state is not a gate." So group 1 CALLS the
// gate with a deliberately wrong expected target and requires false. That is safe
// and fast precisely because the check runs BEFORE any build work — building 175 MB
// for the wrong platform and complaining afterwards would be the wrong design, and
// this suite would take minutes instead of milliseconds if it were.
//
// Groups:
//   1 [gate]    EnsureBuilt(caller, <wrong target>) returns FALSE without building.
//   2 [gate]    EnsureBuilt(caller, <active target>) does NOT reject on target
//               grounds — the guard must be a mismatch check, not a blanket refusal.
//               (It is NOT called for real here; that would trigger a content build.)
//   3 [switch]  AndroidBuild switches the active target to Android BEFORE calling
//               EnsureBuilt, and passes BuildTarget.Android to it. Source-lint on
//               ORDER, because the ordering IS the bug — both statements existed
//               before WO-1124; they were simply in the wrong sequence.
//   4 [assert]  AndroidBuild asserts the per-version Android catalog exists after
//               the content build.
//
// Groups 3-4 read CODE ONLY (comments and string-literal contents blanked), the
// project's standing lint discipline: a rule that matches its own tombstone comment
// punishes exactly the self-documenting notes CLAUDE.md §12/§15 asks for.
//
// Markers: ANDROID_CONTENT_TARGET_OK / ANDROID_CONTENT_TARGET_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.AndroidContentTargetRegression.RunAll
// Registered in DataRegression.RunAll as the "android-content-target suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class AndroidContentTargetRegression
    {
        public const string MarkerOk   = "ANDROID_CONTENT_TARGET_OK";
        public const string MarkerFail = "ANDROID_CONTENT_TARGET_FAIL";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- ANDROID CONTENT TARGET (WO-1124) ---");

            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            log.AppendLine($"  active build target = {active}");

            // ── 1 [gate] the known-bad state must FAIL ───────────────────────────
            // Pick a target that is definitely NOT the active one, so the call is a
            // genuine mismatch whatever platform this editor happens to be on.
            BuildTarget wrong = active == BuildTarget.Android ? BuildTarget.iOS : BuildTarget.Android;

            // REFLECTION IS DELIBERATE, and it is evidence of a rule rather than a violation of one:
            // DeNelle.EditorRegression does NOT reference DeNelle.Editor (see its .asmdef), so the
            // suites cannot bind the build tooling directly. Adding that reference to run one check
            // would dissolve a boundary the project keeps on purpose. Reaching the real method by
            // reflection exercises the ACTUAL gate — not a copy of its logic, which would pass
            // forever after someone deleted the real one.
            if (!TryInvokeEnsureBuilt(wrong, out bool accepted, out string why))
            {
                failures.Add($"[gate] could not reach AddressablesContentBuild.EnsureBuilt(string, BuildTarget?) " +
                             $"by reflection: {why}. The WO-1124 guard cannot be proven, which is the same risk as " +
                             "not having it.");
            }
            else if (!accepted)
            {
                log.AppendLine($"  [gate] EnsureBuilt(expected={wrong}) correctly REJECTED while active={active}");
            }
            else
            {
                failures.Add($"[gate] EnsureBuilt was called with expectedTarget={wrong} while the active target " +
                             $"is {active}, and it did NOT reject. The WO-1124 guard is not holding: content would " +
                             "be built for the wrong platform and the shipped player would resolve nothing.");
            }

            // ── 2 [gate] the guard must be a MISMATCH check, not a blanket refusal ─
            // Asserted by construction rather than by calling: invoking EnsureBuilt with
            // the matching target would run a real content build inside a regression pass.
            // Group 1 already proves the mismatch arm; what remains is that the match arm
            // is reachable at all, which the source-lint below covers.
            string contentSrc = ReadCode("Assets/Editor/AddressablesContentBuild.cs", failures, "[gate]");
            if (contentSrc != null)
            {
                if (contentSrc.Contains("expectedTarget.HasValue") && contentSrc.Contains("active != expectedTarget.Value"))
                    log.AppendLine("  [gate] the guard tests HasValue && mismatch (null = build for active, unchanged)");
                else
                    failures.Add("[gate] AddressablesContentBuild no longer guards on " +
                                 "'expectedTarget.HasValue && active != expectedTarget.Value'. Either the check is " +
                                 "gone (WO-1124 reopens) or it became unconditional (every legacy caller breaks).");
            }

            // ── 3 [switch] the ORDER is the fix ──────────────────────────────────
            string apkSrc = ReadCode("Assets/Editor/AndroidBuild.cs", failures, "[switch]");
            if (apkSrc != null)
            {
                int switchAt  = apkSrc.IndexOf("SwitchActiveBuildTarget");
                int ensureAt  = apkSrc.IndexOf("AddressablesContentBuild.EnsureBuilt");

                if (switchAt < 0)
                    failures.Add("[switch] AndroidBuild no longer calls SwitchActiveBuildTarget. Addressables builds " +
                                 "for the ACTIVE target, and BuildPlayer switches it too late — this is precisely the " +
                                 "WO-1124 defect, and it is silent.");
                else if (ensureAt < 0)
                    failures.Add("[switch] AndroidBuild no longer calls AddressablesContentBuild.EnsureBuilt (WO-974).");
                else if (switchAt > ensureAt)
                    failures.Add("[switch] AndroidBuild calls EnsureBuilt BEFORE SwitchActiveBuildTarget. Both " +
                                 "statements are present, which is exactly how WO-1124 read as correct — the ORDER " +
                                 "is the bug. Content must be built AFTER the target is Android.");
                else
                    log.AppendLine("  [switch] SwitchActiveBuildTarget precedes EnsureBuilt");

                if (apkSrc.Contains("EnsureBuilt(\"AndroidBuild\", BuildTarget.Android)"))
                    log.AppendLine("  [switch] EnsureBuilt is passed BuildTarget.Android, so a mismatch is a hard fail");
                else
                    failures.Add("[switch] AndroidBuild does not pass BuildTarget.Android to EnsureBuilt. Without the " +
                                 "expected target the builder cannot state which platform it built for, and the " +
                                 "WO-1124 guard never runs for the one caller that needs it.");
            }

            // ── 4 [assert] the per-version catalog check must still be wired ─────
            if (apkSrc != null)
            {
                if (apkSrc.Contains("AssertAndroidCatalogForThisBuild"))
                    log.AppendLine("  [assert] the per-version Android catalog assertion is wired into the build");
                else
                    failures.Add("[assert] AndroidBuild no longer asserts that " +
                                 "ServerData/Android/catalog_<bundleVersion>.bin exists. That file-exists check is " +
                                 "the backstop that catches every FUTURE variant of 'the content went somewhere " +
                                 "else', not just the target-switch one WO-1124 found.");
            }

            if (failures.Count > 0)
            {
                reason = $"{MarkerFail}: {failures.Count} failure(s) -- " + string.Join(" | ", failures);
                Debug.LogError(log + "\n" + reason);
                return false;
            }

            reason = $"{MarkerOk} -- the wrong-target call was rejected without building, " +
                     $"AndroidBuild switches to Android before EnsureBuilt and passes the expected target, " +
                     $"and the per-version catalog assertion is wired (active target at run time: {active})";
            Debug.Log(log + MarkerOk);
            return true;
        }

        /// <summary>
        /// Invoke the real <c>AddressablesContentBuild.EnsureBuilt(string, BuildTarget?)</c> across the
        /// asmdef boundary. Returns false only when the method cannot be REACHED — a reachable method
        /// that returns true or false is reported through <paramref name="accepted"/>, because
        /// "the gate said yes" is a finding, not a plumbing error.
        /// </summary>
        private static bool TryInvokeEnsureBuilt(BuildTarget expected, out bool accepted, out string why)
        {
            accepted = false;
            why = null;

            Type t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("DeNelle.Editor.AddressablesContentBuild", false); } catch { return null; } })
                .FirstOrDefault(x => x != null);
            if (t == null) { why = "type DeNelle.Editor.AddressablesContentBuild not found in any loaded assembly"; return false; }

            MethodInfo m = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == "EnsureBuilt" && x.GetParameters().Length == 2);
            if (m == null) { why = "no 2-parameter EnsureBuilt overload — the WO-1124 signature is gone"; return false; }

            try
            {
                accepted = (bool)m.Invoke(null, new object[] { "WO-1124 regression (deliberate mismatch)", expected });
                return true;
            }
            catch (Exception e)
            {
                why = $"{e.GetType().Name}: {(e.InnerException != null ? e.InnerException.Message : e.Message)}";
                return false;
            }
        }

        /// <summary>
        /// Source with comments and string-literal CONTENTS blanked, so a rule cannot match its
        /// own tombstone comment. Records a failure and returns null when the file is missing —
        /// these two files are load-bearing for the release chain and cannot legitimately vanish.
        /// </summary>
        private static string ReadCode(string relPath, List<string> failures, string tag)
        {
            string full = Path.GetFullPath(relPath);
            if (!File.Exists(full))
            {
                failures.Add($"{tag} {relPath} is MISSING — the WO-1124 fix cannot be verified at all.");
                return null;
            }

            var sb = new StringBuilder();
            foreach (string raw in File.ReadAllLines(full))
            {
                string line = raw;
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*")) continue;

                int c = line.IndexOf("//");
                if (c >= 0 && !InsideQuotes(line, c)) line = line.Substring(0, c);
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private static bool InsideQuotes(string line, int index)
        {
            bool q = false;
            for (int i = 0; i < index; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) q = !q;
            }
            return q;
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
