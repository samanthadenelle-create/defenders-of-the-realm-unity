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
    // WO-181: plaza, roads, Heart (Elarion), Keep, gameplay buildings -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void BuildPlaza(Transform parent)
        {
            var plazaRoot = NewChild(parent, "Plaza");
            var stone = LoadModel(HexTilesRoads + "hex_road_B.fbx");

            // ~6 hex wide × 5 hex deep block of paving centred on the origin.
            for (int row = -4; row <= 4; row++)
            {
                bool oddRow = (row & 1) != 0;
                float z = row * HexDepth;
                float xShift = oddRow ? HexWidth * 0.5f : 0f;
                for (int col = -5; col <= 5; col++)
                {
                    float x = col * HexWidth + xShift;
                    var tile = InstantiateModel(stone, "hex_road_B.fbx",
                        $"PlazaTile ({col},{row})");
                    tile.transform.SetParent(plazaRoot, false);
                    // WO-192: FLUSH overlay just above the city floor (y=0.02). A flat
                    // visual paving, NOT raised geometry — no ledge for the NavMeshAgent
                    // to get stuck stepping off. Y-scale flattened to a thin skin.
                    tile.transform.localPosition = new Vector3(x, 0.03f, z);
                    FlattenOverlayTile(tile);
                    if (stone == null)
                    {
                        tile.transform.localScale = new Vector3(HexWidth, 0.02f, HexWidth);
                        ApplyColor(tile, new Color(0.62f, 0.60f, 0.56f));
                    }
                    _roadCount++;
                }
            }
            if (stone == null)
                Debug.LogWarning("[VillageSceneBuilder] hex_road_B.fbx missing -- plaza used placeholder paving.");
        }

        /// <summary>
        /// Lays the N-S spine and E-W cross — two 2-hex-wide paved roads forming
        /// a '+' from the plaza out to the four gates (§7.1). KayKit road tiles
        /// overlay the grass floor.
        /// </summary>
        private static void BuildRoads(Transform parent, Type tWallLayout)
        {
            var road = LoadModel(HexTilesRoads + "hex_road_A.fbx");
            var roadsRoot = NewChild(parent, "MainRoads");
            if (road == null)
                Debug.LogWarning("[VillageSceneBuilder] hex_road_A.fbx missing -- main roads used placeholders.");

            // N-S spine: two columns of tiles either side of X=0, the plaza
            // edge out to the N and S wall line.
            for (float z = HexDepth * 4f; z <= WallHalfZ - 1f; z += HexDepth)
            {
                LayRoadPair(roadsRoot, road, true, z);    // north arm
            }
            for (float z = -HexDepth * 4f; z >= -(WallHalfZ - 1f); z -= HexDepth)
            {
                LayRoadPair(roadsRoot, road, true, z);    // south arm
            }
            // E-W cross: two rows either side of Z=0.
            for (float x = HexWidth * 5f; x <= WallHalfX - 1f; x += HexWidth)
            {
                LayRoadPair(roadsRoot, road, false, x);   // east arm
            }
            for (float x = -HexWidth * 5f; x >= -(WallHalfX - 1f); x -= HexWidth)
            {
                LayRoadPair(roadsRoot, road, false, x);   // west arm
            }
        }

        /// <summary>Lays a 2-tile-wide road cross-section at one step along an arm.</summary>
        private static void LayRoadPair(Transform parent, GameObject road, bool northSouth, float along)
        {
            float[] lateral = { -HexWidth, 0f, HexWidth };
            foreach (var off in lateral)
            {
                var tile = InstantiateModel(road, "hex_road_A.fbx",
                    northSouth ? $"Road-NS ({along:0.0})" : $"Road-EW ({along:0.0})");
                tile.transform.SetParent(parent, false);
                // WO-192: roads are a FLUSH visual overlay (y=0.03, just above the
                // city floor at y=0.02), NOT raised blocks. The old 0.015 height +
                // the FBX's own mesh thickness made a ledge the NavMeshAgent couldn't
                // step off — flatten the tile to a thin skin so it's paint, not a wall.
                Vector3 p = northSouth
                    ? new Vector3(off, 0.03f, along)
                    : new Vector3(along, 0.03f, off);
                tile.transform.localPosition = p;
                FlattenOverlayTile(tile);
                if (road == null)
                {
                    tile.transform.localScale = new Vector3(HexWidth, 0.02f, HexWidth);
                    ApplyColor(tile, new Color(0.55f, 0.46f, 0.34f));
                }
                _roadCount++;
            }
        }

        // =====================================================================
        //  Centerpiece 1 — Elarion the world-tree (§3.1)
        // =====================================================================

        /// <summary>
        /// Elarion — the sentient world-tree. Sits on a raised mound centre-west
        /// of the plaza, scaled ~3× normal, with violet emissive crystalline
        /// veins and a 6-stone ring (§14 Q3 default). NO building on its hex.
        /// </summary>
        private static Component BuildElarion(Transform parent)
        {
            // Owner direction 2026-05-20: replace the rock-cluster + tree
            // centerpiece with the Tripo fantasy cathedral at village centre,
            // scaled up to read as the village spire. The cathedral arrived
            // as Assets/Models/Cathedral/Cathedral.fbx (Tripo embedded-mat
            // FBX — TripoMaterialFixer rebuilds the URP material on Awake).
            Vector3 site = new Vector3(0f, 0f, 1f);

            var go = new GameObject("Heart (Elarion Tree of Life)");
            go.transform.SetParent(parent, false);
            go.transform.position = site;

            // ── Cathedral spire (Tripo FBX) ─────────────────────────────────
            // Owner 2026-05-20: skip the ElarionMound cylinder when the
            // cathedral loads — the green disk peeked out from under the
            // spire as an ugly leftover.
            GameObject cathedralInstance = null;
            // Owner direction 2026-05-25: REVERSE the 2026-05-20 cathedral-spire
            // call. The Cathedral "spire" renders as a rainbow-patchwork Tripo
            // mesh and is an 84MB memory hog — remove it and restore the Elarion
            // world-tree ("Tree of Life") centerpiece (raised mound + large tree
            // + violet crystal veins + 6 standing stones) built in the blocks
            // below, which all run when cathedralModel is null.
            GameObject cathedralModel = null;
            if (cathedralModel == null)
            {
                var mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mound.name = "ElarionMound";
                mound.transform.SetParent(go.transform, false);
                mound.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                mound.transform.localScale = new Vector3(5.0f, 0.35f, 5.0f);
                ApplyColor(mound, new Color(0.34f, 0.42f, 0.24f));
            }
            if (cathedralModel != null)
            {
                var cathedral = (GameObject)PrefabUtility.InstantiatePrefab(cathedralModel);
                cathedral.name = "CathedralSpire";
                cathedral.transform.SetParent(go.transform, false);
                cathedral.transform.localPosition = Vector3.zero;
                // Owner 2026-05-21: bumped from 8.05m → 16m so the new
                // dragon-tower Cathedral FBX (88MB, Tripo) reads at city-edge
                // distance — the dragon detail and stained-glass rose window
                // both want a taller silhouette to resolve as architecture
                // instead of as stone-coloured noise.
                NormalizeProp(cathedral, 16f);
                StripColliders(cathedral);
                // Owner 2026-05-20 "spire sits off the ground a little":
                // after NormalizeProp, the post-scale mesh bottom isn't
                // necessarily at y=0 (Tripo FBX pivots vary). Measure the
                // world-space bounds and lower the spire so feet land flush
                // on the village floor.
                SnapFeetToParent(cathedral);
                cathedralInstance = cathedral;
                // Attach the runtime URP-material fixer with a stone-grey tint
                // fallback so the cathedral reads as stone even if Tripo's
                // embedded basecolor extract didn't survive the build (owner
                // 2026-05-20 "still not colored"). The texture is preferred
                // when present; tint is the safety net.
                var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fixerType != null)
                {
                    var fixer = cathedral.AddComponent(fixerType);
                    var setTex = fixerType.GetMethod("SetFallbackTexture");
                    // Real basecolor from Tripo Send-To-Unity extract ships at
                    // Resources/Textures/Cathedral.png (~26 MB).
                    setTex?.Invoke(fixer, new object[] { "Textures/Cathedral" });
                    var setTint = fixerType.GetMethod("SetFallbackTint");
                    // Stone-grey with a faint warm bias — reads as cathedral
                    // limestone if the texture doesn't load. NOT white anymore
                    // (white = invisible against the sky / hard to read).
                    setTint?.Invoke(fixer, new object[] { new Color(0.74f, 0.72f, 0.68f) });
                }
                Debug.Log("[VillageSceneBuilder] Cathedral spire mounted at heart, scaled to ~8m, feet snapped to ground.");
            }
            else
            {
                // The Cathedral spire was retired (owner 2026-05-25); the Elarion world-tree
                // (Tree of Life) is the canon Heart centerpiece, built in the block below.
                // (No warning — the tree is intended, not a fallback for a missing asset.)
            }

            // ── The tree / spire ─────────────────────────────────────────────
            // Owner 2026-05-20: cathedral mesh replaces the tree. Skip the
            // tree block entirely when cathedralModel loaded successfully.
            if (cathedralModel == null)
            {
                // Owner 2026-05-25: real Tripo "Tree of Life" centerpiece model at
                // Resources/Structures/tree_of_life.fbx (with its own basecolor/normal
                // maps). Prefer it; fall back to the KayKit hex tree, then a primitive.
                var tolModel = LoadModel("Assets/Resources/Structures/tree_of_life.fbx");
                GameObject tree;
                if (tolModel != null)
                {
                    tree = (GameObject)PrefabUtility.InstantiatePrefab(tolModel);
                    tree.name = "ElarionTree";
                    tree.transform.SetParent(go.transform, false);
                    tree.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                    // Recent owner Tripo exports import upright (the -90°X that stands
                    // the OLD buildings up tips the NEW ones onto their backs), so
                    // leave rotation identity — VERIFY orientation after the re-bake.
                    NormalizeProp(tree, 9f);   // tall centerpiece rising over the mound
                    StripColliders(tree);
                    StripRigidbodies(tree);
                    SnapFeetToParent(tree);
                    var fxType = FindType("DeNelle.Core.TripoMaterialFixer");
                    if (fxType != null)
                    {
                        var fx = tree.AddComponent(fxType);
                        fxType.GetMethod("SetFallbackTint")?.Invoke(fx, new object[] { new Color(0.24f, 0.42f, 0.22f) });
                        fxType.GetMethod("ForceRebuildAll")?.Invoke(fx, null);
                    }
                }
                else
                {
                    var treeModel = LoadModel(HexDecoNature + "trees_A_large.fbx");
                    if (treeModel != null)
                    {
                        tree = InstantiateModel(treeModel, "trees_A_large.fbx", "ElarionTree");
                        tree.name = "ElarionTree";
                        tree.transform.SetParent(go.transform, false);
                        tree.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                        tree.transform.localScale = Vector3.one * 3.0f;
                        ApplyColorAll(tree, new Color(0.24f, 0.42f, 0.22f));
                    }
                    else
                    {
                        tree = new GameObject("[PLACEHOLDER] Elarion world-tree");
                        NotePlaceholder("Elarion world-tree");
                        tree.transform.SetParent(go.transform, false);
                        tree.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                        PrimitiveChild(tree.transform, "Trunk", PrimitiveType.Cylinder,
                            new Vector3(0f, 4f, 0f), new Vector3(1.4f, 4f, 1.4f),
                            new Color(0.30f, 0.21f, 0.13f));
                        PrimitiveChild(tree.transform, "Canopy", PrimitiveType.Sphere,
                            new Vector3(0f, 9f, 0f), new Vector3(7f, 6f, 7f),
                            new Color(0.24f, 0.42f, 0.22f));
                    }
                }
            }

            // Skip the violet veins + standing-stone ring when the cathedral
            // is the centerpiece (owner 2026-05-20 — the rock cluster + glow
            // shards read as clutter at the new village centre).
            if (cathedralModel != null) goto SkipLegacyDressing;

            // ── Violet emissive crystalline veins up the trunk (§3.1) ────────
            // WO-136: DISABLED (owner 2026-05-30) — the violet veins read as "tree
            // roots"/clutter on the trunk. The Tree of Life centerpiece STAYS; only
            // these emissive vein shards are removed. (Block kept for re-enable.)
            // Color veinColor = HexColor("9d6fff");
            // var veinsRoot = NewChild(go.transform, "CrystalVeins");
            // for (int i = 0; i < 5; i++) { ... emissive vein shards ... }  // WO-136: disabled

            // ── 6-stone ring of standing stones (§3.1 / §14 Q3 default 6) ────
            // No prop_stone_pillar in this pack -- rock_single_C is the upright
            // boulder that best reads as a standing stone.
            var stoneModel = LoadModel(HexDecoNature + "rock_single_C.fbx");
            var ringRoot = NewChild(go.transform, "StandingStones");
            const int stoneCount = 6;
            const float ringRadius = 4.4f;
            for (int i = 0; i < stoneCount; i++)
            {
                float ang = (360f / stoneCount) * i;
                Vector3 sp = new Vector3(
                    Mathf.Cos(ang * Mathf.Deg2Rad) * ringRadius,
                    0.2f,
                    Mathf.Sin(ang * Mathf.Deg2Rad) * ringRadius);
                var stone = InstantiateModel(stoneModel, "rock_single_C.fbx",
                    $"StandingStone-{i}");
                stone.name = $"StandingStone-{i}";
                stone.transform.SetParent(ringRoot, false);
                stone.transform.localPosition = sp;
                stone.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
                if (stoneModel != null)
                {
                    stone.transform.localScale = new Vector3(1.1f, 2.0f, 1.1f);
                    // The KayKit hex rock atlas does not resolve onto these
                    // meshes (they render white even when the shared material
                    // is force-assigned). Tint the ring a pale violet-grey so it
                    // reads as standing stone and ties to Elarion's crystalline
                    // veins (spec §3.1) — see unity-decisions.md 2026-05-19.
                    ApplyColorAll(stone, new Color(0.62f, 0.60f, 0.66f));
                }
                else
                {
                    stone.transform.localScale = new Vector3(0.7f, 2.2f, 0.7f);
                    stone.transform.localPosition += Vector3.up * 1.1f;
                    ApplyColor(stone, new Color(0.5f, 0.49f, 0.46f));
                }
                _propCount++;
            }

            SkipLegacyDressing:
            // Capture the authored transform before adding HeartController.
            // Awake() will snap to origin + scale unless _useAuthoredTransform=true.
            // We persist BOTH the toggle AND a fallback: directly re-apply the
            // authored values right after the component is added so even if the
            // SerializedObject write doesn't survive scene save, runtime gets
            // the right transform written into the scene asset. Fixes the
            // standing-stones "floating in air" symptom (2026-05-19).
            Vector3 authoredPos   = go.transform.position;
            Vector3 authoredScale = go.transform.localScale;

            var heartComp = AddVillageComponent(go, TypeHeartController);
            if (heartComp != null)
            {
                var so = new SerializedObject(heartComp);
                var prop = so.FindProperty("_useAuthoredTransform");
                if (prop != null) { prop.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
                else Debug.LogWarning("[VillageSceneBuilder] HeartController._useAuthoredTransform not found -- Elarion may snap to origin at Play.");
            }

            // Re-stamp the authored transform AFTER the controller is wired.
            go.transform.position   = authoredPos;
            go.transform.localScale = authoredScale;

            // Owner 2026-05-20: random cinematic dragon fly-bys across Elarion.
            // Foreshadows the apex wave-boss without interrupting gameplay.
            // Lives in DeNelle.Village (Cinematics) so the editor asmdef stays
            // free of a runtime dependency — resolved by reflection here, same
            // pattern as DragonBoss is wired through.
            var flybyType = FindType("DeNelle.Village.Cinematics.DragonCinematicFlyby");
            if (flybyType != null)
            {
                var flybyGo = new GameObject("DragonCinematicFlyby");
                flybyGo.transform.SetParent(go.transform, false);
                flybyGo.transform.localPosition = Vector3.zero; // village centre
                flybyGo.AddComponent(flybyType);
                Debug.Log("[VillageSceneBuilder] DragonCinematicFlyby attached at village centre " +
                          "— random cinematic dragon fly-bys scheduled.");
            }
            else
            {
                Debug.LogWarning("[VillageSceneBuilder] DeNelle.Village.Cinematics.DragonCinematicFlyby " +
                                 "not found — is the DeNelle.Village assembly compiled? Cameo fly-bys will not run.");
            }

            return heartComp;
        }

        // =====================================================================
        //  Centerpiece 2 — the Keeper's Keep (§3.2)
        // =====================================================================

        /// <summary>
        /// The Keeper's Keep — building_castle, placed adjacent to Elarion and
        /// slightly south-east so the two anchors frame the plaza (§3.2). Modest
        /// 2×2-hex footprint (§14 Q1 default). A violet banner flanks it (§3.2).
        /// </summary>
        private static void BuildKeep(Transform parent)
        {
            // Owner direction 2026-05-20 ("THESE TWO THINGS NEED REMOVED"):
            // the Keep building_castle + violet Avalon Banner both read as
            // a flat-untextured block + a tall violet pole next to the
            // cathedral spire — clutter, not centerpiece. Both removed.
            // The plaza centre is now the cathedral alone.
        }

        // =====================================================================
        //  Five gameplay buildings (§5)
        // =====================================================================

        private struct BuildingPlacement
        {
            public int Type;            // BuildingType enum ordinal
            public string Id;
            public string Label;
            public float X;
            public float Z;
            public float YawDeg;        // face the road / plaza
            public string Fbx;          // base name in buildings/<color>/
            public Color PlaceholderColor;
            public string FenceKind;    // "wood" or "stone"
            public string CustomFbx;    // optional full asset path to a custom (Tripo) FBX; overrides Fbx/Building() when set
            public string BaseColorTex; // optional: force this basecolor texture onto a clean URP/Lit material (single-texture Tripo buildings)
            public string PrefabM;      // WO-101: full asset path to a lightweight polyperfect _M prefab; wins over CustomFbx/Fbx when set
            public string PrefabM2;     // WO-101: optional secondary polyperfect _M prefab placed alongside the primary (offset by +3m X)
        }

        // WO-101: polyperfect Low Poly Ultimate Pack — mobile (_M) Medieval prefabs.
        private const string PolyMedievalDir =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/";

        // WO-101: secondary polyperfect prefab folder (Farm_House lives in Farm_M, not Medieval_M).
        private const string PolyFarmDir =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Farm_M/";

        // Quadrant placements per spec §5 / WO-101 Phase B.
        // N = +Z. Positions updated to WO-101 spec; Cathedral/Heart of Elarion NOT replaced.
        // PrefabM  = primary polyperfect _M prefab (wins over Tripo CustomFbx / KayKit Fbx).
        // PrefabM2 = optional secondary polyperfect prefab placed +3 m offset from primary.
        private static readonly BuildingPlacement[] Buildings =
        {
            // WO-150: Crystal Mine REMOVED from the village roster — the crystal
            // economy is relocating to harvestable WORLD NODES (WO-111 resource
            // pillar / outer-world regions), not lost. Its old plot (-20/+10) frees
            // up so the remaining five read as one-building-per-area. (Entry kept
            // commented for reference / future re-enable if ever village-bound.)
            // new BuildingPlacement { Type = 0, Id = "crystal-mine", Label = "Crystal Mine",
            //     X = -20f, Z = 10f, YawDeg = 135f, Fbx = "building_mine",
            //     CustomFbx = "Assets/Art/TripoStructures/LumberMill.fbx",
            //     BaseColorTex = "Assets/Art/TripoStructures/LumberMill.fbm/LumberMill_basecolor.JPEG",
            //     PrefabM  = PolyMedievalDir + "House_Medieval_Small.prefab",
            //     PrefabM2 = PolyMedievalDir + "Well.prefab",
            //     PlaceholderColor = new Color(0.38f, 0.65f, 0.98f), FenceKind = "stone" },
            // Echo Hollow (WO-338: rebranded from "Pet House") — WO-101: SM_Stables_Medieval.
            // Internal id stays "pet-house" (keyed by catalog/PetDeployer/DialogueService/Yarn node).
            // DEF-101: position (+20, 0, +10) — safe clearance from all gates.
            new BuildingPlacement { Type = 1, Id = "pet-house", Label = "Echo Hollow",
                X = 20f, Z = 10f, YawDeg = 55f, Fbx = "building_stables",
                CustomFbx = "Assets/Art/TripoStructures/PetHome.fbx",
                PrefabM = PolyMedievalDir + "Stables_Medieval.prefab",
                PlaceholderColor = new Color(0.98f, 0.82f, 0.48f), FenceKind = "wood" },
            // Arcane Tower — WO-101: SM_Tower_Medieval_Big.
            // DEF-101: position (-20, 0, -10) — safe clearance from all gates.
            new BuildingPlacement { Type = 2, Id = "arcane-tower", Label = "Arcane Tower",
                X = -20f, Z = -10f, YawDeg = 0f, Fbx = "building_tower_A",
                CustomFbx = "Assets/Art/TripoStructures/BuildTower.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/BuildTower.fbm/build_tower_basecolor.JPEG",
                PrefabM = PolyMedievalDir + "Tower_Medieval_Big.prefab",
                PlaceholderColor = new Color(0.65f, 0.55f, 0.98f), FenceKind = "stone" },
            // Forge (WO-150: relabeled from "Workshop") — WO-101: SM_House_Medieval_Medium.
            // DEF-101: position (+20, 0, -10) — safe clearance from all gates.
            // Id stays "workshop" (Building.Type=3 dispatch / interactor wiring is keyed
            // on it); only the player-facing Label changes to "Forge".
            new BuildingPlacement { Type = 3, Id = "workshop", Label = "Forge",
                X = 20f, Z = -10f, YawDeg = 215f, Fbx = "building_workshop",
                CustomFbx = "Assets/Art/TripoStructures/Forge.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/Forge.fbm/Forge_basecolor.JPEG",
                PrefabM = PolyMedievalDir + "House_Medieval_Medium.prefab",
                PlaceholderColor = new Color(1f, 0.60f, 0.32f), FenceKind = "wood" },
            // Farm — WO-101: SM_Farm_House (primary) + SM_Windmill_Medieval (secondary).
            // DEF-101: position (-15, 0, +20) — moved off the north gate (was 0,+25).
            new BuildingPlacement { Type = 4, Id = "farm", Label = "Farm",
                X = -15f, Z = 14f, YawDeg = 270f, Fbx = "building_windmill", // WO-126 Bug2: Z 20->14 clears z=21 inner wall
                CustomFbx = "Assets/Art/TripoStructures/Farm.fbx",
                BaseColorTex = "Assets/Art/TripoStructures/Farm.fbm/farm_basecolor.JPEG",
                PrefabM  = PolyFarmDir + "Farm_House.prefab",
                PrefabM2 = PolyMedievalDir + "Windmill_Medieval.prefab",
                PlaceholderColor = new Color(1f, 0.85f, 0.54f), FenceKind = "wood" },
            // Market — WO-101: SM_House_Medieval_Large + SM_Marketplace_Stand_Simple.
            // DEF-101: position (+15, 0, -20) — off the south gate.
            // NOTE (2026-06-02): Market reuses Type=5, which NOW maps to the new
            // BuildingType.Lumbermill enum value. This is BENIGN: Building identity is
            // keyed by Id ("market"), and BuildingInteractable's F-prompt for an
            // un-cased type falls through to label "Building" / "Interaction unavailable"
            // (unchanged behaviour). Flagged for the owner — Market wants its own enum
            // value eventually; left as-is here to avoid touching working Market wiring.
            new BuildingPlacement { Type = 5, Id = "market", Label = "Market",
                X = 15f, Z = -20f, YawDeg = 0f, Fbx = "building_market",
                PrefabM  = PolyMedievalDir + "House_Medieval_Large.prefab",
                PrefabM2 = PolyMedievalDir + "Marketplace_Stand_Simple.prefab",
                PlaceholderColor = new Color(0.78f, 0.55f, 0.30f), FenceKind = "stone" },
            // Sawmill (catalog id "lumbermill", BuildingType.Lumbermill = 5) — owner ask
            // "farm sawmill armorer". Upgrade-district building in the SW outer quadrant.
            // Position (-22, 0, -22): >24 m from every cardinal gate (clears the 8 m
            // DEF-101 guard) and >=12 m from the arcane-tower plot. Fbx building_lumbermill
            // (KayKit Medieval Hexagon). Player-facing nameplate "Sawmill" via DisplayLabel.
            new BuildingPlacement { Type = 5, Id = "lumbermill", Label = "Sawmill",
                X = -22f, Z = -22f, YawDeg = 45f, Fbx = "building_lumbermill",
                PlaceholderColor = new Color(0.62f, 0.45f, 0.28f), FenceKind = "wood" },
            // Armorer (catalog id "forge", BuildingType.Forge = 6) — owner ask. NOTE the
            // existing workshop is already LABELLED "Forge"; this new building uses the
            // player-facing name "Armorer" to avoid two "Forge" nameplates (see report).
            // Upgrade-district building in the NE outer quadrant. Position (+22, 0, +22):
            // >24 m from every gate (clears the 8 m guard) and >=12 m from the pet-house
            // plot. Fbx building_blacksmith (KayKit). Nameplate "Armorer" via DisplayLabel.
            new BuildingPlacement { Type = 6, Id = "forge", Label = "Armorer",
                X = 22f, Z = 22f, YawDeg = 225f, Fbx = "building_blacksmith",
                PlaceholderColor = new Color(0.58f, 0.58f, 0.62f), FenceKind = "stone" },
        };

        /// <summary>
        /// DEF-101 gate-clearance guard: logs an error if a building centroid sits
        /// within 8 m of any cardinal gate, where it would voxelize into the navmesh
        /// approach corridor and block the enemy lane (the Farm-on-the-north-gate bug).
        /// </summary>
        private static void ValidateBuildingGateClearance(string label, Vector3 centroid)
        {
            const float MinGateClearance = 8f;
            var gates = new[]
            {
                new Vector3(0f, 0f, -33f), new Vector3(42f, 0f, 0f),
                new Vector3(-42f, 0f, 0f), new Vector3(0f, 0f, 33f),
            };
            var flat = new Vector3(centroid.x, 0f, centroid.z);
            foreach (var g in gates)
            {
                float dist = Vector3.Distance(flat, g);
                if (dist < MinGateClearance)
                    Debug.LogError($"[VillageSceneBuilder] DEF-101 gate-clearance violation: " +
                                   $"building '{label}' at {centroid} is {dist:0.0} m from gate {g} " +
                                   $"-- minimum is {MinGateClearance} m. Move it off the gate lane.");
            }
        }

        private static int BuildBuildings(Transform parent, Component controller)
        {
            int count = 0;
            foreach (var b in Buildings)
            {
                PlaceBuilding(parent, b, controller, seatToGround: false);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Places ONE building (KayKit/poly/Tripo model + footprint collider + Building
        /// component + forge yard) under <paramref name="parent"/> at its spec position.
        /// Extracted from BuildBuildings so the Village2 injector can reuse the EXACT
        /// placement (if-not-exists, delta-only). <paramref name="seatToGround"/>
        /// raycasts the plot down onto the scene ground (Village2's terrain Y may differ
        /// from the legacy Village's Y=0). Returns the plot root GameObject.
        /// </summary>
        private static GameObject PlaceBuilding(Transform parent, BuildingPlacement b, Component controller, bool seatToGround)
        {
                var go = new GameObject($"Building-{b.Id} ({b.Label})");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(b.X, 0f, b.Z);
                go.transform.rotation = Quaternion.Euler(0f, b.YawDeg, 0f);

                // DEF-101: assert this building clears every cardinal gate by >= 8 m.
                ValidateBuildingGateClearance(b.Label, new Vector3(b.X, 0f, b.Z));

                // WO-101: a lightweight polyperfect _M building when PrefabM is set
                // (replaces the heavy Tripo mesh — the Seeker file-size win). Instantiated
                // DIRECTLY via PrefabUtility so its atlas material survives —
                // InstantiateModel's ForceHexMaterial would clobber it. Else: the legacy
                // path (owner Tripo CustomFbx, or native KayKit).
                bool poly   = !string.IsNullOrEmpty(b.PrefabM);
                bool custom = !poly && !string.IsNullOrEmpty(b.CustomFbx);
                GameObject model = poly
                    ? AssetDatabase.LoadAssetAtPath<GameObject>(b.PrefabM)
                    : LoadModel(custom ? b.CustomFbx : Building(b.Fbx));
                GameObject visual = (poly && model != null)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(model)
                    : InstantiateModel(model,
                        custom ? System.IO.Path.GetFileName(b.CustomFbx)
                               : b.Fbx + "_" + BuildingColor + ".fbx",
                        $"{(poly ? b.PrefabM : (custom ? b.CustomFbx : b.Fbx))} -> {b.Label}");
                visual.transform.SetParent(go.transform, false);
                if (model == null)
                {
                    visual.transform.localScale = new Vector3(3f, 3f, 3f);
                    visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    ApplyColor(visual, b.PlaceholderColor);
                }
                else if (custom)
                {
                    // Owner Tripo building. The FBX imports tipped ~90deg (lying
                    // flat), so stand it upright; then normalize the longest
                    // dimension to ~7 m and strip native colliders/rigidbody (the
                    // plot footprint BoxCollider below is the single gameplay collider).
                    visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    NormalizeProp(visual, 7f);
                    StripColliders(visual);
                    StripRigidbodies(visual);

                    if (!string.IsNullOrEmpty(b.BaseColorTex))
                    {
                        // Single-texture building: the auto-extracted Tripo materials
                        // render as a rainbow patchwork, so force a clean URP/Lit
                        // material built straight from the model's basecolor.
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(b.BaseColorTex);
                        var lit = Shader.Find("Universal Render Pipeline/Lit");
                        if (tex != null && lit != null)
                        {
                            var mat = new Material(lit) { name = b.Id + "_basecolor (URP)" };
                            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                            foreach (var rr in visual.GetComponentsInChildren<Renderer>(true))
                            {
                                var arr = rr.sharedMaterials;
                                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                                rr.sharedMaterials = arr;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[VillageSceneBuilder] basecolor '{b.BaseColorTex}' not found for {b.Id}; leaving Tripo materials.");
                        }
                    }
                    else
                    {
                        // Multi-part building (PetHome): rebuild each part's material
                        // as plain URP/Lit carrying its own basecolor (force-rebuild).
                        var bFixer = FindType("DeNelle.Core.TripoMaterialFixer");
                        if (bFixer != null)
                        {
                            var fx = visual.AddComponent(bFixer);
                            bFixer.GetMethod("ForceRebuildAll")?.Invoke(fx, null);
                        }
                    }
                }
                else if (poly)
                {
                    // polyperfect _M building: upright, normalized to ~7 m, colliders +
                    // rigidbodies stripped (the plot footprint BoxCollider below is the only
                    // gameplay collider). Its atlas material is already URP-correct — NO
                    // TripoMaterialFixer / ForceHexMaterial (that's why it isn't InstantiateModel'd).
                    visual.transform.localRotation = Quaternion.identity;
                    NormalizeProp(visual, 7f);
                    StripColliders(visual);
                    StripRigidbodies(visual);
                }
                else
                {
                    // Native KayKit houses are ~1.2m tall; hero capsule is 2m.
                    // Owner direction (2026-05-19): houses should read as 3x
                    // hero height (≈6m). 4.5x lifts a ~1.2m house to ~5.4m.
                    visual.transform.localScale = new Vector3(BuildingScale, BuildingScale, BuildingScale);
                }
                // WO-101: secondary polyperfect prefab (e.g. Well alongside Crystal
                // Mine, Windmill alongside Farm House, Marketplace Stand alongside
                // Market). Placed +3 m to the right of the plot root in its local
                // space so it reads as a companion structure, not an overlap.
                if (!string.IsNullOrEmpty(b.PrefabM2))
                {
                    var model2 = AssetDatabase.LoadAssetAtPath<GameObject>(b.PrefabM2);
                    if (model2 != null)
                    {
                        var visual2 = (GameObject)PrefabUtility.InstantiatePrefab(model2);
                        if (visual2 != null)
                        {
                            visual2.name = model2.name + "_secondary";
                            visual2.transform.SetParent(go.transform, false);
                            visual2.transform.localPosition = new Vector3(3f, 0f, 0f);
                            visual2.transform.localRotation = Quaternion.identity;
                            NormalizeProp(visual2, 4f);   // secondary is smaller than the primary
                            StripColliders(visual2);
                            StripRigidbodies(visual2);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[VillageSceneBuilder] WO-101 secondary prefab not found: '{b.PrefabM2}' " +
                                         $"— secondary mesh for {b.Label} skipped (polyperfect pack re-import needed).");
                    }
                }

                // Owner 2026-05-20: hero used to walk THROUGH buildings —
                // adding a footprint BoxCollider so HeroLocomotion's CapsuleCast
                // blocks the move. Sized from mesh bounds; sits on the plot
                // GameObject (not the visual mesh) so it isn't scaled twice.
                AddBuildingFootprintCollider(go, visual);

                // Low fence marking the 2×2-hex property line (§5 -- dressing,
                // no collider). Build it as a child of the plot, not the
                // building visual, so it stays put.
                BuildPlotFence(go.transform, 3.4f, 3.4f, b.FenceKind);

                var comp = AddVillageComponent(go, TypeBuilding);
                if (comp != null)
                {
                    InvokeConfigure(comp, "Configure", b.Type, b.Id, b.Label);
                    RegisterWith(controller, "RegisterBuilding", comp);
                }

                // DEF-220: the Forge building uses a generic house shell (PrefabM
                // House_Medieval_Medium). Dress it with a working SMITHY so it reads
                // as a forge — an anvil + hammer + sword-blank from the committed
                // Blacksmith asset pack, set out front of the plot. (A dedicated forge
                // BUILDING mesh does not exist in-repo; see report.)
                if (string.Equals(b.Id, "workshop", StringComparison.OrdinalIgnoreCase))
                    BuildForgeYard(go.transform);

                // Village2 injection: drop the plot onto the scene ground (its terrain
                // Y may differ from the legacy Village's Y=0). No-op for the legacy path.
                if (seatToGround) SeatBuildingOnGround(go);
                return go;
        }

        /// <summary>
        /// Raycasts a building plot straight down onto the scene ground/terrain and sets
        /// its Y so it sits flush (used by the Village2 injector). Falls back to Y=0 if
        /// nothing is hit. Temporarily ignores the plot's own footprint collider so the
        /// ray doesn't hit itself.
        /// </summary>
        private static void SeatBuildingOnGround(GameObject go)
        {
            if (go == null) return;
            var ownCols = go.GetComponentsInChildren<Collider>(true);
            foreach (var c in ownCols) if (c != null) c.enabled = false;
            Vector3 p = go.transform.position;
            float y = 0f;
            if (Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out var hit, 1000f))
                y = hit.point.y;
            go.transform.position = new Vector3(p.x, y, p.z);
            foreach (var c in ownCols) if (c != null) c.enabled = true;
        }

        // Committed Blacksmith asset pack (NOT gitignored, unlike polyperfect) — the
        // only in-repo forge/anvil/hammer meshes. Used to dress the Forge plot.
        private const string BlacksmithProps = "Assets/Models/People/Blacksmith/";

        /// <summary>
        /// DEF-220: a small smithy dressing in front of the Forge plot — an anvil,
        /// a hammer and a sword blank — so the generic-house Forge shell reads as a
        /// working forge. Props are the committed SM_* meshes (Assets/Models/People/
        /// Blacksmith), instantiated DIRECTLY (not via InstantiateModel, which would
        /// force the hex atlas onto them) with a clean dark-iron URP material so the
        /// metal reads correctly. Colliders are stripped so they never block pathing.
        /// On a missing mesh: a labelled placeholder cube (Debug.LogWarning), per §4.
        /// </summary>
        private static void BuildForgeYard(Transform plot)
        {
            if (plot == null) return;

            var yard = new GameObject("ForgeYard");
            yard.transform.SetParent(plot, false);
            // In front of the building (plot local +Z), clear of the door.
            yard.transform.localPosition = new Vector3(0f, 0f, 2.6f);

            // Dark-iron URP material for the metal props (clean, de-glossed).
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material iron = null;
            if (lit != null)
            {
                iron = new Material(lit) { name = "ForgeIron" };
                if (iron.HasProperty("_BaseColor"))
                    iron.SetColor("_BaseColor", new Color(0.20f, 0.20f, 0.22f));
                if (iron.HasProperty("_Smoothness")) iron.SetFloat("_Smoothness", 0.35f);
                if (iron.HasProperty("_Metallic")) iron.SetFloat("_Metallic", 0.6f);
            }

            // (mesh file, local pos, yaw, target longest-edge size).
            var props = new (string fbx, Vector3 pos, float yaw, float size)[]
            {
                ("SM_Anvil.fbx",       new Vector3( 0f,   0f, 0f),   20f, 1.1f),
                ("SM_Hammed.fbx",      new Vector3( 0.55f,0f, 0.1f), 70f, 0.7f),
                ("SM_Sword_Blank.fbx", new Vector3(-0.9f, 0f, 0.3f), -30f, 1.0f),
            };

            foreach (var (fbx, pos, yaw, size) in props)
            {
                var model = LoadModel(BlacksmithProps + fbx);
                if (model == null)
                {
                    Debug.LogWarning($"[VillageSceneBuilder] DEF-220 forge prop '{fbx}' not found at " +
                                     $"'{BlacksmithProps + fbx}' — skipped (smithy dressing incomplete).");
                    continue;
                }
                var prop = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (prop == null) continue;
                prop.name = model.name;
                prop.transform.SetParent(yard.transform, false);
                prop.transform.localPosition = pos;
                prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                NormalizeProp(prop, size);
                if (iron != null)
                {
                    foreach (var rr in prop.GetComponentsInChildren<Renderer>(true))
                        rr.sharedMaterial = iron;
                }
                StripColliders(prop);
                StripRigidbodies(prop);
            }

            Debug.Log("[VillageSceneBuilder] DEF-220 ForgeYard: anvil + hammer + sword-blank " +
                      "set out front of the Forge plot (committed Blacksmith pack meshes).");
        }
    }
}
