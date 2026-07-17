// =============================================================================
// ScrappedDecorRemover - runtime removal of VALUELESS baked hub decor, WITHOUT a
// scene rebuild (CLAUDE.md Section 3: never hand-edit the .unity YAML).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER RULING (2026-07-16, felt-test "WELL STILL WHITE"): the decorative WELL in
// the castle hub "offers no value" and was scrapped ("if easier can scrap it").
// The well is a BAKED prefab-instance named "Well" in Main_Castle_Overworld (part
// of the old fresh-start "tree + well + walls/gates" set - the well is now dropped
// from that set). It renders pure-white in the built player (its polyperfect
// Standard-shader materials strip under URP), and it carries no gameplay behaviour,
// so rather than recover the material we simply delete it.
//
// Runs on every hub load (mirrors HubStructureVisualInjector / MagentaGuard):
// [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded re-arm
// (the player boots into Title and reaches the hub later). Finds the baked node by
// EXACT name "Well" and destroys it. Exact-name is safe: player-placeable
// well-mesh structures are named by their catalog displayName ("Crystal Mine" /
// "Wellspring of Elarion"), never "Well" - only the scrapped decorative prop is.
// Idempotent (a destroyed well is simply not found on later loads) + guarded (an
// uncaught sceneLoaded exception would halt the WebGL player).
//
// TO SCRAP ANOTHER VALUELESS BAKED PROP: add its exact name to ScrappedNames below.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Deletes scrapped, valueless baked hub decor (e.g. the Well) at runtime.</summary>
    public static class ScrappedDecorRemover
    {
        // Exact GameObject names of baked hub decor the owner has scrapped.
        private static readonly string[] ScrappedNames = { "Well" };

        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // sceneLoaded does NOT fire for the scene already active at boot - sweep it now.
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Sweep("boot");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) Sweep(scene.name);
        }

        private static void Sweep(string sceneName)
        {
            try
            {
                int removed = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t == null) continue;
                    if (!IsScrapped(t.name)) continue;
                    FlowTrace.Step("Hub",
                        "ScrappedDecorRemover: deleting scrapped decor '" + t.name + "' (scene '" +
                        sceneName + "') - owner ruling: valueless, removed from fresh-start set.");
                    Object.Destroy(t.gameObject);
                    removed++;
                }
                if (removed > 0)
                    FlowTrace.Step("Hub", "ScrappedDecorRemover: removed " + removed + " scrapped prop(s) in '" + sceneName + "'.");
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("Hub", "ScrappedDecorRemover sweep '" + sceneName + "' threw: " + e.Message);
            }
        }

        private static bool IsScrapped(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < ScrappedNames.Length; i++)
                if (name == ScrappedNames[i]) return true;
            return false;
        }
    }
}
