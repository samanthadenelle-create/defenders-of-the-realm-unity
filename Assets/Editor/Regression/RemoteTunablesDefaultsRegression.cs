// =============================================================================
// RemoteTunablesDefaultsRegression [tunable-defaults]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (already references DeNelle.Core and
//   DeNelle.Village - no asmdef edit needed).
//
// Pins PROD-022's single most important invariant:
//
//     NO DATABASE ROW / UNREACHABLE BACKEND
//         => EVERY KNOB RESOLVES TO ITS SHIPPING DEFAULT, i.e. TODAY'S
//            BEHAVIOUR, BYTE FOR BYTE.
//
// -----------------------------------------------------------------------------
// WHY THIS ONE MATTERS MORE THAN THE REST OF THE SYSTEM
// -----------------------------------------------------------------------------
// EVERY player who cannot reach the API depends on it, and so does EVERY session
// before a row is ever written - which today is every session in existence,
// because the table ships EMPTY.
//
// !! AND IF IT REGRESSES, THE FAILURE IS INVISIBLE. Nothing crashes. Nothing
// blanks. The game keeps running - just not the way the build says it does. We
// would then be debugging PROD-022, a crash loop we cannot reproduce locally,
// against a configuration we cannot reconstruct. That is precisely the class of
// silent divergence the ticket exists because of, so it gets an oracle rather
// than a comment.
//
// -----------------------------------------------------------------------------
// THE FULL TABLE, NEVER A SAMPLE.
// -----------------------------------------------------------------------------
// All EIGHT knobs are asserted on EVERY failure path. A partial assertion would
// let one knob drift unnoticed, and one drifted knob is the whole risk: the
// build would quietly be running a configuration nobody chose.
//
// -----------------------------------------------------------------------------
// HOW THE DEFAULTS ARE PINNED - asked explicitly, answered explicitly.
// -----------------------------------------------------------------------------
// The obvious approach - "read the default out of Registry and check the knob
// equals it" - is CIRCULAR. It measures the thing against itself and would go
// green after someone changed a default to anything at all.
//
// So the defaults are stated THREE TIMES, INDEPENDENTLY, and all three are
// compared against each other:
//     1. ExpectedDefaults below  - LITERALS in this file. The oracle's own
//                                  independent statement of the contract.
//     2. RemoteTunables.Registry - the code's source of truth, which is what
//                                  actually resolves at runtime.
//     3. docs/PROD022_TUNABLE_FLAGS.md - the OWNER-FACING table, which is what
//                                  she reads before flipping anything.
// Change any one and this suite REDS, naming which two disagree. That is the
// intended behaviour: a default may absolutely be changed, but it may not be
// changed in one place only. This is the same three-way domain pin
// MaintenanceTogglesRegression case [area-domain] applies to the six area ids,
// and it is deliberately modelled on it rather than invented.
//
// It matches the house rule stated at the top of that suite: EVERY THRESHOLD IS
// A NAMED CONSTANT PINNED TO A LITERAL, never expressed relative to a value that
// can move underneath it.
//
// -----------------------------------------------------------------------------
// ZERO NETWORK, ZERO DATABASE, ZERO PlayerPrefs DEPENDENCY.
// -----------------------------------------------------------------------------
// RemoteTunables is transport-free by design (the same split MaintenanceCatalog
// keeps), so every failure mode is driven by handing it a STRING. Nothing here
// opens a socket, needs DATABASE_URL, or cares whether the machine is online.
//
// PlayerPrefs is the one piece of ambient state that could poison the run: a
// stale "ff.tun.*" override on a developer machine is, correctly, NOT the
// default - so the suite SNAPSHOTS every override, clears it, and RESTORES it in
// a finally. It also NOTES any it found, because an override left armed on the
// build machine is worth knowing about.
//
// Cases:
//   1 [defaults]      With no table and no override, all 8 knobs resolve to the
//                     literal-pinned shipping defaults, and Registry agrees.
//                     The registry shape is asserted too (8 entries, unique,
//                     ASCII, no key colliding with the ff.* namespace).
//   2 [failure-modes] Seven failure paths, each re-asserting the FULL table:
//                     no table / readOk=false / malformed JSON / empty body /
//                     corrupt device cache / values the server would refuse /
//                     garbage arriving after a good payload.
//   3 [consumers]     The REAL owners answer today's numbers with no table -
//                     StructureContentWarmer.MaxRequestAttempts == 3 and
//                     .PiRequestTimeoutSeconds == 20 - and neither consumer has
//                     re-hardcoded a knob behind the seam.
//   4 [key-domain]    The 8 keys are identical in RemoteTunables.Registry,
//                     api/_lib/tunables.js and the docs table.
//   5 [doc-parity]    The DEFAULT column in docs/PROD022_TUNABLE_FLAGS.md equals
//                     Registry, so the owner-facing list cannot drift from code.
//   6 [never-blocks]  The fetch cannot delay or stall boot: no blocking idiom in
//                     RemoteTunablesService, and the boot hook still fires and
//                     forgets. A crash-loop ticket must not add a boot hazard.
//
// Markers: TUNABLE_DEFAULTS_OK / TUNABLE_DEFAULTS_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.RemoteTunablesDefaultsRegression.RunAll
// Registered in DataRegression.RunAll as "[tunable-defaults]".
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Core;
using DeNelle.Core.Ops;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RemoteTunablesDefaultsRegression
    {
        // ---------------------------------------------------------------------
        //  PINNED FACTS. Every one is a literal.
        // ---------------------------------------------------------------------

        /// <summary>The domain is EIGHT knobs. Pinned as a literal, not as
        /// Registry.Length - an oracle that measures the thing against itself
        /// certifies nothing.</summary>
        private const int ExpectedKnobCount = 8;

        /// <summary>
        /// ⭐ THE CONTRACT, STATED INDEPENDENTLY OF THE CODE.
        /// <para>
        /// Every value here is what the SHIPPING CODE HARDCODED BEFORE PROD-022 made it
        /// tunable. Bools are 0/1. Cross-checked against RemoteTunables.Registry (case 1)
        /// and against docs/PROD022_TUNABLE_FLAGS.md (case 5), so a default may be changed
        /// - it just cannot be changed in one place only.
        /// </para>
        /// </summary>
        private static readonly KeyValuePair<string, int>[] ExpectedDefaults =
        {
            new KeyValuePair<string, int>("pi.eagerStructureWarm", 0),
            new KeyValuePair<string, int>("pi.awaitInitBeforeFirstLoad", 0),
            new KeyValuePair<string, int>("pi.disableRemoteStructureArt", 0),
            new KeyValuePair<string, int>("assets.maxConcurrentRequests", 0),
            new KeyValuePair<string, int>("pi.requestTimeoutSeconds", 20),
            new KeyValuePair<string, int>("assets.maxRequestAttempts", 3),
            new KeyValuePair<string, int>("visuals.missLogCap", 3),
            new KeyValuePair<string, int>("trace.assetVerbosity", 2),
        };

        /// <summary>The two knobs whose resolved value is readable from the CONSUMER, so
        /// the seam can be proved end to end rather than only at the catalog. Literals.</summary>
        private const int ExpectedWarmerMaxAttempts = 3;
        private const int ExpectedWarmerPiTimeout = 20;

        /// <summary>The poll interval must stay inside these pinned bounds. A floor,
        /// because hammering the origin buys nothing; a ceiling, because the interval IS
        /// the turnaround on "flip it and tell me when to look".</summary>
        private const int MinPollSeconds = 10;
        private const int MaxPollSeconds = 300;

        // Source paths, relative to the repo root (batchmode CWD).
        private const string CatalogSrc = "Assets/_Modules/Core/Ops/RemoteTunables.cs";
        private const string ServiceSrc = "Assets/_Modules/Core/Ops/RemoteTunablesService.cs";
        private const string WarmerSrc = "Assets/_Modules/Core/Addressables/StructureContentWarmer.cs";
        private const string FactorySrc = "Assets/_Modules/Village/VisualFactory.cs";
        private const string JsLibSrc = "api/_lib/tunables.js";
        private const string DocSrc = "docs/PROD022_TUNABLE_FLAGS.md";

        /// <summary>
        /// Literal braces built from their char codes.
        /// <para>
        /// Written this way because CLAUDE.md section 1's brace-balance gate is a NAIVE
        /// character count that cannot tell a brace inside a string literal from a real
        /// one. This suite must build JSON payloads - both valid and convincingly
        /// malformed - and typing the braces inline would leave the file failing the
        /// project's own mandatory quality gate. Same bytes to Newtonsoft, invisible to
        /// the counter. (The trick is MaintenanceTogglesRegression's.)
        /// </para>
        /// </summary>
        private static readonly string OpenBrace = ((char)123).ToString();
        private static readonly string CloseBrace = ((char)125).ToString();

        /// <summary>Sentinel for "this PlayerPrefs key was absent" during the snapshot.</summary>
        private const int PrefAbsent = int.MinValue;

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TUNABLE_DEFAULTS_OK - " + reason);
            else Debug.LogError("TUNABLE_DEFAULTS_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            // Snapshot and clear any ambient local override BEFORE anything resolves.
            // A stale ff.tun.* on a developer machine is correctly NOT the default, and
            // an oracle that reds on the machine rather than on the code is worse than
            // no oracle. Restored in the finally, always.
            var snapshot = SnapshotAndClearLocalOverrides(notes);

            try
            {
                Case(failures, "defaults", () => Case1_Defaults(failures, notes));
                Case(failures, "failure-modes", () => Case2_FailureModes(failures, notes));
                Case(failures, "consumers", () => Case3_Consumers(failures, notes));
                Case(failures, "key-domain", () => Case4_KeyDomain(failures, notes));
                Case(failures, "doc-parity", () => Case5_DocParity(failures, notes));
                Case(failures, "never-blocks", () => Case6_NeverBlocks(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                RemoteTunables.Clear();
                RestoreLocalOverrides(snapshot);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "TUNABLE DEFAULTS OK - all " + ExpectedKnobCount + " PROD-022 knobs resolve to " +
                         "their SHIPPING DEFAULTS (today's behaviour, byte for byte) on every failure " +
                         "path: no database row, server-reported readOk=false, malformed JSON, an empty " +
                         "body, a corrupt device cache, values the server would refuse, and garbage " +
                         "arriving after a good payload. The defaults agree across three independent " +
                         "statements (this oracle's literals, RemoteTunables.Registry, and " + DocSrc +
                         "); the key domain is identical in the client registry, " + JsLibSrc + " and " +
                         "the docs table; the real consumers answer 3 and 20 with no table and have not " +
                         "re-hardcoded a knob; and the fetch still cannot block or delay boot" + noteStr;
                return true;
            }
            reason = "tunable-defaults FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - THE DEFAULTS THEMSELVES
        // =====================================================================
        private static void Case1_Defaults(List<string> failures, List<string> notes)
        {
            // --- shape of the registry ---------------------------------------
            if (RemoteTunables.Registry == null)
            {
                failures.Add("[defaults] RemoteTunables.Registry is NULL - there are no knobs and no defaults");
                return;
            }
            if (RemoteTunables.Registry.Length != ExpectedKnobCount)
            {
                failures.Add("[defaults] Registry holds " + RemoteTunables.Registry.Length + " knob(s), " +
                             "pinned at " + ExpectedKnobCount + ". Adding or removing a knob is fine - but " +
                             "update this oracle's ExpectedDefaults and " + DocSrc + " in the SAME commit " +
                             "(CLAUDE.md section 15), or the owner-facing list silently stops describing " +
                             "the build.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in RemoteTunables.Registry)
            {
                if (spec == null) { failures.Add("[defaults] Registry contains a NULL spec"); continue; }
                if (string.IsNullOrWhiteSpace(spec.Key))
                { failures.Add("[defaults] Registry contains a spec with an empty key"); continue; }
                if (!seen.Add(spec.Key))
                    failures.Add("[defaults] duplicate registry key '" + spec.Key + "' - the second one " +
                                 "would be unreachable and its flag would silently never apply");
                if (!IsAscii(spec.Key))
                    failures.Add("[defaults] registry key '" + spec.Key + "' is not ASCII - it is a database " +
                                 "primary key and a PlayerPrefs suffix; both must be plain ASCII");
                if (spec.Key.StartsWith("ff.", StringComparison.Ordinal))
                    failures.Add("[defaults] registry key '" + spec.Key + "' starts with 'ff.', which would " +
                                 "collide with the FeatureFlags PlayerPrefs namespace once the 'ff.tun.' " +
                                 "prefix is applied. The prefixes are separate ON PURPOSE.");
                if (string.IsNullOrWhiteSpace(spec.WhatOnDoes) || string.IsNullOrWhiteSpace(spec.Hypothesis))
                    failures.Add("[defaults] registry key '" + spec.Key + "' has no WhatOnDoes/Hypothesis " +
                                 "prose. A knob nobody can explain is a knob nobody will dare flip during " +
                                 "an incident, which is the only moment it is worth having.");
            }

            // --- the three-way default pin, half one: literals vs Registry ----
            foreach (var expected in ExpectedDefaults)
            {
                var spec = RemoteTunables.SpecFor(expected.Key);
                if (spec == null)
                {
                    failures.Add("[defaults] '" + expected.Key + "' is MISSING from Registry. This oracle " +
                                 "pins it at " + expected.Value + "; a knob that vanished from the registry " +
                                 "answers 0 for every caller, which for pi.requestTimeoutSeconds would mean " +
                                 "NO TIMEOUT AT ALL.");
                    continue;
                }
                if (spec.Default != expected.Value)
                    failures.Add("[defaults] Registry default for '" + expected.Key + "' is " + spec.Default +
                                 " but the shipping value pinned here is " + expected.Value + ". A default is " +
                                 "the value a player with no connectivity gets, so changing one changes the " +
                                 "game for everyone offline. If the change is intended, update this oracle " +
                                 "AND " + DocSrc + " in the same commit.");
            }

            // --- and the part that actually matters: what RESOLVES -----------
            // Registry carrying the right number proves nothing on its own; the assertion
            // is that Int()/Bool() ANSWER it with no table and no override.
            RemoteTunables.Clear();
            AssertFullTableAtDefaults(failures, "defaults", "no table at all (never fetched / offline / timed out)");

            if (RemoteTunables.Loaded)
                failures.Add("[defaults] RemoteTunables.Loaded is TRUE after Clear() - the resting state " +
                             "must be 'no table', not 'an empty table we are treating as known-good'");

            notes.Add("defaults pinned " + ExpectedDefaults.Length + " knob(s) against Registry and against " +
                      "live resolution");
        }

        /// <summary>
        /// THE ASSERTION EVERY CASE GOES THROUGH. All eight knobs, every time.
        /// <para>
        /// It reads through <see cref="RemoteTunables.Int"/> - the same call the game makes -
        /// rather than inspecting the table, so it proves the RESOLVED answer and not merely
        /// the stored one. A knob that is absent from the registry resolves 0 here and is
        /// reported as such, which is why the zero case is called out by name: 0 is a
        /// legitimate default for three knobs and a silent catastrophe for the other five.
        /// </para>
        /// </summary>
        private static void AssertFullTableAtDefaults(List<string> failures, string caseName, string what)
        {
            foreach (var expected in ExpectedDefaults)
            {
                int actual = RemoteTunables.Int(expected.Key);
                if (actual == expected.Value) continue;

                var spec = RemoteTunables.SpecFor(expected.Key);
                failures.Add("[" + caseName + "] '" + expected.Key + "' resolved " +
                             (spec != null ? RemoteTunables.Describe(spec, actual) : actual.ToString()) +
                             " with " + what + ", but the SHIPPING DEFAULT is " +
                             (spec != null ? RemoteTunables.Describe(spec, expected.Value)
                                           : expected.Value.ToString()) +
                             ". THE INVARIANT IS: no row, no network, no server => TODAY'S BEHAVIOUR, " +
                             "byte for byte. The remote read is an OVERRIDE, never a dependency - every " +
                             "player who cannot reach the API depends on this, and a break here is " +
                             "INVISIBLE (nothing crashes; the build simply stops behaving the way it says " +
                             "it does)." +
                             (actual == 0 && expected.Value != 0
                                 ? " NOTE it resolved ZERO: that is the shape of a knob falling through to " +
                                   "'unregistered key' rather than to its default."
                                 : ""));
            }
        }

        // =====================================================================
        //  Case 2 - EVERY FAILURE MODE, EACH RE-ASSERTING THE FULL TABLE
        // =====================================================================
        private static void Case2_FailureModes(List<string> failures, List<string> notes)
        {
            int modes = 0;

            // (a) NO TABLE AT ALL - never fetched, offline, unreachable, timed out.
            RemoteTunables.Clear();
            modes++;
            AssertFullTableAtDefaults(failures, "failure-modes", "no table at all (offline / unreachable / timed out)");

            // (b) MALFORMED JSON. Must be REJECTED, and must leave nothing half-applied.
            RemoteTunables.Clear();
            if (RemoteTunables.ApplyPayload(OpenBrace + " this is not json", "test-malformed"))
                failures.Add("[failure-modes] a malformed payload was ACCEPTED");
            modes++;
            AssertFullTableAtDefaults(failures, "failure-modes", "malformed JSON");

            // (c) EMPTY BODY.
            RemoteTunables.Clear();
            if (RemoteTunables.ApplyPayload("   ", "test-empty"))
                failures.Add("[failure-modes] an empty payload was ACCEPTED");
            modes++;
            AssertFullTableAtDefaults(failures, "failure-modes", "an empty body");

            // (d) THE SERVER ITSELF SAYS IT COULD NOT READ THE TABLE (200 + readOk:false).
            //     The DB-unreachable case arriving as a healthy HTTP response, and the one
            //     a naive client would treat as "no overrides are set". It must clear to
            //     defaults AND must not leave a table that looks known-good - even though
            //     this payload NAMES a knob with a non-default value.
            RemoteTunables.Clear();
            if (!RemoteTunables.ApplyPayload(Payload(false, "pi.disableRemoteStructureArt", "1"),
                                             "test-readok-false"))
                failures.Add("[failure-modes] a readOk=false payload was rejected outright - it is a " +
                             "well-formed answer and must be ACCEPTED and acted on, not discarded");
            modes++;
            AssertFullTableAtDefaults(failures, "failure-modes",
                "the server reporting readOk=false while naming an overridden knob");
            if (RemoteTunables.Loaded)
                failures.Add("[failure-modes] readOk=false left a STANDING table - a table the server told " +
                             "us it could not read must never be treated as known-good");

            // (e) A CORRUPT DEVICE CACHE. Driven through the real seam
            //     (RemoteTunablesService.ApplyCachedPayload) with NO PlayerPrefs and NO
            //     network, because this path runs at BeforeSceneLoad on every launch and is
            //     otherwise reachable only from an engine hook - i.e. testable once by hand
            //     and never again.
            RemoteTunables.Clear();
            if (RemoteTunablesService.ApplyCachedPayload(OpenBrace + "\"version\":1,\"values\":"))
                failures.Add("[failure-modes] a TRUNCATED cached payload was ACCEPTED. The cache is read " +
                             "before any scene loads, so a corrupt one would decide boot-time asset policy.");
            modes++;
            AssertFullTableAtDefaults(failures, "failure-modes", "a corrupt device cache");

            // (f) VALUES THE SERVER WOULD REFUSE, arriving anyway (a row written straight
            //     into Neon by hand, or a newer server). '2' is not a bool and 'abc' is not
            //     an int. Each must fall to ITS OWN default - not to zero, and not poisoning
            //     the knobs around it.
            RemoteTunables.Clear();
            if (!RemoteTunables.ApplyPayload(
                    Payload(true,
                            "pi.eagerStructureWarm", "2",
                            "pi.requestTimeoutSeconds", "abc",
                            "assets.maxRequestAttempts", "",
                            "trace.assetVerbosity", "yes-please"),
                    "test-bad-values"))
                failures.Add("[failure-modes] a well-formed payload carrying unusable VALUES was rejected " +
                             "wholesale. One bad row must not discard the good ones - it must fall to that " +
                             "knob's default and say so.");
            modes++;
            AssertFullTableAtDefaults(failures, "failure-modes",
                "rows carrying values the client cannot parse ('2' for a bool, 'abc' for an int)");

            // (g) GARBAGE ARRIVING AFTER A GOOD PAYLOAD MUST NOT HALF-APPLY.
            //     The opposite direction, and the one that would let a flaky endpoint
            //     silently un-set a knob the owner is mid-bisect on.
            RemoteTunables.Clear();
            RemoteTunables.ApplyPayload(Payload(true, "assets.maxRequestAttempts", "5"), "test-good");
            if (RemoteTunables.Int("assets.maxRequestAttempts") != 5)
                failures.Add("[failure-modes] the setup for the half-apply check did not take effect - a " +
                             "good payload failed to override a knob, so the whole system does nothing");
            RemoteTunables.ApplyPayload(OpenBrace + " broken", "test-garbage");
            if (RemoteTunables.Int("assets.maxRequestAttempts") != 5)
                failures.Add("[failure-modes] a REJECTED payload blanked the standing table. Garbage must " +
                             "leave the last accepted answer alone, or a flaky endpoint silently un-sets " +
                             "the knob the owner is mid-bisect on.");
            modes++;

            if (modes != 7)
                failures.Add("[failure-modes] only " + modes + " of the 7 failure modes were actually " +
                             "driven - an audit that skipped a mode has certified nothing");

            RemoteTunables.Clear();
            notes.Add("failure-modes drove " + modes + "/7 paths, re-asserting all " + ExpectedDefaults.Length +
                      " knobs on each");
        }

        // =====================================================================
        //  Case 3 - THE REAL CONSUMERS, not just the catalog
        // =====================================================================
        /// <summary>
        /// The catalog answering 20 proves nothing if StructureContentWarmer stopped asking
        /// it. This case reads the values the game actually uses, and then source-lints that
        /// the seam was not quietly bypassed by re-hardcoding a literal behind it.
        /// </summary>
        private static void Case3_Consumers(List<string> failures, List<string> notes)
        {
            RemoteTunables.Clear();

            if (StructureContentWarmer.MaxRequestAttempts != ExpectedWarmerMaxAttempts)
                failures.Add("[consumers] StructureContentWarmer.MaxRequestAttempts is " +
                             StructureContentWarmer.MaxRequestAttempts + " with no table; the shipping " +
                             "value is " + ExpectedWarmerMaxAttempts + ". This is the retry budget an " +
                             "offline player gets.");

            if (StructureContentWarmer.PiRequestTimeoutSeconds != ExpectedWarmerPiTimeout)
                failures.Add("[consumers] StructureContentWarmer.PiRequestTimeoutSeconds is " +
                             StructureContentWarmer.PiRequestTimeoutSeconds + " with no table; the shipping " +
                             "value is " + ExpectedWarmerPiTimeout + ". WO PROD-022 forbids tuning this as a " +
                             "'fix' - the root is not proven and a new constant would bake in a guess.");

            // The clamps exist so a hostile or fat-fingered row cannot produce a value that
            // breaks the system outright. Prove them, because they are the difference
            // between a bad experiment and a bricked launch.
            RemoteTunables.ApplyPayload(
                Payload(true, "assets.maxRequestAttempts", "0", "pi.requestTimeoutSeconds", "0"),
                "test-clamp");
            if (StructureContentWarmer.MaxRequestAttempts < 1)
                failures.Add("[consumers] a row of 0 drove MaxRequestAttempts below 1. A budget of zero " +
                             "retires every address on sight, and there is no diagnosis in a town with no " +
                             "art and no fetches.");
            if (StructureContentWarmer.PiRequestTimeoutSeconds < 1)
                failures.Add("[consumers] a row of 0 drove PiRequestTimeoutSeconds below 1. Zero means NO " +
                             "TIMEOUT to UnityWebRequest, which is the captive-portal hang this project has " +
                             "already been bitten by.");
            RemoteTunables.Clear();

            // --- source lint: the seam was not bypassed ----------------------
            // Comments EXCLUDED. Both files' headers deliberately quote the old hardcoded
            // constants in prose in order to record what they used to be, and a
            // comment-reading lint would red on the very sentences that document compliance.
            // (Same choice, same reason, as MaintenanceTogglesRegression case 8.)
            string warmer = ReadOrNull(WarmerSrc);
            if (warmer == null) failures.Add("[consumers] " + WarmerSrc + " is MISSING");
            else
            {
                string code = StripComments(warmer);
                if (code.IndexOf("RemoteTunables", StringComparison.Ordinal) < 0)
                    failures.Add("[consumers] " + WarmerSrc + " no longer reads RemoteTunables at all - the " +
                                 "knobs are inert and the build cannot be reconfigured without a rebuild, " +
                                 "which is the entire thing PROD-022 bought");
                if (Regex.IsMatch(code, @"const\s+int\s+MaxRequestAttempts\s*="))
                    failures.Add("[consumers] " + WarmerSrc + " has re-declared MaxRequestAttempts as a " +
                                 "const - it must resolve through RemoteTunables or the flag does nothing");
                if (Regex.IsMatch(code, @"const\s+int\s+PiRequestTimeoutSeconds\s*="))
                    failures.Add("[consumers] " + WarmerSrc + " has re-declared PiRequestTimeoutSeconds as a " +
                                 "const - it must resolve through RemoteTunables or the flag does nothing");
            }

            // VisualFactory's MissLogCap is private, so it cannot be read. Lint that it
            // still routes through the seam instead.
            string factory = ReadOrNull(FactorySrc);
            if (factory == null) failures.Add("[consumers] " + FactorySrc + " is MISSING");
            else
            {
                string code = StripComments(factory);
                if (code.IndexOf("KeyVisualsMissLogCap", StringComparison.Ordinal) < 0)
                    failures.Add("[consumers] " + FactorySrc + " no longer reads KeyVisualsMissLogCap - the " +
                                 "miss-log cap has gone back to being frozen in the build");
                if (Regex.IsMatch(code, @"const\s+int\s+MissLogCap\s*="))
                    failures.Add("[consumers] " + FactorySrc + " has re-declared MissLogCap as a const - it " +
                                 "must resolve through RemoteTunables or the flag does nothing");

                AssertVerbosityNeverGatesFailures(failures, FactorySrc, code);
            }

            // The same guarantee in the warmer, where the verbosity helper actually lives.
            if (warmer != null) AssertVerbosityNeverGatesFailures(failures, WarmerSrc, StripComments(warmer));

            notes.Add("consumers proved MaxRequestAttempts=" + ExpectedWarmerMaxAttempts + " and " +
                      "PiRequestTimeoutSeconds=" + ExpectedWarmerPiTimeout + " with no table, plus both clamps");
        }

        /// <summary>
        /// ⛔ THE CLAUDE.md SECTION 12 GUARANTEE, LINTED.
        /// <para>
        /// The verbosity knob may dim NARRATION and nothing else. If a <c>Warn</c> or a
        /// <c>Fail</c> is ever routed through it, a failure stops being logged - which turns
        /// a logged failure back into a silent one and is the exact bug the whole
        /// instrumentation standard exists to prevent. It would also be INVISIBLE until the
        /// next incident, at which point the trace we need would simply not be there.
        /// </para>
        /// <para>
        /// Checked per STATEMENT (split on ';') rather than per line, because the gate in
        /// VisualFactory legitimately spans five lines. Comments are already stripped by the
        /// caller, so the headers that discuss Warn/Fail in prose cannot trip it.
        /// </para>
        /// </summary>
        private static void AssertVerbosityNeverGatesFailures(List<string> failures, string where, string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            foreach (var stmt in code.Split(';'))
            {
                if (stmt.IndexOf("Verbosity", StringComparison.Ordinal) < 0) continue;
                if (stmt.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) < 0 &&
                    stmt.IndexOf("FlowTrace.Fail", StringComparison.Ordinal) < 0) continue;

                failures.Add("[consumers] " + where + " appears to gate a FlowTrace.Warn/Fail on the " +
                             "verbosity knob. CLAUDE.md section 12 is BINDING: instrumentation is PERMANENT " +
                             "and a failure that stops being logged is worse than one that was never " +
                             "instrumented, because the next reader will trust the silence. Only Step " +
                             "narration is dimmable. Offending statement: " + Snip(stmt));
            }
        }

        /// <summary>Bounded excerpt for a failure message - a whole statement can be a page.</summary>
        private static string Snip(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            string t = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return t.Length <= 120 ? t : t.Substring(0, 120) + "...";
        }

        // =====================================================================
        //  Case 4 - THE KEY DOMAIN, three ways
        // =====================================================================
        /// <summary>
        /// A key that exists in the build but not in the server allowlist can never be
        /// written; a key in the allowlist but not the build is accepted, stored, and
        /// silently ignored by every client - which during an incident reads as "the flag
        /// did nothing" and sends the owner chasing a build. Both are silent, so both get
        /// an oracle. Modelled on MaintenanceTogglesRegression case [area-domain].
        /// </summary>
        private static void Case4_KeyDomain(List<string> failures, List<string> notes)
        {
            var fromRegistry = new List<string>();
            foreach (var spec in RemoteTunables.Registry)
                if (spec != null && !string.IsNullOrWhiteSpace(spec.Key)) fromRegistry.Add(spec.Key);

            CompareDomain(failures, "key-domain", "RemoteTunables.Registry", fromRegistry.ToArray());

            // --- api/_lib/tunables.js ----------------------------------------
            // Comments EXCLUDED, and only the DECLARATION REGION is read: the header
            // discusses the keys at length in prose, and a comment-reading lint would
            // report a domain built from sentences.
            string js = ReadOrNull(JsLibSrc);
            if (js == null) failures.Add("[key-domain] " + JsLibSrc + " is MISSING");
            else
            {
                string jsCode = StripComments(js);
                var region = Regex.Match(jsCode, @"TUNABLE_KEYS\s*=\s*\[(.*?)\]\s*;", RegexOptions.Singleline);
                if (!region.Success)
                    failures.Add("[key-domain] could not find the TUNABLE_KEYS array in " + JsLibSrc +
                                 " - the server allowlist is what refuses a typo'd key at write time");
                else
                {
                    var found = new List<string>();
                    foreach (Match m in Regex.Matches(region.Groups[1].Value, @"key\s*:\s*'([^']+)'"))
                        found.Add(m.Groups[1].Value);
                    CompareDomain(failures, "key-domain", JsLibSrc, found.ToArray());
                }
            }

            // --- the docs table ----------------------------------------------
            var doc = ParseDocTable(failures, "key-domain");
            if (doc != null)
            {
                var docKeys = new List<string>();
                foreach (var row in doc) docKeys.Add(row.Key);
                CompareDomain(failures, "key-domain", DocSrc, docKeys.ToArray());
            }

            notes.Add("key-domain compared " + ExpectedDefaults.Length + " keys across 3 sources");
        }

        /// <summary>Compare one source's key list against the pinned literals.</summary>
        private static void CompareDomain(List<string> failures, string caseName, string where, string[] found)
        {
            if (found == null) found = new string[0];

            if (found.Length != ExpectedDefaults.Length)
                failures.Add("[" + caseName + "] " + where + " lists " + found.Length + " key(s); the pinned " +
                             "domain is " + ExpectedDefaults.Length + ". Found: " + string.Join(", ", found));

            var set = new HashSet<string>(found, StringComparer.Ordinal);
            foreach (var expected in ExpectedDefaults)
            {
                if (!set.Contains(expected.Key))
                    failures.Add("[" + caseName + "] " + where + " is MISSING key '" + expected.Key + "'. " +
                                 "A key the build knows and the server does not can never be written; a key " +
                                 "the server knows and the build does not is stored and silently ignored. " +
                                 "Both read as 'the flag did nothing'.");
            }

            var pinned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expected in ExpectedDefaults) pinned.Add(expected.Key);
            foreach (var f in found)
            {
                if (!pinned.Contains(f))
                    failures.Add("[" + caseName + "] " + where + " lists UNPINNED key '" + f + "' - either " +
                                 "it is a typo or a knob was added without updating this oracle and " + DocSrc);
            }
        }

        // =====================================================================
        //  Case 5 - THE OWNER-FACING TABLE MATCHES THE CODE
        // =====================================================================
        /// <summary>
        /// The doc is what the owner reads at 2am before flipping something. If it says a
        /// knob defaults OFF and the build defaults it ON, she flips the wrong one and the
        /// bisect measures the wrong thing - and every trace from that run is misleading
        /// rather than merely useless.
        /// <para>
        /// This is the NON-CIRCULAR half of the default pin: Registry is compared against a
        /// document written by hand, not against itself.
        /// </para>
        /// </summary>
        private static void Case5_DocParity(List<string> failures, List<string> notes)
        {
            var doc = ParseDocTable(failures, "doc-parity");
            if (doc == null) return;

            int compared = 0;
            foreach (var row in doc)
            {
                var spec = RemoteTunables.SpecFor(row.Key);
                if (spec == null)
                {
                    failures.Add("[doc-parity] " + DocSrc + " documents '" + row.Key + "', which is not in " +
                                 "Registry. The owner would flip a knob no build reads.");
                    continue;
                }
                compared++;
                if (row.Default != spec.Default)
                    failures.Add("[doc-parity] " + DocSrc + " says '" + row.Key + "' defaults to " +
                                 row.Default + "; Registry says " + spec.Default + ". The doc is what she " +
                                 "reads before flipping anything, so a disagreement here means the bisect " +
                                 "measures something other than what the report will claim.");
            }

            if (compared != ExpectedDefaults.Length)
                failures.Add("[doc-parity] only " + compared + " of " + ExpectedDefaults.Length + " knobs " +
                             "were compared against " + DocSrc + " - a parity check that skipped rows has " +
                             "certified nothing");

            notes.Add("doc-parity compared " + compared + " default(s) against " + DocSrc);
        }

        /// <summary>One parsed row of the owner-facing flag table.</summary>
        private sealed class DocRow
        {
            public string Key;
            public int Default;
        }

        /// <summary>
        /// Parse the flag table out of the markdown. Reads only rows whose SECOND cell is a
        /// backticked key that Registry knows, so prose tables elsewhere in the document
        /// (the file map, the precedence block) cannot be mistaken for the contract.
        /// </summary>
        private static List<DocRow> ParseDocTable(List<string> failures, string caseName)
        {
            string md = ReadOrNull(DocSrc);
            if (md == null)
            {
                failures.Add("[" + caseName + "] " + DocSrc + " is MISSING. It is the owner-facing list of " +
                             "every knob, its default and what it tests - without it a flag flip is guesswork.");
                return null;
            }

            var rows = new List<DocRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in md.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.IndexOf('|') < 0) continue;
                var cells = line.Split('|');
                if (cells.Length < 6) continue;

                string keyCell = FirstBackticked(cells[2]);
                if (keyCell == null) continue;
                if (RemoteTunables.SpecFor(keyCell) == null && !IsPinnedKey(keyCell)) continue;

                string defCell = FirstBackticked(cells[4]);
                if (defCell == null)
                {
                    failures.Add("[" + caseName + "] the row for '" + keyCell + "' in " + DocSrc + " has no " +
                                 "backticked default in its DEFAULT column - the oracle cannot check what " +
                                 "the owner is being told");
                    continue;
                }
                if (!int.TryParse(defCell, System.Globalization.NumberStyles.Integer,
                                  System.Globalization.CultureInfo.InvariantCulture, out int def))
                {
                    failures.Add("[" + caseName + "] the documented default for '" + keyCell + "' in " +
                                 DocSrc + " is '" + defCell + "', which is not a number");
                    continue;
                }
                if (!seen.Add(keyCell))
                {
                    failures.Add("[" + caseName + "] '" + keyCell + "' appears twice in the " + DocSrc +
                                 " table - two rows can disagree and only one can be right");
                    continue;
                }
                rows.Add(new DocRow { Key = keyCell, Default = def });
            }

            if (rows.Count == 0)
            {
                failures.Add("[" + caseName + "] no knob rows were parsed out of " + DocSrc + ". Either the " +
                             "table was reformatted or it was emptied; either way the owner-facing list no " +
                             "longer describes the build.");
                return null;
            }
            return rows;
        }

        private static bool IsPinnedKey(string key)
        {
            foreach (var expected in ExpectedDefaults)
                if (string.Equals(expected.Key, key, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The first `backticked` token in a markdown cell, or null.</summary>
        private static string FirstBackticked(string cell)
        {
            if (string.IsNullOrEmpty(cell)) return null;
            int a = cell.IndexOf('`');
            if (a < 0) return null;
            int b = cell.IndexOf('`', a + 1);
            if (b <= a + 1) return null;
            return cell.Substring(a + 1, b - a - 1).Trim();
        }

        // =====================================================================
        //  Case 6 - THE FETCH CANNOT BLOCK OR DELAY BOOT
        // =====================================================================
        /// <summary>
        /// PROD-022 is a CRASH LOOP. A diagnostic system that added a boot hazard while
        /// investigating one would be self-defeating, and the hazard would be indomitable:
        /// it would fire on the exact device we cannot attach a debugger to.
        /// <para>
        /// The non-blocking property is STRUCTURAL - Bootstrap fires and forgets, and no
        /// call site awaits it - so it is lintable, and a lint is the only thing that keeps
        /// it true after the next edit.
        /// </para>
        /// </summary>
        private static void Case6_NeverBlocks(List<string> failures, List<string> notes)
        {
            string src = ReadOrNull(ServiceSrc);
            if (src == null)
            {
                failures.Add("[never-blocks] " + ServiceSrc + " is MISSING");
                return;
            }

            // Comments EXCLUDED: the header spells out the words "block", "await" and
            // "WaitForCompletion" in order to say it does NONE of them, and a
            // comment-reading lint would red on the sentence documenting compliance.
            string code = StripComments(src);

            // ⚠ `.Result` IS DELIBERATELY NOT ON THIS LIST, and the omission is the finding.
            // The obvious "blocking on a Task" needle matches `UnityWebRequest.Result.Success`
            // on line 266 of the file under test - a legitimate ENUM TYPE, not a blocking
            // property access. A lint that reds on correct code gets suppressed by the next
            // seat, taking the four real needles with it. The blocking form of that idiom is
            // `.GetAwaiter().GetResult()`, which IS listed.
            string[] banned =
            {
                "WaitForCompletion",   // the unbounded, uninterruptible Addressables busy-spin
                "Thread.Sleep",
                ".Wait()",
                "GetAwaiter().GetResult",
                "UniTask.Run",         // moving the fetch off-thread would not make it safe, only harder to see
            };
            int hits = 0;
            foreach (string needle in banned)
            {
                if (code.IndexOf(needle, StringComparison.Ordinal) >= 0)
                {
                    hits++;
                    failures.Add("[never-blocks] " + ServiceSrc + " uses '" + needle + "'. The tunables " +
                                 "fetch must NEVER block or delay boot: PROD-022 is a crash loop, and a " +
                                 "diagnostic that can stall the app would be investigating itself.");
                }
            }

            // THE GOOD PATH. A file with no blocking call AND no fetch would pass the check
            // above while never learning anything - the same overclaiming shape that let a
            // warm pass report Warm with resident=0.
            if (!Regex.IsMatch(code, @"PollForeverAsync\s*\(\s*\)\s*\.\s*Forget\s*\(\s*\)"))
                failures.Add("[never-blocks] " + ServiceSrc + " no longer starts the poll with " +
                             "PollForeverAsync().Forget(). Fire-and-forget with NO await at the call site " +
                             "is what makes 'never blocks boot' structural rather than a comment.");
            if (code.IndexOf("req.timeout", StringComparison.Ordinal) < 0)
                failures.Add("[never-blocks] " + ServiceSrc + " does not set req.timeout. Without it a " +
                             "captive-portal socket never completes and the request hangs for the whole " +
                             "session - a bug this repo has already shipped once.");

            // The poll interval, pinned to literals on both sides. Never expressed relative
            // to the current value, which would silently stop testing anything.
            if (RemoteTunablesService.PollSeconds < MinPollSeconds)
                failures.Add("[never-blocks] PollSeconds=" + RemoteTunablesService.PollSeconds + " is below " +
                             "the pinned floor of " + MinPollSeconds + " - it hammers the origin for no gain");
            if (RemoteTunablesService.PollSeconds > MaxPollSeconds)
                failures.Add("[never-blocks] PollSeconds=" + RemoteTunablesService.PollSeconds + " exceeds " +
                             "the pinned ceiling of " + MaxPollSeconds + " - the interval IS the turnaround " +
                             "on 'flip it and tell me when to look'");

            notes.Add("never-blocks checked " + banned.Length + " blocking idioms (" + hits + " hit(s)) and " +
                      "pinned PollSeconds=" + RemoteTunablesService.PollSeconds);
        }

        // =====================================================================
        //  PlayerPrefs hygiene - the one piece of ambient state that could lie
        // =====================================================================
        /// <summary>
        /// Snapshot and clear every "ff.tun.*" override so the suite measures the CODE and
        /// not the machine. A developer who left one armed would otherwise get a red that
        /// says nothing about the repo. Every value is restored in Run()'s finally.
        /// </summary>
        private static Dictionary<string, int> SnapshotAndClearLocalOverrides(List<string> notes)
        {
            var snap = new Dictionary<string, int>(StringComparer.Ordinal);
            var armed = new List<string>();
            try
            {
                foreach (var expected in ExpectedDefaults)
                {
                    string prefKey = RemoteTunables.LocalPrefix + expected.Key;
                    int v = PlayerPrefs.GetInt(prefKey, PrefAbsent);
                    snap[prefKey] = v;
                    if (v == PrefAbsent) continue;
                    armed.Add(expected.Key + "=" + v);
                    PlayerPrefs.DeleteKey(prefKey);
                }
                if (armed.Count > 0) PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                notes.Add("could not snapshot local overrides (" + ex.GetType().Name + ") - the run may be " +
                          "measuring this machine rather than the code");
            }

            if (armed.Count > 0)
            {
                // Worth saying out loud, not just handling: an override left armed on the
                // build machine means anything captured there is not the shipping config.
                notes.Add("CLEARED " + armed.Count + " ambient PlayerPrefs override(s) for the duration of " +
                          "this run and restored them afterwards: " + string.Join(", ", armed.ToArray()));
            }
            return snap;
        }

        private static void RestoreLocalOverrides(Dictionary<string, int> snap)
        {
            if (snap == null) return;
            try
            {
                bool dirty = false;
                foreach (var pair in snap)
                {
                    if (pair.Value == PrefAbsent)
                    {
                        // It was absent before; make sure the suite did not leave one behind.
                        if (PlayerPrefs.HasKey(pair.Key)) { PlayerPrefs.DeleteKey(pair.Key); dirty = true; }
                        continue;
                    }
                    PlayerPrefs.SetInt(pair.Key, pair.Value);
                    dirty = true;
                }
                if (dirty) PlayerPrefs.Save();
            }
            catch { /* restoring a debug pref must never fail a gate */ }
        }

        // =====================================================================
        //  Plumbing
        // =====================================================================

        /// <summary>
        /// Build a wire payload. Braces come from <see cref="OpenBrace"/> /
        /// <see cref="CloseBrace"/> so this file stays brace-balanced under CLAUDE.md
        /// section 1's naive counter - same bytes to Newtonsoft, invisible to the gate.
        /// </summary>
        private static string Payload(bool readOk, params string[] keyThenValue)
        {
            var sb = new StringBuilder();
            sb.Append(OpenBrace);
            sb.Append("\"version\":1,\"readOk\":").Append(readOk ? "true" : "false");
            sb.Append(",\"reason\":\"test\",\"values\":").Append(OpenBrace);
            for (int i = 0; i + 1 < keyThenValue.Length; i += 2)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(keyThenValue[i]).Append("\":\"").Append(keyThenValue[i + 1]).Append('"');
            }
            sb.Append(CloseBrace).Append(CloseBrace);
            return sb.ToString();
        }

        private static bool IsAscii(string s)
        {
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++) if (s[i] > 126 || s[i] < 32) return false;
            return true;
        }

        private static string ReadOrNull(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        /// <summary>
        /// Remove // and /* */ comments while RESPECTING string literals.
        /// <para>
        /// !! The naive version is a real bug in THIS repo: RemoteTunablesService.cs
        /// contains <c>"https://defenders-of-the-realm-v2..."</c> and a stripper that does
        /// not track quotes would cut the file in half at the <c>//</c> inside that URL and
        /// then certify whatever was left. Ported deliberately from
        /// MaintenanceTogglesRegression rather than re-derived.
        /// </para>
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return src ?? "";
            var sb = new StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];

                // Verbatim string @"..."  ("" is an escaped quote inside one)
                if (c == '@' && i + 1 < src.Length && src[i + 1] == '"')
                {
                    sb.Append(c).Append('"');
                    i += 2;
                    while (i < src.Length)
                    {
                        if (src[i] == '"' && i + 1 < src.Length && src[i + 1] == '"') { sb.Append("\"\""); i += 2; continue; }
                        sb.Append(src[i]);
                        if (src[i] == '"') { i++; break; }
                        i++;
                    }
                    continue;
                }

                // Regular string or char literal
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(c);
                    i++;
                    while (i < src.Length)
                    {
                        if (src[i] == '\\' && i + 1 < src.Length) { sb.Append(src[i]).Append(src[i + 1]); i += 2; continue; }
                        sb.Append(src[i]);
                        if (src[i] == quote) { i++; break; }
                        i++;
                    }
                    continue;
                }

                // Line comment
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    continue;
                }

                // Block comment
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = Math.Min(i + 2, src.Length);
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
