// BridgeDeckMeasure — batchmode diag (2026-07-03, owner: "there should be a collider and
// I walk ON TOP of the bridge"): measure the REAL walkway surface height of the bridge
// FBX from its mesh triangles, so the runtime deck collider (CastleMoatBuilder's analytic
// slab) can be proven to coincide with the stone the player SEES — not just weld the nav.
//
// Why editor-side: the FBX ships non-readable, so the mesh is only inspectable here.
// Method: load Resources/Bridges/Bridge_Medieval_Stone, walk every triangle, keep those
// facing UP (normal.y > 0.7 in local space) whose centroid lies within the central walk
// corridor (|x| < 40% of width — excludes parapet tops), histogram their heights, and
// report the dominant band = the walkway surface. Prints local heights plus where that
// surface lands under the live offsets.json pose (pos/rot/scale from bridge_south).
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace DeNelle.Editor
{
    public static class BridgeDeckMeasure
    {
        public static void Run()
        {
            var prefab = Resources.Load<GameObject>("Bridges/Bridge_Medieval_Stone");
            if (prefab == null) { Debug.LogError("[BridgeDeckMeasure] prefab not found"); return; }

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Debug.Log($"[BridgeDeckMeasure] prefab '{prefab.name}': {filters.Length} MeshFilter(s)");

            foreach (var mf in filters)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                Bounds b = mesh.bounds;
                Debug.Log($"[BridgeDeckMeasure] mesh '{mesh.name}': {v.Length} verts, local bounds " +
                          $"min={b.min} max={b.max} size={b.size}");

                // Upward-facing triangles in the central corridor, histogram by height (0.1 bins).
                var hist = new Dictionary<int, (int count, float area)>();
                float corridorHalfX = b.extents.x * 0.4f;
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], c = v[t[i + 1]], d = v[t[i + 2]];
                    Vector3 n = Vector3.Cross(c - a, d - a);
                    float nMag = n.magnitude;
                    if (nMag < 1e-8f) continue;
                    if (n.y / nMag < 0.7f) continue;                         // not up-facing
                    Vector3 centroid = (a + c + d) / 3f;
                    if (Mathf.Abs(centroid.x - b.center.x) > corridorHalfX) continue; // parapet strip
                    int bin = Mathf.RoundToInt(centroid.y * 10f);
                    float area = nMag * 0.5f;
                    hist.TryGetValue(bin, out var e);
                    hist[bin] = (e.count + 1, e.area + area);
                }

                // Report the top bands by area — the walkway is the dominant one.
                var bins = new List<KeyValuePair<int, (int count, float area)>>(hist);
                bins.Sort((x, y) => y.Value.area.CompareTo(x.Value.area));
                int show = Mathf.Min(6, bins.Count);
                for (int i = 0; i < show; i++)
                    Debug.Log($"[BridgeDeckMeasure]   up-face band local y={bins[i].Key / 10f:F1}: " +
                              $"{bins[i].Value.count} tris, area {bins[i].Value.area:F1}");

                if (bins.Count > 0)
                {
                    float deckLocalY = bins[0].Key / 10f;
                    Debug.Log($"[BridgeDeckMeasure] DOMINANT WALKWAY SURFACE local y = {deckLocalY:F2} " +
                              $"(vs mesh local top {b.max.y:F2}, bottom {b.min.y:F2})");
                }
            }

            // Where does that land in the WORLD under the live south pose? Print the pose inputs
            // so the comparison against the analytic slab (castle end y=liftY) is mechanical.
            var offsets = Resources.Load<TextAsset>("OffsetForge/offsets");
            Debug.Log("[BridgeDeckMeasure] offsets.json present in Resources/OffsetForge: " + (offsets != null));
            Debug.Log("[BridgeDeckMeasure] DONE — compare dominant band (scaled by bridge_south scaleY, " +
                      "posed by its pos/rot) against the analytic slab plane (castle end y=liftY=3).");
        }
    }
}
