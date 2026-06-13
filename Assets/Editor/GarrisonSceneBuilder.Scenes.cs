// =============================================================================
// GarrisonSceneBuilder.Scenes — the GENERIC recipe->scene realization + the
// CATALOG-FIRST prop-role map. Partial of GarrisonSceneBuilder.
// -----------------------------------------------------------------------------
// One BuildFromRecipe() builds ANY recipe (garrison fort OR dungeon cave). There
// are NO per-theme methods — theme/size/props/enemies/levelRange/element are all
// DATA read off the GarrisonRecipe. To add a fort/cave/region: add a JSON line.
//
// PROP-ROLE MAP: a recipe lists ROLES ("wall_wood", "watchtower", "stalagmite",
// "ice_floe", "dungeon_pillar"...). ResolveRole() maps each role to a real catalog
// prefab on disk — polyperfect Low Poly Ultimate Pack first, committed
// Resources/Structures next, then null => a tinted-primitive stand-in. Every miss
// is one LogWarning (CLAUDE.md §4). The map is the single source of truth for
// prop-role -> prefab; extend the map (not the build flow) for new roles.
//
// Batchmode-safe. ASCII strings. Canon: the village is Elarion.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.World;   // GarrisonRecipe

namespace DeNelle.Editor
{
    public static partial class GarrisonSceneBuilder
    {
        // ------------------------------------------------------------------ paths
        private const string ScenesDir = "Assets/Scenes";
        private const string PolyPrefabRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";
        private const string ResStructRoot = "Assets/Resources/Structures/";

        // OUTPOST-DRESSING PASS (2026-06-13): the owner's Tribal_T set lives in the
        // _T quality tier (NOT _M), so it has its own root. polyperfect is GITIGNORED
        // (CLAUDE.md §4) — an editor builder CAN still load it by ASSET PATH via
        // AssetDatabase.LoadAssetAtPath (Resources.Load could NOT, the pack is not under
        // a Resources folder). Every miss is a single LogWarning, never an error, so a
        // fresh clone without the pack degrades the scene rather than breaking the bake.
        private const string TribalPrefabRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_T/Prefabs_T/Tribal_T/";

        // Where the hero lands back in OuterWorld after a raid (shared with the castle seam).
        // SOUTH-GATE SEAM FIX (2026-06-13): was (0,0.5,-80) — the OuterWorld MIDPOINT/south-region
        // depth (Mirewood @ (0,0,-90)), which "yanked the hero to the middle of OuterWorld" (owner).
        // OuterWorld's four regions fan out from world ORIGIN (300x300 terrain centered on 0), so
        // land at the near-origin hub (just south of 0, facing into the world) to match the castle
        // seam (CastleHubBuilder EnsureExitSeamAtRecipeGate / WireOuterWorldConnection).
        private static readonly Vector3 OuterWorldReturnPos = new Vector3(0f, 0.5f, -12f);

        // ===================================================================
        //  GENERIC RECIPE -> SCENE. Builds Garrison_<id>.unity end to end.
        // ===================================================================
        private static void BuildFromRecipe(GarrisonRecipe recipe)
        {
            string sceneName = recipe.SceneName;
            Log($"--- Building {sceneName} (kind={recipe.Kind}, theme={recipe.LightingToken}, " +
                $"size={recipe.Size}, levels {recipe.MinLevel}-{recipe.MaxLevel}) ---");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            var environment = new GameObject("Environment");
            environment.transform.SetParent(root.transform, false);
            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);
            var spawnGroup = new GameObject("EnemySpawnPoints");
            spawnGroup.transform.SetParent(root.transform, false);

            // Footprint from size — drives the enclosure half-extents + spawn count.
            float half = HalfExtentFor(recipe.Size);   // square half-width in metres
            bool dungeon = recipe.IsDungeon;

            // Moody lighting tinted by the recipe's lighting/theme/element token.
            BuildMoodyLighting(root.transform, recipe);

            // Ground / floor (a dungeon gets a tinted stone floor + perimeter walls; a
            // garrison gets open earthen ground). Both feed the navmesh via MeshColliders.
            BuildGroundOrFloor(environment.transform, recipe, half, dungeon);

            // Hero entry + a return seam back to OuterWorld (single-load, generous radius).
            var entryPos = new Vector3(0f, dungeon ? 0.1f : 0.1f, -(half + 6f));
            var entry = new GameObject("HeroStartPoint_PlayerSpawn");
            entry.transform.SetParent(root.transform, false);
            entry.transform.position = entryPos;
            entry.transform.rotation = Quaternion.LookRotation((Vector3.zero - entryPos).normalized, Vector3.up);
            BuildReturnSeam(root.transform, half);

            // The PROPS — the heart of the catalog-first system. Each role string in
            // recipe.Props is resolved to a real prefab + placed by a layout rule.
            PlaceRecipeProps(environment.transform, props.transform, recipe, half, dungeon);

            // OUTPOST-DRESSING PASS (2026-06-13): the three open-air garrison outposts
            // (troll_outpost / ruined_keep / frost_keep) read as empty boxes. Dress each
            // with a handful of the owner's Tribal_T props (watchtowers at the palisade
            // corners, a gate-flank torch pair, an interior camp cluster) so the fort
            // reads as an INHABITED CAMP. Dungeons keep their stone-cave dressing.
            if (!dungeon)
                DressWithTribalCamp(props.transform, half);

            // Spawn points (6-10), scaled by footprint, biased to the front approach.
            var spawnPoints = BuildSpawnPoints(spawnGroup.transform, half, dungeon);

            // GarrisonController wired to the recipe's enemies + levelRange + threat (reflection).
            WireGarrisonController(root, spawnPoints, recipe);

            // Bake + save + register.
            BakeNavMesh(root.transform, sceneName);
            string scenePath = $"{ScenesDir}/{sceneName}.unity";
            SaveSceneTo(scene, scenePath);
            AddSceneToBuildSettings(scenePath);

            Log($"--- {sceneName} built: {spawnPoints.Count} spawn points, " +
                $"{(recipe.Props != null ? recipe.Props.Count : 0)} prop role(s), " +
                $"enemies [{string.Join(",", recipe.EnemyIds)}], baked + saved. ---");
        }

        private static float HalfExtentFor(string size)
        {
            switch ((size ?? "medium").ToLowerInvariant())
            {
                case "small":  return 12f;
                case "large":  return 20f;
                default:       return 16f;   // medium
            }
        }

        // ===================================================================
        //  LIGHTING — moody, tinted by lighting/theme/element token. Unknown tokens
        //  fall to a neutral dusk so a NEW theme name never breaks the bake.
        // ===================================================================
        private static void BuildMoodyLighting(Transform parent, GarrisonRecipe recipe)
        {
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);   // low, long shadows
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            Color sunColor, ambient, fog; float sunI, fogDensity;
            string token = (recipe.LightingToken ?? "troll").ToLowerInvariant();
            string element = (recipe.Element ?? "").ToLowerInvariant();

            // Element wins over theme for a dungeon's mood (ice => cold blue, fire => ember).
            if (element == "ice" || token == "frost")
            {
                sunColor = new Color(0.70f, 0.82f, 0.95f); sunI = 0.70f;
                ambient  = new Color(0.20f, 0.26f, 0.34f);
                fog      = new Color(0.30f, 0.38f, 0.48f); fogDensity = 0.022f;
            }
            else if (element == "fire" || token == "ember")
            {
                sunColor = new Color(0.98f, 0.55f, 0.30f); sunI = 0.85f;
                ambient  = new Color(0.26f, 0.13f, 0.08f);
                fog      = new Color(0.28f, 0.12f, 0.07f); fogDensity = 0.018f;
            }
            else if (element == "water")
            {
                sunColor = new Color(0.62f, 0.78f, 0.80f); sunI = 0.72f;
                ambient  = new Color(0.16f, 0.24f, 0.26f);
                fog      = new Color(0.18f, 0.28f, 0.30f); fogDensity = 0.020f;
            }
            else if (token == "ruined")
            {
                sunColor = new Color(0.62f, 0.70f, 0.62f); sunI = 0.75f;
                ambient  = new Color(0.18f, 0.22f, 0.18f);
                fog      = new Color(0.16f, 0.20f, 0.17f); fogDensity = 0.018f;
            }
            else if (token == "hill")
            {
                sunColor = new Color(0.95f, 0.74f, 0.52f); sunI = 0.95f;
                ambient  = new Color(0.24f, 0.18f, 0.14f);
                fog      = new Color(0.22f, 0.16f, 0.12f); fogDensity = 0.012f;
            }
            else if (token == "troll")
            {
                sunColor = new Color(0.90f, 0.58f, 0.36f); sunI = 0.80f;
                ambient  = new Color(0.22f, 0.15f, 0.12f);
                fog      = new Color(0.20f, 0.13f, 0.10f); fogDensity = 0.015f;
            }
            else
            {
                // Unknown theme => neutral dusk (never breaks the bake).
                Warn($"Unknown lighting/theme token '{token}' — using a neutral dusk.");
                sunColor = new Color(0.85f, 0.80f, 0.72f); sunI = 0.85f;
                ambient  = new Color(0.22f, 0.22f, 0.22f);
                fog      = new Color(0.20f, 0.20f, 0.20f); fogDensity = 0.012f;
            }

            sun.color = sunColor; sun.intensity = sunI;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = recipe.IsDungeon ? fogDensity * 1.4f : fogDensity;

            Log($"Moody lighting set (token={token}, element={(string.IsNullOrEmpty(element) ? "none" : element)}).");
        }

        // ===================================================================
        //  GROUND / FLOOR. Garrison: open earthen plane. Dungeon: a tinted stone
        //  floor plane + a solid perimeter wall ring (enclosure) so it reads as a cave.
        // ===================================================================
        private static void BuildGroundOrFloor(Transform parent, GarrisonRecipe recipe, float half, bool dungeon)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = dungeon ? "DungeonFloor" : "GarrisonGround";
            ground.transform.SetParent(parent, false);
            float planeScale = (half + 8f) / 5f;            // Unity plane is 10m => /5 per half
            ground.transform.localScale = new Vector3(planeScale, 1f, planeScale);
            MarkStatic(ground);

            string token = (recipe.LightingToken ?? "troll").ToLowerInvariant();
            Color groundTint =
                dungeon                 ? new Color(0.20f, 0.21f, 0.24f) :  // stone
                token == "ruined"       ? new Color(0.24f, 0.27f, 0.20f) :  // mossy
                token == "frost"        ? new Color(0.55f, 0.62f, 0.70f) :  // snow
                                          new Color(0.30f, 0.25f, 0.18f);   // dirt
            TintMesh(ground, groundTint);

            if (dungeon)
            {
                // A solid perimeter wall ring (cubes) so the cave is enclosed + bakes a
                // bounded navmesh. The detailed dungeon_wall props decorate on top.
                BuildEnclosureRing(parent, half + 2f);
            }
        }

        // A simple solid wall ring (4 long cubes) enclosing a dungeon footprint.
        private static void BuildEnclosureRing(Transform parent, float half)
        {
            float t = 1.0f, h = 4.0f, len = half * 2f + 2f;
            var specs = new (Vector3 pos, Vector3 scale)[]
            {
                (new Vector3(0f, h * 0.5f, -half), new Vector3(len, h, t)),
                (new Vector3(0f, h * 0.5f,  half), new Vector3(len, h, t)),
                (new Vector3(-half, h * 0.5f, 0f), new Vector3(t, h, len)),
                (new Vector3( half, h * 0.5f, 0f), new Vector3(t, h, len)),
            };
            foreach (var s in specs)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = "DungeonEnclosure";
                w.transform.SetParent(parent, false);
                w.transform.localPosition = s.pos;
                w.transform.localScale = s.scale;
                MarkStatic(w);
                TintMesh(w, new Color(0.16f, 0.16f, 0.19f));
            }
        }

        // ===================================================================
        //  PROP PLACEMENT — the recipe-first heart. Walk recipe.Props (a multiset of
        //  ROLE strings), bucket them, and place by simple layout rules: enclosure
        //  roles ring the footprint, structures sit inside, scatter props sprinkle.
        //  Repeated roles (e.g. four "watchtower") place at distinct ring slots.
        // ===================================================================
        private static void PlaceRecipeProps(Transform env, Transform props, GarrisonRecipe recipe,
            float half, bool dungeon)
        {
            var roles = (recipe.Props != null && recipe.Props.Count > 0)
                ? recipe.Props
                : DefaultRolesFor(recipe, dungeon);

            // Bucket the role multiset.
            int watchtowers = 0, huts = 0;
            bool wallWood = false, wallStone = false, wallWoodBreached = false;
            bool gate = false, ramp = false, platform = false, stoneTower = false;
            bool dungeonFloor = false, dungeonWall = false;
            int dungeonPillars = 0;
            bool dungeonDoor = false, altar = false;
            // scatter roles -> count
            var scatter = new Dictionary<string, int>();

            foreach (var raw in roles)
            {
                string role = (raw ?? "").Trim().ToLowerInvariant();
                switch (role)
                {
                    case "wall_wood":          wallWood = true; break;
                    case "wall_wood_breached": wallWoodBreached = true; break;
                    case "wall_stone":         wallStone = true; break;
                    case "gate_wood":
                    case "gate":               gate = true; break;
                    case "ramp":               ramp = true; break;
                    case "platform":           platform = true; break;
                    case "stone_tower":        stoneTower = true; break;
                    case "watchtower":         watchtowers++; break;
                    case "hut":                huts++; break;
                    case "dungeon_floor":      dungeonFloor = true; break;
                    case "dungeon_wall":       dungeonWall = true; break;
                    case "dungeon_pillar":     dungeonPillars++; break;
                    case "dungeon_door":       dungeonDoor = true; break;
                    case "altar":              altar = true; break;
                    default:
                        // everything else is a scatter prop (campfire/crate/bones/spikes/
                        // torch/banner/barrel/rubble/ivy/stalagmite/ice_floe/icicle/crystals)
                        if (!scatter.ContainsKey(role)) scatter[role] = 0;
                        scatter[role]++;
                        break;
                }
            }

            // Optional raised platform (hill-fort style) — a wide low cube the fort sits on.
            float top = 0f;
            if (platform)
            {
                top = 2.0f;
                var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plat.name = "RaisedPlatform";
                plat.transform.SetParent(env, false);
                plat.transform.localPosition = new Vector3(0f, 1.0f, 2f);
                plat.transform.localScale = new Vector3(half * 1.9f, 2.0f, half * 1.7f);
                MarkStatic(plat);
                TintMesh(plat, new Color(0.34f, 0.29f, 0.22f));
            }

            // Enclosure: wood/stone palisade ring (front gap for the gate). Breached =>
            // random missing segments (chokepoints).
            if (wallWood || wallWoodBreached)
                BuildPalisadeRing(env, half, top, wood: true, breached: wallWoodBreached, frontGap: gate);
            if (wallStone)
                BuildBrokenStoneWalls(env, half);
            if (dungeonWall)
                BuildDungeonWalls(env, half);

            // Gate at the front gap.
            if (gate)
                PlaceOne(env, ResolveRole("gate_wood"), new Vector3(0f, top, -half), 0f, "Gate");

            // Ramp choke up to a platform.
            if (ramp && platform)
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
                r.name = "EntryRamp_Choke";
                r.transform.SetParent(env, false);
                r.transform.localPosition = new Vector3(0f, top * 0.5f, -(half + 1f));
                r.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
                r.transform.localScale = new Vector3(7f, 0.5f, 12f);
                MarkStatic(r);
                TintMesh(r, new Color(0.32f, 0.27f, 0.20f));
            }

            // Central stone tower / keep core.
            if (stoneTower)
                PlaceOne(env, ResolveRole("stone_tower"), new Vector3(0f, top, half * 0.35f), 0f, "Keep_Core");

            // Watchtowers ring the rear corners/edges (distinct slots per count).
            PlaceRing(env, "watchtower", watchtowers, half * 0.92f, top, startAngle: 35f, label: "Watchtower");

            // Huts cluster inside.
            PlaceCluster(env, "hut", huts, half * 0.45f, top, label: "Hut");

            // Dungeon detail: floor tiles accent + pillars in a grid + a door + altar.
            if (dungeonFloor)
                ScatterRole(props, "dungeon_floor", 6, seed: 7, area: half * 0.8f, baseY: 0.02f, label: "FloorTile");
            if (dungeonPillars > 0)
                PlaceRing(env, "dungeon_pillar", dungeonPillars, half * 0.6f, 0f, startAngle: 0f, label: "Pillar");
            if (dungeonDoor)
                PlaceOne(env, ResolveRole("dungeon_door"), new Vector3(0f, 0f, -half), 0f, "DungeonDoor");
            if (altar)
                PlaceOne(env, ResolveRole("altar"), new Vector3(0f, 0f, half * 0.4f), 0f, "Altar");

            // Scatter props — deterministic sprinkle per role, count from the multiset
            // (so listing "bones" twice scatters more). 3 instances per listing.
            int seed = 101;
            foreach (var kv in scatter)
            {
                int instances = Mathf.Clamp(kv.Value * 3, 3, 9);
                ScatterRole(props, kv.Key, instances, seed, area: half * 0.85f, baseY: top, label: kv.Key);
                seed += 17;
            }
        }

        // A default role-set when a recipe omits props[] entirely.
        private static List<string> DefaultRolesFor(GarrisonRecipe recipe, bool dungeon)
        {
            if (dungeon)
                return new List<string> { "dungeon_floor", "dungeon_wall", "dungeon_pillar",
                                          "dungeon_door", "torch", "bones" };
            return new List<string> { "wall_wood", "gate_wood", "watchtower", "watchtower",
                                      "hut", "hut", "campfire", "crate", "torch" };
        }

        // ===================================================================
        //  PALISADE / WALL helpers.
        // ===================================================================
        private static void BuildPalisadeRing(Transform parent, float half, float y,
            bool wood, bool breached, bool frontGap)
        {
            GameObject wallPrefab = wood ? ResolveRole("wall_wood") : ResolveRole("wall_stone");
            const float seg = 3f;
            float halfX = half, halfZ = half;

            for (float x = -halfX; x <= halfX; x += seg)
            {
                bool atGate = frontGap && Mathf.Abs(x) <= seg * 0.6f;
                if (!(breached && Hash(x, 1) > 0.72f) && !atGate)
                    PlaceOne(parent, wallPrefab, new Vector3(x, y, -halfZ), 0f, "Wall_Front");
                if (!(breached && Hash(x, 2) > 0.72f))
                    PlaceOne(parent, wallPrefab, new Vector3(x, y, halfZ), 0f, "Wall_Back");
            }
            for (float z = -halfZ; z <= halfZ; z += seg)
            {
                if (!(breached && Hash(z, 3) > 0.72f))
                    PlaceOne(parent, wallPrefab, new Vector3(-halfX, y, z), 90f, "Wall_Left");
                if (!(breached && Hash(z, 4) > 0.72f))
                    PlaceOne(parent, wallPrefab, new Vector3( halfX, y, z), 90f, "Wall_Right");
            }
        }

        // Partial broken stone walls — short disconnected runs (chokepoints) for a ruined keep.
        private static void BuildBrokenStoneWalls(Transform parent, float half)
        {
            GameObject stone = ResolveRole("wall_stone");
            PlaceOne(parent, stone, new Vector3(-half, 0f, -4f), 90f, "BrokenWall_W1");
            PlaceOne(parent, stone, new Vector3(-half, 0f,  2f), 90f, "BrokenWall_W2");
            PlaceOne(parent, stone, new Vector3( half, 0f, -2f), 90f, "BrokenWall_E1");
            PlaceOne(parent, stone, new Vector3( half, 0f,  6f), 90f, "BrokenWall_E2");
            PlaceOne(parent, stone, new Vector3(-6f, 0f, half),  0f, "BrokenWall_N1");
            PlaceOne(parent, stone, new Vector3( 5f, 0f, half),  0f, "BrokenWall_N2");
        }

        // Decorative dungeon wall prefabs lining the enclosure (the solid ring is the collider).
        private static void BuildDungeonWalls(Transform parent, float half)
        {
            GameObject w = ResolveRole("dungeon_wall");
            const float seg = 3f;
            for (float x = -half; x <= half; x += seg)
            {
                PlaceOne(parent, w, new Vector3(x, 0f, -half), 0f, "DWall_S");
                PlaceOne(parent, w, new Vector3(x, 0f,  half), 0f, "DWall_N");
            }
            for (float z = -half; z <= half; z += seg)
            {
                PlaceOne(parent, w, new Vector3(-half, 0f, z), 90f, "DWall_W");
                PlaceOne(parent, w, new Vector3( half, 0f, z), 90f, "DWall_E");
            }
        }

        // Place N copies of a role evenly around a ring of the given radius.
        private static void PlaceRing(Transform parent, string role, int count, float radius,
            float y, float startAngle, string label)
        {
            if (count <= 0) return;
            GameObject prefab = ResolveRole(role);
            for (int i = 0; i < count; i++)
            {
                float ang = (startAngle + i * (360f / count)) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(ang) * radius, y, Mathf.Cos(ang) * radius);
                float yaw = -startAngle - i * (360f / count);   // face inward-ish
                PlaceOne(parent, prefab, pos, yaw, $"{label}_{i}");
            }
        }

        // Place N copies of a role in a small interior cluster (deterministic).
        private static void PlaceCluster(Transform parent, string role, int count, float radius,
            float y, string label)
        {
            if (count <= 0) return;
            GameObject prefab = ResolveRole(role);
            var rng = new System.Random(role.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                float a = (float)(rng.NextDouble() * 2.0 * Mathf.PI);
                float rr = radius * (0.4f + 0.6f * (float)rng.NextDouble());
                var pos = new Vector3(Mathf.Sin(a) * rr, y, Mathf.Cos(a) * rr + radius * 0.3f);
                PlaceOne(parent, prefab, pos, (float)(rng.NextDouble() * 360.0), $"{label}_{i}");
            }
        }

        // ===================================================================
        //  SPAWN POINTS — 6-10 child Transforms, biased to the front approach so
        //  defenders meet the hero. Count scales a touch with footprint.
        // ===================================================================
        private static List<Transform> BuildSpawnPoints(Transform group, float half, bool dungeon)
        {
            float y = 0f;
            float f = half / 16f;   // footprint factor (1.0 at medium)
            var positions = new List<Vector3>
            {
                new Vector3( 0f, y, -6f * f),   // front guard
                new Vector3(-5f * f, y, -4f * f),
                new Vector3( 5f * f, y, -4f * f),
                new Vector3(-8f * f, y,  2f * f),
                new Vector3( 8f * f, y,  2f * f),
                new Vector3( 0f, y,  4f * f),    // centre holder
                new Vector3(-6f * f, y,  8f * f),
                new Vector3( 6f * f, y,  8f * f),
            };
            if (half >= 20f)   // large => a couple more
            {
                positions.Add(new Vector3(0f, y, 11f * f));
                positions.Add(new Vector3(0f, y, -10f * f));
            }

            var list = new List<Transform>();
            for (int i = 0; i < positions.Count; i++)
            {
                var sp = new GameObject($"Spawn_{i}");
                sp.transform.SetParent(group, false);
                sp.transform.position = positions[i];
                sp.transform.rotation = Quaternion.LookRotation(
                    (new Vector3(0f, y, -(half + 4f)) - positions[i]).normalized, Vector3.up);
                list.Add(sp.transform);
            }
            return list;
        }

        // ===================================================================
        //  GARRISON CONTROLLER — added + wired via reflection (DeNelle.Village not
        //  referenced by the Editor asmdef). Wires spawnPoints + enemyTypeIds +
        //  min/maxLevel + threatLevel straight off the recipe.
        // ===================================================================
        private static void WireGarrisonController(GameObject root, List<Transform> spawnPoints, GarrisonRecipe recipe)
        {
            var type = FindType("DeNelle.Village.World.Camps.GarrisonController");
            if (type == null)
            {
                Warn("DeNelle.Village.World.Camps.GarrisonController not found (is it compiled?). " +
                     "Root placed WITHOUT the controller — re-run after compile.");
                return;
            }

            var comp = root.AddComponent(type);

            var fSpawns = type.GetField("spawnPoints");
            if (fSpawns != null) fSpawns.SetValue(comp, spawnPoints.ToArray());
            else Warn("GarrisonController.spawnPoints field not found via reflection.");

            // enemyTypeIds (string[]) straight from the recipe.
            var ids = new List<string>(recipe.EnemyIds);
            SetField(type, comp, "enemyTypeIds", ids.ToArray());

            // levelRange band + threat.
            SetField(type, comp, "minLevel",    recipe.MinLevel);
            SetField(type, comp, "maxLevel",    recipe.MaxLevel);
            SetField(type, comp, "threatLevel", recipe.Threat);

            Log($"GarrisonController wired: {spawnPoints.Count} spawn points, enemies " +
                $"[{string.Join(",", recipe.EnemyIds)}], levels {recipe.MinLevel}-{recipe.MaxLevel}, threat {recipe.Threat}.");
        }

        // ===================================================================
        //  RETURN seam — SceneTransitionTrigger back to OuterWorld (single load,
        //  generous proximity radius). Added via reflection (DeNelle.Village type).
        // ===================================================================
        private static void BuildReturnSeam(Transform parent, float half)
        {
            var seamGo = new GameObject("ReturnToOuterWorld_Seam");
            seamGo.transform.SetParent(parent, false);
            seamGo.transform.position = new Vector3(0f, 1.5f, -(half + 8f));

            var box = seamGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(20f, 6f, 10f);

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType == null)
            {
                Warn("DeNelle.Village.SceneTransitionTrigger not found — return trigger collider " +
                     "added WITHOUT the behaviour. Re-run after compile.");
                return;
            }

            var comp = seamGo.AddComponent(transType);
            SetField(transType, comp, "targetSceneName", "OuterWorld");
            SetField(transType, comp, "targetPosition", OuterWorldReturnPos);
            SetField(transType, comp, "loadAdditive", false);
            SetField(transType, comp, "ProximityRadius", 16f);
            Log("Return seam wired: SceneTransitionTrigger -> OuterWorld (single load), proximity 16m.");
        }

        // ===================================================================
        //  OUTPOST-DRESSING PASS (2026-06-13) — Tribal_T camp dressing.
        //  Places a TASTEFUL handful (not clutter) of the owner's Tribal_T props so an
        //  open-air garrison outpost reads as an inhabited camp:
        //    - 4 Tribal watchtowers at the palisade corners (tier varies a touch)
        //    - a torch pair flanking the front gate
        //    - an interior camp cluster (furnace/cooking pot/totem/bone pile/vases)
        //  Loaded by ASSET PATH (AssetDatabase) because polyperfect is gitignored and
        //  lives outside any Resources folder. Each prefab is null-guarded; a missing
        //  one is a single LogWarning (pack not imported) and is simply skipped.
        // ===================================================================
        private static void DressWithTribalCamp(Transform props, float half)
        {
            var camp = new GameObject("TribalCampDressing");
            camp.transform.SetParent(props, false);
            Transform c = camp.transform;

            int placed = 0;

            // --- 4 corner watchtowers (just inside the palisade corners) ---
            float r = half * 0.86f;
            var corners = new (Vector3 pos, string tower)[]
            {
                (new Vector3(-r, 0f, -r), "Tower_Tribal_Tier1"),
                (new Vector3( r, 0f, -r), "Tower_Tribal_Tier2"),
                (new Vector3(-r, 0f,  r), "Tower_Tribal_Tier3"),
                (new Vector3( r, 0f,  r), "Tower_Tribal_Tier2"),
            };
            foreach (var corner in corners)
            {
                // face the tower roughly toward the camp centre
                float yaw = Quaternion.LookRotation((Vector3.zero - corner.pos).normalized, Vector3.up).eulerAngles.y;
                if (PlaceTribal(c, corner.tower, corner.pos, yaw, $"Watchtower_{corner.tower}")) placed++;
            }

            // --- torch pair flanking the front gate (front edge = -Z) ---
            if (PlaceTribal(c, "Torch_Standing_Tribal", new Vector3(-2.5f, 0f, -half + 0.5f), 0f, "GateTorch_L")) placed++;
            if (PlaceTribal(c, "Torch_Standing_Tribal", new Vector3( 2.5f, 0f, -half + 0.5f), 0f, "GateTorch_R")) placed++;

            // --- interior camp cluster (a small inhabited hearth) ---
            if (PlaceTribal(c, "Furnace_Tribal",       new Vector3(-3f, 0f,  2f),   25f, "Camp_Furnace")) placed++;
            if (PlaceTribal(c, "Cookingpot_Tribal",    new Vector3(-0.5f, 0f, 3.5f), 0f, "Camp_CookingPot")) placed++;
            if (PlaceTribal(c, "Totem_Dinosaur_Tribal",new Vector3( 3f, 0f,  4f),  -15f, "Camp_Totem")) placed++;
            if (PlaceTribal(c, "Sm_Bone_Pile_Prehistoric", new Vector3(4f, 0f, -1f), 60f, "Camp_BonePile")) placed++;
            if (PlaceTribal(c, "Vase_Tribal_Big",      new Vector3(-4f, 0f, -1.5f),120f, "Camp_VaseBig")) placed++;
            if (PlaceTribal(c, "Flag_Stand_Tribal",    new Vector3( 0f, 0f, -3f),    0f, "Camp_Flag")) placed++;

            Log($"Tribal outpost dressing: {placed} Tribal prop(s) placed (towers/torches/camp). " +
                "0 = polyperfect not imported (LogWarnings above).");
        }

        // Load a Tribal_T prefab by asset path (gitignored pack -> editor path load,
        // never Resources.Load). Null + a single LogWarning if absent.
        private static GameObject LoadTribal(string prefabName)
        {
            string path = $"{TribalPrefabRoot}{prefabName}.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                Warn($"Tribal prop '{prefabName}' not found at '{path}' — skipped " +
                     "(polyperfect pack may not be imported on this clone).");
            return go;
        }

        // Place one Tribal prop; returns true only if the prefab actually loaded (no
        // primitive stand-in for the dressing pass — a missing prop is simply skipped).
        private static bool PlaceTribal(Transform parent, string prefabName, Vector3 localPos, float yaw, string name)
        {
            var prefab = LoadTribal(prefabName);
            if (prefab == null) return false;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(parent, false);
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.name = name;
            MarkStatic(inst);
            return true;
        }

        // ===================================================================
        //  CATALOG-FIRST PROP-ROLE MAP — the single source of truth that maps a
        //  recipe ROLE to a real prefab on disk. polyperfect pack first (with a small
        //  alt-category search), committed Resources/Structures fallback, then null
        //  (PlaceOne substitutes a tinted primitive). Every miss is ONE LogWarning.
        // ===================================================================
        private static GameObject ResolveRole(string role)
        {
            switch ((role ?? "").Trim().ToLowerInvariant())
            {
                // ---- fort / structure roles ----
                case "wall_wood":          return Resolve("Wall_Medieval_Wood",  "Medieval_M", ResStructRoot + "Wall_Medieval_Wood.prefab");
                case "wall_wood_breached": return Resolve("Wall_Medieval_Wood",  "Medieval_M", ResStructRoot + "Wall_Medieval_Wood.prefab");
                case "wall_stone":         return Resolve("Wall_Medieval_Stone", "Medieval_M", ResStructRoot + "Wall_Medieval_Stone.prefab");
                case "gate_wood":
                case "gate":               return Resolve("Gate_Medieval_Medium","Medieval_M", ResStructRoot + "Gate_Medieval_Medium.prefab");
                case "watchtower":         return Resolve("Tower_Medieval_Wood", "Medieval_M", ResStructRoot + "Tower_Medieval_Wood.prefab");
                case "stone_tower":        return Resolve("Tower_Castle_Round",  "Medieval_M", ResStructRoot + "Tower_Castle_Round.prefab");
                case "hut":                return Resolve("House_Medieval_Small","Medieval_M", ResStructRoot + "House_Medieval_Small.prefab");
                case "well":               return Resolve("Well",                "Medieval_M", ResStructRoot + "Well.prefab");

                // ---- dungeon roles (Fantasy_M has all of these) ----
                case "dungeon_floor":      return Resolve("Dungeon_Floor_Stone", "Fantasy_M", null);
                case "dungeon_wall":       return Resolve("Dungeon_Wall_Stone",  "Fantasy_M", null);
                case "dungeon_pillar":     return Resolve("Dungeon_Pillar_Stone_Round", "Fantasy_M", null);
                case "dungeon_door":       return Resolve("Dungeon_Door_Stone",  "Fantasy_M", null);
                case "altar":              return Resolve("Altar",               "Fantasy_M", ResStructRoot + "Altar.prefab");

                // ---- scatter props ----
                case "campfire":           return Resolve("Fire",        "Survival_M", null);
                case "torch":              return Resolve("Torche_Wall", "Fantasy_M",  ResStructRoot + "Torche_Wall.prefab");
                case "crate":              return Resolve("Crate_Box",   "Fantasy_M",  null);
                case "barrel":             return Resolve("Jar_Big",     "Fantasy_M",  null);
                case "banner":             return Resolve("Flag_Medieval","Medieval_M", null);
                case "spikes":             return Resolve("Stakes",      "Medieval_M", null);
                case "bones":              return Resolve("Skull_Human", "Fantasy_M",  null);
                case "rubble":             return Resolve("Rubble_Stone","Fantasy_M",  null);
                case "ivy":                return Resolve("Grave_Dirt",  "Fantasy_M",  null);   // overgrowth stand-in

                // ---- nature: ice + cave (Nature_M/Ice_M, Nature_M/Stones_M) ----
                case "stalagmite":         return Resolve("Stalags",  "Nature_M/Stones_M", null);
                case "crystals":           return Resolve("Crystals", "Nature_M/Stones_M", null);
                case "cave_skull":         return Resolve("Cave_Skull","Nature_M/Stones_M", null);
                case "ice_floe":           return Resolve("Floe",     "Nature_M/Ice_M", null);
                case "icicle":             return Resolve("Icicle",   "Nature_M/Ice_M", null);

                default:
                    Warn($"Unknown prop role '{role}' — no catalog entry; a tinted primitive will stand in.");
                    return null;
            }
        }

        // Try the named prefab in the primary polyperfect category, then a few alt
        // categories, then the explicit Resources fallback. Note: a category may be a
        // nested path (e.g. "Nature_M/Stones_M").
        private static readonly string[] AltCategories =
            { "Medieval_M", "Fantasy_M", "Survival_M", "Props_M", "Nature_M", "Tools_M", "Farm_M" };

        private static GameObject Resolve(string prefabName, string primaryCategory, string resourcesFallback)
        {
            var go = LoadPoly(primaryCategory, prefabName);
            if (go != null) return go;

            foreach (var cat in AltCategories)
            {
                if (cat == primaryCategory) continue;
                go = LoadPoly(cat, prefabName);
                if (go != null) return go;
            }

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

        private static void ScatterRole(Transform parent, string role, int count, int seed,
            float area, float baseY, string label)
        {
            GameObject prefab = ResolveRole(role);
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
    }
}
