// =============================================================================
// StructureLoadBoundedRegression — the structure art load path CANNOT block the
// main thread. Markers: STRUCTURE_LOAD_BOUNDED_OK / STRUCTURE_LOAD_BOUNDED_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Register in DeNelle.Editor.DataRegression.RunAll.
// Standalone: run-unity-method -Method DeNelle.Editor.StructureLoadBoundedRegression.RunAll
//
// ⛔ THE DEFECT THIS PINS (captured 2026-08-20, Seeker device, dungeon -> town):
//   08-20 10:25:14.917 [Flow:VisualFactory] -> Skin('Structures/barracks')
//       ... HubStructureVisualInjector:OnSceneLoaded(Scene, LoadSceneMode)
//   ...and then NOTHING. Device clock 10:28:35, last game log 10:25:14.917 — three
//   minutes of total process silence, alive and foregrounded, stale frame, no ANR.
//   NOT a network fault: the owner pinged the R2 CDN from the hung device, 2/2
//   packets, 0% loss, 31.5 ms.
//
//   The mechanism was Addressables WaitForCompletion() called from inside a
//   SceneManager.sceneLoaded ENGINE CALLBACK. In Addressables 2.9.1
//   (Library/PackageCache/com.unity.addressables@8460f1c9c927) that method is
//       while (!InvokeWaitForCompletion()) { }
//   (AsyncOperations/AsyncOperationBase.cs:171) — no timeout, no yield, no exit —
//   and ProviderOperation.InvokeWaitForCompletion (ProviderOperation.cs:66) returns
//   false forever when the provider installed no completion callback, while
//   AssetBundleResource.WaitForCompletionHandler (AssetBundleProvider.cs:543)
//   Thread.Sleep()s the main thread waiting on progress the player loop must drive.
//   Blocked inside a nested engine callback, the thread that would finish the
//   operation is the thread waiting for it. A TIMEOUT WOULD NOT HAVE HELPED, and
//   the package does not offer one.
//
// ⛔ WHY THIS IS A SOURCE LINT AND NOT A RUNTIME TEST.
// The property is "no code path can block", and the failure it guards is a DEADLOCK.
// A runtime test of a deadlock either hangs the gate (useless — that is the very
// symptom) or proves nothing because the content happened to be resident that run.
// The only oracle that is both cheap and reliable is: THE BLOCKING CALL IS NOT ON
// THIS PATH. So this suite proves the absence structurally, and proves the
// GRACEFUL-SKIP + LOUD-REPORT contract that must hold in its place.
//
// ⛔ IT LINTS CODE ONLY — comments and string-literal contents are blanked (via
// ComposedDungeonRunRegression.StripCommentsAndStrings) before any match. The files
// it guards carry long tombstone comments naming WaitForCompletion precisely so the
// next author understands the ban; a rule that matched its own tombstone would
// punish exactly the documentation CLAUDE.md §12/§15 demands. Group 5 proves the
// blanking works rather than assuming it.
//
// ⛔ IT ALSO PINS THE FOLLOW-ON DEFECT (group 3b), because the first fix caused it.
// The shipped warm pass downloaded the BUNDLES and never loaded the ASSETS, then
// reported itself Warm with resident=0. TryGet missed every address, the loader fell
// through to a Resources copy the CDN migration had deleted, and the owner's build-mode
// placement ghosts rendered as placeholder PILLS. Nothing checked whether "Warm" was
// true, which is why it reached a device. A marker that cannot be falsified is not a
// marker. Group 3b makes the residency claim checkable at build time.
//
// ⛔ IT ASSERTS BOTH DIRECTIONS (group 5). A gate that does not FAIL the known-bad
// state is not a gate, and a gate that fails a clean state becomes a permanent red
// everyone learns to ignore. Group 5 runs the real detector over synthetic sources —
// the pre-fix shape MUST be flagged, a clean shape MUST NOT be, and a source whose
// only mention is prose/strings MUST NOT be.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Source-lint: the structure asset load path is incapable of an unbounded main-thread
    /// block, degrades by SKIPPING (baked twin survives), and reports every skip loudly.
    /// Returns true (summary) / false (detail); never throws.
    /// </summary>
    public static class StructureLoadBoundedRegression
    {
        public const string MarkerOk   = "STRUCTURE_LOAD_BOUNDED_OK";
        public const string MarkerFail = "STRUCTURE_LOAD_BOUNDED_FAIL";

        /// <summary>The blocking call. Assembled from parts so this constant is not itself a
        /// literal that a future, dumber grep of this repo would flag.</summary>
        private const string BlockingCall = "WaitFor" + "Completion";

        // Repo-relative paths of the seam under guard.
        private const string LoaderRel   = "_Modules/Core/Addressables/StructureAssetLoader.cs";
        private const string WarmerRel   = "_Modules/Core/Addressables/StructureContentWarmer.cs";
        private const string ResolverRel = "_Modules/Core/Addressables/StructureEditorSyncResolver.cs";
        private const string InjectorRel = "_Modules/Village/HubStructureVisualInjector.cs";

        /// <summary>The ONE file in the structure seam allowed to block, because it is entirely
        /// inside #if UNITY_EDITOR and the Editor resolves through the AssetDatabase provider —
        /// no bundle, no UnityWebRequest, no player loop to starve.</summary>
        private const string AllowedBlockingFile = "StructureEditorSyncResolver.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- STRUCTURE LOAD IS BOUNDED (P0 hang, 2026-08-20) ---");

            string assets = null;
            try { assets = Application.dataPath; } catch { }
            if (string.IsNullOrEmpty(assets) || !Directory.Exists(assets))
            {
                // hollow-pass-ok: Assets/ cannot be absent in this project, so the skip is
                // unreachable rather than risky (same shape as the other source-lints).
                reason = null;
                Debug.Log(log + "  (skipped -- Assets/ not found)\n" + MarkerOk);
                return true;
            }

            string loaderPath   = Path.Combine(assets, LoaderRel.Replace('/', Path.DirectorySeparatorChar));
            string warmerPath   = Path.Combine(assets, WarmerRel.Replace('/', Path.DirectorySeparatorChar));
            string resolverPath = Path.Combine(assets, ResolverRel.Replace('/', Path.DirectorySeparatorChar));
            string injectorPath = Path.Combine(assets, InjectorRel.Replace('/', Path.DirectorySeparatorChar));

            // ── 1. THE LOADER DOES NOT BLOCK. AT ALL. ─────────────────────────
            //  This is the case that would have caught the P0 before it shipped.
            string loaderRaw = ReadOrNull(loaderPath);
            if (loaderRaw == null)
            {
                failures.Add($"Assets/{LoaderRel} is missing — the structure seam has moved or been " +
                             "deleted; this gate cannot protect a path it cannot find.");
            }
            else
            {
                string loader = Code(loaderRaw);

                if (Blocks(loader, out int at))
                {
                    failures.Add($"Assets/{LoaderRel} calls {BlockingCall}() (code offset {at}). " +
                                 "That call is an UNINTERRUPTIBLE, UNBOUNDED spin with no timeout API " +
                                 "(Addressables 2.9.1 AsyncOperationBase.cs:171), and reaching it from a " +
                                 "sceneLoaded callback DEADLOCKED the game for three minutes on a device " +
                                 "with a healthy 31.5 ms link to the CDN. Serve resident content through " +
                                 "StructureContentWarmer.TryGet and SKIP when it is not resident.");
                }

                // The loader must reach content through the residency cache, and must ASK for a
                // miss asynchronously instead of waiting for it.
                RequireToken(failures, loader, "StructureContentWarmer.TryGet", LoaderRel,
                    "the resident-cache probe is the ONLY non-blocking way to serve an Addressables-backed " +
                    "asset synchronously; without it the loader has no fast path and will grow one that blocks");
                RequireToken(failures, loader, "StructureContentWarmer.Request", LoaderRel,
                    "a miss must kick an ASYNC fetch and return, so the next attempt succeeds; " +
                    "without it a cold cache is a permanent miss and someone will 'fix' it by waiting");

                // LOUD REPORTING. The three minutes cost what they cost because NOTHING announced
                // the wait. A skip that does not name the address and the elapsed wait recreates
                // exactly that hole in the log.
                RequireToken(failures, loader, "FlowTrace.Fail", LoaderRel,
                    "an unresolved structure is an invisible/wrong building and must be error-level");
                if (loader.IndexOf("FlowTrace.Throttle", StringComparison.Ordinal) < 0 &&
                    loader.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) < 0)
                {
                    failures.Add($"Assets/{LoaderRel} no longer Warns/Throttles on the SKIP path — a silent " +
                                 "skip is how a three-minute stall went unexplained. Every skip must name the " +
                                 "address and how long it has waited.");
                }
                RequireToken(failures, loader, "SecondsWaiting", LoaderRel,
                    "the report must say HOW LONG it waited, not merely that it skipped");

                log.AppendLine($"  {LoaderRel}: no {BlockingCall}, resident-first, async-request, reports loudly.");
            }

            // ── 2. NOTHING ELSE IN THE SEAM BLOCKS, except the editor-only site ──
            string addrDir = Path.Combine(assets, "_Modules/Core/Addressables".Replace('/', Path.DirectorySeparatorChar));
            string[] seamFiles;
            try { seamFiles = Directory.GetFiles(addrDir, "Structure*.cs", SearchOption.TopDirectoryOnly); }
            catch (Exception ex) { seamFiles = Array.Empty<string>(); log.AppendLine("  (seam scan failed: " + ex.Message + ")"); }

            int seamScanned = 0;
            foreach (var path in seamFiles)
            {
                string file = Path.GetFileName(path);
                string raw = ReadOrNull(path);
                if (raw == null) continue;
                seamScanned++;

                if (!Blocks(Code(raw), out _)) continue;

                if (!string.Equals(file, AllowedBlockingFile, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"Assets/_Modules/Core/Addressables/{file} calls {BlockingCall}(). The ONLY " +
                                 $"allowlisted blocking site in the structure seam is {AllowedBlockingFile}, " +
                                 "and only because that whole file is inside #if UNITY_EDITOR. Adding a second " +
                                 "one re-opens the 2026-08-20 deadlock.");
                    continue;
                }

                // The allowlisted file EARNS its allowance only by being editor-only. Verified on the
                // RAW text: #if directives are code, and blanking never touches them, but they are
                // structural rather than token-ish so they are checked explicitly.
                if (FirstDirective(raw) != "#if UNITY_EDITOR")
                {
                    failures.Add($"{AllowedBlockingFile} is allowed to call {BlockingCall}() ONLY because it is " +
                                 "entirely editor-only, but its first preprocessor directive is no longer " +
                                 "'#if UNITY_EDITOR'. As written it would compile into a PLAYER, where that call " +
                                 "deadlocks. Either restore the guard or remove the call.");
                }
                if (raw.TrimEnd().EndsWith("#endif", StringComparison.Ordinal) == false)
                {
                    failures.Add($"{AllowedBlockingFile} does not END with '#endif' — the editor-only guard no " +
                                 "longer wraps the whole file, so part of it ships to the player.");
                }
                // The pre-fix loader leaked a location handle on EVERY call. Whatever survives here
                // must release what it opens.
                string resolverCode = Code(raw);
                int opened = Count(resolverCode, "LoadResourceLocationsAsync");
                int released = Count(resolverCode, "Addressables.Release");
                if (opened > 0 && released < opened)
                {
                    failures.Add($"{AllowedBlockingFile} opens {opened} location handle(s) but releases {released}. " +
                                 "The pre-fix StructureAssetLoader.AddressableRegistered<T> never released its " +
                                 "LoadResourceLocationsAsync handle — a leak on EVERY structure load. Do not " +
                                 "reintroduce it.");
                }
            }
            log.AppendLine($"  scanned {seamScanned} Structure*.cs seam file(s) under Core/Addressables.");

            // ── 3. THE WARMER IS ASYNC, RETAINS, AND NEVER BLOCKS ─────────────
            string warmerRaw = ReadOrNull(warmerPath);
            if (warmerRaw == null)
            {
                failures.Add($"Assets/{WarmerRel} is missing — it IS the non-blocking replacement for the " +
                             "synchronous load. Without it the loader has nowhere to serve resident content from.");
            }
            else
            {
                string warmer = Code(warmerRaw);

                if (Blocks(warmer, out _))
                    failures.Add($"Assets/{WarmerRel} calls {BlockingCall}(). The warmer exists precisely so that " +
                                 "nothing has to.");

                // Async by construction: the work happens in a coroutine, on the player loop — the
                // only place the ResourceManager can actually be pumped.
                if (warmer.IndexOf("yield return", StringComparison.Ordinal) < 0)
                    failures.Add($"Assets/{WarmerRel} has no 'yield return' — the warm pass is no longer a " +
                                 "coroutine, so it is no longer running on the player loop. A blocking warm pass " +
                                 "is the original bug wearing a new name.");

                RequireToken(failures, warmer, "MonoBehaviour", WarmerRel,
                    "the coroutine needs a DontDestroyOnLoad host; without one there is no player loop to warm from");
                RequireToken(failures, warmer, "public static bool TryGet", WarmerRel,
                    "TryGet is the synchronous, non-blocking read the whole fix rests on");
                RequireToken(failures, warmer, "public static void Defer", WarmerRel,
                    "Defer is how a scene-load callback gets OFF the engine callback before touching content");
                RequireToken(failures, warmer, "WarmDeadlineSeconds", WarmerRel,
                    "the warm pass must have a reporting deadline so a stalled pull is announced, not silent");

                // RETENTION. Releasing loaded structure content is what lets the dungeon load evict
                // it and puts the next town load back on the cold path where the deadlock lives.
                RequireToken(failures, warmer, "s_retained", WarmerRel,
                    "loaded structure content must be retained for the process, or the owner's constant " +
                    "town -> dungeon -> town loop re-downloads and re-evicts it every cycle");
                if (warmer.IndexOf("s_retained.Clear", StringComparison.Ordinal) >= 0 ||
                    warmer.IndexOf("s_retained.Remove", StringComparison.Ordinal) >= 0)
                {
                    failures.Add($"Assets/{WarmerRel} clears/removes from s_retained. That list is the ONLY thing " +
                                 "keeping structure content resident across a dungeon round-trip; draining it " +
                                 "restores the eviction cycle this fix exists to stop.");
                }

                // ── 3b. THE WARM PASS ACTUALLY LOADS, AND CANNOT OVERCLAIM ────
                //  The defect that reached the owner's device: downloaded, never loaded,
                //  reported Warm with resident=0, shipped pills.
                string warmBody;
                if (!TryMethodBody(warmer, "IEnumerator WarmRoutine(", out warmBody))
                {
                    failures.Add($"Assets/{WarmerRel}: WarmRoutine() not found — the warm pass has been " +
                                 "renamed or removed and this gate can no longer see what it does.");
                }
                else
                {
                    if (warmBody.IndexOf("Request(", StringComparison.Ordinal) < 0 &&
                        warmBody.IndexOf("LoadAssetAsync", StringComparison.Ordinal) < 0)
                    {
                        failures.Add($"Assets/{WarmerRel}: WarmRoutine downloads but never LOADS. " +
                                     "DownloadDependenciesAsync makes the BUNDLES local; it puts nothing in the " +
                                     "resident dictionary, so TryGet misses every address and every synchronous " +
                                     "caller degrades. That shipped on 2026-08-20 and rendered placement ghosts " +
                                     "as pills. The warm pass must load and retain the assets themselves.");
                    }

                    if (warmBody.IndexOf("DecideState(", StringComparison.Ordinal) < 0)
                        failures.Add($"Assets/{WarmerRel}: WarmRoutine no longer routes its final state through " +
                                     "DecideState(). That method is the single place Warm can be decided; " +
                                     "bypassing it is how the reported state drifts from the achieved one.");
                }

                // Warm may be RETURNED only by DecideState, never ASSIGNED anywhere. The assignment
                // form is what let 'Warm' be stamped without anyone checking residency.
                if (warmer.IndexOf("State = StructureContentState.Warm;", StringComparison.Ordinal) >= 0)
                {
                    failures.Add($"Assets/{WarmerRel} assigns State = Warm directly. Warm must be decided ONLY by " +
                                 "DecideState(discovered, resident), which refuses it when nothing is resident. " +
                                 "A direct assignment reinstates the marker that claimed success with resident=0.");
                }

                string decideBody;
                if (!TryMethodBody(warmer, "StructureContentState DecideState(", out decideBody))
                {
                    failures.Add($"Assets/{WarmerRel}: DecideState(discovered, resident) is missing. It is the " +
                                 "guard that makes 'Warm' falsifiable — without it the warm pass can report " +
                                 "success having loaded nothing, which is exactly what reached the owner.");
                }
                else
                {
                    if (!ResidentZeroIsRefused(decideBody))
                        failures.Add($"Assets/{WarmerRel}: DecideState no longer refuses the resident==0 case. " +
                                     "It must return Degraded AND FlowTrace.Fail when it discovered addresses but " +
                                     "holds none of them — reporting Warm there is the 2026-08-20 'pills loading' bug.");
                    if (decideBody.IndexOf("StructureContentState.Warm;", StringComparison.Ordinal) < 0)
                        failures.Add($"Assets/{WarmerRel}: DecideState never returns Warm — the fast path can " +
                                     "never be reported as available, which makes the whole residency cache dead code.");
                }

                // Honest fallback reporting: a Resources answer is a double-ship only when the
                // address is ALSO registered. Four Harvest/* lines were reported as duplication on
                // 2026-08-20 and none of them was.
                RequireToken(failures, warmer, "IsRegisteredAddress", WarmerRel,
                    "DependencyClosureTrace needs it to tell a REAL double-ship from content that " +
                    "legitimately still lives in Resources; without it the fallback warning cries wolf");

                log.AppendLine($"  {WarmerRel}: coroutine warm pass LOADS + retains, Warm gated on residency.");
            }

            // ── 3c. THE FALLBACK REPORT DOES NOT CRY WOLF ─────────────────────
            string dctPath = Path.Combine(assets,
                "_Modules/Core/Addressables/DependencyClosureTrace.cs".Replace('/', Path.DirectorySeparatorChar));
            string dctRaw = ReadOrNull(dctPath);
            if (dctRaw == null)
            {
                failures.Add("Assets/_Modules/Core/Addressables/DependencyClosureTrace.cs is missing.");
            }
            else
            {
                string dct = Code(dctRaw);
                RequireToken(failures, dct, "StructureContentWarmer.IsRegisteredAddress", "_Modules/Core/Addressables/DependencyClosureTrace.cs",
                    "the Resources-fallback branch must ASK whether the address is registered before calling it a " +
                    "double-ship. Unconditional, it reported the four Harvest/* FBXs (~33-156 KB, deliberately " +
                    "Resources-resident, NO Addressables key) as shipping twice and sent triage down a dead end");
                log.AppendLine("  DependencyClosureTrace.cs: double-ship claim is conditional on registration.");
            }

            // ── 4. THE HUB DEGRADES GRACEFULLY AND OFF THE CALLBACK ───────────
            string injectorRaw = ReadOrNull(injectorPath);
            if (injectorRaw == null)
            {
                failures.Add($"Assets/{InjectorRel} is missing — it is the call site the capture named.");
            }
            else
            {
                string injector = Code(injectorRaw);

                // (a) OFF THE ENGINE CALLBACK. The capture's stack was
                //     Internal_SceneLoaded -> OnSceneLoaded -> ApplyAll -> ... -> the block.
                RequireToken(failures, injector, "StructureContentWarmer.Defer(ApplyAll)", InjectorRel,
                    "OnSceneLoaded must hand ApplyAll to the NEXT FRAME rather than run it inside the " +
                    "sceneLoaded engine callback — that nesting is what made the block a DEADLOCK rather " +
                    "than a stall, because the player loop could not re-enter to drive the operation");

                if (OnSceneLoadedCallsApplyAllInline(injector))
                    failures.Add($"Assets/{InjectorRel}: OnSceneLoaded calls ApplyAll() INLINE again. That is the " +
                                 "exact stack from the 2026-08-20 capture. Defer it.");

                // (b) GRACEFUL SKIP. A null from the loader must leave the baked twin visible.
                //     A slightly wrong-looking building beats a dead game.
                RequireToken(failures, injector, "r.enabled = true", InjectorRel,
                    "SkinStorefront must re-enable the baked renderers when VisualFactory.Skin returns null, " +
                    "or a skipped skin leaves an INVISIBLE building instead of a baked one");
                RequireToken(failures, injector, "StructureContentWarmer.WhenSettled", InjectorRel,
                    "a skip must be recoverable: re-apply the hub skins once the content is resident, " +
                    "otherwise the town keeps the baked twins for the whole session");

                log.AppendLine($"  {InjectorRel}: deferred off sceneLoaded, restores baked twin, re-applies when settled.");
            }

            // ── 5. THE ORACLE ITSELF — BOTH DIRECTIONS ────────────────────────
            //  Everything above is only worth the bytes if Blocks() actually discriminates.
            foreach (var c in OracleCases())
            {
                bool flagged = Blocks(Code(c.Source), out _);
                if (flagged == c.ShouldFlag) continue;
                failures.Add($"ORACLE SELF-TEST FAILED ({c.Name}): expected " +
                             (c.ShouldFlag ? "a FLAG" : "NO flag") + $" but got {(flagged ? "a FLAG" : "none")}. " +
                             "The detector no longer discriminates, so every PASS above is meaningless.");
            }
            log.AppendLine($"  oracle self-test: {OracleCases().Count} case(s), known-bad flagged + clean/tombstone clear.");

            //  The residency guard needs its own both-directions proof, for the same reason: if
            //  ResidentZeroIsRefused() cannot tell a guarded DecideState from an unguarded one,
            //  group 3b is decoration.
            foreach (var c in ResidencyOracleCases())
            {
                bool refuses = ResidentZeroIsRefused(Code(c.Source));
                if (refuses == c.ShouldFlag) continue;
                failures.Add($"RESIDENCY ORACLE SELF-TEST FAILED ({c.Name}): expected " +
                             (c.ShouldFlag ? "REFUSES resident==0" : "does NOT refuse") +
                             $" but got {(refuses ? "refuses" : "does not refuse")}. " +
                             "The residency guard no longer discriminates, so 'Warm' is unchecked again.");
            }
            log.AppendLine($"  residency oracle: {ResidencyOracleCases().Count} case(s), " +
                           "guarded DecideState accepted + the shipped unguarded one rejected.");

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log + MarkerOk);
                return true;
            }

            reason = "structure-load-bounded: " + string.Join("; ", failures);
            Debug.LogError(log + MarkerFail + ": " + reason);
            return false;
        }

        // =====================================================================
        //  The detector, and the synthetic sources that prove it discriminates
        // =====================================================================

        /// <summary>
        /// True when <paramref name="code"/> (ALREADY comment- and string-blanked) contains the
        /// blocking call. Deliberately has NO '#if' escape hatch: a conditional block is exactly
        /// how such a ban erodes — the next author adds one, the gate keeps passing, and the
        /// deadlock comes back. The single editor-only exemption is granted by FILE PATH in
        /// group 2, where the guard can also be verified.
        /// </summary>
        private static bool Blocks(string code, out int offset)
        {
            offset = string.IsNullOrEmpty(code)
                ? -1
                : code.IndexOf(BlockingCall, StringComparison.Ordinal);
            return offset >= 0;
        }

        private struct OracleCase
        {
            public string Name;
            public string Source;
            public bool   ShouldFlag;
        }

        /// <summary>
        /// Synthetic sources covering both directions. The KNOWN-BAD case is the pre-fix
        /// StructureAssetLoader body, reproduced closely enough that the gate provably would
        /// have caught the P0. The CLEAN and TOMBSTONE cases prove it will not become a
        /// permanent red — including the case that matters most for this repo's documentation
        /// style, where the banned token appears only in prose and in an error string.
        /// </summary>
        private static List<OracleCase> OracleCases()
        {
            // Assembled, never written as one literal: a source that CONTAINS the token as a
            // literal would be blanked by Code() before the detector ever saw it, and the
            // known-bad case would silently stop being known-bad.
            string call = BlockingCall + "()";

            return new List<OracleCase>
            {
                new OracleCase
                {
                    Name = "known-bad: pre-fix loader (the shipped P0)",
                    ShouldFlag = true,
                    Source =
                        "class L { void M() {\n" +
                        "  var handle = Addressables.LoadAssetAsync<T>(address);\n" +
                        "  result = handle." + call + ";\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "known-bad: pre-fix registration probe (the handle leak too)",
                    ShouldFlag = true,
                    Source =
                        "class L { bool M() {\n" +
                        "  var locHandle = Addressables.LoadResourceLocationsAsync(address, typeof(T));\n" +
                        "  var locations = locHandle." + call + ";\n" +
                        "  return locations != null && locations.Count > 0;\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "known-bad: hidden behind a conditional (the erosion path)",
                    ShouldFlag = true,
                    Source =
                        "class L { void M() {\n" +
                        "#if UNITY_EDITOR\n" +
                        "  result = handle." + call + ";\n" +
                        "#endif\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "clean: resident-first, async-request, skip",
                    ShouldFlag = false,
                    Source =
                        "class L { void M() {\n" +
                        "  if (StructureContentWarmer.TryGet(address, out result)) return result;\n" +
                        "  StructureContentWarmer.Request(address);\n" +
                        "  return null;\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "clean: warm pass that loads and retains",
                    ShouldFlag = false,
                    Source =
                        "class W { void M() {\n" +
                        "  for (int i = 0; i < keys.Count; i++) Request(keys[i]);\n" +
                        "  State = DecideState(keys.Count, s_resident.Count);\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "tombstone: the token only in a line comment",
                    ShouldFlag = false,
                    Source =
                        "class L { void M() {\n" +
                        "  // Never call " + call + " here - it deadlocked the game on 2026-08-20.\n" +
                        "  return null;\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "tombstone: the token only in a block comment",
                    ShouldFlag = false,
                    Source =
                        "class L { /* " + call + " is banned on this path. */ void M() { return null; } }",
                },
                new OracleCase
                {
                    Name = "tombstone: the token only inside a string literal (an error message)",
                    ShouldFlag = false,
                    Source =
                        "class L { void M() {\n" +
                        "  FlowTrace.Fail(Sys, \"do not use " + call + " on this path\");\n" +
                        "} }",
                },
                new OracleCase
                {
                    Name = "tombstone: the token only inside an interpolated string",
                    ShouldFlag = false,
                    Source =
                        "class L { void M() {\n" +
                        "  FlowTrace.Fail(Sys, $\"'{address}' must never reach " + call + "\");\n" +
                        "} }",
                },
            };
        }

        // =====================================================================
        //  Plumbing
        // =====================================================================

        /// <summary>
        /// CODE ONLY — comments and string-literal contents blanked. Shared with the rest of the
        /// suite family (ComposedDungeonRunRegression owns the scanner) so there is ONE stripper
        /// to be correct rather than six that drift.
        /// </summary>
        private static string Code(string source) =>
            ComposedDungeonRunRegression.StripCommentsAndStrings(source ?? string.Empty);

        private static void RequireToken(List<string> failures, string code, string token,
                                         string relPath, string why)
        {
            if (code.IndexOf(token, StringComparison.Ordinal) >= 0) return;
            failures.Add($"Assets/{relPath} no longer contains '{token}' — {why}.");
        }

        /// <summary>
        /// True when this DecideState body REFUSES to call a zero-residency pass Warm: it must both
        /// test the resident count against zero and report that refusal at error level. Either half
        /// alone is not enough — a silent Degraded is how the owner ends up reading the log instead
        /// of the game telling her.
        /// </summary>
        private static bool ResidentZeroIsRefused(string decideBody)
        {
            if (string.IsNullOrEmpty(decideBody)) return false;
            bool testsZero = decideBody.IndexOf("resident == 0", StringComparison.Ordinal) >= 0 ||
                             decideBody.IndexOf("resident <= 0", StringComparison.Ordinal) >= 0 ||
                             decideBody.IndexOf("resident < 1", StringComparison.Ordinal) >= 0;
            bool degrades  = decideBody.IndexOf("StructureContentState.Degraded", StringComparison.Ordinal) >= 0;
            bool reports   = decideBody.IndexOf("FlowTrace.Fail", StringComparison.Ordinal) >= 0;
            return testsZero && degrades && reports;
        }

        /// <summary>
        /// Both directions for <see cref="ResidentZeroIsRefused"/>. The known-bad case is the exact
        /// line that shipped on 2026-08-20 — a ternary on the DISCOVERED count, which is true the
        /// moment a catalog exists and says nothing at all about what was loaded.
        /// </summary>
        private static List<OracleCase> ResidencyOracleCases()
        {
            return new List<OracleCase>
            {
                new OracleCase
                {
                    Name = "clean: guarded on residency, Fails loudly",
                    ShouldFlag = true,
                    Source =
                        "if (resident == 0) {\n" +
                        "  FlowTrace.Fail(System, \"nothing resident\");\n" +
                        "  return StructureContentState.Degraded;\n" +
                        "}\n" +
                        "return StructureContentState.Warm;",
                },
                new OracleCase
                {
                    Name = "known-bad: the shipped ternary on the DISCOVERED count",
                    ShouldFlag = false,
                    Source = "State = keys.Count > 0 ? StructureContentState.Warm : StructureContentState.Degraded;",
                },
                new OracleCase
                {
                    Name = "known-bad: degrades silently, never reports",
                    ShouldFlag = false,
                    Source =
                        "if (resident == 0) return StructureContentState.Degraded;\n" +
                        "return StructureContentState.Warm;",
                },
                new OracleCase
                {
                    Name = "known-bad: tests residency but still calls it Warm",
                    ShouldFlag = false,
                    Source =
                        "if (resident == 0) FlowTrace.Warn(System, \"empty\");\n" +
                        "return StructureContentState.Warm;",
                },
            };
        }

        /// <summary>
        /// Balanced-brace body of the first method whose signature contains
        /// <paramref name="signatureNeedle"/>. Runs on ALREADY-BLANKED source, so no brace inside a
        /// string or comment can throw the depth count off. Brace chars come from code points
        /// (123/125) to keep this file's own brace balance clean under CLAUDE.md sec.1.
        /// </summary>
        private static bool TryMethodBody(string code, string signatureNeedle, out string body)
        {
            body = null;
            if (string.IsNullOrEmpty(code)) return false;
            char openBrace = (char)123;
            char closeBrace = (char)125;
            int sig = code.IndexOf(signatureNeedle, StringComparison.Ordinal);
            if (sig < 0) return false;
            int open = code.IndexOf(openBrace, sig);
            if (open < 0) return false;
            int depth = 0, i = open;
            for (; i < code.Length; i++)
            {
                if (code[i] == openBrace) depth++;
                else if (code[i] == closeBrace) { depth--; if (depth == 0) break; }
            }
            if (depth != 0) return false;
            body = code.Substring(open, i - open + 1);
            return true;
        }

        private static int Count(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }

        /// <summary>
        /// The file's first preprocessor directive, trimmed — read from RAW text on purpose:
        /// directives are structure, not tokens, and the blanker does not touch them.
        /// </summary>
        private static string FirstDirective(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            foreach (var rawLine in raw.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length > 0 && line[0] == '#') return line;
            }
            return string.Empty;
        }

        /// <summary>
        /// True when OnSceneLoaded's body still calls ApplyAll() directly — the exact stack from
        /// the capture. Scans the blanked code from the method signature to the next method-ish
        /// boundary; deliberately coarse, because a false positive here costs one clarifying edit
        /// and a false negative costs another three-minute hang.
        /// </summary>
        private static bool OnSceneLoadedCallsApplyAllInline(string code)
        {
            int at = code.IndexOf("OnSceneLoaded(Scene", StringComparison.Ordinal);
            while (at >= 0)
            {
                // Brace chars come from code points (123/125) so this file's own brace balance
                // stays clean under the CLAUDE.md sec.1 gate — the same trick the sibling
                // source-lints use.
                char openBrace = (char)123;
                char closeBrace = (char)125;

                int open = code.IndexOf(openBrace, at);
                if (open < 0) return false;

                int depth = 0, i = open;
                for (; i < code.Length; i++)
                {
                    if (code[i] == openBrace) depth++;
                    else if (code[i] == closeBrace) { depth--; if (depth == 0) break; }
                }
                string body = code.Substring(open, Math.Min(i, code.Length - 1) - open + 1);
                if (body.IndexOf("ApplyAll()", StringComparison.Ordinal) >= 0) return true;

                at = code.IndexOf("OnSceneLoaded(Scene", i, StringComparison.Ordinal);
            }
            return false;
        }

        private static string ReadOrNull(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (!ok)
            {
                Debug.LogError(reason);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log(MarkerOk + " (structure load path proven non-blocking)");
        }
    }
}
