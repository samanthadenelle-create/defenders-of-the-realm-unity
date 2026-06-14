// =============================================================================
// PerimeterWallGenerator — the owner's 2026-06-13 CoC perimeter algorithm:
//   "add tower, rotate 90 repeat for all four sides, then take the distance
//    between the ends and fill with wall segments; width mod 2 -> force ODD so
//    the single CENTER slot is a natural GATE."
//
// SIZE is a BaseSize enum (Small/Medium/Large), NOT a hardcoded meters value
// (owner 2026-06-13): the enum drives the SLOT COUNT per side; the perimeter
// extent + corner positions are DERIVED from it (span = slots * segWidth). Δ is
// ±2 slots — one removed/added on EACH side of the gate — so the count stays ODD
// and the gate stays perfectly centered at every size. Medium is the default;
// Small = Medium-2, Large = Medium+2.
//
// Extends GridWallBuilder (the proven square-fill precedent): same box-fit /
// upright (-90X) / ground-seat / WallSegment+tier place logic, but adds 4 corner
// TOWERS via the CastleSideMirror 90/180/270-around-origin pattern, spans each
// side between the corner-tower ENDS, and leaves the single center slot OPEN as
// the natural gate.
//
// Tier-driven: Tier selects the wall art via WallTierData.Get(Tier).
// SegmentPrefabPath — so this IS "wood/iron/steel replaces the outer wall."
// Builds into the OPEN scene under its own root (idempotent: re-run rebuilds,
// delete the root to remove). Working castle walls/seam untouched (separate root).
//
// Batchmode: DeNelle.Editor.PerimeterWallGenerator.BuildInOpenScene
// Menu:      Defenders/Walls/Build Perimeter (towers + natural gate)
//
// FOLLOW-UPS (out of scope, noted): functional Gate object + NavMesh opening at
// the gate slot (reuse CastleHubBuilder gate/nav helpers); source Tier + Size
// from scene-configs.json (wallTier/baseRadius/towers[]); tier-upgrade art swap
// in VillageController.SetWallTier. See WO-454.
// =============================================================================
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeNelle.Village;          // WallSegment
using DeNelle.Village.Walls;    // WallTier, WallTierData

namespace DeNelle.Editor
{
    public static class PerimeterWallGenerator
    {
        /// <summary>Base footprint. Drives slots/side; extent is derived. Δ±2 keeps it odd.</summary>
        public enum BaseSize { Small, Medium, Large }

        // ── Tunables (owner-editable; later sourced from scene-configs.json) ──
        // Which tier art fills the ring (1 Wood / 2 Iron / 3 ReinforcedSteel).
        private const WallTier Tier = WallTier.Wood;
        // Map size. Medium is the default; Small/Large step the slot count by ±2.
        private const BaseSize Size = BaseSize.Medium;
        // Medium slots per side (ODD — the gate is the centre slot). Small = -2, Large = +2.
        private const int MediumSlotsPerSide = 13;   // 13 * 1.5m = 19.5m wall run/side
        // Corner tower art (Resources path). Falls back gracefully if missing.
        private const string CornerTowerPath = "Structures/Tower_Medieval_Wood";

        // One wall slot = the owner's box; X = the tiling unit (segment width).
        private static readonly Vector3 SegSize = new Vector3(1.5f, 3.0f, 1.5f);
        private const string RootName = "Perimeter_Square";

        /// <summary>Slots/side for a size — Medium ± 2, clamped odd and ≥3.</summary>
        private static int SlotsForSize(BaseSize size)
        {
            int n = MediumSlotsPerSide + (size == BaseSize.Small ? -2 : size == BaseSize.Large ? +2 : 0);
            if ((n & 1) == 0) n++;          // safety: a wrong Medium const can't break centering
            return Mathf.Max(3, n);
        }

        private const string CastleScene = "Assets/Scenes/MainCastle_Hall.unity";

        [MenuItem("Defenders/Walls/Build Perimeter (towers + natural gate)")]
        public static void BuildInOpenScene()
        {
            var root = Build();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[PerimeterWallGenerator] built '{RootName}' in the open scene " +
                      $"(tier {Tier}, size {Size} — Ctrl+S to keep).");
        }

        // Batch: open MainCastle_Hall, build the ring under its own root, save. Reproducible
        // (re-run rebuilds; delete the root to remove). Castle walls/seam untouched.
        public static void BuildInCastle()
        {
            var scene = EditorSceneManager.OpenScene(CastleScene, OpenSceneMode.Single);
            Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PerimeterWallGenerator] built + saved '{RootName}' into {CastleScene} " +
                      $"(tier {Tier}, size {Size}).");
        }

        private static GameObject Build()
        {
            var prior = GameObject.Find(RootName);
            if (prior != null) Object.DestroyImmediate(prior);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            // ── Slot count from the size enum; gate is the centre (odd → centred) ─
            int n = SlotsForSize(Size);
            int gateIndex = (n - 1) / 2;
            float span = n * SegSize.x;                 // wall-run length per side (ends-to-ends)

            // ── Tower footprint → derive the corner offset (extent) ──────────────
            var towerPrefab = Resources.Load<GameObject>(CornerTowerPath);
            float towerHalf = MeasureTowerHalf(towerPrefab);        // half-reach along a side
            // Corner sits beyond the wall run by the tower's half-footprint, so the run
            // fills exactly the gap between the two corner towers' inner ends.
            float halfExtent = span * 0.5f + towerHalf;

            // ── 1+2. Four corner TOWERS via the 90°-around-origin mirror ─────────
            // Author one corner at (H,0,-H); swing 90/180/270 around the Heart (0,0,0).
            int towers = 0;
            var baseCorner = new Vector3(halfExtent, 0f, -halfExtent);
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0f, 90f * s, 0f);
                if (PlaceCornerTower(root.transform, towerPrefab, rot * baseCorner, $"{s}")) towers++;
            }

            // ── 3+4+5. Fill each side; skip the centre slot = natural gate ───────
            int segs = 0;
            for (int s = 0; s < 4; s++)
            {
                var rot = Quaternion.Euler(0f, 90f * s, 0f);
                var midpoint = rot * new Vector3(0f, 0f, -halfExtent);
                var alongDir = rot * Vector3.right;
                for (int i = 0; i < n; i++)
                {
                    if (i == gateIndex) continue;                  // the natural gate opening
                    float along = -span * 0.5f + (i + 0.5f) * SegSize.x;
                    segs += PlaceSegment(root.transform, midpoint + alongDir * along, rot, $"S{s}_{i}");
                }
            }

            Debug.Log($"[PerimeterWallGenerator] '{RootName}': size {Size} → {n} slots/side " +
                      $"({towers} corner towers, {segs} wall segments, tier {Tier}); " +
                      $"span {span:F1}m, extent ±{halfExtent:F1}m, gate slot {gateIndex} OPEN per side.");
            return root;
        }

        // Half the larger horizontal extent of the corner tower prefab (its reach toward
        // the wall run). Measured from a throwaway instance; SegSize.x fallback if unknown.
        private static float MeasureTowerHalf(GameObject prefab)
        {
            if (prefab == null) return SegSize.x;
            var tmp = Object.Instantiate(prefab);
            float half = SegSize.x;
            var rends = tmp.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                half = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            }
            Object.DestroyImmediate(tmp);
            return half;
        }

        // Place a corner tower (seated, facing outward from the Heart). False on missing prefab.
        private static bool PlaceCornerTower(Transform parent, GameObject prefab, Vector3 pos, string name)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[PerimeterWallGenerator] corner tower missing at Resources/{CornerTowerPath} — skipping towers.");
                return false;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            go.name = $"Corner_{name}";
            go.transform.SetParent(parent, false);
            var outward = pos.sqrMagnitude > 0.001f ? pos.normalized : Vector3.forward;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z), Vector3.up);
            go.transform.position = pos;

            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                go.transform.position += new Vector3(0f, -b.min.y, 0f);   // ground-seat
            }
            return true;
        }

        // Place one tier wall segment along a (rotated) side. Box-fit + seat are done
        // while axis-aligned (reliable world-AABB fit), THEN the wrapper is rotated to
        // the side and positioned — Y-rotation preserves the fit/seat.
        private static int PlaceSegment(Transform parent, Vector3 pos, Quaternion sideRot, string name)
        {
            var prefab = Resources.Load<GameObject>(WallTierData.Get(Tier).SegmentPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PerimeterWallGenerator] missing Resources/{WallTierData.Get(Tier).SegmentPrefabPath} (tier {Tier}).");
                return 0;
            }

            var seg = new GameObject($"Wall_{name}");
            seg.transform.SetParent(parent, false);
            seg.transform.localPosition = Vector3.zero;
            seg.transform.localRotation = Quaternion.identity;     // fit while axis-aligned

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            go.transform.SetParent(seg.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // FBX imports flat → stand upright

            float seatY = 0f;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                var sc = seg.transform.localScale;
                if (b.size.x > 0.0001f) sc.x *= SegSize.x / b.size.x;
                if (b.size.y > 0.0001f) sc.y *= SegSize.y / b.size.y;
                if (b.size.z > 0.0001f) sc.z *= SegSize.z / b.size.z;
                seg.transform.localScale = sc;
                var r2 = go.GetComponentsInChildren<Renderer>(true);
                var b2 = r2[0].bounds;
                for (int k = 1; k < r2.Length; k++) b2.Encapsulate(r2[k].bounds);
                seatY = -b2.min.y;                                  // ground-seat offset
            }

            seg.transform.rotation = sideRot;
            seg.transform.position = new Vector3(pos.x, seatY, pos.z);

            var ws = seg.AddComponent<WallSegment>();
            ws.SetTier((int)Tier);
            return 1;
        }
    }
}
