// =============================================================================
// RaidsDiscoverabilityRegression — WO-1008 oracle: the Raids face is VISIBLE-and-
// EXPLAINED the moment a Barracks exists, never ABSENT.
// Marker: RAIDS_DISCOVERABILITY_OK / RAIDS_DISCOVERABILITY_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Registered into
// DataRegression.RunAll (sibling-suite protocol).
//
// WHAT COST US A SESSION (owner, 2026-08-16): she had a built Barracks and an
// empty army. The RaidCapabilityHudBridge visibility predicate required
// ">=1 deployable troop", so the Raids face was completely absent from the action
// bar and she reported "I do not see a way to start a raid". A feature that hides
// itself is indistinguishable from a broken one. Owner ask, verbatim: "can we add
// a greyed out option once we have a barracks with build troops to raid".
//
// THIS SUITE PINS, so it cannot silently regress:
//   D1. A built Barracks with ZERO troops yields a VISIBLE, DIMMED Raids face
//       (reason NoTroops) — not a hidden one.
//   D2. Once capable, the face is NEVER hidden by any troop count.
//   D3. The dim reason is carried in WORDS/NUMBERS, not hue: the greyed face text
//       differs from the live face text, and the two reasons never share copy.
//       (The owner is red/green colourblind — a grey tint alone says nothing.)
//   D4. The UNDERLYING full-army gate is UNCHANGED: RaidSelectionScreen.Open still
//       recomputes ArmyReadiness, still refuses, still redirects to the drillmaster,
//       and the ff.raidtest bypass still composes with it (one bypass, not two).
//   D5. The deleted troop clause never returns to the visibility bridge.
//   D6. All user-facing dim copy is ASCII (mobile font-atlas law).
//
// SOURCE LINTS STRIP COMMENTS **AND** STRING LITERALS FIRST — otherwise this very
// file's own explanatory prose, or a doc comment naming the retired clause, would
// satisfy or trip a match and the pass would be hollow.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;   // HudActionBarModel.Active is IReadOnlyList<T>, which has no Contains of its
                     // own — without Enumerable.Contains the compiler falls through to the
                     // ReadOnlySpan<char> overload and demands a StringComparison. Do not remove.
using System.Text;
using UnityEngine;
using DeNelle.Core.HudModel;

namespace DeNelle.Editor
{
    public static class RaidsDiscoverabilityRegression
    {
        // Deterministic fake — drives the REAL Core compute through each state.
        private sealed class FakeSource : HudActionBarModel.ISource
        {
            public bool Talk;
            public bool Capable = true;      // barracks built + FeatureFlags.Raid on
            public bool ArmyReady;
            public bool Onboarded = true;
            public bool Focused;
            public int Deployable;
            public int Queued;
            public int Cap = 5;

            public bool TalkAvailable => Talk;
            public bool RaidCapable => Capable;
            public bool RaidArmyReady => ArmyReady;
            public int RaidDeployableSlots => Deployable;
            public int RaidQueuedSlots => Queued;
            public int RaidCapSlots => Cap;
            public bool MapUnlocked => Onboarded;
            public bool BuildingFocused => Focused;
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== RaidsDiscoverabilityRegression (WO-1008): greyed-and-explained, never absent ===");

            try
            {
                CheckVisibleAndDimmed(failures, log);
                CheckReasonsSpeakInWords(failures, log);
                CheckVisibilityPredicateSource(failures, log);
                CheckUnderlyingGateUnchanged(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("RaidsDiscoverabilityRegression threw: " + ex.GetType().Name + ": " + ex.Message);
            }

            return Verdict(failures, log, out reason);
        }

        // ── D1/D2: visible-and-dimmed, never hidden ──────────────────────────
        private static void CheckVisibleAndDimmed(List<string> failures, StringBuilder log)
        {
            var src = new FakeSource();
            var model = new HudActionBarModel(src);
            model.SetPosture(HudActionBarModel.PostureTown);

            // D1 — THE OWNER'S EXACT STATE: barracks built, army completely empty.
            src.Capable = true; src.ArmyReady = false;
            src.Deployable = 0; src.Queued = 0; src.Cap = 5;
            model.Tick();
            if (!model.Active.Contains(ActionBarButtonId.Raids))
                failures.Add("D1: a built Barracks with ZERO troops HIDES the Raids face — this is the exact " +
                             "2026-08-16 bug ('I do not see a way to start a raid'); it must be greyed, not absent");
            if (!model.RaidsDimmed)
                failures.Add("D1: zero troops did not GREY the Raids face");
            if (model.RaidsDimReason != HudActionBarModel.RaidDimReason.NoTroops)
                failures.Add("D1: zero troops reported dim reason '" + model.RaidsDimReason +
                             "' — expected NoTroops, so the copy can say 'train troops at the Barracks'");

            // D2 — no troop count may ever hide the face once capable.
            int[,] states = { { 0, 0 }, { 0, 3 }, { 1, 0 }, { 3, 1 }, { 5, 0 } };
            for (int i = 0; i < states.GetLength(0); i++)
            {
                src.Deployable = states[i, 0];
                src.Queued = states[i, 1];
                src.ArmyReady = src.Deployable + src.Queued >= src.Cap;
                model.Tick();
                if (!model.Active.Contains(ActionBarButtonId.Raids))
                    failures.Add("D2: Raids face hidden at deployable=" + src.Deployable + " queued=" + src.Queued +
                                 " — once a Barracks exists and the flag is on, the face is never hidden");
            }

            // A full army un-greys it (the WO-820 semantics are preserved, not inverted).
            src.Deployable = 5; src.Queued = 0; src.ArmyReady = true;
            model.Tick();
            if (model.RaidsDimmed)
                failures.Add("D2: a FULL army left the Raids face greyed");
            if (!string.Equals(model.RaidsFaceLabel, HudActionBarModel.RaidsBaseLabel, StringComparison.Ordinal))
                failures.Add("D2: the live (undimmed) face text is '" + model.RaidsFaceLabel +
                             "' — it must be the plain base label '" + HudActionBarModel.RaidsBaseLabel + "'");

            // Still absent when the BUILDING is missing (the hide rule that survives).
            src.Capable = false; model.Tick();
            if (model.Active.Contains(ActionBarButtonId.Raids))
                failures.Add("D2: Raids visible with no Barracks / raid flag off — that hide rule still stands");

            log.AppendLine("  D1/D2 visible-and-dimmed with zero troops; never hidden once capable — OK");
        }

        // ── D3/D6: the tell is words, the reasons differ, the copy is ASCII ───
        private static void CheckReasonsSpeakInWords(List<string> failures, StringBuilder log)
        {
            var src = new FakeSource();
            var model = new HudActionBarModel(src);
            model.SetPosture(HudActionBarModel.PostureTown);

            src.Capable = true; src.ArmyReady = false;
            src.Deployable = 0; src.Queued = 0; src.Cap = 5;
            model.Tick();
            string noTroopsFace = model.RaidsFaceLabel;
            string noTroopsMsg = model.RaidsDimMessage;

            if (string.Equals(noTroopsFace, HudActionBarModel.RaidsBaseLabel, StringComparison.Ordinal))
                failures.Add("D3: the greyed face text is identical to the live face text — the dim state would " +
                             "then be conveyed by HUE ALONE, which is invisible to the owner (red/green colourblind)");
            if (string.IsNullOrEmpty(noTroopsMsg))
                failures.Add("D3: the NoTroops dim state carries no message");
            else if (noTroopsMsg.IndexOf("Barracks", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("D3: the NoTroops message does not name the Barracks — it must say where to fix it");

            src.Deployable = 3; model.Tick();
            if (model.RaidsDimReason != HudActionBarModel.RaidDimReason.ArmyNotFull)
                failures.Add("D3: a partly filled army reported '" + model.RaidsDimReason + "' — expected ArmyNotFull");
            if (string.Equals(noTroopsFace, model.RaidsFaceLabel, StringComparison.Ordinal))
                failures.Add("D3: both dim reasons render the SAME face text ('" + noTroopsFace +
                             "') — a single generic grey tells the player nothing");
            if (string.Equals(noTroopsMsg, model.RaidsDimMessage, StringComparison.Ordinal))
                failures.Add("D3: both dim reasons share the SAME message");
            if (string.Equals(model.RaidsFaceLabel, HudActionBarModel.RaidsBaseLabel, StringComparison.Ordinal))
                failures.Add("D3: the ArmyNotFull face text is the plain base label — hue-only tell");

            // D6 — ASCII-only user-facing copy.
            AssertAscii(failures, "NoTroops face label", noTroopsFace);
            AssertAscii(failures, "NoTroops message", noTroopsMsg);
            AssertAscii(failures, "ArmyNotFull face label", model.RaidsFaceLabel);
            AssertAscii(failures, "ArmyNotFull message", model.RaidsDimMessage);

            log.AppendLine("  D3/D6 two distinct worded reasons, ASCII-only — OK");
        }

        private static void AssertAscii(List<string> failures, string what, string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            for (int i = 0; i < s.Length; i++)
                if (s[i] > 126)
                {
                    failures.Add("D6: " + what + " carries a non-ASCII char (U+" + ((int)s[i]).ToString("X4") +
                                 ") — mobile font-atlas law");
                    return;
                }
        }

        // ── D5: the deleted troop clause never returns to the VISIBILITY bridge ─
        private static void CheckVisibilityPredicateSource(List<string> failures, StringBuilder log)
        {
            string path = Path.Combine(Application.dataPath, "_Modules/Village/Troops/RaidCapabilityHudBridge.cs");
            if (!File.Exists(path)) { failures.Add("D5: RaidCapabilityHudBridge.cs missing at " + path); return; }

            string code = StripCommentsAndStrings(File.ReadAllText(path));

            if (code.IndexOf("DeployableSlots", StringComparison.Ordinal) >= 0)
                failures.Add("D5: RaidCapabilityHudBridge reads DeployableSlots again — the troop clause is back in " +
                             "the VISIBILITY predicate and the face will vanish on an empty army (WO-1008 regression)");
            if (code.IndexOf("ArmyReadiness", StringComparison.Ordinal) >= 0)
                failures.Add("D5: RaidCapabilityHudBridge references ArmyReadiness again — readiness decides the DIM " +
                             "state (Core model) and the REFUSAL (RaidSelectionScreen), never visibility");
            if (code.IndexOf("StructureSingleton.IsBuilt", StringComparison.Ordinal) < 0)
                failures.Add("D5: RaidCapabilityHudBridge no longer checks StructureSingleton.IsBuilt — the barracks " +
                             "half of the predicate is the ONE remaining hide rule");
            if (code.IndexOf("SetRaidCapable", StringComparison.Ordinal) < 0)
                failures.Add("D5: RaidCapabilityHudBridge never publishes SetRaidCapable");

            // The View must paint the model's WORDS, not invent its own (or paint hue only).
            string kit = Path.Combine(Application.dataPath, "_Modules/HUD/Kit/HudKitController.cs");
            if (!File.Exists(kit)) { failures.Add("D5: HudKitController.cs missing at " + kit); return; }
            string kitCode = StripCommentsAndStrings(File.ReadAllText(kit));
            if (kitCode.IndexOf("RaidsFaceLabel", StringComparison.Ordinal) < 0)
                failures.Add("D3/D5: HudKitController never applies HudActionBarModel.RaidsFaceLabel — the greyed " +
                             "Raids face would carry its meaning in hue alone");

            log.AppendLine("  D5 visibility predicate = flag + barracks only; View paints the model's words — OK");
        }

        // ── D4: the real gate is untouched ───────────────────────────────────
        private static void CheckUnderlyingGateUnchanged(List<string> failures, StringBuilder log)
        {
            string path = Path.Combine(Application.dataPath, "_Modules/Village/Hero/RaidSelectionScreen.cs");
            if (!File.Exists(path)) { failures.Add("D4: RaidSelectionScreen.cs missing at " + path); return; }

            string code = StripCommentsAndStrings(File.ReadAllText(path));

            if (code.IndexOf("ArmyReadiness.Compute", StringComparison.Ordinal) < 0)
                failures.Add("D4: RaidSelectionScreen.Open no longer recomputes ArmyReadiness — WO-1008 changes the " +
                             "BUTTON's legibility only; the product rule (a full army to raid) must be untouched");
            if (code.IndexOf("readiness.Ready", StringComparison.Ordinal) < 0)
                failures.Add("D4: RaidSelectionScreen.Open no longer branches on readiness.Ready — the full-army " +
                             "refusal was weakened");
            if (code.IndexOf("TroopDialogueCommands.ShowTrainingUI", StringComparison.Ordinal) < 0)
                failures.Add("D4: the refusal no longer redirects to the drillmaster training panel");
            if (code.IndexOf("RaidTestBypassArmyGate", StringComparison.Ordinal) < 0)
                failures.Add("D4: the ff.raidtest bypass vanished — the owner needs it to test the raid pillar");

            // Exactly ONE bypass: the test flag must not have leaked into the visibility bridge too
            // (that would double-gate / double-bypass and make a capture unreadable).
            string bridge = Path.Combine(Application.dataPath, "_Modules/Village/Troops/RaidCapabilityHudBridge.cs");
            if (File.Exists(bridge) &&
                StripCommentsAndStrings(File.ReadAllText(bridge))
                    .IndexOf("RaidTestBypassArmyGate", StringComparison.Ordinal) >= 0)
                failures.Add("D4: RaidTestBypassArmyGate leaked into the visibility bridge — the bypass belongs to " +
                             "the SELECTION-SCREEN gate only, one bypass in one place");

            log.AppendLine("  D4 full-army refusal + drillmaster redirect + single ff.raidtest bypass intact — OK");
        }

        // ── comment AND string-literal stripper (no hollow source matches) ────
        private static string StripCommentsAndStrings(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];

                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    sb.Append(' ');
                    if (end < 0) break;
                    i = end + 1;
                    continue;
                }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                if (c == '@' && i + 1 < src.Length && src[i + 1] == '"')      // verbatim string
                {
                    i += 2;
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }  // "" escape
                            break;
                        }
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                if (c == '"' || c == '\'')                                    // string / char literal
                {
                    char quote = c;
                    i++;
                    while (i < src.Length && src[i] != quote)
                    {
                        if (src[i] == '\\') i++;   // skip the escaped char
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "RAIDS DISCOVERABILITY OK — a built Barracks always shows the Raids face; zero troops " +
                         "greys it with a NoTroops reason; the two dim reasons carry distinct words; the " +
                         "full-army gate is unchanged";
                Debug.Log("RAIDS_DISCOVERABILITY_OK\n" + log);
                return true;
            }
            reason = "RAIDS DISCOVERABILITY: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("RAIDS_DISCOVERABILITY_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
