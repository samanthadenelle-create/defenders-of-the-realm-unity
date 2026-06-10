// =============================================================================
// GarrisonSceneBuilder.Scenes — the THREE themed enemy-garrison raid scenes.
// -----------------------------------------------------------------------------
// Run from: Defenders > Scenes > Build All Garrison Scenes
// Batchmode: DeNelle.Editor.GarrisonSceneBuilder.BuildAll  (via run-unity-method)
//
// Partial extension of the existing single-scene GarrisonSceneBuilder. Builds three
// distinct, additively-loadable garrison scenes from EXISTING assets (polyperfect
// Low Poly Ultimate Pack first, committed Resources/Structures prefabs as the
// guaranteed-present fallback, tinted primitives as the last resort):
//
//   * Garrison_TrollOutpost — messy wooden troll war camp (campfire + spit, crude
//     huts, spiked palisade, corner watchtowers, bone/crate scatter, wooden gate).
//   * Garrison_RuinedKeep   — overgrown stone ruins held by trolls (ruined keep,
//     wooden reinforcements, breached outer palisade, tents, ivy, underground hint).
//   * Garrison_HillFort     — organized elevated stronghold (raised platform, full
//     palisade, command hut + structures, multiple watchtowers, ramp choke, spikes).
//
// COMMON to all three: a GarrisonRoot (empty) with children EnemySpawnPoints
// (6-10 child Transforms), Environment (static geometry) and Props. A
// GarrisonController is added to the root via REFLECTION (DeNelle.Village is NOT
// referenced by the Editor asmdef — same pattern the single-scene builder uses for
// EnemyOutpost) and its spawnPoints[] wired to the EnemySpawnPoints children.
// Moody lighting (darker than the village, red/orange ambient, deep shadows).
// NavMesh baked, scene saved, added to EditorBuildSettings.
//
// All prefab loads are graceful: a missing pack prefab logs a WARNING (never an
// error) and the scene degrades (fallback prefab or tinted primitive). No .unity
// hand-edits. Batchmode-safe (no dialogs). Canon: the village is Elarion.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;   // NavMeshSurface / CollectObjects
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static partial class GarrisonSceneBuilder
    {
        // ------------------------------------------------------------------ paths
        private const string ScenesDir = "Assets/Scenes";
        private const string PolyPrefabRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";
        private const string ResStructRoot = "Assets/Resources/Structures/";

        // Theme identity for each of the three scenes.
        private enum GarrisonTheme { TrollOutpost, RuinedKeep, HillFort }

        // ===================================================================
        //  ENTRY POINTS
        // ===================================================================

        [MenuItem("Defenders/Scenes/Build All Garrison Scenes")]
        public static void BuildAll()
        {
            Log("=== Build ALL garrison scenes START ===");
            BuildThemed(GarrisonTheme.TrollOutpost, "Garrison_TrollOutpost");
            BuildThemed(GarrisonTheme.RuinedKeep,   "Garrison_RuinedKeep");
            BuildThemed(GarrisonTheme.HillFort,     "Garrison_HillFort");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("=== Build ALL garrison scenes DONE (3 scenes built + baked + saved + in Build Settings) ===");
        }

        [MenuItem("Defenders/Scenes/Build Garrison - Troll Outpost")]
        public static void BuildTrollOutpost() => BuildThemed(GarrisonTheme.TrollOutpost, "Garrison_TrollOutpost");

        [MenuItem("Defenders/Scenes/Build Garrison - Ruined Keep")]
        public static void BuildRuinedKeep() => BuildThemed(GarrisonTheme.RuinedKeep, "Garrison_RuinedKeep");

        [MenuItem("Defenders/Scenes/Build Garrison - Hill Fort")]
        public static void BuildHillFort() => BuildThemed(GarrisonTheme.HillFort, "Garrison_HillFort");

        // ===================================================================
        //  CORE — build one themed garrison scene end to end.
        // ===================================================================

        private static void BuildThemed(GarrisonTheme theme, string sceneName)
        {
            Log($"--- Building {sceneName} ({theme}) ---");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject(RootName);   // "GarrisonRoot" (shared const)
            root.transform.position = Vector3.zero;

            // Child groups per the common spec.
            var environment = new GameObject("Environment");
            environment.transform.SetParent(root.transform, false);
            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);
            var spawnGroup = new GameObject("EnemySpawnPoints");
            spawnGroup.transform.SetParent(root.transform, false);

            // Moody, theme-tinted lighting (darker than the village).
            BuildMoodyLighting(root.transform, theme);

            // Ground (raised earthen platform for the hill fort; flat dirt otherwise).
            BuildThemedGround(environment.transform, theme);

            // Hero entry + return seam (reuses the single-scene builder helpers).
            var entryPos = new Vector3(0f, 0.1f, -30f);
            var entry = new GameObject("HeroStartPoint_PlayerSpawn");
            entry.transform.SetParent(root.transform, false);
            entry.transform.position = entryPos;
            entry.transform.rotation = Quaternion.LookRotation((Vector3.zero - entryPos).normalized, Vector3.up);

            BuildReturnSeam(root.transform);   // from the single-scene partial

            // Theme geometry + props.
            switch (theme)
            {
                case GarrisonTheme.TrollOutpost: BuildTrollOutpostContent(environment.transform, props.transform); break;
                case GarrisonTheme.RuinedKeep:   BuildRuinedKeepContent(environment.transform, props.transform);   break;
                case GarrisonTheme.HillFort:     BuildHillFortContent(environment.transform, props.transform);     break;
            }

            // Spawn points (6-10), laid out for tower-defense waves around the centre.
            var spawnPoints = BuildSpawnPoints(spawnGroup.transform, theme);

            // GarrisonController on the root (reflection — DeNelle.Village not referenced).
            WireGarrisonController(root, spawnPoints, theme);

            // Bake nav (scene-specific asset path, so the 3 scenes don't collide) + save + register.
            BakeThemedNavMesh(root.transform, sceneName);
            string scenePath = $"{ScenesDir}/{sceneName}.unity";
            SaveThemedScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);    // from the single-scene partial

            Log($"--- {sceneName} built: {spawnPoints.Count} spawn points, GarrisonController wired, baked + saved. ---");
        }

        // ===================================================================
        //  LIGHTING — moody, theme-tinted, darker than the village.
        // ===================================================================
        private static void BuildMoodyLighting(Transform parent, GarrisonTheme theme)
        {
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);   // low, long shadows
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            // Per-theme tint: troll camp = warm firelit dusk; ruined keep = cold green
            // overgrowth gloom; hill fort = harsh organized amber.
            Color sunColor; Color ambient; Color fog; float sunI; float fogDensity;
            switch (theme)
            {
                case GarrisonTheme.RuinedKeep:
                    sunColor = new Color(0.62f, 0.70f, 0.62f); sunI = 0.75f;
                    ambient  = new Color(0.18f, 0.22f, 0.18f);
                    fog      = new Color(0.16f, 0.20f, 0.17f); fogDensity = 0.018f;
                    break;
                case GarrisonTheme.HillFort:
                    sunColor = new Color(0.95f, 0.74f, 0.52f); sunI = 0.95f;
                    ambient  = new Color(0.24f, 0.18f, 0.14f);
                    fog      = new Color(0.22f, 0.16f, 0.12f); fogDensity = 0.012f;
                    break;
                default: // TrollOutpost
                    sunColor = new Color(0.90f, 0.58f, 0.36f); sunI = 0.80f;
                    ambient  = new Color(0.22f, 0.15f, 0.12f);
                    fog      = new Color(0.20f, 0.13f, 0.10f); fogDensity = 0.015f;
                    break;
            }
            sun.color = sunColor;
            sun.intensity = sunI;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;

            Log($"Moody lighting set for {theme} (deep shadows, red/orange or cold-green ambient).");
        }

        // ===================================================================
        //  GROUND — flat dirt; the hill fort sits on a raised earthen platform.
        // ===================================================================
        private static void BuildThemedGround(Transform parent, GarrisonTheme theme)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GarrisonGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(9f, 1f, 9f);   // 90x90m
            MarkStatic(ground);
            TintMesh(ground, theme == GarrisonTheme.RuinedKeep
                ? new Color(0.24f, 0.27f, 0.20f)    // mossy
                : new Color(0.30f, 0.25f, 0.18f));  // dirt

            if (theme == GarrisonTheme.HillFort)
            {
                // Raised earthen/stone platform the fort sits on (a wide low cube). Its
                // MeshCollider feeds the navmesh; a ramp choke (built in content) reaches it.
                var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plat.name = "RaisedPlatform";
                plat.transform.SetParent(parent, false);
                plat.transform.localPosition = new Vector3(0f, 1.0f, 4f);
                plat.transform.localScale = new Vector3(34f, 2.0f, 30f);
                MarkStatic(plat);
                TintMesh(plat, new Color(0.34f, 0.29f, 0.22f));
            }
        }

        // ===================================================================
        //  THEME 1 — TROLL OUTPOST: messy wooden war camp.
        // ===================================================================
        private static void BuildTrollOutpostContent(Transform env, Transform props)
        {
            // Spiked wooden palisade enclosure (a square ring of wood walls, gap at front).
            BuildPalisadeRing(env, halfX: 16f, halfZ: 14f, y: 0f, frontGapForGate: true, wood: true);

            // Wooden gate at the front gap.
            PlaceOne(env, PickGate(wood: true), new Vector3(0f, 0f, -14f), 0f, "Gate_Wood");

            // 2 corner watchtowers (rear corners — overlook the camp).
            PlaceOne(env, PickWatchtower(), new Vector3(-15f, 0f, 12f), 0f, "Watchtower_NW");
            PlaceOne(env, PickWatchtower(), new Vector3( 15f, 0f, 12f), 0f, "Watchtower_NE");

            // 3-4 crude huts/tents scattered (small houses stand in for crude huts).
            PlaceOne(env, PickHut(), new Vector3(-9f, 0f,  6f),  20f, "Hut_1");
            PlaceOne(env, PickHut(), new Vector3( 9f, 0f,  6f), -25f, "Hut_2");
            PlaceOne(env, PickHut(), new Vector3(-6f, 0f, -4f),  40f, "Hut_3");
            PlaceOne(env, PickHut(), new Vector3( 7f, 0f, -3f), -15f, "Hut_4");

            // Central campfire + roasting spit (Fire prop + a crate stand-in for the spit).
            PlaceOne(props, PickCampfire(), new Vector3(0f, 0f, 1f), 0f, "Campfire_Central");
            PlaceOne(props, PickCrate(),    new Vector3(1.6f, 0f, 1f), 30f, "RoastingSpit_Stand");

            // Bone piles / broken crates / banners scatter (chaotic).
            ScatterProps(props, PickBones(), 4, seed: 11, area: 13f, label: "BonePile");
            ScatterProps(props, PickCrate(), 5, seed: 23, area: 14f, label: "BrokenCrate");
            ScatterProps(props, PickBanner(), 3, seed: 31, area: 12f, label: "TrollBanner");
            ScatterProps(props, PickSpikes(), 6, seed: 41, area: 15f, label: "Spikes");
            ScatterProps(props, PickTorch(),  4, seed: 53, area: 12f, label: "Torch");
        }

        // ===================================================================
        //  THEME 2 — RUINED KEEP: overgrown stone ruins held by trolls.
        // ===================================================================
        private static void BuildRuinedKeepContent(Transform env, Transform props)
        {
            // Central ruined keep: a stone tower core + partial broken stone walls.
            PlaceOne(env, PickStoneTower(), new Vector3(0f, 0f, 6f), 0f, "RuinedKeep_Core");

            // Partial outer stone walls with collapsed gaps (chokepoints).
            BuildBrokenStoneWalls(env, halfX: 17f, halfZ: 15f);

            // Breached outer wooden palisade (trolls' crude reinforcement, full of gaps).
            BuildPalisadeRing(env, halfX: 20f, halfZ: 18f, y: 0f, frontGapForGate: true, wood: true, breached: true);

            // Wooden troll reinforcements: spikes + crude towers braced against the ruins.
            PlaceOne(env, PickWatchtower(), new Vector3(-12f, 0f, 10f), 0f, "TrollTower_W");
            PlaceOne(env, PickWatchtower(), new Vector3( 12f, 0f, 10f), 0f, "TrollTower_E");

            // Tents inside the ruins (crude huts).
            PlaceOne(env, PickHut(), new Vector3(-7f, 0f, -2f), 30f, "Tent_1");
            PlaceOne(env, PickHut(), new Vector3( 7f, 0f, -3f), -20f, "Tent_2");

            // Hint of an underground entrance: a dark recessed slab + rubble at the keep base.
            var hatch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hatch.name = "UndergroundEntrance_Hint";
            hatch.transform.SetParent(env, false);
            hatch.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            hatch.transform.localScale = new Vector3(3.5f, 0.6f, 3.5f);
            MarkStatic(hatch);
            TintMesh(hatch, new Color(0.07f, 0.08f, 0.07f));

            // Ivy / overgrowth + rubble + bones scatter (the "held by trolls" decay).
            ScatterProps(props, PickVine(),  6, seed: 13, area: 16f, label: "Ivy");
            ScatterProps(props, PickRubble(), 7, seed: 27, area: 17f, label: "Rubble");
            ScatterProps(props, PickBones(),  4, seed: 37, area: 14f, label: "BonePile");
            ScatterProps(props, PickTorch(),  5, seed: 47, area: 14f, label: "Torch");
            PlaceOne(props, PickCampfire(), new Vector3(2f, 0f, -4f), 0f, "Campfire");
        }

        // ===================================================================
        //  THEME 3 — HILL FORT: organized elevated stronghold.
        // ===================================================================
        private static void BuildHillFortContent(Transform env, Transform props)
        {
            // Everything sits on the raised platform (y ~2.0 top). Full palisade ring.
            const float top = 2.0f;
            BuildPalisadeRing(env, halfX: 15f, halfZ: 13f, y: top, frontGapForGate: true, wood: true, centreZ: 4f);

            // Ramp/stairs choke up to the main gate (front, leading onto the platform).
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "EntryRamp_Choke";
            ramp.transform.SetParent(env, false);
            ramp.transform.localPosition = new Vector3(0f, top * 0.5f, -11f);
            ramp.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            ramp.transform.localScale = new Vector3(7f, 0.5f, 12f);   // narrow choke
            MarkStatic(ramp);
            TintMesh(ramp, new Color(0.32f, 0.27f, 0.20f));

            // Main gate at the top of the ramp.
            PlaceOne(env, PickGate(wood: true), new Vector3(0f, top, -9f), 0f, "MainGate");

            // Multiple watchtowers with line-of-sight (4 around the platform edge).
            PlaceOne(env, PickWatchtower(), new Vector3(-14f, top, -7f), 0f, "Watchtower_SW");
            PlaceOne(env, PickWatchtower(), new Vector3( 14f, top, -7f), 0f, "Watchtower_SE");
            PlaceOne(env, PickWatchtower(), new Vector3(-14f, top, 15f), 0f, "Watchtower_NW");
            PlaceOne(env, PickWatchtower(), new Vector3( 14f, top, 15f), 0f, "Watchtower_NE");

            // Central command hut + 3 support structures (organized).
            PlaceOne(env, PickHut(), new Vector3(0f, top, 6f),   0f, "CommandHut");
            PlaceOne(env, PickHut(), new Vector3(-8f, top, 10f), 25f, "Barracks_1");
            PlaceOne(env, PickHut(), new Vector3( 8f, top, 10f), -25f, "Barracks_2");
            PlaceOne(env, PickHut(), new Vector3(0f, top, 13f),  180f, "Storehouse");

            // Defensive spikes + oil barrels along the front line (organized defense).
            for (int i = -2; i <= 2; i++)
                PlaceOne(props, PickSpikes(), new Vector3(i * 3f, top, -8f), 0f, $"FrontSpike_{i}");
            PlaceOne(props, PickBarrel(), new Vector3(-3f, top, -6f), 0f, "OilBarrel_1");
            PlaceOne(props, PickBarrel(), new Vector3( 3f, top, -6f), 0f, "OilBarrel_2");
            ScatterProps(props, PickBanner(), 4, seed: 61, area: 12f, baseY: top, label: "WarBanner");
            ScatterProps(props, PickTorch(),  6, seed: 71, area: 13f, baseY: top, label: "Torch");
        }

        // ===================================================================
        //  PALISADE RING — a square ring of wall segments (wood or stone), with an
        //  optional front gap for the gate and optional "breached" missing segments.
        // ===================================================================
        private static void BuildPalisadeRing(Transform parent, float halfX, float halfZ, float y,
            bool frontGapForGate, bool wood, bool breached = false, float centreZ = 0f)
        {
            GameObject wallPrefab = wood ? PickWoodWall() : PickStoneWall();
            const float seg = 3f;   // segment width (pack walls snap at ~3m)

            // Front + back runs (along X).
            for (float x = -halfX; x <= halfX; x += seg)
            {
                bool frontGap = frontGapForGate && Mathf.Abs(x) <= seg * 0.6f;
                if (breached && Hash(x, 1) > 0.72f) continue;   // random breach gaps
                if (!frontGap)
                    PlaceOne(parent, wallPrefab, new Vector3(x, y, -halfZ + centreZ), 0f, "Wall_Front");
                if (breached && Hash(x, 2) > 0.72f) continue;
                PlaceOne(parent, wallPrefab, new Vector3(x, y, halfZ + centreZ), 0f, "Wall_Back");
            }
            // Side runs (along Z), rotated 90.
            for (float z = -halfZ; z <= halfZ; z += seg)
            {
                if (breached && Hash(z, 3) > 0.72f) continue;
                PlaceOne(parent, wallPrefab, new Vector3(-halfX, y, z + centreZ), 90f, "Wall_Left");
                if (breached && Hash(z, 4) > 0.72f) continue;
                PlaceOne(parent, wallPrefab, new Vector3( halfX, y, z + centreZ), 90f, "Wall_Right");
            }
        }

        // Partial broken stone walls for the ruined keep — short disconnected runs with
        // collapsed gaps that read as chokepoints.
        private static void BuildBrokenStoneWalls(Transform parent, float halfX, float halfZ)
        {
            GameObject stone = PickStoneWall();
            // A few broken segments per side (deliberate gaps between them).
            PlaceOne(parent, stone, new Vector3(-halfX, 0f, -4f), 90f, "BrokenWall_W1");
            PlaceOne(parent, stone, new Vector3(-halfX, 0f,  2f), 90f, "BrokenWall_W2");
            PlaceOne(parent, stone, new Vector3( halfX, 0f, -2f), 90f, "BrokenWall_E1");
            PlaceOne(parent, stone, new Vector3( halfX, 0f,  6f), 90f, "BrokenWall_E2");
            PlaceOne(parent, stone, new Vector3(-6f, 0f, halfZ),  0f, "BrokenWall_N1");
            PlaceOne(parent, stone, new Vector3( 5f, 0f, halfZ),  0f, "BrokenWall_N2");
        }

        // ===================================================================
        //  SPAWN POINTS — 6-10 child Transforms, laid out for TD waves around the
        //  centre, biased toward the front approach so defenders meet the hero.
        // ===================================================================
        private static List<Transform> BuildSpawnPoints(Transform group, GarrisonTheme theme)
        {
            float y = theme == GarrisonTheme.HillFort ? 2.0f : 0f;
            float centreZ = theme == GarrisonTheme.HillFort ? 4f
                          : theme == GarrisonTheme.RuinedKeep ? 2f : 1f;

            // 8 points: a defensive arc + a couple interior holders.
            var positions = new List<Vector3>
            {
                new Vector3( 0f, y, centreZ - 6f),   // front gate guard
                new Vector3(-5f, y, centreZ - 4f),
                new Vector3( 5f, y, centreZ - 4f),
                new Vector3(-8f, y, centreZ + 2f),
                new Vector3( 8f, y, centreZ + 2f),
                new Vector3( 0f, y, centreZ + 4f),   // centre boss-ish holder
                new Vector3(-6f, y, centreZ + 8f),
                new Vector3( 6f, y, centreZ + 8f),
            };

            var list = new List<Transform>();
            for (int i = 0; i < positions.Count; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(group, false);
                sp.transform.position = positions[i];
                sp.transform.rotation = Quaternion.LookRotation(
                    (new Vector3(0f, y, centreZ - 12f) - positions[i]).normalized, Vector3.up);
                list.Add(sp.transform);
            }
            return list;
        }

        // ===================================================================
        //  GARRISON CONTROLLER — added + wired via reflection (DeNelle.Village not
        //  referenced by the Editor asmdef; same pattern as the EnemyOutpost host).
        // ===================================================================
        private static void WireGarrisonController(GameObject root, List<Transform> spawnPoints, GarrisonTheme theme)
        {
            var type = FindType("DeNelle.Village.World.Camps.GarrisonController");
            if (type == null)
            {
                Warn("DeNelle.Village.World.Camps.GarrisonController not found (is it compiled?). " +
                     "Root placed WITHOUT the controller — add it + wire spawnPoints manually, or re-run after compile.");
                return;
            }

            var comp = root.AddComponent(type);

            // spawnPoints (Transform[]) — public field on the controller.
            var fSpawns = type.GetField("spawnPoints");
            if (fSpawns != null) fSpawns.SetValue(comp, spawnPoints.ToArray());
            else Warn("GarrisonController.spawnPoints field not found via reflection.");

            // threatLevel — a touch tougher for the organized hill fort.
            var fThreat = type.GetField("threatLevel");
            if (fThreat != null) fThreat.SetValue(comp, theme == GarrisonTheme.HillFort ? 3 : 2);

            Log($"GarrisonController wired ({spawnPoints.Count} spawn points, threat " +
                (theme == GarrisonTheme.HillFort ? 3 : 2) + "). It will Activate() on raid start.");
        }

        // ===================================================================
        //  PREFAB PICKERS — graceful resolution: try polyperfect (multiple candidate
        //  category folders), then the committed Resources/Structures set, then null
        //  (PlaceOne substitutes a tinted primitive). Every miss is a single WARNING.
        // ===================================================================
        private static GameObject PickWoodWall()   => Resolve("Wall_Medieval_Wood",  "Medieval_M", ResStructRoot + "Wall_Medieval_Wood.prefab");
        private static GameObject PickStoneWall()  => Resolve("Wall_Medieval_Stone", "Medieval_M", ResStructRoot + "Wall_Medieval_Stone.prefab");
        private static GameObject PickGate(bool wood) => Resolve("Gate_Medieval_Medium", "Medieval_M", ResStructRoot + "Gate_Medieval_Medium.prefab");
        private static GameObject PickWatchtower()  => Resolve("Tower_Medieval_Wood",  "Medieval_M", ResStructRoot + "Tower_Medieval_Wood.prefab");
        private static GameObject PickStoneTower()  => Resolve("Tower_Castle_Round",   "Medieval_M", ResStructRoot + "Tower_Castle_Round.prefab");
        private static GameObject PickHut()         => Resolve("House_Medieval_Small", "Medieval_M", ResStructRoot + "House_Medieval_Small.prefab");
        private static GameObject PickCampfire()    => Resolve("Fire",        "Survival_M", null);
        private static GameObject PickTorch()       => Resolve("Torche_Wall", "Fantasy_M", ResStructRoot + "Torche_Wall.prefab");
        private static GameObject PickCrate()       => Resolve("Crate_Box",   "Fantasy_M", null);
        private static GameObject PickBarrel()      => Resolve("Barrel",      "Survival_M", null);
        private static GameObject PickBanner()      => Resolve("Flag_Medieval", "Medieval_M", null);
        private static GameObject PickSpikes()      => Resolve("Stakes",      "Medieval_M", null);
        private static GameObject PickBones()       => Resolve("Skull_Human", "Fantasy_M", null);
        private static GameObject PickRubble()      => Resolve("Rubble_Stone", "Fantasy_M", null);
        private static GameObject PickVine()        => Resolve("Prop_Vine1",  "Nature_M", null);

        // Try the named prefab in the given polyperfect category, then a small set of
        // common alternative categories, then the explicit Resources fallback path.
        private static readonly string[] AltCategories =
            { "Medieval_M", "Fantasy_M", "Survival_M", "Props_M", "Nature_M", "Tools_M", "Farm_M" };

        private static GameObject Resolve(string prefabName, string primaryCategory, string resourcesFallback)
        {
            // 1) primary polyperfect category
            var go = LoadPoly(primaryCategory, prefabName);
            if (go != null) return go;

            // 2) alternative polyperfect categories (the catalog is occasionally off by folder)
            foreach (var cat in AltCategories)
            {
                if (cat == primaryCategory) continue;
                go = LoadPoly(cat, prefabName);
                if (go != null) return go;
            }

            // 3) committed Resources/Structures fallback (guaranteed present in repo)
            if (!string.IsNullOrEmpty(resourcesFallback))
            {
                go = AssetDatabase.LoadAssetAtPath<GameObject>(resourcesFallback);
                if (go != null) return go;
            }

            Warn($"Prefab '{prefabName}' not found in polyperfect ({primaryCategory} + alts) " +
                 (resourcesFallback != null ? $"or fallback '{resourcesFallback}' " : "") +
                 "- a tinted primitive will stand in (pack may not be imported).");
            return null;
        }

        private static GameObject LoadPoly(string category, string prefabName)
        {
            string path = $"{PolyPrefabRoot}{category}/{prefabName}.prefab";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ===================================================================
        //  PLACEMENT helpers.
        // ===================================================================

        // Instantiate a prefab (or a tinted-cube fallback) at a local pose under parent.
        private static GameObject PlaceOne(Transform parent, GameObject prefab, Vector3 localPos, float yaw, string name)
        {
            GameObject inst;
            if (prefab != null)
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(parent, false);
            }
            else
            {
                // Tinted-cube fallback so the scene still reads + bakes navmesh around it.
                inst = GameObject.CreatePrimitive(PrimitiveType.Cube);
                inst.transform.SetParent(parent, false);
                inst.transform.localScale = new Vector3(2f, 2.5f, 2f);
                TintMesh(inst, new Color(0.35f, 0.22f, 0.16f));
            }
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.name = name;
            MarkStatic(inst);
            return inst;
        }

        // Deterministically scatter N copies of a prop within a square area.
        private static void ScatterProps(Transform parent, GameObject prefab, int count, int seed,
            float area, string label, float baseY = 0f)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * area;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * area;
                float yaw = (float)(rng.NextDouble() * 360.0);
                PlaceOne(parent, prefab, new Vector3(x, baseY, z), yaw, $"{label}_{i}");
            }
        }

        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void TintMesh(GameObject go, Color c)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            mr.sharedMaterial = mat;
        }

        // Cheap deterministic 0..1 hash for breach-gap decisions.
        private static float Hash(float v, int salt)
        {
            float h = Mathf.Sin(v * 12.9898f + salt * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ===================================================================
        //  NAVMESH — bake the root's NavMeshSurface and persist the data to a
        //  PER-SCENE asset path (so the three themed scenes don't overwrite one
        //  shared nav asset). Use Geometry = Physics Colliders + Collect = All so
        //  the ground plane + platform + wall MeshColliders are the source.
        // ===================================================================
        private static void BakeThemedNavMesh(Transform parent, string sceneName)
        {
            var surface = parent.GetComponent<NavMeshSurface>();
            if (surface == null) surface = parent.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();

            var data = surface.navMeshData;
            if (data == null)
            {
                Warn($"{sceneName}: navMeshData null after bake — verify ground/platform MeshColliders.");
                return;
            }

            string dir = $"{ScenesDir}/{sceneName}";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(ScenesDir, sceneName);
            string assetPath = $"{dir}/NavMesh-{sceneName}.asset";
            if (!AssetDatabase.Contains(data))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (existing != null) AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(data, assetPath);
                Log($"{sceneName}: navmesh asset written -> {assetPath}");
            }
        }

        // ===================================================================
        //  SAVE — themed scenes go straight to their named path.
        // ===================================================================
        private static void SaveThemedScene(Scene scene, string scenePath)
        {
            if (!AssetDatabase.IsValidFolder(ScenesDir))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            if (ok) Log("Saved scene -> " + scenePath);
            else Err("SaveScene FAILED for " + scenePath);
        }
    }
}
