// =============================================================================
// EnemyStrongholdBuilder — RECIPE-DRIVEN, CATALOG-FIRST enemy-stronghold builder
// that REGENERATES the Village2 scene as a LAYERED enemy fortress.
// -----------------------------------------------------------------------------
// Run from:  Defenders > World > Build Village2 Enemy Stronghold
// Batchmode: DeNelle.Editor.EnemyStrongholdBuilder.Build   (via run-unity-method)
//
// WHY THIS EXISTS (WORK_ORDER_Village2_Enemy_Stronghold.md): keep the Village2
// scene NAME (referenced everywhere — SceneRouter.Village = "Village2") but swap
// its CONTENTS for a layered enemy stronghold:
//   outer courtyard (wall ring + main gate + corner towers)
//     -> chokepoint (inner wall + narrow gate, traps here)
//       -> RAISED inner keep on a foundation platform reached by STAIRS
//         -> optional RAISED boss chamber
// Verticality is done RIGHT: every stair run gets a NavMeshLink (start at the
// base, end at platform height) so NavMeshAgents path up/down — the exact thing
// whose ABSENCE hid the castle seam for weeks. The link uses Unity.AI.Navigation
// DIRECTLY (the DeNelle.Editor asmdef references that package — same as
// GarrisonSceneBuilder uses NavMeshSurface directly) and FAILS LOUD if the type
// cannot be resolved (no silent link failure).
//
// RECONCILED, NOT GREENFIELD: this mirrors GarrisonSceneBuilder's proven path —
//   * polyperfect _M / Resources/Structures asset-path prop resolution
//     (the SAME ResolveRole keys), NOT StructureFactory.Create. StructureFactory
//     needs a POPULATED CatalogRegistry, which is empty at editor-batchmode time
//     (no Village content bootstrap runs without play mode) — so it would place
//     nothing in a headless bake. GarrisonSceneBuilder loads prefabs by asset
//     path for exactly this reason; we follow it and add a render-verify +
//     FlowTrace per piece so the TGVRU "no silent invisible blocker" rule still
//     holds (every miss is a single LogWarning -> tinted primitive, never an
//     error, so a pack-less clone degrades the scene instead of breaking it).
//   * cross-assembly REFLECTION for DeNelle.Village types (GarrisonController),
//     because the Editor asmdef references DeNelle.Core + Unity.AI.Navigation but
//     NOT DeNelle.Village.
//
// The stronghold-specific layout (courtyard/chokepoint/keep/bossChamber + traps +
// destruction + boss) lives in the SAME garrison-recipes.json under
// id "village2_stronghold", in fields Core's GarrisonRecipe does not model. We
// parse those extra blocks with a LOCAL Newtonsoft DTO (StrongholdRecipe) so Core
// stays untouched (Newtonsoft ignores the unknown fields when GarrisonRecipeCatalog
// reads the same file). One recipe, two readers.
//
// BAKED-LIGHT-READY: torches place a torch GameObject + a small light ANCHOR
// marker (Empty named "TorchLightAnchor_*"); we do NOT add a shadowed realtime
// Point Light per torch (mobile perf). Bake lighting after generate.
//
// No .unity hand-edits. Batchmode-safe (no EditorUtility dialogs). Idempotent
// (clear-then-build). Canon: the village is Elarion.
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;          // NavMeshSurface + NavMeshLink — referenced directly (asmdef)
using UnityEngine.SceneManagement;
using DeNelle.Core;                 // CanonicalJson
using DeNelle.Core.Diagnostics;     // FlowTrace / Guard — TGVRU (CLAUDE.md §12)

namespace DeNelle.Editor
{
    public static class EnemyStrongholdBuilder
    {
        // ------------------------------------------------------------------ paths
        private const string RecipeRelPath = "Data/Canonical/garrison-recipes.json";
        private const string RecipeId      = "village2_stronghold";

        private const string ScenePath = "Assets/Scenes/Village2.unity";
        private const string ScenesDir = "Assets/Scenes";
        private const string NavDir    = "Assets/Scenes/Village2";
        private const string NavAsset  = "Assets/Scenes/Village2/NavMesh-Village2.asset";

        private const string PolyPrefabRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";
        private const string ResStructRoot = "Assets/Resources/Structures/";

        private const string RootName = "StrongholdRoot";

        // ===================================================================
        //  ENTRY POINTS
        // ===================================================================

        [MenuItem("Defenders/World/Build Village2 Enemy Stronghold")]
        public static void Build()
        {
            using var _ = FlowTrace.Enter("Stronghold", "Build Village2 enemy stronghold");
            Log("=== Build Village2 Enemy Stronghold START ===");

            var recipe = LoadStrongholdRecipe();
            if (recipe == null)
            {
                FlowTrace.Fail("Stronghold", $"No recipe '{RecipeId}' in {RecipeRelPath} — nothing built. " +
                    "Verify the Resources + StreamingAssets garrison-recipes.json copies.");
                Err($"No recipe '{RecipeId}' found — aborting (see {RecipeRelPath}).");
                return;
            }

            BuildFromRecipe(recipe);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("=== Build Village2 Enemy Stronghold DONE ===");
        }

        // ===================================================================
        //  RECIPE LOAD — parse the SAME garrison-recipes.json the Core catalog
        //  reads, but into a LOCAL DTO that ALSO captures the stronghold-only
        //  layout/traps/destruction/boss blocks (Core's GarrisonRecipe omits them).
        // ===================================================================
        private static StrongholdRecipe LoadStrongholdRecipe()
        {
            return FlowTrace.Try("Stronghold", "load + parse recipe", () =>
            {
                string text = CanonicalJson.Read(RecipeRelPath);
                if (string.IsNullOrEmpty(text))
                {
                    Warn($"{RecipeRelPath} not found / empty.");
                    return null;
                }
                var file = JsonConvert.DeserializeObject<StrongholdRecipeFile>(text);
                if (file == null || file.Recipes == null) return null;
                foreach (var r in file.Recipes)
                    if (r != null && string.Equals(r.Id, RecipeId, System.StringComparison.OrdinalIgnoreCase))
                        return r;
                return null;
            }, fallback: null);
        }

        // ===================================================================
        //  THE BUILD — opens Village2, clears it, builds the layered stronghold,
        //  bakes, saves. Keeps the scene NAME = Village2.
        // ===================================================================
        private static void BuildFromRecipe(StrongholdRecipe recipe)
        {
            // 1) Open the Village2 scene and clear its contents (idempotent rebuild).
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Log($"Opened {ScenePath}; clearing {scene.GetRootGameObjects().Length} root object(s).");
                foreach (var go in scene.GetRootGameObjects())
                    Object.DestroyImmediate(go);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Warn($"{ScenePath} did not exist — built a fresh empty scene (will save to that path).");
            }

            // 2) Scene scaffold.
            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;
            var environment = new GameObject("Environment"); environment.transform.SetParent(root.transform, false);
            var props       = new GameObject("Props");       props.transform.SetParent(root.transform, false);
            var traps       = new GameObject("Traps");       traps.transform.SetParent(root.transform, false);
            var links       = new GameObject("NavLinks");    links.transform.SetParent(root.transform, false);
            var spawnGroup  = new GameObject("EnemySpawnPoints"); spawnGroup.transform.SetParent(root.transform, false);

            // Layout dims (defaults are safe when a layout block is absent).
            var layout = recipe.Layout ?? new StrongholdLayout();
            float courtyardHalf = Mathf.Max(8f, (layout.Courtyard?.Size ?? 14) );   // outer wall ring half-extent
            float chokeHalf     = courtyardHalf * 0.55f;                            // inner (chokepoint) ring half
            float gateWidth     = Mathf.Max(2f, layout.Chokepoint?.Width ?? 2);     // narrow inner gate (tiles)
            float keepHalf      = chokeHalf * 0.55f;                                // keep platform half-extent
            float platH         = layout.Keep?.PlatformHeight ?? 1.5f;             // keep raise
            float bossH         = platH + 1.5f;                                     // boss chamber raise (above keep)

            // 3) Moody stronghold lighting (ruined/dim) — directional + ambient + fog.
            BuildMoodyLighting(root.transform, recipe);

            // 4) Ground floor (big stone plane) — feeds the navmesh via its MeshCollider.
            BuildGroundFloor(environment.transform, courtyardHalf);

            // 5) Hero entry seam ONLY (mirrors GarrisonSceneBuilder). WO-480 (E): Village2 is a
            //    ONE-WAY outpost — do NOT build the ReturnToOuterWorld_Seam (the BuildReturnSeam
            //    method stays defined but unused; the seam GameObject must NOT be created).
            var entryPos = new Vector3(0f, 0.1f, -(courtyardHalf + 6f));
            var entry = new GameObject("HeroStartPoint_PlayerSpawn");
            entry.transform.SetParent(root.transform, false);
            entry.transform.position = entryPos;
            entry.transform.rotation = Quaternion.LookRotation((Vector3.zero - entryPos).normalized, Vector3.up);

            int placed = 0;

            // 6) LAYER 1 — outer courtyard: stone wall ring with a front MAIN gate gap +
            //    4 corner towers. Destruction swaps a fraction of walls to rubble piles.
            placed += BuildWallRing(environment.transform, courtyardHalf, 0f,
                wallRole: (layout.Courtyard?.Walls == "wood" ? "wall_wood" : "wall_stone"),
                frontGateHalf: gateWidth, destruction: recipe.Destruction, label: "Outer");
            placed += PlaceOneCounted(environment.transform, "gate",
                new Vector3(0f, 0f, -courtyardHalf), 0f, "MainGate");
            placed += BuildCornerTowers(environment.transform, courtyardHalf,
                count: layout.Courtyard?.Towers ?? 4);

            // 7) LAYER 2 — chokepoint: an INNER wall ring with a NARROW front gate.
            //    Traps cluster in the chokepoint lane (between outer gate and inner gate).
            // WO-480 (pass 2, C): widen the inner gate gap to the FULL gateWidth (was gateWidth*0.6 ~0.6
            // tiles). After the bake erodes the opening by the navmesh agent radius, a 0.6-tile half-gap
            // is too narrow for the agent to fit through, so courtyard<->chokepoint stayed split even with
            // the gate collider stripped (Edit A). Match the outer ring's half-gap so the agent fits.
            placed += BuildWallRing(environment.transform, chokeHalf, 0f,
                wallRole: "wall_stone", frontGateHalf: gateWidth,
                destruction: recipe.Destruction, label: "Inner", carve: false);   // WO-480 (A): no carve — keep courtyard+chokepoint connected
            placed += PlaceOneCounted(environment.transform, "gate",
                new Vector3(0f, 0f, -chokeHalf), 0f, "ChokepointGate");

            // 8) LAYER 3 — RAISED inner keep on a foundation platform, reached by STAIRS.
            placed += BuildRaisedKeep(environment.transform, links.transform,
                keepHalf, platH, frontZ: -chokeHalf + 1f);

            // 9) LAYER 4 — optional RAISED boss chamber behind the keep, its own stairs+link.
            if (layout.BossChamber != null && layout.BossChamber.Enabled)
                placed += BuildBossChamber(environment.transform, links.transform,
                    keepHalf * 0.8f, bossH, frontZ: keepHalf + 1f, altar: layout.BossChamber.Altar);

            // 10) Scatter the recipe's decorative props (torches/banners/barrels/crates/
            //     chests/rubble/bones) across the courtyard.
            placed += ScatterDecorProps(props.transform, recipe, courtyardHalf);

            // 11) Traps — spike/arrow tiles in the courtyard + chokepoint.
            placed += BuildTraps(traps.transform, recipe, courtyardHalf, chokeHalf, gateWidth);

            // 12) Baked-light-ready torch anchors at the gates + keep corners.
            BuildTorchAnchors(props.transform, courtyardHalf, keepHalf, platH);

            if (placed <= 0)
                FlowTrace.Fail("Stronghold", "0 stronghold pieces placed — recipe/props resolved to nothing. " +
                    "Check polyperfect import + Resources/Structures fallbacks.");
            else
                FlowTrace.Step("Stronghold", $"placed {placed} stronghold piece(s).");

            // 13) Enemy spawn posts (guard posts) + GarrisonController wired to the recipe.
            var spawns = BuildSpawnPoints(spawnGroup.transform, courtyardHalf, chokeHalf, keepHalf, platH);
            WireGarrisonController(root, spawns, recipe);

            // 14) Bake the navmesh (the stair NavMeshLinks bridge the raised tiers).
            BakeNavMesh(root.transform);

            // 15) Save the scene to the Village2 path (keeps the name).
            SaveScene(scene);

            Log($"Village2 stronghold built: {placed} piece(s), {spawns.Count} spawn post(s), " +
                $"enemies [{string.Join(",", recipe.EnemyIds)}], baked + saved.");
        }

        // ===================================================================
        //  LAYER BUILDERS
        // ===================================================================

        // A wall ring (front/back/side runs) of `wallRole`, with a front gate gap, plus a
        // SOLID invisible carve-ring so the navmesh stops at the wall line (mirrors
        // GarrisonSceneBuilder.BuildPalisadeCarveRing — thin wall props don't carve alone).
        // Destruction: a fraction of wall slots become a "rubble" pile instead of a wall.
        // WO-480 (A): `carve` gates the invisible carve-ring. The INNER chokepoint ring passes
        // carve:false so the courtyard + chokepoint bake as ONE connected navmesh patch (concentric
        // carve rings were sealing them into separate islands). Inner walls stay as visual props.
        private static int BuildWallRing(Transform parent, float half, float y, string wallRole,
            float frontGateHalf, StrongholdDestruction destruction, string label, bool carve = true)
        {
            int placed = 0;
            const float seg = 3f;
            float dmgChance = destruction != null ? Mathf.Clamp01(destruction.WallDamageChance) : 0f;
            var rng = new System.Random((label + "|" + half).GetHashCode());

            void Slot(Vector3 pos, float yaw, string nm)
            {
                bool broken = dmgChance > 0f && rng.NextDouble() < dmgChance;
                if (broken)
                {
                    // WO-480 (D): rubble is VISUAL ONLY — must NOT fragment/trap the navmesh. Place it
                    // NOT NavigationStatic and strip its (potentially non-convex MeshCollider) collider,
                    // so the NavMeshSurface bakes straight through it (no island split, no agent trap —
                    // the named InnerWall_Front oracle break came from rubble MeshColliders).
                    if (PlaceVisualOnly(parent, "rubble", pos, yaw, nm) > 0) placed++;
                }
                else
                {
                    if (PlaceOneCounted(parent, wallRole, pos, yaw, nm) > 0) placed++;
                }
            }

            // Front (-Z) with gate gap, back (+Z), and the two sides.
            // WO-480 (pass 4): the wall PREFAB is ~15.75m wide but slots step every 3m, so a wall
            // placed just outside frontGateHalf SPILLS across the gate opening, pinching the gate
            // navmesh to a hairline pathfinding can't cross (proven by the gate-approach probe: the
            // x=0 corridor samples on-mesh but CalculatePath dead-ends short). Skip any FRONT wall
            // whose body would overlap the opening — clear a band wider than the wall half-width so a
            // genuinely connected corridor bakes through the gate. The gate ARCH prop still marks it.
            const float frontWallSkip = 9f; // > wall prefab half-width (~7.9m) -> ~5m clear gate
            for (float x = -half; x <= half; x += seg)
            {
                if (Mathf.Abs(x) > frontWallSkip) Slot(new Vector3(x, y, -half), 0f, $"{label}Wall_Front");
                Slot(new Vector3(x, y, half), 0f, $"{label}Wall_Back");
            }
            for (float z = -half; z <= half; z += seg)
            {
                Slot(new Vector3(-half, y, z), 90f, $"{label}Wall_Left");
                Slot(new Vector3( half, y, z), 90f, $"{label}Wall_Right");
            }

            // WO-480 (pass 4): the carve-ring is now REDUNDANT — the walls are themselves NotWalkable
            // colliders (pass 3) that carve the navmesh at the wall line. Worse, the front carve cubes
            // (FrontL/FrontR) PINCHED the gate opening, contributing to the hairline gate seam. So the
            // carve-ring is no longer built; walls bound the navmesh, gate gaps stay genuinely open.
            // (BuildCarveRing/AddCarveWall are retained but unused.)
            _ = carve; // param kept for call-site compatibility; carve-ring intentionally not built
            return placed;
        }

        // Four invisible solid carve cubes (collider only, renderer disabled, NavigationStatic)
        // hugging the wall line so the bake carves a bounded navmesh; the front edge splits
        // around the gate gap. Mirrors GarrisonSceneBuilder.BuildPalisadeCarveRing.
        private static void BuildCarveRing(Transform parent, float half, float y, float gateHalf, string label)
        {
            const float t = 1.0f, h = 4.0f;
            float baseY = y + h * 0.5f;
            float len = half * 2f + t;

            AddCarveWall(parent, new Vector3(0f, baseY, half),  new Vector3(len, h, t), $"{label}Carve_Back");
            AddCarveWall(parent, new Vector3(-half, baseY, 0f), new Vector3(t, h, len), $"{label}Carve_Left");
            AddCarveWall(parent, new Vector3( half, baseY, 0f), new Vector3(t, h, len), $"{label}Carve_Right");

            float sideLen = half - gateHalf;
            if (sideLen > 0.1f)
            {
                float cx = (half + gateHalf) * 0.5f;
                AddCarveWall(parent, new Vector3(-cx, baseY, -half), new Vector3(sideLen, h, t), $"{label}Carve_FrontL");
                AddCarveWall(parent, new Vector3( cx, baseY, -half), new Vector3(sideLen, h, t), $"{label}Carve_FrontR");
            }
        }

        private static void AddCarveWall(Transform parent, Vector3 localPos, Vector3 scale, string name)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = name;
            w.transform.SetParent(parent, false);
            w.transform.localPosition = localPos;
            w.transform.localScale = scale;
            var mr = w.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;   // invisible — collider only
            MarkStatic(w);
            // WO-480 (pass 3): a carve cube is non-floor OBSTACLE geometry — its job is to carve the
            // wall line, NOT to be walked on. Mark it NOT WALKABLE so its 4m-tall top doesn't bake its
            // own walkable sheet (the same y~6.84 wall-top root cause that fragments the floor).
            MarkNotWalkable(w);
        }

        private static int BuildCornerTowers(Transform parent, float half, int count)
        {
            int placed = 0;
            float r = half + 0.5f;
            var corners = new[]
            {
                new Vector3(-r, 0f, -r), new Vector3( r, 0f, -r),
                new Vector3(-r, 0f,  r), new Vector3( r, 0f,  r),
            };
            for (int i = 0; i < Mathf.Min(count, corners.Length); i++)
            {
                float yaw = Quaternion.LookRotation((Vector3.zero - corners[i]).normalized, Vector3.up).eulerAngles.y;
                if (PlaceOneCounted(parent, "watchtower", corners[i], yaw, $"CornerTower_{i}") > 0) placed++;
            }
            return placed;
        }

        // A RAISED keep: a foundation platform cube (walkable top) + a central stone-tower
        // keep core, reached by a stair ramp from the front. The stairs get a NavMeshLink so
        // agents path up onto the platform.
        private static int BuildRaisedKeep(Transform env, Transform links, float keepHalf,
            float platH, float frontZ)
        {
            int placed = 0;

            // Foundation platform (the raised floor — named Platform_* for the bake).
            var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plat.name = "Platform_Keep";
            plat.transform.SetParent(env, false);
            plat.transform.localPosition = new Vector3(0f, platH * 0.5f, keepHalf * 0.4f);
            plat.transform.localScale = new Vector3(keepHalf * 2.2f, platH, keepHalf * 2.2f);
            // WO-480 (pass 2, B): the platform TOP must bake navmesh so Spawn_Keep is on-mesh (it was
            // OFF-MESH entirely — the ramp led nowhere). The primitive cube carries a BoxCollider that
            // the NavMeshSurface samples (useGeometry = PhysicsColliders), and MarkStatic flags it
            // NavigationStatic. Platform TOP y = localPos.y(platH*0.5) + halfScale.y(platH*0.5) = platH,
            // which is exactly where the keep ramp arrives (rampTop.y = platH) — slope meets surface.
            MarkStatic(plat);
            TintMesh(plat, new Color(0.30f, 0.28f, 0.26f));
            placed++;
            FlowTrace.Step("Stronghold",
                $"Platform_Keep walkable: top y={platH} (NavigationStatic + BoxCollider) — ramp top meets it.");

            // Keep core (a stone tower) on top of the platform. It sits CENTRE only — do NOT carve
            // interior walls that would re-fragment the platform top (WO-480 pass 2, B: leave it open).
            placed += PlaceOneCounted(env, "stone_tower",
                new Vector3(0f, platH, keepHalf * 0.4f), 0f, "Keep_Core");

            // Stairs up the front of the platform + the NavMeshLink that makes the climb pathable.
            float stairBaseZ = frontZ;                                  // base at chokepoint front
            float stairTopZ  = -keepHalf * 0.4f;                        // landing on the platform front edge
            placed += BuildStairsWithLink(env, links, "Keep",
                basePos: new Vector3(0f, 0f, stairBaseZ),
                topPos:  new Vector3(0f, platH, stairTopZ),
                width: 4f);
            return placed;
        }

        private static int BuildBossChamber(Transform env, Transform links, float half,
            float bossH, float frontZ, bool altar)
        {
            int placed = 0;

            var plat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plat.name = "Platform_BossChamber";
            plat.transform.SetParent(env, false);
            plat.transform.localPosition = new Vector3(0f, bossH * 0.5f, half + 2f);
            plat.transform.localScale = new Vector3(half * 2.0f, bossH, half * 2.0f);
            MarkStatic(plat);
            TintMesh(plat, new Color(0.26f, 0.22f, 0.24f));
            placed++;

            if (altar)
                placed += PlaceOneCounted(env, "altar",
                    new Vector3(0f, bossH, half + 2f), 0f, "Boss_Altar");

            // Stairs from the keep platform up to the boss chamber + its NavMeshLink.
            placed += BuildStairsWithLink(env, links, "Boss",
                basePos: new Vector3(0f, 0f, frontZ),
                topPos:  new Vector3(0f, bossH, half + 0.5f),
                width: 4f);
            return placed;
        }

        // WO-480 (C): WALKABLE RAMP, not a NavMeshLink. The Village2 hero is an INPUT-driven
        // NavMeshAgent (Move(), not SetDestination) — it CANNOT auto-cross a NavMeshLink, so a
        // raised tier reached only by a link is ALWAYS a separate island for the player. Instead we
        // lay a NavigationStatic sloped box from courtyard ground up to the platform top, with a
        // shallow-enough pitch (rise platH over run ~platH*4) that the NavMeshSurface bakes a
        // CONTINUOUS slope onto the keep. The agent simply walks up. No NavMeshLink is created.
        private static int BuildStairsWithLink(Transform env, Transform links, string label,
            Vector3 basePos, Vector3 topPos, float width)
        {
            int placed = 0;

            // Shallow walkable slope: enforce run >= rise*4 (<= ~14 deg) so the surface bakes onto it.
            float rise = topPos.y - basePos.y;
            float dirZ = (topPos.z - basePos.z);
            float dirSign = dirZ >= 0f ? 1f : -1f;
            float minRun = Mathf.Max(1f, Mathf.Abs(rise) * 4f);     // shallow enough to bake
            // Re-derive the ramp top so the run is long enough (extend the base outward from the platform).
            float runLen = Mathf.Max(minRun, Mathf.Abs(dirZ));
            Vector3 rampBase = new Vector3(topPos.x, basePos.y, topPos.z - dirSign * runLen);
            Vector3 rampTop  = new Vector3(topPos.x, topPos.y, topPos.z);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = $"Ramp_{label}";   // the RAMP carries the navmesh (WO-480 C)
            ramp.transform.SetParent(env, false);
            Vector3 mid = (rampBase + rampTop) * 0.5f;
            float pitch = Mathf.Atan2(rise, runLen) * Mathf.Rad2Deg;   // shallow incline
            ramp.transform.localPosition = mid;
            ramp.transform.localRotation = Quaternion.Euler(-pitch * dirSign, 0f, 0f);
            float slabLen = Mathf.Sqrt(runLen * runLen + rise * rise);
            ramp.transform.localScale = new Vector3(Mathf.Max(width, 4f), 0.4f, Mathf.Max(1f, slabLen));
            MarkStatic(ramp);   // NavigationStatic — the slope bakes onto the keep platform
            TintMesh(ramp, new Color(0.33f, 0.30f, 0.27f));
            placed++;

            // WO-480 (C): NO NavMeshLink — the input-hero cannot cross it. The ramp IS the path.
            FlowTrace.Step("Stronghold",
                $"Ramp_{label}: walkable slope base={rampBase} top={rampTop} pitch={pitch:F1}deg — input-hero walks up (no NavMeshLink).");
            return placed;
        }

        // Add a NavMeshLink bridging basePos -> topPos. Uses the type DIRECTLY (the
        // DeNelle.Editor asmdef references Unity.AI.Navigation). LOUD FAIL on any miss.
        // WO-480 (C): NO LONGER CALLED for Village2 — the input-driven hero cannot cross a
        // NavMeshLink, so verticality is now a walkable ramp (see BuildStairsWithLink). Kept
        // defined for reference / future SetDestination-only strongholds.
        private static void BuildNavLink(Transform parent, string name, Vector3 worldBase,
            Vector3 worldTop, float width)
        {
            bool ok = Guard.Try("Stronghold", $"add NavMeshLink '{name}'", () =>
            {
                var host = new GameObject(name);
                host.transform.SetParent(parent, false);
                host.transform.position = Vector3.zero;   // endpoints below are absolute (link space == host local @ origin)

                var link = host.AddComponent<NavMeshLink>();
                if (link == null)
                    throw new System.Exception("AddComponent<NavMeshLink> returned null.");

                // Endpoints are expressed in the host's local space; host sits at origin so
                // local == world. start at the base, end at the platform-height landing.
                link.startPoint = worldBase;
                link.endPoint   = worldTop;
                link.width      = width;
                link.bidirectional = true;
                link.area = 0;          // Walkable
                link.UpdateLink();
                FlowTrace.Step("Stronghold",
                    $"NavMeshLink '{name}' start={worldBase} end={worldTop} width={width} — verticality bridged.");
            });

            if (!ok)
                FlowTrace.Fail("Stronghold",
                    $"NavMeshLink '{name}' FAILED to create — the raised tier ({worldBase} -> {worldTop}) " +
                    "will NOT be pathable. (Unity.AI.Navigation must resolve; do NOT ship this silently.)");
        }

        // ===================================================================
        //  PROPS / TRAPS / TORCHES
        // ===================================================================

        // Scatter the recipe's decorative props (skip the structural roles already placed by
        // the layer builders). Deterministic sprinkle, 3 per listing.
        private static int ScatterDecorProps(Transform props, StrongholdRecipe recipe, float half)
        {
            var structural = new HashSet<string> { "wall_wood", "wall_stone", "gate", "gate_wood",
                "watchtower", "stone_tower", "stairs", "platform", "rubble" };
            int placed = 0, seed = 211;
            var roles = recipe.Props ?? new List<string>();
            foreach (var raw in roles)
            {
                string role = (raw ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(role) || structural.Contains(role)) continue;
                placed += ScatterRole(props, role, 3, seed, half * 0.8f, 0f, role);
                seed += 17;
            }
            return placed;
        }

        // Traps: spike/arrow tiles. Up to traps.max, biased to the courtyard + the chokepoint
        // lane (between the outer gate and the inner gate). spike -> a spikes prop; arrow ->
        // a tinted floor tile marker (arrow-trap trigger volume).
        private static int BuildTraps(Transform parent, StrongholdRecipe recipe,
            float courtyardHalf, float chokeHalf, float gateWidth)
        {
            int placed = 0;
            int max = recipe.Traps != null ? Mathf.Max(0, recipe.Traps.Max) : 0;
            if (max <= 0) return 0;

            var rng = new System.Random("traps".GetHashCode());
            bool inCourtyard = recipe.Traps == null || recipe.Traps.Courtyard;
            bool inChoke     = recipe.Traps == null || recipe.Traps.Chokepoint;

            for (int i = 0; i < max; i++)
            {
                // Alternate spike/arrow.
                bool spike = (i % 2 == 0);
                Vector3 pos;
                if (inChoke && (!inCourtyard || i % 2 == 1))
                {
                    // chokepoint lane: between -courtyardHalf and -chokeHalf on the centre corridor
                    float z = Mathf.Lerp(-courtyardHalf + 1f, -chokeHalf - 1f, (float)rng.NextDouble());
                    float x = ((float)rng.NextDouble() * 2f - 1f) * (gateWidth * 0.8f);
                    pos = new Vector3(x, 0.02f, z);
                }
                else
                {
                    float x = ((float)rng.NextDouble() * 2f - 1f) * chokeHalf * 0.9f;
                    float z = ((float)rng.NextDouble() * 2f - 1f) * chokeHalf * 0.9f;
                    pos = new Vector3(x, 0.02f, z);
                }

                if (spike)
                {
                    if (PlaceOneCounted(parent, "spikes", pos, (float)(rng.NextDouble() * 360.0), $"Trap_Spike_{i}") > 0)
                        placed++;
                }
                else
                {
                    // arrow trap = a tinted floor marker tile (trigger volume added at runtime later).
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Trap_Arrow_{i}";
                    tile.transform.SetParent(parent, false);
                    tile.transform.localPosition = pos;
                    tile.transform.localScale = new Vector3(2f, 0.05f, 2f);
                    var col = tile.GetComponent<Collider>(); if (col != null) col.isTrigger = true;
                    TintMesh(tile, new Color(0.45f, 0.15f, 0.12f));
                    placed++;
                }
            }
            FlowTrace.Step("Stronghold", $"placed {placed} trap(s) (max={max}, courtyard={inCourtyard}, choke={inChoke}).");
            return placed;
        }

        // Baked-light-ready torch ANCHORS: a torch prop + an empty light-anchor marker. We do
        // NOT add a shadowed realtime Point Light per torch (mobile perf). Bake lighting after
        // generate — a lighting pass can read the "TorchLightAnchor_*" markers to place baked
        // lights or a small capped realtime budget.
        private static void BuildTorchAnchors(Transform props, float courtyardHalf, float keepHalf, float platH)
        {
            var positions = new (Vector3 pos, string nm)[]
            {
                (new Vector3(-2.5f, 0f, -courtyardHalf + 0.5f), "GateTorch_L"),
                (new Vector3( 2.5f, 0f, -courtyardHalf + 0.5f), "GateTorch_R"),
                (new Vector3(-keepHalf, platH, keepHalf * 0.4f), "KeepTorch_L"),
                (new Vector3( keepHalf, platH, keepHalf * 0.4f), "KeepTorch_R"),
            };
            foreach (var (pos, nm) in positions)
            {
                PlaceOneCounted(props, "torch", pos, 0f, $"Torch_{nm}");
                // Light anchor marker only — NO realtime Point Light (bake lighting after generate).
                var anchor = new GameObject($"TorchLightAnchor_{nm}");
                anchor.transform.SetParent(props, false);
                anchor.transform.localPosition = pos + new Vector3(0f, 2.2f, 0f);
            }
        }

        // ===================================================================
        //  GROUND / LIGHTING / SEAM / SPAWNS
        // ===================================================================

        // WO-480 (B + F): ONE continuous interior floor covering the FULL traversed interior:
        // the arrival point at z = -(half + 6), the keep base, and the OuterWorld CavePortal warp
        // target (20.6, 0.1, -38.3). The plane must reach every walked point so the NavMeshSurface
        // bakes a single connected patch (no off-mesh arrival, no keep gap).
        // *** The CavePortal target (20.6, -38.3) MUST stay on this floor — if it moves, re-size here. ***
        private static void BuildGroundFloor(Transform parent, float half)
        {
            const float CavePortalZ = -38.3f;   // OuterWorld CavePortal warp Z (arrival must be on-mesh)
            const float CavePortalX = 20.6f;    // OuterWorld CavePortal warp X
            // Required half-extent: cover at least (half + 10) every direction, the arrival -Z
            // (half + 6), AND the CavePortal point with margin (WO-480 F).
            float reqExtent = Mathf.Max(
                half + 10f,
                half + 6f + 4f,                 // arrival point + margin
                Mathf.Abs(CavePortalZ) + 6f,    // CavePortal Z + margin
                Mathf.Abs(CavePortalX) + 6f);   // CavePortal X + margin

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Floor_Stronghold";
            ground.transform.SetParent(parent, false);
            float planeScale = reqExtent / 5f;   // Unity plane is 10m => /5 per half-extent
            ground.transform.localScale = new Vector3(planeScale, 1f, planeScale);
            TintMesh(ground, new Color(0.22f, 0.21f, 0.20f));   // dark stone
            MarkStatic(ground);   // NavigationStatic — the NavMeshSurface bakes the connected surface on this floor
        }

        private static void BuildMoodyLighting(Transform parent, StrongholdRecipe recipe)
        {
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(34f, -32f, 0f);   // low, long shadows
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(0.60f, 0.66f, 0.62f);   // ruined-grey daylight
            sun.intensity = 0.72f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.20f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.15f, 0.18f, 0.16f);
            RenderSettings.fogDensity = 0.016f;
            Log($"Moody stronghold lighting set (theme={recipe.Theme}).");
        }

        // Return seam back to OuterWorld (single load, generous radius). Added by reflection
        // (DeNelle.Village.SceneTransitionTrigger — not referenced by the Editor asmdef).
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
                Warn("DeNelle.Village.SceneTransitionTrigger not found — return collider added WITHOUT " +
                     "the behaviour. Re-run after compile.");
                return;
            }
            var comp = seamGo.AddComponent(transType);
            SetField(transType, comp, "targetSceneName", "OuterWorld");
            SetField(transType, comp, "targetPosition", new Vector3(0f, 0.5f, -12f));
            SetField(transType, comp, "loadAdditive", false);
            SetField(transType, comp, "ProximityRadius", 16f);
            Log("Return seam wired: SceneTransitionTrigger -> OuterWorld (single load), proximity 16m.");
        }

        // Enemy guard posts: front approach, courtyard, chokepoint, and atop the keep platform.
        private static List<Transform> BuildSpawnPoints(Transform group, float courtyardHalf,
            float chokeHalf, float keepHalf, float platH)
        {
            var positions = new List<(Vector3 pos, string nm)>
            {
                (new Vector3( 0f, 0f, -courtyardHalf + 3f), "Spawn_Gate"),
                (new Vector3(-courtyardHalf * 0.6f, 0f, -2f), "Spawn_CourtyardW"),
                (new Vector3( courtyardHalf * 0.6f, 0f, -2f), "Spawn_CourtyardE"),
                (new Vector3( 0f, 0f, -chokeHalf + 1f), "Spawn_Chokepoint"),
                (new Vector3(-chokeHalf * 0.5f, 0f, chokeHalf * 0.4f), "Spawn_InnerW"),
                (new Vector3( chokeHalf * 0.5f, 0f, chokeHalf * 0.4f), "Spawn_InnerE"),
                (new Vector3( 0f, platH, keepHalf * 0.4f), "Spawn_Keep"),
                (new Vector3( 0f, 0f, courtyardHalf * 0.7f), "Spawn_Rear"),
            };

            var list = new List<Transform>();
            for (int i = 0; i < positions.Count; i++)
            {
                var sp = new GameObject(positions[i].nm);
                sp.transform.SetParent(group, false);
                sp.transform.position = positions[i].pos;
                sp.transform.rotation = Quaternion.LookRotation(
                    (new Vector3(0f, positions[i].pos.y, -(courtyardHalf + 4f)) - positions[i].pos).normalized, Vector3.up);
                list.Add(sp.transform);
            }
            return list;
        }

        // GarrisonController + reflection-wired enemies/levelRange/threat (mirrors
        // GarrisonSceneBuilder.WireGarrisonController exactly).
        private static void WireGarrisonController(GameObject root, List<Transform> spawns, StrongholdRecipe recipe)
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
            if (fSpawns != null) fSpawns.SetValue(comp, spawns.ToArray());
            else Warn("GarrisonController.spawnPoints field not found via reflection.");

            var ids = new List<string>(recipe.EnemyIds);
            SetField(type, comp, "enemyTypeIds", ids.ToArray());
            SetField(type, comp, "minLevel",    recipe.MinLevel);
            SetField(type, comp, "maxLevel",    recipe.MaxLevel);
            SetField(type, comp, "threatLevel", recipe.Threat);

            Log($"GarrisonController wired: {spawns.Count} spawn post(s), enemies " +
                $"[{string.Join(",", recipe.EnemyIds)}], levels {recipe.MinLevel}-{recipe.MaxLevel}, threat {recipe.Threat}.");
        }

        // ===================================================================
        //  BAKE + SAVE
        // ===================================================================
        private static void BakeNavMesh(Transform root)
        {
            var surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();

            var data = surface.navMeshData;
            if (data == null)
            {
                Warn("Village2: navMeshData null after bake — verify floor/platform MeshColliders.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(NavDir))
                AssetDatabase.CreateFolder(ScenesDir, "Village2");
            if (!AssetDatabase.Contains(data))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Object>(NavAsset);
                if (existing != null) AssetDatabase.DeleteAsset(NavAsset);
                AssetDatabase.CreateAsset(data, NavAsset);
                Log($"Village2: navmesh asset written -> {NavAsset}");
            }
        }

        private static void SaveScene(Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(ScenesDir))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            if (ok) Log("Saved scene -> " + ScenePath);
            else Err("SaveScene FAILED for " + ScenePath);
        }

        // ===================================================================
        //  PROP RESOLUTION + PLACEMENT (polyperfect _M -> Resources/Structures ->
        //  tinted primitive). Mirrors GarrisonSceneBuilder.ResolveRole keys + adds a
        //  per-piece render-verify + FlowTrace (TGVRU "no silent invisible blocker").
        // ===================================================================

        // Place one role + render-verify. Returns 1 on a placed piece, 0 if nothing placed.
        private static int PlaceOneCounted(Transform parent, string role, Vector3 localPos, float yaw, string name)
        {
            var prefab = ResolveRole(role);
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
                TintMesh(inst, new Color(0.32f, 0.22f, 0.18f));
            }
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.name = name;

            // WO-480 (pass 2, A): a GATE prop is purely DECORATIVE — the wall-ring gap defines the
            // actual opening. Left intact, the gate prop's blocking MeshCollider re-seals its own gap
            // (the owner previously fixed traversal by HAND-removing the ChokepointGate collider), which
            // split arrival<->courtyard (MainGate) and courtyard<->chokepoint (ChokepointGate) into
            // separate navmesh islands. So strip ALL its colliders AND keep it NOT NavigationStatic so
            // the NavMeshSurface bakes straight through the gap. (Walls/towers/etc. still MarkStatic below.)
            bool isGate = string.Equals((role ?? "").Trim(), "gate", System.StringComparison.OrdinalIgnoreCase)
                       || string.Equals((role ?? "").Trim(), "gate_wood", System.StringComparison.OrdinalIgnoreCase);
            if (isGate)
            {
                foreach (var col in inst.GetComponentsInChildren<Collider>(true))
                    if (col != null) Object.DestroyImmediate(col);
                // Deliberately NOT MarkStatic — the gate prop is excluded from NavigationStatic geometry.
                FlowTrace.Step("Stronghold",
                    $"gate prop '{name}' colliders stripped (gap stays open for nav).");
            }
            else
            {
                MarkStatic(inst);
                // WO-480 (pass 3): EVERY prop placed through PlaceOneCounted is NON-floor geometry
                // (walls, watchtower/stone_tower, altar, torch, spikes, decor — the floor/ramp/platform
                // are built as primitives elsewhere and never come through here). Mark it NOT WALKABLE so
                // its collider CARVES the floor but bakes no walkable top (kills the y~6.84 wall-top sheet
                // + lets the floor bake as ONE connected surface). Roles excluded by construction: the gate
                // prop above (colliders stripped, not static) and Floor_Stronghold / Ramp_* / Platform_Keep.
                MarkNotWalkable(inst);
            }

            // Render-verify: a placed piece with NO visible mesh self-reports (footprint logged).
            VerifyRenders(inst, role, name);
            return 1;
        }

        // WO-480 (D): place a role as VISUAL-ONLY decoration — NOT NavigationStatic and with all
        // colliders stripped — so it can never carve/fragment the navmesh or trap the agent.
        // Used for destruction rubble (the InnerWall_Front oracle trap was a rubble MeshCollider).
        private static int PlaceVisualOnly(Transform parent, string role, Vector3 localPos, float yaw, string name)
        {
            var prefab = ResolveRole(role);
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
                inst.transform.localScale = new Vector3(2f, 1.2f, 2f);
                TintMesh(inst, new Color(0.30f, 0.27f, 0.24f));
            }
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.name = name;

            // Strip every collider so the bake (PhysicsColliders geometry) ignores the rubble entirely.
            foreach (var col in inst.GetComponentsInChildren<Collider>(true))
                if (col != null) Object.DestroyImmediate(col);
            // Deliberately NOT MarkStatic — rubble is excluded from NavigationStatic geometry.
            // WO-480 (pass 3): also flag NOT WALKABLE for belt-and-suspenders (with colliders stripped it
            // contributes no bake geometry anyway, but this guarantees rubble can never bake a walkable top).
            MarkNotWalkable(inst);

            VerifyRenders(inst, role, name);
            return 1;
        }

        // >=1 enabled renderer with a non-null mesh, else FlowTrace.Fail + footprint log (a
        // collider with no mesh is the invisible-blocker shape). Mirrors
        // StructureFactory.VerifyStructureRenders at editor-bake time.
        private static void VerifyRenders(GameObject go, string role, string name)
        {
            if (go == null) return;
            int total = 0, enabled = 0, withMesh = 0;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r == null) continue;
                total++;
                if (r.enabled) enabled++;
                if (RendererHasMesh(r)) withMesh++;
            }
            // Carve walls + light anchors are intentionally meshless — they pass through PlaceOneCounted
            // only as primitives (always meshed), so any miss here is a real prop miss.
            if (enabled == 0 || withMesh == 0)
            {
                var col = go.GetComponentInChildren<Collider>(true);
                string fp = col != null ? $" collider bounds={col.bounds.size}" : "";
                FlowTrace.Fail("Stronghold",
                    $"piece '{name}' (role '{role}') renders nothing (renderers total={total} enabled={enabled} " +
                    $"withMesh={withMesh}){fp} — invisible/blocker piece.");
            }
        }

        private static bool RendererHasMesh(Renderer r)
        {
            if (r == null) return false;
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh != null;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null;
        }

        private static int ScatterRole(Transform parent, string role, int count, int seed,
            float area, float baseY, string label)
        {
            int placed = 0;
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * area;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * area;
                float yaw = (float)(rng.NextDouble() * 360.0);
                placed += PlaceOneCounted(parent, role, new Vector3(x, baseY, z), yaw, $"{label}_{i}");
            }
            return placed;
        }

        // ROLE -> prefab. Same keys as GarrisonSceneBuilder.ResolveRole (+ chest_gold/banner).
        private static GameObject ResolveRole(string role)
        {
            switch ((role ?? "").Trim().ToLowerInvariant())
            {
                case "wall_wood":   return Resolve("Wall_Medieval_Wood",  "Medieval_M", ResStructRoot + "Wall_Medieval_Wood.prefab");
                case "wall_stone":  return Resolve("Wall_Medieval_Stone", "Medieval_M", ResStructRoot + "Wall_Medieval_Stone.prefab");
                case "gate_wood":
                case "gate":        return Resolve("Gate_Medieval_Medium","Medieval_M", ResStructRoot + "Gate_Medieval_Medium.prefab");
                case "watchtower":  return Resolve("Tower_Medieval_Wood", "Medieval_M", ResStructRoot + "Tower_Medieval_Wood.prefab");
                case "stone_tower": return Resolve("Tower_Castle_Round",  "Medieval_M", ResStructRoot + "Tower_Castle_Round.prefab");
                case "altar":       return Resolve("Altar",               "Fantasy_M",  ResStructRoot + "Altar.prefab");

                case "torch":       return Resolve("Torche_Wall", "Fantasy_M",  ResStructRoot + "Torche_Wall.prefab");
                case "crate":       return Resolve("Crate_Box",   "Fantasy_M",  null);
                case "barrel":      return Resolve("Jar_Big",     "Fantasy_M",  null);
                case "banner":      return Resolve("Flag_Medieval","Medieval_M", null);
                case "chest_gold":  return Resolve("Chest",       "Fantasy_M",  null);
                case "spikes":      return Resolve("Stakes",      "Medieval_M", null);
                case "bones":       return Resolve("Skull_Human", "Fantasy_M",  null);
                case "rubble":      return Resolve("Rubble_Stone","Fantasy_M",  null);

                default:
                    Warn($"Unknown prop role '{role}' — a tinted primitive will stand in.");
                    return null;
            }
        }

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
        //  SHARED helpers (tint / static flags / reflection) — mirror GarrisonSceneBuilder.
        // ===================================================================
        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        // WO-480 (pass 3): mark NON-floor geometry as NOT WALKABLE so it CARVES the floor
        // navmesh but contributes NO walkable surface. ROOT CAUSE (proven by bake diag, not a
        // guess): the StrongholdRoot NavMeshSurface has collectObjects=All, useGeometry=
        // PhysicsColliders, layerMask=~0 — so it bakes EVERY collider as a walkable surface,
        // including the flat TOPS of walls + towers. The diag found a navmesh sheet at y~6.84
        // (wall/tower tops) stacked over the y~0 courtyard floor, and the floor fragmented into
        // disconnected islands (wall/tower/prop footprints + their baked tops split it). The hero
        // can never reach the wall-tops and the floor isn't one connected sheet.
        //
        // FIX: add a NavMeshModifier with overrideArea=true and area=1 (the built-in "Not Walkable"
        // area index) to every wall / tower / gate / decorative prop / rubble / trap-visual. The
        // collider still OBSTRUCTS (carves) the floor — the hero can't walk through a wall — but the
        // bake produces NO walkable top, so the y~6.84 wall-top sheet vanishes and the floor bakes as
        // ONE connected surface (wall footprints carved + gate gaps open). NavMeshModifier applies to
        // the GameObject it's on AND its children (unless a child has its own modifier), so adding it
        // to the placed prop root covers the whole prop hierarchy.
        //
        // Apply ONLY to non-floor geometry. Do NOT apply to the walkable surfaces:
        // Floor_Stronghold, the Ramp_* slopes, and Platform_Keep (they must stay default area 0).
        private const int NotWalkableArea = 1;   // Unity built-in "Not Walkable" navmesh area index

        private static void MarkNotWalkable(GameObject go)
        {
            if (go == null) return;
            var mod = go.GetComponent<NavMeshModifier>();
            if (mod == null) mod = go.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = NotWalkableArea;   // carves the floor, bakes no walkable top (no y~6.84 sheet)
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

        private static void SetField(System.Type type, Object comp, string fieldName, object value)
        {
            var f = type.GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(comp, value);
            else Warn("Field '" + fieldName + "' not found on " + type.Name + " — skipped.");
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void Log(string m)  => Debug.Log("[EnemyStrongholdBuilder] " + m);
        private static void Warn(string m) => Debug.LogWarning("[EnemyStrongholdBuilder] " + m);
        private static void Err(string m)  => Debug.LogError("[EnemyStrongholdBuilder] " + m);

        // ===================================================================
        //  LOCAL RECIPE DTO — parses the SAME garrison-recipes.json, capturing the
        //  stronghold-only blocks Core's GarrisonRecipe does not model. Newtonsoft
        //  ignores the garrison fields it doesn't need + vice-versa, so one file
        //  feeds both readers. ASCII-only. WebGL-safe (CanonicalJson reads it).
        // ===================================================================
        private sealed class StrongholdRecipeFile
        {
            [JsonProperty("recipes")] public List<StrongholdRecipe> Recipes = new List<StrongholdRecipe>();
        }

        private sealed class StrongholdRecipe
        {
            [JsonProperty("id")]         public string Id = "village2_stronghold";
            [JsonProperty("kind")]       public string Kind = "stronghold";
            [JsonProperty("size")]       public string Size = "large";
            [JsonProperty("theme")]      public string Theme = "ruined";
            [JsonProperty("lighting")]   public string Lighting;
            [JsonProperty("enemies")]    public List<string> Enemies = new List<string>();
            [JsonProperty("levelRange")] public List<int> LevelRange = new List<int>();
            [JsonProperty("threat")]     public int Threat = 3;
            [JsonProperty("boss")]       public string Boss;
            [JsonProperty("layout")]     public StrongholdLayout Layout;
            [JsonProperty("traps")]      public StrongholdTraps Traps;
            [JsonProperty("destruction")]public StrongholdDestruction Destruction;
            [JsonProperty("props")]      public List<string> Props = new List<string>();
            [JsonProperty("element")]    public string Element;

            [JsonIgnore] public int MinLevel =>
                (LevelRange != null && LevelRange.Count >= 1) ? System.Math.Max(1, LevelRange[0]) : 1;
            [JsonIgnore] public int MaxLevel =>
                (LevelRange != null && LevelRange.Count >= 2) ? System.Math.Max(MinLevel, LevelRange[1]) : MinLevel;
            [JsonIgnore] public IReadOnlyList<string> EnemyIds =>
                (Enemies != null && Enemies.Count > 0) ? Enemies : DefaultEnemies;
            private static readonly List<string> DefaultEnemies = new List<string> { "orc-berserker" };
        }

        private sealed class StrongholdLayout
        {
            [JsonProperty("courtyard")]   public CourtyardSpec Courtyard;
            [JsonProperty("chokepoint")]  public ChokepointSpec Chokepoint;
            [JsonProperty("keep")]        public KeepSpec Keep;
            [JsonProperty("bossChamber")] public BossChamberSpec BossChamber;
        }

        private sealed class CourtyardSpec
        {
            [JsonProperty("size")]   public float Size = 14f;
            [JsonProperty("walls")]  public string Walls = "stone";
            [JsonProperty("gate")]   public string Gate = "main";
            [JsonProperty("towers")] public int Towers = 4;
        }

        private sealed class ChokepointSpec
        {
            [JsonProperty("width")] public float Width = 2f;
            [JsonProperty("traps")] public List<string> Traps = new List<string>();
        }

        private sealed class KeepSpec
        {
            [JsonProperty("raised")]         public bool Raised = true;
            [JsonProperty("platformHeight")] public float PlatformHeight = 1.5f;
            [JsonProperty("stairs")]         public bool Stairs = true;
            [JsonProperty("navlink")]        public bool Navlink = true;
        }

        private sealed class BossChamberSpec
        {
            [JsonProperty("enabled")] public bool Enabled = true;
            [JsonProperty("raised")]  public bool Raised = true;
            [JsonProperty("navlink")] public bool Navlink = true;
            [JsonProperty("altar")]   public bool Altar = true;
        }

        private sealed class StrongholdTraps
        {
            [JsonProperty("max")]        public int Max = 8;
            [JsonProperty("courtyard")]  public bool Courtyard = true;
            [JsonProperty("chokepoint")] public bool Chokepoint = true;
        }

        private sealed class StrongholdDestruction
        {
            [JsonProperty("wallDamageChance")] public float WallDamageChance = 0.3f;
            [JsonProperty("level")]            public int Level = 1;
        }
    }
}
