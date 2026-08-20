// =============================================================================
// SkyGeometryDiag — find the geometry that is HANGING IN THE SKY.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Batch:
//   -executeMethod DeNelle.Editor.SkyGeometryDiag.RunOnStarterLoop
//   -executeMethod DeNelle.Editor.SkyGeometryDiag.RunOnAllDungeons
//   -executeMethod DeNelle.Editor.SkyGeometryDiag.Run            (active scene)
// Marker: SKY_GEOMETRY_DIAG_OK
//
// WHY THIS EXISTS (CLAUDE.md §12 — INSTRUMENT, DON'T GUESS). The owner's device
// screenshot shows a sky-spanning grey/brown rock mass over ordinary green terrain
// while the active scene is 'dg_starter_loop'. Two prior theories were BOTH wrong
// on inspection (SeatOnGround misread a centre pivot; the dungeon portal prop is
// 4x7x6 m at y=0). Neither theory came from measurement, which is precisely the
// banned move. This tool measures instead: it walks EVERY Renderer in the loaded
// scene(s) and prints world-space bounds, so "what is that thing in the sky" is
// answered by a number and a hierarchy path, not by a hypothesis.
//
// TWO INDEPENDENT NETS, because the defect could be either shape:
//   HIGH  — bounds.min.y >= MinYThreshold: something whose LOWEST point is above
//           the player's head. A ceiling hanging over an outdoor scene lands here.
//   HUGE  — any bounds axis >= MaxSizeThreshold: something big enough to span the
//           frame regardless of where its pivot sits. A mis-scaled prop lands here.
// A renderer is reported if it trips EITHER net; the flags column says which.
//
// WORLD BOUNDS, NOT localPosition. The prior misdiagnosis happened because a trace
// printed transform.localPosition — a PIVOT — and a centre-pivoted 4 m body reads
// y=+2.00 when it is seated perfectly. Renderer.bounds is the axis-aligned WORLD
// box the camera actually rasterises, so min.y IS the height of the lowest visible
// pixel. That is the only number that can settle a "floating in the air" report.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class SkyGeometryDiag
    {
        /// <summary>Lowest visible point at or above this (metres) counts as SKY.</summary>
        private const float MinYThreshold = 15f;

        /// <summary>Any single world-bounds axis at or above this (metres) counts as HUGE.</summary>
        private const float MaxSizeThreshold = 50f;

        private const string StarterLoopPath = "Assets/Scenes/DungeonCompose/dg_starter_loop.unity";
        private const string ComposeFolder = "Assets/Scenes/DungeonCompose";
        private const string OverworldPath = "Assets/Scenes/Main_Castle_Overworld.unity";

        [MenuItem("Defenders/World/Diagnose sky geometry (active scene)")]
        public static void Run()
        {
            var report = new StringBuilder();
            int flagged = Sweep(report, SceneManager.GetActiveScene().name);
            Debug.Log(report.ToString());
            Debug.Log($"SKY_GEOMETRY_DIAG_OK {flagged} flagged renderer(s) in active scene");
        }

        [MenuItem("Defenders/World/Diagnose sky geometry (dg_starter_loop)")]
        public static void RunOnStarterLoop()
        {
            var report = new StringBuilder();
            int flagged = SweepScenePath(report, StarterLoopPath);
            Debug.Log(report.ToString());
            Debug.Log($"SKY_GEOMETRY_DIAG_OK {flagged} flagged renderer(s) in dg_starter_loop");
        }

        [MenuItem("Defenders/World/Diagnose sky geometry (all composed dungeons)")]
        public static void RunOnAllDungeons()
        {
            var report = new StringBuilder();
            int total = 0;

            var guids = AssetDatabase.FindAssets("t:Scene", new[] { ComposeFolder });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                             .Where(p => !string.IsNullOrEmpty(p))
                             .OrderBy(p => p)
                             .ToList();

            if (paths.Count == 0)
            {
                Debug.LogError($"SKY_GEOMETRY_DIAG_FAIL :: no scenes found under {ComposeFolder}");
                return;
            }

            foreach (var p in paths)
                total += SweepScenePath(report, p);

            Debug.Log(report.ToString());
            Debug.Log($"SKY_GEOMETRY_DIAG_OK {total} flagged renderer(s) across {paths.Count} composed dungeon scene(s)");
        }

        [MenuItem("Defenders/World/Diagnose sky geometry (overworld hub)")]
        public static void RunOnOverworld()
        {
            var report = new StringBuilder();
            int flagged = SweepScenePath(report, OverworldPath);
            Debug.Log(report.ToString());
            Debug.Log($"SKY_GEOMETRY_DIAG_OK {flagged} flagged renderer(s) in Main_Castle_Overworld");
        }

        /// <summary>
        /// Sweep whatever scene path SKY_DIAG_SCENE names — so a new suspect costs a batch
        /// argument, not a code edit and a recompile.
        /// </summary>
        public static void RunOnEnvScene()
        {
            string path = Environment.GetEnvironmentVariable("SKY_DIAG_SCENE");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("SKY_GEOMETRY_DIAG_FAIL :: SKY_DIAG_SCENE env var is empty.");
                return;
            }

            var report = new StringBuilder();
            int flagged = SweepScenePath(report, path);
            Debug.Log(report.ToString());
            Debug.Log($"SKY_GEOMETRY_DIAG_OK {flagged} flagged renderer(s) in '{path}'");
        }

        /// <summary>
        /// Answer "what is ABOVE the player, and where is the ground under her" for one XZ column.
        /// Defaults to the starter-loop world portal at (140, 20) — the exact spot the device trace
        /// puts the hero at y=-2.65 while the portal is authored at y=0.
        /// Override with SKY_DIAG_PROBE="x,z[,radius]".
        /// </summary>
        [MenuItem("Defenders/World/Probe the portal column (overworld)")]
        public static void ProbePortalColumn()
        {
            float px = 140f, pz = 20f, radius = 60f;

            string env = Environment.GetEnvironmentVariable("SKY_DIAG_PROBE");
            if (!string.IsNullOrEmpty(env))
            {
                var parts = env.Split(',');
                if (parts.Length >= 2)
                {
                    float.TryParse(parts[0], out px);
                    float.TryParse(parts[1], out pz);
                    if (parts.Length >= 3) float.TryParse(parts[2], out radius);
                }
            }

            try
            {
                EditorSceneManager.OpenScene(OverworldPath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SKY_GEOMETRY_DIAG_FAIL :: OpenScene threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine("=============================================================");
            report.AppendLine($"--- PORTAL COLUMN PROBE :: ({px:0.0}, *, {pz:0.0}) radius {radius:0.0} m ---");

            // --- Terrains. Terrain is NOT a Renderer, so the sweep above CANNOT see it.
            // That blind spot matters here: a low-poly landscape read from below is exactly
            // the shape the owner's screenshot shows.
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            report.AppendLine($"  terrains: {(terrains != null ? terrains.Length : 0)}");
            if (terrains != null)
            {
                foreach (var t in terrains)
                {
                    if (t == null) continue;
                    var td = t.terrainData;
                    Vector3 origin = t.transform.position;
                    Vector3 size = td != null ? td.size : Vector3.zero;
                    float sampled = float.NaN;
                    try { sampled = t.SampleHeight(new Vector3(px, 0f, pz)) + origin.y; }
                    catch (Exception ex) { report.AppendLine($"    !! SampleHeight threw {ex.GetType().Name}"); }

                    report.AppendLine($"  TERRAIN '{t.name}' path={HierarchyPath(t.transform)}");
                    report.AppendLine($"      active={t.gameObject.activeInHierarchy} enabled={t.enabled} " +
                                      $"drawHeightmap={t.drawHeightmap}");
                    report.AppendLine($"      origin={Fmt(origin)}  dataSize={Fmt(size)}  " +
                                      $"covers x[{origin.x:0.0}..{origin.x + size.x:0.0}] z[{origin.z:0.0}..{origin.z + size.z:0.0}]");
                    report.AppendLine($"      HEIGHT AT PROBE = {sampled:0.00} m  " +
                                      $"(inside={(px >= origin.x && px <= origin.x + size.x && pz >= origin.z && pz <= origin.z + size.z)})");
                }
            }

            // --- Terrain height GRID around the probe. A single sample says how deep the hole is;
            // only the neighbourhood says whether she is standing in a walled pit whose rim can
            // fill the top of the frame. Rows are Z, columns are X, both centred on the probe.
            if (terrains != null && terrains.Length > 0)
            {
                var t0 = terrains[0];
                report.AppendLine();
                report.AppendLine("  TERRAIN HEIGHT GRID (metres, rows=Z north-up, cols=X east-right, step 20 m):");
                var head = new StringBuilder("        ");
                for (int dx = -100; dx <= 100; dx += 20) head.Append($"{px + dx,8:0}");
                report.AppendLine(head.ToString());
                for (int dz = 100; dz >= -100; dz -= 20)
                {
                    var line = new StringBuilder($"  z={pz + dz,5:0} ");
                    for (int dx = -100; dx <= 100; dx += 20)
                    {
                        float h;
                        try { h = t0.SampleHeight(new Vector3(px + dx, 0f, pz + dz)) + t0.transform.position.y; }
                        catch { h = float.NaN; }
                        line.Append($"{h,8:0.0}");
                    }
                    report.AppendLine(line.ToString());
                }
            }

            // --- Everything whose XZ footprint overlaps the probe column, sorted by how high it sits.
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var hits = new List<(Renderer r, Bounds b, float dist)>();

            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    Bounds b;
                    try { b = r.bounds; } catch { continue; }
                    if (b.size == Vector3.zero) continue;

                    float dx = Mathf.Max(0f, Mathf.Max(b.min.x - px, px - b.max.x));
                    float dz = Mathf.Max(0f, Mathf.Max(b.min.z - pz, pz - b.max.z));
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > radius) continue;

                    hits.Add((r, b, dist));
                }
            }

            report.AppendLine();
            report.AppendLine($"  {hits.Count} renderer(s) within {radius:0.0} m of the column, highest first:");
            foreach (var h in hits.OrderByDescending(x => x.b.max.y).Take(60))
            {
                report.AppendLine($"    y[{h.b.min.y,8:0.00} .. {h.b.max.y,8:0.00}]  d={h.dist,6:0.0}m  " +
                                  $"size={Fmt(h.b.size)}  active={h.r.gameObject.activeInHierarchy}  " +
                                  $"{HierarchyPath(h.r.transform)}");
            }

            report.AppendLine("=============================================================");
            Debug.Log(report.ToString());
            Debug.Log($"SKY_GEOMETRY_DIAG_OK column probe at ({px:0.0},{pz:0.0}) — {hits.Count} renderer(s)");
        }

        // ---------------------------------------------------------------------

        private static int SweepScenePath(StringBuilder report, string scenePath)
        {
            // Guard, not assume: a missing scene must say so, never silently score zero.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath) == null)
            {
                report.AppendLine($"!! scene asset not found: {scenePath}");
                return 0;
            }

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                report.AppendLine($"!! OpenScene('{scenePath}') threw {ex.GetType().Name}: {ex.Message}");
                return 0;
            }

            return Sweep(report, System.IO.Path.GetFileNameWithoutExtension(scenePath));
        }

        private static int Sweep(StringBuilder report, string label)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            report.AppendLine();
            report.AppendLine("=============================================================");
            report.AppendLine($"--- SKY GEOMETRY DIAG :: '{label}' ---");
            report.AppendLine($"  loaded scenes: {DescribeLoadedScenes()}");
            report.AppendLine($"  renderers scanned: {(renderers != null ? renderers.Length : 0)}");
            report.AppendLine($"  nets: HIGH = bounds.min.y >= {MinYThreshold:0.#} m ; " +
                              $"HUGE = any bounds axis >= {MaxSizeThreshold:0.#} m");

            if (renderers == null || renderers.Length == 0)
            {
                report.AppendLine("  (no renderers — nothing to measure)");
                return 0;
            }

            var rows = new List<Row>();
            float sceneMaxY = float.NegativeInfinity;
            float sceneMinY = float.PositiveInfinity;

            foreach (var r in renderers)
            {
                if (r == null) continue;

                Bounds b;
                try { b = r.bounds; }
                catch (Exception ex)
                {
                    report.AppendLine($"  !! bounds threw on '{SafeName(r)}': {ex.GetType().Name}");
                    continue;
                }

                // A zero-size box is a renderer with nothing to draw; it cannot be the sky mass.
                if (b.size == Vector3.zero) continue;

                sceneMaxY = Mathf.Max(sceneMaxY, b.max.y);
                sceneMinY = Mathf.Min(sceneMinY, b.min.y);

                bool high = b.min.y >= MinYThreshold;
                float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                bool huge = biggest >= MaxSizeThreshold;
                if (!high && !huge) continue;

                rows.Add(new Row
                {
                    Renderer = r,
                    Bounds = b,
                    Biggest = biggest,
                    High = high,
                    Huge = huge,
                });
            }

            report.AppendLine($"  scene vertical extent: minY={sceneMinY:0.00} m  maxY={sceneMaxY:0.00} m");
            report.AppendLine();

            if (rows.Count == 0)
            {
                report.AppendLine("  NO renderer trips either net — nothing in this scene is in the sky");
                report.AppendLine("  and nothing is oversized. If the owner sees a sky mass here, it is");
                report.AppendLine("  NOT authored in this scene: look at runtime spawners or a second scene.");
                report.AppendLine("=============================================================");
                return 0;
            }

            // Biggest offender first — the sky mass spans the frame, so it sorts to the top.
            foreach (var row in rows.OrderByDescending(x => x.Biggest).ThenByDescending(x => x.Bounds.min.y))
            {
                var r = row.Renderer;
                var b = row.Bounds;
                var go = r.gameObject;
                string flags = (row.High ? "HIGH" : "----") + "/" + (row.Huge ? "HUGE" : "----");

                report.AppendLine($"  [{flags}] '{go.name}'  ({r.GetType().Name})");
                report.AppendLine($"      path        : {HierarchyPath(go.transform)}");
                report.AppendLine($"      root        : {go.transform.root.name}");
                report.AppendLine($"      scene       : {go.scene.name}");
                report.AppendLine($"      activeInHier: {go.activeInHierarchy}  activeSelf={go.activeSelf}  " +
                                  $"rendererEnabled={r.enabled}  layer={LayerMask.LayerToName(go.layer)}");
                report.AppendLine($"      worldPos    : {Fmt(go.transform.position)}  lossyScale={Fmt(go.transform.lossyScale)}");
                report.AppendLine($"      bounds ctr  : {Fmt(b.center)}");
                report.AppendLine($"      bounds size : {Fmt(b.size)}");
                report.AppendLine($"      bounds y    : min={b.min.y:0.00}  max={b.max.y:0.00}");
                report.AppendLine($"      material    : {DescribeMaterial(r)}");
                report.AppendLine();
            }

            report.AppendLine($"  {rows.Count} flagged renderer(s) in '{label}'");
            report.AppendLine("=============================================================");
            return rows.Count;
        }

        // ---------------------------------------------------------------------

        private struct Row
        {
            public Renderer Renderer;
            public Bounds Bounds;
            public float Biggest;
            public bool High;
            public bool Huge;
        }

        private static string DescribeLoadedScenes()
        {
            var sb = new StringBuilder();
            int n = SceneManager.sceneCount;
            for (int i = 0; i < n; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (i > 0) sb.Append(", ");
                sb.Append($"'{s.name}'(loaded={s.isLoaded},roots={(s.isLoaded ? s.rootCount : 0)})");
            }
            return sb.Length == 0 ? "<none>" : sb.ToString();
        }

        private static string DescribeMaterial(Renderer r)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return "<none>";
            var m = mats[0];
            if (m == null) return "<NULL material>";
            string sh = m.shader != null ? m.shader.name : "<null shader>";
            return $"'{m.name}' shader='{sh}'" + (mats.Length > 1 ? $" (+{mats.Length - 1} more)" : "");
        }

        private static string SafeName(Renderer r)
        {
            try { return r != null && r.gameObject != null ? r.gameObject.name : "<null>"; }
            catch { return "<unreadable>"; }
        }

        private static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

        private static string HierarchyPath(Transform t)
        {
            if (t == null) return "<null>";
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }
    }
}
