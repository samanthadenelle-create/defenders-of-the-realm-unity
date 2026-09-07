using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless compile gate. CLI uses the <c>COMPILE_GATE_OK</c> marker as the
    /// authoritative "does the tree compile" check before committing.
    ///
    /// WHY THIS FILE WAS REWRITTEN (gate-integrity bug, 2026-08-09):
    /// the old gate ASSUMED "if scripts didn't compile, this method never executes"
    /// and printed the OK marker unconditionally. That assumption is FALSE, and we
    /// have the captured proof: <c>Builds/chrome-compile.log</c> carries
    /// <c>COMPILE_GATE_OK</c> at line 4803 AND 54 <c>error CS</c> lines at 3681+.
    /// The mechanism, read off that log rather than guessed:
    ///   line  261  first script compilation requested -> SUCCEEDED
    ///   line 2428  domain reloaded with the good assemblies
    ///   line 2547  SECOND compilation requested (a .cs was saved mid-run) -> FAILED
    ///               (##### ExitCode 1, DeNelle.Village, 54 CS errors)
    ///   line 4803  -executeMethod ran anyway, off the STALE loaded domain, and the
    ///               old Run() printed the OK marker
    ///   line 4814  "Scripts have compiler errors."  (return code 1)
    /// i.e. Unity honours -executeMethod against the previously-loaded assemblies
    /// when a LATER compile of a RUNTIME asmdef fails. The marker described the
    /// stale DLLs, not the tree on disk. This is the
    /// "gates-report-success-without-proving-it" failure class, and it is severe
    /// precisely because every seat trusts the marker BECAUSE exit codes lie.
    ///
    /// THE FIX: <see cref="Run"/> now PROVES cleanliness instead of assuming it.
    /// The primary proof is a scan of the editor log this session is writing — the
    /// C# compiler's own error output, which the log ordering above shows is
    /// already on disk by the time Run() executes. Corroborated by
    /// EditorUtility.scriptCompilationFailed (reflected, may not exist) and by an
    /// assembly-output existence check via CompilationPipeline.
    ///
    /// DESIGN RULE, NON-NEGOTIABLE: a check that CANNOT DETERMINE the answer FAILS
    /// LOUD. It never passes silently. An "I couldn't tell" that prints green is
    /// the exact bug this file exists to kill.
    ///
    /// Also retained (WO-434): the NUL-byte scan and the brace-balance scan. The
    /// Linux-mount <-> Windows desync (CLAUDE.md §0) can leave a .cs NUL-padded —
    /// Windows + HEAD look byte-clean but the mount-written copy carries embedded
    /// \x00 bytes that poison a commit and break compilation.
    /// </summary>
    public static class CompileGate
    {
        /// <summary>
        /// EXACT marker other scripts grep for (run-tests.ps1,
        /// tools/regression/checkin_gate.ps1). Do not change this string.
        /// </summary>
        private const string OkMarker = "COMPILE_GATE_OK :: scripts compiled clean";

        /// <summary>
        /// Failure marker. Deliberately does NOT contain the substring
        /// "COMPILE_GATE_OK", so a `Select-String -Pattern 'COMPILE_GATE_OK'`
        /// consumer cannot mistake a failure for a pass.
        /// </summary>
        private const string FailMarker = "COMPILE_GATE_FAIL";

        // ---------------------------------------------------------------------
        // WO-1575 — the WebGL player-script pass. THREE distinct markers so a log
        // reader can tell "the pass ran and passed" from "the pass ran and failed"
        // from "the pass never ran", and so marker ABSENCE on a fresh log is itself
        // a failure (CLAUDE.md §16 - judge by the marker, never the exit code).
        //
        // ⛔ SUBSTRING DISCIPLINE, deliberate: none of the three contains the
        // substring "COMPILE_GATE_OK", and neither the FAIL nor the SKIPPED literal
        // contains "COMPILE_GATE_WEBGL_OK". So an existing
        // `Select-String -Pattern 'COMPILE_GATE_OK'` consumer cannot be fooled by
        // this stage, and a new `COMPILE_GATE_WEBGL_OK` consumer cannot be fooled
        // by its own failure line. Same rule the FailMarker above was written to.
        // ---------------------------------------------------------------------
        private const string WebGlOkMarker = "COMPILE_GATE_WEBGL_OK";
        private const string WebGlFailMarker = "COMPILE_GATE_WEBGL_FAIL";
        private const string WebGlSkippedMarker = "COMPILE_GATE_WEBGL_SKIPPED";

        /// <summary>
        /// Scratch output folder for the WebGL player-script compile. Under Temp/,
        /// which Unity owns and .gitignore excludes, and which the gate's own NUL /
        /// brace scans already skip (they filter "/Temp/").
        /// </summary>
        private const string WebGlScratchFolder = "Temp/CompileGateWebGL";

        /// <summary>Cap on how many offenders we name per check; the rest are counted.</summary>
        private const int MaxNamed = 12;

        /// <summary>
        /// TEST HOOK - internal, so only DeNelle.Editor can set it, and null in every
        /// production path. When set, the gate reads THIS log instead of discovering
        /// the live session log, which lets <see cref="CompileGateSelfTest"/> replay a
        /// real captured log (Builds/chrome-compile.log, the actual 2026-08-09
        /// false-green run) through the untouched production code path and prove the
        /// gate now goes RED on it. Always restored to null by the self-test.
        /// </summary>
        internal static string LogPathOverrideForSelfTest;

        public static void Run()
        {
            RunInternal();
        }

        /// <summary>
        /// The gate proper. Returns TRUE only when the tree is positively proven
        /// clean (i.e. the OK marker was printed). <see cref="Run"/> is the void
        /// entry point Unity's -executeMethod calls; this overload exists so the
        /// self-test can assert on the verdict instead of scraping the log.
        /// </summary>
        internal static bool RunInternal()
        {
            var failures = new List<string>();

            // ---- CHECK 1: did the C# compiler actually succeed? -----------------
            // Runs FIRST and reads the log BEFORE this method prints anything, so
            // our own diagnostic output can never poison our own scan.
            failures.AddRange(ProveScriptsCompiled());
            // Recorded HERE, not at the verdict: only an EDITOR-compile failure makes
            // the WebGL pass pointless (the same broken tree, a second compiler, the
            // same errors twice). A NUL/brace failure from checks 2-3 must NOT
            // suppress it. This is also what keeps CompileGateSelfTest cheap: it
            // replays a 54-error log through RunInternal, and that replay now stands
            // the WebGL pass down instead of firing a real player compile.
            bool editorCompileAlreadyFailed = failures.Count > 0;

            // ---- CHECK 2: mount-garble NUL bytes (WO-434) -----------------------
            List<string> nulOffenders = ScanForNulBytes();
            if (nulOffenders.Count > 0)
            {
                List<string> nulShown = Cap(nulOffenders, out int nulMore);
                foreach (string path in nulShown)
                {
                    Debug.LogError(
                        "[CompileGate] NUL-BYTE CORRUPTION in " + path +
                        " - mount-garbled, do not commit (see CLAUDE.md §0)");
                }
                if (nulMore > 0)
                    Debug.LogError("[CompileGate] ... and " + nulMore + " more NUL-corrupt file(s)");
                failures.Add(nulOffenders.Count + " .cs file(s) carry NUL bytes");
            }

            // ---- CHECK 3: brace balance ----------------------------------------
            List<string> braceOffenders = ScanBraceBalance();
            if (braceOffenders.Count > 0)
            {
                List<string> braceShown = Cap(braceOffenders, out int braceMore);
                foreach (string path in braceShown)
                    Debug.LogError("[CompileGate] BRACE MISMATCH in " + path);
                if (braceMore > 0)
                    Debug.LogError("[CompileGate] ... and " + braceMore + " more brace-mismatched file(s)");
                failures.Add(braceOffenders.Count + " .cs file(s) have mismatched braces");
            }

            // ---- CHECK 4: WebGL player-script compile (WO-1575) -----------------
            // MUST run AFTER check 1: this pass writes its own `error CS` lines into
            // the live editor log, and check 1's contract is "read the log before
            // this method prints anything". Running it earlier would let our own
            // WebGL diagnostics poison the editor-compile scan.
            failures.AddRange(CompileForWebGl(editorCompileAlreadyFailed));

            // ---- verdict --------------------------------------------------------
            if (failures.Count > 0)
            {
                foreach (string reason in failures)
                    Debug.LogError("[CompileGate] REASON: " + reason);
                // OK marker withheld -> run-unity-method / checkin_gate see a failed gate.
                Debug.LogError(FailMarker + " :: " + failures.Count +
                               " check(s) failed - see [CompileGate] lines above. OK marker withheld.");
                return false;
            }

            Debug.Log(OkMarker);
            return true;
        }

        // =====================================================================
        // CHECK 1 - compile proof
        // =====================================================================

        /// <summary>
        /// Returns a list of failure reasons proving the scripts did NOT compile
        /// clean. Empty list == positively proven clean. Never returns empty on an
        /// inconclusive result: if no evidence source can be consulted, that is
        /// itself a failure reason (see the DESIGN RULE in the class doc).
        /// </summary>
        private static List<string> ProveScriptsCompiled()
        {
            var failures = new List<string>();
            bool haveAuthoritativeEvidence = false;

            // --- 1a. the editor log: the C# compiler's own output ---------------
            // This is the ground truth that the 2026-08-09 false-green proved was
            // sitting right there unread (errors at log line 3681, marker at 4803).
            string logPath = FindEditorLogPath();
            if (!string.IsNullOrEmpty(logPath))
            {
                if (TryScanLogForCompileErrors(logPath, out var byFile, out int totalErrors,
                                               out var samples, out bool unityVerdict, out string readError))
                {
                    haveAuthoritativeEvidence = true;

                    if (totalErrors > 0 || unityVerdict)
                    {
                        Debug.LogError("[CompileGate] COMPILE ERRORS found in the editor log for THIS session:");
                        Debug.LogError("[CompileGate]   log = " + logPath);

                        var names = new List<string>();
                        foreach (var kv in byFile)
                            names.Add(kv.Key + "  (" + kv.Value + " error" + (kv.Value == 1 ? "" : "s") + ")");
                        names.Sort(StringComparer.OrdinalIgnoreCase);

                        List<string> namesShown = Cap(names, out int more);
                        foreach (string n in namesShown)
                            Debug.LogError("[CompileGate]   FILE: " + n);
                        if (more > 0)
                            Debug.LogError("[CompileGate]   ... and " + more + " more file(s) with errors");

                        foreach (string s in samples)
                            Debug.LogError("[CompileGate]   > " + s);

                        if (unityVerdict)
                            Debug.LogError("[CompileGate]   Unity's own verdict line present: \"Scripts have compiler errors.\"");

                        failures.Add(totalErrors + " C# compile error(s) across " + byFile.Count +
                                     " file(s) - the loaded assemblies do NOT match the tree on disk");
                    }
                }
                else
                {
                    // Could not read a log we DID locate: inconclusive -> fail loud.
                    Debug.LogError("[CompileGate] could not read the editor log '" + logPath +
                                   "': " + readError);
                }
            }
            else
            {
                Debug.LogWarning("[CompileGate] no editor log path could be resolved " +
                                 "(no -logFile argument and no Application.consoleLogPath).");
            }

            // --- 1b. EditorUtility.scriptCompilationFailed (reflected) ----------
            // Public surface for this has moved around across Unity versions, so we
            // reflect rather than bind. Present == authoritative; absent == simply
            // no evidence from this source (it does not, on its own, pass anything).
            if (TryGetScriptCompilationFailed(out bool compilationFailed, out string reflectNote))
            {
                haveAuthoritativeEvidence = true;
                if (compilationFailed)
                {
                    Debug.LogError("[CompileGate] EditorUtility.scriptCompilationFailed == true " +
                                   "(Unity reports the script compilation for this domain FAILED).");
                    failures.Add("EditorUtility.scriptCompilationFailed == true");
                }
            }
            else
            {
                Debug.Log("[CompileGate] note: EditorUtility.scriptCompilationFailed unavailable (" +
                          reflectNote + ") - relying on the log scan + assembly outputs.");
            }

            // --- 1c. every compiled assembly must have produced an output DLL ---
            // Catches "this assembly never built at all". Deliberately checks
            // EXISTENCE, not mtime: an mtime comparison false-fails constantly in
            // this repo because git checkout / stash rewrites .cs timestamps while
            // Unity's content hashing correctly skips the recompile.
            try
            {
                var missing = new List<string>();
                // Fully qualified: `Assembly` alone is ambiguous between
                // System.Reflection.Assembly and UnityEditor.Compilation.Assembly.
                foreach (UnityEditor.Compilation.Assembly asm in
                         CompilationPipeline.GetAssemblies(AssembliesType.Editor))
                {
                    if (string.IsNullOrEmpty(asm.outputPath)) continue;
                    string outPath = asm.outputPath;
                    if (!Path.IsPathRooted(outPath))
                        outPath = Path.Combine(Directory.GetCurrentDirectory(), outPath);
                    if (!File.Exists(outPath))
                        missing.Add(asm.name + " -> " + asm.outputPath);
                }

                haveAuthoritativeEvidence = true;

                if (missing.Count > 0)
                {
                    List<string> missingShown = Cap(missing, out int missingMore);
                    foreach (string m in missingShown)
                        Debug.LogError("[CompileGate] MISSING ASSEMBLY OUTPUT: " + m);
                    if (missingMore > 0)
                        Debug.LogError("[CompileGate] ... and " + missingMore + " more assembly output(s) missing");
                    failures.Add(missing.Count + " assembly output DLL(s) missing - those assemblies never built");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CompileGate] CompilationPipeline.GetAssemblies threw: " + e.Message);
            }

            // --- inconclusive == FAIL. Never pass silently. ----------------------
            if (!haveAuthoritativeEvidence)
            {
                failures.Add("INCONCLUSIVE - no compile-status evidence source could be consulted " +
                             "(log unreadable, scriptCompilationFailed absent, CompilationPipeline threw). " +
                             "A gate that cannot prove green must report red.");
            }

            return failures;
        }

        // =====================================================================
        // CHECK 4 - WebGL player-script compile (WO-1575)
        // =====================================================================

        /// <summary>
        /// Compiles the PLAYER assemblies for <see cref="BuildTarget.WebGL"/> without
        /// switching the active build target, so code behind <c>#if UNITY_WEBGL</c>
        /// is handed to a compiler by the ordinary commit gate instead of rotting
        /// until an Addressables WebGL content build trips over it.
        ///
        /// WHY THIS EXISTS (WO-1575, measured — not inferred):
        /// <c>Builds/webgl-build.log</c> failed the WebGL content build with exactly
        /// one error, <c>Assets\_Modules\Core\Diagnostics\WebTrace.cs(325,35): error
        /// CS1501: No overload for method 'Warn' takes 3 arguments</c>, while the
        /// desktop compile gate on this machine read green the whole time. The gate
        /// only ever saw the ACTIVE build target's define set, so a platform-guarded
        /// block was never compiled by anything a seat runs before committing.
        ///
        /// WHY THIS API. <c>PlayerBuildInterface.CompilePlayerScripts</c> is the
        /// EXACT call the failing build makes: Scriptable Build Pipeline's
        /// <c>BuildPlayerScripts</c> task invokes it at
        /// <c>Library/PackageCache/com.unity.scriptablebuildpipeline@36e3b5898ee2/
        /// Editor/Tasks/BuildPlayerScripts.cs:41</c>, with settings built by
        /// <c>BuildParameters.GetScriptCompilationSettings()</c>
        /// (<c>Editor/Shared/BuildParameters.cs:140-147</c> — the fields are exactly
        /// <c>group</c> / <c>target</c> / <c>options</c>). Same call, same shape, so
        /// this gate fails on the same input the content build fails on. SBP's own
        /// verdict rule is on the next line of that task
        /// (<c>assemblies.IsNullOrEmpty() &amp;&amp; typeDB == null -> ReturnCode.Error</c>);
        /// this method is deliberately STRICTER, because SBP is allowed to be
        /// inconclusive and a gate is not (see the DESIGN RULE in the class doc).
        ///
        /// EVIDENCE, THREE SOURCES — a compile that cannot be judged FAILS:
        ///   (a) <c>CompilationPipeline.assemblyCompilationFinished</c> messages of
        ///       type Error, collected across the call. It is NOT proven on this
        ///       machine that this event fires for PLAYER compiles in 6000.4, which
        ///       is precisely why it is not the only source.
        ///   (b) the editor log TAIL — the byte range this call appended — rescanned
        ///       with the same <see cref="CsErrorRx"/> check 1 uses. This is what
        ///       preserves the <c>file(line,col): error CSxxxx</c> shape verbatim.
        ///   (c) <c>result.assemblies</c> — empty means nothing was produced.
        /// Errors from (a) or (b) -> FAIL. No errors and (c) non-empty -> OK. No
        /// errors and (c) empty -> FAIL as INCONCLUSIVE.
        ///
        /// SKIPPED IS NOT A FAILURE, AND THAT IS A POLICY, NOT AN OVERSIGHT.
        /// A machine with no WebGL module installed still gets a working commit gate;
        /// blocking the Android ship lane over an absent module would be a refusal
        /// unrelated to the tree. The visibility is the named
        /// <c>COMPILE_GATE_WEBGL_SKIPPED reason=</c> line PLUS the absence of
        /// <c>COMPILE_GATE_WEBGL_OK</c> on a fresh log. Reversible by the owner in
        /// one line (move the skip reasons into <c>failures</c>).
        /// </summary>
        /// <param name="editorCompileAlreadyFailed">
        /// True when check 1 already proved the tree does not compile for the ACTIVE
        /// target. Running a second compiler over the same broken tree only reprints
        /// the same errors, so the pass stands down with a named reason.
        /// </param>
        private static List<string> CompileForWebGl(bool editorCompileAlreadyFailed)
        {
            var failures = new List<string>();

            if (editorCompileAlreadyFailed)
            {
                Debug.LogWarning(WebGlSkippedMarker + " reason=editor-compile-already-failed :: " +
                                 "the tree does not compile for the active target, so a WebGL " +
                                 "player compile would only reprint the same errors. Fix check 1 " +
                                 "and re-run to exercise the WebGL pass.");
                return failures;
            }

            // ---- module guard: a named refusal, never a silent pass --------------
            bool supported;
            try
            {
                supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }
            catch (Exception e)
            {
                Debug.LogError(WebGlFailMarker + " :: BuildPipeline.IsBuildTargetSupported threw " +
                               e.GetType().Name + ": " + e.Message +
                               " - cannot determine whether the WebGL module is present, so this " +
                               "check reports red rather than passing silently.");
                failures.Add("WebGL support probe threw (" + e.GetType().Name + ") - WebGL compile status undeterminable");
                return failures;
            }

            if (!supported)
            {
                Debug.LogWarning(WebGlSkippedMarker + " reason=webgl-module-not-installed :: " +
                                 "BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, " +
                                 "BuildTarget.WebGL) == false on this machine. #if UNITY_WEBGL code " +
                                 "was NOT compiled by this gate. Install the WebGL Build Support " +
                                 "module to close the hole; a machine that ships WebGL content MUST " +
                                 "have it.");
                return failures;
            }

            // AC4: the active build target must be identical before and after. A target
            // switch here would trigger a full asset reimport and would corrupt the
            // Android/Windows ship chain (memory `desktop-build-after-android-target`).
            BuildTarget targetBefore = EditorUserBuildSettings.activeBuildTarget;

            // ---- evidence source (b): where the log stands BEFORE we compile -----
            // Deliberately not consulted while the self-test replay hook is armed:
            // that path points FindEditorLogPath at a captured file we are not writing.
            string logPath = string.IsNullOrEmpty(LogPathOverrideForSelfTest) ? FindEditorLogPath() : null;
            long logOffset = -1;
            if (!string.IsNullOrEmpty(logPath))
            {
                try { logOffset = new FileInfo(logPath).Length; }
                catch (Exception) { logOffset = -1; }
            }

            // ---- evidence source (a): the compiler's own messages ----------------
            var callbackErrors = new List<string>();
            Action<string, CompilerMessage[]> onAssemblyFinished = (asmPath, messages) =>
            {
                if (messages == null) return;
                foreach (CompilerMessage m in messages)
                {
                    if (m.type != CompilerMessageType.Error) continue;
                    callbackErrors.Add(m.file + "(" + m.line + "," + m.column + "): error: " + m.message);
                }
            };

            var sw = Stopwatch.StartNew();
            ScriptCompilationResult result = default(ScriptCompilationResult);
            bool threw = false;

            try
            {
                CompilationPipeline.assemblyCompilationFinished += onAssemblyFinished;

                string outFolder = Path.Combine(Directory.GetCurrentDirectory(),
                                                WebGlScratchFolder.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(outFolder);

                var settings = new ScriptCompilationSettings
                {
                    group = BuildTargetGroup.WebGL,
                    target = BuildTarget.WebGL,
                    options = ScriptCompilationOptions.None,
                };

                Debug.Log("[CompileGate] WebGL pass: compiling player scripts for BuildTarget.WebGL " +
                          "(active target stays " + targetBefore + ") -> " + outFolder);

                result = PlayerBuildInterface.CompilePlayerScripts(settings, outFolder);
            }
            catch (Exception e)
            {
                threw = true;
                Debug.LogError(WebGlFailMarker + " :: PlayerBuildInterface.CompilePlayerScripts threw " +
                               e.GetType().Name + ": " + e.Message);
                failures.Add("WebGL player-script compile threw (" + e.GetType().Name + ": " + e.Message + ")");
            }
            finally
            {
                CompilationPipeline.assemblyCompilationFinished -= onAssemblyFinished;
                sw.Stop();
            }

            // ---- AC4: prove the active target did not move -----------------------
            BuildTarget targetAfter = EditorUserBuildSettings.activeBuildTarget;
            if (targetAfter != targetBefore)
            {
                Debug.LogError("[CompileGate] ACTIVE BUILD TARGET MOVED during the WebGL pass: " +
                               targetBefore + " -> " + targetAfter + ". That is a full reimport and " +
                               "it breaks the ship chain - the WebGL pass must never switch targets.");
                failures.Add("active build target changed during the WebGL compile pass (" +
                             targetBefore + " -> " + targetAfter + ")");
            }

            if (threw) return failures;

            // ---- evidence source (b): rescan only the bytes this call appended ---
            var logErrors = new List<string>();
            if (!string.IsNullOrEmpty(logPath) && logOffset >= 0)
            {
                try
                {
                    using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                                                   FileShare.ReadWrite | FileShare.Delete))
                    {
                        if (logOffset <= fs.Length) fs.Seek(logOffset, SeekOrigin.Begin);
                        using (var sr = new StreamReader(fs))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (line.IndexOf("error CS", StringComparison.Ordinal) < 0) continue;
                                if (!CsErrorRx.IsMatch(line)) continue;
                                logErrors.Add(line.Trim());
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[CompileGate] WebGL pass: could not re-read the editor log tail (" +
                                     e.GetType().Name + ": " + e.Message +
                                     ") - falling back to the compiler callback + assembly outputs.");
                }
            }

            // ReadOnlyCollection<string> in 6000.4 (the WO-1575 lane wrote .Length; cg-wave10 CS1061).
            int assemblyCount = result.assemblies == null ? 0 : result.assemblies.Count;
            string seconds = (sw.ElapsedMilliseconds / 1000f).ToString("0.0");

            // ---- package-only errors are ADVISORY, not a verdict (cg-wave10, 2026-09-07) ----
            // First live run: the only errors were Packages\com.solana.unity_sdk\...\WebGLInput.cs
            // CS1069 ('WebGLInput' forwarded to UnityEngine.WebGLModule) - a module reference this
            // player-script compile does not carry while the active target is Android. The REAL WebGL
            // content build (shipped 2026-09-07 05:26, WEBGL_BUILD_OK) resolves it, so a red here on a
            // Packages/ path is the pass's reference set, not the tree. Errors under Assets/ - the
            // #if UNITY_WEBGL code this pass exists to catch (WebTrace.cs:325 CS1501) - still FAIL.
            // Closing the reference gap properly is the WO-1575 follow-up; until then the package
            // lines are printed under their own marker so their presence stays visible on the log.
            var packageErrors = new List<string>();
            logErrors = SplitPackageErrors(logErrors, packageErrors);
            callbackErrors = SplitPackageErrors(callbackErrors, packageErrors);
            if (packageErrors.Count > 0)
            {
                List<string> pkgShown = Cap(packageErrors, out int pkgMore);
                foreach (string l in pkgShown)
                    Debug.LogWarning("[CompileGate]   ~ (package, advisory) " + l);
                Debug.LogWarning("COMPILE_GATE_WEBGL_ADVISORY :: " + packageErrors.Count + " error line(s) in " +
                                 "Packages/ ignored by the WebGL pass (module reference gap, WO-1575)" +
                                 (pkgMore > 0 ? " - " + pkgMore + " more not shown" : ""));
            }

            // ---- verdict ---------------------------------------------------------
            if (callbackErrors.Count > 0 || logErrors.Count > 0)
            {
                Debug.LogError("[CompileGate] WEBGL-ONLY COMPILE ERRORS - this code is invisible to " +
                               "the active-target compile and would only have failed in a WebGL " +
                               "content build:");

                // Log lines FIRST: they carry the verbatim `file(line,col): error CSxxxx`
                // shape every existing log reader already parses (AC2).
                List<string> logShown = Cap(logErrors, out int logMore);
                foreach (string l in logShown)
                    Debug.LogError("[CompileGate]   > " + l);
                if (logMore > 0)
                    Debug.LogError("[CompileGate]   ... and " + logMore + " more WebGL error line(s) in the log");

                List<string> cbShown = Cap(callbackErrors, out int cbMore);
                foreach (string l in cbShown)
                    Debug.LogError("[CompileGate]   > (compiler callback) " + l);
                if (cbMore > 0)
                    Debug.LogError("[CompileGate]   ... and " + cbMore + " more callback error(s)");

                Debug.LogError(WebGlFailMarker + " :: " + logErrors.Count + " log error line(s) + " +
                               callbackErrors.Count + " compiler-callback error(s) in " + seconds +
                               "s. OK marker withheld.");
                failures.Add("WebGL player-script compile failed (" +
                             Math.Max(logErrors.Count, callbackErrors.Count) +
                             " error(s)) - #if UNITY_WEBGL code does not compile");
                return failures;
            }

            if (assemblyCount <= 0 && packageErrors.Count > 0)
            {
                // The package reference gap stopped the compile before any assembly was produced, so
                // this pass could not judge the tree at all. That is a SKIP with a named reason (the
                // class doc's SKIPPED policy: visible, does not withhold COMPILE_GATE_OK), not a red
                // against code that the real WebGL content build compiles. WO-1575 follow-up closes it.
                Debug.LogWarning(WebGlSkippedMarker + " reason=package-reference-gap :: the WebGL pass " +
                                 "stopped on " + packageErrors.Count + " Packages/ error line(s) before " +
                                 "producing an assembly (" + seconds + "s); the tree's own #if UNITY_WEBGL " +
                                 "code was NOT judged this run - see COMPILE_GATE_WEBGL_ADVISORY above.");
                return failures;
            }

            if (assemblyCount <= 0)
            {
                // No errors AND no assemblies == we learned nothing. Fail loud; never
                // pass silently (class doc DESIGN RULE).
                Debug.LogError(WebGlFailMarker + " :: INCONCLUSIVE - the WebGL compile produced 0 " +
                               "assemblies and no diagnosable error lines after " + seconds + "s. A " +
                               "check that cannot prove green must report red.");
                failures.Add("WebGL player-script compile produced 0 assemblies and no error lines - " +
                             "inconclusive, so the gate reports red");
                return failures;
            }

            Debug.Log(WebGlOkMarker + " :: " + assemblyCount + " player assemblies compiled for " +
                      "BuildTarget.WebGL in " + seconds + "s (active target unchanged: " + targetAfter + ")");
            return failures;
        }

        /// <summary>
        /// Resolves the log file this editor session is writing: the -logFile
        /// command-line argument first (that is what run-unity-method.ps1 passes),
        /// then Application.consoleLogPath. Returns null when neither resolves to
        /// an existing file (e.g. `-logFile -` streams to stdout with no file).
        /// </summary>
        private static string FindEditorLogPath()
        {
            // Self-test replay hook; null in every production path.
            if (!string.IsNullOrEmpty(LogPathOverrideForSelfTest) &&
                File.Exists(LogPathOverrideForSelfTest))
            {
                return LogPathOverrideForSelfTest;
            }

            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (!string.Equals(args[i], "-logFile", StringComparison.OrdinalIgnoreCase)) continue;
                    string candidate = args[i + 1];
                    if (string.IsNullOrEmpty(candidate) || candidate == "-") break;
                    if (!Path.IsPathRooted(candidate))
                        candidate = Path.Combine(Directory.GetCurrentDirectory(), candidate);
                    if (File.Exists(candidate)) return candidate;
                    break;
                }
            }
            catch (Exception)
            {
                // fall through to consoleLogPath
            }

            try
            {
                string console = Application.consoleLogPath;
                if (!string.IsNullOrEmpty(console) && File.Exists(console)) return console;
            }
            catch (Exception)
            {
                // no log available
            }

            return null;
        }

        private static readonly Regex CsErrorRx =
            new Regex(@"\berror CS\d{3,5}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Streams the (still open, still being written) editor log looking for
        /// C# compiler error lines of the form
        ///   Assets\Path\File.cs(258,27): error CS0103: The name '...' ...
        /// plus Unity's own "Scripts have compiler errors." verdict line.
        /// Opened with FileShare.ReadWrite|Delete because the editor holds it open.
        /// Returns false only when the log could not be read at all.
        /// </summary>
        private static bool TryScanLogForCompileErrors(
            string logPath,
            out Dictionary<string, int> byFile,
            out int totalErrors,
            out List<string> samples,
            out bool unityVerdict,
            out string readError)
        {
            byFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            totalErrors = 0;
            samples = new List<string>();
            unityVerdict = false;
            readError = null;

            try
            {
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.IndexOf("Scripts have compiler errors", StringComparison.Ordinal) >= 0)
                        {
                            unityVerdict = true;
                            continue;
                        }

                        // cheap reject before the regex - the log is large
                        if (line.IndexOf("error CS", StringComparison.Ordinal) < 0) continue;
                        if (!CsErrorRx.IsMatch(line)) continue;

                        totalErrors++;

                        string file = ExtractSourceFile(line);
                        byFile.TryGetValue(file, out int n);
                        byFile[file] = n + 1;

                        if (samples.Count < 3) samples.Add(line.Trim());
                    }
                }
            }
            catch (Exception e)
            {
                readError = e.GetType().Name + ": " + e.Message;
                return false;
            }

            return true;
        }

        /// <summary>Pulls the "Path\File.cs(line,col)" prefix out of a compiler error line.</summary>
        private static string ExtractSourceFile(string line)
        {
            int cs = line.IndexOf(".cs(", StringComparison.OrdinalIgnoreCase);
            if (cs < 0) return "(unattributed)";
            int close = line.IndexOf(')', cs);
            if (close < 0) return line.Substring(0, cs + 3).Trim();
            // file path only (drop the (line,col)) so counts aggregate per file
            return line.Substring(0, cs + 3).Trim();
        }

        /// <summary>
        /// Reads EditorUtility.scriptCompilationFailed without a compile-time bind
        /// (its accessibility has varied by Unity version). Returns false when the
        /// member does not exist - which means "no evidence", NOT "clean".
        /// </summary>
        private static bool TryGetScriptCompilationFailed(out bool failed, out string note)
        {
            failed = false;
            note = "not found";
            try
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                Type t = typeof(EditorUtility);

                PropertyInfo p = t.GetProperty("scriptCompilationFailed", flags);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                {
                    failed = (bool)p.GetValue(null, null);
                    note = "property";
                    return true;
                }

                FieldInfo f = t.GetField("scriptCompilationFailed", flags);
                if (f != null && f.FieldType == typeof(bool))
                {
                    failed = (bool)f.GetValue(null);
                    note = "field";
                    return true;
                }
            }
            catch (Exception e)
            {
                note = "threw " + e.GetType().Name;
            }

            return false;
        }

        /// <summary>
        /// Moves every error line whose path lives under Packages/ (or Library/PackageCache/) into
        /// <paramref name="packageErrors"/> and returns the rest. An error the tree does not own cannot be
        /// the tree's verdict; it is reported under COMPILE_GATE_WEBGL_ADVISORY instead (WO-1575 follow-up).
        /// </summary>
        private static List<string> SplitPackageErrors(List<string> all, List<string> packageErrors)
        {
            var mine = new List<string>(all.Count);
            foreach (string line in all)
            {
                string norm = (line ?? "").TrimStart().Replace('\\', '/');
                bool isPackage = norm.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                                 norm.StartsWith("Library/PackageCache/", StringComparison.OrdinalIgnoreCase) ||
                                 norm.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 norm.IndexOf("/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isPackage) packageErrors.Add(line); else mine.Add(line);
            }
            return mine;
        }

        /// <summary>Returns at most <see cref="MaxNamed"/> entries; <paramref name="more"/> gets the remainder count.</summary>
        private static List<string> Cap(List<string> all, out int more)
        {
            if (all.Count <= MaxNamed)
            {
                more = 0;
                return all;
            }

            more = all.Count - MaxNamed;
            return all.GetRange(0, MaxNamed);
        }

        // =====================================================================
        // WO-434 NUL scan + brace scan (unchanged behaviour)
        // =====================================================================

        /// <summary>
        /// Scans every project .cs file under Assets/ for an embedded/trailing NUL
        /// byte (\x00). Returns the list of offending paths (empty when clean).
        /// Fast: reads bytes and early-outs on the first NUL per file. Skips
        /// Library/Temp/obj/.git and non-.cs files.
        /// </summary>
        public static List<string> ScanForNulBytes()
        {
            var offenders = new List<string>();
            string assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            if (!Directory.Exists(assetsRoot))
            {
                return offenders;
            }

            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Library/") || norm.Contains("/Temp/") ||
                    norm.Contains("/obj/") || norm.Contains("/.git/"))
                {
                    continue;
                }

                if (FileContainsNul(path))
                {
                    offenders.Add(path);
                }
            }

            return offenders;
        }

        private static bool FileContainsNul(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[64 * 1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            if (buffer[i] == 0)
                            {
                                return true; // early-out on first NUL
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable file is not our concern here; treat as clean.
            }

            return false;
        }

        /// <summary>Fast static scan: every Assets/*.cs must have balanced { } counts.
        /// Catches mount-garble / half-written files BEFORE a wasted compile cycle.</summary>
        public static List<string> ScanBraceBalance()
        {
            var offenders = new List<string>();
            string assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            if (!Directory.Exists(assetsRoot)) return offenders;

            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Library/") || norm.Contains("/Temp/") ||
                    norm.Contains("/obj/") || norm.Contains("/.git/"))
                    continue;

                try
                {
                    string text = File.ReadAllText(path);
                    if (!BraceBalanced(text, out int open, out int close))
                        offenders.Add(norm + " (" + open + " open vs " + close + " close)");
                }
                catch (IOException)
                {
                    // unreadable - skip
                }
            }

            return offenders;
        }

        /// <summary>Counts { } outside strings / line+block comments (avoids lint-test false positives).</summary>
        private static bool BraceBalanced(string text, out int open, out int close)
        {
            open = close = 0;
            bool lineComment = false, blockComment = false, str = false, chr = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (lineComment)
                {
                    if (c == '\n') lineComment = false;
                    continue;
                }
                if (blockComment)
                {
                    if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }
                if (str)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') str = false;
                    continue;
                }
                if (chr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') chr = false;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] == '/')
                    {
                        lineComment = true;
                        i++;
                        continue;
                    }
                    if (text[i + 1] == '*')
                    {
                        blockComment = true;
                        i++;
                        continue;
                    }
                }
                if (c == '"') { str = true; continue; }
                if (c == '\'') { chr = true; continue; }
                if (c == '{') open++;
                else if (c == '}') close++;
            }
            return open == close;
        }
    }
}
