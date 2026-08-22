// =============================================================================
// DungeonRoomBinder (WO-797) - rooms OWN their enemies in composed dungeons.
// -----------------------------------------------------------------------------
// F8 seq 622 (owner felt-test, dg_starter_loop): 13 'outpost-hollow-*' enemies with
// no room ownership drifted and CAMPED THE ENTRANCE GATE, burying the injected
// RETURN exit ("no way to exit"). The already-baked dg_starter_loop.unity is
// BINARY-serialized (no hand-edit; re-bake only in an isolated worktree), so this
// binder covers it at RUNTIME with the DungeonExitSpawner idiom: hook sceneLoaded
// once (RuntimeInitializeOnLoadMethod), and for every DungeonCompose_* scene bind
// each baked OutpostEnemyGroupSpawner to the room that contains it BEFORE the
// spawner's Start() auto-spawn runs (sceneLoaded fires after Awake, before Start).
//
// Data-driven, never hardcoded:
//   - room ids + AABBs come from the room instances the baked scene already
//     carries (RoomPrefabMeta footprint via the shared DungeonRoomBounds math);
//   - wake/slack/counts come from the layout JSON's WO-797 encounter blocks
//     (Resources dual-copy - synchronous on every platform), defaults otherwise.
//
// Idempotent: a spawner whose room area was already written at bake time
// (re-baked scene, SerializedObject fields) is skipped. Instrumented per
// CLAUDE.md sec.12 - every step/branch emits [Flow:DungeonRooms].
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;
using DeNelle.Village;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Auto-installer: binds every composed-dungeon enemy spawner to its owning room
    /// (bounds + wake + confinement) at scene load, so no mob ever drifts to camp the
    /// entrance. Self-arming, idempotent, no re-bake required.
    /// </summary>
    internal static class DungeonRoomBinder
    {
        private const string Sys = "DungeonRooms";
        private const string ComposeRootPrefix = "DungeonCompose_";
        private const string LayoutsResourcePath = "Data/Canonical/dungeon-layouts/";
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // A build that boots straight into a composed dungeon already loaded its
            // scene before this hook - process the active scene once, too.
            TryBind(SceneManager.GetActiveScene());
            FlowTrace.Step(Sys, "installed sceneLoaded hook (composed-dungeon room-ownership binder)");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBind(scene);

        private static void TryBind(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots;
            try { roots = scene.GetRootGameObjects(); }
            catch (Exception ex) { FlowTrace.Warn(Sys, $"GetRootGameObjects failed for '{scene.name}': {ex.Message}"); return; }

            Transform composeRoot = null;
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go != null && go.name.StartsWith(ComposeRootPrefix, StringComparison.Ordinal))
                {
                    composeRoot = go.transform;
                    break;
                }
            }
            // Not a composed dungeon - nothing to do (HealersCottage seats its own encounters).
            if (composeRoot == null) return;

            var spawners = CollectSpawners(scene);
            if (spawners.Count == 0)
            {
                FlowTrace.Step(Sys, $"'{scene.name}': no OutpostEnemyGroupSpawner found - nothing to bind");
                return;
            }

            // Rooms = the compose root's children that carry RoomPrefabMeta (the baked room
            // instances, named by their layout instanceId). The scene itself is the data source.
            var rooms = CollectRooms(composeRoot);
            if (rooms.Count == 0)
            {
                FlowTrace.Warn(Sys, $"'{scene.name}': composed scene has NO RoomPrefabMeta rooms - " +
                    "cannot bind spawners to rooms (mobs fall back to the WO-770.11 anchor leash)");
                return;
            }

            // Optional encounter data (wake/slack/counts) from the layout JSON dual-copy in
            // Resources (synchronous everywhere). Missing file/blocks => confinement defaults.
            string dungeonId = composeRoot.name.Substring(ComposeRootPrefix.Length);
            var encounters = LoadEncounters(dungeonId);
            int dungeonTier = LoadTier(dungeonId);

            int bound = 0;
            foreach (var spawner in spawners)
            {
                if (spawner == null) continue;

                // Owning room = the room whose footprint contains the spawner (XZ);
                // nearest footprint as fallback (a nav-snap can nudge a seat past an edge).
                Vector3 pos = spawner.transform.position;
                string roomId = spawner.HasRoomArea && rooms.ContainsKey(spawner.RoomId)
                    ? spawner.RoomId
                    : null;
                Bounds roomBounds = default;
                if (roomId != null) roomBounds = rooms[roomId];
                float best = roomId != null ? 0f : float.MaxValue;
                foreach (var kv in rooms)
                {
                    if (roomId != null) break;
                    float d = DungeonRoomBounds.SqrDistanceXZ(kv.Value, pos);
                    if (d < best) { best = d; roomId = kv.Key; roomBounds = kv.Value; }
                    if (d <= 0f) break; // strictly inside - owned
                }
                if (roomId == null)
                {
                    FlowTrace.Warn(Sys, $"spawner '{spawner.gameObject.name}' at {pos}: no owning room resolved - left unbound");
                    continue;
                }
                if (best > 0f)
                    FlowTrace.Warn(Sys, $"spawner '{spawner.gameObject.name}' at {pos} sits OUTSIDE every room " +
                        $"footprint (nearest '{roomId}', {Mathf.Sqrt(best):F2}m) - binding to nearest");

                float wake = 6f, slack = 2f;
                int min = -1, max = -1;
                float formation = -1f;
                int threat = 1;
                string displayName = null;
                // WO-1001 slice 2: the authored ENEMY FAMILY travels with the encounter block
                // so a scene baked BEFORE the encounterKind field existed still fields the
                // family its layout asks for. null = leave the serialized kind alone.
                string kind = null;
                if (encounters != null && encounters.TryGetValue(roomId, out var enc) && enc != null)
                {
                    if (enc.confine != null)
                    {
                        wake = enc.confine.wakeRadius;
                        slack = enc.confine.slack;
                    }
                    min = enc.min;
                    max = enc.max;
                    if (enc.formationRadius > 0f) formation = enc.formationRadius;
                    if (!string.IsNullOrEmpty(enc.kind)) kind = enc.kind.Trim();
                    int floorDepth = Mathf.RoundToInt(Mathf.Abs(roomBounds.center.y) /
                        DungeonBakerChecks.FloorSeparationY);
                    threat = enc.threat > 0 ? enc.threat : dungeonTier + floorDepth;
                    displayName = enc.displayName;
                }

                spawner.ConfigureRoomArea(roomId, roomBounds, wake, slack, min, max, formation,
                    kind, threat, displayName);
                bound++;
                FlowTrace.Step(Sys, $"bound spawner '{spawner.gameObject.name}' -> room '{roomId}' " +
                    $"bounds c{roomBounds.center} s{roomBounds.size} wake {wake:F1} slack {slack:F1} " +
                    $"kind '{(string.IsNullOrEmpty(kind) ? "<serialized>" : kind)}'");
            }

            FlowTrace.Step(Sys, $"'{scene.name}': room binding done - rooms={rooms.Count} " +
                $"spawners={spawners.Count} bound={bound}");
        }

        private static List<OutpostEnemyGroupSpawner> CollectSpawners(Scene scene)
        {
            var result = new List<OutpostEnemyGroupSpawner>();
            var all = UnityEngine.Object.FindObjectsByType<OutpostEnemyGroupSpawner>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in all)
                if (s != null && s.gameObject.scene == scene)
                    result.Add(s);
            return result;
        }

        private static Dictionary<string, Bounds> CollectRooms(Transform composeRoot)
        {
            var rooms = new Dictionary<string, Bounds>(StringComparer.Ordinal);
            foreach (Transform child in composeRoot)
            {
                if (child == null) continue;
                var meta = child.GetComponent<RoomPrefabMeta>();
                if (meta == null) continue;
                Bounds b = DungeonRoomBounds.Compute(child.gameObject);
                if (b.size.sqrMagnitude <= 0.01f)
                {
                    FlowTrace.Warn(Sys, $"room '{child.name}': zero-size bounds - skipped");
                    continue;
                }
                rooms[child.name] = b;
            }
            return rooms;
        }

        // Layout JSON (Resources dual-copy) -> instanceId -> encounter spec. Null-safe:
        // a missing/unparseable file just means defaults (never blanks the binding).
        private static Dictionary<string, EncounterSpec> LoadEncounters(string dungeonId)
        {
            var text = Guard.Try(Sys, $"load layout resource '{dungeonId}'",
                () => Resources.Load<TextAsset>(LayoutsResourcePath + dungeonId), null);
            if (text == null)
            {
                FlowTrace.Warn(Sys, $"layout '{LayoutsResourcePath}{dungeonId}' not in Resources - " +
                    "using default confinement (wake 6, slack 2)");
                return null;
            }
            var layout = Guard.Try(Sys, $"parse layout '{dungeonId}'",
                () => JsonConvert.DeserializeObject<DungeonComposeLayout>(text.text), null);
            if (layout == null || layout.rooms == null) return null;

            var map = new Dictionary<string, EncounterSpec>(StringComparer.Ordinal);
            foreach (var room in layout.rooms)
            {
                if (room == null || room.encounter == null) continue;
                string id = string.IsNullOrEmpty(room.instanceId) ? room.prefab : room.instanceId;
                if (!string.IsNullOrEmpty(id)) map[id] = room.encounter;
            }
            FlowTrace.Step(Sys, $"layout '{dungeonId}': {map.Count} encounter block(s) loaded");
            return map;
        }

        private static int LoadTier(string dungeonId)
        {
            var text = Resources.Load<TextAsset>(LayoutsResourcePath + dungeonId);
            if (text == null) return 1;
            var layout = Guard.Try(Sys, $"parse layout '{dungeonId}' for tier",
                () => JsonConvert.DeserializeObject<DungeonComposeLayout>(text.text), null);
            return layout != null ? Mathf.Max(1, layout.tier) : 1;
        }
    }
}
