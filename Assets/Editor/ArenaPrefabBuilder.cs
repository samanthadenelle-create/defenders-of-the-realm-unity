// =============================================================================
// ArenaPrefabBuilder (WO-506) -- authors the real "forest-clearing" arena landscape
// PREFAB the runtime BattleArena loads instead of assembling primitives every fight.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)  Namespace: DeNelle.Editor
//
// Owner directive (WO-506): "basics first -- a scene and landscape, then bells &
// whistles; simple play should be a scene." This replaces the procedural primitive
// floor + code-built edge ring with a SAVED landscape prefab under Resources/Arena/
// so Resources.Load finds it. BattleArena.BuildArena instantiates it onto the same
// far-offset _arenaRoot; the invisible kite walls + bloom + backdrop stay in code.
//
// THE PREFAB (Resources/Arena/ForestClearingArena.prefab):
//   Root (local 0,0,0)
//    - Ground     : a scaled Plane mesh (~70 x 55) with a MeshCollider on the DEFAULT
//                   layer (so ArenaNavMeshBaker bakes it), Grass_1.mat (emission OFF).
//    - EdgeProps  : a perimeter ring of the real Rock_*/Tree_* FBX, placed with the
//                   SAME ring math BattleArena.DressArenaEdge uses (reused here).
//    - Lighting   : a soft realtime Directional Light + a Reflection Probe (no bake).
//
// This is BONES: a first-pass real-art place the OWNER felt-tunes / replaces in the
// editor on the SAME load path. Idempotent (overwrites if the prefab exists).
//
//   Defenders > Arena > Build Forest Clearing Prefab
//   (batchmode: DeNelle.Editor.ArenaPrefabBuilder.Build)
//   Prints marker: ARENA_PREFAB_OK :: <path>
// =============================================================================

using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ArenaPrefabBuilder
    {
        private const string ArenaDir   = "Assets/Resources/Arena";
        private const string PrefabOut  = ArenaDir + "/ForestClearingArena.prefab";
        private const string GroundMat  = ArenaDir + "/Grass_1.mat";

        // Mirror BattleArena's kite footprint (ArenaHalfWidth 30 x ArenaHalfDepth 24 =>
        // a 60 x 48 kite) so the ground + ring line up with the runtime walls/spawns.
        private const float ArenaHalfWidth = 30f;
        private const float ArenaHalfDepth = 24f;

        // Edge-prop FBX (Resources/Arena/<name>) -- the real low-poly forest set.
        private static readonly string[] EdgeProps =
        {
            "Tree_2_A_Color1", "Tree_5_C_Color1", "Tree_7_A_Color1",
            "Tree_Bare_1_A_Color1", "Rock_1_A_Color1", "Rock_3_E_Color1",
        };
        private const int EdgePropCount = 18;   // matches DressArenaEdge's outerworld count (capped 20)

        [MenuItem("Defenders/Arena/Build Forest Clearing Prefab")]
        public static void Build()
        {
            EnsureFolder(ArenaDir);

            GameObject root = null;
            try
            {
                root = new GameObject("ForestClearingArena");
                root.transform.localPosition = Vector3.zero;

                BuildGround(root.transform);
                BuildEdgeProps(root.transform);
                BuildLighting(root.transform);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabOut, out bool ok);
                if (!ok || saved == null)
                {
                    Debug.LogError("[ArenaPrefabBuilder] SaveAsPrefabAsset failed for " + PrefabOut);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ArenaPrefabBuilder] DONE. ARENA_PREFAB_OK :: " + PrefabOut);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // Ground: a Plane (10m / unit) scaled to ~70 x 55 (covers the 60x48 kite + margin),
        // grass material with emission OFF, MeshCollider kept on the Default layer so
        // ArenaNavMeshBaker (useGeometry = PhysicsColliders) bakes it reliably.
        private static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // MeshFilter + MeshRenderer + MeshCollider
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = Vector3.zero;
            // 70 x 55 footprint: a Plane is 10m per unit of scale.
            ground.transform.localScale = new Vector3(70f / 10f, 1f, 55f / 10f);
            ground.layer = 0; // Default layer -- the agent type 0 surface bakes Default geometry.

            var mr = ground.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // CRITICAL: assign a SERIALIZABLE material ASSET reference -- NEVER a
                // `new Material(...)` runtime instance. A runtime instance does NOT
                // serialize into a prefab via SaveAsPrefabAsset, so the Ground material
                // saves as null ({fileID: 0}) -> URP renders the floor MAGENTA.
                // Grass_1.mat already has emission black, so a direct assign is correct.
                var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMat);
                if (mat == null)
                {
                    // Fallback: another EXISTING Arena material ASSET (still serializable).
                    Debug.LogWarning("[ArenaPrefabBuilder] " + GroundMat + " not found -- falling back to Dwarven_Ground.mat.");
                    mat = AssetDatabase.LoadAssetAtPath<Material>(ArenaDir + "/Dwarven_Ground.mat");
                }
                if (mat == null)
                {
                    Debug.LogError("[ArenaPrefabBuilder] No ground material ASSET found in " + ArenaDir + " -- Ground left unassigned (would render magenta). Add Grass_1.mat or Dwarven_Ground.mat.");
                }
                else
                {
                    mr.sharedMaterial = mat;
                }
            }
        }

        // EdgeProps: a perimeter ring of the real Rock_*/Tree_* FBX, placed with the SAME
        // ring math BattleArena.DressArenaEdge uses (ArenaHalf + 4.5 radius, even angular
        // spacing + jitter, 0.9..1.5 scale, random yaw, deterministic seed). Colliders
        // stripped (pure silhouette). Owner felt-tunes the look later.
        private static void BuildEdgeProps(Transform parent)
        {
            var edge = new GameObject("EdgeProps");
            edge.transform.SetParent(parent, false);

            int count = Mathf.Clamp(EdgePropCount, 0, 20);
            // Deterministic seed (matches DressArenaEdge's autopilot-chaos convention).
            var rng = new System.Random("forest".GetHashCode());

            float ringHalfX = ArenaHalfWidth + 4.5f;   // OUTSIDE where the runtime walls sit
            float ringHalfZ = ArenaHalfDepth + 4.5f;

            for (int i = 0; i < count; i++)
            {
                float baseAng = (i / (float)count) * Mathf.PI * 2f;
                float ang = baseAng + (float)(rng.NextDouble() - 0.5) * 0.35f;
                float radJitter = (float)rng.NextDouble() * 1.5f;
                float x = Mathf.Cos(ang) * (ringHalfX + radJitter);
                float z = Mathf.Sin(ang) * (ringHalfZ + radJitter);

                string name = EdgeProps[rng.Next(EdgeProps.Length)];
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaDir + "/" + name + ".fbx");
                if (fbx == null)
                {
                    Debug.LogWarning("[ArenaPrefabBuilder] edge prop '" + name + ".fbx' not found - skipped.");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                // Unpack so the prop is plain geometry baked into our prefab (no nested FBX link).
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "Edge_" + name + "_" + i;
                go.transform.SetParent(edge.transform, false);
                go.transform.localPosition = new Vector3(x, 0f, z);
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                float s = 0.9f + (float)rng.NextDouble() * 0.6f;
                go.transform.localScale = new Vector3(s, s, s);
                StripColliders(go);
            }
        }

        // Lighting: a soft realtime directional key light + a realtime reflection probe.
        // No baked lightmaps (the arena is staged at a far offset and torn down per fight).
        private static void BuildLighting(Transform parent)
        {
            var lightGo = new GameObject("KeyLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);   // warm soft daylight
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.lightmapBakeType = LightmapBakeType.Realtime;

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(parent, false);
            probeGo.transform.localPosition = new Vector3(0f, 6f, 0f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.size = new Vector3(ArenaHalfWidth * 2f + 12f, 24f, ArenaHalfDepth * 2f + 12f);
            probe.boxProjection = false;
        }

        private static void StripColliders(GameObject go)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) UnityEngine.Object.DestroyImmediate(cols[i]);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
