// =============================================================================
// OuterWorldBuilder — builds the outer-world region layer (WO-142 Phase A).
// -----------------------------------------------------------------------------
// SEPARATE builder from VillageSceneBuilder (CLAUDE.md §9 parallel lane) and from
// ExteriorTerrainBuilder. Runs AFTER the terrain exists; it adds region anchors +
// mine nodes on top, under its own idempotent "OuterWorldRoot" so re-running is
// clean and it never touches the village or the terrain.
//
// Phase A (this pass): the four region anchors (Goldfields E / Stoneback W /
// Mirewood S / Ashwood N — matching ExteriorTerrainBuilder's biomes + the
// ZoneManager classifier) and a starter set of MineNodes placed per region, with
// resource + yield tuned to the region's danger tier (danger = reward). Scenery,
// wandering NPCs, and ruins are later phases (WO-142 B/C/D).
//
// Headless: -executeMethod DeNelle.Editor.OuterWorldBuilder.BuildOuterWorld
// =============================================================================
using System;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Editor
{
    public static class OuterWorldBuilder
    {
        private const string RootName = "OuterWorldRoot";

        // DeNelle.Editor.asmdef deliberately does NOT reference DeNelle.Village
        // (same rule VillageSceneBuilder follows — it reaches WallSegment etc. by
        // reflection). So MineNode is attached by reflection too: resolve the type,
        // AddComponent, set the public fields by name. Resource is set as an int
        // (the MineResource enum's underlying value) to avoid referencing the enum
        // type from this assembly.
        private const string MineNodeTypeName = "DeNelle.Village.MineNode";

        // MineResource enum values (mirror DeNelle.Village.MineResource order):
        // Iron=0, Wood=1, Stone=2, AetherCrystal=3.
        private const int ResIron = 0, ResWood = 1, ResStone = 2, ResAether = 3;

        // A starter mine-node placement: where (region centre offset), what, how much.
        private struct NodeDef
        {
            public RegionId Region;
            public Vector3  Pos;       // world position (well outside the ±42/±33 walls)
            public int      Res;       // MineResource enum value (Iron/Wood/Stone/Aether)
            public int      Yield;
        }

        // Iron in stony Stoneback (W), wood in forested Ashwood (N) + a little in
        // Goldfields, stone broadly, and rare aether crystal in the deadly Mirewood
        // (S, toward the Wound). Positions sit in each region's quadrant, clear of
        // the wall footprint. Designers tune freely.
        private static readonly NodeDef[] Nodes =
        {
            // Goldfields (E, +X) — safe breadbasket: wood + stone, modest yield.
            new NodeDef { Region = RegionId.Goldfields, Pos = new Vector3( 70f, 0f,  10f), Res = ResWood,  Yield = 5 },
            new NodeDef { Region = RegionId.Goldfields, Pos = new Vector3( 85f, 0f, -15f), Res = ResStone, Yield = 5 },
            // Stoneback (W, -X) — stony uplands: the iron region.
            new NodeDef { Region = RegionId.Stoneback,  Pos = new Vector3(-72f, 0f,  12f), Res = ResIron,  Yield = 5 },
            new NodeDef { Region = RegionId.Stoneback,  Pos = new Vector3(-88f, 0f, -10f), Res = ResIron,  Yield = 5 },
            // Mirewood (S, -Z) — drowned valley: rare aether crystal, higher risk.
            new NodeDef { Region = RegionId.Mirewood,   Pos = new Vector3( 10f, 0f, -78f), Res = ResAether, Yield = 3 },
            new NodeDef { Region = RegionId.Mirewood,   Pos = new Vector3(-18f, 0f, -90f), Res = ResStone, Yield = 6 },
            // Ashwood (N, +Z) — ruined front line: wood (dead timber) + crystal, richest tier.
            new NodeDef { Region = RegionId.Ashwood,    Pos = new Vector3(  8f, 0f,  80f), Res = ResWood,  Yield = 6 },
            new NodeDef { Region = RegionId.Ashwood,    Pos = new Vector3(-15f, 0f,  92f), Res = ResAether, Yield = 4 },
        };

        // The outer world is its OWN scene (loaded additively over Village at
        // runtime by WorldSceneLoader) — NOT baked into Village.unity. This keeps
        // the two builders on separate files so they never collide (the owner's
        // two-scene world). VillageSceneBuilder owns Village.unity; this owns
        // OuterWorld.unity.
        private const string OuterWorldScenePath = "Assets/Scenes/OuterWorld.unity";

        [MenuItem("Defenders/World/Build Outer World (Regions + Mine Nodes)")]
        public static void BuildOuterWorld()
        {
            // Create a fresh empty OuterWorld scene (or overwrite the existing one).
            // Headless-safe: NewScene gives a valid scene we save by explicit path,
            // so no Save dialog is ever raised.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            // Idempotency within the fresh scene (defensive — should be empty).
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName)
                    UnityEngine.Object.DestroyImmediate(go);

            var root = new GameObject(RootName);

            // ── Region anchors — one empty marker per region at its quadrant centre.
            var anchorsRoot = new GameObject("RegionAnchors");
            anchorsRoot.transform.SetParent(root.transform, false);
            int anchors = 0;
            foreach (var kv in ZoneManager.Regions)
            {
                var rz = kv.Value;
                if (rz.Id == RegionId.Village) continue;   // village is the home zone, not an outer anchor
                var a = new GameObject($"Region-{rz.DisplayName} (T{rz.DangerTier} {rz.Cardinal})");
                a.transform.SetParent(anchorsRoot.transform, false);
                a.transform.position = AnchorPos(rz.Id);
                anchors++;
            }

            // ── Mine nodes — placed per region, verified against the classifier.
            // MineNode lives in DeNelle.Village; resolve it by reflection (the
            // Editor asmdef can't reference Village — same pattern as VillageSceneBuilder).
            Type mineNodeType = FindType(MineNodeTypeName);
            if (mineNodeType == null)
                Debug.LogError($"[OuterWorldBuilder] {MineNodeTypeName} not found — is DeNelle.Village " +
                               "compiled? Mine nodes will be bare markers without behaviour.");

            var nodesRoot = new GameObject("MineNodes");
            nodesRoot.transform.SetParent(root.transform, false);
            int placed = 0, mismatched = 0;
            foreach (var def in Nodes)
            {
                // Sanity: the chosen position should classify into the intended region.
                RegionId actual = ZoneManager.GetZone(def.Pos);
                if (actual != def.Region)
                {
                    Debug.LogWarning($"[OuterWorldBuilder] node at {def.Pos} intended {def.Region} " +
                                     $"but classifies as {actual} — placing anyway, tune the position.");
                    mismatched++;
                }

                var node = GameObject.CreatePrimitive(PrimitiveType.Cube);
                node.name = $"MineNode-{ResName(def.Res)}-{def.Region}";
                node.transform.SetParent(nodesRoot.transform, false);
                node.transform.position = def.Pos + Vector3.up * 0.5f;
                node.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

                if (mineNodeType != null)
                {
                    var mn = node.AddComponent(mineNodeType);
                    // Set public fields by name; Resource takes the enum's int value.
                    var fRes = mineNodeType.GetField("Resource");
                    if (fRes != null) fRes.SetValue(mn, Enum.ToObject(fRes.FieldType, def.Res));
                    var fYield = mineNodeType.GetField("YieldPerExtract");
                    if (fYield != null) fYield.SetValue(mn, def.Yield);
                }

                Tint(node, def.Res);
                placed++;
            }

            EditorSceneManager_MarkAndKeep(scene);
            EnsureInBuildSettings();   // so WorldSceneLoader can load it by name at runtime
            Debug.Log($"[OuterWorldBuilder] Phase A complete — {anchors} region anchors, " +
                      $"{placed} mine nodes ({mismatched} position mismatch). " +
                      "Regions: Goldfields/Stoneback/Mirewood/Ashwood.");
        }

        // Region quadrant-centre anchor positions (well outside the walls).
        private static Vector3 AnchorPos(RegionId id)
        {
            switch (id)
            {
                case RegionId.Goldfields: return new Vector3( 90f, 0f,   0f);  // E
                case RegionId.Stoneback:  return new Vector3(-90f, 0f,   0f);  // W
                case RegionId.Mirewood:   return new Vector3(  0f, 0f, -90f);  // S
                case RegionId.Ashwood:    return new Vector3(  0f, 0f,  90f);  // N
                default:                  return Vector3.zero;
            }
        }

        private static string ResName(int res) => res switch
        {
            ResIron => "Iron", ResWood => "Wood", ResStone => "Stone",
            ResAether => "AetherCrystal", _ => "Resource",
        };

        private static void Tint(GameObject go, int res)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return;
            Color c = res switch
            {
                ResIron   => new Color(0.55f, 0.55f, 0.60f),
                ResWood   => new Color(0.45f, 0.32f, 0.18f),
                ResStone  => new Color(0.60f, 0.58f, 0.54f),
                ResAether => new Color(0.32f, 0.80f, 0.95f),
                _         => Color.gray,
            };
            var m = new Material(sh) { name = "MineNode_" + ResName(res) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }

        // Reflection type resolver (mirrors VillageSceneBuilder.FindType) — scans
        // loaded assemblies for a fully-qualified type name.
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // Add OuterWorld.unity to Build Settings (enabled) if absent, so
        // WorldSceneLoader can additively load it by name. Idempotent.
        private static void EnsureInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == OuterWorldScenePath) return;   // already listed
            scenes.Add(new EditorBuildSettingsScene(OuterWorldScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[OuterWorldBuilder] Added " + OuterWorldScenePath + " to Build Settings.");
        }

        private static void EditorSceneManager_MarkAndKeep(UnityEngine.SceneManagement.Scene scene)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            // Save to the dedicated OuterWorld scene path — never Village — so the
            // two builders stay on separate files and batchmode never raises a
            // Save dialog.
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, OuterWorldScenePath);
        }

        // =====================================================================
        //  WO-173 / DEF-108 -- combined Village + Terrain navmesh
        // =====================================================================

        /// <summary>
        /// Bakes ONE continuous navmesh spanning BOTH Village.unity and OuterWorld.unity
        /// (the legacy multi-scene workflow) so the NavMeshAgent hero can walk OUT of the
        /// village onto the exterior terrain instead of stopping at the village-navmesh edge
        /// as an "invisible wall." Marks the terrain NavigationStatic, opens both scenes,
        /// bakes a single connected surface, and saves each scene's NavMeshData. Run AFTER
        /// BuildOuterWorld + BuildExterior + BuildVillage. Editor-closed batchmode.
        /// </summary>
        [MenuItem("Defenders/World/Bake World NavMesh (Village + Terrain)")]
        public static void BakeWorldNavMesh()
        {
            const string villagePath = "Assets/Scenes/Village.unity";
            if (!System.IO.File.Exists(villagePath) || !System.IO.File.Exists(OuterWorldScenePath))
            {
                Debug.LogError("[OuterWorldBuilder] BakeWorldNavMesh: Village.unity or OuterWorld.unity " +
                               "missing -- run the village + outer-world + exterior builds first.");
                return;
            }

            var village = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                villagePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            var outer = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                OuterWorldScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);

            // Owner 2026-05-31: the village hex-ground is DROPPED, so the terrain is the single
            // continuous ground and there's no separate floor to reference. The village
            // structures sit at Y=0, so just lift the (flat) terrain to Y=0 -- a fixed target,
            // no dynamic village-ground read. The terrain shipped FLAT at Y=-0.5 (its heightmap
            // didn't persist into the build); this lifts it flush so the combined navmesh bakes
            // ALIGNED and the hero walks village -> terrain seamlessly.
            const float villageFloorY = 0f;
            foreach (var go in outer.GetRootGameObjects())
                foreach (var terr in go.GetComponentsInChildren<Terrain>(true))
                {
                    float edgeSurface = terr.transform.position.y + terr.SampleHeight(new Vector3(42f, 0f, 0f));
                    float delta = villageFloorY - edgeSurface;
                    terr.transform.position += new Vector3(0f, delta, 0f);
                    Debug.Log("[OuterWorldBuilder] leveled terrain by " + delta.ToString("0.000") +
                              " (villageFloor=" + villageFloorY.ToString("0.000") +
                              ", edgeSurface was " + edgeSurface.ToString("0.000") + ") -> flush with village.");
                }

            // Mark the exterior terrain NavigationStatic so the bake makes it walkable.
            int marked = 0;
            foreach (var go in outer.GetRootGameObjects())
                foreach (var terr in go.GetComponentsInChildren<Terrain>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(terr.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(terr.gameObject,
                        flags | StaticEditorFlags.NavigationStatic);
                    marked++;
                }

            // Village floor/walls keep the NavigationStatic flags saved by BuildVillage, so a
            // combined bake across BOTH open scenes produces ONE connected walkable surface.
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(village);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(outer, OuterWorldScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[OuterWorldBuilder] BakeWorldNavMesh: marked " + marked + " terrain(s) " +
                      "NavigationStatic; baked ONE combined navmesh across Village + OuterWorld; " +
                      "saved both scenes. Hero (NavMeshAgent) can now walk village -> terrain.");
        }
    }
}
