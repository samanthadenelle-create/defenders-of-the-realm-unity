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

        // =====================================================================
        // WO-923 — STAIR CONNECTOR RESOLUTION
        // ---------------------------------------------------------------------
        // The graphs/layouts author the SOCKET-ONLY stair nodes "StairDown"/"StairUp". Those
        // prefabs carry the vertical socket and NOTHING ELSE — no flight, no landing — which is
        // why every multi-level bake produced floors that stack but never connect. Verified at
        // source 2026-08-07 by reading the prefab YAML, not by trusting a comment:
        //
        //   Assets/Dungeon/Rooms/StairDown.prefab              -> exactly 2 RoomSockets, 0 geometry
        //     Socket_s_door_01        localPos (0,0,-5)  rot (0,1,0,0)
        //                             id=s_door_01      type=0 facing=S halfWidth=1.1
        //     Socket_stair_StairDown  localPos (0,-3,0)  rot (0.7071068,0,0,0.7071068)
        //                             id=stair_down_01  type=3 facing=U halfWidth=1.2
        //     RoomPrefabMeta          archetype=hub footprintCells {1,1} cellSize=10
        //
        //   Assets/Dungeon/Rooms/StairConnector_Vertical_Down.prefab
        //     -> IDENTICAL on EVERY one of those fields (same socket names, ids, types, facings,
        //        halfWidths, local positions AND local rotations; same footprint/cell/archetype).
        //        Only RoomPrefabMeta.roomId differs. It additionally carries the real
        //        StairAssembly/StairShape_Vertical geometry.
        //
        // That socket parity is the whole reason this can be a NAME-LEVEL swap at the resolution
        // point instead of a graph edit: GraphDungeonComposer already SOLVED and wrote the layout
        // JSON against those socket offsets, and DungeonBakerChecks.Compose re-verifies mate
        // distance + opposing alignment against a hard gate (L176: any failure aborts the bake).
        // Because the connector puts the same sockets in the same places, that gate sees exactly
        // the geometry it saw before and the pre-solved layouts stay valid unedited.
        //
        // VARIANT SEAM — DELIBERATELY NOT CLOSED HERE. Left/Right connectors exist on disk and are
        // catalogued (rooms-catalog.json, both dual copies), but NOTHING in the schema selects a
        // variant: a layout room is {prefab, instanceId, cell, yawDeg, archetype, encounter} and a
        // graph node is {id, prefab, ...} — there is no shape field. Inventing one is a schema
        // change and a separate work order. Until then EVERY stair resolves to VERTICAL. The seam
        // to close is in GraphDungeonComposer (GraphNode ~L69 / the layout write ~L508): let the
        // node name its variant and have the composer emit the CONCRETE connector stem into
        // `prefab`. At that point this alias simply stops firing for those nodes — no edit needed
        // here, because a concrete stem is not a key in this map.
        private static readonly Dictionary<string, string> StairConnectorAliases =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "StairDown", "StairConnector_Vertical_Down" },
                { "StairUp",   "StairConnector_Vertical_Up"   },
            };

        // Per-bake tally of how the stair nodes actually resolved (reset at the top of every bake).
        // Read by DressVerticalStairPorts to decide + REPORT which traversal mode ran.
        private static int _stairConnectorsResolved;
        private static int _stairConnectorsFellBack;

        // WO-923 — are the DungeonPortLink "Descend"/"Climb" teleport prompts STILL placed once a
        // real walkable connector is in the scene?
        //
        // DEFAULT TRUE = KEEP THEM. The ramp is UNPROVEN until a bake reports PathComplete across
        // the floors (today the FlowTrace path check at L293-302 only walks first->last placed
        // room). The ports are the only vertical traversal that has ever worked; removing the
        // working path before its replacement is proven is how a dungeon becomes unplayable. Both
        // can coexist — a port is a prompt the player opts into, not a wall.
        //
        // Flip via EditorPrefs (same idiom as SaveFailedScenesPref above — no recompile, and the
        // shipped DEFAULT is what every clean machine bakes with) ONLY after a multi-level bake
        // reports PathComplete over the connector.
        private const string StairPortsWithConnectorPref = "DungeonBaker.StairPortsWithConnector";
        private const bool StairPortsWithConnectorDefault = true;

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

            float cell = layout.cellSize > 0.1f ? layout.cellSize : RoomForgeCanon.Cell;
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

            // WO-923: per-bake stair tally, zeroed here so a second bake in the same editor
            // session never reports the previous bake's connectors (statics survive between bakes).
            _stairConnectorsResolved = 0;
            _stairConnectorsFellBack = 0;

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

            // WO-923: one captured line per bake saying whether the walkable stair geometry
            // actually landed. 0/0 on a flat layout is the expected reading.
            FlowTrace.Step(Sys, $"STAIR RESOLUTION: connectors={_stairConnectorsResolved} " +
                                $"fallbacks={_stairConnectorsFellBack} (WO-923; a fallback means socket-only " +
                                "stubs and NO walkable link between floors)");

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

            // =================================================================
            // RELIGHT (WO-1004 §1.3) — baked into the SCENE's RenderSettings.
            // -----------------------------------------------------------------
            // RenderSettings persist into the saved .unity, so setting them HERE is what makes
            // EVERY composed dungeon come out moody by default — pipeline-level, never a
            // per-scene hand-fix. Bake Wave 1 (commit 94c23be3) already closed the GEOMETRY half
            // (RoomForgeCanon.WallHeight 2.8->4.0 + DefaultDungeonRoomsBuilder.BuildCeiling) and
            // the skybox half below; the ATMOSPHERE half is this block.
            //
            // The numbers are NOT invented. They are DungeonSceneBuilder's proven WO-1000 values,
            // read at source so both dungeon pipelines light identically:
            //   Assets/Editor/DungeonSceneBuilder.cs L170  AmbientIntensity = 0.05f
            //   Assets/Editor/DungeonSceneBuilder.cs L1987-2000 ConfigureAmbient (fog 14->42 #0a0a10)
            //   Assets/Editor/DungeonSceneBuilder.cs L2018-2029 CreateDirectionalLight (#39414f @ 0.18)
            // WAS: ambient (0.08,0.09,0.12) with NO fog and a 0.35 WHITE directional — i.e. a
            // second sun indoors, which is half of why the enclosed rooms still read as daylight
            // greybox even after the ceilings landed.
            const float ambient = 0.05f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(ambient, ambient, ambient * 1.1f);
            RenderSettings.ambientIntensity = ambient;
            RenderSettings.fog = true;
            // LINEAR (not the Exponential the other builders use): a start/end pair is what gives
            // a corridor a readable near field and a swallowed far end at a KNOWN distance, which
            // is the property WO-1000 tuned. 14 m clear, gone by 42 m.
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.039f, 0.039f, 0.063f); // #0a0a10 dark blue
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 42f;

            // WO-919 sky kill. A NewScene(EmptyScene) inherits the PROCEDURAL SKYBOX in its
            // lighting settings, and RenderSettings persist into the saved .unity - so every
            // composed dungeon shipped a bright blue dome over its (until now, open-top) rooms.
            // Nulling it is safe with ambientMode=Flat above: flat ambient never samples the
            // skybox, so no light changes; only the sky stops being drawn.
            // SCOPE NOTE (still true, and now MEASURED): the composed bake seats no camera, so the
            // clear-flags/background belongs to exactly ONE owner and that owner is the runtime
            // rig. Verified at source 2026-08-07: Assets/_Modules/Village/Hero/HeroControlEnsurer.cs
            // L283-290 creates "GameplayCamera (ensured)" and sets NEITHER clearFlags NOR
            // backgroundColor — so it keeps Unity's defaults (Skybox / #314D79 blue) and, with
            // RenderSettings.skybox null, URP clears to that DEFAULT BLUE. Any hairline the shell
            // leaves therefore still reads as "sky". Fixing that is a Village-side edit and is
            // reported, NOT done here — two owners of one camera field is how it drifts.
            RenderSettings.skybox = null;

            var lightGo = new GameObject("DirLight");
            lightGo.transform.SetParent(root, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            // Faint cold FILL, not a key. Shadows deliberately OFF: with shadows on, the WO-919
            // ceiling slab occludes this light completely and the only illumination left is
            // ambient 0.05 + torches + the runtime Lantern — a near-black room. Shadowless at
            // 0.18 it acts as a uniform legibility floor from above (and costs no shadow pass on
            // mobile, which a whole-dungeon real-time directional shadow map would).
            light.color = new Color(0.224f, 0.255f, 0.310f);      // #39414f
            light.intensity = 0.18f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            FlowTrace.Step(Sys, $"RELIGHT: ambient=Flat {ambient:F2} fog=Linear #0a0a10 " +
                                $"{RenderSettings.fogStartDistance:F0}->{RenderSettings.fogEndDistance:F0}m " +
                                $"skybox=null dirLight=#39414f @{light.intensity:F2} shadows=None " +
                                "(camera background stays owned by the runtime rig - HeroControlEnsurer L283)");

            // Dress each composed room with real props (torches at the corners + barrels/crates/
            // decor against the walls). Props have colliders STRIPPED, and the NavMesh below bakes
            // from PhysicsColliders, so dressing does NOT block or carve the mesh regardless of
            // order; placed against walls with doorway clearance so paths stay clear. Seeded by
            // room index for reproducible bakes. Runs BEFORE the bake so dress+bake is one pass.
            //
            // WO-921 Phase A option A3: the ENTRY/spawn room keeps its torch MESHES but gets NO
            // torch point lights, so the hero never spawns inside the glow. Which rooms those are
            // is layout knowledge, so the BAKER decides and the dresser just obeys.
            var entryRoomIds = ResolveEntryRoomIds(layout);
            // Zero the dresser's per-dungeon realtime-light tally (its statics survive between
            // bakes) so the count logged below is THIS dungeon's, not a running total.
            DungeonDresser.BeginDungeon();
            int dressedRooms = 0, dressedProps = 0, roomIdx = 0, entryRooms = 0;
            foreach (var kv in instances)
            {
                if (kv.Value == null) { roomIdx++; continue; }
                bool isEntry = entryRoomIds.Contains(kv.Key);
                if (isEntry) entryRooms++;
                int seated = DungeonDresser.DressRoom(kv.Value, roomIdx, isEntry);
                if (seated > 0) { dressedRooms++; dressedProps += seated; }
                roomIdx++;
            }
            FlowTrace.Step("Dungeon", $"DRESS complete: rooms={dressedRooms}/{instances.Count} props={dressedProps} " +
                                      $"entryRooms={entryRooms} (torch LIGHTS skipped there - WO-921 A3)");

            // WO-1004 candle pass: the ONE number that says whether this dungeon is within the
            // render budget. dresserLights are the torch flame lights (now seated on each
            // CandleAnchor); +1 is the DirLight created above. Nothing else in a composed bake
            // creates a Light, so this total is the scene's whole realtime light count.
            int dresserLights = DungeonDresser.DungeonLightsSeated;
            FlowTrace.Step("Dungeon", $"LIGHT COUNT dungeon='{layout.dungeonId}': flameLights={dresserLights} " +
                                      $"suppressed={DungeonDresser.DungeonLightsSuppressed} directional=1 " +
                                      $"TOTAL={dresserLights + 1} realtime " +
                                      $"(per-object cap is 4 - DeNelle-URP.asset L48 - and rooms are capped to " +
                                      "that; shadows=None on every one, L49 disallows additional-light shadows)");

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
                PopulateForPlay(root, instances, layout);

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

        /// <summary>
        /// WO-921 Phase A: which instance ids are the ENTRY/spawn room(s). Used ONLY to suppress
        /// cosmetic torch LIGHTS there (option A3) — the meshes still seat, and nothing about
        /// gameplay placement changes.
        ///
        /// The rule deliberately mirrors <see cref="PopulateForPlay"/>'s own hero seat, which is
        /// <c>instances["entry"]</c> and FALLS BACK to <see cref="Vector3.zero"/> when no such id
        /// exists — so the room parked at cell [0,0,0] is the spawn room in that case. Encoding
        /// both halves here keeps "where the hero starts" a single rule rather than two that can
        /// drift apart. Prefab/archetype name matches ("EntryHall", "Entrance") catch layouts that
        /// name the node something else.
        /// </summary>
        private static HashSet<string> ResolveEntryRoomIds(DungeonComposeLayout layout)
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (layout?.rooms == null) return set;
            foreach (var place in layout.rooms)
            {
                if (place == null) continue;
                string id = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                if (string.IsNullOrEmpty(id)) continue;

                bool atOrigin = place.cell != null && place.cell.Length >= 3 &&
                                place.cell[0] == 0 && place.cell[1] == 0 && place.cell[2] == 0;
                bool isEntry = id.Equals("entry", System.StringComparison.OrdinalIgnoreCase) ||
                               Mentions(place.archetype, "entry") || Mentions(place.archetype, "entrance") ||
                               Mentions(place.prefab, "entry") || Mentions(place.prefab, "entrance") ||
                               atOrigin;
                if (isEntry) set.Add(id);
            }
            return set;
        }

        private static bool Mentions(string s, string token)
            => !string.IsNullOrEmpty(s) &&
               s.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(OutputScenesFolder))
                AssetDatabase.CreateFolder("Assets/Scenes", "DungeonCompose");
        }

        /// <summary>
        /// Resolve a layout's room prefab stem to an asset, applying the WO-923 stair-connector
        /// alias FIRST (see <see cref="StairConnectorAliases"/> for the socket-parity proof and
        /// the variant seam). A missing connector DEGRADES to the original socket-only prefab with
        /// a warning — today's behaviour, never a throw and never a blocked bake.
        /// </summary>
        private static GameObject LoadRoomPrefab(string prefabStem)
        {
            if (!string.IsNullOrEmpty(prefabStem) &&
                StairConnectorAliases.TryGetValue(prefabStem, out string connectorStem))
            {
                var connector = LoadRoomPrefabByStem(connectorStem);
                if (connector != null)
                {
                    _stairConnectorsResolved++;
                    FlowTrace.Step(Sys, $"STAIR CONNECTOR '{prefabStem}' -> '{connectorStem}' (WO-923; " +
                                        "VERTICAL is the default variant - no schema field selects Left/Right yet)");
                    return connector;
                }
                _stairConnectorsFellBack++;
                FlowTrace.Warn(Sys, $"STAIR CONNECTOR MISSING '{connectorStem}' - falling back to the socket-only " +
                                    $"'{prefabStem}'. Floors will STACK BUT NOT CONNECT; vertical traversal stays on " +
                                    "the DungeonPortLink ports. Rebuild via " +
                                    "DeNelle.Editor.RoomForge.DefaultStairConnectorRoomsBuilder.BuildAllBatch.");
            }
            return LoadRoomPrefabByStem(prefabStem);
        }

        /// <summary>Raw stem -> prefab lookup (Rooms folder, then Resources, then a GUID search).</summary>
        private static GameObject LoadRoomPrefabByStem(string prefabStem)
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
            meta.cellSize = RoomForgeCanon.Cell;
            meta.footprintCells = Vector2Int.one;

            // WO-922: derived from the canon cell, not a hardcoded 6. A placeholder that stayed
            // 6x6 inside a 10u kit would mate at the wrong reach AND under-report its footprint
            // to the overlap check - a missing prefab would have silently produced a broken
            // layout instead of an obviously-stubbed one.
            float span = RoomForgeCanon.Cell;
            float half = span * 0.5f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(go.transform, false);
            floor.transform.localPosition = new Vector3(0f, -RoomForgeCanon.FloorSlabThickness * 0.5f, 0f);
            floor.transform.localScale = new Vector3(span, RoomForgeCanon.FloorSlabThickness, span);
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

            // Four door sockets at cardinals (short ids match rooms-catalog.json convention).
            AddPlaceholderSocket(go, "n_door_01", "N", new Vector3(0, 0, half), Vector3.forward);
            AddPlaceholderSocket(go, "s_door_01", "S", new Vector3(0, 0, -half), Vector3.back);
            AddPlaceholderSocket(go, "e_door_01", "E", new Vector3(half, 0, 0), Vector3.right);
            AddPlaceholderSocket(go, "w_door_01", "W", new Vector3(-half, 0, 0), Vector3.left);
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
        private static void PopulateForPlay(Transform root, Dictionary<string, GameObject> instances,
                                            DungeonComposeLayout layout)
        {
            // Hero seat = EntryHall (entry node at origin), sampled onto the walkable NavMesh.
            Vector3 entryPos = instances.TryGetValue("entry", out var eGo) && eGo != null
                ? eGo.transform.position : Vector3.zero;
            Vector3 heroPos = SampleNav(entryPos, 8f) + Vector3.up * 0.9f;

            // Hero root + "HeroBody" child (WO-796 kill-shot, audit 2026-08-01): the canonical
            // hero shape — DungeonSceneBuilder.BuildHero and HeroControlEnsurer.SpawnEmergencyHero
            // both build an EMPTY root with a child named "HeroBody". HeroBodySwapper (added
            // below) finds + REPLACES that child with the player's real animated class FBX at
            // runtime, and HeroLocomotion also looks for a "HeroBody" child. The old bare-capsule
            // ROOT could never be swapped, so the composed dungeon stayed a white pill forever.
            // TOP-LEVEL (no parent) so its transform.root is itself — HeroControlEnsurer.
            // DedupeHeroes destroys a hero's whole root, and parenting under the dungeon root
            // would risk nuking the geometry. NOTE: already-baked DungeonCompose scenes carry the
            // OLD bare-capsule hero — a RE-BAKE is required for this shape to take effect.
            var hero = new GameObject("Hero (Blaise)");
            Guard.Try(Sys, "tag hero Player", () => { hero.tag = "Player"; });
            hero.transform.position = heroPos;

            // Visible fallback body — child named "HeroBody" so the swapper destroys + replaces
            // it. Collider stripped so HeroLocomotion's CapsuleCast can't self-block (it sweeps
            // against OTHER colliders for walls — audit note; do not restore it). Tinted emissive
            // warm amber (c98a3a, matching DungeonSceneBuilder's pill fix) so a swap-miss reads
            // as an intentional stand-in, never a blank white pill.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "HeroBody";
            body.transform.SetParent(hero.transform, false);
            var hcol = body.GetComponent<Collider>();
            if (hcol != null) Object.DestroyImmediate(hcol); // HeroLocomotion CapsuleCast must not self-block
            TintFallbackHeroBody(body);

            var heroType = FindType("DeNelle.Village.HeroLocomotion");
            if (heroType != null)
            {
                hero.AddComponent(heroType);
                FlowTrace.Step(Sys, $"HERO 'Hero (Blaise)' seated at {heroPos} (root + 'HeroBody' child, +HeroLocomotion; HeroControlEnsurer adds combat+camera at runtime)");
            }
            else
            {
                FlowTrace.Warn(Sys, "HeroLocomotion type unresolved at bake — hero placed WITHOUT locomotion " +
                                    "(runtime HeroControlEnsurer emergency hero would still cover; re-run after compile if this persists)");
            }
            // HeroBodySwapper on the ROOT alongside HeroLocomotion — swaps the "HeroBody"
            // capsule for the persisted class FBX at runtime. Same reflection idiom as above
            // (DeNelle.Editor cannot reference DeNelle.Village); mirrors DungeonSceneBuilder's
            // AddComponentByName(heroGo, "DeNelle.Village.HeroBodySwapper").
            var swapperType = FindType("DeNelle.Village.HeroBodySwapper");
            if (swapperType != null)
            {
                hero.AddComponent(swapperType);
                FlowTrace.Step(Sys, "HeroBodySwapper attached to hero root -- 'HeroBody' swaps to the real class body at runtime");
            }
            else
            {
                FlowTrace.Warn(Sys, "HeroBodySwapper type unresolved at bake -- amber stand-in body will persist (re-run after compile).");
            }

            // Enemy spawners (WO-797 — rooms OWN their enemies): driven by the layout's
            // per-room encounter blocks, NOT a hardcoded room literal (the old
            // {junction,loop1,loop3} array is deleted). Each encounter room gets one
            // OutpostEnemyGroupSpawner seated at its room centre, and the spawner's
            // room-ownership fields (roomId/areaCenter/areaSize/areaSlack/wakeRadius +
            // counts) are written via SerializedObject so they LAND IN THE SCENE — the
            // 2026-07-20 bake predated the leash field entirely and only the C# default
            // armed it (data-proven cause 4). Under an "Encounters" root (dedup-safe).
            var encRoot = new GameObject("Encounters");
            encRoot.transform.SetParent(root, false);
            var spawnerType = FindType("DeNelle.Village.OutpostEnemyGroupSpawner");
            int placed = 0, specs = 0;
            foreach (var place in layout.rooms)
            {
                if (place == null || place.encounter == null) continue;
                var enc = place.encounter;
                if (!string.IsNullOrEmpty(enc.kind) &&
                    enc.kind.Equals("none", System.StringComparison.OrdinalIgnoreCase)) continue;
                specs++;

                string id = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                if (!instances.TryGetValue(id, out var rGo) || rGo == null)
                {
                    FlowTrace.Warn(Sys, $"encounter room '{id}' has no baked instance - spawner skipped");
                    continue;
                }

                // Room AABB via the ONE shared math (DungeonRoomBounds — same code the
                // runtime binder and the regression oracle use). Seat at the room centre,
                // nav-sampled, and verify the seat lands inside the room's own footprint
                // (the WO-797 oracle: a seat outside its room re-creates the drift bug).
                Bounds roomBounds = DungeonRoomBounds.Compute(rGo);
                Vector3 seat = new Vector3(roomBounds.center.x, rGo.transform.position.y, roomBounds.center.z);
                Vector3 pos = SampleNav(seat, 6f);
                if (!DungeonRoomBounds.ContainsXZ(roomBounds, pos))
                    FlowTrace.Warn(Sys, $"encounter room '{id}': nav-sampled seat {pos} is OUTSIDE the room " +
                        $"footprint c{roomBounds.center} s{roomBounds.size} - check the room's NavMesh");

                var marker = new GameObject($"SkeletonGroup_Spawn_{id}");
                marker.transform.SetParent(encRoot.transform, true);
                marker.transform.position = pos;
                if (spawnerType != null)
                {
                    var comp = marker.AddComponent(spawnerType);
                    WriteEncounterFields(comp, id, roomBounds, enc);
                    placed++;
                    FlowTrace.Step(Sys, $"SPAWNER '{id}' (OutpostEnemyGroupSpawner) at {pos} - room-bound " +
                        $"c{roomBounds.center} s{roomBounds.size} count {enc.min}-{enc.max} " +
                        $"kind '{(string.IsNullOrEmpty(enc.kind) ? "<empty>" : enc.kind)}' " +
                        $"wake {(enc.confine != null ? enc.confine.wakeRadius : 6f):F1} " +
                        $"slack {(enc.confine != null ? enc.confine.slack : 2f):F1}");
                }
                else
                {
                    FlowTrace.Warn(Sys, $"OutpostEnemyGroupSpawner type unresolved — marker '{id}' placed WITHOUT spawner (re-run after compile).");
                }
            }
            if (specs == 0)
                FlowTrace.Warn(Sys, $"layout '{layout.dungeonId}' has NO encounter blocks - 0 spawners placed " +
                    "(WO-797: author room.encounter blocks in the graph/layout JSON)");

            // WO-1001 slice 1b (default: triggered floor transition — refine to walk-through later).
            // Multi-level floors are separate navmesh islands (PathPartial). Mated StairUp/StairDown
            // pairs get DungeonPortLink ports so the Keeper can Descend/Climb between floors.
            int stairPorts = DressVerticalStairPorts(root, instances, hero.transform);

            // WO-1001 slices 4–8: chests, oil, traps, keys/locks, extract points.
            int chests = PlaceComposeChests(root, instances, layout);
            int oil = PlaceComposeOilStones(root, instances, layout);
            int traps = PlaceComposeTraps(root, instances, layout);
            int keys = PlaceComposeKeys(root, instances, layout);
            int locks = PlaceComposeLocks(root, instances, layout, hero.transform);
            int extracts = PlaceComposeExtracts(root, instances, layout);

            FlowTrace.Step(Sys, $"PopulateForPlay done: hero=1 encounterRooms={specs} spawners={placed} " +
                $"stairPorts={stairPorts} chests={chests} oilStones={oil} traps={traps} " +
                $"keys={keys} locks={locks} extracts={extracts} (WO-1001 1b–8)");
        }

        /// <summary>
        /// WO-1001 slice 1b: for every mated vertical stair pair, seat two
        /// <c>DungeonPortLink</c>s (Descend / Climb) that fade+warp the Keeper between floor
        /// islands. Reuses the cottage WO-711 port idiom — no staircase mesh, no NavMeshLink
        /// yet. Owner can later swap to walk-through without re-authoring graph edges.
        /// </summary>
        private static int DressVerticalStairPorts(Transform root, Dictionary<string, GameObject> instances,
                                                   Transform hero)
        {
            if (hero == null || instances == null || instances.Count == 0) return 0;

            // Group mated vertical sockets by connection id (matedTo = "a.sock::b.sock").
            // Sealed markers (SEALED_VERTICAL / SEALED_WALL / SEALED_SECRET) are skipped.
            var byConn = new Dictionary<string, List<RoomSocket>>();
            foreach (var kv in instances)
            {
                if (kv.Value == null) continue;
                foreach (var s in kv.Value.GetComponentsInChildren<RoomSocket>(true))
                {
                    if (s == null || string.IsNullOrEmpty(s.matedTo)) continue;
                    if (s.matedTo.StartsWith("SEALED_", System.StringComparison.Ordinal)) continue;
                    if (!DungeonBakerChecks.IsVertical(s.type)) continue;
                    if (!byConn.TryGetValue(s.matedTo, out var list))
                    {
                        list = new List<RoomSocket>(2);
                        byConn[s.matedTo] = list;
                    }
                    list.Add(s);
                }
            }

            // WO-923 — WHICH TRAVERSAL MODE RAN. Once a real walkable connector is placed the
            // ports become the FALLBACK path rather than the primary one, but they are not ripped
            // out: see StairPortsWithConnectorPref for why the default keeps them. Whichever way
            // this lands, it lands as a captured line — never a silent difference between bakes.
            bool keepPorts = EditorPrefs.GetBool(StairPortsWithConnectorPref, StairPortsWithConnectorDefault);
            bool connectorPresent = _stairConnectorsResolved > 0;
            if (connectorPresent && !keepPorts)
            {
                FlowTrace.Step(Sys, $"STAIR PORT MODE=connector-only: {_stairConnectorsResolved} walkable connector " +
                                    $"room(s) placed and EditorPref '{StairPortsWithConnectorPref}'=false, so " +
                                    $"{byConn.Count} mated vertical pair(s) get NO DungeonPortLink (walk the ramp).");
                return 0;
            }
            FlowTrace.Step(Sys, "STAIR PORT MODE=" +
                                (connectorPresent
                                    ? "connector+ports (WO-923 default: the ramp is unproven until a bake reports PathComplete)"
                                    : "ports-only (no walkable connector resolved - ports ARE the traversal)") +
                                $" connectors={_stairConnectorsResolved} fallbacks={_stairConnectorsFellBack} " +
                                $"pairs={byConn.Count} keepPorts={keepPorts}");

            var portType = FindType("DeNelle.Dungeons.DungeonPortLink");
            if (portType == null)
            {
                FlowTrace.Warn(Sys, "DungeonPortLink type unresolved — vertical stair ports skipped (1b)");
                return 0;
            }

            var portsRoot = new GameObject("StairPorts");
            portsRoot.transform.SetParent(root, false);

            int placed = 0;
            foreach (var kv in byConn)
            {
                if (kv.Value == null || kv.Value.Count != 2)
                {
                    FlowTrace.Warn(Sys, $"stair port skip conn='{kv.Key}' mates={kv.Value?.Count ?? 0} (need exactly 2)");
                    continue;
                }

                var a = kv.Value[0];
                var b = kv.Value[1];
                // Prefer A = StairDown (upper) so prompts read correctly; order is free if swapped.
                if (a.type == RoomSocketType.StairUp && b.type == RoomSocketType.StairDown)
                {
                    var tmp = a; a = b; b = tmp;
                }

                Vector3 seatA = SeatOnFloorNearSocket(a);
                Vector3 seatB = SeatOnFloorNearSocket(b);
                float faceTowardB = YawToward(seatA, seatB);
                float faceTowardA = YawToward(seatB, seatA);

                // Upper floor (StairDown) → Descend to lower.
                placed += PlaceStairPort(portsRoot.transform, portType, hero,
                    name: $"StairPort_Descend_{SanitizeId(kv.Key)}",
                    prompt: "Descend",
                    standAt: seatA,
                    target: seatB,
                    faceY: faceTowardB,
                    fromLabel: a.type.ToString(),
                    toLabel: b.type.ToString()) ? 1 : 0;

                // Lower floor (StairUp) → Climb to upper.
                placed += PlaceStairPort(portsRoot.transform, portType, hero,
                    name: $"StairPort_Climb_{SanitizeId(kv.Key)}",
                    prompt: "Climb",
                    standAt: seatB,
                    target: seatA,
                    faceY: faceTowardA,
                    fromLabel: b.type.ToString(),
                    toLabel: a.type.ToString()) ? 1 : 0;

                FlowTrace.Step(Sys,
                    $"stair port pair conn='{kv.Key}' Descend@{seatA:F1} Climb@{seatB:F1} " +
                    $"(triggered transition — WO-1001 1b default)");
            }

            if (placed == 0)
                FlowTrace.Step(Sys, "DressVerticalStairPorts: no mated vertical pairs (flat layout — OK)");
            return placed;
        }

        private static Vector3 SeatOnFloorNearSocket(RoomSocket sock)
        {
            // Socket sits half a floor off room origin for vertical mates — project to the
            // room's floor plane (room root Y) then nav-sample so the stand point is on mesh.
            var room = sock.transform;
            while (room.parent != null && room.GetComponent<RoomPrefabMeta>() == null)
                room = room.parent;
            float floorY = room.position.y;
            Vector3 want = new Vector3(sock.WorldPosition.x, floorY, sock.WorldPosition.z);
            // Nudge slightly into the room (opposite outward) so the port is not in the wall.
            want -= sock.Outward * 1.2f;
            want.y = floorY;
            return SampleNav(want, 4f) + Vector3.up * 0.05f;
        }

        private static float YawToward(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return 0f;
            return Quaternion.LookRotation(d.normalized, Vector3.up).eulerAngles.y;
        }

        private static string SanitizeId(string connId)
        {
            if (string.IsNullOrEmpty(connId)) return "stair";
            var sb = new StringBuilder(connId.Length);
            foreach (char c in connId)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        private static bool PlaceStairPort(Transform parent, System.Type portType, Transform hero,
            string name, string prompt, Vector3 standAt, Vector3 target, float faceY,
            string fromLabel, string toLabel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = standAt;
            var link = go.AddComponent(portType);
            // Configure(prompt, target, faceY, hero, dungeonHero, from, to, radius)
            var configure = portType.GetMethod("Configure",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (configure == null)
            {
                FlowTrace.Warn(Sys, "DungeonPortLink.Configure not found — stair port not wired");
                Object.DestroyImmediate(go);
                return false;
            }
            configure.Invoke(link, new object[]
            {
                prompt, target, faceY, hero, null, fromLabel, toLabel, 2.2f
            });
            return true;
        }

        // WO-797: write the spawner's serialized room-ownership + count fields through
        // SerializedObject so they persist INTO THE SCENE FILE (an AddComponent field set
        // via reflection would serialize too, but SerializedObject is the editor-canonical
        // path and survives FormerlySerializedAs renames). Field names mirror
        // OutpostEnemyGroupSpawner's [SerializeField] privates.
        private static void WriteEncounterFields(Component spawner, string roomId, Bounds roomBounds,
                                                 DeNelle.Dungeons.RoomForge.EncounterSpec enc)
        {
            var so = new SerializedObject(spawner);
            SetString(so, "roomId", roomId);
            // WO-1001 slice 2: the AUTHORED encounter family. Before this, EncounterSpec.kind
            // was compared ONLY to "none" and every other value fell through to the same
            // hollow spawn - authoring "orc-group" SILENTLY SPAWNED HOLLOWS. Written raw (not
            // normalised) on purpose: DeNelle.Editor cannot reference DeNelle.Village, so the
            // spawner owns the one kind-resolution table and warns at runtime on an unknown
            // value. DungeonEncounterFamilyRegression fails the gate if a shipped layout
            // authors a kind the spawner does not know.
            SetString(so, "encounterKind", string.IsNullOrEmpty(enc.kind) ? "hollow-group" : enc.kind.Trim());
            // WO-1001 slice 3: boss / fixed elite.
            SetBool(so, "isBoss", enc.isBoss);
            SetString(so, "fixedEnemyId", enc.enemyType ?? "");
            if (enc.isBoss)
            {
                SetInt(so, "minCount", 1);
                SetInt(so, "maxCount", 1);
            }
            else
            {
                SetInt(so, "minCount", Mathf.Max(1, enc.min));
                SetInt(so, "maxCount", Mathf.Max(Mathf.Max(1, enc.min), enc.max));
            }
            SetVector3(so, "areaCenter", roomBounds.center);
            SetVector3(so, "areaSize", roomBounds.size);
            SetFloat(so, "areaSlack", enc.confine != null ? enc.confine.slack : 2f);
            SetFloat(so, "wakeRadius", enc.confine != null ? enc.confine.wakeRadius : 6f);
            if (enc.formationRadius > 0f) SetFloat(so, "formationRadius", enc.formationRadius);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
            else FlowTrace.Warn(Sys, $"spawner field '{field}' not found - SerializedObject write skipped");
        }

        /// <summary>WO-1001 slice 4: seat BreakableContainer loot props from layout.chests.</summary>
        private static int PlaceComposeChests(Transform root, Dictionary<string, GameObject> instances,
                                             DungeonComposeLayout layout)
        {
            if (layout?.rooms == null) return 0;
            var chestRoot = new GameObject("Chests");
            chestRoot.transform.SetParent(root, false);
            int n = 0;
            foreach (var place in layout.rooms)
            {
                if (place?.chests == null || place.chests.Count == 0) continue;
                string id = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                if (!instances.TryGetValue(id, out var rGo) || rGo == null) continue;
                Bounds roomBounds = DungeonRoomBounds.Compute(rGo);
                Vector3 centre = new Vector3(roomBounds.center.x, rGo.transform.position.y, roomBounds.center.z);
                foreach (var c in place.chests)
                {
                    if (c == null) continue;
                    Vector3 off = Offset3(c.offset);
                    Vector3 pos = SampleNav(centre + off, 4f);
                    string table = string.IsNullOrEmpty(c.lootTableId) ? "dungeon-chest" : c.lootTableId;
                    string visual = string.IsNullOrEmpty(c.visual) ? "chest" : c.visual;
                    var bcType = FindType("DeNelle.Village.BreakableContainer");
                    if (bcType == null)
                    {
                        FlowTrace.Warn(Sys, "BreakableContainer missing — chest not placed");
                        continue;
                    }
                    var create = bcType.GetMethod("Create",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (create == null) continue;
                    var created = create.Invoke(null, new object[] { chestRoot.transform, pos, table, visual });
                    // WO-1001 slice 6: deepboss legendary gated on darkness.
                    if (created is Component bcComp &&
                        table.IndexOf("deepboss", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var gateType = FindType("DeNelle.Dungeons.ComposedLegendaryGate");
                        if (gateType != null) bcComp.gameObject.AddComponent(gateType);
                    }
                    n++;
                    FlowTrace.Step(Sys, $"CHEST '{c.id ?? visual}' table='{table}' @ {pos} room='{id}'");
                }
            }
            if (n == 0) Object.DestroyImmediate(chestRoot);
            return n;
        }

        /// <summary>WO-1001 slice 5: seat ComposedOilStone markers for lantern refill.</summary>
        private static int PlaceComposeOilStones(Transform root, Dictionary<string, GameObject> instances,
                                                DungeonComposeLayout layout)
        {
            if (layout?.oilStones == null || layout.oilStones.Count == 0) return 0;
            var oilRoot = new GameObject("OilStones");
            oilRoot.transform.SetParent(root, false);
            var markerType = FindType("DeNelle.Dungeons.ComposedOilStone");
            if (markerType == null)
            {
                FlowTrace.Warn(Sys, "ComposedOilStone type missing — oil stones not placed");
                return 0;
            }
            int n = 0;
            foreach (var stone in layout.oilStones)
            {
                if (stone == null) continue;
                Vector3 pos = Vector3.zero;
                if (!string.IsNullOrEmpty(stone.roomId) &&
                    instances.TryGetValue(stone.roomId, out var rGo) && rGo != null)
                {
                    Bounds b = DungeonRoomBounds.Compute(rGo);
                    pos = new Vector3(b.center.x, rGo.transform.position.y, b.center.z) + Offset3(stone.offset);
                }
                else
                {
                    pos = Offset3(stone.offset);
                }
                pos = SampleNav(pos, 4f);
                var go = new GameObject(string.IsNullOrEmpty(stone.id) ? "OilStone" : $"OilStone_{stone.id}");
                go.transform.SetParent(oilRoot.transform, false);
                go.transform.position = pos;
                var marker = go.AddComponent(markerType);
                var configure = markerType.GetMethod("Configure",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                configure?.Invoke(marker, new object[] { stone.id ?? "oil", stone.radius > 0f ? stone.radius : 2.5f });
                n++;
                FlowTrace.Step(Sys, $"OILSTONE '{stone.id}' @ {pos} r={stone.radius:F1}");
            }
            return n;
        }

        private static Vector3 Offset3(float[] o)
        {
            if (o == null || o.Length == 0) return Vector3.zero;
            float x = o.Length > 0 ? o[0] : 0f;
            float y = o.Length > 1 ? o[1] : 0f;
            float z = o.Length > 2 ? o[2] : 0f;
            return new Vector3(x, y, z);
        }

        private static Vector3 RoomSeat(Dictionary<string, GameObject> instances, string roomId, float[] offset)
        {
            if (!string.IsNullOrEmpty(roomId) && instances.TryGetValue(roomId, out var rGo) && rGo != null)
            {
                Bounds b = DungeonRoomBounds.Compute(rGo);
                return SampleNav(new Vector3(b.center.x, rGo.transform.position.y, b.center.z) + Offset3(offset), 4f);
            }
            return SampleNav(Offset3(offset), 4f);
        }

        /// <summary>WO-1001 slice 7: step-on traps.</summary>
        private static int PlaceComposeTraps(Transform root, Dictionary<string, GameObject> instances,
                                            DungeonComposeLayout layout)
        {
            if (layout?.traps == null || layout.traps.Count == 0) return 0;
            var trapRoot = new GameObject("Traps");
            trapRoot.transform.SetParent(root, false);
            var trapType = FindType("DeNelle.Dungeons.ComposedTrapHazard");
            if (trapType == null) { FlowTrace.Warn(Sys, "ComposedTrapHazard missing"); return 0; }
            int n = 0;
            foreach (var t in layout.traps)
            {
                if (t == null) continue;
                Vector3 pos = RoomSeat(instances, t.roomId, t.offset);
                var go = new GameObject(string.IsNullOrEmpty(t.id) ? "Trap" : $"Trap_{t.id}");
                go.transform.SetParent(trapRoot.transform, false);
                go.transform.position = pos;
                var comp = go.AddComponent(trapType);
                trapType.GetMethod("Configure")?.Invoke(comp, new object[]
                {
                    t.id ?? "trap", t.kind ?? "spike", t.damage > 0f ? t.damage : 12f,
                    t.radius > 0f ? t.radius : 1.4f
                });
                n++;
                FlowTrace.Step(Sys, $"TRAP '{t.id}' kind='{t.kind}' dmg={t.damage} @ {pos}");
            }
            return n;
        }

        /// <summary>WO-1001 slice 7: key pickups.</summary>
        private static int PlaceComposeKeys(Transform root, Dictionary<string, GameObject> instances,
                                           DungeonComposeLayout layout)
        {
            if (layout?.keys == null || layout.keys.Count == 0) return 0;
            var keyRoot = new GameObject("Keys");
            keyRoot.transform.SetParent(root, false);
            var keyType = FindType("DeNelle.Dungeons.ComposedKeyPickup");
            if (keyType == null) { FlowTrace.Warn(Sys, "ComposedKeyPickup missing"); return 0; }
            int n = 0;
            foreach (var k in layout.keys)
            {
                if (k == null) continue;
                Vector3 pos = RoomSeat(instances, k.roomId, k.offset) + Vector3.up * 0.4f;
                var go = new GameObject(string.IsNullOrEmpty(k.id) ? "Key" : $"Key_{k.id}");
                go.transform.SetParent(keyRoot.transform, false);
                go.transform.position = pos;
                var sphere = go.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 1.1f;
                var comp = go.AddComponent(keyType);
                keyType.GetMethod("Configure")?.Invoke(comp, new object[] { k.keyId ?? "key" });
                n++;
                FlowTrace.Step(Sys, $"KEY '{k.keyId}' @ {pos}");
            }
            return n;
        }

        /// <summary>WO-1001 slice 7: locked ports between rooms.</summary>
        private static int PlaceComposeLocks(Transform root, Dictionary<string, GameObject> instances,
                                            DungeonComposeLayout layout, Transform hero)
        {
            if (layout?.locks == null || layout.locks.Count == 0 || hero == null) return 0;
            var lockRoot = new GameObject("Locks");
            lockRoot.transform.SetParent(root, false);
            var lockType = FindType("DeNelle.Dungeons.ComposedLockedPort");
            if (lockType == null) { FlowTrace.Warn(Sys, "ComposedLockedPort missing"); return 0; }
            int n = 0;
            foreach (var L in layout.locks)
            {
                if (L == null) continue;
                Vector3 from = RoomSeat(instances, L.fromRoomId, L.fromOffset);
                Vector3 to = RoomSeat(instances, L.toRoomId, L.toOffset);
                float face = YawToward(from, to);
                var go = new GameObject(string.IsNullOrEmpty(L.id) ? "Lock" : $"Lock_{L.id}");
                go.transform.SetParent(lockRoot.transform, false);
                go.transform.position = from;
                var comp = go.AddComponent(lockType);
                lockType.GetMethod("Configure")?.Invoke(comp, new object[]
                {
                    L.keyId ?? "key", to, face, hero,
                    "Locked — need key", "Unlock & pass", 2.2f
                });
                n++;
                FlowTrace.Step(Sys, $"LOCK '{L.id}' key='{L.keyId}' {from} -> {to}");
            }
            return n;
        }

        /// <summary>WO-1001 slice 8: per-floor extract pads (bank-and-leave via DungeonExitInteractable.Spawn).</summary>
        private static int PlaceComposeExtracts(Transform root, Dictionary<string, GameObject> instances,
                                               DungeonComposeLayout layout)
        {
            if (layout?.extracts == null || layout.extracts.Count == 0) return 0;
            var exitType = FindType("DeNelle.Dungeons.DungeonExitInteractable");
            if (exitType == null)
            {
                FlowTrace.Warn(Sys, "DungeonExitInteractable missing — extracts not placed");
                return 0;
            }
            var spawn = exitType.GetMethod("Spawn",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (spawn == null) return 0;
            int n = 0;
            foreach (var e in layout.extracts)
            {
                if (e == null) continue;
                Vector3 pos = RoomSeat(instances, e.roomId, e.offset);
                string label = string.IsNullOrEmpty(e.label) ? "Extract" : e.label;
                // Spawn(position, onLeave, label)
                var exit = spawn.Invoke(null, new object[] { pos, null, label }) as Component;
                if (exit != null)
                {
                    exit.gameObject.name = string.IsNullOrEmpty(e.id) ? "Extract" : $"Extract_{e.id}";
                    exit.transform.SetParent(root, true);
                    n++;
                    FlowTrace.Step(Sys, $"EXTRACT '{e.id}' label='{label}' @ {pos}");
                }
            }
            return n;
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
            else FlowTrace.Warn(Sys, $"spawner field '{field}' not found - SerializedObject write skipped (rename drift?)");
        }

        private static void SetVector3(SerializedObject so, string field, Vector3 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector3Value = value;
            else FlowTrace.Warn(Sys, $"spawner field '{field}' not found - SerializedObject write skipped (rename drift?)");
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else FlowTrace.Warn(Sys, $"spawner field '{field}' not found - SerializedObject write skipped (rename drift?)");
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else FlowTrace.Warn(Sys, $"spawner field '{field}' not found - SerializedObject write skipped (rename drift?)");
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

        // WO-796 (audit 2026-08-01): emissive warm-amber fallback tint for the composed-dungeon
        // hero stand-in — mirrors DungeonSceneBuilder's ApplyEmissive(body, HexColor("c98a3a"),
        // 0.6f) so BOTH bake paths' swap-miss capsules read identically as intentional stand-ins,
        // never a blank white pill under the oil-lantern lights.
        private static void TintFallbackHeroBody(GameObject body)
        {
            var renderer = body != null ? body.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            Color amber = ColorUtility.TryParseHtmlString("#c98a3a", out var c)
                ? c : new Color(0.788f, 0.541f, 0.227f);
            var mat = new Material(shader) { color = amber };
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", amber * 0.6f);
            renderer.sharedMaterial = mat;
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
