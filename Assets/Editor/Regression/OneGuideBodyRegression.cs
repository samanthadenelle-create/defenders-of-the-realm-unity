// =============================================================================
// OneGuideBodyRegression [one-guide-body]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
//
// WO-1014 acceptance line "EXACTLY ONE guide body spawns in the tutorial, EVER."
// Owner felt-test 2026-08-10 on the 20:42 build, verbatim: "but still wolf and npc".
//
// THE SHAPE OF THE BUG, so a future reader does not have to re-derive it: the
// guide's identity resolves down a CHAIN in TutorialWorldAnchors.ResolveGuide -
// (1) the live pet-Echo body, (2) the steward stand-in found by
// GameObject.Find("Sylas"), (3) the Heart. SylasStewardInjector seats link (2)
// on hub load, gated ONLY on "the founding arc is incomplete" - it had no notion
// that link (1) might exist. That was harmless for exactly as long as the guide
// had no body. WO-961 shipped the body, and the chain became two figures standing
// in the same courtyard: the spotlight pointed at the wolf while the stand-in it
// was supposed to REPLACE stood next to it. Retiring the legacy Sylas DIALOGUE
// (the same WO's Half A) could never have fixed this - the injector spawns
// independently of any dialogue, which is exactly why the owner still saw two
// bodies after the script was clean.
//
// THE INVARIANT: one authority decides whether the guide has a body, and every
// consumer asks IT. TutorialWorldAnchors.LiveGuideBody / .HasLiveGuideBody is
// that authority; the chain head reads it, and the stand-in's spawn gate and
// stand-down watch read the same call. A second, private copy of that lookup
// anywhere is the defect returning in a new costume.
//
// WHAT THIS SUITE PROVES, AND WHAT IT CANNOT:
//   (a) SOURCE INVARIANT (comment-stripped lint) - the authority exists, the
//       chain consults it FIRST, and the stand-in is gated on it in BOTH
//       directions (never seat when a body exists; stand down when one appears).
//   (b) CENSUS - exactly one place in the whole module tree can seat a
//       GameObject under the load-bearing name "Sylas" that ResolveGuide finds.
//       A second seater is a second guide body by construction.
//   NOT provable here: that only one body is on screen at the ARRIVE beat. That
//   is a RUNTIME fact and needs a capture or the owner's felt-verify - headless
//   editor code cannot stand in the courtyard and look (the 08-09 lesson; PO
//   closes, per docs/TICKET_PIPELINE.md).
//
// Markers: ONE_GUIDE_BODY_OK / ONE_GUIDE_BODY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.OneGuideBodyRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class OneGuideBodyRegression
    {
        private const string AnchorsSrc = "Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs";
        private const string StewardSrc = "Assets/_Modules/Village/NPCs/SylasStewardInjector.cs";
        private const string ModulesRoot = "Assets/_Modules";

        /// <summary>The load-bearing name ResolveGuide finds the stand-in by. Renaming it
        /// silently breaks the chain, so the census keys on it.</summary>
        private const string StandInName = "Sylas";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ONE_GUIDE_BODY_OK - " + reason);
            else Debug.LogError("ONE_GUIDE_BODY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "single-authority", () => Case1_SingleAuthority(failures));
                Case(failures, "chain-order",      () => Case2_ChainAsksAuthorityFirst(failures));
                Case(failures, "standin-gated",    () => Case3_StandInGatedBothWays(failures));
                Case(failures, "one-seater",       () => Case4_OnlyOneStandInSeater(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "ONE GUIDE BODY OK - TutorialWorldAnchors.LiveGuideBody/.HasLiveGuideBody is the single " +
                         "authority on whether the founding guide has a world body; ResolveGuide asks it at the " +
                         "HEAD of the chain (before the '" + StandInName + "' stand-in link); SylasStewardInjector " +
                         "asks the SAME call in both directions - it refuses to seat when a body already exists " +
                         "and stands the stand-in down the moment one appears - and exactly one place in " +
                         ModulesRoot + " can seat a GameObject under the load-bearing stand-in name, so a second " +
                         "guide figure cannot be introduced without failing this suite.";
                return true;
            }
            reason = "one-guide-body FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - ONE authority on "does the guide have a body"
        // =====================================================================

        private static void Case1_SingleAuthority(List<string> failures)
        {
            string anchors = ReadStripped(AnchorsSrc, failures);
            if (anchors == null) return;

            if (!Regex.IsMatch(anchors, @"public\s+static\s+Transform\s+LiveGuideBody\s*\("))
                failures.Add("[single-authority] TutorialWorldAnchors no longer declares " +
                             "'public static Transform LiveGuideBody()' - that call IS the single authority on " +
                             "whether the guide has a body (WO-1014). Without it every consumer re-implements " +
                             "the lookup and they drift apart, which is how the stand-in ended up standing next " +
                             "to the wolf instead of being replaced by it.");

            if (!Regex.IsMatch(anchors, @"public\s+static\s+bool\s+HasLiveGuideBody"))
                failures.Add("[single-authority] TutorialWorldAnchors no longer exposes 'HasLiveGuideBody' - " +
                             "the predicate the stand-in's spawn gate reads.");

            // The actual scene lookup may live in exactly ONE place: the authority itself.
            int lookups = Regex.Matches(anchors, @"FindAnyObjectByType\s*<\s*(DeNelle\.Pets\.)?Pet\s*>").Count;
            if (lookups != 1)
                failures.Add("[single-authority] TutorialWorldAnchors performs the live-Pet lookup " + lookups +
                             " time(s); it must be exactly ONCE, inside LiveGuideBody. More than one copy is " +
                             "two authorities that can disagree.");

            // And no consumer may roll its own.
            string steward = ReadStripped(StewardSrc, failures);
            if (steward != null && Regex.IsMatch(steward, @"FindAnyObjectByType\s*<\s*(DeNelle\.Pets\.)?Pet\s*>"))
                failures.Add("[single-authority] SylasStewardInjector performs its OWN live-Pet lookup instead " +
                             "of asking TutorialWorldAnchors.HasLiveGuideBody - the stand-in and the anchor " +
                             "chain must never be able to disagree about who the guide is.");
        }

        // =====================================================================
        //  Case 2 - the chain is a CHAIN: the body link answers before the stand-in link
        // =====================================================================

        private static void Case2_ChainAsksAuthorityFirst(List<string> failures)
        {
            string anchors = ReadStripped(AnchorsSrc, failures);
            if (anchors == null) return;

            // Find the METHOD by its declaration (not by any mention of its name) so the
            // ordering check below reads the real chain body and nothing else.
            var decl = Regex.Match(anchors, @"private\s+static\s+Transform\s+ResolveGuide\s*\(\s*\)");
            if (!decl.Success)
            {
                failures.Add("[chain-order] TutorialWorldAnchors has no 'private static Transform ResolveGuide()' " +
                             "- the guide resolution chain has moved; re-point this suite before trusting it.");
                return;
            }

            string body = anchors.Substring(decl.Index);
            int authority = body.IndexOf("LiveGuideBody()", StringComparison.Ordinal);
            int standIn = body.IndexOf("\"" + StandInName + "\"", StringComparison.Ordinal);

            if (authority < 0)
                failures.Add("[chain-order] ResolveGuide does not call LiveGuideBody() - the chain head must ask " +
                             "the single authority, not re-query the scene itself.");
            if (standIn < 0)
                failures.Add("[chain-order] ResolveGuide no longer looks for the \"" + StandInName + "\" stand-in " +
                             "- the body-less fallback is gone, so a failed guide summon would spotlight the " +
                             "Heart or empty air instead of a real character.");
            if (authority >= 0 && standIn >= 0 && authority > standIn)
                failures.Add("[chain-order] ResolveGuide consults the \"" + StandInName + "\" stand-in BEFORE the " +
                             "live guide body - the chain is inverted, so the stand-in would win over the real " +
                             "guide and the spotlight would point at the wrong figure.");
        }

        // =====================================================================
        //  Case 3 - the stand-in is gated in BOTH directions
        // =====================================================================

        private static void Case3_StandInGatedBothWays(List<string> failures)
        {
            string steward = ReadStripped(StewardSrc, failures);
            if (steward == null) return;

            int gates = Regex.Matches(steward, @"HasLiveGuideBody").Count;
            if (gates < 2)
                failures.Add("[standin-gated] SylasStewardInjector reads " +
                             "TutorialWorldAnchors.HasLiveGuideBody " + gates + " time(s); it needs BOTH gates. " +
                             "(1) Inject() must refuse to seat when the guide already has a body - the reload / " +
                             "hub re-enter case. (2) the poll in Update() must stand the stand-in DOWN when a " +
                             "body appears later - the FIRST-RUN case, because the hub loads before the ARRIVE " +
                             "beat summons the wolf, so the steward legitimately seats first and only becomes a " +
                             "second figure a beat later. One gate without the other leaves the owner's exact " +
                             "symptom ('but still wolf and npc') in one of the two paths.");

            var inject = MethodBody(steward, @"private\s+void\s+Inject\s*\(\s*\)");
            if (inject == null)
                failures.Add("[standin-gated] SylasStewardInjector has no Inject() method to gate.");
            else if (!inject.Contains("HasLiveGuideBody"))
                failures.Add("[standin-gated] SylasStewardInjector.Inject() does not check HasLiveGuideBody - " +
                             "a hub re-load or a resumed mid-arc save would seat a second guide figure.");

            var update = MethodBody(steward, @"private\s+void\s+Update\s*\(\s*\)");
            if (update == null)
                failures.Add("[standin-gated] SylasStewardInjector has no Update() poll to stand the stand-in down.");
            else
            {
                if (!update.Contains("HasLiveGuideBody"))
                    failures.Add("[standin-gated] SylasStewardInjector.Update() does not watch HasLiveGuideBody - " +
                                 "the stand-in seats before the guide's body exists, so WITHOUT this watch the " +
                                 "two stand side by side for the whole founding arc (the shipped 20:42 defect).");
                if (!update.Contains("Destroy("))
                    failures.Add("[standin-gated] SylasStewardInjector.Update() never destroys the stand-in " +
                                 "holder - watching without acting is not a gate.");
            }

            // The stand-in must NOT be deleted outright: it is still the honest degradation
            // path when the guide's body fails to summon (TutorialFlow says so in its own
            // warn). Pin that the seating code still exists.
            if (!steward.Contains("SpawnBody("))
                failures.Add("[standin-gated] SylasStewardInjector no longer spawns a body at all - the stand-in " +
                             "was DELETED rather than gated. It must survive as the body-less fallback, or a " +
                             "failed guide summon leaves 'Follow {guide}' pointing at nothing.");
        }

        // =====================================================================
        //  Case 4 - exactly one place can seat the stand-in
        // =====================================================================

        private static void Case4_OnlyOneStandInSeater(List<string> failures)
        {
            if (!Directory.Exists(ModulesRoot))
            {
                failures.Add("[one-seater] " + ModulesRoot + " does not exist.");
                return;
            }

            // Any assignment of the load-bearing name to a GameObject, or a GameObject
            // constructed under it. Both are ways to become a thing ResolveGuide finds.
            var seatPattern = new Regex(
                @"(\.name\s*=\s*""" + StandInName + @"""|new\s+GameObject\s*\(\s*""" + StandInName + @"""\s*\))");

            var seaters = new List<string>();
            foreach (string path in Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string src = StripComments(File.ReadAllText(path));
                if (seatPattern.IsMatch(src)) seaters.Add(path.Replace('\\', '/'));
            }

            if (seaters.Count != 1)
                failures.Add("[one-seater] " + seaters.Count + " source file(s) can seat a GameObject named \"" +
                             StandInName + "\" [" + string.Join(", ", seaters) + "] - expected exactly ONE " +
                             "(SylasStewardInjector). ResolveGuide finds the stand-in BY THAT NAME, so a second " +
                             "seater is a second guide figure by construction, and the chain cannot tell them " +
                             "apart. Note this is about the NPC BODY only: 'Sylas' remains a canon hero/companion " +
                             "NAME (HeroCanonNames, CompanionSpawner) and this suite deliberately does not " +
                             "restrict that.");
            else if (!seaters[0].EndsWith("SylasStewardInjector.cs", StringComparison.OrdinalIgnoreCase))
                failures.Add("[one-seater] the single stand-in seater is '" + seaters[0] + "', not " +
                             "SylasStewardInjector.cs - the gating in Case 3 lints the wrong file.");

            // ResolveGuide's stand-in link actually looks for "CompanionIntroducer" FIRST and
            // only then the steward. That body is seated by CastleCompanionIntroducerInjector,
            // which stands down under ff.singlehero (default ON) - so today it cannot appear.
            // Pin that standdown: without it there is a THIRD possible guide figure, seated by
            // a different file, that the census above would not catch.
            const string IntroducerSrc = "Assets/_Modules/Village/NPCs/CastleCompanionIntroducerInjector.cs";
            if (File.Exists(IntroducerSrc))
            {
                string intro = StripComments(File.ReadAllText(IntroducerSrc));
                if (!Regex.IsMatch(intro, @"FeatureFlags\.SingleHero\s*\)\s*return"))
                    failures.Add("[one-seater] CastleCompanionIntroducerInjector no longer stands down on " +
                                 "FeatureFlags.SingleHero - it seats a 'CompanionIntroducer' body that " +
                                 "ResolveGuide prefers even over the steward, so a second stand-in could " +
                                 "appear beside the guide's real body.");
            }
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        private static string ReadStripped(string path, List<string> failures)
        {
            if (!File.Exists(path)) { failures.Add("[read] missing " + path); return null; }
            return StripComments(File.ReadAllText(path));
        }

        /// <summary>Comment-stripped lint input: every invariant below is about CODE, and
        /// this repo's comments quote the very identifiers being asserted (they narrate the
        /// history), so an un-stripped lint would pass on prose alone.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", " ");
            return src;
        }

        /// <summary>The brace-balanced body of the first method matching <paramref name="declPattern"/>,
        /// or null. Operates on comment-stripped source.</summary>
        private static string MethodBody(string strippedSrc, string declPattern)
        {
            // Brace CHARACTERS are spelled by code point, never as literals: the project's
            // mandatory C# gate counts raw brace characters per file, and a lone open-brace
            // char literal makes a perfectly correct file read as unbalanced to it.
            const char OpenBrace = (char)123;
            const char CloseBrace = (char)125;

            var m = Regex.Match(strippedSrc, declPattern);
            if (!m.Success) return null;
            int open = strippedSrc.IndexOf(OpenBrace, m.Index + m.Length);
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < strippedSrc.Length; i++)
            {
                if (strippedSrc[i] == OpenBrace) depth++;
                else if (strippedSrc[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return strippedSrc.Substring(open, i - open + 1);
                }
            }
            return null;
        }
    }
}
