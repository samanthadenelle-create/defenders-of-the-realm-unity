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
        private BuildSelectionUI _selectionUi;
        private GhostPreview _ghost;

        private Camera _camera;
        private CatalogEntry _armed;
        private int _armedYawSteps;

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
            if (!IsActive) return;
            // DEF-117 — raycast from the camera that is ACTUALLY on screen (the one
            // we pulled into the overview), never Camera.main: with rogue cameras in
            // play Camera.main can resolve to a non-rendering / wrong camera, so taps
            // would miss the build grid. _camera is set in PullCameraBack().
            if (_camera == null) { _camera = ActiveScreenCamera(); if (_camera == null) return; }

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
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, _rayDistance, _groundMask)) return true;
            return Physics.Raycast(ray, out hit, _rayDistance, ~0);
        }

        /// <summary>Idle mode: a left-click on a PlacedStructure selects it for edit.</summary>
        private void UpdateSelectLoop()
        {
            if (!Input.GetMouseButtonDown(0)) return;

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

            // Right-click / Escape cancels the armed entry (keeps build mode open).
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelArmed();
                return;
            }

            // R rotates the ghost in 90° steps before placing (free).
            if (Input.GetKeyDown(KeyCode.R))
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

            if (valid && Input.GetMouseButtonDown(0))
                Place(cell, footprint, snapped);
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

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelMove();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
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

            if (valid && Input.GetMouseButtonDown(0))
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
            footprint = _grid.FootprintCells(
                entry != null && entry.repo != null && entry.repo.placement != null ? entry.repo.placement.footprint : 3f);

            // 1. Flat, upward-facing top (TowerPlacementSystem.IsValidSurface rule).
            if (hit.collider == null) return false;
            if (hit.normal.y < 0.85f) return false;
            if (hit.collider.CompareTag("Tower") || hit.collider.CompareTag("Building")) return false;

            // 2. Footprint cells free + in-bounds. (During a MOVE the structure's own
            //    cells were freed on enter, so they read as free and never self-block.)
            if (!_grid.CanPlace(cell, footprint)) return false;

            // 3. Gate-lane clearance — never wall off the spawn→Heart corridor.
            if (_gateClearance > 0f && IsTooCloseToGate(snapped)) return false;

            // 4. Affordable from the persisted multi-resource ledger (EconomyService —
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
            var data = new PlacedStructureData(_armed.id, cell.x, cell.y, _armedYawSteps, 1);

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
            if (_ghost == null) _ghost = new GameObject("GhostPreview").AddComponent<GhostPreview>();
            _ghost.SetEntry(entry);
        }

        private void CancelArmed()
        {
            _armed = null;
            _ghost?.Hide();
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

            var entry = CatalogRegistry.Get(ps.itemId);
            string label = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : ps.itemId;

            EnsureSelectionUi();
            _selectionUi?.Show(label, RefundFor(ps));
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
            var entry = CatalogRegistry.Get(_selected.itemId);
            string label = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : _selected.itemId;
            _selectionUi?.Show(label, RefundFor(_selected));
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
        /// The full ~50% multi-resource refund for a placed structure (each slot of its
        /// resolved build cost halved, rounded down). Crystals-only entries refund only
        /// Crystals (their buildCost fallback halved).
        /// </summary>
        private static DeNelle.Core.Catalog.ResourceCost RefundCostFor(PlacedStructure ps)
        {
            if (ps == null) return default;
            var cost = CostFor(CatalogRegistry.Get(ps.itemId));
            return new DeNelle.Core.Catalog.ResourceCost
            {
                wood     = cost.wood     / 2,
                food     = cost.food     / 2,
                iron     = cost.iron     / 2,
                crystals = cost.crystals / 2,
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
            _camera.transform.position = new Vector3(centre.x, _buildModeHeight, centre.z - 1f);
            _camera.transform.rotation = Quaternion.Euler(_buildModePitch, 0f, 0f);
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
        }

        private void EnsureSelectionUi()
        {
            if (_selectionUi != null) return;
            var go = new GameObject("BuildSelectionUI");
            _selectionUi = go.AddComponent<BuildSelectionUI>();
            _selectionUi.OnMoveRequested += BeginMoveSelected;
            _selectionUi.OnSellRequested += SellSelected;
            _selectionUi.OnCancelRequested += ClearSelection;
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
