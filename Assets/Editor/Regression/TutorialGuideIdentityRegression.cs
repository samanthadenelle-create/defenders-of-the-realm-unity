// =============================================================================
// TutorialGuideIdentityRegression [tutorial-guide-identity]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
//
// WO-1014 (owner felt-test 2026-08-10, verbatim: "the wolf is supposed to guide
// it, however the story line in[trodu]ces himself as Storm or something and a NPC
// and wolf both spawn together. There is no knowledge of who this wolf is. The
// intro is badly composed.").
//
// THE BUG THIS SUITE EXISTS TO PREVENT COMING BACK: for weeks TWO COMPLETE,
// CONTRADICTORY TUTORIAL SCRIPTS were live in dialogues.json at the same time -
// a legacy arc spoken by a hard-coded human, "Sylas, Scout of the Reach"
// (tut_move_to_sylas / tut_meet_sylas / tut_first_tower* / tut_world_encounter* /
// tut_return_home / tut_freedom), and the current founding arc spoken by the
// "{guide}" token that resolves to the pet-Echo. Nothing in code or in
// tutorial-steps.json chose between them, so the player met two narrators and no
// identity. Retiring the legacy records fixes TODAY; only a pinned invariant
// stops the next arc re-author from re-seeding it, which is the whole lesson of
// CLAUDE.md section 15 (duplicated state drifts, silently, until a player finds it).
//
// The four invariants, each of which HAS already broken:
//
//   (1) ONE SPEAKER, EVER. Every dialogue whose id starts with "tut_" speaks as
//       the literal "{guide}" token on EVERY line. A hard-coded name in a tutorial
//       record is a second narrator by construction - and it also defeats the
//       TutorialGuide identity seam, because a baked name cannot be re-pointed
//       when the owner rules on the guide's name (WO-1014 2b).
//
//   (2) NO LEGACY ARC ID MAY RETURN, in either canonical copy, and no step may
//       reference one. Named explicitly rather than inferred, so a re-add fails
//       loudly instead of quietly re-arming a retired narrator.
//
//   (3) THE UTILITY LINE COMES BEFORE THE ASK. "I can farm. I can mend. Put me to
//       work, Keeper." must live in the arc's FIRST beat (tut_founding_greet) and
//       must NOT sit in a beat that plays after the player has been told to build
//       (tut_founding_ack is an ACKNOWLEDGMENT - by definition it plays after the
//       act; tut_founding_hollow_done likewise). Owner: "the wolf asks what to do,
//       but never explained that's what it does". Ordering is content, and content
//       regressions are exactly the ones nobody notices until a felt-test.
//
//   (4) THE DUAL COPY IS BYTE-IDENTICAL and every player-visible tutorial string
//       is ASCII. The shipped player loads Resources/; an edit made in only one
//       copy is invisible on device, and any non-ASCII glyph renders as TOFU.
//
// WHAT THIS CANNOT PROVE: that the guide is well WRITTEN, that the wolf physically
// leads the walk beat, or that exactly one body spawns at runtime. The first is
// the owner's voice pass; the second and third are RUNTIME facts and belong to a
// capture - WO-1014 Half B adds the [Flow:Pets] guide-lead forensics for exactly
// that and deliberately changes no movement logic (CLAUDE.md section 12).
//
// Markers: TUTORIAL_GUIDE_IDENTITY_OK / TUTORIAL_GUIDE_IDENTITY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TutorialGuideIdentityRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class TutorialGuideIdentityRegression
    {
        private const string DialoguesRes = "Assets/Resources/Data/Canonical/dialogue/dialogues.json";
        private const string DialoguesSA  = "Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json";
        private const string StepsRes     = "Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json";
        private const string StepsSA      = "Assets/StreamingAssets/Data/Canonical/tutorial/tutorial-steps.json";

        /// <summary>The ONE speaker token any tutorial line may carry.</summary>
        private const string GuideToken = "{guide}";

        /// <summary>The prefix that marks a dialogue record as tutorial content.</summary>
        private const string TutPrefix = "tut_";

        /// <summary>The retired legacy "Sylas" tutorial arc. Named, not inferred - a
        /// re-add must fail by NAME so the failure text says what came back.</summary>
        private static readonly string[] RetiredLegacyArc =
        {
            "tut_move_to_sylas",
            "tut_meet_sylas",
            "tut_first_tower",
            "tut_first_tower_done",
            "tut_world_encounter",
            "tut_world_encounter_win",
            "tut_world_encounter_retry",
            "tut_return_home",
            "tut_freedom",
        };

        /// <summary>The guide's utility line (WO-1014 2c) and where it may / may not live.</summary>
        private const string UtilityLine    = "I can farm. I can mend.";
        private const string UtilityHome    = "tut_founding_greet";
        private static readonly string[] UtilityBannedFrom =
        {
            "tut_founding_ack",           // an ACK plays after the build - too late to teach utility
            "tut_founding_hollow_done",   // same shape; this is where it was originally buried
        };

        // =====================================================================

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TUTORIAL_GUIDE_IDENTITY_OK - " + reason);
            else Debug.LogError("TUTORIAL_GUIDE_IDENTITY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            int tutRecords = 0;
            try
            {
                Case(failures, "dual-copy", () => Case1_DualCopy(failures));

                JObject dialogues = LoadJson(DialoguesRes, "dialogues.json", failures);
                if (dialogues != null)
                {
                    var tut = TutorialRecords(dialogues);
                    tutRecords = tut.Count;
                    if (tutRecords == 0)
                        failures.Add("[one-speaker] dialogues.json contains NO 'tut_*' records at all - the " +
                                     "tutorial arc has vanished, which no legitimate change does.");
                    Case(failures, "one-speaker", () => Case2_OneSpeaker(tut, failures));
                    Case(failures, "no-legacy-arc", () => Case3_NoLegacyArc(dialogues, tut, failures));
                    Case(failures, "utility-order", () => Case4_UtilityBeforeAsk(tut, failures));
                    Case(failures, "ascii", () => Case5_AsciiPlayerStrings(tut, failures));
                }
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "TUTORIAL GUIDE IDENTITY OK - all " + tutRecords + " 'tut_*' dialogue records speak as " +
                         "the single \"" + GuideToken + "\" token (no second narrator can reach the FTUE), all " +
                         RetiredLegacyArc.Length + " retired legacy 'Sylas' arc ids are absent from both canonical " +
                         "copies and unreferenced by tutorial-steps.json, the guide's utility line is stated in " +
                         UtilityHome + " BEFORE any ask (and absent from the post-ask beats), dialogues.json and " +
                         "tutorial-steps.json are byte-identical dual pairs, and every player-visible tutorial " +
                         "string is ASCII.";
                return true;
            }
            reason = "tutorial-guide-identity FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the canonical dual copies are byte-identical
        // =====================================================================

        private static void Case1_DualCopy(List<string> failures)
        {
            DualCopy(DialoguesRes, DialoguesSA, "dialogues.json", failures);
            DualCopy(StepsRes, StepsSA, "tutorial-steps.json", failures);
        }

        private static void DualCopy(string res, string sa, string label, List<string> failures)
        {
            if (!File.Exists(res)) { failures.Add("[dual-copy] missing " + res); return; }
            if (!File.Exists(sa))  { failures.Add("[dual-copy] missing " + sa);  return; }

            byte[] a = File.ReadAllBytes(res);
            byte[] b = File.ReadAllBytes(sa);
            if (a.Length != b.Length || !a.SequenceEqual(b))
                failures.Add("[dual-copy] " + label + " Resources and StreamingAssets copies DIFFER (" +
                             a.Length + " vs " + b.Length + " bytes) - the shipped player loads Resources/, " +
                             "so an edit made in only one copy is invisible on device.");

            foreach (var p in new[] { res, sa })
            {
                byte[] raw = File.ReadAllBytes(p);
                if (Array.IndexOf(raw, (byte)0) >= 0)
                    failures.Add("[dual-copy] " + p + " contains a NUL byte (mount-garble signature).");
            }
        }

        // =====================================================================
        //  Case 2 - ONE speaker identity, ever
        // =====================================================================

        private static void Case2_OneSpeaker(List<JObject> tut, List<string> failures)
        {
            var offenders = new List<string>();
            foreach (var rec in tut)
            {
                string id = (string)rec["id"];
                foreach (var line in Lines(rec))
                {
                    string speaker = (string)line["speaker"];
                    if (!string.Equals(speaker, GuideToken, StringComparison.Ordinal))
                        offenders.Add(id + " -> speaker '" + (speaker ?? "<null>") + "'");
                }
            }

            if (offenders.Count > 0)
                failures.Add("[one-speaker] " + offenders.Count + " tutorial line(s) are spoken by something " +
                             "other than the \"" + GuideToken + "\" token [" +
                             string.Join(", ", offenders.Take(8)) + (offenders.Count > 8 ? ", ..." : "") +
                             "] - a hard-coded name in a 'tut_*' record IS a second narrator (the WO-1014 bug: " +
                             "a human 'Sylas' arc and the pet-Echo arc were live at the same time), and it also " +
                             "defeats the TutorialGuide identity seam, which can only re-point the token.");
        }

        // =====================================================================
        //  Case 3 - no retired legacy arc id may return, anywhere
        // =====================================================================

        private static void Case3_NoLegacyArc(JObject dialogues, List<JObject> tut, List<string> failures)
        {
            var live = new HashSet<string>(tut.Select(r => (string)r["id"]), StringComparer.OrdinalIgnoreCase);
            foreach (string id in RetiredLegacyArc)
                if (live.Contains(id))
                    failures.Add("[no-legacy-arc] retired dialogue id '" + id + "' is back in dialogues.json - " +
                                 "it belongs to the legacy human-scout tutorial arc retired by WO-1014. One arc " +
                                 "may be live and the founding arc is it.");

            // A retired id must also be unreachable from the step registry, in BOTH copies.
            foreach (string path in new[] { StepsRes, StepsSA })
            {
                if (!File.Exists(path)) continue;
                string text = File.ReadAllText(path);
                foreach (string id in RetiredLegacyArc)
                    if (text.IndexOf("\"" + id + "\"", StringComparison.Ordinal) >= 0)
                        failures.Add("[no-legacy-arc] " + path + " references retired dialogue id '" + id +
                                     "' - a step that names it re-arms the retired narrator.");
            }

            // The StreamingAssets copy is checked as raw text too: Case 1 proves the copies
            // match, but if that ever fails this keeps the legacy check honest on both files.
            if (File.Exists(DialoguesSA))
            {
                string sa = File.ReadAllText(DialoguesSA);
                foreach (string id in RetiredLegacyArc)
                    if (sa.IndexOf("\"id\": \"" + id + "\"", StringComparison.Ordinal) >= 0)
                        failures.Add("[no-legacy-arc] the StreamingAssets copy still defines retired id '" +
                                     id + "'.");
            }
        }

        // =====================================================================
        //  Case 4 - the utility line is stated BEFORE the ask
        // =====================================================================

        private static void Case4_UtilityBeforeAsk(List<JObject> tut, List<string> failures)
        {
            var byId = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in tut)
            {
                string id = (string)rec["id"];
                if (!string.IsNullOrEmpty(id)) byId[id] = rec;
            }

            if (!byId.TryGetValue(UtilityHome, out JObject home))
            {
                failures.Add("[utility-order] the arc's opening beat '" + UtilityHome + "' is missing from " +
                             "dialogues.json.");
                return;
            }

            bool inHome = Lines(home).Any(l => ((string)l["text"] ?? "")
                              .IndexOf(UtilityLine, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!inHome)
                failures.Add("[utility-order] the guide's utility line (\"" + UtilityLine + "\") is NOT in '" +
                             UtilityHome + "' - WO-1014 2c requires the guide to state what it can do in the " +
                             "arc's FIRST beat, before it ever asks the player to act (owner 2026-08-10: 'the " +
                             "wolf asks what to do, but never explained that's what it does').");

            foreach (string banned in UtilityBannedFrom)
            {
                if (!byId.TryGetValue(banned, out JObject rec)) continue;
                if (Lines(rec).Any(l => ((string)l["text"] ?? "")
                        .IndexOf(UtilityLine, StringComparison.OrdinalIgnoreCase) >= 0))
                    failures.Add("[utility-order] the utility line is back in '" + banned + "', which plays " +
                                 "AFTER the player has already been asked to build - that is the exact " +
                                 "ordering defect WO-1014 2c fixed. It belongs in '" + UtilityHome + "' only.");
            }
        }

        // =====================================================================
        //  Case 5 - player-visible tutorial strings are ASCII
        // =====================================================================

        private static void Case5_AsciiPlayerStrings(List<JObject> tut, List<string> failures)
        {
            var offenders = new List<string>();
            foreach (var rec in tut)
            {
                string id = (string)rec["id"];
                foreach (var line in Lines(rec))
                {
                    string text = (string)line["text"];
                    if (text == null) continue;
                    char bad = text.FirstOrDefault(c => c > 127);
                    if (bad != '\0')
                        offenders.Add(id + " (U+" + ((int)bad).ToString("X4") + ")");
                }
            }

            // The step registry's objective texts are player-visible too.
            if (File.Exists(StepsRes))
            {
                JObject steps = LoadJson(StepsRes, "tutorial-steps.json", failures);
                var arr = steps != null ? steps["steps"] as JArray : null;
                if (arr != null)
                    foreach (var s in arr.OfType<JObject>())
                    {
                        string text = s["objective"] != null ? (string)s["objective"]["text"] : null;
                        if (text == null) continue;
                        char bad = text.FirstOrDefault(c => c > 127);
                        if (bad != '\0')
                            offenders.Add("step " + (string)s["id"] + " objective (U+" +
                                          ((int)bad).ToString("X4") + ")");
                    }
            }

            if (offenders.Count > 0)
                failures.Add("[ascii] " + offenders.Count + " player-visible tutorial string(s) contain " +
                             "non-ASCII characters [" + string.Join(", ", offenders.Take(8)) +
                             (offenders.Count > 8 ? ", ..." : "") + "] - every non-ASCII glyph renders as TOFU " +
                             "in TMP on device (binding project rule).");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        private static List<JObject> TutorialRecords(JObject dialogues)
        {
            var list = new List<JObject>();
            var arr = dialogues["dialogues"] as JArray;
            if (arr == null) return list;
            foreach (var o in arr.OfType<JObject>())
            {
                string id = (string)o["id"];
                if (!string.IsNullOrEmpty(id) && id.StartsWith(TutPrefix, StringComparison.Ordinal))
                    list.Add(o);
            }
            return list;
        }

        private static IEnumerable<JObject> Lines(JObject record)
        {
            var nodes = record["nodes"] as JArray;
            if (nodes == null) yield break;
            foreach (var n in nodes.OfType<JObject>())
            {
                var lines = n["lines"] as JArray;
                if (lines == null) continue;
                foreach (var l in lines.OfType<JObject>()) yield return l;
            }
        }

        private static JObject LoadJson(string path, string label, List<string> failures)
        {
            if (!File.Exists(path)) { failures.Add("[parse] missing " + path); return null; }
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("[parse] " + label + " is not valid JSON: " + ex.Message);
                return null;
            }
        }
    }
}
