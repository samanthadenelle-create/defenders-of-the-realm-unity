// =============================================================================
// PlacementGrid — the shared cell-occupancy grid for Build Mode (WO-108 P1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Promotes TowerPlacementSystem's radius snap into a real 2D cell grid with
// EXACT multi-cell footprint occupancy (build-mode-architecture.md §3). 3 m cells
// match the polyperfect 3×3 modular wall grain; the interior is 28×22 cells
// (84×66 m) centred on the village origin (0,0,0 = Heart of Elarion).
//
// HEADLESS-PURE seam (principle #3): CanPlace / WorldToCell / CellToWorld are
// pure functions over the grid state + config — no scene raycast, no Camera — so
// an async-raid server can re-verify a layout headless. The Y of a placed object
// comes from the placement raycast (TowerPlacementSystem.IsValidSurface), not the
// grid, so the grid stays a flat XZ planner.
//
// Singleton: BuildModeController ensures one exists on Enter.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// A 2D occupancy grid over the village interior. Tracks which cells are taken,
    /// converts world ⇄ cell, snaps a world point to a cell centre, and validates a
    /// multi-cell footprint. Pure/headless apart from the optional overlay mesh.
    /// </summary>
    public sealed class PlacementGrid : MonoBehaviour
    {
        public static PlacementGrid Instance { get; private set; }

        [Header("Grid")]
        [Tooltip("Cell edge in metres — 3 m matches polyperfect 3×3 modular walls.")]
        public float cellSize = 3f;

        // FOOTPRINT EXPANDED (owner F8 2026-06-28 "expand the build footprint enough to get
        // north and south towers"): the castle perimeter is ~square — the S/N walls sit at
        // z≈±40.5 (castle-south-recipe.json: gate z=-40.6, walls -40.55/-40.93, corner
        // tower -40.03) and the E/W walls at x≈±42. The old grid was 84m×66m (±42 X, ±33 Z),
        // so the N/S walls fell ~7.5m OUTSIDE the buildable Z range → "can't place towers"
        // there, while E/W (at the ±42 X edge) worked. Now a symmetric 90m×90m grid (±45 on
        // BOTH axes) reaches every wall AND the ±42.33 corner towers. Placement is still gated
        // by surface/occupancy/gate-lane checks, so a bigger grid doesn't allow silly builds.
        [Tooltip("Cells across X — 90 m / 3 m = 30 (±45, reaches the E/W walls + corner towers).")]
        public int gridWidth = 30;

        [Tooltip("Cells across Z — 90 m / 3 m = 30 (±45, reaches the N/S walls at ±40.5 + corner towers).")]
        public int gridHeight = 30;

        [Tooltip("World-space XZ of the grid's (0,0) cell-corner. Default centres the grid on the origin.")]
        public Vector3 origin = Vector3.zero;

        // EDGE-ALLOW (owner hard requirement) — how many cells in from the grid border are
        // BLOCKED for placement. 0 = the whole grid is buildable INCLUDING the boundary cells,
        // so a perimeter wall run reaches the map edge. Was implicitly clamped to the interior
        // before; keep this at 0 unless the owner asks for an inset. A negative value is treated
        // as 0 (never inset).
        [Tooltip("Cells of border to block from placement. 0 = edge-allow (perimeter walls reach the boundary).")]
        public int edgeMargin = 0;

        // occupied[x,z] → the structure id occupying that cell, or null if free.
        private string[,] _occupied;

        // Overlay (build-mode only) — a single quad ground-decal toggled on Enter.
        private GameObject _overlay;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            EnsureGrid();
            // Centre the grid on the configured origin if it was left at default.
            if (origin == Vector3.zero)
                origin = new Vector3(-gridWidth * cellSize * 0.5f, 0f, -gridHeight * cellSize * 0.5f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureGrid()
        {
            if (_occupied == null ||
                _occupied.GetLength(0) != gridWidth ||
                _occupied.GetLength(1) != gridHeight)
            {
                _occupied = new string[Mathf.Max(1, gridWidth), Mathf.Max(1, gridHeight)];
            }
        }

        // ── Conversions (pure) ────────────────────────────────────────────────

        /// <summary>World XZ → the cell it falls in (clamped reads still return the raw cell).</summary>
        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
            int z = Mathf.FloorToInt((worldPos.z - origin.z) / cellSize);
            return new Vector2Int(x, z);
        }

        /// <summary>Cell → the world-space centre of that cell (Y from the grid plane).</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            float x = origin.x + (cell.x + 0.5f) * cellSize;
            float z = origin.z + (cell.y + 0.5f) * cellSize;
            return new Vector3(x, origin.y, z);
        }

        /// <summary>Snap a world point to its cell centre, preserving the incoming Y.</summary>
        public Vector3 SnapToGrid(Vector3 worldPos)
        {
            var cell = WorldToCell(worldPos);
            var snapped = CellToWorld(cell);
            snapped.y = worldPos.y;   // keep the surface height from the placement ray
            return snapped;
        }

        // ── Occupancy (pure) ──────────────────────────────────────────────────

        /// <summary>True when every cell of <paramref name="footprint"/> at
        /// <paramref name="cell"/> is in-bounds and currently free.</summary>
        public bool CanPlace(Vector2Int cell, Vector2Int footprint)
        {
            EnsureGrid();
            int fw = Mathf.Max(1, footprint.x);
            int fh = Mathf.Max(1, footprint.y);
            // EDGE-ALLOW — the buildable bounds. margin 0 = the full grid (incl. boundary
            // cells) is placeable so perimeter walls reach the map edge.
            int m = Mathf.Max(0, edgeMargin);
            int minX = m, minZ = m, maxX = gridWidth - m, maxZ = gridHeight - m;
            for (int dx = 0; dx < fw; dx++)
            {
                for (int dz = 0; dz < fh; dz++)
                {
                    int x = cell.x + dx, z = cell.y + dz;
                    if (x < minX || z < minZ || x >= maxX || z >= maxZ) return false;
                    if (!string.IsNullOrEmpty(_occupied[x, z])) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// True when every cell of <paramref name="footprint"/> at <paramref name="cell"/>
        /// is within the buildable bounds (ignores occupancy). WO-394 — lets the placement
        /// gate tell "outside the build area" apart from "no space here" (cells taken).
        /// </summary>
        public bool InBounds(Vector2Int cell, Vector2Int footprint)
        {
            int fw = Mathf.Max(1, footprint.x);
            int fh = Mathf.Max(1, footprint.y);
            int m = Mathf.Max(0, edgeMargin);
            int minX = m, minZ = m, maxX = gridWidth - m, maxZ = gridHeight - m;
            for (int dx = 0; dx < fw; dx++)
                for (int dz = 0; dz < fh; dz++)
                {
                    int x = cell.x + dx, z = cell.y + dz;
                    if (x < minX || z < minZ || x >= maxX || z >= maxZ) return false;
                }
            return true;
        }

        /// <summary>Mark a footprint's cells occupied by <paramref name="structureId"/>.</summary>
        public void Occupy(Vector2Int cell, Vector2Int footprint, string structureId)
        {
            FlowTrace.Step("Grid", $"Occupy cell=({cell.x},{cell.y}) footprint=({footprint.x}x{footprint.y}) id='{structureId ?? "<null>"}'");
            EnsureGrid();
            int fw = Mathf.Max(1, footprint.x);
            int fh = Mathf.Max(1, footprint.y);
            for (int dx = 0; dx < fw; dx++)
            {
                for (int dz = 0; dz < fh; dz++)
                {
                    int x = cell.x + dx, z = cell.y + dz;
                    if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight) continue;
                    _occupied[x, z] = structureId;
                }
            }
        }

        /// <summary>Clear a footprint's cells (sell / move).</summary>
        public void Free(Vector2Int cell, Vector2Int footprint)
        {
            EnsureGrid();
            int fw = Mathf.Max(1, footprint.x);
            int fh = Mathf.Max(1, footprint.y);
            for (int dx = 0; dx < fw; dx++)
            {
                for (int dz = 0; dz < fh; dz++)
                {
                    int x = cell.x + dx, z = cell.y + dz;
                    if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight) continue;
                    _occupied[x, z] = null;
                }
            }
        }

        /// <summary>Wipe all occupancy (re-seeding the grid from a fresh layout).</summary>
        public void ClearAll()
        {
            FlowTrace.Step("Grid", $"ClearAll — wiping {gridWidth}x{gridHeight} occupancy");
            _occupied = new string[Mathf.Max(1, gridWidth), Mathf.Max(1, gridHeight)];
        }

        /// <summary>Convert a metric footprint radius (CatalogEntry placement) to whole cells.</summary>
        public Vector2Int FootprintCells(float footprintMetres)
        {
            int cells = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, footprintMetres) / cellSize));
            return new Vector2Int(cells, cells);
        }

        // ── Overlay (build-mode only) ──────────────────────────────────────────

        /// <summary>Show/hide a translucent grid overlay over the build area.</summary>
        public void SetGridVisible(bool visible)
        {
            if (visible)
            {
                if (_overlay == null) _overlay = BuildOverlay();
                if (_overlay != null) _overlay.SetActive(true);
            }
            else if (_overlay != null)
            {
                _overlay.SetActive(false);
            }
        }

        private GameObject BuildOverlay()
        {
            // EDGES ONLY (owner: "no fill, just edges") — a boundary outline of the buildable area,
            // NOT a solid fill, so the ground + the placement ghost stay visible underneath.
            var go = new GameObject("PlacementGridOverlay");
            go.transform.SetParent(transform, false);

            float y  = origin.y + 0.05f;
            float x0 = origin.x,                         x1 = origin.x + gridWidth  * cellSize;
            float z0 = origin.z,                         z1 = origin.z + gridHeight * cellSize;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace    = true;
            lr.loop             = true;
            lr.positionCount    = 4;
            lr.SetPositions(new[]
            {
                new Vector3(x0, y, z0), new Vector3(x1, y, z0),
                new Vector3(x1, y, z1), new Vector3(x0, y, z1),
            });
            lr.widthMultiplier   = 0.35f;
            lr.numCornerVertices = 2;
            lr.alignment         = LineAlignment.TransformZ;   // ribbon lies flat on the ground
            lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            lr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows     = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.25f, 0.7f, 1f, 0.9f));
            lr.sharedMaterial = mat;
            return go;
        }
    }
}
