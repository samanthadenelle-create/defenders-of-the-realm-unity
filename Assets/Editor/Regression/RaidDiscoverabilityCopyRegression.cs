// =============================================================================
// RaidDiscoverabilityCopyRegression [raid-discoverability]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: RAID_DISCOVERY_OK / _FAIL.
//
// WO-1374, the four small discoverability holes the loops audit MEASURED. Each is
// tiny; each closes a way a player is told something untrue about raids.
//
//   1. THE GAME GUIDE WAS WRONG. guide-content.json's `raids` section said
//      "Open Raids from the HUD." WO-1286 retired that bar face. The ONE document
//      that explains raids was giving a direction that does not exist.
//   2. RAID DAILIES WERE UNGATED. Both combat.raid.* templates carried
//      requiresFeature: null, so a player with no Barracks could be handed
//      "clear 1 enemy outpost" as their daily - occupying one of three slots for
//      a whole day with something they cannot attempt.
//   3. THE ARENA HERALD BYPASSED THE RAID GATE. WO-1357 taught the Journey card
//      to lock gracefully; the world NPC called RaidSelectionScreen.Open()
//      directly and nothing on that path asked. Front door locked, side door open.
//   4. THE REFUSAL NAMED THE WRONG THING. Every refusal talked about troops and
//      barracks slots, because the army check was the only check - so a player
//      blocked by "raids are off in this build" was told to go train troops.
//
// -----------------------------------------------------------------------------
// PROVEN RED FIRST. Every case below fails against the pre-WO-1374 tree:
//   A1 forbids "from the HUD"        -> the shipped guide said exactly that.
//   B1 requires requiresFeature raids -> both templates read null.
//   C1 requires the capability gate inside Open() -> Open() had no such check.
//   C3 requires "raids" to resolve per-save -> FeatureShipped returned a flat true.
// None of these is a tautology dressed as a test.
//
// -----------------------------------------------------------------------------
// (!) BOTH CANONICAL COPIES, AND THEY MUST BE IDENTICAL.
// -----------------------------------------------------------------------------
// The catalogs live in Assets/Resources/Data/Canonical (which WINS at runtime) and
// Assets/StreamingAssets/Data/Canonical. Checking only one is how a fix ships to
// the copy nobody reads. The suite asserts the text in each AND that the two agree.
//
// Zero scene, zero network, zero PlayMode: files on disk and comment-stripped source.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Pins the four WO-1374 discoverability fixes: the Guide's direction, the raid
    /// dailies' feature gate, the single gated raid door, and a refusal that names
    /// the actual blocker.
    /// </summary>
    public static class RaidDiscoverabilityCopyRegression
    {
        private const string GuideFile = "guide-content.json";
        private const string DailiesFile = "daily-quests.json";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- RAID DISCOVERABILITY (WO-1374) ---");

            // =================================================================
            //  (A) THE GAME GUIDE POINTS AT JOURNEY, NOT AT A RETIRED HUD FACE.
            // =================================================================
            foreach (var pair in CanonicalPair(GuideFile))
            {
                string label = pair.Key;
                string text = pair.Value;
                if (text == null)
                {
                    failures.Add("[A0] " + label + " not found - the Guide copy cannot be checked");
                    continue;
                }
                string raids = SectionAround(text, "\"id\": \"raids\"", 2600);
                if (raids == null)
                {
                    failures.Add("[A0] " + label + " has no `raids` section - the one document that explains " +
                                 "raids is missing");
                    continue;
                }

                // ⛔ The retired direction, in the forms it could come back as.
                string[] retired = { "Open Raids from the HUD", "from the HUD", "Raids button" };
                foreach (var r in retired)
                    if (raids.Contains(r))
                        failures.Add("[A1] " + label + " `raids` still says \"" + r + "\". WO-1286 retired that " +
                                     "bar face; the Guide is the only document explaining raids and it is " +
                                     "sending the player somewhere that does not exist.");

                if (!raids.Contains("Journey"))
                    failures.Add("[A2] " + label + " `raids` never names Journey - the section must say where " +
                                 "the raid door actually is, or it explains a feature the reader cannot find");

                // The Guide must also stop promising a troop precondition the code no longer
                // enforces: WO-1008 made zero troops a DIM state on a visible face, and
                // WO-1374 grants the first squad free. "stays shut without them" is now false.
                if (raids.Contains("stays shut without them"))
                    failures.Add("[A3] " + label + " `raids` still says the Raids entry stays shut without " +
                                 "troops. Since WO-1008 an empty army DIMS a visible entry rather than hiding " +
                                 "it, and since WO-1374 the first squad is free - the sentence is now untrue.");

                foreach (char c in raids)
                    if (c > 126 || c < 9)
                    { failures.Add("[A4] " + label + " `raids` copy is not 7-bit ASCII (mobile font-atlas law)"); break; }
            }
            RequireTwinsIdentical(failures, GuideFile, "A5");

            // =================================================================
            //  (B) RAID DAILIES REQUIRE THE RAID FEATURE.
            // =================================================================
            foreach (var pair in CanonicalPair(DailiesFile))
            {
                string label = pair.Key;
                string text = pair.Value;
                if (text == null) { failures.Add("[B0] " + label + " not found"); continue; }

                foreach (var id in new[] { "combat.raid.single", "combat.raid.double" })
                {
                    string block = SectionAround(text, "\"id\": \"" + id + "\"", 400);
                    if (block == null)
                    {
                        failures.Add("[B0] " + label + " has no template '" + id + "'");
                        continue;
                    }
                    if (!block.Contains("\"requiresFeature\": \"raids\""))
                        failures.Add("[B1] " + label + " template '" + id + "' does not carry " +
                                     "requiresFeature \"raids\". Without it a player with no Barracks can be " +
                                     "handed a raid daily, which burns one of three slots for a day on " +
                                     "something they cannot attempt.");
                }
            }
            RequireTwinsIdentical(failures, DailiesFile, "B2");

            // (B3) - and the gate has to MEAN something. FeatureShipped answered a flat true
            // for every string before this ticket, so authoring the field without teaching the
            // resolver would have looked fixed and changed nothing.
            string questCode = RaidLootCurrencyRegression.ReadStripped("DailyQuests.cs");
            if (questCode == null)
                failures.Add("[B3] DailyQuests.cs not found - cannot prove the requiresFeature gate resolves");
            else
            {
                if (!questCode.Contains("\"raids\""))
                    failures.Add("[B3] DailyQuests.cs live code has no case for \"raids\" - the field authored in " +
                                 "daily-quests.json falls through to the catch-all TRUE and gates nothing");
                if (!questCode.Contains("RaidCapable"))
                    failures.Add("[B3] DailyQuests.cs live code does not read PostureSignals.RaidCapable - the " +
                                 "raids gate must read the ONE raid predicate, never a second barracks check " +
                                 "(WO-1357: two checks drift, and the drift is the defect)");
            }

            // =================================================================
            //  (C) ONE GATED DOOR, AND A REFUSAL THAT NAMES THE BLOCKER.
            // =================================================================
            string selectionCode = RaidLootCurrencyRegression.ReadStripped("RaidSelectionScreen.cs");
            if (selectionCode == null)
            {
                failures.Add("[C0] RaidSelectionScreen.cs not found - the raid door cannot be checked");
            }
            else
            {
                if (!selectionCode.Contains("PostureSignals.RaidCapable"))
                    failures.Add("[C1] RaidSelectionScreen.Open does not check PostureSignals.RaidCapable. " +
                                 "That is the Arena Herald bypass: the Journey card locks gracefully while the " +
                                 "world NPC opens the camp list for a player who cannot raid.");
                if (!selectionCode.Contains("RaidLockCopy"))
                    failures.Add("[C2] RaidSelectionScreen.cs does not use PostureSignals.RaidLockCopy - the " +
                                 "refusal must name the ACTUAL missing thing (no Barracks / destroyed Barracks " +
                                 "/ raids off in this build), and RaidLockCopy is the one owner of those words. " +
                                 "Telling a player to train troops when the blocker is a missing building is " +
                                 "advice that cannot work.");

                // The capability gate must come BEFORE the army gate, or a player with no
                // Barracks is still shown the troop refusal and sent to the drillmaster.
                int capAt = selectionCode.IndexOf("PostureSignals.RaidCapable", System.StringComparison.Ordinal);
                int armyAt = selectionCode.IndexOf("ArmyReadiness.Compute", System.StringComparison.Ordinal);
                if (capAt >= 0 && armyAt >= 0 && capAt > armyAt)
                    failures.Add("[C3] the capability gate runs AFTER the army-readiness gate in " +
                                 "RaidSelectionScreen.Open, so a player with no Barracks gets the troop " +
                                 "refusal and a training panel instead of the truth");
            }

            // (C4) The Herald must reach the gated door and nothing else. If it ever opens a
            // raid surface by a second route, the gate above stops being the single door.
            string heraldCode = RaidLootCurrencyRegression.ReadStripped("ArenaHeraldSpawner.cs");
            if (heraldCode == null)
                failures.Add("[C4] ArenaHeraldSpawner.cs not found - cannot prove the Herald goes through the gate");
            else
            {
                if (!heraldCode.Contains("RaidSelectionScreen.Open"))
                    failures.Add("[C4] ArenaHeraldSpawner live code no longer calls RaidSelectionScreen.Open - " +
                                 "if it now reaches raids another way, that route is UNGATED");
                if (heraldCode.Contains("RaidDeployScreen.Open"))
                    failures.Add("[C4] ArenaHeraldSpawner live code opens RaidDeployScreen directly, skipping " +
                                 "the selection screen where the capability gate lives");
            }

            // (C5) THE GATE MUST BE ABLE TO SPEAK. Every lock reason has copy - a reason
            // added without words would refuse the player with a blank toast, which is the
            // "Locked" non-answer the owner's colourblindness makes doubly useless.
            foreach (DeNelle.Core.HudModel.PostureSignals.RaidLockReason r in
                     System.Enum.GetValues(typeof(DeNelle.Core.HudModel.PostureSignals.RaidLockReason)))
            {
                if (r == DeNelle.Core.HudModel.PostureSignals.RaidLockReason.None) continue;
                string copy = DeNelle.Core.HudModel.PostureSignals.RaidLockCopy(r);
                if (string.IsNullOrEmpty(copy))
                {
                    failures.Add("[C5] lock reason '" + r + "' has no player-facing copy - the refusal would be " +
                                 "blank or generic, which is exactly the failure this ticket names");
                    continue;
                }
                foreach (char c in copy)
                    if (c > 126 || c < 32)
                    { failures.Add("[C5] lock copy for '" + r + "' is not 7-bit ASCII: \"" + copy + "\""); break; }
            }

            if (failures.Count == 0)
            {
                reason = "RAID DISCOVERABILITY OK - the Game Guide sends the player to Journey rather than the " +
                         "HUD face WO-1286 retired, both combat.raid.* dailies require the raids feature and " +
                         "DailyQuests resolves it from the ONE raid predicate, the capability gate sits at the " +
                         "top of RaidSelectionScreen.Open (ahead of the army gate) so the Arena Herald can no " +
                         "longer walk past it, every lock reason has ASCII copy that names the missing thing, " +
                         "and both canonical twins of each catalog are byte-identical";
                Debug.Log(log.ToString() + "RAID_DISCOVERY_OK");
                return true;
            }

            reason = "raid-discoverability: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "RAID_DISCOVERY_FAIL: " + reason);
            return false;
        }

        // =====================================================================

        /// <summary>Resources copy (wins at runtime) + StreamingAssets copy, by label.</summary>
        private static List<KeyValuePair<string, string>> CanonicalPair(string file)
        {
            var list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>("Resources/" + file, ReadCanonical("Resources", file)));
            list.Add(new KeyValuePair<string, string>("StreamingAssets/" + file, ReadCanonical("StreamingAssets", file)));
            return list;
        }

        private static string ReadCanonical(string root, string file)
        {
            try
            {
                string p = Path.Combine(Application.dataPath, root + "/Data/Canonical/" + file);
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// The twins must agree. Resources WINS at runtime, so a fix applied to only the
        /// StreamingAssets copy ships to the file nobody reads - and looks done.
        /// </summary>
        private static void RequireTwinsIdentical(List<string> failures, string file, string caseId)
        {
            string a = ReadCanonical("Resources", file);
            string b = ReadCanonical("StreamingAssets", file);
            if (a == null || b == null)
            {
                failures.Add("[" + caseId + "] one canonical twin of " + file + " is missing - Resources=" +
                             (a == null ? "MISSING" : "present") + " StreamingAssets=" +
                             (b == null ? "MISSING" : "present"));
                return;
            }
            if (Normalize(a) != Normalize(b))
                failures.Add("[" + caseId + "] the two canonical copies of " + file + " DIFFER. Resources wins " +
                             "at runtime, so an edit that landed in only one of them is either invisible or " +
                             "silently authoritative - both are defects.");
        }

        /// <summary>Line-ending-insensitive compare: a CRLF/LF difference is a checkout
        /// artefact, not a content divergence, and failing on it would train people to
        /// ignore this case.</summary>
        private static string Normalize(string s)
            => s == null ? null : s.Replace("\r\n", "\n").Replace("\r", "\n");

        /// <summary>
        /// The slice of <paramref name="text"/> starting at <paramref name="anchor"/>, up to
        /// <paramref name="length"/> characters. Deliberately a window rather than a JSON
        /// parse: the assertions are about COPY, and a window keeps the suite readable and
        /// dependency-free. Returns null when the anchor is absent, which is itself reported.
        /// </summary>
        private static string SectionAround(string text, string anchor, int length)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = text.IndexOf(anchor, System.StringComparison.Ordinal);
            if (i < 0) return null;
            int end = Mathf.Min(text.Length, i + length);
            return text.Substring(i, end - i);
        }
    }
}
