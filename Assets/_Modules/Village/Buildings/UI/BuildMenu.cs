// =============================================================================
// BuildMenu — the village build-menu UI + tap-to-place flow (Week 4).
// -----------------------------------------------------------------------------
// Port spec Week 4 ("Village systems"): "Build menu (UI Toolkit): floating menu
// near the build cursor; tap-to-place at valid tiles. Costs in crystals."
//
// C# + UI Toolkit. The menu is CONTENT-driven: it builds one card per building
// from the canonical BuildingCatalog (data/buildings.json) — no building name,
// cost or HP is typed inline (port spec Part 4). Card names resolve through
// VillageStrings so the canon strings flow from canon-strings.json.
//
// Flow:
//   1. Open()                — shows the floating panel; cards list the five
//                              buildings with their crystal cost + HP.
//   2. Player picks a card   — that BuildingDef becomes the "armed" building;
//                              a ghost preview follows the build cursor.
//   3. Player taps the world — BuildMenu raycasts the ground, snaps to the
//                              nearest hex-tile centre, checks tile validity,
//                              and if affordable: deducts the crystal cost,
//                              instantiates the building prefab, calls
//                              Building.Configure(def).
//
// async-free: tap-to-place is frame-driven (Update), not an async flow.
//
// INTEGRATOR NOTE: VillageController owns scene wiring (per the task brief this
// file does NOT edit it). To make the menu live, the integrator must:
//   - add a UIDocument with BuildMenu.uxml + this component to the Village HUD,
//   - assign _buildCamera, _groundMask, _buildArea and a prefab per building
//     in _buildingPrefabs (matched by BuildingType),
//   - call Open() from the HUD's "Build" button and (optionally) RegisterBuilt
//     so VillageController.RegisterBuilding sees the new building.
// See docs/port-notes/week4-buildings.md.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// One building's placeable prefab, paired with its <see cref="BuildingType"/>.
    /// The build menu instantiates the prefab matched to the armed building's
    /// type. Set up in the inspector by the integrator.
    /// </summary>
    [Serializable]
    public struct BuildingPrefabEntry
    {
        [Tooltip("Which of the five canonical buildings this prefab is.")]
        public BuildingType Type;

        [Tooltip("The prefab instantiated when this building is placed. Should carry a Building component.")]
        public GameObject Prefab;
    }

    /// <summary>
    /// The village build-menu controller. Builds a card per building from the
    /// canonical <see cref="BuildingCatalog"/>, runs the floating-panel + tap-
    /// to-place flow, deducts crystals on a successful placement.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BuildMenu : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument hosting BuildMenu.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        [Header("Placement")]
        [Tooltip("Camera used to raycast the build cursor into the world. Defaults to Camera.main.")]
        [SerializeField] private Camera _buildCamera;

        [Tooltip("Layer mask for the buildable ground. The placement raycast hits only these layers.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Tooltip("Hex-tile edge-to-edge size (world units). Tap positions snap to this grid.")]
        [SerializeField, Min(0.1f)] private float _hexSize = 4f;

        [Tooltip("Half-extent (world units) of the square buildable area, centred on the Heart at origin.")]
        [SerializeField, Min(1f)] private float _buildAreaHalfExtent = 26f;

        [Tooltip("Minimum clear distance to any existing building / wall for a tile to be valid.")]
        [SerializeField, Min(0f)] private float _minClearRadius = 2.5f;

        [Tooltip("Optional ghost-preview prefab shown at the snapped tile while a building is armed.")]
        [SerializeField] private GameObject _ghostPrefab;

        [Header("Building prefabs (wired by the integrator)")]
        [Tooltip("One placeable prefab per BuildingType. The placed prefab should carry a Building component.")]
        [SerializeField] private List<BuildingPrefabEntry> _buildingPrefabs = new List<BuildingPrefabEntry>();

        [Header("Crystal balance")]
        [Tooltip("When true, the menu spends from GameState.Resources.Crystals via GameStateService. " +
                 "When false it spends from _localCrystalBalance below (standalone testing).")]
        [SerializeField] private bool _useGameState = true;

        [Tooltip("Crystal balance used when _useGameState is false (standalone testing only).")]
        [SerializeField, Min(0)] private int _localCrystalBalance = 500;

        // ── UXML element names — the binding contract with BuildMenu.uxml ────
        private const string PanelName = "build-menu-panel";
        private const string BalanceLabelName = "build-menu-balance";
        private const string ListName = "build-menu-list";
        private const string StatusLabelName = "build-menu-status";
        private const string CloseButtonName = "build-menu-close";

        // ── USS class names — styled by BuildMenu.uss ────────────────────────
        private const string CardClass = "build-card";
        private const string CardSelectedClass = "build-card--selected";
        private const string CardUnaffordableClass = "build-card--unaffordable";
        private const string CardNameClass = "build-card__name";
        private const string CardDescClass = "build-card__desc";
        private const string CardFooterClass = "build-card__footer";
        private const string CardCostClass = "build-card__cost";
        private const string CardCostUnaffordableClass = "build-card__cost--unaffordable";
        private const string CardHpClass = "build-card__hp";
        private const string StatusErrorClass = "build-menu-status--error";

        private VisualElement _root;
        private VisualElement _panel;
        private Label _balanceLabel;
        private VisualElement _list;
        private Label _statusLabel;
        private Button _closeButton;

        private bool _isOpen;
        private BuildingDef _armed;          // the building the player picked, or null
        private GameObject _ghost;           // live ghost-preview instance
        private bool _hasValidTile;          // is the cursor over a valid tile this frame?
        private Vector3 _snappedTile;        // snapped hex-tile centre under the cursor

        /// <summary>Raised when a building is successfully placed — carries the new Building + its def.</summary>
        public event Action<Building, BuildingDef> BuildingPlaced;

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>The currently-armed building definition, or null when nothing is picked.</summary>
        public BuildingDef ArmedBuilding => _armed;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            if (_buildCamera == null) _buildCamera = Camera.main;
        }

        private void OnEnable()
        {
            BindElements();
            // The menu starts hidden — the HUD's Build button calls Open().
            SetPanelVisible(false);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.clicked -= Close;
            DestroyGhost();
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return;

            _panel = _root.Q<VisualElement>(PanelName);
            _balanceLabel = _root.Q<Label>(BalanceLabelName);
            _list = _root.Q<VisualElement>(ListName);
            _statusLabel = _root.Q<Label>(StatusLabelName);
            _closeButton = _root.Q<Button>(CloseButtonName);

            if (_closeButton != null) _closeButton.clicked += Close;
        }

        // =====================================================================
        //  Open / Close
        // =====================================================================

        /// <summary>
        /// Opens the build menu — rebuilds the card list from the canonical
        /// catalogue and shows the floating panel. Called by the HUD's
        /// "Build" button.
        /// </summary>
        public void Open()
        {
            _isOpen = true;
            SetPanelVisible(true);
            Disarm();
            Render();
        }

        /// <summary>
        /// Closes the build menu, cancelling any in-progress placement.
        /// Bound to the panel's close button.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            Disarm();
            SetPanelVisible(false);
        }

        /// <summary>Toggles the menu open/closed.</summary>
        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null)
                _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // =====================================================================
        //  Rendering
        // =====================================================================

        /// <summary>Rebuilds every building card from the canonical catalogue.</summary>
        public void Render()
        {
            if (_list == null) return;
            _list.Clear();

            UpdateBalanceLabel();

            foreach (var def in BuildingCatalog.Buildings)
            {
                if (def == null) continue;
                _list.Add(BuildCard(def));
            }
        }

        private VisualElement BuildCard(BuildingDef def)
        {
            var balance = CrystalBalance;
            var affordable = balance >= def.CrystalCost;

            var card = new VisualElement { name = $"build-card-{def.Id}" };
            card.AddToClassList(CardClass);
            if (!affordable) card.AddToClassList(CardUnaffordableClass);
            if (_armed != null && _armed.Id == def.Id) card.AddToClassList(CardSelectedClass);

            // Name — resolved through VillageStrings (canon strings, never inline).
            var nameLabel = new Label(VillageStrings.BuildingName(def));
            nameLabel.AddToClassList(CardNameClass);
            card.Add(nameLabel);

            // Flavour description (en.json buildingDesc.*).
            var desc = VillageStrings.BuildingDescription(def);
            if (!string.IsNullOrEmpty(desc))
            {
                var descLabel = new Label(desc);
                descLabel.AddToClassList(CardDescClass);
                card.Add(descLabel);
            }

            // Footer — crystal cost + HP.
            var footer = new VisualElement();
            footer.AddToClassList(CardFooterClass);

            var cost = new Label($"◆ {def.CrystalCost}"); // ◆ crystal glyph
            cost.AddToClassList(CardCostClass);
            if (!affordable) cost.AddToClassList(CardCostUnaffordableClass);
            footer.Add(cost);

            var hp = new Label($"{Mathf.RoundToInt(def.MaxHp)} HP");
            hp.AddToClassList(CardHpClass);
            footer.Add(hp);

            card.Add(footer);

            // Tapping a card arms that building (only if affordable).
            card.RegisterCallback<ClickEvent>(_ => OnCardClicked(def));

            return card;
        }

        private void OnCardClicked(BuildingDef def)
        {
            if (CrystalBalance < def.CrystalCost)
            {
                SetStatus($"Not enough crystals for {VillageStrings.BuildingName(def)} " +
                          $"(needs {def.CrystalCost}).", isError: true);
                return;
            }

            // Toggle: tapping the armed card again disarms it.
            if (_armed != null && _armed.Id == def.Id)
            {
                Disarm();
                Render();
                return;
            }

            Arm(def);
            Render();
        }

        private void UpdateBalanceLabel()
        {
            if (_balanceLabel != null)
                _balanceLabel.text = $"◆ {CrystalBalance}";
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = message;
            _statusLabel.EnableInClassList(StatusErrorClass, isError);
        }

        // =====================================================================
        //  Arming a building
        // =====================================================================

        private void Arm(BuildingDef def)
        {
            _armed = def;
            SpawnGhost();
            SetStatus($"Tap a clear tile to raise {VillageStrings.BuildingName(def)}.");
        }

        private void Disarm()
        {
            _armed = null;
            _hasValidTile = false;
            DestroyGhost();
            SetStatus("Pick a building, then tap a clear tile to raise it.");
        }

        private void SpawnGhost()
        {
            DestroyGhost();
            if (_ghostPrefab == null) return;
            _ghost = Instantiate(_ghostPrefab);
            _ghost.name = "BuildMenu_Ghost";
            _ghost.SetActive(false);
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }

        // =====================================================================
        //  Tap-to-place — frame-driven
        // =====================================================================

        private void Update()
        {
            if (!_isOpen || _armed == null) return;

            // Track the build cursor and refresh the ghost preview.
            _hasValidTile = ResolveCursorTile(out _snappedTile, out var blockedReason);
            UpdateGhost();

            if (!_hasValidTile && !string.IsNullOrEmpty(blockedReason))
                SetStatus(blockedReason, isError: true);

            // A tap (mouse click or touch) commits the placement.
            if (TapPressedThisFrame() && !IsPointerOverUi())
            {
                if (_hasValidTile) TryPlace(_snappedTile);
                else SetStatus(string.IsNullOrEmpty(blockedReason)
                        ? "That tile is not buildable."
                        : blockedReason, isError: true);
            }
        }

        /// <summary>
        /// Raycasts the build cursor into the world, snaps the hit to the
        /// nearest hex-tile centre and validates the tile. Returns true and the
        /// snapped position when the tile is buildable; false + a reason when not.
        /// </summary>
        private bool ResolveCursorTile(out Vector3 tile, out string reason)
        {
            tile = Vector3.zero;
            reason = null;

            var cam = _buildCamera != null ? _buildCamera : Camera.main;
            if (cam == null) { reason = "No build camera."; return false; }

            var screen = PointerScreenPosition();
            var ray = cam.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 500f, _groundMask))
            {
                reason = "Aim at the ground to place a building.";
                return false;
            }

            tile = SnapToHex(hit.point);

            // Inside the buildable square (centred on the Heart at origin)?
            if (Mathf.Abs(tile.x) > _buildAreaHalfExtent || Mathf.Abs(tile.z) > _buildAreaHalfExtent)
            {
                reason = "Outside the village grounds.";
                return false;
            }

            // Clear of any existing building / wall?
            if (!IsTileClear(tile))
            {
                reason = "That tile is too close to another structure.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Snaps a world point to the nearest hex-tile centre. Pointy-top
        /// axial layout: rows offset by half a tile (the village's flat-ground
        /// hex grid). Y is taken from the snapped ground sample.
        /// </summary>
        private Vector3 SnapToHex(Vector3 world)
        {
            float row = Mathf.Round(world.z / _hexSize);
            // Odd rows are offset by half a tile — the classic hex stagger.
            float colOffset = (Mathf.Abs((int)row) % 2 == 1) ? _hexSize * 0.5f : 0f;
            float col = Mathf.Round((world.x - colOffset) / _hexSize);
            float x = col * _hexSize + colOffset;
            float z = row * _hexSize;
            return new Vector3(x, world.y, z);
        }

        /// <summary>True when no building / wall sits within <see cref="_minClearRadius"/> of the tile.</summary>
        private bool IsTileClear(Vector3 tile)
        {
            var hits = Physics.OverlapSphere(tile, _minClearRadius);
            foreach (var col in hits)
            {
                if (col == null) continue;
                if (col.GetComponentInParent<Building>() != null) return false;
                if (col.GetComponentInParent<WallSegment>() != null) return false;
                if (col.GetComponentInParent<Gate>() != null) return false;
            }
            return true;
        }

        private void UpdateGhost()
        {
            if (_ghost == null) return;
            if (_hasValidTile)
            {
                _ghost.SetActive(true);
                _ghost.transform.position = _snappedTile;
            }
            else
            {
                _ghost.SetActive(false);
            }
        }

        // =====================================================================
        //  Placement — instantiate + deduct crystals
        // =====================================================================

        /// <summary>
        /// Commits the armed building at the given tile: deducts the crystal
        /// cost, instantiates the matching prefab, calls
        /// <see cref="Building.Configure(BuildingDef)"/>, and disarms.
        /// </summary>
        private void TryPlace(Vector3 tile)
        {
            if (_armed == null) return;

            var cost = _armed.CrystalCost;
            if (CrystalBalance < cost)
            {
                SetStatus($"Not enough crystals (needs {cost}).", isError: true);
                return;
            }

            var prefab = PrefabFor(_armed.ResolvedType);
            if (prefab == null)
            {
                Debug.LogWarning($"[BuildMenu] No prefab wired for building type '{_armed.ResolvedType}'.");
                SetStatus("That building has no prefab yet.", isError: true);
                return;
            }

            // Deduct first so a failed spawn never gives a free building.
            if (!SpendCrystals(cost))
            {
                SetStatus("Could not spend crystals.", isError: true);
                return;
            }

            var go = Instantiate(prefab, tile, Quaternion.identity);
            go.name = $"Building_{_armed.Id}";

            var building = go.GetComponent<Building>();
            if (building == null) building = go.GetComponentInChildren<Building>();
            if (building != null)
            {
                building.Configure(_armed);
            }
            else
            {
                Debug.LogWarning($"[BuildMenu] Placed prefab for '{_armed.Id}' has no Building component.");
            }

            SetStatus($"{VillageStrings.BuildingName(_armed)} raised.");
            BuildingPlaced?.Invoke(building, _armed);

            // After a placement: disarm, then refresh the cards so the new
            // balance re-evaluates affordability.
            Disarm();
            Render();
        }

        private GameObject PrefabFor(BuildingType type)
        {
            foreach (var entry in _buildingPrefabs)
                if (entry.Type == type && entry.Prefab != null) return entry.Prefab;
            return null;
        }

        // =====================================================================
        //  Crystal balance — GameState-backed or local (standalone testing)
        // =====================================================================

        /// <summary>The current crystal balance the menu spends from.</summary>
        public int CrystalBalance
        {
            get
            {
                if (_useGameState)
                {
                    var service = GameStateService.Instance;
                    if (service != null && service.State != null)
                        return service.State.Resources.Crystals;
                    // No service in a standalone test — fall back to the local int.
                }
                return _localCrystalBalance;
            }
        }

        /// <summary>
        /// Spends <paramref name="amount"/> crystals. Mirrors the PackStore.cs
        /// pattern — mutates <see cref="GameState.Resources"/> (a struct, so it
        /// is written back whole), persists through <see cref="GameStateService.Save"/>,
        /// and raises the service's <c>ResourcesChanged</c> event so the HUD
        /// resource bar updates. Returns false when the balance is short.
        /// </summary>
        private bool SpendCrystals(int amount)
        {
            if (amount <= 0) return true;

            if (_useGameState)
            {
                var service = GameStateService.Instance;
                if (service != null && service.State != null)
                {
                    var r = service.State.Resources;
                    if (r.Crystals < amount) return false;
                    r.Crystals -= amount;
                    service.State.Resources = r;
                    service.Save();
                    service.ResourcesChanged.Invoke();
                    return true;
                }
                // Fall through to the local balance when there is no service.
            }

            if (_localCrystalBalance < amount) return false;
            _localCrystalBalance -= amount;
            return true;
        }

        // =====================================================================
        //  Input helpers — Unity Input System (port spec Part 2)
        // =====================================================================

        private static Vector2 PointerScreenPosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
        }

        private static bool TapPressedThisFrame()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            return false;
        }

        /// <summary>
        /// True when the pointer is over a UI Toolkit element of this document —
        /// so a tap on the build-menu panel is not also read as a world tap.
        /// </summary>
        private bool IsPointerOverUi()
        {
            if (_root == null || _root.panel == null) return false;
            var screen = PointerScreenPosition();
            // UI Toolkit panel space is top-left origin; screen space is bottom-left.
            var panelPos = RuntimePanelUtils.ScreenToPanel(
                _root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = _root.panel.Pick(panelPos);
            return picked != null && picked != _root;
        }
    }
}
