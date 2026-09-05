// =============================================================================
// NewGamePrefStoreSweepRegression [newgame-pref-sweep]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core).
//
// ⛔ THE CLASS OF DEFECT THIS EXISTS FOR, not the instance.
//
// FOUR separate "a New Game inherited X" bugs have now shipped, each fixed alone:
//   WO-860   the equipped axe came back on every new hero
//   WO-1019  the hot-swap ability bar came back
//   WO-1220  a brand-new Ranger came up wearing a level-4 Mage's talents
//   WO-1371  a game 11 SECONDS old banked 14,089 resources of inherited collector
//            fill (farm +7500, lumbermill +5760, forge +829), against a measured
//            honest rate of ~0.58/s - about 6.7 hours of accrual
//
// Every one of them is the same failure: a persistence store that lives OUTSIDE the
// save envelope, in PlayerPrefs, that ResetToNewGame had never heard of.
//
// ⭐ AND THE EXISTING ORACLE ADMITS IT CANNOT SEE THEM. ResetToNewGameFullClearRegression
// Case 1 sweeps GameState FIELDS by reflection, and its own Case 5 comment says so
// verbatim - "Case 1 sweeps GameState fields, and this store is not one". That is why
// WO-1371 shipped to a production candidate under REGRESSION_OK 358/358.
//
// So this suite sweeps the OTHER axis: GameStateService.NewGamePrefStores, the explicit
// ledger of every out-of-envelope store and what a New Game does about it. Bringing a
// store under this oracle costs ONE ROW. The ledger also carries the WO-1371 audit's
// remaining KNOWN GAPS as NotYetCleared rows - reported every run, so the list cannot
// quietly rot into a list of only the things already fixed.
//
// WHY THE BEHAVIOURAL HALF CALLS THE CLEAR HELPERS AND NEVER ResetToNewGame: exactly the
// trade ResetToNewGameFullClearRegression documents. ResetToNewGame ends in Save(), so
// driving it in editor batchmode would wipe the developer's editor save every time the
// gate runs - a worse defect than the one being pinned. Every key this suite touches is
// snapshotted and restored.
//
// Cases:
//   1 [ledger-shape]     the ledger exists, is non-empty, and every row names a key and
//                        a reason (a store is not exempt because nobody noticed it).
//   2 [reset-calls-it]   ResetToNewGame calls ClearHarvestPrefs() - a helper nobody
//                        invokes fixes nothing (WO-1220's lesson, re-applied).
//   3 [cleared-are-gone] BEHAVIOURAL. Poison every ClearedByReset store, run the clear
//                        helpers, assert not one survives. This is the case that fails
//                        RED against the pre-WO-1371 tree.
//   4 [carried-survive]  DeliberatelyCarried stores are still there afterwards - a reset
//                        that eats the player's audio settings is also a defect.
//   5 [live-half]        the LIVE collectors are told, not just the prefs: ResourceCollector
//                        subscribes to NewGameStarted and calls ResourceBuildingState.ResetAll
//                        (which is how TechTree.ResetAll is reached at all).
//   6 [known-gaps]       reports the NotYetCleared rows as a NOTE, and fails only if one
//                        carries no reason.
//   7 [newgame-away-clock] WO-1414. A fresh save's away anchor is ZERO and its ever-built
//                        ledger is EMPTY; the PARKED welcome-back reveal, the harvest tick's
//                        own per-id state and the tutorial-dialogue deferral are all told
//                        about New Game; and the coordinator's window trace still names the
//                        anchor + its provenance. Three mutations, one per fix, listed at
//                        the case.
//
// Markers: NEWGAME_PREF_SWEEP_OK / NEWGAME_PREF_SWEEP_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.NewGamePrefStoreSweepRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class NewGamePrefStoreSweepRegression
    {
        private const string ServiceSrc   = "Assets/_Modules/Core/State/GameStateService.cs";
        private const string CollectorSrc = "Assets/_Modules/Village/Buildings/Progression/ResourceCollector.cs";

        /// <summary>The id used to probe every PER-ID prefix. Deliberately not a real building id
        /// so a failed restore can never corrupt the developer's farm.</summary>
        private const string ProbeId = "regression_probe_collector";

        /// <summary>The owner's actual inherited figure, used as the poison value so a failure
        /// message quotes the real defect rather than a generic sentinel.</summary>
        private const string Poison = "14089";

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("NEWGAME_PREF_SWEEP_OK - " + reason);
            else Debug.LogError("NEWGAME_PREF_SWEEP_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                var stores = GameStateService.NewGamePrefStores;
                Case1_LedgerShape(stores, failures);
                Case2_ResetCallsIt(failures);
                Case3And4_Behaviour(stores, failures);
                Case5_LiveHalf(failures);
                Case6_KnownGaps(stores, failures, notes);
                Case7_NewGameAwayClockAndLedger(failures);
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "NEW GAME PREF SWEEP OK - every store on the out-of-envelope ledger is either " +
                         "provably erased by the New Game clear helpers or is a named, reasoned carry-over, " +
                         "and the LIVE collectors are told as well as their PlayerPrefs" + noteStr;
                return true;
            }
            reason = string.Join("; ", failures) + noteStr;
            return false;
        }

        // =====================================================================

        private static void Case1_LedgerShape(GameStateService.NewGamePrefStore[] stores, List<string> failures)
        {
            if (stores == null || stores.Length == 0)
            {
                failures.Add("[ledger-shape] GameStateService.NewGamePrefStores is EMPTY - every case below " +
                             "would pass vacuously, which is how this whole class of defect stays invisible");
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in stores)
            {
                if (s == null || string.IsNullOrEmpty(s.Key))
                { failures.Add("[ledger-shape] a ledger row has no key"); continue; }
                if (string.IsNullOrEmpty(s.Why))
                    failures.Add("[ledger-shape] '" + s.Key + "' has no reason - a store is exempt because a " +
                                 "reason is written, never because nobody noticed it");
                if (!seen.Add(s.Key))
                    failures.Add("[ledger-shape] '" + s.Key + "' is listed twice - two rows can disagree");
            }
        }

        private static void Case2_ResetCallsIt(List<string> failures)
        {
            var clear = typeof(GameStateService).GetMethod("ClearHarvestPrefs",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (clear == null)
            {
                failures.Add("[reset-calls-it] GameStateService.ClearHarvestPrefs (static) not found - WO-1371 is " +
                             "not in this tree, so a New Game still inherits the previous save's collector fill");
                return;
            }
            string src = File.Exists(ServiceSrc) ? File.ReadAllText(ServiceSrc) : string.Empty;
            if (src.IndexOf("ClearHarvestPrefs();", StringComparison.Ordinal) < 0)
                failures.Add("[reset-calls-it] nothing calls ClearHarvestPrefs() - the eraser exists but New Game " +
                             "never runs it (the exact shape WO-860, WO-1019 and WO-1220 each fixed once)");
        }

        // ── Cases 3 + 4 share one snapshot/restore window ────────────────────────────
        private static void Case3And4_Behaviour(GameStateService.NewGamePrefStore[] stores, List<string> failures)
        {
            if (stores == null || stores.Length == 0) return;

            var clearHarvest = typeof(GameStateService).GetMethod("ClearHarvestPrefs",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var clearProgression = typeof(GameStateService).GetMethod("ClearProgressionPrefs",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (clearHarvest == null) return;   // already reported by Case 2

            // Every key this probe will read, write or delete - including the developer's REAL
            // collector keys, because ClearHarvestPrefs deletes those by design.
            var touched = new List<string>();
            foreach (var s in stores)
            {
                if (s.Disposition == GameStateService.NewGamePrefDisposition.NotYetCleared) continue;
                touched.Add(s.PerId ? s.Key + ProbeId : s.Key);
            }
            foreach (var id in GameStateService.KnownCollectorIds())
            {
                foreach (var p in GameStateService.CollectorPrefPrefixes) touched.Add(p + id);
                touched.Add(GameStateService.ResourceBuildingLevelPrefPrefix + id);
            }
            touched.Add(GameStateService.CollectorKnownIdsPrefKey);

            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in touched)
                if (!snapshot.ContainsKey(k) && PlayerPrefs.HasKey(k))
                    snapshot[k] = PlayerPrefs.GetString(k, string.Empty);

            try
            {
                // POISON. The per-id probe must also be discoverable, or the sweep would "pass"
                // by simply never looking at it - which is the vacuous-green failure mode.
                var expectGone = new List<string>();
                var expectKept = new List<string>();
                foreach (var s in stores)
                {
                    string key = s.PerId ? s.Key + ProbeId : s.Key;
                    switch (s.Disposition)
                    {
                        case GameStateService.NewGamePrefDisposition.ClearedByReset:
                            PlayerPrefs.SetString(key, Poison);
                            expectGone.Add(key);
                            break;
                        case GameStateService.NewGamePrefDisposition.DeliberatelyCarried:
                            PlayerPrefs.SetString(key, Poison);
                            expectKept.Add(key);
                            break;
                    }
                }
                GameStateService.RegisterCollectorId(ProbeId);
                PlayerPrefs.Save();

                clearHarvest.Invoke(null, null);
                clearProgression?.Invoke(null, null);

                var survivors = new List<string>();
                foreach (var k in expectGone) if (PlayerPrefs.HasKey(k)) survivors.Add(k);
                if (survivors.Count > 0)
                    failures.Add("[cleared-are-gone] ⛔ " + survivors.Count + " out-of-envelope store(s) SURVIVED the " +
                                 "New Game clear: " + string.Join(", ", survivors) + " - a fresh save inherits them. " +
                                 "This is the shape that handed an 11-second-old game 14,089 resources");

                var lost = new List<string>();
                foreach (var k in expectKept) if (!PlayerPrefs.HasKey(k)) lost.Add(k);
                if (lost.Count > 0)
                    failures.Add("[carried-survive] " + lost.Count + " store(s) marked DeliberatelyCarried were ERASED " +
                                 "by the New Game clear: " + string.Join(", ", lost) + " - starting over must not cost " +
                                 "the player her settings or her identity");
            }
            finally
            {
                foreach (var k in touched) PlayerPrefs.DeleteKey(k);
                foreach (var kv in snapshot) PlayerPrefs.SetString(kv.Key, kv.Value);
                PlayerPrefs.Save();
            }
        }

        private static void Case5_LiveHalf(List<string> failures)
        {
            string src = File.Exists(CollectorSrc) ? File.ReadAllText(CollectorSrc) : string.Empty;
            if (src.Length == 0)
            {
                failures.Add("[live-half] ResourceCollector.cs is MISSING - the live half cannot be evaluated");
                return;
            }
            if (src.IndexOf("GameStateService.NewGameStarted +=", StringComparison.Ordinal) < 0)
                failures.Add("[live-half] ResourceCollector does not subscribe to NewGameStarted - collectors already " +
                             "in memory keep their pending figure in FIELDS and write it straight back out on the " +
                             "next save, restoring the fill the prefs sweep just deleted");
            if (src.IndexOf("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) < 0)
                failures.Add("[live-half] the collector's New Game hook is not self-installing - it would depend on a " +
                             "collector happening to be alive when Start New is pressed");
            if (src.IndexOf("ResourceBuildingState.ResetAll()", StringComparison.Ordinal) < 0)
                failures.Add("[live-half] nothing calls ResourceBuildingState.ResetAll - it is documented as \"used by " +
                             "a New Game / dev reset\" and had ZERO CALLERS, which is why a fresh town's farm carried " +
                             "a 7500 capacity instead of base, and why TechTree.ResetAll was unreachable too");
        }

        // =====================================================================
        //  Case 7 [newgame-away-clock] -- WO-1414
        // ---------------------------------------------------------------------
        //  THE DEFECT (owner device 2026-09-05 09:57, build 2026.09.05.356468): a BRAND-NEW
        //  game opened on "YOUR REALM WORKED FOR 8h 22m" with +11520 Wood / +6912 Iron /
        //  +15000 Stone waiting. 8h22m was the wall time since the PREVIOUS session; a second
        //  New Game reported 1h56m. The window itself was honest -- it was measured on the
        //  TITLE screen against the OLD save. What was wrong is that it was PARKED
        //  (OfflineHarvestService._deferredReveal, added 2026-09-04 by commit d1fd1f6e0 so the
        //  popup would stop firing over Title) and then RELEASED onto the new game's hub by
        //  sceneLoaded, because ResetToNewGame had never heard of that parked result.
        //
        //  Instance SIX of the shape this whole suite exists for. It is pinned HERE and not in
        //  an offline suite because the axis is "what does a New Game inherit", not "is the
        //  away arithmetic right" -- that stays pinned by OfflineClaimFanOutRegression
        //  (fresh clock => zero window, no fan-out) and OfflineHarvestRegression case 1.
        //
        //  SOURCE LINT, for the reason Cases 2 and 5 are source lint: this assembly references
        //  DeNelle.Core only, so the DeNelle.Village types involved (OfflineHarvestService,
        //  ResourceBuildingHarvester, TutorialFlow) are not callable from here. The STATE half
        //  below IS behavioural and needs no Village type.
        //
        //  THE THREE MUTATIONS THAT RED THIS CASE, one per fix:
        //    A: delete "GameStateService.NewGameStarted +=" from OfflineHarvestService.cs
        //       (the parked reveal survives START NEW again -> [newgame-away-clock] A).
        //    B: delete "GameStateService.NewGameStarted -=/+=" from ResourceBuildingHarvester.cs
        //       (the tick's owed interval + cached gate verdict survive -> ...B), or delete
        //       "EverBuiltStructureIds = new List<string>()" from ResetToNewGame (-> ...B-state).
        //    C: delete "TutorialFlow.IsAwaitingDialogue" from OfflineHarvestService.cs
        //       (the modal can cover the founding beat again -> ...C).
        // =====================================================================

        private const string OfflineSrc   = "Assets/_Modules/Village/Harvest/OfflineHarvestService.cs";
        private const string HarvesterSrc = "Assets/_Modules/Village/Buildings/Progression/ResourceBuildingHarvester.cs";
        private const string TutorialSrc  = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";
        private const string CoordSrc     = "Assets/_Modules/Village/Harvest/OfflineClaimCoordinator.cs";

        private static void Case7_NewGameAwayClockAndLedger(List<string> failures)
        {
            // -- STATE: what a New Game's own fields must be. Asserted on a fresh GameState,
            //    which is the shape ResetToNewGame re-seeds to; driving ResetToNewGame itself
            //    is refused here for the reason this file's header gives (it ends in Save()).
            var fresh = ScriptableObject.CreateInstance<GameState>();
            try
            {
                if (fresh.LastHarvestClaimMs > 0d)
                    failures.Add("[newgame-away-clock] a fresh GameState carries a NON-ZERO away anchor (" +
                                 fresh.LastHarvestClaimMs + ") - the coordinator's fresh-clock arm would not " +
                                 "fire and a brand-new town would claim a window it was never away for");
                if (fresh.EverBuiltStructureIds == null || fresh.EverBuiltStructureIds.Count != 0)
                    failures.Add("[newgame-away-clock] a fresh GameState's ever-built ledger is not EMPTY - the " +
                                 "harvest tick's existence gate would open for a building the town does not have " +
                                 "and HOLD its income forever");
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(fresh);
                else UnityEngine.Object.DestroyImmediate(fresh);
            }

            string svcSrc = ReadSrc(ServiceSrc);
            if (svcSrc.IndexOf("EverBuiltStructureIds = new List<string>()", StringComparison.Ordinal) < 0)
                failures.Add("[newgame-away-clock] B-state: ResetToNewGame no longer clears EverBuiltStructureIds - " +
                             "a new town would inherit the previous save's ever-built ledger and pay a building " +
                             "that does not exist");
            if (svcSrc.IndexOf("LastHarvestClaimMs = 0;", StringComparison.Ordinal) < 0)
                failures.Add("[newgame-away-clock] ResetToNewGame no longer zeroes the away anchor - the first claim " +
                             "of a new game would measure from the PREVIOUS save's stamp");

            // -- A: the parked away summary must be dropped on New Game.
            string off = ReadSrc(OfflineSrc);
            if (off.Length == 0)
                failures.Add("[newgame-away-clock] A: OfflineHarvestService.cs is MISSING");
            else
            {
                if (off.IndexOf("GameStateService.NewGameStarted +=", StringComparison.Ordinal) < 0)
                    failures.Add("[newgame-away-clock] A: OfflineHarvestService does not subscribe to NewGameStarted - " +
                                 "a welcome-back result parked on the Title screen (the 2026-09-04 hub deferral) is " +
                                 "released onto the NEW game by sceneLoaded. That is the 8h22m the owner saw on a " +
                                 "town seconds old");
                if (off.IndexOf("_deferredReveal = null", StringComparison.Ordinal) < 0)
                    failures.Add("[newgame-away-clock] A: nothing clears _deferredReveal - the parked reveal is the " +
                                 "state a New Game inherits");
                if (off.IndexOf("GameStateService.NewGameStarted -=", StringComparison.Ordinal) < 0)
                    failures.Add("[newgame-away-clock] A: the New Game subscription is never removed - the event's own " +
                                 "note says an INSTANCE handler that does not unsubscribe is held forever");
                // -- C: the reveal must not open over a tutorial beat awaiting its dialogue.
                if (off.IndexOf("TutorialFlow.IsAwaitingDialogue", StringComparison.Ordinal) < 0)
                    failures.Add("[newgame-away-clock] C: TryShowPopup does not consult TutorialFlow.IsAwaitingDialogue - " +
                                 "the modal covers the SKIP control (SKIP_TOP_HIT_BLOCKED top=ObsidianPanel " +
                                 "path=WelcomeBackUI/ObsidianPanel) and the founding beat then dies on its watchdog " +
                                 "(STEP-STUCK :: founding_greet), silently skipping the first-run tutorial");
            }

            string tut = ReadSrc(TutorialSrc);
            if (tut.IndexOf("AwaitedDialogueSignal", StringComparison.Ordinal) < 0)
                failures.Add("[newgame-away-clock] C: TutorialFlow no longer publishes AwaitedDialogueSignal - the " +
                             "popup has no way to know a beat is waiting on a dialogue");

            // -- B: the live half of the harvest tick.
            string harv = ReadSrc(HarvesterSrc);
            if (harv.Length == 0)
                failures.Add("[newgame-away-clock] B: ResourceBuildingHarvester.cs is MISSING");
            else
            {
                if (harv.IndexOf("GameStateService.NewGameStarted +=", StringComparison.Ordinal) < 0)
                    failures.Add("[newgame-away-clock] B: ResourceBuildingHarvester does not subscribe to " +
                                 "NewGameStarted - its per-id _elapsed carries a WO-1208 OWED interval and _lastGate " +
                                 "carries the previous town's gate verdict across Start New");
                if (harv.IndexOf("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) < 0)
                    failures.Add("[newgame-away-clock] B: the harvester's New Game hook is not self-installing - it " +
                                 "would depend on a harvester happening to be alive when Start New is pressed");
            }

            // -- INSTRUMENT (CLAUDE.md s12): the anchor and its provenance, every claim.
            string coord = ReadSrc(CoordSrc);
            if (coord.IndexOf("provenance=", StringComparison.Ordinal) < 0 ||
                coord.IndexOf("anchor=", StringComparison.Ordinal) < 0)
                failures.Add("[newgame-away-clock] the coordinator's window trace no longer names the ANCHOR and its " +
                             "PROVENANCE - that one line is what turns 'the window was 8h22m' into 'the anchor came " +
                             "from the previous save', and it must print even when the window is 0");
        }

        private static string ReadSrc(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;

        private static void Case6_KnownGaps(GameStateService.NewGamePrefStore[] stores, List<string> failures, List<string> notes)
        {
            if (stores == null) return;
            int gaps = 0;
            foreach (var s in stores)
            {
                if (s == null || s.Disposition != GameStateService.NewGamePrefDisposition.NotYetCleared) continue;
                gaps++;
                if (string.IsNullOrEmpty(s.Why))
                    failures.Add("[known-gaps] '" + s.Key + "' is an unaddressed store with no reason recorded");
            }
            if (gaps > 0)
                notes.Add(gaps + " KNOWN GAP(s) still inherited by a New Game (WO-1371 audit, deliberately not fixed " +
                          "in that pass) - each is a candidate ticket; move its row to ClearedByReset in the same " +
                          "change that clears it and this suite starts enforcing it");
        }
    }
}
