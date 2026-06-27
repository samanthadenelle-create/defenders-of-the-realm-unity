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
using UnityEngine.UIElements;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
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

        // ── WO-31: multi-screen build/upgrade flow ───────────────────────────
        // The menu now has three screens: a Root chooser (Build Tower / Upgrade
        // Tower / Repair Wall), a Build-Tower screen (element radio + cost +
        // timing), and an Upgrade-Tower screen (placed-tower list + upgrade info).
        private enum MenuScreen { Root, BuildTower, UpgradeTower }
        private MenuScreen _screen = MenuScreen.Root;

        // Local element enum — deliberately NOT DeNelle.BattleATB's ElementType:
        // DeNelle.Village must not take a dependency on BattleATB (WO-31 §Files).
        private enum TowerElement { Flame, Ice, Aether, Physical }
        private TowerElement _selectedElement = TowerElement.Flame;
        // WO-127: the manage/upgrade screen tracks a LIVE Tower (not a Building) so
        // its level reads correctly and the Upgrade button calls Tower.Upgrade().
        private Tower _selectedTowerForUpgrade;

        // USS classes for the WO-31 sub-screens (styled in BuildMenu.uss).
        private const string OptionTileClass = "build-option-tile";
        private const string BackBtnClass = "build-menu-back-btn";
        private const string RadioGroupClass = "element-radio-group";
        private const string RadioRowClass = "element-radio-row";
        private const string RadioRowSelectedClass = "element-radio-row--selected";
        private const string CostRowClass = "cost-row";
        private const string CostCheckClass = "cost-check";
        private const string CostFailClass = "cost-fail";
        private const string ConfirmBtnClass = "build-confirm-btn";
        private const string TowerRowClass = "tower-row";
        private const string TowerRowSelectedClass = "tower-row--selected";

        /// <summary>One element tower's stub costs/timing (WO-31 — Week 6 moves these to JSON).</summary>
        private struct TowerVariantDef
        {
            public TowerElement Element;
            public string DisplayName;
            public int CrystalCost;
            public int Wood, Stone;
            public int BuildTimeSec;
            public int UpgradeCrystalCost, UpgradeStone, UpgradeTimeSec;
            public int Dps, Hp;
        }

        // STUB — Week 6: hard-coded variant table until tower-variants.json lands.
        private static readonly TowerVariantDef[] Variants =
        {
            new TowerVariantDef { Element = TowerElement.Flame,    DisplayName = "Flame Tower",  CrystalCost = 150, Wood = 20, Stone = 5,  BuildTimeSec = 150, UpgradeCrystalCost = 200, UpgradeStone = 15, UpgradeTimeSec = 300, Dps = 30, Hp = 200 },
            new TowerVariantDef { Element = TowerElement.Ice,      DisplayName = "Ice Tower",    CrystalCost = 150, Wood = 20, Stone = 5,  BuildTimeSec = 150, UpgradeCrystalCost = 200, UpgradeStone = 15, UpgradeTimeSec = 300, Dps = 26, Hp = 220 },
            new TowerVariantDef { Element = TowerElement.Aether,   DisplayName = "Aether Tower", CrystalCost = 180, Wood = 20, Stone = 8,  BuildTimeSec = 180, UpgradeCrystalCost = 240, UpgradeStone = 18, UpgradeTimeSec = 360, Dps = 34, Hp = 190 },
            new TowerVariantDef { Element = TowerElement.Physical, DisplayName = "Stone Tower",  CrystalCost = 120, Wood = 15, Stone = 10, BuildTimeSec = 120, UpgradeCrystalCost = 160, UpgradeStone = 12, UpgradeTimeSec = 240, Dps = 24, Hp = 260 },
        };

        /// <summary>Raised when a building is successfully placed — carries the new Building + its def.</summary>
        public event Action<Building, BuildingDef> BuildingPlaced;

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => _isOpen;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            EnsurePanelSettings();
        }

        /// <summary>
        /// The builder creates this UIDocument without a PanelSettings asset, which
        /// leaves the menu INVISIBLE when Open() runs — so the HUD "Build" button
        /// appeared to "do nothing" (it opened a panel that never rendered). Borrow
        /// the scene's PanelSettings (e.g. the village HUD's) and sit above it.
        /// No-op if a PanelSettings is already assigned. (Mirrors HelpMenu.)
        /// </summary>
        private void EnsurePanelSettings()
        {
            if (_document == null) return;
            // Owner 2026-05-25 ("nothing triggers on build"): Open() fired but the
            // menu never rendered. The builder bakes the FIRST project PanelSettings
            // onto this doc (WirePanelSettings), which can render off-screen / at the
            // wrong scale. ALWAYS adopt a live sibling's PanelSettings instead —
            // preferring the HUD's — so the menu renders on the same on-screen panel
            // as everything else. (Awake runs before OnEnable's BindElements, so the
            // root reflects the correct panel by the time we query it.)
            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", System.StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                if (_document.panelSettings != src.panelSettings)
                    Debug.Log("[BuildMenu] Adopted PanelSettings '" + src.panelSettings.name + "' from '" +
                              src.name + "' (was " + (_document.panelSettings != null ? _document.panelSettings.name : "NULL") + ").");
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder = src.sortingOrder + 5; // render above the HUD
            }
            else if (_document.panelSettings == null)
            {
                Debug.LogWarning("[BuildMenu] No sibling PanelSettings found — build menu will not render.");
            }
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
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
        }

        /// <summary>Re-render the open menu when crystals change (dev grant, wave reward).</summary>
        private void OnResourcesChanged()
        {
            if (_isOpen) Render();
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

            // Start pass-through so the closed menu's root never blocks HUD clicks.
            _root.pickingMode = PickingMode.Ignore;
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
            // FIX (owner 2026-05-25 "build does nothing"): the runtime log showed
            // panel=False — BindElements() ran in OnEnable BEFORE the UIDocument
            // finished cloning its UXML (component order on this GameObject), so
            // _panel was null and SetPanelVisible had nothing to show. By the time
            // Open() is clicked the document is fully built, so re-bind here.
            if (_panel == null) BindElements();
            // DIAG: dump the render prereqs (panel should now be True).
            Debug.Log("[BuildMenu] Open: doc=" + (_document != null) +
                      " panelSettings=" + (_document != null && _document.panelSettings != null ? _document.panelSettings.name : "NULL") +
                      " sort=" + (_document != null ? _document.sortingOrder : -1) +
                      " uxml=" + (_document != null && _document.visualTreeAsset != null ? _document.visualTreeAsset.name : "NULL") +
                      " root=" + (_root != null) + " panel=" + (_panel != null) + " list=" + (_list != null));

            // KNOWN BUILD ISSUE (uxml-uidocuments-dont-render-in-builds): in player
            // builds BuildMenu.uxml does not clone, so _panel/_list are null and the
            // menu opens EMPTY. Fall back to driving the new TowerPlacementSystem
            // directly, so the HUD Build button still arms the (viking) tower. The
            // full code-built menu UI is the follow-up; this makes Build work today.
            if (_panel == null)
            {
                // UXML didn't clone (the player-build issue the Player.log confirmed:
                // panel=False). CODE UIElements DO render in builds, so show a small
                // code-built window instead of the empty .uxml one.
                ShowCodeFallbackMenu();
                return;
            }

            SetPanelVisible(true);
            _screen = MenuScreen.Root;            // always open on the chooser
            _selectedTowerForUpgrade = null;
            Disarm();
            Render();

            // Live-refresh the balance + affordability while the menu is open if
            // crystals change (a dev grant, a wave reward). Remove-then-add guards
            // against a double subscription.
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
                svc.ResourcesChanged.AddListener(OnResourcesChanged);
            }
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
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
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

            // NON-MODAL (owner 2026-05-26 "click the build button -> everything else
            // stops working"): this UIDocument's full-screen ROOT renders ABOVE the
            // HUD. Flipping it to PickingMode.Position while open made it a screen-
            // wide modal that SWALLOWED every world/HUD/camera click — and worse, ate
            // the world tile-taps needed to actually place a tower. Keep the root
            // click-through (Ignore) at ALL times: PickingMode.Ignore affects only
            // the root itself, NOT its descendants, so the _panel and its buttons
            // (default PickingMode.Position) still receive clicks, while the
            // transparent area outside the panel passes through to the world + HUD.
            if (_root != null)
                _root.pickingMode = PickingMode.Ignore;
        }

        // =====================================================================
        //  Code-built fallback menu — renders in player builds, unlike the .uxml
        //  (which clones empty: panel=False in the Player.log). Built straight into
        //  _root with code UIElements. Replace with a full code menu in DEF-86.
        // =====================================================================
        private VisualElement _codePanel;

        private void ShowCodeFallbackMenu()
        {
            if (_root == null) return;
            if (_codePanel == null)
            {
                _codePanel = new VisualElement { name = "buildmenu-code-fallback" };
                var s = _codePanel.style;
                s.position = Position.Absolute;
                // DEF-134: was left:20, top:90 — that overlapped the HUD's top-left
                // column (the "⊕ Skills" button sits at left:16, top:110, and the
                // vitals cards above it), so the menu covered the Skills control.
                // Drop it BELOW that column (skills ends ~top:150) and indent it so it
                // clears the left HUD chrome while staying well left of the bottom-
                // right ability diamond + Build button cluster.
                s.left = 16f; s.top = 158f; s.minWidth = 220f;
                s.paddingTop = 14f; s.paddingBottom = 14f; s.paddingLeft = 16f; s.paddingRight = 16f;
                // Dark stone glass + runic-gold rim + rounding, from the shared town-HUD
                // palette (ElarionUi) so the fallback reads as one designed UI.
                ElarionUi.StylePanel(_codePanel, dark: true);

                var title = new Label("Build");
                title.style.color = ElarionUi.Gilt; title.style.fontSize = ElarionUi.FontHead;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.letterSpacing = 1.5f; title.style.marginBottom = 10f;
                _codePanel.Add(title);

                _codePanel.Add(CodeMenuButton("Build Tower", ElarionUi.ButtonKind.Gold, () =>
                {
                    var t = Resources.Load<DeNelle.Core.Data.TowerData>("Towers/DevTower");
                    if (t != null)
                    {
                        if (TowerPlacementSystem.Instance == null)
                            new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();
                        TowerPlacementSystem.Instance.StartPlacing(t);
                        SetStatus("Click a clear tile to raise the tower.");
                    }
                    HideCodeFallbackMenu();
                }));

                _codePanel.Add(CodeMenuButton("Upgrade Last Tower", ElarionUi.ButtonKind.Confirm, () =>
                {
                    var towers = UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
                    if (towers.Length > 0)
                    {
                        var t = towers[towers.Length - 1];
                        bool ok = t.Upgrade();
                        SetStatus(ok ? $"Upgraded the last tower to L{t.CurrentLevel}." : "Tower already at max level.");
                    }
                    else SetStatus("No towers to upgrade.");
                    HideCodeFallbackMenu();
                }));

                _codePanel.Add(CodeMenuButton("Raze Last Tower", ElarionUi.ButtonKind.Danger, () =>
                {
                    var towers = UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
                    if (towers.Length > 0) { Destroy(towers[towers.Length - 1].gameObject); SetStatus("Razed the last tower."); }
                    else SetStatus("No towers to raze.");
                    HideCodeFallbackMenu();
                }));

                _codePanel.Add(CodeMenuButton("Manage Towers", ElarionUi.ButtonKind.Neutral, () =>
                {
                    HideCodeFallbackMenu();
                    DeNelle.Village.UI.TowerManagerPanel.Instance?.Show();   // or press M
                }));

                // WO-108 — the CREATE verb: enter the player Build Mode (catalog
                // palette + grid placement + persisted BaseLayout). EnsureExists()
                // self-installs the controller; Enter() freezes waves + shows the palette.
                _codePanel.Add(CodeMenuButton("Build Mode", ElarionUi.ButtonKind.Neutral, () =>
                {
                    HideCodeFallbackMenu();
                    BuildModeController.EnsureExists().Enter();
                }));

                _codePanel.Add(CodeMenuButton("Close", ElarionUi.ButtonKind.Neutral, HideCodeFallbackMenu));

                _root.Add(_codePanel);
            }
            _codePanel.style.display = DisplayStyle.Flex;
            _isOpen = true;
        }

        private void HideCodeFallbackMenu()
        {
            if (_codePanel != null) _codePanel.style.display = DisplayStyle.None;
            _isOpen = false;
        }

        private static Button CodeMenuButton(string label, ElarionUi.ButtonKind kind, System.Action onClick)
        {
            var b = new Button(onClick) { text = label };
            // Shared themed button (stone/gold/green/red fill + rim + rounding + hover/press
            // feedback) from the town-HUD palette, so the fallback matches the canon look.
            ElarionUi.StyleButton(b, kind);
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.marginTop = 4f; b.style.marginBottom = 4f;
            return b;
        }

        // =====================================================================
        //  Rendering
        // =====================================================================

        /// <summary>
        /// Rebuilds the menu for the current <see cref="_screen"/>. WO-31 turned
        /// the flat two-card menu into a three-screen flow: Root chooser →
        /// Build-Tower (element radio + costs + timing) or Upgrade-Tower
        /// (placed-tower list + upgrade info). All screens build into the same
        /// runtime <c>build-menu-list</c> container — no UXML structure change.
        /// </summary>
        public void Render()
        {
            if (_list == null) return;
            _list.Clear();
            UpdateBalanceLabel();

            switch (_screen)
            {
                case MenuScreen.BuildTower:   RenderBuildTower();   break;
                case MenuScreen.UpgradeTower: RenderUpgradeTower(); break;
                default:                      RenderRoot();         break;
            }
        }

        // ── Root chooser ─────────────────────────────────────────────────────

        /// <summary>
        /// The top-level chooser: three big tiles — Build Tower, Upgrade Tower,
        /// Repair Wall. (Owner WO-31: "dialog or option to select tower or build
        /// tower … maybe upgrade tower popup".)
        /// </summary>
        private void RenderRoot()
        {
            _list.Add(BuildOptionTile("🏗  Build Tower",
                "Raise a new elemental defence tower.",
                () => { _screen = MenuScreen.BuildTower; Render(); }));

            _list.Add(BuildOptionTile("⬆  Upgrade Tower",
                "Spend crystals + stone to level up a placed tower.",
                () => { _screen = MenuScreen.UpgradeTower; _selectedTowerForUpgrade = null; Render(); }));

            _list.Add(BuildRepairCard());
        }

        /// <summary>A large tappable tile used by the Root chooser.</summary>
        private VisualElement BuildOptionTile(string title, string subtitle, Action onClick)
        {
            var tile = new Button(() => onClick?.Invoke());
            tile.AddToClassList(OptionTileClass);

            var t = new Label(title);
            t.AddToClassList(CardNameClass);
            tile.Add(t);

            var s = new Label(subtitle);
            s.AddToClassList(CardDescClass);
            s.style.whiteSpace = WhiteSpace.Normal;
            tile.Add(s);

            return tile;
        }

        /// <summary>The "← Back" row shown on every sub-screen; returns to Root.</summary>
        private VisualElement BuildBackButton()
        {
            var back = new Button(() => { _screen = MenuScreen.Root; _selectedTowerForUpgrade = null; Disarm(); Render(); })
            {
                text = "← Back"
            };
            back.AddToClassList(BackBtnClass);
            return back;
        }

        // ── Build Tower screen ────────────────────────────────────────────────

        /// <summary>
        /// Element radio (Flame / Ice / Aether / Physical) + the selected
        /// variant's crystal + material costs (each with a ✓/✗) + build time +
        /// a Build button greyed out when anything is unaffordable.
        /// </summary>
        private void RenderBuildTower()
        {
            _list.Add(BuildBackButton());

            var heading = new Label("BUILD TOWER");
            heading.AddToClassList(CardNameClass);
            _list.Add(heading);

            var elementLabel = new Label("Element:");
            elementLabel.AddToClassList(CardDescClass);
            _list.Add(elementLabel);

            var radioGroup = new VisualElement();
            radioGroup.AddToClassList(RadioGroupClass);
            foreach (TowerElement el in new[] { TowerElement.Flame, TowerElement.Ice,
                                                TowerElement.Aether, TowerElement.Physical })
                radioGroup.Add(BuildElementRadioRow(el));
            _list.Add(radioGroup);

            var variant = VariantFor(_selectedElement);
            _list.Add(BuildCostBlock(variant));
            _list.Add(BuildTimingLabel("Build time: " + FormatTime(variant.BuildTimeSec)));

            bool canBuild = CanAfford(variant);
            var btn = new Button(() => OnConfirmBuild(variant)) { text = "Build" };
            btn.AddToClassList(ConfirmBtnClass);
            btn.SetEnabled(canBuild);
            _list.Add(btn);
        }

        /// <summary>A toggle-style radio row that selects an element on click.</summary>
        private VisualElement BuildElementRadioRow(TowerElement el)
        {
            bool selected = el == _selectedElement;
            var row = new Button(() => { _selectedElement = el; Render(); });
            row.AddToClassList(RadioRowClass);
            row.EnableInClassList(RadioRowSelectedClass, selected);
            row.text = (selected ? "◉  " : "○  ") + el;
            return row;
        }

        /// <summary>Crystal row + one row per material, each with a ✓ (afford) / ✗ (short).</summary>
        private VisualElement BuildCostBlock(TowerVariantDef v)
        {
            var block = new VisualElement();
            block.Add(BuildCostRow("◆ Crystals", v.CrystalCost, CrystalBalance));
            if (v.Wood > 0)  block.Add(BuildCostRow("Wood",  v.Wood,  GetMaterialCount("wood")));
            if (v.Stone > 0) block.Add(BuildCostRow("Stone", v.Stone, GetMaterialCount("stone")));
            return block;
        }

        private VisualElement BuildCostRow(string label, int required, int have)
        {
            var row = new VisualElement();
            row.AddToClassList(CostRowClass);

            var name = new Label($"{label}: {required}");
            name.AddToClassList(CardCostClass);
            row.Add(name);

            bool ok = have >= required;
            var mark = new Label(ok ? $"✓ {have}" : $"✗ {have}");
            mark.AddToClassList(ok ? CostCheckClass : CostFailClass);
            row.Add(mark);
            return row;
        }

        private Label BuildTimingLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList(CardHpClass);
            return l;
        }

        /// <summary>
        /// Arms the canonical arcane-tower def for tap-to-place (the placement
        /// pipeline is shared) and returns to Root with a placement hint. The
        /// chosen element is remembered for when the variant system goes live.
        /// </summary>
        private void OnConfirmBuild(TowerVariantDef v)
        {
            if (!CanAfford(v))
            {
                SetStatus("Not enough crystals or materials for the " + v.DisplayName + ".", isError: true);
                return;
            }

            var data = Resources.Load<DeNelle.Core.Data.TowerData>("Towers/DevTower");
            if (data == null)
            {
                SetStatus("Tower definition asset missing (Towers/DevTower).", isError: true);
                return;
            }

            // WO-131 — SINGLE AUTHORITATIVE CRYSTAL SPEND for tower placement.
            // Deduct the DISPLAYED cost (v.CrystalCost) from the SAME store the menu
            // and the village HUD read: GameState.Resources.Crystals (via
            // GameStateService.AddCrystals, which clamps >= 0, persists, and raises
            // ResourcesChanged). This is the one and only place a placement charges
            // crystals — TowerPlacementSystem no longer touches the economy, so a
            // placement can never double-charge or charge a divergent (Wood / Aether)
            // pool. CanAfford(v) above re-checked the live balance one statement ago.
            var gss = GameStateService.Instance;
            if (gss == null)
            {
                SetStatus("Game state unavailable — cannot charge crystals.", isError: true);
                return;
            }
            gss.AddCrystals(-v.CrystalCost);   // negative = spend; persisted + HUD-synced

            if (TowerPlacementSystem.Instance == null)
                new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();
            // Pass the already-paid cost so TowerPlacementSystem does NOT charge again.
            TowerPlacementSystem.Instance.StartPlacing(data, prepaid: true);
            Close();   // hide the menu so the world click lands the placement
            SetStatus($"Click a clear tile to raise the {v.DisplayName}.");
        }

        // ── Upgrade Tower screen ──────────────────────────────────────────────

        /// <summary>
        /// WO-127: lists every LIVE placed <see cref="Tower"/> (not Building), so the
        /// row level matches the tower's actual <c>CurrentLevel</c>. Selecting one
        /// shows its upgrade info; the Upgrade button performs a real
        /// <see cref="Tower.Upgrade"/> and is hidden at <see cref="Tower.MaxLevel"/>.
        /// </summary>
        private void RenderUpgradeTower()
        {
            _list.Add(BuildBackButton());

            var heading = new Label("UPGRADE TOWER");
            heading.AddToClassList(CardNameClass);
            _list.Add(heading);

            // WO-127 root cause: enumerate LIVE Tower components (the type whose
            // _currentLevel actually upgrades), not the separate Building type whose
            // serialized Level never mutates.
            var towers = UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);

            // Drop a selection that no longer exists in the scene.
            if (_selectedTowerForUpgrade == null ||
                System.Array.IndexOf(towers, _selectedTowerForUpgrade) < 0)
                _selectedTowerForUpgrade = null;

            var list = new VisualElement();
            bool any = false;
            foreach (var t in towers)
            {
                if (t == null) continue;
                list.Add(BuildTowerSelectRow(t));
                any = true;
            }
            if (!any)
            {
                var none = new Label("No towers placed yet.");
                none.AddToClassList(CardDescClass);
                list.Add(none);
            }
            _list.Add(list);

            if (_selectedTowerForUpgrade != null)
                _list.Add(BuildUpgradeInfoBlock(_selectedTowerForUpgrade));
        }

        private VisualElement BuildTowerSelectRow(Tower t)
        {
            bool selected = ReferenceEquals(t, _selectedTowerForUpgrade);
            var row = new Button(() => { _selectedTowerForUpgrade = t; Render(); });
            row.AddToClassList(TowerRowClass);
            row.EnableInClassList(TowerRowSelectedClass, selected);
            // WO-127: print the LIVE level (1..MaxLevel) via the null-conditional, so
            // it always matches TowerManagerPanel's t.CurrentLevel for the same tower.
            int level = t != null ? t.CurrentLevel : 1;
            string label = t != null ? t.name.Replace("Tower-", "").Replace("Tower_", "") : "Tower";
            row.text = (selected ? "◉  " : "○  ") + label
                       + "  (Lvl " + level + "/" + Tower.MaxLevel + ")";
            return row;
        }

        /// <summary>
        /// WO-127: upgrade info + a REAL upgrade action for the selected live tower.
        /// Reads <c>t.CurrentLevel</c> for the result line; the Upgrade button calls
        /// <see cref="Tower.Upgrade"/> then re-renders, and is hidden once the tower
        /// is at <see cref="Tower.MaxLevel"/>.
        /// </summary>
        private VisualElement BuildUpgradeInfoBlock(Tower t)
        {
            var block = new VisualElement();

            int level = t != null ? t.CurrentLevel : 1;
            bool atMax = level >= Tower.MaxLevel;

            // Cost block kept as the element-variant stub (economy gating is optional
            // per the WO — the minimum viable fix is correct display + a real upgrade).
            var v = VariantFor(_selectedElement);
            block.Add(BuildCostRow("◆ Crystals", v.UpgradeCrystalCost, CrystalBalance));
            if (v.UpgradeStone > 0) block.Add(BuildCostRow("Stone", v.UpgradeStone, GetMaterialCount("stone")));
            block.Add(BuildTimingLabel("Upgrade time: " + FormatTime(v.UpgradeTimeSec)));

            if (atMax)
            {
                var maxed = new Label($"Lvl {level}/{Tower.MaxLevel} — fully upgraded.");
                maxed.AddToClassList(CardDescClass);
                maxed.style.whiteSpace = WhiteSpace.Normal;
                block.Add(maxed);
                return block;   // no Upgrade button at max level (WO-127)
            }

            var result = new Label($"Result: Lvl {level + 1}/{Tower.MaxLevel}  (+{v.Dps} DPS, +{v.Hp / 4} HP)");
            result.AddToClassList(CardDescClass);
            result.style.whiteSpace = WhiteSpace.Normal;
            block.Add(result);

            var upgrade = new Button(() =>
            {
                // DEPRECATED (owner 2026-06-27, tower-upgrade CONSOLIDATION): this menu
                // screen was one of three duplicate upgrade paths and called the FREE
                // Tower.Upgrade() (cost displayed, NOT enforced — see the cost-block stub
                // comment above). The canonical surface is now the proximity HUD context
                // button (TowerInteractable -> HudBuildingFocus). This button is no longer
                // free — it routes through the single cost-enforced Tower.TryUpgrade so it
                // can never bypass the economy. Prefer the on-approach context button.
                var res = _selectedTowerForUpgrade != null
                    ? _selectedTowerForUpgrade.TryUpgrade()
                    : Tower.UpgradeResult.Uninitialized;
                bool ok = res == Tower.UpgradeResult.Success;
                string msg = res switch
                {
                    Tower.UpgradeResult.Success     => $"Upgraded to Lvl {_selectedTowerForUpgrade?.CurrentLevel}.",
                    Tower.UpgradeResult.Maxed       => "Tower already at max level.",
                    Tower.UpgradeResult.CantAfford  => "Not enough resources to upgrade.",
                    Tower.UpgradeResult.UnknownCost => "Upgrade cost not set for this tower.",
                    Tower.UpgradeResult.NoEconomy   => "Economy unavailable — cannot upgrade.",
                    _                               => "Cannot upgrade this tower.",
                };
                SetStatus(msg, isError: !ok);
                Render();
            })
            { text = "Upgrade" };
            upgrade.AddToClassList(ConfirmBtnClass);
            block.Add(upgrade);
            return block;
        }

        // ── WO-31 helpers ─────────────────────────────────────────────────────

        private static TowerVariantDef VariantFor(TowerElement el)
        {
            foreach (var v in Variants) if (v.Element == el) return v;
            return Variants[0];
        }

        private bool CanAfford(TowerVariantDef v)
        {
            return CrystalBalance >= v.CrystalCost
                   && GetMaterialCount("wood") >= v.Wood
                   && GetMaterialCount("stone") >= v.Stone;
        }

        // STUB — Week 6: material inventory is not tracked yet, so report fixed
        // on-hand counts. Crystals come from the live GameState (CrystalBalance).
        private static int GetMaterialCount(string id)
        {
            switch (id)
            {
                case "wood":  return 20;
                case "stone": return 5;
                default:      return 0;
            }
        }

        /// <summary>Formats seconds as "Xm Ys" (or "Ys" under a minute).</summary>
        private static string FormatTime(int seconds)
        {
            int m = seconds / 60, s = seconds % 60;
            return m > 0 ? $"{m}m {s}s" : $"{s}s";
        }

        private VisualElement BuildRepairCard()
        {
            var card = new VisualElement { name = "build-card-repair-wall" };
            card.AddToClassList(CardClass);

            var nameLabel = new Label("Repair Wall");
            nameLabel.AddToClassList(CardNameClass);
            card.Add(nameLabel);

            var desc = new Label("Restore the nearest damaged wall section to full health.");
            desc.AddToClassList(CardDescClass);
            desc.style.whiteSpace = WhiteSpace.Normal;
            card.Add(desc);

            var btn = new Button(InvokeRepairNearestWall) { text = "Repair" };
            btn.AddToClassList(CardFooterClass);
            card.Add(btn);

            return card;
        }

        private static void InvokeRepairNearestWall()
        {
            try
            {
                System.Type t = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("DeNelle.Village.WallRepairController", false);
                    if (t != null) break;
                }
                if (t == null) { Debug.LogWarning("[BuildMenu] WallRepairController not found."); return; }
                var inst = UnityEngine.Object.FindObjectOfType(t) as Component;
                if (inst == null) { Debug.LogWarning("[BuildMenu] WallRepairController not in scene."); return; }
                var m = t.GetMethod("RepairNearestDamagedWall")
                        ?? t.GetMethod("ConfirmRepair")
                        ?? t.GetMethod("StartRepair");
                if (m == null) { Debug.LogWarning("[BuildMenu] WallRepairController.Repair* not found."); return; }
                m.Invoke(inst, m.GetParameters().Length == 0 ? null : new object[] { });
                Debug.Log("[BuildMenu] Repair Wall click → " + m.Name + "().");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[BuildMenu] Repair invoke failed: " + ex.Message);
            }
        }

        private VisualElement BuildCard(BuildingDef def)
        {
            var balance = CrystalBalance;
            var affordable = balance >= def.CrystalCost;

            var card = new VisualElement { name = $"build-card-{def.Id}" };
            card.AddToClassList(CardClass);
            if (!affordable) card.AddToClassList(CardUnaffordableClass);
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

            var data = Resources.Load<DeNelle.Core.Data.TowerData>("Towers/DevTower");
            if (data == null)
            {
                SetStatus("Tower definition asset missing (Towers/DevTower).", isError: true);
                return;
            }

            // WO-131 — charge the DISPLAYED cost from the single crystal store
            // (GameState.Resources.Crystals) exactly once, then hand a PREPAID
            // placement to TowerPlacementSystem (refunded on cancel, never re-charged).
            var gss = GameStateService.Instance;
            if (gss == null)
            {
                SetStatus("Game state unavailable — cannot charge crystals.", isError: true);
                return;
            }
            gss.AddCrystals(-def.CrystalCost);   // negative = spend; persisted + HUD-synced

            if (TowerPlacementSystem.Instance == null)
                new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();
            TowerPlacementSystem.Instance.StartPlacing(data, prepaid: true);
            Close();   // hide the menu so the world click lands the placement
            SetStatus($"Click a clear tile to raise the {VillageStrings.BuildingName(def)}.");
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

        private void Disarm()
        {
            SetStatus("Pick a building, then tap a clear tile to raise it.");
        }


        // =====================================================================
        //  Crystal balance — GameState-backed or local (standalone testing)
        // =====================================================================

        /// <summary>The current crystal balance the menu spends from.</summary>
        public int CrystalBalance
        {
            get
            {
                // Prefer the live GameState whenever it exists — it always does now
                // that GameStateService self-bootstraps — so the menu shares ONE
                // crystal store with the HUD and the dev grants. Keying off the
                // _useGameState serialized flag was the trap: if it was left off on
                // the scene instance, dev "+Crystals" updated GameState but the menu
                // still read its local int (owner: "balance doesn't reflect the grant").
                var service = GameStateService.Instance;
                if (service != null && service.State != null)
                    return service.State.Resources.Crystals;
                return _localCrystalBalance;   // true standalone test only (no service)
            }
        }

    }
}
