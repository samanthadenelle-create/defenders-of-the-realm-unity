// =============================================================================
// OneGuideBodyRegression [one-guide-body]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
//
// WO-971 acceptance line: "EXACTLY ONE TUTORIAL, AND EXACTLY ONE GUIDE BODY."
// Owner ruling 2026-08-10, verbatim: "why are two tutorials active?" /
// "remove the original" / "only the new wolf one stays".
//
// ── WHAT WENT WRONG, SO A FUTURE READER NEED NOT RE-DERIVE IT ────────────────
// PROVEN BY CAPTURE (owner Player.log, the 2026-08-10 20:42 build), four lines:
//     [Flow:SylasSteward] Sylas steward spawned at (2.00, 0.08, 3.00) - founding beats have a body.
//     [Flow:Tutorial]  step 'founding_greet' grant.starterPet - guide BODY summoned ('ice-wolf') at (2.00, 0.06, 3.00)
//     [Flow:Tutorial]  FocusMask resolved highlightId=world.guide target=Sylas
//     [Flow:Tutorial]  FocusMask resolved highlightId=world.guide target=Pet_ice-wolf
// Two guide bodies TWO CENTIMETRES apart, and the single "world.guide" spotlight
// alternating between them while the objective strip read "Follow Aldwin to the
// gate". Her screenshot shows the gold ring around a peasant NPC with the wolf
// standing inside the same ring.
//
// The cause was structural, not a bug in either body. The guide's identity
// resolved down a CHAIN in TutorialWorldAnchors.ResolveGuide - (1) the live
// pet-Echo body, (2) a steward stand-in found by GameObject.Find("Sylas"),
// (3) the Heart. SylasStewardInjector seated link (2) on hub load. That was
// harmless for exactly as long as the guide had no body; WO-961 shipped the
// body and the chain became two figures in one courtyard.
//
// WO-1014 tried to KEEP link (2) and GATE it (never seat when a body exists;
// stand down when one appears). That did not hold - the stand-down never fired
// in the shipped build (ZERO occurrences of its own trace line in her log) - and
// more importantly the owner overruled the approach itself: a fallback that can
// be on screen at the same time as the real guide is not a fallback, it is a
// second guide. WO-971 therefore REMOVES rather than gates.
//
// ── WHAT WAS REMOVED (all four, deliberately, by the ruling) ─────────────────
//   * Assets/_Modules/Village/NPCs/SylasStewardInjector.cs  (the second BODY)
//   * Assets/_Modules/Village/Tutorial/TutorialDirector.cs  (the legacy FTUE FLOW)
//   * Assets/_Modules/Village/Tutorial/PetIntroduction.cs   (director-only screen)
//   * the stand-in link inside TutorialWorldAnchors.ResolveGuide
// NOT removed, and deliberately so: Sylas the CHARACTER. He is a canon hero name
// (HeroCanonNames / hero.ranger in en.json), keeps his portrait, his abilities.json
// kit and his non-tutorial "SylasFirstMeeting" companion beat. Only his role as a
// tutorial GUIDE BODY is gone. Case 5 pins that distinction so a later cleanup
// cannot mistake this suite for a licence to delete the character.
// Also NOT removed: TutorialWaveSpawner, TutorialAutoWalk, TutorialHudOverlay,
// TutorialDialogue, DialogueService, DialogueCommandSink and CompanionSpawner -
// they live in the same folder but the SURVIVING arc and live non-tutorial
// systems consume them (TutorialFlow.cs:1550 adds a TutorialWaveSpawner;
// DialogueCommandSink adds TutorialAutoWalk + TutorialHudOverlay;
// ElaraWaveThreeJoin and StoryCompanionInjector call CompanionSpawner). Deleting
// them would have been an orphaned-reference outage, not a cleanup. Case 6 pins
// that they still exist, so this suite guards BOTH failure directions.
//
// ── WHAT THIS SUITE PROVES, AND WHAT IT CANNOT ──────────────────────────────
//   (a) SOURCE INVARIANT (comment-stripped lint) - one authority on "does the
//       guide have a body"; the chain asks it and has NO second body link.
//   (b) CENSUS over the whole module tree - no file may seat a guide-stand-in
//       body, and no file may declare a second tutorial FLOW.
//   NOT provable here: that only one body is on screen at the ARRIVE beat. That
//   is a RUNTIME fact and needs a capture or the owner's felt-verify - headless
//   editor code cannot stand in the courtyard and look. PO closes (§13).
//
// Markers: ONE_GUIDE_BODY_OK / ONE_GUIDE_BODY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.OneGuideBodyRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; the class name and
// entry points are UNCHANGED by WO-971, so no DataRegression edit is required
// (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class OneGuideBodyRegression
    {
        private const string AnchorsSrc  = "Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs";
        private const string FlowSrc     = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";
        private const string ModulesRoot = "Assets/_Modules";

        /// <summary>The name the DELETED stand-in was seated under, and that ResolveGuide
        /// used to find it by. The census keys on it because re-seating a GameObject under
        /// this name is the cheapest way to accidentally resurrect the second guide.</summary>
        private const string StandInName = "Sylas";

        /// <summary>Every file WO-971 deleted. Any of them coming back is the ruling undone.</summary>
        private static readonly string[] RemovedSources =
        {
            "Assets/_Modules/Village/NPCs/SylasStewardInjector.cs",
            "Assets/_Modules/Village/Tutorial/TutorialDirector.cs",
            "Assets/_Modules/Village/Tutorial/PetIntroduction.cs",
        };

        /// <summary>Shared machinery the SURVIVING arc and live non-tutorial systems consume.
        /// Listed so an over-eager "delete the legacy tutorial folder" pass fails loudly
        /// instead of leaving orphaned references that compile and blank at runtime.</summary>
        private static readonly string[] MustSurviveSources =
        {
            "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs",
            "Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs",
            "Assets/_Modules/Village/Tutorial/TutorialWaveSpawner.cs",
            "Assets/_Modules/Village/Tutorial/TutorialAutoWalk.cs",
            "Assets/_Modules/Village/Tutorial/TutorialHudOverlay.cs",
            "Assets/_Modules/Village/Tutorial/TutorialDialogue.cs",
            "Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs",
            "Assets/_Modules/Village/Tutorial/CompanionSpawner.cs",
        };

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
                Case(failures, "single-authority",  () => Case1_SingleAuthority(failures));
                Case(failures, "no-standin-link",   () => Case2_ChainHasNoStandInLink(failures));
                Case(failures, "original-removed",  () => Case3_TheOriginalIsRemoved(failures));
                Case(failures, "one-flow",          () => Case4_ExactlyOneTutorialFlow(failures));
                Case(failures, "character-kept",    () => Case5_SylasTheCharacterSurvives(failures));
                Case(failures, "shared-kept",       () => Case6_SharedMachinerySurvives(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "ONE GUIDE BODY OK - exactly ONE tutorial flow arms (TutorialFlow, the founding " +
                         "wolf-guide arc) and the guide has exactly ONE possible body. " +
                         "TutorialWorldAnchors.LiveGuideBody/.HasLiveGuideBody remains the single authority " +
                         "on whether that body exists; ResolveGuide carries NO second body link (the \"" +
                         StandInName + "\" stand-in is removed, not gated); nothing under " + ModulesRoot +
                         " can seat a stand-in under that name; the legacy TutorialDirector FTUE and its " +
                         "director-only screen are deleted; and the shared machinery the surviving arc " +
                         "depends on is still present. Sylas remains a canon CHARACTER - only his tutorial " +
                         "guide-body role is gone (WO-971, owner: 'remove the original', 'only the new wolf " +
                         "one stays').";
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
                             "whether the guide has a body. Without it every consumer re-implements the lookup " +
                             "and they drift apart, which is exactly how a stand-in ended up standing next to " +
                             "the wolf instead of being replaced by it.");

            if (!Regex.IsMatch(anchors, @"public\s+static\s+bool\s+HasLiveGuideBody"))
                failures.Add("[single-authority] TutorialWorldAnchors no longer exposes 'HasLiveGuideBody' - " +
                             "the predicate every consumer must read instead of re-querying the scene.");

            // The actual scene lookup may live in exactly ONE place: the authority itself.
            int lookups = Regex.Matches(anchors, @"FindAnyObjectByType\s*<\s*(DeNelle\.Pets\.)?Pet\s*>").Count;
            if (lookups != 1)
                failures.Add("[single-authority] TutorialWorldAnchors performs the live-Pet lookup " + lookups +
                             " time(s); it must be exactly ONCE, inside LiveGuideBody. More than one copy is " +
                             "two authorities that can disagree.");
        }

        // =====================================================================
        //  Case 2 - the chain has NO stand-in body link at all
        // =====================================================================

        private static void Case2_ChainHasNoStandInLink(List<string> failures)
        {
            string anchors = ReadStripped(AnchorsSrc, failures);
            if (anchors == null) return;

            var decl = Regex.Match(anchors, @"private\s+static\s+Transform\s+ResolveGuide\s*\(\s*\)");
            if (!decl.Success)
            {
                failures.Add("[no-standin-link] TutorialWorldAnchors has no 'private static Transform " +
                             "ResolveGuide()' - the guide resolution chain has moved; re-point this suite " +
                             "before trusting it.");
                return;
            }

            string body = MethodBody(anchors, @"private\s+static\s+Transform\s+ResolveGuide\s*\(\s*\)");
            if (body == null)
            {
                failures.Add("[no-standin-link] could not read the body of ResolveGuide().");
                return;
            }

            if (body.IndexOf("LiveGuideBody()", StringComparison.Ordinal) < 0)
                failures.Add("[no-standin-link] ResolveGuide does not call LiveGuideBody() - the chain head " +
                             "must ask the single authority, not re-query the scene itself.");

            // THE RULING, as code: no second BODY may answer "who is the guide".
            if (body.IndexOf("\"" + StandInName + "\"", StringComparison.Ordinal) >= 0)
                failures.Add("[no-standin-link] ResolveGuide looks for a GameObject named \"" + StandInName +
                             "\" again. That link is the SECOND GUIDE the owner ruled out on 2026-08-10 " +
                             "(\"remove the original\", \"only the new wolf one stays\") - it put a steward NPC " +
                             "two centimetres from the wolf with the one spotlight alternating between them. " +
                             "It was already tried as a GATED fallback (WO-1014) and the owner still saw both. " +
                             "Fall through to the Heart instead.");

            if (body.IndexOf("\"CompanionIntroducer\"", StringComparison.Ordinal) >= 0)
                failures.Add("[no-standin-link] ResolveGuide looks for a \"CompanionIntroducer\" body again - " +
                             "the same second-guide defect wearing the other stand-in's name.");

            // The honest degradation path must still exist: the Heart, never a character.
            if (body.IndexOf("HeartController", StringComparison.Ordinal) < 0)
                failures.Add("[no-standin-link] ResolveGuide no longer falls back to the HeartController. With " +
                             "the stand-in removed the Heart IS the degradation path - the tree the guide wakes " +
                             "from. Without it a failed summon spotlights empty air.");
        }

        // =====================================================================
        //  Case 3 - the ORIGINAL is removed, not disabled and not flag-gated
        // =====================================================================

        private static void Case3_TheOriginalIsRemoved(List<string> failures)
        {
            foreach (string path in RemovedSources)
            {
                if (!File.Exists(path)) continue;
                failures.Add("[original-removed] " + path + " is back. The owner ruled on 2026-08-10: " +
                             "\"remove the original\", \"only the new wolf one stays\" - REMOVED, not disabled " +
                             "and not flag-gated. A feature flag is what this file already had (ff.tutorialv2 " +
                             "stood the legacy director down) and the owner still ended up with two tutorials " +
                             "in the tree. If this file is genuinely needed again, that is an owner decision, " +
                             "not a merge.");
            }
        }

        // =====================================================================
        //  Case 4 - exactly ONE tutorial flow, and no way to seat a second guide body
        // =====================================================================

        private static void Case4_ExactlyOneTutorialFlow(List<string> failures)
        {
            if (!Directory.Exists(ModulesRoot))
            {
                failures.Add("[one-flow] " + ModulesRoot + " does not exist.");
                return;
            }

            // (a) BODY CENSUS. Any assignment of the load-bearing name to a GameObject, or a
            //     GameObject constructed under it - both are ways to become a second guide.
            var seatPattern = new Regex(
                @"(\.name\s*=\s*""" + StandInName + @"""|new\s+GameObject\s*\(\s*""" + StandInName + @"""\s*\))");

            // (b) FLOW CENSUS. A tutorial FLOW is a MonoBehaviour that (i) self-installs via
            //     RuntimeInitializeOnLoadMethod or is added by one, and (ii) drives tutorial
            //     STEPS. The cheap, stable signature for (ii) is emitting a step-enter trace.
            //     TutorialFlow is the one legitimate holder.
            var stepDriver = new Regex(@"STEP-ENTER|AdvanceToNextStep\s*\(");

            var seaters = new List<string>();
            var flows = new List<string>();
            foreach (string path in Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories))
            {
                string src = StripComments(File.ReadAllText(path));
                string norm = path.Replace('\\', '/');
                if (seatPattern.IsMatch(src)) seaters.Add(norm);
                if (stepDriver.IsMatch(src)) flows.Add(norm);
            }

            if (seaters.Count != 0)
                failures.Add("[one-flow] " + seaters.Count + " source file(s) can seat a GameObject named \"" +
                             StandInName + "\" [" + string.Join(", ", seaters) + "] - expected ZERO. WO-971 " +
                             "removed the guide stand-in entirely; any seater is a second guide figure by " +
                             "construction. NOTE this is about the NPC BODY only - \"" + StandInName + "\" " +
                             "remains a canon hero/companion NAME and this suite deliberately does not " +
                             "restrict that (see Case 5).");

            if (flows.Count != 1)
                failures.Add("[one-flow] " + flows.Count + " source file(s) drive tutorial steps [" +
                             string.Join(", ", flows) + "] - expected exactly ONE. The owner asked \"why are " +
                             "two tutorials active?\" and ruled that only the founding wolf-guide arc stays. " +
                             "Two step drivers is two tutorials, whatever the flags say.");
            else if (!flows[0].EndsWith("V2/TutorialFlow.cs", StringComparison.OrdinalIgnoreCase))
                failures.Add("[one-flow] the single tutorial step driver is '" + flows[0] + "', not " +
                             "V2/TutorialFlow.cs - the surviving arc is supposed to be the founding " +
                             "wolf-guide flow. Re-point this suite only if the owner moved it deliberately.");

            // (c) The surviving flow must still be the WOLF one (owner: "only the new wolf one stays").
            string flow = ReadStripped(FlowSrc, failures);
            if (flow != null && flow.IndexOf("ice-wolf", StringComparison.Ordinal) < 0)
                failures.Add("[one-flow] " + FlowSrc + " no longer references the 'ice-wolf' guide body. The " +
                             "surviving tutorial is defined by its guide: the founding arc summons exactly one " +
                             "Echo with a world body because a beat tells the player to follow it (WO-961).");
        }

        // =====================================================================
        //  Case 5 - Sylas the CHARACTER survives; only the guide-body role went
        // =====================================================================

        private static void Case5_SylasTheCharacterSurvives(List<string> failures)
        {
            // The carve-out the owner's ruling did NOT cover. Deleting the injector must not
            // be read as deleting the person: en.json gives hero.ranger = Sylas, and he owns
            // a hero kit in abilities.json plus a non-tutorial companion beat.
            const string CanonNames = "Assets/_Modules/Core";
            bool nameFound = false;
            if (Directory.Exists(CanonNames))
            {
                foreach (string path in Directory.GetFiles(CanonNames, "*.cs", SearchOption.AllDirectories))
                {
                    if (File.ReadAllText(path).IndexOf("\"" + StandInName + "\"", StringComparison.Ordinal) >= 0)
                    { nameFound = true; break; }
                }
            }
            if (!nameFound)
                failures.Add("[character-kept] the canon hero NAME \"" + StandInName + "\" is no longer present " +
                             "anywhere under " + CanonNames + ". WO-971 removed his tutorial GUIDE BODY, not the " +
                             "character - en.json binds hero.ranger to him and abilities.json carries his kit. " +
                             "If this suite's removal was read as 'delete Sylas', that is a mis-read: revert the " +
                             "character, keep the guide-body removal.");

            const string CompanionBeat = "Assets/_Modules/Village/NPCs/SylasFirstMeeting.cs";
            if (!File.Exists(CompanionBeat))
                failures.Add("[character-kept] " + CompanionBeat + " is gone. That is his NON-tutorial companion " +
                             "beat (already stood down under ff.singlehero) and it is out of WO-971's scope - " +
                             "only the tutorial arc and the guide body were ruled out.");
        }

        // =====================================================================
        //  Case 6 - the shared machinery the surviving arc depends on is intact
        // =====================================================================

        private static void Case6_SharedMachinerySurvives(List<string> failures)
        {
            foreach (string path in MustSurviveSources)
            {
                if (File.Exists(path)) continue;
                failures.Add("[shared-kept] " + path + " is missing. It sits in (or beside) the legacy tutorial " +
                             "folder but the SURVIVING founding arc or a live non-tutorial system consumes it - " +
                             "TutorialFlow adds a TutorialWaveSpawner, DialogueCommandSink adds TutorialAutoWalk " +
                             "and TutorialHudOverlay, and ElaraWaveThreeJoin / StoryCompanionInjector call " +
                             "CompanionSpawner. Deleting the whole folder is an orphaned-reference outage, not a " +
                             "cleanup: it compiles right up until the beat runs and blanks.");
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

        /// <summary>Comment-stripped lint input: every invariant here is about CODE, and this
        /// repo's comments quote the very identifiers being asserted (they narrate the history),
        /// so an un-stripped lint would pass on prose alone - including on the removal notes
        /// this very WO left behind, which name "Sylas" and "CompanionIntroducer" verbatim.</summary>
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
