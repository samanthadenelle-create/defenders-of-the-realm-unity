using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    // WO-181: navmesh bake, static batching, layer assignment -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void BakeVillageNavMesh(GameObject root)
        {
            int marked = 0;

            // Mark the walkable / obstacle geometry navigation-static. Renderers
            // under Ground / Roads / Approaches are the walkable floor; Walls /
            // Buildings are obstacles the agents path around. Marking by
            // GameObjectUtility static flags is what NavMeshBuilder reads.
            //
            // WO-27 fix: the inner "Gates" root is DELIBERATELY excluded. The KayKit
            // gate arch (wall_straight_gate, scaled 4.5x) would voxelize into a navmesh
            // wall across the opening, leaving the route blocked at every gate -- so an
            // enemy could batter a gate down and STILL have no navmesh path through
            // to the Heart. Enemies are held at a CLOSED gate by gameplay instead
            // (Enemy.ProbeForStructure hits the Gate's blocker BoxCollider, which
            // has no renderer and so never affects the bake); when the gate is
            // destroyed they resume onto the continuous navmesh and pour through.
            // The opening therefore must stay WALKABLE in the bake.
            //
            // DEF gate-nav fix (2026-05-30): the OUTER perimeter gatehouses
            // (BuildWallPerimeter: "Gate-*-Main" / "Gate-*-Side", scale 10) live UNDER
            // the "Walls" root, so the sweep below marked their arch mesh NavigationStatic
            // and it voxelized SOLID across every opening -- sealing the gate on the
            // NavMesh. This was invisible while the hero moved by free transform, but
            // HeroLocomotion now moves as a NavMeshAgent (constrained to the bake), so a
            // plugged opening means "cannot exit." Skip these gate arches exactly like
            // the inner Gates root; the curtain wall segments on either side still bound
            // the opening, and the Ground/Approaches floor keeps it walkable through.
            string[] navStaticRoots =
            {
                "Ground", "Roads", "Approaches", "Walls", "Buildings",
            };
            foreach (var rootName in navStaticRoots)
            {
                var sub = root.transform.Find(rootName);
                if (sub == null) continue;
                foreach (var r in sub.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    if (IsUnderPerimeterGate(r.transform, sub)) continue; // keep gate openings walkable
                    if (IsNonWalkableMoatPiece(r.transform)) continue;    // water shelf + drawbridge lip fragment the gate crossing
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                        flags | StaticEditorFlags.NavigationStatic);
                    marked++;
                }
            }

            // Bake the open Village scene. The legacy UnityEditor.AI.NavMeshBuilder
            // bakes the ACTIVE scene synchronously using the project's default
            // agent settings (Window > AI > Navigation > Agents). ClearAllNavMeshes
            // first keeps a re-run idempotent. The skeleton enemy's NavMeshAgent
            // (radius 0.4, height 2.0) sits inside the Unity default Humanoid
            // agent (radius 0.5, height 2.0) -- the bake is valid for it.
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            // -- Static-batching pass (mobile-perf audit P0-4 / section 2.1) --
            // The NavMesh bake above flags only NavigationStatic -- which leaves
            // static batching OFF for all ~2,930 village instances. The audit's
            // headline mobile risk is draw-call submission: 2,600+ ground tiles
            // issuing 2,600+ draws per frame. Flagging BatchingStatic lets Unity
            // merge identical static meshes at scene load, collapsing the draw
            // count. Done AFTER the bake so NavigationStatic is already set; we
            // OR the flag in so the existing NavigationStatic bit is preserved.
            MarkStaticBatchingAndInstancing();

            Debug.Log($"[VillageSceneBuilder] NavMesh baked -- {marked} renderer object(s) " +
                      "marked Navigation Static (Ground/Roads/Approaches walkable, " +
                      "Walls/Buildings obstacles; Gates left WALKABLE so enemies path " +
                      "through once destroyed). Legacy UnityEditor.AI synchronous bake.");
        }

        // =====================================================================
        //  Static batching + GPU instancing (mobile-perf audit P0-4 / section 2.1)
        // =====================================================================

        /// <summary>
        /// Flags the static village geometry <c>BatchingStatic</c> and enables
        /// GPU instancing on the repeated-prop materials -- the audit's P0-4
        /// draw-call fix (recommendations 2 and 3 in section 2.1).
        /// </summary>
        private static void MarkStaticBatchingAndInstancing()
        {
            var root = GameObject.Find(VillageRootName);
            if (root == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] BatchingStatic pass skipped -- " +
                                 "VillageRoot not found.");
                return;
            }

            string[] staticGeometryRoots =
            {
                "Ground", "Walls", "Gates", "Roads", "Buildings",
                "Centerpieces", "CityDressing", "Approaches",
            };

            int batched = 0;
            foreach (var rootName in staticGeometryRoots)
            {
                var sub = root.transform.Find(rootName);
                if (sub == null) continue;
                foreach (var r in sub.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                        flags | StaticEditorFlags.BatchingStatic);
                    batched++;
                }
            }

            int instanced = EnableInstancingOnDressingMaterials(root);

            Debug.Log($"[VillageSceneBuilder] Static-batching pass -- {batched} renderer object(s) " +
                      "flagged BatchingStatic (OR-ed onto the existing NavigationStatic bit); " +
                      $"GPU instancing enabled on {instanced} dressing material(s). " +
                      "Mobile-perf audit P0-4 / section 2.1.");
        }

        /// <summary>
        /// Sets <c>Material.enableInstancing</c> on every distinct shared
        /// material under the CityDressing root. Returns the count flipped.
        /// </summary>
        private static int EnableInstancingOnDressingMaterials(GameObject root)
        {
            var dressing = root.transform.Find("CityDressing");
            if (dressing == null) return 0;

            var seen = new HashSet<Material>();
            int flipped = 0;
            foreach (var r in dressing.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !seen.Add(mat)) continue;
                    if (!mat.enableInstancing)
                    {
                        mat.enableInstancing = true;
                        EditorUtility.SetDirty(mat);
                        flipped++;
                    }
                }
            }
            return flipped;
        }

        // =====================================================================
        //  Week-4 reflection / wiring helpers
        // =====================================================================

        /// <summary>
        /// Sets a serialized <c>LayerMask</c> field. A LayerMask SerializedProperty
        /// is backed by an int -- <c>intValue</c> carries the mask bits.
        /// </summary>
        private static void SetLayerMaskField(SerializedObject so, string field, int mask)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[VillageSceneBuilder] LayerMask field '{field}' not found on " +
                                 $"{so.targetObject.GetType().Name} -- mask not set.");
                return;
            }
            prop.intValue = mask;
        }

        /// <summary>Recursively sets the layer on a GameObject and all its descendants.</summary>
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
