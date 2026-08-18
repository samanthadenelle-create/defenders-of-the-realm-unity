// =============================================================================
// RealmStoreReachability — proves the player can actually WALK to the storefront.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS AT ALL: PROD-003 moves the game's only monetization surface out
// of a dialogue menu and into the world. A storefront the player cannot reach is
// strictly worse than the dialogue option it replaced — the old one was buried,
// this one would be impossible. "It is in the scene" and "it is reachable" are
// different claims, and only the second one matters here.
//
// The suspicion that prompted it: after placing the store, the navmesh re-bake
// reported the SAME vertex and triangle counts as before (3716 / 1786). The bake
// definitely saw the new collider (rotated-collider count went 34 -> 35), so an
// unchanged navmesh means the store's footprint carved nothing — which is what
// you would expect if it sits on ground that was never walkable.
//
// Samples the navmesh around the storefront and reports the nearest walkable
// point and its distance. Close = reachable. Far (or none) = the placement needs
// moving, and better to learn that from a number than from a player.
// =============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    /// <summary>Navmesh reachability probe for the PROD-003 storefront.</summary>
    public static class RealmStoreReachability
    {
        private const string HubScene   = "Assets/Scenes/Main_Castle_Overworld.unity";
        private const string ObjectName = "RealmStore_Storefront";
        private const string OkMarker   = "REALM_STORE_REACHABLE_OK";

        [MenuItem("Defenders/World/Check Realm Store reachability")]
        public static void RunMenu() => Run();

        public static void Run()
        {
            if (!SceneManager.GetActiveScene().isLoaded ||
                SceneManager.GetActiveScene().path != HubScene)
            {
                EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            }

            Transform store = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == ObjectName) { store = t; break; }
                if (store != null) break;
            }

            if (store == null)
            {
                Debug.LogError($"[RealmStore] '{ObjectName}' not in the scene — run the placer first.");
                return;
            }

            Vector3 p = store.position;
            Debug.Log($"[RealmStore] storefront at {p}");

            // Sample outward. A shopper stands in FRONT of a shop, not inside it, so a hit a few
            // metres away is the expected healthy answer — the building itself is an obstacle.
            foreach (float radius in new[] { 2f, 5f, 10f, 20f, 40f })
            {
                if (NavMesh.SamplePosition(p, out var hit, radius, NavMesh.AllAreas))
                {
                    float d = Vector3.Distance(p, hit.position);
                    Debug.Log($"[RealmStore] nearest walkable point: {hit.position} — {d:F2} m from the storefront " +
                              $"(found within a {radius} m probe).");

                    if (d <= 12f)
                    {
                        Debug.Log($"{OkMarker} nearest walkable {d:F2}m");
                        return;
                    }

                    Debug.LogError($"[RealmStore] NEAREST WALKABLE GROUND IS {d:F2} m AWAY — the player cannot " +
                                   "reasonably reach this storefront. PROD-003 exists to make the store easy to " +
                                   "find; putting it somewhere unwalkable is a worse failure than the buried " +
                                   "dialogue option it replaced. MOVE THE PLACEMENT.");
                    return;
                }
            }

            Debug.LogError("[RealmStore] NO walkable navmesh within 40 m of the storefront — it is completely " +
                           "unreachable. Move the placement, then re-bake.");
        }
    }
}
