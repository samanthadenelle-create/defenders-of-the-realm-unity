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
    // WO-181: moat, ramparts, building-footprint collider, gameplay-systems wiring, gate force-fields (BUG-ZONE adjacent) -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void BuildMoat(Transform wallRoot)
        {
            // WO-104: Terrain_Plane_Water is the clean water surface; Terrain_Plane_Lake is a
            // GRASS tile with a pond cut into it (tiled 204x it read as a green holey mass).
            const string LakePath       = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Terrains_M/Planes_M/Terrain_Plane_Water.prefab";
            const string DrawbridgePath = PolyMedievalDir + "Drawbridge_Medieval.prefab";

            var lake       = AssetDatabase.LoadAssetAtPath<GameObject>(LakePath);
            var drawbridge = AssetDatabase.LoadAssetAtPath<GameObject>(DrawbridgePath);
            if (lake == null)
            {
                Debug.LogWarning($"[VillageSceneBuilder] WO-104 moat: Terrain_Plane_Lake not found at " +
                                 $"'{LakePath}' — moat skipped (polyperfect re-import needed).");
                return;
            }

            var moatRoot = new GameObject("Moat");
            moatRoot.transform.SetParent(wallRoot, false);
            var mr = moatRoot.transform;

            const float innerX = 42f, innerZ = 33f;   // flush with wall base
            const float outerX = 48f, outerZ = 39f;   // 6 m outside the wall
            const float step    = 3f;
            const float waterY  = -0.4f;               // sit in a channel, below grade
            const float gateHalf = 3f;                 // 6 m drawbridge spans to leave clear

            int placed = 0;

            for (float x = -outerX + step * 0.5f; x < outerX; x += step)
            for (float z = -outerZ + step * 0.5f; z < outerZ; z += step)
            {
                float ax = Mathf.Abs(x), az = Mathf.Abs(z);
                bool insideWall = ax < innerX && az < innerZ;   // strictly inside the curtain
                bool insideOuter = ax <= outerX && az <= outerZ;
                if (insideWall || !insideOuter) continue;        // only the 6 m ring band

                // Leave the gate spans clear for the drawbridges (all 4 gates).
                bool northGate = ax < gateHalf && z >  innerZ - 0.5f;   // north band, x≈0
                bool southGate = ax < gateHalf && z < -innerZ + 0.5f;   // south band, x≈0
                bool eastGate  = az < gateHalf && x >  innerX - 0.5f;   // east band, z≈0
                bool westGate  = az < gateHalf && x < -innerX + 0.5f;   // west band, z≈0
                if (northGate || southGate || eastGate || westGate) continue;

                var t = (GameObject)PrefabUtility.InstantiatePrefab(lake);
                if (t == null) continue;
                t.name = "MoatTile";
                t.transform.SetParent(mr, false);
                t.transform.position = new Vector3(x, waterY, z);
                FitGroundTile(t, step);
                StripColliders(t);
                StripRigidbodies(t);
                placed++;
            }

            // ── Drawbridges: flat across the moat at each gate (WO-158: all 4) ──
            if (drawbridge != null)
            {
                // (label, position just outside the gate, yaw facing outward)
                var spans = new (string name, Vector3 pos, float yaw)[]
                {
                    ("Drawbridge-North", new Vector3(0f, 0f, (innerZ + 3f)), 180f),
                    ("Drawbridge-South", new Vector3(0f, 0f, -(innerZ + 3f)), 0f),
                    ("Drawbridge-East",  new Vector3(innerX + 3f, 0f, 0f),    90f),
                    ("Drawbridge-West",  new Vector3(-(innerX + 3f), 0f, 0f), 270f),
                };
                foreach (var (name, pos, yaw) in spans)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(drawbridge);
                    if (d == null) continue;
                    d.name = name;
                    d.transform.SetParent(mr, false);
                    d.transform.position = pos;
                    d.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    NormalizeProp(d, 7f);          // span the 6 m moat with margin
                    SnapFeetToParent(d);           // sit flush on the ground (was a raised surface)
                    StripColliders(d);             // decorative: hero crosses on the ground through the gate
                    StripRigidbodies(d);           //   (the raised collider was forcing a walk-around)
                }
            }
            else
            {
                Debug.LogWarning($"[VillageSceneBuilder] WO-104 moat: Drawbridge_Medieval not found at " +
                                 $"'{DrawbridgePath}' — gates left open across the moat.");
            }

            Debug.Log($"[VillageSceneBuilder] WO-104 BuildMoat: {placed} water tiles in the " +
                      $"6 m ring (inner +-{innerX}/+-{innerZ}, outer +-{outerX}/+-{outerZ}, y={waterY}); " +
                      $"{(drawbridge != null ? 3 : 0)} drawbridges at the gate spans.");
        }

        /// <summary>
        /// WO-104 §7 + unified-NavMesh rampart (owner 2026-05-30): a WALKABLE wall-walk the hero
        /// AND enemies navigate via the shared NavMesh. A flat stone WALKWAY runs along each wall's
        /// inner top edge (y = wall height), and a gentle stone RAMP (~29°, under the 45° NavMesh
        /// slope limit) climbs from the interior ground up to it, flanking each gate. All pieces are
        /// flagged NavigationStatic so BakeVillageNavMesh connects ground -> ramp -> walkway — making
        /// a hero defending up top reachable: enemies path up the same ramp to attack.
        /// </summary>
        private static void BuildRamparts(Transform wallRoot)
        {
            var root = new GameObject("Ramparts");
            root.transform.SetParent(wallRoot, false);
            var rr = root.transform;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var stone = sh != null ? new Material(sh) { name = "RampartStone" } : null;
            if (stone != null && stone.HasProperty("_BaseColor"))
                stone.SetColor("_BaseColor", new Color(0.52f, 0.50f, 0.46f));

            // Owner 2026-05-30: show the DESIGNED staircase as the visual and hide the nav ramp
            // beneath it, so it reads as climbing real stairs while the NavMesh stays a clean ramp.
            var stairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PolyMedievalDir + "Stairs_Medieval_Stone.prefab");

            const float wallX = 42f, wallZ = 33f;   // poly curtain inner edges
            const float topY  = 5f;                 // wall height -> walkway level
            const float walkW = 3f;                 // walkway depth (inner side)
            const float rampRun = 9f;               // horizontal run (5 m rise -> ~29°, < 45° limit)
            const float rampW = 3f;                 // ramp width
            int pieces = 0;

            // Local: nav-static stone box (CreatePrimitive carries a BoxCollider; harmless — the
            // agents move on the NavMesh, not via physics).
            System.Func<string, Vector3, Vector3, Quaternion, GameObject> Box =
                (name, pos, size, rot) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(rr, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = size;
                if (stone != null) { var rd = go.GetComponent<Renderer>(); if (rd != null) rd.sharedMaterial = stone; }
                GameObjectUtility.SetStaticEditorFlags(go,
                    GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.NavigationStatic);
                pieces++;
                return go;
            };

            // ── Walkways: flat slabs along each wall's INNER top edge (y = topY) ──
            float wkN = wallZ - walkW * 0.5f;
            Box("Walkway-North", new Vector3(0f, topY, wkN),  new Vector3(2f * wallX, 0.4f, walkW), Quaternion.identity);
            Box("Walkway-South", new Vector3(0f, topY, -wkN), new Vector3(2f * wallX, 0.4f, walkW), Quaternion.identity);
            float wkE = wallX - walkW * 0.5f;
            Box("Walkway-East",  new Vector3(wkE,  topY, 0f), new Vector3(walkW, 0.4f, 2f * wallZ), Quaternion.identity);
            Box("Walkway-West",  new Vector3(-wkE, topY, 0f), new Vector3(walkW, 0.4f, 2f * wallZ), Quaternion.identity);

            // ── Parapet (WO-136): a low battlement along each walkway's OUTER edge ──
            // The walkway top sits at topY + 0.2 (0.4-thick slab). The parapet is a
            // nav-static stone box rising ~1.2 m above it on the side facing OUT of
            // the village, so the hero reads as protected and the Box collider stops
            // him walking off the rampart edge (WO-136 acceptance: parapet fall-off).
            const float parH    = 1.4f;                 // parapet height above the walk
            const float parThk  = 0.4f;                 // parapet thickness
            float parTopY = topY + 0.2f + parH * 0.5f;  // centre y (walk-top + half height)
            float parZ    = wallZ - parThk * 0.5f;       // outer edge, inset by half-thickness
            float parX    = wallX - parThk * 0.5f;
            Box("Parapet-North", new Vector3(0f,  parTopY,  parZ), new Vector3(2f * wallX, parH, parThk), Quaternion.identity);
            Box("Parapet-South", new Vector3(0f,  parTopY, -parZ), new Vector3(2f * wallX, parH, parThk), Quaternion.identity);
            Box("Parapet-East",  new Vector3(parX,  parTopY, 0f),  new Vector3(parThk, parH, 2f * wallZ), Quaternion.identity);
            Box("Parapet-West",  new Vector3(-parX, parTopY, 0f),  new Vector3(parThk, parH, 2f * wallZ), Quaternion.identity);

            // ── Wall barrier collision (WO-136): collide on the REAL visible wall ──
            // BUG (owner): the perimeter wall mesh (BuildWallPerimeter, ±42/±33) has its
            // colliders STRIPPED (line ~2925) — gameplay collision lived on the hidden
            // inner KayKit ring (BuildWallRing, ±28/±21), so hero/enemies collided with an
            // invisible wall offset from the one they see. FIX: a full-height barrier box on
            // the visible wall line, Y=0 → wall-top (topY=5), broken at the SAME gate gaps the
            // wall mesh leaves so enemy lanes stay open. Nav-static so the bake routes around it.
            //   Gate gaps (from BuildWallPerimeter): South/East/West each skip |coord|<3 (6 m
            //   opening); North has NO gate (unbroken). Mirror that exactly here.
            const float barThk = 1.2f;                 // match the visible wall thickness (wallThick)
            const float barH   = topY;                 // Y=0 → wall-top (5 m)
            float barY  = barH * 0.5f;                 // box centre
            float barZ  = wallZ - barThk * 0.5f;       // sit on the visible wall line, inset half-thickness
            float barX  = wallX - barThk * 0.5f;
            const float gateHalf = 3f;                 // 6 m gate opening half-width
            float runHalf = wallX - gateHalf;          // half-length of one side of a gated (S) wall span
            float sideHalf = (wallX - gateHalf) * 0.5f;// centre offset of each half-span (gated walls)
            // North wall — gate at x=0: two spans flanking the 6 m opening.
            Box("WallBarrier-North-W", new Vector3(-(gateHalf + sideHalf), barY, barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            Box("WallBarrier-North-E", new Vector3( (gateHalf + sideHalf), barY, barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            // South wall — main gate at x=0: two spans flanking the 6 m gap.
            Box("WallBarrier-South-W", new Vector3(-(gateHalf + sideHalf), barY, -barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            Box("WallBarrier-South-E", new Vector3( (gateHalf + sideHalf), barY, -barZ), new Vector3(2f * sideHalf, barH, barThk), Quaternion.identity);
            // East wall — side gate at z=0: two spans flanking the gap.
            float sideHalfZ = (wallZ - gateHalf) * 0.5f;
            Box("WallBarrier-East-S", new Vector3(barX, barY, -(gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            Box("WallBarrier-East-N", new Vector3(barX, barY,  (gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            // West wall — side gate at z=0: two spans flanking the gap.
            Box("WallBarrier-West-S", new Vector3(-barX, barY, -(gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            Box("WallBarrier-West-N", new Vector3(-barX, barY,  (gateHalf + sideHalfZ)), new Vector3(barThk, barH, 2f * sideHalfZ), Quaternion.identity);
            _ = runHalf;  // (kept for clarity; spans computed via sideHalf/sideHalfZ)

            // ── Ramps: gentle stone inclines from interior ground up to the walkway edge ──
            // Defined by bottom (interior, y=0) + top (walkway edge, y=topY); LookRotation aligns
            // the slab's length to the slope so its top face is the walkable surface.
            System.Action<string, Vector3, Vector3> Ramp = (name, bottom, top) =>
            {
                Vector3 mid = (bottom + top) * 0.5f;
                Vector3 fwd = (top - bottom).normalized;
                float len = Vector3.Distance(bottom, top);
                // Two objects in parallel: an INVISIBLE nav plank (the walkable surface the agents
                // climb) + the DESIGNED staircase as the visual on top. Decouples look from navigate.
                var rampGo = Box(name, mid, new Vector3(rampW, 0.4f, len), Quaternion.LookRotation(fwd, Vector3.up));
                var rd = rampGo.GetComponent<Renderer>();
                if (rd != null) rd.enabled = false;   // hidden — the staircase below is the visual
                if (stairPrefab != null)
                {
                    var st = (GameObject)PrefabUtility.InstantiatePrefab(stairPrefab);
                    if (st != null)
                    {
                        st.name = name + "-Visual";
                        st.transform.SetParent(rr, false);
                        st.transform.position = bottom;
                        Vector3 horiz = new Vector3(fwd.x, 0f, fwd.z).normalized;
                        if (horiz.sqrMagnitude > 0.0001f)
                            st.transform.rotation = Quaternion.LookRotation(horiz, Vector3.up);
                        NormalizeProp(st, 4f);   // WO-136: 7f read as oversized "big steps" — 4f sits proportional to the 5m wall
                        StripColliders(st);
                        StripRigidbodies(st);
                    }
                }
            };
            // WO-166 #4: ramps run PARALLEL to (hugging) their wall, not perpendicular
            // into the courtyard. Both ends sit on the walkway-edge line (z=±zEdge for
            // N/S, x=±xEdge for E/W); the 9 m climb run goes ALONG the wall axis, so the
            // stairs read as climbing the wall face instead of jutting 9 m mid-courtyard
            // (owner: "the stps in middle"). Run spans x∈[-15,-6] (N/S) / z∈[-15,-6] (E/W),
            // clearing the centred gate gap (|coord|<3); the slab's 3 m width sits just
            // inside the wall. NavMesh link ground→walkway is preserved (ends unchanged in Y).
            float zEdge = wallZ - walkW;   // walkway inner edge (=30): ramp top meets it
            Ramp("Ramp-South", new Vector3(-6f - rampRun, 0f, -zEdge), new Vector3(-6f, topY, -zEdge));
            Ramp("Ramp-North", new Vector3(-6f - rampRun, 0f,  zEdge), new Vector3(-6f, topY,  zEdge));
            float xEdge = wallX - walkW;   // =39
            Ramp("Ramp-East",  new Vector3( xEdge, 0f, -6f - rampRun), new Vector3( xEdge, topY, -6f));
            Ramp("Ramp-West",  new Vector3(-xEdge, 0f, -6f - rampRun), new Vector3(-xEdge, topY, -6f));

            Debug.Log($"[VillageSceneBuilder] WO-104 BuildRamparts: {pieces} nav-static stone pieces " +
                      "(4 wall-walks + 4 climb ramps); hero + enemies share the NavMesh up to the rampart.");
        }

        /// <summary>
        /// Adds a BoxCollider sized to the building's mesh bounds to the plot
        /// root GameObject. HeroLocomotion's CapsuleCast sweeps against these
        /// each frame so the hero can no longer walk through structures
        /// (owner 2026-05-20). The visual mesh still has its own colliders
        /// stripped via InstantiateModel so there's a single source of truth
        /// for collision.
        /// </summary>
        private static void AddBuildingFootprintCollider(GameObject root, GameObject visual)
        {
            if (root == null || visual == null) return;
            // Don't duplicate.
            if (root.GetComponent<BoxCollider>() != null) return;

            Bounds bounds = ComputeMeshBounds(visual);
            if (bounds.size == Vector3.zero) return;
            // Convert world-space mesh bounds back to root-local — the visual
            // sits inside root with its own scale/rotation, so we ask Unity to
            // express the bounds in root's local frame.
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(bounds.center);
            // size of the visual in world units, then divide by root.lossyScale
            // so the collider tracks the world bounds even if root is scaled.
            Vector3 sz = bounds.size;
            Vector3 ls = root.transform.lossyScale;
            col.size = new Vector3(
                Mathf.Max(1.2f, (ls.x != 0f ? sz.x / ls.x : sz.x) * 0.8f),
                ls.y != 0f ? sz.y / ls.y : sz.y,
                Mathf.Max(1.2f, (ls.z != 0f ? sz.z / ls.z : sz.z) * 0.8f));
        }


        // #####################################################################
        // ##  WEEK 4 — village gameplay-system integration                  ##
        // ##  ------------------------------------------------------------   ##
        // ##  Wires every item from the three week4-*.md integration         ##
        // ##  checklists into the Village scene:                             ##
        // ##    - WaveManager wired to the Heart + spawn points              ##
        // ##    - HeroAbilities on a hero rig near the Heart                 ##
        // ##    - PetDeployer (auto-deploys the three starter pets)          ##
        // ##    - BuildMenu UIDocument with the 5 building prefabs           ##
        // ##    - the KayKit Skeleton enemy prefab (Enemy + EnemyDamageable) ##
        // ##    - the ForceFieldGate material wired onto every Gate          ##
        // ##  Every gameplay TYPE is touched by full-name reflection — the   ##
        // ##  DeNelle.Editor asmdef cannot reference DeNelle.Village/.Pets.  ##
        // #####################################################################

        /// <summary>
        /// Builds + wires the Week-4 village gameplay systems. Idempotent — the
        /// generated GameObjects all live under <c>VillageRoot</c> (cleared at
        /// the top of <see cref="BuildVillage"/>); the prefab + material assets
        /// are overwritten in place on a re-run.
        /// </summary>
        /// <param name="root">The VillageRoot transform.</param>
        /// <param name="gateRoot">The Gates sub-root — every Gate gets the force-field material.</param>
        /// <param name="heart">The HeartController component (may be null if the type was missing).</param>
        private static void BuildGameplaySystems(GameObject root, Transform gateRoot, Component heart)
        {
            EnsureFolder(GeneratedPrefabDir);

            var systemsRoot = NewChild(root.transform, "GameplaySystems");

            // 1) Assets — the force-field material, the enemy prefab, the five
            //    building prefabs. Built first so the scene components can wire
            //    against them.
            Material forceFieldMat = EnsureForceFieldMaterial();
            GameObject enemyPrefab = EnsureEnemyPrefab();
            var buildingPrefabs = EnsureBuildingPrefabs();

            // 2) Wire the force-field material onto every Gate's renderer.
            WireGateForceFields(gateRoot, forceFieldMat);

            // 3) The Heart's world position — the centre of the pet ring + the
            //    hero's spawn anchor. Falls back to origin when the Heart type
            //    was not found.
            Vector3 heartPos = heart != null ? heart.transform.position : Vector3.zero;

            // 4) WaveManager — its own sub-system GameObject.
            BuildWaveManager(systemsRoot, heart, enemyPrefab);

            // 5) HeroAbilities — on a hero rig stood near the Heart.
            GameObject hero = BuildHero(systemsRoot, heart, heartPos);

            // 5b) Wire the over-shoulder camera onto the hero now that it exists.
            //     CreateCamera() attached the follow component without a target
            //     because the hero hadn't been built yet.
            WireVillageCameraTarget(hero);

            // 6) PetDeployer — auto-deploys the three starter pets on Start().
            BuildPetDeployer(systemsRoot, heartPos);

            // 7) BuildMenu — a UIDocument GameObject with the build-menu UI.
            BuildBuildMenu(systemsRoot, buildingPrefabs);

            // 7b) Marketplace + PackStore — DISABLED for now. Placing it worked, but
            //     opening it rendered the WRONG panel (the hero talent tree) because
            //     PackStore's UIDocument grabbed the SHARED PanelSettings, and the UXML
            //     came up blank in the build. Re-enable after PackStore gets its OWN
            //     PanelSettings + a code-built UI (not UXML-template-driven).
            // BuildMarketplace(systemsRoot);

            // 8) Ambient townsfolk — wandering / idle KayKit villagers with
            //    engage-on-approach word bubbles. They watch the hero rig built
            //    in step 5 for the proximity check (Workstream D).
            int townsfolk = BuildTownsfolk(root.transform, heartPos, hero);

            Debug.Log("[VillageSceneBuilder] Week-4 gameplay systems wired -- " +
                      "WaveManager + HeroAbilities + PetDeployer + BuildMenu, " +
                      $"{townsfolk} ambient townsfolk, " +
                      $"enemy prefab {(enemyPrefab != null ? "OK" : "MISSING")}, " +
                      $"force-field material {(forceFieldMat != null ? "OK" : "MISSING")}.");
        }

        // =====================================================================
        //  Force-field gate material
        // =====================================================================

        /// <summary>
        /// Creates (or refreshes) the <c>ForceFieldGate.mat</c> material asset
        /// from <c>Assets/Shaders/ForceFieldGate.shader</c> and returns it. The
        /// material carries no per-instance overrides — <c>Gate.cs</c> drives the
        /// <c>_Collapse</c> property at runtime via a MaterialPropertyBlock.
        /// </summary>
        private static Material EnsureForceFieldMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ForceFieldShaderPath);
            if (shader == null)
            {
                Debug.LogError("[VillageSceneBuilder] ForceFieldGate.shader not found at " +
                               $"'{ForceFieldShaderPath}' -- gate force-field material skipped.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(ForceFieldMaterialPath);
            if (existing != null)
            {
                // Keep the asset; just make sure it still runs the right shader.
                if (existing.shader != shader) existing.shader = shader;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var mat = new Material(shader) { name = "ForceFieldGate" };
            AssetDatabase.CreateAsset(mat, ForceFieldMaterialPath);
            return mat;
        }

        /// <summary>
        /// Assigns the force-field material to each Gate's force-field renderer
        /// and wires that renderer into <c>Gate._forceFieldRenderer</c>. The
        /// Week-3 builder placed a <c>ForceFieldShimmer</c> cube child per gate —
        /// that cube's MeshRenderer becomes the shader-driven sheet.
        /// </summary>
        private static void WireGateForceFields(Transform gateRoot, Material forceFieldMat)
        {
            if (gateRoot == null) return;
            int wired = 0;
            var gateType = FindType(TypeGate);

            foreach (Transform gateGo in gateRoot)
            {
                // The Week-3 builder names the violet sheet "ForceFieldShimmer".
                Transform shimmer = gateGo.Find("ForceFieldShimmer");
                Renderer fieldRenderer = shimmer != null
                    ? shimmer.GetComponent<Renderer>()
                    : gateGo.GetComponentInChildren<Renderer>();

                if (fieldRenderer != null && forceFieldMat != null)
                    fieldRenderer.sharedMaterial = forceFieldMat;

                // Wire Gate._forceFieldRenderer so Gate.cs can drive _Collapse.
                if (gateType != null)
                {
                    var gateComp = gateGo.GetComponent(gateType);
                    if (gateComp != null && fieldRenderer != null)
                    {
                        var so = new SerializedObject(gateComp);
                        SetObjectField(so, "_forceFieldRenderer", fieldRenderer);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                wired++;
            }
            Debug.Log($"[VillageSceneBuilder] Force-field material wired onto {wired} gate(s).");
        }
    }
}
