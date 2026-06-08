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
            if (!InVillageScene()) return;

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

        // True when the active scene is one of the playable towns (Village2 canonical,
        // Village3, Village). Keeps this off Title / HeroSelect / DTT / dungeons that
        // this RuntimeInitialize hook also fires in.
        private static bool InVillageScene()
        {
            string n = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(n) && n.StartsWith("Village");
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
            // Generator names the plane exactly "Ground"; tolerate a "(Clone)" suffix
            // and case just in case.
            string lower = n.ToLowerInvariant();
            return lower == "ground" || lower.StartsWith("ground");
        }
    }
}
