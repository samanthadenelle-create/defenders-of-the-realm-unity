// =============================================================================
// DungeonStatusRegression [dungeon-status]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (already references DeNelle.Core +
//   DeNelle.Village — no asmdef edit needed).
//
// Pins WO-1114: the remotely-flippable dungeon door state.
//
// THE RULE THIS SUITE EXISTS TO STOP ROTTING (WO criterion 4):
//   A closed dungeon must read as WORLD, never as BUILD STATUS. "Under
//   construction" / "coming soon" / "disabled for dev" convert a deliberate
//   world back into a visibly unfinished build — the exact outcome the WO buys
//   its way out of. That rule is a sentence in a document, which is the kind of
//   thing that rots. Case 1 makes it a GATE.
//
// AND THE ONE THAT CAN HURT A PLAYER — ⛔ INVERTED 2026-08-26:
//   This header used to read "every failure in this system must resolve toward
//   OPEN". The OWNER RULED THE OPPOSITE (WO-1223), verbatim:
//       "not acesable if not in table, if in table and works then yes"
//   Every failure now resolves toward CLOSED: absent id, absent table (no cache
//   / server unreachable / timed out), rejected payload, unparseable status.
//   Case 2 drives that whole matrix directly against DungeonStatusCatalog —
//   which is transport-free precisely so this can run headlessly, with no
//   network and no PlayMode. Case 7 pins the DEFAULT itself, failure mode by
//   failure mode, because "we added two ids to a list" is not the ruling.
//
// Cases:
//   1 [door-copy]     No build vocabulary in any of the eight dungeon door
//                     strings, in EITHER copy of canon-strings.json. Scanned on
//                     the PARSED VALUES only, so the authoring _comment stays
//                     free to name the very words it bans (the same reason
//                     GlossaryRegression.cs:372-373 scans parsed fields).
//   2 [door-fallback] The safety direction is one-way and it points CLOSED:
//                     unknown id, unknown status, absent id, null id and a
//                     garbage payload all resolve SEALED; a version mismatch
//                     still parses forward-compatibly; a rejected payload still
//                     leaves the standing table intact rather than blanking it;
//                     and the KILL SWITCH provenance still forces every door open.
//   3 [door-keys]     All eight canon keys exist in BOTH copies, are non-empty,
//                     are byte-equal across the copies, and carry real prose
//                     (not a placeholder). Guarantees "[[missing:key]]" can
//                     never reach a player.
//   4 [door-ids]      The status domain is EXACTLY the AuthoredPortal ids (six
//                     since the owner ruled dg_folks_granary + dg_healers_cottage
//                     gatable, 2026-08-26). Fixtures and probes (dg_descent_probe,
//                     dg_stair_rig, dg_stairwell_probe) and the CROSSROADS
//                     dg_hollow_roads are asserted ABSENT from it — gating any of
//                     them would gate something that has no door, and under
//                     fail-closed that would BAR it permanently.
//   6 [door-coverage] WO-1223 — COMPLETENESS, which case 4 structurally cannot
//                     reach. Case 4 iterates PortalDungeonIds, so it validates the
//                     CONTENTS of the domain and can never notice a dungeon that is
//                     missing from it. This case computes the set of dungeons the
//                     player can actually REACH and asserts each one is accounted
//                     for — in PortalDungeonIds or, with a stated reason, in
//                     MustNotBeGated. Anything in neither FAILS by name.
//   7 [door-default]  WO-1223 / owner ruling 2026-08-26 — THE FAIL-CLOSED DEFAULT
//                     itself, asserted failure mode by failure mode: id absent from
//                     the table, no table at all (server unreachable / timed out),
//                     malformed payload, EMPTY payload, and a row present whose
//                     status does not parse. All five must be CLOSED. The two
//                     sanctioned escapes (the kill switch, the UngatedIds allowlist)
//                     are asserted to still open, so this case cannot be satisfied
//                     by closing everything.
//   5 [door-appearance] STANDS DOWN today, by design — see the note it emits.
//                     The appearance half (DungeonWorldPortalSpawner.ApplyDoorState)
//                     is a later phase; this case names the hole instead of
//                     pretending it is covered.
//
// ⚠ "dev" IS A SUBSTRING OF ORDINARY ENGLISH ("devour", "devastation", "devout"
//   — all plausible in this game's register). It is matched on a WORD BOUNDARY.
//   A naive IndexOf("dev") reds on the first good sentence the owner writes, and
//   an oracle that cries wolf gets switched off — which is the failure this case
//   exists to prevent.
//
// Markers: DUNGEON_STATUS_OK / DUNGEON_STATUS_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DungeonStatusRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DeNelle.Core.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class DungeonStatusRegression
    {
        private const string CanonRes = "Assets/Resources/Data/Canonical/canon-strings.json";
        private const string CanonSA = "Assets/StreamingAssets/Data/Canonical/canon-strings.json";

        /// <summary>Source-lint target for the id contract (Case 4).</summary>
        private const string SpawnerSrc = "Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs";

        /// <summary>WO-1223 — the client/server coverage contract (Case 6). Repo-relative,
        /// like the Assets paths above: batchmode's working directory IS the project root.
        /// It lives under api/_lib because the OTHER half of the contract is a node test
        /// (test/dungeon-status.manifest.test.js) that asserts every gated id in it has a
        /// dungeon_status row — the half a Unity oracle can never reach.</summary>
        private const string ManifestPath = "api/_lib/dungeon-manifest.json";

        /// <summary>The appearance owner Case 5 will assert once that phase lands.</summary>
        private const string ApplyDoorStateSymbol = "ApplyDoorState";

        /// <summary>The eight door-copy keys, in status order.</summary>
        private static readonly string[] DoorCopyKeys =
        {
            "dungeonSealedHeadline",    "dungeonSealedBody",
            "dungeonCollapsedHeadline", "dungeonCollapsedBody",
            "dungeonRescueHeadline",    "dungeonRescueBody",
            "dungeonFloodedHeadline",   "dungeonFloodedBody",
        };

        /// <summary>A body shorter than this is a placeholder, not prose.
        /// (Same reasoning as GlossaryRegression.MinDefinitionChars.)</summary>
        private const int MinBodyChars = 20;

        /// <summary>A headline shorter than this is not a sentence.</summary>
        private const int MinHeadlineChars = 10;

        /// <summary>
        /// Build vocabulary. NONE of this may reach a player through the door.
        /// Kept SEPARATE from GlossaryRegression.BannedInPlayerCopy on purpose: these
        /// needles ("dev", "WIP") are far more false-positive-prone than that suite's
        /// retired-canon needles, and must not be able to red the glossary.
        /// </summary>
        private static readonly string[] BannedLiteral =
        {
            "construction", "coming soon", "disabled", "wip", "todo",
            "placeholder", "not implemented", "unfinished", "work in progress",
        };

        /// <summary>Word-boundary needles — see the "dev" note in the header.</summary>
        private static readonly string[] BannedWholeWord = { "dev" };

        /// <summary>
        /// Ids that MUST NOT appear in any status table: three fixtures/probes (no portal
        /// exists for them) and one CROSSROADS gated by FeatureFlags.BiomeRoads.
        /// <para>
        /// ⚠ NO LONGER A LITERAL HERE (2026-08-26). It reads
        /// <see cref="DungeonStatusCatalog.UngatedIds"/>, because under the fail-closed
        /// ruling that array stopped being a lint list and became RUNTIME BEHAVIOUR — it
        /// is the allowlist <c>For()</c> consults before closing a door. A second copy in
        /// this file could drift from the one the game actually runs, which is CLAUDE.md's
        /// duplicated-state failure and would let this suite certify the wrong array.
        /// </para>
        /// </summary>
        private static readonly string[] MustNotBeGated = DungeonStatusCatalog.UngatedIds;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_STATUS_OK - " + reason);
            else Debug.LogError("DUNGEON_STATUS_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "door-copy", () => Case1_BannedCopy(failures, notes));
                Case(failures, "door-fallback", () => Case2_Fallback(failures, notes));
                Case(failures, "door-keys", () => Case3_CanonKeys(failures, notes));
                Case(failures, "door-ids", () => Case4_IdContract(failures, notes));
                Case(failures, "door-appearance", () => Case5_AppearanceOwner(failures, notes));
                Case(failures, "door-coverage", () => Case6_Coverage(failures, notes));
                Case(failures, "door-default", () => Case7_FailClosedDefault(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DUNGEON STATUS OK - the eight door strings are byte-equal across both canon-strings.json " +
                         "copies and carry no build vocabulary; every failure path in DungeonStatusCatalog resolves " +
                         "CLOSED per the owner ruling of 2026-08-26 (absent row, absent table, malformed payload, " +
                         "empty payload, unparseable status, null id), with the kill switch and the UngatedIds " +
                         "allowlist proven to still open; a rejected payload leaves the standing table intact; the " +
                         "status domain is exactly the " + DungeonStatusCatalog.PortalDungeonIds.Length +
                         " AuthoredPortal ids with the fixtures/probes/crossroads asserted ungatable; and every " +
                         "REACHABLE dungeon is accounted for in one list or the other" + noteStr;
                return true;
            }
            reason = "dungeon-status FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - no build vocabulary in the door copy
        // =====================================================================
        private static void Case1_BannedCopy(List<string> failures, List<string> notes)
        {
            int scanned = 0;
            foreach (string path in new[] { CanonRes, CanonSA })
            {
                if (!File.Exists(path)) { failures.Add("[door-copy] missing " + path); continue; }

                JObject obj = ParseCanon(path, failures, "door-copy");
                if (obj == null) continue;

                foreach (string key in DoorCopyKeys)
                {
                    JToken tok = obj[key];
                    if (tok == null) continue;   // absence is Case 3's job, not this one's
                    string value = tok.Type == JTokenType.String ? tok.Value<string>() : tok.ToString();
                    if (string.IsNullOrEmpty(value)) continue;

                    scanned++;
                    string hit = FindBannedWord(value);
                    if (hit != null)
                    {
                        failures.Add("[door-copy] " + Path.GetFileName(path) + " key '" + key +
                                     "' leaks build vocabulary '" + hit + "' to the player: \"" + value + "\"");
                    }
                }
            }

            if (scanned == 0)
                failures.Add("[door-copy] scanned ZERO door strings - the oracle asserted nothing");
            else
                notes.Add("door-copy scanned " + scanned + " parsed values across both canon copies");
        }

        /// <summary>Returns the offending needle, or null. Case-insensitive; the
        /// word-boundary needles are matched as whole words only.</summary>
        internal static string FindBannedWord(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string lower = value.ToLowerInvariant();

            for (int i = 0; i < BannedLiteral.Length; i++)
                if (lower.Contains(BannedLiteral[i])) return BannedLiteral[i];

            for (int i = 0; i < BannedWholeWord.Length; i++)
            {
                if (Regex.IsMatch(value, "\\b" + Regex.Escape(BannedWholeWord[i]) + "\\b",
                                  RegexOptions.IgnoreCase))
                    return BannedWholeWord[i];
            }
            return null;
        }

        // =====================================================================
        //  CASE 2 - the safety direction is one-way, and since 2026-08-26 it
        //  points CLOSED.
        // ---------------------------------------------------------------------
        //  ⛔ EVERY "must resolve OPEN" ASSERTION IN THIS CASE WAS INVERTED on
        //  2026-08-26 by owner ruling (WO-1223): "not acesable if not in table, if
        //  in table and works then yes". That is not a weakening of this case - the
        //  same failure modes are still driven, one by one, and each still has an
        //  assertion. What changed is the answer they must give.
        //
        //  WHAT KEEPS THIS FROM BECOMING "assert everything is shut", which would be
        //  a hollow case: the two SANCTIONED escapes are asserted to still OPEN -
        //  the kill switch (provenance "flag-off") and the UngatedIds allowlist. A
        //  blanket-close regression would red on both.
        // =====================================================================
        private static void Case2_Fallback(List<string> failures, List<string> notes)
        {
            const string sealedPayload =
                "{\"version\":1,\"dungeons\":{\"dg_bonecrypt\":{\"status\":\"sealed\"," +
                "\"headline\":\"The Bonecrypt is sealed\",\"body\":\"The Wardens drove iron through the doors.\"," +
                "\"sigil\":\"seal\"}}}";

            try
            {
                // --- cleared catalog: the ground state is CLOSED ---------------
                DungeonStatusCatalog.Clear();
                foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    if (DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-fallback] a CLEARED catalog resolved '" + id + "' OPEN - with no table " +
                                     "there is nothing saying it works, so it must be CLOSED (owner ruling 2026-08-26)");

                // --- null / empty / unknown id --------------------------------
                if (DungeonStatusCatalog.For(null).IsOpen) failures.Add("[door-fallback] For(null) was OPEN");
                if (DungeonStatusCatalog.For("").IsOpen) failures.Add("[door-fallback] For(\"\") was OPEN");
                if (DungeonStatusCatalog.For("dg_does_not_exist").IsOpen)
                    failures.Add("[door-fallback] an unknown id was OPEN - absence is a closure now");

                // --- garbage payload is REJECTED and leaves the table alone ----
                // NOTE: the garbage literal deliberately carries NO brace character -
                // the CLAUDE.md §1 brace-balance gate counts braces in string literals
                // too, and an unmatched one here would red the gate on a healthy file.
                if (DungeonStatusCatalog.ApplyPayload("<<< not json at all >>>", "test"))
                    failures.Add("[door-fallback] a garbage payload was ACCEPTED");
                if (DungeonStatusCatalog.ApplyPayload(null, "test"))
                    failures.Add("[door-fallback] a null payload was ACCEPTED");
                if (DungeonStatusCatalog.ApplyPayload("{\"version\":1}", "test"))
                    failures.Add("[door-fallback] a payload with no 'dungeons' map was ACCEPTED");

                // --- a good payload closes exactly one door, EXPLICITLY --------
                if (!DungeonStatusCatalog.ApplyPayload(sealedPayload, "test"))
                {
                    failures.Add("[door-fallback] the valid sealed payload was REJECTED");
                }
                else
                {
                    var crypt = DungeonStatusCatalog.For("dg_bonecrypt");
                    if (crypt.State != DungeonDoorState.Sealed)
                        failures.Add("[door-fallback] dg_bonecrypt resolved " + crypt.State + ", expected Sealed");
                    if (crypt.IsOpen)
                        failures.Add("[door-fallback] a Sealed door reported IsOpen");
                    if (crypt.Headline != "The Bonecrypt is sealed" || string.IsNullOrEmpty(crypt.Body))
                        failures.Add("[door-fallback] authored prose did not ride through the payload");
                    if (crypt.Sigil != "seal")
                        failures.Add("[door-fallback] the sigil did not ride through the payload");

                    // ⚠ THE INVERSION, and the one an implementer is most likely to get
                    // wrong. This block used to assert "every other dungeon is unaffected"
                    // (WO-1114 criterion 2). Under fail-closed a one-row payload leaves the
                    // OTHER ids row-less, so they are closed too - and that is the ruling
                    // working, not a bug. What must still hold is that they are closed for
                    // want of a ROW and are NAMED by the detector, never silently.
                    var rowless = new List<string>(DungeonStatusCatalog.MissingPortalRows());
                    foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    {
                        if (id == "dg_bonecrypt") continue;
                        if (DungeonStatusCatalog.IsOpen(id))
                            failures.Add("[door-fallback] '" + id + "' has NO row in this payload yet resolved OPEN");
                        if (!rowless.Contains(id))
                            failures.Add("[door-fallback] '" + id + "' is row-less but MissingPortalRows did not name " +
                                         "it - a door shut with nobody told is the WO-1223 defect all over again");
                    }
                    if (rowless.Contains("dg_bonecrypt"))
                        failures.Add("[door-fallback] the detector named dg_bonecrypt, which DOES have a row");
                    if (DungeonStatusCatalog.Provenance != "test")
                        failures.Add("[door-fallback] provenance was not stamped by ApplyPayload");
                }

                // --- A REJECTED PAYLOAD MUST NOT BLANK A GOOD TABLE -----------
                // Under fail-closed this matters MORE than it did: blanking the table no
                // longer means "everything opens", it means EVERY DOOR SHUTS.
                DungeonStatusCatalog.ApplyPayload("<html>502 Bad Gateway</html>", "live");
                if (DungeonStatusCatalog.For("dg_bonecrypt").State != DungeonDoorState.Sealed)
                    failures.Add("[door-fallback] a rejected payload BLANKED the standing table");

                // --- unknown status string is CLOSED --------------------------
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"dg_ember_deep\":{\"status\":\"banana\"}}}", "test"))
                    failures.Add("[door-fallback] a payload with an unknown status string was rejected outright");
                else if (DungeonStatusCatalog.IsOpen("dg_ember_deep"))
                    failures.Add("[door-fallback] an unknown status string FAILED OPEN - a row that does not parse " +
                                 "does not work, so it must not open (owner ruling 2026-08-26)");

                if (DungeonStatusCatalog.ParseState("SeAlEd", "t") != DungeonDoorState.Sealed)
                    failures.Add("[door-fallback] status parse is not case-insensitive");
                if (DungeonStatusCatalog.ParseState("open", "t") != DungeonDoorState.Open)
                    failures.Add("[door-fallback] the literal 'open' did not parse OPEN - the one path through");
                if (DungeonStatusCatalog.ParseState(null, "t") == DungeonDoorState.Open)
                    failures.Add("[door-fallback] a null status parsed OPEN - a row that says nothing does not say open");

                // --- an unshipped id is kept, and closes nothing real ----------
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"dg_nonexistent\":{\"status\":\"sealed\"}}}", "test"))
                    failures.Add("[door-fallback] a payload naming an unshipped id was rejected");
                if (DungeonStatusCatalog.RowCount != 1)
                    failures.Add("[door-fallback] the unshipped id was not kept in the table (rows=" +
                                 DungeonStatusCatalog.RowCount + ")");

                // --- a FUTURE version still parses (forward-compatible) -------
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":99,\"dungeons\":{\"dg_sunken_vault\":{\"status\":\"flooded\"}}}", "test"))
                    failures.Add("[door-fallback] a v99 payload was rejected - it must parse forward-compatibly");
                else if (DungeonStatusCatalog.For("dg_sunken_vault").State != DungeonDoorState.Flooded)
                    failures.Add("[door-fallback] a v99 payload parsed but did not apply");

                // --- THE ESCAPES STILL OPEN. Without these two, this case would be
                //     satisfiable by a For() that returns Sealed unconditionally.
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceFlagOff);
                foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    if (!DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-fallback] the KILL SWITCH (provenance=" +
                                     DungeonStatusCatalog.ProvenanceFlagOff + ") did not open '" + id +
                                     "' - it is the only lever that survives a bad table with no rebuild");

                DungeonStatusCatalog.Clear();
                foreach (string id in DungeonStatusCatalog.UngatedIds)
                    if (!DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-fallback] the allowlisted id '" + id + "' was CLOSED - fixtures, probes " +
                                     "and the Rootways crossroads have no door and can never have a row, so " +
                                     "fail-closed would bar them forever");

                notes.Add("door-fallback drove the catalog headlessly (no network, no PlayMode); direction = CLOSED");
            }
            finally
            {
                // Never leave a suite-local table standing for the next suite.
                DungeonStatusCatalog.Clear();
            }
        }

        // =====================================================================
        //  CASE 3 - the eight canon keys exist, match, and are real prose
        // =====================================================================
        private static void Case3_CanonKeys(List<string> failures, List<string> notes)
        {
            JObject res = File.Exists(CanonRes) ? ParseCanon(CanonRes, failures, "door-keys") : null;
            JObject sa = File.Exists(CanonSA) ? ParseCanon(CanonSA, failures, "door-keys") : null;
            if (res == null || sa == null)
            {
                failures.Add("[door-keys] one or both canon-strings.json copies are missing or unparseable");
                return;
            }

            foreach (string key in DoorCopyKeys)
            {
                string a = res[key]?.Type == JTokenType.String ? res[key].Value<string>() : null;
                string b = sa[key]?.Type == JTokenType.String ? sa[key].Value<string>() : null;

                if (string.IsNullOrWhiteSpace(a)) { failures.Add("[door-keys] Resources copy missing/empty '" + key + "' - the player would see [[missing:" + key + "]]"); continue; }
                if (string.IsNullOrWhiteSpace(b)) { failures.Add("[door-keys] StreamingAssets copy missing/empty '" + key + "' - the DEVICE would see [[missing:" + key + "]]"); continue; }
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    failures.Add("[door-keys] '" + key + "' DIFFERS between the two copies - editor and device would read different prose");

                int min = key.EndsWith("Body", StringComparison.Ordinal) ? MinBodyChars : MinHeadlineChars;
                if (a.Trim().Length < min)
                    failures.Add("[door-keys] '" + key + "' is " + a.Trim().Length + " chars (< " + min + ") - that is a placeholder, not prose");
            }

            notes.Add("door-keys checked " + DoorCopyKeys.Length + " keys in both copies");
        }

        // =====================================================================
        //  CASE 4 - the status domain is exactly the AuthoredPortal ids
        // ---------------------------------------------------------------------
        //  ⚠ THE EXPECTED COUNT WENT 4 -> 6 on 2026-08-26. The owner ruled
        //  dg_folks_granary and dg_healers_cottage GATABLE (WO-1223), so they joined
        //  PortalDungeonIds. The count is still pinned as a LITERAL and deliberately
        //  so: reading it off domain.Length would make this branch assert nothing at
        //  all, which is the hollow shape case 6 exists to punish. When a dungeon is
        //  legitimately added, this number moves in the SAME edit as the array.
        // =====================================================================
        private static void Case4_IdContract(List<string> failures, List<string> notes)
        {
            const int ExpectedDomainSize = 6;
            var domain = DungeonStatusCatalog.PortalDungeonIds;
            if (domain == null || domain.Length != ExpectedDomainSize)
            {
                failures.Add("[door-ids] PortalDungeonIds is not the " + ExpectedDomainSize +
                             " AuthoredPortal ids (count=" +
                             (domain == null ? "null" : domain.Length.ToString()) + ")");
                return;
            }

            // Every id in the domain must really be an AuthoredPortal row.
            if (!File.Exists(SpawnerSrc))
            {
                notes.Add(RegressionOutcome.PartialSkip("door-ids source lint",
                    "missing " + SpawnerSrc + " - the id set was not cross-checked against the portal table"));
            }
            else
            {
                string src = File.ReadAllText(SpawnerSrc);
                foreach (string id in domain)
                {
                    if (!src.Contains("new AuthoredPortal(\"" + id + "\""))
                        failures.Add("[door-ids] '" + id + "' is in the status domain but has NO AuthoredPortal row " +
                                     "in DungeonWorldPortalSpawner - a door that does not exist cannot be gated");
                }
            }

            // Fixtures, probes and the crossroads must never be gatable.
            foreach (string id in MustNotBeGated)
            {
                if (Array.IndexOf(domain, id) >= 0)
                    failures.Add("[door-ids] '" + id + "' is in the status domain - it is a fixture/probe/crossroads " +
                                 "with no dungeon door and must never be gated by this system");
            }

            // dg_not_yet_baked is a deliberately non-existent string used by another
            // suite. It must never be treated as a real dungeon.
            if (Array.IndexOf(domain, "dg_not_yet_baked") >= 0)
                failures.Add("[door-ids] 'dg_not_yet_baked' is a deliberately non-existent test string, not a dungeon");

            notes.Add("door-ids domain = " + string.Join(",", domain));
        }

        // =====================================================================
        //  CASE 5 - the appearance owner (stands down until that phase lands)
        // =====================================================================
        private static void Case5_AppearanceOwner(List<string> failures, List<string> notes)
        {
            if (!File.Exists(SpawnerSrc))
            {
                notes.Add(RegressionOutcome.PartialSkip("door-appearance", "missing " + SpawnerSrc));
                return;
            }

            string src = File.ReadAllText(SpawnerSrc);
            if (!src.Contains(ApplyDoorStateSymbol))
            {
                // HONEST HOLE, NAMED. The appearance half of WO-1114 (the sigil
                // treatment on a closed portal) is a later phase. Do not delete this
                // case to tidy the log - the moment ApplyDoorState lands, the
                // assertions below start running and the hole closes itself.
                notes.Add(RegressionOutcome.PartialSkip("door-appearance",
                    "DungeonWorldPortalSpawner." + ApplyDoorStateSymbol + " does not exist yet (WO-1114 " +
                    "appearance phase unbuilt) - the one-appearance-owner rule is NOT yet asserted"));
                return;
            }

            // Once it exists: the sigil must be MEASURED through the existing helpers,
            // not guessed, and it must be re-seated when the real portal art swaps in.
            foreach (string helper in new[] { "MeasurePortalBounds", "OpeningCentre", "OpeningTargetSize" })
                if (!src.Contains(helper))
                    failures.Add("[door-appearance] " + ApplyDoorStateSymbol + " exists but the file does not use '" +
                                 helper + "' - the sigil is guessed, not measured");

            if (!src.Contains("SwapInSharedStructureAsync"))
                failures.Add("[door-appearance] no SwapInSharedStructureAsync in the file - the sigil cannot re-seat " +
                             "when the real portal art swaps in over the placeholder arch");

            notes.Add("door-appearance asserted against " + ApplyDoorStateSymbol);
        }

        // =====================================================================
        //  CASE 6 - WO-1223. COMPLETENESS: is every REACHABLE dungeon accounted for?
        // ---------------------------------------------------------------------
        //  ⭐ WHY CASE 4 COULD NEVER HAVE CAUGHT THIS. Case 4 reads "the status domain
        //  is EXACTLY the four AuthoredPortal ids" and it is genuinely strict - about
        //  the four it already knows. It ITERATES PortalDungeonIds, so a fifth
        //  reachable dungeon is not something it fails on; it is something it cannot
        //  see. That is a hollow SCOPE, not a hollow pass: nothing is skipped, the
        //  suite reports OK, and the thing it exists to protect is untested.
        //  dg_healers_cottage was reachable, populated and ungateable for four days
        //  under a green DUNGEON_STATUS_OK.
        //
        //  THE REACHABILITY AUTHORITY, and why it is these three sources ANDed:
        //  a portal exists in the world if, and only if, TryPlace builds one, and
        //  TryPlace needs all three of -
        //    (1) a row in DungeonWorldPortalSpawner.AuthoredPortals - TryGetAuthored
        //        fails the def otherwise and logs "NO authored world position";
        //    (2) a DungeonDef, which for every live dungeon comes from a LoadDefs
        //        injection keyed on a `const string X = "dg_..."` literal (the
        //        Resources/Dungeons folder that foreach walks is empty in practice -
        //        both legacy .assets were deleted 2026-08-22);
        //    (3) that scene ENABLED in EditorBuildSettings - LoadDefs gates every
        //        injection on CanStreamedLevelBeLoaded, which is that list at runtime.
        //  Anything missing one of the three cannot put a door in front of a player,
        //  so the intersection IS reachability. The composed-layout JSON under
        //  Resources/Data/Canonical/dungeon-layouts was rejected as the authority: it
        //  carries dg_stair_rig and dg_stairwell_probe (fixtures with no portal and
        //  their scenes switched off in the build), so it over-reports, and it would
        //  have made this case red on ids no player can ever stand in.
        //
        //  THE HOLLOW ROADS is added by hand and that is not a cheat: its portal row
        //  is DERIVED (TryDeriveHollowRoadsPortal), not typed, and its def is injected
        //  through BiomeRoads.TunnelSceneId rather than a literal, so neither regex
        //  finds it. Both source symbols are asserted present before it is counted.
        //
        //  ⛔ THE ONE THING THIS CASE MAY NOT DO IS BE MADE GREEN BY ADDING AN ID.
        //  A reachable dungeon in neither list is a FINDING for the owner: whether it
        //  should be gateable or exempt is a design call, and a MustNotBeGated entry
        //  without a stated reason is precisely the softening this suite exists to
        //  stop. It fails by name and stays failed until she rules.
        // =====================================================================
        private static void Case6_Coverage(List<string> failures, List<string> notes)
        {
            if (!File.Exists(SpawnerSrc))
            {
                // NOT a PartialSkip. Without the spawner there is no reachability set, and a
                // completeness check that computed nothing must never report coverage.
                failures.Add("[door-coverage] missing " + SpawnerSrc + " - reachability cannot be computed, " +
                             "so coverage cannot be claimed");
                return;
            }

            string src = File.ReadAllText(SpawnerSrc);

            var authored = MatchIds(src, "new AuthoredPortal\\(\"(dg_[a-z0-9_]+)\"");
            var injected = MatchIds(src, "const\\s+string\\s+\\w+\\s*=\\s*\"(dg_[a-z0-9_]+)\"");

            // The derived tunnel mouth - see the note above.
            string tunnel = DeNelle.Core.World.BiomeRoads.TunnelSceneId;
            bool tunnelWired = src.Contains("TryDeriveHollowRoadsPortal") && src.Contains("BiomeRoads.TunnelSceneId");
            if (tunnelWired)
            {
                if (!authored.Contains(tunnel)) authored.Add(tunnel);
                if (!injected.Contains(tunnel)) injected.Add(tunnel);
            }
            else
            {
                notes.Add(RegressionOutcome.PartialSkip("door-coverage tunnel",
                    "TryDeriveHollowRoadsPortal / BiomeRoads.TunnelSceneId no longer appear in the spawner - " +
                    "'" + tunnel + "' was NOT counted as reachable"));
            }

            if (authored.Count == 0)
                failures.Add("[door-coverage] found ZERO AuthoredPortal rows in the spawner - the reachability " +
                             "lint matched nothing, so this case asserted nothing");
            if (injected.Count == 0)
                failures.Add("[door-coverage] found ZERO injected dungeon ids in LoadDefs - the reachability " +
                             "lint matched nothing, so this case asserted nothing");

            var enabledScenes = new HashSet<string>(StringComparer.Ordinal);
            var buildList = UnityEditor.EditorBuildSettings.scenes;
            for (int i = 0; i < buildList.Length; i++)
            {
                var entry = buildList[i];
                if (entry == null || !entry.enabled || string.IsNullOrEmpty(entry.path)) continue;
                enabledScenes.Add(Path.GetFileNameWithoutExtension(entry.path));
            }
            if (enabledScenes.Count == 0)
                failures.Add("[door-coverage] EditorBuildSettings carries no enabled scenes - reachability " +
                             "cannot be computed");

            // REACHABLE = authored AND injected AND in the enabled build list.
            var reachable = new List<string>();
            for (int i = 0; i < authored.Count; i++)
            {
                string id = authored[i];
                if (injected.Contains(id) && enabledScenes.Contains(id)) reachable.Add(id);
            }
            reachable.Sort(StringComparer.Ordinal);

            if (reachable.Count == 0)
            {
                failures.Add("[door-coverage] the reachable set came out EMPTY. That is not a pass - it means " +
                             "the three sources stopped agreeing and this case is no longer measuring anything");
                return;
            }

            // An authored door whose def is never injected places nothing (TryPlace logs it).
            // Reported as a note, not a failure: it is an authoring gap in a lane this suite
            // does not own, and reddening on it would put this case's fate outside its scope.
            for (int i = 0; i < authored.Count; i++)
                if (!injected.Contains(authored[i]))
                    notes.Add("door-coverage: '" + authored[i] + "' has an AuthoredPortal row but no LoadDefs " +
                              "injection - no def, so no portal is placed");
            for (int i = 0; i < injected.Count; i++)
                if (!authored.Contains(injected[i]))
                    notes.Add("door-coverage: '" + injected[i] + "' is injected in LoadDefs but has no " +
                              "AuthoredPortal row - TryPlace warns and places nothing");

            // A GATED door that is not reachable is the mirror finding: something can be
            // closed that no player can walk to.
            foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                if (!reachable.Contains(id))
                    failures.Add("[door-coverage] '" + id + "' is in PortalDungeonIds but is NOT reachable " +
                                 "(authored=" + authored.Contains(id) + " injected=" + injected.Contains(id) +
                                 " inBuild=" + enabledScenes.Contains(id) + ") - a door nobody can reach is " +
                                 "being gated");

            // ── THE ASSERTION THIS CASE EXISTS FOR ───────────────────────────
            var unaccounted = new List<string>();
            foreach (string id in reachable)
            {
                bool gated = Array.IndexOf(DungeonStatusCatalog.PortalDungeonIds, id) >= 0;
                bool exempt = Array.IndexOf(MustNotBeGated, id) >= 0;

                if (gated && exempt)
                {
                    failures.Add("[door-coverage] '" + id + "' is in BOTH PortalDungeonIds and MustNotBeGated - " +
                                 "the two lists contradict each other");
                    continue;
                }
                if (gated || exempt) continue;

                unaccounted.Add(id);

                // !! WARNING, NOT A FAILURE -- OWNER RULING 2026-08-26 ("regression should show that
                // as warning not error"), and the severity moved because THE RISK MODEL MOVED.
                //
                // This message used to read "It reads OPEN and there is no row to close it, so it
                // cannot be sealed when it breaks", and it FAILED the suite. Both were correct in a
                // FAIL-OPEN world: an uncovered id was a live hole that nothing could shut.
                //
                // Under the owner's fail-closed ruling (same day) an uncovered id now SEALS itself --
                // DungeonStatusCatalog.For returns Sealed for an id absent from the table. So the
                // harm inverted: it is no longer a SECURITY hole that cannot be closed, it is an
                // AVAILABILITY gap (a door the player cannot open and we have no row to open with).
                // That is worth surfacing every run and is not worth blocking a build over.
                //
                // !! Do NOT quietly restore this to failures.Add. If fail-closed is ever reverted,
                // this severity MUST go back with it -- the two are one decision.
                notes.Add("[door-coverage] WARNING: '" + id + "' is a REACHABLE dungeon (authored portal + " +
                          "injected def + enabled build scene) that appears in NEITHER " +
                          "DungeonStatusCatalog.PortalDungeonIds NOR DungeonStatusRegression.MustNotBeGated. " +
                          "Under fail-closed it now SEALS (provenance 'no-table'), so this is an AVAILABILITY " +
                          "gap, not an outage: the player cannot enter it and we have no row to open it with. " +
                          "⛔ Do NOT resolve this by adding the id to a list - which list it belongs in is " +
                          "the OWNER's ruling (WO-1223).");
            }

            // ── The manifest is the contract the node test reads ─────────────
            CheckManifest(failures, notes, reachable);

            // ── The reverse direction, pinned as behaviour and not as a log line ──
            CheckMissingRowDetector(failures);

            notes.Add("door-coverage reachable(" + reachable.Count + ") = " + string.Join(",", reachable.ToArray()) +
                      "; unaccounted(" + unaccounted.Count + ") = " +
                      (unaccounted.Count == 0 ? "none" : string.Join(",", unaccounted.ToArray())));
        }

        /// <summary>
        /// api/_lib/dungeon-manifest.json must be EXACTLY the reachable set, and each entry's
        /// accounting must match the C# list the id is actually in. This is what stops the
        /// manifest from being a second copy of the truth that quietly rots: it is a duplicate
        /// on purpose (node cannot read a C# array) and it is guarded in both directions.
        /// </summary>
        private static void CheckManifest(List<string> failures, List<string> notes, List<string> reachable)
        {
            if (!File.Exists(ManifestPath))
            {
                failures.Add("[door-coverage] missing " + ManifestPath + " - it is the client/server coverage " +
                             "contract; test/dungeon-status.manifest.test.js asserts the DB half against it");
                return;
            }

            JObject root;
            try { root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(ManifestPath)); }
            catch (Exception ex)
            {
                failures.Add("[door-coverage] " + ManifestPath + " failed to parse: " + ex.GetType().Name +
                             ": " + ex.Message);
                return;
            }

            var rows = root == null ? null : root["dungeons"] as JArray;
            if (rows == null || rows.Count == 0)
            {
                failures.Add("[door-coverage] " + ManifestPath + " carries no 'dungeons' array");
                return;
            }

            var listed = new List<string>();
            foreach (JToken row in rows)
            {
                string id = row["id"]?.Value<string>();
                string accounting = row["accounting"]?.Value<string>();
                string reason = row["reason"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(id))
                {
                    failures.Add("[door-coverage] the manifest carries an entry with no id");
                    continue;
                }
                if (listed.Contains(id))
                {
                    failures.Add("[door-coverage] the manifest lists '" + id + "' twice");
                    continue;
                }
                listed.Add(id);

                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 20)
                    failures.Add("[door-coverage] manifest entry '" + id + "' has no stated reason - an entry " +
                                 "without one is the softening this suite exists to stop");

                bool gated = Array.IndexOf(DungeonStatusCatalog.PortalDungeonIds, id) >= 0;
                bool exempt = Array.IndexOf(MustNotBeGated, id) >= 0;
                string expected = gated ? "portal-gated" : (exempt ? "not-gated" : "unaccounted");
                if (!string.Equals(accounting, expected, StringComparison.Ordinal))
                    failures.Add("[door-coverage] manifest entry '" + id + "' says accounting='" +
                                 (accounting ?? "null") + "' but the code says '" + expected +
                                 "' - the manifest has drifted from the C# lists");
            }

            foreach (string id in reachable)
                if (!listed.Contains(id))
                    failures.Add("[door-coverage] '" + id + "' is reachable but is MISSING from " + ManifestPath +
                                 " - the server side of the contract cannot see it at all");

            foreach (string id in listed)
                if (!reachable.Contains(id))
                    failures.Add("[door-coverage] " + ManifestPath + " lists '" + id + "', which is NOT reachable " +
                                 "in this build - the manifest is claiming coverage of a door that does not exist");

            notes.Add("door-coverage manifest = " + listed.Count + " entries, checked against the reachable set " +
                      "in both directions");
        }

        /// <summary>
        /// WO-1223 part 2 — the reverse direction, asserted as BEHAVIOUR.
        /// DungeonStatusCatalog has always traced a row with no dungeon; a dungeon with no
        /// row said nothing. MissingPortalRows is that detector, and this pins it so the
        /// detector cannot itself rot into a no-op.
        /// </summary>
        private static void CheckMissingRowDetector(List<string> failures)
        {
            try
            {
                DungeonStatusCatalog.Clear();
                if (DungeonStatusCatalog.MissingPortalRows().Length != DungeonStatusCatalog.PortalDungeonIds.Length)
                    failures.Add("[door-coverage] a cleared catalog did not report every portal id as row-less");

                var sb = new System.Text.StringBuilder();
                sb.Append("{\"version\":1,\"dungeons\":");
                sb.Append("{");
                for (int i = 0; i < DungeonStatusCatalog.PortalDungeonIds.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("\"").Append(DungeonStatusCatalog.PortalDungeonIds[i]).Append("\":");
                    sb.Append("{\"status\":\"open\"}");
                }
                sb.Append("}");
                sb.Append("}");

                if (!DungeonStatusCatalog.ApplyPayload(sb.ToString(), "test"))
                {
                    failures.Add("[door-coverage] the full-coverage payload was rejected");
                }
                else if (DungeonStatusCatalog.MissingPortalRows().Length != 0)
                {
                    failures.Add("[door-coverage] a payload covering every portal id still reported missing rows: " +
                                 string.Join(",", DungeonStatusCatalog.MissingPortalRows()));
                }

                // One row only: the other three must be NAMED, not silently open.
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"dg_bonecrypt\":{\"status\":\"sealed\"}}}", "test"))
                {
                    failures.Add("[door-coverage] the single-row payload was rejected");
                }
                else
                {
                    var missing = DungeonStatusCatalog.MissingPortalRows();
                    if (missing.Length != DungeonStatusCatalog.PortalDungeonIds.Length - 1)
                        failures.Add("[door-coverage] a payload covering ONE of " +
                                     DungeonStatusCatalog.PortalDungeonIds.Length + " portal ids reported " +
                                     missing.Length + " missing rows - the detector is not counting");
                    if (Array.IndexOf(missing, "dg_bonecrypt") >= 0)
                        failures.Add("[door-coverage] the detector named a dungeon that DOES have a row");

                    // ⛔ INVERTED 2026-08-26 (owner ruling, WO-1223). This block used to
                    // assert "a row-less dungeon did not resolve OPEN - the detector must
                    // not have changed the fail-open direction". Fail-open is retired: a
                    // row-less dungeon is now CLOSED, and the detector's job is to make sure
                    // an operator is TOLD which doors that shut. Both halves are asserted.
                    foreach (string id in missing)
                        if (DungeonStatusCatalog.IsOpen(id))
                            failures.Add("[door-coverage] the row-less dungeon '" + id + "' resolved OPEN - " +
                                         "\"not acesable if not in table\" (owner ruling 2026-08-26)");
                }
            }
            finally
            {
                DungeonStatusCatalog.Clear();
            }
        }

        // =====================================================================
        //  CASE 7 - WO-1223 / owner ruling 2026-08-26. THE DEFAULT ITSELF.
        // ---------------------------------------------------------------------
        //  Owner, verbatim: "not acesable if not in table, if in table and works
        //  then yes". Adding two ids to PortalDungeonIds satisfies the COVERAGE half
        //  of that sentence and none of the SAFETY half. Coverage says every dungeon
        //  has somewhere to be listed; this case says what happens when the listing
        //  is not there, and that is the half a player feels.
        //
        //  ⭐ WHY IT IS SEPARATE FROM CASE 2. Case 2 drives the fallback matrix
        //  through ApplyPayload - it always has a table by the time it asks. The
        //  modes that actually cost the owner the black screen are the ones where
        //  there is NO table at all (a device that never reached the network, a
        //  request that timed out, a 502 rejected before it landed). Those never
        //  reach ApplyPayload, so no amount of payload-driven testing sees them.
        //  This case asks For() directly, once per failure mode, by name.
        //
        //  THE FIVE MODES, each mapped to the branch of DungeonStatusCatalog.For
        //  that decides it - so a future reader can find the code from the failure:
        //    1 id absent from a present table        -> For branch (e)
        //    2 no table at all: server unreachable   -> For branch (d)
        //    3 no table at all: request timed out    -> For branch (d), same seam
        //    4 malformed / empty payload rejected    -> ApplyPayload false, then (d)
        //    5 row present, status does not parse    -> ParseState default -> Sealed
        //
        //  ⛔ AND THE TWO ESCAPES ARE ASSERTED OPEN. Without them a For() hardwired
        //  to return Sealed would satisfy this case, which would be a hollow pass of
        //  exactly the kind case 6's header calls the most expensive class in the repo.
        // =====================================================================
        private static void Case7_FailClosedDefault(List<string> failures, List<string> notes)
        {
            const string Probe = "dg_healers_cottage";   // the id the owner black-screened in
            int modes = 0;

            try
            {
                // ---- MODE 1: a table exists, this id has no row in it -------------
                // The literal sentence of the ruling. Seed a table that covers a
                // DIFFERENT id so the table is real and only the row is missing.
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"dg_starter_loop\":{\"status\":\"open\"}}}", "test"))
                {
                    failures.Add("[door-default] mode 1 setup failed - the one-row payload was rejected");
                }
                else
                {
                    modes++;
                    if (!DungeonStatusCatalog.Loaded || DungeonStatusCatalog.RowCount != 1)
                        failures.Add("[door-default] mode 1 setup is not what it claims (loaded=" +
                                     DungeonStatusCatalog.Loaded + " rows=" + DungeonStatusCatalog.RowCount + ")");
                    if (DungeonStatusCatalog.IsOpen(Probe))
                        failures.Add("[door-default] MODE 1 FAIL-OPEN: '" + Probe + "' is absent from a present " +
                                     "table and resolved OPEN. Owner ruling 2026-08-26: not accessible if not " +
                                     "in the table");
                    if (DungeonStatusCatalog.For(Probe).State != DungeonDoorState.Sealed)
                        failures.Add("[door-default] mode 1: an absent id resolved " +
                                     DungeonStatusCatalog.For(Probe).State + ", expected Sealed - the closed " +
                                     "default must carry a state the door copy has prose for");
                    // The prose must come from canon, not from this file (CLAUDE.md §7).
                    var info = DungeonStatusCatalog.For(Probe);
                    if (!string.IsNullOrEmpty(info.Headline) || !string.IsNullOrEmpty(info.Body))
                        failures.Add("[door-default] mode 1: the closed default carries hardcoded prose - it must " +
                                     "be null so DungeonSealedDoorPanel falls back to canon-strings.json");
                    // And the player must still be told which doors this shut.
                    if (Array.IndexOf(DungeonStatusCatalog.MissingPortalRows(), Probe) < 0)
                        failures.Add("[door-default] mode 1: '" + Probe + "' is shut for want of a row and " +
                                     "MissingPortalRows did not name it");
                }

                // ---- MODE 2: NO TABLE AT ALL - server unreachable ----------------
                // What the client actually holds after a DNS failure / no connectivity
                // on a device with no cache: DungeonStatusService.LoadCache misses, so
                // Clear(ProvenanceDefault) is the whole state. Reproduced exactly.
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                modes++;
                if (DungeonStatusCatalog.Loaded)
                    failures.Add("[door-default] mode 2 setup is not what it claims - a table is still standing");
                foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    if (DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-default] MODE 2 FAIL-OPEN: with NO status table (server unreachable, " +
                                     "no cache) '" + id + "' resolved OPEN. An unreachable server must not be " +
                                     "able to open a door it cannot describe");

                // ---- MODE 3: NO TABLE AT ALL - the request TIMED OUT -------------
                // DungeonStatusService.RefreshAsync surfaces a req.timeout expiry as a
                // non-Success result and RETURNS without touching the table, so the
                // client state is byte-identical to mode 2. Asserted separately anyway:
                // the two are the same seam today and a future refactor could split them,
                // and a timeout is the mode most likely to be "handled" back to open.
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                modes++;
                if (DungeonStatusCatalog.For(Probe).IsOpen)
                    failures.Add("[door-default] MODE 3 FAIL-OPEN: a timed-out status fetch left '" + Probe +
                                 "' OPEN");
                if (DungeonStatusCatalog.MissingPortalRows().Length != DungeonStatusCatalog.PortalDungeonIds.Length)
                    failures.Add("[door-default] mode 3: with no table the detector must name EVERY portal id as " +
                                 "row-less - the operator's only warning that the whole domain is shut");

                // ---- MODE 4: malformed AND empty payloads ------------------------
                // Each must be REJECTED (so it cannot poison the cache) and must leave
                // the client closed rather than open.
                foreach (string bad in new[] { "<html>502 Bad Gateway</html>", "", "   ", "null" })
                {
                    DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                    if (DungeonStatusCatalog.ApplyPayload(bad, "live"))
                        failures.Add("[door-default] mode 4: a malformed/empty payload was ACCEPTED (len=" +
                                     bad.Length + ")");
                    if (DungeonStatusCatalog.IsOpen(Probe))
                        failures.Add("[door-default] MODE 4 FAIL-OPEN: after a rejected payload (len=" + bad.Length +
                                     ") '" + Probe + "' resolved OPEN");
                }
                // An empty-but-WELL-FORMED table is the subtlest one: it parses, it is
                // accepted, and it says nothing about any dungeon. Absence is a closure.
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                if (!DungeonStatusCatalog.ApplyPayload("{\"version\":1,\"dungeons\":{ }}", "live"))
                    failures.Add("[door-default] mode 4: a well-formed EMPTY table was rejected - it is a valid " +
                                 "answer and must be accepted, then read as closed");
                else if (DungeonStatusCatalog.IsOpen(Probe))
                    failures.Add("[door-default] MODE 4 FAIL-OPEN: an EMPTY dungeons map left '" + Probe +
                                 "' OPEN. An empty table names nothing, so it opens nothing");
                modes++;

                // ---- MODE 5: a row IS present, but its status does not parse -----
                // "if in table and works then yes" - the second clause. A row that does
                // not parse does not work.
                foreach (string junk in new[] { "banana", "OPEN_", "", "   " })
                {
                    string payload = "{\"version\":1,\"dungeons\":{\"" + Probe + "\":{\"status\":\"" + junk + "\"}}}";
                    DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                    if (!DungeonStatusCatalog.ApplyPayload(payload, "live"))
                    {
                        failures.Add("[door-default] mode 5: a payload with status='" + junk + "' was rejected " +
                                     "outright - it is well-formed JSON and must parse, then read as closed");
                        continue;
                    }
                    if (DungeonStatusCatalog.IsOpen(Probe))
                        failures.Add("[door-default] MODE 5 FAIL-OPEN: '" + Probe + "' carries status='" + junk +
                                     "', which does not parse, and it resolved OPEN. Owner ruling 2026-08-26: " +
                                     "in the table AND WORKS is the condition, not merely in the table");
                }
                // A row with NO status key at all - the same clause, one level subtler.
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                if (DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"" + Probe + "\":{ }}}", "live") &&
                    DungeonStatusCatalog.IsOpen(Probe))
                    failures.Add("[door-default] MODE 5 FAIL-OPEN: a row with no 'status' field at all left '" +
                                 Probe + "' OPEN");
                modes++;

                // ---- THE ONE PATH THROUGH -------------------------------------
                // Without this the whole case is satisfiable by returning Sealed always.
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"" + Probe + "\":{\"status\":\"open\"}}}", "live"))
                    failures.Add("[door-default] the good payload was rejected - the success path is unproven");
                else if (!DungeonStatusCatalog.IsOpen(Probe))
                    failures.Add("[door-default] FAIL-CLOSED TOO FAR: a well-formed row saying 'open' did NOT " +
                                 "open '" + Probe + "'. \"if in table and works then yes\" is half the ruling");

                // ---- ESCAPE 1: the kill switch ---------------------------------
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceFlagOff);
                foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    if (!DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-default] the kill switch (FeatureFlags.DungeonStatus=0, provenance='" +
                                     DungeonStatusCatalog.ProvenanceFlagOff + "') did not open '" + id + "'. " +
                                     "Fail-closed is only safe to ship BECAUSE this lever exists");

                // ---- ESCAPE 2: the UngatedIds allowlist ------------------------
                DungeonStatusCatalog.Clear(DungeonStatusCatalog.ProvenanceDefault);
                if (DungeonStatusCatalog.UngatedIds == null || DungeonStatusCatalog.UngatedIds.Length == 0)
                    failures.Add("[door-default] UngatedIds is empty - the crossroads and the fixtures have no " +
                                 "door and can never have a row, so fail-closed would bar them forever");
                foreach (string id in DungeonStatusCatalog.UngatedIds)
                {
                    if (!DungeonStatusCatalog.IsUngated(id))
                        failures.Add("[door-default] IsUngated('" + id + "') is false for an id in its own array");
                    if (!DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-default] the allowlisted id '" + id + "' resolved CLOSED");
                    if (Array.IndexOf(DungeonStatusCatalog.PortalDungeonIds, id) >= 0)
                        failures.Add("[door-default] '" + id + "' is on BOTH the allowlist and the gated domain - " +
                                     "the allowlist would silently override the gate");
                }
                if (DungeonStatusCatalog.IsUngated("dg_healers_cottage"))
                    failures.Add("[door-default] 'dg_healers_cottage' is on the allowlist - the owner ruled it " +
                                 "GATABLE on 2026-08-26, not exempt");
                if (DungeonStatusCatalog.IsUngated("dg_folks_granary"))
                    failures.Add("[door-default] 'dg_folks_granary' is on the allowlist - the owner ruled it " +
                                 "GATABLE on 2026-08-26, not exempt");

                if (modes != 5)
                    failures.Add("[door-default] only " + modes + " of the 5 failure modes were actually driven - " +
                                 "a default audit that skipped a mode has certified nothing");

                notes.Add("door-default drove " + modes + "/5 failure modes CLOSED plus the success path and " +
                          "both sanctioned escapes (kill switch, allowlist)");
            }
            finally
            {
                DungeonStatusCatalog.Clear();
            }
        }

        /// <summary>Distinct capture-group-1 matches, in source order.</summary>
        private static List<string> MatchIds(string src, string pattern)
        {
            var found = new List<string>();
            foreach (Match m in Regex.Matches(src, pattern))
            {
                string id = m.Groups[1].Value;
                if (!string.IsNullOrEmpty(id) && !found.Contains(id)) found.Add(id);
            }
            return found;
        }

        // =====================================================================
        //  Shared helper
        // =====================================================================
        private static JObject ParseCanon(string path, List<string> failures, string caseName)
        {
            try
            {
                return JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                failures.Add("[" + caseName + "] " + Path.GetFileName(path) + " failed to parse: " +
                             ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }
    }
}
