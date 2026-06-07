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
// INPUT is read through the IBuildInput seam (Build Mode S6), not Input.* directly:
// DesktopBuildInput (mouse/keyboard, unchanged) is the default; on a touch device
// LeanTouchBuildDriver installs itself on Enter() and feeds the same place/move/
// select/rotate/cancel intents from touch gestures + a code-built button bar.
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

        /// <summary>
        /// Broadcast whenever build mode is entered (true) or exited (false). Lets
        /// cross-assembly listeners react without referencing DeNelle.Village types
        /// directly — the BuildModeHudBridge subscribes to this to hide the bottom-middle
        /// combat HUD (which otherwise overlaps the build palette) on Enter and restore it
        /// on Exit. Static so a listener can subscribe before any controller instance
        /// exists (EnsureExists creates it lazily).
        /// </summary>
        public static event System.Action<bool> BuildModeChanged;

        /// <summary>True while a build session is active.</summary>
        public bool IsActive { get; private set; }

        [Header("Camera overview")]
        [Tooltip("Camera height (Y) while in build mode — angled 3D overview so structures read as upright.")]
        [SerializeField] private float _buildModeHeight = 28f;
        [Tooltip("Pitch (degrees) while in build mode — angled, not top-down, so 3D orientation is visible.")]
        [SerializeField] private float _buildModePitch = 45f;

        [Header("Build camera pan / zoom (desktop)")]
        [Tooltip("Pan speed (m/sec) for WASD + edge-scroll.")]
        [SerializeField] private float _camPanSpeed = 24f;
        [Tooltip("Pan speed multiplier while middle/right-dragging (m per screen pixel).")]
        [SerializeField] private float _camDragSpeed = 0.08f;
        [Tooltip("Screen-edge band (px) that triggers edge-scroll pan.")]
        [SerializeField] private float _camEdgeBand = 18f;
        [Tooltip("Zoom (height) step per scroll notch.")]
        [SerializeField] private float _camZoomStep = 4f;
        [Tooltip("Min / max build-camera height (zoom clamp).")]
        [SerializeField] private float _camHeightMin = 14f;
        [SerializeField] private float _camHeightMax = 60f;

        // Live overview state — the focus point the camera looks at (XZ on the grid plane)
        // and the current zoom height. Seeded in PullCameraBack, mutated by the pan/zoom
        // loop, and clamped to the grid (map) bounds so the player can't pan into the void.
        private Vector3 _camFocus;
        private float   _camHeight;
        private bool    _camDragging;
        private Vector2 _camDragLastPoint;

        [Header("Placement")]
        [SerializeField] private float _rayDistance = 800f;
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("Min clearance (m) a placement must keep from the gate LANE, so the spawn→Heart corridor stays open.")]
        [SerializeField] private float _gateClearance = 3f;

        private PlacementGrid _grid;
        private BuildPaletteUI _palette;
        private BuildSelectionUI _selectionUi;
        private GhostPreview _ghost;
        // WO-335 — simple PLAYER yaw-only rotate panel opened on a tower place-confirm.
        // (The old UIToolkit 3-axis TowerPlacementRotateMenu is now the editor/dev OFFSET
        //  tool — no longer called from placement.)
        private RotateModelMenu _rotateMenu;
        // The 3-axis dev/orient editor opened from the palette "Orient" button — while it is
        // live the placement loops are frozen so a tap behind the modal can't drop a piece.
        private TowerPlacementRotateMenu _orientEditor;

        private Camera _camera;
        private CatalogEntry _armed;
        private int _armedYawSteps;
        private float _armedYawOffset; // from preview modal (free or 90deg)

        // Pending place data while modal is open for rotation confirmation.
        private bool _pendingPlace;
        private Vector2Int _pendingCell;
        private Vector2Int _pendingFootprint;
        private Vector3 _pendingSnapped;

        // ── Input seam (Build Mode S6) ────────────────────────────────────────
        // The place/move/select loops poll high-level intents from this source
        // instead of reading Input.* directly. Defaults to the mouse/keyboard impl
        // so desktop is unchanged; on a touch device the Lean.Touch driver installs
        // itself on Enter() and is removed on Exit().
        private IBuildInput _input = new DesktopBuildInput();
        private LeanTouchBuildDriver _touchDriver;

        // ── Selection / edit state (P2) ───────────────────────────────────────
        // The currently tap-selected placed structure (move/sell target).
        private PlacedStructure _selected;
        // True while re-placing _selected (the MOVE ghost loop). During a move the
        // structure's OWN cells are freed so it cannot block itself; on a valid tap
        // it commits to the new cells, on cancel it returns to its origin.
        private bool _movingSelected;
        private Vector2Int _moveOriginCell;   // origin to restore if a move is cancelled

        // Camera restore state.
        private Vector3 _savedCamPos;
        private Quaternion _savedCamRot;
        private readonly List<MonoBehaviour> _disabledCamDrivers = new List<MonoBehaviour>();

        // DEF-117 — every OTHER screen camera we disabled on enter so the overview
        // does not split-screen with a second view (a runtime-spawned embedded FBX
        // camera, etc.). Re-enabled on exit. While SmartMobileCamera (the depth=100
        // renderer) is disabled for the overview, its per-second EnforceSoleCamera
        // rogue-camera cull stops running, so the build controller must cull them.
        private readonly List<Camera> _suppressedCameras = new List<Camera>();

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

            EnsureTouchInput();   // install the Lean.Touch driver on a touch device (S6)

            // Notify cross-assembly listeners (BuildModeHudBridge hides the combat HUD
            // so the bottom-middle bars don't overlap the build palette).
            BuildModeChanged?.Invoke(true);

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
            ClearSelection();
            _palette?.Hide();
            _selectionUi?.Hide();
            _grid?.SetGridVisible(false);

            // Stop the Lean.Touch driver + hide its button bar; revert to the desktop
            // input source so a re-entry on desktop is unaffected (S6).
            if (_touchDriver != null)
            {
                _touchDriver.Uninstall();
                _input = new DesktopBuildInput();
            }

            CommitLayout();

            RestoreCamera();
            ResumeWaves();

            // Restore the combat HUD now that the build palette is gone.
            BuildModeChanged?.Invoke(false);

            Debug.Log("[BuildMode] Exited build mode — layout saved.");
        }

        // =====================================================================
        //  Placement loop
        // =====================================================================

        private void Update()
        {
            if (!IsActive) return;
            // DEF-117 — raycast from the camera that is ACTUALLY on screen (the one
            // we pulled into the overview), never Camera.main: with rogue cameras in
            // play Camera.main can resolve to a non-rendering / wrong camera, so taps
            // would miss the build grid. _camera is set in PullCameraBack().
            if (_camera == null) { _camera = ActiveScreenCamera(); if (_camera == null) return; }

            // Move the overview each frame (WASD / edge-scroll / drag / zoom on desktop;
            // touch pans via the Lean driver). Runs in every mode so the player can re-frame
            // while arming, moving, or idle.
            UpdateBuildCameraPan();

            // While the 3-axis orient editor is open, the placement loops are frozen so a tap
            // behind the modal can't drop a piece (the modal owns its own confirm/cancel).
            if (_orientEditor != null && _orientEditor.isActiveAndEnabled && _orientEditor.IsOpen)
            { _ghost?.Hide(); return; }

            // Three exclusive modes: re-placing a selected structure (MOVE), arming a
            // new one (CREATE), or idle (tap a structure to SELECT it).
            if (_movingSelected) { UpdateMoveLoop(); return; }
            if (_armed != null) { UpdatePlaceLoop(); return; }
            UpdateSelectLoop();
        }

        /// <summary>
        /// Cursor→ground raycast shared by the placement loops. Tries the configured
        /// <see cref="_groundMask"/> first; if it misses — e.g. the scene-baked mask
        /// excludes the ground's layer (WO-215: VillageSceneBuilder baked 1&lt;&lt;0 /
        /// Default-only, but the village ground collider is on another layer, so the
        /// raycast missed every frame and the ghost never appeared) — retries against
        /// all layers so placement always tracks real ground. Mirrors
        /// TowerPlacementSystem's ~0 mask, which works.
        /// </summary>
        private bool RaycastGround(out RaycastHit hit)
        {
            Ray ray = _camera.ScreenPointToRay(_input.ScreenPoint);
            if (Physics.Raycast(ray, out hit, _rayDistance, _groundMask)) return true;
            return Physics.Raycast(ray, out hit, _rayDistance, ~0);
        }

        /// <summary>
        /// Read the place/select confirm intent for THIS frame, gated so a tap/click
        /// that lands inside the on-screen move joystick's engage zone never confirms a
        /// placement or selection (DEF-171). The hero locomotion stick stays live during
        /// Build Mode, so without this guard a thumb-drag that starts on the stick would
        /// also drop a structure under it. Shares VirtualJoystick.IsInZone — the SAME
        /// circle the stick + CameraPanInput use — so the exclusion can't drift from the
        /// live stick layout. IBuildInput.PlaceOrSelect is a single-frame latch, so we
        /// must consume it (read once) even when suppressing, or a queued tap would leak
        /// to the next frame.
        /// </summary>
        private bool PlaceConfirmedThisFrame()
        {
            bool confirmed = _input.PlaceOrSelect;   // consumes the latch (touch driver)
            if (!confirmed) return false;
            // Suppress confirms whose screen point sits in the move-stick zone.
            if (VirtualJoystick.IsInZone(_input.ScreenPoint)) return false;
            return true;
        }

        /// <summary>Idle mode: a tap/click on a PlacedStructure selects it for edit.</summary>
        private void UpdateSelectLoop()
        {
            if (!PlaceConfirmedThisFrame()) return;

            if (!RaycastGround(out RaycastHit hit)) return;

            // Hit collider's GameObject or any parent may carry the marker.
            var ps = hit.collider != null
                ? hit.collider.GetComponentInParent<PlacedStructure>()
                : null;
            if (ps != null) SelectStructure(ps);
        }

        /// <summary>CREATE mode: the original armed-entry ghost-follow place loop (P1).</summary>
        private void UpdatePlaceLoop()
        {
            if (_ghost == null) return;

            // WO-334 — while the Preview & Rotate panel is open, the placement is in the
            // player's hands (the modal owns confirm/cancel). Freeze the ghost loop so a
            // stray tap behind the modal can't drop a second structure.
            if (_pendingPlace) { _ghost.Hide(); return; }

            // Cancel (right-click / Escape / touch Cancel button) backs out the armed
            // entry (keeps build mode open).
            if (_input.Cancel)
            {
                CancelArmed();
                return;
            }

            // Rotate (R key / touch Rotate button) yaws the ghost 90° before placing (free).
            if (_input.Rotate)
                _armedYawSteps = (_armedYawSteps + 1) & 3;

            if (!RaycastGround(out RaycastHit hit))
            {
                _ghost.Hide();
                return;
            }

            Vector3 snapped = _grid.SnapToGrid(hit.point);
            _ghost.MoveTo(snapped, _armedYawSteps);

            bool valid = IsValidPlacement(hit, snapped, _armed, out Vector2Int cell, out Vector2Int footprint);
            _ghost.SetValid(valid);

            if (PlaceConfirmedThisFrame() && valid)
            {
                // PIVOT (owner decision) — placement is now IN-WORLD for ALL types: the
                // ghost shows the model upright (GhostPreview applies the entry's
                // OrientationFix) and the player rotates in-world via the R key / touch
                // Rotate button before tapping to drop. The old modal Preview & Rotate
                // panel (OpenRotateMenu/OnRotateConfirmed/OnRotateCancelled/_rotateMenu)
                // is now dormant — no longer called from placement. Direct place leaves
                // _armedYawOffset = 0, so PlacedStructureData yaw = yawSteps × 90 only
                // (this also retires the latent double-rotation).
                Place(cell, footprint, snapped);

                // STAY-ARMED (CoC-style) — deliberately do NOT clear _armed / hide the
                // ghost after a place. The same entry stays armed so the next valid tap
                // drops ANOTHER copy (lay a wall/fence run without re-selecting), and
                // _armedYawSteps is kept so the run holds its rotation. Each copy still
                // re-validates + re-charges inside Place(). The player stops by Cancel/
                // Esc (_input.Cancel → CancelArmed, which fully disarms + hides the ghost)
                // or by arming a different palette item (Arm() resets state). Do not add
                // a disarm here.
            }
        }

        /// <summary>True when the armed catalog entry is a tower (gets the rotate panel).</summary>
        private static bool IsTowerEntry(CatalogEntry entry)
            => entry != null && entry.type == CatalogType.Tower;

        /// <summary>
        /// WO-334 — open the Preview &amp; Rotate panel for the armed tower at a validated
        /// cell. Stashes the placement target in the _pending* fields (which freezes the
        /// ghost loop), seeds the panel with the ghost's current free-rotate yaw, and on
        /// confirm maps the chosen Quaternion → yaw offset/steps then commits via Place();
        /// on cancel the entry stays armed (nothing placed). The preview model is the
        /// armed entry's visual prefab loaded from Resources (CatalogEntry carries a PATH,
        /// not a TowerData SO — see the prefab Open() overload).
        /// </summary>
        private void OpenRotateMenu(Vector2Int cell, Vector2Int footprint, Vector3 snapped)
        {
            if (_armed == null) return;

            _pendingPlace     = true;
            _pendingCell      = cell;
            _pendingFootprint = footprint;
            _pendingSnapped   = snapped;

            EnsureRotateMenu();

            GameObject prefab = !string.IsNullOrEmpty(_armed.visualPrefabPath)
                ? Resources.Load<GameObject>(_armed.visualPrefabPath)
                : null;
            string name = !string.IsNullOrEmpty(_armed.displayName) ? _armed.displayName : _armed.id;
            double costSkr = CostFor(_armed).crystals;

            // Seed the panel from the ghost's current free-rotate yaw step (R key steps).
            int initialYawSteps = _armedYawSteps & 3;

            // WO-335 FIX — forward the armed entry's upright correction so the preview
            // matches the placed result (StructureFactory applies entry.orientation at
            // build time; the preview must apply the SAME correction or towers show
            // sideways in the panel but stand up when placed).
            Debug.Log($"[Orient] open: id={_armed.id} prefab={(prefab != null ? prefab.name : "<none>")} yaw0={initialYawSteps * 90} orient={(_armed.orientation != null && _armed.orientation.manual ? "manual" : "none")}");
            _rotateMenu.Open(prefab, name, costSkr, initialYawSteps, OnRotateConfirmed, OnRotateCancelled, _armed.orientation);
        }

        /// <summary>
        /// WO-335 confirm callback — the player committed a yaw step (0..3 → 0/90/180/270°).
        /// Map it onto the same fields the place path writes (so the rotation persists into
        /// PlacedStructureData identically): _armedYawSteps = the step, _armedYawOffset =
        /// the exact yaw in degrees. Then commit at the pending cell.
        /// </summary>
        private void OnRotateConfirmed(int yawSteps)
        {
            _armedYawSteps  = yawSteps & 3;
            _armedYawOffset = _armedYawSteps * 90f;
            Debug.Log($"[Orient] confirmed: yawSteps={_armedYawSteps} yaw={_armedYawOffset:F0} cell={_pendingCell}");

            _pendingPlace = false;
            Place(_pendingCell, _pendingFootprint, _pendingSnapped);
        }

        /// <summary>WO-334 cancel callback — keep the entry armed; nothing is placed.</summary>
        private void OnRotateCancelled()
        {
            _pendingPlace = false;
            Debug.Log("[Orient] cancelled (entry stays armed).");
            // Entry stays armed so the player can re-position / re-confirm or cancel out.
        }

        /// <summary>
        /// MOVE mode: re-place the selected structure with a ghost seeded from its own
        /// entry + yaw. Its origin cells are already FREE (released on enter) so it
        /// never blocks itself. A valid tap re-occupies the new cells + moves the
        /// object + syncs BaseLayout; right-click/Escape cancels back to the origin.
        /// </summary>
        private void UpdateMoveLoop()
        {
            if (_ghost == null || _selected == null) { CancelMove(); return; }

            if (_input.Cancel)
            {
                CancelMove();
                return;
            }

            if (_input.Rotate)
                _armedYawSteps = (_armedYawSteps + 1) & 3;

            if (!RaycastGround(out RaycastHit hit))
            {
                _ghost.Hide();
                return;
            }

            Vector3 snapped = _grid.SnapToGrid(hit.point);
            _ghost.MoveTo(snapped, _armedYawSteps);

            // Affordability is irrelevant for a move (free) — validate placement only.
            bool valid = IsValidPlacement(hit, snapped, CatalogRegistry.Get(_selected.itemId),
                out Vector2Int cell, out Vector2Int footprint, ignoreCost: true);
            _ghost.SetValid(valid);

            if (PlaceConfirmedThisFrame() && valid)
                CommitMove(cell, footprint, snapped);
        }

        /// <summary>
        /// Combined validity: flat upward surface, footprint cells free + in-bounds,
        /// gate-lane clearance, and affordable. Pure over grid + config apart from
        /// the surface raycast hit (which the caller supplies).
        /// </summary>
        private bool IsValidPlacement(RaycastHit hit, Vector3 snapped, CatalogEntry entry,
            out Vector2Int cell, out Vector2Int footprint, bool ignoreCost = false)
        {
            cell = _grid.WorldToCell(snapped);
            // FIX (footprint from CORRECTED bounds) — measure the UPRIGHT, OrientationFix-
            // applied mesh (the SAME geometry the ghost + the placed structure use) so the
            // validity footprint matches what lands, letting pieces sit tight to a wall.
            footprint = _grid.FootprintCells(StructureFactory.MeasureUprightFootprintMetres(entry));

            // 1. Flat, upward-facing top (TowerPlacementSystem.IsValidSurface rule).
            if (hit.collider == null) return false;
            if (hit.normal.y < 0.85f) return false;
            if (hit.collider.CompareTag("Tower") || hit.collider.CompareTag("Building")) return false;

            // 2. Footprint cells free + in-bounds. (During a MOVE the structure's own
            //    cells were freed on enter, so they read as free and never self-block.)
            if (!_grid.CanPlace(cell, footprint)) return false;

            // The footprint's world-space AABB on the XZ plane (cellSize × footprint
            // cells, centred on the snapped cell-block). Used by the world-overlap +
            // gate-clearance tests below, which catch SCENE objects the cell grid does
            // not track (gates + the default-village structures are placed by the
            // scene builder, never Occupy()'d — so CanPlace can't see them).
            Bounds footprintAabb = FootprintWorldBounds(cell, footprint, snapped.y);

            // 3. World overlap — reject if the footprint overlaps an existing placed
            //    structure or a gate collider in the scene (NOT just the cell grid).
            if (OverlapsExistingStructure(footprintAabb)) return false;

            // 4. Gate-lane clearance — never wall off the spawn→Heart corridor. Tests
            //    the whole footprint AABB against each gate's real bounds (expanded by
            //    the clearance), so a structure whose body overlaps the gate is caught
            //    even when its origin cell is just outside the radius.
            if (_gateClearance > 0f && IsTooCloseToGate(footprintAabb)) return false;

            // 5. Affordable from the persisted multi-resource ledger (EconomyService —
            //    the GameState-backed Wood/Food/Iron/Crystals surface). Crystals-only
            //    entries fall back to a Crystals cost. A move is free, so the cost gate
            //    is skipped for it.
            if (!ignoreCost)
            {
                DeNelle.Core.Catalog.ResourceCost cost = CostFor(entry);
                if (!CanAfford(cost)) return false;
            }

            return true;
        }

        /// <summary>
        /// The world-space XZ bounding box of a footprint at <paramref name="cell"/>:
        /// the block of <paramref name="footprint"/> cells, each cellSize wide, with a
        /// thin Y extent at the placement height. Pure over the grid. This is the
        /// real area the structure will occupy, so the world-overlap + gate tests can
        /// reason about the whole structure, not just its origin cell centre.
        /// </summary>
        private Bounds FootprintWorldBounds(Vector2Int cell, Vector2Int footprint, float y)
        {
            float cs = _grid != null ? _grid.cellSize : 3f;
            int fw = Mathf.Max(1, footprint.x);
            int fh = Mathf.Max(1, footprint.y);

            // Min/max cell corners → world. CellToWorld returns the cell CENTRE, so
            // expand by half a cell on each side to cover the full block.
            Vector3 minC = _grid.CellToWorld(new Vector2Int(cell.x, cell.y));
            Vector3 maxC = _grid.CellToWorld(new Vector2Int(cell.x + fw - 1, cell.y + fh - 1));
            Vector3 min = new Vector3(Mathf.Min(minC.x, maxC.x) - cs * 0.5f, y - 0.5f, Mathf.Min(minC.z, maxC.z) - cs * 0.5f);
            Vector3 max = new Vector3(Mathf.Max(minC.x, maxC.x) + cs * 0.5f, y + 0.5f, Mathf.Max(minC.z, maxC.z) + cs * 0.5f);

            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        /// <summary>
        /// True when the placement footprint overlaps a structure already in the scene
        /// (player-placed OR default-village) on the XZ plane. The cell grid only
        /// tracks structures that were Occupy()'d (loader + player placements); the
        /// default-village buildings + gates are scene objects the grid never saw, so
        /// CanPlace alone cannot reject overlapping them. We test the footprint AABB
        /// against every live PlacedStructure's renderer bounds — using RENDERER bounds
        /// (centre + extents in world) sidesteps the scaled-pivot displacement trap
        /// where transform.position sits far from the visible mesh. During a MOVE the
        /// selected structure is excluded so it never blocks its own re-placement.
        /// </summary>
        private bool OverlapsExistingStructure(Bounds footprintAabb)
        {
            var all = FindObjectsByType<PlacedStructure>(FindObjectsSortMode.None);
            foreach (var ps in all)
            {
                if (ps == null) continue;
                if (_movingSelected && ps == _selected) continue;   // don't self-block a move

                Bounds wb;
                if (!TryWorldBounds(ps.gameObject, out wb)) continue;
                if (OverlapsXZ(footprintAabb, wb)) return true;
            }
            return false;
        }

        /// <summary>
        /// True when the footprint AABB intrudes on a gate's spawn→Heart LANE. TUNED
        /// (build-mode playtest): the old test expanded each gate's WHOLE bounds by the
        /// clearance on every side, which walled off a broad block around the gate and
        /// blocked legitimate placement along the adjacent wall. Now it guards only the
        /// actual gate OPENING — a narrow lane the width of the gate, running INWARD
        /// toward the Heart (0,0,0) and a short way OUTWARD toward the spawn — so a piece
        /// can sit right next to the gate as long as it doesn't block the doorway the
        /// enemies path through. Clearance is a small pad on the lane (default 3 m), not a
        /// radius around the gate. Uses renderer/collider bounds (displacement-trap safe).
        /// </summary>
        private bool IsTooCloseToGate(Bounds footprintAabb)
        {
            var gates = FindObjectsByType<Gate>(FindObjectsSortMode.None);
            foreach (var gate in gates)
            {
                if (gate == null) continue;
                Bounds gb;
                if (!TryWorldBounds(gate.gameObject, out gb)) gb = new Bounds(gate.transform.position, Vector3.one);

                // The lane runs along the gate→Heart axis (the spawn→Heart corridor). The
                // gate's SHORTER horizontal span is the opening WIDTH; the lane keeps that
                // width (+ a small pad) and extends along the through-axis so it covers the
                // doorway the enemies walk, NOT the whole wall the gate sits in.
                Vector3 c = gb.center;
                Vector3 toHeart = new Vector3(-c.x, 0f, -c.z);   // Heart is at origin
                bool throughIsX = Mathf.Abs(toHeart.x) >= Mathf.Abs(toHeart.z);

                float openWidth = throughIsX ? gb.size.z : gb.size.x;   // span across the opening
                float halfWidth = openWidth * 0.5f + _gateClearance;
                float laneReach = 6f + _gateClearance;                   // how far the lane guards in/out

                Bounds lane = new Bounds(c, Vector3.one);
                if (throughIsX)
                    lane.SetMinMax(
                        new Vector3(c.x - laneReach, c.y - 0.5f, c.z - halfWidth),
                        new Vector3(c.x + laneReach, c.y + 0.5f, c.z + halfWidth));
                else
                    lane.SetMinMax(
                        new Vector3(c.x - halfWidth, c.y - 0.5f, c.z - laneReach),
                        new Vector3(c.x + halfWidth, c.y + 0.5f, c.z + laneReach));

                if (OverlapsXZ(footprintAabb, lane)) return true;
            }
            return false;
        }

        /// <summary>
        /// World-space renderer bounds of <paramref name="go"/> (centre + extents in
        /// world). Renderer bounds are robust to the scaled/off-pivot mesh trap; falls
        /// back to a collider, then false when neither exists.
        /// </summary>
        private static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null) return false;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                bounds = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                return true;
            }
            var col = go.GetComponentInChildren<Collider>(true);
            if (col != null) { bounds = col.bounds; return true; }
            return false;
        }

        /// <summary>True when two bounds overlap on the XZ plane (Y is ignored — placement is a flat planner).</summary>
        private static bool OverlapsXZ(Bounds a, Bounds b)
        {
            return a.min.x < b.max.x && a.max.x > b.min.x
                && a.min.z < b.max.z && a.max.z > b.min.z;
        }

        /// <summary>
        /// Commit a placement: build via StructureFactory, occupy the grid, CHARGE the
        /// persisted crystal wallet (only here, after the valid commit — WO-131), add
        /// the PlacedStructure marker, and append to the live BaseLayout. The entry
        /// stays armed so the player can place several in a row (CoC behaviour).
        /// </summary>
        private void Place(Vector2Int cell, Vector2Int footprint, Vector3 snapped)
        {
            // Re-check affordability AT commit through the same ledger the validity gate
            // used — never spawn if the player can't pay (defensive: balance may have
            // changed since the ghost frame). Charge ONLY after this, per WO-131.
            DeNelle.Core.Catalog.ResourceCost cost = CostFor(_armed);
            if (!CanAfford(cost))
            {
                Debug.Log($"[BuildMode] Not enough resources to place '{_armed.id}' — placement aborted.");
                return;
            }

            var loader = BaseLayoutLoader.EnsureExists();
            var data = new PlacedStructureData(_armed.id, cell.x, cell.y, _armedYawSteps, 1, _armedYawOffset);

            var ps = loader.Spawn(data, _grid);
            if (ps == null)
            {
                Debug.LogWarning($"[BuildMode] Placement of '{_armed.id}' failed to spawn — not charged.");
                return;
            }

            // Charge ONLY AFTER the committed valid placement (WO-131): the persisted
            // multi-resource ledger (EconomyService → GameState-backed Crystals/Food +
            // in-session Wood/Iron). TrySpend is atomic; it can't fail here (we re-checked
            // CanAfford above) but the bool is honoured for safety.
            ChargeLedger(cost);

            // Append to the live BaseLayout so Exit() persists it.
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state != null)
            {
                if (state.BaseLayout == null) state.BaseLayout = new List<PlacedStructureData>();
                state.BaseLayout.Add(data);
            }

            Debug.Log($"[BuildMode] Placed '{_armed.id}' at cell ({cell.x},{cell.y}) yaw {_armedYawSteps * 90}°, charged {Describe(cost)}.");
        }

        // =====================================================================
        //  Arming
        // =====================================================================

        private void Arm(CatalogEntry entry)
        {
            // Entering CREATE mode clears any active selection / move (P2).
            ClearSelection();
            _armed = entry;
            _armedYawSteps = 0;
            _armedYawOffset = 0;
            _pendingPlace = false;
            if (_ghost == null) _ghost = new GameObject("GhostPreview").AddComponent<GhostPreview>();
            _ghost.SetEntry(entry);
        }

        private void CancelArmed()
        {
            _armed = null;
            _ghost?.Hide();
            // WO-334 — if the rotate panel was mid-confirm, tear it down (no callback).
            if (_pendingPlace)
            {
                _pendingPlace = false;
                _rotateMenu?.Close();
            }
        }

        // =====================================================================
        //  Select / Move / Sell (P2 edit verbs)
        // =====================================================================

        /// <summary>
        /// Select a placed structure: highlight it and show the Move/Sell/Cancel
        /// action panel (refund = 50% of its catalog buildCost, rounded down). Any
        /// previously-armed CREATE entry is dropped so the modes never overlap.
        /// </summary>
        private void SelectStructure(PlacedStructure ps)
        {
            if (ps == null) return;
            CancelArmed();
            ClearSelection();   // drop any prior highlight before re-selecting

            _selected = ps;
            _selected.SetHighlighted(true);

            EnsureSelectionUi();
            ShowSelectionPanel(ps);
        }

        /// <summary>
        /// Populate + show the edit panel for <paramref name="ps"/> with its current tier
        /// state (S5): label, sell refund, level/maxLevel, and the next-tier upgrade cost +
        /// whether it is currently affordable. Shared by select + post-move + post-upgrade.
        /// </summary>
        private void ShowSelectionPanel(PlacedStructure ps)
        {
            if (ps == null) return;
            var entry = CatalogRegistry.Get(ps.itemId);
            string label = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : ps.itemId;

            int level = Mathf.Max(1, ps.level);
            int maxLevel = MaxLevelFor(entry);

            int upgradeTotal = 0;
            bool canAfford = false;
            if (level < maxLevel)
            {
                DeNelle.Core.Catalog.ResourceCost up = UpgradeCostFor(entry, level);
                upgradeTotal = up.wood + up.food + up.iron + up.crystals;
                canAfford = CanAfford(up);
            }

            _selectionUi?.Show(label, RefundFor(ps), level, maxLevel, upgradeTotal, canAfford);
        }

        /// <summary>Drop the current selection (highlight + panel) and any in-progress move.</summary>
        private void ClearSelection()
        {
            if (_movingSelected) CancelMove();
            if (_selected != null) _selected.SetHighlighted(false);
            _selected = null;
            _selectionUi?.Hide();
        }

        /// <summary>
        /// SELL the selected structure: free its grid cells, drop its BaseLayout
        /// record (matched by cell + itemId), destroy the GameObject, and REFUND 50%
        /// of its buildCost to the persisted crystal wallet (WO-131 single wallet).
        /// </summary>
        private void SellSelected()
        {
            if (_selected == null) return;
            var ps = _selected;

            DeNelle.Core.Catalog.ResourceCost refund = RefundCostFor(ps);

            // Free the cells it held.
            _grid?.Free(ps.gridCell, ps.footprint);

            // Drop the persisted record (match by cell + itemId).
            RemoveLayoutEntry(ps.itemId, ps.gridCell);

            // Drop it from the loader's live set so it doesn't double-free on Exit.
            BaseLayoutLoader.Instance?.Forget(ps);

            // Refund ~50% of the full multi-resource cost into the persisted ledger
            // (EconomyService.Grant → GameState-backed Crystals/Food + in-session
            // Wood/Iron). Crystals-only entries refund Crystals only.
            RefundLedger(refund);

            Debug.Log($"[BuildMode] Sold '{ps.itemId}' at cell ({ps.gridCell.x},{ps.gridCell.y}) — refunded {Describe(refund)}.");

            // Clear selection BEFORE destroy so the highlight teardown sees a live object.
            _selected.SetHighlighted(false);
            _selected = null;
            _selectionUi?.Hide();
            Destroy(ps.gameObject);
        }

        // =====================================================================
        //  Upgrade (S5 — the CoC sink: level 1→maxLevel)
        // =====================================================================

        /// <summary>
        /// UPGRADE the selected structure one tier: if it is below its catalog maxLevel AND
        /// the next-tier cost is affordable, spend that cost through the persisted ledger,
        /// increment <see cref="PlacedStructure.level"/>, step the visual (StructureTierVisual)
        /// and the gameplay stats (DefenseTower range/damage · WallSegment toughness) up a tier,
        /// re-persist the level in BaseLayout, and re-show the panel at the new tier. Bails
        /// (logs) at the ceiling or when unaffordable — never charges on a no-op.
        /// </summary>
        private void UpgradeSelected()
        {
            if (_selected == null) return;
            var ps = _selected;
            var entry = CatalogRegistry.Get(ps.itemId);

            int level = Mathf.Max(1, ps.level);
            int maxLevel = MaxLevelFor(entry);
            if (level >= maxLevel)
            {
                Debug.Log($"[BuildMode] '{ps.itemId}' already at max tier ({maxLevel}) — no upgrade.");
                return;
            }

            DeNelle.Core.Catalog.ResourceCost cost = UpgradeCostFor(entry, level);
            if (!CanAfford(cost))
            {
                Debug.Log($"[BuildMode] Not enough resources to upgrade '{ps.itemId}' to tier {level + 1} " +
                          $"(needs {Describe(cost)}).");
                // Re-show so the button reflects the (still-unaffordable) state.
                ShowSelectionPanel(ps);
                return;
            }

            // Charge ONLY after the affordability gate passes (mirrors the place-charge rule).
            ChargeLedger(cost);

            int newLevel = level + 1;
            ps.level = newLevel;

            // Step the visual tier (scale + accent). Apply is idempotent + re-collects renderers.
            if (ps.TierVisual != null) { ps.TierVisual.Apply(newLevel); ps.TierVisual.Refresh(); }

            // Step the gameplay stats per tier (range/damage for towers, toughness for walls).
            ApplyTierStats(ps, newLevel);

            // Persist the new level in the live BaseLayout (so Exit()'s Save() round-trips it).
            UpdateLayoutLevel(ps.itemId, ps.gridCell, newLevel);

            Debug.Log($"[BuildMode] Upgraded '{ps.itemId}' at cell ({ps.gridCell.x},{ps.gridCell.y}) " +
                      $"to tier {newLevel}/{maxLevel}, charged {Describe(cost)}.");

            // Re-show the panel at the new tier (refreshed cost / Max-tier state).
            ShowSelectionPanel(ps);
        }

        /// <summary>
        /// Apply per-tier gameplay stats to a structure at <paramref name="level"/>. Towers
        /// scale range + damage with the tier (the catalog base × a per-tier multiplier); walls
        /// step their durability toughness. Visual stepping is owned by StructureTierVisual;
        /// this is the BEHAVIOUR half. Null-/component-safe (a decoration upgrades visually only).
        /// Internal so BaseLayoutLoader can re-assert the tier stats when a saved structure
        /// reloads above level 1 (so a tier-3 tower comes back at tier-3 stats, not base).
        /// </summary>
        internal static void ApplyTierStats(PlacedStructure ps, int level)
        {
            if (ps == null) return;
            int tier = Mathf.Clamp(level, 1, 3);
            var entry = CatalogRegistry.Get(ps.itemId);
            var repo = entry != null ? entry.repo : null;

            // Tower — scale range + damage from the catalog base off the tier multiplier so a
            // tier-3 tower hits harder + further (1.0 / 1.25 / 1.55). Read base off the catalog
            // (not the live field) so repeated upgrades never compound.
            var tower = ps.GetComponent<DefenseTower>();
            if (tower != null && repo != null)
            {
                float mul = s_towerTierMul[tier];
                tower.Range  = repo.range  * mul;
                tower.Damage = repo.damage * mul;
                // FireRate intentionally unchanged — range + damage are the readable tier wins.
            }

            // Wall — step the durability tier (incoming-damage toughness on the 0-100 track).
            var wall = ps.GetComponent<WallSegment>();
            if (wall != null) wall.SetTier(tier);
        }

        // Per-tier tower stat multiplier (index 0 unused): L1 ×1.0 · L2 ×1.25 · L3 ×1.55.
        private static readonly float[] s_towerTierMul = { 1f, 1f, 1.25f, 1.55f };

        /// <summary>
        /// The catalog max upgrade level for an entry (S5). Clamped to the visual tier ceiling
        /// (3 — StructureTierVisual supports 1..3). Defaults to 1 (not upgradeable) for a null
        /// entry or a row that omits maxLevel.
        /// </summary>
        private static int MaxLevelFor(CatalogEntry entry)
        {
            var repo = entry != null ? entry.repo : null;
            if (repo == null) return 1;
            return Mathf.Clamp(repo.maxLevel, 1, 3);
        }

        /// <summary>
        /// Resolve the upgrade cost for the step <paramref name="fromLevel"/> → fromLevel+1.
        /// Uses the authored per-step table (repo.upgradeCost[fromLevel-1]) when present;
        /// otherwise falls back to a data scaler — the build cost scaled by the level being
        /// left (so L1→L2 ≈ 1× the build cost, L2→L3 ≈ 2× — a rising CoC-style sink) — so a
        /// row can opt into upgrades with just maxLevel and no explicit table. Null-safe.
        /// </summary>
        private static DeNelle.Core.Catalog.ResourceCost UpgradeCostFor(CatalogEntry entry, int fromLevel)
        {
            var repo = entry != null ? entry.repo : null;
            if (repo == null) return default;

            int idx = Mathf.Max(0, fromLevel - 1);   // L1→L2 uses index 0
            if (repo.upgradeCost != null && idx < repo.upgradeCost.Length)
            {
                var authored = repo.upgradeCost[idx];
                if (!authored.IsZero) return authored;   // authored table wins
            }

            // Fallback scaler: the resolved build cost × the level being left.
            DeNelle.Core.Catalog.ResourceCost baseCost = CostFor(entry);
            int scale = Mathf.Max(1, fromLevel);
            return new DeNelle.Core.Catalog.ResourceCost
            {
                wood     = baseCost.wood     * scale,
                food     = baseCost.food     * scale,
                iron     = baseCost.iron     * scale,
                crystals = baseCost.crystals * scale,
            };
        }

        /// <summary>
        /// Begin MOVE: seed the ghost from the selected structure's entry + current
        /// yaw, FREE its current cells (so it can't block its own re-placement), and
        /// hand control to the move loop. The action panel hides during the move.
        /// </summary>
        private void BeginMoveSelected()
        {
            if (_selected == null) return;
            var entry = CatalogRegistry.Get(_selected.itemId);
            if (entry == null)
            {
                Debug.LogWarning($"[BuildMode] Cannot move '{_selected.itemId}' — not in registry.");
                return;
            }

            _moveOriginCell = _selected.gridCell;
            _armedYawSteps = _selected.yawSteps;

            // Release the structure's own cells for the duration of the move.
            _grid?.Free(_selected.gridCell, _selected.footprint);

            if (_ghost == null) _ghost = new GameObject("GhostPreview").AddComponent<GhostPreview>();
            _ghost.SetEntry(entry);

            _movingSelected = true;
            _selectionUi?.Hide();
        }

        /// <summary>
        /// Commit a MOVE to a validated cell: occupy the new cells, reposition the
        /// GameObject, and sync the PlacedStructure marker + its BaseLayout record
        /// (cellX/cellZ/yawSteps). Free, so the wallet is untouched.
        /// </summary>
        private void CommitMove(Vector2Int cell, Vector2Int footprint, Vector3 snapped)
        {
            if (_selected == null) { CancelMove(); return; }

            _grid?.Occupy(cell, footprint, _selected.itemId);

            // Move the object (keep the surface height from the snap point).
            _selected.transform.SetPositionAndRotation(
                snapped, Quaternion.Euler(0f, _armedYawSteps * 90f, 0f));

            // Sync the live marker, then the matching persisted record (old cell → new).
            var oldCell = _selected.gridCell;
            _selected.gridCell = cell;
            _selected.footprint = footprint;
            _selected.yawSteps = _armedYawSteps;
            UpdateLayoutEntry(_selected.itemId, oldCell, cell, _armedYawSteps);

            Debug.Log($"[BuildMode] Moved '{_selected.itemId}' to cell ({cell.x},{cell.y}) yaw {_armedYawSteps * 90}° (free).");

            _movingSelected = false;
            _ghost?.Hide();

            // Re-show the action panel on the moved structure (stays selected).
            ShowSelectionPanel(_selected);
        }

        /// <summary>Abort an in-progress move: re-occupy the origin cells, keep it put.</summary>
        private void CancelMove()
        {
            _movingSelected = false;
            _ghost?.Hide();
            if (_selected != null)
                _grid?.Occupy(_moveOriginCell, _selected.footprint, _selected.itemId);
        }

        /// <summary>
        /// Display refund for the sell panel: the SUM of the 50% multi-resource refund
        /// across all four pools, rounded down. The panel shows a single "◆ N" value,
        /// so we surface the total units refunded; the actual per-resource refund is
        /// applied by <see cref="RefundLedger"/>. For a crystals-only entry this equals
        /// 50% of buildCost, so the old display is unchanged.
        /// </summary>
        private static int RefundFor(PlacedStructure ps)
        {
            var r = RefundCostFor(ps);
            return r.wood + r.food + r.iron + r.crystals;
        }

        /// <summary>
        /// The full ~50% multi-resource refund for a placed structure: the build cost PLUS
        /// every upgrade step it has paid into (S5 — invested-cost-aware), each slot halved
        /// (rounded down). So selling a tier-3 tower returns 50% of build + L1→L2 + L2→L3.
        /// Crystals-only entries refund only Crystals (their buildCost fallback halved).
        /// </summary>
        private static DeNelle.Core.Catalog.ResourceCost RefundCostFor(PlacedStructure ps)
        {
            if (ps == null) return default;
            var entry = CatalogRegistry.Get(ps.itemId);

            // Base build cost…
            var total = CostFor(entry);

            // …plus each upgrade step paid to reach the current level (L1→L2, …, (lvl-1)→lvl).
            int level = Mathf.Max(1, ps.level);
            for (int from = 1; from < level; from++)
            {
                var step = UpgradeCostFor(entry, from);
                total.wood     += step.wood;
                total.food     += step.food;
                total.iron     += step.iron;
                total.crystals += step.crystals;
            }

            return new DeNelle.Core.Catalog.ResourceCost
            {
                wood     = total.wood     / 2,
                food     = total.food     / 2,
                iron     = total.iron     / 2,
                crystals = total.crystals / 2,
            };
        }

        // =====================================================================
        //  Cost resolution + ledger boundary (S4 — multi-resource economy)
        // =====================================================================

        /// <summary>
        /// Resolve a catalog entry's build cost to the Core multi-resource shape, with
        /// the crystals-only FALLBACK: if the entry authored a non-zero multi-cost
        /// (repo.cost), use it verbatim; otherwise charge repo.buildCost Crystals so
        /// legacy / cost-less rows never regress. Null-safe (returns a free cost).
        /// </summary>
        private static DeNelle.Core.Catalog.ResourceCost CostFor(CatalogEntry entry)
        {
            var repo = entry != null ? entry.repo : null;
            if (repo == null) return default;
            if (!repo.cost.IsZero) return repo.cost;                       // multi-cost wins
            return new DeNelle.Core.Catalog.ResourceCost { crystals = repo.buildCost };   // crystals fallback
        }

        /// <summary>Map the Core cost to EconomyService.ResourceCost (1:1 field copy).</summary>
        private static ResourceCost ToEconomy(DeNelle.Core.Catalog.ResourceCost c)
            => new ResourceCost(c.wood, c.food, c.iron, c.crystals);

        /// <summary>
        /// Affordability via the persisted multi-resource ledger (EconomyService). Falls
        /// back to the crystal wallet read if the service isn't up yet, so the build
        /// menu still gates on Crystals in a service-less edge case.
        /// </summary>
        private static bool CanAfford(DeNelle.Core.Catalog.ResourceCost cost)
        {
            var econ = EconomyService.Instance;
            if (econ != null) return econ.CanAfford(ToEconomy(cost));
            // Service-less fallback: only Crystals are GameState-readable here.
            return CrystalBalance >= cost.crystals;
        }

        /// <summary>Spend the cost through the ledger (atomic). No-op for a free cost.</summary>
        private static void ChargeLedger(DeNelle.Core.Catalog.ResourceCost cost)
        {
            if (cost.IsZero) return;
            var econ = EconomyService.Instance;
            if (econ != null) { econ.TrySpend(ToEconomy(cost)); return; }
            // Service-less fallback: charge the persisted crystal wallet directly.
            if (cost.crystals > 0) GameStateService.Instance?.AddCrystals(-cost.crystals);
        }

        /// <summary>Refund the cost through the ledger (Grant). No-op for a free cost.</summary>
        private static void RefundLedger(DeNelle.Core.Catalog.ResourceCost cost)
        {
            if (cost.IsZero) return;
            var econ = EconomyService.Instance;
            if (econ != null) { econ.Grant(ToEconomy(cost)); return; }
            // Service-less fallback: refund the persisted crystal wallet directly.
            if (cost.crystals > 0) GameStateService.Instance?.AddCrystals(+cost.crystals);
        }

        /// <summary>Compact human-readable cost string for logs (skips zero slots).</summary>
        private static string Describe(DeNelle.Core.Catalog.ResourceCost c)
        {
            if (c.IsZero) return "nothing";
            var parts = new List<string>(4);
            if (c.wood     > 0) parts.Add($"{c.wood} wood");
            if (c.food     > 0) parts.Add($"{c.food} food");
            if (c.iron     > 0) parts.Add($"{c.iron} iron");
            if (c.crystals > 0) parts.Add($"{c.crystals} crystals");
            return string.Join(", ", parts);
        }

        // ── BaseLayout sync (struct list — match by cell + itemId) ───────────────

        /// <summary>Remove the persisted record matching <paramref name="itemId"/> at <paramref name="cell"/>.</summary>
        private static void RemoveLayoutEntry(string itemId, Vector2Int cell)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null) return;
            for (int i = layout.Count - 1; i >= 0; i--)
            {
                if (layout[i].itemId == itemId && layout[i].cellX == cell.x && layout[i].cellZ == cell.y)
                {
                    layout.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Re-point the persisted record from <paramref name="oldCell"/> to
        /// <paramref name="newCell"/> + yaw. PlacedStructureData is a struct, so we
        /// replace the element by index (not mutate a copy).
        /// </summary>
        private static void UpdateLayoutEntry(string itemId, Vector2Int oldCell, Vector2Int newCell, int yawSteps)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null) return;
            for (int i = 0; i < layout.Count; i++)
            {
                if (layout[i].itemId == itemId && layout[i].cellX == oldCell.x && layout[i].cellZ == oldCell.y)
                {
                    var d = layout[i];
                    d.cellX = newCell.x;
                    d.cellZ = newCell.y;
                    d.yawSteps = yawSteps;
                    layout[i] = d;
                    return;
                }
            }
        }

        /// <summary>
        /// S5 — re-stamp the persisted record's upgrade level (match by cell + itemId).
        /// PlacedStructureData is a struct, so replace the element by index. Exit()'s Save()
        /// then round-trips the new level so an upgraded structure reloads at its tier.
        /// </summary>
        private static void UpdateLayoutLevel(string itemId, Vector2Int cell, int level)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null) return;
            for (int i = 0; i < layout.Count; i++)
            {
                if (layout[i].itemId == itemId && layout[i].cellX == cell.x && layout[i].cellZ == cell.y)
                {
                    var d = layout[i];
                    d.level = level;
                    layout[i] = d;
                    return;
                }
            }
        }

        // =====================================================================
        //  Seeding — first entry copies the default village into BaseLayout
        // =====================================================================

        /// <summary>
        /// First build-mode entry init. BaseLayout holds ONLY the player's added deltas
        /// (catalog placements) — never the baked default village. The baked village stays
        /// the scene default; BaseLayout is the delta layered on top.
        ///
        /// FIX (double-village / mass-rebuild): this used to copy every in-scene
        /// PlacedStructure (incl. baked GameplayBuildings + a stale prior-session
        /// loaded set) into BaseLayout. That double-counted the baked village and grew
        /// the persisted set, which Rebuild()/LoadFromState() then destroyed-and-
        /// respawned wholesale — making all prior pieces pop on at once. We no longer
        /// seed the baked buildings. The lazy-init of an empty list is preserved so the
        /// Place() add path has a list to append to. Idempotent: a non-empty layout is
        /// left untouched.
        /// </summary>
        private void SeedBaseLayoutIfFirstEntry()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return;
            if (state.BaseLayout != null && state.BaseLayout.Count > 0) return;

            // Ensure the delta list exists so Place() can append, but DO NOT seed it from
            // baked scene buildings — the baked village is the scene default, not a delta.
            if (state.BaseLayout == null) state.BaseLayout = new List<PlacedStructureData>();
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
            // DEF-117 — drive the camera that is ACTUALLY rendering to the screen, not
            // the tag-resolved Camera.main (which can be a rogue / embedded FBX camera
            // once the game camera's per-frame sole-camera cull is paused below).
            _camera = ActiveScreenCamera();
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

            // DEF-117 (split-screen) — disabling the game camera's driver above also
            // pauses its EnforceSoleCamera() rogue-cull. So suppress EVERY other live
            // screen camera ourselves for the duration of build mode; otherwise a
            // second view (e.g. a runtime-spawned embedded FBX camera on a half
            // viewport) renders alongside the overview and the screen splits.
            SuppressRogueCameras();

            // Top-down overview centred on the grid. The overview owns the whole
            // screen (full viewport, top render priority) so nothing double-renders.
            _camera.rect = new Rect(0f, 0f, 1f, 1f);
            _camera.enabled = true;

            Vector3 centre = _grid != null
                ? _grid.CellToWorld(new Vector2Int(_grid.gridWidth / 2, _grid.gridHeight / 2))
                : Vector3.zero;
            // Seed the live pan/zoom state, then apply. The camera now LOOKS AT _camFocus
            // from _camHeight at the build pitch, so pan = move focus, zoom = change height.
            _camFocus  = new Vector3(centre.x, 0f, centre.z);
            _camHeight = Mathf.Clamp(_buildModeHeight, _camHeightMin, _camHeightMax);
            _camDragging = false;
            ApplyBuildCamera();
        }

        /// <summary>
        /// Place the overview camera from the live focus + zoom state at the build pitch.
        /// Keeps the original framing (back-and-up by the height, 45° down) but driven by
        /// _camFocus / _camHeight so the pan/zoom loop just mutates those.
        /// </summary>
        private void ApplyBuildCamera()
        {
            if (_camera == null) return;
            _camera.transform.position = new Vector3(_camFocus.x, _camHeight, _camFocus.z - _camHeight);
            _camera.transform.rotation = Quaternion.Euler(_buildModePitch, 0f, 0f);
        }

        /// <summary>
        /// Desktop pan + zoom for the build overview (touch already pans via the Lean
        /// driver). WASD / arrow keys + screen-edge scroll move the focus on the XZ plane;
        /// middle- or right-drag grabs the view; the scroll wheel zooms (changes height).
        /// The focus is CLAMPED to the grid (map) bounds so the player can't pan into the
        /// void. New Input System reads (Village runs with legacy Input disabled).
        /// </summary>
        private void UpdateBuildCameraPan()
        {
            if (_camera == null) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            float dt = Time.unscaledDeltaTime;

            // Camera-local right/forward projected onto the XZ plane (pitch-independent pan).
            Vector3 fwd = _camera.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = _camera.transform.right; right.y = 0f; right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;

            Vector2 move = Vector2.zero;   // x = strafe, y = forward
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  move.x -= 1f;
            }

            // Edge-scroll — pointer near a screen border nudges the view that way.
            if (mouse != null && _camEdgeBand > 0f)
            {
                Vector2 p = mouse.position.ReadValue();
                if (p.x <= _camEdgeBand)                    move.x -= 1f;
                else if (p.x >= Screen.width - _camEdgeBand) move.x += 1f;
                if (p.y <= _camEdgeBand)                    move.y -= 1f;
                else if (p.y >= Screen.height - _camEdgeBand) move.y += 1f;
            }

            if (move.sqrMagnitude > 1e-4f)
            {
                move = Vector2.ClampMagnitude(move, 1f);
                _camFocus += (right * move.x + fwd * move.y) * (_camPanSpeed * dt);
            }

            // Middle-drag grabs the view (screen-pixel delta → world pan). Right-drag also
            // pans, but ONLY when idle (nothing armed / not moving) — while armed/moving the
            // right button is reserved for Cancel (IBuildInput.Cancel), so a right-drag there
            // would disarm rather than pan. Touch pans via the Lean driver.
            if (mouse != null)
            {
                bool rightPanAllowed = _armed == null && !_movingSelected;
                bool dragHeld = mouse.middleButton.isPressed || (rightPanAllowed && mouse.rightButton.isPressed);
                Vector2 sp = mouse.position.ReadValue();
                if (dragHeld && !_camDragging) { _camDragging = true; _camDragLastPoint = sp; }
                else if (!dragHeld)            { _camDragging = false; }
                else if (_camDragging)
                {
                    Vector2 d = sp - _camDragLastPoint;
                    _camDragLastPoint = sp;
                    // Drag-right pushes the world left (grab metaphor) → subtract.
                    _camFocus -= (right * d.x + fwd * d.y) * _camDragSpeed;
                }

                // Scroll wheel → zoom (changes height).
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    _camHeight = Mathf.Clamp(_camHeight - Mathf.Sign(scroll) * _camZoomStep, _camHeightMin, _camHeightMax);
            }

            // Clamp the focus to the grid (map) bounds so the view can't leave the world.
            if (_grid != null)
            {
                float halfW = _grid.gridWidth  * _grid.cellSize * 0.5f;
                float halfH = _grid.gridHeight * _grid.cellSize * 0.5f;
                Vector3 mapCentre = _grid.origin + new Vector3(halfW, 0f, halfH);
                _camFocus.x = Mathf.Clamp(_camFocus.x, mapCentre.x - halfW, mapCentre.x + halfW);
                _camFocus.z = Mathf.Clamp(_camFocus.z, mapCentre.z - halfH, mapCentre.z + halfH);
            }

            ApplyBuildCamera();
        }

        private void RestoreCamera()
        {
            foreach (var mb in _disabledCamDrivers)
                if (mb != null) mb.enabled = true;
            _disabledCamDrivers.Clear();

            // Re-enable the cameras we suppressed for the overview (DEF-117).
            foreach (var c in _suppressedCameras)
                if (c != null) c.enabled = true;
            _suppressedCameras.Clear();

            if (_camera != null)
            {
                _camera.transform.position = _savedCamPos;
                _camera.transform.rotation = _savedCamRot;
            }
        }

        /// <summary>
        /// DEF-117 — the camera the player actually sees: the enabled, screen-bound
        /// (no targetTexture) camera with the highest depth. This is the same rule
        /// SmartMobileCamera/VillageCamera use, so it returns the depth=100 game
        /// camera rather than a rogue embedded one. Falls back to Camera.main.
        /// </summary>
        private static Camera ActiveScreenCamera()
        {
            Camera best = null;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || !c.enabled) continue;
                if (c.targetTexture != null) continue;   // offscreen RT cams are fine
                if (best == null || c.depth > best.depth) best = c;
            }
            return best != null ? best : Camera.main;
        }

        /// <summary>
        /// DEF-117 (split-screen) — disable every OTHER live screen camera for the
        /// build session so only the overview renders, and remember them so Exit()
        /// can re-enable them. The overview camera (_camera) is left untouched.
        /// </summary>
        private void SuppressRogueCameras()
        {
            _suppressedCameras.Clear();
            foreach (var c in Camera.allCameras)
            {
                if (c == null || c == _camera) continue;
                if (c.targetTexture != null) continue;   // leave render-texture cams alone
                if (!c.enabled) continue;
                c.enabled = false;
                _suppressedCameras.Add(c);
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
            _palette.OnOrientRequested += OpenOrientEditorForArmed;
        }

        /// <summary>
        /// WO (build-mode orient) — open the 3-axis orient EDITOR on the ARMED entry (no id
        /// typing). Resolves the armed entry's visual prefab from Resources and hands it to
        /// TowerPlacementRotateMenu.OpenDevOrient, which logs an [OrientRecipe] line + applies
        /// the dialed offset on Confirm. The standalone (AdminOverlay) path still drives the
        /// same editor by typed/dropdown id.
        /// </summary>
        private void OpenOrientEditorForArmed(string id)
        {
            var entry = _armed ?? CatalogRegistry.Get(id);
            if (entry == null)
            {
                Debug.LogWarning($"[BuildMode] Orient: no armed entry / '{id}' not in registry.");
                return;
            }

            GameObject prefab = !string.IsNullOrEmpty(entry.visualPrefabPath)
                ? Resources.Load<GameObject>(entry.visualPrefabPath)
                : null;
            if (prefab == null)
            {
                Debug.LogWarning($"[BuildMode] Orient: '{entry.id}' has no loadable visual prefab ('{entry.visualPrefabPath}').");
                return;
            }

            var menu = FindObjectOfType<TowerPlacementRotateMenu>();
            if (menu == null)
            {
                var mgo = new GameObject("DevOrientMenu");
                menu = mgo.AddComponent<TowerPlacementRotateMenu>();
            }
            _orientEditor = menu;
            string name = !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : entry.id;
            menu.OpenDevOrient(entry.id, prefab, name);
            Debug.Log($"[Orient] build-mode orient opened on armed '{entry.id}'.");
        }

        private void EnsureSelectionUi()
        {
            if (_selectionUi != null) return;
            var go = new GameObject("BuildSelectionUI");
            _selectionUi = go.AddComponent<BuildSelectionUI>();
            _selectionUi.OnMoveRequested += BeginMoveSelected;
            _selectionUi.OnSellRequested += SellSelected;
            _selectionUi.OnUpgradeRequested += UpgradeSelected;
            _selectionUi.OnCancelRequested += ClearSelection;
        }

        /// <summary>Lazily create the WO-335 player Rotate Model panel host (one per session).</summary>
        private void EnsureRotateMenu()
        {
            if (_rotateMenu != null) return;
            var go = new GameObject("RotateModelMenu");
            _rotateMenu = go.AddComponent<RotateModelMenu>();
        }

        /// <summary>
        /// Install the Lean.Touch driver as the input source on a touch device (S6).
        /// On desktop (no touch support) the mouse/keyboard DesktopBuildInput stays in
        /// place, so the desktop path is untouched. The driver is wired to the live
        /// overview camera (_camera, set by PullCameraBack() earlier in Enter) so its
        /// two-finger pan/zoom drives the right view.
        /// </summary>
        private void EnsureTouchInput()
        {
            if (!Input.touchSupported) return;   // desktop → keep DesktopBuildInput

            if (_touchDriver == null)
            {
                var go = new GameObject("LeanTouchBuildDriver");
                _touchDriver = go.AddComponent<LeanTouchBuildDriver>();
            }
            _touchDriver.Install(_camera);
            _input = _touchDriver;
        }

        /// <summary>
        /// Override the input source (e.g. a test harness, or to force the touch driver
        /// on a desktop build for QA). Null reverts to the default mouse/keyboard impl.
        /// </summary>
        public void SetInput(IBuildInput input)
        {
            _input = input ?? new DesktopBuildInput();
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
