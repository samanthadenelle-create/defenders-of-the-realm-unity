// =============================================================================
// ArenaScenery -- SURGICAL visual pass on the owner's HAND-DRESSED arena prefab.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)  Namespace: DeNelle.Editor
//
// Two felt-test fixes, applied surgically (LoadPrefabContents -> modify -> save),
// so the owner's hand-added "Design" group + EdgeProps + lights + ReflectionProbe
// are preserved byte-for-byte:
//
//   1) MUTE THE GRASS. The play-area "Ground" reads as neon green. We create (once)
//      a dedicated SERIALIZABLE material asset Assets/Resources/Arena/Materials/
//      ArenaGround.mat (URP/Lit, a muted natural green, emission off) and assign it
//      to the Ground MeshRenderer.sharedMaterial. We touch ONLY the material -- the
//      MeshCollider / Default layer / geometry stay intact so navmesh still bakes.
//      (Asset, NOT a runtime `new Material(...)` -- that was the magenta bug.)
//
//   2) ADD A BACKGROUND LANDSCAPE. A single child group "BackgroundLandscape" under
//      the root: a large distant ground plane (no collider, slightly lower Y) so the
//      horizon reads as continuous land, plus a SPARSE ring of distant trees well
//      beyond the arena frame (no colliders -- pure backdrop). One toggle to tune/hide.
//
// IDEMPOTENT: re-running re-points the Ground material (harmless) and SKIPS rebuilding
// BackgroundLandscape if it already exists -- never duplicates children.
//
// NOTE: the green shade, ring radius, tree count + scale are FELT values the owner
// will dial after -- they live as clearly-named consts at the top.
//
//   Defenders > Arena > Apply Scenery
//   (batchmode: DeNelle.Editor.ArenaScenery.Apply)
//   Prints marker: ARENA_SCENERY_OK :: <path>
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ArenaScenery
    {
        private const string PrefabPath   = "Assets/Resources/Arena/ForestClearingArena.prefab";
        private const string MatFolder    = "Assets/Resources/Arena/Materials";
        private const string GroundMat    = "Assets/Resources/Arena/Materials/ArenaGround.mat";
        private const string BackdropName = "BackgroundLandscape";

        // ---- FELT TUNABLES (owner dials these after) --------------------------
        // Muted natural green -- NOT neon. Linear-ish base color for URP/Lit.
        private static readonly Color GroundColor = new Color(0.28f, 0.40f, 0.22f, 1f);
        // Tiling on the play-area ground (slight repeat so it doesn't read flat).
        private static readonly Vector2 GroundTiling = new Vector2(4f, 4f);

        // Distant backdrop ground plane (built-in 10x10 plane scaled to ~300m).
        private const float BackdropPlaneSize = 300f; // world metres edge-to-edge
        private const float BackdropPlaneY    = -0.2f; // slightly LOWER than play ground

        // Sparse ring of distant trees -- low density, simple, not a forest wall.
        private const int   TreeCount      = 14;    // ~12-16
        private const float RingRadiusMin  = 80f;   // well beyond the ~36m arena frame
        private const float RingRadiusMax  = 115f;
        private const float TreeScaleMin   = 1.5f;
        private const float TreeScaleMax   = 2.5f;
        private const int   RingSeed       = 8675309; // deterministic placement
        // -----------------------------------------------------------------------

        private static readonly string[] TreeAssets =
        {
            "Assets/Resources/Arena/Tree_2_A_Color1.fbx",
            "Assets/Resources/Arena/Tree_5_C_Color1.fbx",
            "Assets/Resources/Arena/Tree_7_A_Color1.fbx",
            "Assets/Resources/Arena/Tree_Bare_1_A_Color1.fbx",
        };

        [MenuItem("Defenders/Arena/Apply Scenery")]
        public static void Apply()
        {
            // 1) Ensure the serializable ground material asset exists.
            var mat = EnsureGroundMaterial();
            if (mat == null)
            {
                Debug.LogError("[ArenaScenery] Could not create/load ground material -- aborting.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError("[ArenaScenery] Could not load prefab contents: " + PrefabPath);
                return;
            }

            try
            {
                // --- Mute the grass: re-material ONLY the Ground renderer ---
                Transform groundT = FindByName(contents.transform, "Ground");
                if (groundT != null)
                {
                    var mr = groundT.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sharedMaterial = mat; // asset ref, serializes cleanly
                        Debug.Log("[ArenaScenery] Ground re-materialed to muted ArenaGround.mat (collider/layer/geometry untouched).");
                    }
                    else
                    {
                        Debug.LogWarning("[ArenaScenery] 'Ground' has no MeshRenderer -- skipped re-material.");
                    }
                }
                else
                {
                    Debug.LogWarning("[ArenaScenery] 'Ground' child not found -- skipped re-material.");
                }

                // --- Add background landscape (idempotent) ---
                Transform existing = FindByName(contents.transform, BackdropName);
                if (existing != null)
                {
                    Debug.Log("[ArenaScenery] '" + BackdropName + "' already present -- skipped rebuild (idempotent).");
                }
                else
                {
                    BuildBackdrop(contents.transform, mat);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError("[ArenaScenery] SaveAsPrefabAsset reported failure for " + PrefabPath);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ArenaScenery] DONE. ARENA_SCENERY_OK :: " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // Create the muted ground material as a serializable ASSET (once). Idempotent:
        // if it exists, re-apply the felt color/emission/tiling and return it.
        private static Material EnsureGroundMaterial()
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Arena", "Materials");
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ArenaScenery] URP/Lit shader not found.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMat);
            bool created = false;
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, GroundMat);
                created = true;
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", GroundColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", GroundColor);
            // Emission OFF (no glow -> no neon).
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            // Matte: no metal, high roughness.
            if (mat.HasProperty("_Metallic"))  mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", GroundTiling);

            EditorUtility.SetDirty(mat);
            Debug.Log("[ArenaScenery] ArenaGround.mat " + (created ? "created" : "updated")
                      + " color=" + GroundColor + " (emission off).");
            return mat;
        }

        // Build the BackgroundLandscape group: distant plane + sparse tree ring.
        // All colliderless -> ArenaNavMeshBaker ignores it; placed OUTSIDE the play area.
        private static void BuildBackdrop(Transform root, Material groundMat)
        {
            var group = new GameObject(BackdropName);
            group.transform.SetParent(root, false);
            group.transform.localPosition = Vector3.zero;

            // -- Distant ground plane (built-in plane mesh is 10x10 at scale 1) --
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "DistantGround";
            // Strip the auto-added collider -> pure backdrop, no navmesh interference.
            var planeCol = plane.GetComponent<Collider>();
            if (planeCol != null) Object.DestroyImmediate(planeCol);
            plane.transform.SetParent(group.transform, false);
            plane.transform.localPosition = new Vector3(0f, BackdropPlaneY, 0f);
            float planeScale = BackdropPlaneSize / 10f; // plane mesh is 10m -> scale up
            plane.transform.localScale = new Vector3(planeScale, 1f, planeScale);
            var planeMr = plane.GetComponent<MeshRenderer>();
            if (planeMr != null) planeMr.sharedMaterial = groundMat;

            // -- Sparse ring of distant trees (no colliders -- FBX import addColliders:0) --
            var rng = new System.Random(RingSeed);
            int placed = 0;
            for (int i = 0; i < TreeCount; i++)
            {
                string assetPath = TreeAssets[i % TreeAssets.Length];
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (fbx == null)
                {
                    Debug.LogWarning("[ArenaScenery] Tree asset missing (skipped): " + assetPath);
                    continue;
                }

                var tree = (GameObject)PrefabUtility.InstantiatePrefab(fbx, group.transform);
                if (tree == null)
                {
                    tree = Object.Instantiate(fbx, group.transform);
                }
                tree.name = "DistantTree_" + i;

                // Even angular spread + jitter so it reads natural, not clocked.
                float baseAngle = (360f / TreeCount) * i;
                float jitter = (float)(rng.NextDouble() * 18.0 - 9.0);
                float angRad = (baseAngle + jitter) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(RingRadiusMin, RingRadiusMax, (float)rng.NextDouble());
                float x = Mathf.Cos(angRad) * radius;
                float z = Mathf.Sin(angRad) * radius;
                tree.transform.localPosition = new Vector3(x, 0f, z);

                float yaw = (float)(rng.NextDouble() * 360.0);
                tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                float s = Mathf.Lerp(TreeScaleMin, TreeScaleMax, (float)rng.NextDouble());
                tree.transform.localScale = new Vector3(s, s, s);

                // Defensive: ensure no colliders sneak in (pure backdrop).
                foreach (var c in tree.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c);
                }
                placed++;
            }

            Debug.Log("[ArenaScenery] BackgroundLandscape built: 1 distant plane (~"
                      + BackdropPlaneSize + "m, no collider) + " + placed
                      + " distant trees (ring " + RingRadiusMin + "-" + RingRadiusMax + "m, no colliders).");
        }

        // Depth-first search for the first descendant (or self) named `name`.
        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindByName(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
