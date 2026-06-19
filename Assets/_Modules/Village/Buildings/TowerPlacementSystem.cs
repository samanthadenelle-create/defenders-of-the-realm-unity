// =============================================================================
// TowerPlacementSystem — DEF-73 (Linear). Singleton that drives the tower-build
// loop: Build HUD calls StartPlacing(TowerData) → a green/red ghost marker tracks
// the cursor on the ground → a valid left-click spawns + initializes the tower and
// spends its cost.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village (DEF-73 CP1 Issue 4). Lives in the Buildings folder
// inside the existing DeNelle.Village asmdef, which already references DeNelle.Core
// (TowerData) — so no new asmdef. EconomyService (DEF-78) and SkillSystem (DEF-73)
// are reached through their Instance singletons.
//
// Correction Pass 1 applied in full:
//   • Full singleton guard + OnDestroy clear (Issue 5).
//   • Camera.main cached in Start() into _mainCamera (Issue 6).
//   • Marker Renderer + MaterialPropertyBlock cached at StartPlacing (Issues 7, 8)
//     — colour set via _mpb.SetColor("_BaseColor", ...) + SetPropertyBlock, never
//     marker.material.color (which would leak a Material instance per frame).
//   • Overlap via Physics.OverlapSphereNonAlloc into a pre-allocated buffer with a
//     cached "Tower"/"Building" layer mask (Issue 9).
//   • LEGACY input only: Input.GetMouseButtonDown(0) / Input.mousePosition
//     (Clarifications Confirmed) — NOT UnityEngine.InputSystem.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Data;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Progression;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>Drives tower ghost-placement and spawning. Singleton.</summary>
    public class TowerPlacementSystem : MonoBehaviour
    {
        public static TowerPlacementSystem Instance;

        /// <summary>Fired when a tower is placed (queued for construction) — arg = its data.</summary>
        public event Action<TowerData> OnTowerPlaced;

        [Header("Placement")]
        [SerializeField] private float _gridSize = 1f;          // snap step in metres
        [SerializeField] private float _overlapRadius = 1.8f;   // build-clearance radius
        [SerializeField] private float _rayDistance = 500f;
        [SerializeField] private LayerMask _groundMask = ~0;    // what the cursor ray hits

        private Camera _mainCamera;

        // --- Ghost marker (cached at StartPlacing — never queried per frame) -----
        private TowerData _selectedTower;
        private GameObject _currentMarker;
        private Renderer _markerRenderer;
        private MaterialPropertyBlock _markerPropertyBlock;
        private bool _placing;

        // WO-131 — when true, the caller (BuildMenu.OnConfirmBuild) ALREADY deducted
        // the crystal cost from GameState.Resources.Crystals (the single spend site),
        // so this system must NOT charge again. If the player cancels the placement
        // before landing it, the prepaid cost is REFUNDED to the same store.
        private bool _prepaid;
        private int  _prepaidCost;

        // --- Overlap test (pre-allocated; no per-frame GC) -----------------------
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private int _towerBuildingLayer;

        // Placement GUIDE colours (not a solid object): a thin, translucent green
        // ground-shadow ring when valid, dimmed red when blocked. ~35% alpha so it
        // reads as a subtle projected guide rather than a filled disc.
        private static readonly Color s_validColor   = new Color(0.30f, 0.95f, 0.40f, 0.35f);
        private static readonly Color s_invalidColor = new Color(0.90f, 0.25f, 0.25f, 0.35f);

        // Ring geometry — the guide footprint radius + how flat it hugs the ground.
        private const float MarkerRadius   = 1.6f;     // matches the old disc footprint
        private const int   MarkerSegments = 48;       // smoothness of the ring
        private const float MarkerWidth    = 0.08f;    // thinnest readable line weight
        private const float MarkerGroundLift = 0.03f;  // tiny lift to avoid z-fighting the ground

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // LayerMask.GetMask returns 0 for names that don't exist; that's a safe
            // "match nothing" so overlap simply never blocks if the layers are absent.
            _towerBuildingLayer = LayerMask.GetMask("Tower", "Building");
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            // DEF-117 — resolve the camera that is ACTUALLY on screen (highest-depth
            // enabled screen camera), not the tag-based Camera.main, which can resolve
            // to a rogue / embedded camera and cast the placement ray from the wrong
            // view so taps miss the ground.
            _mainCamera = ActiveScreenCamera();
        }

        /// <summary>
        /// DEF-117 — the camera the player sees: the enabled, screen-bound (no
        /// targetTexture) camera with the highest depth. Mirrors the game camera's
        /// own sole-camera rule; falls back to Camera.main.
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

        /// <summary>
        /// Begin placing <paramref name="data"/>: spawns the ghost marker.
        /// </summary>
        /// <param name="data">The tower type to place.</param>
        /// <param name="prepaid">
        /// WO-131 — true when the caller already deducted the crystal cost
        /// (BuildMenu.OnConfirmBuild is the single spend site). When prepaid, this
        /// system neither charges on placement nor gates the cursor on affordability;
        /// it only validates geometry/skill. A cancelled prepaid placement is refunded.
        /// When false (legacy / direct callers), the system charges
        /// <see cref="TowerData.cost"/> on placement as before — now routed through
        /// the unified GameState crystal store rather than the old Wood pool.
        /// </param>
        public void StartPlacing(TowerData data, bool prepaid = false)
        {
            if (data == null) return;
            CancelPlacing();   // drop any in-flight marker first (refunds nothing — _prepaid cleared)

            _selectedTower = data;
            _placing = true;
            _prepaid = prepaid;
            _prepaidCost = prepaid ? data.cost : 0;

            _currentMarker = BuildMarker();
            _markerRenderer = _currentMarker.GetComponentInChildren<Renderer>();
            _markerPropertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Abort the current placement and tear down the ghost marker. If the cost
        /// was prepaid by the caller (WO-131) and never spent on a tower, REFUND it
        /// to the same GameState crystal store so a cancelled build is free.
        /// </summary>
        public void CancelPlacing()
        {
            if (_prepaid && _prepaidCost > 0)
            {
                // Refund to the single source of truth (Resources.Crystals).
                GameStateService.Instance?.AddCrystals(_prepaidCost);
            }
            _prepaid = false;
            _prepaidCost = 0;

            if (_currentMarker != null) Destroy(_currentMarker);
            _currentMarker = null;
            _markerRenderer = null;
            _selectedTower = null;
            _placing = false;
        }

        private void Update()
        {
            if (!_placing || _selectedTower == null || _currentMarker == null) return;
            // Re-resolve each frame while null AND refresh if the cached camera was
            // disabled (DEF-117) so the ray always comes from the on-screen camera.
            if (_mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy)
            {
                _mainCamera = ActiveScreenCamera();
                if (_mainCamera == null) return;
            }

            // Right-click cancels (legacy input).
            if (Input.GetMouseButtonDown(1)) { CancelPlacing(); return; }

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _rayDistance, _groundMask))
            {
                // Cursor off the ground — hide and bail.
                _currentMarker.SetActive(false);
                return;
            }
            _currentMarker.SetActive(true);

            Vector3 pos = SnapToGrid(hit.point);
            _currentMarker.transform.position = pos;

            BuildRejectReason reason;
            bool valid = IsValidSurface(hit, out reason) && CanPlace(pos, out reason);
            Color guideColor = valid ? s_validColor : s_invalidColor;
            if (_markerRenderer != null)
            {
                _markerPropertyBlock.SetColor("_BaseColor", guideColor);
                _markerPropertyBlock.SetColor("_Color", guideColor);
                _markerRenderer.SetPropertyBlock(_markerPropertyBlock);
            }
            // LineRenderer vertex colours modulate the material — set them too so the
            // green/red guide tint is reliable regardless of the resolved shader.
            if (_markerRenderer is LineRenderer lr)
            {
                lr.startColor = guideColor;
                lr.endColor = guideColor;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (valid) PlaceTower(pos);
                else SurfaceRejection(reason);   // WO-394 — never fail a click silently
            }
        }

        /// <summary>
        /// WO-394 — pop the build-feedback toast for a rejected tower click. For an
        /// unaffordable tower it names the crystal shortfall (the tower cost is Crystals);
        /// other reasons use the shared default message.
        /// </summary>
        private void SurfaceRejection(BuildRejectReason reason)
        {
            if (reason == BuildRejectReason.CannotAfford && _selectedTower != null)
                BuildFeedbackToast.Show($"Not enough Crystals ({_selectedTower.cost})");
            else
                BuildFeedbackToast.Show(reason);
        }

        /// <summary>
        /// True when the cursor hit is buildable ground: a near-flat top surface that
        /// is not another tower/building. The ray hits any collider on the Default
        /// layer (rooftops, props, slopes), so without this check a tower could be
        /// placed in the air on top of whatever the cursor happened to be over.
        /// </summary>
        private static bool IsValidSurface(RaycastHit hit, out BuildRejectReason reason)
        {
            reason = BuildRejectReason.Generic;
            if (hit.collider == null) { reason = BuildRejectReason.BadSurface; return false; }
            if (hit.normal.y < 0.85f) { reason = BuildRejectReason.BadSurface; return false; }   // not a flat, upward-facing top
            if (hit.collider.CompareTag("Tower") || hit.collider.CompareTag("Building"))
            { reason = BuildRejectReason.Occupied; return false; }                                // standing on a structure
            return true;
        }

        /// <summary>Snap a world point to the build grid (keeps Y from the hit).</summary>
        private Vector3 SnapToGrid(Vector3 p)
        {
            if (_gridSize <= 0f) return p;
            return new Vector3(
                Mathf.Round(p.x / _gridSize) * _gridSize,
                p.y,
                Mathf.Round(p.z / _gridSize) * _gridSize);
        }

        /// <summary>Valid = affordable AND skill-gated AND no tower/building overlap.</summary>
        private bool CanPlace(Vector3 pos, out BuildRejectReason reason)
        {
            reason = BuildRejectReason.Generic;
            if (_selectedTower == null) return false;

            // WO-131 — affordability is gated by the SINGLE crystal source of truth
            // (GameState.Resources.Crystals). When the cost was prepaid by the caller
            // (BuildMenu), it was already validated + deducted there, so skip the
            // affordability gate here (the crystals are spent — re-checking the
            // now-lower balance would wrongly reject the placement).
            if (!_prepaid)
            {
                // Single source of truth: GameState.Resources.Crystals — the SAME
                // store the BuildMenu and village HUD display. (CrystalEconomy targets
                // the separate AetherCrystals field, which the build HUD does not show,
                // so it is NOT used for build affordability.)
                var svc   = GameStateService.Instance;
                var state = svc != null ? svc.State : null;
                if (state == null || state.Resources.Crystals < _selectedTower.cost)
                { reason = BuildRejectReason.CannotAfford; return false; }
            }

            if (SkillSystem.Instance == null ||
                !SkillSystem.Instance.HasRequiredSkill(_selectedTower.requiredSkill))
            { reason = BuildRejectReason.Locked; return false; }   // a prerequisite skill/unlock isn't met

            int count = Physics.OverlapSphereNonAlloc(pos, _overlapRadius, _overlapBuffer, _towerBuildingLayer);
            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (c == null) continue;
                if (c.CompareTag("Tower") || c.CompareTag("Building"))
                { reason = BuildRejectReason.Occupied; return false; }
            }
            return true;
        }

        /// <summary>Spend the cost (unless prepaid) and hand the build to the construction queue (DEF-76).</summary>
        private void PlaceTower(Vector3 pos)
        {
            using var _ = FlowTrace.Enter("TowerPlace", $"PlaceTower pos={pos} tower='{_selectedTower?.towerName ?? "<null>"}'");
            if (_selectedTower == null)
            {
                FlowTrace.Fail("TowerPlace", "PlaceTower: _selectedTower is null — nothing to place (build dropped).");
                return;
            }

            // WO-131 — crystal spend routes through the single source of truth:
            // GameState.Resources.Crystals (the store the HUD + BuildMenu display).
            // When prepaid, the caller (BuildMenu) already deducted it — do NOT
            // charge again. Otherwise re-check + deduct atomically here.
            if (!_prepaid)
            {
                var svc   = GameStateService.Instance;
                var state = svc != null ? svc.State : null;
                if (state == null || state.Resources.Crystals < _selectedTower.cost)
                    return;   // can't afford on this frame — reject without spending
                svc.AddCrystals(-_selectedTower.cost);   // negative = spend; persisted + HUD-synced
            }

            // The cost is now consumed by a real placement — clear the prepaid flag
            // BEFORE CancelPlacing() so the teardown does NOT refund a placed tower.
            _prepaid = false;
            _prepaidCost = 0;

            // DEF-76 — towers no longer pop in instantly. The queue raises them over
            // buildTime (scaffolding + worker VFX + progress bar) and calls
            // Tower.Initialize on completion. The queue self-bootstraps, so Instance
            // is non-null at runtime; guard anyway.
            // §12 / VERIFY: the construction queue self-bootstraps so Instance should be
            // non-null at runtime. If it IS null, the placed tower is SILENTLY LOST (the
            // crystals were spent / prepaid but no tower is ever queued or built — the audit
            // gap). FAIL loudly so a "paid but no tower appeared" report self-reports here.
            var queue = TowerConstructionQueue.Instance;
            if (queue != null)
            {
                queue.AddToQueue(_selectedTower, pos);
                FlowTrace.Step("TowerPlace",
                    $"PlaceTower: queued '{_selectedTower.towerName}' (pending={queue.PendingCount}, building={queue.IsBuilding}).");
            }
            else
            {
                FlowTrace.Fail("TowerPlace",
                    $"PlaceTower: TowerConstructionQueue.Instance is NULL — tower '{_selectedTower.towerName}' " +
                    "was NOT queued (cost spent, no tower will build). Construction queue bootstrap did not run.");
            }

            // DEF-183: tower-place confirm "thunk" (via CoreServices.Audio, guarded).
            GameSfx.PlayTowerPlace();

            OnTowerPlaced?.Invoke(_selectedTower);
            CancelPlacing();   // one tower per StartPlacing; HUD re-arms for the next
        }

        // --- Placement guide construction (thin ground-projected ring) -----------
        // RESTYLE — the marker used to be a SOLID translucent cylinder DISC, which
        // read as a real object lying on the ground. The placement indicator should
        // be a subtle GUIDE, not an object: a thin, translucent green line-weight ring
        // projected flat on the ground (a "footprint shadow"). Built from a LineRenderer
        // looped into a circle that hugs the ground plane. The renderer is still tinted
        // green/red each frame via the existing MaterialPropertyBlock path (Update), so
        // the valid/blocked feedback is preserved — only the look changed.
        private GameObject BuildMarker()
        {
            var marker = new GameObject("TowerPlacementGuide");

            var lr = marker.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;                 // points are local → moves with the marker
            lr.loop = true;                           // closed ring
            lr.alignment = LineAlignment.TransformZ;  // lie flat in the local XZ plane
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
            lr.widthMultiplier = MarkerWidth;         // thinnest readable line weight
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = MarkerSegments;

            // Orient the ring flat on the ground: rotate the host so the LineRenderer's
            // local XZ ring lies in the world XZ plane, lifted a hair to avoid z-fighting.
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Lay out the circle points in the local plane.
            for (int i = 0; i < MarkerSegments; i++)
            {
                float a = (i / (float)MarkerSegments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * MarkerRadius,
                                              Mathf.Sin(a) * MarkerRadius,
                                              -MarkerGroundLift));
            }

            lr.sharedMaterial = BuildGuideMaterial();
            return marker;
        }

        /// <summary>
        /// A transparent, unlit URP material for the ground guide ring (falls back if
        /// URP absent). Unlit so the guide is a flat, lighting-independent shadow line;
        /// alpha-blended so the ~35% green/red tint reads as a subtle projected guide.
        /// </summary>
        private static Material BuildGuideMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);             // 0 opaque / 1 transparent
                mat.SetFloat("_Blend", 0f);               // alpha blend
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", s_validColor);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", s_validColor);
            return mat;
        }
    }
}
