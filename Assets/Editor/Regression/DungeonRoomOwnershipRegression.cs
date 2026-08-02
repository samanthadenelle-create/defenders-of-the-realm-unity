// =============================================================================
// DungeonRoomOwnershipRegression [dungeon-rooms] (WO-797) - rooms OWN their enemies.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Dungeons).
//
// Pins the WO-797 composed-dungeon contract (F8 seq 461/622 "all enemies at the
// entrance" / "no way to exit"):
//   1 [schema]        dg_starter_loop layout+graph carry per-room encounter blocks
//                     (sane confine values, ids resolve) and the StreamingAssets /
//                     Resources dual-copies are content-identical.
//   2 [brain]         EnemyBrain room binding: SetRoomArea stores the assignment
//                     (HasRoomArea/AreaRoomId - "every spawned enemy carries a room
//                     assignment"), and the PURE wake + confinement math holds
//                     (wake from the ROOM FOOTPRINT; destinations clamped to
//                     AABB + slack even for provoked chases).
//   3 [spawner]       OutpostEnemyGroupSpawner carries the serialized room fields
//                     the baker writes via SerializedObject (guards rename drift)
//                     and ConfigureRoomArea arms room ownership.
//   4 [room-bounds]   DungeonRoomBounds (the ONE shared AABB math) - footprint,
//                     yaw swap, containment; encounter rooms' baked prefabs seat
//                     their own centre inside their own AABB (best-effort when
//                     room prefabs are present).
//   5 [binder]        DungeonRoomBinder runtime injector exists with its
//                     RuntimeInitializeOnLoadMethod hook + TryBind path.
//   6 [exit-beacon]   An injected exit carries the discoverability beacon:
//                     DungeonExitBeacon + a Light + the walk-in trigger.
//
// Markers: DUNGEON_ROOM_OWNERSHIP_OK / DUNGEON_ROOM_OWNERSHIP_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.DungeonRoomOwnershipRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using DeNelle.Dungeons;
using DeNelle.Dungeons.RoomForge;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class DungeonRoomOwnershipRegression
    {
        private const string LayoutSA = "Assets/StreamingAssets/Data/Canonical/dungeon-layouts/dg_starter_loop.json";
        private const string LayoutRes = "Assets/Resources/Data/Canonical/dungeon-layouts/dg_starter_loop.json";
        private const string GraphSA = "Assets/StreamingAssets/Data/Canonical/dungeon-graphs/dg_starter_loop.json";
        private const string GraphRes = "Assets/Resources/Data/Canonical/dungeon-graphs/dg_starter_loop.json";
        private const string RoomsFolder = "Assets/Dungeon/Rooms";

        private static readonly List<GameObject> s_spawned = new List<GameObject>();

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_ROOM_OWNERSHIP_OK - " + reason);
            else Debug.LogError("DUNGEON_ROOM_OWNERSHIP_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "schema", () => Case1_Schema(failures));
                Case(failures, "brain", () => Case2_BrainContract(failures));
                Case(failures, "spawner", () => Case3_SpawnerContract(failures));
                Case(failures, "room-bounds", () => Case4_RoomBounds(failures, notes));
                Case(failures, "binder", () => Case5_Binder(failures));
                Case(failures, "exit-beacon", () => Case6_ExitBeacon(failures));
                Case(failures, "pursuit-bound", () => Case7_PursuitBound(failures));
            }
            finally
            {
                Cleanup();
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DUNGEON ROOM OWNERSHIP OK - encounter schema + dual-copy, brain wake/confine math, " +
                         "spawner serialized fields, shared room-AABB math, runtime binder, exit beacon" + noteStr;
                return true;
            }
            reason = "dungeon-rooms FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  CASE 1 - schema + dual-copy
        // =====================================================================
        private static void Case1_Schema(List<string> failures)
        {
            if (!File.Exists(LayoutSA)) { failures.Add("[schema] layout missing: " + LayoutSA); return; }
            var layout = JsonConvert.DeserializeObject<DungeonComposeLayout>(File.ReadAllText(LayoutSA));
            if (layout == null || layout.rooms == null || layout.rooms.Count == 0)
            { failures.Add("[schema] layout deserialized empty"); return; }

            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in layout.rooms)
                if (r != null) roomIds.Add(string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId);

            int encounters = 0;
            foreach (var r in layout.rooms)
            {
                if (r == null || r.encounter == null) continue;
                encounters++;
                string id = string.IsNullOrEmpty(r.instanceId) ? r.prefab : r.instanceId;
                var e = r.encounter;
                if (e.min < 1 || e.max < e.min)
                    failures.Add($"[schema] room '{id}' encounter counts insane (min {e.min} max {e.max})");
                if (e.confine == null)
                { failures.Add($"[schema] room '{id}' encounter has NO confine block (mobs would roam)"); continue; }
                if (e.confine.wakeRadius <= 0f)
                    failures.Add($"[schema] room '{id}' confine.wakeRadius {e.confine.wakeRadius} <= 0");
                if (e.confine.slack < 0f)
                    failures.Add($"[schema] room '{id}' confine.slack {e.confine.slack} < 0");
                if (!roomIds.Contains(id))
                    failures.Add($"[schema] encounter room '{id}' not present in rooms list");
            }
            if (encounters == 0)
                failures.Add("[schema] dg_starter_loop has ZERO encounter blocks - the WO-797 data was lost " +
                             "(spawners would bake unowned again)");

            // Dual-copy law: StreamingAssets and Resources copies content-identical.
            DualCopy(failures, LayoutSA, LayoutRes, "layout");
            DualCopy(failures, GraphSA, GraphRes, "graph");

            // Graph copy carries the same encounter authoring (parsed loosely - the graph
            // model lives in the editor assembly; the encounter shape is what matters).
            if (File.Exists(GraphSA))
            {
                string graphText = File.ReadAllText(GraphSA);
                if (!graphText.Contains("\"encounter\""))
                    failures.Add("[schema] graph dg_starter_loop.json has no encounter blocks - a re-compose would drop them");
            }
            else failures.Add("[schema] graph missing: " + GraphSA);
        }

        private static void DualCopy(List<string> failures, string a, string b, string label)
        {
            if (!File.Exists(a)) { failures.Add($"[schema] {label} StreamingAssets copy missing: {a}"); return; }
            if (!File.Exists(b)) { failures.Add($"[schema] {label} Resources copy missing: {b}"); return; }
            string na = Normalize(File.ReadAllText(a));
            string nb = Normalize(File.ReadAllText(b));
            if (na != nb)
                failures.Add($"[schema] {label} dual-copy DRIFT: StreamingAssets({na.Length}b) != Resources({nb.Length}b)");
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.Length > 0 && s[0] == (char)0xFEFF) s = s.Substring(1);
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // =====================================================================
        //  CASE 2 - EnemyBrain room binding + pure wake/confine math
        // =====================================================================
        private static void Case2_BrainContract(List<string> failures)
        {
            // Room 6x6 centred at (0,4/2,12) - the starter-loop junction shape.
            var area = new Bounds(new Vector3(0f, 2f, 12f), new Vector3(6f, 4f, 6f));

            // -- assignment contract: every bound brain carries its room id --
            var host = new GameObject("__wo797_brain");
            s_spawned.Add(host);
            var brain = host.AddComponent<EnemyBrain>();
            if (brain.HasRoomArea)
                failures.Add("[brain] a fresh EnemyBrain claims a room area (default must be OFF - village regression risk)");
            brain.SetRoomArea("junction", area, 2f, 6f);
            if (!brain.HasRoomArea)
                failures.Add("[brain] SetRoomArea did not arm HasRoomArea");
            if (brain.AreaRoomId != "junction")
                failures.Add($"[brain] AreaRoomId '{brain.AreaRoomId}' != 'junction' (room-assignment contract broken)");
            brain.SetRoomArea("x", new Bounds(Vector3.zero, Vector3.zero), 2f, 6f);
            if (brain.HasRoomArea)
                failures.Add("[brain] zero-size area must DISABLE room binding");

            // -- wake: measured from the FOOTPRINT, not a slot --
            // Hero at the entry seat (0,0,0.9): 8.1m from the junction footprint edge (z=9)
            // -> dormant at wake 6 (the frame-one beeline is dead).
            if (EnemyBrain.ShouldWake(area, 6f, true, new Vector3(0f, 0f, 0.9f)))
                failures.Add("[brain] hero at the entry seat must NOT wake the junction room (footprint dist ~8.1m > 6m)");
            // Hero in the corridor at z=4: 5m from the footprint -> awake.
            if (!EnemyBrain.ShouldWake(area, 6f, true, new Vector3(0f, 0f, 4f)))
                failures.Add("[brain] hero 5m from the room footprint must wake it (wake 6)");
            // Hero INSIDE the room -> distance 0 -> awake.
            if (!EnemyBrain.ShouldWake(area, 6f, true, new Vector3(1f, 0f, 12f)))
                failures.Add("[brain] hero inside the room footprint must wake it");
            // Hero absent -> dormant.
            if (EnemyBrain.ShouldWake(area, 6f, false, Vector3.zero))
                failures.Add("[brain] absent hero must leave the room dormant");
            // wakeRadius <= 0 -> no gate (always awake).
            if (!EnemyBrain.ShouldWake(area, 0f, true, new Vector3(100f, 0f, 100f)))
                failures.Add("[brain] wakeRadius 0 must mean no wake gate (always awake)");

            // -- confinement: destinations clamp into AABB + slack (the provoked-chase cap) --
            Vector3 inside = new Vector3(1f, 0f, 12f);
            if (EnemyBrain.ConfineToArea(inside, area, 2f) != inside)
                failures.Add("[brain] a destination inside the room must pass through unchanged");
            // Hero camping the entrance at z=0: clamp to the room's south face + slack (z=7).
            Vector3 confined = EnemyBrain.ConfineToArea(new Vector3(0f, 0f, 0f), area, 2f);
            if (Mathf.Abs(confined.z - 7f) > 0.01f || Mathf.Abs(confined.x) > 0.01f)
                failures.Add($"[brain] entrance chase must clamp to room edge + slack (expected z=7, got {confined})");
            // Negative slack shrinks (spawn-slot seating stays strictly in-room).
            Vector3 seat = EnemyBrain.ConfineToArea(new Vector3(0f, 0f, 8.8f), area, -0.5f);
            if (seat.z < 9.5f - 0.01f)
                failures.Add($"[brain] negative slack must seat INSIDE the room (expected z>=9.5, got {seat.z:F2})");
            // Y passes through.
            if (Mathf.Abs(EnemyBrain.ConfineToArea(new Vector3(0f, 1.7f, 0f), area, 2f).y - 1.7f) > 0.001f)
                failures.Add("[brain] ConfineToArea must not touch Y");
        }

        // =====================================================================
        //  CASE 3 - spawner serialized fields + ConfigureRoomArea
        // =====================================================================
        private static void Case3_SpawnerContract(List<string> failures)
        {
            // The baker writes these [SerializeField] privates via SerializedObject by NAME -
            // a rename breaks the bake silently. Pin them.
            var t = typeof(OutpostEnemyGroupSpawner);
            foreach (var field in new[] { "roomId", "areaCenter", "areaSize", "areaSlack", "wakeRadius",
                                          "minCount", "maxCount", "formationRadius", "leashRadius" })
            {
                if (t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null)
                    failures.Add($"[spawner] serialized field '{field}' missing on OutpostEnemyGroupSpawner " +
                                 "(DungeonBaker.WriteEncounterFields writes it by name)");
            }

            var host = new GameObject("__wo797_spawner");
            s_spawned.Add(host);
            var spawner = host.AddComponent<OutpostEnemyGroupSpawner>();
            if (spawner.HasRoomArea)
                failures.Add("[spawner] fresh spawner claims a room area (default must be OFF)");
            var area = new Bounds(new Vector3(0f, 2f, 12f), new Vector3(6f, 4f, 6f));
            spawner.ConfigureRoomArea("junction", area, 6f, 2f);
            if (!spawner.HasRoomArea)
                failures.Add("[spawner] ConfigureRoomArea did not arm HasRoomArea");
            if (spawner.RoomId != "junction")
                failures.Add($"[spawner] RoomId '{spawner.RoomId}' != 'junction'");
        }

        // =====================================================================
        //  CASE 4 - shared room-AABB math (+ best-effort real-prefab seat oracle)
        // =====================================================================
        private static void Case4_RoomBounds(List<string> failures, List<string> notes)
        {
            // Synthetic 1x1 room @6u at (0,0,12): 6x6 XZ footprint centred on the room.
            var room = new GameObject("__wo797_room");
            s_spawned.Add(room);
            var meta = room.AddComponent<RoomPrefabMeta>();
            meta.roomId = "synth";
            meta.footprintCells = new Vector2Int(1, 1);
            meta.cellSize = 6f;
            room.transform.position = new Vector3(0f, 0f, 12f);
            Bounds b = DungeonRoomBounds.Compute(room);
            if (Mathf.Abs(b.size.x - 6f) > 0.01f || Mathf.Abs(b.size.z - 6f) > 0.01f)
                failures.Add($"[room-bounds] 1x1@6 room computed size {b.size} (expected 6x6 XZ)");
            if (Mathf.Abs(b.center.x) > 0.01f || Mathf.Abs(b.center.z - 12f) > 0.01f)
                failures.Add($"[room-bounds] 1x1 room centre {b.center} (expected x=0 z=12)");
            if (!DungeonRoomBounds.ContainsXZ(b, new Vector3(2.9f, 0f, 14.9f)))
                failures.Add("[room-bounds] ContainsXZ false for an in-room point");
            if (DungeonRoomBounds.ContainsXZ(b, new Vector3(0f, 0f, 15.5f)))
                failures.Add("[room-bounds] ContainsXZ true for a point past the footprint");
            float d = Mathf.Sqrt(DungeonRoomBounds.SqrDistanceXZ(b, new Vector3(0f, 0f, 0.9f)));
            if (Mathf.Abs(d - 8.1f) > 0.05f)
                failures.Add($"[room-bounds] entry-seat -> junction footprint distance {d:F2} (expected ~8.1)");

            // Yaw 90 swaps a 2x1 footprint.
            var wide = new GameObject("__wo797_room_wide");
            s_spawned.Add(wide);
            var wmeta = wide.AddComponent<RoomPrefabMeta>();
            wmeta.footprintCells = new Vector2Int(2, 1);
            wmeta.cellSize = 6f;
            wide.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Bounds wb = DungeonRoomBounds.Compute(wide);
            if (Mathf.Abs(wb.size.x - 6f) > 0.01f || Mathf.Abs(wb.size.z - 12f) > 0.01f)
                failures.Add($"[room-bounds] 2x1 room at yaw 90 computed size {wb.size} (expected 6x12 XZ)");

            // Best-effort: every encounter room's REAL prefab seats its own centre inside
            // its own AABB (the bake-time seat oracle). Skipped with a note if the room
            // prefabs are absent in this checkout.
            if (!File.Exists(LayoutSA)) return;
            var layout = JsonConvert.DeserializeObject<DungeonComposeLayout>(File.ReadAllText(LayoutSA));
            if (layout?.rooms == null) return;
            float cell = layout.cellSize > 0.1f ? layout.cellSize : 6f;
            foreach (var place in layout.rooms)
            {
                if (place == null || place.encounter == null) continue;
                string id = string.IsNullOrEmpty(place.instanceId) ? place.prefab : place.instanceId;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomsFolder}/{place.prefab}.prefab");
                if (prefab == null) { notes.Add($"room prefab '{place.prefab}' absent - seat oracle skipped for '{id}'"); continue; }
                var inst = UnityEngine.Object.Instantiate(prefab);
                s_spawned.Add(inst);
                inst.transform.position = new Vector3(place.cell[0] * cell, place.cell[1] * cell, place.cell[2] * cell);
                inst.transform.rotation = Quaternion.Euler(0f, place.yawDeg, 0f);
                Bounds rb = DungeonRoomBounds.Compute(inst);
                if (rb.size.sqrMagnitude <= 0.01f)
                { failures.Add($"[room-bounds] encounter room '{id}' computed ZERO-size bounds"); continue; }
                if (!DungeonRoomBounds.ContainsXZ(rb, inst.transform.position))
                    failures.Add($"[room-bounds] encounter room '{id}' does not contain its own centre - seat oracle broken");
            }
        }

        // =====================================================================
        //  CASE 5 - runtime binder present + hooked
        // =====================================================================
        private static void Case5_Binder(List<string> failures)
        {
            var binderT = FindType("DeNelle.Dungeons.DungeonRoomBinder");
            if (binderT == null)
            {
                failures.Add("[binder] DungeonRoomBinder not found - already-baked composed scenes would spawn " +
                             "unowned mobs again (the F8 seq 622 entrance camp)");
                return;
            }
            var install = binderT.GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var bind = binderT.GetMethod("TryBind", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            bool hooked = install != null &&
                install.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0;
            if (bind == null)
                failures.Add("[binder] DungeonRoomBinder.TryBind not found - the binding path is gone");
            if (!hooked)
                failures.Add("[binder] DungeonRoomBinder.Install lacks [RuntimeInitializeOnLoadMethod] - it would never arm");
        }

        // =====================================================================
        //  CASE 6 - exit beacon (discoverability contract)
        // =====================================================================
        private static void Case6_ExitBeacon(List<string> failures)
        {
            DungeonExitInteractable exit = null;
            try
            {
                exit = DungeonExitInteractable.Spawn(Vector3.zero);
                if (exit == null) { failures.Add("[exit-beacon] Spawn returned null"); return; }
                s_spawned.Add(exit.gameObject);

                if (exit.GetComponentInChildren<DungeonExitBeacon>(true) == null)
                    failures.Add("[exit-beacon] spawned exit has NO DungeonExitBeacon - the F8 seq 622 " +
                                 "'no way to exit' discoverability fix is missing");
                if (exit.GetComponentInChildren<Light>(true) == null)
                    failures.Add("[exit-beacon] spawned exit has NO Light - the follow-the-light cue is gone");
                var trigger = exit.GetComponent<SphereCollider>();
                if (trigger == null || !trigger.isTrigger)
                    failures.Add("[exit-beacon] spawned exit lost its walk-in trigger");
                Transform beam = exit.transform.Find("Beacon_Beam");
                if (beam == null)
                    failures.Add("[exit-beacon] spawned exit has no Beacon_Beam glow pillar");
            }
            catch (Exception ex)
            {
                failures.Add($"[exit-beacon] Spawn threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        // =====================================================================
        //  CASE 7 - WO-849 pursuit bound (F8 seq 629 "not attacking me")
        // =====================================================================
        // The room a mob may WANDER in is tighter than the room it may PURSUE into.
        // Captured cause: an engaged skeleton's chase destination was clamped with the
        // 2m wander slack, pinning it on 'loop3's boundary while the hero stood ~1.7m
        // outside - five aggroed mobs unable to land a hit. Rule: pursuit clamps to
        // max(slack, wakeRadius) - "a mob may pursue as far as it can perceive".
        // This case pins BOTH halves: the near hero becomes reachable, AND the entrance
        // camp stays fixed (the whole point of WO-797).
        private static void Case7_PursuitBound(List<string> failures)
        {
            // THE LITERAL CAPTURED CASE (F8 seq 629, Player.log, starter loop):
            //   room 'loop3' centre (12,2,18) size (6,4,6) wake 6.0 slack 2.0
            //   "desired (5.30, 0.08, 22.55) snapped to (7.00, 0.08, 22.55) (slack 2.0m)"
            // loop3's XZ footprint is x 9..15; +2m wander slack reaches x=7.0; the hero
            // stood at x=5.30 - 3.7m outside the face, so 1.7m BEYOND the wander bound.
            var loop3 = new Bounds(new Vector3(12f, 2f, 18f), new Vector3(6f, 4f, 6f));
            const float wanderSlack = 2f;
            const float wakeRadius = 6f;
            float pursuit = Mathf.Max(wanderSlack, wakeRadius);   // the WO-849 rule
            if (pursuit <= wanderSlack)
                failures.Add("[pursuit-bound] pursuit slack must EXCEED the wander slack or the fix is inert");

            // (a) THE BUG, replayed exactly: the wander clamp snaps the chase to x=7.00,
            //     short of the hero at x=5.30 -> engaged but physically unable to reach.
            var heroDesired = new Vector3(5.30f, 0.08f, 22.55f);
            Vector3 wander = EnemyBrain.ConfineToArea(heroDesired, loop3, wanderSlack);
            if (Mathf.Abs(wander.x - 7f) > 0.01f || Mathf.Abs(wander.z - 22.55f) > 0.01f)
                failures.Add($"[pursuit-bound] precondition drift: wander clamp put the chase at {wander}, " +
                             "expected (7.00, _, 22.55) - the captured seq-629 defect no longer reproduces here");

            // (b) THE FIX: the SAME destination under the pursuit bound passes through
            //     unclamped - the mob can actually reach her.
            Vector3 chased = EnemyBrain.ConfineToArea(heroDesired, loop3, pursuit);
            if ((chased - heroDesired).sqrMagnitude > 0.0001f)
                failures.Add($"[pursuit-bound] the captured hero position must be REACHABLE under the pursuit bound " +
                             $"(desired {heroDesired} -> {chased}) - F8 seq 629 'not attacking me' would still repro");

            // (c) WO-797 MUST STILL HOLD: against the JUNCTION room (0,2,12), the entry
            //     seat (0,0,0.9) is 8.1m from the footprint - beyond wake 6 - so even the
            //     WIDER bound cannot reach it. If this ever passes through unclamped, the
            //     "all enemies gathered at the gate" camp is back.
            var junction = new Bounds(new Vector3(0f, 2f, 12f), new Vector3(6f, 4f, 6f));
            var entrySeat = new Vector3(0f, 0f, 0.9f);
            Vector3 atEntrance = EnemyBrain.ConfineToArea(entrySeat, junction, pursuit);
            if ((atEntrance - entrySeat).sqrMagnitude <= 0.0001f)
                failures.Add("[pursuit-bound] the ENTRY SEAT must remain OUT of pursuit range - the WO-797 " +
                             "'all enemies gathered at the gate' camp would return");
            if (atEntrance.z < 12f - 3f - pursuit - 0.01f)
                failures.Add($"[pursuit-bound] entrance chase clamped past the pursuit face (z={atEntrance.z:F2})");

            // (d) The brain still ACCEPTS the binding contract these bounds ride on.
            var host = new GameObject("__wo849_brain");
            s_spawned.Add(host);
            var brain = host.AddComponent<EnemyBrain>();
            brain.SetRoomArea("junction", junction, wanderSlack, wakeRadius);
            if (!brain.HasRoomArea || brain.AreaRoomId != "junction")
                failures.Add("[pursuit-bound] SetRoomArea contract broke - the pursuit bound has nothing to ride on");
        }

        // ---- helpers --------------------------------------------------------
        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void Cleanup()
        {
            foreach (var go in s_spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            s_spawned.Clear();
        }
    }
}
