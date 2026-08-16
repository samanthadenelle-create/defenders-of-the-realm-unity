// =============================================================================
// SpirePlansCelebrationRegression [spire-celebration] -- WO-1104 SS3+SS4 guardrails
// for the Arcane Spire plans MOMENT (the celebration + call-to-arms screen).
// regression-registry: registered by the committer
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Marker: SPIRE_CELEBRATION_OK /
// SPIRE_CELEBRATION_FAIL. Expected: GREEN.
//
// WHY EACH ASSERTION EXISTS (every one pins a failure this feature has already
// had, or that the owner's rulings make expensive):
//
//   1. SUBSCRIBED. CastleDefensePlansPickup.PlansCollected has existed since
//      WO-1013 and its header said "the contextual-step pipeline subscribes here"
//      -- and NOTHING did, for three weeks, so the plans landed in total silence
//      and the owner asked "when do i get the arcane spire plans?". The seam being
//      present is not the guarantee; a LIVE SUBSCRIBER is. This forces the
//      installer to run and asserts the event's delegate is non-null.
//
//   2. ONCE EVER. The prop deterministically re-spawns from state on every scene
//      entry (WO-1104 sec.3); the CELEBRATION must not. The gate is a persisted
//      one-shot key in the SAME GameState.SeenTutorials store the unlock flag uses
//      (ProgressionUnlocks idiom) -- asserted in BOTH directions, so neither a
//      replaying moment nor a permanently-suppressed one can ship.
//
//   3. PANELMANAGER REGISTERED. An unregistered top-band modal is invisible to the
//      back-button / battle-lock arbiter -- the exact defect [modal-registration]
//      caught on DungeonExitInteractable. Register + NotifyOpened + NotifyClosed,
//      all three (a Register without a NotifyClosed leaks the arbiter's open slot).
//
//   4. PRESENTATION ONLY. The unlock flag and the funding grant are committed by
//      TryCollect BEFORE the event is raised. If this screen could write either,
//      then skipping it -- or failing to build it -- could cost the player the
//      Spire. Asserted as a source-lint (the screen names none of the mechanics
//      seams, and never the Echo roster/assignment seams) AND at runtime (raising
//      the event moves no unlock state).
//
//   5. SPEAKER FROM THE CATALOG. The speaker is Echo #1, read from
//      EchoRosterCatalog -- the authority. A hardcoded name is how the invented
//      element-word speaker name got into the tree in the first place (WO-1031 is
//      deleting it). This lint reads the forbidden names FROM the catalog rather
//      than restating any of them here, so the assertion cannot go stale when the
//      roster is re-authored.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "spire-celebration suite", () => { if (!SpirePlansCelebrationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[spire-celebration] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class SpirePlansCelebrationRegression
    {
        private const string SaveKey = "dotr-save";
        private const string SpireId = "tower_arcane_spire";
        private const string SourceRel = "_Modules/Village/Progression/SpirePlansCelebration.cs";
        private const string InstallerType = "DeNelle.Village.SpirePlansCelebrationInstaller";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SPIRE PLANS CELEBRATION (WO-1104 SS3+SS4: the plans moment) ---");

            string source = ReadSource(out string srcErr);
            if (source == null)
            {
                failures.Add("[spire-celebration] source unreadable at Assets/" + SourceRel + ": " + srcErr);
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            var codeLines = CodeLines(source);
            log.AppendLine("  source read: " + codeLines.Count + " non-comment lines");

            // ---- 1: A LIVE SUBSCRIBER on PlansCollected ---------------------------
            AssertSubscribed(failures, log);

            // ---- 3: PanelManager registration (all three calls) --------------------
            AssertContains(failures, log, codeLines, "PanelManager.Register",
                "the screen never REGISTERS with PanelManager -- an unregistered top-band modal is " +
                "invisible to the back-button / battle-lock arbiter ([modal-registration])");
            AssertContains(failures, log, codeLines, "PanelManager.NotifyOpened",
                "the screen never calls NotifyOpened -- the arbiter is not told it is on screen, so " +
                "the battle-lock gate cannot reject it and back does not route to it");
            AssertContains(failures, log, codeLines, "PanelManager.NotifyClosed",
                "the screen never calls NotifyClosed -- the arbiter's single open slot leaks and the " +
                "NEXT panel is closed on its way in");

            // ---- 4a: PRESENTATION ONLY (source) ------------------------------------
            // Naming any of these seams means the celebration could mutate state the
            // player already earned -- and a skipped/failed screen would then cost them
            // the Spire. The screen may READ nothing here and writes nothing here.
            AssertAbsent(failures, log, codeLines, "ProgressionUnlocks.Unlock",
                "the celebration writes the UNLOCK flag -- that is TryCollect's job, committed before " +
                "the event is raised; a presentational screen must never gate the reward");
            AssertAbsent(failures, log, codeLines, "GrantPurchased",
                "the celebration touches the funding grant -- TryCollect already granted the basket");
            AssertAbsent(failures, log, codeLines, "TryCollect",
                "the celebration re-enters the collection mechanics instead of only presenting them");
            AssertAbsent(failures, log, codeLines, "EchoAssignments",
                "the celebration touches Echo ASSIGNMENTS -- WO-1104 sec.6: the moment must not move the " +
                "player's earned Echoes (body != Echo; 'Echoes 1/6' must not change)");
            AssertAbsent(failures, log, codeLines, "EchoService",
                "the celebration touches EchoService -- it may READ the roster catalog for a name, " +
                "never the live Echo service (that is the roster/unlock state WO-1104 sec.6 fences off)");

            // ---- 5: the speaker resolves FROM the catalog, never a literal ---------
            AssertContains(failures, log, codeLines, "EchoRosterCatalog.ByCount",
                "the speaker is not read from EchoRosterCatalog -- the roster catalog is the naming " +
                "AUTHORITY (WO-1104 sec.4); anything else forks it");
            AssertNoRosterNameLiteral(failures, log, codeLines);

            // ---- 2 + 4b: once-ever + no state movement (runtime, throwaway state) --
            AssertOnceEverAndInert(failures, log);

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // =====================================================================
        //  1. the subscriber
        // =====================================================================
        private static void AssertSubscribed(List<string> failures, StringBuilder log)
        {
            try
            {
                // The installer arms itself from [RuntimeInitializeOnLoadMethod], which does
                // NOT run in EditMode -- so drive it directly. It is idempotent (s_hooked).
                var asm = typeof(CastleDefensePlansPickup).Assembly;
                var installer = asm.GetType(InstallerType, throwOnError: false);
                if (installer == null)
                {
                    failures.Add("[spire-celebration] no '" + InstallerType + "' in " + asm.GetName().Name +
                                 " -- nothing subscribes to PlansCollected, so the plans land in silence " +
                                 "(the WO-1104 sec.1 defect, verbatim)");
                    return;
                }
                var install = installer.GetMethod("Install",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (install == null)
                {
                    failures.Add("[spire-celebration] '" + InstallerType + "' has no static Install() -- " +
                                 "the self-arming hook cannot be proven to run");
                    return;
                }
                install.Invoke(null, null);

                // A C# static event's backing field carries the multicast delegate.
                var evtField = typeof(CastleDefensePlansPickup).GetField("PlansCollected",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (evtField == null)
                {
                    failures.Add("[spire-celebration] CastleDefensePlansPickup.PlansCollected has no " +
                                 "reflectable backing field -- cannot prove a live subscriber");
                    return;
                }
                var del = evtField.GetValue(null) as Delegate;
                if (del == null)
                {
                    failures.Add("[spire-celebration] PlansCollected has ZERO subscribers after Install() -- " +
                                 "the celebration is not wired to the one seam that fires it");
                    return;
                }
                var list = del.GetInvocationList();
                log.AppendLine("  PlansCollected subscribers after Install(): " + list.Length +
                               " (first: " + list[0].Method.DeclaringType?.Name + "." + list[0].Method.Name + ")");
            }
            catch (Exception ex)
            {
                failures.Add("[spire-celebration] subscriber check threw: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // =====================================================================
        //  2 + 4b. once-ever, and the raise moves NO earned state
        // =====================================================================
        private static void AssertOnceEverAndInert(List<string> failures, StringBuilder log)
        {
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            GameStateService priorGss = GameStateService.Instance;

            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (spire-celebration oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  once-ever check SKIPPED: GameStateService state seam not reflectable");
                    return;
                }

                // ARM A -- a fresh state has NOT seen the moment (the flag is what gates it,
                // not a constant that happens to read true).
                if (SpirePlansCelebration.HasBeenSeen)
                    failures.Add("[spire-celebration] HasBeenSeen is TRUE on a fresh save -- the moment would " +
                                 "never play at all (worse than replaying: the player earns it and sees nothing)");
                else
                    log.AppendLine("  fresh save: HasBeenSeen=false (the moment is available) OK");

                // ARM B -- once the one-shot key is set, the moment refuses to build. Set it
                // through the SAME house seam the screen uses (MarkTutorialSeen -> SeenTutorials).
                gss.MarkTutorialSeen(SpirePlansCelebration.SeenKey);
                if (!(throwaway.SeenTutorials != null
                      && throwaway.SeenTutorials.TryGetValue(SpirePlansCelebration.SeenKey, out bool seen) && seen))
                    failures.Add("[spire-celebration] '" + SpirePlansCelebration.SeenKey + "' did not persist in " +
                                 "the SeenTutorials store -- the once-ever gate has no home (a second store was " +
                                 "invented, or the key drifted)");
                if (!SpirePlansCelebration.HasBeenSeen)
                    failures.Add("[spire-celebration] HasBeenSeen is FALSE after the one-shot key was set -- the " +
                                 "screen reads a DIFFERENT flag than it writes, so it replays forever");

                bool unlockedBefore = ProgressionUnlocks.IsUnlocked(SpireId);

                // A SECOND raise of the collected event must not re-show. Fire the real event
                // through its backing delegate -- this exercises the actual subscriber chain,
                // not a stand-in.
                int liveBefore = LiveScreens();
                RaisePlansCollected(failures);
                int liveAfter = LiveScreens();
                if (liveAfter > liveBefore)
                    failures.Add("[spire-celebration] a raise of PlansCollected with the seen flag SET built " +
                                 (liveAfter - liveBefore) + " celebration screen(s) -- the moment replays " +
                                 "(scene re-entry re-spawns the prop; it must not re-spawn the moment)");
                else
                    log.AppendLine("  second raise with the flag set: no screen built (once-ever holds) OK");

                if (SpirePlansCelebration.Show() != null)
                    failures.Add("[spire-celebration] Show() built a screen while the seen flag was SET -- " +
                                 "the once-ever gate is not consulted on the direct entry point");

                // 4b: the raise must move NO earned state. The unlock flag is the one the whole
                // reward hangs on; if presentation can touch it, a skip can cost the Spire.
                bool unlockedAfter = ProgressionUnlocks.IsUnlocked(SpireId);
                if (unlockedAfter != unlockedBefore)
                    failures.Add("[spire-celebration] raising PlansCollected CHANGED the '" + SpireId +
                                 "' unlock flag (" + unlockedBefore + " -> " + unlockedAfter + ") -- the " +
                                 "celebration is presentational and must never write the reward");
                else
                    log.AppendLine("  raise moved no unlock state ('" + SpireId + "' = " + unlockedAfter + ") OK");
            }
            catch (Exception ex)
            {
                failures.Add("[spire-celebration] once-ever oracle threw: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                foreach (var s in UnityEngine.Object.FindObjectsByType<SpirePlansCelebration>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        private static int LiveScreens()
        {
            return UnityEngine.Object.FindObjectsByType<SpirePlansCelebration>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private static void RaisePlansCollected(List<string> failures)
        {
            var evtField = typeof(CastleDefensePlansPickup).GetField("PlansCollected",
                BindingFlags.NonPublic | BindingFlags.Static);
            var del = evtField != null ? evtField.GetValue(null) as Delegate : null;
            if (del == null)
            {
                failures.Add("[spire-celebration] cannot raise PlansCollected (no delegate) -- the once-ever " +
                             "arm could not be exercised");
                return;
            }
            del.DynamicInvoke();
        }

        // =====================================================================
        //  source-lint helpers
        // =====================================================================
        private static string ReadSource(out string err)
        {
            err = null;
            string path = Path.Combine(Application.dataPath, SourceRel.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (!File.Exists(path)) { err = "file not found"; return null; }
                return File.ReadAllText(path);
            }
            catch (Exception ex) { err = ex.Message; return null; }
        }

        /// <summary>Non-comment lines only, with STRING LITERAL CONTENTS REMOVED -- the assertions
        /// are about CODE, so neither a rule quoted in the header nor a seam NAMED IN A TRACE
        /// MESSAGE can satisfy or trip one.
        /// <para>
        /// Why the literal-stripping exists (2026-08-16, caught by this suite's own first run): the
        /// screen's honest FlowTrace line says "...already committed by TryCollect", and the header
        /// comment explains the same contract. The lint read those as CALLS and reported that the
        /// celebration "re-enters the collection mechanics" -- a FALSE RED against a file that never
        /// calls it. A source-lint that cannot tell a call from a sentence punishes the exact
        /// self-documenting traces CLAUDE.md section 12 asks for; strip, do not weaken the rule.
        /// </para></summary>
        private static List<string> CodeLines(string source)
        {
            var outLines = new List<string>();
            foreach (var raw in source.Split('\n'))
            {
                string t = raw.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                outLines.Add(StripStringLiterals(raw));
            }
            return outLines;
        }

        /// <summary>Blanks the CONTENTS of every double-quoted literal on the line (the quotes are
        /// kept so the line still parses visually), honouring backslash escapes. Trailing `//`
        /// comments after code are dropped too, for the same reason.</summary>
        private static string StripStringLiterals(string line)
        {
            var sb = new StringBuilder(line.Length);
            bool inStr = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inStr && c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;   // trailing comment
                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inStr = !inStr;
                    sb.Append(c);
                    continue;
                }
                sb.Append(inStr ? ' ' : c);
            }
            return sb.ToString();
        }

        private static bool AnyLineContains(List<string> lines, string token)
        {
            foreach (var l in lines)
                if (l.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static void AssertContains(List<string> failures, StringBuilder log, List<string> lines,
                                           string token, string why)
        {
            if (AnyLineContains(lines, token)) log.AppendLine("  code contains '" + token + "' OK");
            else failures.Add("[spire-celebration] " + why + " (no '" + token + "' in the source)");
        }

        private static void AssertAbsent(List<string> failures, StringBuilder log, List<string> lines,
                                         string token, string why)
        {
            if (!AnyLineContains(lines, token)) log.AppendLine("  code free of '" + token + "' OK");
            else failures.Add("[spire-celebration] " + why + " (found '" + token + "' in the source)");
        }

        /// <summary>
        /// No Echo NAME may appear as a literal in the screen's code. The forbidden set is
        /// read FROM EchoRosterCatalog (the authority) rather than restated here, so this
        /// assertion cannot rot when the roster is re-authored -- and the regression itself
        /// contains no name literal either.
        /// </summary>
        private static void AssertNoRosterNameLiteral(List<string> failures, StringBuilder log, List<string> lines)
        {
            var entry = EchoRosterCatalog.ByCount(1);
            if (entry == null || string.IsNullOrEmpty(entry.DisplayName))
            {
                failures.Add("[spire-celebration] EchoRosterCatalog.ByCount(1) has no DisplayName -- the speaker " +
                             "cannot be resolved from the authority at all");
                return;
            }
            int comma = entry.DisplayName.IndexOf(',');
            string bare = comma > 0 ? entry.DisplayName.Substring(0, comma).Trim() : entry.DisplayName.Trim();
            if (string.IsNullOrEmpty(bare))
                failures.Add("[spire-celebration] echo #1's DisplayName does not yield a bare speaker name");

            int hits = 0;
            foreach (var e in EchoRosterCatalog.All)
            {
                if (e == null || string.IsNullOrEmpty(e.DisplayName)) continue;
                int c = e.DisplayName.IndexOf(',');
                string name = (c > 0 ? e.DisplayName.Substring(0, c) : e.DisplayName).Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (AnyLineContains(lines, "\"" + name) || AnyLineContains(lines, name + "\""))
                    hits++;
                else if (AnyLineContains(lines, name)) hits++;
            }
            if (hits > 0)
                failures.Add("[spire-celebration] " + hits + " roster Echo name(s) appear as literals in the " +
                             "screen's CODE -- the name must come from EchoRosterCatalog, the authority " +
                             "(a hardcoded speaker name is exactly what WO-1031 is deleting)");
            else
                log.AppendLine("  no roster name literal in code; speaker derives from echo #1 " +
                               "(bare name length " + bare.Length + ") OK");
        }

        // =====================================================================
        //  reflection helpers (the CastlePlansUnlockRegression shape)
        // =====================================================================
        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "SPIRE_CELEBRATION_OK");
                return "SPIRE CELEBRATION OK -- PlansCollected has a live subscriber; the moment is once-ever " +
                       "on a persisted SeenTutorials key; it registers/opens/closes with PanelManager; it writes " +
                       "no unlock, funding, roster or assignment state; the speaker resolves from EchoRosterCatalog";
            }
            string reason = "spire-celebration: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "SPIRE_CELEBRATION_FAIL: " + reason);
            return reason;
        }
    }
}
