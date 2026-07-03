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
    // WO-181: plot fences, city dressing, props, approaches -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void BuildPlotFence(Transform parent, float halfX, float halfZ, string kind)
        {
            // Owner direction 2026-05-20: "those wooden things in ground need
            // to disappear" — the per-plot fences read as clutter around the
            // bare yards and add nothing visually. Disabled entirely until a
            // real reason to wrap a plot lands.
            return;
            #pragma warning disable CS0162 // unreachable code retained for re-enable
            string fbx = kind == "stone"
                ? HexNeutral + "fence_stone_straight.fbx"
                : HexNeutral + "fence_wood_straight.fbx";
            var fenceModel = LoadModel(fbx);
            var fenceRoot = NewChild(parent, "PlotFence");

            // Four sides; each side is one fence piece scaled to span.
            (Vector3 pos, float yaw, float span)[] sides =
            {
                (new Vector3(0f, 0f, halfZ), 0f, halfX * 2f),
                (new Vector3(0f, 0f, -halfZ), 0f, halfX * 2f),
                (new Vector3(halfX, 0f, 0f), 90f, halfZ * 2f),
                (new Vector3(-halfX, 0f, 0f), 90f, halfZ * 2f),
            };
            foreach (var (pos, yaw, span) in sides)
            {
                var f = InstantiateModel(fenceModel, Path.GetFileName(fbx), "plot fence");
                f.transform.SetParent(fenceRoot, false);
                f.transform.localPosition = pos;
                f.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                StripColliders(f);
                if (fenceModel != null)
                {
                    // Same fixed-length-module fit as the curtain wall: stretch
                    // the fence piece's long horizontal axis to span the plot
                    // side. Local-space measure -> immune to the side's yaw.
                    FitWallVisualToRun(f, span);
                }
                else
                {
                    f.transform.localScale = new Vector3(span, 0.7f, 0.12f);
                    f.transform.localPosition = pos + Vector3.up * 0.35f;
                    ApplyColor(f, kind == "stone"
                        ? new Color(0.55f, 0.53f, 0.49f)
                        : new Color(0.46f, 0.34f, 0.22f));
                }
                _propCount++;
            }
            #pragma warning restore CS0162
        }

        // =====================================================================
        //  City dressing (§6) — residential / market / workshop / orchard
        // =====================================================================

        private struct DressDef
        {
            public string Name;
            public string Fbx;       // base name (colour appended for coloured buildings)
            public bool Neutral;     // true => no colour suffix (path is neutral/)
            public float X, Z, Yaw;
            public Color PlaceholderColor;
        }

        private static void BuildCityDressing(Transform parent)
        {
            // Owner direction 2026-05-20 ("spread out the town structures
            // wider — the clustered ones prevent navigation"). Spaced each
            // dressing building's footprint by ~1.5× from its prior
            // position so the hero (+ pet pack) can walk between them
            // freely. Quarter labels unchanged.

            // ── §6.1 Residential cluster (SW) — homes around a well ──────────
            var residential = NewChild(parent, "Residential-SW");
            var residentialDefs = new[]
            {
                new DressDef { Name = "Home-A1", Fbx = "building_home_A", X = -30f, Z = -8f, Yaw = 70f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-A2", Fbx = "building_home_A", X = -30f, Z = -18f, Yaw = 95f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-A3", Fbx = "building_home_A", X = -14f, Z = -22f, Yaw = 160f, PlaceholderColor = C("d8c69a") },
                new DressDef { Name = "Home-B1", Fbx = "building_home_B", X = -22f, Z = -23f, Yaw = 200f, PlaceholderColor = C("c9b48a") },
                new DressDef { Name = "Home-B2", Fbx = "building_home_B", X = -32f, Z = -23f, Yaw = 25f, PlaceholderColor = C("c9b48a") },
                new DressDef { Name = "Home-B3", Fbx = "building_home_B", X = -14f, Z = -14f, Yaw = 120f, PlaceholderColor = C("c9b48a") },
            };
            foreach (var d in residentialDefs) PlaceDressing(residential, d, false);
            PlaceDressing(residential,
                new DressDef { Name = "Well", Fbx = "building_well", X = -23f, Z = -16f, Yaw = 0f, PlaceholderColor = C("8aa0b0") },
                false);

            // ── §6.2 Market quarter (around the plaza, south) ────────────────
            var market = NewChild(parent, "Market-S");
            PlaceDressing(market,
                new DressDef { Name = "Market", Fbx = "building_market", X = -4f, Z = -13f, Yaw = 10f, PlaceholderColor = C("c98f4a") },
                false);
            PlaceDressing(market,
                new DressDef { Name = "Tavern", Fbx = "building_tavern", X = 16f, Z = -12f, Yaw = 250f, PlaceholderColor = C("b5793c") },
                false);
            PlaceDressing(market,
                new DressDef { Name = "Church", Fbx = "building_church", X = -3f, Z = 14f, Yaw = 185f, PlaceholderColor = C("d7d2c4") },
                false);

            // ── §6.3 Workshop quarter (NE) — blacksmith + townhall ───────────
            var workshopQ = NewChild(parent, "Workshop-NE");
            PlaceDressing(workshopQ,
                new DressDef { Name = "Blacksmith", Fbx = "building_blacksmith", X = 30f, Z = 13f, Yaw = 230f, PlaceholderColor = C("8a7d6a") },
                false);
            PlaceDressing(workshopQ,
                new DressDef { Name = "Townhall", Fbx = "building_townhall", X = 16f, Z = 12f, Yaw = 200f, PlaceholderColor = C("c2b79a") },
                false);
            BuildWorkshopYard(workshopQ, new Vector3(27f, 0f, 13f));

            // ── §6.4 Farm / orchard (E) — orchard tiles + farmer's hut ───────
            // WO-136: owner manually removed the orchard trees + farmer's hut from
            // the village interior; the BuildOrchard call + FarmersHut dressing are
            // disabled so a full BuildVillage regen no longer resurrects them.
            // (Method kept for reference / future re-enable.) The Orchard-E root is
            // still created empty so any downstream lookups don't null.
            var orchard = NewChild(parent, "Orchard-E");
            // BuildOrchard(orchard, new Vector3(26f, 0f, -1f));   // WO-136: regen trap — disabled
            // PlaceDressing(orchard,
            //     new DressDef { Name = "FarmersHut", Fbx = "building_home_A", X = 31f, Z = -14f, Yaw = 290f, PlaceholderColor = C("d8c69a") },
            //     false);                                          // WO-136: regen trap — disabled

            // ── §6.5 Northern open ground ────────────────────────────────────
            // Owner direction 2026-05-20 ("rock still persists north gate"):
            // the building_shrine + scatter trees (cut-stumps that read as
            // rocks) all removed. Northern open ground is now genuinely
            // open — clean approach to the north gate.
            var northern = NewChild(parent, "Northern-OpenGround");
            // ScatterTrees(northern, new[] { ... }) — removed.
            // The return below short-circuits the legacy trailing
            // ScatterTrees call lower in this method.
            return;
            // A few scattered trees on the northern open ground (§6.5).
            // Kept (not deleted) for an easy revert — pragma silences the intentional CS0162.
#pragma warning disable 0162
            ScatterTrees(northern, new[]
            {
                new Vector3(-9f, 0f, 16f), new Vector3(9f, 0f, 15f),
                new Vector3(-20f, 0f, 6f), new Vector3(21f, 0f, 17f),
            });
#pragma warning restore 0162
        }

        /// <summary>Instantiates one city-dressing building from a <see cref="DressDef"/>.</summary>
        private static void PlaceDressing(Transform parent, DressDef d, bool neutral)
        {
            var go = new GameObject("Dress-" + d.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d.X, 0f, d.Z);
            go.transform.rotation = Quaternion.Euler(0f, d.Yaw, 0f);

            string path = neutral || d.Neutral
                ? HexNeutral + d.Fbx + ".fbx"
                : Building(d.Fbx);
            var model = LoadModel(path);
            var visual = InstantiateModel(model, Path.GetFileName(path), d.Name);
            visual.transform.SetParent(go.transform, false);
            if (model == null)
            {
                visual.transform.localScale = new Vector3(2.6f, 2.6f, 2.6f);
                visual.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                ApplyColor(visual, d.PlaceholderColor);
            }
            else
            {
                visual.transform.localScale = new Vector3(BuildingScale, BuildingScale, BuildingScale);
                // Owner 2026-05-20 (black blobs in screenshot): several
                // KayKit dressing FBXes render as untextured dark shapes
                // because the hex atlas doesn't resolve in URP. Attach the
                // TripoMaterialFixer with the building's placeholder colour
                // as a tint so the building reads as itself, not a black
                // blob. Idempotent — a no-op when the atlas DID bind.
                var fixerType = FindType("DeNelle.Core.TripoMaterialFixer");
                if (fixerType != null)
                {
                    var fixer = visual.AddComponent(fixerType);
                    var setTint = fixerType.GetMethod("SetFallbackTint");
                    setTint?.Invoke(fixer, new object[] { d.PlaceholderColor });
                }
            }
            // Owner 2026-05-20: hero could walk through dressing buildings —
            // add a footprint BoxCollider so HeroLocomotion's sweep cast blocks
            // the move. Same approach as gameplay buildings.
            AddBuildingFootprintCollider(go, visual);
            _dressingCount++;
        }

        /// <summary>
        /// A small fenced yard between Workshop + Blacksmith with anvil / lumber
        /// / tool props (§6.3).
        /// </summary>
        private static void BuildWorkshopYard(Transform parent, Vector3 centre)
        {
            var yard = new GameObject("WorkshopYard");
            yard.transform.SetParent(parent, false);
            yard.transform.position = centre;
            BuildPlotFence(yard.transform, 2.6f, 2.4f, "wood");

            // Props — KayKit decoration/props. Each is normalised to a believable
            // largest-dimension target (metres) so the yard dressing reads at a
            // consistent scale despite the meshes' differing native sizes.
            // Owner direction 2026-05-20: the yard's wood weaponrack + lumber
            // + stone + barrel props read as "items that cause issues and
            // offer no value" — they clutter the plaza and block hero
            // pathing. Yard stripped down to just the plot fence; per-
            // building dressing can be reauthored later when each prop has
            // a real interaction hook.
        }

        /// <summary>
        /// The Farm's orchard — a patch of grass tiles dressed with apple/fruit
        /// trees + haybales around the windmill plot (§6.4).
        /// </summary>
        private static void BuildOrchard(Transform parent, Vector3 centre)
        {
            var orchardRoot = new GameObject("OrchardPlot");
            orchardRoot.transform.SetParent(parent, false);
            orchardRoot.transform.position = centre;

            // A 4×3 grid of fruit trees flanking the windmill (kept clear of
            // the building's own 2×2 plot).
            var treeModel = LoadModel(HexDecoNature + "trees_B_medium.fbx");
            for (int r = -1; r <= 1; r++)
            {
                for (int c = -2; c <= 2; c++)
                {
                    if (Mathf.Abs(c) <= 1 && r == 0) continue; // leave the mill clear
                    var t = InstantiateModel(treeModel, "trees_B_medium.fbx", "orchard tree");
                    t.name = "OrchardTree";
                    t.transform.SetParent(orchardRoot.transform, false);
                    t.transform.localPosition = new Vector3(c * 2.4f, 0f, r * 3.0f - 6f);
                    t.transform.localRotation = Quaternion.Euler(0f, (c + r) * 47f, 0f);
                    if (treeModel != null)
                        // Normalise to a consistent ~3.5m fruit tree -- the raw
                        // mesh size varies, so a flat *1.2 multiplier left the
                        // orchard reading unevenly.
                        NormalizeProp(t, 3.5f);
                    else
                    {
                        t.transform.localScale = new Vector3(1.4f, 2.6f, 1.4f);
                        t.transform.localPosition += Vector3.up * 1.3f;
                        ApplyColor(t, C("4f7a3a"));
                    }
                    _propCount++;
                }
            }
            // Haybales at the orchard edge — normalised to a ~1.4m bale.
            PlaceProp(orchardRoot.transform, HexDecoProps + "haybale.fbx",
                new Vector3(4.5f, 0f, -7f), 25f, "haybale", 1.4f);
            PlaceProp(orchardRoot.transform, HexDecoProps + "haybale.fbx",
                new Vector3(-5f, 0f, -8.5f), -40f, "haybale", 1.4f);
        }

        /// <summary>Scatters single trees at the given world positions (foliage dressing).</summary>
        private static void ScatterTrees(Transform parent, Vector3[] positions)
        {
            var treeModel = LoadModel(HexDecoNature + "tree_single_A.fbx");
            var treesRoot = NewChild(parent, "ScatteredTrees");
            int i = 0;
            foreach (var p in positions)
            {
                var t = InstantiateModel(treeModel, "tree_single_A.fbx", "scattered tree");
                t.name = $"Tree-{i++}";
                t.transform.SetParent(treesRoot, false);
                t.transform.localPosition = p;
                t.transform.localRotation = Quaternion.Euler(0f, i * 63f, 0f);
                if (treeModel != null)
                    // Normalise to a consistent ~5m tree, then apply a small
                    // per-tree size variation on top so the scatter still reads
                    // natural (the variation is now relative to a known base,
                    // not a raw mesh size that differs per import).
                    NormalizeProp(t, 5f * Mathf.Lerp(0.9f, 1.3f, (i % 3) / 3f));
                else
                {
                    t.transform.localScale = new Vector3(1.3f, 2.6f, 1.3f);
                    t.transform.localPosition += Vector3.up * 1.3f;
                    ApplyColor(t, C("3f6e34"));
                }
                _propCount++;
            }
        }

        /// <summary>
        /// Instantiates one KayKit prop at a local position; placeholder on miss.
        /// <paramref name="targetSize"/> is the prop's largest world dimension in
        /// metres — every prop is normalised to it via <see cref="NormalizeProp"/>
        /// so props from different KayKit folders read at a consistent scale.
        /// </summary>
        private static void PlaceProp(Transform parent, string assetPath, Vector3 localPos,
            float yaw, string label, float targetSize = 1.0f)
        {
            var model = LoadModel(assetPath);
            var prop = InstantiateModel(model, Path.GetFileName(assetPath), label);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPos;
            prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (model == null)
            {
                prop.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                prop.transform.localPosition = localPos + Vector3.up * 0.35f;
                ApplyColor(prop, C("9a8a6a"));
            }
            else
            {
                // KayKit props have inconsistent native mesh sizes -- normalise
                // each to a common yardstick so the dressing reads coherently.
                NormalizeProp(prop, targetSize);
            }
            _propCount++;
        }

        // =====================================================================
        //  Approach lanes + wave spawn points (§8)
        // =====================================================================

        // WO-27 (playable loop): enemies materialize this far OUTSIDE each gate and
        // march in down a paved corridor. World units (gate -> spawn ring), so the
        // distance is identical for N/S and E/W gates regardless of hex step. The
        // corridor + apron are built under the nav-static "Approaches" root and so
        // are included in BakeVillageNavMesh -> a continuous march lane to the gate.
        private const float ApproachLength = 40f;

        private static int BuildApproaches(Transform parent, Type tWallLayout, Component controller)
        {
            if (tWallLayout == null) return 0;
            var gates = ReadEnumerable(tWallLayout, "Gates");
            if (gates == null) return 0;

            var road = LoadModel(HexTilesRoads + "hex_road_A.fbx");
            var grass = LoadModel(HexTilesBase + "hex_grass.fbx");
            int spawnCount = 0;

            foreach (var gap in gates)
            {
                int index = (int)GetMember(gap, "Index");
                string direction = (string)GetMember(gap, "Direction");
                Vector3 gatePos = (Vector3)GetMember(gap, "Position");
                Vector3 outward = (Vector3)GetMember(gap, "OutwardNormal");

                var laneRoot = new GameObject($"Approach-{direction}");
                laneRoot.transform.SetParent(parent, false);

                float step = (Mathf.Abs(outward.z) > 0.5f) ? HexDepth : HexWidth;

                // WO-27: paved march corridor the full ApproachLength (40 m) out
                // each gate, 5 tiles wide (~8 m), so the NavMesh bakes a continuous
                // lane for the enemies to march down. Loops outward in hex steps
                // until the corridor reaches the spawn ring.
                Vector3 lateral = new Vector3(-outward.z, 0f, outward.x); // perpendicular
                int steps = Mathf.CeilToInt(ApproachLength / step);
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 along = gatePos + outward * (i * step);
                    foreach (var lat in new[] { -2f * HexWidth, -HexWidth, 0f, HexWidth, 2f * HexWidth })
                    {
                        var tile = InstantiateModel(road, "hex_road_A.fbx",
                            $"ApproachRoad-{direction}-{i}");
                        tile.transform.SetParent(laneRoot.transform, false);
                        tile.transform.position = along + lateral * lat + Vector3.up * 0.015f;
                        if (road == null)
                        {
                            tile.transform.localScale = new Vector3(HexWidth, 0.14f, HexWidth);
                            ApplyColor(tile, new Color(0.55f, 0.46f, 0.34f));
                        }
                        _roadCount++;
                    }
                }

                // Lane foliage / boulders removed per owner direction 2026-05-20
                // ("rocks in front of entrance"). Bare paving + grass apron only.

                // WO-27: the wave-spawn apron — a ~16 m x 16 m flat grass pad at the
                // corridor end (ApproachLength out), room for a full 12-enemy batch
                // to materialize on baked navmesh without overlap. Overlaps the
                // corridor end so apron + corridor navmesh are one continuous surface.
                Vector3 spawnCentre = gatePos + outward * ApproachLength;
                var zoneRoot = new GameObject($"SpawnZone-{direction}");
                zoneRoot.transform.SetParent(laneRoot.transform, false);
                zoneRoot.transform.position = spawnCentre;
                for (int gx = -5; gx <= 5; gx++)
                {
                    for (int gz = -5; gz <= 5; gz++)
                    {
                        var tile = InstantiateModel(grass, "hex_grass.fbx", "spawn tile");
                        tile.transform.SetParent(zoneRoot.transform, false);
                        tile.transform.localPosition = new Vector3(gx * HexWidth, 0.01f, gz * HexDepth);
                        if (grass == null)
                        {
                            tile.transform.localScale = new Vector3(HexWidth, 0.18f, HexWidth);
                            ApplyColor(tile, C("4a5a32"));
                        }
                        else
                        {
                            // Owner 2026-06-01 (DEF-25 "yellow plane"): the loaded hex_grass FBX renders
                            // YELLOW on its native atlas in this spawn apron beyond each gate. Tint it the
                            // same mossy green as the village interior so the apron reads as grass, not a
                            // yellow slab. Instance-only (ApplyColorAll touches this tile's renderers only).
                            ApplyColorAll(tile, C("4a5a32"));
                        }
                        _groundCount++;
                    }
                }

                // The WaveSpawnPoint marker (§8.3) — invisible empty GO.
                var spawnGo = new GameObject($"WaveSpawnPoint-{direction}");
                spawnGo.transform.SetParent(zoneRoot.transform, false);
                spawnGo.transform.localPosition = Vector3.zero;
                var spawnComp = AddVillageComponent(spawnGo, TypeWaveSpawnPoint);
                if (spawnComp != null)
                {
                    InvokeConfigure(spawnComp, "Configure",
                        "spawn-" + index, index, direction, gatePos);
                }
                spawnCount++;
            }
            _ = controller;
            return spawnCount;
        }
    }
}
