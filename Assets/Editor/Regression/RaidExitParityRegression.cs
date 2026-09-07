// =============================================================================
// RaidExitParityRegression [raid-exit-parity]  --  markers RAID_EXIT_PARITY_OK / _FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Source-lint (edit mode, no PlayMode).
// Registered in DataRegression.RunAll.  NEVER throws.
//
// Locks the three WO-1110 rulings so they cannot silently rot back:
//
//   PIN 1  EXIT PARITY (WO-1110 sec.3).  A raid has three non-victory exits and they
//          must PAY THE SAME. Hero death used to reconcile the army and stop - it
//          never called RaidScoring.Finalize/LootFor - so a player who razed two
//          thirds of a base and then FELL got LESS than one who razed the same and
//          tapped Retreat. That is the inverse of the perverse incentive the
//          retreat-loot block was written to remove. Both exits now funnel through
//          the ONE authority, RaidDeployController.SettlePartialLoot, and this pin
//          asserts (a) the authority exists, (b) retreat calls it and does not fork
//          its own Finalize, (c) hero death calls it, BEFORE the army reconcile, in
//          the same order retreat uses.
//
//   PIN 2  THE SOFTLOCK ORDER (WO-1110 sec.1).  Start() must bind the clock-expiry
//          subscriber (BindScoringRoutine) BEFORE it builds the HUD, and must build
//          the HUD inside a Guard.Try. With the old order a throw inside BuildHud
//          skipped the StartCoroutine line entirely: no tray, no Retreat button AND
//          no 180s timeout rescue - the raid's only exitless state. The ORDER is the
//          fix; the Guard is the seatbelt. Both are asserted, because either one
//          alone still leaves a hole.
//
//   PIN 3  NO SILENT CATCH (WO-1110 sec.2 / CLAUDE.md sec.12).  The four named sites
//          must each carry a FlowTrace line, and no raid runtime file may contain a
//          bare `catch { }` again. The expensive one is RaidScoring's reward
//          multiplier: a catalog miss silently paid x1 where the card promised x2.2,
//          a 55% pay cut with no trace anywhere.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RaidExitParityRegression
    {
        // Relative to Application.dataPath.
        private const string CtrlRel   = "_Modules/Village/Troops/RaidDeployController.cs";
        private const string ScoreRel  = "_Modules/Village/Troops/RaidScoring.cs";
        private const string HeroRel   = "_Modules/Village/Hero/HeroHealth.cs";
        private const string VmRel     = "_Modules/Village/Hero/RaidDeployVM.cs";
        private const string SelRel    = "_Modules/Village/Hero/RaidSelectionScreen.cs";
        private const string ScreenRel = "_Modules/Village/Hero/RaidDeployScreen.cs";

        // Declared as a balanced PAIR on one line on purpose (RegressionMarkerRegression's
        // precedent): a lone brace char literal trips the CLAUDE.md rule-1 brace counter.
        private const char OpenBrace = '{', CloseBrace = '}';

        /// <summary>A catch block that swallows without logging - CLAUDE.md sec.12 forbids it.</summary>
        private static readonly Regex BareCatch = new Regex(
            @"catch\s*(?:\([^)]*\))?\s*\{\s*\}", RegexOptions.Compiled);

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "raid-exit-parity: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>Standalone batch entry.</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("RAID_EXIT_PARITY_OK - " + reason);
            else Debug.LogError("RAID_EXIT_PARITY_FAIL - " + reason);
        }

        private static bool RunCore(out string reason)
        {
            var fails = new List<string>();

            // Comments are stripped from every file BEFORE any match: this suite's own
            // explanatory comments in those files name the very symbols it looks for
            // (SettlePartialLoot, Finalize, ReconcileRaidEnd), and a comment mention
            // would read as a call - the ordering pin especially would go blind.
            string ctrl   = ReadCode(CtrlRel,   fails);
            string score  = ReadCode(ScoreRel,  fails);
            string hero   = ReadCode(HeroRel,   fails);
            string vm     = ReadCode(VmRel,     fails);
            string sel    = ReadCode(SelRel,    fails);
            string screen = ReadCode(ScreenRel, fails);

            // -----------------------------------------------------------------
            //  PIN 1 - exit parity: ONE settlement authority, both exits use it
            // -----------------------------------------------------------------
            if (!ctrl.Contains("public void SettlePartialLoot("))
                fails.Add("RaidDeployController no longer exposes `public void SettlePartialLoot(` - " +
                          "the single non-victory loot authority is gone, so the retreat and death " +
                          "exits have nothing to share and will drift apart again (WO-1110 sec.3)");

            string settleBody = Body(ctrl, @"void\s+SettlePartialLoot\s*\([^)]*\)");
            if (string.IsNullOrEmpty(settleBody))
                fails.Add("could not locate SettlePartialLoot's body in RaidDeployController");
            else
            {
                if (!settleBody.Contains("Finalize(false)"))
                    fails.Add("SettlePartialLoot does not call RaidScoring.Finalize(false) - nothing settles the score");
                if (!settleBody.Contains("LootFor("))
                    fails.Add("SettlePartialLoot does not call LootFor(...) - no loot is computed from the settled result");
                if (!settleBody.Contains("GrantRetreatLoot("))
                    fails.Add("SettlePartialLoot does not call GrantRetreatLoot(...) - the loot is computed and then never paid");
            }

            // WO-1561 widened the signature to DoRetreat(string reason) so the timeout exit can
            // name itself on the shared result screen. The pattern follows it rather than
            // matching an empty-bodied forwarder - a lint that locks onto the wrong overload
            // passes while every assertion below it silently stops applying.
            string retreatBody = Body(ctrl, @"void\s+DoRetreat\s*\(");
            if (string.IsNullOrEmpty(retreatBody))
                fails.Add("could not locate DoRetreat's body in RaidDeployController");
            else
            {
                if (!retreatBody.Contains("SettlePartialLoot("))
                    fails.Add("DoRetreat does not route through SettlePartialLoot - the retreat/timeout exit " +
                              "has forked away from the shared settlement (WO-1110 sec.3)");
                if (retreatBody.Contains("Finalize("))
                    fails.Add("DoRetreat calls Finalize(...) directly again - a second settlement path means " +
                              "retreat and death can pay differently, which is the exact bug WO-1110 closed");

                // -------------------------------------------------------------
                //  WO-1561 - A NON-VICTORY EXIT MAY NOT ROUTE TO TOWN IN SILENCE
                // -------------------------------------------------------------
                // THE DEFECT, MEASURED ON THE PRE-CHANGE TREE: DoRetreat ended
                // `SetStatus("Retreating to the castle..."); SceneRouter.GoCastle();`
                // and grep -c "EndStateVM\.|EndStateView.Show" on the whole file
                // returned 1 - a COMMENT. The clock-expiry exit funnels here too, and
                // nothing in town picked the outcome up either (every reader of
                // RaidResult is raid-scene-side), so the result was computed, BANKED
                // and discarded unread. A WIN got the full treatment; the exit a new
                // player is most likely to finish got no screen at all.
                //
                // The pin is a PAIR, because either half alone still leaves the hole:
                // the exit must SHOW a result, and it must not ALSO leave by itself.
                if (!retreatBody.Contains("ShowNonVictoryResult(") &&
                    !retreatBody.Contains("EndStateView.Show"))
                    fails.Add("DoRetreat routes home without showing an end state - the retreat/timeout exit " +
                              "settles the score, pays the loot, reconciles the army and then tells the player " +
                              "NOTHING: not razed %, not stars, not the spoils it just banked, not which troops " +
                              "came home wounded (WO-1561, P0)");
                if (Regex.IsMatch(retreatBody, @"SceneRouter\s*\.\s*GoCastle"))
                    fails.Add("DoRetreat calls SceneRouter.GoCastle directly again - the route home belongs to the " +
                              "result screen's primary action (and its re-armable guard), or the screen is shown " +
                              "and instantly abandoned by a scene load underneath it (WO-1561 / WO-1543)");
            }

            // The result screen exists and reports what was BANKED, not what was awarded. WO-1461
            // records the live case: the deploy card quoted ~1,800 wood and 25 arrived, because
            // the bank was full. A screen fed the REQUESTED loot would restate that lie.
            string showBody = Body(ctrl, @"void\s+ShowNonVictoryResult\s*\(");
            if (!string.IsNullOrEmpty(showBody))
            {
                if (!showBody.Contains("EndStateView.Show"))
                    fails.Add("RaidDeployController.ShowNonVictoryResult never calls EndStateView.Show - the " +
                              "non-victory result is composed and then dropped");
                if (!showBody.Contains("_retreatCredited"))
                    fails.Add("RaidDeployController.ShowNonVictoryResult does not feed the MEASURED credit " +
                              "(_retreatCredited) to the screen. It must report what the wallet actually took, " +
                              "never the loot that was awarded - at a capped town bank those differ, and the " +
                              "screen is what the player believes (WO-978 / WO-1461)");
            }

            // Hero death: it must settle loot, and settle it BEFORE the army reconcile,
            // matching DoRetreat's order (Finalize samples destruction/survival off the
            // live field, so reconciling first would score a torn-down raid).
            int iSettle = hero.IndexOf("SettlePartialLoot(", StringComparison.Ordinal);
            int iRecon  = hero.IndexOf("ReconcileRaidEnd(0)", StringComparison.Ordinal);
            if (iSettle < 0)
                fails.Add("HeroHealth's enemy-owned death branch does not call SettlePartialLoot - dying " +
                          "forfeits razing credit that retreating pays, punishing the more committed play " +
                          "(WO-1110 sec.3; owner default = death pays what retreat pays)");
            if (iRecon < 0)
                fails.Add("HeroHealth's enemy-owned death branch no longer calls ReconcileRaidEnd(0) - the " +
                          "death exit stopped settling the army");
            if (iSettle >= 0 && iRecon >= 0 && iSettle > iRecon)
                fails.Add("HeroHealth settles the army BEFORE the loot (SettlePartialLoot at " + iSettle +
                          ", ReconcileRaidEnd at " + iRecon + ") - the opposite of DoRetreat's order. " +
                          "Finalize samples destruction/survival off the live field, so the two exits " +
                          "would score differently even while calling the same method");

            // -----------------------------------------------------------------
            //  PIN 2 - the softlock: subscribe the clock BEFORE building the HUD
            // -----------------------------------------------------------------
            string startBody = Body(ctrl, @"void\s+Start\s*\(\s*\)");
            if (string.IsNullOrEmpty(startBody))
                fails.Add("could not locate RaidDeployController.Start's body");
            else
            {
                int iBind  = startBody.IndexOf("BindScoringRoutine", StringComparison.Ordinal);
                int iBuild = startBody.IndexOf("BuildHud", StringComparison.Ordinal);
                if (iBind < 0)
                    fails.Add("RaidDeployController.Start no longer starts BindScoringRoutine - the 180s " +
                              "OnTimeExpired subscriber is the raid's last-resort exit");
                if (iBuild < 0)
                    fails.Add("RaidDeployController.Start no longer builds the HUD");
                if (iBind >= 0 && iBuild >= 0 && iBind > iBuild)
                    fails.Add("RaidDeployController.Start builds the HUD BEFORE binding the raid clock. " +
                              "A throw inside BuildHud then skips the subscribe entirely: no tray, no " +
                              "Retreat button and no timeout rescue - the raid's ONLY exitless state " +
                              "(WO-1110 sec.1). Subscribe first, present second");
                if (iBuild >= 0 && !Regex.IsMatch(startBody, @"Guard\.Try\s*\([^;]*BuildHud"))
                    fails.Add("RaidDeployController.Start calls BuildHud outside a Guard.Try - every other " +
                              "risky op in that file is wrapped, and an unguarded presentation throw is what " +
                              "produced the softlock (CLAUDE.md sec.12)");
            }

            // The fault-injection hook the acceptance proof depends on.
            if (!ctrl.Contains("DebugForceBuildHudThrow"))
                fails.Add("the DebugForceBuildHudThrow injection hook is gone - the 'a HUD failure still " +
                          "leaves an exit' acceptance can no longer be PROVEN by injection, only argued");

            // -----------------------------------------------------------------
            //  PIN 3 - no silent catches in the raid runtime
            // -----------------------------------------------------------------
            CheckNoBareCatch(CtrlRel, ctrl, fails);
            CheckNoBareCatch(ScoreRel, score, fails);
            CheckNoBareCatch(VmRel, vm, fails);
            CheckNoBareCatch(SelRel, sel, fails);
            CheckNoBareCatch(ScreenRel, screen, fails);

            CheckTraced(score, @"float\s+ResolveRewardMultiplier\s*\(\s*\)",
                "RaidScoring.ResolveRewardMultiplier",
                "a catalog miss here silently pays x1 where the card promised x2.2 - a 55% pay cut " +
                "the player cannot see and no trace records", fails);
            CheckTraced(vm, @"string\s+ComputeArmyCapText\s*\(\s*\)",
                "RaidDeployVM.ComputeArmyCapText",
                "the army readout silently falls back to 'Army: -' with nothing saying why", fails);
            CheckTraced(sel, @"void\s+OnCardTapped\s*\([^)]*\)",
                "RaidSelectionScreen.OnCardTapped",
                "an unresolved card tap is a DEAD TAP - it reads to the player as a frozen game", fails);
            CheckTraced(screen, @"void\s+Open\s*\(\s*SceneConfigDef[^)]*\)",
                "RaidDeployScreen.Open",
                "a null def means the deploy screen never opens, with no player feedback at all", fails);

            if (fails.Count == 0)
            {
                Debug.Log("RAID_EXIT_PARITY_OK");
                reason = "RAID EXIT PARITY OK -- one SettlePartialLoot authority shared by the retreat and " +
                         "hero-death exits (loot settled before the army reconcile on both); Start binds the " +
                         "raid clock before a Guard.Try'd BuildHud (no exitless state); 4 named catches traced " +
                         "and 0 bare catches across 5 raid runtime files";
                return true;
            }

            reason = "raid-exit-parity (" + fails.Count + "): " + string.Join(" | ", fails.ToArray());
            Debug.LogError("RAID_EXIT_PARITY_FAIL: " + reason);
            return false;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void CheckNoBareCatch(string rel, string code, List<string> fails)
        {
            if (string.IsNullOrEmpty(code)) return;   // the missing-file failure was already recorded
            if (BareCatch.IsMatch(code))
                fails.Add(rel + " contains a bare empty catch block again - CLAUDE.md sec.12 forbids a " +
                          "catch that swallows without logging (WO-1110 sec.2)");
        }

        private static void CheckTraced(string code, string signature, string label, string why, List<string> fails)
        {
            if (string.IsNullOrEmpty(code)) return;
            string body = Body(code, signature);
            if (string.IsNullOrEmpty(body))
            {
                fails.Add("could not locate " + label + "'s body - its silent-failure pin cannot be verified");
                return;
            }
            if (body.IndexOf("FlowTrace.", StringComparison.Ordinal) < 0)
                fails.Add(label + " emits no FlowTrace line on its failure path: " + why + " (WO-1110 sec.2)");
        }

        /// <summary>
        /// The brace-matched body of the first method whose signature matches, from the
        /// signature's opening brace to its balanced close. Brace-matched rather than
        /// indentation-matched so a nested block cannot end the extraction early.
        /// </summary>
        private static string Body(string code, string signaturePattern)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var m = Regex.Match(code, signaturePattern);
            if (!m.Success) return string.Empty;
            int open = code.IndexOf(OpenBrace, m.Index + m.Length);
            if (open < 0) return string.Empty;
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == OpenBrace) depth++;
                else if (code[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return code.Substring(open, i - open + 1);
                }
            }
            return string.Empty;
        }

        /// <summary>Reads a file under Assets/ with // comments stripped; records a failure if missing.</summary>
        private static string ReadCode(string rel, List<string> fails)
        {
            string path = Path.Combine(Application.dataPath, rel);
            if (!File.Exists(path))
            {
                fails.Add("raid runtime file missing: " + rel);
                return string.Empty;
            }
            try { return StripLineComments(File.ReadAllText(path)); }
            catch (IOException ex)
            {
                fails.Add("could not read " + rel + ": " + ex.Message);
                return string.Empty;
            }
        }

        /// <summary>Strips // line comments (string-literal aware), preserving line structure.</summary>
        private static string StripLineComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            bool inStr = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '/' && n == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    if (i < src.Length) sb.Append('\n');
                    continue;
                }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
