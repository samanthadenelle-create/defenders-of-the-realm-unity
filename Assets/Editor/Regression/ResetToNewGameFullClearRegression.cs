// =============================================================================
// ResetToNewGameFullClearRegression [reset-full-clear]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core).
//
// Pins the owner acceptance criterion "Start New must actually start new" against
// the two fields that shipped un-wiped (audit 2026-08-02):
//
//   DEFECT 1  GameState.Settlements was simply ABSENT from ResetToNewGame's body.
//             Tribes and Wards, added in the SAME v34 batch three lines above, were
//             there. A new game therefore inherited every claimed and razed node
//             settlement from the previous save - including its 3-day razed lockout,
//             which silently forbids building on sites the player has never seen.
//   DEFECT 2  GameState.Zones was worse than absent. The reset called
//             EnsureZoneGraph(s), which is a BACKFILL helper: it early-returns the
//             moment Zones is non-empty (correctly - it must not duplicate the 5
//             defaults across the fresh-save / post-load / migrator call sites). So
//             the call could only ever top up an EMPTY graph, never reseed a full
//             one, and a "new" game opened on the previous save's fully explored and
//             cleared realm.
//
// WHY THE MAIN CASE IS A REFLECTION SWEEP, NOT TWO FIELD CHECKS: both defects are
// the same failure - somebody added a persisted field and did not add a reset line.
// Pinning only Settlements and Zones would pin the two we already fixed and miss the
// next one, which is the whole cost. Case 1 therefore enumerates EVERY public
// instance field on GameState and requires each to be assigned in the reset body or
// to appear on an EXPLICIT, justified carve-out list. Adding a persisted field now
// fails this suite until it is either reset or consciously exempted.
//
// WHY IT IS A SOURCE SWEEP AND NOT A LIVE CALL (the honest trade, stated plainly):
// DataRegression runs in EDITOR BATCHMODE with no play session. ResetToNewGame is an
// instance method that ends in Save() and ClearEquipPrefs() - both of which write and
// DELETE PlayerPrefs keys shared with the editor. Driving it here would wipe the
// owner's editor save every time the gate runs, which is a far worse defect than the
// one being pinned, and there is no seam to call the field-clearing core without the
// persistence tail. So the BEHAVIOURAL proof lives where a scene and a scratch save
// are available - Assets/_Modules/Core/Tests/ResetCarveOutTest.cs, extended in the
// same change with reset_reseeds_the_zone_graph_instead_of_inheriting_it and
// reset_clears_claimed_and_razed_settlements - and Case 4 below asserts those tests
// still exist, so the behavioural half cannot be deleted while this lint stays green.
// Nothing in this suite no-ops on a missing singleton: it never touches one.
//
// Cases:
//   1 [field-coverage]  Every public instance field of GameState is assigned in
//                       ResetToNewGame or is a documented carve-out; and every
//                       carve-out name still resolves to a real field.
//   2 [zone-reseed]     The reset FORCES a zone reseed rather than relying on the
//                       backfill helper's early-return.
//   3 [settlement-wipe] Settlements is cleared to a FRESH EMPTY list (an assignment
//                       alone would satisfy Case 1).
//   4 [behaviour-home]  The EditMode fixture still holds the behavioural assertions.
//
// Markers: RESET_FULL_CLEAR_OK / RESET_FULL_CLEAR_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.ResetToNewGameFullClearRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class ResetToNewGameFullClearRegression
    {
        private const string ServiceSrc = "Assets/_Modules/Core/State/GameStateService.cs";
        private const string TestSrc    = "Assets/_Modules/Core/Tests/ResetCarveOutTest.cs";

        /// <summary>
        /// Fields ResetToNewGame is ALLOWED to leave alone, each with the reason it is
        /// exempt. This list is the specification - a field is not exempt because nobody
        /// noticed it, it is exempt because it is named here. Case 1 also proves every
        /// entry still resolves to a real field, so a rename cannot turn a carve-out into
        /// a blind spot.
        /// </summary>
        private static readonly Dictionary<string, string> CarveOuts = new Dictionary<string, string>
        {
            // Identity + preference (the documented React reset() carve-out).
            { "BoundWallet",     "identity - the save stays tagged to its wallet across a New Game" },
            { "BreachStyle",     "player preference - survives a New Game" },

            // Settings. These are preferences, not progression; wiping them would reset the
            // player's audio/controls every time they start over. (JoystickSensitivity is
            // deliberately NOT here: the React reset() restores it to 1, and the port matches.)
            { "MovementStyle",   "setting - control preference" },
            { "Muted",           "setting - audio preference" },
            { "MusicVolume",     "setting - audio preference" },
            { "SfxVolume",       "setting - audio preference" },
            { "Difficulty",      "setting - player-chosen difficulty" },
            { "VoiceOvers",      "setting - audio preference" },

            // Social identity. reset() never touches it.
            { "MyInviteCode",    "social identity - reset() never touches it" },
            { "Contacts",        "social identity - friend list survives a New Game" },
            { "BlockedCodes",    "social identity - blocks survive a New Game" },
            { "Inbox",           "social identity - messages survive a New Game" },
            { "LastInboxSyncAt", "social identity - inbox sync cursor" },
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RESET_FULL_CLEAR_OK - " + reason);
            else Debug.LogError("RESET_FULL_CLEAR_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                string src = ReadSource(ServiceSrc, failures);
                string body = src == null ? null : ExtractResetBody(src, failures);
                if (body != null)
                {
                    Case(failures, "field-coverage",  () => Case1_FieldCoverage(body, failures, notes));
                    Case(failures, "zone-reseed",     () => Case2_ZoneReseed(body, StripComments(src), failures));
                    Case(failures, "settlement-wipe", () => Case3_SettlementWipe(body, failures));
                }
                Case(failures, "behaviour-home", () => Case4_BehaviourHome(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "RESET FULL CLEAR OK - every persisted GameState field is assigned by " +
                         "ResetToNewGame or is a named carve-out, the zone graph is force-RESEEDED " +
                         "rather than backfilled, settlements are cleared to a fresh empty list, and " +
                         "the EditMode fixture still proves all of it behaviourally" + noteStr;
                return true;
            }
            reason = "reset-full-clear FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the generalization: no field may be forgotten
        // =====================================================================
        private static void Case1_FieldCoverage(string body, List<string> failures, List<string> notes)
        {
            // DeclaredOnly: GameState's own persisted surface, not ScriptableObject's.
            // Fields only - a computed property (e.g. PartySize => PartyMemberIds.Count)
            // has nothing to reset and must not be demanded.
            FieldInfo[] fields = typeof(GameState).GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (fields.Length == 0)
            {
                failures.Add("[field-coverage] GameState exposes no public instance fields - either the " +
                             "save model was restructured or this oracle is reflecting the wrong type, " +
                             "and either way it is currently proving NOTHING");
                return;
            }

            var missing = new List<string>();
            foreach (FieldInfo f in fields)
            {
                if (CarveOuts.ContainsKey(f.Name)) continue;
                // "s.<Field> =" but never "==" - the reset body assigns through the local `s`.
                if (!Regex.IsMatch(body, @"\bs\." + Regex.Escape(f.Name) + @"\s*=(?!=)"))
                    missing.Add(f.Name);
            }

            if (missing.Count > 0)
                failures.Add("[field-coverage] ResetToNewGame never assigns " + missing.Count +
                             " persisted GameState field(s): " + string.Join(", ", missing) +
                             " - a New Game inherits them from the PREVIOUS save (this is exactly how " +
                             "Settlements shipped un-wiped). Reset each one, or add it to CarveOuts with " +
                             "the reason it must survive.");

            // A carve-out that no longer names a real field is a silent exemption.
            var stale = new List<string>();
            foreach (var kv in CarveOuts)
                if (typeof(GameState).GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance) == null)
                    stale.Add(kv.Key);
            if (stale.Count > 0)
                failures.Add("[field-coverage] carve-out(s) naming a field that no longer exists: " +
                             string.Join(", ", stale) + " - the field was renamed or removed and its " +
                             "exemption is now dead text that could shadow a real successor field");

            notes.Add(fields.Length + " GameState fields swept, " + CarveOuts.Count + " carved out");
        }

        // =====================================================================
        //  CASE 2 - the zone graph is RESEEDED, not backfilled
        // =====================================================================
        private static void Case2_ZoneReseed(string body, string wholeFile, List<string> failures)
        {
            int seedCall = body.IndexOf("EnsureZoneGraph(", StringComparison.Ordinal);
            if (seedCall < 0)
            {
                failures.Add("[zone-reseed] ResetToNewGame no longer seeds a zone graph at all - a New " +
                             "Game would start with no world map");
                return;
            }

            // The forced clear must come BEFORE the seeder, or the seeder still early-returns.
            Match clear = Regex.Match(body.Substring(0, seedCall),
                                      @"\bs\.Zones\s*=\s*(null|new\s)[^;]*;", RegexOptions.RightToLeft);

            // Is the helper still backfill-only? If it were changed to always reseed, the
            // forced clear would be belt-and-braces rather than load-bearing, and demanding
            // it would be a false alarm. So the defect is asserted as the CONJUNCTION.
            bool helperBackfillsOnly = Regex.IsMatch(
                wholeFile,
                @"static\s+void\s+EnsureZoneGraph\s*\([^)]*\)[\s\S]{0,400}?s\.Zones\s*!=\s*null\s*&&\s*s\.Zones\.Count\s*>\s*0[\s\S]{0,40}?return\s*;");

            if (!clear.Success && helperBackfillsOnly)
                failures.Add("[zone-reseed] ResetToNewGame relies on EnsureZoneGraph alone, and that helper " +
                             "still EARLY-RETURNS on a non-empty graph - so the reset can only backfill and " +
                             "the previous save's zone discovery/clear flags survive into the new game. " +
                             "Clear s.Zones before the seeder (that is what turns the backfill into a reseed).");

            if (!clear.Success && !helperBackfillsOnly)
                failures.Add("[zone-reseed] the reset does not clear s.Zones, and EnsureZoneGraph no longer " +
                             "looks like a backfill-only helper - this oracle can no longer tell whether a " +
                             "reseed happens. Re-read both and update this case rather than deleting it.");
        }

        // =====================================================================
        //  CASE 3 - settlements are emptied, not merely touched
        // =====================================================================
        private static void Case3_SettlementWipe(string body, List<string> failures)
        {
            if (!Regex.IsMatch(body, @"\bs\.Settlements\s*=\s*new\s+List<[^>]*SettlementState>\s*\(\s*\)\s*;"))
                failures.Add("[settlement-wipe] ResetToNewGame does not clear Settlements to a FRESH EMPTY " +
                             "list - a New Game keeps the previous save's claimed nodes and any live 3-day " +
                             "razed lockout, which forbids building on sites the new player has never seen");
        }

        // =====================================================================
        //  CASE 4 - the behavioural half still exists
        // =====================================================================
        private static void Case4_BehaviourHome(List<string> failures)
        {
            string test = ReadSource(TestSrc, failures);
            if (test == null) return;

            foreach (var probe in new[]
            {
                "reset_reseeds_the_zone_graph_instead_of_inheriting_it",
                "reset_clears_claimed_and_razed_settlements",
            })
            {
                if (test.IndexOf(probe, StringComparison.Ordinal) < 0)
                    failures.Add("[behaviour-home] " + TestSrc + " no longer contains " + probe +
                                 " - the BEHAVIOURAL proof of the reset is gone and only this source lint " +
                                 "remains, which cannot see a wrong value (only a missing line)");
            }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>
        /// The literal body of ResetToNewGame, comments stripped. Ends at
        /// StateReplaced.Invoke() - everything after that is the notify/persist tail and
        /// assigns nothing. Comments MUST go: this method documents every field it clears
        /// in prose, so linting raw text would pass on the comments alone.
        /// </summary>
        private static string ExtractResetBody(string src, List<string> failures)
        {
            const string sig = "public void ResetToNewGame()";
            int start = src.IndexOf(sig, StringComparison.Ordinal);
            if (start < 0)
            {
                failures.Add("[reset-body] " + ServiceSrc + " no longer declares " + sig +
                             " - the reset was renamed or removed and this oracle is blind");
                return null;
            }
            int end = src.IndexOf("StateReplaced.Invoke();", start, StringComparison.Ordinal);
            if (end < 0)
            {
                failures.Add("[reset-body] could not find the end of ResetToNewGame " +
                             "(StateReplaced.Invoke()) - the method shape changed");
                return null;
            }
            return StripComments(src.Substring(start, end - start));
        }

        private static string ReadSource(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[source] " + path + " not found - the file moved without updating this oracle");
                return null;
            }
            try { return File.ReadAllText(path); }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }
    }
}
