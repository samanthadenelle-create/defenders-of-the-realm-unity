// =============================================================================
// CastleWallStairsSeatFix — seat the four hand-placed perimeter wall stairs
// (Dungeon_Stairs_Stone) flush against their wall AND on top of the WO-593 plinth,
// WITHOUT hand-editing the scene YAML (CLAUDE.md §3).
//
// PROVEN ROOT (owner F8 2026-07-02 "steps inside the wall the X should be even
// with wall" + read-only RCA): the 4 stairs are static scene prefab instances —
// NO builder creates them — authored ~4.7–6.5 m inboard of the wall faces, and
// they were left at y≈0 by the WO-593 raise, so they now sit buried inside the
// CastleBasePlinth (top y=liftY) while the walls they climb rose to +liftY.
//
// METHOD (measure, not guess — every target is read from live renderer bounds):
//   • wall inner face  = combined bounds of the CastleSide_<side> wall group,
//     taken on the courtyard-facing edge
//   • stair extents    = the stair's own combined renderer bounds
//   • floor level      = CastleBasePlinth bounds.max.y (the plinth top the hero
//     walks on), fallback PlayerPrefs "castle.liftY" (default 3) if plinth absent
// Each stair slides along its wall's outward axis until its wall-side face is
// flush with the wall's inner face (no gap, no penetration), and its base seats
// on the plinth top. Lateral position along the wall is preserved (owner-authored).
// Idempotent: re-running recomputes from current bounds and converges.
//
// Run (EDITOR CLOSED, batchmode):
//   -executeMethod DeNelle.Editor.CastleWallStairsSeatFix.Run
// Then rebake nav (the stairs carry colliders) — run the castle batch rebuild
// AFTER this so the bake captures the seated stairs.
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class CastleWallStairsSeatFix
    {
        private const string ScenePath       = "Assets/Scenes/MainCastle_Hall.unity";
        private const string MergedScenePath = "Assets/Scenes/Main_Castle_Overworld.unity"; // the SHIPPED world (F8-24)
        private const string StairPrefix = "Dungeon_Stairs";
        private const string PlinthName  = "CastleBasePlinth";

        [MenuItem("Defenders/Castle/Seat Wall Stairs Flush (F8 2026-07-02)")]
        public static void Run()
        {
            // Standalone path: open + seat + save. Batch callers must use RunOnOpenScene —
            // OpenScene(Single) DISCARDS every unsaved change in the calling batch (proven
            // 2026-07-02: it silently reverted BatchRebuild's debris purge + prefab cleanup).
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RunOnOpenScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Log("=== wall-stairs seat fix standalone save: saved=" + saved + " ===");
        }

        // Last owner-approved seats (from the 2026-07-02 4/4 run) — the ALONG-WALL coordinate is
        // authored taste; the flush axis + Y are re-derived below, so only the lateral seat matters.
        private static readonly Dictionary<string, Vector3> DefaultSeat = new Dictionary<string, Vector3>
        {
            { "South", new Vector3(-13.12f, 3f, -35.17f) },
            { "West",  new Vector3(-35.20f, 3f,  21.00f) },
            { "North", new Vector3(-14.40f, 3f,  35.17f) },
            { "East",  new Vector3( 33.92f, 3f, -15.75f) },
        };
        private static readonly Dictionary<string, float> SideYaw = new Dictionary<string, float>
        { { "South", 0f }, { "West", 90f }, { "North", 180f }, { "East", 270f } };

        // ── Owner ruling 2026-07-08: "if you cannot fix so they end at the top simply
        // remove the steps for now" ──────────────────────────────────────────────────
        // The walls rose +liftY (WO-593) but the Dungeon_Stairs_Stone prefab's rise is
        // fixed, so the seated stairs END MID-AIR short of the rampart top ("stairs
        // still in air", "2 sets of steps on the air" — the census self-heal can also
        // duplicate: rebuild-restored originals + surviving clones). Removal is gated
        // by EditorPrefs 'castle.stairsRemoved' so the census can't resurrect them;
        // clear the pref (or run Restore) when stair models that reach the top exist.
        private const string RemovedPref = "castle.stairsRemoved";

        [MenuItem("Defenders/Castle/REMOVE Wall Stairs (owner 2026-07-08)")]
        public static void RemoveAll() => RemoveAllInScene(ScenePath);

        // F8-24 (owner "two sets of steps still in the air"): the SHIPPED scene is the merged
        // Main_Castle_Overworld.unity, not the Hall — the 07-07 removal commit swept only the Hall,
        // leaving the two ORIGINAL PrefabInstances ('Dungeon_Stairs_Stone' / '(1)') floating at y≈3
        // (WorldMergeBuilder.LiftedCastleRoots lowers by exact name and only lists the _West/_South
        // clones). This entry sweeps the merged scene directly. Batchmode:
        //   -executeMethod DeNelle.Editor.CastleWallStairsSeatFix.RemoveAllInMergedScene
        // Then rebake nav (the floaters carry MeshColliders): WorldMergeBuilder.BakeMergedWorldNavmesh.
        [MenuItem("Defenders/Castle/REMOVE Wall Stairs in MERGED World (F8-24)")]
        public static void RemoveAllInMergedScene() => RemoveAllInScene(MergedScenePath);

        /// <summary>Open <paramref name="scenePath"/>, destroy every Dungeon_Stairs* object, save.
        /// Sets the 'castle.stairsRemoved' gate so the census/seat-fix can't resurrect them.</summary>
        public static void RemoveAllInScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int removed = RemoveAllOnOpenScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StairsSweep] removed {removed} stair object(s) from {scenePath}");
            Log("=== wall-stairs REMOVE standalone save: saved=" + saved + " (" + scenePath + ") ===");
        }

        /// <summary>Sweep the ALREADY-OPEN scene (caller owns lifecycle/save). Returns the count destroyed.</summary>
        public static int RemoveAllOnOpenScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorPrefs.SetBool(RemovedPref, true);
            var doomed = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith(StairPrefix, System.StringComparison.OrdinalIgnoreCase)
                        && !HasStairAncestor(t))
                        doomed.Add(t);
            foreach (var t in doomed)
            {
                Log("REMOVED '" + t.name + "' at " + t.position + " (owner ruling: steps end mid-air).");
                Object.DestroyImmediate(t.gameObject);
            }
            Log($"=== wall-stairs REMOVE DONE — {doomed.Count} object(s) destroyed; census disarmed " +
                "(EditorPrefs castle.stairsRemoved). Rebake nav next. ===");
            return doomed.Count;
        }

        [MenuItem("Defenders/Castle/Restore Wall Stairs (re-arm census)")]
        public static void RestoreCensus()
        {
            EditorPrefs.DeleteKey(RemovedPref);
            Log("census re-armed — the next seat-fix run re-clones the four wall stairs.");
        }

        /// <summary>Seat the stairs in the ALREADY-OPEN scene. No open, no save — the caller
        /// owns scene lifecycle (the batch rebuild saves once at the end).</summary>
        public static void RunOnOpenScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (EditorPrefs.GetBool(RemovedPref, false))
            {
                // Owner ruling in force: keep the scene stair-free even across rebuilds
                // (CastleHubBuilder calls this seat-fix — without the sweep a rebuild-restored
                // original would come back mid-air).
                RemoveAllOnOpenScene(scene);
                Log("seat fix SKIPPED — owner removal ruling in force (castle.stairsRemoved).");
                return;
            }
            Log("=== wall-stairs seat fix START ===");
            EnsureFourStairs(scene);

            // Floor level = plinth top, measured; PlayerPref fallback if the plinth
            // hasn't been built into the saved scene yet.
            float floorY = PlayerPrefs.GetFloat("castle.liftY", 3f);
            var plinth = GameObject.Find(PlinthName);
            if (plinth != null)
            {
                var pr = plinth.GetComponent<Renderer>();
                if (pr != null) floorY = pr.bounds.max.y;
            }
            else Warn(PlinthName + " not in scene — using PlayerPrefs castle.liftY fallback " + floorY);

            // Wall groups per side: combined renderer bounds of each CastleSide_* group's WALL RUN
            // pieces ONLY (children named Wall_* — the authored runs + T-007 seam fills that define
            // the wall PLANE). WO-593 F8 follow-up: the whole-group bounds also swallowed the
            // CornerTower_* (radial ~42.33, fatter than the wall line at ~40.6) and the Gate arch,
            // whose inboard bulge became the measured "inner face" — so the stairs seated ~1.7m+
            // short of the actual wall plane (the owner's "X should be even with wall" recess).
            var sideBounds = new Dictionary<string, Bounds>();
            foreach (var side in new[] { "South", "West", "North", "East" })
            {
                var group = GameObject.Find("CastleSide_" + side);
                if (group == null) { Warn("CastleSide_" + side + " not found — that side's stair will be skipped."); continue; }
                if (TryCombinedWallBounds(group.transform, out var b)) sideBounds[side] = b;
                else if (TryCombinedBounds(group.transform, out b))
                {
                    Warn("CastleSide_" + side + ": no Wall_* children measured — falling back to whole-group bounds (tower/gate bulge may skew the flush plane).");
                    sideBounds[side] = b;
                }
            }

            // Every Dungeon_Stairs* instance in the scene (they live under different parents).
            var stairs = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith(StairPrefix, System.StringComparison.OrdinalIgnoreCase)
                        && t.GetComponentInChildren<Renderer>(true) != null
                        && !HasStairAncestor(t))
                        stairs.Add(t);

            int moved = 0;
            foreach (var stair in stairs)
            {
                if (!TryCombinedBounds(stair, out var sb)) { Warn(stair.name + ": no renderer bounds — skipped."); continue; }

                // Side = dominant axis of the stair position (walls are axis-aligned).
                Vector3 p = stair.position;
                bool xDominant = Mathf.Abs(p.x) > Mathf.Abs(p.z);
                string side = xDominant ? (p.x < 0f ? "West" : "East")
                                        : (p.z < 0f ? "South" : "North");
                if (!sideBounds.TryGetValue(side, out var wb)) { Warn(stair.name + ": no wall bounds for side " + side + " — skipped."); continue; }

                // Wall inner (courtyard-facing) face along the outward axis, and the stair's
                // current wall-side face. Slide the stair so those two coincide (flush).
                float delta;
                if (xDominant)
                {
                    float wallInner = p.x < 0f ? wb.max.x : wb.min.x;   // West wall's +x face / East wall's -x face
                    float stairFace = p.x < 0f ? sb.min.x : sb.max.x;   // stair face toward the wall
                    delta = wallInner - stairFace;
                    stair.position += new Vector3(delta, 0f, 0f);
                }
                else
                {
                    float wallInner = p.z < 0f ? wb.max.z : wb.min.z;
                    float stairFace = p.z < 0f ? sb.min.z : sb.max.z;
                    delta = wallInner - stairFace;
                    stair.position += new Vector3(0f, 0f, delta);
                }

                // Seat the base on the plinth top (preserve the transform's offset from its bounds).
                TryCombinedBounds(stair, out sb); // re-read after the lateral move
                float yDelta = floorY - sb.min.y;
                stair.position += new Vector3(0f, yDelta, 0f);

                moved++;
                Log($"{stair.name} [{side}]: slid {delta:F2} to wall inner face, raised {yDelta:F2} to floor y={floorY:F2} -> pos {stair.position}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Log($"=== wall-stairs seat fix DONE — {moved}/{stairs.Count} stair(s) seated (scene dirty; caller saves). Rebake nav next. ===");
        }

        // ── Self-healing stair census (2026-07-02) ─────────────────────────────────
        // CastleWallsFromRecipe.Recreate() destroys the CastleSide_* groups each rebuild;
        // stairs parented under them die with the group (proven: bake3 log 'removed
        // CastleSide_South/West' + this fix then finding only 2/2 stairs). Ensure one stair
        // per side by cloning a survivor for any missing side, parented OUTSIDE the recipe
        // groups so the next rebuild can't eat it. Seat/flush is re-derived after, so the
        // clone only needs the side's authored lateral seat + facing.
        private static void EnsureFourStairs(UnityEngine.SceneManagement.Scene scene)
        {
            var found = new Dictionary<string, Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith(StairPrefix, System.StringComparison.OrdinalIgnoreCase)
                        && t.GetComponentInChildren<Renderer>(true) != null && !HasStairAncestor(t))
                    {
                        Vector3 p = t.position;
                        bool xDom = Mathf.Abs(p.x) > Mathf.Abs(p.z);
                        string side = xDom ? (p.x < 0f ? "West" : "East") : (p.z < 0f ? "South" : "North");
                        if (!found.ContainsKey(side)) found[side] = t;
                    }

            if (found.Count == 0)
            {
                Warn("no wall stairs in scene at all (KayKit pack absent on fresh clone?) — nothing to clone; skipping census.");
                return;
            }
            if (found.Count == 4) return;

            // Donor = any survivor; clones go under the donor's parent IF it isn't a recipe
            // group, else the scene root (survival guarantee is the whole point).
            Transform donor = null; string donorSide = null;
            foreach (var kv in found) { donor = kv.Value; donorSide = kv.Key; break; }
            Transform parent = donor.parent != null && !donor.parent.name.StartsWith("CastleSide_")
                             ? donor.parent : null;

            foreach (var side in new[] { "South", "West", "North", "East" })
            {
                if (found.ContainsKey(side)) continue;
                var clone = Object.Instantiate(donor.gameObject);
                clone.name = donor.name.Replace("(Clone)", "").Trim() + "_" + side;
                clone.transform.SetParent(parent, true);
                float yawDelta = SideYaw[side] - SideYaw[donorSide];
                clone.transform.rotation = Quaternion.Euler(0f, yawDelta, 0f) * donor.rotation;
                clone.transform.position = DefaultSeat[side];
                Log($"census: side {side} had NO stair — cloned '{donor.name}' -> '{clone.name}' at authored seat {DefaultSeat[side]} (flush/Y re-derived below).");
            }
        }

        // Never treat a nested child of another matched stair as its own stair.
        private static bool HasStairAncestor(Transform t)
        {
            for (var a = t.parent; a != null; a = a.parent)
                if (a.name.StartsWith(StairPrefix, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Combined world-space renderer bounds of ONLY the Wall_* children of a side group —
        // the pieces that define the actual wall PLANE (excludes CornerTower_*/Gate_* whose
        // inboard bulge otherwise skews the flush target). False if no Wall_* renderers.
        private static bool TryCombinedWallBounds(Transform sideGroup, out Bounds bounds)
        {
            bounds = default;
            bool have = false;
            foreach (Transform child in sideGroup)
            {
                if (!child.name.StartsWith("Wall_", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!TryCombinedBounds(child, out var b)) continue;
                if (!have) { bounds = b; have = true; }
                else bounds.Encapsulate(b);
            }
            return have;
        }

        // Combined world-space renderer bounds of a subtree. False if no renderers.
        private static bool TryCombinedBounds(Transform root, out Bounds bounds)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            if (rends.Length == 0) return false;
            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
            return true;
        }

        private static void Log(string m)  => Debug.Log("[CastleWallStairsSeatFix] " + m);
        private static void Warn(string m) => Debug.LogWarning("[CastleWallStairsSeatFix] " + m);
    }
}
