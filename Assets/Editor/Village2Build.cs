// Village2Build.cs — headless editor tooling to (1) harvest the Quaternius
// Medieval Village MegaKit's artist-assembled buildings into prefabs, and
// (2) wire + run Grok's Village2Generator from those prefabs.
//
// EXPERIMENT / build-tooling only. Editor-only. Does NOT touch
// VillageSceneBuilder or Village.unity.
//
// NOTE on reflection: Village2Generator lives in Assets/_Village2 with no
// asmdef and no namespace, so it compiles into Assembly-CSharp. An assembly
// definition (DeNelle.Editor) cannot reference Assembly-CSharp, so there is no
// compile-time path to the type. We therefore add the component and assign its
// public slot fields via reflection. This is build tooling, not a runtime
// cross-module bridge, so it does not violate the "no reflection bridges" rule.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class Village2Build
    {
        // ---- Asset path constants -------------------------------------------------

        const string SampleScenePath =
            "Assets/Quaternius/Medieval Village MegaKit/Levels/L_SampleScene_1.unity";

        // Quaternius pieces actually live under Modules/Prefabs/<Category>/.
        // We search several candidate roots so we are robust to pack layout drift.
        static readonly string[] QuaterniusPieceRoots =
        {
            "Assets/Quaternius/Medieval Village MegaKit/Modules/Prefabs/Wall/",
            "Assets/Quaternius/Medieval Village MegaKit/Modules/Prefabs/Prop/",
            "Assets/Quaternius/Medieval Village MegaKit/Modules/Prefabs/Roof/",
            "Assets/Quaternius/Medieval Village MegaKit/Modules/Prefabs/Window-Door/",
            "Assets/Quaternius/Medieval Village MegaKit/Prefabs/",
        };

        const string TreeFbxPath = "Assets/Art/Tree_Of_Life.fbx";

        const string BuildingsPrefabDir = "Assets/Prefabs/Buildings";
        const string EnvironmentPrefabDir = "Assets/Prefabs/Environment";

        const string TreeOfLifePrefabPath = "Assets/Prefabs/Environment/TreeOfLife.prefab";

        // Scene-root name -> clean prefab name.
        static readonly (string sceneName, string cleanName)[] HarvestTargets =
        {
            ("Houses A",     "HouseA"),
            ("House C",      "HouseC"),
            ("House C (2)",  "HouseC2"),
            ("House D",      "HouseD"),
            ("Tower",        "KitTower"),
        };

        // =====================================================================
        // 1. HarvestQuaterniusBuildings
        // =====================================================================
        [MenuItem("Defenders/Village2/1. Harvest Quaternius Buildings")]
        public static void HarvestQuaterniusBuildings()
        {
            Debug.Log("[Village2Build] === HarvestQuaterniusBuildings START ===");

            if (!File.Exists(SampleScenePath))
            {
                Debug.LogError($"[Village2Build] Sample scene not found at '{SampleScenePath}'. Aborting harvest.");
                return;
            }

            EnsureFolder(BuildingsPrefabDir);
            EnsureFolder(EnvironmentPrefabDir);

            // --- Tree of Life (independent of the scene) -------------------------
            HarvestTreeOfLife();

            // --- Open the Quaternius sample scene --------------------------------
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            GameObject[] roots = scene.GetRootGameObjects();
            Debug.Log($"[Village2Build] Opened '{SampleScenePath}' — {roots.Length} root GameObjects:");
            foreach (var r in roots)
                Debug.Log($"[Village2Build]   ROOT: \"{r.name}\"");

            // Index roots by name for lookup.
            var byName = new Dictionary<string, GameObject>();
            foreach (var r in roots)
                if (!byName.ContainsKey(r.name))
                    byName[r.name] = r;

            int saved = 0;
            foreach (var (sceneName, cleanName) in HarvestTargets)
            {
                if (!byName.TryGetValue(sceneName, out GameObject src) || src == null)
                {
                    Debug.LogWarning($"[Village2Build] Harvest target root \"{sceneName}\" NOT FOUND in scene — skipping {cleanName}.");
                    continue;
                }

                GameObject dup = Object.Instantiate(src);
                dup.name = cleanName;
                dup.transform.position = Vector3.zero;
                dup.transform.rotation = Quaternion.identity;

                SeatOnGround(dup);

                string prefabPath = $"{BuildingsPrefabDir}/{cleanName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(dup, prefabPath, out bool ok);
                Object.DestroyImmediate(dup);

                if (ok)
                {
                    saved++;
                    Debug.Log($"[Village2Build]   SAVED \"{sceneName}\" -> {prefabPath}");
                }
                else
                {
                    Debug.LogWarning($"[Village2Build]   FAILED to save prefab for \"{sceneName}\" -> {prefabPath}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Village2Build] === HarvestQuaterniusBuildings DONE — {saved}/{HarvestTargets.Length} building prefabs saved ===");

            // Re-open a normal project scene if one is easy to find; otherwise leave.
            ReopenNormalScene();
        }

        static void HarvestTreeOfLife()
        {
            if (!File.Exists(TreeFbxPath))
            {
                Debug.LogWarning($"[Village2Build] Tree of Life FBX not found at '{TreeFbxPath}' — skipping TreeOfLife prefab.");
                return;
            }

            GameObject treeSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TreeFbxPath);
            if (treeSrc == null)
            {
                Debug.LogWarning($"[Village2Build] Could not load Tree FBX at '{TreeFbxPath}' — skipping TreeOfLife prefab.");
                return;
            }

            GameObject treeInst = (GameObject)PrefabUtility.InstantiatePrefab(treeSrc);
            treeInst.name = "TreeOfLife";
            treeInst.transform.position = Vector3.zero;
            treeInst.transform.rotation = Quaternion.identity;
            SeatOnGround(treeInst);

            PrefabUtility.SaveAsPrefabAsset(treeInst, TreeOfLifePrefabPath, out bool treeOk);
            Object.DestroyImmediate(treeInst);

            if (treeOk)
                Debug.Log($"[Village2Build]   SAVED Tree_Of_Life.fbx -> {TreeOfLifePrefabPath}");
            else
                Debug.LogWarning($"[Village2Build]   FAILED to save {TreeOfLifePrefabPath}");
        }

        // Shift all children so the combined renderer-bounds min.y sits at y = 0.
        static void SeatOnGround(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            bool haveBounds = false;
            Bounds bounds = default;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!haveBounds)
                {
                    bounds = r.bounds;
                    haveBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (!haveBounds)
                return;

            float dy = -bounds.min.y;
            if (Mathf.Abs(dy) < 0.0001f)
                return;

            // Move each direct child by dy (root stays at origin).
            foreach (Transform child in root.transform)
                child.position += new Vector3(0f, dy, 0f);
        }

        // =====================================================================
        // 2. SetupAndGenerateVillage2
        // =====================================================================
        [MenuItem("Defenders/Village2/2. Setup + Generate Village2")]
        public static void SetupAndGenerateVillage2() => SetupAndGenerateVillage2(bonesOnly: false);

        // bonesOnly = true: generate ONLY the structural bones — terrain/ground (roads),
        // the 4 perimeter WALLS + gates + towers, and the centrepiece Tree-of-Life — by
        // leaving the DECORATIVE slots (houses, specialty buildings, market stalls) and the
        // NATURE-RING arrays (trees/bushes/rocks) UNASSIGNED. The generator's CreateQuadrant
        // places nothing when its house/specialty slots are null (Place/Pick null-guard), and
        // ScatterNatureRing early-returns when no nature prefabs are assigned. Village2Generator
        // itself is NEVER edited — this is a pure slot-wiring switch.
        public static void SetupAndGenerateVillage2(bool bonesOnly) =>
            SetupAndGenerateVillage2(bonesOnly, terrainOnly: false);

        // terrainOnly = true: a TRULY BARE authoring canvas. Implies bonesOnly (no houses/specialty/
        // nature) AND additionally strips the generated PERIMETER — walls, gates, towers, moat, and
        // perimeter torches — by setting the generator's skipPerimeter flag. The result is ground/
        // roads + the centrepiece Tree-of-Life only, so the player authors the entire perimeter from
        // scratch. Also always assigns the ground material (fixes the magenta/blue bare-ground).
        public static void SetupAndGenerateVillage2(bool bonesOnly, bool terrainOnly)
        {
            if (terrainOnly) bonesOnly = true;   // terrain-only is a strict superset of bones-only
            Debug.Log(terrainOnly
                ? "[Village2Build] === SetupAndGenerateVillage2 START (TERRAIN ONLY — no perimeter) ==="
                : bonesOnly
                    ? "[Village2Build] === SetupAndGenerateVillage2 START (BONES ONLY) ==="
                    : "[Village2Build] === SetupAndGenerateVillage2 START ===");

            // Resolve the Village2Generator type (lives in Assembly-CSharp, no namespace).
            System.Type genType = FindTypeByName("Village2Generator");
            if (genType == null)
            {
                Debug.LogError("[Village2Build] Village2Generator type not found in any loaded assembly. " +
                               "Ensure Assets/_Village2/Village2Generator.cs exists and compiled. Aborting.");
                return;
            }

            // ---- Load harvested + Quaternius piece prefabs ----------------------
            GameObject houseA  = LoadBuilding("HouseA");
            GameObject houseC  = LoadBuilding("HouseC");
            GameObject houseC2 = LoadBuilding("HouseC2");
            GameObject houseD  = LoadBuilding("HouseD");
            GameObject kitTower = LoadBuilding("KitTower");
            // Centrepiece tree: ONLY the canonical enchanted Tree of Life may bake at
            // origin (0,0,0). It has its basecolor (auto-bound at runtime by
            // TreeOfLifeMaterialFixer). WO-311: do NOT fall back to a generic polyperfect
            // tree (SM_Tree_Round / SM_Tree_Oak / SM_Tree_Baobab) at the village centre —
            // a generic tree at origin is the bug. If the canonical prefab is missing,
            // leave the centre EMPTY (treeOfLife == null) and let the runtime
            // TreeOfLifeMaterialFixer spawn-guard fill it; never bake a placeholder here.
            GameObject treeOfLife =
                LoadAt(TreeOfLifePrefabPath, "TreeOfLife")
                ?? LoadAt("Assets/Resources/Structures/tree_of_life.fbx", "tree_of_life");
            if (treeOfLife == null)
                Debug.LogWarning("[Village2Build] Canonical Tree of Life prefab/FBX not found — " +
                                 "leaving village centre EMPTY (runtime TreeOfLifeMaterialFixer will spawn it). " +
                                 "NOT placing a generic tree at origin (WO-311).");

            GameObject wallStraight = LoadPiece("Wall_UnevenBrick_Straight.prefab");
            GameObject gatePrefab   = LoadPiece("Wall_Arch.prefab");
            GameObject stairsPrefab = LoadPiece("Stairs_Exterior_Straight.prefab");
            GameObject roadStraight = LoadPiece("Floor_Brick.prefab");
            GameObject towerBase    = LoadPiece("Corner_ExteriorWide_Brick.prefab");
            GameObject balconyStraight = LoadPiece("Balcony_Simple_Straight.prefab");
            GameObject balconyCorner   = LoadPiece("Balcony_Simple_Corner.prefab");
            // moatWaterPlane + torchPrefab intentionally null for v1.

            // Ground material: the Quaternius kit's own sample-scene ground (a URP ShaderGraph mat
            // from the same pack as the walls/roads). This is what the bare shell paints its ground
            // plane with so it no longer renders the default magenta/blue. Falls back to a name
            // search if the path moves; null only if the pack isn't imported (graceful warn).
            Material groundMat =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Quaternius/Medieval Village MegaKit/Materials/MI_SampleScene_Ground.mat")
                ?? LoadMaterialByName("MI_SampleScene_Ground");
            if (groundMat == null)
                Debug.LogWarning("[Village2Build] MI_SampleScene_Ground material not found — ground plane will be skipped (bare canvas stays magenta).");

            // ---- Build arrays for the [] slots (skip nulls) ---------------------
            GameObject[] smallHouses   = NonNull(houseC, houseC2);
            GameObject[] mediumHouses  = NonNull(houseD, houseA);
            GameObject[] marketStalls  = NonNull(houseC);

            // ---- Create Village2 host + component -------------------------------
            // Clean up any prior Village2 host so re-runs are idempotent.
            var prior = GameObject.Find("Village2");
            if (prior != null)
            {
                Object.DestroyImmediate(prior);
                Debug.Log("[Village2Build] Removed prior 'Village2' GameObject.");
            }

            GameObject host = new GameObject("Village2");
            Component gen = host.AddComponent(genType);

            // ---- Assign public slot fields via reflection -----------------------
            SetField(gen, "villageParent", host.transform);
            SetField(gen, "treeOfLife", treeOfLife);
            // Polyperfect trees are authored UPRIGHT, so no corrective rotation (the -90 X default
            // was for the lying Tree_Of_Life.fbx). Identity keeps the green centrepiece standing.
            SetField(gen, "treeUprightEuler", Vector3.zero);

            // DECORATIVE town content — houses + specialty buildings + market stalls.
            // BONES-ONLY skips all of these (leaves the slots null) so CreateQuadrant places
            // nothing; the walls/gates/towers/tree still generate from the slots below.
            if (!bonesOnly)
            {
                // Specialty buildings reuse harvested houses (re-skin later).
                SetField(gen, "blacksmithForge", houseA);
                SetField(gen, "armorerShop", houseD);
                SetField(gen, "lumbermill", houseA);
                SetField(gen, "tavern", houseD);
                SetField(gen, "church", houseA);

                SetField(gen, "smallHouses", smallHouses);
                SetField(gen, "mediumHouses", mediumHouses);
                SetField(gen, "marketStalls", marketStalls);
            }
            else
            {
                Debug.Log("[Village2Build] BONES ONLY: skipped house/specialty/market slot wiring — quadrants stay empty.");
            }

            SetField(gen, "wallStraight", wallStraight);
            SetField(gen, "balconyStraight", balconyStraight);
            SetField(gen, "balconyCorner", balconyCorner);
            SetField(gen, "towerBase", towerBase);
            SetField(gen, "gatePrefab", gatePrefab);
            SetField(gen, "stairsPrefab", stairsPrefab);
            SetField(gen, "moatWaterPlane", null);   // moat skipped v1
            SetField(gen, "roadStraight", roadStraight);
            SetField(gen, "torchPrefab", null);      // torches skipped v1

            // Ground plane + bare-canvas perimeter strip.
            SetField(gen, "groundMaterial", groundMat);
            SetField(gen, "skipPerimeter", terrainOnly);
            if (terrainOnly)
                Debug.Log("[Village2Build] TERRAIN ONLY: skipPerimeter=true — walls/gates/towers/moat/torches will NOT generate.");

            SetField(gen, "townHalfWidth", 42f);
            SetField(gen, "townHalfDepth", 33f);

            // Measure the wall piece so the balcony rampart seats flush + walls snap edge-to-edge.
            float wallH = 5f, wallW = 4f;
            if (wallStraight != null)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(wallStraight);
                if (probe != null)
                {
                    var rends = probe.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0)
                    {
                        Bounds b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        wallH = b.size.y; wallW = b.size.x;
                    }
                    Object.DestroyImmediate(probe);
                }
            }
            SetField(gen, "wallHeight", wallH);
            SetField(gen, "wallStep", wallW);
            Debug.Log($"[Village2Build] measured wall: height={wallH:F2} width={wallW:F2}");

            // DEF-254: measure the gate arch so the opening EXACTLY spans it — walls butt
            // against the arch (the "gate doesn't touch the walls" gap). gateHalf =
            // (archWidth + wallStep)/2 puts the innermost wall's inner edge on the arch edge.
            float archW = 4f;
            if (gatePrefab != null)
            {
                var aProbe = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab);
                if (aProbe != null)
                {
                    var ar = aProbe.GetComponentsInChildren<Renderer>();
                    if (ar.Length > 0)
                    {
                        Bounds ab = ar[0].bounds;
                        for (int i = 1; i < ar.Length; i++) ab.Encapsulate(ar[i].bounds);
                        archW = Mathf.Max(ab.size.x, ab.size.z);   // span along the wall run
                    }
                    Object.DestroyImmediate(aProbe);
                }
            }
            float gateHalf = (archW + wallW) * 0.5f;
            SetField(gen, "gateHalfNorth", gateHalf);
            SetField(gen, "gateHalfOther", gateHalf);
            Debug.Log($"[Village2Build] measured gate arch: width={archW:F2} -> gateHalf={gateHalf:F2} (walls meet the arch)");

            // DEPTH: a wood encircling the town (procedural scatter, gate lanes kept clear).
            // Pull from whatever nature packs are imported — polyperfect Nature_M (gitignored but
            // present) + KayKit Forest. LoadMany drops any that aren't on this machine.
            // Polyperfect SM_Tree/Bush/Rock only — they render green/textured. The KayKit
            // "_Color1" set is a WHITE/snow palette (the white pines in the playtest) — dropped.
            // BONES-ONLY leaves trees/bushes/rocks null so ScatterNatureRing early-returns
            // (no 85-piece encircling wood) — a truly bare canvas.
            if (!bonesOnly)
            {
                SetField(gen, "trees", LoadMany(
                    "SM_Tree_Beech", "SM_Tree_Oak", "SM_Tree_Birch", "SM_Tree_Maple", "SM_Tree_Conifer",
                    "SM_Tree_Fir", "SM_Tree_Round", "SM_Tree_Lime", "SM_Tree_Poplar"));
                SetField(gen, "bushes", LoadMany(
                    "SM_Bush_Big", "SM_Bush_Medium", "SM_Bush_Small", "SM_Shrub", "SM_Shrub_Round"));
                SetField(gen, "rocks", LoadMany(
                    "SM_Rock_Large", "SM_Rocks_Small", "SM_Stone", "SM_Rock_Sharp"));
            }
            else
            {
                Debug.Log("[Village2Build] BONES ONLY: skipped nature-ring (trees/bushes/rocks) wiring — ScatterNatureRing will early-return.");
            }

            // ---- Invoke GenerateVillage() ---------------------------------------
            MethodInfo generate = genType.GetMethod("GenerateVillage",
                BindingFlags.Public | BindingFlags.Instance);
            if (generate == null)
            {
                Debug.LogError("[Village2Build] Village2Generator.GenerateVillage() not found. Aborting.");
                return;
            }

            Debug.Log("[Village2Build] Slot wiring summary:");
            Debug.Log($"[Village2Build]   treeOfLife={NameOf(treeOfLife)}  tower={NameOf(kitTower)}  wallStraight={NameOf(wallStraight)}");
            Debug.Log($"[Village2Build]   gate={NameOf(gatePrefab)}  stairs={NameOf(stairsPrefab)}  road={NameOf(roadStraight)}");
            Debug.Log($"[Village2Build]   smallHouses[{smallHouses.Length}] mediumHouses[{mediumHouses.Length}] marketStalls[{marketStalls.Length}]");
            Debug.Log($"[Village2Build]   forge/armorer/lumbermill/tavern/church reuse houses (re-skin later)");

            generate.Invoke(gen, null);

            int placed = host.transform.childCount;
            Debug.Log($"[Village2Build] === SetupAndGenerateVillage2 DONE — Village2 has {placed} top-level child groups/objects placed ===");

            try
            {
                System.IO.Directory.CreateDirectory("Assets/Scenes");
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),
                    "Assets/Scenes/Village2Test.unity");
                Debug.Log("[Village2Build] saved scene Assets/Scenes/Village2Test.unity");
            }
            catch (System.Exception e) { Debug.LogWarning("[Village2Build] save scene failed: " + e.Message); }

            CaptureOverhead();
        }

        // Renders an angled 3/4 overhead of the generated town to Builds/village2_overhead.png.
        static void CaptureOverhead()
        {
            try
            {
                var camGo = new GameObject("V2Cam");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 45f;
                cam.transform.position = new Vector3(0f, 70f, -95f);
                cam.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
                cam.farClipPlane = 600f;
                const int W = 1280, H = 1280;
                var rt = new RenderTexture(W, H, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                var png = tex.EncodeToPNG();
                string outPath = System.IO.Path.Combine(
                    System.IO.Directory.GetParent(Application.dataPath).FullName, "Builds", "village2_overhead.png");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                System.IO.File.WriteAllBytes(outPath, png);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Debug.Log("[Village2Build] overhead screenshot saved: " + outPath);
            }
            catch (System.Exception e) { Debug.LogWarning("[Village2Build] screenshot failed: " + e.Message); }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        static GameObject LoadBuilding(string cleanName)
        {
            return LoadAt($"{BuildingsPrefabDir}/{cleanName}.prefab", cleanName);
        }

        static GameObject LoadAt(string path, string label)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                Debug.LogWarning($"[Village2Build] Prefab '{label}' NOT FOUND at '{path}' — slot will be null.");
            return go;
        }

        // Search all known Quaternius piece roots for a given prefab filename.
        static GameObject LoadPiece(string fileName)
        {
            foreach (var root in QuaterniusPieceRoots)
            {
                string path = root + fileName;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    Debug.Log($"[Village2Build] Loaded Quaternius piece '{fileName}' from '{path}'.");
                    return go;
                }
            }
            Debug.LogWarning($"[Village2Build] Quaternius piece '{fileName}' NOT FOUND in any known root — slot will be null.");
            return null;
        }

        // Finds a GameObject/FBX/prefab by asset name ANYWHERE in the project (robust to pack
        // location). Exact filename match preferred, else first partial. Null if absent.
        static GameObject LoadByName(string name)
        {
            string exact = null;
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:GameObject"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(file, name, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (exact == null) exact = path;   // remember first partial as fallback
            }
            return exact != null ? AssetDatabase.LoadAssetAtPath<GameObject>(exact) : null;
        }

        // Finds a Material by asset name anywhere in the project (robust to pack location).
        static Material LoadMaterialByName(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Material"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(file, name, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return null;
        }

        // Loads many by name, dropping any that aren't imported (graceful — packs are gitignored).
        static GameObject[] LoadMany(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (var n in names) { var g = LoadByName(n); if (g != null) list.Add(g); }
            Debug.Log($"[Village2Build] LoadMany resolved {list.Count}/{names.Length} ({string.Join(",", names)}).");
            return list.ToArray();
        }

        static GameObject[] NonNull(params GameObject[] items)
        {
            var list = new List<GameObject>();
            foreach (var g in items)
                if (g != null)
                    list.Add(g);
            return list.ToArray();
        }

        static string NameOf(GameObject g) => g != null ? g.name : "<null>";

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        static System.Type FindTypeByName(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null)
                    return t;
                // Fallback: scan exported types for an unnamespaced match.
                foreach (var candidate in SafeGetTypes(asm))
                {
                    if (candidate.Name == typeName)
                        return candidate;
                }
            }
            return null;
        }

        static System.Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch { return System.Array.Empty<System.Type>(); }
        }

        static void SetField(Component target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            if (f == null)
            {
                Debug.LogWarning($"[Village2Build] Field '{fieldName}' not found on {target.GetType().Name} — skipping.");
                return;
            }
            try
            {
                f.SetValue(target, value);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Village2Build] Could not set field '{fieldName}': {e.Message}");
            }
        }

        static void ReopenNormalScene()
        {
            string[] candidates =
            {
                "Assets/Scenes/OuterWorld.unity",
                "Assets/Scenes/Village.unity",
                "Assets/Scenes/MainMenu.unity",
            };
            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                    Debug.Log($"[Village2Build] Re-opened normal scene '{p}'.");
                    return;
                }
            }
            Debug.Log("[Village2Build] No standard scene found to re-open; leaving current scene (gatekeeper handles).");
        }
    }
}
