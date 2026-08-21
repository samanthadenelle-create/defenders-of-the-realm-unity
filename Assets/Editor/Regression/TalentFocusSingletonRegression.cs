// =============================================================================
// TalentFocusSingletonRegression [talent-focus] (WO-1021 sec 2.1d) - the talent
// board can never again grow ONE OVERSIZED GOLD PLATE PER TRACK.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
// Markers: TALENT_FOCUS_OK / TALENT_FOCUS_FAIL (DISTINCT per CLAUDE.md sec.8 - a
// bare REGRESSION_OK is how a 22-case pass once read as the full suite's pass).
//
// WHAT BROKE (owner F8, screenshot at WIS 252, verbatim "Still Messy"), traced at
// source 2026-08-16:
//
//   HeroSkillTreeVM.ResolveStates resets its `nextTaken` flag on EVERY call and is
//   invoked ONCE PER TRACK. Its own comment is precise: "On an ORDERED track exactly
//   ONE node may be Next" - PER TRACK. So the board carries one SkillNodeState.Next
//   per ordered track, which is CORRECT and load-bearing for WO-910's Inert rule.
//
//   The VIEW then read that per-track signal as if it were board-level:
//       bool focus = seat.State == SkillNodeState.Next || (selectedId == seat.Node.Id);
//   and every focus plate was built at NodeFocusPx (168) instead of NodeSizePx (136)
//   with a thick gold double ring. One oversized shouting plate PER TRACK - ~10 of
//   them once Wisdom was spendable, overlapping each other and occluding neighbours'
//   art and cost pips. The panel's own header premise ("ONE thick gold FOCUS plate")
//   was violated BY CONSTRUCTION the moment a second track existed. It was INVISIBLE
//   at 0 currency because nothing qualified - which is why the board read WORSE with
//   currency than without.
//
// THE LAW THIS PINS - size is the scarce visual channel and SELECTION owns it:
//   1 [singleton]  ResolveFocusNodeId returns AT MOST ONE id across a MULTI-TRACK
//                  fixture, so at most ONE plate is ever built at NodeFocusPx.
//   2 [next-cue]   the per-track NEXT cue is present, is NOT size-differentiated
//                  (NodePlateSizePx depends on focus and nothing else), and is not
//                  hue-only: its ink/disc clear a Rec.709 LUMA gap, so it survives a
//                  greyscale pass (owner is red/green colourblind - colourblind law).
//   3 [solver]     SolveGraphLatticePx (WO-1021 sec 2.1b) keeps every pair at or past
//                  MinNodePitchPx in Chebyshev distance and insets the extreme plates
//                  by a FOCUS half-plate, at the reference well AND at a degenerate one.
//   4 [source]     the view can never re-conflate the two ideas: no `focus` expression
//                  may read SkillNodeState.Next, no plate SIZE may key off a state, and
//                  the NEXT badge builder may not touch the plate-size constants.
//                  Matched on source with comments AND STRING LITERALS stripped - three
//                  oracles on 2026-08-15 passed by matching their own prose.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.TalentFocusSingletonRegression.RunAll
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village.Talents;

namespace DeNelle.Editor.Regression
{
    public static class TalentFocusSingletonRegression
    {
        private const string ViewSrc = "Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs";

        /// <summary>Reference body well (2340x1080 device), the same rect the sibling
        /// skills-panel oracle replays its band arithmetic against.</summary>
        private const float RefBodyWidthPx = 1695f;
        private const float RefBodyHeightPx = 493f;

        /// <summary>Minimum Rec.709 luma separation for a cue to survive a GREYSCALE pass.
        /// 0.45 is deliberately far above "just visible": the badge must read at arm's
        /// length on a device, with hue stripped, over arbitrary skill art.</summary>
        private const float MinGreyscaleLumaGap = 0.45f;

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TALENT_FOCUS_OK - " + reason);
            else Debug.LogError("TALENT_FOCUS_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "singleton", () => Case1_FocusSingleton(failures, notes));
                Case(failures, "next-cue", () => Case2_NextCue(failures, notes));
                Case(failures, "solver", () => Case3_SolverPitch(failures, notes));
                Case(failures, "source", () => Case4_SourceLaws(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "TALENT FOCUS OK - at most ONE oversized plate on a multi-track board, " +
                         "the per-track NEXT cue is normal-size and greyscale-separable, and the " +
                         "lattice solver holds the minimum pitch" + noteStr;
                return true;
            }
            reason = "talent-focus FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - AT MOST ONE oversized plate, on a MULTI-TRACK board
        // =====================================================================
        private static void Case1_FocusSingleton(List<string> failures, List<string> notes)
        {
            // Three ORDERED tracks, each with its own frontier step - i.e. THREE Next
            // seats, exactly what the VM legitimately produces and what the view used to
            // turn into three oversized plates.
            var ids = new List<string>
            {
                "a1", "a2", "a3",
                "b1", "b2", "b3",
                "c1", "c2", "c3"
            };
            var states = new List<SkillNodeState>
            {
                SkillNodeState.Owned,  SkillNodeState.Next,   SkillNodeState.Locked,
                SkillNodeState.Owned,  SkillNodeState.Next,   SkillNodeState.Locked,
                SkillNodeState.Owned,  SkillNodeState.Next,   SkillNodeState.Locked
            };

            int nextSeats = 0;
            for (int i = 0; i < states.Count; i++) if (states[i] == SkillNodeState.Next) nextSeats++;
            if (nextSeats < 3)
            {
                failures.Add("[singleton] the fixture no longer carries multiple per-track Next seats - " +
                             "this oracle is blind to the defect it exists for");
                return;
            }

            // (a) NOTHING selected - the untouched board.
            string focusIdle = HeroSkillTreePanelMvvm.ResolveFocusNodeId(ids, states, null);
            int oversizedIdle = CountOversized(ids, focusIdle);
            if (oversizedIdle > 1)
                failures.Add("[singleton] " + oversizedIdle + " oversized plates on an UNSELECTED board with " +
                             nextSeats + " per-track Next seats - NodeFocusPx may apply to AT MOST ONE plate. " +
                             "This is the WIS-252 \"still messy\" defect: the view is consuming a per-track " +
                             "signal as a board-level one again. Fix the VIEW, never HeroSkillTreeVM");

            // (b) the player taps a node in the LAST track - selection wins, and it is still one.
            string focusSel = HeroSkillTreePanelMvvm.ResolveFocusNodeId(ids, states, "c3");
            if (!string.Equals(focusSel, "c3", StringComparison.Ordinal))
                failures.Add("[singleton] the tapped node 'c3' is not the focus (got \"" + focusSel + "\") - " +
                             "SELECTION is the board-level singleton and must own the size channel");
            int oversizedSel = CountOversized(ids, focusSel);
            if (oversizedSel > 1)
                failures.Add("[singleton] " + oversizedSel + " oversized plates while a node is SELECTED - " +
                             "the per-track Next seats are still buying themselves size");

            // (c) a selection that is NOT on the board must not orphan the focus into a set.
            string focusStale = HeroSkillTreePanelMvvm.ResolveFocusNodeId(ids, states, "zz-not-on-board");
            if (CountOversized(ids, focusStale) > 1)
                failures.Add("[singleton] a stale selection produced more than one oversized plate");

            // (d) no ordered track at all - zero is legal, more than one never is.
            var flatStates = new List<SkillNodeState>();
            for (int i = 0; i < ids.Count; i++) flatStates.Add(SkillNodeState.Locked);
            string focusNone = HeroSkillTreePanelMvvm.ResolveFocusNodeId(ids, flatStates, null);
            if (CountOversized(ids, focusNone) > 1)
                failures.Add("[singleton] a fully locked board produced more than one oversized plate");

            notes.Add("multi-track fixture: " + ids.Count + " seats / " + nextSeats + " per-track Next -> " +
                      "oversized idle=" + oversizedIdle + ", selected=" + oversizedSel +
                      " (law: <=1), focusIdle=\"" + focusIdle + "\"");
        }

        /// <summary>How many plates the view would build ABOVE NodeSizePx for this focus id.
        /// Mirrors BuildGraphNode's only size decision: NodePlateSizePx(id == focusId).</summary>
        private static int CountOversized(List<string> ids, string focusId)
        {
            int n = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                bool focus = !string.IsNullOrEmpty(focusId) &&
                             string.Equals(ids[i], focusId, StringComparison.Ordinal);
                if (HeroSkillTreePanelMvvm.NodePlateSizePx(focus) > HeroSkillTreePanelMvvm.NodeSizePx) n++;
            }
            return n;
        }

        // =====================================================================
        //  CASE 2 - the per-track NEXT cue: present, NOT size-carried, greyscale-safe
        // =====================================================================
        private static void Case2_NextCue(List<string> failures, List<string> notes)
        {
            // Size depends on FOCUS and nothing else. If a state could buy size, the
            // per-track signal would be back in the scarce channel by another door.
            float normal = HeroSkillTreePanelMvvm.NodePlateSizePx(false);
            float focused = HeroSkillTreePanelMvvm.NodePlateSizePx(true);
            if (Mathf.Abs(normal - HeroSkillTreePanelMvvm.NodeSizePx) > 0.01f)
                failures.Add("[next-cue] NodePlateSizePx(false)=" + normal + " is not NodeSizePx=" +
                             HeroSkillTreePanelMvvm.NodeSizePx + " - a non-focus plate is being resized, " +
                             "so some state is buying itself the size channel again");
            if (focused <= normal)
                failures.Add("[next-cue] the FOCUS plate (" + focused + ") is not larger than a normal plate (" +
                             normal + ") - the selection has lost its only size-carried read");

            // Greyscale law: the cue is a SHAPE on a disc, and the two must separate with
            // hue stripped. Rec.709 luma is the greyscale a colourblind pass actually sees.
            float inkLuma = Luma(HeroSkillTreePanelMvvm.NextMarkerInk);
            float discLuma = Luma(HeroSkillTreePanelMvvm.NextMarkerDisc);
            float gap = Mathf.Abs(inkLuma - discLuma);
            if (gap < MinGreyscaleLumaGap)
                failures.Add("[next-cue] the NEXT badge separates by only " + gap.ToString("F2") +
                             " Rec.709 luma (needs >= " + MinGreyscaleLumaGap.ToString("F2") + ") - it would " +
                             "read as a HUE-ONLY cue and vanish in a greyscale pass. The owner is red/green " +
                             "colourblind: every state must be separable with colour stripped");
            if (HeroSkillTreePanelMvvm.NextMarkerInk.a < 0.5f)
                failures.Add("[next-cue] the NEXT badge ink is under 50% alpha - a cue that faint is a tint, " +
                             "not a shape");

            notes.Add("next cue: plate stays " + normal + "px (focus " + focused + "px), " +
                      "badge luma gap " + gap.ToString("F2") + " (ink " + inkLuma.ToString("F2") +
                      " / disc " + discLuma.ToString("F2") + ")");
        }

        /// <summary>Rec.709 relative luminance - what a greyscale pass of the capture shows.</summary>
        private static float Luma(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        // =====================================================================
        //  CASE 3 - the lattice solver holds the pitch law (WO-1021 sec 2.1b)
        // =====================================================================
        private static void Case3_SolverPitch(List<string> failures, List<string> notes)
        {
            float pitch = HeroSkillTreePanelMvvm.MinNodePitchPx;
            float half = HeroSkillTreePanelMvvm.NodeFocusPx * 0.5f;
            float pad = HeroSkillTreePanelMvvm.GraphPadPx;

            // Seven rows (4 class tiers + 3 shared, the WO-1105 shared-pool shape) with
            // clustered and duplicate authored values - the worst case for a solver.
            var norms = new List<float>();
            for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                    norms.AddRange(new[] { 0.08f + c * 0.21f, 0.05f + r * 0.15f });
            // Two nodes authored at the SAME point (the "plates land on each other" case).
            norms.AddRange(new[] { 0.08f, 0.05f });

            float boxW = RefBodyWidthPx - pad * 2f;
            float boxH = RefBodyHeightPx - pad * 2f - HeroSkillTreePanelMvvm.RankBandPx;
            CheckSolve(failures, notes, "reference well", norms.ToArray(), boxW, boxH, pitch, half);

            // A degenerate well (rect not laid out yet / a tiny aspect) must still be legal.
            CheckSolve(failures, notes, "degenerate well", norms.ToArray(), 0f, 0f, pitch, half);
        }

        private static void CheckSolve(List<string> failures, List<string> notes, string label,
                                       float[] norms, float boxW, float boxH, float pitch, float half)
        {
            float[] px = HeroSkillTreePanelMvvm.SolveGraphLatticePx(norms, boxW, boxH);
            int n = px.Length / 2;
            if (n != norms.Length / 2)
            {
                failures.Add("[solver] " + label + ": solver returned " + n + " centres for " +
                             (norms.Length / 2) + " nodes");
                return;
            }

            float worst = float.MaxValue;
            string worstPair = "-";
            float minX = float.MaxValue, minY = float.MaxValue, maxX = 0f, maxY = 0f;
            for (int i = 0; i < n; i++)
            {
                float xi = px[i * 2], yi = px[i * 2 + 1];
                if (xi < minX) minX = xi;
                if (yi < minY) minY = yi;
                if (xi > maxX) maxX = xi;
                if (yi > maxY) maxY = yi;
                for (int j = i + 1; j < n; j++)
                {
                    float dx = Mathf.Abs(xi - px[j * 2]);
                    float dy = Mathf.Abs(yi - px[j * 2 + 1]);
                    float sep = Mathf.Max(dx, dy);   // Chebyshev: an AABB pair clears once EITHER axis does
                    if (sep < worst) { worst = sep; worstPair = i + "/" + j; }
                }
            }

            if (n > 1 && worst < pitch - 0.5f)
                failures.Add("[solver] " + label + ": the tightest resolved pair (" + worstPair + ") clears only " +
                             worst.ToString("F1") + " px against the pitch law " + pitch.ToString("F1") +
                             " px - plates would touch and a corner cost pip would land on the NEIGHBOURING " +
                             "plate (the misread measured on device 2026-08-16)");

            if (minX < half - 0.5f || minY < half - 0.5f)
                failures.Add("[solver] " + label + ": a resolved plate sits " + Mathf.Min(minX, minY).ToString("F1") +
                             " px from the content origin, inside the FOCUS half-plate inset " +
                             half.ToString("F1") + " px - the first row/column is clipped mid-plate at the mask " +
                             "edge the moment that plate is the oversized one (the s2.png top-edge clip)");

            notes.Add(label + ": " + n + " nodes, tightest resolved pitch " + worst.ToString("F0") +
                      "px (law " + pitch.ToString("F0") + "), extent " + (maxX - minX).ToString("F0") + "x" +
                      (maxY - minY).ToString("F0") + " px in a " + boxW.ToString("F0") + "x" +
                      boxH.ToString("F0") + " box");
        }

        // =====================================================================
        //  CASE 4 - source laws (comments AND string literals stripped first)
        // =====================================================================
        private static void Case4_SourceLaws(List<string> failures, List<string> notes)
        {
            string src;
            try
            {
                if (!File.Exists(ViewSrc))
                {
                    failures.Add("[source] source not found: " + ViewSrc);
                    return;
                }
                src = File.ReadAllText(ViewSrc);
            }
            catch (Exception ex)
            {
                failures.Add("[source] could not read " + ViewSrc + ": " + ex.Message);
                return;
            }

            if (src.IndexOf('\0') >= 0)
                failures.Add("[source] HeroSkillTreePanelMvvm.cs contains an embedded NUL byte " +
                             "(mount-garble, CLAUDE.md Sec.0) - the compile gate rejects this");

            string code = StripCommentsAndStrings(src);

            // THE defect, verbatim: a focus/size decision that reads the per-track state.
            if (Regex.IsMatch(code, @"\bfocus\b\s*=\s*[^;]*SkillNodeState\s*\.\s*Next"))
                failures.Add("[source] a `focus` expression reads SkillNodeState.Next again - Next is a " +
                             "PER-TRACK signal (HeroSkillTreeVM.ResolveStates resets nextTaken per track), so " +
                             "this grows one oversized gold plate PER TRACK. Focus is a BOARD-LEVEL singleton: " +
                             "resolve it once with ResolveFocusNodeId");

            if (Regex.IsMatch(code, @"\bsize\b\s*=\s*[^;]*SkillNodeState\s*\."))
                failures.Add("[source] a plate SIZE expression keys off a node STATE - size is the scarce " +
                             "channel and only the board-level selection may spend it");

            Law(failures, code, "ResolveFocusNodeId",
                "the board-level focus resolver is gone - the view is deciding focus per seat again");
            Law(failures, code, "NodePlateSizePx",
                "the single plate-size decision is gone - size can drift back onto a state");
            Law(failures, code, "BuildNextTrackMarker",
                "the per-track NEXT badge is gone - with focus now a singleton, removing the badge leaves " +
                "the frontier step of every OTHER track with no cue at all");
            Law(failures, code, "SolveGraphLatticePx",
                "the one-place lattice solver is gone - position solving has been split across methods " +
                "again, which is what let authored and auto-placed plates overlap");
            Law(failures, code, "MinNodePitchPx",
                "the minimum-pitch law is gone - nothing stops two plates touching");

            // The NEXT badge must not touch the plate-size constants or grow outside the plate.
            string body = MethodBody(code, "BuildNextTrackMarker");
            if (body == null)
                failures.Add("[source] could not isolate BuildNextTrackMarker's body - the badge builder was " +
                             "reshaped; re-point this law rather than deleting it");
            else
            {
                foreach (string banned in new[] { "NodeFocusPx", "NodeSizePx", "BuildOuterRing" })
                    if (body.IndexOf(banned, StringComparison.Ordinal) >= 0)
                        failures.Add("[source] BuildNextTrackMarker references '" + banned + "' - the per-track " +
                                     "cue must be SHAPE and POSITION carried at normal size; it may not resize " +
                                     "the plate or grow a ring outside its bounds");
                if (body.IndexOf("ElarionUiKit.Label", StringComparison.Ordinal) < 0)
                    failures.Add("[source] BuildNextTrackMarker no longer draws its explicit NEXT word badge - " +
                                 "the frontier cue must be text/shape carried, never tint-only or arrow-only");
            }

            notes.Add("source laws checked on " + ViewSrc + " (comments + string literals stripped)");
        }

        private static void Law(List<string> failures, string code, string token, string why)
        {
            if (code.IndexOf(token, StringComparison.Ordinal) < 0)
                failures.Add("[source] '" + token + "' is gone from HeroSkillTreePanelMvvm - " + why);
        }

        /// <summary>The brace-balanced body of a named method in already-stripped source.</summary>
        private static string MethodBody(string code, string methodName)
        {
            var m = Regex.Match(code, @"\b" + Regex.Escape(methodName) + @"\s*\([^)]*\)\s*\{");
            if (!m.Success) return null;
            int start = m.Index + m.Length;
            int depth = 1;
            for (int i = start; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}')
                {
                    depth--;
                    if (depth == 0) return code.Substring(start, i - start);
                }
            }
            return null;
        }

        /// <summary>Blank out // and /* */ comments AND every string/char literal. A lesson
        /// written in prose - or a log line that QUOTES the retired shape - must never satisfy
        /// or trip a source law. Three oracles on 2026-08-15 passed by matching their own text;
        /// stripping comments alone is not enough, because FlowTrace messages quote the code.</summary>
        private static string StripCommentsAndStrings(string src)
        {
            var sb = new StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];

                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    sb.Append(' ');
                    continue;
                }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = Mathf.Min(src.Length, i + 2);
                    sb.Append(' ');
                    continue;
                }
                if (c == '@' && i + 1 < src.Length && src[i + 1] == '"')
                {
                    i += 2;
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++;
                    while (i < src.Length && src[i] != quote)
                    {
                        if (src[i] == '\\') i++;
                        i++;
                    }
                    i++;
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
