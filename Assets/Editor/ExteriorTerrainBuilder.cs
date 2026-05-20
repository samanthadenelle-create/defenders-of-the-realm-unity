// =============================================================================
// ExteriorTerrainBuilder — Avalon EXTERIOR wilderness generator (Editor-only).
// -----------------------------------------------------------------------------
// One static entry point that the main Unity session runs (manually via the
// Defenders menu, or via the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.ExteriorTerrainBuilder.BuildExterior
//
// What it does (avalon-village-layout-spec.md §9 -- the outer landscape):
//   1. Opens Assets/Scenes/Village.unity (the walled interior is a SEPARATE
//      agent's job -- this builder only adds the world AROUND it).
//   2. Creates a single Unity Terrain (~300x300 world units) centred on the
//      village, its base offset so the heightmap straddles world Y=0 (the
//      village sits at Y=0). A flat plateau under the wall footprint keeps the
//      seam at Y=0 (§9.8); a soft falloff blends the plateau into the biomes.
//   3. Heightmaps 4 directional biomes (§9.2 / §9.3):
//        North  -- temperate forest rising to Y+15..+30, snow at the edge
//        East   -- gentle rolling farmland, +/-5u
//        South  -- descending barren toward "the Wound", Y-10..-15
//        West   -- river valley: ridge +12 / valley -5 / ridge +10, a river
//      Micro features: boulders, 1-2 cliffs, 6 small ponds.
//   4. Paints 5 terrain splat layers (§9.3): grass / exposed stone / mud path /
//      snow (north, above Y+20) / dark dead ground (south, below Y-8). Slope +
//      elevation rules drive the blend; soft.
//   5. Paints 5 creative "natural" paths into the mud layer (§9.4) -- landscape
//      storytelling, NOT gameplay routes.
//   6. Paints ~200-400 trees with Unity's Terrain tree system (treePrototypes +
//      treeInstances), biome-distributed (§9.6). Uses the KayKit Forest Nature
//      Pack tree meshes (Tree_* / Tree_Bare_*); falls back to the Hexagon
//      pack's nature trees if the Forest pack is absent.
//   7. Scatters boulders + cliff rocks (Forest Pack Rock_* meshes) on slopes.
//   8. Sets a soft-dawn procedural skybox + atmospheric fog via RenderSettings
//      (§9.5), and places three distant-landmark suggestions: a northern
//      mountain peak, a western tower silhouette, a southern "Wound" crack.
//
// IDEMPOTENT -- re-running BuildExterior() destroys the generated
// "ExteriorRoot" (terrain + props + landmarks) and the generated TerrainData
// asset, then rebuilds from scratch. Safe to run repeatedly.
//
// ASSEMBLY / SCOPE NOTES.
//   * Writes only inside Assets/. Creates Assets/Generated/Terrain/ for the
//     TerrainData + skybox material assets.
//   * Takes NO compile-time dependency on DeNelle.Village MonoBehaviours -- it
//     only adds terrain + props, no village scripts. Pure UnityEngine /
//     UnityEditor terrain API.
//   * Does NOT touch any .asmdef. Does NOT run Unity itself.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that builds the Avalon exterior wilderness -- a Unity
    /// Terrain with four directional biomes, elevation, splatmaps, natural
    /// paths, instanced trees, scattered rock props, a dawn skybox and
    /// atmospheric fog -- around the walled village. Entry point:
    /// <see cref="BuildExterior"/>.
    /// </summary>
    public static class ExteriorTerrainBuilder
    {
        // ── Project paths ────────────────────────────────────────────────────
        private const string ScenesDir = "Assets/Scenes";
        private const string VillageScenePath = ScenesDir + "/Village.unity";
        private const string GeneratedDir = "Assets/Generated";
        private const string TerrainAssetDir = GeneratedDir + "/Terrain";
        private const string TerrainDataPath = TerrainAssetDir + "/ExteriorTerrainData.asset";
        private const string SkyboxMatPath = TerrainAssetDir + "/AvalonDawnSkybox.mat";

        /// <summary>Root for everything this builder generates -- cleared + rebuilt each run.</summary>
        private const string ExteriorRootName = "ExteriorRoot";

        // ── KayKit pack roots ────────────────────────────────────────────────
        // The Forest Nature Pack supplies the trees + rocks; the "(unity)"
        // variant folder holds the Unity-axis-corrected FBX models. Color1 is
        // the default warm palette and blends with the village grass.
        private const string ForestPackColor1 =
            "Assets/Models/KayKit/KayKit Forest Nature Pack 1.0/Assets/fbx(unity)/Color1/";

        // Hexagon pack nature decoration -- the fallback tree source if the
        // Forest pack is not imported.
        private const string HexDecoNature =
            "Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/decoration/nature/";

        // ── Terrain geometry ─────────────────────────────────────────────────
        // The terrain is 300 x 300 world units (§9.1). The walled village is
        // ~300x240u, but the seam-flattening plateau only needs to cover the
        // wall footprint; the terrain is centred on the village origin so it
        // extends ~equal distance beyond every wall.
        private const float TerrainSizeXZ = 300f;

        // Vertical span of the heightmap. North rises to +30, South sinks to
        // -15; we give a little headroom so the heightmap (which is normalised
        // 0..1) maps cleanly. The terrain GameObject is offset DOWN by
        // TerrainBaseDepth so heightmap value 0 sits below Y=0 and the village
        // baseline (Y=0) lands at a known normalised height.
        private const float TerrainHeight = 30f;       // total heightmap span (mountains ~30 m above village)
        // Owner 2026-05-20 ("same level as village" → black hex Z-fight →
        // settled depth). 1.5 m caused Z-fighting between the village hex
        // tiles at Y≈0.015 and the seam-blended terrain at Y≈0. 0.5 m
        // gives a small visible step at the wall while keeping the
        // terrain BELOW the hex tiles so they don't compete for pixels.
        private const float TerrainBaseDepth = 0.5f;   // owner 2026-05-20: was 22 — terrain dropped out of
                                                       // hero sight from village ground. 0.5 m below village
                                                       // floor keeps a clean seam with the hex disc edge.

        // Heightmap resolution -- must be 2^n + 1. 513 gives ~0.58u per sample
        // across 300u, plenty for rolling hills without tanking import time.
        private const int HeightmapRes = 513;

        // Splatmap (control texture) resolution -- 512 is ample for soft biome
        // blends across 300u.
        private const int SplatmapRes = 512;

        // The flat seam plateau: the village footprint is ~150u half-width
        // east-west, ~120u half-depth north-south. Inside this radius the
        // terrain is held flat at Y=0 so the wall base never floats or sinks
        // (§9.8). A falloff band blends the plateau edge into the biomes.
        private const float VillageHalfX = 150f;       // wall half-extent E-W
        private const float VillageHalfZ = 120f;       // wall half-extent N-S
        // Narrowed to 20 (2026-05-19 PO P0: "no exterior map outside the castle").
        // The 80-u falloff covered nearly the entire 300-u terrain — the tree +
        // rock scatter rejected almost every candidate position, leaving the
        // view from inside the village empty. 20 u keeps the village interior
        // flat but lets trees/rocks land within ~120 u of the walls.
        private const float SeamFalloff = 20f;         // blend band width beyond the wall

        // ── Tree budget (§9.6) ───────────────────────────────────────────────
        private const int TreeTargetCount = 320;       // within the 200-400 budget

        // ── Splat layer indices ──────────────────────────────────────────────
        private const int LayerGrass = 0;
        private const int LayerStone = 1;
        private const int LayerMud = 2;
        private const int LayerSnow = 3;
        private const int LayerDead = 4;
        private const int LayerCount = 5;

        // Deterministic RNG so re-runs are reproducible.
        private static System.Random _rng;

        // Running tallies for the summary log.
        private static int _treeCount;
        private static int _rockCount;
        private static int _pondCount;
        private static int _treePrototypeCount;
        private static readonly List<string> _notes = new List<string>();

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the Avalon exterior wilderness around the walled village.
        /// Runnable via
        /// <c>-executeMethod DeNelle.Editor.ExteriorTerrainBuilder.BuildExterior</c>.
        /// Idempotent -- re-running clears the generated ExteriorRoot + assets.
        /// </summary>
        [MenuItem("Defenders/Week 3/Build Exterior Terrain")]
        public static void BuildExterior()
        {
            _rng = new System.Random(20260518);
            _treeCount = 0;
            _rockCount = 0;
            _pondCount = 0;
            _treePrototypeCount = 0;
            _notes.Clear();

            EnsureFolder(TerrainAssetDir);
            AssetDatabase.Refresh();

            // ── Open the Village scene (must exist -- interior agent owns it) ─
            if (!File.Exists(VillageScenePath))
            {
                Debug.LogError("[ExteriorTerrainBuilder] Village.unity not found at " +
                               VillageScenePath + " -- run the village builder first. Aborting.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);

            // ── Idempotency: nuke any prior generated exterior root ──────────
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == ExteriorRootName)
                    UnityEngine.Object.DestroyImmediate(go);
            }

            var root = new GameObject(ExteriorRootName);

            // ── Build the heightmap + terrain data ───────────────────────────
            var terrainData = CreateTerrainData();

            // ── Spawn the Terrain GameObject, offset so the heightmap floor
            //    sits TerrainBaseDepth below world Y=0 ──────────────────────────
            var terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = "ExteriorTerrain";
            terrainGo.transform.SetParent(root.transform, false);
            // Terrain origin is its SW corner; centre it on the village origin
            // and drop it so heightmap-zero lands at Y = -TerrainBaseDepth.
            terrainGo.transform.position = new Vector3(
                -TerrainSizeXZ * 0.5f, -TerrainBaseDepth, -TerrainSizeXZ * 0.5f);

            var terrain = terrainGo.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 220f;
            terrain.drawInstanced = true;            // GPU-instanced terrain detail
            terrain.treeDistance = 280f;
            terrain.treeBillboardDistance = 110f;
            terrain.treeCrossFadeLength = 12f;
            terrain.treeMaximumFullLODCount = TreeTargetCount;

            // ── Texture the terrain (splatmaps) ──────────────────────────────
            PaintSplatmaps(terrainData);

            // ── Paint the 5 natural paths into the mud layer (§9.4) ──────────
            PaintNaturalPaths(terrainData);

            // ── Trees -- biome-distributed instanced painter (§9.6) ──────────
            PaintTrees(terrainData);

            // ── Micro props: boulders / cliff rocks + ponds (§9.3) ───────────
            ScatterRocks(root.transform, terrainData);
            PlacePonds(root.transform);

            // ── Distant-landmark suggestions (§9.5) ──────────────────────────
            PlaceDistantLandmarks(root.transform);

            // ── Skybox + atmospheric fog (§9.5) ──────────────────────────────
            ApplySkyAndFog();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VillageScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ExteriorTerrainBuilder] BuildExterior complete -- " +
                      $"terrain {TerrainSizeXZ:0}x{TerrainSizeXZ:0}u, " +
                      $"{_treePrototypeCount} tree prototype(s), {_treeCount} tree instances, " +
                      $"{_rockCount} rock props, {_pondCount} ponds, 5 biome splat layers, " +
                      $"5 natural paths, dawn skybox + fog. " +
                      (_notes.Count > 0 ? "Notes: " + string.Join("; ", _notes) : "No fallbacks used."));
        }

        // =====================================================================
        //  Heightmap -- the 4 directional biomes (§9.2 / §9.3)
        // =====================================================================

        /// <summary>
        /// Builds a fresh <see cref="TerrainData"/> asset: heightmap, splat
        /// layers and resolution settings. The heightmap encodes the four
        /// directional biomes plus a flat seam plateau under the village.
        /// </summary>
        private static TerrainData CreateTerrainData()
        {
            // Idempotency: drop any prior generated TerrainData asset.
            if (File.Exists(TerrainDataPath))
                AssetDatabase.DeleteAsset(TerrainDataPath);

            var td = new TerrainData
            {
                heightmapResolution = HeightmapRes,
                alphamapResolution = SplatmapRes,
                baseMapResolution = 1024,
                size = new Vector3(TerrainSizeXZ, TerrainHeight, TerrainSizeXZ),
            };
            td.SetDetailResolution(512, 16);

            // ── Build the height array ──────────────────────────────────────
            int res = td.heightmapResolution;
            var heights = new float[res, res];

            // The village sits at world Y=0. Heightmap value 0 sits at world
            // Y = -TerrainBaseDepth, so the normalised height for Y=0 is:
            float baseLevel01 = TerrainBaseDepth / TerrainHeight;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    // World-space position of this heightmap sample. The
                    // terrain origin is the village centre offset by half-size.
                    float u = x / (float)(res - 1);   // 0..1 across X (west->east)
                    float v = z / (float)(res - 1);   // 0..1 across Z (south->north)
                    float worldX = (u - 0.5f) * TerrainSizeXZ;
                    float worldZ = (v - 0.5f) * TerrainSizeXZ;

                    // Biome elevation in WORLD units relative to Y=0.
                    float biomeY = SampleBiomeHeight(worldX, worldZ);

                    // Seam blend: hold the village footprint flat at Y=0 and
                    // fade the biome elevation in across the falloff band.
                    float seam = SeamWeight(worldX, worldZ); // 1 inside walls, 0 far out
                    float elevatedY = Mathf.Lerp(biomeY, 0f, seam);

                    // Normalise back to 0..1 heightmap space.
                    heights[z, x] = Mathf.Clamp01(baseLevel01 + elevatedY / TerrainHeight);
                }
            }
            td.SetHeights(0, 0, heights);

            AssetDatabase.CreateAsset(td, TerrainDataPath);
            return td;
        }

        /// <summary>
        /// Seam weight at a world position: 1.0 inside the village footprint
        /// (terrain held flat at Y=0), smoothly falling to 0.0 beyond the
        /// falloff band (full biome elevation). Implements §9.8 -- the wall
        /// base sits at Y=0 with a soft 1-2 hex blend into the terrain.
        /// </summary>
        private static float SeamWeight(float worldX, float worldZ)
        {
            // Signed distance "outside" the village rectangle, per axis.
            float dx = Mathf.Max(0f, Mathf.Abs(worldX) - VillageHalfX);
            float dz = Mathf.Max(0f, Mathf.Abs(worldZ) - VillageHalfZ);
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist <= 0f) return 1f;
            if (dist >= SeamFalloff) return 0f;
            // Smoothstep falloff for a soft, non-linear blend.
            float t = dist / SeamFalloff;
            return 1f - (t * t * (3f - 2f * t));
        }

        /// <summary>
        /// Directional biome height field (world units, relative to Y=0).
        /// North = +Z, East = +X, South = -Z, West = -X. The four cardinal
        /// fields are blended by direction weight so the biomes meet smoothly
        /// at the diagonals.
        /// </summary>
        private static float SampleBiomeHeight(float worldX, float worldZ)
        {
            // Direction weights -- how "north", "east" etc. this point is.
            // Normalised so the dominant direction drives its biome field.
            float wN = Mathf.Max(0f, worldZ) / (TerrainSizeXZ * 0.5f);
            float wS = Mathf.Max(0f, -worldZ) / (TerrainSizeXZ * 0.5f);
            float wE = Mathf.Max(0f, worldX) / (TerrainSizeXZ * 0.5f);
            float wW = Mathf.Max(0f, -worldX) / (TerrainSizeXZ * 0.5f);
            float sum = wN + wS + wE + wW;
            if (sum < 0.0001f) return 0f;
            wN /= sum; wS /= sum; wE /= sum; wW /= sum;

            float h = 0f;
            h += wN * NorthHeight(worldX, worldZ);
            h += wS * SouthHeight(worldX, worldZ);
            h += wE * EastHeight(worldX, worldZ);
            h += wW * WestHeight(worldX, worldZ);
            return h;
        }

        // ── North: rising forest -> pine -> rock -> snow (§9.2) ──────────────
        // Gentle slopes within ~50u, then steeper hills climbing to +15..+30.
        private static float NorthHeight(float x, float z)
        {
            float d = Mathf.Max(0f, z - VillageHalfZ);          // distance past wall
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfZ));
            // Eased rise: gentle near the wall, steeper toward the edge.
            float rise = Mathf.SmoothStep(0f, 1f, t);
            float baseH = Mathf.Lerp(0f, 28f, rise);
            // A mountain ridge silhouetted at the very northern edge.
            float ridge = Mathf.Pow(t, 4f) * 9f;
            // Stone outcrops -- mid-frequency lumps poking through.
            float lumps = PerlinFbm(x * 0.018f + 11f, z * 0.018f + 4f, 3) * 6f * t;
            return baseH + ridge + lumps;
        }

        // ── East: gentle rolling farmland, +/-5u (§9.2) ──────────────────────
        private static float EastHeight(float x, float z)
        {
            float d = Mathf.Max(0f, x - VillageHalfX);
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfX));
            // Soft long-wavelength rolls -- cropland feel.
            float rolls = Mathf.Sin(z * 0.035f) * Mathf.Cos(x * 0.028f) * 4.5f;
            float gentle = PerlinFbm(x * 0.012f + 31f, z * 0.012f + 19f, 2) * 4f;
            return (rolls + gentle - 2f) * t;
        }

        // ── South: descending barren toward "the Wound", -10..-15 (§9.2) ─────
        private static float SouthHeight(float x, float z)
        {
            float d = Mathf.Max(0f, -z - VillageHalfZ);
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfZ));
            float sink = Mathf.SmoothStep(0f, 1f, t);
            float baseH = Mathf.Lerp(0f, -14f, sink);
            // Cracked, broken micro-relief as the land nears the Wound.
            float cracks = PerlinFbm(x * 0.03f + 7f, z * 0.03f + 23f, 3) * 3.5f * t;
            return baseH + cracks - 1.5f * sink;
        }

        // ── West: river valley -- ridge +12 / valley -5 / ridge +10 (§9.2) ───
        private static float WestHeight(float x, float z)
        {
            float d = Mathf.Max(0f, -x - VillageHalfX);
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfX));
            // The river runs roughly north-south at a meandering X line.
            float riverX = -VillageHalfX - 48f + Mathf.Sin(z * 0.02f) * 14f;
            float distToRiver = Mathf.Abs(x - riverX);
            // Valley carve: a smooth U-channel ~26u wide cut down to ~-5u.
            float valley = -5f * Mathf.Clamp01(1f - distToRiver / 26f);
            // Ridges flanking the valley.
            float ridgeNear = 12f * Mathf.Clamp01(1f - Mathf.Abs(distToRiver - 40f) / 30f);
            float ridgeFar = 10f * Mathf.Clamp01(1f - Mathf.Abs(distToRiver - 86f) / 34f);
            float relief = PerlinFbm(x * 0.02f + 51f, z * 0.02f + 61f, 3) * 4f;
            return (valley + Mathf.Max(ridgeNear, ridgeFar) + relief) * t;
        }

        /// <summary>Fractal Brownian motion over Unity's Perlin noise -- octave sum.</summary>
        private static float PerlinFbm(float x, float y, int octaves)
        {
            float total = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += (Mathf.PerlinNoise(x * freq, y * freq) - 0.5f) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return norm > 0f ? total / norm : 0f;
        }

        /// <summary>World height (Y) sampled at a world XZ -- mirrors the seam-blended heightmap.</summary>
        private static float WorldHeightAt(float worldX, float worldZ)
        {
            float biomeY = SampleBiomeHeight(worldX, worldZ);
            float seam = SeamWeight(worldX, worldZ);
            return Mathf.Lerp(biomeY, 0f, seam);
        }

        // =====================================================================
        //  Splatmaps -- 5 biome textures (§9.3)
        // =====================================================================

        /// <summary>
        /// Assigns 5 terrain layers (grass / exposed stone / mud path / snow /
        /// dark dead ground) and paints the alphamaps from slope + elevation +
        /// biome rules. Blend zones are soft. The mud layer is left near-zero
        /// here; <see cref="PaintNaturalPaths"/> writes the paths into it.
        /// </summary>
        private static void PaintSplatmaps(TerrainData td)
        {
            td.terrainLayers = new[]
            {
                MakeLayer("Exterior_Grass", new Color(0.34f, 0.46f, 0.24f), 14f),  // warm green
                MakeLayer("Exterior_Stone", new Color(0.55f, 0.52f, 0.45f), 11f),  // warm tan-grey
                MakeLayer("Exterior_Mud",   new Color(0.40f, 0.31f, 0.21f), 7f),   // foot-worn dirt
                MakeLayer("Exterior_Snow",  new Color(0.90f, 0.92f, 0.96f), 16f),  // snow
                MakeLayer("Exterior_Dead",  new Color(0.20f, 0.17f, 0.16f), 12f),  // dark dead ground
            };

            int res = td.alphamapResolution;
            var splat = new float[res, res, LayerCount];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = x / (float)(res - 1);
                    float v = z / (float)(res - 1);
                    float worldX = (u - 0.5f) * TerrainSizeXZ;
                    float worldZ = (v - 0.5f) * TerrainSizeXZ;

                    float y = WorldHeightAt(worldX, worldZ);
                    float slope = SteepnessAt(worldX, worldZ);   // 0..1

                    float wGrass = 1f;
                    float wStone = 0f;
                    float wSnow = 0f;
                    float wDead = 0f;

                    // Exposed stone on steep slopes everywhere (§9.3).
                    wStone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.62f, slope));

                    // Snow above Y+20 in the north only (§9.3).
                    if (worldZ > 0f && y > 14f)
                        wSnow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 24f, y));

                    // Dark dead ground below Y-8 in the south only (§9.3).
                    if (worldZ < 0f && y < -6f)
                        wDead = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, -11f, y));

                    // Grass is whatever the other layers don't claim.
                    wGrass = Mathf.Max(0f, 1f - wStone - wSnow - wDead);

                    float total = wGrass + wStone + wSnow + wDead;
                    if (total < 0.0001f) { wGrass = 1f; total = 1f; }

                    splat[z, x, LayerGrass] = wGrass / total;
                    splat[z, x, LayerStone] = wStone / total;
                    splat[z, x, LayerMud] = 0f;
                    splat[z, x, LayerSnow] = wSnow / total;
                    splat[z, x, LayerDead] = wDead / total;
                }
            }
            td.SetAlphamaps(0, 0, splat);
        }

        /// <summary>
        /// Approximate terrain steepness (0..1) at a world XZ via finite
        /// differences on the seam-blended height field.
        /// </summary>
        private static float SteepnessAt(float worldX, float worldZ)
        {
            const float e = 2.0f;
            float hL = WorldHeightAt(worldX - e, worldZ);
            float hR = WorldHeightAt(worldX + e, worldZ);
            float hD = WorldHeightAt(worldX, worldZ - e);
            float hU = WorldHeightAt(worldX, worldZ + e);
            float gx = (hR - hL) / (2f * e);
            float gz = (hU - hD) / (2f * e);
            float grad = Mathf.Sqrt(gx * gx + gz * gz);
            // Map gradient magnitude to a 0..1 steepness (grad 1.0 ~ 45deg).
            return Mathf.Clamp01(grad);
        }

        /// <summary>Creates a flat-colour terrain layer (no texture asset needed -- diffuse tint).</summary>
        private static TerrainLayer MakeLayer(string name, Color tint, float tileSize)
        {
            var layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = MakeSolidTexture(tint),
                tileSize = new Vector2(tileSize, tileSize),
                tileOffset = Vector2.zero,
            };
            string path = TerrainAssetDir + "/" + name + ".terrainlayer";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        /// <summary>Builds a small solid-colour texture asset (terrain layers need a diffuse texture).</summary>
        private static Texture2D MakeSolidTexture(Color c)
        {
            // A subtly noised 32x32 keeps the terrain from reading dead-flat.
            var tex = new Texture2D(32, 32, TextureFormat.RGB24, true);
            var px = new Color[32 * 32];
            for (int i = 0; i < px.Length; i++)
            {
                float n = (float)(_rng.NextDouble() - 0.5) * 0.06f;
                px[i] = new Color(
                    Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n));
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.name = "ExteriorLayerTex";
            // Embed the texture in the TerrainData folder so it persists.
            AssetDatabase.AddObjectToAsset(tex, TerrainDataPath);
            return tex;
        }

        // =====================================================================
        //  Natural paths -- 5 creative storytelling routes (§9.4)
        // =====================================================================

        /// <summary>
        /// Paints 5 wandering "natural" paths into the mud splat layer. These
        /// are landscape storytelling, NOT gameplay routes (enemies/the player
        /// never use them). Each path is a bezier-ish polyline; mud is blended
        /// in along it and the grass weight reduced to match.
        /// </summary>
        private static void PaintNaturalPaths(TerrainData td)
        {
            int res = td.alphamapResolution;
            var splat = td.GetAlphamaps(0, 0, res, res);

            // Five routes, in WORLD-space control points (XZ). Chosen for
            // narrative flavour per §9.4; reported in the build summary.
            //
            // 1. Animal track   -- from N gate approach, follows a ridge east.
            // 2. Smuggler's path-- across the western river valley, to nowhere.
            // 3. Pilgrim's road -- from S gate approach, winding SW, fading off.
            // 4. Hunter's trail -- through the eastern orchards, between trees.
            // 5. Crystal-vein   -- NW corner, worn path to a cliffside ore seam.
            var paths = new[]
            {
                new[] { V(0, 124), V(22, 138), V(58, 132), V(96, 116), V(124, 96) },     // animal track
                new[] { V(-150, 96), V(-186, 52), V(-198, -8), V(-176, -64), V(-148, -118) }, // smuggler's path
                new[] { V(-18, -124), V(-58, -150), V(-104, -168), V(-138, -196), V(-152, -240*0.5f) }, // pilgrim's road
                new[] { V(132, 70), V(160, 40), V(176, 4), V(168, -38), V(150, -78) },    // hunter's trail
                new[] { V(-128, 96), V(-150, 118), V(-176, 136), V(-198, 150) },          // crystal-vein path
            };

            const float pathHalfWidth = 4.5f;   // world units -- a worn footpath
            foreach (var pts in paths)
                PaintPolylineToMud(splat, res, pts, pathHalfWidth);

            td.SetAlphamaps(0, 0, splat);
        }

        private static Vector2 V(float x, float z) => new Vector2(x, z);

        /// <summary>
        /// Stamps mud along a world-space polyline (sampled densely) into the
        /// splat array, falling off softly to the path's edge.
        /// </summary>
        private static void PaintPolylineToMud(float[,,] splat, int res,
            Vector2[] worldPts, float halfWidth)
        {
            // Densely sample the polyline so the stamps overlap.
            const int samplesPerSeg = 48;
            for (int s = 0; s < worldPts.Length - 1; s++)
            {
                Vector2 a = worldPts[s];
                Vector2 b = worldPts[s + 1];
                for (int i = 0; i <= samplesPerSeg; i++)
                {
                    float t = i / (float)samplesPerSeg;
                    Vector2 p = Vector2.Lerp(a, b, t);
                    StampMud(splat, res, p.x, p.y, halfWidth);
                }
            }
        }

        /// <summary>Soft circular mud stamp at a world XZ into the splat array.</summary>
        private static void StampMud(float[,,] splat, int res, float worldX, float worldZ,
            float halfWidth)
        {
            // World XZ -> splatmap pixel space.
            float u = worldX / TerrainSizeXZ + 0.5f;
            float v = worldZ / TerrainSizeXZ + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;

            int cx = Mathf.RoundToInt(u * (res - 1));
            int cz = Mathf.RoundToInt(v * (res - 1));
            int radPx = Mathf.CeilToInt(halfWidth / TerrainSizeXZ * res);

            for (int dz = -radPx; dz <= radPx; dz++)
            {
                int z = cz + dz;
                if (z < 0 || z >= res) continue;
                for (int dx = -radPx; dx <= radPx; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= res) continue;
                    float distPx = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distPx > radPx) continue;
                    // Soft edge: full mud at centre, fading to 0 at the rim.
                    float w = 1f - (distPx / Mathf.Max(1, radPx));
                    w = w * w;   // ease the falloff
                    float mud = Mathf.Max(splat[z, x, LayerMud], w);
                    // Renormalise: mud takes from grass first, then stone.
                    float take = mud - splat[z, x, LayerMud];
                    splat[z, x, LayerMud] = mud;
                    float fromGrass = Mathf.Min(splat[z, x, LayerGrass], take);
                    splat[z, x, LayerGrass] -= fromGrass;
                    take -= fromGrass;
                    if (take > 0f)
                        splat[z, x, LayerStone] = Mathf.Max(0f, splat[z, x, LayerStone] - take);
                }
            }
        }

        // =====================================================================
        //  Trees -- biome-distributed instanced painter (§9.6)
        // =====================================================================

        /// <summary>
        /// Registers tree prototypes from the KayKit Forest Nature Pack (or the
        /// Hexagon pack as a fallback) and scatters ~320 instances, distributed
        /// by biome: dense pine/forest in the north, orchard clusters in the
        /// east, sparse dead trees in the south, willows/birch in the western
        /// valley, bare rock outcrops left treeless (§9.6).
        /// </summary>
        private static void PaintTrees(TerrainData td)
        {
            // ── Resolve tree meshes ──────────────────────────────────────────
            // Forest pack: leafy trees, conifer-ish variants, and bare trees.
            var leafy = new List<GameObject>();
            var conifer = new List<GameObject>();
            var bare = new List<GameObject>();

            // Tree_1..Tree_7 are the Forest pack's full canopy trees; the
            // taller-narrow series read as conifers, the rounder ones as
            // broadleaf. We classify by series for biome flavour.
            string[] leafyNames = { "Tree_1_A", "Tree_1_B", "Tree_2_A", "Tree_2_C", "Tree_4_A", "Tree_4_C" };
            string[] coniferNames = { "Tree_3_A", "Tree_3_B", "Tree_5_A", "Tree_5_C", "Tree_6_A", "Tree_7_A" };
            string[] bareNames = { "Tree_Bare_1_A", "Tree_Bare_1_C", "Tree_Bare_2_A", "Tree_Bare_2_C" };

            foreach (var n in leafyNames) AddIfFound(leafy, ForestPackColor1 + n + "_Color1.fbx");
            foreach (var n in coniferNames) AddIfFound(conifer, ForestPackColor1 + n + "_Color1.fbx");
            foreach (var n in bareNames) AddIfFound(bare, ForestPackColor1 + n + "_Color1.fbx");

            bool usingForestPack = leafy.Count + conifer.Count + bare.Count > 0;
            if (!usingForestPack)
            {
                // Fallback: Hexagon pack nature trees (§9.6 / task directive).
                _notes.Add("Forest pack trees not found -- fell back to Hexagon pack nature trees");
                AddIfFound(leafy, HexDecoNature + "trees_A_large.fbx");
                AddIfFound(leafy, HexDecoNature + "trees_A_medium.fbx");
                AddIfFound(conifer, HexDecoNature + "trees_B_large.fbx");
                AddIfFound(conifer, HexDecoNature + "trees_B_medium.fbx");
                AddIfFound(bare, HexDecoNature + "trees_A_cut.fbx");
                AddIfFound(bare, HexDecoNature + "tree_single_B_cut.fbx");
            }

            // Ordered prototype list. Index ranges let the scatter pick by biome.
            var protoList = new List<GameObject>();
            int leafyStart = protoList.Count;
            protoList.AddRange(leafy);
            int coniferStart = protoList.Count;
            protoList.AddRange(conifer);
            int bareStart = protoList.Count;
            protoList.AddRange(bare);

            if (protoList.Count == 0)
            {
                _notes.Add("NO tree meshes found in either pack -- trees skipped");
                Debug.LogWarning("[ExteriorTerrainBuilder] No tree meshes resolved -- " +
                                 "tree pass skipped (terrain still valid).");
                return;
            }

            var prototypes = new TreePrototype[protoList.Count];
            for (int i = 0; i < protoList.Count; i++)
                prototypes[i] = new TreePrototype { prefab = protoList[i], bendFactor = 0.4f };
            td.treePrototypes = prototypes;
            _treePrototypeCount = prototypes.Length;

            int leafyCount = coniferStart - leafyStart;
            int coniferCount = bareStart - coniferStart;
            int bareCount = protoList.Count - bareStart;

            // ── Scatter instances ───────────────────────────────────────────
            var instances = new List<TreeInstance>(TreeTargetCount);
            int attempts = 0;
            int maxAttempts = TreeTargetCount * 14;

            while (instances.Count < TreeTargetCount && attempts < maxAttempts)
            {
                attempts++;
                // Uniform candidate in normalised terrain space (0..1).
                float nx = (float)_rng.NextDouble();
                float nz = (float)_rng.NextDouble();
                float worldX = (nx - 0.5f) * TerrainSizeXZ;
                float worldZ = (nz - 0.5f) * TerrainSizeXZ;

                // Never plant inside the village footprint or its blend band.
                if (SeamWeight(worldX, worldZ) > 0.05f) continue;

                float y = WorldHeightAt(worldX, worldZ);
                float slope = SteepnessAt(worldX, worldZ);
                if (slope > 0.55f) continue;                 // too steep -- bare rock

                // ── Biome density + prototype selection ─────────────────────
                int protoIndex;
                float keepChance;

                if (worldZ > VillageHalfZ)
                {
                    // NORTH -- forest. Dense conifers; thins to bare rock /
                    // snow at the highest elevations (§9.6).
                    if (y > 20f) continue;                   // rock + snow line: no trees
                    keepChance = y > 12f ? 0.35f : 0.92f;    // dense forest, thinning up
                    protoIndex = y > 8f && coniferCount > 0
                        ? coniferStart + _rng.Next(coniferCount)
                        : (leafyCount > 0 ? leafyStart + _rng.Next(leafyCount)
                                          : coniferStart + _rng.Next(Mathf.Max(1, coniferCount)));
                }
                else if (worldZ < -VillageHalfZ)
                {
                    // SOUTH -- barren. Sparse dead trees only (§9.6).
                    keepChance = 0.16f;
                    protoIndex = bareCount > 0
                        ? bareStart + _rng.Next(bareCount)
                        : _rng.Next(protoList.Count);
                }
                else if (worldX > VillageHalfX)
                {
                    // EAST -- orchard. Clustered leafy fruit trees (§9.6).
                    // Cluster bias: a coarse noise gate makes orderly groves.
                    float grove = Mathf.PerlinNoise(worldX * 0.05f + 5f, worldZ * 0.05f + 9f);
                    keepChance = grove > 0.55f ? 0.85f : 0.18f;
                    protoIndex = leafyCount > 0
                        ? leafyStart + _rng.Next(leafyCount)
                        : _rng.Next(protoList.Count);
                }
                else
                {
                    // WEST -- river valley. Streamside willows + birch; denser
                    // near the valley floor, sparse on the dry ridges (§9.6).
                    keepChance = y < 2f ? 0.7f : 0.3f;
                    protoIndex = (_rng.NextDouble() < 0.5 && leafyCount > 0)
                        ? leafyStart + _rng.Next(leafyCount)
                        : (coniferCount > 0 ? coniferStart + _rng.Next(coniferCount)
                                            : _rng.Next(protoList.Count));
                }

                if (_rng.NextDouble() > keepChance) continue;

                // Heightmap-normalised height for the instance (0..1).
                float instHeight01 = Mathf.Clamp01((TerrainBaseDepth + y) / TerrainHeight);
                float scale = 0.8f + (float)_rng.NextDouble() * 0.7f;

                instances.Add(new TreeInstance
                {
                    position = new Vector3(nx, instHeight01, nz),
                    prototypeIndex = Mathf.Clamp(protoIndex, 0, protoList.Count - 1),
                    widthScale = scale,
                    heightScale = scale * (0.95f + (float)_rng.NextDouble() * 0.25f),
                    rotation = (float)_rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }

            td.SetTreeInstances(instances.ToArray(), true);
            _treeCount = instances.Count;
        }

        /// <summary>Loads a model and appends it to a list if the asset resolves.</summary>
        private static void AddIfFound(List<GameObject> list, string assetPath)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go != null) list.Add(go);
        }

        // =====================================================================
        //  Micro props -- boulders + cliff rocks (§9.3)
        // =====================================================================

        /// <summary>
        /// Scatters rock / boulder props on the terrain slopes. Boulders prefer
        /// the steeper north + valley-ridge ground; this also seeds two cliff
        /// faces (north ridge + west valley wall) with a denser rock cluster.
        /// Uses the Forest pack's Rock_* meshes.
        /// </summary>
        private static void ScatterRocks(Transform parent, TerrainData td)
        {
            var rockMeshes = new List<GameObject>();
            string[] rockNames =
            {
                "Rock_1_A", "Rock_1_E", "Rock_2_B", "Rock_3_C", "Rock_3_H",
                "Rock_4_A", "Rock_5_B", "Rock_6_D", "Rock_6_G",
            };
            foreach (var n in rockNames)
                AddIfFound(rockMeshes, ForestPackColor1 + n + "_Color1.fbx");

            if (rockMeshes.Count == 0)
            {
                // Fallback: Hexagon pack single rocks.
                _notes.Add("Forest pack rocks not found -- fell back to Hexagon rock_single meshes");
                for (char c = 'A'; c <= 'E'; c++)
                    AddIfFound(rockMeshes, HexDecoNature + "rock_single_" + c + ".fbx");
            }

            var rockRoot = new GameObject("Rocks");
            rockRoot.transform.SetParent(parent, false);

            if (rockMeshes.Count == 0)
            {
                _notes.Add("NO rock meshes found -- boulder scatter used primitive stand-ins");
            }

            // ── Scattered boulders on slopes ─────────────────────────────────
            // Owner direction 2026-05-20 ("rocks in front of door"): the
            // wilderness boulder scatter landed several rocks right outside
            // the cardinal gates, reading as obstacles. Disabled until we
            // add a per-gate exclusion radius.
            int boulderTarget = 0;
            int attempts = 0;
            while (_rockCount < boulderTarget && attempts < boulderTarget * 20)
            {
                attempts++;
                float worldX = ((float)_rng.NextDouble() - 0.5f) * TerrainSizeXZ;
                float worldZ = ((float)_rng.NextDouble() - 0.5f) * TerrainSizeXZ;
                if (SeamWeight(worldX, worldZ) > 0.05f) continue;

                float y = WorldHeightAt(worldX, worldZ);
                float slope = SteepnessAt(worldX, worldZ);
                // Boulders favour slopes + the rocky north / valley ridges.
                bool rocky = slope > 0.22f || (worldZ > 60f && y > 14f);
                if (!rocky && _rng.NextDouble() > 0.18) continue;

                SpawnRock(rockRoot.transform, rockMeshes, worldX, y, worldZ,
                    0.8f + (float)_rng.NextDouble() * 1.6f);
            }

            // Cliff rock clusters disabled per owner direction 2026-05-20:
            // "rocks in front of door" — even though SeedCliff placed
            // clusters far north/west of the village, some rocks landed
            // visible from the gate threshold. The two SeedCliff calls
            // accounted for ~32 of the scene's rock refs.
            // SeedCliff(rockRoot.transform, rockMeshes, 40f, 118f, "NorthRidge");
            // SeedCliff(rockRoot.transform, rockMeshes, -120f, -10f, "WestValleyWall");
        }

        /// <summary>Spawns a tight cluster of rocks to read as a cliff face / cave mouth.</summary>
        private static void SeedCliff(Transform parent, List<GameObject> rockMeshes,
            float cx, float cz, string label)
        {
            var cliff = new GameObject("Cliff_" + label);
            cliff.transform.SetParent(parent, false);
            for (int i = 0; i < 16; i++)
            {
                float ox = ((float)_rng.NextDouble() - 0.5f) * 26f;
                float oz = ((float)_rng.NextDouble() - 0.5f) * 18f;
                float wx = cx + ox;
                float wz = cz + oz;
                float y = WorldHeightAt(wx, wz);
                SpawnRock(cliff.transform, rockMeshes, wx, y, wz,
                    1.6f + (float)_rng.NextDouble() * 2.6f);
            }
        }

        /// <summary>Instantiates one rock prop (or a primitive stand-in) at a world position.</summary>
        private static void SpawnRock(Transform parent, List<GameObject> rockMeshes,
            float worldX, float worldY, float worldZ, float scale)
        {
            GameObject go;
            if (rockMeshes.Count > 0)
            {
                var src = rockMeshes[_rng.Next(rockMeshes.Count)];
                go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                if (go == null) go = MakeRockPrimitive();
                else go.name = src.name;
            }
            else
            {
                go = MakeRockPrimitive();
            }
            go.transform.SetParent(parent, false);
            // Sink slightly so the rock beds into the terrain.
            go.transform.position = new Vector3(worldX, worldY - 0.25f * scale, worldZ);
            go.transform.rotation = Quaternion.Euler(
                (float)(_rng.NextDouble() - 0.5) * 18f,
                (float)_rng.NextDouble() * 360f,
                (float)(_rng.NextDouble() - 0.5) * 18f);
            go.transform.localScale = Vector3.one * scale;
            _rockCount++;
        }

        private static GameObject MakeRockPrimitive()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "[PLACEHOLDER] boulder";
            ApplyColor(go, new Color(0.5f, 0.48f, 0.42f));
            return go;
        }

        // =====================================================================
        //  Ponds -- small water patches in low spots (§9.3)
        // =====================================================================

        /// <summary>
        /// Places 6 small ponds (flat translucent water quads) in natural low
        /// spots -- ~2-3 per quadrant per §9.3. Uses a simple URP-transparent
        /// disc; no water shader dependency.
        /// </summary>
        private static void PlacePonds(Transform parent)
        {
            var pondRoot = new GameObject("Ponds");
            pondRoot.transform.SetParent(parent, false);

            // Hand-picked low-spot world positions, spread across the quadrants.
            var spots = new[]
            {
                V(72, 96),    V(-58, 84),     // north-ish
                V(118, -36),  V(146, 58),     // east farmland hollows
                V(-44, -132), V(-150, -40),   // south barren seep + west valley
            };

            foreach (var s in spots)
            {
                float y = WorldHeightAt(s.x, s.y);
                var pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pond.name = "Pond";
                pond.transform.SetParent(pondRoot.transform, false);
                // Cylinder default height 2u -> scale to a thin disc.
                float radius = 7f + (float)_rng.NextDouble() * 6f;
                pond.transform.localScale = new Vector3(radius, 0.12f, radius);
                // Seat the water surface just below local ground.
                pond.transform.position = new Vector3(s.x, y - 0.35f, s.y);
                var col = pond.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                ApplyWater(pond, new Color(0.28f, 0.46f, 0.58f, 0.78f));
                _pondCount++;
            }
        }

        // =====================================================================
        //  Distant landmarks (§9.5)
        // =====================================================================

        /// <summary>
        /// Places three distant-landmark suggestions at the terrain horizon
        /// (§9.5): a northern mountain peak, a western tower silhouette, and a
        /// southern "Wound" crack. All sit beyond the playable terrain edge as
        /// silhouette dressing.
        /// </summary>
        private static void PlaceDistantLandmarks(Transform parent)
        {
            var lmRoot = new GameObject("DistantLandmarks");
            lmRoot.transform.SetParent(parent, false);

            // ── Northern mountain peak ──────────────────────────────────────
            // Reuse the Hexagon pack's mountain mesh if present, scaled large.
            var mountainMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
                HexDecoNature + "mountain_A.fbx");
            GameObject mountain;
            if (mountainMesh != null)
            {
                mountain = (GameObject)PrefabUtility.InstantiatePrefab(mountainMesh);
                mountain.name = "DistantMountainPeak";
                mountain.transform.localScale = Vector3.one * 26f;
            }
            else
            {
                mountain = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mountain.name = "DistantMountainPeak";
                // Owner observed 2026-05-20: previous 80-tall cube with
                // pivot at Y=24 left the base 16 m underground but the top
                // 64 m in the air -- it read as "structure in sky". Halve
                // the height and ground-anchor it so it reads as a real
                // distant peak instead of a floating block.
                mountain.transform.localScale = new Vector3(120f, 40f, 60f);
                mountain.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                ApplyColor(mountain, new Color(0.42f, 0.45f, 0.52f));
            }
            mountain.transform.SetParent(lmRoot.transform, false);
            // Pivot is center; scale.y/2 = 20 puts the base flush with Y=0.
            mountain.transform.position = new Vector3(20f, 20f, 230f);

            // ── Western tower silhouette (Mira's place -- just a hint) ──────
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.name = "DistantTowerSilhouette";
            tower.transform.SetParent(lmRoot.transform, false);
            tower.transform.localScale = new Vector3(7f, 26f, 7f);
            // Cylinder pivot is center; base sits at Y=0 when position.y =
            // scale.y/2 = 13. Previous Y=24 floated the base 11 m off the
            // ground (owner 2026-05-20: structure-in-sky bug).
            tower.transform.position = new Vector3(-228f, 13f, -30f);
            ApplyColor(tower, new Color(0.30f, 0.30f, 0.38f));

            // ── Southern "Wound" crack -- a dark, easy-to-miss horizon scar ──
            var crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crack.name = "DistantWoundCrack";
            crack.transform.SetParent(lmRoot.transform, false);
            crack.transform.localScale = new Vector3(140f, 34f, 4f);
            crack.transform.rotation = Quaternion.Euler(0f, 12f, 6f);
            crack.transform.position = new Vector3(-10f, 6f, -236f);
            ApplyEmissive(crack, new Color(0.16f, 0.06f, 0.22f), 0.4f); // faint violet glow
        }

        // =====================================================================
        //  Skybox + atmospheric fog (§9.5)
        // =====================================================================

        /// <summary>
        /// Applies a soft-dawn procedural skybox (pink-violet horizon, soft blue
        /// overhead, low warm sun) and a gentle atmospheric fog via
        /// <see cref="RenderSettings"/> -- §9.5. Fog is continuous so the seam
        /// with the interior is invisible (§9.8).
        /// </summary>
        private static void ApplySkyAndFog()
        {
            // ── Procedural dawn skybox ──────────────────────────────────────
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material sky;
                if (File.Exists(SkyboxMatPath))
                    sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
                else
                    sky = null;

                if (sky == null)
                {
                    sky = new Material(skyShader);
                    AssetDatabase.CreateAsset(sky, SkyboxMatPath);
                }
                else if (sky.shader != skyShader)
                {
                    sky.shader = skyShader;
                }

                // Dawn: a low sun, warm-tinted, gentle atmosphere thickness so
                // the horizon blooms pink-violet.
                sky.SetFloat("_SunSize", 0.045f);
                sky.SetFloat("_SunSizeConvergence", 4f);
                sky.SetFloat("_AtmosphereThickness", 1.35f);
                // Sky tint -- a soft cool-blue zenith.
                sky.SetColor("_SkyTint", new Color(0.52f, 0.58f, 0.78f));
                // Ground tint -- warm pink-violet horizon haze.
                sky.SetColor("_GroundColor", new Color(0.78f, 0.62f, 0.70f));
                sky.SetFloat("_Exposure", 1.15f);
                EditorUtility.SetDirty(sky);

                RenderSettings.skybox = sky;
            }
            else
            {
                _notes.Add("Skybox/Procedural shader missing -- skybox left at scene default");
            }

            // ── Directional sun -- low dawn angle, slightly warm ────────────
            // Reuse the scene's existing directional light if the interior
            // builder made one; otherwise create a dawn sun.
            Light sun = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(
                         FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                var sunGo = new GameObject("Directional Light (Dawn)");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }
            // Sun ~15deg above the horizon (§9.5), warm dawn colour.
            sun.transform.rotation = Quaternion.Euler(15f, -28f, 0f);
            sun.color = new Color(1f, 0.86f, 0.74f);
            sun.intensity = 1.05f;
            RenderSettings.sun = sun;

            // ── Ambient -- soft dawn gradient ───────────────────────────────
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.55f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.55f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.26f);

            // ── Atmospheric fog -- light dawn haze (§9.5) ───────────────────
            // Exponential-squared fog so distant terrain reads soft, near
            // terrain stays crisp. Tuned dense enough to soften the horizon
            // but light enough that the 300u terrain is fully visible.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.80f, 0.74f, 0.80f); // pink-violet haze
            // §9.5 wants a denser-south / lighter-east gradient. Built-in fog
            // is uniform; we pick a mid density that suits the whole map and
            // leave the per-direction gradient as a volumetric follow-up note.
            RenderSettings.fogDensity = 0.0014f;
            _notes.Add("Fog is uniform exponential-squared (built-in); per-direction " +
                       "density gradient (denser south) deferred to a volumetric pass");

            // Halo / flare strength kept gentle for the dawn register.
            RenderSettings.haloStrength = 0.35f;
            RenderSettings.flareStrength = 0.7f;
        }

        // =====================================================================
        //  Material helpers
        // =====================================================================

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            renderer.sharedMaterial = mat;
        }

        private static void ApplyEmissive(GameObject go, Color color, float intensity)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", color * intensity);
            renderer.sharedMaterial = mat;
        }

        private static void ApplyWater(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            // Transparent surface mode for the URP Lit shader.
            mat.SetFloat("_Surface", 1f);          // 0 opaque, 1 transparent
            mat.SetFloat("_Blend", 0f);            // alpha blend
            mat.SetFloat("_Smoothness", 0.85f);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            renderer.sharedMaterial = mat;
        }

        // =====================================================================
        //  Folder helper
        // =====================================================================

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
