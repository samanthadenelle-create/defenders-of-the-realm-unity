// =============================================================================
// GroundZFightFixer — WO-333 (P0) runtime floor z-fighting fix (no rebake).
// -----------------------------------------------------------------------------
// SYMPTOM (owner playtest): a flickering / shimmering floor in the playable
// Village2 scene — the whole ground shimmers as the camera moves, with double
// (overlapping) colliders under the player.
//
// ROOT CAUSE: Village2's baked "Ground" plane sits at exactly Y=0, COPLANAR with
// the additively-loaded OuterWorld ExteriorTerrain (leveled flat to Y=0 across the
// village footprint by the seam weighting). Two opaque surfaces at the same depth
// → z-fighting flicker over the entire floor, plus two colliders stacked at Y=0.
//
// THE GENERATOR IS ALREADY FIXED — Village2Generator.CreateGroundPlane() now seats
// the plane at Y=-0.05 so the terrain wins the depth test. But that only takes
// effect on a FRESH village REGEN. The SHIPPED baked Village2.unity still has the
// ground at Y=0. This runtime component lowers the BAKED plane at load so the fix
// lands WITHOUT a rebake.
//
// WHAT THIS DOES (asset-independent, always lands):
//   On every village scene load, find the large (~140x140 m) plane named "Ground"
//   near the village centre at/around Y=0 and lower it to Y=-0.05. The OuterWorld
//   terrain then wins the depth test as the single visible floor; the plane stays
//   in place as the Build-Mode raycast collider (Build Mode positions its ghost by
//   raycasting this floor collider — so we keep the object, just drop it 5 cm).
//
// Mirrors the runtime-fixer pattern of TreeOfLifeMaterialFixer /
// EnvironmentTreeMaterialFixer:
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded
//     re-arm — the player boots into Title and reaches Village2 LATER, so a one-shot
//     check would miss the town; we re-run on every scene load.
//   • Village*-scene-gated — never touches Title / HeroSelect / DTT / dungeons.
//   • WEBGL-SAFE (WO-331): an uncaught exception in a sceneLoaded handler HALTS the
//     WebGL player, so every entry point is wrapped in try/catch. No File I/O, no
//     scene-mesh-ref dependency — just transform reads/writes.
//   • IDEMPOTENT: only lowers a plane that is still at/near Y=0 (within a small
//     epsilon); a plane already at -0.05 (regenerated town, or a second load) is
//     left untouched, so repeated loads never drift it further down.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    public static class GroundZFightFixer
    {
        // Target depth for the baked ground plane: 0.5 m below Y=0 so the coplanar
        // OuterWorld terrain wins the depth test EVERYWHERE. Matches the value
        // Village2Generator.CreateGroundPlane() now bakes.
        //   WO-333 update: a 5 cm drop was NOT enough — the terrain RENDER mesh is
        //   LOD-simplified (heightmapPixelError=4) so its visible surface deviates
        //   several cm from the flat heightmap; in patches it dipped below -0.05 and the
        //   plane poked through, giving the MOTTLED dark-grey/green CHECKERBOARD. -0.5 m
        //   clears all terrain render deviation so the terrain is the single visible floor.
        private const float TargetY = -0.5f;

        // CASTLE HUB target — DATA-PROVEN regression fix (2026-06-28, owner F8 "well/castle not
        // on ground, ground under y=0"). The hub plaza is the DESIGNED stone floor; it must WIN
        // the depth test, not yield. The old code sank the 26 CourtyardFloor tiles to TargetY
        // (-0.5) — correct for Village2 (its "Ground" plane should yield to nicer terrain) but
        // BACKWARDS for the castle. Player.log proved the mover: "[GroundZFightFixer] hub —
        // lowered 26 CourtyardFloor tiles to Y=-0.5". The sink is old, but f3ef39f9 re-centered
        // OuterWorld terrain to origin + re-baked the navmesh to y=0.06, raising props (~0) and
        // the hero (0.06) above the still-sunk plaza → the 0.5m float. Seat the plaza a hair
        // above origin so it sits at prop/hero level (no float) and still wins over any coplanar
        // terrain at y=0. Authored near-zero tiles are left alone by the idempotency guard.
        private const float HubTargetY = 0.02f;

        // Only lower a plane that is still ABOVE the target (a baked plane at Y=0 OR the
        // earlier -0.05 fix). A plane already at/below the target (regenerated town, or a
        // re-load after we lowered it) is skipped — keeps the fix idempotent and prevents
        // drift on repeated loads.
        private const float NearZeroEpsilon = 0.01f;

        // How far (XZ) from the village centre the plane's centre may sit and still
        // count as THE ground plane. The baked plane is centred on origin; this is a
        // generous guard so we never grab some unrelated small plane elsewhere.
        private const float CentreRadius = 30f;

        // Minimum world footprint (per axis) to qualify as the big ground plane and
        // not a small decorative quad. The real ground spans ~140 m; require >=60 m.
        private const float MinFootprint = 60f;

        /// <summary>
        /// Registrar. Runs once at app start, then re-runs on EVERY scene load — the
        /// player boots into Title and reaches Village2 LATER, so a one-shot check
        /// would miss the town. Idempotent per load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // Also fix the scene already active at app start.
            SafeFix();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            SafeFix();
        }

        // WO-331: never let the ground fix throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeFix()
        {
            try { FixGroundPlane(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GroundZFightFixer] ground fix threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Lower the baked Village2 ground plane to Y=-0.05 so the OuterWorld terrain
        /// is the single visible floor. Public so a generator or test can call it.
        /// No-op outside a village scene, or when the plane is already lowered.
        /// </summary>
        public static void FixGroundPlane()
        {
            // GARRISON / OUTPOST path FIRST, and UNGATED by the active scene.
            // The outpost (Garrison_troll_outpost / _frost_keep / _ruined_keep / hill_fort)
            // loads ADDITIVELY over the hub/OuterWorld and is NEVER SetActive'd, so
            // GetActiveScene() still reports "MainCastle_*" / "Village*" while the outpost
            // floor is live. InFixableScene()/InHubScene() therefore can never route to it.
            // Run the Garrison pass whenever ANY Garrison_* scene is loaded (cheap +
            // idempotent), independent of which scene is active.
            if (AnyGarrisonSceneLoaded())
            {
                FixGarrisonFloor();
                // Do NOT return — the active hub/village may ALSO need its own floor fix
                // while the outpost is additively layered on top.
            }

            if (!InFixableScene()) return;

            // CASTLE HUB path: the plaza floor is a 5x5 GRID of small tiles named
            // "CourtyardFloor_{x}_{z}" (~8 m each) at Y=0.01, coplanar with the
            // additively-loaded OuterWorld terrain at Y=0 → flashing floor. The big
            // single-plane logic below never matches these (no >60m footprint, and the
            // hub has no "Ground" plane), so the hub gets its OWN pass.
            if (InHubScene())
            {
                FixHubFloorTiles();
                return;
            }

            GameObject ground = FindBakedGroundPlane();
            if (ground == null) return;

            float y = ground.transform.position.y;
            // IDEMPOTENT: only lower a plane still ABOVE the target (baked Y=0, or the
            // earlier -0.05 fix). Already-lowered planes (regen / re-load) are at/below
            // the target → skip.
            if (y > TargetY + NearZeroEpsilon)
            {
                Vector3 p = ground.transform.position;
                p.y = TargetY;
                ground.transform.position = p;
                Debug.Log("[GroundZFightFixer] WO-333 — lowered baked '" + ground.name +
                          "' plane from Y=" + y.ToString("0.###") + " to Y=" + TargetY +
                          " so the OuterWorld terrain wins the depth test (no rebake; " +
                          "plane kept as the Build-Mode raycast collider).");
            }
        }

        // Castle-hub pass: find ALL plaza floor tiles (CourtyardFloor_* / qFloorWood*)
        // and lower EACH still-high tile to TargetY so the OuterWorld terrain wins the
        // depth test. Idempotent: a tile already at/below TargetY (re-load) is skipped,
        // so repeated loads never drift it down. Small tiles → no >60m footprint guard.
        private static void FixHubFloorTiles()
        {
            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int lowered = 0;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsHubFloorTile(mr.name)) continue;

                Transform t = mr.transform;
                float y = t.position.y;
                // SEAT the hub plaza at HubTargetY (~0) so it reads as the floor at prop/hero
                // level. Only MOVE a tile meaningfully off-target (the old -0.5 sink, or an
                // outlier); a tile already within epsilon of HubTargetY is left alone.
                if (Mathf.Abs(y - HubTargetY) > NearZeroEpsilon)
                {
                    Vector3 p = t.position;
                    p.y = HubTargetY;
                    t.position = p;
                    lowered++;
                }
            }
            if (lowered > 0)
            {
                Debug.Log("[GroundZFightFixer] hub — seated " + lowered +
                          " CourtyardFloor tiles to Y=" + HubTargetY +
                          " (plaza is the castle floor; wins over coplanar terrain; no float).");
            }
        }

        private static bool NameIsHubFloorTile(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            return lower.StartsWith("courtyardfloor") || lower.StartsWith("qfloorwood");
        }

        // True when ANY Garrison_* scene is currently loaded (additively or single).
        // The raid outpost is loaded additively over the hub/OuterWorld and is NEVER
        // SetActive'd, so GetActiveScene() never reports it — we must walk the loaded
        // scene list instead of asking for the active one (the "additive load never
        // becomes active" trap).
        private static bool AnyGarrisonSceneLoaded()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && !string.IsNullOrEmpty(s.name) && s.name.StartsWith("Garrison"))
                    return true;
            }
            return false;
        }

        // GARRISON / OUTPOST pass: the outpost floor is a SINGLE large Plane named
        // "GarrisonGround" (open camps) or "DungeonFloor" (caves), built at Y=0 by
        // GarrisonSceneBuilder.BuildGroundOrFloor — coplanar with the additively-loaded
        // OuterWorld terrain at Y=0 → the SAME z-fighting flicker the hub had. The big
        // single-plane finder below cannot catch it: (a) the active scene is the hub, not
        // the outpost, so it never routes here, and (b) the outpost plane footprint is
        // ~48-56 m (medium half=16 / large half=20), BELOW the 60 m MinFootprint guard.
        // So this dedicated pass matches by NAME and lowers EACH still-high outpost floor
        // to TargetY so the terrain wins the depth test. Idempotent: a floor already
        // at/below TargetY (re-load) is skipped, so repeated loads never drift it down.
        private static void FixGarrisonFloor()
        {
            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int lowered = 0;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsGarrisonFloor(mr.name)) continue;

                Transform t = mr.transform;
                float y = t.position.y;
                // IDEMPOTENT: only lower a floor still ABOVE the target.
                if (y > TargetY + NearZeroEpsilon)
                {
                    Vector3 p = t.position;
                    p.y = TargetY;
                    t.position = p;
                    lowered++;
                }
            }
            if (lowered > 0)
            {
                string scene = ActiveGarrisonSceneName();
                FlowTrace.Step("GroundZFightFixer",
                    "Garrison " + scene + " — offset " + lowered + " floor tiles to Y=" +
                    TargetY + " so the OuterWorld terrain wins the depth test (no rebake).");
            }
        }

        // Match the GarrisonSceneBuilder floor objects: open camps build "GarrisonGround";
        // dungeon/cave recipes build "DungeonFloor". Tolerate a "(Clone)" suffix + case.
        private static bool NameIsGarrisonFloor(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            return lower.StartsWith("garrisonground") || lower.StartsWith("dungeonfloor");
        }

        // The name of the first loaded Garrison_* scene (for the FlowTrace proof line).
        private static string ActiveGarrisonSceneName()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && !string.IsNullOrEmpty(s.name) && s.name.StartsWith("Garrison"))
                    return s.name;
            }
            return "Garrison_?";
        }

        // True when the active scene is fixable by this component: one of the playable
        // towns (Village2 canonical, Village3, Village), the castle hub
        // (MainCastle_Hall / any Castle* scene), OR the garrison troll outpost
        // (Garrison_troll_outpost / any Garrison* scene). Keeps this off Title /
        // HeroSelect / DTT / dungeons that this RuntimeInitialize hook also fires in.
        private static bool InFixableScene()
        {
            string n = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(n)) return false;
            return n.StartsWith("Village") || n.StartsWith("MainCastle") ||
                   n.StartsWith("Castle") || n.StartsWith("Garrison");
        }

        // True when the active scene is the castle hub (grid-tile plaza), which uses the
        // separate FixHubFloorTiles pass rather than the single-big-plane Village logic.
        private static bool InHubScene()
        {
            string n = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(n) && (n.StartsWith("MainCastle") || n.StartsWith("Castle"));
        }

        // Find the large ground plane named "Ground" centred near the village origin at
        // or around Y=0. Matches by NAME + SIZE + CENTRE so we never grab an unrelated
        // small plane. Uses renderer bounds (visible footprint), not transform values,
        // so a scaled primitive Plane is measured correctly.
        private static GameObject FindBakedGroundPlane()
        {
            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsGround(mr.name)) continue;

                Bounds b = mr.bounds;
                // Big enough to be THE floor, not a decorative quad.
                if (b.size.x < MinFootprint || b.size.z < MinFootprint) continue;
                // Centred near the village origin on XZ.
                float d = new Vector2(b.center.x, b.center.z).sqrMagnitude;
                if (d > CentreRadius * CentreRadius) continue;
                // Roughly flat near Y=0 (don't grab a wall or elevated platform).
                if (Mathf.Abs(b.center.y) > 2f) continue;

                if (d < bestDist) { bestDist = d; best = mr.gameObject; }
            }
            return best;
        }

        private static bool NameIsGround(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            // Village generator names the plane exactly "Ground". (The GARRISON outpost
            // floor "GarrisonGround"/"DungeonFloor" is handled by the dedicated
            // FixGarrisonFloor pass instead — its ~48-56 m footprint is below this
            // finder's 60 m MinFootprint guard and the outpost is never the active scene.)
            // Tolerate a "(Clone)" suffix and case just in case.
            string lower = n.ToLowerInvariant();
            return lower == "ground" || lower.StartsWith("ground");
        }
    }
}
