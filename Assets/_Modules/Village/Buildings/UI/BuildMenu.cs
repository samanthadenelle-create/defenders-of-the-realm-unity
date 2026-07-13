// =============================================================================
// BuildMenu — the village build-menu UI (Week 4 → WO-D kit conversion).
// -----------------------------------------------------------------------------
// WO-D conversion (2026-07-03, coverage matrix row #35): UIDocument/UITK panel
// (+ its code-built ElarionUi fallback) -> ONE code-built uGUI surface on the
// Obsidian master frame (BuildObsidianModal: FrameCore, "hammer" medallion).
// The old UXML path never rendered in player builds (uxml-uidocuments-dont-
// render-in-builds), so this retires BOTH the dead .uxml binding and the
// interim ShowCodeFallbackMenu — the kit modal is now the single menu.
//
// Screens (WO-31 flow preserved):
//   Root         — Build Tower / Upgrade Tower / Repair Wall / Manage Towers /
//                  Build Mode (Obsidian button rows; Close = the chrome's ONE
//                  shared Close per obsidian-panel-chrome canon)
//   BuildTower   — element radio + costs + timing + Build (WO-131 prepaid spend)
//   UpgradeTower — placed-tower list + upgrade info (cost-enforced TryUpgrade)
//
// Behavioural contracts preserved:
//   • Open()/Close()/Toggle()/IsOpen — BuildMenuHudBridge (HUD Build button) +
//     OnboardingIntegrator call Open().
//   • BuildingPlaced event — still relayed from TowerPlacementSystem.OnTowerPlaced
//     for menu-initiated placements (WO-T1); OnboardingIntegrator +
//     TutorialSignalAdapters subscribe.
//   • WO-131 single authoritative crystal spend (OnConfirmBuild, prepaid: true).
//   • New: routes through the PanelManager modal arbiter ("Build") like every
//     other kit modal — battle-lock may reject an open (revert + stay hidden).
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The village build-menu controller — a code-built Obsidian kit modal.
    /// Root chooser → Build-Tower (element + costs) / Upgrade-Tower (live list),
    /// plus Repair Wall / Manage Towers / Build Mode verbs. Deducts crystals on
    /// a confirmed build (WO-131 prepaid) and relays <see cref="BuildingPlaced"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildMenu : MonoBehaviour
    {
        [Header("Crystal balance")]
        [Tooltip("When true, the menu spends from GameState.Resources.Crystals via GameStateService. " +
                 "When false it spends from _localCrystalBalance below (standalone testing).")]
        [SerializeField] private bool _useGameState = true;

        [Tooltip("Crystal balance used when no GameStateService is alive (standalone testing only).")]
        [SerializeField, Min(0)] private int _localCrystalBalance = 500;

        // ── Kit modal (lazy-built on first Open) ─────────────────────────────
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _bodyHost;          // frame body drop-zone — screens rebuild here
        private TextMeshProUGUI _statusLabel; // footer strip — status / placement hints

        private bool _isOpen;

        // Modal arbiter handle (DEF-212): opening closes any other open panel;
        // battle-lock may reject — revert and stay hidden.
        private PanelHandle _panelHandle;

        // ── WO-31: multi-screen build/upgrade flow ───────────────────────────
        private enum MenuScreen { Root, BuildTower, UpgradeTower }
        private MenuScreen _screen = MenuScreen.Root;

        // Local element enum — deliberately NOT DeNelle.BattleATB's ElementType:
        // DeNelle.Village must not take a dependency on BattleATB (WO-31 §Files).
        private enum TowerElement { Flame, Ice, Aether, Physical }
        private TowerElement _selectedElement = TowerElement.Flame;
        // WO-127: the manage/upgrade screen tracks a LIVE Tower (not a Building) so
        // its level reads correctly and the Upgrade button calls Tower.TryUpgrade().
        private Tower _selectedTowerForUpgrade;

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

        /// <summary>Raised when a building is successfully placed — carries the new Building + its def.
        /// (Relayed from TowerPlacementSystem's commit for menu-initiated placements; args may be null —
        /// both live subscribers treat them as optional. See WO-T1 note at the relay.)</summary>
        public event Action<Building, BuildingDef> BuildingPlaced;

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => _isOpen;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Build", Close, () => IsOpen);
        }

        private void OnDisable()
        {
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
            UnhookPlacementRelay();   // WO-T1 — never leave a stale relay across a teardown
        }

        private void OnDestroy()
        {
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        /// <summary>Re-render the open menu when crystals change (dev grant, wave reward).</summary>
        private void OnResourcesChanged()
        {
            if (_isOpen) Render();
        }

        // =====================================================================
        //  Open / Close
        // =====================================================================

        /// <summary>
        /// Opens the build menu — lazily builds the kit modal, then shows the Root
        /// chooser. Called by the HUD's "Build" button (BuildMenuHudBridge) and the
        /// onboarding flow.
        /// </summary>
        public void Open()
        {
            EnsureBuilt();
            if (_modal == null || _modal.canvas == null) return;
            _isOpen = true;
            _modal.canvas.SetActive(true);
            // Arbiter (DEF-212): closes any other open panel; battle-lock may reject —
            // revert and stay hidden, never force-show (VillageCraftingPanel pattern).
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                _isOpen = false;
                _modal.canvas.SetActive(false);
                return;
            }

            _screen = MenuScreen.Root;            // always open on the chooser
            _selectedTowerForUpgrade = null;
            Disarm();
            Render();
            FlowTrace.Step("UI", "Build menu shown (kit modal, FrameCore)");

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
        /// Closes the build menu. Wired to the chrome's ONE shared Close (and the
        /// modal arbiter's close callback).
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            if (_modal != null && _modal.canvas != null) _modal.canvas.SetActive(false);
            PanelManager.NotifyClosed(_panelHandle);
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
        }

        /// <summary>Toggles the menu open/closed.</summary>
        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        // =====================================================================
        //  Kit modal construction (lazy — first Open builds)
        // =====================================================================

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            _modal = ElarionUiKit.BuildObsidianModal("BuildMenuUI", "Build",
                new Vector2(0.20f, 0.10f), new Vector2(0.80f, 0.90f), Close,
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "hammer");

            var layout = _modal.chrome.layout;
            _bodyHost = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // Footer strip carries the status / placement-hint line.
            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer
                : _modal.chrome.content.transform;
            _statusLabel = MakeText(footHost, "", 13, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.Center, new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));

            _modal.canvas.SetActive(false);   // built hidden; Open shows it
        }

        // =====================================================================
        //  Rendering — rebuilds the body drop-zone for the current screen
        // =====================================================================

        /// <summary>
        /// Rebuilds the body for the current <see cref="_screen"/> (WO-31 three-screen
        /// flow). All screens rebuild into the frame's body drop-zone; the crystal
        /// balance readout tops every screen.
        /// </summary>
        public void Render()
        {
            if (_bodyHost == null) return;
            for (int i = _bodyHost.childCount - 1; i >= 0; i--)
                Destroy(_bodyHost.GetChild(i).gameObject);

            // WO-697: balance through the ONE kit formatter (compact >= 10k).
            MakeText(_bodyHost, "Crystals: " + ElarionUi.CompactNumber(CrystalBalance), 14, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Right, new Vector2(0.06f, 0.955f), new Vector2(0.94f, 1f));

            switch (_screen)
            {
                case MenuScreen.BuildTower:   RenderBuildTower();   break;
                case MenuScreen.UpgradeTower: RenderUpgradeTower(); break;
                default:                      RenderRoot();         break;
            }
        }

        // ── Root chooser ─────────────────────────────────────────────────────

        /// <summary>
        /// The top-level chooser: Obsidian button rows — Build Tower / Upgrade
        /// Tower / Repair Wall / Manage Towers / Build Mode. (No Close row — the
        /// chrome's ONE shared Close covers it.)
        /// </summary>
        private void RenderRoot()
        {
            float top = 0.93f;
            AddRow("Build Tower", ElarionUiKit.ObsidianButtonColor.Yellow,
                () => { _screen = MenuScreen.BuildTower; Render(); }, ref top);
            AddRow("Upgrade Tower", ElarionUiKit.ObsidianButtonColor.Gray,
                () => { _screen = MenuScreen.UpgradeTower; _selectedTowerForUpgrade = null; Render(); }, ref top);
            AddRow("Repair Wall", ElarionUiKit.ObsidianButtonColor.Gray, OnRepairWall, ref top);
            AddRow("Manage Towers", ElarionUiKit.ObsidianButtonColor.Gray, () =>
            {
                Close();
                DeNelle.Village.UI.TowerManagerPanel.Instance?.Show();
            }, ref top);
            // WO-108 — the CREATE verb: enter the player Build Mode (catalog palette +
            // grid placement + persisted BaseLayout). EnsureExists() self-installs the
            // controller; Enter() freezes waves + shows the palette.
            AddRow("Build Mode", ElarionUiKit.ObsidianButtonColor.Gray, () =>
            {
                Close();
                BuildModeController.EnsureExists().Enter();
            }, ref top);
        }

        /// <summary>One full-width Obsidian row, stacked downward from <paramref name="top"/>.</summary>
        private void AddRow(string label, ElarionUiKit.ObsidianButtonColor color,
            Action onClick, ref float top)
        {
            const float rowH = 0.115f, gap = 0.022f;
            ElarionUiKit.BuildObsidianButton(_bodyHost, label,
                ElarionUiKit.ObsidianButtonStyle.Style1, color,
                new Vector2(0.08f, top - rowH), new Vector2(0.92f, top), onClick);
            top -= rowH + gap;
        }

        private void OnRepairWall()
        {
            InvokeRepairNearestWall();
            SetStatus("Restoring the nearest damaged wall section.");
        }

        /// <summary>The "Back" row shown on every sub-screen; returns to Root.</summary>
        private float AddBackButton()
        {
            ElarionUiKit.BuildObsidianButton(_bodyHost, "< Back",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.08f, 0.855f), new Vector2(0.38f, 0.935f),
                () => { _screen = MenuScreen.Root; _selectedTowerForUpgrade = null; Disarm(); Render(); });
            return 0.83f;   // content starts below the back row
        }

        // ── Build Tower screen ────────────────────────────────────────────────

        /// <summary>
        /// Element radio (Flame / Ice / Aether / Physical) + the selected variant's
        /// crystal + material costs (ASCII +/- marks — glyphs are missing from the
        /// TMP font) + build time + a Build button disabled when unaffordable.
        /// </summary>
        private void RenderBuildTower()
        {
            float top = AddBackButton();

            MakeText(_bodyHost, "BUILD TOWER", 16, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.08f, top - 0.05f), new Vector2(0.92f, top));
            top -= 0.06f;

            foreach (TowerElement el in new[] { TowerElement.Flame, TowerElement.Ice,
                                                TowerElement.Aether, TowerElement.Physical })
            {
                const float rowH = 0.075f, gap = 0.012f;
                bool selected = el == _selectedElement;
                TowerElement captured = el;
                ElarionUiKit.BuildObsidianButton(_bodyHost,
                    (selected ? "> " : "") + el,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.10f, top - rowH), new Vector2(0.90f, top),
                    () => { _selectedElement = captured; Render(); });
                top -= rowH + gap;
            }
            top -= 0.02f;

            var variant = VariantFor(_selectedElement);
            top = AddCostRow("Crystals", variant.CrystalCost, CrystalBalance, top);
            if (variant.Wood > 0)  top = AddCostRow("Wood",  variant.Wood,  GetMaterialCount("wood"),  top);
            if (variant.Stone > 0) top = AddCostRow("Stone", variant.Stone, GetMaterialCount("stone"), top);

            MakeText(_bodyHost, "Build time: " + FormatTime(variant.BuildTimeSec), 13,
                ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(0.10f, top - 0.045f), new Vector2(0.90f, top));

            bool canBuild = CanAfford(variant);
            var btn = ElarionUiKit.BuildObsidianButton(_bodyHost, "Build",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canBuild ? ElarionUiKit.ObsidianButtonColor.Green
                         : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.28f, 0.02f), new Vector2(0.72f, 0.12f),
                () => OnConfirmBuild(variant));
            btn.interactable = canBuild;
        }

        /// <summary>Cost line: "Label: required" + a "+/- have" mark (ASCII — no glyphs in TMP).</summary>
        private float AddCostRow(string label, int required, int have, float top)
        {
            const float rowH = 0.045f;
            bool ok = have >= required;
            // WO-697: cost/have numbers through the ONE kit formatter (compact >= 10k).
            MakeText(_bodyHost, label + ": " + ElarionUi.CompactNumber(required), 13, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.10f, top - rowH), new Vector2(0.62f, top));
            MakeText(_bodyHost, (ok ? "+ " : "- ") + ElarionUi.CompactNumber(have), 13,
                ok ? ElarionUi.Affordable : ElarionUi.Danger, FontStyles.Bold,
                TextAlignmentOptions.Right, new Vector2(0.62f, top - rowH), new Vector2(0.90f, top));
            return top - rowH - 0.008f;
        }

        /// <summary>
        /// Arms the canonical arcane-tower def for tap-to-place (the placement
        /// pipeline is shared). The chosen element is remembered for when the
        /// variant system goes live.
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
            // Deduct the DISPLAYED cost (v.CrystalCost) through BuildModeController.ChargeLedger,
            // which routes through EconomyService for multi-resource costs or GameState.Crystals fallback.
            // This ensures a placement charges through the canonical ledger, never double-charging.
            // CanAfford(v) above re-checked the live balance one statement ago.
            var cost = new DeNelle.Core.Catalog.ResourceCost
            {
                crystals = v.CrystalCost,
                wood = v.Wood,
                iron = v.Stone,   // Note: UI calls it "Stone", catalog uses "Iron" for the third resource
                food = 0
            };
            BuildModeController.ChargeLedger(cost);

            if (TowerPlacementSystem.Instance == null)
                new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();
            // Pass the already-paid cost so TowerPlacementSystem does NOT charge again.
            // WO-T1 FIX — relay TowerPlacementSystem's commit (PlaceTower ->
            // OnTowerPlaced) so this menu's own BuildingPlaced event fires for
            // placements THIS menu initiated (subscribed by OnboardingIntegrator +
            // TutorialSignalAdapters).
            HookPlacementRelay(TowerPlacementSystem.Instance);
            TowerPlacementSystem.Instance.StartPlacing(data, prepaid: true);
            Close();   // hide the menu so the world click lands the placement
            SetStatus($"Click a clear tile to raise the {v.DisplayName}.");
        }

        // ── WO-T1: BuildingPlaced relay (menu-initiated placements) ───────────
        // One-shot: armed per OnConfirmBuild, fires on the next committed placement,
        // then unhooks (TowerPlacementSystem cancels arming after each placement too).
        private TowerPlacementSystem _relayHooked;

        private void HookPlacementRelay(TowerPlacementSystem tps)
        {
            if (tps == null) return;
            UnhookPlacementRelay();
            _relayHooked = tps;
            tps.OnTowerPlaced += OnMenuInitiatedTowerPlaced;
        }

        private void UnhookPlacementRelay()
        {
            if (_relayHooked != null)
            {
                _relayHooked.OnTowerPlaced -= OnMenuInitiatedTowerPlaced;
                _relayHooked = null;
            }
        }

        private void OnMenuInitiatedTowerPlaced(DeNelle.Core.Data.TowerData _)
        {
            UnhookPlacementRelay();
            // The tower body is raised by TowerConstructionQueue over buildTime (DEF-76),
            // so no live Building/def exists at commit — both live subscribers
            // (OnboardingIntegrator.OnBuildingPlaced ignores its args; tutorial adapters
            // only need the fact) treat the args as optional.
            BuildingPlaced?.Invoke(null, null);
        }

        // ── Upgrade Tower screen ──────────────────────────────────────────────

        /// <summary>
        /// WO-127: lists every LIVE placed <see cref="Tower"/> (not Building), so the
        /// row level matches the tower's actual <c>CurrentLevel</c>. Selecting one
        /// shows its upgrade info; the Upgrade button routes through the single
        /// cost-enforced <see cref="Tower.TryUpgrade"/>.
        /// </summary>
        private void RenderUpgradeTower()
        {
            float top = AddBackButton();

            MakeText(_bodyHost, "UPGRADE TOWER", 16, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.08f, top - 0.05f), new Vector2(0.92f, top));
            top -= 0.06f;

            // WO-127 root cause: enumerate LIVE Tower components (the type whose
            // _currentLevel actually upgrades), not the separate Building type whose
            // serialized Level never mutates.
            var towers = UnityEngine.Object.FindObjectsByType<Tower>();

            // Drop a selection that no longer exists in the scene.
            if (_selectedTowerForUpgrade == null ||
                System.Array.IndexOf(towers, _selectedTowerForUpgrade) < 0)
                _selectedTowerForUpgrade = null;

            bool any = false;
            foreach (var t in towers)
            {
                if (t == null) continue;
                const float rowH = 0.07f, gap = 0.012f;
                bool selected = ReferenceEquals(t, _selectedTowerForUpgrade);
                // WO-127: print the LIVE level (1..MaxLevel) so it always matches
                // TowerManagerPanel's t.CurrentLevel for the same tower.
                string label = (selected ? "> " : "")
                             + t.name.Replace("Tower-", "").Replace("Tower_", "")
                             + "  (Lvl " + t.CurrentLevel + "/" + Tower.MaxLevel + ")";
                Tower captured = t;
                ElarionUiKit.BuildObsidianButton(_bodyHost, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.10f, top - rowH), new Vector2(0.90f, top),
                    () => { _selectedTowerForUpgrade = captured; Render(); });
                top -= rowH + gap;
                any = true;
                if (top < 0.40f) break;   // bounded: leave room for the info block
            }
            if (!any)
            {
                MakeText(_bodyHost, "No towers placed yet.", 14, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.10f, top - 0.06f), new Vector2(0.90f, top));
                top -= 0.07f;
            }

            if (_selectedTowerForUpgrade != null)
                BuildUpgradeInfoBlock(_selectedTowerForUpgrade, top - 0.02f);
        }

        /// <summary>
        /// WO-127: upgrade info + a REAL upgrade action for the selected live tower.
        /// The Upgrade button calls the single cost-enforced <see cref="Tower.TryUpgrade"/>
        /// (never the free Upgrade — tower-upgrade consolidation, owner 2026-06-27; the
        /// canonical surface remains the proximity HUD context button) and is hidden at
        /// <see cref="Tower.MaxLevel"/>.
        /// </summary>
        private void BuildUpgradeInfoBlock(Tower t, float top)
        {
            int level = t != null ? t.CurrentLevel : 1;
            bool atMax = level >= Tower.MaxLevel;

            // Cost block kept as the element-variant stub (economy gating happens in
            // TryUpgrade — the display is informative).
            var v = VariantFor(_selectedElement);
            top = AddCostRow("Crystals", v.UpgradeCrystalCost, CrystalBalance, top);
            if (v.UpgradeStone > 0) top = AddCostRow("Stone", v.UpgradeStone, GetMaterialCount("stone"), top);
            MakeText(_bodyHost, "Upgrade time: " + FormatTime(v.UpgradeTimeSec), 13,
                ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(0.10f, top - 0.045f), new Vector2(0.90f, top));
            top -= 0.055f;

            if (atMax)
            {
                MakeText(_bodyHost, $"Lvl {level}/{Tower.MaxLevel} - fully upgraded.", 13,
                    ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                    new Vector2(0.10f, top - 0.05f), new Vector2(0.90f, top));
                return;   // no Upgrade button at max level (WO-127)
            }

            MakeText(_bodyHost, $"Result: Lvl {level + 1}/{Tower.MaxLevel}  (+{v.Dps} DPS, +{v.Hp / 4} HP)",
                13, ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.Left,
                new Vector2(0.10f, top - 0.05f), new Vector2(0.90f, top));

            ElarionUiKit.BuildObsidianButton(_bodyHost, "Upgrade",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.28f, 0.02f), new Vector2(0.72f, 0.12f), () =>
                {
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
                });
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
                var inst = UnityEngine.Object.FindAnyObjectByType(t) as Component;
                if (inst == null) { Debug.LogWarning("[BuildMenu] WallRepairController not in scene."); return; }
                var m = t.GetMethod("RepairNearestDamagedWall")
                        ?? t.GetMethod("ConfirmRepair")
                        ?? t.GetMethod("StartRepair");
                if (m == null) { Debug.LogWarning("[BuildMenu] WallRepairController.Repair* not found."); return; }
                m.Invoke(inst, m.GetParameters().Length == 0 ? null : new object[] { });
                Debug.Log("[BuildMenu] Repair Wall click -> " + m.Name + "().");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[BuildMenu] Repair invoke failed: " + ex.Message);
            }
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = message;
            _statusLabel.color = isError ? ElarionUi.Danger : ElarionUi.ParchmentDim;
        }

        private void Disarm()
        {
            SetStatus("Pick an action, then tap a clear tile to raise a tower.");
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

        // ── uGUI helper (LeaderboardPanel/VillageCraftingPanel shape) ─────────

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
