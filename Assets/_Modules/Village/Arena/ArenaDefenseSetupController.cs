// =============================================================================
// ArenaDefenseSetupController — the Arena Defense SETUP / placement loop (WO-389 P2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// The defense-layer twin of BuildModeController, kept SLIM: it does NOT thread a
// "mode" flag through the 1581-line BuildModeController (reuse-map option 2 — a
// slim sibling). It REUSES the proven placement PRIMITIVES verbatim:
//   • PlacementGrid  — cell snap / occupancy / world⇄cell (same grid the base build uses)
//   • GhostPreview   — the translucent cursor ghost (placeholder body, SetPlaceholder)
//   • IBuildInput    — the place/cancel/rotate input seam (DesktopBuildInput default)
//   • ActiveScreenCamera()-style ray — cursor → ground for the snap point
//
// It DIFFERS from BuildModeController in exactly three places:
//   1. Catalog   = ArenaDefenseCatalog (arm an ArenaDefenseDef), NOT a CatalogEntry.
//   2. Budget    = the POINT POOL (DefensePointPool = 50 / SpentPoints / CanAfford),
//                  NOT EconomyService resources. The single affordability check =
//                  "sum of placed PointCost + this def's cost <= 50".
//   3. Commit    = append a PlacedDefenderData(defId, cellX, cellZ, yawSteps) to
//                  GameState.ArenaDefense (via GameStateService.State), NOT BaseLayout.
//
// This is the SETUP screen ONLY — place troops, spend points, save into
// GameState.ArenaDefense. The raid-time friendly spawn lives in EnemyOutpost
// (a separate WO-389 piece); this controller never spawns a combat unit.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Drives the Arena Defense setup session: enter/exit, an armed-def ghost-follow
    /// place loop, point-pool affordability, and a commit into
    /// <see cref="GameState.ArenaDefense"/>. Singleton; reuses PlacementGrid +
    /// GhostPreview rather than forking a parallel placement system.
    /// </summary>
    public sealed class ArenaDefenseSetupController : MonoBehaviour
    {
        public static ArenaDefenseSetupController Instance { get; private set; }

        /// <summary>Broadcast on enter (true) / exit (false) — mirror BuildModeController
        /// so a HUD bridge can hide overlapping combat UI during setup.</summary>
        public static event System.Action<bool> SetupModeChanged;

        /// <summary>True while a setup session is active.</summary>
        public bool IsActive { get; private set; }

        [Header("Camera overview")]
        [SerializeField] private float _setupHeight = 22f;
        [SerializeField] private float _setupPitch = 45f;

        [Header("Placement")]
        [SerializeField] private float _rayDistance = 800f;
        [SerializeField] private LayerMask _groundMask = ~0;

        private PlacementGrid _grid;
        private GhostPreview _ghost;
        private ArenaDefensePaletteUI _palette;

        private Camera _camera;
        private ArenaDefenseDef _armed;
        private int _armedYawSteps;

        // The input seam — reuse the Build Mode source (desktop default; a touch device
        // could swap in the Lean driver the same way, left for parity later).
        private IBuildInput _input = new DesktopBuildInput();

        // Camera restore state.
        private Vector3 _savedCamPos;
        private Quaternion _savedCamRot;
        private readonly List<MonoBehaviour> _disabledCamDrivers = new List<MonoBehaviour>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            // Defensive HUD restore: if this controller is torn down (scene change,
            // destroyed object, exception) while a setup session was active, Exit() may
            // never have run — so the base HUD would stay alpha=0 / non-interactive
            // (HUD vanishes, BAG dead, controls frozen). Re-show it from EVERY teardown
            // path. Idempotent: a second SetVisible(true) after a clean Exit is harmless.
            if (IsActive) ArenaHudBridge.SetVisible(true);
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            // Mirror OnDestroy's defensive restore for the disable path (the GameObject
            // being deactivated counts as "left the screen by another path").
            if (IsActive) ArenaHudBridge.SetVisible(true);
        }

        /// <summary>Ensure a controller exists (entry point for a HUD "Arena Defense" button).</summary>
        public static ArenaDefenseSetupController EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ArenaDefenseSetupController");
            return go.AddComponent<ArenaDefenseSetupController>();
        }

        /// <summary>Toggle the setup session.</summary>
        public void Toggle()
        {
            if (IsActive) Exit();
            else Enter();
        }

        // =====================================================================
        //  Enter / Exit
        // =====================================================================

        /// <summary>
        /// Enter Arena Defense Setup: ensure the grid + the live ArenaDefense list,
        /// pull the camera to the overview, show the grid + the point-pool palette.
        /// </summary>
        public void Enter()
        {
            if (IsActive) return;
            IsActive = true;

            EnsureGrid();
            EnsureLayoutList();

            PullCameraBack();
            _grid.SetGridVisible(true);

            if (_ghost == null)
                _ghost = new GameObject("ArenaDefenseGhost").AddComponent<GhostPreview>();

            EnsurePalette();
            ShowPalette();

            // Suppress the whole gameplay HUD so the live town/wave readout doesn't
            // clutter the placement view. The CASTLE stays visible (we do NOT cover the
            // world here — troops are placed onto it); only the HUD is hidden.
            ArenaHudBridge.SetVisible(false);

            SetupModeChanged?.Invoke(true);
            Debug.Log("[ArenaDefense] Entered Arena Defense Setup.");
        }

        /// <summary>
        /// Exit Arena Defense Setup: persist GameState.ArenaDefense via Save(), hide UI,
        /// restore the camera.
        /// </summary>
        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;

            CancelArmed();
            _palette?.Hide();
            _grid?.SetGridVisible(false);

            // The ArenaDefense list is mutated live as troops are placed; persist it now.
            GameStateService.Instance?.Save();

            RestoreCamera();
            // Restore the gameplay HUD suppressed on Enter.
            ArenaHudBridge.SetVisible(true);
            SetupModeChanged?.Invoke(false);
            Debug.Log("[ArenaDefense] Exited Arena Defense Setup — defense saved.");
        }

        // =====================================================================
        //  Placement loop
        // =====================================================================

        private void Update()
        {
            if (!IsActive) return;
            if (_camera == null) { _camera = ActiveScreenCamera(); if (_camera == null) return; }
            if (_armed == null) return;   // idle — nothing armed, no place loop
            UpdatePlaceLoop();
        }

        /// <summary>
        /// The armed-def ghost-follow place loop. Cancel disarms; Rotate yaws the ghost
        /// 90 deg (free); a valid tap commits a PlacedDefenderData and STAYS ARMED so the
        /// player can lay several of the same troop (CoC-style). Each commit re-checks the
        /// point pool inside Place().
        /// </summary>
        private void UpdatePlaceLoop()
        {
            if (_ghost == null) return;

            if (_input.Cancel) { CancelArmed(); return; }
            if (_input.Rotate) _armedYawSteps = (_armedYawSteps + 1) & 3;

            if (!RaycastGround(out RaycastHit hit))
            {
                _ghost.Hide();
                return;
            }

            Vector3 snapped = _grid.SnapToGrid(hit.point);
            _ghost.MoveTo(snapped, _armedYawSteps);

            bool valid = IsValidPlacement(hit, snapped, _armed, out Vector2Int cell);
            _ghost.SetValid(valid);

            if (PlaceConfirmedThisFrame() && valid)
                Place(cell);
        }

        /// <summary>
        /// Cursor → ground raycast, mirroring BuildModeController.RaycastGround: the
        /// configured mask first, then a fall-back against all layers so the ghost always
        /// tracks real ground even when the scene-baked mask excludes the ground layer.
        /// </summary>
        private bool RaycastGround(out RaycastHit hit)
        {
            Ray ray = _camera.ScreenPointToRay(_input.ScreenPoint);
            if (Physics.Raycast(ray, out hit, _rayDistance, _groundMask)) return true;
            return Physics.Raycast(ray, out hit, _rayDistance, ~0);
        }

        /// <summary>
        /// Read this frame's place confirm, suppressed inside the move-stick zone (so a
        /// thumb-drag that starts on the locomotion stick can't drop a troop) — mirrors
        /// BuildModeController.PlaceConfirmedThisFrame. PlaceOrSelect is a single-frame
        /// latch, so it is consumed (read once) even when suppressed.
        /// </summary>
        private bool PlaceConfirmedThisFrame()
        {
            bool confirmed = _input.PlaceOrSelect;   // consume the latch
            if (!confirmed) return false;
            if (VirtualJoystick.IsInZone(_input.ScreenPoint)) return false;
            return true;
        }

        /// <summary>
        /// Validity for an Arena defender placement: a flat upward surface, a free single
        /// cell in-bounds, and AFFORDABLE against the POINT POOL. The single affordability
        /// check (the BuildModeController step-5 analogue) is the point-pool rule, NOT an
        /// EconomyService resource cost: sum of placed PointCost + this def's cost <= 50.
        /// Defenders occupy ONE cell (units are small; structures snap to a cell), so the
        /// footprint is 1x1.
        /// </summary>
        private bool IsValidPlacement(RaycastHit hit, Vector3 snapped, ArenaDefenseDef def, out Vector2Int cell)
        {
            cell = _grid.WorldToCell(snapped);
            var footprint = Vector2Int.one;   // defenders are single-cell

            // 1. Flat, upward-facing surface (same rule as the base-build placement).
            if (hit.collider == null) return false;
            if (hit.normal.y < 0.85f) return false;

            // 2. The cell is free + in-bounds.
            if (!_grid.CanPlace(cell, footprint)) return false;

            // 3. Affordable from the POINT POOL (the only differing gate vs base build).
            if (def == null) return false;
            if (!ArenaDefenseCatalog.CanAfford(CurrentLayout, def.Id)) return false;

            return true;
        }

        /// <summary>
        /// Commit a defender placement: re-check the point pool, occupy the cell, and
        /// append a PlacedDefenderData(defId, cellX, cellZ, yawSteps) to
        /// GameState.ArenaDefense. STAYS ARMED so the player can lay several (CoC). The
        /// palette re-renders against the new remaining-points total.
        /// </summary>
        private void Place(Vector2Int cell)
        {
            if (_armed == null) return;

            // Re-check affordability at commit (defensive — the layout may have changed
            // since the ghost frame). The point pool is the single budget gate.
            if (!ArenaDefenseCatalog.CanAfford(CurrentLayout, _armed.Id))
            {
                Debug.Log($"[ArenaDefense] Not enough points to place '{_armed.Id}' — placement aborted.");
                return;
            }

            var layout = CurrentLayout;
            if (layout == null) return;

            // Occupy the cell so two defenders can't share it (reuses the SAME grid the
            // base build uses — the defender's own id marks the cell).
            _grid.Occupy(cell, Vector2Int.one, _armed.Id);

            layout.Add(new PlacedDefenderData(_armed.Id, cell.x, cell.y, _armedYawSteps));

            Debug.Log($"[ArenaDefense] Placed '{_armed.Id}' at cell ({cell.x},{cell.y}) yaw {_armedYawSteps * 90}deg — " +
                      $"{ArenaDefenseCatalog.RemainingPoints(layout)} pts left.");

            RefreshPalette();
        }

        // =====================================================================
        //  Arming
        // =====================================================================

        private void Arm(ArenaDefenseDef def)
        {
            _armed = def;
            _armedYawSteps = 0;
            if (_ghost == null) _ghost = new GameObject("ArenaDefenseGhost").AddComponent<GhostPreview>();
            // Reuse the ghost PRIMITIVE with a placeholder body — full troop-body skinning
            // happens at raid spawn (EnemyOutpost), not at setup time.
            _ghost.SetPlaceholder(def != null ? def.Id : "<none>");
        }

        private void CancelArmed()
        {
            _armed = null;
            _ghost?.Hide();
        }

        // =====================================================================
        //  Live layout + budget
        // =====================================================================

        /// <summary>The live GameState.ArenaDefense list (the placed-defense layout).</summary>
        private static List<PlacedDefenderData> CurrentLayout
        {
            get
            {
                var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
                return state != null ? state.ArenaDefense : null;
            }
        }

        /// <summary>Ensure GameState.ArenaDefense exists so Place() can append.</summary>
        private static void EnsureLayoutList()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return;
            if (state.ArenaDefense == null) state.ArenaDefense = new List<PlacedDefenderData>();
        }

        // =====================================================================
        //  Palette wiring
        // =====================================================================

        private void EnsurePalette()
        {
            if (_palette != null) return;
            var go = new GameObject("ArenaDefensePaletteUI");
            _palette = go.AddComponent<ArenaDefensePaletteUI>();
            _palette.OnDefSelected += Arm;
            _palette.OnExitRequested += Exit;
        }

        private void ShowPalette()
        {
            var layout = CurrentLayout;
            int spent = ArenaDefenseCatalog.SpentPoints(layout);
            int remaining = ArenaDefenseCatalog.RemainingPoints(layout);
            _palette?.Show(spent, remaining);
        }

        private void RefreshPalette()
        {
            var layout = CurrentLayout;
            int spent = ArenaDefenseCatalog.SpentPoints(layout);
            int remaining = ArenaDefenseCatalog.RemainingPoints(layout);
            _palette?.Render(spent, remaining);
        }

        // =====================================================================
        //  Camera overview (slim version of BuildModeController's PullCameraBack)
        // =====================================================================

        private void PullCameraBack()
        {
            _camera = ActiveScreenCamera();
            if (_camera == null) return;

            _savedCamPos = _camera.transform.position;
            _savedCamRot = _camera.transform.rotation;

            // Disable camera-driver behaviours so they don't fight the overview.
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

            Vector3 centre = _grid != null
                ? _grid.CellToWorld(new Vector2Int(_grid.gridWidth / 2, _grid.gridHeight / 2))
                : Vector3.zero;
            _camera.transform.position = new Vector3(centre.x, _setupHeight, centre.z - _setupHeight);
            _camera.transform.rotation = Quaternion.Euler(_setupPitch, 0f, 0f);
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

        /// <summary>
        /// The camera the player actually sees: the enabled, screen-bound (no
        /// targetTexture) camera with the highest depth. Same rule BuildModeController
        /// uses; falls back to Camera.main.
        /// </summary>
        private static Camera ActiveScreenCamera()
        {
            Camera best = null;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || !c.enabled) continue;
                if (c.targetTexture != null) continue;
                if (best == null || c.depth > best.depth) best = c;
            }
            return best != null ? best : Camera.main;
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

        /// <summary>Override the input source (test harness / QA). Null reverts to desktop.</summary>
        public void SetInput(IBuildInput input)
        {
            _input = input ?? new DesktopBuildInput();
        }
    }
}
