// =============================================================================
// BuildModeController — the Build Mode entry/exit + placement loop (WO-108 P1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The CREATE-verb controller. Enter() freezes the threat (WaveManager), pulls the
// camera to a top-down overview, shows the grid + the code-built palette. Tapping
// a palette card arms a CatalogEntry; a ghost tracks the cursor and tints green/
// red; a valid tap places the structure through the ONE creation path
// (StructureFactory.Create), occupies the grid, charges the persisted crystal
// wallet ONLY AFTER a committed placement (WO-131), and appends to the live
// BaseLayout. Exit() persists BaseLayout via GameStateService.Save(), restores the
// camera, and resumes waves.
//
// P1 = place-only (move / sell / rotate-edit / upgrade are P2, deferred). Rotate
// the ghost before placing is supported (R key / rotate path) since it is free.
//
// LEGACY input (Input.GetMouseButton*) to match TowerPlacementSystem; a Lean.Touch
// driver is the mobile follow-up (P2).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives the Build Mode edit session: enter/exit, the armed-entry ghost loop,
    /// place + cost + persist. Singleton; reuses CatalogRegistry + StructureFactory
    /// + PlacementGrid rather than forking a parallel placement system.
    /// </summary>
    public sealed class BuildModeController : MonoBehaviour
    {
        public static BuildModeController Instance { get; private set; }

        /// <summary>True while a build session is active.</summary>
        public bool IsActive { get; private set; }

        [Header("Camera overview")]
        [Tooltip("Camera height (Y) while in build mode — pulled back top-down.")]
        [SerializeField] private float _buildModeHeight = 55f;
        [Tooltip("Top-down pitch (degrees) while in build mode.")]
        [SerializeField] private float _buildModePitch = 70f;

        [Header("Placement")]
        [SerializeField] private float _rayDistance = 800f;
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("Min clearance (m) a placement must keep from a gate, so the spawn→Heart lane stays open.")]
        [SerializeField] private float _gateClearance = 8f;

        private PlacementGrid _grid;
        private BuildPaletteUI _palette;
        private GhostPreview _ghost;

        private Camera _camera;
        private CatalogEntry _armed;
        private int _armedYawSteps;

        // Camera restore state.
        private Vector3 _savedCamPos;
        private Quaternion _savedCamRot;
        private readonly List<MonoBehaviour> _disabledCamDrivers = new List<MonoBehaviour>();

        // Wave drivers frozen on enter, re-enabled on exit.
        private readonly List<WaveManager> _frozenWaves = new List<WaveManager>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Ensure a controller exists (HUD "Build" button entry point).</summary>
        public static BuildModeController EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BuildModeController");
            return go.AddComponent<BuildModeController>();
        }

        /// <summary>Toggle the build session.</summary>
        public void Toggle()
        {
            if (IsActive) Exit();
            else Enter();
        }

        // =====================================================================
        //  Enter / Exit
        // =====================================================================

        /// <summary>
        /// Enter Build Mode: seed BaseLayout from the default village on first entry,
        /// freeze waves, pull the camera back, show the grid + palette.
        /// </summary>
        public void Enter()
        {
            if (IsActive) return;
            IsActive = true;

            EnsureGrid();
            SeedBaseLayoutIfFirstEntry();

            FreezeWaves();
            PullCameraBack();

            _grid.SetGridVisible(true);

            if (_ghost == null)
                _ghost = new GameObject("GhostPreview").AddComponent<GhostPreview>();

            EnsurePalette();
            _palette.Show();

            Debug.Log("[BuildMode] Entered build mode.");
        }

        /// <summary>
        /// Exit Build Mode: commit BaseLayout to GameState + Save(), hide UI, restore
        /// the camera, resume waves.
        /// </summary>
        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;

            CancelArmed();
            _palette?.Hide();
            _grid?.SetGridVisible(false);

            CommitLayout();

            RestoreCamera();
            ResumeWaves();

            Debug.Log("[BuildMode] Exited build mode — layout saved.");
        }

        // =====================================================================
        //  Placement loop
        // =====================================================================

        private void Update()
        {
            if (!IsActive || _armed == null || _ghost == null) return;
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }

            // Right-click / Escape cancels the armed entry (keeps build mode open).
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelArmed();
                return;
            }

            // R rotates the ghost in 90° steps before placing (free; full edit is P2).
            if (Input.GetKeyDown(KeyCode.R))
                _armedYawSteps = (_armedYawSteps + 1) & 3;

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _rayDistance, _groundMask))
            {
                _ghost.Hide();
                return;
            }

            Vector3 snapped = _grid.SnapToGrid(hit.point);
            _ghost.MoveTo(snapped, _armedYawSteps);

            bool valid = IsValidPlacement(hit, snapped, out Vector2Int cell, out Vector2Int footprint);
            _ghost.SetValid(valid);

            if (valid && Input.GetMouseButtonDown(0))
                Place(cell, footprint, snapped);
        }

        /// <summary>
        /// Combined validity: flat upward surface, footprint cells free + in-bounds,
        /// gate-lane clearance, and affordable. Pure over grid + config apart from
        /// the surface raycast hit (which the caller supplies).
        /// </summary>
        private bool IsValidPlacement(RaycastHit hit, Vector3 snapped, out Vector2Int cell, out Vector2Int footprint)
        {
            cell = _grid.WorldToCell(snapped);
            footprint = _grid.FootprintCells(
                _armed.repo != null && _armed.repo.placement != null ? _armed.repo.placement.footprint : 3f);

            // 1. Flat, upward-facing top (TowerPlacementSystem.IsValidSurface rule).
            if (hit.collider == null) return false;
            if (hit.normal.y < 0.85f) return false;
            if (hit.collider.CompareTag("Tower") || hit.collider.CompareTag("Building")) return false;

            // 2. Footprint cells free + in-bounds.
            if (!_grid.CanPlace(cell, footprint)) return false;

            // 3. Gate-lane clearance — never wall off the spawn→Heart corridor.
            if (_gateClearance > 0f && IsTooCloseToGate(snapped)) return false;

            // 4. Affordable from the persisted wallet (the WO-131 single source).
            int cost = _armed.repo != null ? _armed.repo.buildCost : 0;
            if (CrystalBalance < cost) return false;

            return true;
        }

        /// <summary>True when the point is within the gate-clearance radius of any Gate tagged collider.</summary>
        private bool IsTooCloseToGate(Vector3 worldPos)
        {
            // Gates are tagged "Building" in this project's clearance rule; check by
            // name to avoid coupling to the Gate type. A missing gate set = no rule.
            // FindGameObjectsWithTag throws if the tag is undefined — guard it.
            GameObject[] gates;
            try { gates = GameObject.FindGameObjectsWithTag("Building"); }
            catch (UnityException) { return false; }
            foreach (var g in gates)
            {
                if (g == null) continue;
                if (g.name.IndexOf("gate", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Vector3 gp = g.transform.position; gp.y = worldPos.y;
                if (Vector3.Distance(gp, worldPos) < _gateClearance) return true;
            }
            return false;
        }

        /// <summary>
        /// Commit a placement: build via StructureFactory, occupy the grid, CHARGE the
        /// persisted crystal wallet (only here, after the valid commit — WO-131), add
        /// the PlacedStructure marker, and append to the live BaseLayout. The entry
        /// stays armed so the player can place several in a row (CoC behaviour).
        /// </summary>
        private void Place(Vector2Int cell, Vector2Int footprint, Vector3 snapped)
        {
            var loader = BaseLayoutLoader.EnsureExists();
            var data = new PlacedStructureData(_armed.id, cell.x, cell.y, _armedYawSteps, 1);

            var ps = loader.Spawn(data, _grid);
            if (ps == null)
            {
                Debug.LogWarning($"[BuildMode] Placement of '{_armed.id}' failed to spawn — not charged.");
                return;
            }

            // Charge ONLY AFTER the committed valid placement (WO-131): the persisted
            // GameState crystal wallet, never a session/second balance.
            int cost = _armed.repo != null ? _armed.repo.buildCost : 0;
            if (cost > 0) GameStateService.Instance?.AddCrystals(-cost);

            // Append to the live BaseLayout so Exit() persists it.
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state != null)
            {
                if (state.BaseLayout == null) state.BaseLayout = new List<PlacedStructureData>();
                state.BaseLayout.Add(data);
            }

            Debug.Log($"[BuildMode] Placed '{_armed.id}' at cell ({cell.x},{cell.y}) yaw {_armedYawSteps * 90}°, charged {cost}.");
        }

        // =====================================================================
        //  Arming
        // =====================================================================

        private void Arm(CatalogEntry entry)
        {
            _armed = entry;
            _armedYawSteps = 0;
            if (_ghost == null) _ghost = new GameObject("GhostPreview").AddComponent<GhostPreview>();
            _ghost.SetEntry(entry);
        }

        private void CancelArmed()
        {
            _armed = null;
            _ghost?.Hide();
        }

        // =====================================================================
        //  Seeding — first entry copies the default village into BaseLayout
        // =====================================================================

        /// <summary>
        /// On the very first build-mode entry with an empty BaseLayout, seed it from
        /// the live default-village structures so the player edits their familiar
        /// town (not a blank plot). Seeds from any in-scene PlacedStructure first; if
        /// none exist yet, leaves the layout empty (the default village stays the
        /// seed and the player builds on top). Idempotent: a non-empty layout is left
        /// alone.
        /// </summary>
        private void SeedBaseLayoutIfFirstEntry()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return;
            if (state.BaseLayout != null && state.BaseLayout.Count > 0) return;
            if (state.BaseLayout == null) state.BaseLayout = new List<PlacedStructureData>();

            // Seed from any structures the loader already placed (a re-entry case).
            var existing = FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None);
            foreach (var ps in existing)
            {
                if (ps == null) continue;
                state.BaseLayout.Add(ps.ToSaveData());
            }
            // If none exist, BaseLayout stays empty — the default VillageSceneBuilder
            // village remains the seed and the player adds to it. (Authoring the full
            // default layout into catalog records is the WO-148/builder follow-up.)
        }

        // =====================================================================
        //  Persist
        // =====================================================================

        private void CommitLayout()
        {
            // BaseLayout is mutated live as structures are placed; persist it now.
            GameStateService.Instance?.Save();
        }

        // =====================================================================
        //  Waves freeze / resume
        // =====================================================================

        private void FreezeWaves()
        {
            _frozenWaves.Clear();
            foreach (var wm in FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
            {
                if (wm == null || !wm.enabled) continue;
                wm.enabled = false;   // stops the wave loop's Update/coroutine progression
                _frozenWaves.Add(wm);
            }
        }

        private void ResumeWaves()
        {
            foreach (var wm in _frozenWaves)
                if (wm != null) wm.enabled = true;
            _frozenWaves.Clear();
        }

        // =====================================================================
        //  Camera overview
        // =====================================================================

        private void PullCameraBack()
        {
            _camera = Camera.main;
            if (_camera == null) return;

            _savedCamPos = _camera.transform.position;
            _savedCamRot = _camera.transform.rotation;

            // Disable any camera-driver behaviours so they don't fight the overview.
            _disabledCamDrivers.Clear();
            foreach (var mb in _camera.GetComponents<MonoBehaviour>())
            {
                if (mb == null || !mb.enabled) continue;
                string n = mb.GetType().Name;
                if (n.IndexOf("Camera", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Cinemachine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Brain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mb.enabled = false;
                    _disabledCamDrivers.Add(mb);
                }
            }

            // Top-down overview centred on the grid.
            Vector3 centre = _grid != null
                ? _grid.CellToWorld(new Vector2Int(_grid.gridWidth / 2, _grid.gridHeight / 2))
                : Vector3.zero;
            _camera.transform.position = new Vector3(centre.x, _buildModeHeight, centre.z - 1f);
            _camera.transform.rotation = Quaternion.Euler(_buildModePitch, 0f, 0f);
        }

        private void RestoreCamera()
        {
            foreach (var mb in _disabledCamDrivers)
                if (mb != null) mb.enabled = true;
            _disabledCamDrivers.Clear();

            if (_camera != null)
            {
                _camera.transform.position = _savedCamPos;
                _camera.transform.rotation = _savedCamRot;
            }
        }

        // =====================================================================
        //  Wiring
        // =====================================================================

        private void EnsureGrid()
        {
            _grid = PlacementGrid.Instance;
            if (_grid == null)
                _grid = new GameObject("PlacementGrid").AddComponent<PlacementGrid>();
        }

        private void EnsurePalette()
        {
            if (_palette != null) return;
            var go = new GameObject("BuildPaletteUI");
            _palette = go.AddComponent<BuildPaletteUI>();
            _palette.OnEntrySelected += Arm;
            _palette.OnExitRequested += Exit;
        }

        /// <summary>The persisted crystal wallet (WO-131 — single source of truth).</summary>
        private static int CrystalBalance
        {
            get
            {
                var svc = GameStateService.Instance;
                return svc != null && svc.State != null ? svc.State.Resources.Crystals : 0;
            }
        }
    }
}
