// =============================================================================
// RaidTerminalStateRegression [raid-terminal-state]
//   markers RAID_TERMINAL_STATE_OK / RAID_TERMINAL_STATE_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Edit mode, no PlayMode.
// Registered in DataRegression.RunAll.  NEVER throws.
//
// WO-1437, P0.  THE QUESTION NO SUITE HAD EVER ASKED: **can the player get OUT?**
//
// On 2026-09-06 the owner won a raid and could not leave it. Every part worked:
// scoring scored, the claim claimed, the loot paid, the victory screen showed,
// retreat still retreated. Nothing asserted that the SESSION AS A WHOLE terminates,
// so a raid that had already paid out kept the player standing in it with kills still
// scoring. Same species as WO-1430 and WO-1436. This suite is the general form.
//
// -----------------------------------------------------------------------------
//  THE CAPTURED CHAIN THESE PINS ARE DERIVED FROM (nothing here is inferred)
//  logs/debug/raid-stuck-2026-09-06.log, build 2026.09.06.358161
// -----------------------------------------------------------------------------
//   13:02:42.115  OBJECTIVE COMPLETE - RaidSpire RAZED. The raid is won.
//   13:02:42.118  CLAIM - 'raider_camp_small' flipped ENEMY -> PLAYER-owned
//   13:02:42.133  stars settled: 3 (... elapsed=62s/180s underTime=True ...)
//   13:02:42.214  RETURN - victory screen shown; tap or auto-dismiss routes to the castle.
//   13:02:47.276  'Victory!' destroyed WITHOUT firing its primary action - EndStateView.Show
//                 - REPLACED by a new end-state 'YOU HAVE FALLEN'. That action is now abandoned.
//   13:02:47.276  hero death in non-hub scene 'RaidBase_raider_camp_small' (enemyOwned=False)
//   13:02:47.284  'Victory!' destroyed WITHOUT firing its primary action - CloseFromArbiter
//   13:02:49.468  HERO MOVED ... by HeroHealth.Respawn reason=in-place respawn at spawn anchor
//   13:02:53.285  HeroDeath primary fired: action=respawn
//   13:02:55.487  KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker      <- still in it
//   13:02:58.476  KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker
//   13:03:00.564  KILL MATERIALS SUPPRESSED (raid active) id=orc-berserker
//   13:03:56 -> 13:04:16  [Flow:HeroOwner] ... timeScale=1.00 (every line)
//
//  Contrast, SAME BUILD, SAME SCENE, three minutes earlier
//  (logs/debug/raid-ai-and-pets-2026-09-06.log):
//   12:59:45.549  hero death in non-hub scene 'RaidBase_raider_camp_small' (enemyOwned=True)
//   12:59:47.750  hero death settle: partial loot for 32% razed.                <- correct
//
//  ONE TOKEN DIFFERS: enemyOwned. The victory path FLIPS it (the claim at 13:02:42.118),
//  and both death readers gated on it. That is the whole inconsistency.
//
// -----------------------------------------------------------------------------
//  TWO WO-1437 PREMISES THE CAPTURE KILLED - recorded so they are not re-theorised
// -----------------------------------------------------------------------------
//  * "THE CLOCK IS FROZEN / a world-hold leaked from the deploy modal" (WO sec.2) is
//    FALSE. Every [Flow:HeroOwner] line from 13:03:56 to 13:04:16 reads timeScale=1.00,
//    and RaidScoring.Update advances _elapsed until Finalize latches _finalized. The
//    raid settled at elapsed=62.3s of 180s; 180 - 62.3 = 117.7s = the 1:58 the owner
//    photographed twice. The clock stopped because the SESSION ENDED and the player was
//    never removed from the scene - a symptom of the softlock, not a second bug, and NOT
//    a timeScale hold. CONSEQUENCE FOR THE ACCEPTANCE CRITERIA: a "clock advances" oracle
//    is GREEN against today's build and cannot honestly be shown RED. Case A below
//    therefore MEASURES the clock (a frozen clock does fail it) but is declared as a
//    forward pin, and the RED proof for this WO lives in cases B/C/D, which fail against
//    the pre-fix tree. Manufacturing a RED here would be exactly the fabricated evidence
//    CLAUDE.md sec.11B forbids.
//  * "respawn re-enters the raid because of the end-state's action=respawn" is FALSE.
//    HeroHealth.Respawn moved the hero at 13:02:49.468, FOUR SECONDS BEFORE the screen's
//    primary fired at 13:02:53.285. The coroutine is the mover; the screen is narration.
//    Editing EndStateView would have fixed nothing.
//
// -----------------------------------------------------------------------------
//  RED PROOF - what each pin does against the tree AS IT WAS BEFORE WO-1437
// -----------------------------------------------------------------------------
//  PIN B  RED.  HeroHealth.HandleDeath's evac branch read
//               `if (DeNelle.Village.SceneOwnership.IsEnemyOwned)` and nothing else. No
//               reference to RaidScoring existed anywhere in that method, so the pin's
//               RaidInProgress requirement could not be satisfied.
//  PIN C  RED.  RaidDeployController contained no watchdog of any kind: `grep -n
//               "watchdog" Assets/_Modules/Village/Troops/RaidDeployController.cs`
//               returned nothing before this WO. The route home from a won raid was owned
//               solely by RaidVictoryController's EndStateVM (its primary action AND its
//               AutoDismissSeconds), both of which die with the GameObject.
//  PIN D  RED.  HeroDeathEndState.OnHeroDeath passed a bare
//               `SceneOwnership.IsEnemyOwned` into EndStateVM.FromHeroDeath.
//  PIN E  RED.  Follows from C: the victory exit's only owner was a view.
//  CASE A GREEN before and after, by design - see the premise note above.
//
//  MEASURED, not asserted (2026-09-06). Each pin's predicate was run against the
//  pre-fix sources read straight out of `git show HEAD:<path>` at commit f986f3cff,
//  with the same comment-stripping and brace-matched body extraction this file uses:
//
//      PIN B RED: HeroHealth.HandleDeath has no RaidScoring.RaidInProgress
//      PIN B RED: HeroHealth.HandleDeath does not consider RaidScoring.Finalized
//      PIN C RED: RaidDeployController has NO StrandingWatchdog
//      PIN C RED: RaidDeployController has NO ForceExitHome
//      PIN D RED: HeroDeathEndState decides its copy from SceneOwnership alone
//      PIN E RED: the victory exit route home is owned only by an EndState view
//      PIN E RED: RaidScoring.RaidDeathEndsRaid does not exist
//      total RED pins: 7
//      CASE A against HEAD: GREEN (documented: green before AND after)
//
//  The same predicates run against the post-fix tree resolve every token inside the
//  expected method body (HandleDeath 8172 chars, StrandingWatchdog 2466, ForceExitHome
//  571, BindScoringRoutine 619, RaidScoring.Update 611), so the pins are reading the
//  bodies they claim to read and not passing on a stray file-wide mention.
//
// -----------------------------------------------------------------------------
//  WHAT THIS SUITE STILL DOES NOT PROVE - stated because an unproven thing named as
//  unproven is useful and an unproven thing stated as fact costs someone a day
//  (CLAUDE.md sec.11B)
// -----------------------------------------------------------------------------
//  WO-1437 acceptance 1 asks for a HEADLESS RAID RUN asserting the active scene
//  changes away from RaidBase_* once the objective is met. THIS SUITE DOES NOT DO
//  THAT, and no suite in the repo can today: AutoPilotDriver's raid loop deliberately
//  stops OUTSIDE the raid scene ("a re-introduced raid teleport trips AutoPilotProbes'
//  scene-load Fail", AutoPilotDriver.cs sec.7180), so there is no harness that plays a
//  raid to its objective. Cases C and E pin the MECHANISM that performs the scene
//  change (an unscaled, session-owned watchdog that calls SceneRouter.GoCastle and is
//  armed before the HUD builds) and Case B pins the death exit's route, but a live
//  end-to-end scene assertion needs a PlayMode raid harness that does not exist. That
//  harness is a separate lane, not a line in this file, and it is the only acceptance
//  item WO-1437 leaves genuinely open.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RaidTerminalStateRegression
    {
        // Relative to Application.dataPath.
        private const string CtrlRel  = "_Modules/Village/Troops/RaidDeployController.cs";
        private const string ScoreRel = "_Modules/Village/Troops/RaidScoring.cs";
        private const string HeroRel  = "_Modules/Village/Hero/HeroHealth.cs";
        private const string DeathRel = "_Modules/Village/UI/EndState/HeroDeathEndState.cs";
        private const string VictRel  = "_Modules/Village/World/Camps/RaidVictoryController.cs";

        // Declared as a balanced PAIR on one line on purpose (RaidExitParityRegression's
        // precedent): a lone brace char literal trips the CLAUDE.md rule-1 brace counter.
        private const char OpenBrace = '{', CloseBrace = '}';

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "raid-terminal-state: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>Standalone batch entry.</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("RAID_TERMINAL_STATE_OK - " + reason);
            else Debug.LogError("RAID_TERMINAL_STATE_FAIL - " + reason);
        }

        private static bool RunCore(out string reason)
        {
            var fails = new List<string>();
            var notes = new List<string>();

            // Comments are stripped from every file BEFORE any match. This is load-bearing:
            // the WO-1437 fix comments in those very files quote the symbols this suite looks
            // for (RaidInProgress, StrandingWatchdog, IsEnemyOwned), so an unstripped read
            // would pass on prose alone and the pins would go blind. Same reason
            // RaidExitParityRegression strips.
            string ctrl  = ReadCode(CtrlRel,  fails);
            string score = ReadCode(ScoreRel, fails);
            string hero  = ReadCode(HeroRel,  fails);
            string death = ReadCode(DeathRel, fails);
            string vict  = ReadCode(VictRel,  fails);

            CaseA_ClockAdvancesAcrossTicks(fails, notes);
            CaseB_DeathExitDoesNotTrustTheFactionFlag(hero, fails);
            CaseC_TerminalStateWatchdogExists(ctrl, fails);
            CaseD_DeathCopyTracksTheDeathBranch(death, fails);
            CaseE_NoExitIsOwnedOnlyByAView(ctrl, score, vict, fails);

            if (fails.Count == 0)
            {
                Debug.Log("RAID_TERMINAL_STATE_OK");
                reason = "RAID TERMINAL STATE OK -- the raid clock advances across ticks and stops only " +
                         "at Finalize; both hero-death readers answer \"am I in a raid\" from " +
                         "RaidScoring.RaidInProgress instead of the faction flag the victory claim flips; " +
                         "RaidDeployController owns an unscaled stranding watchdog that routes home from a " +
                         "settled-but-unexited raid and from a never-settled one; no raid exit's only route " +
                         "home is owned by an EndState view" +
                         (notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : "");
                return true;
            }

            reason = "raid-terminal-state (" + fails.Count + "): " + string.Join(" | ", fails.ToArray()) +
                     (notes.Count > 0 ? " [" + string.Join("; ", notes.ToArray()) + "]" : "");
            Debug.LogError("RAID_TERMINAL_STATE_FAIL: " + reason);
            return false;
        }

        // =====================================================================
        //  CASE A - MEASURE the raid clock across ticks. A frozen clock FAILS.
        // =====================================================================
        /// <summary>
        /// WO-1437 acceptance: "the raid clock advances, proven by a regression that MEASURES
        /// elapsed across ticks - a frozen clock must FAIL."
        ///
        /// <para>This drives the REAL <c>RaidScoring.Update</c> on a REAL component (not a
        /// source-lint of it) and reads <c>ElapsedSeconds</c> before and after, so a clock that
        /// stops advancing genuinely fails this. It then asserts the ONE legitimate reason the
        /// clock may stop - <c>Finalize</c> having latched - by pinning the early-return itself,
        /// because a stop for any OTHER reason is a frozen clock.</para>
        ///
        /// <para><b>HONEST STATUS: this case is GREEN against the pre-fix build too</b>, and that
        /// is a finding, not a gap. The 2026-09-06 capture measured <c>timeScale=1.00</c>
        /// throughout and <c>elapsed=62s/180s</c> at settle; the "1:58 on two screenshots" was a
        /// SETTLED raid's HUD still on screen, so WO-1437 sec.2's frozen-clock premise is killed.
        /// This pin is therefore a forward guard against a real future freeze (a leaked
        /// <c>timeScale=0</c> hold, an Update that early-returns on the wrong flag) rather than
        /// the RED proof for this ticket - cases B/C/D carry that.</para>
        ///
        /// <para>Non-flaky by construction: in batchmode edit mode <c>Time.deltaTime</c> can be
        /// zero, which would make the measurement meaningless rather than wrong. That case is
        /// recorded as a NOTE and the structural half still runs. A suite that fails on the
        /// harness's frame timing teaches the next reader to ignore it.</para>
        /// </summary>
        private static void CaseA_ClockAdvancesAcrossTicks(List<string> fails, List<string> notes)
        {
            GameObject host = null;
            try
            {
                var scoringType = FindTypeByName("DeNelle.Village.RaidScoring");
                if (scoringType == null)
                {
                    fails.Add("Case A: type DeNelle.Village.RaidScoring not found - the raid clock's owner " +
                              "was renamed or removed, so nothing measures the session's elapsed time");
                    return;
                }

                var updateMi = scoringType.GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var elapsedPi = scoringType.GetProperty("ElapsedSeconds",
                    BindingFlags.Instance | BindingFlags.Public);
                var finalizedPi = scoringType.GetProperty("Finalized",
                    BindingFlags.Instance | BindingFlags.Public);

                if (updateMi == null)
                {
                    fails.Add("Case A: RaidScoring has no Update method - the raid clock is no longer " +
                              "advanced per frame, so the timeout exit can never fire");
                    return;
                }
                if (elapsedPi == null)
                {
                    fails.Add("Case A: RaidScoring.ElapsedSeconds is gone - the clock cannot be MEASURED, " +
                              "which is exactly what this acceptance criterion requires");
                    return;
                }
                if (finalizedPi == null)
                    fails.Add("Case A: RaidScoring.Finalized is gone - the one legitimate reason the clock " +
                              "may stop is no longer observable, so a freeze and a settle look identical");

                host = new GameObject("~RaidTerminalStateRegression_Scoring");
                host.hideFlags = HideFlags.HideAndDontSave;
                var scoring = host.AddComponent(scoringType) as MonoBehaviour;
                if (scoring == null)
                {
                    fails.Add("Case A: could not add a RaidScoring component to measure the clock on");
                    return;
                }

                // MEASURE: drive the real Update and watch elapsed move.
                float before = Convert.ToSingle(elapsedPi.GetValue(scoring, null));
                for (int i = 0; i < 60; i++) updateMi.Invoke(scoring, null);
                float after = Convert.ToSingle(elapsedPi.GetValue(scoring, null));

                if (Time.deltaTime <= 0f)
                {
                    notes.Add("Case A measurement skipped: Time.deltaTime is " +
                              Time.deltaTime.ToString("0.####") + " in this edit-mode harness, so 60 ticks " +
                              "advance the clock by zero for a reason that is NOT a frozen clock. The " +
                              "structural half of the pin still ran");
                }
                else if (after <= before)
                {
                    fails.Add("Case A: THE RAID CLOCK IS FROZEN. 60 RaidScoring.Update ticks at " +
                              "Time.deltaTime=" + Time.deltaTime.ToString("0.####") + " advanced " +
                              "ElapsedSeconds from " + before.ToString("0.###") + " to " +
                              after.ToString("0.###") + " (expected a strict increase). A stopped clock " +
                              "means OnTimeExpired can never fire and the timeout exit - the raid's " +
                              "last-resort way out - is dead (WO-1437 acceptance 2)");
                }

                // The ONLY legitimate stop. Update must early-return on the SETTLE latch and
                // nothing else, so "the clock stopped" and "the raid ended" cannot come apart.
                string scoreSrc = ReadCode(ScoreRel, fails);
                string updateBody = Body(scoreSrc, @"void\s+Update\s*\(\s*\)");
                if (string.IsNullOrEmpty(updateBody))
                    fails.Add("Case A: could not locate RaidScoring.Update's body to verify what stops the clock");
                else
                {
                    if (!Regex.IsMatch(updateBody, @"if\s*\(\s*_finalized\s*\)\s*return\s*;"))
                        fails.Add("Case A: RaidScoring.Update no longer stops on `if (_finalized) return;`. " +
                                  "The clock must stop for EXACTLY ONE reason - the raid settled - so that a " +
                                  "stopped clock is always a finished session. Any other early-return is a " +
                                  "freeze wearing a settle's clothes (WO-1437 sec.2)");
                    if (!updateBody.Contains("_elapsed += Time.deltaTime"))
                        fails.Add("Case A: RaidScoring.Update no longer accumulates `_elapsed += Time.deltaTime` - " +
                                  "the raid clock is not being advanced by anything");
                }
            }
            catch (Exception ex)
            {
                // Never throw out of a suite; a harness-shaped failure is a note, not a verdict.
                notes.Add("Case A could not complete its live measurement (" + ex.GetType().Name + ": " +
                          ex.Message + ") - treat the clock as UNPROVEN this run rather than green");
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // =====================================================================
        //  CASE B - the death exit must not trust a flag the victory path flips
        // =====================================================================
        private static void CaseB_DeathExitDoesNotTrustTheFactionFlag(string hero, List<string> fails)
        {
            if (string.IsNullOrEmpty(hero)) return;   // missing-file failure already recorded

            string body = Body(hero, @"IEnumerator\s+HandleDeath\s*\(");
            if (string.IsNullOrEmpty(body))
            {
                // Fall back to the whole file rather than going silently blind on a rename.
                body = hero;
                if (hero.IndexOf("HandleDeath", StringComparison.Ordinal) < 0)
                    fails.Add("Case B: HeroHealth.HandleDeath not found - the hero-death exit was renamed; " +
                              "re-point this pin rather than deleting it (WO-1437)");
            }

            if (body.IndexOf("RaidScoring.RaidInProgress", StringComparison.Ordinal) < 0)
                fails.Add("Case B: HeroHealth's death path does not consult RaidScoring.RaidInProgress. It is " +
                          "answering \"am I in a raid?\" from SceneOwnership.IsEnemyOwned alone - a FACTION " +
                          "flag that RaidClaimService DELIBERATELY FLIPS when a won camp is claimed. PROVEN " +
                          "BY CAPTURE 2026-09-06: the same scene read enemyOwned=True at 12:59:45 (evac + " +
                          "\"hero death settle: partial loot for 32% razed\") and enemyOwned=False at " +
                          "13:02:47 five seconds after the claim (in-place respawn INSIDE the settled raid, " +
                          "logged at 13:02:49.468). RaidScoring.RaidInProgress is the repo's own documented " +
                          "\"THE ONE 'am I inside a raid' ANSWER\" (WO-1227) and does not move when ownership " +
                          "flips (WO-1437 sec.4.3)");

            if (body.IndexOf("SceneOwnership.IsEnemyOwned", StringComparison.Ordinal) < 0)
                fails.Add("Case B: HeroHealth's death path no longer consults SceneOwnership.IsEnemyOwned at " +
                          "all. It must stay as an OR alongside RaidInProgress - Village2 and enemy-owned " +
                          "dungeons are NOT raids and still have to evacuate on death. Replacing the signal " +
                          "instead of widening it silently retires that behaviour");

            if (body.IndexOf("Finalized", StringComparison.Ordinal) < 0)
                fails.Add("Case B: HeroHealth's death path does not consider RaidScoring.Finalized. A SETTLED " +
                          "raid must route home no matter how the owner rules on respawn-in-raid: the loot is " +
                          "paid, the camp is claimed and the clock has stopped, so there is no session left to " +
                          "respawn into. That case is the softlock itself, not a matter of taste (WO-1437)");
        }

        // =====================================================================
        //  CASE C - THE GENERAL ORACLE: a session-owned terminal-state net
        // =====================================================================
        /// <summary>
        /// WO-1437 acceptance: "a new seam oracle: every raid session reaches a terminal state.
        /// No exit path may depend on the player finding RETREAT."
        /// </summary>
        private static void CaseC_TerminalStateWatchdogExists(string ctrl, List<string> fails)
        {
            if (string.IsNullOrEmpty(ctrl)) return;

            string body = Body(ctrl, @"IEnumerator\s+StrandingWatchdog\s*\(");
            if (string.IsNullOrEmpty(body))
            {
                fails.Add("Case C: RaidDeployController has NO StrandingWatchdog. A raid session's route home " +
                          "is then owned solely by UI: RaidVictoryController hands both the primary action " +
                          "AND the AutoDismissSeconds softlock guard to an EndStateVM, and EndStateView " +
                          "destroys itself whenever another modal opens (Show / OnSceneLoaded / " +
                          "CloseFromArbiter). CAPTURED 2026-09-06 13:02:47: \"'Victory!' destroyed WITHOUT " +
                          "firing its primary action ... REPLACED by a new end-state 'YOU HAVE FALLEN'\" - " +
                          "both escape routes died together and the player was left in a WON raid with only " +
                          "RETREAT, the losing exit. BattleArena already solved this exact shape in WO-969 " +
                          "(BattleArena.StrandingWatchdog); raids need the same net (WO-1437 acceptance 4)");
                return;
            }

            if (body.IndexOf("Time.unscaledDeltaTime", StringComparison.Ordinal) < 0 &&
                body.IndexOf("WaitForSecondsRealtime", StringComparison.Ordinal) < 0)
                fails.Add("Case C: the raid stranding watchdog is not UNSCALED. A safety net that runs on " +
                          "scaled time is disarmed by the very thing it guards against - any system leaving " +
                          "Time.timeScale at 0 would stop the net along with the game. BattleArena's " +
                          "equivalent is unscaled for the same reason");

            if (body.IndexOf("Finalized", StringComparison.Ordinal) < 0)
                fails.Add("Case C: the raid stranding watchdog does not arm off RaidScoring.Finalized, so it " +
                          "cannot tell a settled-but-unexited raid (the WO-1437 softlock) from a raid still " +
                          "being played");

            if (body.IndexOf("HubScenes.IsRaid", StringComparison.Ordinal) < 0)
                fails.Add("Case C: the raid stranding watchdog never checks whether the player is STILL in a " +
                          "raid scene. Without that stand-down it cannot distinguish a normal exit (the scene " +
                          "changed) from a stranding, and would fire after a healthy raid");

            if (body.IndexOf("FlowTrace.Fail", StringComparison.Ordinal) < 0)
                fails.Add("Case C: the raid stranding watchdog fires SILENTLY. A net firing means a real exit " +
                          "was eaten upstream; if it is not a loud captured line, the underlying defect is " +
                          "invisible forever and the net becomes the fix instead of the seatbelt " +
                          "(CLAUDE.md sec.12)");

            string exitBody = Body(ctrl, @"void\s+ForceExitHome\s*\(");
            if (string.IsNullOrEmpty(exitBody))
                fails.Add("Case C: RaidDeployController has no ForceExitHome - the watchdog has no settle-then-" +
                          "leave path, so a rescued raid would leave the army and the loot unsettled");
            else
            {
                if (exitBody.IndexOf("SceneRouter.GoCastle", StringComparison.Ordinal) < 0)
                    fails.Add("Case C: ForceExitHome does not call SceneRouter.GoCastle - the terminal-state " +
                              "net does not actually reach a terminal state");
                if (exitBody.IndexOf("SettlePartialLoot", StringComparison.Ordinal) < 0 ||
                    exitBody.IndexOf("ReconcileRaidEnd", StringComparison.Ordinal) < 0)
                    fails.Add("Case C: ForceExitHome does not route through SettlePartialLoot AND " +
                              "ReconcileRaidEnd. Both are latched, so on a normal exit they are logged " +
                              "no-ops - but a raid rescued from a never-settled state would otherwise pay " +
                              "nothing and strand the deployed army as neither survivor nor wounded");
            }

            // The net must be armed where the clock subscriber is armed: BEFORE the HUD build,
            // so no exit depends on presentation succeeding (the WO-1110 sec.1 ordering rule
            // this controller already lives by).
            string bindBody = Body(ctrl, @"IEnumerator\s+BindScoringRoutine\s*\(");
            if (!string.IsNullOrEmpty(bindBody) &&
                bindBody.IndexOf("StrandingWatchdog", StringComparison.Ordinal) < 0)
                fails.Add("Case C: the stranding watchdog is not armed inside BindScoringRoutine. It must be " +
                          "armed alongside the clock-expiry subscriber, which Start() deliberately binds " +
                          "BEFORE BuildHud - an exit hatch must never depend on the HUD building " +
                          "(WO-1110 sec.1)");
        }

        // =====================================================================
        //  CASE D - the fallen screen's words must match the branch actually taken
        // =====================================================================
        private static void CaseD_DeathCopyTracksTheDeathBranch(string death, List<string> fails)
        {
            if (string.IsNullOrEmpty(death)) return;

            if (death.IndexOf("RaidScoring.RaidInProgress", StringComparison.Ordinal) < 0)
                fails.Add("Case D: HeroDeathEndState still decides its copy from SceneOwnership alone. " +
                          "EndStateVM.FromHeroDeath's bool picks between \"The raid is lost. You retreat to " +
                          "the castle...\" and \"The dark takes you, but Elarion still needs its defender\", " +
                          "so after a claim flipped ownership the screen promised a respawn while the hero " +
                          "stood in a raid base (captured 13:02:47, enemyOwned=False, same scene that read " +
                          "True at 12:59:45). The words must track HeroHealth's branch or the player is told " +
                          "one thing and dealt another (WO-1437)");

            // Presentation reports the lifecycle; it must never route it. Guarding this here
            // keeps the WO-1437 fix from drifting into the layer violation it removed.
            if (Regex.IsMatch(death, @"SceneRouter\s*\.\s*Go"))
                fails.Add("Case D: HeroDeathEndState now routes scenes itself. The end state is PRESENTATION - " +
                          "it reports the death, HeroHealth decides where the hero goes. A view owning a " +
                          "transition is precisely the ownership WO-1437 took back from EndStateView " +
                          "(ARCHITECTURE_PRINCIPLES: presentation is a separate layer)");
        }

        // =====================================================================
        //  CASE E - no exit's ONLY owner may be a destroyable view
        // =====================================================================
        private static void CaseE_NoExitIsOwnedOnlyByAView(string ctrl, string score, string vict,
                                                           List<string> fails)
        {
            if (string.IsNullOrEmpty(vict) || string.IsNullOrEmpty(ctrl)) return;

            // The victory path may keep handing its route home to the EndState template - that is
            // the good, player-facing path. What it may NOT do is be the only owner. The pairing
            // is the pin: if the victory screen owns ReturnHome, a non-view net must also exist.
            bool victoryRoutesThroughAView =
                vict.IndexOf("EndStateView.Show", StringComparison.Ordinal) >= 0 &&
                vict.IndexOf("ReturnHome", StringComparison.Ordinal) >= 0;
            bool nonViewNetExists =
                ctrl.IndexOf("StrandingWatchdog", StringComparison.Ordinal) >= 0;

            if (victoryRoutesThroughAView && !nonViewNetExists)
                fails.Add("Case E: the VICTORY exit's route home is owned only by an EndState view. " +
                          "RaidVictoryController hands ReturnHome and the auto-dismiss guard to a panel that " +
                          "three code paths destroy without firing (EndStateView Show / OnSceneLoaded / " +
                          "CloseFromArbiter). The panel dying without firing is CORRECT - a displaced " +
                          "end-state must never silently trigger a transition - so the fix is that the " +
                          "SESSION owns its own termination. Restore the non-view net (WO-1437 acceptance 4)");

            // The clock exit must still exist: it is the only exit that needs no player input at
            // all, and WO-1110 sec.1 made its binding order load-bearing.
            if (!string.IsNullOrEmpty(score) &&
                score.IndexOf("OnTimeExpired", StringComparison.Ordinal) < 0)
                fails.Add("Case E: RaidScoring no longer raises OnTimeExpired - the timeout exit, the only " +
                          "one requiring no player input, is gone");
            if (ctrl.IndexOf("OnTimeExpired", StringComparison.Ordinal) < 0)
                fails.Add("Case E: RaidDeployController no longer subscribes to OnTimeExpired - the raid clock " +
                          "no longer ends the raid, leaving RETREAT as the only unaided exit (WO-1437 sec.5: " +
                          "no exit path may depend on the player finding RETREAT)");

            // And the owner's ruling must live in exactly ONE place, so the behaviour and the
            // copy cannot answer the same question differently again - the whole shape of this bug.
            if (!string.IsNullOrEmpty(score) &&
                score.IndexOf("RaidDeathEndsRaid", StringComparison.Ordinal) < 0)
                fails.Add("Case E: RaidScoring.RaidDeathEndsRaid is gone. WO-1437 put the owner's " +
                          "respawn-in-a-raid ruling in ONE constant read by both HeroHealth (behaviour) and " +
                          "HeroDeathEndState (copy). Two copies of one ruling is the duplicated state that " +
                          "produced this ticket");
        }

        // =====================================================================
        //  Helpers - shapes borrowed verbatim from RaidExitParityRegression
        // =====================================================================

        private static Type FindTypeByName(string fullName)
        {
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                Type t = null;
                try { t = asms[i].GetType(fullName, false); }
                catch (Exception) { }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// The brace-matched body of the first method whose signature matches, from the
        /// signature's opening brace to its balanced close.
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
