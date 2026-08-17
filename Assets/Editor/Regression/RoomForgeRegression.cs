// =============================================================================
// RoomForgeRegression (WO-745) — the Room Forge pipeline permission gate.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only).  Namespace: DeNelle.Editor.Regression
//
// Markers: ROOMFORGE_REGRESSION_OK / ROOMFORGE_REGRESSION_FAIL (FAIL via
// Debug.LogError -> break-log.jsonl, per docs/INSTRUMENTATION_STANDARD.md §4/§5).
//
// Runs standalone:
//   run-unity-method DeNelle.Editor.Regression.RoomForgeRegression.RunAll
// AND is wired into DataRegression.RunAll as [room-forge] (Run(out string reason),
// the same covenant-style contract as the sibling oracles).
//
// The suite pins the door-touch-door contract by driving the SHARED
// DeNelle.Dungeons.RoomForge.DungeonBakerChecks (the SAME code the editor
// DungeonBaker composes with) — NO logic is duplicated here. Every case builds
// throwaway in-memory GameObjects (torn down in finally); it NEVER opens or saves a
// shipping .unity scene, and it references NO KayKit art (passes with the pack ABSENT).
//
// The 11 cases (WO-745 §4, + case 11 from WO-919/WO-922):
//   1  catalog integrity        6  hard gate (fix 1 — abort on any failure)
//   2  dual-copy law            7  re-verify (drift) + overlap (fix 2)
//   3  TypesCompatible matrix   8  navmesh path-connectivity (best-effort headless)
//   4  mate math (synthetic)    9  sample layouts green (spine + demo, sealed pin)
//   5  seal behavior           10  determinism + hygiene
//                              11  SHIPPED room shells match RoomForgeCanon (cell/wall/ceiling)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Unity.AI.Navigation;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.Regression
{
    public static class RoomForgeRegression
    {
        private const string LayoutsDir = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts";
        private const string LayoutsDirRes = "Assets/Resources/Data/Canonical/dungeon-layouts";
        private const string RoomsFolder = "Assets/Dungeon/Rooms";
        private const int ExpectedRoomCount = 17;   // WO-741 default library

        // Expected sealed-count pins for the sample layouts (WO-745 §4 case 9). One unmated
        // socket each: the dungeon-mouth's south door (start.s_door_01 / EntryHall.s_door_01).
        private const int SpineSealedExpected = 1;
        private const int DemoSealedExpected = 1;

        private static readonly List<GameObject> s_spawned = new List<GameObject>();

        // ---- entry points -------------------------------------------------------

        /// <summary>Standalone batch entry — prints the ROOMFORGE_REGRESSION_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ROOMFORGE_REGRESSION_OK - " + reason);
            else Debug.LogError("ROOMFORGE_REGRESSION_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([room-forge]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "catalog", () => Case1_CatalogIntegrity(failures));
                Case(failures, "dual-copy", () => Case2_DualCopy(failures));
                Case(failures, "types", () => Case3_TypesMatrix(failures));
                Case(failures, "mate-math", () => Case4_MateMath(failures));
                Case(failures, "seal", () => Case5_Seal(failures));
                Case(failures, "hard-gate", () => Case6_HardGate(failures));
                Case(failures, "reverify-overlap", () => Case7_ReverifyOverlap(failures));
                Case(failures, "navmesh", () => Case8_NavMesh(failures, notes));
                Case(failures, "samples", () => Case9_SampleLayouts(failures));
                Case(failures, "determinism", () => Case10_Determinism(failures));
                Case(failures, "room-shell", () => Case11_ShippedShellsMatchCanon(failures, notes));
            }
            finally
            {
                Cleanup();
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "ROOM-FORGE OK - 11/11 cases pass (17-room catalog, dual-copy, mate/seal/drift/overlap " +
                         $"contract, spine+demo green sealed=1, shipped shells @ cell {RoomForgeCanon.Cell:0.#}m " +
                         $"wall {RoomForgeCanon.WallHeight:0.#}m + ceiling)" + noteStr;
                return true;
            }
            reason = "ROOM-FORGE FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  CASE 1 — catalog integrity
        // =====================================================================
        private static void Case1_CatalogIntegrity(List<string> failures)
        {
            string path = LayoutsDir + "/rooms-catalog.json";
            if (!File.Exists(path)) { failures.Add("[catalog] rooms-catalog.json missing at " + path); return; }
            RoomCatalogFile cat;
            try { cat = JsonConvert.DeserializeObject<RoomCatalogFile>(File.ReadAllText(path)); }
            catch (Exception ex) { failures.Add("[catalog] rooms-catalog.json parse error: " + ex.Message); return; }
            if (cat == null || cat.rooms == null || cat.rooms.Count == 0)
            { failures.Add("[catalog] rooms-catalog.json deserialized to 0 rooms (mapping break)"); return; }

            if (cat.rooms.Count < ExpectedRoomCount)
                failures.Add($"[catalog] only {cat.rooms.Count} rooms (expected >= {ExpectedRoomCount} from WO-741)");

            foreach (var entry in cat.rooms)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) { failures.Add("[catalog] entry with null/empty id"); continue; }
                if (string.IsNullOrEmpty(entry.prefabPath)) { failures.Add($"[catalog] '{entry.id}' has no prefabPath"); continue; }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
                if (prefab == null) { failures.Add($"[catalog] '{entry.id}' prefabPath '{entry.prefabPath}' does not load"); continue; }

                var meta = prefab.GetComponent<RoomPrefabMeta>();
                if (meta == null) { failures.Add($"[catalog] '{entry.id}' prefab has no RoomPrefabMeta"); continue; }
                if (meta.roomId != entry.id)
                    failures.Add($"[catalog] '{entry.id}' prefab RoomPrefabMeta.roomId='{meta.roomId}' != catalog id");

                var sockets = prefab.GetComponentsInChildren<RoomSocket>(true);
                int catCount = entry.sockets != null ? entry.sockets.Count : 0;
                if (sockets.Length != catCount)
                    failures.Add($"[catalog] '{entry.id}' prefab has {sockets.Length} sockets, catalog lists {catCount}");

                if (entry.sockets != null)
                {
                    foreach (var cs in entry.sockets)
                    {
                        if (cs == null || string.IsNullOrEmpty(cs.id)) continue;
                        RoomSocket match = null;
                        foreach (var s in sockets) if (s != null && s.id == cs.id) { match = s; break; }
                        if (match == null) { failures.Add($"[catalog] '{entry.id}' socket '{cs.id}' not on prefab"); continue; }
                        if (!string.IsNullOrEmpty(cs.type) && !string.Equals(cs.type, match.type.ToString(), StringComparison.OrdinalIgnoreCase))
                            failures.Add($"[catalog] '{entry.id}' socket '{cs.id}' type catalog='{cs.type}' prefab='{match.type}'");
                    }
                }
            }
        }

        // =====================================================================
        //  CASE 2 — dual-copy law (StreamingAssets vs Resources, content-identical)
        // =====================================================================
        private static void Case2_DualCopy(List<string> failures)
        {
            foreach (var file in new[] { "d4_sunken_crypt_spine.json", "demo_branching_kit.json", "rooms-catalog.json" })
            {
                string sa = LayoutsDir + "/" + file;
                string re = LayoutsDirRes + "/" + file;
                if (!File.Exists(sa)) { failures.Add($"[dual-copy] StreamingAssets copy missing: {sa}"); continue; }
                if (!File.Exists(re)) { failures.Add($"[dual-copy] Resources copy missing: {re}"); continue; }
                string a = Normalize(File.ReadAllText(sa));
                string b = Normalize(File.ReadAllText(re));
                if (a != b)
                    failures.Add($"[dual-copy] DRIFT '{file}' StreamingAssets({a.Length}b) != Resources({b.Length}b) - copies out of sync");
            }
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length > 0 && s[0] == (char)0xFEFF) s = s.Substring(1);   // strip leading BOM if present
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // =====================================================================
        //  CASE 3 — TypesCompatible matrix
        // =====================================================================
        private static void Case3_TypesMatrix(List<string> failures)
        {
            void Expect(RoomSocketType a, RoomSocketType b, bool want)
            {
                if (DungeonBakerChecks.TypesCompatible(a, b) != want)
                    failures.Add($"[types] {a} vs {b} expected {(want ? "compatible" : "incompatible")}");
            }
            Expect(RoomSocketType.Door, RoomSocketType.Door, true);
            Expect(RoomSocketType.Arch, RoomSocketType.Arch, true);
            Expect(RoomSocketType.Door, RoomSocketType.Arch, true);
            Expect(RoomSocketType.Arch, RoomSocketType.Door, true);
            Expect(RoomSocketType.StairUp, RoomSocketType.StairDown, true);
            Expect(RoomSocketType.StairDown, RoomSocketType.StairUp, true);
            Expect(RoomSocketType.Door, RoomSocketType.StairUp, false);
            Expect(RoomSocketType.Door, RoomSocketType.StairDown, false);
            Expect(RoomSocketType.Arch, RoomSocketType.StairUp, false);
            Expect(RoomSocketType.Arch, RoomSocketType.StairDown, false);
        }

        // =====================================================================
        //  CASE 4 — mate math on synthetic rooms (drives the shared TryMate)
        // =====================================================================
        private static void Case4_MateMath(List<string> failures)
        {
            const float maxD = 1.25f;

            // exact touch OK
            {
                var a = MakeRoom("A_exact", Vector2Int.one, Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
                var b = MakeRoom("B_exact", Vector2Int.one, Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                Place(b, new Vector3(0, 0, 6), 0);
                var r = DungeonBakerChecks.TryMate(Sk(a, "n"), Sk(b, "s"), b, maxD);
                if (!r.ok || r.nudge > 0.001f) failures.Add($"[mate-math] exact touch expected ok/no-nudge (ok={r.ok} dist={r.dist:F2} nudge={r.nudge:F2})");
            }
            // within maxMateDistance OK (small gap, no nudge)
            {
                var a = MakeRoom("A_near", Vector2Int.one, Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
                var b = MakeRoom("B_near", Vector2Int.one, Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                Place(b, new Vector3(0, 0, 6.5f), 0);   // socket gap 0.5 <= maxD
                var r = DungeonBakerChecks.TryMate(Sk(a, "n"), Sk(b, "s"), b, maxD);
                if (!r.ok || r.nudge > 0.001f) failures.Add($"[mate-math] within-maxD expected ok/no-nudge (ok={r.ok} dist={r.dist:F2} nudge={r.nudge:F2})");
            }
            // off-by-slightly -> nudge closes it, OK
            {
                var a = MakeRoom("A_nudge", Vector2Int.one, Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
                var b = MakeRoom("B_nudge", Vector2Int.one, Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                Place(b, new Vector3(0, 0, 7.5f), 0);   // socket gap 1.5 > maxD -> planar nudge
                var r = DungeonBakerChecks.TryMate(Sk(a, "n"), Sk(b, "s"), b, maxD);
                if (!r.ok || r.nudge < 0.5f) failures.Add($"[mate-math] off-by-slightly expected ok WITH nudge (ok={r.ok} dist={r.dist:F2} nudge={r.nudge:F2})");
            }
            // beyond nudge reach (planar nudge can't close a Y gap) -> FAIL distance
            {
                var a = MakeRoom("A_far", Vector2Int.one, Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
                var b = MakeRoom("B_far", Vector2Int.one, Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                Place(b, new Vector3(0, 3f, 6), 0);     // 3u vertical gap; planar nudge leaves it
                var r = DungeonBakerChecks.TryMate(Sk(a, "n"), Sk(b, "s"), b, maxD);
                if (r.ok || r.reason != MateFailReason.Distance) failures.Add($"[mate-math] Y-gap expected FAIL/distance (ok={r.ok} reason={r.reason} dist={r.dist:F2})");
            }
            // facing same direction (align < threshold) -> FAIL alignment
            {
                var a = MakeRoom("A_align", Vector2Int.one, Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
                var b = MakeRoom("B_align", Vector2Int.one, Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.forward)); // both point +Z
                Place(b, new Vector3(0, 0, 6), 0);
                var r = DungeonBakerChecks.TryMate(Sk(a, "n"), Sk(b, "s"), b, maxD);
                if (r.ok || r.reason != MateFailReason.Alignment) failures.Add($"[mate-math] same-facing expected FAIL/alignment (ok={r.ok} reason={r.reason} align={r.align:F2})");
            }
            // yaw 90 / 180 / 270 rotated rooms mate correctly when sockets oppose. Author b's socket
            // so that AFTER the room yaw it sits at (0,0,3) and faces -Z (opposing a.n's +Z).
            foreach (float yaw in new[] { 90f, 180f, 270f })
            {
                var a = MakeRoom($"A_yaw{yaw}", Vector2Int.one, Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward)); // world (0,0,3) out +Z
                Quaternion inv = Quaternion.Euler(0f, -yaw, 0f);
                Vector3 localOut = inv * Vector3.back;               // world -Z after the room yaw
                Vector3 localPos = inv * new Vector3(0, 0, 3);       // world (0,0,3) after the room yaw (b at origin)
                var b = MakeRoom($"B_yaw{yaw}", Vector2Int.one, Sock("s", RoomSocketType.Door, localPos, localOut));
                Place(b, Vector3.zero, yaw);
                var r = DungeonBakerChecks.TryMate(Sk(a, "n"), Sk(b, "s"), b, maxD);
                if (!r.ok)
                    failures.Add($"[mate-math] yaw{yaw} opposing sockets should mate (ok={r.ok} reason={r.reason} dist={r.dist:F2} align={r.align:F2})");
            }
        }

        // =====================================================================
        //  CASE 5 — seal behavior (wall vs secret vs sealUnmated=false)
        // =====================================================================
        private static void Case5_Seal(List<string> failures)
        {
            // normal unmated socket -> SEALED_WALL + a Seal_<id> child scaled to halfWidth*2
            {
                var room = MakeRoom("R_seal", Vector2Int.one, Sock("s_door_01", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                var s = Sk(room, "s_door_01");
                s.halfWidth = 1.1f;
                bool wall = DungeonBakerChecks.SealSocket(s);
                if (!wall) failures.Add("[seal] normal socket did not spawn wall geometry");
                if (s.matedTo != "SEALED_WALL") failures.Add($"[seal] normal socket matedTo='{s.matedTo}' expected SEALED_WALL");
                Transform sealT = s.transform.Find("Seal_s_door_01");
                if (sealT == null) failures.Add("[seal] normal socket has no Seal_<id> cube");
                else if (Mathf.Abs(sealT.localScale.x - s.halfWidth * 2f) > 0.01f)
                    failures.Add($"[seal] wall width={sealT.localScale.x:F2} expected {s.halfWidth * 2f:F2}");
            }
            // secret unmated socket -> SEALED_SECRET, NO geometry
            {
                var room = MakeRoom("R_secret", Vector2Int.one, Sock("s_door_01", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back, secret: true));
                var s = Sk(room, "s_door_01");
                bool wall = DungeonBakerChecks.SealSocket(s);
                if (wall) failures.Add("[seal] secret socket spawned geometry (should be invisible)");
                if (s.matedTo != "SEALED_SECRET") failures.Add($"[seal] secret socket matedTo='{s.matedTo}' expected SEALED_SECRET");
                if (s.transform.Find("Seal_s_door_01") != null) failures.Add("[seal] secret socket spawned a Seal_ cube");
            }
            // sealUnmated=false -> Compose leaves sockets open, sealedN==0
            {
                var room = MakeRoom("R_open", Vector2Int.one, Sock("s_door_01", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                var layout = new DungeonComposeLayout
                {
                    dungeonId = "seal_off",
                    rooms = new List<ComposeRoomPlacement> { new ComposeRoomPlacement { prefab = "R_open", instanceId = "R_open" } },
                    connections = new List<ComposeConnection>(),
                    rules = new ComposeRules { sealUnmated = false },
                };
                var instances = new Dictionary<string, GameObject> { { "R_open", room } };
                var outcome = DungeonBakerChecks.Compose(instances, layout);
                if (outcome.sealedN != 0) failures.Add($"[seal] sealUnmated=false sealedN={outcome.sealedN} expected 0");
                if (Sk(room, "s_door_01").IsMated) failures.Add("[seal] sealUnmated=false left a socket marked mated");
            }
        }

        // =====================================================================
        //  CASE 6 — hard gate (fix 1): a type-mismatch layout must ABORT
        // =====================================================================
        private static void Case6_HardGate(List<string> failures)
        {
            var a = MakeRoom("HG_A", Vector2Int.one, Sock("n_door_01", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
            var b = MakeRoom("HG_B", Vector2Int.one, Sock("s_stair_01", RoomSocketType.StairDown, new Vector3(0, 0, -3), Vector3.back));
            Place(b, new Vector3(0, 0, 6), 0);
            var layout = new DungeonComposeLayout
            {
                dungeonId = "hardgate_mismatch",
                rooms = new List<ComposeRoomPlacement>
                {
                    new ComposeRoomPlacement { prefab = "HG_A", instanceId = "HG_A" },
                    new ComposeRoomPlacement { prefab = "HG_B", instanceId = "HG_B", cell = new[] { 0, 0, 1 } },
                },
                connections = new List<ComposeConnection>
                {
                    new ComposeConnection { fromInstance = "HG_A", fromSocket = "n_door_01", toInstance = "HG_B", toSocket = "s_stair_01" },
                },
                rules = new ComposeRules(),
            };
            var instances = new Dictionary<string, GameObject> { { "HG_A", a }, { "HG_B", b } };
            var outcome = DungeonBakerChecks.Compose(instances, layout);

            if (!outcome.Aborted)
                failures.Add("[hard-gate] type-mismatch layout did NOT abort (fix 1 broken: baker would save a bad scene)");
            if (outcome.mateFail < 1)
                failures.Add($"[hard-gate] expected >=1 mateFail, got {outcome.mateFail}");
            bool sawTypeMismatch = false;
            foreach (var c in outcome.connections) if (!c.ok && c.reason == MateFailReason.TypeMismatch) sawTypeMismatch = true;
            if (!sawTypeMismatch)
                failures.Add("[hard-gate] no connection reported reason=type-mismatch");
        }

        // =====================================================================
        //  CASE 7 — re-verify (drift) + overlap (fix 2)
        // =====================================================================
        private static void Case7_ReverifyOverlap(List<string> failures)
        {
            // --- DRIFT: conn2's nudge drags room M off the mate conn1 already made ---
            {
                var a = MakeRoom("D_A", Vector2Int.one, Sock("n_door_01", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
                var m = MakeRoom("D_M", Vector2Int.one,
                    Sock("s_door_01", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back),
                    Sock("e_door_01", RoomSocketType.Door, new Vector3(3, 0, 0), Vector3.right));
                var c = MakeRoom("D_C", Vector2Int.one, Sock("w_door_01", RoomSocketType.Door, new Vector3(-3, 0, 0), Vector3.left));
                Place(a, Vector3.zero, 0);
                Place(m, new Vector3(0, 0, 6), 0);     // M.s world (0,0,3) touches A.n
                Place(c, new Vector3(20, 0, 6), 0);    // C.w world (17,0,6); mating M.e will nudge M far in +X
                var layout = new DungeonComposeLayout
                {
                    dungeonId = "drift",
                    rooms = new List<ComposeRoomPlacement>
                    {
                        new ComposeRoomPlacement { prefab = "D_A", instanceId = "D_A" },
                        new ComposeRoomPlacement { prefab = "D_M", instanceId = "D_M" },
                        new ComposeRoomPlacement { prefab = "D_C", instanceId = "D_C" },
                    },
                    connections = new List<ComposeConnection>
                    {
                        new ComposeConnection { fromInstance = "D_A", fromSocket = "n_door_01", toInstance = "D_M", toSocket = "s_door_01" },
                        new ComposeConnection { fromInstance = "D_C", fromSocket = "w_door_01", toInstance = "D_M", toSocket = "e_door_01" },
                    },
                    rules = new ComposeRules { maxMateDistance = 1.25f, sealUnmated = false },
                };
                var instances = new Dictionary<string, GameObject> { { "D_A", a }, { "D_M", m }, { "D_C", c } };
                var outcome = DungeonBakerChecks.Compose(instances, layout);
                if (outcome.driftFail < 1)
                    failures.Add($"[reverify-overlap] expected drift failure (a later nudge broke an earlier mate), driftFail={outcome.driftFail}");
                if (!outcome.Aborted)
                    failures.Add("[reverify-overlap] drift did not abort the bake (fix 2a broken)");
            }

            // --- OVERLAP: two rooms on the same cell ---
            {
                var a = MakeRoom("O_A", Vector2Int.one, Sock("s_door_01", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                var b = MakeRoom("O_B", Vector2Int.one, Sock("s_door_01", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
                Place(a, Vector3.zero, 0);
                Place(b, Vector3.zero, 0);   // SAME cell -> footprints fully overlap
                var layout = new DungeonComposeLayout
                {
                    dungeonId = "overlap",
                    rooms = new List<ComposeRoomPlacement>
                    {
                        new ComposeRoomPlacement { prefab = "O_A", instanceId = "O_A" },
                        new ComposeRoomPlacement { prefab = "O_B", instanceId = "O_B" },
                    },
                    connections = new List<ComposeConnection>(),
                    rules = new ComposeRules { sealUnmated = false },
                };
                var instances = new Dictionary<string, GameObject> { { "O_A", a }, { "O_B", b } };
                var outcome = DungeonBakerChecks.Compose(instances, layout);
                if (outcome.overlapFail < 1)
                    failures.Add($"[reverify-overlap] expected overlap failure for same-cell rooms, overlapFail={outcome.overlapFail}");
                if (!outcome.Aborted)
                    failures.Add("[reverify-overlap] overlap did not abort the bake (fix 2b broken)");
            }
        }

        // =====================================================================
        //  CASE 8 — navmesh path-connectivity (best-effort; headless -nographics)
        // =====================================================================
        private static void Case8_NavMesh(List<string> failures, List<string> notes)
        {
            var layout = LoadLayout("d4_sunken_crypt_spine.json");
            if (layout == null) { failures.Add("[navmesh] could not load spine layout"); return; }
            var instances = InstantiateLayout(layout, out var order, out string err);
            if (err != null) { failures.Add("[navmesh] " + err); return; }
            DungeonBakerChecks.Compose(instances, layout);   // position + seal (walls block navmesh)

            var host = new GameObject("__rf_navmesh");
            s_spawned.Add(host);
            NavMeshData built = null;
            try
            {
                var surface = host.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = ~0;
                surface.BuildNavMesh();
                built = surface.navMeshData;

                bool anyWalkable = NavMesh.SamplePosition(Vector3.zero, out _, 8f, NavMesh.AllAreas);
                if (!anyWalkable)
                {
                    notes.Add(DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                        "path check", "navmesh produced no walkable area in headless (-nographics)"));
                    return;
                }
                var first = instances[order[0]].transform.position;
                var last = instances[order[order.Count - 1]].transform.position;
                var path = new NavMeshPath();
                bool got = NavMesh.SamplePosition(first, out var fh, 8f, NavMesh.AllAreas) &&
                           NavMesh.SamplePosition(last, out var lh, 8f, NavMesh.AllAreas) &&
                           NavMesh.CalculatePath(fh.position, lh.position, NavMesh.AllAreas, path);
                if (!got || path.status == NavMeshPathStatus.PathInvalid)
                    failures.Add($"[navmesh] no complete path first->last (got={got} status={(got ? path.status.ToString() : "n/a")})");
                else if (path.status != NavMeshPathStatus.PathComplete)
                    notes.Add($"navmesh path status={path.status} (partial - geometry gaps, not a hard fail)");
            }
            finally
            {
                if (built != null) NavMesh.RemoveAllNavMeshData();
            }
        }

        // =====================================================================
        //  CASE 9 — sample layouts green (spine + demo) with sealed-count pins
        // =====================================================================
        private static void Case9_SampleLayouts(List<string> failures)
        {
            CheckSample(failures, "d4_sunken_crypt_spine.json", SpineSealedExpected);
            CheckSample(failures, "demo_branching_kit.json", DemoSealedExpected);
        }

        private static void CheckSample(List<string> failures, string file, int sealedExpected)
        {
            var layout = LoadLayout(file);
            if (layout == null) { failures.Add($"[samples] could not load '{file}'"); return; }
            var instances = InstantiateLayout(layout, out _, out string err);
            if (err != null) { failures.Add($"[samples] '{file}' {err}"); return; }

            var outcome = DungeonBakerChecks.Compose(instances, layout);
            int connCount = layout.connections != null ? layout.connections.Count : 0;
            if (outcome.mateOk != connCount)
                failures.Add($"[samples] '{file}' matesOk={outcome.mateOk} expected {connCount} (all connections)");
            if (outcome.mateFail != 0)
                failures.Add($"[samples] '{file}' matesFail={outcome.mateFail} expected 0 - {DescribeFails(outcome)}");
            if (outcome.driftFail != 0)
                failures.Add($"[samples] '{file}' driftFail={outcome.driftFail} expected 0");
            if (outcome.overlapFail != 0)
                failures.Add($"[samples] '{file}' overlapFail={outcome.overlapFail} ({string.Join(",", outcome.overlaps)}) expected 0");
            if (outcome.sealedN != sealedExpected)
                failures.Add($"[samples] '{file}' sealed={outcome.sealedN} expected {sealedExpected} (silent socket edit?)");
        }

        private static string DescribeFails(ComposeOutcome o)
        {
            var sb = new StringBuilder();
            foreach (var c in o.connections) if (!c.ok) sb.Append($"{c.connId}[{DungeonBakerChecks.ReasonKey(c.reason)} dist={c.dist:F2} align={c.align:F2}] ");
            return sb.ToString();
        }

        // =====================================================================
        //  CASE 10 — determinism + hygiene
        // =====================================================================
        private static void Case10_Determinism(List<string> failures)
        {
            var l1 = LoadLayout("d4_sunken_crypt_spine.json");
            var l2 = LoadLayout("d4_sunken_crypt_spine.json");
            if (l1 == null || l2 == null) { failures.Add("[determinism] could not load spine twice"); return; }

            var i1 = InstantiateLayout(l1, out var ord1, out string e1);
            var i2 = InstantiateLayout(l2, out var ord2, out string e2);
            if (e1 != null || e2 != null) { failures.Add("[determinism] " + (e1 ?? e2)); return; }

            var o1 = DungeonBakerChecks.Compose(i1, l1);
            var o2 = DungeonBakerChecks.Compose(i2, l2);

            if (o1.mateOk != o2.mateOk || o1.sealedN != o2.sealedN)
                failures.Add($"[determinism] summary differs run1(ok={o1.mateOk},sealed={o1.sealedN}) run2(ok={o2.mateOk},sealed={o2.sealedN})");

            foreach (var id in ord1)
            {
                if (!i1.TryGetValue(id, out var g1) || !i2.TryGetValue(id, out var g2)) continue;
                if (Vector3.Distance(g1.transform.position, g2.transform.position) > 0.001f)
                    failures.Add($"[determinism] room '{id}' position differs between identical bakes");
                if (Mathf.Abs(Mathf.DeltaAngle(g1.transform.eulerAngles.y, g2.transform.eulerAngles.y)) > 0.01f)
                    failures.Add($"[determinism] room '{id}' yaw differs between identical bakes");
            }
            // Build-Settings dedup hygiene is enforced by DungeonBaker.EnsureInBuildSettings (guarded
            // against duplicate paths); not exercised here to avoid mutating the project's build list.
        }

        // =====================================================================
        //  CASE 11 — the SHIPPED room prefabs match RoomForgeCanon (WO-919 + WO-922)
        // =====================================================================
        // Same reasoning as DungeonMultiLevelRegression Case 5, applied to the room SHELL:
        // Assets/Dungeon/Rooms/*.prefab are GENERATED, so editing DefaultDungeonRoomsBuilder
        // changes nothing on disk until "Defenders/Dungeon/Build Default Room Prefabs" is re-run.
        // Without this case a widened-and-enclosed builder and a stale 6u open-top prefab library
        // both read as green, and the first evidence would be a screenshot after a full bake.
        //
        // EXPECT THIS TO FAIL until the prefab rebuild lands. That is the case working.
        private static void Case11_ShippedShellsMatchCanon(List<string> failures, List<string> notes)
        {
            string path = LayoutsDir + "/rooms-catalog.json";
            if (!File.Exists(path)) { notes.Add("rooms-catalog.json absent - shell check skipped"); return; }
            RoomCatalogFile cat;
            try { cat = JsonConvert.DeserializeObject<RoomCatalogFile>(File.ReadAllText(path)); }
            catch { notes.Add("rooms-catalog.json unparseable - shell check skipped (case 1 owns that failure)"); return; }
            if (cat?.rooms == null || cat.rooms.Count == 0) return;

            int checkedRooms = 0;
            foreach (var entry in cat.rooms)
            {
                if (entry == null || string.IsNullOrEmpty(entry.prefabPath)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
                if (prefab == null) continue;   // case 1 already fails on a dead prefabPath
                var meta = prefab.GetComponent<RoomPrefabMeta>();
                if (meta == null) continue;     // case 1 owns that too
                checkedRooms++;

                // --- WO-922: the room was forged at the canon cell ---
                if (Mathf.Abs(meta.cellSize - RoomForgeCanon.Cell) > 0.01f)
                {
                    failures.Add($"[room-shell] '{entry.id}' cellSize={meta.cellSize:0.##} expected " +
                                 $"{RoomForgeCanon.Cell:0.##} - the room prefabs are STALE. Re-run " +
                                 "Defenders/Dungeon/Build Default Room Prefabs (DefaultDungeonRoomsBuilder.BuildAll), " +
                                 "then recompose every graph and re-bake.");
                    continue;   // every metric below is derived from the cell; one message is enough
                }

                Vector2 fp = meta.FootprintWorld;

                // --- WO-922 + shaft rework: the FLOOR covers the footprint MINUS its shafts ---
                //  Was: FindChild("Floor") and assert its localScale == the footprint exactly.
                //  That demanded ONE child covering everything, which no floor with a stairwell
                //  hole in it can be - it reported "has no 'Floor' child" on six correct prefabs.
                //  Now: sample the footprint and require every point to be covered UNLESS it is
                //  inside a declared shaft, and every point inside a shaft to be OPEN.
                AssertSurface(failures, entry.id, prefab.transform, "Floor", fp,
                              meta.floorShafts, meta.cellSize);

                // --- WO-919: walls reach the canon height ---
                float tallest = 0f;
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || !t.name.StartsWith("Wall", StringComparison.Ordinal)) continue;
                    tallest = Mathf.Max(tallest, t.localScale.y);
                }
                if (tallest <= 0.01f)
                    failures.Add($"[room-shell] '{entry.id}' has no 'Wall*' children - the shell was not built");
                else if (Mathf.Abs(tallest - RoomForgeCanon.WallHeight) > 0.01f)
                    failures.Add($"[room-shell] '{entry.id}' tallest wall is {tallest:0.##}u, expected " +
                                 $"{RoomForgeCanon.WallHeight:0.##}u (WO-919) - stale prefab, re-run BuildAll");

                // --- WO-919 + shaft rework: the CEILING roofs the footprint minus its shafts ---
                var ceilPieces = CollectSurface(prefab.transform, "Ceil");
                if (ceilPieces.Count == 0)
                {
                    failures.Add($"[room-shell] '{entry.id}' has NO 'Ceil*' children - the room is open to sky " +
                                 "(WO-919). Re-run BuildAll.");
                    continue;
                }
                AssertSurface(failures, entry.id, prefab.transform, "Ceil", fp,
                              meta.ceilingShafts, meta.cellSize);

                // Per-piece assertions now run over EVERY piece, not just a single named child.
                // That is the half the old check could not do: a multi-piece ceiling could have
                // had one compliant slab and three that were collider-bearing or dropped to head
                // height, and only the first was ever looked at.
                float shellTop = 0f;
                foreach (var c in ceilPieces)
                {
                    float underside = c.localPosition.y - c.localScale.y * 0.5f;
                    if (Mathf.Abs(underside - RoomForgeCanon.WallHeight) > 0.01f)
                        failures.Add($"[room-shell] '{entry.id}' ceiling piece '{c.name}' underside y={underside:0.##}, " +
                                     $"expected {RoomForgeCanon.WallHeight:0.##} (flush with the wall top)");

                    // A collider here would voxelize into a WALKABLE roof under the baker's
                    // PhysicsColliders NavMesh, which SamplePosition can then snap a hero seat or
                    // an enemy spawner onto. Nav-static would be the same bug by a different route.
                    if (c.GetComponent<Collider>() != null)
                        failures.Add($"[room-shell] '{entry.id}' ceiling piece '{c.name}' has a Collider - the NavMesh " +
                                     "bakes from PhysicsColliders and would produce a walkable roof surface");
                    if ((GameObjectUtility.GetStaticEditorFlags(c.gameObject) & StaticEditorFlags.NavigationStatic) != 0)
                        failures.Add($"[room-shell] '{entry.id}' ceiling piece '{c.name}' is NavigationStatic - " +
                                     "it must be geometry only");

                    shellTop = Mathf.Max(shellTop, c.localPosition.y + c.localScale.y * 0.5f);
                }

                // --- the whole shell has to fit inside one floor of a descent ---
                if (shellTop + RoomForgeCanon.FloorSlabThickness >= DungeonBakerChecks.FloorSeparationY)
                    failures.Add($"[room-shell] '{entry.id}' occupies {shellTop + RoomForgeCanon.FloorSlabThickness:0.##}u " +
                                 $"of vertical space but floors are stacked {DungeonBakerChecks.FloorSeparationY:0.##}u " +
                                 "apart - a multi-level bake would interpenetrate");
            }

            if (checkedRooms == 0) notes.Add("no loadable room prefabs - shell check had nothing to verify");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name) return t;
            return null;
        }

        /// <summary>
        /// Every child whose name starts with <paramref name="prefix"/> — "Floor" catches both the
        /// single slab and a multi-piece "Floor_Landing_00..03"; "Ceil" catches "Ceiling",
        /// "Ceiling_Shaft_*" and the older "Ceil_N/S/E/W".
        /// </summary>
        private static List<Transform> CollectSurface(Transform root, string prefix)
        {
            var hits = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t != root && t.name.StartsWith(prefix, StringComparison.Ordinal))
                    hits.Add(t);
            return hits;
        }

        /// <summary>
        /// A surface covers the footprint EXCEPT where a shaft is declared, and every declared
        /// shaft is genuinely OPEN.
        ///
        /// <para>SAMPLED rather than computed from bounds, and that choice is the whole point.
        /// Union-bounds — "do the pieces' combined extents reach the footprint" — is the cheap
        /// version, and it would have PASSED the bug found on 2026-08-07: the stair connectors
        /// shipped a ceiling built as Ceil_N/S/E/W, a perimeter RING whose union bounds covered
        /// the room perfectly while its centre was open to sky. Union bounds cannot see a hole.
        /// Sampling can.</para>
        ///
        /// <para>Assumes axis-aligned pieces (localPosition = centre, localScale = size), which is
        /// true of everything RoomForge builds — and is asserted, not assumed, below.</para>
        /// </summary>
        private static void AssertSurface(List<string> failures, string roomId, Transform root,
                                          string prefix, Vector2 fp, List<Rect> shafts, float cellSize)
        {
            var pieces = CollectSurface(root, prefix);
            if (pieces.Count == 0)
            {
                failures.Add($"[room-shell] '{roomId}' has no '{prefix}*' children - the {prefix.ToLowerInvariant()} " +
                             "surface was not built");
                return;
            }

            // A rotated piece breaks the centre±scale/2 reasoning below. Say so rather than
            // silently mis-measuring it.
            foreach (var p in pieces)
            {
                var e = p.localRotation.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) > 0.5f || Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) > 0.5f)
                    failures.Add($"[room-shell] '{roomId}' {prefix} piece '{p.name}' is tilted " +
                                 $"({e.x:0.#},{e.y:0.#},{e.z:0.#}) - this check measures axis-aligned slabs " +
                                 "and cannot verify coverage of a tilted one");
            }

            float hx = fp.x * 0.5f, hz = fp.y * 0.5f;
            const float Step = 0.25f;          // 40x40 samples on a 10m room - cheap, and finer
                                               // than any gap that would matter to a player
            const float EdgeInset = 0.05f;     // ignore the outermost sliver: pieces legitimately
                                               // end flush with the wall line and float noise there
                                               // is not a hole

            int uncovered = 0, blocked = 0;
            Vector2 firstUncovered = Vector2.zero, firstBlocked = Vector2.zero;

            for (float x = -hx + EdgeInset; x <= hx - EdgeInset; x += Step)
            for (float z = -hz + EdgeInset; z <= hz - EdgeInset; z += Step)
            {
                bool inShaft = RoomPrefabMeta.InAnyShaft(shafts, x, z);
                bool covered = false;
                for (int i = 0; i < pieces.Count && !covered; i++)
                {
                    var p = pieces[i];
                    float pxHalf = Mathf.Abs(p.localScale.x) * 0.5f;
                    float pzHalf = Mathf.Abs(p.localScale.z) * 0.5f;
                    covered = Mathf.Abs(x - p.localPosition.x) <= pxHalf &&
                              Mathf.Abs(z - p.localPosition.z) <= pzHalf;
                }

                if (inShaft && covered)
                {
                    if (blocked == 0) firstBlocked = new Vector2(x, z);
                    blocked++;
                }
                else if (!inShaft && !covered)
                {
                    if (uncovered == 0) firstUncovered = new Vector2(x, z);
                    uncovered++;
                }
            }

            if (uncovered > 0)
                failures.Add($"[room-shell] '{roomId}' {prefix} surface has a HOLE: {uncovered} sample(s) " +
                             $"uncovered, first at local ({firstUncovered.x:0.##},{firstUncovered.y:0.##}), " +
                             $"over a {fp.x:0.##}x{fp.y:0.##} footprint with {(shafts == null ? 0 : shafts.Count)} " +
                             "declared shaft(s). Either the surface is incomplete, or the opening is real and " +
                             "must be DECLARED on RoomPrefabMeta so it can be checked instead of tolerated.");

            if (blocked > 0)
                failures.Add($"[room-shell] '{roomId}' {prefix} surface BLOCKS a declared shaft: {blocked} sample(s) " +
                             $"covered inside it, first at local ({firstBlocked.x:0.##},{firstBlocked.y:0.##}). " +
                             "A stairwell that is declared open and built solid is a flight into a slab.");
        }

        // ---- shared helpers -----------------------------------------------------

        private static DungeonComposeLayout LoadLayout(string file)
        {
            string path = LayoutsDir + "/" + file;
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<DungeonComposeLayout>(File.ReadAllText(path)); }
            catch { return null; }
        }

        // Instantiate the real prefabs (Assets/Dungeon/Rooms/<stem>.prefab), position by cell,
        // apply yaw — mirroring DungeonBaker's placement. Returns instanceId->GameObject.
        private static Dictionary<string, GameObject> InstantiateLayout(
            DungeonComposeLayout layout, out List<string> order, out string err)
        {
            order = new List<string>();
            err = null;
            var instances = new Dictionary<string, GameObject>();
            float cell = layout.cellSize > 0.1f ? layout.cellSize : RoomForgeCanon.Cell;
            foreach (var place in layout.rooms)
            {
                if (place == null || string.IsNullOrEmpty(place.prefab)) continue;
                string instId = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomsFolder}/{place.prefab}.prefab");
                if (prefab == null) { err = $"prefab '{place.prefab}' not found at {RoomsFolder}"; return instances; }
                var go = (GameObject)UnityEngine.Object.Instantiate(prefab);
                s_spawned.Add(go);
                go.name = instId;
                int cx = place.cell != null && place.cell.Length > 0 ? place.cell[0] : 0;
                int cy = place.cell != null && place.cell.Length > 1 ? place.cell[1] : 0;
                int cz = place.cell != null && place.cell.Length > 2 ? place.cell[2] : 0;
                go.transform.position = new Vector3(cx * cell, cy * cell, cz * cell);
                go.transform.rotation = Quaternion.Euler(0f, place.yawDeg, 0f);
                instances[instId] = go;
                order.Add(instId);
            }
            return instances;
        }

        private struct SockSpec { public string id; public RoomSocketType type; public Vector3 local; public Vector3 outward; public bool secret; }

        private static SockSpec Sock(string id, RoomSocketType type, Vector3 local, Vector3 outward, bool secret = false)
            => new SockSpec { id = id, type = type, local = local, outward = outward, secret = secret };

        /// <summary>
        /// Metres per cell for this suite's SYNTHETIC fixtures. Deliberately NOT
        /// RoomForgeCanon.Cell: cases 4/5/6/7 hand-author their sockets at +/-3 and place rooms
        /// 6u apart, so the fixture cell is pinned by those literals, not by the shipping kit.
        /// Binding it to the canon would silently turn case 7's touching drift fixture into an
        /// overlap the moment the kit widened. The SHIPPED prefabs are checked against the real
        /// canon in case 11 - that is where kit geometry belongs.
        /// </summary>
        private const float FixtureCell = 6f;

        // Build a throwaway room GameObject with RoomPrefabMeta + the given sockets.
        private static GameObject MakeRoom(string id, Vector2Int footprint, params SockSpec[] socks)
        {
            var go = new GameObject(id);
            s_spawned.Add(go);
            var meta = go.AddComponent<RoomPrefabMeta>();
            meta.roomId = id;
            meta.archetype = "combat";
            meta.cellSize = FixtureCell;
            meta.footprintCells = footprint;
            foreach (var s in socks)
            {
                var sg = new GameObject("Socket_" + s.id);
                sg.transform.SetParent(go.transform, false);
                sg.transform.localPosition = s.local;
                Vector3 fwd = s.outward.sqrMagnitude > 0.0001f ? s.outward : Vector3.forward;
                sg.transform.localRotation = Quaternion.LookRotation(fwd);
                var rs = sg.AddComponent<RoomSocket>();
                rs.id = s.id;
                rs.type = s.type;
                rs.isSecret = s.secret;
                rs.halfWidth = 1f;
            }
            return go;
        }

        private static void Place(GameObject go, Vector3 pos, float yaw)
        {
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static RoomSocket Sk(GameObject room, string id) => DungeonBakerChecks.FindSocket(room, id);

        private static void Cleanup()
        {
            foreach (var go in s_spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            s_spawned.Clear();
        }
    }
}
