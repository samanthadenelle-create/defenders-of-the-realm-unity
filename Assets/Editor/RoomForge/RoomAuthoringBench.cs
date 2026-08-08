// =============================================================================
// RoomAuthoringBench — hand-authoring copies of room prefabs, safe from BuildAll.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only).
//
// WHY THIS EXISTS:
// DefaultDungeonRoomsBuilder.BuildAll() writes Assets/Dungeon/Rooms/*.prefab and
// OVERWRITES them, and it runs on every bake wave. So editing a shipped room prefab
// in place is work that gets destroyed the next time the kit is rebuilt — silently,
// with no error, because overwriting is exactly what the builder is supposed to do.
//
// This bench copies rooms into Assets/Dungeon/Rooms/Authoring/, which BuildAll never
// touches. The owner edits THERE. Nothing is lost to a rebuild.
//
// THE HANDBACK IS THE POINT. Once a room is authored, it has to become "expected",
// and there are exactly two honest ways to do that:
//
//   (A) AUTHORED WINS — the builder stops generating that room and the shipped
//       prefab becomes a copy of the authored one. Correct for anything with real
//       ART in it (placed stair meshes, dressed props): a procedural builder cannot
//       reproduce hand-placement, and pretending otherwise loses the work.
//       COST: that room stops responding to canon changes. Widen RoomForgeCanon.Cell
//       again and every generated room follows; an authored room does not, and will
//       silently be the wrong size. Any room promoted this way MUST get an oracle
//       case asserting its dimensions against the canon, or it rots.
//
//   (B) READ-BACK — measure the authored prefab and update the BUILDER so it
//       generates that shape. Correct when the edits are PARAMETERS (wall height,
//       tile span, a colour, a socket offset). Keeps the room procedural, so canon
//       changes keep propagating.
//       COST: only works for things expressible as numbers.
//
// Which applies is per-room and is a judgement call, not a default. Stairs are art
// (A). A wall height is a parameter (B). Say which was used, in the commit.
//
// MENU: Defenders > Dungeon > Authoring > ...
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor.RoomForge
{
    public static class RoomAuthoringBench
    {
        private const string ShippedFolder  = "Assets/Dungeon/Rooms";
        private const string AuthoringFolder = "Assets/Dungeon/Rooms/Authoring";
        private const string Sys = "RoomAuthor";

        /// <summary>
        /// The three rooms picked for the first authoring pass, and why each earns a slot.
        /// Deliberately small — three rooms is a session's work; thirteen is a project.
        /// </summary>
        private static readonly (string id, string why)[] Bench =
        {
            ("StairDown",
             "STRAIGHT / HORIZONTAL variant base. Today it places only a vertical socket - no stair " +
             "geometry at all - so floors are stacked and joined by a 'Descend' teleport prompt with no " +
             "NavMeshLink, which is why no enemy can follow the player between floors. One flight along " +
             "the run axis: 6m rise over the full 10m cell = 31 degrees."),

            ("StairDown_Left",
             "LEFT variant. Half-flight -> landing -> quarter-turn LEFT -> half-flight. Two 3m rises over " +
             "5m each is the SAME 31 degrees in HALF the linear footprint, so it fits a 1x1 with room to " +
             "spare - and the landing is the natural home for the ceiling/floor cut-out. Also gives the " +
             "composer somewhere to turn, instead of every descent continuing on one heading."),

            ("StairDown_Right",
             "RIGHT variant. Must be a genuine MIRROR of Left, not Left rotated 180 - a rotated left turn " +
             "is still a left turn. If the art carries any asymmetry (railing on one side, a wall torch), " +
             "mirroring must not bury it inside geometry."),
        };

        [MenuItem("Defenders/Dungeon/Authoring/1. Copy bench rooms for hand-editing")]
        public static void CopyBench()
        {
            if (!AssetDatabase.IsValidFolder(AuthoringFolder))
                AssetDatabase.CreateFolder(ShippedFolder, "Authoring");

            var copied = new List<string>();
            var missing = new List<string>();
            var skipped = new List<string>();

            foreach (var (id, why) in Bench)
            {
                string src = $"{ShippedFolder}/{id}.prefab";
                string dst = $"{AuthoringFolder}/{id}.prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null) { missing.Add(id); continue; }

                // NEVER clobber an existing authored room. That file is hand work; this
                // command is a convenience and must not be able to destroy it.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null) { skipped.Add(id); continue; }

                if (AssetDatabase.CopyAsset(src, dst)) copied.Add(id);
                else missing.Add(id + " (copy failed)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            FlowTrace.Step(Sys, $"AUTHORING BENCH -> {AuthoringFolder}");
            foreach (var (id, why) in Bench) FlowTrace.Step(Sys, $"  {id}: {why}");
            if (copied.Count  > 0) FlowTrace.Step(Sys, $"  copied: {string.Join(", ", copied)}");
            if (skipped.Count > 0) FlowTrace.Warn(Sys, $"  ALREADY AUTHORED, left untouched: {string.Join(", ", skipped)}");
            if (missing.Count > 0) FlowTrace.Fail(Sys, $"  MISSING from {ShippedFolder}: {string.Join(", ", missing)} - run BuildAll first");

            Debug.Log($"ROOM_AUTHORING_READY copied={copied.Count} skipped={skipped.Count} missing={missing.Count}");
        }

        [MenuItem("Defenders/Dungeon/Authoring/2. Report differences vs shipped")]
        public static void ReportDiff()
        {
            if (!AssetDatabase.IsValidFolder(AuthoringFolder))
            {
                Debug.LogWarning($"[{Sys}] no authoring folder yet - run '1. Copy bench rooms' first.");
                return;
            }

            foreach (var (id, _) in Bench)
            {
                var authored = AssetDatabase.LoadAssetAtPath<GameObject>($"{AuthoringFolder}/{id}.prefab");
                var shipped  = AssetDatabase.LoadAssetAtPath<GameObject>($"{ShippedFolder}/{id}.prefab");
                if (authored == null) { FlowTrace.Step(Sys, $"{id}: not authored yet"); continue; }
                if (shipped  == null) { FlowTrace.Warn(Sys, $"{id}: authored but NOT in the shipped kit"); continue; }

                Describe(id, "shipped ", shipped);
                Describe(id, "authored", authored);
            }
        }

        /// <summary>
        /// A measured summary rather than a visual one - renderer/collider counts and the
        /// world bounds. Enough to see AT A GLANCE what the hand pass added, and enough to
        /// tell whether the handback is a parameter change (option B) or real art (option A).
        /// </summary>
        private static void Describe(string id, string label, GameObject go)
        {
            int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
            int colliders = go.GetComponentsInChildren<Collider>(true).Length;
            int children  = go.GetComponentsInChildren<Transform>(true).Length - 1;

            var b = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }

            FlowTrace.Step(Sys,
                $"{id} [{label}] children={children} renderers={renderers} colliders={colliders} " +
                (any ? $"bounds={b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}" : "bounds=(none)"));
        }
    }
}
