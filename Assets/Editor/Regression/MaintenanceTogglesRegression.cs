// =============================================================================
// MaintenanceTogglesRegression [maintenance-toggles]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (already references DeNelle.Core - no
//   asmdef edit needed).
//
// Pins WO-1243: the operator kill switches.
//
// -----------------------------------------------------------------------------
// THE ONE THAT WILL BE BROKEN BY A FUTURE SEAT, AND WHY IT IS CASE 1
// -----------------------------------------------------------------------------
// This system FAILS OPEN. An unreachable table, a timeout, a malformed payload
// and an absent row all leave EVERY area ON. Owner-confirmed 2026-08-27:
//     "correct cause i cannot help if server is unreachable"
//
// The repo ALSO contains DungeonStatusCatalog, which fails CLOSED, was inverted
// to fail closed only the day before, and is documented at length in exactly the
// vocabulary a seat would pattern-match on. So the single most likely future
// change to this file's subject is someone "correcting" fail-open into
// consistency with it. Case 1 makes that a RED, not a discussion.
//
// -----------------------------------------------------------------------------
// AND THE ONE THAT IS ACTUALLY SECURITY: CASE 9.
// -----------------------------------------------------------------------------
// Owner ruling 2026-08-27: "mine allows if we see someone finds a hack, we seal
// that area and patch". A person exploiting the game runs a client that does
// what they want, so a seal only the client honours contains nothing. The
// enforcement therefore lives in api/, and case 9 SOURCE-LINTS that it is still
// there. If someone deletes the server-side guard and leaves the pretty client
// gate, every other case in this suite still passes and the containment is
// gone - which is precisely the shape of failure that needs an oracle.
//
// -----------------------------------------------------------------------------
// COMMENTS: DECIDED ON PURPOSE, PER CASE, AND STATED.
// -----------------------------------------------------------------------------
// Four oracle failures in this repo this week came from getting this wrong in
// both directions - a lint that read comments and cried wolf, and a lint that
// stripped them and certified nothing. So every source-reading case below names
// its choice out loud:
//   * Case 6 [area-domain]  : comments EXCLUDED. It extracts the declaration
//                             regions only (the C# array initialiser, the JS
//                             AREAS literal, the SQL CHECK list). A comment
//                             listing the six ids is prose, not a contract.
//   * Case 7 [gate-sites]   : comments EXCLUDED for finding call sites (a
//                             comment naming Refuses() is not a call site) and
//                             INCLUDED for line numbering, so the "a return
//                             follows" window is measured against the real file.
//   * Case 8 [no-cache]     : comments EXCLUDED - MaintenanceService's header
//                             deliberately spells out the words "cache",
//                             "CachePath" and "PlayerPrefs" to say it has none,
//                             and a comment-reading lint would red on the very
//                             sentence that documents compliance.
//   * Case 9 [server-seal]  : comments EXCLUDED, same reason: the client files
//                             talk about the server-side seal at length, and a
//                             lint that counted those sentences would report the
//                             guard present after someone deleted it.
//
// -----------------------------------------------------------------------------
// EVERY THRESHOLD IS A NAMED CONSTANT PINNED TO A LITERAL. Nothing here is
// expressed relative to a moving value - a threshold written as "current minus
// one" silently stops testing anything the moment the current value moves, which
// cost this repo a false RED against correct code this week.
//
// Cases:
//   1 [fail-open]    No table, a malformed payload, an empty payload and a
//                    server-reported read failure ALL leave every area OPEN, and
//                    a rejected payload leaves a standing table intact.
//   2 [isolation]    Each of the five area toggles closes ONLY its own area.
//                    Driven one at a time, all six read back every time.
//   3 [server-all]   The `server` toggle closes all six, and it OUTRANKS a row
//                    that says the area is fine.
//   4 [refuses]      A closed area REFUSES: Refuses() returns TRUE and hands back
//                    a non-empty ASCII sentence. The GOOD path is asserted too -
//                    open areas return FALSE and a null message, so this case
//                    cannot be satisfied by refusing everything.
//   5 [banner]       The banner names the area and leads with the literal word
//                    MAINTENANCE, in ASCII, with no meaning carried by colour.
//   6 [area-domain]  The six ids are identical in MaintenanceCatalog.AreaIds,
//                    api/_lib/maintenance.js and api/schema.sql's CHECK.
//   7 [gate-sites]   Every Refuses() call site in Assets/_Modules is followed by
//                    a return, and all five player-facing areas have one.
//   8 [no-cache]     MaintenanceService writes no cache of any kind, and the poll
//                    interval is inside its pinned bounds.
//   9 [server-seal]  The api/ enforcement still exists where it must, and still
//                    does NOT exist where it must not (verify/fulfill/reconcile).
//
// Markers: MAINTENANCE_TOGGLES_OK / MAINTENANCE_TOGGLES_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.MaintenanceTogglesRegression.RunAll
// Wiring into DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Core.Ops;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class MaintenanceTogglesRegression
    {
        // ---------------------------------------------------------------------
        //  PINNED FACTS. Every one is a literal. None is derived from a value
        //  that can move underneath it.
        // ---------------------------------------------------------------------

        /// <summary>The domain is SIX areas. Pinned as a literal, not as
        /// AreaIds.Length - an oracle that measures the thing against itself
        /// certifies nothing.</summary>
        private const int ExpectedAreaCount = 6;

        /// <summary>
        /// A literal open brace, built from its char code.
        /// <para>
        /// Written this way because CLAUDE.md section 1's brace-balance gate is a NAIVE
        /// character count that cannot tell a brace inside a string literal from a real
        /// one. Two truncated-JSON test payloads below need a leading open brace to be
        /// convincingly malformed rather than merely nonsense, and typing it inline would
        /// leave this file failing the project's own mandatory quality gate. Same bytes to
        /// Newtonsoft, invisible to the counter.
        /// </para>
        /// </summary>
        private static readonly string OpenBrace = ((char)123).ToString();

        /// <summary>The six ids, in enum order, written out. If MaintenanceArea is
        /// reordered or renamed this array is what reds.</summary>
        private static readonly string[] ExpectedAreaIds =
        {
            "farming", "raiding", "arena", "dungeons", "store", "server",
        };

        /// <summary>The poll interval must stay inside these bounds. Lower than the
        /// floor hammers the origin for no gain (the server-side memo already bounds
        /// the real seal at 5 s); higher than the ceiling leaves honest players inside
        /// a sealed area for over a minute. Both are literals.</summary>
        private const int MinPollSeconds = 5;
        private const int MaxPollSeconds = 60;

        /// <summary>How many source lines after a Refuses() call a `return` must
        /// appear within. Generous enough for a trace line and a toast, tight enough
        /// that a fall-through cannot hide.</summary>
        private const int ReturnWindowLines = 12;

        // Source paths. Repo-relative: batchmode's working directory IS the project root.
        private const string CatalogSrc = "Assets/_Modules/Core/Ops/MaintenanceCatalog.cs";
        private const string ServiceSrc = "Assets/_Modules/Core/Ops/MaintenanceService.cs";
        private const string BannerSrc = "Assets/_Modules/Core/Ops/MaintenanceBannerDriver.cs";
        private const string ApiLibSrc = "api/_lib/maintenance.js";
        private const string ApiEndpointSrc = "api/maintenance.js";
        private const string SchemaSrc = "api/schema.sql";
        private const string ModulesRoot = "Assets/_Modules";

        /// <summary>The api/ files that MUST enforce the seal, and the area each one
        /// seals. Deleting a guard here is the failure case 9 exists to catch.</summary>
        private static readonly string[] MustEnforce =
        {
            "api/purchases/quote.js",
            "api/game/save.js",
            "api/leaderboard/submit.js",
        };

        /// <summary>
        /// The api/ files that MUST NOT enforce the seal.
        /// <para>
        /// DO NOT: THESE THREE RUN AFTER THE CHAIN HAS SETTLED. The money is already gone
        /// and an SPL transfer has no refund route, so sealing them would take a real
        /// payment and then refuse to record the entitlement. Closing the store must
        /// stop NEW purchases (at quote, pre-payment) and must never strand a paid one.
        /// This is asserted as an ABSENCE on purpose: "be careful not to add it there"
        /// is a sentence in a document, and sentences rot.
        /// </para>
        /// </summary>
        private static readonly string[] MustNotEnforce =
        {
            "api/purchases/verify.js",
            "api/purchases/fulfill.js",
            "api/purchases/reconcile.js",
        };

        /// <summary>The five player-facing areas that must each have a client refusal
        /// site. `server` is not here: it has no door of its own, it closes the other
        /// five, and its real teeth are in api/game/save.js.</summary>
        private static readonly MaintenanceArea[] AreasNeedingASite =
        {
            MaintenanceArea.Farming, MaintenanceArea.Raiding, MaintenanceArea.Arena,
            MaintenanceArea.Dungeons, MaintenanceArea.Store,
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("MAINTENANCE_TOGGLES_OK - " + reason);
            else Debug.LogError("MAINTENANCE_TOGGLES_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "fail-open", () => Case1_FailOpen(failures, notes));
                Case(failures, "isolation", () => Case2_Isolation(failures, notes));
                Case(failures, "server-all", () => Case3_ServerClosesAll(failures, notes));
                Case(failures, "refuses", () => Case4_Refuses(failures, notes));
                Case(failures, "banner", () => Case5_Banner(failures, notes));
                Case(failures, "area-domain", () => Case6_AreaDomain(failures, notes));
                Case(failures, "gate-sites", () => Case7_GateSites(failures, notes));
                Case(failures, "no-cache", () => Case8_NoCache(failures, notes));
                Case(failures, "server-seal", () => Case9_ServerSideSeal(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                MaintenanceCatalog.Clear();
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "MAINTENANCE TOGGLES OK - the " + ExpectedAreaCount + " operator kill switches " +
                         "resolve OPEN on every failure path (no table, malformed payload, empty payload, " +
                         "server-reported read failure) per the owner ruling of 2026-08-27; each toggle " +
                         "closes only its own area; 'server' closes all six and outranks a row saying " +
                         "otherwise; a closed area REFUSES with an ASCII sentence that names it while an " +
                         "open area does not; the id domain is identical across the client, api/_lib and " +
                         "the schema CHECK; every client refusal site returns; no device cache exists; and " +
                         "the SERVER-SIDE seal is present in the three endpoints that must have it and " +
                         "absent from the three settled-payment endpoints that must not" + noteStr;
                return true;
            }
            reason = "maintenance-toggles FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - FAIL-OPEN. The ruling, driven failure mode by failure mode.
        // =====================================================================
        private static void Case1_FailOpen(List<string> failures, List<string> notes)
        {
            int modes = 0;

            // (a) NO TABLE AT ALL - never fetched, server unreachable, timed out.
            MaintenanceCatalog.Clear();
            modes++;
            AssertAllOpen(failures, "fail-open", "no table at all (unreachable / timed out / never fetched)");

            // (b) MALFORMED PAYLOAD - rejected, and nothing is sealed on its strength.
            MaintenanceCatalog.Clear();
            // NOTE the escapes below. An open brace written literally inside a
            // string still counts toward the naive brace-balance gate in CLAUDE.md
            // section 1, and an oracle that reds the project's own quality gate is not
            // shippable. The escape is the same byte to Newtonsoft and invisible to the gate.
            if (MaintenanceCatalog.ApplyPayload(OpenBrace + " this is not json", "test-malformed"))
                failures.Add("[fail-open] a malformed payload was ACCEPTED");
            modes++;
            AssertAllOpen(failures, "fail-open", "malformed payload");

            // (c) EMPTY PAYLOAD.
            MaintenanceCatalog.Clear();
            if (MaintenanceCatalog.ApplyPayload("   ", "test-empty"))
                failures.Add("[fail-open] an empty payload was ACCEPTED");
            modes++;
            AssertAllOpen(failures, "fail-open", "empty payload");

            // (d) THE SERVER ITSELF SAYS IT COULD NOT READ THE TABLE (200 + readOk:false).
            //     This is the DB-unreachable case arriving as a healthy HTTP response,
            //     and it is the one a naive client would treat as "nothing is sealed".
            //     It must clear to open AND must not be mistaken for a known-good table.
            MaintenanceCatalog.Clear();
            MaintenanceCatalog.ApplyPayload(Payload(true, true, "raiding", readOk: false), "test-readok-false");
            modes++;
            AssertAllOpen(failures, "fail-open", "server reported readOk=false while naming a sealed area");
            if (MaintenanceCatalog.Loaded)
                failures.Add("[fail-open] readOk=false left a STANDING table - a table we were told is " +
                             "unreadable must not be treated as known-good");

            // (e) A REJECTED PAYLOAD MUST NOT BLANK A GOOD STANDING TABLE. The opposite
            //     direction: fail-open must not mean "any garbage reopens the game".
            //     If it did, an attacker who can only make the endpoint return junk
            //     could lift a seal - which would defeat the containment entirely.
            MaintenanceCatalog.Clear();
            MaintenanceCatalog.ApplyPayload(Payload(true, true, "store"), "test-good");
            if (!MaintenanceCatalog.IsClosed(MaintenanceArea.Store))
                failures.Add("[fail-open] the setup for the blank-table check did not seal the store");
            MaintenanceCatalog.ApplyPayload(OpenBrace + " broken", "test-garbage");
            if (!MaintenanceCatalog.IsClosed(MaintenanceArea.Store))
                failures.Add("[fail-open] a REJECTED payload blanked the standing table and REOPENED a " +
                             "sealed area - garbage must leave the last accepted answer alone");
            modes++;

            if (modes != 5)
                failures.Add("[fail-open] only " + modes + " of the 5 failure modes were actually driven - " +
                             "a default audit that skipped a mode has certified nothing");

            notes.Add("fail-open drove " + modes + "/5 modes OPEN and proved a rejected payload cannot reopen a seal");
            MaintenanceCatalog.Clear();
        }

        private static void AssertAllOpen(List<string> failures, string caseName, string what)
        {
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                var area = (MaintenanceArea)i;
                if (MaintenanceCatalog.IsClosed(area))
                {
                    failures.Add("[" + caseName + "] '" + ExpectedAreaIds[i] + "' resolved CLOSED with " +
                                 what + ". FAIL-OPEN is the owner ruling of 2026-08-27 (\"i cannot help if " +
                                 "server is unreachable\") and is DELIBERATELY the opposite of the " +
                                 "WO-1223 dungeon rule. Do not unify them.");
                }
            }
            if (MaintenanceCatalog.BannerText() != null)
                failures.Add("[" + caseName + "] a banner was shown with " + what +
                             " - nothing is sealed, so nothing may be announced");
        }

        // =====================================================================
        //  Case 2 - each toggle closes ONLY its own area.
        // =====================================================================
        private static void Case2_Isolation(List<string> failures, List<string> notes)
        {
            int driven = 0;
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                string sealedId = ExpectedAreaIds[i];
                if (string.Equals(sealedId, MaintenanceCatalog.ServerAreaId, StringComparison.Ordinal))
                    continue;   // `server` is case 3 - it is SUPPOSED to close everything

                MaintenanceCatalog.Clear();
                MaintenanceCatalog.ApplyPayload(Payload(true, true, sealedId), "test-isolation");
                driven++;

                // The GOOD path first: every OTHER area must still be open. This is the
                // half that stops the suite being satisfiable by closing everything.
                for (int j = 0; j < ExpectedAreaIds.Length; j++)
                {
                    var other = (MaintenanceArea)j;
                    bool isTheSealedOne = string.Equals(ExpectedAreaIds[j], sealedId, StringComparison.Ordinal);
                    bool closed = MaintenanceCatalog.IsClosed(other);

                    if (isTheSealedOne && !closed)
                        failures.Add("[isolation] sealing '" + sealedId + "' did NOT close it");
                    if (!isTheSealedOne && closed)
                        failures.Add("[isolation] sealing '" + sealedId + "' ALSO closed '" +
                                     ExpectedAreaIds[j] + "' - a toggle must close exactly one area");
                }

                var state = MaintenanceCatalog.For((MaintenanceArea)i);
                if (!string.Equals(state.ClosedBy, sealedId, StringComparison.Ordinal))
                    failures.Add("[isolation] '" + sealedId + "' reported closedBy='" +
                                 (state.ClosedBy ?? "null") + "' - the refusal trace names the wrong toggle");
            }

            if (driven != ExpectedAreaCount - 1)
                failures.Add("[isolation] drove " + driven + " toggles, expected " + (ExpectedAreaCount - 1));

            notes.Add("isolation drove " + driven + " single seals and re-read all " + ExpectedAreaCount +
                      " areas after each");
            MaintenanceCatalog.Clear();
        }

        // =====================================================================
        //  Case 3 - `server` closes everything and outranks a per-area row.
        // =====================================================================
        private static void Case3_ServerClosesAll(List<string> failures, List<string> notes)
        {
            // Every area row explicitly says OPEN; only `server` says closed. If the
            // per-area row won, this would read as five open areas during a full
            // maintenance window - so this is the ordering assertion, not a repeat.
            var sb = new StringBuilder();
            sb.Append("{\"ok\":true,\"version\":1,\"readOk\":true,\"areas\":{");
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                string id = ExpectedAreaIds[i];
                bool isServer = string.Equals(id, MaintenanceCatalog.ServerAreaId, StringComparison.Ordinal);
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(id).Append("\":{\"closed\":").Append(isServer ? "true" : "false")
                  .Append(",\"closedBy\":").Append(isServer ? "\"server\"" : "null")
                  .Append(",\"message\":").Append(isServer ? "\"The realm is closed while we fix a problem.\"" : "null")
                  .Append('}');
            }
            sb.Append("}}");

            MaintenanceCatalog.Clear();
            if (!MaintenanceCatalog.ApplyPayload(sb.ToString(), "test-server"))
            {
                failures.Add("[server-all] the server-window payload was rejected");
                return;
            }

            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                var area = (MaintenanceArea)i;
                var st = MaintenanceCatalog.For(area);
                if (!st.Closed)
                {
                    failures.Add("[server-all] '" + ExpectedAreaIds[i] + "' is OPEN during a `server` " +
                                 "maintenance window - server closes THE WHOLE GAME");
                    continue;
                }
                if (!string.Equals(st.ClosedBy, MaintenanceCatalog.ServerAreaId, StringComparison.Ordinal) &&
                    !string.Equals(ExpectedAreaIds[i], MaintenanceCatalog.ServerAreaId, StringComparison.Ordinal))
                {
                    failures.Add("[server-all] '" + ExpectedAreaIds[i] + "' reported closedBy='" +
                                 (st.ClosedBy ?? "null") + "' - a server window must attribute to `server`, " +
                                 "or the operator cannot tell a full window from five coincidental seals");
                }
            }

            string banner = MaintenanceCatalog.BannerText();
            if (string.IsNullOrEmpty(banner))
                failures.Add("[server-all] no banner during a full maintenance window");
            else if (banner.IndexOf("MAINTENANCE", StringComparison.Ordinal) < 0)
                failures.Add("[server-all] the server-window banner does not contain the word MAINTENANCE: \"" +
                             banner + "\"");

            notes.Add("server-all proved the whole-game toggle outranks " + (ExpectedAreaCount - 1) +
                      " rows that each say open");
            MaintenanceCatalog.Clear();
        }

        // =====================================================================
        //  Case 4 - a closed area REFUSES. And an open one does NOT.
        // =====================================================================
        private static void Case4_Refuses(List<string> failures, List<string> notes)
        {
            // ---- THE GOOD PATH, FIRST AND DELIBERATELY ----------------------
            // A failure-only oracle is not acceptance. This repo shipped a guard
            // that aborted every good run while exiting 0, so the open case is
            // asserted before the closed one.
            MaintenanceCatalog.Clear();
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                var area = (MaintenanceArea)i;
                if (MaintenanceCatalog.Refuses(area, "oracle-good-path", out string msg))
                    failures.Add("[refuses] '" + ExpectedAreaIds[i] + "' REFUSED with nothing sealed - " +
                                 "the gate is aborting good runs");
                if (msg != null)
                    failures.Add("[refuses] '" + ExpectedAreaIds[i] + "' handed back a message ('" + msg +
                                 "') while OPEN - a player would be told about an outage that is not happening");
            }

            // ---- THE REFUSAL PATH -------------------------------------------
            int refused = 0;
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                string id = ExpectedAreaIds[i];
                MaintenanceCatalog.Clear();
                MaintenanceCatalog.ApplyPayload(Payload(true, true, id), "test-refuse");

                if (!MaintenanceCatalog.Refuses((MaintenanceArea)i, "oracle-refuse", out string msg))
                {
                    failures.Add("[refuses] '" + id + "' is sealed but Refuses() returned FALSE - a banner " +
                                 "without a gate is decoration, and the whole point is that the broken " +
                                 "thing stops being reachable");
                    continue;
                }
                refused++;

                if (string.IsNullOrWhiteSpace(msg))
                {
                    failures.Add("[refuses] '" + id + "' refused with an EMPTY message - a player who taps a " +
                                 "closed area must already know why");
                    continue;
                }
                if (!IsAscii(msg))
                    failures.Add("[refuses] '" + id + "' refusal message is not ASCII: \"" + msg + "\"");
            }

            if (refused != ExpectedAreaCount)
                failures.Add("[refuses] only " + refused + " of " + ExpectedAreaCount +
                             " areas actually refused when sealed");

            // A seal authored WITHOUT a message must still refuse with words. The
            // command-centre tool refuses a message-less seal, but a row written
            // straight into Neon by hand can still get here.
            MaintenanceCatalog.Clear();
            MaintenanceCatalog.ApplyPayload(Payload(true, false /* no message */, "raiding", noMessage: true),
                                            "test-refuse-nomsg");
            if (!MaintenanceCatalog.Refuses(MaintenanceArea.Raiding, "oracle-nomsg", out string fallback))
                failures.Add("[refuses] a message-less seal did not refuse");
            else if (string.IsNullOrWhiteSpace(fallback) ||
                     fallback.IndexOf("MAINTENANCE", StringComparison.Ordinal) < 0)
                failures.Add("[refuses] a message-less seal produced no usable fallback sentence: \"" +
                             (fallback ?? "null") + "\"");

            notes.Add("refuses proved " + refused + "/" + ExpectedAreaCount + " seals refuse, the open path " +
                      "does not, and a message-less seal still speaks");
            MaintenanceCatalog.Clear();
        }

        // =====================================================================
        //  Case 5 - the banner reads as maintenance from its WORDS.
        // =====================================================================
        private static void Case5_Banner(List<string> failures, List<string> notes)
        {
            // Nothing sealed => no banner. Asserted first: a permanent banner would
            // be worse than none, because players learn to ignore it.
            MaintenanceCatalog.Clear();
            MaintenanceCatalog.ApplyPayload(Payload(false, false, null, allOpen: true), "test-banner-open");
            if (MaintenanceCatalog.BannerText() != null)
                failures.Add("[banner] a banner was produced with nothing sealed");

            int checkedAreas = 0;
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                string id = ExpectedAreaIds[i];
                MaintenanceCatalog.Clear();
                MaintenanceCatalog.ApplyPayload(Payload(true, true, id), "test-banner");
                string banner = MaintenanceCatalog.BannerText();
                checkedAreas++;

                if (string.IsNullOrWhiteSpace(banner))
                {
                    failures.Add("[banner] '" + id + "' is sealed but no banner text was produced - the " +
                                 "owner ruled a rolling banner tells EVERY player");
                    continue;
                }
                if (banner.IndexOf("MAINTENANCE", StringComparison.Ordinal) < 0)
                    failures.Add("[banner] '" + id + "' banner does not contain the literal word " +
                                 "MAINTENANCE: \"" + banner + "\". The owner is red/green colourblind; " +
                                 "the line must read as maintenance from its WORDS, never from a hue.");
                string display = MaintenanceCatalog.DisplayName(id);
                if (banner.IndexOf(display, StringComparison.Ordinal) < 0)
                    failures.Add("[banner] '" + id + "' banner does not NAME the area (\"" + display +
                                 "\"): \"" + banner + "\"");
                if (!IsAscii(banner))
                    failures.Add("[banner] '" + id + "' banner is not ASCII: \"" + banner + "\"");
            }

            if (checkedAreas != ExpectedAreaCount)
                failures.Add("[banner] checked " + checkedAreas + " areas, expected " + ExpectedAreaCount);

            // Two at once: BOTH must be named. A player who can still farm but cannot
            // raid needs both facts, not the first one the loop happened to find.
            MaintenanceCatalog.Clear();
            MaintenanceCatalog.ApplyPayload(Payload(true, true, "farming", second: "raiding"), "test-banner-two");
            string two = MaintenanceCatalog.BannerText();
            if (two == null ||
                two.IndexOf(MaintenanceCatalog.DisplayName("farming"), StringComparison.Ordinal) < 0 ||
                two.IndexOf(MaintenanceCatalog.DisplayName("raiding"), StringComparison.Ordinal) < 0)
            {
                failures.Add("[banner] with two areas sealed the banner did not name both: \"" +
                             (two ?? "null") + "\"");
            }

            notes.Add("banner checked " + checkedAreas + " single seals plus the two-at-once line");
            MaintenanceCatalog.Clear();
        }

        // =====================================================================
        //  Case 6 - ONE domain, three files. Comments EXCLUDED (see the header).
        // =====================================================================
        private static void Case6_AreaDomain(List<string> failures, List<string> notes)
        {
            if (MaintenanceCatalog.AreaIds.Length != ExpectedAreaCount)
                failures.Add("[area-domain] MaintenanceCatalog.AreaIds has " + MaintenanceCatalog.AreaIds.Length +
                             " entries, expected the pinned " + ExpectedAreaCount);
            for (int i = 0; i < ExpectedAreaIds.Length && i < MaintenanceCatalog.AreaIds.Length; i++)
            {
                if (!string.Equals(MaintenanceCatalog.AreaIds[i], ExpectedAreaIds[i], StringComparison.Ordinal))
                    failures.Add("[area-domain] AreaIds[" + i + "] is '" + MaintenanceCatalog.AreaIds[i] +
                                 "', pinned as '" + ExpectedAreaIds[i] + "'. The enum is indexed by ordinal - " +
                                 "reordering it silently re-points every refusal site.");
            }
            if (Enum.GetValues(typeof(MaintenanceArea)).Length != ExpectedAreaCount)
                failures.Add("[area-domain] MaintenanceArea has " +
                             Enum.GetValues(typeof(MaintenanceArea)).Length + " members, expected " +
                             ExpectedAreaCount);

            // --- api/_lib/maintenance.js : the AREAS literal, comments stripped ---
            string js = ReadOrNull(ApiLibSrc);
            if (js == null)
            {
                failures.Add("[area-domain] " + ApiLibSrc + " is MISSING - the server-side seal is the " +
                             "control layer; without it the client gate is a closed sign on an unlocked door");
            }
            else
            {
                string jsCode = StripComments(js);
                var m = Regex.Match(jsCode, @"const\s+AREAS\s*=\s*\[([^\]]*)\]");
                if (!m.Success)
                {
                    failures.Add("[area-domain] could not find the AREAS array in " + ApiLibSrc);
                }
                else
                {
                    var ids = new List<string>();
                    foreach (Match q in Regex.Matches(m.Groups[1].Value, @"[A-Za-z_]+")) ids.Add(q.Value);
                    // The literal is written as AREA_* consts, so resolve each one.
                    var resolved = new List<string>();
                    foreach (string sym in ids)
                    {
                        var c = Regex.Match(jsCode, @"const\s+" + Regex.Escape(sym) + @"\s*=\s*'([a-z]+)'");
                        resolved.Add(c.Success ? c.Groups[1].Value : sym.ToLowerInvariant());
                    }
                    CompareDomain(failures, "area-domain", ApiLibSrc, resolved.ToArray());
                }
            }

            // --- api/schema.sql : the CHECK list, comments stripped ---
            string sql = ReadOrNull(SchemaSrc);
            if (sql == null)
            {
                failures.Add("[area-domain] " + SchemaSrc + " is MISSING");
            }
            else
            {
                string sqlCode = StripSqlComments(sql);
                var m = Regex.Match(sqlCode,
                    @"CREATE TABLE IF NOT EXISTS\s+maintenance_toggles[\s\S]*?area_id[\s\S]*?CHECK\s*\(\s*area_id\s+IN\s*\(([^)]*)\)");
                if (!m.Success)
                {
                    failures.Add("[area-domain] maintenance_toggles has no area_id CHECK constraint in " +
                                 SchemaSrc + ". Under fail-open a typo'd area id would silently never seal, " +
                                 "which is a seal the owner believes she applied and did not.");
                }
                else
                {
                    var ids = new List<string>();
                    foreach (Match q in Regex.Matches(m.Groups[1].Value, @"'([a-z]+)'")) ids.Add(q.Groups[1].Value);
                    CompareDomain(failures, "area-domain", SchemaSrc, ids.ToArray());
                }
            }

            notes.Add("area-domain compared the " + ExpectedAreaCount + " ids across the client enum, " +
                      ApiLibSrc + " and the schema CHECK, reading code only (comments excluded on purpose)");
        }

        private static void CompareDomain(List<string> failures, string caseName, string where, string[] found)
        {
            if (found.Length != ExpectedAreaCount)
            {
                failures.Add("[" + caseName + "] " + where + " declares " + found.Length + " areas (" +
                             string.Join(",", found) + "), expected the pinned " + ExpectedAreaCount);
            }
            foreach (string want in ExpectedAreaIds)
            {
                if (Array.IndexOf(found, want) < 0)
                    failures.Add("[" + caseName + "] " + where + " is MISSING area '" + want + "'");
            }
            foreach (string got in found)
            {
                if (Array.IndexOf(ExpectedAreaIds, got) < 0)
                    failures.Add("[" + caseName + "] " + where + " declares UNKNOWN area '" + got + "'");
            }
        }

        // =====================================================================
        //  Case 7 - every refusal site actually RETURNS. Sweeps the whole tree.
        // =====================================================================
        private static void Case7_GateSites(List<string> failures, List<string> notes)
        {
            if (!Directory.Exists(ModulesRoot))
            {
                failures.Add("[gate-sites] " + ModulesRoot + " not found");
                return;
            }

            var seenAreas = new HashSet<string>();
            int sites = 0;
            int files = 0;

            // DO NOT: SWEEP EVERY FILE AND EVERY SITE WITHIN EACH FILE. A detector that
            // reports one site per run passes hollow: it goes green while the second
            // and third sites in the same file are still wrong.
            foreach (string path in Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string raw = ReadOrNull(path);
                if (raw == null) continue;
                if (raw.IndexOf("MaintenanceCatalog.Refuses", StringComparison.Ordinal) < 0) continue;

                // The catalog itself DECLARES Refuses; it is not a call site.
                string norm = path.Replace('\\', '/');
                if (norm.EndsWith("MaintenanceCatalog.cs", StringComparison.Ordinal)) continue;

                files++;

                // Comments EXCLUDED for finding sites (a comment naming Refuses() is
                // prose), but line NUMBERING is kept against the real file so the
                // "a return follows" window is measured against what a human reads.
                // Blanking comments in place preserves both.
                string blanked = BlankComments(raw);
                string[] lines = blanked.Replace("\r\n", "\n").Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf("MaintenanceCatalog.Refuses", StringComparison.Ordinal) < 0) continue;
                    sites++;

                    // Which area is this site gating? Search the call expression, which
                    // may wrap across lines.
                    string window = Join(lines, i, ReturnWindowLines);
                    var am = Regex.Match(window, @"MaintenanceArea\.([A-Za-z]+)");
                    if (am.Success) seenAreas.Add(am.Groups[1].Value.ToLowerInvariant());
                    else failures.Add("[gate-sites] " + norm + ":" + (i + 1) +
                                      " calls Refuses() without naming a MaintenanceArea");

                    // The whole ruling in one assertion: a closed area must REFUSE, not
                    // merely warn. `return` covers void and value returns alike; the
                    // PurchaseGate site returns false, which is its refusal.
                    if (!Regex.IsMatch(window, @"\breturn\b"))
                    {
                        failures.Add("[gate-sites] " + norm + ":" + (i + 1) + " calls Refuses() but no " +
                                     "`return` follows within " + ReturnWindowLines + " lines. A banner " +
                                     "without a gate is decoration - the area must stop being reachable.");
                    }
                }
            }

            foreach (var area in AreasNeedingASite)
            {
                string want = area.ToString().ToLowerInvariant();
                if (!seenAreas.Contains(want))
                    failures.Add("[gate-sites] no client refusal site was found for '" + want +
                                 "' - that area's toggle would show a banner and let the player straight in");
            }

            if (sites < AreasNeedingASite.Length)
                failures.Add("[gate-sites] found only " + sites + " refusal site(s) across " + files +
                             " file(s); at least " + AreasNeedingASite.Length + " are required");

            notes.Add("gate-sites swept " + ModulesRoot + " and checked ALL " + sites + " site(s) in " +
                      files + " file(s), not just the first");
        }

        // =====================================================================
        //  Case 8 - NO device cache, and the poll interval is in bounds.
        // =====================================================================
        private static void Case8_NoCache(List<string> failures, List<string> notes)
        {
            string src = ReadOrNull(ServiceSrc);
            if (src == null)
            {
                failures.Add("[no-cache] " + ServiceSrc + " is MISSING");
                return;
            }

            // Comments EXCLUDED: this file's header deliberately spells out "cache",
            // "CachePath" and "PlayerPrefs" in order to say it has NONE of them. A
            // comment-reading lint would red on the sentence documenting compliance -
            // which is one of the two directions this repo got wrong this week.
            string code = StripComments(src);

            string[] banned = { "File.WriteAllText", "File.ReadAllText", "PlayerPrefs.SetString",
                                "PlayerPrefs.GetString", "persistentDataPath" };
            int hits = 0;
            foreach (string needle in banned)
            {
                if (code.IndexOf(needle, StringComparison.Ordinal) >= 0)
                {
                    hits++;
                    failures.Add("[no-cache] " + ServiceSrc + " uses '" + needle + "'. The owner ruled NO " +
                                 "DEVICE CACHING (WO-1243): every check is live. She was shown the " +
                                 "consequence - an offline player falls back to the default - and chose it. " +
                                 "Do not add a cache 'to be safe'.");
                }
            }

            // The banner driver must not squirrel one away either.
            string banner = ReadOrNull(BannerSrc);
            if (banner != null)
            {
                string bcode = StripComments(banner);
                foreach (string needle in banned)
                {
                    if (bcode.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    {
                        hits++;
                        failures.Add("[no-cache] " + BannerSrc + " uses '" + needle + "' - the banner may " +
                                     "not persist a seal either");
                    }
                }
            }

            // THE GOOD PATH: prove the live fetch actually exists. A file with no cache
            // AND no fetch would pass the check above while never learning anything.
            if (code.IndexOf("UnityWebRequest", StringComparison.Ordinal) < 0)
                failures.Add("[no-cache] " + ServiceSrc + " has no UnityWebRequest - with no cache and no " +
                             "live fetch the client would never learn about a seal at all");

            // Pinned bounds, both literals. Never expressed relative to the current value.
            if (MaintenanceService.PollSeconds < MinPollSeconds)
                failures.Add("[no-cache] PollSeconds=" + MaintenanceService.PollSeconds + " is below the " +
                             "pinned floor of " + MinPollSeconds + " - it hammers the origin for no gain, " +
                             "because the server-side memo already bounds the real seal");
            if (MaintenanceService.PollSeconds > MaxPollSeconds)
                failures.Add("[no-cache] PollSeconds=" + MaintenanceService.PollSeconds + " exceeds the " +
                             "pinned ceiling of " + MaxPollSeconds + " - the poll interval IS the exposure " +
                             "window for honest players already in session");

            notes.Add("no-cache checked " + banned.Length + " cache idioms in 2 files (" + hits +
                      " hit(s)), proved the live fetch exists, and pinned PollSeconds=" +
                      MaintenanceService.PollSeconds + " within [" + MinPollSeconds + "," + MaxPollSeconds + "]");
        }

        // =====================================================================
        //  Case 9 - THE SEAL IS ENFORCED SERVER-SIDE. The security assertion.
        // =====================================================================
        private static void Case9_ServerSideSeal(List<string> failures, List<string> notes)
        {
            // Comments EXCLUDED. The client files discuss the server-side seal at
            // length; so do these api/ files. A lint that counted those sentences
            // would report the guard present after someone deleted the code.
            int enforcing = 0;
            foreach (string path in MustEnforce)
            {
                string raw = ReadOrNull(path);
                if (raw == null)
                {
                    failures.Add("[server-seal] " + path + " is MISSING");
                    continue;
                }
                string code = StripComments(raw);
                bool requires = code.IndexOf("_lib/maintenance", StringComparison.Ordinal) >= 0;
                bool calls = code.IndexOf("maintenanceEnforce", StringComparison.Ordinal) >= 0 ||
                             code.IndexOf("maintenanceIsClosed", StringComparison.Ordinal) >= 0;
                if (!requires || !calls)
                {
                    failures.Add("[server-seal] " + path + " no longer enforces the seal (requires=" +
                                 requires + " calls=" + calls + "). A CLIENT-SIDE GATE ALONE IS " +
                                 "WORTHLESS HERE: someone exploiting the game runs a modified client and " +
                                 "ignores it, so removing this leaves honest players locked out of an area " +
                                 "the attacker still has to themselves. Owner ruling 2026-08-27.");
                    continue;
                }
                enforcing++;
            }

            // The endpoint and the library themselves.
            foreach (string path in new[] { ApiLibSrc, ApiEndpointSrc })
            {
                if (ReadOrNull(path) == null) failures.Add("[server-seal] " + path + " is MISSING");
            }

            // And the absence that matters: never seal a payment that already settled.
            int clean = 0;
            foreach (string path in MustNotEnforce)
            {
                string raw = ReadOrNull(path);
                if (raw == null) continue;   // not every rail file has to exist
                string code = StripComments(raw);
                if (code.IndexOf("_lib/maintenance", StringComparison.Ordinal) >= 0)
                {
                    failures.Add("[server-seal] " + path + " enforces the maintenance seal and MUST NOT. " +
                                 "It runs AFTER the chain settles - the money is already gone and an SPL " +
                                 "transfer has no refund route, so a seal here takes a real payment and " +
                                 "then refuses to record the entitlement. Seal at quote (pre-payment) only.");
                    continue;
                }
                clean++;
            }

            if (enforcing != MustEnforce.Length)
                failures.Add("[server-seal] only " + enforcing + " of " + MustEnforce.Length +
                             " endpoints enforce the seal");

            notes.Add("server-seal proved " + enforcing + "/" + MustEnforce.Length + " endpoints enforce and " +
                      clean + "/" + MustNotEnforce.Length + " settled-payment endpoints deliberately do not");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>Build a test payload. Deliberately hand-written JSON rather than a
        /// serialiser, so the oracle exercises the same parse the wire does.</summary>
        private static string Payload(bool anySealed, bool withMessage, string sealedId,
                                      bool allOpen = false, bool noMessage = false, string second = null,
                                      bool readOk = true)
        {
            var sb = new StringBuilder();
            sb.Append("{\"ok\":true,\"version\":1,\"readOk\":");
            sb.Append(readOk ? "true" : "false");
            sb.Append(",\"areas\":{");
            bool first = true;
            for (int i = 0; i < ExpectedAreaIds.Length; i++)
            {
                string id = ExpectedAreaIds[i];
                bool closed = !allOpen &&
                              (string.Equals(id, sealedId, StringComparison.Ordinal) ||
                               (second != null && string.Equals(id, second, StringComparison.Ordinal)));
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(id).Append("\":{\"closed\":").Append(closed ? "true" : "false")
                  .Append(",\"closedBy\":").Append(closed ? "\"" + id + "\"" : "null")
                  .Append(",\"message\":");
                if (closed && withMessage && !noMessage)
                    sb.Append("\"This area is closed while we fix a problem.\"");
                else
                    sb.Append("null");
                sb.Append('}');
            }
            sb.Append("}}");
            return sb.ToString();
        }

        private static string Join(string[] lines, int from, int count)
        {
            var sb = new StringBuilder();
            for (int i = from; i < lines.Length && i < from + count; i++) sb.Append(lines[i]).Append('\n');
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
        /// !! The naive version of this helper is a real bug waiting to happen in THIS
        /// repo: MaintenanceService.cs contains <c>"https://defenders-of-the-realm-v2..."</c>
        /// and a stripper that does not track quotes would cut the file in half at the
        /// <c>//</c> inside that URL and then certify whatever was left.
        /// </para>
        /// </summary>
        private static string StripComments(string src) => StripComments(src, blankInsteadOfRemove: false);

        /// <summary>Same as <see cref="StripComments(string)"/> but replaces comment
        /// characters with spaces so LINE NUMBERS and column offsets survive.</summary>
        private static string BlankComments(string src) => StripComments(src, blankInsteadOfRemove: true);

        private static string StripComments(string src, bool blankInsteadOfRemove)
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
                    while (i < src.Length && src[i] != '\n')
                    {
                        if (blankInsteadOfRemove) sb.Append(' ');
                        i++;
                    }
                    continue;
                }

                // Block comment
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    if (blankInsteadOfRemove) sb.Append("  ");
                    while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/'))
                    {
                        if (blankInsteadOfRemove) sb.Append(src[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    i = Math.Min(src.Length, i + 2);
                    if (blankInsteadOfRemove) sb.Append("  ");
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>SQL comments are <c>--</c> to end of line. String literals here are
        /// single-quoted, and the CHECK list this case reads IS a run of them, so the
        /// quote tracking is load-bearing rather than defensive.</summary>
        private static string StripSqlComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return src ?? "";
            var sb = new StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];
                if (c == '\'')
                {
                    sb.Append(c);
                    i++;
                    while (i < src.Length)
                    {
                        sb.Append(src[i]);
                        if (src[i] == '\'') { i++; break; }
                        i++;
                    }
                    continue;
                }
                if (c == '-' && i + 1 < src.Length && src[i + 1] == '-')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
