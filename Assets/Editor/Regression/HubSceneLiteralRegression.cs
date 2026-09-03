// =============================================================================
// HubSceneLiteralRegression [hub-scene-literal]   Marker: HUB_SCENE_LITERAL_OK / _FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Registered in DataRegression.RunAll.
//
// THE DEFECT THIS EXISTS FOR (WO-1112, Sunday coverage audit):
//   THREE separate gates were watching a RETIRED scene, each by hardcoding the same
//   stale hub-scene name as a string literal:
//     * UICaptureMode.BootScene          = "MainCastle_Hall"  -> UI_CAPTURE_OK screenshotted
//                                          the wrong scene's HUD and could not see a hub
//                                          HUD regression at all.
//     * TowerRespawnRegression.HomeHub   = "MainCastle_Hall"  -> TOWER_RESPAWN_OK could print
//                                          while placed towers vanished on a death->hub reload.
//     * FloorDeepDiag.TargetScene        = "MainCastle_Hall"  -> the deep floor dump never fired
//                                          in the scene the owner was standing in.
//   (ArcaneTowerDiag.TargetScene was the same shape, merely pinned to the branch that
//   happens to be live today -- correct now, stale the moment the flag or the name moves.)
//
//   The three instances are SYMPTOMS. The defect is the CLASS: a gate or a runtime
//   diagnostic that decides "which scene do I watch / boot / assert against" from a name
//   typed into itself, instead of resolving it from SceneRouter. A duplicated name cannot
//   be kept in sync, so it goes stale SILENTLY -- and a stale gate does not go red, it goes
//   green while proving nothing. That is strictly worse than having no gate, because the
//   marker is trusted.
//
// WHAT THIS ORACLE ASSERTS
//   For every .cs under the scanned roots (gates + runtime diagnostics), with COMMENTS
//   STRIPPED, no scene name that SceneRouter.Castle can resolve to may appear as a string
//   literal -- unless the (file, literal) pair is in the small, justified allowlist below.
//
//   The scanned names come from SceneRouter.CastleCandidates, so this oracle itself owns no
//   copy of them: adding a hub variant there extends this guard for free, and there is no
//   second list to go stale (which would be this ticket's own bug, in this file).
//
// NO HOLLOW PASSES (CLAUDE.md sec.12 / the audit's standing rule):
//   * zero scanned roots, zero scanned files, or an empty candidate set => FAIL, never OK.
//     A lint that finds nothing to look at has not passed, it has not run.
//   * an allowlist entry that matches NOTHING => FAIL. The allowlist is a RATCHET: it may
//     only shrink, and it may never quietly outlive the code it excused.
//
// Standalone batch entry:
//   -Method DeNelle.Editor.Regression.HubSceneLiteralRegression.RunStandalone
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor.Regression
{
    public static class HubSceneLiteralRegression
    {
        /// <summary>This file names the hub scenes in its own prose + allowlist; never scan itself.</summary>
        private const string SelfFileName = "HubSceneLiteralRegression.cs";

        /// <summary>
        /// Project-root-relative roots that hold GATES and RUNTIME DIAGNOSTICS -- the code whose
        /// whole job is to judge or observe the game. Scene BUILDERS (Assets/Editor/*Builder.cs)
        /// are deliberately OUT of scope: naming the scene file it opens and saves is what a
        /// builder legitimately does, and dragging them in would make this lint noise nobody reads.
        /// </summary>
        private static readonly string[] ScanRoots =
        {
            "Assets/Editor/Regression",
            "Assets/_Modules/Core/Diagnostics",
        };

        /// <summary>
        /// (file, literal) pairs that are DELIBERATE and stay. SHRINK THIS LIST; do not grow it.
        /// Each entry must still match something in the file, or the suite FAILS -- an allowlist
        /// that outlives its code is how the original stale constants survived review.
        ///
        /// TownSuspendSceneFloorRegression.cs / "MainCastle_Hall": Case1's `mustNotSuspend` array
        /// enumerates RETIRED hub names on purpose, to prove TownSuspension still classifies the
        /// legacy names correctly. That is a test INPUT, not a resolution of "which scene am I".
        /// </summary>
        private static readonly (string File, string Literal)[] Allowed =
        {
            ("TownSuspendSceneFloorRegression.cs", "MainCastle_Hall"),
            // WO-1229 classifier controls: these are deliberate negative TEST INPUTS proving
            // neither live nor legacy hub names are misclassified as dungeon scenes.
            ("VfxAmbientLoopBudgetRegression.cs", "Main_Castle_Overworld"),
            ("VfxAmbientLoopBudgetRegression.cs", "MainCastle_Hall"),
        };

        /// <summary>Standalone batch entry - prints HUB_SCENE_LITERAL_OK / _FAIL.</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("HUB_SCENE_LITERAL_OK - " + reason);
            else Debug.LogError("HUB_SCENE_LITERAL_FAIL - " + reason);
        }

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "hub-scene-literal: oracle THREW " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool RunCore(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                reason = "hub-scene-literal: could not resolve the project root from Application.dataPath - " +
                         "the scan cannot run, so this is a FAILURE, not a skip";
                return false;
            }

            // The names to hunt for come from the canonical property, not from a copy here.
            string[] hubNames = SceneRouter.CastleCandidates;
            if (hubNames == null || hubNames.Length == 0)
            {
                reason = "hub-scene-literal: SceneRouter.CastleCandidates is EMPTY - this lint would scan for " +
                         "nothing and report OK against every file in the repo. Zero targets is a failure.";
                return false;
            }
            log.AppendLine("scanning for hub-scene literals " + Join(hubNames) +
                           " (live SceneRouter.Castle = '" + SceneRouter.Castle + "')");

            // Which allowlist entries actually fired. Any that never fires is dead debt -> FAIL.
            var allowUsed = new HashSet<string>(StringComparer.Ordinal);

            int filesScanned = 0, rootsFound = 0;
            foreach (var root in ScanRoots)
            {
                string dir = Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(dir))
                {
                    // A scan root that moved is a SILENCED lint. Say so loudly rather than skipping.
                    failures.Add("scan root '" + root + "' does not exist - this lint is no longer looking at it. " +
                                 "Re-point ScanRoots; a missing root silently disables the guard.");
                    continue;
                }
                rootsFound++;

                foreach (var path in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(path);
                    if (string.Equals(name, SelfFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    filesScanned++;

                    string code = StripComments(ReadOrEmpty(path));
                    foreach (var hub in hubNames)
                    {
                        string needle = "\"" + hub + "\"";
                        int at = code.IndexOf(needle, StringComparison.Ordinal);
                        if (at < 0) continue;

                        if (IsAllowed(name, hub))
                        {
                            allowUsed.Add(Key(name, hub));
                            continue;
                        }

                        failures.Add(name + " (line " + LineOf(code, at) + ") hardcodes the hub scene name " + needle +
                                     " as a string literal. A gate/diagnostic must RESOLVE its hub from " +
                                     "SceneRouter.Castle (or iterate SceneRouter.CastleCandidates when it must hold for " +
                                     "both ff.MergedWorld branches). A typed-in name goes stale silently, and a stale " +
                                     "gate reports OK while watching a scene the player never loads - which is exactly " +
                                     "how UICaptureMode, TowerRespawnRegression and FloorDeepDiag all ended up pinned to " +
                                     "the retired hub at once. If the literal is a deliberate TEST INPUT, add it to " +
                                     "HubSceneLiteralRegression.Allowed with the reason.");
                    }
                }
            }

            // ---- BEHAVIOURAL CASE: the lint's own defect class, proven by CALLING it. ----
            CaseGroundFixerReachesLiveHub(failures, log);

            // ---- HOLLOW-PASS GUARDS: finding nothing is never a pass. --------------------
            if (rootsFound == 0)
                failures.Add("NO scan root resolved on disk - this suite asserted against zero files and would " +
                             "otherwise have printed HUB_SCENE_LITERAL_OK. A lint with no corpus has not run.");
            if (filesScanned == 0)
                failures.Add("scanned 0 .cs files across " + ScanRoots.Length + " root(s) - zero targets is a FAILURE, " +
                             "not a pass; the gates and diagnostics this guards cannot all have vanished.");

            foreach (var a in Allowed)
            {
                if (allowUsed.Contains(Key(a.File, a.Literal))) continue;
                failures.Add("allowlist entry (" + a.File + ", \"" + a.Literal + "\") matched NOTHING - either the " +
                             "file was renamed/deleted or the literal is gone. Delete the entry. An allowlist that " +
                             "outlives its code is an excuse nobody re-examines (the ratchet only shrinks).");
            }

            log.AppendLine("roots=" + rootsFound + "/" + ScanRoots.Length + ", files=" + filesScanned +
                           ", allowlist entries used=" + allowUsed.Count + "/" + Allowed.Length);

            if (failures.Count > 0)
            {
                reason = "hub-scene-literal FAIL x" + failures.Count + ": " + string.Join(" | ", failures.ToArray());
                Debug.LogError(log.ToString() + "HUB_SCENE_LITERAL_FAIL: " + reason);
                return false;
            }

            reason = "HUB SCENE LITERAL OK - " + filesScanned + " gate/diagnostic file(s) across " + rootsFound +
                     " root(s) resolve their hub from SceneRouter; no stale hub-scene literal " + Join(hubNames) +
                     " outside the " + Allowed.Length + "-entry justified allowlist";
            Debug.Log(log.ToString() + "HUB_SCENE_LITERAL_OK");
            return true;
        }

        // =====================================================================
        //  BEHAVIOURAL CASE (WO-1301) — GroundZFightFixer reaches the LIVE hub
        // ---------------------------------------------------------------------
        //  WHY IT LIVES IN THIS SUITE: the lint above proves no gate CONTAINS a
        //  stale hub literal. GroundZFightFixer proved that is not sufficient — it
        //  held no hub literal at all. It held a stale hub PREFIX
        //  (StartsWith("MainCastle") / StartsWith("Castle")), which the string scan
        //  cannot see, and which the WO-608 rename to the live merged hub silently
        //  falsified: that name begins "Main_", so it matched neither prefix. The
        //  entire floor z-fight fixer became a no-op in the only town the player
        //  stands in, and nothing went red.
        //
        //  PROOF IT WAS REAL, not inferred (CLAUDE.md sec.12): 35 MB of the owner's
        //  Player.log, thick with scene='<the live hub>' gameplay, contains ZERO
        //  "GroundZFightFixer" lines, while other [Flow:*] tags from that same scene
        //  appear in the thousands. The fixer never ran. The owner, 2026-09-03:
        //  "It was fixed and now it's back."
        //
        //  So this case does what a string scan structurally cannot: it CALLS the
        //  live routing decision and asserts it says YES for every scene the router
        //  can send the player home to. A prefix, a Contains, a switch, or a fourth
        //  copy of the name all fail this the moment they drift.
        //
        //  RED-FIRST (the mutation this case is built to catch): restore the old
        //  allow-list gate --
        //      return n.StartsWith("Village") || n.StartsWith("MainCastle") ||
        //             n.StartsWith("Castle")  || n.StartsWith("Garrison");
        //  and this case FAILS on SceneRouter.CastleCandidates[0], naming it.
        // =====================================================================
        private static void CaseGroundFixerReachesLiveHub(List<string> failures, StringBuilder log)
        {
            // Every scene the router can send the player HOME to, both ff.MergedWorld
            // branches. Resolved from canon, never typed here (that is this file's whole point).
            string[] homes = SceneRouter.CastleCandidates;
            string[] hubs = HubScenes.Names;

            if (homes == null || homes.Length == 0 || hubs == null || hubs.Length == 0)
            {
                failures.Add("ground-fixer routing: SceneRouter.CastleCandidates or HubScenes.Names is EMPTY, " +
                             "so this case would assert against nothing and pass hollow. Zero targets is a FAILURE.");
                return;
            }

            int asserted = 0;
            foreach (var scene in homes)
            {
                if (string.IsNullOrEmpty(scene)) continue;
                asserted++;
                if (!GroundZFightFixer.WouldRunInScene(scene))
                    failures.Add("GroundZFightFixer.WouldRunInScene('" + scene + "') is FALSE, but SceneRouter can " +
                                 "route the player home to that scene. The floor z-fight fixer would be a SILENT " +
                                 "no-op in the town the player lives in - which is the exact WO-608 regression this " +
                                 "case exists for. Gate the fixer by EXCLUSION + geometry, never by a hub-name prefix.");
            }
            foreach (var scene in hubs)
            {
                if (string.IsNullOrEmpty(scene)) continue;
                asserted++;
                if (!GroundZFightFixer.WouldRunInScene(scene))
                    failures.Add("GroundZFightFixer.WouldRunInScene('" + scene + "') is FALSE for a canonical hub " +
                                 "listed in HubScenes.Names. Adding a hub there must never require a second edit " +
                                 "inside the fixer.");
            }

            // The exclusions the fixer must KEEP. These are not hub names and cannot go stale
            // the way a hub name does; a dungeon is resolved through HubScenes.IsDungeon.
            //
            // "RaidBase_IronBastion" is the TEMPORARY one (2026-09-03, lead ruling): raid
            // scenes are held out of the widened gate for felt-test scope containment while
            // the owner verifies four unrelated fixes on device, NOT because raids are judged
            // not to need the floor fix. See GroundZFightFixer.IsTemporarilyExcludedScene for
            // the full reasoning and the lift procedure. WHEN THAT EXCLUSION IS LIFTED, THIS
            // ENTRY MOVES OUT IN THE SAME EDIT - a suite asserting an exclusion the code no
            // longer applies is precisely the code/oracle drift this ticket exists to kill.
            var mustNotRun = new[] { "Title", "HeroSelect", "PetSelect", "ATBBattle", "dg_starter_loop", "Dungeon_Demo",
                                     "RaidBase_IronBastion" };
            foreach (var scene in mustNotRun)
            {
                asserted++;
                if (GroundZFightFixer.WouldRunInScene(scene))
                    failures.Add("GroundZFightFixer.WouldRunInScene('" + scene + "') is TRUE - the fixer must never " +
                                 "touch front-end, battle-box or dungeon floors (those scenes own their own).");
            }

            // A REAL separation, not merely 'not exactly equal'. A 0.0001 m offset satisfies
            // 'not coplanar' and still z-fights on device, so the constant itself is pinned:
            // the terrain render mesh is LOD-simplified and deviates several cm from its
            // heightmap, which is why the shipped sink is half a metre.
            asserted++;
            if (GroundZFightFixer.CoplanarSinkBelow < 0.1f)
                failures.Add("GroundZFightFixer.CoplanarSinkBelow is " + GroundZFightFixer.CoplanarSinkBelow +
                             " m - too small to clear LOD deviation in the terrain RENDER mesh. A token offset " +
                             "passes a 'not equal' test and still z-fights on the device. Keep it >= 0.1 m.");
            asserted++;
            if (GroundZFightFixer.CoplanarEpsilon <= 0f)
                failures.Add("GroundZFightFixer.CoplanarEpsilon is " + GroundZFightFixer.CoplanarEpsilon +
                             " - a non-positive epsilon detects only EXACT coplanarity, so two surfaces 1 mm apart " +
                             "(which z-fight just as badly) would never be repaired.");

            log.AppendLine("ground-fixer routing: " + asserted + " assertion(s) over " + homes.Length +
                           " router home(s) + " + hubs.Length + " canonical hub(s) + " + mustNotRun.Length +
                           " excluded scene(s) + 2 separation constant(s)");

            // [PARTIAL-SKIP] [device-only] HONEST LIMIT, stated rather than faked:
            //   The SHIMMER cannot be asserted here. Batchmode runs no render pass, so no suite
            //   can observe z-fighting; and the coplanar sweep operates on a LOADED scene's
            //   renderers and Terrain, which this EditMode lint does not stand up. What is
            //   pinned above is the half that is real: the fixer REACHES the live hub, keeps its
            //   exclusions, and applies a separation big enough to matter. That the ground stops
            //   shimmering is the owner's felt-verify on device (docs/TICKET_PIPELINE.md: PO closes).
            //   Do NOT add an assertion here that gestures at the visual and always passes.
            log.AppendLine("ground-fixer routing: [PARTIAL-SKIP] [device-only] the z-fight itself is not " +
                           "observable in batchmode (no render pass); routing + separation are pinned, the " +
                           "visual is the owner's felt-verify");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static string Key(string file, string literal) => file + "|" + literal;

        private static bool IsAllowed(string fileName, string literal)
        {
            foreach (var a in Allowed)
                if (string.Equals(a.File, fileName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(a.Literal, literal, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string Join(string[] names) => "{ " + string.Join(", ", names) + " }";

        /// <summary>1-based line number of <paramref name="index"/> in <paramref name="text"/>.</summary>
        private static int LineOf(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        /// <summary>
        /// Blank out // line comments and block comments, preserving line COUNT so the reported
        /// line number still points at the real source line. Comments are stripped because this
        /// file's own prose -- and every fix note left beside a repaired constant -- quotes the
        /// forbidden literal while explaining why not to write it; a lint that cannot tell code
        /// from prose punishes the author for documenting the trap (the same trade
        /// RaidScoringRegression.StripComments makes, for the same reason).
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' '); break; }
                    // Keep the newlines inside the block so line numbers do not drift.
                    for (int k = i; k <= end + 1; k++) sb.Append(src[k] == '\n' ? '\n' : ' ');
                    i = end + 1;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
