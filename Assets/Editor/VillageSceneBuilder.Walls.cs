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
    // WO-181: wall ring, cardinal gates, castle arch, clear-walls-near-gates, perimeter, ground-tile fit (BUG-ZONE: village wall/gate geometry + collision) -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static int BuildWallRing(Transform parent, Type tWallLayout, Component controller)
        {
            if (tWallLayout == null) return 0;

            var segments = ReadEnumerable(tWallLayout, "Segments");
            if (segments == null)
            {
                Debug.LogError("[VillageSceneBuilder] WallLayout.Segments returned null -- wall ring skipped.");
                return 0;
            }

            const float wallHeight = 3.0f; // tier 0 (Wooden) -- WALL_TARGET_H[0]
            int count = 0;

            var straightModel = LoadModel(HexNeutral + "wall_straight.fbx");
            var cornerModel = LoadModel(HexNeutral + "wall_corner_A_outside.fbx");

            foreach (var seg in segments)
            {
                int index = (int)GetMember(seg, "Index");
                float length = (float)GetMember(seg, "Length");
                bool corner = (bool)GetMember(seg, "Corner");
                Vector3 pos = (Vector3)GetMember(seg, "Position");
                Quaternion rot = (Quaternion)GetMember(seg, "Rotation");

                var go = new GameObject((corner ? "WallCorner-" : "WallSection-") + index);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);

                var model = corner ? cornerModel : straightModel;
                string meshName = corner ? "wall_corner_A_outside.fbx" : "wall_straight.fbx";
                GameObject visual = InstantiateModel(model, meshName,
                    corner ? $"wall_corner ({index})" : $"wall_straight ({index})");
                visual.transform.SetParent(go.transform, false);

                // WO-104: the poly castle curtain walls (BuildWallPerimeter) are now the
                // perimeter VISUAL — hide this KayKit wall mesh so they don't double-stack.
                // The WallSegment + its collider on `go` stay for gameplay (barrier/repair).
                foreach (var rr in visual.GetComponentsInChildren<Renderer>()) rr.enabled = false;

                // KayKit wall pieces' native orientation differs from WallLayout's
                // local-X run assumption — owner observed 2026-05-19 that straights
                // sit ~90deg off and corners ~180deg off. Tunable yaw correction
                // applied to the visual mesh (the WallSegment collider on `go`
                // keeps WallLayout's transform).
                visual.transform.localRotation = Quaternion.Euler(
                    0f, corner ? WallCornerYawFix : WallStraightYawFix, 0f);

                // Match the 4.5x building scale so walls don't look dwarfed by
                // the (now 6 m tall) houses. FitWallVisualToRun below stretches
                // the run axis independently, so this is safe — height +
                // thickness get the lift, length stays at runLength.
                if (model != null)
                    visual.transform.localScale *= BuildingScale;

                if (model != null && !corner)
                {
                    // KayKit wall_straight is a fixed-length module; stretch its
                    // long horizontal axis so the section fills its computed run
                    // length EXACTLY (WallLayout already insets the run ends off
                    // the corner blocks, so a flush fit leaves no gap/overlap).
                    // Corner pieces stay at native scale. FitWallVisualToRun
                    // measures in the visual's own local space, so the result is
                    // immune to both WallStraightYawFix and the segment rotation
                    // on `go` -- the old world-AABB measure read thickness, not
                    // length, which is what produced the gaps/overlaps.
                    FitWallVisualToRun(visual, length);
                }
                else if (model == null)
                {
                    visual.transform.localScale = corner
                        ? new Vector3(length, wallHeight, length)
                        : new Vector3(length, wallHeight, WallThicknessConst);
                    visual.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, 0f);
                    ApplyColor(visual, new Color(0.49f, 0.45f, 0.40f));
                }

                var segComp = AddVillageComponent(go, TypeWallSegment);
                if (segComp != null)
                {
                    InvokeConfigure(segComp, "Configure", seg, wallHeight);
                    RegisterWith(controller, "RegisterWallSegment", segComp, seg);
                }
                count++;
            }
            return count;
        }

        // =====================================================================
        //  Cardinal gates — driven by WallLayout.Gates
        // =====================================================================

        private static int BuildGates(Transform parent, Type tWallLayout, Component controller)
        {
            if (tWallLayout == null) return 0;

            var gates = ReadEnumerable(tWallLayout, "Gates");
            if (gates == null)
            {
                Debug.LogError("[VillageSceneBuilder] WallLayout.Gates returned null -- gates skipped.");
                return 0;
            }

            var gateModel = LoadModel(HexNeutral + "wall_straight_gate.fbx");

            int count = 0;
            foreach (var gap in gates)
            {
                string id = (string)GetMember(gap, "Id");
                string direction = (string)GetMember(gap, "Direction");
                Vector3 pos = (Vector3)GetMember(gap, "Position");
                Quaternion rot = (Quaternion)GetMember(gap, "Rotation");

                var go = new GameObject($"Gate-{id} ({direction})");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);

                GameObject visual;
                if (gateModel != null)
                {
                    visual = InstantiateModel(gateModel, "wall_straight_gate.fbx",
                        $"gate ({direction})");
                }
                else
                {
                    visual = new GameObject($"[PLACEHOLDER] gate ({direction})");
                    NotePlaceholder($"gate ({direction})");
                    float half = GateHalfWidthConst;
                    var left = PrimitiveChild(visual.transform, "PillarL", PrimitiveType.Cube,
                        new Vector3(-half, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f),
                        new Color(0.49f, 0.45f, 0.40f));
                    var right = PrimitiveChild(visual.transform, "PillarR", PrimitiveType.Cube,
                        new Vector3(half, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f),
                        new Color(0.49f, 0.45f, 0.40f));
                    var field = PrimitiveChild(visual.transform, "ForceField", PrimitiveType.Cube,
                        new Vector3(0f, 1.6f, 0f), new Vector3(half * 2f, 3.0f, 0.1f),
                        new Color(0.61f, 0.44f, 1f, 1f));
                    _ = left; _ = right; _ = field;
                }
                visual.transform.SetParent(go.transform, false);

                // WO-136: hide the KayKit gate visual mesh — the polyperfect
                // perimeter (BuildWallPerimeter) is now the visible wall+gate art,
                // so the old KayKit gate arch double-stacked as "old outer gates."
                // Keep the GameObject + its gate gameplay/collider/force-field; only
                // the visual renderers are disabled (same approach as the wall ring).
                foreach (var rr in visual.GetComponentsInChildren<Renderer>()) rr.enabled = false;

                // KayKit gate piece (wall_straight_gate) needs the same yaw
                // correction as wall_straight — owner-observed 2026-05-19 the
                // gates sit ~90deg off.
                visual.transform.localRotation = Quaternion.Euler(0f, WallStraightYawFix, 0f);

                // Match the 4.5x wall scaling so the gate doesn't look like a
                // pinhole between two giant wall sections (2026-05-19 PO P0).
                if (gateModel != null)
                    visual.transform.localScale *= BuildingScale;

                // Owner 2026-05-20 ("hole where walls don't touch door"):
                // WallLayout leaves a GAP of GateGapHalf*2 = 4 m, but the
                // gate mesh's natural width matches only GateHalfWidth*2 =
                // 2.8 m — so a 1.43× stretch on the gate's run-axis fills
                // the gap so the wall sections meet the gate edge cleanly.
                // After WallStraightYawFix (90°) the gate's local X is the
                // run direction, so scale.x is what to stretch.
                if (gateModel != null)
                {
                    Vector3 s = visual.transform.localScale;
                    s.x *= (1.4f + 0.6f) / 1.4f;
                    visual.transform.localScale = s;
                }

                // Owner 2026-05-20 ("purple frame on gate"): the KayKit gate
                // mesh has a stone-arch submesh whose material falls through
                // to URP's magenta fallback in the player build. Attach the
                // TripoMaterialFixer with a stone-grey tint so the arch
                // reads as proper stone instead of the broken-material pink.
                var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fixerType != null && gateModel != null)
                {
                    var fixer = visual.AddComponent(fixerType);
                    var setTint = fixerType.GetMethod("SetFallbackTint");
                    setTint?.Invoke(fixer, new object[] { new Color(0.52f, 0.50f, 0.46f) });
                }

                // Owner 2026-05-20 ("still rocks in front of gate cannot
                // access gate"): the KayKit gate FBX ships with a MeshCollider
                // that follows the stone arch + wall geometry, which blocks
                // the hero's CapsuleCast right in the gate threshold. Strip
                // all colliders on the gate visual so the doorway is genuinely
                // walk-through. The wall sections on either side keep their
                // colliders so the perimeter remains solid.
                StripColliders(visual);

                // DEF-19 (2026-05-27): ClearWallsNearGates uses
                // radius = BuildingScale*2 = 6 m to widen the visual gate
                // opening, but that destroys wall-section BoxColliders in a
                // [GateGapHalf=2 m … 6 m] wing on EACH SIDE of the gate
                // centre.  The gate visual fills the centre 4 m, leaving the
                // outer 4 m wings on each side as invisible collision gaps the
                // hero can walk through.  Fix: add two invisible wing-blocker
                // BoxColliders directly on the gate root (`go`) to close those
                // gaps.  Local X of `go` is the wall run direction (WallLayout
                // convention), so ±wingCenterLocal positions the blockers
                // correctly for all four cardinal gates.
                {
                    const float wingInner       = GateGapHalfConst;          // 2.0 m — gate visual edge
                    const float wingOuter       = BuildingScale * 2f;         // 6.0 m — ClearWallsNearGates radius
                    const float wingLength      = wingOuter - wingInner;      // 4.0 m per wing
                    const float wingCenterLocal = (wingInner + wingOuter) * 0.5f; // 4.0 m from gate centre
                    const float gateWallH       = 3.0f;                       // mirrors wallHeight in BuildWallRing
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size   = new Vector3(wingLength, gateWallH, WallThicknessConst);
                        bc.center = new Vector3(side * wingCenterLocal, gateWallH * 0.5f, 0f);
                    }
                }

                // Castle arch removed per owner direction 2026-05-20: the
                // Tripo castle ballast Tower FBX rendered as a pink ghost in
                // the player build even after the URP material fix, so the
                // gate falls back to the bare wall_straight_gate piece. No
                // force-field shimmer either — the gate reads as a simple
                // open passage now.

                var gateComp = AddVillageComponent(go, TypeGate);
                if (gateComp != null)
                {
                    InvokeConfigure(gateComp, "Configure", gap);
                    RegisterWith(controller, "RegisterGate", gateComp, gap);
                }
                count++;
            }
            return count;
        }

        // =====================================================================
        //  Plaza + road network (§7)
        // =====================================================================

        /// <summary>
        /// The central paved plaza between Elarion and the Keep (§3.3 / §7.3) —
        /// a slightly-raised patch of stone road tiles. Simple stone paving
        /// (§14 Q2 default).
        /// </summary>

        /// <summary>
        /// Rings a building plot with a low KayKit fence (dressing only — the
        /// fence pieces have their colliders stripped so they never block
        /// pathing, per spec §5).
        /// </summary>

        // =====================================================================
        //  Camera + light
        // =====================================================================


        /// <summary>
        /// Spawns a glowing stone arch near the south gate that routes the
        /// player into Dungeon_HealersCottage via DungeonPortal (DeNelle.Village).
        /// Idempotent — a prior "DungeonPortal" GO is destroyed first.
        /// </summary>
        /// <summary>
        /// Loads Assets/Models/CastleGate/castle+ballast+Tower.fbx and parents
        /// a stripped-collider instance to <paramref name="gate"/>. Owner ask
        /// 2026-05-20: replaces the violet force-field shimmer with a real
        /// castle arch so the gate reads as a fortified entrance.
        /// </summary>
        private static void AttachCastleArch(Transform gate)
        {
            const string CastlePath = "Assets/Models/CastleGate/castle+ballast+Tower.fbx";
            var model = LoadModel(CastlePath);
            if (model == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] castle+ballast+Tower.fbx not found at " +
                                 $"'{CastlePath}' — skipping castle arch.");
                return;
            }
            var arch = InstantiateModel(model, "castle+ballast+Tower.fbx", "CastleArch");
            arch.transform.SetParent(gate, false);

            // Align with the gate opening: the existing wall_straight_gate
            // already takes WallStraightYawFix; the arch needs the same yaw so
            // it sits flush rather than 90deg across the opening.
            arch.transform.localPosition = Vector3.zero;
            arch.transform.localRotation = Quaternion.Euler(0f, WallStraightYawFix, 0f);

            // Normalise arch to the gate width so a single FBX serves all
            // four cardinal gates regardless of native KayKit / Tripo scale.
            // Target: ~2 × gate half-width on the wall axis, BuildingScale up.
            float targetWidth = GateHalfWidthConst * 2f * BuildingScale;
            var renderers = arch.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float maxDim = Mathf.Max(b.size.x, b.size.z);
                if (maxDim > 0.01f)
                    arch.transform.localScale *= (targetWidth / maxDim);
            }
            else
            {
                arch.transform.localScale = Vector3.one * BuildingScale;
            }

            // Strip every collider so hero + pets walk through unobstructed
            // (the wall_straight_gate piece already supplies the side-pillar
            // colliders that read as the gate's solid bits).
            foreach (var c in arch.GetComponentsInChildren<Collider>(true))
                if (c != null) UnityEngine.Object.DestroyImmediate(c);

            // The Tripo FBX imports with Phong materials that URP can't render
            // (owner 2026-05-20: arch read as transparent pink ghost). Mount
            // the TripoMaterialFixer runtime component with a Resources-loaded
            // fallback texture so the arch is properly rebuilt in URP/Lit when
            // the player loads the scene.
            var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
            if (fixerType != null)
            {
                var fixer = arch.AddComponent(fixerType);
                var setMethod = fixerType.GetMethod("SetFallbackTexture");
                setMethod?.Invoke(fixer, new object[] { "Textures/CastleArch" });
            }
        }


        /// <summary>
        /// Wires WaveManager → VillageHudController so the HUD's wave timer
        /// actually updates. Adds a <c>WaveHudBridge</c> onto the WaveManager
        /// GameObject (the bridge talks to the HUD by reflection so DeNelle.Village
        /// does not have to reference DeNelle.HUD).
        /// </summary>


        /// <summary>
        /// Destroys any wall section / corner whose world centre lies within
        /// <c>BuildingScale × 2 m</c> of any gate's world position. The
        /// WallLayout already carves a 4 m gap for each gate, but at 3.0×
        /// scale that gap is visually narrow vs the now-9 m-tall walls; the
        /// flanking segments end up obscuring the gate's arch from the hero's
        /// eye line. Owner direction 2026-05-20.
        /// </summary>
        private static void ClearWallsNearGates(Transform wallRoot, Transform gateRoot)
        {
            if (wallRoot == null || gateRoot == null) return;
            float radius = BuildingScale * 2f;
            float r2 = radius * radius;

            // Cache gate positions so we don't enumerate twice in the inner loop.
            var gates = new System.Collections.Generic.List<Vector3>(gateRoot.childCount);
            for (int i = 0; i < gateRoot.childCount; i++)
                gates.Add(gateRoot.GetChild(i).position);
            if (gates.Count == 0) return;

            // Destroy any wall plot whose centre is inside any gate's radius.
            // Iterate snapshot so destroying children doesn't skew the loop.
            var walls = new System.Collections.Generic.List<Transform>(wallRoot.childCount);
            for (int i = 0; i < wallRoot.childCount; i++) walls.Add(wallRoot.GetChild(i));
            int removed = 0;
            foreach (var wall in walls)
            {
                if (wall == null) continue;
                Vector3 wp = wall.position;
                foreach (var gp in gates)
                {
                    float dx = wp.x - gp.x;
                    float dz = wp.z - gp.z;
                    if (dx * dx + dz * dz <= r2)
                    {
                        UnityEngine.Object.DestroyImmediate(wall.gameObject);
                        removed++;
                        break;
                    }
                }
            }
            if (removed > 0)
                Debug.Log($"[VillageSceneBuilder] ClearWallsNearGates removed {removed} wall section(s) " +
                          $"within {radius:F1} m of a gate.");
        }

        // =====================================================================
        //  WO-101: polyperfect stone wall perimeter
        // =====================================================================

        /// <summary>
        /// Builds the polyperfect stone wall perimeter around the village using
        /// Low Poly Ultimate Pack _M prefabs. Layout:
        ///   North wall  z = +33, segments every 3 m, x = -42 to +42.
        ///   South wall  z = -33, same + Gate_Medieval_Medium at x = 0.
        ///   East wall   x = +42, segments every 3 m, z = -33 to +33 + Gate_Medieval_Small at z = 0.
        ///   West wall   x = -42, same + Gate_Medieval_Small at z = 0.
        ///   Corner towers (SM_Tower_Castle_Round) at (±42, 0, ±33).
        ///   Mid-wall towers (SM_Tower_Medieval_Wood) at each wall midpoint.
        /// All placed under <paramref name="wallRoot"/> so ClearWallsNearGates /
        /// BakeVillageNavMesh automatically include them.
        /// </summary>
        private static void BuildWallPerimeter(Transform wallRoot)
        {
            // ── Polyperfect prefab paths ──────────────────────────────────────
            // WO-104: the real curtain wall is Wall_Stone_3x3_A in Buildings_M/parts/
            // Building Walls_M/ (Medieval_M never had "Wall_Medieval_Stone" → walls
            // were silently missing). Towers/gates stay in Medieval_M.
            const string WallSeg      = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Buildings_M/parts/Building Walls_M/Wall_Stone_3x3_A.prefab";
            const string GateMedium   = PolyMedievalDir + "Gate_Medieval_Medium.prefab";
            const string GateSmall    = PolyMedievalDir + "Gate_Medieval_Small.prefab";
            const string TowerCorner  = PolyMedievalDir + "Tower_Castle_Round.prefab";
            const string TowerMidWall = PolyMedievalDir + "Tower_Castle_Square.prefab"; // WO-104 square watchtower

            // Load prefabs once (null → graceful placeholder skip).
            var wallSegModel  = AssetDatabase.LoadAssetAtPath<GameObject>(WallSeg);
            var gateMedModel  = AssetDatabase.LoadAssetAtPath<GameObject>(GateMedium);
            var gateSmModel   = AssetDatabase.LoadAssetAtPath<GameObject>(GateSmall);
            var towerCorModel = AssetDatabase.LoadAssetAtPath<GameObject>(TowerCorner);
            var towerMidModel = AssetDatabase.LoadAssetAtPath<GameObject>(TowerMidWall);

            if (wallSegModel == null)
                Debug.LogWarning("[VillageSceneBuilder] WO-101 Wall_Medieval_Stone prefab not found " +
                                 $"at '{WallSeg}' — wall perimeter will have no stone segments " +
                                 "(polyperfect pack re-import needed on this machine).");

            // ── Layout constants ──────────────────────────────────────────────
            const float wallZ    = 33f;  // north/south wall Z
            const float wallX    = 42f;  // east/west wall X
            const float segStep  =  3f;  // segment pitch (pack snaps at 3 × 3 m)

            var perimeterRoot = new GameObject("WallPerimeter");
            perimeterRoot.transform.SetParent(wallRoot, false);
            var pr = perimeterRoot.transform;

            // ── Helpers: instantiate, then size per type ──────────────────────
            // Poly _M prefabs are tiny AND narrow natively -> uniform scaling made tall
            // thin bars with gaps (the picket-fence the owner saw). Walls must be STRETCHED
            // horizontally to fill the 3 m run and pinned to a fixed height so neighbours
            // abut into a continuous curtain. Towers/gates get a uniform NormalizeProp.
            // Wall sizing is measured from WORLD renderer bounds at yaw 0 (world x/z == local
            // x/z before rotating), so the maths needs no rotation bookkeeping.
            const float wallHeight  = 5f;  // curtain wall world height
            const float towerTarget = 9f;  // corner/mid towers stand above the curtain
            // Gate FIX (2026-05-30): NormalizeProp fits the LARGEST dimension to this
            // value. At 6 the gatehouse came out shorter than the 5 m wall + a tiny
            // opening ("way too small", all gates). The gatehouse prefab is taller
            // than wide, so fitting its height to ~10 makes it read as a proper
            // gatehouse rising to/above the curtain with a hero-passable arch.
            const float gateTarget  = 10f; // gatehouse — taller than the 5 m curtain, full-size arch
            bool loggedTile = false;

            System.Func<GameObject, string, Vector3, GameObject> Make =
                (prefab, label, pos) =>
            {
                if (prefab == null) return null;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (inst == null) return null;
                inst.name = label;
                inst.transform.SetParent(pr, false);
                inst.transform.position = pos;
                RepairPerimeterMaterials(inst);   // WO-126: stone fallback for null/error material slots (magenta gate arch)
                return inst;
            };

            System.Action<GameObject, string, Vector3, float> Wall =
                (prefab, label, pos, yaw) =>
            {
                var g = Make(prefab, label, pos);
                if (g == null) return;
                var rs = g.GetComponentsInChildren<Renderer>();
                if (rs != null && rs.Length > 0)
                {
                    Bounds wb = rs[0].bounds;                                   // world AABB, yaw 0
                    for (int i = 1; i < rs.Length; i++) wb.Encapsulate(rs[i].bounds);
                    if (!loggedTile)
                    {
                        Debug.Log($"[VillageSceneBuilder] WO-104 wall tile native world bounds = {wb.size} " +
                                  $"(localScale {g.transform.localScale}); fitting run->{segStep}m height->{wallHeight}m.");
                        loggedTile = true;
                    }
                    var s = g.transform.localScale;
                    // Scale run axis to the 3 m pitch, thickness axis up to a SUBSTANTIAL
                    // 1.2 m (native 0.35 m read as thin bars/posts with the void showing
                    // through), height to wallHeight. Run = the longer horizontal extent.
                    const float wallThick   = 1.2f;
                    const float wallOverlap = 1.5f;   // run spans 1.5x the 3 m pitch so the tile's
                                                      // solid stone (narrower than its 3 m bbox)
                                                      // overlaps its neighbour -> no gaps between segments
                    if (wb.size.x >= wb.size.z)
                    {
                        if (wb.size.x > 0.001f) s.x *= (segStep * wallOverlap) / wb.size.x;   // run, overlapped
                        if (wb.size.z > 0.001f) s.z *= wallThick / wb.size.z;                  // thickness -> 1.2 m
                    }
                    else
                    {
                        if (wb.size.z > 0.001f) s.z *= (segStep * wallOverlap) / wb.size.z;
                        if (wb.size.x > 0.001f) s.x *= wallThick / wb.size.x;
                    }
                    if (wb.size.y > 0.001f) s.y *= wallHeight / wb.size.y;
                    g.transform.localScale = s;
                }
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                // DIAGNOSTIC (WO-158 castle debris): log each segment's FINAL world
                // bounds + position so we can see which pieces come out flat/wrong
                // (the slabs embedded in the wall). Remove once the cause is found.
                {
                    var dr = g.GetComponentsInChildren<Renderer>();
                    if (dr != null && dr.Length > 0)
                    {
                        Bounds fb = dr[0].bounds;
                        for (int i = 1; i < dr.Length; i++) fb.Encapsulate(dr[i].bounds);
                        Debug.Log($"[WALLDIAG] {g.name} pos={g.transform.position} " +
                                  $"worldSize={fb.size} center={fb.center} scale={g.transform.localScale}");
                    }
                }
                StripColliders(g);                                            // WallSegment colliders (BuildWallRing) own gameplay
                StripRigidbodies(g);
            };

            System.Action<GameObject, string, Vector3, float, float> Big =
                (prefab, label, pos, yaw, target) =>
            {
                var g = Make(prefab, label, pos);
                if (g == null) return;
                NormalizeProp(g, target);                                     // uniform measure + scale
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                StripColliders(g);
                StripRigidbodies(g);
            };

            // ── Gate (2026-06-02 SEAM FIX) ───────────────────────────────────
            // ROOT CAUSE of the "seam where gate meets wall": the gatehouse prefab
            // is TALLER than wide, so NormalizeProp(target=10) fits its HEIGHT to 10
            // and leaves its WIDTH well short of the wall opening. The flanking wall
            // stones resume at a fixed inner edge (north opening was widened to |x|<6,
            // so the first full stone's solid inner edge is ~5.25 m out), so a bare gap
            // opens on each side of the arch between the narrow gatehouse and the wall.
            // FIX: after the uniform NormalizeProp (which gives the correct HEIGHT),
            // stretch ONLY the gatehouse's horizontal RUN axis (the axis along the wall)
            // so its outer edges reach the flanking stones flush. Done in LOCAL space
            // BEFORE applying the yaw, so it is orientation-independent (works for the
            // 0/90/270-deg gates alike). The gatehouse colliders are stripped (decorative),
            // and the real walkable opening is governed by the Fortify WallBarrier nav gap
            // (north ±6, others ±3) — NOT by this visual width — so widening the mesh has
            // ZERO navmesh/hero-passage impact; it only closes the visible seam.
            System.Action<GameObject, string, Vector3, float, float, float> GateFill =
                (prefab, label, pos, yaw, target, openingWidth) =>
            {
                var g = Make(prefab, label, pos);
                if (g == null) return;
                NormalizeProp(g, target);                                     // uniform: fixes HEIGHT
                // Identify the wider horizontal LOCAL axis (the run, along the wall)
                // vs the thinner one (thickness, through the wall) and stretch the run
                // axis so the gatehouse world-width fills the opening flush.
                if (openingWidth > 0.01f && TryMeasureLocalBounds(g, out var glb))
                {
                    Vector3 s = g.transform.localScale;
                    float worldX = glb.size.x * Mathf.Abs(s.x);
                    float worldZ = glb.size.z * Mathf.Abs(s.z);
                    if (worldX >= worldZ)   // local X is the run axis
                    {
                        if (worldX > 0.01f && openingWidth > worldX) s.x *= openingWidth / worldX;
                    }
                    else                    // local Z is the run axis
                    {
                        if (worldZ > 0.01f && openingWidth > worldZ) s.z *= openingWidth / worldZ;
                    }
                    g.transform.localScale = s;
                }
                g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);         // yaw AFTER the local stretch
                StripColliders(g);
                StripRigidbodies(g);
            };

            // ── North wall (z = +33) — segments + gate at x = 0 (WO-158: 4th gate) ──
            // The "too small / all gates broken" issue was a SCALE bug (gateTarget,
            // fixed above to 10), not a north-specific fault — so north keeps its gate.
            for (float x = -wallX + segStep * 0.5f; x < wallX; x += segStep)
            {
                // Owner 2026-06-02 ("WallPerimeter-N partially blocks north gate"): the
                // ±4.5 m flanking segments (segStep=3 lands them at ±4.5) crowded the gate
                // arch. Widen the north opening to |x|<6 so the wall resumes at ±7.5 and
                // frames the arch cleanly — also a wider clean exit to the overworld.
                if (Mathf.Abs(x) < 6f) continue;   // 12 m north gate opening (clears flanking segment)
                Wall(wallSegModel, "WallPerimeter-North", new Vector3(x, 0f, wallZ), 0f);
            }
            // SEAM FIX (2026-06-02): the |x|<6 opening puts the first full north wall
            // stone at x=±7.5 with a 4.5 m run -> its solid inner edge sits ~5.25 m from
            // centre. Fill the gatehouse to ~10.5 m wide so it meets both flanking stones
            // flush (no seam). Decorative width only; nav gap stays Fortify's north ±6.
            GateFill(gateMedModel, "Gate-North-Main", new Vector3(0f, 0f, wallZ), 0f, gateTarget, 10.5f);

            // ── South wall (z = -33) — segments + main gate at x = 0 ─────────
            for (float x = -wallX + segStep * 0.5f; x < wallX; x += segStep)
            {
                if (Mathf.Abs(x) < 3f) continue;   // 6 m opening for the main gate
                Wall(wallSegModel, "WallPerimeter-South", new Vector3(x, 0f, -wallZ), 0f);
            }
            // SEAM FIX: |x|<3 -> first stone at x=±4.5 (run 4.5) -> inner edge ~2.25 m;
            // fill the gatehouse to ~4.5 m wide so it meets the flanking stones flush.
            GateFill(gateMedModel, "Gate-South-Main", new Vector3(0f, 0f, -wallZ), 0f, gateTarget, 4.5f);

            // ── East wall (x = +42) — segments from z = -33 to +33 ───────────
            for (float z = -wallZ + segStep * 0.5f; z < wallZ; z += segStep)
            {
                if (Mathf.Abs(z) < 3f) continue;   // opening for east gate
                Wall(wallSegModel, "WallPerimeter-East", new Vector3(wallX, 0f, z), 90f);
            }
            // SEAM FIX: same 6 m opening as south -> fill gatehouse to ~4.5 m wide. The
            // run axis here is world Z (90-deg yaw); GateFill stretches the LOCAL run
            // axis BEFORE the yaw, so this is handled correctly without axis bookkeeping.
            GateFill(gateSmModel, "Gate-East-Side", new Vector3(wallX, 0f, 0f), 90f, gateTarget, 4.5f);

            // ── West wall (x = -42) — segments from z = -33 to +33 ───────────
            for (float z = -wallZ + segStep * 0.5f; z < wallZ; z += segStep)
            {
                if (Mathf.Abs(z) < 3f) continue;   // opening for west gate
                Wall(wallSegModel, "WallPerimeter-West", new Vector3(-wallX, 0f, z), 270f);
            }
            // SEAM FIX: mirror of the east gate -> fill gatehouse to ~4.5 m wide.
            GateFill(gateSmModel, "Gate-West-Side", new Vector3(-wallX, 0f, 0f), 270f, gateTarget, 4.5f);

            // ── Corner towers at (±42, 0, ±33) ───────────────────────────────
            Big(towerCorModel, "Tower-NE-Corner", new Vector3( wallX, 0f,  wallZ), 0f, towerTarget);
            Big(towerCorModel, "Tower-NW-Corner", new Vector3(-wallX, 0f,  wallZ), 0f, towerTarget);
            Big(towerCorModel, "Tower-SE-Corner", new Vector3( wallX, 0f, -wallZ), 0f, towerTarget);
            Big(towerCorModel, "Tower-SW-Corner", new Vector3(-wallX, 0f, -wallZ), 0f, towerTarget);

            // ── Mid-wall towers at each cardinal wall midpoint ────────────────
            Big(towerMidModel, "Tower-North-Mid", new Vector3(  -10f, 0f,  wallZ), 0f,   towerTarget);  // off-centre: flank the north gate
            Big(towerMidModel, "Tower-South-Mid", new Vector3(  -10f, 0f, -wallZ), 180f, towerTarget);  // off-centre: flank the south gate
            Big(towerMidModel, "Tower-East-Mid",  new Vector3( wallX, 0f,   -10f), 90f,  towerTarget);  // off-centre: flank the east gate
            Big(towerMidModel, "Tower-West-Mid",  new Vector3(-wallX, 0f,   -10f), 270f, towerTarget);  // off-centre: flank the west gate

            // Count placed pieces for the build log (reuse _propCount as a proxy).
            int segCount = perimeterRoot.transform.childCount;
            Debug.Log($"[VillageSceneBuilder] WO-101 BuildWallPerimeter: {segCount} polyperfect perimeter " +
                      "pieces placed (wall segments + corner/mid towers + cardinal gates). " +
                      "Polyperfect atlas materials URP-correct; colliders stripped.");
        }

        /// <summary>
        /// Scales a flat ground/water plane so its horizontal footprint is exactly
        /// <paramref name="size"/> m on both axes (tiles abut), leaving Y untouched.
        /// Measured from world renderer bounds (planes carry no rotation here).
        /// </summary>
        private static void FitGroundTile(GameObject go, float size)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0) return;
            Bounds wb = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) wb.Encapsulate(rs[i].bounds);
            var s = go.transform.localScale;
            if (wb.size.x > 0.001f) s.x *= size / wb.size.x;
            if (wb.size.z > 0.001f) s.z *= size / wb.size.z;
            go.transform.localScale = s;
        }
    }
}
