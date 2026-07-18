// =============================================================================
// DungeonBaker — instantiate socketed rooms from DungeonComposeLayout JSON.
// -----------------------------------------------------------------------------
// Menu: Defenders/Dungeon/Bake Compose Layout
// Hard gate: each listed connection must mate within maxMateDistance and
// opposing alignment. Unmated sockets → seal (wall box) or secret flag.
// Reuses NavMesh bake patterns from DungeonChainBuilder / DungeonComposer.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.RoomForge
{
    public static class DungeonBaker
    {
        private const string LayoutsFolder = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts";
        private const string DefaultLayout = "d4_sunken_crypt_spine.json";
        private const string OutputScenesFolder = "Assets/Scenes/DungeonCompose";
        private const string Sys = "DungeonBake";
        // Editor pref (default OFF): when ON, a FAILED bake is saved to a _FAILED_<id>.unity
        // OUTSIDE Build Settings for debugging. Default off keeps a broken layout from leaving
        // any scene behind (WO-745 §2 fix 1).
        private const string SaveFailedScenesPref = "DungeonBaker.SaveFailedScenes";

        [MenuItem("Defenders/Dungeon/Bake Compose Layout (default spine)")]
        public static void BakeDefault()
        {
            string path = Path.Combine(LayoutsFolder, DefaultLayout);
            BakeFromFile(path);
        }

        [MenuItem("Defenders/Dungeon/Bake Compose Layout From Selected JSON")]
        public static void BakeSelected()
        {
            var obj = Selection.activeObject;
            string path = obj != null ? AssetDatabase.GetAssetPath(obj) : null;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".json"))
            {
                EditorUtility.DisplayDialog("DungeonBaker",
                    "Select a dungeon-layouts JSON asset first (or use Bake Compose Layout default spine).",
                    "OK");
                return;
            }
            BakeFromFile(path);
        }

        /// <summary>Batchmode entry: -executeMethod DeNelle.Editor.RoomForge.DungeonBaker.BakeDefault</summary>
        public static void BakeDefaultBatch()
        {
            BakeDefault();
            EditorApplication.Exit(0);
        }

        // Convert a project-relative "Assets/..." path to an absolute filesystem path.
        // Only the LEADING "Assets/" is the project marker (Application.dataPath already ends in
        // "/Assets"); a naive Replace("Assets/", ...) ALSO mangles the "Assets/" inside
        // "StreamingAssets/" -> a doubled path (the WO-742 bake crash). Strip the leading marker only.
        private static string ToFilesystemPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return assetPath;
            if (Path.IsPathRooted(assetPath)) return assetPath;
            if (assetPath.StartsWith("Assets/", System.StringComparison.Ordinal))
                return Application.dataPath + "/" + assetPath.Substring("Assets/".Length);
            return assetPath;
        }

        public static void BakeFromFile(string layoutAssetPath, bool populateForPlay = false)
        {
            // Resolve to an absolute filesystem path (see ToFilesystemPath for the doubled-path fix).
            string fsPath = ToFilesystemPath(layoutAssetPath);
            if (!File.Exists(fsPath))
            {
                FlowTrace.Fail(Sys, $"layout not found: {layoutAssetPath} (resolved '{fsPath}')");
                return;
            }
            layoutAssetPath = fsPath;

            string json = Guard.Try(Sys, "read layout json", () => File.ReadAllText(layoutAssetPath, Encoding.UTF8), null);
            if (string.IsNullOrEmpty(json))
            {
                FlowTrace.Fail(Sys, $"layout unreadable/empty file: {layoutAssetPath}");
                return;
            }

            DungeonComposeLayout layout = Guard.Try(Sys, "parse layout json",
                () => JsonConvert.DeserializeObject<DungeonComposeLayout>(json), null);
            if (layout == null)
            {
                FlowTrace.Fail(Sys, "JSON parse returned null - abort (no scene left open)");
                return;
            }

            if (layout.rooms == null || layout.rooms.Count == 0)
            {
                FlowTrace.Fail(Sys, $"layout '{layout.dungeonId}' has 0 rooms - abort");
                return;
            }

            float cell = layout.cellSize > 0.1f ? layout.cellSize : 6f;
            var rules = layout.rules ?? new ComposeRules();
            int connCount = layout.connections != null ? layout.connections.Count : 0;
            FlowTrace.Step(Sys, $"layout loaded id='{layout.dungeonId}' rooms={layout.rooms.Count} " +
                                $"connections={connCount} cellSize={cell:F1} maxMateDist={rules.maxMateDistance:F2} " +
                                $"sealUnmated={rules.sealUnmated}");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject($"DungeonCompose_{layout.dungeonId}").transform;

            // Instance lookup
            var instances = new Dictionary<string, GameObject>();
            var instanceMeta = new Dictionary<string, string>(); // instanceId -> archetype
            var placedOrder = new List<string>();                // instantiate order (for navmesh first/last)

            foreach (var place in layout.rooms)
            {
                if (place == null || string.IsNullOrEmpty(place.prefab)) continue;
                string instId = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                GameObject prefab = LoadRoomPrefab(place.prefab);

                GameObject go;
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    if (go == null) go = Object.Instantiate(prefab, root);
                    FlowTrace.Step(Sys, $"instantiate inst='{instId}' prefab='{place.prefab}'");
                }
                else
                {
                    go = CreatePlaceholderRoom(instId, root);
                    FlowTrace.Warn(Sys, $"instantiate inst='{instId}' PLACEHOLDER (prefab '{place.prefab}' not found under Assets/Dungeon/Rooms or Resources)");
                }

                go.name = instId;
                int cx = place.cell != null && place.cell.Length > 0 ? place.cell[0] : 0;
                int cy = place.cell != null && place.cell.Length > 1 ? place.cell[1] : 0;
                int cz = place.cell != null && place.cell.Length > 2 ? place.cell[2] : 0;
                go.transform.position = new Vector3(cx * cell, cy * cell, cz * cell);
                go.transform.rotation = Quaternion.Euler(0f, place.yawDeg, 0f);

                instances[instId] = go;
                placedOrder.Add(instId);
                string arch = place.archetype;
                if (string.IsNullOrEmpty(arch))
                {
                    var roomMeta = go.GetComponent<RoomPrefabMeta>();
                    arch = roomMeta != null ? roomMeta.archetype : "combat";
                }
                instanceMeta[instId] = arch ?? "combat";
            }

            // Mate + re-verify (drift) + overlap + seal — the shared DungeonBakerChecks.Compose is
            // the SINGLE source of truth the RoomForgeRegression oracle also drives. It emits the
            // [Flow:DungeonBake] band (per-connection reason enum + seal events) itself (WO-745 §3).
            var outcome = DungeonBakerChecks.Compose(instances, layout);
            int mateOk = outcome.mateOk;
            int sealedN = outcome.sealedN;
            int totalFail = outcome.mateFail + outcome.driftFail + outcome.overlapFail;

            // Pacing lint
            LintPacing(instanceMeta, rules);

            // ---- §2 fix 1: HARD GATE. Any mate/drift/overlap failure => do NOT bake navmesh,
            // do NOT save the shipping scene, do NOT touch Build Settings. Abort with the
            // machine-parseable summary so the failure is a captured line, not a silent bad scene.
            if (totalFail > 0)
            {
                string failSummary = $"SUMMARY id={layout.dungeonId} rooms={instances.Count} " +
                                     $"matesOk={mateOk} matesFail={outcome.ConnectionFail} sealed={sealedN} " +
                                     $"saved=False drift={outcome.driftFail} overlaps={outcome.overlapFail}";
                FlowTrace.Fail(Sys, failSummary + " ABORT: not saving scene, not touching Build Settings (WO-745 fix 1)");

                // Optional debug-only save OUTSIDE Build Settings (default off).
                if (EditorPrefs.GetBool(SaveFailedScenesPref, false))
                {
                    EnsureOutputFolder();
                    string failPath = $"{OutputScenesFolder}/_FAILED_{layout.dungeonId}.unity";
                    EditorSceneManager.MarkSceneDirty(scene);
                    Guard.Try(Sys, "save FAILED debug scene", () => { EditorSceneManager.SaveScene(scene, failPath); });
                    FlowTrace.Warn(Sys, $"saved FAILED debug scene (NOT in Build Settings): {failPath}");
                }
                return;
            }

            // Lighting defaults (dim)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f);
            var lightGo = new GameObject("DirLight");
            lightGo.transform.SetParent(root, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // NavMesh + path-connectivity (stronger than a single origin sample): confirm a path
            // from the first placed room centre to the last actually completes.
            var navHost = new GameObject("NavMesh");
            navHost.transform.SetParent(root, false);
            var surface = navHost.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
            bool walkable = NavMesh.SamplePosition(Vector3.zero, out _, 8f, NavMesh.AllAreas);
            string navResult = "walkable=" + walkable;
            if (placedOrder.Count >= 2 &&
                instances.TryGetValue(placedOrder[0], out var firstGo) &&
                instances.TryGetValue(placedOrder[placedOrder.Count - 1], out var lastGo))
            {
                var path = new NavMeshPath();
                bool got = NavMesh.SamplePosition(firstGo.transform.position, out var fHit, 8f, NavMesh.AllAreas) &&
                           NavMesh.SamplePosition(lastGo.transform.position, out var lHit, 8f, NavMesh.AllAreas) &&
                           NavMesh.CalculatePath(fHit.position, lHit.position, NavMesh.AllAreas, path);
                navResult += $" path[{placedOrder[0]}->{placedOrder[placedOrder.Count - 1]}]={(got ? path.status.ToString() : "NoSample")}";
            }
            FlowTrace.Step(Sys, $"navmesh baked; {navResult}");

            // Optional: seat a playable hero + hero-aggro enemy spawners on the walkable NavMesh
            // (opt-in; only the composed starter-loop passes populateForPlay). Done AFTER the
            // NavMesh bake so both seat on real mesh, and BEFORE SaveScene so it is one bake pass.
            if (populateForPlay)
                PopulateForPlay(root, instances);

            // Save scene
            EnsureOutputFolder();
            string scenePath = $"{OutputScenesFolder}/{layout.dungeonId}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            EnsureInBuildSettings(scenePath);

            // Force TEXT (YAML) serialization. In -batchmode, EditorSceneManager.SaveScene
            // emits a BINARY Unity SerializedFile (mostly-NUL, non-diffable .unity that reads
            // as "corrupt", e.g. the committed binary d4_sunken_crypt.unity) because the running
            // batchmode editor's effective serialization mode is NOT ForceText even though the
            // EditorSettings.asset stores ForceText. Force the mode explicitly, then reserialize
            // the just-saved scene so it lands as %YAML text like every curated scene.
            FlowTrace.Step(Sys, $"serializationMode(before)={EditorSettings.serializationMode}");
            EditorSettings.serializationMode = SerializationMode.ForceText;
            // ForceReserializeAssets will NOT rewrite the currently-OPEN scene, so close it
            // (open a throwaway empty scene) first — the content is already on disk from
            // SaveScene above. Then reserialize the on-disk file under ForceText -> %YAML.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ForceReserializeAssets(new[] { scenePath },
                ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            AssetDatabase.SaveAssets();

            // Self-report the on-disk header so a binary/text regression is a captured line.
            string hdr = Guard.Try(Sys, "read scene header", () =>
            {
                string abs = ToFilesystemPath(scenePath);
                using var fs = new FileStream(abs, FileMode.Open, FileAccess.Read);
                var buf = new byte[5];
                int n = fs.Read(buf, 0, 5);
                return n >= 5 ? Encoding.ASCII.GetString(buf) : "(short)";
            }, "(unread)");
            if (hdr != "%YAML")
                // NOTE: pure -batchmode does NOT honor EditorSettings ForceText for SaveScene or
                // ForceReserializeAssets, so a batch bake lands BINARY (valid + loadable, but not
                // diffable). Running the compose from an OPEN editor (GUI) reserializes to %YAML.
                // Warn (not Fail) so a batch bake does not spam error-level break-log tickets.
                FlowTrace.Warn(Sys, $"scene '{scenePath}' saved BINARY header='{hdr}' (batchmode cannot ForceText; " +
                                    "run compose from the GUI editor to get %YAML text)");
            else
                FlowTrace.Step(Sys, $"scene serialized as TEXT (header='%YAML') at {scenePath}");

            FlowTrace.Step(Sys, $"SUMMARY id={layout.dungeonId} rooms={instances.Count} " +
                                $"matesOk={mateOk} matesFail=0 sealed={sealedN} saved={saved} " +
                                $"path={scenePath} {navResult}");
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(OutputScenesFolder))
                AssetDatabase.CreateFolder("Assets/Scenes", "DungeonCompose");
        }

        private static GameObject LoadRoomPrefab(string prefabStem)
        {
            // Prefer Assets/Dungeon/Rooms/<stem>.prefab
            string p1 = $"Assets/Dungeon/Rooms/{prefabStem}.prefab";
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(p1);
            if (a != null) return a;
            // Resources
            var r = Resources.Load<GameObject>($"Dungeon/Rooms/{prefabStem}");
            if (r != null) return r;
            // GUID search by name
            string[] guids = AssetDatabase.FindAssets($"{prefabStem} t:Prefab");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.Contains("/Rooms/") || path.EndsWith($"/{prefabStem}.prefab"))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) return go;
                }
            }
            return null;
        }

        private static GameObject CreatePlaceholderRoom(string id, Transform parent)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            var meta = go.AddComponent<RoomPrefabMeta>();
            meta.roomId = id;
            meta.archetype = "combat";
            meta.cellSize = 6f;
            meta.footprintCells = Vector2Int.one;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(go.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

            // Four door sockets at cardinals (short ids match rooms-catalog.json convention).
            AddPlaceholderSocket(go, "n_door_01", "N", new Vector3(0, 0, 3f), Vector3.forward);
            AddPlaceholderSocket(go, "s_door_01", "S", new Vector3(0, 0, -3f), Vector3.back);
            AddPlaceholderSocket(go, "e_door_01", "E", new Vector3(3f, 0, 0), Vector3.right);
            AddPlaceholderSocket(go, "w_door_01", "W", new Vector3(-3f, 0, 0), Vector3.left);
            return go;
        }

        private static void AddPlaceholderSocket(GameObject room, string id, string facing, Vector3 local, Vector3 outward)
        {
            var sgo = new GameObject($"Socket_{id}");
            sgo.transform.SetParent(room.transform, false);
            sgo.transform.localPosition = local;
            sgo.transform.localRotation = Quaternion.LookRotation(outward);
            var sock = sgo.AddComponent<RoomSocket>();
            sock.id = id;
            sock.type = RoomSocketType.Door;
            sock.facing = facing;
        }

        private static void LintPacing(Dictionary<string, string> archetypes, ComposeRules rules)
        {
            int combat = 0, lore = 0, reward = 0, other = 0;
            foreach (var a in archetypes.Values)
            {
                string k = (a ?? "").ToLowerInvariant();
                if (k.Contains("combat") || k.Contains("boss")) combat++;
                else if (k.Contains("lore") || k.Contains("story")) lore++;
                else if (k.Contains("reward") || k.Contains("loot") || k.Contains("treasure")) reward++;
                else other++;
            }
            int total = combat + lore + reward + other;
            if (total <= 0) return;
            float rc = combat / (float)total;
            float rl = lore / (float)total;
            float rr = reward / (float)total;
            FlowTrace.Step(Sys, $"pacing rooms={total} combat={rc:P0} (target {rules.pacingCombat:P0}) " +
                                $"lore={rl:P0} (target {rules.pacingLore:P0}) reward={rr:P0} (target {rules.pacingReward:P0}) other={other}");
            // Soft warn only — small spines will not hit 60/20/20.
            if (total >= 5 && Mathf.Abs(rc - rules.pacingCombat) > 0.25f)
                FlowTrace.Warn(Sys, "pacing: combat ratio far from 60/20/20 canon - author more lore/reward rooms");
        }

        // =====================================================================
        // Play population (opt-in) — a bare Player-tagged hero + hero-aggro enemy
        // spawners, so a portal-loaded composed dungeon is enterable + fightable.
        // WHY a baked hero: DungeonPortal enters via SceneManager.LoadScene (Single),
        // which DESTROYS the overworld hero (it is re-homed into its scene by the last
        // SceneTransitionTrigger, not permanently DDOL) — so NO hero carries in. This
        // mirrors Dungeon_HealersCottage baking its own Keeper. HeroControlEnsurer then
        // (runtime, every sceneLoaded) upgrades the bare hero: PlayerAttackController
        // (melee), GearLoadout, and the follow camera. DeNelle.Editor cannot reference
        // DeNelle.Village, so the Village MonoBehaviours are attached by REFLECTION
        // (the DungeonChainBuilder idiom).
        // =====================================================================
        private static void PopulateForPlay(Transform root, Dictionary<string, GameObject> instances)
        {
            // Hero seat = EntryHall (entry node at origin), sampled onto the walkable NavMesh.
            Vector3 entryPos = instances.TryGetValue("entry", out var eGo) && eGo != null
                ? eGo.transform.position : Vector3.zero;
            Vector3 heroPos = SampleNav(entryPos, 8f) + Vector3.up * 0.9f;

            // Visible capsule body (mirrors HeroControlEnsurer.SpawnEmergencyHero so the hero
            // renders even before HeroBodySwapper runs). TOP-LEVEL (no parent) so its
            // transform.root is itself — HeroControlEnsurer.DedupeHeroes destroys a hero's
            // whole root, and parenting under the dungeon root would risk nuking the geometry.
            var hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hero.name = "Hero (Blaise)";
            Guard.Try(Sys, "tag hero Player", () => { hero.tag = "Player"; });
            hero.transform.position = heroPos;
            var hcol = hero.GetComponent<Collider>();
            if (hcol != null) Object.DestroyImmediate(hcol); // HeroLocomotion CapsuleCast must not self-block
            var heroType = FindType("DeNelle.Village.HeroLocomotion");
            if (heroType != null)
            {
                hero.AddComponent(heroType);
                FlowTrace.Step(Sys, $"HERO 'Hero (Blaise)' seated at {heroPos} (+HeroLocomotion; HeroControlEnsurer adds combat+camera at runtime)");
            }
            else
            {
                FlowTrace.Warn(Sys, "HeroLocomotion type unresolved at bake — hero placed WITHOUT locomotion " +
                                    "(runtime HeroControlEnsurer emergency hero would still cover; re-run after compile if this persists)");
            }

            // Enemy spawners: OutpostEnemyGroupSpawner self-spawns 3-7 hero-aggro hollows on
            // Start (heart=null + SetHeroOnlyTarget -> they chase the hero, BattleLock engages).
            // Seated DEEP (junction + two loop rooms), NOT at the entry, so the first fight is a
            // few steps in. Under an "Encounters" root for tidiness (not a hero -> dedup-safe).
            var encRoot = new GameObject("Encounters");
            encRoot.transform.SetParent(root, false);
            var spawnerType = FindType("DeNelle.Village.OutpostEnemyGroupSpawner");
            string[] rooms = { "junction", "loop1", "loop3" };
            int placed = 0;
            foreach (var id in rooms)
            {
                if (!instances.TryGetValue(id, out var rGo) || rGo == null) continue;
                Vector3 pos = SampleNav(rGo.transform.position, 6f);
                var marker = new GameObject($"SkeletonGroup_Spawn_{id}");
                marker.transform.SetParent(encRoot.transform, true);
                marker.transform.position = pos;
                if (spawnerType != null)
                {
                    marker.AddComponent(spawnerType);
                    placed++;
                    FlowTrace.Step(Sys, $"SPAWNER '{id}' (OutpostEnemyGroupSpawner) at {pos}");
                }
                else
                {
                    FlowTrace.Warn(Sys, $"OutpostEnemyGroupSpawner type unresolved — marker '{id}' placed WITHOUT spawner (re-run after compile).");
                }
            }
            FlowTrace.Step(Sys, $"PopulateForPlay done: hero=1 spawners={placed} (each spawns 3-7 hero-aggro hollows at runtime)");
        }

        private static Vector3 SampleNav(Vector3 p, float radius)
            => NavMesh.SamplePosition(p, out var hit, radius, NavMesh.AllAreas) ? hit.position : p;

        // Resolve a runtime type by full name across all loaded assemblies (DeNelle.Editor cannot
        // reference DeNelle.Village, so Village MonoBehaviours are attached by reflection).
        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static void EnsureInBuildSettings(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
                if (s.path == scenePath) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
