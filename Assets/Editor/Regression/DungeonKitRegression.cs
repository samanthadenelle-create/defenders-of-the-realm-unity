// =============================================================================
// DungeonKitRegression [dungeon-kit] -- WO-595 tracked 24-piece snap-kit contract.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class DungeonKitRegression
    {
        private const string JsonRel = "Resources/Data/dungeon-kit.json";
        private const string BuilderRel = "Editor/RoomForge/DungeonKitBuilder.cs";

        public static bool Run(out string reason)
        {
            try
            {
                var failures = new List<string>();
                string jsonPath = Path.Combine(Application.dataPath, JsonRel);
                JObject root = JObject.Parse(File.ReadAllText(jsonPath));
                JObject grid = (JObject)root["grid"];
                JArray chunks = (JArray)root["chunks"];
                JObject themes = (JObject)root["themes"];

                Exact(grid, "cellSize", 4f, failures);
                Exact(grid, "wallHeight", 4f, failures);
                Exact(grid, "subGrid", 2f, failures);
                Exact(grid, "doorWidth", 2f, failures);
                Exact(grid, "levelStepY", 4f, failures);

                if (chunks == null || chunks.Count != 24)
                    failures.Add("catalog must contain exactly 24 chunks, found " + (chunks?.Count ?? 0));

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var modelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (chunks != null)
                {
                    foreach (JObject chunk in chunks)
                    {
                        string id = (string)chunk["id"];
                        if (string.IsNullOrWhiteSpace(id)) failures.Add("chunk has blank id");
                        else if (!ids.Add(id)) failures.Add("duplicate chunk id '" + id + "'");
                        var cells = chunk["cells"] as JArray;
                        if (cells == null || cells.Count != 2 || (int)cells[0] < 1 || (int)cells[1] < 1)
                            failures.Add(id + " has invalid integer cell footprint");
                        var sockets = chunk["sockets"] as JObject;
                        foreach (string side in new[] { "N", "E", "S", "W" })
                        {
                            string value = (string)sockets?[side];
                            if (value != "open" && value != "closed")
                                failures.Add(id + " socket " + side + " is not open|closed");
                        }
                        var parts = chunk["parts"] as JArray;
                        if (parts == null || parts.Count == 0) failures.Add(id + " has empty parts[]");
                        else foreach (JObject part in parts)
                        {
                            string fbx = (string)part["fbx"];
                            if (string.IsNullOrWhiteSpace(fbx)) failures.Add(id + " has a part with blank fbx");
                            else modelNames.Add(fbx);
                            var pos = part["pos"] as JArray;
                            if (pos == null || pos.Count != 3) failures.Add(id + "/" + fbx + " pos is not xyz");
                        }
                    }
                }

                foreach (string required in new[] {
                    "room_small", "room_medium", "room_large", "boss_room",
                    "hall_straight", "hall_corner_L", "hall_T", "hall_cross",
                    "door_gate", "door_arch", "entrance", "exit_portal",
                    "stairs_up", "stairs_down", "stairs_grand", "elevator",
                    "trap_spikes", "trap_pit", "trap_grate_hall", "pillar_hall" })
                    if (!ids.Contains(required)) failures.Add("required chunk missing: " + required);

                var options = themes?["options"] as JArray;
                if (options == null || options.Count != 7)
                    failures.Add("theme catalog must expose default plus six atlas variants");

                string modelDir = (string)root["modelDir"];
                string absoluteModels = Path.Combine(Application.dataPath, "..",
                    (modelDir ?? "").Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(absoluteModels))
                    foreach (string model in modelNames)
                        if (!File.Exists(Path.Combine(absoluteModels, model + ".fbx")))
                            failures.Add("catalog references missing KayKit model '" + model + "'");

                string builder = File.ReadAllText(Path.Combine(Application.dataPath, BuilderRel));
                Require(builder, "BuildPerimeter(", failures, "builder does not derive closed/open boundary walls");
                Require(builder, "BuildSockets(", failures, "builder does not author standard RoomSockets");
                Require(builder, "ApplyThemeAndCollision(", failures, "builder omits theme/collider normalization");
                Require(builder, "PlanRoute(seed, 12)", failures, "seeded self-avoiding preview composer is absent");
                Require(builder, "Debug.LogWarning", failures, "missing gitignored model path is not warning-safe");
                Require(builder, "DUNGEON_KIT_BUILD_OK", failures, "builder success marker is absent");
                Require(builder, "DUNGEON_KIT_COMPOSE_OK", failures, "composer success marker is absent");

                if (failures.Count > 0)
                {
                    reason = "dungeon-kit: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
                    return false;
                }
                reason = "dungeon-kit: 24/24 chunks have integer footprints, N/E/S/W sockets and nonempty model parts; " +
                         "4m grid + seven themes + collider-safe builder + seeded self-avoiding composer pinned";
                return true;
            }
            catch (Exception ex)
            {
                reason = "dungeon-kit: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_KIT_OK - " + reason);
            else Debug.LogError("DUNGEON_KIT_FAIL - " + reason);
        }

        private static void Exact(JObject grid, string field, float expected, List<string> failures)
        {
            float actual = grid?[field]?.Value<float>() ?? float.NaN;
            if (Math.Abs(actual - expected) > 0.001f)
                failures.Add("grid." + field + " expected " + expected + ", found " + actual);
        }

        private static void Require(string source, string token, List<string> failures, string message)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0) failures.Add(message);
        }
    }
}
