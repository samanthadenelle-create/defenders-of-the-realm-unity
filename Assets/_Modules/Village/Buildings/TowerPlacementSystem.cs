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

        private static readonly Color s_validColor   = new Color(0.2f, 0.9f, 0.3f, 0.45f);
        private static readonly Color s_invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.45f);

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
            // Camera.main is a tagged FindObjectWithTag lookup — cache it once.
            _mainCamera = Camera.main;
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
            if (_mainCamera == null) { _mainCamera = Camera.main; if (_mainCamera == null) return; }

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

            bool valid = IsValidSurface(hit) && CanPlace(pos);
            if (_markerRenderer != null)
            {
                _markerPropertyBlock.SetColor("_BaseColor", valid ? s_validColor : s_invalidColor);
                _markerRenderer.SetPropertyBlock(_markerPropertyBlock);
            }

            if (valid && Input.GetMouseButtonDown(0))
                PlaceTower(pos);
        }

        /// <summary>
        /// True when the cursor hit is buildable ground: a near-flat top surface that
        /// is not another tower/building. The ray hits any collider on the Default
        /// layer (rooftops, props, slopes), so without this check a tower could be
        /// placed in the air on top of whatever the cursor happened to be over.
        /// </summary>
        private static bool IsValidSurface(RaycastHit hit)
        {
            if (hit.collider == null) return false;
            if (hit.normal.y < 0.85f) return false;   // not a flat, upward-facing top
            if (hit.collider.CompareTag("Tower") || hit.collider.CompareTag("Building"))
                return false;                          // standing on a structure
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
        private bool CanPlace(Vector3 pos)
        {
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
                    return false;
            }

            if (SkillSystem.Instance == null ||
                !SkillSystem.Instance.HasRequiredSkill(_selectedTower.requiredSkill))
                return false;

            int count = Physics.OverlapSphereNonAlloc(pos, _overlapRadius, _overlapBuffer, _towerBuildingLayer);
            for (int i = 0; i < count; i++)
            {
                var c = _overlapBuffer[i];
                if (c == null) continue;
                if (c.CompareTag("Tower") || c.CompareTag("Building")) return false;
            }
            return true;
        }

        /// <summary>Spend the cost (unless prepaid) and hand the build to the construction queue (DEF-76).</summary>
        private void PlaceTower(Vector3 pos)
        {
            if (_selectedTower == null) return;

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
            if (TowerConstructionQueue.Instance != null)
                TowerConstructionQueue.Instance.AddToQueue(_selectedTower, pos);

            OnTowerPlaced?.Invoke(_selectedTower);
            CancelPlacing();   // one tower per StartPlacing; HUD re-arms for the next
        }

        // --- Ghost marker construction (transparent URP cylinder) ----------------
        private GameObject BuildMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "TowerPlacementMarker";
            marker.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            // The ghost must never block its own overlap test or catch the cursor ray.
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = marker.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = BuildTransparentMaterial();
            return marker;
        }

        /// <summary>A transparent URP material for the ghost (falls back if URP absent).</summary>
        private static Material BuildTransparentMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);

            // Flip URP/Lit to transparent so the green/red tint reads as a ghost.
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);             // 0 opaque / 1 transparent
                mat.SetFloat("_Blend", 0f);               // alpha blend
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", s_validColor);
            return mat;
        }
    }
}
