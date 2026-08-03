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
//   BuildTower   — catalog-tower radio + REAL costs vs the REAL ledger + Build
//                  (WO-131 prepaid spend, WO-861 verified: the spend must SUCCEED)
//   UpgradeTower — placed-tower list + upgrade info (cost-enforced TryUpgrade)
//
// Behavioural contracts preserved:
//   • Open()/Close()/Toggle()/IsOpen — BuildMenuHudBridge (HUD Build button) +
//     OnboardingIntegrator call Open().
//   • BuildingPlaced event — still relayed from TowerPlacementSystem.OnTowerPlaced
//     for menu-initiated placements (WO-T1); OnboardingIntegrator +
//     TutorialSignalAdapters subscribe.
//   • WO-131 single authoritative spend (OnConfirmBuild, prepaid: true).
//   • New: routes through the PanelManager modal arbiter ("Build") like every
//     other kit modal — battle-lock may reject an open (revert + stay hidden).
//
// WO-861 (owner Tier 0, 2026-08-02) — TWO live economy defects removed from THIS View:
//   1. THE FAKE WALLET. A private GetMaterialCount(id) returned the literals wood=20 /
//      stone=5. Those literals were shown to the player as their balance, were what the
//      Build button's afford gate compared against, and were never deducted — so every
//      tower priced in wood/stone was FREE. A second hard-coded TowerVariantDef table
//      (4 rows x crystal/wood/stone cost + times + upgrade cost + DPS/HP) was a rival
//      cost authority to the catalog. BOTH DELETED. The screen now lists real catalog
//      Tower rows priced by BuildModeController.CostFor against the real IEconomy ledger,
//      all via BuildMenuVM (the View reads no state and calls no service).
//   2. THE UNVERIFIED SPEND. OnConfirmBuild called BuildModeController.ChargeLedger,
//      which DISCARDS IEconomy.TrySpend's bool, then placed with prepaid:true even when
//      the ledger DECLINED — a live free-tower path. The spend is now
//      BuildMenuVM.TrySpendBuild, whose bool decides whether the placement happens.
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
        [Tooltip("Crystal balance used when no economy service is alive (standalone testing only).")]
        [SerializeField, Min(0)] private int _localCrystalBalance = 500;

        // ── Kit modal (lazy-built on first Open) ─────────────────────────────
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _bodyHost;          // frame body drop-zone — screens rebuild here
        private TextMeshProUGUI _statusLabel; // footer strip — status / placement hints

        private bool _isOpen;

        // Modal arbiter handle (DEF-212): opening closes any other open panel;
        // battle-lock may reject — revert and stay hidden.
        private PanelHandle _panelHandle;

        // MVVM Silo C — the paired VM owns the crystal balance, the placed-tower scan, and
        // the Repair-Wall command (replacing the removed reflection seam). Created fresh on
        // Open, disposed on Close. This View reads only VM data (no state/scene services).
        private BuildMenuVM _vm;

        // ── WO-31: multi-screen build/upgrade flow ───────────────────────────
        private enum MenuScreen { Root, BuildTower, UpgradeTower }
        private MenuScreen _screen = MenuScreen.Root;

        // WO-861: the Build-Tower radio now selects a REAL catalog tower id. The old local
        // TowerElement enum + the hard-coded TowerVariantDef balance table (a SECOND cost
        // authority divergent from the catalog) are DELETED — every number on this screen
        // comes from BuildMenuVM.TowerOptions (CatalogRegistry -> BuildModeController.CostFor).
        private string _selectedTowerId;
        // WO-127: the manage/upgrade screen tracks a LIVE Tower (not a Building) so
        // its level reads correctly and the Upgrade button calls Tower.TryUpgrade().
        private Tower _selectedTowerForUpgrade;

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
            DisposeVm();              // never leave a stale wallet subscription across a teardown
            UnhookPlacementRelay();   // WO-T1 — never leave a stale relay across a teardown
        }

        private void OnDestroy()
        {
            DisposeVm();
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        /// <summary>Re-render the open menu when the VM changes (crystals: dev grant, wave reward).</summary>
        private void OnResourcesChanged()
        {
            if (_isOpen) Render();
        }

        private void DisposeVm()
        {
            if (_vm != null) { _vm.Changed -= OnResourcesChanged; _vm.Dispose(); _vm = null; }
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

            // Build the VM (resolves economy + tower list + wall-repair controller) and
            // bind its Changed so the open menu live-refreshes when crystals change (a dev
            // grant, a wave reward). Remove-then-add guards against a double subscription.
            DisposeVm();
            _vm = BuildMenuVM.CreateDefault(Close, _localCrystalBalance);
            _vm.Changed += OnResourcesChanged;

            _screen = MenuScreen.Root;            // always open on the chooser
            _selectedTowerId = null;              // re-defaults to the cheapest catalog row
            _selectedTowerForUpgrade = null;
            _upgradeListScrollPos = 1f;           // WO-795: fresh open starts at the list top
            Disarm();
            Render();
            FlowTrace.Step("UI", "Build menu shown (kit modal, FrameCore)");
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
            DisposeVm();
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
                () => { _screen = MenuScreen.UpgradeTower; _selectedTowerForUpgrade = null; _upgradeListScrollPos = 1f; Render(); }, ref top);
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
            _vm?.RepairNearestWall();   // typed WallRepairController command (reflection seam removed)
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
        /// Catalog-tower radio + the selected row's REAL catalog cost (ASCII +/- marks —
        /// glyphs are missing from the TMP font) against the REAL on-hand ledger + the
        /// real raise time + a Build button disabled when the live wallet can't cover it.
        /// WO-861: every number here is VM data; the View authors none of them.
        /// </summary>
        private void RenderBuildTower()
        {
            float top = AddBackButton();

            MakeText(_bodyHost, "BUILD TOWER", 16, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.08f, top - 0.05f), new Vector2(0.92f, top));
            top -= 0.06f;

            var options = _vm != null ? _vm.TowerOptions : null;
            if (options == null || options.Count == 0)
            {
                // No fabricated fallback row: an unbootstrapped catalog is reported, not invented.
                MakeText(_bodyHost, "No tower rows in the catalog - nothing can be priced.", 14,
                    ElarionUi.Danger, FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.08f, top - 0.08f), new Vector2(0.92f, top));
                return;
            }

            var selection = _vm.TowerOptionFor(_selectedTowerId);
            _selectedTowerId = selection.Id;

            foreach (var opt in options)
            {
                const float rowH = 0.075f, gap = 0.012f;
                bool selected = string.Equals(opt.Id, _selectedTowerId, StringComparison.OrdinalIgnoreCase);
                string captured = opt.Id;
                ElarionUiKit.BuildObsidianButton(_bodyHost,
                    (selected ? "> " : "") + opt.DisplayName,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.10f, top - rowH), new Vector2(0.90f, top),
                    () => { _selectedTowerId = captured; Render(); });
                top -= rowH + gap;
            }
            top -= 0.02f;

            var cost = selection.Cost;
            if (cost.crystals > 0) top = AddCostRow("Crystals", cost.crystals, _vm.MaterialCount("crystals"), top);
            if (cost.wood > 0)     top = AddCostRow("Wood",     cost.wood,     _vm.MaterialCount("wood"),     top);
            if (cost.iron > 0)     top = AddCostRow("Iron",     cost.iron,     _vm.MaterialCount("iron"),     top);
            if (cost.food > 0)     top = AddCostRow("Food",     cost.food,     _vm.MaterialCount("food"),     top);

            int buildSeconds = _vm.BuildSeconds;
            if (buildSeconds > 0)
                MakeText(_bodyHost, "Build time: " + FormatTime(buildSeconds), 13,
                    ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                    new Vector2(0.10f, top - 0.045f), new Vector2(0.90f, top));

            bool canBuild = _vm.CanAfford(cost);
            var btn = ElarionUiKit.BuildObsidianButton(_bodyHost, "Build",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canBuild ? ElarionUiKit.ObsidianButtonColor.Green
                         : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.28f, 0.02f), new Vector2(0.72f, 0.12f),
                () => OnConfirmBuild(selection));
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
        /// Arms the canonical tower def for tap-to-place (the placement pipeline is shared)
        /// AFTER the spend has actually landed.
        ///
        /// WO-861 ORDER OF OPERATIONS (owner AC: "insufficient wood -> placement blocked and
        /// resources unchanged"):
        ///   1. re-check affordability on the LIVE ledger (the button state can be stale);
        ///   2. resolve the TowerData FIRST, so a missing asset can never charge the player;
        ///   3. spend through BuildMenuVM.TrySpendBuild — which HONOURS IEconomy.TrySpend's
        ///      bool. Previously this called BuildModeController.ChargeLedger (which DISCARDS
        ///      that bool) and then placed with prepaid:true regardless, so a DECLINED spend
        ///      still raised a tower. Now a false return returns here: no placement, no charge.
        /// </summary>
        private void OnConfirmBuild(BuildMenuVM.TowerBuildOption option)
        {
            if (_vm == null || option.IsEmpty)
            {
                SetStatus("No tower selected.", isError: true);
                return;
            }
            var cost = option.Cost;
            if (!_vm.CanAfford(cost))
            {
                SetStatus(_vm.ShortfallFor(cost) + " for the " + option.DisplayName + ".", isError: true);
                return;
            }

            var data = _vm.PlacedTowerData;
            if (data == null)
            {
                SetStatus("Tower definition asset missing (Towers/DevTower).", isError: true);
                return;
            }

            // THE spend. False => the ledger declined; nothing was deducted and nothing is placed.
            if (!_vm.TrySpendBuild(cost, out string spendFailure))
            {
                SetStatus(spendFailure ?? "The build could not be charged - placement blocked.", isError: true);
                Render();   // re-price against whatever the wallet actually says now
                return;
            }

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
            SetStatus($"Click a clear tile to raise the {option.DisplayName}.");
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

        // WO-795: fixed pixel row height for the tower-list scroll well (matches the
        // RumorBoardPanel / JewelerPanelMvvm pattern rows).
        private const float UpgradeRowPixelH = 96f;
        // Lower edge of the list band; the upgrade info block renders BELOW this,
        // OUTSIDE the scroll well (the old truncation boundary, now a hard band).
        private const float UpgradeInfoTop = 0.40f;
        // Scroll position persisted across Render() rebuilds — selecting a row
        // re-renders the whole body, which would otherwise snap the list to the top.
        private float _upgradeListScrollPos = 1f;   // 1 = top (uGUI normalized)

        /// <summary>
        /// WO-127: lists every LIVE placed <see cref="Tower"/> (not Building), so the
        /// row level matches the tower's actual <c>CurrentLevel</c>. Selecting one
        /// shows its upgrade info; the Upgrade button routes through the single
        /// cost-enforced <see cref="Tower.TryUpgrade"/>. WO-795: the list lives in a
        /// ScrollRect well — an unbounded tower list scrolls instead of truncating.
        /// </summary>
        private void RenderUpgradeTower()
        {
            float top = AddBackButton();

            MakeText(_bodyHost, "UPGRADE TOWER", 16, ElarionUi.Gilt, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.08f, top - 0.05f), new Vector2(0.92f, top));
            top -= 0.06f;

            // WO-127 root cause: enumerate LIVE Tower components (the type whose
            // _currentLevel actually upgrades), not the separate Building type whose
            // serialized Level never mutates. MVVM Silo C: the placed-tower scan lives in
            // the VM's tower list (the sanctioned resolution site) — not in this View.
            _vm.Towers.Refresh();
            var towers = _vm.Towers.Towers;

            // Drop a selection that no longer exists in the scene.
            bool stillPresent = false;
            foreach (var tw in towers)
                if (ReferenceEquals(tw, _selectedTowerForUpgrade)) { stillPresent = true; break; }
            if (_selectedTowerForUpgrade == null || !stillPresent)
                _selectedTowerForUpgrade = null;

            // ── WO-795 scroll well (RumorBoardPanel Open() / JewelerPanelMvvm
            // BuildRecipeScrollWell pattern): Viewport = near-invisible Image drag
            // catcher + RectMask2D + ScrollRect; Content = top-anchored
            // VerticalLayoutGroup + ContentSizeFitter. The info block stays OUTSIDE
            // the well, anchored below UpgradeInfoTop.
            float restoreScrollPos = _upgradeListScrollPos;

            var viewportGo = new GameObject("TowerListViewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportGo.transform.SetParent(_bodyHost, false);
            var vpr = viewportGo.GetComponent<RectTransform>();
            vpr.anchorMin = new Vector2(0.10f, UpgradeInfoTop + 0.02f);
            vpr.anchorMax = new Vector2(0.90f, top);
            vpr.offsetMin = Vector2.zero;
            vpr.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // drag catcher

            var contentGo = new GameObject("TowerListContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot     = new Vector2(0.5f, 1f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true; vlg.childForceExpandWidth  = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            // Bottom pad = one row so the last tower scrolls fully clear of the mask.
            vlg.padding = new RectOffset(0, 0, 0, (int)UpgradeRowPixelH + 8);
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewportGo.GetComponent<ScrollRect>();
            scroll.viewport = vpr;
            scroll.content  = cr;
            scroll.horizontal = false;
            scroll.vertical   = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            scroll.onValueChanged.AddListener(_ => _upgradeListScrollPos = scroll.verticalNormalizedPosition);

            bool any = false;
            foreach (var t in towers)
            {
                if (t == null) continue;
                bool selected = ReferenceEquals(t, _selectedTowerForUpgrade);
                // WO-127: print the LIVE level (1..MaxLevel) so it always matches
                // TowerManagerPanel's t.CurrentLevel for the same tower.
                string label = DeNelle.Village.UI.PlacedTowerListVM.FormatMenuRow(t.name, t.CurrentLevel, selected);
                Tower captured = t;
                // WO-795: fixed-height LayoutElement host; the obsidian button fills it
                // (row internals unchanged — label / colors / click as before).
                var rowHost = new GameObject("TowerRow", typeof(RectTransform), typeof(LayoutElement));
                rowHost.transform.SetParent(contentGo.transform, false);
                rowHost.GetComponent<LayoutElement>().preferredHeight = UpgradeRowPixelH;
                ElarionUiKit.BuildObsidianButton(rowHost.transform, label,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                             : ElarionUiKit.ObsidianButtonColor.Gray,
                    Vector2.zero, Vector2.one,
                    () => { _selectedTowerForUpgrade = captured; Render(); });
                any = true;
            }
            if (!any)
            {
                var emptyHost = new GameObject("EmptyRow", typeof(RectTransform), typeof(LayoutElement));
                emptyHost.transform.SetParent(contentGo.transform, false);
                emptyHost.GetComponent<LayoutElement>().preferredHeight = 60f;
                MakeText(emptyHost.transform, "No towers placed yet.", 14, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one);
            }

            // Restore the scroll position across a selection re-render (layout must be
            // computed first, or the normalized set is a no-op on a zero-height content).
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = restoreScrollPos;

            if (_selectedTowerForUpgrade != null)
                BuildUpgradeInfoBlock(_selectedTowerForUpgrade, UpgradeInfoTop - 0.02f);
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

            // WO-861: the REAL price Tower.TryUpgrade charges — NextUpgradeCost of wood AND
            // iron AND crystals — against the REAL on-hand ledger. The old block showed the
            // deleted variant table's invented UpgradeCrystalCost / UpgradeStone / time.
            var up = _vm != null ? _vm.UpgradePriceFor(t) : default;
            if (up.IsZero)
            {
                MakeText(_bodyHost, "Upgrade cost not authored for this tower.", 13,
                    ElarionUi.ParchmentDim, FontStyles.Italic, TextAlignmentOptions.Left,
                    new Vector2(0.10f, top - 0.045f), new Vector2(0.90f, top));
                top -= 0.055f;
            }
            else
            {
                if (up.crystals > 0) top = AddCostRow("Crystals", up.crystals, _vm.MaterialCount("crystals"), top);
                if (up.wood > 0)     top = AddCostRow("Wood",     up.wood,     _vm.MaterialCount("wood"),     top);
                if (up.iron > 0)     top = AddCostRow("Iron",     up.iron,     _vm.MaterialCount("iron"),     top);
                top -= 0.010f;
            }

            if (atMax)
            {
                MakeText(_bodyHost, $"Lvl {level}/{Tower.MaxLevel} - fully upgraded.", 13,
                    ElarionUi.ParchmentDim, FontStyles.Normal, TextAlignmentOptions.Left,
                    new Vector2(0.10f, top - 0.05f), new Vector2(0.90f, top));
                return;   // no Upgrade button at max level (WO-127)
            }

            // Real live stats off the tower itself — no invented DPS/HP deltas.
            MakeText(_bodyHost,
                $"Result: Lvl {level + 1}/{Tower.MaxLevel}  (now {t.CurrentDamage:0.#} dmg / {t.CurrentRange:0.#}m)",
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
        //
        // DELETED (WO-861 — do not reintroduce, BuildMenuRealEconomyRegression lints for it):
        //   VariantFor / CanAfford(TowerVariantDef) / GetMaterialCount(string)
        // GetMaterialCount was a literal-returning fake wallet ("wood" -> 20, "stone" -> 5)
        // that the player SAW as their balance and the Build button gated on, and that was
        // never deducted. Balances, affordability and the spend all live in BuildMenuVM now,
        // reading IEconomy — the one GameState-backed ledger.

        /// <summary>Formats seconds as "Xm Ys" (or "Ys" under a minute).</summary>
        private static string FormatTime(int seconds)
        {
            int m = seconds / 60, s = seconds % 60;
            return m > 0 ? $"{m}m {s}s" : $"{s}s";
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

        /// <summary>The current crystal balance the menu spends from. MVVM Silo C: sourced from
        /// the VM (IEconomy.Crystals — the SAME single crystal store the HUD + dev grants share),
        /// falling back to the standalone-test local value when the menu is closed / no service.
        /// This View reads only VM data.</summary>
        public int CrystalBalance => _vm != null ? _vm.Crystals : _localCrystalBalance;

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
