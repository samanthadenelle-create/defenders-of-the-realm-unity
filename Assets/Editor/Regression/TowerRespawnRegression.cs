// =============================================================================
// TowerRespawnRegression — F8-39 "towers vanish on death, ALL return on next
// placement." Headless, no-scene, no-PlayMode oracle that proves the DATA/LOGIC
// contradiction in the base-layout replay decision in SECONDS.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core), so
// it reads the REAL code seams the runtime consults — not a re-derivation.
//
// THE BUG (RCA from the instrumentation pass, F8-39):
//   • The player's base is BUILT and COMMITTED in the home hub scene MainCastle_Hall
//     (BuildModeController.CommitLayout: "MainCastle_Hall is the HOME hub"), and it
//     persists into GameState.BaseLayout (a single GLOBAL list, N records).
//   • On an enemy-death evacuation the game reloads the hub via SceneRouter.GoCastle()
//     -> a FRESH BaseLayoutLoader is spun up, whose LoadFromState() decides the replay.
//   • LoadFromState()'s HUB-SCOPE GUARD skips the replay for any scene in the private
//     _hubScenesNoBaseLayout set. When the base's OWN home scene is IN that skip set, the
//     persisted BaseLayout of N is NEVER re-instantiated -> the towers vanish. A later
//     placement runs a full refresh and they all reappear at once. (The set is read live by
//     reflection below; it is deliberately NOT restated here, because a copied membership
//     list in a comment is the same staleness that put the retired hub name in this file.)
//
// WO-1112: the home hub is now RESOLVED from SceneRouter.CastleCandidates and ASSERTED for
// BOTH ff.MergedWorld branches. It used to be the literal "MainCastle_Hall" -- the LEGACY
// scene -- with SceneRouter.Castle merely logged, so this guard watched a scene the player
// does not boot into and could print TOWER_RESPAWN_OK while towers vanished for real.
//
// The contradiction is fully data-decidable WITHOUT a scene: it is the intersection of
// three real seams — (1) HubScenes.IsHub(home) is true, (2) home == where the base is
// built (SceneRouter.Castle / MainCastle_Hall), (3) the loader's skip predicate (the
// real private _hubScenesNoBaseLayout set, read by reflection — the SAME set the guard
// consults at line `if (_hubScenesNoBaseLayout.Contains(scene)) return;`) CONTAINS the
// home scene. When all three hold, a persisted BaseLayout of N replays to 0 on reload.
//
// We can't force SceneManager.GetActiveScene().name headless, so we DON'T load a scene:
// we read the loader's ACTUAL decision set and evaluate its Contains() the way the guard
// does, against a REAL GameState.BaseLayout of N>0 PlacedStructureData records — proving
// replayedCount(0) != persistedCount(N) from the real predicate, not a guess.
//
// Wire into the suite from DataRegression.RunAll (one line — see the return notes):
//   if (!TowerRespawnRegression.Run(out var towerRespawnReason)) failures.Add(towerRespawnReason); else log.AppendLine("[tower-respawn] " + towerRespawnReason);
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class TowerRespawnRegression
    {
        // WO-1112 — RESOLVED, never a literal, and ASSERTED, never logged.
        //
        // This was `const string HomeHubScene = "MainCastle_Hall"` — the LEGACY hub file. The
        // oracle DID read SceneRouter.Castle (below) but only AppendLine'd it, so every check,
        // including the F8-39 contradiction itself, was evaluated against a scene the player no
        // longer boots into. Towers could vanish on a death->hub reload while TOWER_RESPAWN_OK
        // printed, and BaseLayoutLoader._hubScenesNoBaseLayout gaining the LIVE hub would have
        // blinded the guard completely rather than failing it.
        //
        // SceneRouter.CastleCandidates is checked rather than SceneRouter.Castle alone because
        // Castle is ff.MergedWorld-dependent: asserting only the resolved value proves the branch
        // the gate machine happens to be flagged into, and the OTHER branch is exactly where the
        // stale literal was hiding. Both must hold; the live one is named in the failure text.
        private static string[] HomeHubScenes => SceneRouter.CastleCandidates;

        // Number of persisted structures to simulate (N>0). Any N>0 exercises the bug.
        private const int PersistedCount = 4;

        /// <summary>
        /// Proves (or, once fixed, disproves) the F8-39 contradiction. Returns true on PASS
        /// (the persisted base DOES replay on hub reload); false + a reason naming the defect
        /// when the persisted BaseLayout of N is skipped in the very hub it is built in.
        /// Deterministic, self-contained, no scene / no PlayMode. Cleans up all throwaway state.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- TOWER RESPAWN (F8-39: base replay on hub reload) ---");

            GameState throwaway = null;
            try
            {
                // (0) The candidate set must be non-empty, or every loop below is a HOLLOW PASS
                //     that finds zero targets and reports OK. Zero targets = FAIL, always.
                string[] homeHubs = HomeHubScenes;
                string liveCastle = SceneRouter.Castle;
                if (homeHubs == null || homeHubs.Length == 0)
                {
                    failures.Add("SceneRouter.CastleCandidates is empty — this oracle would assert against NOTHING " +
                                 "and still report TOWER_RESPAWN_OK. Zero targets is a failure, not a pass.");
                    reason = Finish(failures, log);
                    return failures.Count == 0;
                }
                log.AppendLine($"home hub candidates = {{ {string.Join(", ", homeHubs)} }}; " +
                               $"SceneRouter.Castle (LIVE, ff.MergedWorld-resolved) = '{liveCastle}'");

                // The resolved live hub must be one of the candidates, or CastleCandidates has
                // drifted from Castle and this oracle is testing scenes nothing boots into —
                // which is the exact defect (a gate watching a retired scene) it now guards.
                if (System.Array.IndexOf(homeHubs, liveCastle) < 0)
                    failures.Add($"SceneRouter.Castle resolves to '{liveCastle}' which is NOT in SceneRouter.CastleCandidates " +
                                 $"{{ {string.Join(", ", homeHubs)} }} — the candidate list drifted from the live property, " +
                                 "so this oracle (and every gate built on it) is watching a scene the player never loads.");

                // (1) Is each home hub a HUB? (the premise the guard rests on) -----------------
                foreach (var hub in homeHubs)
                {
                    bool isHub = HubScenes.IsHub(hub);
                    log.AppendLine($"HubScenes.IsHub('{hub}') = {isHub}");
                    if (!isHub)
                        failures.Add($"premise broken: HubScenes.IsHub('{hub}') is FALSE — a scene SceneRouter.Castle can " +
                                     "resolve to is not recognised as a hub (HubScenes.Names and SceneRouter have drifted apart)");
                }

                // (3) The loader's ACTUAL replay-skip predicate: the private static
                //     _hubScenesNoBaseLayout set that LoadFromState consults at
                //     `if (_hubScenesNoBaseLayout.Contains(scene)) return;`. Read by reflection
                //     so we test the REAL decision, not a re-derived copy.
                var skipField = typeof(BaseLayoutLoader).GetField(
                    "_hubScenesNoBaseLayout", BindingFlags.NonPublic | BindingFlags.Static);
                var skipSet = skipField != null ? skipField.GetValue(null) as HashSet<string> : null;

                if (skipField == null || skipSet == null)
                {
                    // The seam moved — fail loud rather than silently pass a vacuous test.
                    failures.Add("could not read BaseLayoutLoader._hubScenesNoBaseLayout (private skip set) by reflection — " +
                                 "the loader's replay-skip seam was renamed/removed; this oracle can no longer prove F8-39 (re-point it)");
                    reason = Finish(failures, log);
                    return failures.Count == 0;
                }

                log.AppendLine($"BaseLayoutLoader._hubScenesNoBaseLayout = {{ {string.Join(", ", skipSet)} }}");

                // Build a REAL persisted base of N>0 records in a real GameState (the exact
                // field + record types the runtime persists). Proves the data survives.
                throwaway = ScriptableObject.CreateInstance<GameState>();
                throwaway.BaseLayout = new List<PlacedStructureData>();
                for (int i = 0; i < PersistedCount; i++)
                    throwaway.BaseLayout.Add(new PlacedStructureData($"tower_ground_archer", i, 0, 0, 1));
                int persisted = throwaway.BaseLayout.Count;

                // Evaluate the loader's decision the SAME way LoadFromState does, for EVERY scene
                // SceneRouter.Castle can resolve to: skip replay when the (home) scene is in the
                // set. replayedCount is what the base rebuilds to.
                foreach (var hub in homeHubs)
                {
                    bool isLive = string.Equals(hub, liveCastle, System.StringComparison.Ordinal);
                    bool willSkipReplay = skipSet.Contains(hub);
                    int replayedCount = willSkipReplay ? 0 : persisted;

                    log.AppendLine($"persisted BaseLayout = {persisted} record(s); " +
                                   $"skipSet.Contains('{hub}') = {willSkipReplay}" +
                                   (isLive ? " [LIVE hub]" : " [alternate ff.MergedWorld branch]") +
                                   $"; => replayedCount on hub reload = {replayedCount}");

                    // THE CONTRADICTION: home hub is a hub (1) AND the base is built there (2) AND
                    // that very scene is in the loader's skip set (3) => a persisted base of N
                    // replays to 0 on reload. That IS F8-39.
                    if (willSkipReplay && persisted > 0 && replayedCount == 0)
                    {
                        failures.Add(
                            $"F8-39: persisted BaseLayout of {persisted} in the home hub scene ('{hub}'" +
                            (isLive ? ", the LIVE hub SceneRouter.Castle resolves to" : ", the alternate ff.MergedWorld branch") +
                            $", a hub the base is BUILT/committed in) is NEVER replayed on reload — " +
                            $"BaseLayoutLoader.LoadFromState's hub-scope guard (_hubScenesNoBaseLayout.Contains('{hub}')==true) " +
                            $"SKIPS the replay, so the towers vanish on the death->GoCastle() hub reload (a later placement's " +
                            $"full refresh re-adds all {persisted} at once).");
                    }
                    else
                    {
                        log.AppendLine($"OK: '{hub}' is NOT skipped by the loader — the persisted base replays " +
                                       $"({replayedCount}/{persisted}) on hub reload (F8-39 fixed / not reproduced).");
                    }
                }
            }
            finally
            {
                // Never leak the throwaway ScriptableObject (mirror CheckEnemyStructureSweep cleanup).
                if (throwaway != null) Object.DestroyImmediate(throwaway);
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "TOWER_RESPAWN_OK");
                return "TOWER RESPAWN OK — persisted BaseLayout replays on hub reload (F8-39 not reproduced)";
            }
            string reason = "tower-respawn: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "TOWER_RESPAWN_FAIL: " + reason);
            return reason;
        }
    }
}
