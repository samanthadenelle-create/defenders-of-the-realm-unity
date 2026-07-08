// =============================================================================
// SceneRoutingRegression — the DATA-decidable half of scene navigation, proven
// headless in SECONDS (no scene drive, no play mode). ZERO oracle coverage before
// this file (scenes area).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village),
// so it reads the REAL route table (DeNelle.Core.SceneRouter) and the REAL editor
// build list (UnityEditor.EditorBuildSettings) — not a re-derivation.
//
// WHAT IT PROVES (all data-decidable, no running scene):
//   1. Every LOAD-BEARING SceneRouter route const (the typed GoX entry points a
//      player actually reaches now) resolves to a scene that is ENABLED in
//      EditorBuildSettings. A missing one is a runtime STRAND: SceneRouter.LoadScene /
//      LoadSceneWithFade call IsSceneRegistered (Application.CanStreamedLevelBeLoaded,
//      which only sees ENABLED build scenes) and ABORT with a LogError, so the player
//      is stuck on the current scene. We mirror that exact gate (enabled scenes only).
//   2. SceneRouter.Castle is FLAG-AWARE (WO-608): a property that resolves to
//      'Main_Castle_Overworld' when ff.MergedWorld is ON and 'MainCastle_Hall' when
//      OFF. BOTH resolutions must be registered — otherwise flipping the flag strands
//      the player at the home hub (GoCastle -> LoadSceneWithFade abort). We drive the
//      REAL property under BOTH PlayerPrefs states and restore the pref in finally.
//   3. Village.unity (the ABANDONED village scene, PIPELINE_STATE §8) is EXCLUDED —
//      only 'Village2' (SceneRouter.Village) ships. A bare 'Village' re-entering the
//      build list would be a canon regression (retired scene resurrected).
//
// HONEST scope (NOT hard-failed here): the WEEKS-2+ stubbed dungeon scenes and the
// removed PatriciaLight route are reported as NOTES, never failures — canon
// (SceneRouter doc-comments + PIPELINE_STATE) documents them as intentionally
// unbuilt/removed, so failing on them would contradict settled design. The NOTE still
// surfaces a DANGLING route (a public GoX whose scene isn't registered) for triage.
//
// Contract mirrors MonetizationCovenantRegression.Run(out string reason):
//   true  = pass  (reason = one-line summary)
//   false = fail  (reason = exact missing route / bad Castle resolution / resurrected Village)
//
// Orchestrator (DataRegression.RunAll) registers it covenant-style:
//   if (!SceneRoutingRegression.Run(out var sceneRouteReason)) failures.Add(sceneRouteReason); else log.AppendLine("[scene-routing] " + sceneRouteReason);
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Editor
{
    public static class SceneRoutingRegression
    {
        // The abandoned village scene name (PIPELINE_STATE §8: "Village.unity abandoned").
        // Must NEVER be an enabled build scene — only Village2 (SceneRouter.Village) ships.
        private const string AbandonedVillageScene = "Village";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SCENE ROUTING (route consts registered + Castle flag both-states + Village.unity excluded) ---");

            // Build the set of ENABLED build-scene names — the exact set the runtime gate
            // (Application.CanStreamedLevelBeLoaded) can load. Disabled entries are ignored
            // so we mirror IsSceneRegistered, not the raw list.
            var enabled = new HashSet<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s == null || !s.enabled || string.IsNullOrEmpty(s.path)) continue;
                enabled.Add(Path.GetFileNameWithoutExtension(s.path));
            }
            log.AppendLine($"enabled build scenes ({enabled.Count}) = {{ {string.Join(", ", enabled)} }}");

            // (1) LOAD-BEARING routes — the typed GoX entry points reachable on the live
            //     path today. Each MUST be an enabled build scene or LoadScene aborts.
            var loadBearing = new (string name, string via)[]
            {
                (SceneRouter.Title,                     "GoTitle"),
                (SceneRouter.HeroSelect,                "GoHeroSelect"),
                (SceneRouter.PetSelect,                 "GoPetSelect"),
                (SceneRouter.Village,                   "GoVillage"),
                (SceneRouter.ATBBattle,                 "GoBattle"),
                (SceneRouter.DungeonHealersCottage,     "GoDungeon (Week-1 ship)"),
                (SceneRouter.RaidBaseRaiderCampSmall,   "GoRaid"),
                (SceneRouter.RaidBaseFortifiedGarrison, "GoRaid"),
                (SceneRouter.RaidBaseMageEnclave,       "GoRaid"),
            };
            foreach (var (name, via) in loadBearing)
            {
                if (string.IsNullOrEmpty(name))
                    failures.Add($"load-bearing route via {via} resolved to a null/empty const (route table broke)");
                else if (!enabled.Contains(name))
                    failures.Add($"load-bearing route '{name}' (via {via}) is NOT an enabled build scene — {via} will abort (LogError) and STRAND the player");
                else
                    log.AppendLine($"OK: '{name}' registered (via {via})");
            }

            // (2) Castle flag-resolution for BOTH MergedWorld states. Drive the REAL
            //     flag-aware property; both resolved scenes must be registered.
            int priorPref = PlayerPrefs.GetInt("ff.mergedworld", -1);
            try
            {
                // MergedWorld ON -> expect 'Main_Castle_Overworld'.
                PlayerPrefs.SetInt("ff.mergedworld", 1);
                bool onFlag = FeatureFlags.MergedWorld;
                string castleOn = SceneRouter.Castle;
                log.AppendLine($"Castle @ MergedWorld=ON  ({onFlag}) -> '{castleOn}'");
                if (!onFlag)
                    failures.Add("FeatureFlags.MergedWorld did NOT honor PlayerPrefs ff.mergedworld=1 (flag read broke) — Castle resolution untestable");
                if (string.IsNullOrEmpty(castleOn) || !enabled.Contains(castleOn))
                    failures.Add($"Castle (MergedWorld=ON) resolves to '{castleOn}' which is NOT an enabled build scene — GoCastle aborts, player stranded when merged-world is on");

                // MergedWorld OFF -> expect legacy 'MainCastle_Hall'.
                PlayerPrefs.SetInt("ff.mergedworld", 0);
                bool offFlag = FeatureFlags.MergedWorld;
                string castleOff = SceneRouter.Castle;
                log.AppendLine($"Castle @ MergedWorld=OFF ({offFlag}) -> '{castleOff}'");
                if (offFlag)
                    failures.Add("FeatureFlags.MergedWorld did NOT honor PlayerPrefs ff.mergedworld=0 (flag read broke) — Castle resolution untestable");
                if (string.IsNullOrEmpty(castleOff) || !enabled.Contains(castleOff))
                    failures.Add($"Castle (MergedWorld=OFF) resolves to '{castleOff}' which is NOT an enabled build scene — GoCastle aborts, player stranded when the legacy two-scene hub is selected");

                // Both resolutions must also DIFFER (the flag must actually switch scenes).
                if (!string.IsNullOrEmpty(castleOn) && castleOn == castleOff)
                    failures.Add($"Castle resolves to the SAME scene '{castleOn}' for both MergedWorld states — the flag no longer switches the home hub");
            }
            finally
            {
                // Restore the pref exactly (–1 = absent = use default).
                if (priorPref == -1) PlayerPrefs.DeleteKey("ff.mergedworld");
                else PlayerPrefs.SetInt("ff.mergedworld", priorPref);
                PlayerPrefs.Save();
            }

            // (3) Village.unity (abandoned) must be EXCLUDED; Village2 must be present.
            if (enabled.Contains(AbandonedVillageScene))
                failures.Add($"abandoned '{AbandonedVillageScene}.unity' (PIPELINE_STATE §8) is an enabled build scene — the retired village was resurrected; only '{SceneRouter.Village}' should ship");
            else
                log.AppendLine($"OK: abandoned '{AbandonedVillageScene}.unity' correctly EXCLUDED from the build (only '{SceneRouter.Village}' ships)");

            // NOTES (never failures): stubbed/removed routes whose scene isn't registered.
            // Canon documents these as intentionally unbuilt (Weeks 2+) or removed (PatriciaLight).
            var deferred = new (string name, string note)[]
            {
                (SceneRouter.PatriciaLight,        "removed route (only Resources/PatriciaLight/tower2 kept) — GoPatriciaLight is DANGLING"),
                (SceneRouter.DungeonFolksGranary,  "Week-2+ dungeon"),
                (SceneRouter.DungeonSunkenBellTower,  "Week-2+ dungeon (stubbed)"),
                (SceneRouter.DungeonWolfwardensVigil, "Week-2+ dungeon (stubbed)"),
                (SceneRouter.DungeonFrostStair,       "Week-2+ dungeon (stubbed)"),
                (SceneRouter.DungeonGlassCathedral,   "Week-2+ dungeon (stubbed)"),
                (SceneRouter.DungeonApothecarysVault, "Week-2+ dungeon (stubbed)"),
            };
            foreach (var (name, note) in deferred)
            {
                bool present = !string.IsNullOrEmpty(name) && enabled.Contains(name);
                log.AppendLine($"NOTE: route '{name}' registered={present} — {note}");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "SCENE_ROUTING_OK");
                reason = $"SCENE ROUTING OK — {loadBearing.Length} load-bearing routes registered, Castle resolves+registers for both MergedWorld states, '{AbandonedVillageScene}.unity' excluded";
                return true;
            }

            reason = "scene-routing: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "SCENE_ROUTING_FAIL: " + reason);
            return false;
        }
    }
}
