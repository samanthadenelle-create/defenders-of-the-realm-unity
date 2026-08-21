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
// AND THE ONE THAT CAN HURT A PLAYER:
//   Every failure in this system must resolve toward OPEN. A backend typo, a
//   stale cache or a dropped network must never lock a player out of working
//   content. Case 2 drives the whole fallback matrix directly against
//   DungeonStatusCatalog — which is transport-free precisely so this can run
//   headlessly, with no network and no PlayMode.
//
// Cases:
//   1 [door-copy]     No build vocabulary in any of the eight dungeon door
//                     strings, in EITHER copy of canon-strings.json. Scanned on
//                     the PARSED VALUES only, so the authoring _comment stays
//                     free to name the very words it bans (the same reason
//                     GlossaryRegression.cs:372-373 scans parsed fields).
//   2 [door-fallback] The safety direction is one-way: unknown id, unknown
//                     status, absent id, null id, garbage payload and a version
//                     mismatch all resolve OPEN, and a rejected payload leaves
//                     the standing table intact rather than blanking it.
//   3 [door-keys]     All eight canon keys exist in BOTH copies, are non-empty,
//                     are byte-equal across the copies, and carry real prose
//                     (not a placeholder). Guarantees "[[missing:key]]" can
//                     never reach a player.
//   4 [door-ids]      The status domain is EXACTLY the four AuthoredPortal ids.
//                     Fixtures and probes (dg_descent_probe, dg_stair_rig,
//                     dg_stairwell_probe) and the CROSSROADS dg_hollow_roads are
//                     asserted ABSENT — gating any of them would gate something
//                     that has no door.
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

        /// <summary>Ids that MUST NOT appear in any status table. Three fixtures/probes
        /// (no portal exists for them) and one CROSSROADS that is gated by
        /// FeatureFlags.BiomeRoads, not by this system.</summary>
        private static readonly string[] MustNotBeGated =
        {
            "dg_hollow_roads", "dg_descent_probe", "dg_stair_rig", "dg_stairwell_probe",
        };

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
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DUNGEON STATUS OK - the eight door strings are byte-equal across both canon-strings.json " +
                         "copies and carry no build vocabulary, every failure path in DungeonStatusCatalog resolves " +
                         "OPEN (unknown id, unknown status, absent id, null id, garbage payload, version mismatch), " +
                         "a rejected payload leaves the standing table intact, and the status domain is exactly the " +
                         "four AuthoredPortal ids with the fixtures/probes/crossroads asserted ungatable" + noteStr;
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
        //  CASE 2 - the safety direction is one-way: everything resolves OPEN
        // =====================================================================
        private static void Case2_Fallback(List<string> failures, List<string> notes)
        {
            const string sealedPayload =
                "{\"version\":1,\"dungeons\":{\"dg_bonecrypt\":{\"status\":\"sealed\"," +
                "\"headline\":\"The Bonecrypt is sealed\",\"body\":\"The Wardens drove iron through the doors.\"," +
                "\"sigil\":\"seal\"}}}";

            try
            {
                // --- all-open ground state ------------------------------------
                DungeonStatusCatalog.Clear();
                foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    if (!DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-fallback] cleared catalog did not resolve '" + id + "' OPEN");

                // --- null / empty / unknown id --------------------------------
                if (!DungeonStatusCatalog.For(null).IsOpen) failures.Add("[door-fallback] For(null) was not OPEN");
                if (!DungeonStatusCatalog.For("").IsOpen) failures.Add("[door-fallback] For(\"\") was not OPEN");
                if (!DungeonStatusCatalog.For("dg_does_not_exist").IsOpen)
                    failures.Add("[door-fallback] an unknown id was not OPEN");

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

                // --- a good payload closes exactly one door --------------------
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

                    // "every other dungeon is unaffected" - WO criterion 2, second half
                    foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    {
                        if (id == "dg_bonecrypt") continue;
                        if (!DungeonStatusCatalog.IsOpen(id))
                            failures.Add("[door-fallback] closing dg_bonecrypt also closed '" + id + "'");
                    }
                    if (DungeonStatusCatalog.Provenance != "test")
                        failures.Add("[door-fallback] provenance was not stamped by ApplyPayload");
                }

                // --- A REJECTED PAYLOAD MUST NOT BLANK A GOOD TABLE -----------
                // This is the one that protects a player on a bad backend day: the
                // cached table stays standing when a live payload arrives corrupt.
                DungeonStatusCatalog.ApplyPayload("<html>502 Bad Gateway</html>", "live");
                if (DungeonStatusCatalog.For("dg_bonecrypt").State != DungeonDoorState.Sealed)
                    failures.Add("[door-fallback] a rejected payload BLANKED the standing table");

                // --- unknown status string is OPEN, never fail-closed ---------
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"dg_ember_deep\":{\"status\":\"banana\"}}}", "test"))
                    failures.Add("[door-fallback] a payload with an unknown status string was rejected outright");
                else if (!DungeonStatusCatalog.IsOpen("dg_ember_deep"))
                    failures.Add("[door-fallback] an unknown status string FAILED CLOSED - it must resolve OPEN");

                if (DungeonStatusCatalog.ParseState("SeAlEd", "t") != DungeonDoorState.Sealed)
                    failures.Add("[door-fallback] status parse is not case-insensitive");
                if (DungeonStatusCatalog.ParseState(null, "t") != DungeonDoorState.Open)
                    failures.Add("[door-fallback] a null status was not OPEN");

                // --- an unshipped id does not throw and does not close others --
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":1,\"dungeons\":{\"dg_nonexistent\":{\"status\":\"sealed\"}}}", "test"))
                    failures.Add("[door-fallback] a payload naming an unshipped id was rejected");
                foreach (string id in DungeonStatusCatalog.PortalDungeonIds)
                    if (!DungeonStatusCatalog.IsOpen(id))
                        failures.Add("[door-fallback] an unshipped id closed the real door '" + id + "'");

                // --- a FUTURE version still parses (forward-compatible) -------
                if (!DungeonStatusCatalog.ApplyPayload(
                        "{\"version\":99,\"dungeons\":{\"dg_sunken_vault\":{\"status\":\"flooded\"}}}", "test"))
                    failures.Add("[door-fallback] a v99 payload was rejected - it must parse forward-compatibly");
                else if (DungeonStatusCatalog.For("dg_sunken_vault").State != DungeonDoorState.Flooded)
                    failures.Add("[door-fallback] a v99 payload parsed but did not apply");

                notes.Add("door-fallback drove the catalog headlessly (no network, no PlayMode)");
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
        //  CASE 4 - the status domain is exactly the four AuthoredPortal ids
        // =====================================================================
        private static void Case4_IdContract(List<string> failures, List<string> notes)
        {
            var domain = DungeonStatusCatalog.PortalDungeonIds;
            if (domain == null || domain.Length != 4)
            {
                failures.Add("[door-ids] PortalDungeonIds is not the four AuthoredPortal ids (count=" +
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
