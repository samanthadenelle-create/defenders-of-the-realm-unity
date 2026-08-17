// =============================================================================
// ExteriorTerrainBuilder — Elarion EXTERIOR wilderness generator (Editor-only).
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
using DeNelle.Core.Diagnostics;
using DeNelle.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that builds the Elarion exterior wilderness -- a Unity
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
        // Merged hub (2026): OuterWorld.unity was deleted — exterior lives on
        // Main_Castle_Overworld (one navmesh). BuildExterior opens that scene and only
        // nukes ExteriorRoot so castle/hub content is preserved.
        private const string OuterWorldScenePath = ScenesDir + "/Main_Castle_Overworld.unity";
        private const string GeneratedDir = "Assets/Generated";
        private const string TerrainAssetDir = GeneratedDir + "/Terrain";
        private const string TerrainDataPath = TerrainAssetDir + "/ExteriorTerrainData.asset";
        private const string SkyboxMatPath = TerrainAssetDir + "/AvalonDawnSkybox.mat";
        private const string TerrainMaterialPath = TerrainAssetDir + "/ExteriorTerrainMaterial.mat";

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
        // WO-468 Phase 1: enlarged from 300 -> 1000 so the player walks a real
        // distance south from the castle to a cave/portal at the far end. Terrain
        // stays ORIGIN-CENTERED (Phase 2 offsets the origin, not this pass). Every
        // height/splat/scatter computation below derives from this constant, so the
        // heightmap, splatmaps, paths and tree/rock scatter re-fit automatically.
        private const float TerrainSizeXZ = 1000f;

        // ── World placement (WO-483 RE-CENTER, 2026-06-23; supersedes WO-468 south-shift) ──
        // The terrain is ORIGIN-CENTERED again. The WO-468 Phase-2 south-shift
        // (TerrainCenterZ=-572) was for the old "walk south into OuterWorld" design;
        // the WO-482 encounter loop replaced it. ZoneManager (the Core truth) is
        // origin-centered, and the hero now PLAYS around origin..z~-75 (reps anchor
        // there). With the terrain south-shifted, that play area fell in a FLOORLESS
        // gap north of the terrain edge (z=-72) -> no navmesh -> enemy CreateAgent
        // failed / "no COMPLETE path to hero" (owner+trace, 2026-06-23). Centering at
        // 0 puts a 1000u terrain (z = -500..+500) UNDER the whole play area so the
        // navmesh bakes where the encounter loop happens.
        //
        // The biome height functions (North/East/South/WestHeight, SampleBiomeHeight)
        // + SeamWeight expect a CENTERED coordinate (-size/2..+size/2). SampleBiomeHeight
        // is the single chokepoint that re-centers Z by subtracting TerrainCenterZ, so
        // every caller passes TRUE WORLD coordinates and the biomes re-fit automatically
        // to the new center. The legacy cave-path corridor (CavePath*Z below) is in WORLD
        // Z and now runs partly off the south edge — harmless for V1 (cave/portal is
        // legacy, gated; not on the encounter-loop path).
        private const float TerrainCenterZ = 0f;        // terrain world-Z center (X center = 0) — RE-CENTERED to origin

        // ── Cave path corridor (WO-468) ──────────────────────────────────────
        // A clean, LEVEL road runs due south (x≈0) from the north terrain edge
        // (z=CavePathStartZ, the castle arrival) to the cave/portal mouth
        // (z=CavePathEndZ). The corridor is held flat at the village baseline
        // (Y≈0) and kept clear of trees/rocks. Constants are in WORLD Z.
        private const float CavePathEndZ = -700f;       // cave/portal world Z (x≈0, y≈0)
        private const float CavePathStartZ = -76f;      // player arrives from the castle here (just onto north edge)
        // WIDENED 2026-06-20 (navlink RCA): the cave trigger at (0,1,-684) read SEAM-OFF-MESH
        // (no walkable navmesh within 2m). A wider, gentler flat corridor gives the bake a solid
        // agent-width walkable strip the whole way to the cave and keeps the falloff slope shallow
        // enough that the navmesh isn't carved away at the corridor edges.
        private const float CavePathHalfWidth = 10f;    // painted mud road half-width (world units)
        private const float CavePathFlattenHalf = 20f;  // corridor flattened to Y≈0 within |x|<this
        private const float CavePathFlattenFalloff = 14f; // soft blend band beyond the flatten half (gentle slope)

        // Vertical span of the heightmap. North rises to +30, South sinks to
        // -15; we give a little headroom so the heightmap (which is normalised
        // 0..1) maps cleanly. The terrain GameObject is offset DOWN by
        // TerrainBaseDepth so heightmap value 0 sits below Y=0 and the village
        // baseline (Y=0) lands at a known normalised height.
        // Raised 30 → 42 (2026-08-07): stronger biomes + multi-scale relief need headroom
        // so peaks aren't Clamp01-clipped into a pancake. baseLevel01 still maps Y=0 correctly.
        private const float TerrainHeight = 42f;
        // Owner 2026-05-20 ("same level as village" → black hex Z-fight →
        // settled depth). 1.5 m caused Z-fighting between the village hex
        // tiles at Y≈0.015 and the seam-blended terrain at Y≈0. 0.5 m
        // gives a small visible step at the wall while keeping the
        // terrain BELOW the hex tiles so they don't compete for pixels.
        // WO-468 wrapped-seam (2026-06-27): raised 0.5 -> 4.0 so the heightmap has DOWNWARD
        // headroom for the castle-footprint DEPRESSION (CastleDepressionDepth, ~-3 m within ±62 of
        // origin). Heightmap value 0 = world Y = -TerrainBaseDepth; with 0.5 the terrain could only
        // sink to -0.5 (clamped), too shallow to seat OuterWorld ground clearly BELOW the castle
        // floor (Y=0) — the wrapped origin-centered terrain pokes through the castle floor. 4.0 lets
        // the depression reach -3 while baseLevel01 keeps all non-depressed ground exactly at Y=0
        // (the legacy 0.5 Z-fight tuning was for Village.unity hex tiles, abandoned — terrain is in
        // OuterWorld.unity now). The south biome (designed to sink to -14, previously clamped at
        // -0.5) now reveals a gentle valley down to -4, which is more correct, not a regression.
        private const float TerrainBaseDepth = 4.0f;

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

        // Castle footprint kept CLEAR of tree/rock scatter (WO-483 re-center, 2026-06-23):
        // the OuterWorld terrain is origin-centered again and now OVERLAPS the castle scene at
        // world origin, so scatter inside the wall footprint (~±42) lands "trees inside walls"
        // (owner flag). Reject scatter within this footprint + a soft taper. Kept SEPARATE from
        // VillageHalfX/Z (those drive the biome HEIGHT falloff, lines ~426-464) so the clear-zone
        // tunes independently of terrain heights.
        private const float CastleClearHalfX  = 62f;   // origin-centered; covers the ±42 walls + margin
        private const float CastleClearHalfZ  = 62f;
        private const float CastleClearFalloff = 14f;  // soft taper so the tree-line edge isn't a hard ring

        // WO-468 wrapped-seam castle DEPRESSION (2026-06-27): the origin-centered terrain now wraps
        // UNDER the castle (which sits at world origin, floor at Y=0). Without a depression the
        // OuterWorld ground would be coplanar with the castle floor and POKE THROUGH it. Sink the
        // terrain to this depth within the castle footprint (±CastleClearHalfX/Z), smoothly tapering
        // over CastleClearFalloff, so OuterWorld ground sits clearly BELOW the castle floor. Mirrors
        // the SeamWeight footprint (1.0 inside ±62, smoothstep taper). The Task-2 NavMeshModifierVolume
        // separately carves the OuterWorld navmesh hole here; this is the VISUAL no-poke-through term.
        // 2026-06-30: -3f sank the castle-zone OuterWorld ground to surfaceY=-3 — the visible
        // "depressed ground" (owner felt-test + live TERRAINDIAG surfaceY@x=0 -> -3.000). 0f keeps
        // it flush at the village baseline, restoring the ~10 PM felt-verified state. The navmesh
        // hole under the castle is carved separately (NavMeshModifierVolume), so 0f is visual-only.
        private const float CastleDepressionDepth = 0f;

        // ── WORLDFEEL relief (owner 2026-07-01 + 2026-08-07: "mostly flat" → unique land) ──
        // Multi-scale undulation + regional character so each compass sector reads differently.
        // Gentle long-wavelength perlin undulation layered onto the biome heights so the
        // mid-band lawn (the flat ring between the castle clear-zone and the biome ramps)
        // reads as rolling ground instead of a billiard table. DELIBERATELY gentle
        // (amplitude ~±1.8 m over ~110 m wavelengths) — decoration, not restructuring:
        // the terrain origin/levels are untouched (OuterWorldNavBake level-sample landmine).
        //
        // PROTECTED FLAT LANES (relief is masked OFF here — play-critical walkability):
        //   * castle surroundings: plinth / moat ring / bridge+ramp landings (r≈44-60)
        //     and the enemy spawn arcs (z≈-60..-64, 12 m outside each gate) — all inside
        //     the ReliefFlatRadius full-flat disc;
        //   * the walk-to raid-outpost anchors (~70 m out a gate + footprint) — inside
        //     the same disc;
        //   * the cave road / portal approach (x≈0, z -76..-700, trigger z≈-404..-420):
        //     relief is added BEFORE the WO-468 corridor flatten, so CorridorWeight
        //     lerps it back to Y=0 exactly like the biome heights;
        //   * the castle footprint itself: the SeamWeight depression lerp flattens last.
        // 2026-08-07 owner: "mostly flat, should be unique". Play loop lived inside
        // ~95m full-flat disc with only ±1.8m relief — lawn. Bump multi-scale amplitude,
        // shrink flat disc to just past moat/clear, add RegionalCharacter landmarks.
        private const float ReliefAmplitude   = 5.8f;   // was 1.8 — real hills, still walkable
        private const float ReliefFrequency   = 0.011f; // was 0.009 — ~90 m dominant rolls
        private const float ReliefFlatRadius  = 70f;    // was 95 — past castle clear (62) + moat
        private const float ReliefBlendRadius = 108f;   // was 140 — full character sooner
        private const float CharacterAmplitude = 4.2f;  // compass-asymmetric hills/hollows (m)

        // ── Tree budget (§9.6) ───────────────────────────────────────────────
        // WO-468 Phase 1: bumped 320 -> 1000 so the ~11x-larger terrain isn't
        // sparse. WORLDFEEL 2026-07-02: 1000 -> 2400 (owner: "world feels empty";
        // 1000 trees on a 1000x1000 terrain = 1 tree per 1000 m² — sparse). Terrain
        // trees are billboarded past treeBillboardDistance, so this stays cheap.
        // The path-corridor reject (below) keeps the cave road clear.
        private const int TreeTargetCount = 2400;

        // WORLDFEEL: horizon tree-line — within this band of the terrain edge the
        // scatter keep-chance is floored high so every horizon reads as a treeline
        // silhouette instead of empty ground meeting an empty sky.
        private const float HorizonBandWidth = 90f;
        private const float HorizonKeepChance = 0.8f;

        // ── Splat layer indices ──────────────────────────────────────────────
        // ⛔ WO-1101: these are NO LONGER declared here. They used to be six private
        // consts in this file while WorldSceneLoader.cs (DeNelle.Village, the DEF-108
        // runtime repaint) hardcoded the SAME indices as bare literals 0/1/2/4. The
        // runtime repaint is the only splat the player sees on device, so growing the
        // layer set here alone mispaints the ground on device and NOWHERE ELSE — a
        // defect no editor gate can see. The single authority is now
        // DeNelle.Core.World.TerrainLayerSet, which Editor, Village and
        // EditorRegression all reference. Do not reintroduce a local table.
        //
        // Local aliases only — they resolve to the shared authority, they do not
        // restate it. TerrainLayerSet.Count is the array bound everywhere.
        private const int LayerMeadow = TerrainLayerSet.Meadow;
        private const int LayerGoldfields = TerrainLayerSet.GoldfieldsField;
        private const int LayerStone = TerrainLayerSet.StonebackRock;
        private const int LayerSnow = TerrainLayerSet.StonebackSnow;
        private const int LayerMire = TerrainLayerSet.MirewoodMire;
        private const int LayerMireRoots = TerrainLayerSet.MirewoodRoots;
        private const int LayerAsh = TerrainLayerSet.AshwoodAsh;
        private const int LayerPath = TerrainLayerSet.PathDirt;
        private const int LayerCount = TerrainLayerSet.Count;

        // Deterministic RNG so re-runs are reproducible.
        private static System.Random _rng;

        // Running tallies for the summary log.
        private static int _treeCount;
        private static int _rockCount;
        private static int _pondCount;
        private static int _treePrototypeCount;
        private static int _detailPrototypeCount;
        private static int _detailPatchCount;
        private static readonly List<string> _notes = new List<string>();

        // =====================================================================
        //  Entry point
        // =====================================================================

        /// <summary>
        /// Builds the Elarion exterior wilderness around the walled village.
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
            _detailPrototypeCount = 0;
            _detailPatchCount = 0;
            _notes.Clear();

            EnsureFolder(TerrainAssetDir);
            AssetDatabase.Refresh();

            // ── Open the Village scene (must exist -- interior agent owns it) ─
            if (!File.Exists(OuterWorldScenePath))
            {
                Debug.LogError("[ExteriorTerrainBuilder] hub scene not found at " +
                               OuterWorldScenePath + " -- expected Main_Castle_Overworld.unity. Aborting.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(OuterWorldScenePath, OpenSceneMode.Single);

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
                -TerrainSizeXZ * 0.5f, -TerrainBaseDepth,
                TerrainCenterZ - TerrainSizeXZ * 0.5f);

            var terrain = terrainGo.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 220f;
            terrain.drawInstanced = true;            // GPU-instanced terrain detail
            terrain.treeDistance = 280f;
            terrain.treeBillboardDistance = 110f;
            terrain.treeCrossFadeLength = 12f;
            terrain.treeMaximumFullLODCount = TreeTargetCount;
            // WO-1101 ground cover: details are GPU-instanced and hard distance-culled, so a
            // modest draw distance keeps them free on mobile while the near ground reads rich.
            terrain.detailObjectDistance = 90f;
            terrain.detailObjectDensity = 0.75f;

            // WO-173/DEF-108: assign a URP TerrainLit material so the terrain SURFACE
            // renders. With no explicit URP terrain material the Terrain falls back to a
            // template that draws NOTHING under URP — the ground reads as a black VOID
            // (the painted trees + the village's own hex-ground still draw via their own
            // materials, which is why only the horizon tree-line + the centre patch showed).
            // Create + persist the material as an asset so it's packaged into the build.
            terrain.materialTemplate = EnsureTerrainMaterial();
            EnsureTerrainShaderIncluded();   // pin URP terrain shader into the build (else BLACK terrain in player)

            // ── Texture the terrain (splatmaps) ──────────────────────────────
            PaintSplatmaps(terrainData);

            // ── Paint the 5 natural paths into the mud layer (§9.4) ──────────
            PaintNaturalPaths(terrainData);

            // ── Trees -- biome-distributed instanced painter (§9.6) ──────────
            PaintTrees(terrainData);

            // ── Ground cover -- detail grass/bush clutter (WO-1101 "simple aesthetics") ──
            // MUST run after PaintSplatmaps: the detail density is driven by the splat.
            PaintDetailGroundCover(terrainData);

            // ── Micro props: boulders / cliff rocks + ponds (§9.3) ───────────
            ScatterRocks(root.transform, terrainData);
            PlacePonds(root.transform);

            // ── Distant-landmark suggestions (§9.5) ──────────────────────────
            PlaceDistantLandmarks(root.transform);

            // ── Skybox + atmospheric fog (§9.5) ──────────────────────────────
            ApplySkyAndFog();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, OuterWorldScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ExteriorTerrainBuilder] BuildExterior complete -- " +
                      $"terrain {TerrainSizeXZ:0}x{TerrainSizeXZ:0}u, " +
                      $"{_treePrototypeCount} tree prototype(s), {_treeCount} tree instances, " +
                      $"{_rockCount} rock props, {_pondCount} ponds, " +
                      $"{TerrainLayerSet.Count} textured biome splat layers, " +
                      $"{_detailPrototypeCount} detail prototype(s) / {_detailPatchCount} clutter cells, " +
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
                    // TRUE world position of this sample (terrain shifted south by
                    // TerrainCenterZ; SampleBiomeHeight re-centers Z internally).
                    float worldX = (u - 0.5f) * TerrainSizeXZ;
                    float worldZ = (v - 0.5f) * TerrainSizeXZ + TerrainCenterZ;

                    // Biome elevation in WORLD units relative to Y=0.
                    float biomeY = SampleBiomeHeight(worldX, worldZ);

                    // WO-468 Phase 2: the village/seam plateau is GONE — the castle
                    // is a separate scene now, so nothing flattens the terrain
                    // except the cave-path corridor (WO-468), which holds Y≈0 so
                    // the walk south is LEVEL.
                    float flat = CorridorWeight(worldX, worldZ);
                    float elevatedY = Mathf.Lerp(biomeY, 0f, flat);

                    // WO-468 wrapped-seam: sink the castle footprint BELOW the castle floor (Y=0) so
                    // the origin-centered terrain that now wraps under the castle doesn't poke through.
                    float castleW = SeamWeight(worldX, worldZ);
                    elevatedY = Mathf.Lerp(elevatedY, CastleDepressionDepth, castleW);

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
            // WO-483 RE-CENTER (2026-06-23): the terrain is origin-centered again and now
            // OVERLAPS the castle scene at world origin, so the castle/wall footprint MUST stay
            // clear of scattered trees/rocks (owner: "trees populate inside walls"). 1.0 inside
            // the footprint, smoothstep taper over CastleClearFalloff. Used by the tree + rock
            // reject AND (WO-468 wrapped-seam) by the castle-footprint DEPRESSION in CreateTerrainData/
            // WorldHeightAt (biome height still uses VillageHalfX/Z; this only carves the castle dip).
            float dx = Mathf.Abs(worldX) - CastleClearHalfX;
            float dz = Mathf.Abs(worldZ) - CastleClearHalfZ;
            float d = Mathf.Max(dx, dz);                 // <=0 inside the footprint, grows outside
            if (d <= 0f) return 1f;                      // inside the walls -> full reject
            if (d >= CastleClearFalloff) return 0f;      // past the taper -> trees/rocks allowed
            return 1f - Mathf.SmoothStep(0f, 1f, d / CastleClearFalloff);
        }

        /// <summary>
        /// Directional biome height field (world units, relative to Y=0).
        /// North = +Z, East = +X, South = -Z, West = -X. The four cardinal
        /// fields are blended by direction weight so the biomes meet smoothly
        /// at the diagonals.
        /// </summary>
        private static float SampleBiomeHeight(float worldX, float worldZ)
        {
            // WO-468 Phase 2: callers pass TRUE world coords; the biome fields are
            // authored in TERRAIN-CENTERED space (-size/2..+size/2). Re-center Z by
            // the terrain's world-Z center so the biomes render identically, just
            // shifted south. X-center is 0, so worldX is already centered.
            float cz = worldZ - TerrainCenterZ;

            // Direction weights -- how "north", "east" etc. this point is.
            // Normalised so the dominant direction drives its biome field.
            float wN = Mathf.Max(0f, cz) / (TerrainSizeXZ * 0.5f);
            float wS = Mathf.Max(0f, -cz) / (TerrainSizeXZ * 0.5f);
            float wE = Mathf.Max(0f, worldX) / (TerrainSizeXZ * 0.5f);
            float wW = Mathf.Max(0f, -worldX) / (TerrainSizeXZ * 0.5f);
            float sum = wN + wS + wE + wW;
            if (sum < 0.0001f) return 0f;
            wN /= sum; wS /= sum; wE /= sum; wW /= sum;

            float h = 0f;
            h += wN * NorthHeight(worldX, cz);
            h += wS * SouthHeight(worldX, cz);
            h += wE * EastHeight(worldX, cz);
            h += wW * WestHeight(worldX, cz);

            // WORLDFEEL: gentle rolling relief on top of the biome fields. Added at
            // this single chokepoint so CreateTerrainData, WorldHeightAt, the splat
            // slope rules and the tree/rock ground-sampling all see the SAME surface.
            // The corridor flatten + castle depression are applied AFTER this by every
            // caller, so the protected lanes stay exactly flat.
            h += ReliefHeight(worldX, cz);
            return h;
        }

        /// <summary>
        /// WORLDFEEL undulation (world metres): multi-scale FBM + compass-asymmetric
        /// character, masked to ZERO inside <see cref="ReliefFlatRadius"/> (castle /
        /// moat / critical spawns) and full beyond <see cref="ReliefBlendRadius"/>.
        /// Terrain-CENTERED coords (same space as the biome fields).
        /// </summary>
        private static float ReliefHeight(float x, float cz)
        {
            float r = Mathf.Sqrt(x * x + cz * cz);
            if (r <= ReliefFlatRadius) return 0f;
            float mask = r >= ReliefBlendRadius
                ? 1f
                : Mathf.SmoothStep(0f, 1f, (r - ReliefFlatRadius) / (ReliefBlendRadius - ReliefFlatRadius));

            // Multi-scale: long hills + medium rolls + fine bumps (not one soft sine).
            float f = ReliefFrequency;
            float large = PerlinFbm(x * f + 101f, cz * f + 57f, 3) * 2f;           // ±1
            float mid   = PerlinFbm(x * f * 2.4f + 40f, cz * f * 2.4f + 88f, 3) * 2f;
            float fine  = PerlinFbm(x * f * 5.5f + 17f, cz * f * 5.5f + 203f, 2) * 2f;
            float n = large * 0.52f + mid * 0.33f + fine * 0.15f;

            // Compass character so NW ≠ SE (unique landmarks, not tiled noise).
            float character = RegionalCharacter(x, cz);

            return (n * ReliefAmplitude + character) * mask;
        }

        /// <summary>
        /// Asymmetric land features (metres) so each direction of travel feels different:
        /// NW foothills, NE farm rolls, SW broken shelves, SE soft hollows + a few
        /// signature mounds/hollows the player can navigate by eye.
        /// </summary>
        private static float RegionalCharacter(float x, float cz)
        {
            // Soft quadrant weights (0 at origin, grow with distance into that sector).
            float len = Mathf.Max(1f, Mathf.Sqrt(x * x + cz * cz));
            float n = Mathf.Clamp01(cz / len);
            float s = Mathf.Clamp01(-cz / len);
            float e = Mathf.Clamp01(x / len);
            float w = Mathf.Clamp01(-x / len);

            // NW foothills — bigger rises toward the forest mountains.
            float nwHills = Mathf.Max(0f, PerlinFbm(x * 0.014f + 3f, cz * 0.014f + 9f, 3)) * 7f * n * w;
            // NE rolling farmland knolls.
            float neRolls = Mathf.Sin(x * 0.04f + cz * 0.02f) * 2.8f * n * e
                            + PerlinFbm(x * 0.02f + 21f, cz * 0.02f + 5f, 2) * 2.2f * n * e;
            // SW broken shelves / terraces (more angular).
            float swShelf = (Mathf.PerlinNoise(x * 0.05f + 70f, cz * 0.05f + 12f) - 0.35f) * 6.5f * s * w;
            // SE soft hollows (negative = dips that can hold ponds).
            float seHollow = Mathf.Min(0f, PerlinFbm(x * 0.016f + 55f, cz * 0.016f + 33f, 3) * 2f) * 6f * s * e;

            // Signature landmarks (hand-placed mounds/hollows) — navigate-by-eye.
            float landmarks =
                SignatureBump(x, cz,  165f,  130f, 32f,  7.5f) +  // NE knoll
                SignatureBump(x, cz, -175f,  145f, 38f,  9.0f) +  // NW foothill
                SignatureBump(x, cz, -155f, -160f, 30f,  6.0f) +  // SW shelf
                SignatureBump(x, cz,  170f, -150f, 36f, -5.5f) +  // SE hollow (negative)
                SignatureBump(x, cz,   90f, -220f, 28f,  4.5f) +  // south ridge freckle
                SignatureBump(x, cz, -220f,   40f, 34f,  5.5f);   // west river-ridge freckle

            float raw = nwHills + neRolls + swShelf + seHollow + landmarks;
            return Mathf.Clamp(raw, -CharacterAmplitude * 1.4f, CharacterAmplitude * 1.6f);
        }

        /// <summary>Smooth radial mound (or hollow if amp &lt; 0) centered at (cx,cz).</summary>
        private static float SignatureBump(float x, float z, float cx, float cz, float radius, float amp)
        {
            float dx = x - cx, dz = z - cz;
            float d2 = dx * dx + dz * dz;
            float r2 = radius * radius;
            if (d2 >= r2) return 0f;
            float t = 1f - d2 / r2;                 // 1 at center → 0 at edge
            return amp * t * t;                     // smooth falloff
        }

        // ── North: rising forest -> pine -> rock -> snow (§9.2) ──────────────
        // Steeper silhouette + outcrops so the north reads "mountain country".
        private static float NorthHeight(float x, float z)
        {
            float d = Mathf.Max(0f, z - VillageHalfZ);          // distance past wall
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfZ));
            float rise = Mathf.SmoothStep(0f, 1f, t);
            float baseH = Mathf.Lerp(0f, 32f, rise);            // was 28
            float ridge = Mathf.Pow(t, 3.2f) * 12f;             // was pow4 * 9 — earlier silhouette
            float lumps = PerlinFbm(x * 0.018f + 11f, z * 0.018f + 4f, 3) * 8f * t;
            return baseH + ridge + lumps;
        }

        // ── East: rolling farmland with real knolls (not ±2m whispers) ───────
        private static float EastHeight(float x, float z)
        {
            float d = Mathf.Max(0f, x - VillageHalfX);
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfX));
            float rolls = Mathf.Sin(z * 0.032f) * Mathf.Cos(x * 0.024f) * 7f;   // was 4.5
            float gentle = PerlinFbm(x * 0.012f + 31f, z * 0.012f + 19f, 3) * 6.5f;
            float knolls = Mathf.Max(0f, PerlinFbm(x * 0.025f + 8f, z * 0.025f + 44f, 2)) * 5f;
            return (rolls + gentle + knolls - 2.5f) * t;
        }

        // ── South: descending barren toward "the Wound" ─────────────────────
        private static float SouthHeight(float x, float z)
        {
            float d = Mathf.Max(0f, -z - VillageHalfZ);
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfZ));
            float sink = Mathf.SmoothStep(0f, 1f, t);
            float baseH = Mathf.Lerp(0f, -16f, sink);           // was -14 — deeper wound approach
            float cracks = PerlinFbm(x * 0.03f + 7f, z * 0.03f + 23f, 3) * 5f * t;
            float shelves = Mathf.Sin(x * 0.05f) * 2.5f * t;    // broken terraces
            return baseH + cracks + shelves - 2f * sink;
        }

        // ── West: river valley — deeper cut, higher flanking ridges ──────────
        private static float WestHeight(float x, float z)
        {
            float d = Mathf.Max(0f, -x - VillageHalfX);
            float t = Mathf.Clamp01(d / (TerrainSizeXZ * 0.5f - VillageHalfX));
            float riverX = -VillageHalfX - 48f + Mathf.Sin(z * 0.02f) * 18f;
            float distToRiver = Mathf.Abs(x - riverX);
            float valley = -7.5f * Mathf.Clamp01(1f - distToRiver / 30f);       // was -5 / 26
            float ridgeNear = 15f * Mathf.Clamp01(1f - Mathf.Abs(distToRiver - 42f) / 28f);
            float ridgeFar = 12f * Mathf.Clamp01(1f - Mathf.Abs(distToRiver - 90f) / 36f);
            float relief = PerlinFbm(x * 0.02f + 51f, z * 0.02f + 61f, 3) * 5.5f;
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
            // Mirror CreateTerrainData: only the cave corridor (WO-468) flattens
            // now — the village/seam plateau is gone (WO-468 Phase 2).
            float flat = CorridorWeight(worldX, worldZ);
            float elevatedY = Mathf.Lerp(biomeY, 0f, flat);
            // WO-468 wrapped-seam: mirror CreateTerrainData's castle-footprint depression so
            // steepness/scatter sampling matches the real surface.
            float castleW = SeamWeight(worldX, worldZ);
            return Mathf.Lerp(elevatedY, CastleDepressionDepth, castleW);
        }

        /// <summary>
        /// Flatten weight (0..1) for the cave-path corridor (WO-468): 1.0 within
        /// <see cref="CavePathFlattenHalf"/> of the road line (x≈0, z in
        /// [CavePathEndZ, CavePathStartZ]) so the terrain is held flat at Y≈0,
        /// smoothly falling to 0 across <see cref="CavePathFlattenFalloff"/>.
        /// Mirrors <see cref="SeamWeight"/>'s smoothstep falloff so the corridor
        /// blends seamlessly into the surrounding biome.
        /// </summary>
        private static float CorridorWeight(float worldX, float worldZ)
        {
            float dist = DistanceToCavePath(worldX, worldZ);
            if (dist <= CavePathFlattenHalf) return 1f;
            if (dist >= CavePathFlattenHalf + CavePathFlattenFalloff) return 0f;
            float t = (dist - CavePathFlattenHalf) / CavePathFlattenFalloff;
            return 1f - (t * t * (3f - 2f * t));
        }

        /// <summary>
        /// Shortest distance (world units) from an XZ point to the cave-path line
        /// segment — a vertical road at x≈0 spanning z in [CavePathEndZ,
        /// CavePathStartZ]. Used by the corridor flatten + the tree/rock reject.
        /// </summary>
        private static float DistanceToCavePath(float worldX, float worldZ)
        {
            float zClamped = Mathf.Clamp(worldZ, CavePathEndZ, CavePathStartZ);
            float dz = worldZ - zClamped;
            float dx = worldX; // path runs at x = 0
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // =====================================================================
        //  Splatmaps -- the per-march ground (§9.3, rebuilt by WO-1101)
        // =====================================================================

        /// <summary>
        /// Assigns the terrain layers declared by
        /// <see cref="DeNelle.Core.World.TerrainLayerSet"/> — real curated art, two layers
        /// per march — and paints the alphamaps from quadrant + slope + elevation.
        /// <para>
        /// The four marches are separated by VALUE, TEXTURE and LIGHT, never hue (the owner
        /// is red/green colourblind; WO-1044 §1 is canon and TerrainLayerRegression asserts
        /// the ΔL). The path layer is left near-zero here;
        /// <see cref="PaintNaturalPaths"/> stamps roads into it afterwards.
        /// </para>
        /// </summary>
        private static void PaintSplatmaps(TerrainData td)
        {
            // ── Layers come from the ONE authority (WO-1101) ─────────────────
            // Every layer is now real curated art (BaseColor + Normal, 1024, tracked under
            // Assets/Generated/Terrain/Layers/) instead of a 64x64 procedural swatch.
            // MakeLayer falls back to MakeSolidTexture + a WARNING if a PNG is missing so a
            // fresh clone still bakes (CLAUDE.md §4: missing art warns, never errors).
            // Idempotency: drop the pre-WO-1101 layer assets (Exterior_Grass/Stone/Mud/Snow/Dead).
            // They are superseded by the named layers below; left on disk they are dead assets that
            // still LOOK like the ground contract to the next reader.
            foreach (var legacy in new[] { "Exterior_Grass", "Exterior_Stone", "Exterior_Mud", "Exterior_Snow", "Exterior_Dead" })
            {
                string legacyPath = TerrainAssetDir + "/" + legacy + ".terrainlayer";
                if (File.Exists(legacyPath))
                {
                    AssetDatabase.DeleteAsset(legacyPath);
                    _notes.Add("removed legacy layer asset " + legacy);
                }
            }

            var layers = new TerrainLayer[LayerCount];
            for (int i = 0; i < LayerCount; i++) layers[i] = MakeLayer(i);
            td.terrainLayers = layers;

            // Layer manifest — a capture/log diff proves WHICH contract a bake ran with
            // (CLAUDE.md §12). Without this a stale bake and a fresh one read identically.
            Debug.Log("[ExteriorTerrainBuilder] SPLAT MANIFEST (bake authority) " +
                      TerrainLayerSet.Manifest());
            FlowTrace.Step("World", "ExteriorTerrainBuilder.PaintSplatmaps — " + TerrainLayerSet.Manifest());

            int res = td.alphamapResolution;
            var splat = new float[res, res, LayerCount];
            var coverage = new double[LayerCount];
            // Hoisted scratch — res is 1024, so a per-cell allocation here would be a
            // million array allocations per bake.
            var w = new float[LayerCount];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = x / (float)(res - 1);
                    float v = z / (float)(res - 1);
                    // TRUE world position (terrain shifted south by TerrainCenterZ).
                    float worldX = (u - 0.5f) * TerrainSizeXZ;
                    float worldZ = (v - 0.5f) * TerrainSizeXZ + TerrainCenterZ;
                    // Terrain-CENTERED Z for biome-direction (north/south) tests.
                    float cz = worldZ - TerrainCenterZ;

                    float y = WorldHeightAt(worldX, worldZ);
                    float slope = SteepnessAt(worldX, worldZ);   // 0..1

                    // ── Quadrant weights — the SAME derivation the runtime repaint uses
                    //    (WorldSceneLoader.BuildQuadrantWeights). Bake and runtime must
                    //    agree or the ground changes the moment the player is on device.
                    float qGold, qStone, qMire, qAsh;
                    TerrainLayerSet_QuadrantWeights(worldX, cz, out qGold, out qStone, out qMire, out qAsh);

                    // Low-frequency mottle -> each march blends 2 layers instead of reading
                    // as one flat sheet. This is the "texture" axis of the colourblind gate:
                    // biomes differ by GRAIN as well as by value.
                    float mottle = Mathf.PerlinNoise(worldX * 0.012f + 91f, worldZ * 0.012f + 47f);

                    System.Array.Clear(w, 0, LayerCount);

                    // Hub / centre meadow — whatever no march claims.
                    float centre = Mathf.Clamp01(1f - (qGold + qStone + qMire + qAsh));
                    w[LayerMeadow] = centre;

                    // GOLDFIELDS (E) — pale dry field, lowest internal contrast. A little
                    // meadow bleeds in near the hub so the seam is not a line.
                    w[LayerGoldfields] += qGold * (0.82f + 0.18f * mottle);
                    w[LayerMeadow] += qGold * (0.18f - 0.18f * mottle);

                    // STONEBACK (W) — faceted rock; snow ONLY here and only high (canon:
                    // snow patches are Stoneback's only true whites).
                    float snowHere = (y > 14f) ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 26f, y)) : 0f;
                    w[LayerStone] += qStone * (1f - snowHere) * (0.85f + 0.15f * (1f - mottle));
                    w[LayerSnow] += qStone * snowHere;
                    w[LayerMeadow] += qStone * (1f - snowHere) * (0.15f * mottle);

                    // MIREWOOD (S) — two layers in the SAME value band. Canon gives Mirewood
                    // the narrowest value range in the game, so its variety is texture-only:
                    // mire vs root-tangle, never a lighter/darker patch.
                    w[LayerMire] += qMire * (0.55f + 0.45f * mottle);
                    w[LayerMireRoots] += qMire * (0.45f - 0.45f * mottle);

                    // ASHWOOD (N) — PALE powdery ash (WO-1044 §1 "ink on ash"). See the
                    // inversion note in TerrainLayerSet: the shipped Exterior_Dead ground was
                    // L=0.176, the OPPOSITE of ratified canon. The darkness lives in the
                    // trunks, not the floor. Do not "restore" the dark ground.
                    w[LayerAsh] += qAsh * (0.88f + 0.12f * mottle);
                    w[LayerStone] += qAsh * (0.12f - 0.12f * mottle);   // grit/debris, not a second value

                    // Exposed rock on steep ground, in every march (was 0.42..0.72).
                    float rocky = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.72f, slope));
                    if (rocky > 0f)
                    {
                        for (int k = 0; k < LayerCount; k++) w[k] *= (1f - rocky);
                        w[LayerStone] += rocky;
                    }

                    float total = 0f;
                    for (int k = 0; k < LayerCount; k++) { if (w[k] < 0f) w[k] = 0f; total += w[k]; }
                    if (total < 0.0001f) { w[LayerMeadow] = 1f; total = 1f; }

                    for (int k = 0; k < LayerCount; k++)
                    {
                        float n = w[k] / total;
                        splat[z, x, k] = n;
                        coverage[k] += n;
                    }
                }
            }
            td.SetAlphamaps(0, 0, splat);

            // Per-layer coverage % — the proving line. A layer at 0.0% is an authored layer
            // that never reaches the player, which is exactly the silent defect §12 exists for.
            double cells = (double)res * res;
            var cov = new System.Text.StringBuilder("[ExteriorTerrainBuilder] SPLAT COVERAGE ");
            for (int k = 0; k < LayerCount; k++)
                cov.Append(TerrainLayerSet.Layers[k].Name).Append('=')
                   .Append((coverage[k] / cells * 100.0).ToString("0.0")).Append("%  ");
            Debug.Log(cov.ToString());
            for (int k = 0; k < LayerCount; k++)
            {
                // ⚠ SKIP Path_Dirt HERE, AND THIS IS NOT THE GUARD GOING SOFT.
                // This coverage is measured INSIDE PaintSplatmaps, which BuildExterior calls at the
                // biome-splat step — and PaintNaturalPaths stamps the roads on the NEXT line, after
                // us. So the road layer is legitimately 0.0% at this instant and warning about it is
                // measuring the wrong moment, not finding a defect. The first bake after the WO-1101
                // curation duly printed "Path_Dirt covers <0.05% — authored but effectively
                // invisible" and cost a real investigation to dismiss.
                //
                // A guard that cries wolf every single run is WORSE than no guard: the next reader
                // either burns an hour re-deriving that it is benign, or learns to ignore this
                // warning — and then misses the real one. The road layer's true coverage is asserted
                // after PaintNaturalPaths instead (see the road-coverage check there), which is the
                // only place the number means anything.
                if (k == TerrainLayerSet.PathDirt) continue;

                if (coverage[k] / cells < 0.0005)
                    Debug.LogWarning("[ExteriorTerrainBuilder] layer '" + TerrainLayerSet.Layers[k].Name +
                                     "' covers <0.05% of the terrain — authored but effectively invisible.");
            }
        }

        /// <summary>
        /// Quadrant membership weights (Goldfields E / Stoneback W / Mirewood S / Ashwood N)
        /// for a terrain-centred position. Normalised, so the four sum to ~1 away from the
        /// hub and fade to 0 at the centre.
        /// <para>
        /// ⚠ This derivation is MIRRORED in
        /// <c>DeNelle.Village.WorldSceneLoader.BuildQuadrantWeights</c>, which is the splat
        /// the player actually sees on device (DEF-108). The two must stay identical; the
        /// layer INDICES they write are already shared via
        /// <see cref="DeNelle.Core.World.TerrainLayerSet"/>. Changing the shape here without
        /// changing it there produces a ground that differs between editor and device.
        /// </para>
        /// </summary>
        private static void TerrainLayerSet_QuadrantWeights(
            float worldX, float centredZ, out float gold, out float stone, out float mire, out float ash)
        {
            float half = TerrainSizeXZ * 0.5f;
            // Hub hold-out: inside this radius the marches yield to the centre meadow so the
            // town is not painted as a biome.
            float hub = Mathf.Clamp01(Mathf.InverseLerp(70f, 190f,
                Mathf.Sqrt(worldX * worldX + centredZ * centredZ)));

            gold  = Mathf.Max(0f, worldX) / half;
            stone = Mathf.Max(0f, -worldX) / half;
            ash   = Mathf.Max(0f, centredZ) / half;
            mire  = Mathf.Max(0f, -centredZ) / half;

            float sum = gold + stone + ash + mire;
            if (sum < 0.0001f) { gold = stone = ash = mire = 0f; return; }
            gold  = gold / sum * hub;
            stone = stone / sum * hub;
            ash   = ash / sum * hub;
            mire  = mire / sum * hub;
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

        /// <summary>
        /// Builds one terrain layer from the CURATED art named by
        /// <see cref="DeNelle.Core.World.TerrainLayerSet"/> (WO-1101).
        /// <para>
        /// Before this change every layer's diffuse was <see cref="MakeSolidTexture"/> — a
        /// 64x64 solid colour with a Perlin mottle, embedded in ExteriorTerrainData.asset,
        /// with <c>normalMapTexture</c> left NULL on all five layers. Five 64-pixel swatches
        /// and zero normal maps is the whole reason the ground read flat: the world had no
        /// ground texture art at all, not bad art.
        /// </para>
        /// <para>
        /// FALLBACK, not a path we want: if a curated PNG is missing (fresh clone with LFS
        /// not fetched, or someone deleted the folder) the layer degrades to the old solid
        /// tint and WARNS. Never an error — a missing art asset must not stop a bake
        /// (CLAUDE.md §4).
        /// </para>
        /// </summary>
        private static TerrainLayer MakeLayer(int index)
        {
            var def = TerrainLayerSet.Layers[index];
            string basePath = TerrainLayerSet.BaseColorPath(index);
            string normPath = TerrainLayerSet.NormalPath(index);

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            var normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);

            if (baseTex == null)
            {
                _notes.Add("curated basecolor missing for '" + def.Name + "' -> solid-tint fallback");
                Debug.LogWarning("[ExteriorTerrainBuilder] curated ground texture NOT FOUND at '" + basePath +
                                 "' — layer '" + def.Name + "' falls back to a flat tint. The terrain will " +
                                 "bake, but this biome loses its texture and its measured value. " +
                                 "(Check git-lfs has been fetched for Assets/Generated/Terrain/Layers/.)");
                FlowTrace.Warn("World", "MakeLayer fallback: '" + def.Name + "' has no curated BaseColor at " + basePath);
                baseTex = MakeSolidTexture(def.FallbackTint);
            }
            if (normTex == null && baseTex != null)
            {
                _notes.Add("curated normal missing for '" + def.Name + "'");
                Debug.LogWarning("[ExteriorTerrainBuilder] curated NORMAL map NOT FOUND at '" + normPath +
                                 "' — layer '" + def.Name + "' will render without relief.");
                FlowTrace.Warn("World", "MakeLayer: '" + def.Name + "' has no curated Normal at " + normPath);
            }

            var layer = new TerrainLayer
            {
                name = def.Name,
                diffuseTexture = baseTex,
                normalMapTexture = normTex,
                normalScale = def.NormalScale,
                smoothness = def.Smoothness,
                metallic = 0f,
                specular = new Color(0.05f, 0.05f, 0.05f, 1f),
                tileSize = new Vector2(def.TileSize, def.TileSize),
                tileOffset = Vector2.zero,
            };

            string path = TerrainLayerSet.TerrainLayerPath(index);
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        /// <summary>
        /// LAST-RESORT ONLY (WO-1101): a small solid-colour texture for a layer whose curated
        /// PNG is missing. This used to be how EVERY layer got its diffuse; it is now the
        /// warn-and-continue path so a clone without the art still produces a valid terrain
        /// instead of a null-diffuse (colourless/pink) one.
        /// </summary>
        private static Texture2D MakeSolidTexture(Color c)
        {
            // WORLDFEEL 2026-07-02: was a 32x32 pixel-noise tint (±0.06) that still read
            // dead-flat at play distance. Now 64x64 with a LOW-FREQUENCY perlin mottle
            // (±22% luminance patches, seeded per-colour so each layer mottles uniquely)
            // + fine grain, so every layer carries visible tonal variation when tiled.
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var px = new Color[size * size];
            float seed = c.r * 17f + c.g * 31f + c.b * 47f;   // per-layer mottle offset
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Low-frequency mottle (reads as patches when the layer tiles) ...
                    float m = (Mathf.PerlinNoise(x * 0.09f + seed, y * 0.09f + seed * 1.7f) - 0.5f) * 0.44f;
                    // ... plus fine per-pixel grain.
                    float g = (float)(_rng.NextDouble() - 0.5) * 0.05f;
                    float k = 1f + m;
                    px[y * size + x] = new Color(
                        Mathf.Clamp01(c.r * k + g),
                        Mathf.Clamp01(c.g * k + g),
                        Mathf.Clamp01(c.b * k + g));
                }
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
        /// Paints roads + footpaths into the mud splat layer so the world reads as
        /// connected (owner 2026-08-07: paths, roads, and grass). Includes:
        /// main roads from castle approaches to dungeon portals, a ring road outside
        /// the moat, narrative footpaths, and the south cave corridor.
        /// </summary>
        private static void PaintNaturalPaths(TerrainData td)
        {
            int res = td.alphamapResolution;
            var splat = td.GetAlphamaps(0, 0, res, res);

            // ── MAIN ROADS (wide, high opacity) — castle → portals / destinations ──
            // Gate approaches sit just outside the wall ring (~r 50–55). Portals match
            // DungeonWorldPortalSpawner.AuthoredPortals.
            const float roadHalf = 7.5f;       // clear dirt road
            const float trailHalf = 4.2f;      // worn footpath

            // East road → Starter Loop portal (140, 20)
            PaintPolylineToMud(splat, res, new[]
            {
                V(52f, 0f), V(78f, 6f), V(110f, 14f), V(140f, 20f),
            }, roadHalf);

            // NW road → Sunken Vault portal (-100, 100)
            PaintPolylineToMud(splat, res, new[]
            {
                V(-38f, 38f), V(-58f, 58f), V(-78f, 80f), V(-100f, 100f),
            }, roadHalf);

            // South road → Healer's Cottage portal (20, -140)
            PaintPolylineToMud(splat, res, new[]
            {
                V(0f, -52f), V(8f, -80f), V(16f, -110f), V(20f, -140f),
            }, roadHalf);

            // North road — forest approach / animal track toward ridges
            PaintPolylineToMud(splat, res, new[]
            {
                V(0f, 52f), V(12f, 90f), V(40f, 130f), V(70f, 170f), V(100f, 210f),
            }, trailHalf);

            // West road — toward river valley
            PaintPolylineToMud(splat, res, new[]
            {
                V(-52f, 0f), V(-90f, -10f), V(-130f, -20f), V(-170f, -5f), V(-210f, 30f),
            }, trailHalf);

            // Ring road outside moat (~r 78) — links the four approaches so the castle
            // sits in a green field with a dirt circuit, not a featureless pad.
            PaintRingRoad(splat, res, radius: 78f, halfWidth: 5.5f, segments: 64);

            // ── Narrative footpaths (storytelling) ──
            var trails = new[]
            {
                new[] { V(0f, 124f), V(22f, 138f), V(58f, 132f), V(96f, 116f), V(124f, 96f) },
                new[] { V(-150f, 96f), V(-186f, 52f), V(-198f, -8f), V(-176f, -64f), V(-148f, -118f) },
                new[] { V(132f, 70f), V(160f, 40f), V(176f, 4f), V(168f, -38f), V(150f, -78f) },
                new[] { V(-128f, 96f), V(-150f, 118f), V(-176f, 136f), V(-198f, 150f) },
                // Cross-link: starter east road → hunter's orchards
                new[] { V(140f, 20f), V(155f, -10f), V(160f, -50f) },
                // Cross-link: sunken NW → crystal ridge
                new[] { V(-100f, 100f), V(-140f, 120f), V(-175f, 140f) },
            };
            foreach (var pts in trails)
                PaintPolylineToMud(splat, res, pts, trailHalf);

            // South cave corridor road (wide main route)
            PaintPolylineToMud(splat, res, new[]
            {
                V(0f, CavePathStartZ),
                V(0f, CavePathEndZ),
            }, CavePathHalfWidth);

            td.SetAlphamaps(0, 0, splat);

            // ROAD COVERAGE — asserted HERE, because here is the only place the number means
            // anything. PaintSplatmaps measures per-layer coverage before this method runs, so the
            // road layer is necessarily 0.0% at that point; its check deliberately skips PathDirt
            // and defers to this one. If the roads ever stop reaching the alphamap, THIS is the line
            // that says so — and unlike the earlier check, a warning from it is always real.
            double roadCells = 0.0, totalCells = (double)res * res;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    roadCells += splat[z, x, TerrainLayerSet.PathDirt];

            double roadPct = roadCells / totalCells * 100.0;
            FlowTrace.Step("Exterior",
                $"PaintNaturalPaths: gate roads to 3 portals + ring road + footpaths + cave corridor " +
                $"— Path_Dirt now covers {roadPct:0.00}% of the terrain.");
            Debug.Log($"[ExteriorTerrainBuilder] ROAD COVERAGE Path_Dirt={roadPct:0.00}% (post-stamp; " +
                      "the SPLAT COVERAGE line above is measured pre-stamp and reads 0.0% by design).");

            if (roadPct < 0.05)
                Debug.LogWarning("[ExteriorTerrainBuilder] Path_Dirt covers <0.05% AFTER the road stamp — " +
                                 "the 5 natural paths were generated but are not reaching the alphamap. " +
                                 "This one IS a defect: the roads are invisible to the player.");
        }

        /// <summary>Dirt ring road around the castle (outside moat clear zone).</summary>
        private static void PaintRingRoad(float[,,] splat, int res, float radius, float halfWidth, int segments)
        {
            var pts = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                // Slight oval so E-W / N-S approaches meet cleanly.
                float rx = radius * 1.05f;
                float rz = radius * 0.95f;
                pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz);
            }
            PaintPolylineToMud(splat, res, pts, halfWidth);
        }

        private static Vector2 V(float x, float z) => new Vector2(x, z);

        /// <summary>
        /// Like <see cref="V"/> but treats the Z as a terrain-CENTERED coordinate
        /// (old village-origin space) and offsets it into TRUE world Z by
        /// <see cref="TerrainCenterZ"/> (WO-468 Phase 2 south shift). Used for the
        /// cosmetic narrative paths authored before the un-stack.
        /// </summary>
        private static Vector2 Vp(float x, float z) => new Vector2(x, z + TerrainCenterZ);

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
            // TRUE world XZ -> splatmap pixel space. The terrain spans
            // [-size/2, +size/2] in X and [TerrainCenterZ-size/2,
            // TerrainCenterZ+size/2] in Z (WO-468 Phase 2 south shift).
            float u = worldX / TerrainSizeXZ + 0.5f;
            float v = (worldZ - TerrainCenterZ) / TerrainSizeXZ + 0.5f;
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
                    // Soft edge: strong packed dirt at centre (reads as road), soft grass shoulders.
                    float t = 1f - (distPx / Mathf.Max(1, radPx));
                    float w = t * t;
                    // Boost centre so roads punch through grass (owner: want visible paths).
                    if (t > 0.55f) w = Mathf.Min(1f, w * 1.35f);
                    // WO-1101: the road stamps into Path_Dirt and takes its weight from
                    // EVERY other layer PROPORTIONALLY. The old code took from grass then
                    // stone by name, which silently stopped renormalising the moment the
                    // layer set grew past those two — the alphamap would no longer sum to 1
                    // and the road would read as a translucent smear over the new biomes.
                    float road = Mathf.Max(splat[z, x, LayerPath], w);
                    float take = road - splat[z, x, LayerPath];
                    if (take <= 0f) continue;
                    splat[z, x, LayerPath] = road;

                    float rest = 0f;
                    for (int k = 0; k < LayerCount; k++)
                        if (k != LayerPath) rest += splat[z, x, k];
                    if (rest <= 0.0001f) continue;
                    float keep = Mathf.Max(0f, 1f - road) / rest;
                    for (int k = 0; k < LayerCount; k++)
                        if (k != LayerPath) splat[z, x, k] *= keep;
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
                // TRUE world position (terrain shifted south by TerrainCenterZ) +
                // terrain-CENTERED Z for the biome-direction tests below.
                float worldX = (nx - 0.5f) * TerrainSizeXZ;
                float worldZ = (nz - 0.5f) * TerrainSizeXZ + TerrainCenterZ;
                float cz = worldZ - TerrainCenterZ;

                // No village footprint anymore (WO-468 Phase 2) — SeamWeight is 0;
                // kept as a guard in case the plateau is ever reinstated.
                if (SeamWeight(worldX, worldZ) > 0.05f) continue;

                // WO-468: keep the cave road CLEAR. Reject any candidate within
                // the flattened corridor + a margin so trees never block the road.
                if (DistanceToCavePath(worldX, worldZ) <
                    CavePathFlattenHalf + CavePathFlattenFalloff + 3f) continue;

                float y = WorldHeightAt(worldX, worldZ);
                float slope = SteepnessAt(worldX, worldZ);
                if (slope > 0.55f) continue;                 // too steep -- bare rock

                // ── Biome density + prototype selection ─────────────────────
                int protoIndex;
                float keepChance;

                if (cz > VillageHalfZ)
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
                else if (cz < -VillageHalfZ)
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

                // WORLDFEEL: horizon tree-line — floor the keep-chance high near the
                // terrain edge so every horizon direction reads as a treeline silhouette
                // (owner 2026-07-01: the horizon was empty ground meeting an empty sky).
                // The north rock/snow line (y > 20 'continue' above) still wins there.
                float edgeDist = TerrainSizeXZ * 0.5f - Mathf.Max(Mathf.Abs(worldX), Mathf.Abs(cz));
                if (edgeDist < HorizonBandWidth)
                    keepChance = Mathf.Max(keepChance, HorizonKeepChance);

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

        // =====================================================================
        //  Ground cover -- detail grass + bush clutter (WO-1101)
        // =====================================================================

        /// <summary>
        /// Registers Unity Terrain DETAIL prototypes and paints per-biome clutter density.
        /// <para>
        /// This is the owner's literal ask ("grass and simple aesthetics") and it was
        /// entirely unbuilt: <c>CreateTerrainData</c> has always called
        /// <c>SetDetailResolution(512, 16)</c> and then never registered a single
        /// <see cref="DetailPrototype"/> — there was no grass clutter anywhere in the world.
        /// Terrain details are GPU-instanced and distance-culled, so this is the cheapest
        /// high-impact art in the pass.
        /// </para>
        /// <para>
        /// ⚠ PREFAB NAMES ARE VERIFIED AGAINST DISK, not against
        /// <c>docs/WORLD_BIOME_SCATTER_DIRECTION.md</c>. That doc carries two verified errors
        /// that resolve to NULL prefabs (its polyperfect base path is one folder short for
        /// every Nature name, and <c>Stones_Small</c> does not exist — the real prefab is
        /// <c>Stone_Small</c>). It also prescribes differentiating biomes by TINT, which
        /// WO-1044 retires: hue-only differentiation is unusable to a red/green colourblind
        /// owner and fails the greyscale gate. We use KayKit because it is GIT-TRACKED;
        /// polyperfect and Blink are gitignored.
        /// </para>
        /// Densities follow WO-1044 §1: Goldfields dense and moving, Stoneback near-bare
        /// scrub, Mirewood dense understory, Ashwood BARE (silhouette does the work there —
        /// clutter would soften the one biome whose identity is emptiness).
        /// </summary>
        private static void PaintDetailGroundCover(TerrainData td)
        {
            // Single-sided grass cards are the cheap ones; the KayKit pack ships them
            // explicitly for this use. Verified present under ForestPackColor1.
            var protoNames = new[]
            {
                "Grass_1_A_Singlesided",   // 0 — fine field grass (Goldfields / meadow hero)
                "Grass_2_B_Singlesided",   // 1 — coarser tuft, breaks the repeat
                "Bush_1_A",                // 2 — Mirewood understory
                "Bush_4_A",                // 3 — Stoneback scrub, sparse
            };

            var protos = new List<DetailPrototype>();
            var resolved = new List<int>();       // index into protoNames, parallel to protos
            for (int i = 0; i < protoNames.Length; i++)
            {
                string path = ForestPackColor1 + protoNames[i] + "_Color1.fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    _notes.Add("detail prefab missing: " + protoNames[i]);
                    Debug.LogWarning("[ExteriorTerrainBuilder] ground-cover mesh not found at '" + path +
                                     "' — skipping that detail prototype (terrain still valid).");
                    continue;
                }
                if (go.GetComponentInChildren<MeshRenderer>(true) == null)
                {
                    _notes.Add("detail prefab has no MeshRenderer: " + protoNames[i]);
                    Debug.LogWarning("[ExteriorTerrainBuilder] '" + path + "' has no MeshRenderer — " +
                                     "cannot be a mesh detail prototype. Skipped.");
                    continue;
                }
                protos.Add(new DetailPrototype
                {
                    prototype = go,
                    usePrototypeMesh = true,
                    renderMode = DetailRenderMode.VertexLit,   // required for mesh prototypes
                    useInstancing = true,                      // GPU instanced — mobile-cheap
                    minWidth = 0.7f, maxWidth = 1.25f,
                    minHeight = 0.7f, maxHeight = 1.35f,
                    noiseSeed = 20261101 + i,
                    noiseSpread = 0.35f,
                    healthyColor = Color.white,
                    dryColor = Color.white,   // ⚠ no hue tinting — WO-1044 retires tint-as-differentiator
                });
                resolved.Add(i);
            }

            if (protos.Count == 0)
            {
                Debug.LogWarning("[ExteriorTerrainBuilder] NO ground-cover prototypes resolved — " +
                                 "detail pass skipped (terrain still valid).");
                FlowTrace.Warn("World", "PaintDetailGroundCover: zero prototypes resolved from " + ForestPackColor1);
                return;
            }

            td.detailPrototypes = protos.ToArray();
            _detailPrototypeCount = protos.Count;

            int dw = td.detailWidth, dh = td.detailHeight;
            var maps = new int[protos.Count][,];
            for (int p = 0; p < protos.Count; p++) maps[p] = new int[dh, dw];

            int painted = 0;
            for (int dz = 0; dz < dh; dz++)
            {
                float v = dz / (float)(dh - 1);
                float worldZ = (v - 0.5f) * TerrainSizeXZ + TerrainCenterZ;
                float cz = worldZ - TerrainCenterZ;
                for (int dx = 0; dx < dw; dx++)
                {
                    float u = dx / (float)(dw - 1);
                    float worldX = (u - 0.5f) * TerrainSizeXZ;

                    // Steep ground and the cave road stay clear.
                    if (SteepnessAt(worldX, worldZ) > 0.45f) continue;
                    if (DistanceToCavePath(worldX, worldZ) < CavePathFlattenHalf + 2f) continue;

                    float qGold, qStone, qMire, qAsh;
                    TerrainLayerSet_QuadrantWeights(worldX, cz, out qGold, out qStone, out qMire, out qAsh);
                    float centre = Mathf.Clamp01(1f - (qGold + qStone + qMire + qAsh));

                    // Patchiness so clutter reads as clumps, not a carpet.
                    float patch = Mathf.PerlinNoise(worldX * 0.03f + 13f, worldZ * 0.03f + 71f);

                    // Per-biome grass / bush density (0..1), WO-1044 §1.
                    //   Goldfields: dense fine grass — the field is the biome.
                    //   Meadow (hub): dense.
                    //   Stoneback:  near-bare, occasional scrub.
                    //   Mirewood:   dense understory bush.
                    //   Ashwood:    BARE — deliberately zero. Emptiness IS the identity.
                    float grass = (qGold * 1.0f + centre * 0.85f + qStone * 0.10f + qMire * 0.35f) * patch;
                    float bushMire = qMire * 0.9f * patch;
                    float bushRock = qStone * 0.18f * patch;

                    WriteDetail(maps, resolved, 0, dz, dx, grass * 7f);
                    WriteDetail(maps, resolved, 1, dz, dx, grass * 4f);
                    WriteDetail(maps, resolved, 2, dz, dx, bushMire * 3f);
                    WriteDetail(maps, resolved, 3, dz, dx, bushRock * 2f);
                    if (grass + bushMire + bushRock > 0.05f) painted++;
                }
            }

            for (int p = 0; p < protos.Count; p++) td.SetDetailLayer(0, 0, p, maps[p]);
            _detailPatchCount = painted;

            Debug.Log("[ExteriorTerrainBuilder] GROUND COVER " + protos.Count + " prototype(s) [" +
                      string.Join(", ", protos.ConvertAll(p => p.prototype != null ? p.prototype.name : "NULL")) +
                      "] over " + painted + "/" + (dw * dh) + " detail cells " +
                      "(Ashwood intentionally bare — WO-1044 §1).");
            FlowTrace.Step("World", "PaintDetailGroundCover: " + protos.Count + " prototypes, " +
                           painted + " populated detail cells.");
        }

        /// <summary>Writes one detail cell if that prototype resolved (indices shift when art is missing).</summary>
        private static void WriteDetail(int[][,] maps, List<int> resolved, int wantedName, int z, int x, float density)
        {
            int slot = resolved.IndexOf(wantedName);
            if (slot < 0) return;                       // that prefab was missing — warned already
            int d = Mathf.RoundToInt(Mathf.Clamp(density, 0f, 12f));
            if (d > 0) maps[slot][z, x] = d;
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
            // Owner direction 2026-05-20 ("rocks in front of door"): the old
            // scatter landed rocks right outside the cardinal gates. WORLDFEEL
            // 2026-07-02 re-enables it (owner: "world feels empty") with the
            // exclusion that was missing then: a hard 130 m clear radius around
            // the origin keeps every gate exit / moat lane / spawn arc / walk-to
            // outpost anchor rock-free; boulders live out in the wilderness.
            const float BoulderOriginClearRadius = 130f;
            int boulderTarget = 140;
            int attempts = 0;
            while (_rockCount < boulderTarget && attempts < boulderTarget * 20)
            {
                attempts++;
                // TRUE world position (terrain shifted south) + centered Z for the
                // biome-direction "rocky north" test.
                float worldX = ((float)_rng.NextDouble() - 0.5f) * TerrainSizeXZ;
                float worldZ = ((float)_rng.NextDouble() - 0.5f) * TerrainSizeXZ + TerrainCenterZ;
                float cz = worldZ - TerrainCenterZ;
                if (SeamWeight(worldX, worldZ) > 0.05f) continue;
                // WORLDFEEL: hard origin clear-zone — no boulders near the castle
                // gates / moat lanes / spawn arcs / outpost anchors (see boulderTarget note).
                if (Mathf.Sqrt(worldX * worldX + cz * cz) < BoulderOriginClearRadius) continue;
                // WO-468: keep the cave road clear of boulders too (matches PaintTrees).
                if (DistanceToCavePath(worldX, worldZ) <
                    CavePathFlattenHalf + CavePathFlattenFalloff + 3f) continue;

                float y = WorldHeightAt(worldX, worldZ);
                float slope = SteepnessAt(worldX, worldZ);
                // Boulders favour slopes + the rocky north / valley ridges.
                bool rocky = slope > 0.22f || (cz > 60f && y > 14f);
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
            // Re-enabled 2026-08-07 with hard exclusion near the castle (old bug:
            // blue discs read as a leftover "heart pond" under the spire). Only seat
            // ponds outside the clear/moat ring, off the cave road, in real low spots.
            var pondRoot = new GameObject("Ponds");
            pondRoot.transform.SetParent(parent, false);

            // Seeded candidates across wilderness rings — then keep the lowest.
            const int candidates = 48;
            const int keep = 10;
            const float minCastleDist = 110f;   // never near moat/spire
            var scored = new System.Collections.Generic.List<(float x, float z, float y, float score)>(candidates);

            for (int i = 0; i < candidates; i++)
            {
                // Annulus 120..420 from origin (play wilderness, not horizon only).
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float dist = 120f + (float)_rng.NextDouble() * 300f;
                float px = Mathf.Cos(ang) * dist;
                float pz = Mathf.Sin(ang) * dist;

                if (Mathf.Sqrt(px * px + pz * pz) < minCastleDist) continue;
                // Keep the south cave road corridor dry.
                if (Mathf.Abs(px) < CavePathFlattenHalf + 8f && pz < CavePathStartZ && pz > CavePathEndZ)
                    continue;
                // Avoid portal seats (starter east, sunken NW, cottage south).
                if (Vector2.Distance(new Vector2(px, pz), new Vector2(140f, 20f)) < 28f) continue;
                if (Vector2.Distance(new Vector2(px, pz), new Vector2(-100f, 100f)) < 28f) continue;
                if (Vector2.Distance(new Vector2(px, pz), new Vector2(20f, -140f)) < 28f) continue;

                float y = WorldHeightAt(px, pz);
                // Prefer natural hollows (lower than neighbourhood).
                float yN = WorldHeightAt(px, pz + 12f);
                float yS = WorldHeightAt(px, pz - 12f);
                float yE = WorldHeightAt(px + 12f, pz);
                float yW = WorldHeightAt(px - 12f, pz);
                float neigh = 0.25f * (yN + yS + yE + yW);
                float score = neigh - y; // positive = local low
                if (score < 0.35f) continue; // not a real hollow
                scored.Add((px, pz, y, score));
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));
            int placed = 0;
            for (int i = 0; i < scored.Count && placed < keep; i++)
            {
                var s = scored[i];
                // Min spacing against already placed ponds.
                bool near = false;
                for (int c = 0; c < pondRoot.transform.childCount; c++)
                {
                    var other = pondRoot.transform.GetChild(c).position;
                    if (Vector2.Distance(new Vector2(s.x, s.z), new Vector2(other.x, other.z)) < 40f)
                    {
                        near = true;
                        break;
                    }
                }
                if (near) continue;

                var pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pond.name = $"Pond_{placed}";
                pond.transform.SetParent(pondRoot.transform, false);
                float radius = 6f + (float)_rng.NextDouble() * 7f;
                pond.transform.localScale = new Vector3(radius, 0.1f, radius);
                // Water surface just under the local hollow floor.
                pond.transform.position = new Vector3(s.x, s.y - 0.45f, s.z);
                var col = pond.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                // Quiet teal — not bright "heart pond" blue.
                ApplyWater(pond, new Color(0.18f, 0.38f, 0.42f, 0.82f));
                placed++;
                _pondCount++;
            }

            FlowTrace.Step("Exterior",
                $"PlacePonds: kept {placed}/{keep} from {scored.Count} hollow candidates " +
                $"(castle exclusion r>{minCastleDist})");
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
            // F8 2026-07-02 flag_18 ("why is that mountain in scenery floating?"): the mesh path
            // hardcoded y=20 (a CUBE-pivot assumption — base flush at Y=0 for the 40-tall cube),
            // but the WORLDFEEL relief + biome fields mean the FINAL surface at (20,230) is NOT 0,
            // and the FBX pivot is not the cube's centre. Seat it from MEASURED renderer bounds on
            // the SAMPLED final surface (WorldHeightAt = the same chokepoint the heightmap uses),
            // bedded 2m in so the skirt melts into the ground.
            {
                float groundY = WorldHeightAt(20f, 230f);
                mountain.transform.position = new Vector3(20f, groundY, 230f);
                Bounds mb = default; bool haveMb = false;
                foreach (var mr in mountain.GetComponentsInChildren<Renderer>(true))
                {
                    if (mr == null) continue;
                    if (!haveMb) { mb = mr.bounds; haveMb = true; } else mb.Encapsulate(mr.bounds);
                }
                if (haveMb)
                    mountain.transform.position += Vector3.up * (groundY - mb.min.y - 2f);
                _notes.Add("DistantMountainPeak seated: ground(20,230)=" + groundY.ToString("0.0") +
                    (haveMb ? " (bounds-seated, base bedded 2m)" : " (no renderer bounds; pivot at ground)"));
            }

            // ── Western tower silhouette — RETIRED 2026-07-02 (owner F8 flag_24 "what is this?
            // big random cylinder"). The 7x26m primitive cylinder at (-228, -30) sits well INSIDE
            // the walkable 1000m terrain, so the owner met it up close as an unexplained prop, not
            // a horizon silhouette. Removed at the source so no rebuild re-adds it — same treatment
            // as the southern "Wound" crack (2026-06-21). If Mira's tower returns it should be a
            // real model seated on WorldHeightAt, beyond the playable edge.

            // ── Southern "Wound" crack — REMOVED 2026-06-21 (owner). It was a primitive cube (so it
            // carried a default BoxCollider) with a violet glow, reading as a purple slab/wall in the play
            // space rather than a far horizon scar. Owner deleted it; removed at the source so no rebuild
            // re-adds it. (If we ever want the lore scar back, do it renderer-only — strip the collider —
            // and far on the horizon.)
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

                // WORLDFEEL 2026-07-02: dusk "hold the last light" palette — warm amber
                // horizon, deep dusk-blue zenith, visible low sun. Matches the runtime
                // WorldFeelInjector values exactly so the baked scene and the runtime
                // pass agree (ff.worldfeel=0 falls back to THIS baked sky).
                sky.SetFloat("_SunSize", 0.05f);
                sky.SetFloat("_SunSizeConvergence", 4.5f);
                sky.SetFloat("_AtmosphereThickness", 1.25f);
                // Sky tint -- deep dusk-blue zenith.
                sky.SetColor("_SkyTint", new Color(0.42f, 0.50f, 0.72f));
                // Ground tint -- warm amber "last light" horizon.
                sky.SetColor("_GroundColor", new Color(0.86f, 0.62f, 0.44f));
                sky.SetFloat("_Exposure", 1.25f);
                EditorUtility.SetDirty(sky);

                RenderSettings.skybox = sky;
                // BUG-002 fix: with AmbientMode.Skybox (URP default) and no
                // baked lighting, URP/Terrain/Lit reads a zero ambient probe and
                // renders the exterior terrain black. Force reflection-mode to
                // Skybox and clear any stale custom reflection so the env probe
                // captures from this procedural skybox.
                RenderSettings.defaultReflectionMode =
                    UnityEngine.Rendering.DefaultReflectionMode.Skybox;
                RenderSettings.customReflectionTexture = null;
            }
            else
            {
                _notes.Add("Skybox/Procedural shader missing -- skybox left at scene default");
            }

            // ── Directional sun -- low dawn angle, slightly warm ────────────
            // Reuse the scene's existing directional light if the interior
            // builder made one; otherwise create a dawn sun.
            Light sun = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>())
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
            // WORLDFEEL: low warm dusk sun, long shadows (matches WorldFeelInjector).
            sun.transform.rotation = Quaternion.Euler(24f, -38f, 0f);
            sun.color = new Color(1.00f, 0.84f, 0.64f);
            sun.intensity = 1.15f;
            RenderSettings.sun = sun;

            // ── Ambient -- warm dusk trilight (matches WorldFeelInjector) ───
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.50f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.52f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.26f, 0.22f);

            // ── Atmospheric fog -- warm dusk haze (matches WorldFeelInjector) ─
            // Exponential-squared fog so distant terrain reads soft, near
            // terrain stays crisp. Tuned dense enough to soften the horizon
            // but light enough that the terrain is fully visible.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.78f, 0.66f, 0.58f); // warm dusk haze
            // §9.5 wants a denser-south / lighter-east gradient. Built-in fog
            // is uniform; we pick a mid density that suits the whole map and
            // leave the per-direction gradient as a volumetric follow-up note.
            RenderSettings.fogDensity = 0.0012f;
            _notes.Add("Fog is uniform exponential-squared (built-in); per-direction " +
                       "density gradient (denser south) deferred to a volumetric pass");

            // Halo / flare strength kept gentle for the dawn register.
            RenderSettings.haloStrength = 0.35f;
            RenderSettings.flareStrength = 0.7f;

            // BUG-002 fix: re-bake the ambient + reflection probes from the new
            // skybox material so URP/Terrain/Lit gets a non-zero sky ambient
            // contribution and the exterior terrain renders lit instead of
            // pitch black. Cheap; runs once per builder invocation.
            UnityEngine.DynamicGI.UpdateEnvironment();
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

        /// <summary>
        /// WO-173/DEF-108: returns a persisted URP TerrainLit material so the terrain
        /// SURFACE renders in the build. Without an explicit URP terrain material the
        /// Terrain falls back to a template that draws nothing under URP (the black
        /// void). Created once as an asset under Generated/Terrain so it is packaged
        /// into the player build; reused (shader re-pinned) on subsequent bakes.
        /// </summary>
        private static Material EnsureTerrainMaterial()
        {
            // URP terrain shader; fall back to the built-in terrain shader so a
            // non-URP project still bakes instead of throwing.
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                         ?? Shader.Find("Nature/Terrain/Standard");
            if (shader == null)
            {
                Debug.LogWarning("[ExteriorTerrainBuilder] No terrain shader found " +
                                 "(URP TerrainLit / built-in) -- terrain uses the engine default.");
                return null;
            }

            Material mat = File.Exists(TerrainMaterialPath)
                ? AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath)
                : null;

            if (mat == null)
            {
                mat = new Material(shader) { name = "ExteriorTerrainMaterial" };
                AssetDatabase.CreateAsset(mat, TerrainMaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        /// <summary>
        /// WO-173/DEF-108: pins the URP Terrain Lit shader into Graphics Settings'
        /// Always Included Shaders so it is NOT stripped from the player build. Symptom
        /// without this: terrain renders BLACK in the build (the shader is present in the
        /// editor but URP's scriptable stripping drops it from the player) while simpler
        /// objects render fine.
        /// </summary>
        [MenuItem("Defenders/World/Ensure Terrain Shader In Build")]
        public static void EnsureTerrainShaderIncluded()
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[ExteriorTerrainBuilder] URP Terrain Lit shader not found -- cannot pin it into the build.");
                return;
            }
            var so = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogWarning("[ExteriorTerrainBuilder] m_AlwaysIncludedShaders not found on GraphicsSettings.");
                return;
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    Debug.Log("[ExteriorTerrainBuilder] URP Terrain Lit already in Always Included Shaders.");
                    return;
                }
            }
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[ExteriorTerrainBuilder] Pinned URP Terrain Lit into Always Included Shaders (no longer stripped from builds).");
        }

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
