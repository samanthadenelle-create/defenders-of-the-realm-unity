using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    // =============================================================================
    // CastleHubBuilder — Central Castle Hub (CastleHubRoot) scene automation.
    // -----------------------------------------------------------------------------
    // Run from: Defenders > Scenes > Build CastleHub_MainKeep
    //
    // Designed to run AFTER you create a blank/empty scene (File > New Scene > Empty).
    // It is idempotent: re-running in the same scene will clear the prior root and rebuild.
    //
    // Implements the Central Castle Hub spec:
    // - Separate scene for home + hub (distinct from Village2 primary).
    // - Beautifully designed castle using Quaternius (modular beauty, walls/floors/stairs/props)
    //   + polyperfect Low Poly Ultimate Pack _M tier (performant, single-atlas, mobile friendly).
    // - Outer defensive walls + 4 corner towers + main south gate (connection marker to OuterWorld).
    // - Central courtyard/plaza with exactly 8 dedicated structures (storefront + NPC interact points):
    //     1. Blacksmith (Weapons)   2. Lumbermill (Wood)   3. Windmill (Food)
    //     4. Echo Hollow (Pets)     5. Forge (Armor)       6. Arcane Tower (Magic)
    //     7. Jeweler (Gems)         8. Store/Marketplace (Monetization)
    // - Main Keep (Castle_Medieval) with Player Hero Hall / Personal Quarters (home space).
    // - Upper battlements (wide ~42m platform at height, 4 defensive towers, LOS down to courtyard
    //   for player-placed defenses via existing base-building system).
    // - Stairs/ramps for 2-level access.
    // - Connection point south for additive load with OuterWorld + NavMeshLink (bake NavMesh after).
    // - Space/comments for roaming NPCs/animals (Bella), mobile touch interaction points,
    //   integration with Economy/Yarn/NPCUpgradeStation/BuildModeController/WorldSceneLoader.
    // - Low-poly from packs → good mobile perf (static batch, LODs, URP lighting).
    //
    // After run:
    //   1. Save the scene under Assets/Scenes/ (e.g. MainCastle_Hall.unity or CastleHub_MainKeep.unity).
    //   2. Window > AI > Navigation (or add NavMeshSurface + bake for modern multi-scene use; include upper level).
    //   3. Add NavMeshLink at south gate for adjacency to OuterWorld (position root accordingly).
    //   4. Wire the 8 NPC_*_Interactable points with existing systems (Yarn dialogue for shops,
    //      NPCUpgradeStation for blacksmith/forge, pet roaming for Echo Hollow, etc.).
    //   5. Add SmartMobileCamera + Lean Touch setup for overview/follow + touch nav.
    //   6. Optional: extend WorldSceneLoader or add a gate trigger for seamless transition.
    //
    // Packs used (read docs before editing):
    // - polyperfect: Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/...
    // - Quaternius:  Assets/Quaternius/Medieval Village MegaKit/Modules/Prefabs/(Wall|Prop|...)/...
    // See: docs/INSTALLED_PACKS_INDEX.md, POLYPERFECT_NOTES.md, QUATERNIUS_NOTES.md,
    //      polyperfect-asset-catalog.md.
    //
    // No .unity hand-edits. Matches VillageSceneBuilder pattern (Editor-only, menu driven,
    // PrefabUtility.InstantiatePrefab, graceful miss handling).
    // =============================================================================
    public static class CastleHubBuilder
    {
        private const string MenuPath = "Defenders/Scenes/Build CastleHub_MainKeep";
        private const string RootName = "CastleHubRoot";
        private const string NavFloorName = "NavMeshFloor_Invisible_Walkable";

        // Pack roots (validated against catalogs + on-disk)
        private const string PolyRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/";
        private const string QuatRoot =
            "Assets/Quaternius/Medieval Village MegaKit/Modules/Prefabs/";

        [MenuItem(MenuPath)]
        public static void BuildCastleHub()
        {
            // --- Idempotent: clear prior generation in the open scene (safe for re-runs or blank scene) ---
            var prior = GameObject.Find(RootName);
            if (prior != null)
            {
                Debug.Log($"[CastleHubBuilder] Destroying prior {RootName} root for rebuild.");
                Object.DestroyImmediate(prior);
            }

            // Optional: if user wants a truly fresh scene, they created a blank one before running.
            // We populate whatever scene is active.

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            Debug.Log("[CastleHubBuilder] Building Central Castle Hub (CastleHubRoot) from Quaternius + polyperfect packs...");

            // === Load prefabs (null = will skip or placeholder) ===
            GameObject castleCore   = LoadPoly("Castle_Medieval.prefab");
            GameObject towerRound   = LoadPoly("Tower_Castle_Round.prefab");
            GameObject towerSquare  = LoadPoly("Tower_Castle_Square.prefab");
            GameObject towerBig     = LoadPoly("Tower_Medieval_Big.prefab");
            GameObject wallStone    = LoadPoly("Wall_Medieval_Stone.prefab");
            GameObject gateMedium   = LoadPoly("Gate_Medieval_Medium.prefab");
            GameObject drawbridge   = LoadPoly("Drawbridge_Medieval.prefab");
            GameObject stairsStone  = LoadPoly("Stairs_Medieval_Stone.prefab");

            // 8 structures sources
            GameObject houseMed     = LoadPoly("House_Medieval_Medium.prefab");
            GameObject houseLarge   = LoadPoly("House_Medieval_Large.prefab");
            GameObject houseSmall   = LoadPoly("House_Medieval_Small.prefab");
            GameObject windmill     = LoadPoly("Windmill_Medieval.prefab");
            GameObject watermill    = LoadPoly("Watermill_Medieval.prefab");
            GameObject stables      = LoadPoly("Stables_Medieval.prefab");
            GameObject marketStand  = LoadPoly("Marketplace_Stand_Simple.prefab");

            // Quaternius modular beauty pieces (for walls, floors, stairs, vines, details)
            GameObject qWallStraight = LoadQuat("Wall/Wall_Plaster_Straight.prefab");
            GameObject qStairsExt    = LoadQuat("Prop/Stairs_Exterior_Straight.prefab");
            GameObject qFloorWood    = LoadQuat("Wall/Floor_WoodDark.prefab"); // floors live under Wall/ in this pack tree
            GameObject qVine1        = LoadQuat("Prop/Prop_Vine1.prefab");
            GameObject qCrate        = LoadQuat("Prop/Prop_Crate.prefab");
            GameObject qBalcony      = LoadQuat("Prop/Balcony_Simple_Straight.prefab");

            // === OUTER WALLS + TOWERS + BATTLEMENTS (defensive perimeter) ===
            var wallsRoot = new GameObject("OuterWalls_Towers_Battlements");
            wallsRoot.transform.SetParent(root.transform, false);

            // 4 corner towers (polyperfect round for silhouette)
            if (towerRound != null)
            {
                Vector3[] corners = {
                    new Vector3(-42, 0, -42), new Vector3(42, 0, -42),
                    new Vector3(-42, 0, 42),  new Vector3(42, 0, 42)
                };
                for (int i = 0; i < corners.Length; i++)
                {
                    var t = (GameObject)PrefabUtility.InstantiatePrefab(towerRound);
                    t.transform.SetParent(wallsRoot.transform, false);
                    t.transform.localPosition = corners[i];
                    t.name = $"CornerTower_{i+1}";
                }
            }

            // Main perimeter walls (poly stone segments + Quaternius plaster for beauty/detail)
            if (wallStone != null)
            {
                // South wall segments (skip center for gate)
                for (int x = -3; x <= 3; x++)
                {
                    if (Mathf.Abs(x) > 1)
                    {
                        var w = (GameObject)PrefabUtility.InstantiatePrefab(wallStone);
                        w.transform.SetParent(wallsRoot.transform, false);
                        w.transform.localPosition = new Vector3(x * 13f, 0f, -44f);
                        w.name = $"Wall_South_{x}";
                    }
                }
                // North wall
                for (int x = -3; x <= 3; x++)
                {
                    if (Mathf.Abs(x) > 1)
                    {
                        var w = (GameObject)PrefabUtility.InstantiatePrefab(wallStone);
                        w.transform.SetParent(wallsRoot.transform, false);
                        w.transform.localPosition = new Vector3(x * 13f, 0f, 44f);
                        w.name = $"Wall_North_{x}";
                    }
                }
                // East/West simplified (add more Quaternius runs for full beauty)
            }

            // Quaternius west wall line for visual variety + battlements feel
            if (qWallStraight != null)
            {
                for (int i = 0; i < 7; i++)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(qWallStraight);
                    w.transform.SetParent(wallsRoot.transform, false);
                    w.transform.localPosition = new Vector3(-44f, 0f, -30f + i * 10f);
                    w.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    w.name = $"QWall_West_{i}";
                }
                for (int i = 0; i < 7; i++)
                {
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(qWallStraight);
                    w.transform.SetParent(wallsRoot.transform, false);
                    w.transform.localPosition = new Vector3(44f, 0f, -30f + i * 10f);
                    w.transform.localRotation = Quaternion.Euler(0, -90, 0);
                    w.name = $"QWall_East_{i}";
                }
            }

            // Main South Gate + Drawbridge (connection to OuterWorld)
            if (gateMedium != null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(gateMedium);
                g.transform.SetParent(wallsRoot.transform, false);
                g.transform.localPosition = new Vector3(0, 0, -50);
                g.name = "MainGate_South_ToOuterWorld";
            }
            if (drawbridge != null)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(drawbridge);
                d.transform.SetParent(wallsRoot.transform, false);
                d.transform.localPosition = new Vector3(0, 0, -58);
                d.name = "Drawbridge_Approach";
            }

            // === CENTRAL COURTYARD / PLAZA (mobile-friendly open space) ===
            var courtyard = new GameObject("CentralCourtyard_Plaza");
            courtyard.transform.SetParent(root.transform, false);

            // Simple ground/plaza floor (Quaternius wood or poly stone brick)
            if (qFloorWood != null)
            {
                for (int x = -2; x <= 2; x++)
                {
                    for (int z = -2; z <= 2; z++)
                    {
                        var f = (GameObject)PrefabUtility.InstantiatePrefab(qFloorWood);
                        f.transform.SetParent(courtyard.transform, false);
                        f.transform.localPosition = new Vector3(x * 8f, 0.01f, z * 8f);
                        f.name = $"CourtyardFloor_{x}_{z}";
                    }
                }
            }
            else
            {
                // Fallback visual plane
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.transform.SetParent(courtyard.transform, false);
                plane.transform.localScale = new Vector3(8, 1, 8);
                plane.name = "Courtyard_PlazaFallback";
                Object.DestroyImmediate(plane.GetComponent<Collider>());
            }

            // === THE 8 STRUCTURES (ring around plaza, storefronts face center, NPC points) ===
            var structuresRoot = new GameObject("The8Structures_Storefronts_NPCPoints");
            structuresRoot.transform.SetParent(courtyard.transform, false);

            // Exact 8 per spec, positions in a pleasant octagon-ish ring for clear mobile nav + thumb reach
            var structures = new List<(GameObject prefab, string displayName, Vector3 localPos)>
            {
                (houseMed,   "Blacksmith_Weapons_Storefront",   new Vector3(-22, 0, -22)),
                (watermill,  "Lumbermill_Wood_Storefront",      new Vector3( 22, 0, -22)),
                (windmill,   "Windmill_Food_Storefront",        new Vector3(-22, 0,  22)),
                (stables,    "EchoHollow_Pets_RoamingArea",     new Vector3( 22, 0,  22)),
                (houseMed,   "Forge_Armor_Storefront",          new Vector3(-32, 0,   0)),
                (towerBig,   "ArcaneTower_MagicUpgrades",       new Vector3( 32, 0,   0)),
                // Jeweler (Gems) REMOVED from the fixed ring — it was blocking the south door.
                // It is now a player-PLACEABLE build-catalog entry (id "jeweler" in
                // structures-catalog.json, behaviorId GameplayBuilding); the owner lays it
                // wherever she wants via build mode. Do NOT re-add a fixed Jeweler here.
                (houseLarge, "Marketplace_Monetization",        new Vector3(  0, 0,  32)),
            };

            for (int i = 0; i < structures.Count; i++)
            {
                var (prefab, name, pos) = structures[i];
                if (prefab == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(structuresRoot.transform, false);
                inst.transform.localPosition = pos;
                inst.name = name;

                // Front-of-building NPC / storefront interact point (wire with existing Yarn/Economy/NPC systems)
                var npc = new GameObject($"NPC_{name.Split('_')[0]}_Interactable");
                npc.transform.SetParent(inst.transform, false);
                npc.transform.localPosition = new Vector3(0, 0, 6); // front offset; rotate building if needed for facing

                // Optional crate/vine dressing for storefront vibe (Quaternius)
                if (qCrate != null && i % 2 == 0)
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(qCrate);
                    c.transform.SetParent(inst.transform, false);
                    c.transform.localPosition = new Vector3(3, 0, 4);
                    c.name = "StorefrontCrate";
                }
                if (qVine1 != null)
                {
                    var v = (GameObject)PrefabUtility.InstantiatePrefab(qVine1);
                    v.transform.SetParent(inst.transform, false);
                    v.transform.localPosition = new Vector3(-2.5f, 2, 0);
                    v.name = "StorefrontVine";
                }

                Debug.Log($"[CastleHubBuilder] Placed {name} + NPC interact point.");
            }

            // === MAIN KEEP + PLAYER HOME (2 levels: ground hall + upper private quarters) ===
            var keepRoot = new GameObject("MainKeep_CastleWithTwoLevels_Home");
            keepRoot.transform.SetParent(root.transform, false);

            if (castleCore != null)
            {
                var keep = (GameObject)PrefabUtility.InstantiatePrefab(castleCore);
                keep.transform.SetParent(keepRoot.transform, false);
                keep.transform.localPosition = Vector3.zero;
                keep.name = "GroundLevel_Keep_Hall_Entry";
            }

            // Labeled home space (ground + upper access). Dress further with Quaternius floors/walls + KayKit furniture bits.
            var home = new GameObject("PlayerHeroHall_PersonalQuarters_HomeSpace");
            home.transform.SetParent(keepRoot.transform, false);
            home.transform.localPosition = new Vector3(0, 0, -12);

            // Hero start point inside the personal quarters — this becomes the project start location
            // when we move the entry point from Village2 to the Castle Hub.
            var heroStart = new GameObject("HeroStartPoint_InsidePersonalQuarters");
            heroStart.transform.SetParent(home.transform, false);
            heroStart.transform.localPosition = new Vector3(0, 0.1f, 0);
            heroStart.transform.localRotation = Quaternion.Euler(0, 0, 0);

            // === UPPER BATTLEMENTS (wide platform, defensive towers, LOS to courtyard below) ===
            var upper = new GameObject("UpperBattlements_SecondLevel_Defenses_LOS");
            upper.transform.SetParent(keepRoot.transform, false);
            upper.transform.localPosition = new Vector3(0, 11f, 0);

            // Wide flat platform for player base-building defenses (existing PlacementGrid + BuildModeController)
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.transform.SetParent(upper.transform, false);
            platform.transform.localPosition = Vector3.zero;
            platform.transform.localScale = new Vector3(44, 0.8f, 44);
            platform.name = "BattlementsPlatform_Wide42m_ForPlayerTowers_LOS_DownToCourtyard";
            Object.DestroyImmediate(platform.GetComponent<Collider>());

            // Crenellated feel + Quaternius modular walls around edge
            if (qWallStraight != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    var bw = (GameObject)PrefabUtility.InstantiatePrefab(qWallStraight);
                    bw.transform.SetParent(upper.transform, false);
                    bw.transform.localPosition = new Vector3(-22 + i * 5.5f, 1.2f, -23);
                    bw.name = $"BattlementWall_South_{i}";
                }
                for (int i = 0; i < 9; i++)
                {
                    var bw = (GameObject)PrefabUtility.InstantiatePrefab(qWallStraight);
                    bw.transform.SetParent(upper.transform, false);
                    bw.transform.localPosition = new Vector3(-22 + i * 5.5f, 1.2f, 23);
                    bw.name = $"BattlementWall_North_{i}";
                }
            }

            // 4 upper defensive towers (space + pre-placed for LOS defense)
            if (towerBig != null)
            {
                Vector3[] utPos = {
                    new Vector3(-18, 1.5f, -18), new Vector3(18, 1.5f, -18),
                    new Vector3(-18, 1.5f, 18),  new Vector3(18, 1.5f, 18)
                };
                for (int i = 0; i < utPos.Length; i++)
                {
                    var ut = (GameObject)PrefabUtility.InstantiatePrefab(towerBig);
                    ut.transform.SetParent(upper.transform, false);
                    ut.transform.localPosition = utPos[i];
                    ut.name = $"UpperDefensiveTower_{i+1}_PlayerBuildLOS";
                }
            }

            // === GRAND STAIR (WO-384) — the REAL climbable nav path, courtyard → upper battlements ===
            // Replaces both the old single MainStairs_Poly prefab AND the invisible UpperRamp_Nav.
            // Built via the shared, reusable DeNelle.Village.StairwayBuilder (composition, not a
            // one-off): a WIDE curved sweep of real step pieces (MeshColliders ON) that the
            // NavMeshSurface bakes directly as the walkable surface. Fit-to-bounds from the measured
            // courtyard anchor (y≈0) to the upper-battlement EDGE so the top tread lands FLUSH and
            // OVERLAPS UpperBattlements_Nav (the upper tier is one plane whose cube collider is
            // destroyed — the stair top must overlap that nav plane or the bake won't fuse).
            BuildGrandStair(root.transform);

            if (qStairsExt != null)
            {
                var qs = (GameObject)PrefabUtility.InstantiatePrefab(qStairsExt);
                qs.transform.SetParent(upper.transform, false);
                qs.transform.localPosition = new Vector3(-2, 0, -20);
                qs.name = "QuaterniusStairs_UpperAccess";
            }

            // === BEAUTY + DEFENSIVE DRESSING (vines, crates, balconies from Quaternius) ===
            if (qVine1 != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    var v = (GameObject)PrefabUtility.InstantiatePrefab(qVine1);
                    v.transform.SetParent(wallsRoot.transform, false);
                    v.transform.localPosition = new Vector3(-38 + i * 11f, 4f, -40);
                    v.name = $"VineDecor_{i}";
                }
            }
            if (qBalcony != null)
            {
                // A few balcony accents on keep/upper for "home" verticality
                var b = (GameObject)PrefabUtility.InstantiatePrefab(qBalcony);
                b.transform.SetParent(keepRoot.transform, false);
                b.transform.localPosition = new Vector3(8, 6, -8);
                b.name = "KeepBalcony_HomeAccent";
            }

            // === CONNECTION TO OUTERWORLD (NavMesh seam + player transition) ===
            // South gate seam between Castle Hub (home) and OuterWorld regions.
            // - NavMeshLink: AI can path across when both scenes additive.
            // - SceneTransitionTrigger: on player cross, ensures OuterWorld loaded additive + teleports hero to target pos.
            // Alignment tip: When loading CastleHub additive, position its root so the marker's world pos
            // matches the desired "castle approach" point in OuterWorld (e.g. place OuterWorld root at (0,0,0)
            // and set Castle root offset, or vice versa). The marker local pos is south of hub.
            var gateMarker = new GameObject("WorldGate_ConnectToOuterWorld_Marker");
            gateMarker.transform.SetParent(root.transform, false);
            gateMarker.transform.localPosition = new Vector3(0, 0, -68);

            // Actually wire the components (NavMeshLink + trigger with the new SceneTransitionTrigger).
            // Also exposed via the one-off menu for already-saved scenes (e.g. your MainCastle_Hall).
            WireOuterWorldConnection(gateMarker);

            // === ROAMING / AMBIENCE (Bella + NPCs) ===
            var ambience = new GameObject("RoamingNPCs_Animals_Bella_Ambience");
            ambience.transform.SetParent(root.transform, false);
            // Populate later with People_M / Animals_M from polyperfect or existing injectors (StoryCompanionInjector etc.).
            // Echo Hollow (stables) gets extra radius for pet roaming.

            // === LIGHTING / PERF HINTS (do not add heavy lights here — use scene lighting) ===
            // All pieces are low-poly single-atlas or URP ShaderGraph → excellent batching.
            // After placing: mark static where appropriate, add light probes / reflection probes for upper/lower contrast.

            // Invisible, continuous walkable NavMesh floor (interior + gate bridge) so the
            // NavMeshAgent hero can traverse the WHOLE castle and cross the gate. The visual
            // qFloorWood tiles only cover the central ~±16 plaza; this fills the rest.
            BuildNavMeshFloor(root.transform);

            Debug.Log("[CastleHubBuilder] CastleHubRoot complete. 8 structures + keep + upper battlements + gate marker placed.\n" +
                      "Next: Save under Assets/Scenes/ (e.g. MainCastle_Hall.unity), Bake NavMesh (NavMeshSurface recommended), wire NPC points + connection via existing systems (WorldSceneLoader, Economy, Yarn, base-build on battlements).");
            Selection.activeGameObject = root;
        }

        // --- Helpers (graceful loading like VillageSceneBuilder pattern) ---
        private static GameObject LoadPoly(string fileName)
        {
            string path = PolyRoot + fileName;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogWarning($"[CastleHubBuilder] Missing polyperfect prefab (using placeholder behavior): {path}");
            }
            return go;
        }

        private static GameObject LoadQuat(string relative)
        {
            string path = QuatRoot + relative;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogWarning($"[CastleHubBuilder] Missing Quaternius prefab: {path}");
            }
            return go;
        }

        // =====================================================================
        //  Invisible walkable NavMesh floor — ONE continuous collider surface so the
        //  NavMeshAgent hero can traverse the WHOLE castle and cross the gate. The
        //  visual qFloorWood tiles only cover the central ~±16 plaza, leaving the rest
        //  of the ±44 interior + the gate/drawbridge with no walkable ground -> the
        //  "fragmented islands" + uncrossable gate. These planes are renderer-OFF
        //  (invisible) but keep their MeshCollider, so the NavMeshSurface bakes them
        //  when Use Geometry = Physics Colliders. Generated, not hand-placed, so a
        //  rebuild reproduces the walkable floor every time.
        // =====================================================================
        private static void BuildNavMeshFloor(Transform parent)
        {
            // Idempotent: drop any prior generated floor first.
            var priorFloor = GameObject.Find(NavFloorName);
            if (priorFloor != null) Object.DestroyImmediate(priorFloor);

            var floorRoot = new GameObject(NavFloorName);
            floorRoot.transform.SetParent(parent, false);

            // Main interior floor: a Unity Plane is 10x10m at scale 1, so scale 9 = 90x90m,
            // covering the full -45..+45 interior (corner towers ±42, walls ±44). y sits just
            // above the visual tiles (0.01) and below the hero spawn (0.1).
            CreateInvisibleFloor(floorRoot.transform, "CourtyardFloor_Nav", new Vector3(0f, 0.05f, 0f), new Vector3(9f, 1f, 9f));

            // Gate bridges: ONE oriented walkable strip CENTERED ON EACH of the 4 recipe gate
            // openings (S/W/N/E), spanning from the courtyard THROUGH the opening and >=10m OUT
            // onto the OuterWorld terrain. The gates are no longer at fixed (0,0,-50): the owner
            // re-authored them via Resources/Data/castle-south-recipe.json (south gate ~(-4.37,
            // 0,-40.6)) mirrored 90/180/270 around world origin (same math as CastleWallsFromRecipe).
            // A single hardcoded (0,0,-51) bridge missed the real opening (x off by ~4.4m) and never
            // covered W/N/E at all -> uncrossable gates. Drive the strips off the recipe so a regen
            // stays correct even if the owner re-authors the gates.
            BuildGateExitStrips(floorRoot.transform);

            // Keep interior + entrance bridge: GroundLevel_Keep_Hall_Entry (the keep building)
            // sits at origin with 4 wall colliders + a doorway. The walls carve the navmesh and
            // seal the spawn hall (hero spawns inside, can't exit). Fill the keep footprint with
            // floor, RAISED slightly (y 0.12) to sit above any door threshold/step the walls cut,
            // so the interior is walkable AND threads out through the entrance to the courtyard.
            // ~26x26m covers the keep + overlaps the courtyard floor on every side.
            CreateInvisibleFloor(floorRoot.transform, "KeepInterior_Nav", new Vector3(0f, 0.12f, 0f), new Vector3(2.6f, 1f, 2.6f));

            // LEVEL 2 — upper battlements (y~11.4). The platform cube's collider is destroyed,
            // so without this there is NO navmesh up there. ~44x44 plane over the platform.
            CreateInvisibleFloor(floorRoot.transform, "UpperBattlements_Nav", new Vector3(0f, 11.5f, 0f), new Vector3(4.4f, 1f, 4.4f));

            // NOTE (WO-384): the old invisible UpperRamp_Nav (a hidden 36° ramp) was REMOVED.
            // The two levels are now connected by the REAL, visible grand stair built via
            // StairwayBuilder (BuildGrandStair) — its step MeshColliders bake as the climb, with
            // a chord NavMeshLink as the backup. No hidden-ramp-under-cosmetic-stairs mismatch.
        }

        // =====================================================================
        //  GATE EXIT STRIPS — recipe-driven walkable nav strips through all 4 gates.
        // -----------------------------------------------------------------------------
        //  Reads the SAME recipe CastleWallsFromRecipe builds the walls from
        //  (Resources/Data/castle-south-recipe.json) to find the SOUTH gate opening,
        //  then mirrors it 90/180/270 around world origin (identical rotation math:
        //  worldPos = Quaternion.Euler(0,angle,0) * southLocalPos, parent at origin) to
        //  get the W/N/E gate openings. For EACH gate it drops one invisible, renderer-off
        //  walkable plane (MeshCollider kept) oriented so its long axis points OUTWARD
        //  through the wall: it starts INSIDE the courtyard (overlapping the main
        //  CourtyardFloor_Nav so the two fuse), passes THROUGH the gate opening, and
        //  extends >=10m OUT onto the OuterWorld terrain so spawn/load/blend + the
        //  NavMeshLink endpoint land on walkable mesh. Parameterized off the recipe so a
        //  re-author of the gates keeps the strips aligned automatically.
        //
        //  Strip footprint (per gate, in the gate's own outward frame BEFORE rotation):
        //   • width  (across the opening) ~12m  — matches the medium gate clear span
        //   • length (courtyard -> out)   ~26m  — ~8m inside the wall to overlap the
        //     courtyard floor + ~18m outward (well past the >=10m requirement) onto terrain.
        //  A Unity Plane is 10x10m at scale 1, centered on its transform, so we size it
        //  via localScale and rotate it by the side angle so the length axis runs radially
        //  outward from origin.
        // =====================================================================
        private const string GateStripRootName = "GateExitStrips_Nav";

        private struct GatePose { public Vector3 worldPos; public float yaw; public string label; }

        private static void BuildGateExitStrips(Transform parent)
        {
            // Recipe south gate (local under a parent at origin -> world == local).
            Vector3 southGate = ReadSouthGatePos();

            // 4 sides: south is the authored side (0deg), W/N/E are it rotated around origin.
            // Match CastleWallsFromRecipe: world = Quaternion.Euler(0,angle,0) * southPos.
            var poses = new[]
            {
                MakeGatePose(southGate,   0f,   "South"),
                MakeGatePose(southGate,  90f,   "West"),
                MakeGatePose(southGate, 180f,   "North"),
                MakeGatePose(southGate, 270f,   "East"),
            };

            const float halfWidth   = 6f;   // 12m across the opening
            const float insideReach = 8f;   // overlap courtyard floor (inside the wall)
            const float outsideReach = 18f; // >=10m out onto terrain
            float length = insideReach + outsideReach; // 26m total along the radial axis
            float centerOffset = (outsideReach - insideReach) * 0.5f; // shift strip center outward

            foreach (var g in poses)
            {
                // Outward direction in world XZ for this side (south faces -Z at yaw 0).
                Quaternion yawRot = Quaternion.Euler(0f, g.yaw, 0f);
                Vector3 outward = yawRot * Vector3.back; // south(-Z) rotated to this side

                // Center the strip between the courtyard-overlap end and the terrain end.
                Vector3 center = g.worldPos + outward * centerOffset;
                center.y = 0.05f; // same plane as CourtyardFloor_Nav so they fuse

                // Plane is 10m square at scale 1: X = width axis, Z = length axis. The
                // length axis must run OUTWARD (along 'outward'); a plane's local +Z maps to
                // world via the yaw rotation, and at yaw 0 local +Z = world +Z while outward
                // = world -Z, so the |length| sizing is symmetric and orientation-agnostic.
                var strip = GameObject.CreatePrimitive(PrimitiveType.Plane);
                strip.name = $"GateExit_{g.label}_Nav";
                strip.transform.SetParent(parent, false);
                strip.transform.position = center;
                strip.transform.rotation = yawRot; // align the strip's length axis radially
                strip.transform.localScale = new Vector3(halfWidth * 2f / 10f, 1f, length / 10f);

                var r = strip.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
                if (strip.GetComponent<MeshCollider>() == null) strip.AddComponent<MeshCollider>();
            }
        }

        // Build a gate pose by rotating the authored south gate around world origin (parent at 0).
        private static GatePose MakeGatePose(Vector3 southGate, float angle, string label)
        {
            Vector3 world = Quaternion.Euler(0f, angle, 0f) * southGate;
            return new GatePose { worldPos = world, yaw = angle, label = label };
        }

        // Read the SOUTH gate position from the recipe (the SAME data the walls are built from),
        // so the nav strips track any re-author. Falls back to the captured default if absent.
        private static Vector3 ReadSouthGatePos()
        {
            Vector3 fallback = new Vector3(-4.37f, 0f, -40.6f);
            var ta = Resources.Load<TextAsset>("Data/castle-south-recipe");
            if (ta == null)
            {
                Debug.LogWarning("[CastleHubBuilder] castle-south-recipe not found — using fallback south gate " + fallback);
                return fallback;
            }

            // Reuse the recipe shape (same fields CastleWallsFromRecipe parses).
            var recipe = JsonUtility.FromJson<GateRecipe>(ta.text);
            if (recipe != null && recipe.pieces != null)
            {
                foreach (var p in recipe.pieces)
                {
                    if (p != null && p.name == "Gate_South" && p.pos != null && p.pos.Length == 3)
                        return new Vector3(p.pos[0], p.pos[1], p.pos[2]);
                }
            }
            Debug.LogWarning("[CastleHubBuilder] Gate_South not found in recipe — using fallback " + fallback);
            return fallback;
        }

        [System.Serializable] private class GatePiece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [System.Serializable] private class GateRecipe { public GatePiece[] pieces; public float[] parentPos; public float[] parentRot; }

        // =====================================================================
        //  GRAND STAIR (WO-384) — consume the reusable DeNelle.Village.StairwayBuilder
        //  to build the REAL climbable nav path from the courtyard up to the upper
        //  battlement EDGE. Replaces the old MainStairs_Poly prefab + the invisible
        //  UpperRamp_Nav. Cross-assembly (Editor cannot reference DeNelle.Village at
        //  compile time) so we invoke the builder via reflection — same pattern as
        //  FindType/WireOuterWorldConnection. Composition, not copy-paste.
        // -----------------------------------------------------------------------------
        //  ANCHORS (world space; root is at origin):
        //   • Start  = courtyard, east of the keep, y≈0  → (16, 0, 0)
        //   • End    = upper-battlement EDGE on the east side, y≈11.5. The platform /
        //              UpperBattlements_Nav plane spans ±22 around the keep origin, so the
        //              east edge is x≈+22. We aim the top tread at (21, 11.5, 0) so it
        //              sits ON / OVERLAPPING the nav plane (not past it, not short of it).
        //  The builder fits step count + per-step rise to the measured 11.5 m climb, keeps
        //  a ≥8 m walkable band (width 9, radius 12 → no inner pinch), and drops a chord
        //  NavMeshLink (start→end) as the reliability backup. NO runtime bake — the castle
        //  re-bakes its persisted NavMeshSurface via BatchAddFloorAndBakeCastle.
        // =====================================================================
        private static void BuildGrandStair(Transform parent)
        {
            Vector3 start = new Vector3(16f, 0f, 0f);    // courtyard, east of the keep
            Vector3 end   = new Vector3(21f, 11.5f, 0f); // upper-battlement east edge — overlaps UpperBattlements_Nav

            var builderType = FindType("DeNelle.Village.StairwayBuilder");
            if (builderType == null)
            {
                Debug.LogWarning("[CastleHubBuilder] DeNelle.Village.StairwayBuilder not found (is it compiled?). " +
                                 "Grand stair NOT built — the two levels will have no walkable connection until it compiles. " +
                                 "Re-run Build CastleHub after the script compiles.");
                return;
            }

            // Params is a nested struct: DeNelle.Village.StairwayBuilder+Params. Build it via the
            // CastleGrandStair(start, end) static factory so all WO-384 defaults (wide curved sweep,
            // fit-to-bounds, colliders on, railings, chord link) are applied in one place.
            var paramsType = builderType.GetNestedType("Params");
            object prms = null;
            if (paramsType != null)
            {
                var factory = paramsType.GetMethod("CastleGrandStair",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (factory != null)
                    prms = factory.Invoke(null, new object[] { start, end });
            }
            if (prms == null)
            {
                Debug.LogWarning("[CastleHubBuilder] Could not construct StairwayBuilder.Params.CastleGrandStair via reflection. Grand stair NOT built.");
                return;
            }

            var build = builderType.GetMethod("Build",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (build == null)
            {
                Debug.LogWarning("[CastleHubBuilder] StairwayBuilder.Build(Transform,string,Params) not found via reflection. Grand stair NOT built.");
                return;
            }

            build.Invoke(null, new object[] { parent, "GrandStair_CourtyardToBattlements", prms });
            Debug.Log("[CastleHubBuilder] Grand stair built via StairwayBuilder (courtyard → upper-battlement edge, " +
                      "top tread flush at (21,11.5,0) overlapping UpperBattlements_Nav). Re-bake to walk it.");
        }

        private static void CreateInvisibleFloor(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); // Plane = MeshFilter + MeshRenderer + MeshCollider
            plane.name = name;
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = localPos;
            plane.transform.localScale = localScale;

            // Invisible but bakeable: hide the renderer; the NavMesh bakes from the MeshCollider
            // (Use Geometry = Physics Colliders), so visibility doesn't matter to the bake.
            var r = plane.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
            if (plane.GetComponent<MeshCollider>() == null) plane.AddComponent<MeshCollider>();
        }

        // Non-destructive: add ONLY the invisible floor to whatever castle scene is open,
        // without rebuilding the whole hub (preserves the wired hero/camera/gate). Run this,
        // then set the NavMeshSurface Use Geometry = Physics Colliders and Bake.
        [MenuItem("Defenders/Scenes/Add NavMesh Floor to Current Castle")]
        public static void AddNavMeshFloorToCurrentCastle()
        {
            var root = GameObject.Find(RootName);
            Transform parent;
            if (root != null) { parent = root.transform; }
            else
            {
                var holder = new GameObject(RootName + "_FloorHost");
                parent = holder.transform;
                Debug.LogWarning("[CastleHubBuilder] No CastleHubRoot found — floor added under a new host object.");
            }
            BuildNavMeshFloor(parent);
            Debug.Log("[CastleHubBuilder] Added invisible NavMesh floor (interior + gate bridge). " +
                      "NEXT: select your NavMeshSurface, set Use Geometry = Physics Colliders, then Bake. " +
                      "The blue should become ONE connected sheet through the gate.");
            Selection.activeGameObject = parent.gameObject;
        }

        // =====================================================================
        //  OuterWorld connection wiring (NavMeshLink + transition trigger)
        //  Called from BuildCastleHub and exposed as a menu for existing scenes
        //  (e.g. open MainCastle_Hall.unity then run the menu to wire without rebuild).
        // =====================================================================

        [MenuItem("Defenders/Scenes/Wire Current Castle to OuterWorld")]
        public static void WireCurrentCastleToOuterWorld()
        {
            var marker = GameObject.Find("WorldGate_ConnectToOuterWorld_Marker");
            if (marker == null)
            {
                Debug.LogError("[CastleHubBuilder] Could not find 'WorldGate_ConnectToOuterWorld_Marker' in the current scene. " +
                               "Run the main Build CastleHub first (or create the marker manually at your south gate).");
                return;
            }

            WireOuterWorldConnection(marker);
            Debug.Log("[CastleHubBuilder] Wired current scene's gate marker to OuterWorld (NavMeshLink + transition trigger). " +
                      "Make sure 'OuterWorld' is in Build Settings. Align world positions of the two scenes for seam to match.");
            Selection.activeGameObject = marker;
        }

        /// <summary>
        /// Adds a NavMeshLink across the gate for AI pathing + a trigger that loads OuterWorld
        /// additively and moves the player (uses the SceneTransitionTrigger we created to match WorldSceneLoader patterns).
        /// Reflection is used for the custom component (Editor asm cannot directly reference DeNelle.Village).
        /// </summary>
        private static void WireOuterWorldConnection(GameObject gateMarker)
        {
            if (gateMarker == null) return;

            // 0. IDEMPOTENCY — this is called on every wire/build (safe to call again). Without a
            //    cleanup pass it piles up duplicate NavMeshLinks + OuterWorldTransitionTriggers on
            //    each run (3 triggers were found triple-firing the scene transition). Strip any prior
            //    wiring first so we always end with EXACTLY one link + one trigger.
            foreach (var t in gateMarker.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t != gateMarker.transform && t.name == "OuterWorldTransitionTrigger")
                    Object.DestroyImmediate(t.gameObject);
            }
            var priorNavType = System.Type.GetType("Unity.AI.Navigation.NavMeshLink, Unity.AI.Navigation");
            if (priorNavType != null)
            {
                foreach (var oldLink in gateMarker.GetComponents(priorNavType))
                    if (oldLink != null) Object.DestroyImmediate(oldLink);
            }

            // 1. NavMeshLink — bridges the navmesh seam when Castle + OuterWorld are both loaded additive.
            //    Tune start/end/width to the actual gate opening width (Quaternius/poly gate ~12-15m).
            // Use reflection / SerializedObject to add the component and configure it without a hard
            // compile-time reference to the type (the AI Navigation package may not be in the Editor asmdef
            // references in a way that resolves the type for all build contexts). This matches the project's
            // pattern for optional/cross-package Editor scripting.
            var navType = System.Type.GetType("Unity.AI.Navigation.NavMeshLink, Unity.AI.Navigation");
            if (navType != null)
            {
                var comp = gateMarker.AddComponent(navType);
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    // The new Unity.AI.Navigation.NavMeshLink serializes as m_StartPoint/m_EndPoint/m_Width
                    // (NOT the legacy startPoint/endPoint/width) — using the wrong names silently left a
                    // zero-width, mis-oriented link. Try m_-prefixed first, fall back to the legacy names.
                    var p = so.FindProperty("m_StartPoint") ?? so.FindProperty("startPoint"); if (p != null) p.vector3Value = new Vector3(-7f, 0f, -1f);
                    p = so.FindProperty("m_EndPoint") ?? so.FindProperty("endPoint"); if (p != null) p.vector3Value = new Vector3(7f, 0f, -1f);
                    p = so.FindProperty("m_Width") ?? so.FindProperty("width"); if (p != null) p.floatValue = 12f;
                    p = so.FindProperty("m_Bidirectional") ?? so.FindProperty("bidirectional"); if (p != null) p.boolValue = true;
                    p = so.FindProperty("m_Area") ?? so.FindProperty("area"); if (p != null) p.intValue = 0;
                    so.ApplyModifiedProperties();
                }
            }
            else
            {
                Debug.LogWarning("[CastleHubBuilder] Could not resolve NavMeshLink type at runtime. Install the 'AI Navigation' package (Window > Package Manager) and ensure the Editor asmdef references 'Unity.AI.Navigation' for seamless multi-scene NavMesh.");
            }

            // 2. Transition trigger (player crosses the gate).
            //    Box covers the gate area. On enter (Player/HeroTarget tag) it ensures OuterWorld is loaded
            //    and teleports the player to the corresponding spot in OuterWorld world space.
            var triggerGo = new GameObject("OuterWorldTransitionTrigger");
            triggerGo.transform.SetParent(gateMarker.transform, false);
            // Sit the trigger slightly NORTH of the gate marker (toward the reachable courtyard),
            // not 2m south of it — the hero stops at the navmesh edge well short of the marker.
            triggerGo.transform.localPosition = new Vector3(0, 1.5f, 6f);

            var col = triggerGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(16f, 6f, 14f);

            // Add the transition behaviour via reflection (matches MineNode / other builder patterns).
            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType != null)
            {
                var comp = triggerGo.AddComponent(transType);

                // Configure via reflection (public fields on the trigger script).
                var fScene = transType.GetField("targetSceneName");
                if (fScene != null) fScene.SetValue(comp, "OuterWorld");

                // Target position in OuterWorld space — adjust this to where the "castle approach" lives
                // in your OuterWorld layout (e.g. a road south of the Village area or a custom entrance).
                // When the Castle root is at (0,0,0), this is the world pos the player appears at after crossing south.
                var fPos = transType.GetField("targetPosition");
                if (fPos != null) fPos.SetValue(comp, new Vector3(0f, 0.5f, -80f));

                var fAdditive = transType.GetField("loadAdditive");
                if (fAdditive != null) fAdditive.SetValue(comp, true);

                // Generous proximity so it fires as the hero APPROACHES the south gate — the hero
                // stops at the navmesh edge ~10-18m short of the marker, so the default 6m never fired.
                var fRadius = transType.GetField("ProximityRadius");
                if (fRadius != null) fRadius.SetValue(comp, 18f);
            }
            else
            {
                Debug.LogWarning("[CastleHubBuilder] DeNelle.Village.SceneTransitionTrigger not found (is the script compiled?). " +
                                 "Trigger collider was still added — you can attach the behaviour manually or re-run after compile.");
            }

            Debug.Log("[CastleHubBuilder] Wired gate marker with NavMeshLink + SceneTransitionTrigger (target OuterWorld at ~ (0, 0.5, -80)).");
        }

        /// <summary>
        /// Cross-assembly type lookup (same pattern used by OuterWorldBuilder / VillageSceneBuilder).
        /// </summary>
        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        // =====================================================================
        //  Make Castle the primary project start (move from Village2)
        //  + wire hero, camera, defaults, OuterWorld streaming (via generalized loader),
        //  and other core items (gate already wired by previous step).
        //
        //  Run on your saved MainCastle_Hall.unity (or any Castle scene) while it is open.
        //  Reuses Village2Playable (same Editor assembly) for hero + camera setup.
        //  This + the updated WorldSceneLoader means playing the Castle scene now streams
        //  OuterWorld, spawns you in the personal quarters, and gives full controls.
        // =====================================================================

        [MenuItem("Defenders/Scenes/Make CastleHub Primary Start (current scene) + Wire Everything")]
        public static void MakeCastleHubPrimaryStartAndWire()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene == null || !scene.path.Contains("Castle") && !scene.path.Contains("MainCastle"))
            {
                Debug.LogWarning("[CastleHubBuilder] Open a Castle Hub scene (MainCastle_Hall or similar) first.");
                return;
            }

            Log("=== Make CastleHub Primary Start + Full Wiring START ===");

            // 1. Scene defaults (camera with SmartMobileCamera, light, ambient/fog) + EventSystem.
            //    Idempotent.
            Village2Playable.AddSceneDefaultsToActiveScene();
            Village2Playable.ImportEventSystem();
            Log("Scene defaults + EventSystem wired.");

            // 2. Find a good parent for the hero: prefer the personal quarters / home space inside the keep.
            Transform heroParent = FindHeroParentInCastle(scene);
            if (heroParent == null)
            {
                heroParent = scene.GetRootGameObjects().FirstOrDefault(r => r.name.Contains("Castle") || r.name.Contains("Root"))?.transform;
            }
            if (heroParent == null && scene.rootCount > 0)
                heroParent = scene.GetRootGameObjects()[0].transform;

            // 3. Import the hero (Mage rig by default, with locomotion, abilities, gear, body swapper, tagged Player).
            //    No Heart for hub (castle is safe home, not the defend-the-tower core).
            GameObject hero = Village2Playable.ImportHero(heroParent, heart: null);
            if (hero == null)
            {
                Err("ImportHero returned null. Check that Village2Playable and hero assets are present.");
                return;
            }
            Log("Hero imported (locomotion + abilities + visuals).");

            // 4. Place the hero inside the personal quarters (use the HeroStartPoint we placed during build, or fallback).
            PlaceHeroInCastleHome(scene, hero);

            // 5. Wire the smart camera to follow the hero.
            Village2Playable.WireCameraTargetToHero(hero);
            Log("Camera wired to hero.");

            // 5b. Disable the legacy VillageCamera so SmartMobileCamera is the SOLE follower.
            //     Both enabled lets VillageCamera's top-down offset (0,8.5,-6.5) bleed through over
            //     SmartMobileCamera's close 3rd-person (0,2.6,-4.5). Reflection by type-name avoids
            //     an asmdef dependency on the Village type from this Editor assembly.
            var mainCamForLegacy = Camera.main;
            if (mainCamForLegacy != null)
            {
                foreach (var comp in mainCamForLegacy.GetComponents<MonoBehaviour>())
                {
                    if (comp != null && comp.GetType().Name == "VillageCamera" && comp.enabled)
                    {
                        comp.enabled = false;
                        Log("VillageCamera (legacy top-down) DISABLED — SmartMobileCamera is sole follower.");
                    }
                }
            }

            // 6. Ensure HeroLocomotion is enabled (HeroControlEnsurer is often hardcoded to "Village2").
            EnsureHeroControllable(hero);

            // 7. Make sure the OuterWorld gate connection is wired (NavMeshLink + transition trigger).
            //    Safe to call again.
            var gateMarker = GameObject.Find("WorldGate_ConnectToOuterWorld_Marker");
            if (gateMarker != null)
            {
                WireOuterWorldConnection(gateMarker);
            }
            else
            {
                Log("No gate marker found — run the Build or Wire menu first for OuterWorld connection.");
            }

            // 8. Mark dirty + save reminder.
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Log("=== CASTLE IS NOW THE PRIMARY START ===");
            Log("Open/play this scene directly. Hero spawns in the personal quarters.");
            Log("OuterWorld will stream in additively (via updated WorldSceneLoader).");
            Log("South gate is wired for seamless transition to the open world.");
            Log("Other items (NPC points in the 8 structures, build on upper battlements, Echo Hollow pets area) are present — wire specific Yarn/Economy/Pet systems as needed.");
            Log("Save the scene. Add it (and OuterWorld) to Build Settings if not already.");

            Selection.activeGameObject = hero;
        }

        // =====================================================================
        //  Batchmode entry — open MainCastle_Hall, wire it as primary start, SAVE.
        //  Runs the interactive "Make CastleHub Primary Start" pass headless so the
        //  scene comes back ready to Play (hero on the spawn marker + follow camera +
        //  controls + OuterWorld gate). Invoked via run-unity-method.ps1. No dialogs.
        // =====================================================================
        public static void BatchWireCastleAndSave()
        {
            const string scenePath = "Assets/Scenes/MainCastle_Hall.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Log("BATCH: opened " + scenePath);

            // Promote the owner's placeholder capsule to the canonical spawn marker so the
            // NavMeshAgent hero spawns on the verified (first-floor) NavMesh — not the upper
            // quarters anchor, which may sit off-mesh. STRIP the visible mesh so it's an
            // invisible transform-only marker (keep the GameObject + its transform/function).
            // Disabling the renderer alone proved fragile (the pill reappeared in a live
            // build); remove the MeshRenderer + MeshFilter outright and destroy the collider
            // so it can never render OR block the hero. The transform stays as the marker.
            // Catch the marker whether it's still the raw primitive ("Capsule") OR was already
            // promoted in a prior pass (its capsule mesh was renamed but never stripped — the
            // lingering pill). Either way we re-strip the visible mesh below.
            var pill = GameObject.Find("Capsule") ?? GameObject.Find("HeroStartPoint_PlayerSpawn");
            if (pill != null)
            {
                pill.name = "HeroStartPoint_PlayerSpawn";
                var mr = pill.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                var mf = pill.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf);
                var col = pill.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                Log("BATCH: promoted 'Capsule' -> HeroStartPoint_PlayerSpawn (mesh + collider stripped; invisible marker).");
            }
            else
            {
                Log("BATCH: no 'Capsule' pill found — hero will fall back to the personal-quarters anchor.");
            }

            MakeCastleHubPrimaryStartAndWire();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("BATCH: wired + saved MainCastle_Hall. Ready to Play.");
        }

        // =====================================================================
        //  WO-384 — FORCE a fresh grand-stair rebuild (after a code change like the
        //  pivot/rotation/width), then bake. Unlike BatchAddFloorAndBakeCastle (which
        //  PRESERVES an existing GrandStair so a manual in-editor rotation survives),
        //  this removes the existing stair so BuildGrandStair regenerates it from code.
        // =====================================================================
        public static void BatchRebuildGrandStairAndBake()
        {
            const string scenePath = "Assets/Scenes/MainCastle_Hall.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var existing = GameObject.Find("GrandStair_CourtyardToBattlements");
            if (existing != null) { Object.DestroyImmediate(existing); Log("BATCH-REBUILD: removed existing GrandStair for a fresh code rebuild."); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // The normal bake now builds it fresh (build-if-absent) + bakes + saves.
            BatchAddFloorAndBakeCastle();
        }

        // =====================================================================
        //  Batchmode entry — open MainCastle_Hall, ensure the invisible floor
        //  (courtyard + gate + keep entrance), force the NavMeshSurface to
        //  Physics Colliders, BAKE, and persist the navmesh asset + scene.
        //  Reflection on NavMeshSurface (no hard package dep). Invoked headless.
        // =====================================================================
        public static void BatchAddFloorAndBakeCastle()
        {
            const string scenePath = "Assets/Scenes/MainCastle_Hall.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Log("BATCH-BAKE: opened " + scenePath);

            // Remove the owner's leftover hand-placed planes — they still render (z-fight "ground
            // flash") and over-extend the navmesh way past the walls. The generated invisible
            // floor (renderer-off) replaces them cleanly.
            foreach (var n in new[] { "Plane", "Plane (1)" })
            {
                var stray = GameObject.Find(n);
                if (stray != null) { Object.DestroyImmediate(stray); Log("BATCH-BAKE: removed leftover hand-plane '" + n + "'."); }
            }

            // Ensure the generated invisible floor (courtyard + gate + keep entrance) exists.
            var root = GameObject.Find(RootName);
            Transform parent = root != null ? root.transform
                : (GameObject.Find(RootName + "_FloorHost") ?? new GameObject(RootName + "_FloorHost")).transform;
            BuildNavMeshFloor(parent);
            Log("BATCH-BAKE: floor ensured (courtyard + gate + keep interior).");

            // WO-384: ensure the grand spiral stair exists so the bake fuses courtyard → stairs →
            // upper plane into ONE walkable sheet. Always drop the OLD ramp/prefab stair. Treat
            // the GrandStair like a PREFAB INSTANCE: build it ONCE if missing, but if it already
            // exists DON'T regenerate — bake it AS-IS so any manual rotation/placement the owner
            // applied in-editor is PRESERVED (steps + railings + link are one rotatable object,
            // pivoted at the base). Use BatchRebuildGrandStairAndBake to force a fresh rebuild
            // after a code change.
            var strayStair = GameObject.Find("MainStairs_Poly_ToUpperBattlements");
            if (strayStair != null) { Object.DestroyImmediate(strayStair); Log("BATCH-BAKE: removed old MainStairs_Poly prefab."); }
            if (GameObject.Find("GrandStair_CourtyardToBattlements") == null)
            {
                BuildGrandStair(parent);
                Log("BATCH-BAKE: grand spiral stair built (was missing).");
            }
            else
            {
                Log("BATCH-BAKE: existing GrandStair found — baking AS-IS (manual rotation/placement preserved).");
            }

            // Configure + bake every NavMeshSurface via reflection so renderer-off planes are
            // collected (Use Geometry = Physics Colliders). RenderMeshes=0, PhysicsColliders=1.
            var surfType = FindType("Unity.AI.Navigation.NavMeshSurface");
            if (surfType == null)
            {
                Err("BATCH-BAKE: NavMeshSurface type not resolved — bake skipped (do it in-editor).");
            }
            else
            {
                var surfaces = Object.FindObjectsByType(surfType, FindObjectsSortMode.None);
                Log("BATCH-BAKE: NavMeshSurface count = " + surfaces.Length);
                foreach (var s in surfaces)
                {
                    var ug = surfType.GetProperty("useGeometry");
                    if (ug != null) ug.SetValue(s, System.Enum.ToObject(ug.PropertyType, 1)); // PhysicsColliders
                    var co = surfType.GetProperty("collectObjects");
                    if (co != null) co.SetValue(s, System.Enum.ToObject(co.PropertyType, 0)); // All

                    var build = surfType.GetMethod("BuildNavMesh", System.Type.EmptyTypes);
                    if (build != null) { build.Invoke(s, null); Log("BATCH-BAKE: BuildNavMesh() invoked."); }

                    // Persist the freshly-built data as an asset so it survives the scene save.
                    var dataProp = surfType.GetProperty("navMeshData");
                    var data = dataProp != null ? dataProp.GetValue(s) as Object : null;
                    if (data != null)
                    {
                        if (!System.IO.Directory.Exists("Assets/Scenes/MainCastle_Hall"))
                            AssetDatabase.CreateFolder("Assets/Scenes", "MainCastle_Hall");
                        string assetPath = "Assets/Scenes/MainCastle_Hall/NavMesh-NavMeshSurface.asset";
                        if (!AssetDatabase.Contains(data))
                        {
                            var existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                            if (existing != null) AssetDatabase.DeleteAsset(assetPath);
                            AssetDatabase.CreateAsset(data, assetPath);
                            Log("BATCH-BAKE: navmesh asset written -> " + assetPath);
                        }
                        else Log("BATCH-BAKE: navmesh data already an asset (updated in place).");
                    }
                    else Err("BATCH-BAKE: surface.navMeshData was null after bake — bake likely produced nothing.");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Log("BATCH-BAKE: saved scene + assets. Done.");
        }

        private static Transform FindHeroParentInCastle(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                // Prefer the labeled home quarters.
                var home = root.transform.Find("MainKeep_CastleWithTwoLevels_Home/PlayerHeroHall_PersonalQuarters_HomeSpace")
                           ?? root.transform.Find("PlayerHeroHall_PersonalQuarters_HomeSpace");
                if (home != null) return home;

                // Fallbacks
                if (root.name.Contains("PlayerHeroHall") || root.name.Contains("PersonalQuarters") || root.name.Contains("HomeSpace"))
                    return root.transform;

                var keep = root.transform.Find("MainKeep_CastleWithTwoLevels_Home");
                if (keep != null) return keep;
            }
            return null;
        }

        private static void PlaceHeroInCastleHome(Scene scene, GameObject hero)
        {
            // Prefer the marker we (optionally) placed during build.
            var start = GameObject.Find("HeroStartPoint_InsidePersonalQuarters") ?? GameObject.Find("HeroStartPoint_PlayerSpawn");
            if (start != null)
            {
                hero.transform.position = start.transform.position;
                hero.transform.rotation = start.transform.rotation;
                Log("Hero placed at HeroStartPoint inside personal quarters.");
                return;
            }

            // Fallback: place relative to the home quarters using similar logic to CastleWalkable.
            var home = FindHeroParentInCastle(scene);
            if (home != null)
            {
                hero.transform.SetParent(home, false);
                hero.transform.localPosition = new Vector3(0, 0.1f, 0);
                hero.transform.localRotation = Quaternion.identity;
                Log("Hero placed inside home quarters (fallback local pos).");
            }
            else
            {
                hero.transform.position = new Vector3(0, 0.1f, -10); // rough inside keep area
                Log("Hero placed with rough fallback position (no home quarters found).");
            }
        }

        private static void EnsureHeroControllable(GameObject hero)
        {
            if (hero == null) return;
            if (!hero.activeSelf) { hero.SetActive(true); Log("Hero activated."); }

            // HeroLocomotion enable (the control ensurer is frequently hardcoded to Village2).
            var locoType = FindType("DeNelle.Village.HeroLocomotion");
            if (locoType != null)
            {
                var loco = hero.GetComponent(locoType) as Behaviour;
                if (loco != null && !loco.enabled)
                {
                    loco.enabled = true;
                    Log("HeroLocomotion enabled (prevents silent input eat).");
                }
            }

            // Also try to make HeroControlEnsurer happy if present (set its target scene or enable).
            var ensurerType = FindType("DeNelle.Village.HeroControlEnsurer");
            if (ensurerType != null)
            {
                var ensurer = hero.GetComponent(ensurerType) as Behaviour;
                if (ensurer != null && !ensurer.enabled) ensurer.enabled = true;
            }
        }

        private static void Log(string m)  => Debug.Log("[CastleHubBuilder] " + m);
        private static void Err(string m)  => Debug.LogError("[CastleHubBuilder] " + m);
    }
}