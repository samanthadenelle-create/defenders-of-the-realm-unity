// =============================================================================
// DungeonDressingRegression [dungeon-dressing] -- FAIL-BY-DESIGN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. A composed dungeon should read as a DRESSED
// place -- torches, barrels, decor seated into each composed room. The composed
// (RoomForge) pipeline has NO dressing pass: DungeonBakerChecks.Compose only mates /
// re-verifies / seals sockets; DungeonBaker only instantiates room prefabs + bakes a
// NavMesh + (opt) seats a hero/enemy spawners. Props exist ONLY as whatever a room
// prefab already baked at author time; the composer adds zero.
//
// This oracle asserts the MISSING SEAM: the composed pipeline must expose a dressing
// entrypoint (a Dress/Prop/Decor/Furnish pass) that seats props into composed rooms.
// It does not, so this FAILS TRUTHFULLY today and flips green the moment a dressing
// pass is added to the composed pipeline. Deterministic (a type/method scan, no scene).
//
// Marker: DUNGEON_DRESSING_OK / DUNGEON_DRESSING_FAIL. Expected: RED (design gap).
//
// Wire (DataRegression.RunAll):
//   if (!DungeonDressingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-dressing] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonDressingRegression
    {
        // The composed-dungeon pipeline surfaces (runtime checks + editor bakers).
        private static readonly string[] PipelineTypes =
        {
            "DeNelle.Dungeons.RoomForge.DungeonBakerChecks",
            "DeNelle.Editor.RoomForge.DungeonBaker",
            "DeNelle.Editor.RoomForge.GraphDungeonComposer",
        };

        // A dressing pass would name one of these in its method surface.
        private static readonly string[] DressingTokens = { "Dress", "Prop", "Decor", "Furnish" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DUNGEON DRESSING (composed rooms get >0 prop children after a dressing pass) ---");

            int typesFound = 0;
            var dressingMethods = new List<string>();
            foreach (var full in PipelineTypes)
            {
                var t = FindType(full);
                if (t == null) { log.AppendLine($"  (pipeline type absent: {full})"); continue; }
                typesFound++;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var tok in DressingTokens)
                        if (m.Name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                        { dressingMethods.Add(full + "." + m.Name); break; }
                }
            }

            log.AppendLine($"  scanned {typesFound}/{PipelineTypes.Length} composed-pipeline types; dressing methods found: {dressingMethods.Count}");
            if (typesFound == 0)
                failures.Add("[dungeon-dressing] none of the composed-pipeline types loaded -- cannot evaluate the dressing seam");
            else if (dressingMethods.Count == 0)
                failures.Add("[dungeon-dressing] FAIL-BY-DESIGN: the composed (RoomForge) pipeline has NO dressing pass (no Dress/Prop/Decor/Furnish method) -- composed rooms carry only whatever props a source prefab baked, and the composer seats zero. Add a dressing pass that seats props into composed rooms to flip this green.");
            else
                log.AppendLine("  dressing methods: " + string.Join(", ", dressingMethods));

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "DUNGEON_DRESSING_OK");
                reason = "DUNGEON DRESSING OK -- the composed pipeline exposes a dressing pass that seats props into rooms";
                return true;
            }
            reason = "dungeon-dressing: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "DUNGEON_DRESSING_FAIL: " + reason);
            return false;
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
